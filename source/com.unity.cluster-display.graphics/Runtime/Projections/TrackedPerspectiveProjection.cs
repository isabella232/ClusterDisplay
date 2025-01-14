using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

namespace Unity.ClusterDisplay.Graphics
{
    [PopupItem("Tracked Perspective")]
    sealed class TrackedPerspectiveProjection : ProjectionPolicy
    {
        // TODO: Create a custom icon for this.
        const string k_SurfaceIconName = "d_BuildSettings.Standalone.Small";

        [SerializeField]
        List<PlanarProjectionSurface> m_ProjectionSurfaces = new();

        readonly Dictionary<int, RenderTexture> m_RenderTargets = new();
        BlitCommand m_BlitCommand;

        public IReadOnlyList<PlanarProjectionSurface> Surfaces => m_ProjectionSurfaces;

        // Property blocks for preview materials
        readonly Dictionary<int, MaterialPropertyBlock> m_PreviewMaterialProperties = new();

        Mesh m_PlaneMesh;

        public bool SetSurface(PlanarProjectionSurface surface, int index = -1)
        {
            if (index == -1)
            {
                m_ProjectionSurfaces.Add(surface);
                return true;
            }

            if (index > -1 && index < m_ProjectionSurfaces.Count)
            {
                m_ProjectionSurfaces[index] = surface;
                return true;
            }

            return false;
        }

        void OnDisable()
        {
            foreach (var rt in m_RenderTargets.Values)
            {
                if (rt != null)
                {
                    rt.Release();
                }
            }
            m_RenderTargets.Clear();
            DestroyImmediate(m_PlaneMesh);
        }

        public override void UpdateCluster(ClusterRendererSettings clusterSettings, Camera activeCamera)
        {
            var nodeIndex = GetEffectiveNodeIndex();

            if (nodeIndex >= m_ProjectionSurfaces.Count)
            {
                return;
            }

            if (IsDebug)
            {
                for (var index = 0; index < m_ProjectionSurfaces.Count; index++)
                {
                    RenderSurface(index, clusterSettings, activeCamera, true);
                }
            }
            else
            {
                RenderSurface(nodeIndex, clusterSettings, activeCamera);
            }

            m_BlitCommand = new BlitCommand(
                m_RenderTargets[nodeIndex],
                new BlitParams(
                        m_ProjectionSurfaces[nodeIndex].ScreenResolution,
                        clusterSettings.OverScanInPixels, Vector2.zero)
                    .ScaleBias,
                GraphicsUtil.k_IdentityScaleBias,
                customBlitMaterial,
                GetCustomBlitMaterialPropertyBlocks(nodeIndex));

            if (IsDebug)
            {
                DrawPreview(clusterSettings);
            }
        }

        public override void Present(PresentArgs args)
        {
            if (m_ProjectionSurfaces.Count == 0 || m_BlitCommand.texture == null)
            {
                return;
            }

            GraphicsUtil.Blit(args.CommandBuffer, m_BlitCommand, args.FlipY);
        }

        void DrawPreview(ClusterRendererSettings clusterSettings)
        {
            if (m_PlaneMesh == null)
            {
                m_PlaneMesh = PlanarProjectionSurface.CreateMesh();
            }

            for (var index = 0; index < m_ProjectionSurfaces.Count; index++)
            {
                if (m_RenderTargets.TryGetValue(index, out var rt))
                {
                    var surface = m_ProjectionSurfaces[index];
                    var localToWorldMatrix = Origin * Matrix4x4.TRS(surface.LocalPosition, surface.LocalRotation, surface.Scale);
                    m_PreviewMaterialProperties.GetOrCreate(index, out var previewMatProp);
                    previewMatProp.SetTexture(GraphicsUtil.ShaderIDs._MainTex, m_RenderTargets[index]);

                    UnityEngine.Graphics.DrawMesh(m_PlaneMesh,
                        localToWorldMatrix,
                        GraphicsUtil.GetPreviewMaterial(),
                        ClusterRenderer.VirtualObjectLayer,
                        camera: null,
                        submeshIndex: 0,
                        previewMatProp);
                }
            }
        }

        public void OnDrawGizmos()
        {
#if UNITY_EDITOR
            foreach (var surface in Surfaces)
            {
                Gizmos.DrawIcon(Origin.MultiplyPoint(surface.LocalPosition), k_SurfaceIconName);
            }
#endif
        }

