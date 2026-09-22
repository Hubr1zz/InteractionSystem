[中文](README.md) | English

# Mouse Interaction System

A deterministic Unity 2022.3+ mouse interaction package for hover, click, drag, and drop between 3D Colliders and uGUI Graphics. One `InteractableObject` owns an Odin-serialized polymorphic Behaviour list, so effects remain reusable without adding many MonoBehaviour components. Odin Inspector 3.x is required; the new Input System is optional.

## Usage

Add one `InteractionSystem` to the scene. Add `InteractableObject` to a 3D or UI target, then add serializable classes derived from `InteractionBehaviour` to its Odin list. A `↗ TypeName` button on each element opens that Behaviour's script.

Assign `Interaction Camera` explicitly, or call `SetInteractionCamera`. A missing camera reports an error; it is never resolved through Main Camera. For UI-only scenes, set `Max Distance` to 0 and provide an EventSystem and GraphicRaycaster. Selected gizmos show the camera's center ray range and the sphere cast radius at its endpoint.

- `IHoverHandler`: Enter → Stay → Exit
- `IClickHandler`: Down → Held → Up → Click; an outside release does not click
- `IDragHandler`: DragBegin → Drag → DragEnd/DragCanceled
- `IDropHandler`: DragEnter → DragOver → Drop → DragExit

Use `InteractionContext.TryGetDraggedBehaviour<T>()` to communicate between a drag source and drop receiver without reflection. Child Colliders and uGUI Graphics resolve to their parent target automatically, while all geometry belonging to the dragged target is ignored during the drag.

The default pointer source uses Unity's built-in Input Manager. Projects using only the new Input System can implement `IPointerInputSource` and pass it to `InteractionSystem.SetInputSource`; the Chinese README contains a complete adapter.

Returning false from `TryGetFrame` cancels capture and hover. Losing the release edge while the button is no longer held cancels capture. Disabling and re-enabling the dispatcher restores its instance registration.

Callbacks may cancel interactions, disable targets, or add/remove behaviours. Cancellation stops further interaction in that frame; only participants that received a start event receive its terminal event. Canceling or disabling the target during PointerUp suppresses Click. Terminal sequences finish cleanup without duplicate cancellation, and detached behaviours receive no further events.

Do not mutate the same host's list during Initialize, Deinitialize, or refresh cleanup: AddBehaviour and RemoveBehaviour return false, and nested RefreshBehaviours calls are ignored. Compose modules outside these lifecycle callbacks and check mutation return values.

Ordinary colliders and graphics block objects behind them by default. Only GraphicRaycaster results participate in UI selection; PhysicsRaycaster results cannot bypass the configured physics layers. Max Physics Hits is the initial capacity: a full buffer triggers a complete query, expansion, and a warning; later frames reuse the expanded buffer. Unity does not guarantee nearest hits for a full NonAlloc buffer ([API reference](https://docs.unity3d.com/ja/2023.2/ScriptReference/Physics.RaycastNonAlloc.html)).

## Installation

Add the repository through Unity Package Manager or reference a local checkout:

```json
"com.hubrizz.interaction-system": "file:D:/MyLibrary/InteractionSystem"
```

## v2 migration

The current design restores v1's single-host Odin Behaviour composition, local/category switches, runtime add/remove, and exact-type global switches. It keeps v2's explicit drag/drop roles and reflection-free dispatch. Per-Behaviour InputActions and separate 3D/UI Behaviour bases remain removed. Existing v1 serialized data requires a deliberate migration because the base type and event interfaces changed.

## Verification

The included PlayMode suite verifies hover ordering, pointer capture, balanced drag/drop transitions, collider/UI blocking, physics buffer expansion, input loss, re-enable registration, callback cancellation/removal, and lifecycle reentrancy.

## Sample entry point

Import the Basic sample from Package Manager, then open `Scenes/BasicDragAndDrop.unity` to run the drag-and-drop example. The sample README also keeps the manual setup steps. Its scene and UI are persistent assets and do not depend on a runtime generator.
