using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace InteractionSystem.Runtime
{
    /// <summary>
    /// Central mouse dispatcher for 3D colliders and uGUI Graphics. It owns pointer capture so
    /// hover, click, drag and drop have identical ordering regardless of how a target was hit.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class InteractionSystem : MonoBehaviour
    {
        [Header("Pointer")]
        [SerializeField, Range(0, 2)] private int mouseButton;
        [SerializeField, Min(0f)] private float dragThreshold = 5f;

        [Header("Physics")]
        [SerializeField] private Camera interactionCamera;
        [SerializeField] private LayerMask physicsLayers = ~0;
        [SerializeField, Min(0f)] private float maxDistance = 1000f;
        [SerializeField, Min(0f)] private float sphereCastRadius;
        [Tooltip("Initial hit buffer capacity. A full buffer is expanded and reused automatically.")]
        [SerializeField, Min(InteractionSystemConsts.MinPhysicsHits)] private int maxPhysicsHits = 32;
        [SerializeField] private QueryTriggerInteraction queryTriggers = QueryTriggerInteraction.UseGlobal;
        [Tooltip("When enabled, the nearest non-interactable collider prevents selecting targets behind it.")]
        [SerializeField] private bool nonInteractablePhysicsBlocks = true;

        [Header("UI")]
        [SerializeField] private bool queryUI = true;
        [Tooltip("When enabled, the first raycastable UI Graphic prevents interaction with objects behind it.")]
        [SerializeField] private bool nonInteractableUIBlocksPhysics = true;

        private static InteractionSystem instance;
        private readonly List<RaycastResult> uiResults = new(16);

        private readonly List<IHoverHandler> activeHoverHandlers = new(4);
        private readonly List<IDropHandler> activeDropHandlers = new(4);
        private readonly List<IClickHandler> capturedClickHandlers = new(4);
        private readonly List<IDragHandler> activeDragHandlers = new(4);
        private readonly HashSet<Type> disabledBehaviourTypes = new();
        private RaycastHit[] physicsHits;
        private EventSystem eventSystem;
        private PointerEventData pointerEventData;
        private IPointerInputSource inputSource;

        private PointerFrame pointer;
        private PointerHit hoverHit;
        private InteractableObject pressedTarget;
        private InteractableObject draggedTarget;
        private InteractableObject dropTarget;
        private Vector2 pressPosition;
        private bool isDragging;
        private bool isCanceling;
        private bool captureTransition;
        private bool cancelRequested;
        private bool warnedHitCapacity;
        private bool warnedMissingCamera;
        private int interactionVersion;

        public static InteractionSystem Instance
        {
            get
            {
                if (!instance)
                    Debug.LogError("[InteractionSystem] No enabled InteractionSystem exists in the active scene.");
                return instance;
            }
        }

        public static bool TryGetExistingInstance(out InteractionSystem system)
        {
            system = instance;
            return system;
        }

        public InteractableObject HoveredTarget => IsAvailable(hoverHit.Target) ? hoverHit.Target : null;
        public InteractableObject PressedTarget => IsAvailable(pressedTarget) ? pressedTarget : null;
        public InteractableObject DraggedTarget => IsAvailable(draggedTarget) ? draggedTarget : null;
        public InteractableObject DropTarget => IsAvailable(dropTarget) ? dropTarget : null;
        public bool IsDragging => isDragging;

        private void Awake()
        {
            EnsurePhysicsBuffer();
            inputSource ??= new BuiltInMouseInputSource();
        }

        private void OnEnable()
        {
            if (instance && instance != this)
            {
                Debug.LogError("[InteractionSystem] Only one active instance is allowed.", this);
                enabled = false;
                return;
            }

            instance = this;
        }

        private void OnValidate()
        {
            mouseButton = Mathf.Clamp(mouseButton, 0, 2);
            dragThreshold = Mathf.Max(0f, dragThreshold);
            maxDistance = Mathf.Max(0f, maxDistance);
            sphereCastRadius = Mathf.Max(0f, sphereCastRadius);
            maxPhysicsHits = Mathf.Max(InteractionSystemConsts.MinPhysicsHits, maxPhysicsHits);
            if (interactionCamera)
                warnedMissingCamera = false;
        }

        private void OnDrawGizmosSelected()
        {
            if (!interactionCamera || maxDistance <= 0f)
                return;
            var ray = interactionCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(ray.origin, ray.direction * maxDistance);
            if (sphereCastRadius > 0f)
                Gizmos.DrawWireSphere(ray.GetPoint(maxDistance), sphereCastRadius);
        }

        private void OnDisable()
        {
            CancelAllInteractions();
            if (instance == this)
                instance = null;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
                CancelAllInteractions();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
                CancelAllInteractions();
        }

        /// <summary>Overrides pointer input, useful for tests, replay, or a project's chosen input package.</summary>
        public void SetInputSource(IPointerInputSource source)
        {
            inputSource = source ?? throw new ArgumentNullException(nameof(source));
            CancelAllInteractions();
        }

        public void SetInteractionCamera(Camera camera)
        {
            interactionCamera = camera;
            warnedMissingCamera = false;
        }

        private void Update()
        {
            if (inputSource == null || !inputSource.TryGetFrame(mouseButton, out pointer))
            {
                CancelAllInteractions();
                return;
            }

            int version = interactionVersion;
            ValidateCapturedTargets();
            if (pressedTarget && !pointer.IsPressed && !pointer.ReleasedThisFrame)
                CancelCapture();

            if (!CanContinue(version))
                return;
            var hit = ResolvePointerHit(pointer.ScreenPosition, isDragging ? draggedTarget : null);
            TransitionHover(hit);
            if (!CanContinue(version))
                return;

            if (pointer.PressedThisFrame)
                BeginPress(hit);
            if (!CanContinue(version))
                return;

            if (pressedTarget && pointer.IsPressed && !pointer.PressedThisFrame)
            {
                if (!isDragging && HasEnabledHandler(pressedTarget.DragHandlers, InteractionCategories.Drag) && (pointer.ScreenPosition - pressPosition).sqrMagnitude >= dragThreshold * dragThreshold)
                {
                    BeginDrag();
                    if (!CanContinue(version))
                        return;
                    hit = ResolvePointerHit(pointer.ScreenPosition, draggedTarget);
                    TransitionHover(hit);
                    if (!CanContinue(version))
                        return;
                }

                if (isDragging)
                {
                    UpdateDropTarget(hit);
                    if (!CanContinue(version) || !isDragging)
                        return;
                    var context = CreateContext(hit);
                    DispatchDrag(activeDragHandlers, DragPhase.Move, context);
                }
                else
                {
                    var context = CreateContext(hit);
                    DispatchClick(capturedClickHandlers, ClickPhase.Held, context);
                }
            }

            if (pointer.ReleasedThisFrame)
            {
                if (!CanContinue(version))
                    return;
                if (isDragging)
                    CompleteDrag(hit);
                else CompleteClick(hit);

                if (!CanContinue(version))
                    return;
                var releasedHit = ResolvePointerHit(pointer.ScreenPosition, null);
                TransitionHover(releasedHit);
            }
        }

        private void BeginPress(PointerHit hit)
        {
            if (pressedTarget || isDragging)
                CancelCapture();

            if (!IsAvailable(hit.Target))
                return;
            if (!HasEnabledHandler(hit.Target.ClickHandlers, InteractionCategories.Click) && !HasEnabledHandler(hit.Target.DragHandlers, InteractionCategories.Drag))
                return;

            pressedTarget = hit.Target;
            pressPosition = pointer.ScreenPosition;
            CollectEnabled(pressedTarget.ClickHandlers, capturedClickHandlers, InteractionCategories.Click);
            var context = CreateContext(hit);
            DispatchClick(capturedClickHandlers, ClickPhase.Down, context);
            if (!IsAvailable(pressedTarget))
                CancelCapture();
        }

        private void CompleteClick(PointerHit hit)
        {
            if (!IsAvailable(pressedTarget))
            {
                CancelCapture();
                return;
            }

            var captured = pressedTarget;
            bool releasedInside = hit.Target == captured;
            var context = CreateContext(hit);
            captureTransition = true;
            try
            {
                DispatchClick(capturedClickHandlers, ClickPhase.Up, context, releasedInside, true);
                if (releasedInside && !cancelRequested && IsAvailable(captured) && isActiveAndEnabled)
                    DispatchClick(capturedClickHandlers, ClickPhase.Click, context);
            }
            finally
            {
                capturedClickHandlers.Clear();
                pressedTarget = null;
                FinishCaptureTransition();
            }
        }

        private void BeginDrag()
        {
            if (!IsAvailable(pressedTarget))
                return;
            draggedTarget = pressedTarget;
            isDragging = true;

            var context = CreateContext(hoverHit);
            captureTransition = true;
            try
            {
                DispatchClick(capturedClickHandlers, ClickPhase.Cancel, context, false, true);
                capturedClickHandlers.Clear();
                if (!cancelRequested && IsAvailable(draggedTarget) && isActiveAndEnabled)
                {
                    CollectEnabled(draggedTarget.DragHandlers, activeDragHandlers, InteractionCategories.Drag);
                    DispatchDrag(activeDragHandlers, DragPhase.Begin, context);
                }
            }
            finally
            {
                FinishCaptureTransition();
            }

            if (isDragging && (!IsAvailable(draggedTarget) || !HasEnabledHandler(activeDragHandlers, InteractionCategories.Drag)))
                CancelCapture();
        }

        private void CompleteDrag(PointerHit hit)
        {
            UpdateDropTarget(hit);
            if (!isDragging)
                return;
            var context = CreateContext(hit);
            captureTransition = true;
            try
            {
                if (IsAvailable(dropTarget))
                    DispatchDrop(activeDropHandlers, DropPhase.Drop, context);

                if (draggedTarget)
                    DispatchDrag(activeDragHandlers, DragPhase.End, context, true);

                ExitDropTarget(context, true);
            }
            finally
            {
                isDragging = false;
                activeDragHandlers.Clear();
                capturedClickHandlers.Clear();
                draggedTarget = null;
                pressedTarget = null;
                FinishCaptureTransition();
            }
        }

        private void CancelCapture()
        {
            if (captureTransition)
            {
                cancelRequested = true;
                return;
            }
            interactionVersion++;
            captureTransition = true;
            var context = CreateContext(hoverHit);
            try
            {
                if (isDragging && draggedTarget)
                    DispatchDrag(activeDragHandlers, DragPhase.Cancel, context, true);
                else if (pressedTarget)
                    DispatchClick(capturedClickHandlers, ClickPhase.Cancel, context, false, true);

                ExitDropTarget(context, true);
            }
            finally
            {
                isDragging = false;
                activeDragHandlers.Clear();
                capturedClickHandlers.Clear();
                draggedTarget = null;
                pressedTarget = null;
                FinishCaptureTransition();
            }
        }

        private void FinishCaptureTransition()
        {
            captureTransition = false;
            if (!cancelRequested)
                return;
            cancelRequested = false;
            CancelAllInteractions();
        }

        private bool CanContinue(int version) => version == interactionVersion && isActiveAndEnabled && !cancelRequested;

        private void TransitionHover(PointerHit nextHit)
        {
            int version = interactionVersion;
            var previous = hoverHit.Target;
            var next = IsAvailable(nextHit.Target) ? nextHit.Target : null;

            if (previous == next)
            {
                hoverHit = nextHit;
                if (next)
                {
                    var stayContext = CreateContext(nextHit);
                    ReconcileHoverHandlers(next, stayContext);
                    if (!CanContinue(version))
                        return;
                    DispatchHover(activeHoverHandlers, HoverPhase.Stay, stayContext);
                }
                return;
            }

            var exitHandlers = ListPool<IHoverHandler>.Get();
            try
            {
                var exitContext = CreateContext(hoverHit);
                exitHandlers.AddRange(activeHoverHandlers);
                activeHoverHandlers.Clear();
                hoverHit = PointerHit.None;
                DispatchHover(exitHandlers, HoverPhase.Exit, exitContext, true);
            }
            finally
            {
                ListPool<IHoverHandler>.Release(exitHandlers);
            }

            if (!CanContinue(version))
                return;
            hoverHit = nextHit;
            if (IsAvailable(next))
            {
                CollectEnabled(next.HoverHandlers, activeHoverHandlers, InteractionCategories.Hover);
                var enterContext = CreateContext(nextHit);
                DispatchHover(activeHoverHandlers, HoverPhase.Enter, enterContext);
            }
        }

        private void ReconcileHoverHandlers(InteractableObject target, in InteractionContext context)
        {
            int version = interactionVersion;
            for (int i = activeHoverHandlers.Count - 1; i >= 0; i--)
            {
                var handler = activeHoverHandlers[i];
                if (IsHandlerEnabled(handler, InteractionCategories.Hover))
                    continue;

                activeHoverHandlers.RemoveAt(i);
                DispatchHoverSingle(handler, HoverPhase.Exit, context, true);
                if (!CanContinue(version))
                    return;
            }

            var handlers = target.HoverHandlers;
            for (int i = 0; i < handlers.Count; i++)
            {
                var handler = handlers[i];
                if (!IsHandlerEnabled(handler, InteractionCategories.Hover) || activeHoverHandlers.Contains(handler))
                    continue;

                activeHoverHandlers.Add(handler);
                DispatchHoverSingle(handler, HoverPhase.Enter, context);
                if (!CanContinue(version))
                    return;
            }
        }

        private void UpdateDropTarget(PointerHit hit)
        {
            if (!isDragging)
                return;
            int version = interactionVersion;
            var candidate = IsAvailable(hit.Target) && hit.Target != draggedTarget ? hit.Target : null;
            var acceptedHandlers = ListPool<IDropHandler>.Get();
            var candidates = ListPool<IDropHandler>.Get();
            try
            {
                if (candidate)
                {
                    var probeContext = CreateContext(hit, candidate);
                    CollectEnabled(candidate.DropHandlers, candidates, InteractionCategories.Drop);
                    foreach (var handler in candidates)
                    {
                        if (!IsHandlerEnabled(handler, InteractionCategories.Drop))
                            continue;
                        bool accepted = CanAcceptDrop(handler, probeContext);
                        if (!CanContinue(version) || !isDragging || !IsAvailable(candidate))
                            return;
                        if (accepted && CanDispatch(handler, InteractionCategories.Drop, false, candidate))
                            acceptedHandlers.Add(handler);
                    }
                }

                if (candidate != dropTarget || !SameHandlers(activeDropHandlers, acceptedHandlers))
                {
                    ExitDropTarget(CreateContext(hit), true);
                    if (!CanContinue(version) || !isDragging)
                        return;
                    if (acceptedHandlers.Count > 0)
                    {
                        dropTarget = candidate;
                        activeDropHandlers.AddRange(acceptedHandlers);
                        var enterContext = CreateContext(hit);
                        DispatchDrop(activeDropHandlers, DropPhase.Enter, enterContext);
                        if (!CanContinue(version) || !isDragging)
                            return;
                    }
                }

                if (dropTarget)
                {
                    var overContext = CreateContext(hit);
                    DispatchDrop(activeDropHandlers, DropPhase.Over, overContext);
                }
            }
            finally
            {
                ListPool<IDropHandler>.Release(acceptedHandlers);
                ListPool<IDropHandler>.Release(candidates);
            }
        }

        private void ExitDropTarget(InteractionContext context, bool includeDisabled = false)
        {
            var handlers = ListPool<IDropHandler>.Get();
            try
            {
                handlers.AddRange(activeDropHandlers);
                activeDropHandlers.Clear();
                dropTarget = null;
                DispatchDrop(handlers, DropPhase.Exit, context, includeDisabled);
            }
            finally
            {
                ListPool<IDropHandler>.Release(handlers);
            }
        }

        private PointerHit ResolvePointerHit(Vector2 screenPosition, InteractableObject ignoredTarget)
        {
            bool hasUIHit = TryResolveUI(screenPosition, ignoredTarget, out var uiHit, out bool blocksPhysics);
            if (blocksPhysics)
                return PointerHit.None;
            if (hasUIHit && uiHit.IsScreenSpaceUI)
                return uiHit;

            var physicsHit = ResolvePhysics(screenPosition, ignoredTarget);
            if (!hasUIHit)
                return physicsHit;
            if (!physicsHit.HasWorldHit || uiHit.Distance <= physicsHit.Distance)
                return uiHit;
            return physicsHit;
        }

        private bool TryResolveUI(Vector2 screenPosition, InteractableObject ignoredTarget,
            out PointerHit hit, out bool blocksPhysics)
        {
            hit = PointerHit.None;
            blocksPhysics = false;
            if (!queryUI || EventSystem.current == null)
                return false;

            if (eventSystem != EventSystem.current || pointerEventData == null)
            {
                eventSystem = EventSystem.current;
                pointerEventData = new PointerEventData(eventSystem);
            }

            pointerEventData.Reset();
            pointerEventData.position = screenPosition;
            uiResults.Clear();
            eventSystem.RaycastAll(pointerEventData, uiResults);

            foreach (var result in uiResults)
            {
                if (!result.gameObject || result.module is not GraphicRaycaster)
                    continue;
                var alphaFilter = result.gameObject.GetComponent<UIAlphaRaycastFilter>();
                if (alphaFilter && alphaFilter.isActiveAndEnabled && !alphaFilter.AllowsRaycast(screenPosition, result.module.eventCamera))
                    continue;
                var target = result.gameObject.GetComponentInParent<InteractableObject>();
                if (ignoredTarget && target == ignoredTarget)
                    continue;
                var canvas = result.module ? result.module.GetComponent<Canvas>() : null;
                bool isScreenSpace = !canvas || canvas.renderMode != RenderMode.WorldSpace;
                if (IsAvailable(target))
                {
                    hit = PointerHit.ForUI(target, result.gameObject, result.distance, isScreenSpace);
                    return true;
                }

                if (nonInteractableUIBlocksPhysics)
                {
                    if (isScreenSpace)
                    {
                        blocksPhysics = true;
                        return false;
                    }

                    hit = PointerHit.ForUI(null, result.gameObject, result.distance, false);
                    return true;
                }
            }

            return false;
        }

        private PointerHit ResolvePhysics(Vector2 screenPosition, InteractableObject ignoredTarget)
        {
            if (maxDistance <= 0f)
                return PointerHit.None;
            if (!interactionCamera)
            {
                if (!warnedMissingCamera)
                {
                    warnedMissingCamera = true;
                    Debug.LogError("[InteractionSystem] Assign Interaction Camera to enable physics interaction, or set Max Distance to 0 for UI-only scenes.", this);
                }
                return PointerHit.None;
            }
            EnsurePhysicsBuffer();

            var ray = interactionCamera.ScreenPointToRay(screenPosition);
            int count = Physics.RaycastNonAlloc(ray, physicsHits, maxDistance, physicsLayers, queryTriggers);
            bool useSphere = count == 0 && sphereCastRadius > 0f;
            if (useSphere)
                count = Physics.SphereCastNonAlloc(ray, sphereCastRadius, physicsHits, maxDistance, physicsLayers, queryTriggers);

            // NonAlloc queries do not guarantee the nearest hits when the buffer fills.
            // Grow from a complete query once, then reuse the larger buffer on subsequent frames.
            if (count == physicsHits.Length)
            {
                var allHits = useSphere ? Physics.SphereCastAll(ray, sphereCastRadius, maxDistance, physicsLayers, queryTriggers) : Physics.RaycastAll(ray, maxDistance, physicsLayers, queryTriggers);
                physicsHits = new RaycastHit[Mathf.Max(physicsHits.Length * 2, allHits.Length + 1)];
                Array.Copy(allHits, physicsHits, allHits.Length);
                count = allHits.Length;
                if (!warnedHitCapacity)
                {
                    warnedHitCapacity = true;
                    Debug.LogWarning("[InteractionSystem] Physics hit buffer was full and has been expanded. Increase Max Physics Hits to avoid the initial allocation.", this);
                }
            }

            float closestSurfaceDistance = float.PositiveInfinity;
            float closestTargetDistance = float.PositiveInfinity;
            PointerHit closestSurface = PointerHit.None;
            PointerHit closestTarget = PointerHit.None;
            for (int i = 0; i < count; i++)
            {
                var physicsHit = physicsHits[i];
                if (!physicsHit.collider)
                    continue;
                var target = physicsHit.collider.GetComponentInParent<InteractableObject>();
                if (ignoredTarget && target == ignoredTarget)
                    continue;

                if (physicsHit.distance < closestSurfaceDistance)
                {
                    closestSurfaceDistance = physicsHit.distance;
                    closestSurface = PointerHit.ForPhysics(IsAvailable(target) ? target : null, physicsHit);
                }

                if (IsAvailable(target) && physicsHit.distance < closestTargetDistance)
                {
                    closestTargetDistance = physicsHit.distance;
                    closestTarget = PointerHit.ForPhysics(target, physicsHit);
                }
            }

            return nonInteractablePhysicsBlocks ? closestSurface :
                (closestTarget.Target ? closestTarget : closestSurface);
        }

        private void ValidateCapturedTargets()
        {
            if ((pressedTarget && !IsAvailable(pressedTarget)) || (draggedTarget && !IsAvailable(draggedTarget)))
                CancelCapture();
            else if (isDragging && !HasEnabledHandler(activeDragHandlers, InteractionCategories.Drag))
                CancelCapture();
            if (hoverHit.Target && !IsAvailable(hoverHit.Target))
                TransitionHover(PointerHit.None);
            if (dropTarget && !IsAvailable(dropTarget))
                ExitDropTarget(CreateContext(hoverHit), true);
        }

        internal void NotifyTargetUnavailable(InteractableObject target)
        {
            if (!target || isCanceling)
                return;
            interactionVersion++;
            if (captureTransition)
            {
                if (target == pressedTarget || target == draggedTarget || target == dropTarget || target == hoverHit.Target)
                    cancelRequested = true;
                return;
            }
            if (target == pressedTarget || target == draggedTarget)
                CancelCapture();
            if (target == dropTarget)
                ExitDropTarget(CreateContext(hoverHit), true);
            if (target == hoverHit.Target)
                TransitionHover(PointerHit.None);
        }

        public void CancelAllInteractions()
        {
            interactionVersion++;
            if (captureTransition)
            {
                cancelRequested = true;
                return;
            }
            if (isCanceling)
                return;
            isCanceling = true;
            try
            {
                CancelCapture();
                TransitionHover(PointerHit.None);
            }
            finally
            {
                isCanceling = false;
            }
        }

        private InteractionContext CreateContext(PointerHit hit, InteractableObject dropOverride = null)
        {
            return new InteractionContext(this, pointer, hit, pressedTarget, draggedTarget,
                dropOverride ? dropOverride : dropTarget);
        }

        private void EnsurePhysicsBuffer()
        {
            int capacity = Mathf.Max(InteractionSystemConsts.MinPhysicsHits, maxPhysicsHits);
            if (physicsHits == null || physicsHits.Length < capacity)
            {
                physicsHits = new RaycastHit[capacity];
                warnedHitCapacity = false;
            }
        }

        private static bool IsAvailable(InteractableObject target) => target && target.InteractionEnabled;

        public void SetBehaviourTypeEnabled<T>(bool enabled) where T : InteractionBehaviour => SetBehaviourTypeEnabled(typeof(T), enabled);

        public void SetBehaviourTypeEnabled(Type behaviourType, bool enabled)
        {
            if (behaviourType == null)
                throw new ArgumentNullException(nameof(behaviourType));
            if (!typeof(InteractionBehaviour).IsAssignableFrom(behaviourType))
                throw new ArgumentException($"{behaviourType.FullName} does not derive from {nameof(InteractionBehaviour)}.", nameof(behaviourType));

            if (enabled)
                disabledBehaviourTypes.Remove(behaviourType);
            else
                disabledBehaviourTypes.Add(behaviourType);
        }

        public bool IsBehaviourTypeEnabled(Type behaviourType) => behaviourType != null && !disabledBehaviourTypes.Contains(behaviourType);

        public void EnableBehaviourType(Type behaviourType) => SetBehaviourTypeEnabled(behaviourType, true);

        public void DisableBehaviourType(Type behaviourType) => SetBehaviourTypeEnabled(behaviourType, false);

        private bool HasEnabledHandler<T>(IReadOnlyList<T> handlers, InteractionCategories category) where T : class
        {
            for (int i = 0; i < handlers.Count; i++)
                if (IsHandlerEnabled(handlers[i], category))
                    return true;
            return false;
        }

        private void CollectEnabled<T>(IReadOnlyList<T> source, List<T> destination, InteractionCategories category) where T : class
        {
            destination.Clear();
            for (int i = 0; i < source.Count; i++)
                if (IsHandlerEnabled(source[i], category))
                    destination.Add(source[i]);
        }

        private static bool SameHandlers(List<IDropHandler> left, List<IDropHandler> right)
        {
            if (left.Count != right.Count)
                return false;
            for (int i = 0; i < left.Count; i++)
                if (!ReferenceEquals(left[i], right[i]))
                    return false;
            return true;
        }

        private enum HoverPhase { Enter, Stay, Exit }
        private enum ClickPhase { Down, Held, Up, Click, Cancel }
        private enum DragPhase { Begin, Move, End, Cancel }
        private enum DropPhase { Enter, Over, Drop, Exit }

        private bool IsHandlerEnabled(object handler, InteractionCategories category)
        {
            return InteractableObject.IsHandlerEnabled(handler, category) && !disabledBehaviourTypes.Contains(handler.GetType());
        }

        private bool CanDispatch(object handler, InteractionCategories category, bool includeDisabled, InteractableObject expectedTarget)
        {
            if (!InteractableObject.IsHandlerAlive(handler) || ((InteractionBehaviour)handler).Target != expectedTarget)
                return false;
            return includeDisabled || (isActiveAndEnabled && !cancelRequested && IsAvailable(((InteractionBehaviour)handler).Target) && IsHandlerEnabled(handler, category));
        }

        private void DispatchHover(IReadOnlyList<IHoverHandler> handlers, HoverPhase phase,
            in InteractionContext context, bool includeDisabled = false)
        {
            int version = interactionVersion;
            var snapshot = ListPool<IHoverHandler>.Get(handlers);
            if (phase == HoverPhase.Enter)
                activeHoverHandlers.Clear();
            try
            {
                foreach (var handler in snapshot)
                {
                    if (!includeDisabled && !CanContinue(version))
                        return;
                    if (!CanDispatch(handler, InteractionCategories.Hover, includeDisabled, context.HoveredTarget))
                        continue;
                    if (phase == HoverPhase.Enter)
                        activeHoverHandlers.Add(handler);
                    DispatchHoverSingle(handler, phase, context, includeDisabled);
                }
            }
            finally
            {
                ListPool<IHoverHandler>.Release(snapshot);
            }
        }

        private void DispatchHoverSingle(IHoverHandler handler, HoverPhase phase, in InteractionContext context, bool includeDisabled = false)
        {
            if (!CanDispatch(handler, InteractionCategories.Hover, includeDisabled, context.HoveredTarget))
                return;
            try
            {
                switch (phase)
                {
                    case HoverPhase.Enter: handler.OnHoverEnter(context); break;
                    case HoverPhase.Stay: handler.OnHoverStay(context); break;
                    case HoverPhase.Exit: handler.OnHoverExit(context); break;
                }
            }
            catch (Exception exception) { LogHandlerException(exception, handler); }
        }

        private void DispatchClick(IReadOnlyList<IClickHandler> handlers, ClickPhase phase, in InteractionContext context, bool releasedInside = false, bool includeDisabled = false)
        {
            int version = interactionVersion;
            var snapshot = ListPool<IClickHandler>.Get(handlers);
            if (phase == ClickPhase.Down)
                capturedClickHandlers.Clear();
            try
            {
                foreach (var handler in snapshot)
                {
                    if (!includeDisabled && !CanContinue(version))
                        return;
                    if (!CanDispatch(handler, InteractionCategories.Click, includeDisabled, context.PressedTarget))
                        continue;
                    if (phase == ClickPhase.Down)
                        capturedClickHandlers.Add(handler);
                    try
                    {
                        switch (phase)
                        {
                            case ClickPhase.Down: handler.OnPointerDown(context); break;
                            case ClickPhase.Held: handler.OnPointerHeld(context); break;
                            case ClickPhase.Up: handler.OnPointerUp(context, releasedInside); break;
                            case ClickPhase.Click: handler.OnClick(context); break;
                            case ClickPhase.Cancel: handler.OnPointerCanceled(context); break;
                        }
                    }
                    catch (Exception exception) { LogHandlerException(exception, handler); }
                }
            }
            finally
            {
                ListPool<IClickHandler>.Release(snapshot);
            }
        }

        private void DispatchDrag(IReadOnlyList<IDragHandler> handlers, DragPhase phase, in InteractionContext context, bool includeDisabled = false)
        {
            int version = interactionVersion;
            var snapshot = ListPool<IDragHandler>.Get(handlers);
            if (phase == DragPhase.Begin)
                activeDragHandlers.Clear();
            try
            {
                foreach (var handler in snapshot)
                {
                    if (!includeDisabled && !CanContinue(version))
                        return;
                    if (!CanDispatch(handler, InteractionCategories.Drag, includeDisabled, context.DraggedTarget))
                        continue;
                    if (phase == DragPhase.Begin)
                        activeDragHandlers.Add(handler);
                    try
                    {
                        switch (phase)
                        {
                            case DragPhase.Begin: handler.OnDragBegin(context); break;
                            case DragPhase.Move: handler.OnDrag(context); break;
                            case DragPhase.End: handler.OnDragEnd(context); break;
                            case DragPhase.Cancel: handler.OnDragCanceled(context); break;
                        }
                    }
                    catch (Exception exception) { LogHandlerException(exception, handler); }
                }
            }
            finally
            {
                ListPool<IDragHandler>.Release(snapshot);
            }
        }

        private void DispatchDrop(IReadOnlyList<IDropHandler> handlers, DropPhase phase, in InteractionContext context, bool includeDisabled = false)
        {
            int version = interactionVersion;
            var snapshot = ListPool<IDropHandler>.Get(handlers);
            if (phase == DropPhase.Enter)
                activeDropHandlers.Clear();
            try
            {
                foreach (var handler in snapshot)
                {
                    if (!includeDisabled && !CanContinue(version))
                        return;
                    if (!CanDispatch(handler, InteractionCategories.Drop, includeDisabled, context.DropTarget))
                        continue;
                    if (phase == DropPhase.Enter)
                        activeDropHandlers.Add(handler);
                    try
                    {
                        switch (phase)
                        {
                            case DropPhase.Enter: handler.OnDragEnter(context); break;
                            case DropPhase.Over: handler.OnDragOver(context); break;
                            case DropPhase.Drop: handler.OnDrop(context); break;
                            case DropPhase.Exit: handler.OnDragExit(context); break;
                        }
                    }
                    catch (Exception exception) { LogHandlerException(exception, handler); }
                }
            }
            finally
            {
                ListPool<IDropHandler>.Release(snapshot);
            }
        }

        private static bool CanAcceptDrop(IDropHandler handler, in InteractionContext context)
        {
            try { return handler.CanAcceptDrop(context); }
            catch (Exception exception)
            {
                LogHandlerException(exception, handler);
                return false;
            }
        }

        private static void LogHandlerException(Exception exception, object owner)
        {
            Debug.LogException(exception, (owner as InteractionBehaviour)?.Target);
        }

        private static class ListPool<T>
        {
            private static readonly Stack<List<T>> pool = new();
            public static List<T> Get() => pool.Count > 0 ? pool.Pop() : new List<T>(4);
            public static List<T> Get(IReadOnlyList<T> source)
            {
                var list = Get();
                for (int i = 0; i < source.Count; i++)
                    list.Add(source[i]);
                return list;
            }
            public static void Release(List<T> list)
            {
                list.Clear();
                pool.Push(list);
            }
        }
    }
}
