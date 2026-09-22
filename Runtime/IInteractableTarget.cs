using System.Collections.Generic;
using UnityEngine;

namespace InteractionSystem.Runtime
{
    public interface IInteractableTarget
    {
        GameObject gameObject { get; }
        bool InteractionEnabled { get; }
        IReadOnlyList<IHoverHandler> HoverHandlers { get; }
        IReadOnlyList<IClickHandler> ClickHandlers { get; }
        IReadOnlyList<IDragHandler> DragHandlers { get; }
        IReadOnlyList<IDropHandler> DropHandlers { get; }
        bool TryGetBehaviour<T>(out T behaviour, bool includeDisabled = false) where T : class;
    }
}
