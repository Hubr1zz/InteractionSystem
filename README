# EasyInteractive
一个统一处理 **3D 物体** 和 **UI 元素**交互事件的系统，支持聚焦（Focus）、点击（Click）、拖拽（Drag）三种核心交互。
**并未完全测试过，目前基本功能可以正常使用**
**建议配合Odin一起使用。不然配置起来比较麻烦**
---

## 核心思路

系统的核心是把"交互逻辑"从 MonoBehaviour 里抽出来，做成独立的 **Behaviour 类**挂到容器上。

```
EasyInteractiveNew（单例）
    ↓ 每帧 Raycast / EventSystem 检测
IInteractableTarget（统一接口）
    ├── InteractableObject      → 3D 物体容器（Physics Raycast）
    └── InteractableUIElement   → UI 容器（EventSystem）
            ↓ 持有多个
    InteractableBehaviourBase
        ├── InteractableThreeDBehaviour  → 3D 行为（含 InputSettings）
        └── InteractableUIBehaviour      → UI 行为（EventSystem 驱动）
```

EasyInteractive 只认 `IInteractableTarget`，不关心来源是物理射线还是 UI 事件，**3D 和 UI 走同一套派发逻辑**。

---

## 支持的交互

### Focus（聚焦）
鼠标悬停在物体上时触发。

| 接口 | 说明 |
|------|------|
| `IFocusable` | 普通聚焦，鼠标进入/停留/离开时触发 |
| `IFocusable<T>` | 拖拽感知聚焦，只在**特定拖拽物悬停**时触发，可获取拖拽物数据 |

### Click（点击）
鼠标按下到松开的完整点击流程。

| 回调 | 时机 |
|------|------|
| `OnBeginClick` | 按下瞬间 |
| `OnPressing` | 持续按住 |
| `OnClickReleasedInside` | 在物体上松开（有效点击） |
| `OnClickReleasedOutside` | 在物体外松开 |
| `OnMouseOutWhilePressing` | 按住时鼠标移出 |
| `OnMouseEnterWhilePressing` | 按住时鼠标移回 |

### Drag（拖拽）

| 接口 | 说明 |
|------|------|
| `IDraggable` | 普通拖拽，无目标上下文 |
| `IDraggable<T>` | 有目标的拖拽，拖到**特定接收者**上时可获取接收者数据 |

拖拽中还会持续收到无目标时的 `OnDraggingWithoutTarget(RaycastHit?)` 回调，以便自行处理移动逻辑。

---

## 泛型接口：两物体间的数据沟通

`IDraggable<T>` 和 `IFocusable<T>` 是为了处理**两个特定类型对象交互时的数据传递**，比如拖拽一张牌放到卡槽上，需要互相知道对方是谁。

- **T 可以是另一个 Behaviour 类型**，也可以是任意 Component
- 系统在启动时通过反射扫描所有程序集，**自动缓存**泛型方法的调用映射，运行时零反射开销

---

## 关键机制

### Behaviour 的全局开关
每个 Behaviour 类型都有一个全局启用状态（`_behaviourState`），可以整批开关某类行为：

```csharp
EasyInteractiveNew.Instance.DisableBehaviourType(typeof(MyDragBehaviour));
```

### 3D 输入（InputSettings）
`InteractableThreeDBehaviour` 支持配置多组 `InputAction`，每帧轮询是否触发。  
点击/拖拽的"按下"/"持续"/"松开"分别对应 `Pressed / Held / Released`，可灵活绑定任意按键。

### UI 输入
`InteractableUIElement` 实现了 Unity EventSystem 的各类接口（`IPointerEnterHandler` 等），收到事件后直接转发给 `EasyInteractiveNew`，与 3D 走相同的派发路径。

### Raycast 策略
- 优先普通 `RaycastNonAlloc`
- 无命中时自动退回 `SphereCastNonAlloc`（可配置半径）
- 命中排序后，**跳过正在被拖拽的物体自身**，取下一个作为聚焦目标，实现"拖着东西悬停到目标上"的场景

---

## 快速上手

### 场景一：可点击的 3D 物体

```csharp
// 1. 创建 Behaviour
public class MyClickBehaviour : InteractableThreeDBehaviour, IClickable
{
    public void OnBeginClick()             => Debug.Log("按下");
    public void OnPressing()               { }
    public void OnClickReleasedInside()    => Debug.Log("点击完成！");
    public void OnClickReleasedOutside()   { }
    public void OnMouseOutWhilePressing()  { }
    public void OnMouseEnterWhilePressing(){ }
}

// 2. 在继承 InteractableObject 的 MonoBehaviour 上，
//    Inspector 里把 MyClickBehaviour 加入 _interactableBehaviours 列表即可
```

### 场景二：拖拽卡牌放到卡槽（泛型交互）

```csharp
// 卡牌：拖到 SlotBehaviour 时收到回调
public class CardDragBehaviour : InteractableThreeDBehaviour, IDraggable<SlotBehaviour>
{
    public void OnBeginDrag() => Debug.Log("开始拖拽");
    public void OnDragEnterTarget(SlotBehaviour slot, RaycastHit hit) => Debug.Log($"悬停到槽 {slot}");
    public void OnDragReleasedOnTarget(SlotBehaviour slot, RaycastHit hit) => Debug.Log("放入槽！");
    public void OnDragLeaveTarget(SlotBehaviour slot, RaycastHit hit) { }
    public void OnDragStayOnTarget(SlotBehaviour slot, RaycastHit hit) { }
    public void OnDraggingWithoutTarget(RaycastHit? hit) { /* 更新位置 */ }
    public void OnDragReleaseWithoutTarget(RaycastHit? hit) => Debug.Log("放到空处");
    public void OnMouseOutWhileDragging()  { }
    public void OnMouseInWhileDragging()   { }
    public void OnMouseStayWhileDragging() { }
}

// 卡槽：在卡牌悬停时高亮
public class SlotBehaviour : InteractableThreeDBehaviour, IFocusable<CardDragBehaviour>
{
    public void OnDraggedObjectEnter(CardDragBehaviour card)    => Highlight(true);
    public void OnDraggedObjectLeave(CardDragBehaviour card)    => Highlight(false);
    public void OnDraggedObjectReleased(CardDragBehaviour card) => AcceptCard(card);
    public void OnDraggedObjectStay(CardDragBehaviour card)     { }
    public void OnMouseEnterWithoutTarget() { }
    public void OnMouseOutWithoutTarget()   { }
    public void OnMouseStayWithoutTarget()  { }
}
```

### 场景三：UI 按钮

```csharp
// UI Behaviour 不需要 InputSettings，事件由 EventSystem 驱动
public class UIButtonBehaviour : InteractableUIBehaviour, IClickable
{
    public void OnBeginClick()          => Debug.Log("UI 按下");
    public void OnClickReleasedInside() => Debug.Log("UI 点击！");
    // ... 其余接口
}
// 挂载 InteractableUIElement 到 UI GameObject，添加此 Behaviour 即可
```

---

## 场景配置

1. 场景中放一个空物体，挂上 `EasyInteractiveNew`
2. 3D 物体：继承 `InteractableObject`，在 Inspector 添加 Behaviour
3. UI 元素：挂 `InteractableUIElement`，在 Inspector 添加 Behaviour
4. 确保场景有 `EventSystem`（UI 支持必须）
