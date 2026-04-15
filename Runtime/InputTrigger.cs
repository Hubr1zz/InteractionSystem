using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;

namespace InteractionSystem.Runtime
{
    [System.Serializable]
    public enum InputType
    {
        Pressed,     // 按下瞬间
        Released,    // 松开瞬间
        Held,        // 按住
        Tapped,      // 点击 (快速按下松开)
        DoubleTap,   // 双击
        HoldTime,    // 长按
        Axis         // 轴向输入
    }
    [System.Serializable]
    public enum AxisCompare
    {
        Greater,     // 大于阈值
        Less,        // 小于负阈值
        Absolute     // 绝对值大于阈值
    }

    [Serializable]
    [HideReferenceObjectPicker][InlineProperty]
    public class ReferencedInputTrigger
    {
    
        [FoldoutGroup("Input Triggers")]
    
        [SerializeField]
        [FoldoutGroup("Input Triggers"), GUIColor(1f, 0.95f, 0.85f)][InlineProperty,LabelText("Pressed")]
        private InputType press = InputType.Pressed;
        [SerializeField]
        [FoldoutGroup("Input Triggers"), GUIColor(0.85f, 1f, 0.85f)][InlineProperty,LabelText("Held")]
        private InputType held = InputType.Held;
        [SerializeField]
        [FoldoutGroup("Input Triggers"), GUIColor(0.85f, 0.95f, 1f)][InlineProperty,LabelText("Released")]
        private InputType release = InputType.Released;
    
        [ShowIf("press", InputType.Axis)]
        [Range(0, 1)] public float axisThreshold = 0.5f;
        [ShowIf("press", InputType.Axis)]
        private AxisCompare axisCompare = AxisCompare.Greater;
    
        public bool IsTriggered(InputActionReference actionReference, InteractableThreeDBehaviour.InputSetting.TriggerType triggerType)
        {
            if (!actionReference || actionReference.action == null)
                return false;
        
            var action = actionReference.action;
            if (!action.enabled)
                action.Enable(); 
            InputType type = press;
            if (triggerType == InteractableThreeDBehaviour.InputSetting.TriggerType.Pressed)
                type = press;
            if(triggerType == InteractableThreeDBehaviour.InputSetting.TriggerType.Held)
                type = held; 
            if(triggerType == InteractableThreeDBehaviour.InputSetting.TriggerType.Released)
                type = release;
            switch (type)
            {
                case InputType.Pressed:
                    return action.WasPressedThisFrame();
                case InputType.Released:
                    return action.WasReleasedThisFrame();
                case InputType.Held:
                    return action.IsPressed();
                case InputType.Axis:
                    return axisCompare switch
                    {
                        AxisCompare.Greater => action.ReadValue<float>() > axisThreshold,          // 大于阈值
                        AxisCompare.Less => action.ReadValue<float>() < -axisThreshold,            // 小于负阈值
                        AxisCompare.Absolute => Mathf.Abs(action.ReadValue<float>()) > axisThreshold, // 绝对值大于阈值
                        _ => false
                    };
                case InputType.DoubleTap:
                    return triggerType switch
                    {
                        InteractableThreeDBehaviour.InputSetting.TriggerType.Held => action.IsPressed(),
                        InteractableThreeDBehaviour.InputSetting.TriggerType.Pressed => action.WasPressedThisFrame(),
                        InteractableThreeDBehaviour.InputSetting.TriggerType.Released => action.WasReleasedThisFrame(),
                        _ => action.WasPressedThisFrame()
                    };
                case InputType.HoldTime:
                    return triggerType switch
                    {
                        InteractableThreeDBehaviour.InputSetting.TriggerType.Held => action.IsPressed(),
                        InteractableThreeDBehaviour.InputSetting.TriggerType.Pressed => action.WasPressedThisFrame(),
                        InteractableThreeDBehaviour.InputSetting.TriggerType.Released => action.WasReleasedThisFrame(),
                        _ => action.WasPressedThisFrame()
                    };
       
                default:
                    return false;
            }
        }
    }

// 在Inspector中配置：
// 1. 将Input Action拖拽到actionReference字段
// 2. 选择triggerType
// 3. 调用IsTriggered()检查
}