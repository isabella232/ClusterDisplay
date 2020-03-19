﻿using UnityEngine;
using Unity.ClusterRendering;

namespace Unity.ClusterDisplay.Graphics.Example
{
    /// <summary>
    /// Here is an example of a simple runtime GUI displaying useful cluster display related data,
    /// for debugging/monitoring purposes
    /// </summary>
    [ExecuteAlways]
    public class RuntimeGUI : MonoBehaviour
    {
        [Tooltip("ClusterRenderer component whose settings are to be displayed/edited.")]
        [SerializeField]
        ClusterRenderer m_ClusterRenderer; 
        
        [Tooltip("Show/Hide GUI.")]
        [SerializeField]
        bool m_Show = true;

        [Tooltip("Update displayed fps every N frames (for readability).")]
        [SerializeField]
        int m_DisplayUpdateRate;
        
        Vector2 m_ScrollPosition;

        const int k_FpsBufferSize = 24;
        
        float[] m_FpsBuffer = new float[k_FpsBufferSize];
        float m_FpsMovingAverage;

        void Update()
        {
            var index = Time.frameCount % m_FpsBuffer.Length;
            m_FpsBuffer[index] = 1.0f / Time.unscaledDeltaTime;

            m_DisplayUpdateRate = Mathf.Max(1, m_DisplayUpdateRate);
            var updateDisplay = Time.frameCount % m_DisplayUpdateRate == 0;
            if (updateDisplay)
            {
                var sum = 0f;
                for (int i = 0; i < m_FpsBuffer.Length; i++)
                    sum += m_FpsBuffer[i];
                m_FpsMovingAverage = sum / m_FpsBuffer.Length;
            }

            if (Input.GetKeyDown(KeyCode.H))
                m_Show = !m_Show;
        }

        /// <summary>
        /// Override this method to append project specific GUI code.
        /// </summary>
        public virtual void OnCustomGUI() { }

        void OnGUI()
        {
            if (!m_Show) return;

            GUI.color = Color.black * 0.5f;
            GUI.DrawTexture(new Rect(0, 0, Screen.width / 2, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            m_ScrollPosition = GUILayout.BeginScrollView(m_ScrollPosition, GUILayout.Width(Screen.width / 2));

            DrawStats();

            GUILayout.Label($"Cluster Sync active [{ClusterSync.Active}]");
            if (ClusterSync.Active && ClusterSync.Instance != null)
                GUILayout.Label($"Cluster Sync DynamicNodeId [{ClusterSync.Instance.DynamicLocalNodeId}]");
            GUILayout.Label($"Max Queued Frames [{QualitySettings.maxQueuedFrames}]");

            if (m_ClusterRenderer != null)
            {
                GUIUtilities.DrawSettings(m_ClusterRenderer.Settings);
                GUIUtilities.KeyboardControls(m_ClusterRenderer.Settings);

                var prevDebug = m_ClusterRenderer.Debug;
                m_ClusterRenderer.Debug = GUILayout.Toggle(prevDebug, "debug");
                if (m_ClusterRenderer.Debug)
                    GUIUtilities.DrawDebugSettings(m_ClusterRenderer.DebugSettings);
            }

            OnCustomGUI();

            GUILayout.EndScrollView();
            if (GUILayout.Button("Exit GUI"))
                m_Show = false;
            
            GUILayout.Label($"Press <b>H</b> to show/hide GUI");
        }

        void DrawStats()
        {
            GUILayout.Label($"FPS [{m_FpsMovingAverage}]");
            GUILayout.Label($"Frame Count [{Time.renderedFrameCount}]");
            GUILayout.Label($"Resolution [{Screen.width}x{Screen.height}]");
        }
    }
}