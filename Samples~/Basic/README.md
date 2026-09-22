# Basic sample

1. Add one `InteractionSystem` to the scene and assign its Interaction Camera.
2. Create a plane to provide a world-space drag surface.
3. Add `InteractableObject` and a Collider to an item, then add `DraggableItemExample` to its Behaviours list. Assign Target Renderer and choose the material's Color Property (`_BaseColor` for URP Lit, `_Color` for Built-in Standard).
4. Add `InteractableObject` and a Collider to a destination, then add `DropZoneExample` to its Behaviours list and explicitly assign Content Root (the destination's own Transform is valid).

The item changes colour on hover, follows physics hit points while dragging, and is parented to the drop zone when accepted. After a successful drop, click the item again to return it to its initial scene position. While dragging, an invalid location is shown as semi-transparent red and a valid drop target is shown as semi-transparent green.

## Included scene

After importing this sample from Package Manager, open `Scenes/BasicDragAndDrop.unity` and press Play. Hover a cube to highlight it, click to write a console log, drag it onto the green zone to keep it there, or release elsewhere to return it to its start. The sample requires Odin Inspector 3.x and uses the built-in Input Manager (Input Manager or Both); projects using only the new Input System can provide an `IPointerInputSource`. The scene, materials, and UI are saved assets and use no runtime generator.

The scene also includes two saved UI examples. `UI Click Example` uses `ClickableUIExample` and changes colour on hover and after a click. `UI Drop Example` uses `UIDropZoneExample`; drag a cube over it to highlight the panel, then release to move the cube to the orange landing marker and show its name. The Canvas keeps its existing `GraphicRaycaster`, the scene has an `EventSystem`, and `InteractionSystem.queryUI` is enabled. The drop behaviour explicitly references the panel Graphic, status `Text`, and `UI Drop Landing` Transform.

While dragging, an accepted DropZone is shown as translucent green and an invalid position as translucent red. After a successful drop, click the placed item to restore its initial parent and local transform.
