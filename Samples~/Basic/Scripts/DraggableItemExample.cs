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
        [Tooltip("Transparent material used only while dragging. The normal scene material remains opaque and depth-sorted.")]
        [SerializeField] private Material dragFeedbackMaterial;
        [SerializeField] private Color hoverColor = Color.cyan;
        [SerializeField] private float surfaceOffset = 0.1f;
        [Tooltip("Colour used while dragging over a location that cannot accept the item.")]
        [SerializeField] private Color invalidDragColor = new Color(1f, 0f, 0f, 0.45f);
        [Tooltip("Colour used while dragging over a valid drop target.")]
        [SerializeField] private Color validDragColor = new Color(0f, 1f, 0f, 0.45f);

        private MaterialPropertyBlock properties;
        private Material originalMaterial;
        private Color originalColor;
        private int colorPropertyId;
        private Vector3 dragStart;
        private Transform initialParent;
        private Vector3 initialLocalPosition;
        private Quaternion initialLocalRotation;
        private Vector3 initialLocalScale;
        private bool initialTransformCaptured;
        private bool dragging;
        private bool placed;

        public override void Initialize()
        {
            if (!targetRenderer)
                throw new System.InvalidOperationException("Assign Target Renderer on DraggableItemExample.");
            if (string.IsNullOrWhiteSpace(colorProperty) || !targetRenderer.sharedMaterial || !targetRenderer.sharedMaterial.HasProperty(colorProperty))
                throw new System.InvalidOperationException("Assign a material and a valid Color Property on DraggableItemExample.");
            if (!dragFeedbackMaterial || !dragFeedbackMaterial.HasProperty(colorProperty))
                throw new System.InvalidOperationException("Assign a transparent Drag Feedback Material with the configured Color Property on DraggableItemExample.");

            colorPropertyId = Shader.PropertyToID(colorProperty);
            properties ??= new MaterialPropertyBlock();
            if (!initialTransformCaptured)
            {
                originalMaterial = targetRenderer.sharedMaterial;
                originalColor = originalMaterial.GetColor(colorPropertyId);
                initialParent = Transform.parent;
                initialLocalPosition = Transform.localPosition;
                initialLocalRotation = Transform.localRotation;
                initialLocalScale = Transform.localScale;
                initialTransformCaptured = true;
                placed = false;
            }
            dragging = false;
            RestoreOriginalAppearance();
        }

        public override void Deinitialize()
        {
            dragging = false;
            RestoreOriginalAppearance();
        }

        public void OnHoverEnter(in InteractionContext context)
        {
            if (dragging)
                return;
            SetColor(hoverColor);
        }

        public void OnHoverStay(in InteractionContext context) { }

        public void OnHoverExit(in InteractionContext context)
        {
            if (dragging)
                return;
            SetColor(originalColor);
        }

        public void OnPointerDown(in InteractionContext context) { }
        public void OnPointerHeld(in InteractionContext context) { }
        public void OnPointerUp(in InteractionContext context, bool releasedInside) { }

        public void OnClick(in InteractionContext context)
        {
            if (placed)
                RestoreInitialTransform();
            Debug.Log($"Clicked {Owner.name}", Owner);
        }

        public void OnPointerCanceled(in InteractionContext context) { }

        public void OnDragBegin(in InteractionContext context)
        {
            dragStart = Transform.position;
            dragging = true;
            targetRenderer.sharedMaterial = dragFeedbackMaterial;
            SetColor(invalidDragColor);
        }

        public void OnDrag(in InteractionContext context)
        {
            SetColor(context.DropTarget ? validDragColor : invalidDragColor);
            if (!context.Hit.HasWorldHit)
                return;
            Transform.position = context.Hit.WorldHit.point + context.Hit.WorldHit.normal * surfaceOffset;
        }

        public void OnDragEnd(in InteractionContext context)
        {
            if (context.DropTarget)
                placed = true;
            else
                Transform.position = dragStart;
            dragging = false;
            RestoreOriginalAppearance();
        }

        public void OnDragCanceled(in InteractionContext context)
        {
            Transform.position = dragStart;
            dragging = false;
            RestoreOriginalAppearance();
        }

        private void RestoreInitialTransform()
        {
            Transform.SetParent(initialParent, false);
            Transform.localPosition = initialLocalPosition;
            Transform.localRotation = initialLocalRotation;
            Transform.localScale = initialLocalScale;
            placed = false;
        }

        private void SetColor(Color color)
        {
            targetRenderer.GetPropertyBlock(properties);
            properties.SetColor(colorPropertyId, color);
            targetRenderer.SetPropertyBlock(properties);
        }

        private void RestoreOriginalAppearance()
        {
            if (!targetRenderer || !originalMaterial)
                return;
            targetRenderer.sharedMaterial = originalMaterial;
            SetColor(originalColor);
        }
    }
}
