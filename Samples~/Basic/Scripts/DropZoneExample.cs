using InteractionSystem.Runtime;
using UnityEngine;

namespace InteractionSystem.Samples
{
    [System.Serializable]
    public sealed class DropZoneExample : InteractionBehaviour, IDropHandler
    {
        [SerializeField] private Transform contentRoot;

        public override void Initialize()
        {
            if (!contentRoot)
                throw new System.InvalidOperationException("Assign Content Root on DropZoneExample.");
        }

        public bool CanAcceptDrop(in InteractionContext context)
        {
            return context.TryGetDraggedBehaviour<DraggableItemExample>(out var item) && contentRoot != item.Transform && !contentRoot.IsChildOf(item.Transform);
        }

        public void OnDragEnter(in InteractionContext context) { }
        public void OnDragOver(in InteractionContext context) { }
        public void OnDragExit(in InteractionContext context) { }

        public void OnDrop(in InteractionContext context)
        {
            if (!context.TryGetDraggedBehaviour<DraggableItemExample>(out var item))
                return;
            item.Transform.SetParent(contentRoot, true);
        }
    }
}
