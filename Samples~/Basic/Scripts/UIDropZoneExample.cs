using System;
using InteractionSystem.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace InteractionSystem.Samples
{
    [Serializable]
    public sealed class UIDropZoneExample : InteractionBehaviour, IDropHandler
    {
        [SerializeField] private Graphic targetGraphic;
        [SerializeField] private Text statusLabel;
        [SerializeField] private Transform dropPlacement;
        [SerializeField] private Color dragOverColor = Color.cyan;

        private Color originalColor;
        private string originalText;

        public override void Initialize()
        {
            if (!targetGraphic)
                throw new InvalidOperationException("Assign Target Graphic on UIDropZoneExample.");
            if (!statusLabel)
                throw new InvalidOperationException("Assign Status Label on UIDropZoneExample.");
            if (!dropPlacement)
                throw new InvalidOperationException("Assign Drop Placement on UIDropZoneExample.");

            originalColor = targetGraphic.color;
            originalText = statusLabel.text;
        }

        public override void Deinitialize()
        {
            if (targetGraphic)
                targetGraphic.color = originalColor;
            if (statusLabel)
                statusLabel.text = originalText;
        }

        public bool CanAcceptDrop(in InteractionContext context)
        {
            return context.TryGetDraggedBehaviour<DraggableItemExample>(out _);
        }

        public void OnDragEnter(in InteractionContext context)
        {
            targetGraphic.color = dragOverColor;
        }

        public void OnDragOver(in InteractionContext context) { }

        public void OnDragExit(in InteractionContext context)
        {
            targetGraphic.color = originalColor;
        }

        public void OnDrop(in InteractionContext context)
        {
            if (!context.TryGetDraggedBehaviour<DraggableItemExample>(out var item))
                return;

            item.Transform.position = dropPlacement.position;
            statusLabel.text = "Received: " + item.Owner.name;
        }
    }
}
