# Changelog

## Unreleased

- Fixed ordinary collider/UI blockers being skipped and excluded physics raycasters from UI selection.
- Expanded saturated physics buffers using complete results and reused the larger buffer on later frames.
- Restored singleton registration after re-enable; canceled capture on unavailable input or a missed release edge.
- Made event dispatch resilient to callback cancellation, target disable, handler removal, and nested exit notifications; suppressed clicks canceled during PointerUp.
- Guarded behaviour lifecycle refresh and resolved removal by instance after cancellation callbacks.
- Prevented transferred behaviours from receiving their former host's captured events.
- Restricted the Inspector script-link attribute to editor builds so runtime compilation does not reference an editor-only method.
- Required explicit camera/sample references, added selected query gizmos, and corrected sample shader color configuration.
- Added focused PlayMode regressions for blocking, capacity, input recovery, callback mutation, and lifecycle reentrancy.
- Added a persistent Basic drag-and-drop scene and UI feedback sample, and repaired legacy sample behaviour references.

## 2.1.0 - 2026-08-13

- Restored the single-host, Odin-serialized polymorphic Behaviour design without making each Behaviour a MonoBehaviour.
- Restored per-Behaviour and per-interaction-category switches, runtime add/remove, and exact-type global enable/disable.
- Restored the Inspector script navigation button with exact type matching.
- Kept the v2 unified state machine, explicit drag/drop context, input-source abstraction, and reflection-free hot path.
- Removed the old per-Behaviour InputAction polling and separate 3D/UI Behaviour hierarchies.

## 2.0.0 - 2026-08-12

- Replaced serialized Odin behaviours with regular MonoBehaviour handler interfaces.
- Removed the mandatory Odin Inspector and Input System dependencies.
- Added one deterministic state machine for 3D and uGUI hover, click, drag, and drop.
- Added pointer capture, drag threshold, object-disable cancellation, and application-focus recovery.
- Added child Collider/UI Graphic target resolution and dragged-object hit filtering.
- Removed runtime generic reflection dispatch and hot-path LINQ allocations.
- Added UPM package metadata, assembly definitions, tests, and samples.

This is a breaking API redesign. See README.md for the v1 migration guide.
