using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace InteractionSystem.Runtime.Tests
{
    public sealed class InteractionSystemStateTests
    {
        private readonly List<GameObject> objects = new();
        private readonly List<Object> resources = new();
        private InteractionSystem system;
        private Camera camera;
        private FakePointer pointer;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            var cameraObject = Create("Camera");
            camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = Vector3.zero;
            camera.transform.rotation = Quaternion.identity;

            var systemObject = Create("Interaction System");
            system = systemObject.AddComponent<InteractionSystem>();
            system.SetInteractionCamera(camera);
            pointer = new FakePointer();
            system.SetInputSource(pointer);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            for (int i = objects.Count - 1; i >= 0; i--)
                if (objects[i])
                    Object.Destroy(objects[i]);
            objects.Clear();
            for (int i = resources.Count - 1; i >= 0; i--)
                if (resources[i])
                    Object.Destroy(resources[i]);
            resources.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator HoverTransition_FiresExactlyOnceAndInOrder()
        {
            var target = CreateTarget("Target", Vector3.forward * 5f, out var recorder);
            var inside = (Vector2)camera.WorldToScreenPoint(target.transform.position);

            yield return Frame(inside, false, false, false);
            yield return Frame(inside, false, false, false);
            yield return Frame(new Vector2(-1000f, -1000f), false, false, false);

            CollectionAssert.AreEqual(new[] { "hover-enter", "hover-stay", "hover-exit" }, recorder.Events);
        }

        [UnityTest]
        public IEnumerator PressThenMoveOutside_StillStartsCapturedDrag()
        {
            var target = CreateTarget("Target", Vector3.forward * 5f, out var recorder);
            var inside = (Vector2)camera.WorldToScreenPoint(target.transform.position);
            var outside = new Vector2(-1000f, -1000f);

            yield return Frame(inside, true, true, false);
            yield return Frame(outside, false, true, false);
            yield return Frame(outside, false, false, true);

            CollectionAssert.Contains(recorder.Events, "pointer-down");
            CollectionAssert.Contains(recorder.Events, "pointer-cancel");
            CollectionAssert.Contains(recorder.Events, "drag-begin");
            CollectionAssert.Contains(recorder.Events, "drag");
            CollectionAssert.Contains(recorder.Events, "drag-end");
            CollectionAssert.DoesNotContain(recorder.Events, "click");
        }

        [UnityTest]
        public IEnumerator ClickReleaseOutside_DoesNotClick()
        {
            var target = CreateTarget("Target", Vector3.forward * 5f, out var recorder);
            recorder.EnableDrag = false;
            target.RefreshBehaviours();
            var inside = (Vector2)camera.WorldToScreenPoint(target.transform.position);

            yield return Frame(inside, true, true, false);
            yield return Frame(new Vector2(-1000f, -1000f), false, false, true);

            CollectionAssert.Contains(recorder.Events, "pointer-up-outside");
            CollectionAssert.DoesNotContain(recorder.Events, "click");
        }

        [UnityTest]
        public IEnumerator DisablingDraggedTarget_CancelsCaptureExactlyOnce()
        {
            var target = CreateTarget("Target", Vector3.forward * 5f, out var recorder);
            var inside = (Vector2)camera.WorldToScreenPoint(target.transform.position);

            yield return Frame(inside, true, true, false);
            yield return Frame(inside + Vector2.right * 100f, false, true, false);
            target.enabled = false;
            yield return null;

            Assert.AreEqual(1, recorder.Events.FindAll(e => e == "drag-cancel").Count);
            Assert.IsFalse(system.IsDragging);
            Assert.IsNull(system.DraggedTarget);
        }

        [UnityTest]
        public IEnumerator DragAcrossObjects_ProducesBalancedDropSequence()
        {
            var source = CreateTarget("Source", new Vector3(-1f, 0f, 5f), out var sourceRecorder);
            var destination = CreateTarget("Destination", new Vector3(1f, 0f, 5f), out var destinationRecorder);
            destinationRecorder.AcceptDrops = true;
            var sourcePoint = (Vector2)camera.WorldToScreenPoint(source.transform.position);
            var destinationPoint = (Vector2)camera.WorldToScreenPoint(destination.transform.position);

            yield return Frame(sourcePoint, true, true, false);
            yield return Frame(destinationPoint, false, true, false);
            yield return Frame(destinationPoint, false, false, true);

            CollectionAssert.IsSubsetOf(new[] { "drop-enter", "drop-over", "drop", "drop-exit" }, destinationRecorder.Events);
            Assert.Less(destinationRecorder.Events.IndexOf("drop-enter"), destinationRecorder.Events.IndexOf("drop"));
            Assert.Less(destinationRecorder.Events.IndexOf("drop"), destinationRecorder.Events.IndexOf("drop-exit"));
            CollectionAssert.Contains(sourceRecorder.Events, "drag-end");
        }

        [UnityTest]
        public IEnumerator DisablingOnlyDragHandler_CancelsAndCleansCapture()
        {
            var target = CreateTarget("Target", Vector3.forward * 5f, out var recorder);
            var inside = (Vector2)camera.WorldToScreenPoint(target.transform.position);

            yield return Frame(inside, true, true, false);
            yield return Frame(inside + Vector2.right * 100f, false, true, false);
            recorder.BehaviourEnabled = false;
            yield return Frame(inside + Vector2.right * 100f, false, true, false);

            Assert.AreEqual(1, recorder.Events.FindAll(e => e == "drag-cancel").Count);
            Assert.IsFalse(system.IsDragging);
        }

        [UnityTest]
        public IEnumerator PointerUpMayDisableTarget_WithoutDuplicateCancel()
        {
            var target = CreateTarget("Target", Vector3.forward * 5f, out var recorder);
            recorder.DisableTargetOnPointerUp = true;
            var inside = (Vector2)camera.WorldToScreenPoint(target.transform.position);

            yield return Frame(inside, true, true, false);
            yield return Frame(inside, false, false, true);

            CollectionAssert.Contains(recorder.Events, "pointer-up-inside");
            CollectionAssert.DoesNotContain(recorder.Events, "click");
            Assert.AreEqual(0, recorder.Events.FindAll(e => e == "pointer-cancel").Count);
            Assert.IsNull(system.PressedTarget);
        }

        [UnityTest]
        public IEnumerator DragOverOrdinaryCollider_PreservesWorldHit()
        {
            var source = CreateTarget("Source", new Vector3(-1f, 0f, 5f), out var recorder);
            var surface = Create("Ordinary Surface");
            surface.transform.position = new Vector3(1f, 0f, 5f);
            surface.AddComponent<BoxCollider>();
            var sourcePoint = (Vector2)camera.WorldToScreenPoint(source.transform.position);
            var surfacePoint = (Vector2)camera.WorldToScreenPoint(surface.transform.position);

            yield return Frame(sourcePoint, true, true, false);
            yield return Frame(surfacePoint, false, true, false);

            Assert.IsTrue(recorder.LastDragHadWorldHit);
        }

        [UnityTest]
        public IEnumerator UguiGraphic_UsesTheSameHoverStateMachine()
        {
            var eventSystem = Create("Event System");
            eventSystem.AddComponent<EventSystem>();

            var canvasObject = Create("Canvas");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<GraphicRaycaster>();

            var uiObject = Create("UI Target");
            uiObject.transform.SetParent(canvasObject.transform, false);
            var rect = uiObject.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(200f, 200f);
            uiObject.AddComponent<Image>();
            var target = uiObject.AddComponent<InteractableObject>();
            var recorder = new Recorder();
            target.AddBehaviour(recorder);
            Canvas.ForceUpdateCanvases();

            yield return Frame(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), false, false, false);
            yield return Frame(new Vector2(-1000f, -1000f), false, false, false);

            CollectionAssert.AreEqual(new[] { "hover-enter", "hover-exit" }, recorder.Events);
        }

        [UnityTest]
        public IEnumerator ImageAlphaFilter_SkipsTransparentPixelsAndCanBeDisabled()
        {
            Create("Event System").AddComponent<EventSystem>();
            var canvasObject = Create("Canvas");
            canvasObject.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<GraphicRaycaster>();

            var texture = new Texture2D(8, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels(new[]
            {
                new Color(1f, 1f, 1f, 0f), new Color(1f, 1f, 1f, 0f), new Color(1f, 1f, 1f, 0f), new Color(1f, 1f, 1f, 0f),
                Color.white, Color.white, Color.white, Color.white
            });
            texture.Apply();
            resources.Add(texture);
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, 8f, 1f), new Vector2(0.5f, 0.5f), 1f, 0, SpriteMeshType.FullRect);
            resources.Add(sprite);

            var uiObject = Create("Alpha Target");
            uiObject.transform.SetParent(canvasObject.transform, false);
            var rect = uiObject.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(200f, 100f);
            var image = uiObject.AddComponent<Image>();
            image.sprite = sprite;
            var filter = uiObject.AddComponent<UIAlphaRaycastFilter>();
            filter.TargetGraphic = image;
            filter.AlphaAffectsRaycast = true;
            filter.MinimumAlpha = 0.1f;
            var target = uiObject.AddComponent<InteractableObject>();
            var recorder = new Recorder();
            target.AddBehaviour(recorder);
            Canvas.ForceUpdateCanvases();
            yield return null;

            Vector2 transparentPoint = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(new Vector3(-75f, 0f)));
            Vector2 opaquePoint = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(new Vector3(75f, 0f)));
            Assert.IsFalse(filter.AllowsRaycast(transparentPoint, null));
            Assert.IsTrue(filter.AllowsRaycast(opaquePoint, null));
            var physicsTarget = CreateTarget("Target Behind Transparent Pixel", camera.ScreenPointToRay(transparentPoint).GetPoint(5f), out _);
            yield return Frame(transparentPoint, false, false, false);
            Assert.AreSame(physicsTarget, system.HoveredTarget);
            yield return Frame(opaquePoint, false, false, false);
            Assert.AreSame(target, system.HoveredTarget);

            yield return Frame(new Vector2(-1000f, -1000f), false, false, false);
            filter.AlphaAffectsRaycast = false;
            yield return Frame(transparentPoint, false, false, false);
            Assert.AreSame(target, system.HoveredTarget);
        }

        [UnityTest]
        public IEnumerator TextAlphaFilter_UsesGeneratedGlyphGeometryInsteadOfTheFullRect()
        {
            Create("Event System").AddComponent<EventSystem>();
            var canvasObject = Create("Canvas");
            canvasObject.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<GraphicRaycaster>();

            var uiObject = Create("Text Target");
            uiObject.transform.SetParent(canvasObject.transform, false);
            var rect = uiObject.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(300f, 100f);
            var text = uiObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 64;
            text.alignment = TextAnchor.MiddleCenter;
            text.text = "X";
            var filter = uiObject.AddComponent<UIAlphaRaycastFilter>();
            filter.TargetGraphic = text;
            filter.AlphaAffectsRaycast = true;
            filter.MinimumAlpha = 0.1f;
            var target = uiObject.AddComponent<InteractableObject>();
            var recorder = new Recorder();
            target.AddBehaviour(recorder);
            Canvas.ForceUpdateCanvases();
            yield return null;

            Vector2 emptyPoint = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(new Vector3(-120f, 0f)));
            Vector2 glyphPoint = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(Vector3.zero));
            yield return Frame(emptyPoint, false, false, false);
            Assert.IsNull(system.HoveredTarget);
            yield return Frame(glyphPoint, false, false, false);
            Assert.AreSame(target, system.HoveredTarget);
        }

        [UnityTest]
        public IEnumerator RuntimeAddRemove_UsesOneInitializeAndOneDeinitialize()
        {
            var go = Create("Target");
            var target = go.AddComponent<InteractableObject>();
            var recorder = new Recorder();

            Assert.IsTrue(target.AddBehaviour(recorder));
            target.RefreshBehaviours();
            Assert.AreEqual(1, recorder.InitializeCount);
            Assert.AreSame(go, recorder.Owner);
            Assert.IsTrue(target.TryGetBehaviour<Recorder>(out var found));
            Assert.AreSame(recorder, found);

            Assert.IsTrue(target.RemoveBehaviour(recorder));
            Assert.AreEqual(1, recorder.DeinitializeCount);
            Assert.IsFalse(recorder.IsInitialized);
            yield return null;
        }

        [UnityTest]
        public IEnumerator LocalCategoryAndGlobalTypeSwitches_BlockDispatch()
        {
            var target = CreateTarget("Target", Vector3.forward * 5f, out var recorder);
            var inside = (Vector2)camera.WorldToScreenPoint(target.transform.position);

            recorder.EnabledInteractions &= ~InteractionCategories.Hover;
            yield return Frame(inside, false, false, false);
            CollectionAssert.DoesNotContain(recorder.Events, "hover-enter");

            recorder.EnabledInteractions |= InteractionCategories.Hover;
            system.DisableBehaviourType(typeof(Recorder));
            yield return Frame(new Vector2(-1000f, -1000f), false, false, false);
            yield return Frame(inside, false, false, false);
            CollectionAssert.DoesNotContain(recorder.Events, "hover-enter");

            system.EnableBehaviourType(typeof(Recorder));
            yield return Frame(new Vector2(-1000f, -1000f), false, false, false);
            yield return Frame(inside, false, false, false);
            CollectionAssert.Contains(recorder.Events, "hover-enter");
        }


        [UnityTest]
        public IEnumerator OrdinaryCollider_BlocksTargetBehindIt()
        {
            var target = CreateTarget("Target", Vector3.forward * 6f, out var recorder);
            var blocker = Create("Blocker");
            blocker.transform.position = Vector3.forward * 3f;
            blocker.AddComponent<BoxCollider>();
            yield return Frame(camera.WorldToScreenPoint(target.transform.position), true, true, false);
            Assert.IsNull(system.PressedTarget);
            Assert.IsEmpty(recorder.Events);
        }

        [UnityTest]
        public IEnumerator OrdinaryGraphic_BlocksTargetBehindIt()
        {
            Create("Event System").AddComponent<EventSystem>();
            var canvasObject = Create("Canvas");
            canvasObject.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<GraphicRaycaster>();
            var panel = Create("Blocking Panel");
            panel.transform.SetParent(canvasObject.transform, false);
            panel.AddComponent<RectTransform>().sizeDelta = new Vector2(200f, 200f);
            panel.AddComponent<Image>();
            var target = CreateTarget("Target", Vector3.forward * 5f, out var recorder);
            Canvas.ForceUpdateCanvases();
            yield return Frame(camera.WorldToScreenPoint(target.transform.position), true, true, false);
            Assert.IsNull(system.PressedTarget);
            Assert.IsEmpty(recorder.Events);
        }

        [UnityTest]
        public IEnumerator PhysicsRaycaster_DoesNotBypassPhysicsLayerMask()
        {
            Create("Event System").AddComponent<EventSystem>();
            camera.gameObject.AddComponent<PhysicsRaycaster>();
            SetSystemField("physicsLayers", (LayerMask)0);
            var target = CreateTarget("Target", Vector3.forward * 5f, out var recorder);
            yield return Frame(camera.WorldToScreenPoint(target.transform.position), true, true, false);
            Assert.IsNull(system.PressedTarget);
            Assert.IsEmpty(recorder.Events);
        }

        [UnityTest]
        public IEnumerator FullPhysicsBuffer_StillSelectsNearestAndReusesExpansion()
        {
            SetSystemField("maxPhysicsHits", 4);
            SetSystemField("physicsHits", new RaycastHit[4]);
            var nearest = CreateTarget("Nearest", Vector3.forward * 3f, out _);
            for (int i = 0; i < 40; i++)
                CreateTarget("Far Target", Vector3.forward * (5f + i * 2f), out _);
            LogAssert.Expect(LogType.Warning, "[InteractionSystem] Physics hit buffer was full and has been expanded. Increase Max Physics Hits to avoid the initial allocation.");
            var position = camera.WorldToScreenPoint(nearest.transform.position);
            yield return Frame(position, false, false, false);
            Assert.AreSame(nearest, system.HoveredTarget);
            var field = typeof(InteractionSystem).GetField("physicsHits", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var expanded = field.GetValue(system);
            yield return Frame(position, false, false, false);
            Assert.AreSame(expanded, field.GetValue(system));
        }

        [UnityTest]
        public IEnumerator ReenabledSystem_RegistersAndReceivesTargetCancellation()
        {
            system.enabled = false;
            Assert.IsFalse(InteractionSystem.TryGetExistingInstance(out _));
            system.enabled = true;
            Assert.IsTrue(InteractionSystem.TryGetExistingInstance(out var registered));
            Assert.AreSame(system, registered);
            var target = CreateTarget("Target", Vector3.forward * 5f, out var recorder);
            yield return Frame(camera.WorldToScreenPoint(target.transform.position), true, true, false);
            target.enabled = false;
            Assert.IsNull(system.PressedTarget);
            Assert.AreEqual(1, recorder.Events.FindAll(e => e == "pointer-cancel").Count);
        }

        [UnityTest]
        public IEnumerator InputUnavailable_CancelsCaptureAndHoverOnce()
        {
            var target = CreateTarget("Target", Vector3.forward * 5f, out var recorder);
            yield return Frame(camera.WorldToScreenPoint(target.transform.position), true, true, false);
            pointer.Available = false;
            yield return null;
            yield return null;
            Assert.IsNull(system.PressedTarget);
            Assert.IsNull(system.HoveredTarget);
            Assert.AreEqual(1, recorder.Events.FindAll(e => e == "pointer-cancel").Count);
            Assert.AreEqual(1, recorder.Events.FindAll(e => e == "hover-exit").Count);
        }

        [UnityTest]
        public IEnumerator MissingReleaseEdge_CancelsCapture()
        {
            var target = CreateTarget("Target", Vector3.forward * 5f, out var recorder);
            var position = camera.WorldToScreenPoint(target.transform.position);
            yield return Frame(position, true, true, false);
            yield return Frame(position, false, false, false);
            Assert.IsNull(system.PressedTarget);
            CollectionAssert.Contains(recorder.Events, "pointer-cancel");
            CollectionAssert.DoesNotContain(recorder.Events, "click");
        }

        [UnityTest]
        public IEnumerator HoverExitMayCancelAll_WithoutRecursingOrEnteringNextTarget()
        {
            var target = CreateTarget("Target", Vector3.forward * 5f, out var recorder);
            var next = CreateTarget("Next", new Vector3(2f, 0f, 5f), out var nextRecorder);
            recorder.Callback = (name, context) =>
            {
                if (name == "hover-exit")
                    context.Dispatcher.CancelAllInteractions();
            };
            yield return Frame(camera.WorldToScreenPoint(target.transform.position), false, false, false);
            yield return Frame(camera.WorldToScreenPoint(next.transform.position), false, false, false);
            Assert.AreEqual(1, recorder.Events.FindAll(e => e == "hover-exit").Count);
            Assert.IsNull(system.HoveredTarget);
            Assert.IsEmpty(nextRecorder.Events);
        }

        [UnityTest]
        public IEnumerator CancelDuringHoverEnter_DoesNotExitUnstartedHandlersOrPress()
        {
            var target = CreateTarget("Target", Vector3.forward * 5f, out var recorder);
            var second = new Recorder();
            target.AddBehaviour(second);
            recorder.Callback = (name, context) =>
            {
                if (name == "hover-enter")
                    context.Dispatcher.CancelAllInteractions();
            };
            yield return Frame(camera.WorldToScreenPoint(target.transform.position), true, true, false);
            CollectionAssert.AreEqual(new[] { "hover-enter", "hover-exit" }, recorder.Events);
            Assert.IsEmpty(second.Events);
            Assert.IsNull(system.PressedTarget);
        }

        [UnityTest]
        public IEnumerator RemovingBehaviourDuringHoverStay_DoesNotDispatchStaleHandlers()
        {
            var target = CreateTarget("Target", Vector3.forward * 5f, out var recorder);
            var second = new Recorder();
            target.AddBehaviour(second);
            recorder.Callback = (name, context) =>
            {
                if (name == "hover-stay")
                    target.RemoveBehaviour(recorder);
            };
            var position = camera.WorldToScreenPoint(target.transform.position);
            yield return Frame(position, false, false, false);
            yield return Frame(position, false, false, false);
            Assert.IsFalse(recorder.IsInitialized);
            CollectionAssert.AreEqual(new[] { "hover-enter", "hover-exit" }, second.Events);
        }

        [UnityTest]
        public IEnumerator CancelDuringPointerUp_SuppressesClickAndClearsHover()
        {
            var target = CreateTarget("Target", Vector3.forward * 5f, out var recorder);
            recorder.Callback = (name, context) =>
            {
                if (name == "pointer-up-inside")
                    context.Dispatcher.CancelAllInteractions();
            };
            var position = camera.WorldToScreenPoint(target.transform.position);
            yield return Frame(position, true, true, false);
            yield return Frame(position, false, false, true);
            CollectionAssert.DoesNotContain(recorder.Events, "click");
            CollectionAssert.DoesNotContain(recorder.Events, "pointer-cancel");
            Assert.IsNull(system.HoveredTarget);
            Assert.IsNull(system.PressedTarget);
        }

        [UnityTest]
        public IEnumerator CancelDuringClickToDragTransition_DoesNotBeginDrag()
        {
            var target = CreateTarget("Target", Vector3.forward * 5f, out var recorder);
            recorder.Callback = (name, context) =>
            {
                if (name == "pointer-cancel")
                    context.Dispatcher.CancelAllInteractions();
            };
            var position = (Vector2)camera.WorldToScreenPoint(target.transform.position);
            yield return Frame(position, true, true, false);
            yield return Frame(position + Vector2.right * 100f, false, true, false);
            CollectionAssert.DoesNotContain(recorder.Events, "drag-begin");
            CollectionAssert.DoesNotContain(recorder.Events, "drag-cancel");
            Assert.IsFalse(system.IsDragging);
            Assert.IsNull(system.HoveredTarget);
        }

        [UnityTest]
        public IEnumerator CancelDuringDragEnter_DoesNotNotifyUnstartedDropHandlers()
        {
            var source = CreateTarget("Source", new Vector3(-1f, 0f, 5f), out _);
            var destination = CreateTarget("Destination", new Vector3(1f, 0f, 5f), out var recorder);
            recorder.AcceptDrops = true;
            var second = new Recorder { AcceptDrops = true };
            destination.AddBehaviour(second);
            recorder.Callback = (name, context) =>
            {
                if (name == "drop-enter")
                    context.Dispatcher.CancelAllInteractions();
            };
            yield return Frame(camera.WorldToScreenPoint(source.transform.position), true, true, false);
            yield return Frame(camera.WorldToScreenPoint(destination.transform.position), false, true, false);
            Assert.AreEqual(1, recorder.Events.FindAll(e => e == "drop-exit").Count);
            CollectionAssert.DoesNotContain(second.Events, "drop-enter");
            CollectionAssert.DoesNotContain(second.Events, "drop-exit");
            Assert.IsNull(system.DropTarget);
            Assert.IsFalse(system.IsDragging);
        }

        [UnityTest]
        public IEnumerator CanAcceptDropMayRemoveItself_WithoutMutatingEnumeration()
        {
            var source = CreateTarget("Source", new Vector3(-1f, 0f, 5f), out _);
            var destination = CreateTarget("Destination", new Vector3(1f, 0f, 5f), out var recorder);
            recorder.AcceptDrops = true;
            recorder.DropProbe = () => destination.RemoveBehaviour(recorder);
            yield return Frame(camera.WorldToScreenPoint(source.transform.position), true, true, false);
            yield return Frame(camera.WorldToScreenPoint(destination.transform.position), false, true, false);
            Assert.IsFalse(recorder.IsInitialized);
            Assert.IsNull(system.DropTarget);
            CollectionAssert.DoesNotContain(recorder.Events, "drop-enter");
        }

        private void SetSystemField(string name, object value)
        {
            typeof(InteractionSystem).GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(system, value);
        }

        [UnityTest]
        public IEnumerator LifecycleRefresh_DoesNotReenterOrMutateTheBehaviourList()
        {
            var target = Create("Target").AddComponent<InteractableObject>();
            var recorder = new Recorder();
            var nested = new Recorder();
            recorder.InitializeAction = () =>
            {
                target.RefreshBehaviours();
                Assert.IsFalse(target.AddBehaviour(nested));
            };
            recorder.DeinitializeAction = () =>
            {
                target.RefreshBehaviours();
                Assert.IsFalse(target.AddBehaviour(nested));
            };
            Assert.IsTrue(target.AddBehaviour(recorder));
            Assert.IsTrue(target.RemoveBehaviour(recorder));
            Assert.AreEqual(1, recorder.InitializeCount);
            Assert.AreEqual(1, recorder.DeinitializeCount);
            Assert.IsEmpty(target.Behaviours);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RemoveBehaviour_WhenCancellationRemovesAnEarlierEntry_RemovesTheRequestedInstance()
        {
            var target = CreateTarget("Target", Vector3.forward * 5f, out var first);
            var second = new Recorder();
            target.AddBehaviour(second);
            first.Callback = (name, context) =>
            {
                if (name == "pointer-cancel")
                    target.RemoveBehaviour(first);
            };
            yield return Frame(camera.WorldToScreenPoint(target.transform.position), true, true, false);
            Assert.IsTrue(target.RemoveBehaviour(second));
            Assert.IsEmpty(target.Behaviours);
            Assert.AreEqual(1, first.DeinitializeCount);
            Assert.AreEqual(1, second.DeinitializeCount);
            Assert.IsNull(system.PressedTarget);
        }

        [UnityTest]
        public IEnumerator TransferredBehaviour_DoesNotReceiveEventsFromItsFormerCapture()
        {
            var source = CreateTarget("Source", Vector3.forward * 5f, out var first);
            var destination = Create("Destination").AddComponent<InteractableObject>();
            var transferred = new Recorder();
            source.AddBehaviour(transferred);
            first.Callback = (name, context) =>
            {
                if (name != "pointer-up-inside")
                    return;
                Assert.IsTrue(source.RemoveBehaviour(transferred));
                Assert.IsTrue(destination.AddBehaviour(transferred));
            };
            var position = camera.WorldToScreenPoint(source.transform.position);
            yield return Frame(position, true, true, false);
            yield return Frame(position, false, false, true);
            Assert.AreSame(destination, transferred.Target);
            CollectionAssert.Contains(transferred.Events, "pointer-down");
            CollectionAssert.DoesNotContain(transferred.Events, "pointer-up-inside");
            CollectionAssert.DoesNotContain(transferred.Events, "click");
        }

        private GameObject Create(string name)
        {
            var result = new GameObject(name);
            objects.Add(result);
            return result;
        }

        private InteractableObject CreateTarget(string name, Vector3 position, out Recorder recorder)
        {
            var go = Create(name);
            go.transform.position = position;
            go.AddComponent<BoxCollider>();
            var target = go.AddComponent<InteractableObject>();
            recorder = new Recorder();
            target.AddBehaviour(recorder);
            return target;
        }

        private IEnumerator Frame(Vector2 position, bool down, bool held, bool up)
        {
            pointer.Current = new PointerFrame(position, down, held, up);
            yield return null;
            pointer.Current = default;
        }

        private sealed class FakePointer : IPointerInputSource
        {
            public PointerFrame Current;
            public bool Available = true;
            public bool TryGetFrame(int mouseButton, out PointerFrame frame)
            {
                frame = Current;
                return Available;
            }
        }

        [System.Serializable]
        private sealed class Recorder : InteractionBehaviour, IHoverHandler, IClickHandler, IDragHandler, IDropHandler
        {
            public readonly List<string> Events = new();
            public System.Action<string, InteractionContext> Callback;
            public System.Action DropProbe;
            public System.Action InitializeAction;
            public System.Action DeinitializeAction;
            public bool EnableDrag = true;
            public bool AcceptDrops;
            public bool DisableTargetOnPointerUp;
            public bool LastDragHadWorldHit;
            public int InitializeCount;
            public int DeinitializeCount;

            private void Record(string name, in InteractionContext context)
            {
                Events.Add(name);
                Callback?.Invoke(name, context);
            }

            public override void Initialize()
            {
                InitializeCount++;
                InitializeAction?.Invoke();
            }
            public override void Deinitialize()
            {
                DeinitializeCount++;
                DeinitializeAction?.Invoke();
            }

            public void OnHoverEnter(in InteractionContext context) => Record("hover-enter", context);
            public void OnHoverStay(in InteractionContext context) => Record("hover-stay", context);
            public void OnHoverExit(in InteractionContext context) => Record("hover-exit", context);
            public void OnPointerDown(in InteractionContext context) => Record("pointer-down", context);
            public void OnPointerHeld(in InteractionContext context) => Record("pointer-held", context);
            public void OnPointerUp(in InteractionContext context, bool inside)
            {
                Record(inside ? "pointer-up-inside" : "pointer-up-outside", context);
                if (DisableTargetOnPointerUp)
                    Target.enabled = false;
            }
            public void OnClick(in InteractionContext context) => Record("click", context);
            public void OnPointerCanceled(in InteractionContext context) => Record("pointer-cancel", context);
            public void OnDragBegin(in InteractionContext context) { if (EnableDrag) Record("drag-begin", context); }
            public void OnDrag(in InteractionContext context)
            {
                LastDragHadWorldHit = context.Hit.HasWorldHit;
                if (EnableDrag)
                    Record("drag", context);
            }
            public void OnDragEnd(in InteractionContext context) { if (EnableDrag) Record("drag-end", context); }
            public void OnDragCanceled(in InteractionContext context) { if (EnableDrag) Record("drag-cancel", context); }
            public bool CanAcceptDrop(in InteractionContext context) { DropProbe?.Invoke(); return AcceptDrops; }
            public void OnDragEnter(in InteractionContext context) => Record("drop-enter", context);
            public void OnDragOver(in InteractionContext context) => Record("drop-over", context);
            public void OnDragExit(in InteractionContext context) => Record("drop-exit", context);
            public void OnDrop(in InteractionContext context) => Record("drop", context);
        }
    }
}
