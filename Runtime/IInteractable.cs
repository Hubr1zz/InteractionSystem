namespace InteractionSystem.Runtime
{
    /// <summary>Receives pointer hover transitions for an interactable object.</summary>
    public interface IHoverHandler
    {
        void OnHoverEnter(in InteractionContext context);
        void OnHoverStay(in InteractionContext context);
        void OnHoverExit(in InteractionContext context);
    }

    /// <summary>Receives a captured mouse press. A drag automatically cancels the click.</summary>
    public interface IClickHandler
    {
        void OnPointerDown(in InteractionContext context);
        void OnPointerHeld(in InteractionContext context);
        void OnPointerUp(in InteractionContext context, bool releasedInside);
        void OnClick(in InteractionContext context);
        void OnPointerCanceled(in InteractionContext context);
    }

    /// <summary>Receives drag events from the object on which the press began.</summary>
    public interface IDragHandler
    {
        void OnDragBegin(in InteractionContext context);
        void OnDrag(in InteractionContext context);
        void OnDragEnd(in InteractionContext context);
        void OnDragCanceled(in InteractionContext context);
    }

    /// <summary>
    /// Receives a dragged object. Use context.DraggedTarget and its TryGetBehaviour/TryGetComponent
    /// helpers to communicate with the source without runtime reflection.
    /// </summary>
    public interface IDropHandler
    {
        bool CanAcceptDrop(in InteractionContext context);
        void OnDragEnter(in InteractionContext context);
        void OnDragOver(in InteractionContext context);
        void OnDragExit(in InteractionContext context);
        void OnDrop(in InteractionContext context);
    }
}
