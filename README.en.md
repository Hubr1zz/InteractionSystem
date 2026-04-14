English | \[中文](README.md)



\# Interaction System

A unified system for handling interaction events on \*\*3D objects\*\* and \*\*UI elements\*\*, supporting three core interaction types: Focus, Click, and Drag.



\*\*Not fully tested — basic functionality works.\*\*



\*\*Recommended to use with Odin Inspector. Setup without it can be tedious.\*\*



\---

\*\*Note: Odin is included in this repo. Please support the official license if you use it commercially or are able to.\*\*



\---



\## How It Works



The core idea is to extract interaction logic out of MonoBehaviours into standalone \*\*Behaviour classes\*\* attached to container components.



```

InteractionSystem (singleton)

&#x20;   ↓ Raycast / EventSystem polling each frame

IInteractableTarget (unified interface)

&#x20;   ├── InteractableObject      → 3D container (Physics Raycast)

&#x20;   └── InteractableUIElement   → UI container (EventSystem)

&#x20;           ↓ holds multiple

&#x20;   InteractableBehaviourBase

&#x20;       ├── InteractableThreeDBehaviour  → 3D behaviour (with InputSettings)

&#x20;       └── InteractableUIBehaviour      → UI behaviour (EventSystem-driven)

```



`InteractionSystem` only cares about `IInteractableTarget` — it doesn't matter whether the source is a physics raycast or a UI event. \*\*3D and UI share the same dispatch pipeline.\*\*



\---



\## Supported Interactions



\### Focus

Triggered when the mouse hovers over an object.



| Interface | Description |

|-----------|-------------|

| `IFocusable` | Plain focus — triggers on mouse enter / stay / exit |

| `IFocusable<T>` | Drag-aware focus — only triggers when a \*\*specific draggable type\*\* hovers over it; provides dragged object data |



\### Click

Full click lifecycle from mouse down to release.



| Callback | When |

|----------|------|

| `OnBeginClick` | Mouse button pressed |

| `OnPressing` | Held down each frame |

| `OnClickReleasedInside` | Released while still over the object (valid click) |

| `OnClickReleasedOutside` | Released outside the object |

| `OnMouseOutWhilePressing` | Mouse moves off while held |

| `OnMouseEnterWhilePressing` | Mouse moves back on while held |



\### Drag



| Interface | Description |

|-----------|-------------|

| `IDraggable` | Plain drag with no target context |

| `IDraggable<T>` | Targeted drag — provides receiver data when dragged onto a \*\*specific target type\*\* |



`OnDraggingWithoutTarget(RaycastHit?)` is called every frame while dragging with no valid target, so you can handle movement yourself.



\---



\## Generic Interfaces: Communication Between Two Objects



`IDraggable<T>` and `IFocusable<T>` handle \*\*data passing between two specific interacting objects\*\* — for example, dragging a card onto a slot where each needs to know about the other.



\- \*\*T can be another Behaviour type\*\* or any Component

\- At startup, the system uses reflection to scan all assemblies and \*\*pre-caches\*\* generic method mappings — zero reflection overhead at runtime



\---



\## Key Mechanisms



\### Global Behaviour Toggle

Each Behaviour type has a global enabled state (`\_behaviourState`). You can enable/disable an entire class of behaviours at once:



```csharp

InteractionSystem.Instance.DisableBehaviourType(typeof(MyDragBehaviour));

```

Each behaviour also has a local enabled state that belongs to the instance.



\### 3D Input (InputSettings)

`InteractableThreeDBehaviour` supports multiple `InputAction` bindings, polled each frame.  

`Pressed / Held / Released` maps to click/drag begin, hold, and release — bind any key you want.



\### UI Input

`InteractableUIElement` implements Unity's EventSystem interfaces (`IPointerEnterHandler`, etc.) and forwards events directly to `InteractionSystem`, going through the same dispatch path as 3D objects.



\### Raycast Strategy

\- Tries `RaycastNonAlloc` first

