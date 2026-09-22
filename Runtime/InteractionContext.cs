using UnityEngine;

namespace InteractionSystem.Runtime
{
    public readonly struct PointerFrame
    {
        public readonly Vector2 ScreenPosition;
        public readonly bool PressedThisFrame;
        public readonly bool IsPressed;
        public readonly bool ReleasedThisFrame;

        public PointerFrame(Vector2 screenPosition, bool pressedThisFrame, bool isPressed, bool releasedThisFrame)
        {
            ScreenPosition = screenPosition;
            PressedThisFrame = pressedThisFrame;
            IsPressed = isPressed;
            ReleasedThisFrame = releasedThisFrame;
        }
    }

    public readonly struct PointerHit
    {
        public static PointerHit None => default;

        public readonly InteractableObject Target;
        public readonly GameObject SurfaceObject;
        public readonly bool IsUI;
        public readonly bool IsScreenSpaceUI;
        public readonly bool HasWorldHit;
        public readonly RaycastHit WorldHit;
        public readonly float Distance;

        private PointerHit(InteractableObject target, GameObject surfaceObject, bool isUI, bool isScreenSpaceUI,
            bool hasWorldHit, RaycastHit worldHit, float distance)
        {
            Target = target;
            SurfaceObject = surfaceObject;
            IsUI = isUI;
            IsScreenSpaceUI = isScreenSpaceUI;
            HasWorldHit = hasWorldHit;
            WorldHit = worldHit;
            Distance = distance;
        }

        public static PointerHit ForTarget(InteractableObject target)
        {
            return target ? new PointerHit(target, target.gameObject, false, false, false, default, 0f) : None;
        }

        internal static PointerHit ForUI(InteractableObject target, GameObject surfaceObject,
            float distance, bool isScreenSpace)
        {
            return new PointerHit(target, surfaceObject, true, isScreenSpace, false, default, distance);
        }

        internal static PointerHit ForPhysics(InteractableObject target, RaycastHit hit)
        {
            return new PointerHit(target, hit.collider ? hit.collider.gameObject : null,
                false, false, true, hit, hit.distance);
        }
    }

    public readonly struct InteractionContext
    {
        public readonly InteractionSystem Dispatcher;
        public readonly PointerFrame Pointer;
        public readonly PointerHit Hit;
        public readonly InteractableObject PressedTarget;
        public readonly InteractableObject DraggedTarget;
        public readonly InteractableObject DropTarget;

        public InteractableObject HoveredTarget => Hit.Target;

        internal InteractionContext(InteractionSystem dispatcher, PointerFrame pointer, PointerHit hit,
            InteractableObject pressedTarget, InteractableObject draggedTarget, InteractableObject dropTarget)
        {
            Dispatcher = dispatcher;
            Pointer = pointer;
            Hit = hit;
            PressedTarget = pressedTarget;
            DraggedTarget = draggedTarget;
            DropTarget = dropTarget;
        }

        public bool TryGetHoveredBehaviour<T>(out T behaviour, bool includeDisabled = false) where T : class
        {
            behaviour = null;
            return HoveredTarget && HoveredTarget.TryGetBehaviour(out behaviour, includeDisabled);
        }

        public bool TryGetDraggedBehaviour<T>(out T behaviour, bool includeDisabled = false) where T : class
        {
            behaviour = null;
            return DraggedTarget && DraggedTarget.TryGetBehaviour(out behaviour, includeDisabled);
        }

        public bool TryGetDropBehaviour<T>(out T behaviour, bool includeDisabled = false) where T : class
        {
            behaviour = null;
            return DropTarget && DropTarget.TryGetBehaviour(out behaviour, includeDisabled);
        }

        public bool TryGetHoveredComponent<T>(out T component, bool includeChildren = false) where T : Component
        {
            component = null;
            return HoveredTarget && HoveredTarget.TryGetComponentOnTarget(out component, includeChildren);
        }

        public bool TryGetDraggedComponent<T>(out T component, bool includeChildren = false) where T : Component
        {
            component = null;
            return DraggedTarget && DraggedTarget.TryGetComponentOnTarget(out component, includeChildren);
        }

        public bool TryGetDropComponent<T>(out T component, bool includeChildren = false) where T : Component
        {
            component = null;
            return DropTarget && DropTarget.TryGetComponentOnTarget(out component, includeChildren);
        }
    }

    /// <summary>Pluggable mouse input. The package itself has no Input System dependency.</summary>
    public interface IPointerInputSource
    {
        bool TryGetFrame(int mouseButton, out PointerFrame frame);
    }

    public sealed class BuiltInMouseInputSource : IPointerInputSource
    {
        private bool warnedUnavailable;

        public bool TryGetFrame(int mouseButton, out PointerFrame frame)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            frame = new PointerFrame(Input.mousePosition, Input.GetMouseButtonDown(mouseButton),
                Input.GetMouseButton(mouseButton), Input.GetMouseButtonUp(mouseButton));
            return true;
#else
            frame = default;
            if (!warnedUnavailable)
            {
                warnedUnavailable = true;
                Debug.LogError("[InteractionSystem] The built-in Input Manager is disabled. " +
                    "Inject an IPointerInputSource with InteractionSystem.SetInputSource().");
            }
            return false;
#endif
        }
    }
}
