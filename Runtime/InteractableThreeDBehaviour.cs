using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;

namespace InteractionSystem.Runtime
{
    /// <summary>
    /// Base class for 3D interactable behaviours.
    /// Extends InteractableBehaviourBase with InputSettings for polling-based input.
    /// </summary>
    public abstract class InteractableThreeDBehaviour : InteractableBehaviourBase
    {
        [HideReferenceObjectPicker]
        [InlineProperty]
        public sealed class InputSetting
        {
            public enum TriggerType
            {
                Pressed,
                Released,
                Held,
            }

            [SerializeField][InlineProperty, LabelText("InputAction")][Required]
            internal InputActionReference actionReference = null;

            [SerializeField][HideReferenceObjectPicker]
            private ReferencedInputTrigger triggers = new ReferencedInputTrigger();

            public bool IsTriggered(TriggerType type)
            {
                return triggers.IsTriggered(actionReference, type);
            }
        }

        [FoldoutGroup("Behaviour Settings")]
        [HideReferenceObjectPicker]
        [ListDrawerSettings(ShowItemCount = true, DraggableItems = true)]
        [PropertyOrder(-13)]
        [ValidateInput("@$value != null && $value.Count > 0", "InputSettings is null!", InfoMessageType.Warning)]
        public List<InputSetting> InputSettings = new List<InputSetting>();

        public override void Initialize()
        {
            foreach (var setting in InputSettings)
                setting.actionReference?.action?.Enable();
        }

        [PropertyOrder(-10)]
        [OnInspectorGUI]
        private void DrawBottomInfo()
        {
            Sirenix.Utilities.Editor.SirenixEditorGUI.HorizontalLineSeparator(Color.white, 1);
        }
    }
}