# Basic sample

1. Add one `InteractionSystem` to the scene and assign its Interaction Camera.
2. Create a plane to provide a world-space drag surface.
3. Add `InteractableObject` and a Collider to an item, then add `DraggableItemExample` to its Behaviours list. Assign Target Renderer and choose the material's Color Property (`_BaseColor` for URP Lit, `_Color` for Built-in Standard).
4. Add `InteractableObject` and a Collider to a destination, then add `DropZoneExample` to its Behaviours list and explicitly assign Content Root (the destination's own Transform is valid).

The item changes colour on hover, follows physics hit points while dragging, and is parented to the drop zone when accepted.

## Included scene

After importing this sample from Package Manager, open `Scenes/BasicDragAndDrop.unity` and press Play. Hover a cube to highlight it, click to write a console log, drag it onto the green zone to keep it there, or release elsewhere to return it to its start. The sample requires Odin Inspector 3.x and uses the built-in Input Manager (Input Manager or Both); projects using only the new Input System can provide an `IPointerInputSource`. The scene, materials, and UI are saved assets and use no runtime generator.