\- Falls back to `SphereCastNonAlloc` (configurable radius) if nothing is hit

\- Hits are sorted by distance; the \*\*currently dragged object is skipped\*\*, so the next object becomes the focus target — enabling "drag object and hover over a receiver" scenarios



\---



\## Quick Start



\### Example 1: Clickable 3D Object



```csharp

// 1. Create a Behaviour

public class MyClickBehaviour : InteractableThreeDBehaviour, IClickable

{

&#x20;   public void OnBeginClick()             => Debug.Log("Pressed");

&#x20;   public void OnPressing()               { }

&#x20;   public void OnClickReleasedInside()    => Debug.Log("Clicked!");

&#x20;   public void OnClickReleasedOutside()   { }

&#x20;   public void OnMouseOutWhilePressing()  { }

&#x20;   public void OnMouseEnterWhilePressing(){ }

}



// 2. On a MonoBehaviour that inherits InteractableObject,

//    add MyClickBehaviour to the \_interactableBehaviours list in the Inspector

```



\### Example 2: Drag a Card onto a Slot (Generic Interaction)



```csharp

// Card: receives callbacks when dragged onto a SlotBehaviour

public class CardDragBehaviour : InteractableThreeDBehaviour, IDraggable<SlotBehaviour>

{

&#x20;   public void OnBeginDrag() => Debug.Log("Drag started");

&#x20;   public void OnDragEnterTarget(SlotBehaviour slot, RaycastHit hit) => Debug.Log($"Hovering slot {slot}");

&#x20;   public void OnDragReleasedOnTarget(SlotBehaviour slot, RaycastHit hit) => Debug.Log("Dropped on slot!");

&#x20;   public void OnDragLeaveTarget(SlotBehaviour slot, RaycastHit hit) { }

&#x20;   public void OnDragStayOnTarget(SlotBehaviour slot, RaycastHit hit) { }

&#x20;   public void OnDraggingWithoutTarget(RaycastHit? hit) { /\* update position \*/ }

&#x20;   public void OnDragReleaseWithoutTarget(RaycastHit? hit) => Debug.Log("Dropped on nothing");

&#x20;   public void OnMouseOutWhileDragging()  { }

&#x20;   public void OnMouseInWhileDragging()   { }

&#x20;   public void OnMouseStayWhileDragging() { }

}



// Slot: highlights when the card hovers over it

public class SlotBehaviour : InteractableThreeDBehaviour, IFocusable<CardDragBehaviour>

{

&#x20;   public void OnDraggedObjectEnter(CardDragBehaviour card)    => Highlight(true);

&#x20;   public void OnDraggedObjectLeave(CardDragBehaviour card)    => Highlight(false);

&#x20;   public void OnDraggedObjectReleased(CardDragBehaviour card) => AcceptCard(card);

&#x20;   public void OnDraggedObjectStay(CardDragBehaviour card)     { }

&#x20;   public void OnMouseEnterWithoutTarget() { }

&#x20;   public void OnMouseOutWithoutTarget()   { }

&#x20;   public void OnMouseStayWithoutTarget()  { }

}

```



\### Example 3: UI Button



```csharp

// UI Behaviours don't need InputSettings — input is driven by EventSystem

public class UIButtonBehaviour : InteractableUIBehaviour, IClickable

{

&#x20;   public void OnBeginClick()          => Debug.Log("UI Pressed");

&#x20;   public void OnClickReleasedInside() => Debug.Log("UI Clicked!");

&#x20;   // ... other interface members

}

// Add InteractableUIElement to a UI GameObject, then attach this Behaviour

```



\---



\## Scene Setup



1\. Add an empty GameObject to the scene and attach `InteractionSystem`

2\. For 3D objects: inherit from `InteractableObject` and add Behaviours in the Inspector

3\. For UI elements: attach `InteractableUIElement` and add Behaviours in the Inspector

4\. Make sure the scene has an `EventSystem` (required for UI support)

