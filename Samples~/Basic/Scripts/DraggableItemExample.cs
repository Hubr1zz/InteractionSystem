using InteractionSystem.Runtime;
using UnityEngine;

namespace InteractionSystem.Samples
{
    [System.Serializable]
    public sealed class DraggableItemExample : InteractionBehaviour, IHoverHandler, IClickHandler, IDragHandler
    {
        [SerializeField] private Renderer targetRenderer;
        [Tooltip("Use _BaseColor for URP Lit or _Color for the Built-in Standard shader.")]
        [SerializeField] private string colorProperty = "_BaseColor";
        [SerializeField] private Color hoverColor = Color.cyan;
        [SerializeField] private float surfaceOffset = 0.1f;

        private MaterialPropertyBlock properties;
        private Color originalColor;
        private int colorPropertyId;
        private Vector3 dragStart;

        public override void Initialize()
        {
            if (!targetRenderer)
                throw new System.InvalidOperationException("Assign Target Renderer on DraggableItemExample.");
            if (string.IsNullOrWhiteSpace(colorProperty) || !targetRenderer.sharedMaterial || !targetRenderer.sharedMaterial.HasProperty(colorProperty))
                throw new System.InvalidOperationException("Assign a material and a valid Color Property on DraggableItemExample.");

            colorPropertyId = Shader.PropertyToID(colorProperty);
            properties ??= new MaterialPropertyBlock();
            originalColor = targetRenderer.sharedMaterial.GetColor(colorPropertyId);
        }

        public void OnHoverEnter(in InteractionContext context) => SetColor(hoverColor);
        public void OnHoverStay(in InteractionContext context) { }
        public void OnHoverExit(in InteractionContext context) => SetColor(originalColor);

        public void OnPointerDown(in InteractionContext context) { }
        public void OnPointerHeld(in InteractionContext context) { }
        public void OnPointerUp(in InteractionContext context, bool releasedInside) { }
        public void OnClick(in InteractionContext context) => Debug.Log($"Clicked {Owner.name}", Owner);
        public void OnPointerCanceled(in InteractionContext context) { }

        public void OnDragBegin(in InteractionContext context) => dragStart = Transform.position;

        public void OnDrag(in InteractionContext context)
        {
            if (context.Hit.HasWorldHit)
                Transform.position = context.Hit.WorldHit.point + context.Hit.WorldHit.normal * surfaceOffset;
        }

        public void OnDragEnd(in InteractionContext context)
        {
            if (!context.DropTarget)
                Transform.position = dragStart;
        }

        public void OnDragCanceled(in InteractionContext context) => Transform.position = dragStart;

        private void SetColor(Color color)
        {
            targetRenderer.GetPropertyBlock(properties);
            properties.SetColor(colorPropertyId, color);
            targetRenderer.SetPropertyBlock(properties);
        }
    }
}
