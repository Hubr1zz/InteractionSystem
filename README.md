[English](README.en.md) | 中文

# Mouse Interaction System

一个用于 Unity 2022.3+ 的鼠标交互包。它用单个 `InteractableObject` 承载多个 Odin 多态序列化 Behaviour，并通过一套确定的状态机统一处理 3D Collider 与 uGUI Graphic 之间的 Hover、Click、Drag、Drop。Odin Inspector 是必需依赖；新 Input System 不是必需依赖。

## 设计目标

- 开发者只需添加一个 `InteractableObject`，在同一列表中组合多个普通 C# Behaviour；不会为每种效果增加 MonoBehaviour 组件。
- Behaviour 可在不同宿主中复用类型和配置结构，并拥有独立总开关及 Hover/Click/Drag/Drop 分类开关。
- 鼠标按下后由原对象捕获；移出小物体也不会丢失点击或拖拽状态。
- 3D 与 UI 使用完全相同的事件顺序和上下文。
- 拖拽源和接收目标可通过 `InteractionContext` 查询彼此的 Behaviour 或 Component。
- 禁用、销毁、应用失焦、暂停和系统关闭都会产生确定的取消或退出回调。
- 热路径不使用 LINQ、泛型反射或 `MethodInfo.Invoke`。

## 安装

通过 Unity Package Manager 的“Add package from git URL”添加仓库地址，或在 `Packages/manifest.json` 中引用本地目录：

```json
"com.hubrizz.interaction-system": "file:D:/MyLibrary/InteractionSystem"
```

项目需要先安装 Odin Inspector 3.x。场景中只需要一个 `InteractionSystem`。默认使用 Unity 内置 Input Manager 的鼠标输入；包本身没有 `com.unity.inputsystem` 依赖。

在 Inspector 中显式设置 `Interaction Camera`（也可调用 `SetInteractionCamera`）。缺少引用时会报错，不会自动查找 Main Camera。纯 UI 场景可将 `Max Distance` 设为 0；uGUI 需要 EventSystem 和 GraphicRaycaster。选中系统对象时，Gizmos 显示相机中心射线的最大距离及末端球扫半径。

## 30 秒上手

在带 Collider 的 3D 物体或带 Raycast Target Graphic 的 UI 对象（或其父对象）上添加 `InteractableObject`，然后创建普通可序列化 Behaviour：

```csharp
using InteractionSystem.Runtime;
using UnityEngine;

[System.Serializable]
public sealed class CardInteraction : InteractionBehaviour,
    IHoverHandler, IClickHandler, IDragHandler
{
    public void OnHoverEnter(in InteractionContext c) { }
    public void OnHoverStay(in InteractionContext c) { }
    public void OnHoverExit(in InteractionContext c) { }

    public void OnPointerDown(in InteractionContext c) { }
    public void OnPointerHeld(in InteractionContext c) { }
    public void OnPointerUp(in InteractionContext c, bool releasedInside) { }
    public void OnClick(in InteractionContext c) => Debug.Log("Clicked");
    public void OnPointerCanceled(in InteractionContext c) { }

    public void OnDragBegin(in InteractionContext c) { }
    public void OnDrag(in InteractionContext c)
    {
        if (c.Hit.HasWorldHit)
            Transform.position = c.Hit.WorldHit.point;
    }
    public void OnDragEnd(in InteractionContext c) { }
    public void OnDragCanceled(in InteractionContext c) { }
}
```

接收对象实现 `IDropHandler`：

```csharp
[System.Serializable]
public sealed class CardSlot : InteractionBehaviour, IDropHandler
{
    public bool CanAcceptDrop(in InteractionContext c) =>
        c.TryGetDraggedBehaviour<CardInteraction>(out _);

    public void OnDragEnter(in InteractionContext c) { }
    public void OnDragOver(in InteractionContext c) { }
    public void OnDragExit(in InteractionContext c) { }

    public void OnDrop(in InteractionContext c)
    {
        if (c.TryGetDraggedBehaviour<CardInteraction>(out var card))
            card.Transform.SetParent(Transform, true);
    }
}
```

## 接口与事件语义

| 接口 | 用途 | 顺序保证 |
|---|---|---|
| `IHoverHandler` | 光标进入、停留、离开 | Enter → 0..N Stay → Exit；每次转换只触发一次 |
| `IClickHandler` | 按下、按住、释放、有效点击 | Down → 0..N Held → Up → Click；外部释放没有 Click |
| `IDragHandler` | 从按下对象开始拖拽 | DragBegin → 1..N Drag → DragEnd；拖拽开始时 Click 收到 Canceled |
| `IDropHandler` | 接收其他对象 | DragEnter → 1..N DragOver → Drop → DragExit |

鼠标移动超过 `InteractionSystem.dragThreshold` 后才开始拖拽。拖拽一旦开始，点击序列被取消。系统只向开始时已启用的参与者发送终止回调，因此即使处理脚本中途被禁用，也有机会清理状态。

回调中可以禁用对象、调用 `CancelAllInteractions()` 或增删 Behaviour。取消后停止当前帧的后续交互；未收到 Enter/Down/DragBegin 的处理器不会收到对应终止事件。若在 PointerUp 中取消或禁用目标，不再发送 Click。已经进入终止阶段的序列会完成清理，不重复发送 Canceled；已被移除并 Deinitialize 的 Behaviour 不再接收事件。

`InteractionContext` 提供：

- 当前鼠标帧 `Pointer`
- 当前 UI/物理命中 `Hit`
- `PressedTarget`、`DraggedTarget`、`DropTarget`
- `TryGetHoveredBehaviour<T>`、`TryGetDraggedBehaviour<T>`、`TryGetDropBehaviour<T>`
- 对应的 `TryGet*Component<T>`，用于 Rigidbody 等普通 Component

