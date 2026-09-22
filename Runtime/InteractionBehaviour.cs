using System;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace InteractionSystem.Runtime
{
    [Flags]
    public enum InteractionCategories
    {
        None = 0,
        Hover = 1 << 0,
        Click = 1 << 1,
        Drag = 1 << 2,
        Drop = 1 << 3,
        All = Hover | Click | Drag | Drop
    }

    /// <summary>
    /// A serializable interaction module owned by one InteractableObject. It is deliberately not a
    /// MonoBehaviour: Unity lifecycle and input polling remain centralized on the host and dispatcher.
    /// </summary>
    [Serializable]
    [HideReferenceObjectPicker]
    public abstract class InteractionBehaviour
    {
        [FoldoutGroup("Behaviour Settings"), OdinSerialize, PropertyOrder(-20)]
        private bool behaviourEnabled = true;

        [FoldoutGroup("Behaviour Settings"), OdinSerialize, EnumToggleButtons, PropertyOrder(-19)]
        private InteractionCategories enabledInteractions = InteractionCategories.All;

        [NonSerialized] private InteractableObject target;
        [NonSerialized] private bool initialized;

        public bool BehaviourEnabled
        {
            get => behaviourEnabled;
            set => behaviourEnabled = value;
        }

        public InteractionCategories EnabledInteractions
        {
            get => enabledInteractions;
            set => enabledInteractions = value;
        }

        public InteractableObject Target => target;
        public GameObject Owner => target ? target.gameObject : null;
        public Transform Transform => target ? target.transform : null;
        public bool IsInitialized => initialized;

        public bool IsInteractionEnabled(InteractionCategories category) => behaviourEnabled && (enabledInteractions & category) != 0;

        /// <summary>Called once after this instance is attached to its host.</summary>
        public virtual void Initialize() { }

        /// <summary>Called when this instance is removed or its host is destroyed.</summary>
        public virtual void Deinitialize() { }

        internal bool TryAttach(InteractableObject owner)
        {
            if (target && target != owner)
                return false;
            if (initialized)
                return true;

            target = owner;
            initialized = true;
            try
            {
                Initialize();
                return true;
            }
            catch
            {
                initialized = false;
                target = null;
                throw;
            }
        }

        internal void Detach()
        {
            if (!initialized)
            {
                target = null;
                return;
            }

            initialized = false;
            try
            {
                Deinitialize();
            }
            finally
            {
                target = null;
            }
        }
    }
}
