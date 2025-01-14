﻿using UnityEngine;

namespace Unity.ClusterDisplay.RPC
{
    public abstract class ComponentWrapper<InstanceType> : MonoBehaviour
        where InstanceType : UnityEngine.Component
    {
        [SerializeField] InstanceType instance;

        /// <summary>
        /// This will return either the cached instance (If it exists), it will attempt to automatically
        /// find an instance of the target component attached to the GameObject. However, if you have
        /// more then one instance of this type, you should instead use SetInstance(InstanceType instance)
        /// to specify which instance this wrapper will wrap upon adding the component.
        /// </summary>
        /// <param name="outInstance"></param>
        /// <returns></returns>
        internal bool TryGetInstance (out InstanceType outInstance)
        {
            if (instance == null)
            {
                var instances = GetComponents<InstanceType>();
                if (instances.Length == 0)
                {
                    ClusterDebug.LogError($"There is no instance of: \"{typeof(InstanceType).FullName}\" to wrap that is attached to GameObject: \"{gameObject.name}\".");
                    outInstance = null;
                    return false;
                }

                if (instances.Length > 1)
                {
                    ClusterDebug.LogError($"There is more then one instance of type: \"{typeof(InstanceType).FullName}\" attached to GameObject: \"{gameObject.name}\", we cannot determine which instance of the type you want. Therefore, if you want to specific instance, use SetInstance(InsteanceType instance) after adding the component.");
                    outInstance = null;
                    return false;
                }

                instance = instances[0];
            }

            return (outInstance = instance) != null;
        }

        /// <summary>
        /// After adding a component, you can use this method to specify which 
        /// component instance you want to wrap.
        /// </summary>
        /// <param name="instance"></param>
        internal void SetInstance(InstanceType instance) => this.instance = instance;
    } }
