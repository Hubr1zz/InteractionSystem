using System;
using InteractionSystem.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace InteractionSystem.Samples
{
    [Serializable]
    public sealed class ClickableUIExample : InteractionBehaviour, IHoverHandler, IClickHandler
    {
        [SerializeField] private Graphic targetGraphic;
        [SerializeField] private Color hoverColor = Color.cyan;
        [SerializeField] private Color selectedColor = Color.green;

        private Color originalColor;
        private bool hovered;
        private bool selected;

        public override void Initialize()
        {
            if (!targetGraphic)
                throw new InvalidOperationException("Assign Target Graphic on ClickableUIExample.");
            originalColor = targetGraphic.color;
        }

        public override void Deinitialize()
        {
            if (targetGraphic)
                targetGraphic.color = originalColor;
            hovered = false;
            selected = false;
        }

        public void OnHoverEnter(in InteractionContext context)
        {
            hovered = true;
            targetGraphic.color = hoverColor;
        }

        public void OnHoverStay(in InteractionContext context) { }

        public void OnHoverExit(in InteractionContext context)
        {
            hovered = false;
            targetGraphic.color = selected ? selectedColor : originalColor;
        }

        public void OnPointerDown(in InteractionContext context) { }
        public void OnPointerHeld(in InteractionContext context) { }
        public void OnPointerUp(in InteractionContext context, bool releasedInside) { }

        public void OnClick(in InteractionContext context)
        {
            selected = !selected;
            if (hovered)
                targetGraphic.color = hoverColor;
            else if (selected)
                targetGraphic.color = selectedColor;
            else
                targetGraphic.color = originalColor;
        }

        public void OnPointerCanceled(in InteractionContext context) { }
    }
}