        public void AddSurface()
        {
            m_ProjectionSurfaces.Add(PlanarProjectionSurface.Create($"Screen {m_ProjectionSurfaces.Count}"));
        }

        public void RemoveSurface(int index)
        {
            m_ProjectionSurfaces.RemoveAt(index);

            if (m_RenderTargets.TryGetValue(index, out var rt))
            {
                rt.Release();
                m_RenderTargets.Remove(index);
            }
        }

        public void SetSurface(int index, PlanarProjectionSurface surface)
        {
            Assert.IsTrue(index < m_ProjectionSurfaces.Count);
            m_ProjectionSurfaces[index] = surface;
        }

        RenderTexture GetRenderTexture(int index, Vector2Int overscannedSize)
        {
            m_RenderTargets.TryGetValue(index, out var rt);

            if (GraphicsUtil.AllocateIfNeeded(
                ref rt,
                overscannedSize.x,
                overscannedSize.y))
            {
                m_RenderTargets[index] = rt;
            }

            return rt;
        }

        void RenderSurface(int index, ClusterRendererSettings clusterSettings, Camera activeCamera, bool debug = false)
        {
            var surface = m_ProjectionSurfaces[index];
            var overscannedSize = surface.ScreenResolution + clusterSettings.OverScanInPixels * 2 * Vector2Int.one;

            var surfacePlane = surface.GetFrustumPlane(Origin);

            var cameraTransform = activeCamera.transform;
            var position = cameraTransform.position;

            var lookAtPoint = ProjectPointToPlane(position, surfacePlane);

            if (IsDebug)
            {
                Debug.DrawLine(position, lookAtPoint);
            }

            var upDir = surfacePlane.TopLeft - surfacePlane.BottomLeft;
            var alignedRotation = Quaternion.LookRotation(lookAtPoint - position, upDir);
            var alignedCameraTransform = Matrix4x4.TRS(position, alignedRotation, Vector3.one);

            var planeInViewCoords = surfacePlane.ApplyTransform(alignedCameraTransform.inverse);

            var projectionMatrix = GetProjectionMatrix(activeCamera.projectionMatrix,
                planeInViewCoords,
                surface.ScreenResolution,
                clusterSettings.OverScanInPixels);

            var renderFeatures = RenderFeature.AsymmetricProjection;
            if (debug)
            {
                renderFeatures |= RenderFeature.ClearHistory;
            }
            using var cameraScope = CameraScopeFactory.Create(activeCamera, renderFeatures);

            cameraScope.Render(GetRenderTexture(index, overscannedSize),
                projectionMatrix,
                position: cameraTransform.position,
                rotation: alignedRotation);
        }

        static Vector3 ProjectPointToPlane(Vector3 pt, in PlanarProjectionSurface.FrustumPlane plane)
        {
            var normal = Vector3.Cross(plane.BottomRight - plane.BottomLeft,
                    plane.TopLeft - plane.BottomLeft)
                .normalized;
            return pt - Vector3.Dot(pt - plane.BottomLeft, normal) * normal;
        }

        static Matrix4x4 GetProjectionMatrix(
            Matrix4x4 originalProjection,
            in PlanarProjectionSurface.FrustumPlane plane,
            Vector2Int resolution,
            int overScanInPixels)
        {
            var planeLeft = plane.BottomLeft.x;
            var planeRight = plane.BottomRight.x;
            var planeDepth = plane.BottomLeft.z;
            var planeTop = plane.TopLeft.y;
            var planeBottom = plane.BottomLeft.y;
            var originalFrustum = originalProjection.decomposeProjection;
            var frustumPlanes = new FrustumPlanes
            {
                zNear = originalFrustum.zNear,
                zFar = originalFrustum.zFar,
                left = planeLeft * originalFrustum.zNear / planeDepth,
                right = planeRight * originalFrustum.zNear / planeDepth,
                top = planeTop * originalFrustum.zNear / planeDepth,
                bottom = planeBottom * originalFrustum.zNear / planeDepth
            };

            var frustumSize = new Vector2(
                frustumPlanes.right - frustumPlanes.left,
                frustumPlanes.top - frustumPlanes.bottom);
            var overscanDelta = frustumSize / resolution * overScanInPixels;
            frustumPlanes.left -= overscanDelta.x;
            frustumPlanes.right += overscanDelta.x;
            frustumPlanes.bottom -= overscanDelta.y;
            frustumPlanes.top += overscanDelta.y;

            return Matrix4x4.Frustum(frustumPlanes);
        }
    }
}