`PointerHit.HasWorldHit` 即使命中普通地面、而不是交互目标，也会为真，因此拖拽逻辑可以使用地面位置和法线。

## 输入系统选择

默认输入源使用 `UnityEngine.Input`，适合 Input Manager 或 Both 模式。若项目只启用新 Input System，实现并注入一个很小的适配器即可；包无需因此依赖 Input System：

```csharp
using InteractionSystem.Runtime;
using UnityEngine.InputSystem;

public sealed class InputSystemMouseSource : IPointerInputSource
{
    public bool TryGetFrame(int buttonIndex, out PointerFrame frame)
    {
        var mouse = Mouse.current;
        if (mouse == null) { frame = default; return false; }
        var button = buttonIndex == 1 ? mouse.rightButton :
            buttonIndex == 2 ? mouse.middleButton : mouse.leftButton;
        frame = new PointerFrame(mouse.position.ReadValue(), button.wasPressedThisFrame,
            button.isPressed, button.wasReleasedThisFrame);
        return true;
    }
}

// Awake 中：
// interactionSystem.SetInputSource(new InputSystemMouseSource());
```

测试、回放、网络指针也可以通过同一接口接入。

`TryGetFrame` 返回 false 表示输入不可用，系统会取消捕获并退出悬停。若按键已松开但没有收到释放边沿，则取消捕获，避免输入设备断开或漏帧后残留拖拽状态。

在 `InteractableObject` 的 Behaviours 列表中用 Odin 类型选择器添加这些类型。每个列表元素顶部的 `↗ 类型名` 按钮会打开该 Behaviour 的 C# 脚本；查找会验证 `MonoScript.GetClass()`，不会误跳到同名类型。

运行时可以使用 `AddBehaviour`、`RemoveBehaviour` 与 `TryGetBehaviour<T>`。`InteractionSystem.SetBehaviourTypeEnabled<T>(false)` 可以按具体 Behaviour 类型进行全局禁用。添加、移除或禁用正在参与交互的 Behaviour 时，状态机会先完成取消清理。

`Initialize`、`Deinitialize` 和列表刷新清理期间，不支持再次修改同一宿主的 Behaviour 列表：`AddBehaviour` / `RemoveBehaviour` 返回 false，嵌套 `RefreshBehaviours` 被忽略。请在这些生命周期之外完成模块组合，并检查增删方法的返回值。

## 目标解析规则

- Collider 或 UI Graphic 可以放在 `InteractableObject` 的子节点，系统会向父级解析目标。
- 拖拽时会跳过拖拽源的全部子 Collider/UI Graphic，不会自遮挡。
- 屏幕空间 UI 默认优先于物理对象；世界空间 UI 按射线距离与 Collider 比较。
- 非交互 UI 默认阻挡位于其后方的交互对象。
- 非交互 Collider 默认阻挡后方交互对象，避免穿墙点击。
- 可通过 LayerMask、最大距离、Trigger 策略和命中缓冲区控制物理查询。
- `Max Physics Hits` 是初始缓冲容量。容量满时获取完整结果并扩容，输出一次警告；之后复用扩容结果，避免漏掉最近物体。原因见 [Unity RaycastNonAlloc 文档](https://docs.unity3d.com/ja/2023.2/ScriptReference/Physics.RaycastNonAlloc.html)。
- UI 路径仅接受 GraphicRaycaster 结果，场景中的 PhysicsRaycaster 不会绕过系统自身的物理 LayerMask 和遮挡规则。
- Behaviour 固定由最近的 `InteractableObject` 宿主管理；Collider 和 Graphic 可以位于它的子节点。

## 从旧版迁移

1. 旧 Behaviour 改为继承 `InteractionBehaviour`，保留 `[System.Serializable]`，并把 `Initialize()` 中的宿主访问改为 `Owner` 或 `Transform`。
2. 将 `IFocusable` 改为 `IHoverHandler`，`IClickable` 改为 `IClickHandler`。
3. 将 `IDraggable<T>` 拆为源上的 `IDragHandler` 与目标上的 `IDropHandler`，使用 `context.TryGetDraggedBehaviour<T>()` 显式匹配。
4. 删除每个 Behaviour 的 `InputSettings`；鼠标按钮与输入源只在系统上配置一次。
5. 3D 和 UI 都使用 `InteractableObject`；`InteractableUIElement` 仅为兼容别名。

由于旧版列表元素基类和事件接口已经变化，旧场景中的序列化数据应在备份上逐项迁移，不能假设 Odin 会自动把旧类型转换为新类型。

## 测试

包内 PlayMode 测试覆盖：

- Hover 转换只派发一次且顺序正确
- 按下后移出仍从原对象开始拖拽
- 外部释放不会误触 Click
- 拖拽中禁用目标会且只会取消一次
- 单独禁用拖拽处理器也能正确取消
- 终止回调中禁用目标不会产生重复取消
- 普通 Collider 仍向拖拽源提供世界命中
- uGUI Graphic 使用同一套 Hover 状态机
- 多物体 Drag/Drop 的 Enter、Over、Drop、Exit 平衡顺序
- 普通 Collider / Graphic 遮挡、PhysicsRaycaster 隔离与满缓冲区最近目标选择
- 系统重新启用、输入中断、丢失释放边沿时的清理
- 回调中取消、禁用、移除 Behaviour，及生命周期刷新重入

在 Unity Test Framework 中启用本包的 tests，或使用命令行运行 `InteractionSystem.Runtime.Tests`。

## 示例入口

从 Package Manager 导入 Basic sample 后，打开 `Scenes/BasicDragAndDrop.unity` 即可运行拖拽示例；README 中也保留了手动搭建步骤。场景和 UI 都是持久化资产，不依赖运行时生成器。
