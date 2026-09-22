using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace InteractionSystem.Runtime
{
    /// <summary>
    /// Marks a GameObject as a mouse-interaction target and owns its Odin-serialized behaviour modules.
    /// Collider and UI Graphic children are resolved back to this component automatically.
    /// </summary>
    [DisallowMultipleComponent]
    public class InteractableObject : SerializedMonoBehaviour, IInteractableTarget
    {
        [OdinSerialize]
#if UNITY_EDITOR
        [ListDrawerSettings(ShowItemCount = true, DraggableItems = true, OnBeginListElementGUI = nameof(DrawElementScriptLink))]
#endif
        private List<InteractionBehaviour> behaviours = new();

        private readonly HashSet<InteractionBehaviour> uniqueBehaviours = new();
        private readonly HashSet<InteractionBehaviour> attachedBehaviours = new();
        private readonly List<InteractionBehaviour> staleBehaviours = new();
        private readonly List<IHoverHandler> hoverHandlers = new();
        private readonly List<IClickHandler> clickHandlers = new();
        private readonly List<IDragHandler> dragHandlers = new();
        private readonly List<IDropHandler> dropHandlers = new();
        private bool isRefreshing;
        private bool isDestroying;

        public bool InteractionEnabled => isActiveAndEnabled;
        public IReadOnlyList<InteractionBehaviour> Behaviours => behaviours;
        public IReadOnlyList<IHoverHandler> HoverHandlers => hoverHandlers;
        public IReadOnlyList<IClickHandler> ClickHandlers => clickHandlers;
        public IReadOnlyList<IDragHandler> DragHandlers => dragHandlers;
        public IReadOnlyList<IDropHandler> DropHandlers => dropHandlers;

        protected virtual void Awake() => RefreshBehaviours();

        protected virtual void OnEnable() => RefreshBehaviours();

        protected virtual void OnDisable()
        {
            if (InteractionSystem.TryGetExistingInstance(out var system))
                system.NotifyTargetUnavailable(this);
        }

        protected virtual void OnDestroy()
        {
            isDestroying = true;
            var attached = new InteractionBehaviour[attachedBehaviours.Count];
            attachedBehaviours.CopyTo(attached);
            attachedBehaviours.Clear();
            foreach (var behaviour in attached)
                DetachBehaviour(behaviour);
        }

        /// <summary>Rebuilds cached interfaces after Odin deserialization or runtime list changes.</summary>
        public void RefreshBehaviours()
        {
            if (isRefreshing || isDestroying)
                return;
            isRefreshing = true;
            try
            {
                RebuildBehaviours();
            }
            finally
            {
                isRefreshing = false;
            }
        }

        private void RebuildBehaviours()
        {
            CancelActiveInteractions();
            if (isDestroying)
                return;
            behaviours ??= new List<InteractionBehaviour>();
            hoverHandlers.Clear();
            clickHandlers.Clear();
            dragHandlers.Clear();
            dropHandlers.Clear();
            uniqueBehaviours.Clear();

            for (int i = 0; i < behaviours.Count; i++)
            {
                var behaviour = behaviours[i];
                if (behaviour == null)
                    continue;
                if (!uniqueBehaviours.Add(behaviour))
                    Debug.LogWarning($"[InteractionSystem] The same {behaviour.GetType().Name} instance appears more than once on {name}; duplicate entries are ignored.", this);
            }

            staleBehaviours.Clear();
            foreach (var behaviour in attachedBehaviours)
                if (!uniqueBehaviours.Contains(behaviour))
                    staleBehaviours.Add(behaviour);
            for (int i = 0; i < staleBehaviours.Count; i++)
            {
                var behaviour = staleBehaviours[i];
                DetachBehaviour(behaviour);
                attachedBehaviours.Remove(behaviour);
            }

            uniqueBehaviours.Clear();
            for (int i = 0; i < behaviours.Count; i++)
            {
                var behaviour = behaviours[i];
                if (behaviour == null || !uniqueBehaviours.Add(behaviour))
                    continue;

                try
                {
                    if (!behaviour.TryAttach(this))
                    {
                        Debug.LogError($"[InteractionSystem] {behaviour.GetType().Name} is already owned by another InteractableObject.", this);
                        continue;
                    }
                    if (isDestroying)
                    {
                        DetachBehaviour(behaviour);
                        return;
                    }
                    attachedBehaviours.Add(behaviour);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                    continue;
                }

                if (behaviour is IHoverHandler hover)
                    hoverHandlers.Add(hover);
                if (behaviour is IClickHandler click)
                    clickHandlers.Add(click);
                if (behaviour is IDragHandler drag)
                    dragHandlers.Add(drag);
                if (behaviour is IDropHandler drop)
                    dropHandlers.Add(drop);
            }
        }

        [Obsolete("Use RefreshBehaviours().")]
        public void RefreshHandlers() => RefreshBehaviours();

        public T AddBehaviour<T>() where T : InteractionBehaviour, new()
        {
            var behaviour = new T();
            return AddBehaviour(behaviour) ? behaviour : null;
        }

        public bool AddBehaviour(InteractionBehaviour behaviour)
        {
            if (isRefreshing || isDestroying || behaviour == null || behaviours.Contains(behaviour))
                return false;

            CancelActiveInteractions();
            if (isDestroying || behaviours.Contains(behaviour))
                return false;
            behaviours.Add(behaviour);
            RefreshBehaviours();
            if (behaviour.Target == this)
                return true;

            behaviours.Remove(behaviour);
            RefreshBehaviours();
            return false;
        }

        public bool RemoveBehaviour(InteractionBehaviour behaviour)
        {
            if (isRefreshing || isDestroying || !behaviours.Contains(behaviour))
                return false;

            CancelActiveInteractions();
            if (isDestroying || !behaviours.Remove(behaviour))
                return false;
            RefreshBehaviours();
            return true;
        }

        public bool TryGetBehaviour<T>(out T behaviour, bool includeDisabled = false) where T : class
        {
            for (int i = 0; i < behaviours.Count; i++)
            {
                var item = behaviours[i];
                if (item is not T match || item.Target != this || !item.IsInitialized)
                    continue;
                if (!includeDisabled && !item.BehaviourEnabled)
                    continue;
                behaviour = match;
                return true;
            }

            behaviour = null;
            return false;
        }

        public bool TryGetComponentOnTarget<T>(out T component, bool includeChildren = false) where T : Component
        {
            component = includeChildren ? GetComponentInChildren<T>(true) : GetComponent<T>();
            return component;
        }

        private void CancelActiveInteractions()
        {
            if (Application.isPlaying && InteractionSystem.TryGetExistingInstance(out var system))
                system.NotifyTargetUnavailable(this);
        }

        private void DetachBehaviour(InteractionBehaviour behaviour)
        {
            try
            {
                behaviour.Detach();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        internal static bool IsHandlerEnabled(object handler, InteractionCategories category)
        {
            return handler is InteractionBehaviour behaviour && behaviour.IsInitialized && behaviour.Target && behaviour.IsInteractionEnabled(category);
        }

        internal static bool IsHandlerAlive(object handler) => handler is InteractionBehaviour behaviour && behaviour.IsInitialized && behaviour.Target;

#if UNITY_EDITOR
        private static readonly Dictionary<Type, UnityEditor.MonoScript> scriptCache = new();

        private void DrawElementScriptLink(int index)
        {
            if (index < 0 || index >= behaviours.Count || behaviours[index] == null)
                return;

            var behaviour = behaviours[index];
            var script = GetScript(behaviour.GetType());
            if (!script)
                return;

            if (GUILayout.Button($"↗ {behaviour.GetType().Name}", UnityEditor.EditorStyles.miniLabel))
                UnityEditor.AssetDatabase.OpenAsset(script);
        }

        private static UnityEditor.MonoScript GetScript(Type type)
        {
            if (scriptCache.TryGetValue(type, out var cached))
                return cached;

            var guids = UnityEditor.AssetDatabase.FindAssets($"{type.Name} t:MonoScript");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]);
                var script = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.MonoScript>(path);
                if (!script || script.GetClass() != type)
                    continue;

                scriptCache[type] = script;
                return script;
            }

            scriptCache[type] = null;
            return null;
        }

        [UnityEditor.Callbacks.DidReloadScripts]
        private static void ClearScriptCache() => scriptCache.Clear();
#endif
    }
}
