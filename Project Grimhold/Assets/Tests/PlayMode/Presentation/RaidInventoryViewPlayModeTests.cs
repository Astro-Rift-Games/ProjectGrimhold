#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Tests.PlayMode.Presentation
{
    public sealed class RaidInventoryViewPlayModeTests
    {
        private const string SharedInventoryPrefabPath = "Assets/Prefabs/UI/RaidInventoryUI.prefab";

        private GameObject _canvasObject;
        private GameObject _instance;
        private RaidInventoryView _view;

        [SetUp]
        public void SetUp()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SharedInventoryPrefabPath);
            Assert.That(prefab, Is.Not.Null);

            _canvasObject = new GameObject(
                "RaidInventoryViewTestCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            Canvas canvas = _canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            _instance = Object.Instantiate(prefab, _canvasObject.transform, false);
            RectTransform inventoryRoot = (RectTransform)_instance.transform;
            inventoryRoot.anchorMin = Vector2.zero;
            inventoryRoot.anchorMax = Vector2.one;
            inventoryRoot.anchoredPosition = Vector2.zero;
            inventoryRoot.sizeDelta = Vector2.zero;
            inventoryRoot.localScale = Vector3.one;

            _view = _instance.GetComponent<RaidInventoryView>();
            Assert.That(_view, Is.Not.Null);
            Assert.That(_view.PlayerPanel, Is.Not.Null);
            Assert.That(_view.ContainerPanel, Is.Not.Null);
            Assert.That(_view.TakeAllButton, Is.Not.Null);
            Assert.That(_view.ContextMenu, Is.Not.Null);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_canvasObject);
        }

        [UnityTest]
        public IEnumerator PlayerPanelPoolIsStableAndClearKeepsCapacity()
        {
            RaidLootPanelView panel = _view.PlayerPanel;
            Assert.That(panel.EnsureSlotCount(2), Is.True);
            yield return null;

            Transform slots = panel.transform.Find("SlotsGrid");
            Assert.That(slots, Is.Not.Null);
            GameObject firstSlot = slots.GetChild(0).gameObject;
            GameObject secondSlot = slots.GetChild(1).gameObject;

            Assert.That(panel.EnsureSlotCount(2), Is.True);
            var data = new List<RaidInventorySlotData>
            {
                RaidInventorySlotData.Create(new LootEntry(new LootId("coin"), 4), null, null),
                RaidInventorySlotData.Empty
            };

            Assert.That(panel.Present(data, 40, false, false, default), Is.True);
            panel.ClearContent();

            Assert.That(panel.SlotCount, Is.EqualTo(2));
            Assert.That(slots.GetChild(0).gameObject, Is.SameAs(firstSlot));
            Assert.That(slots.GetChild(1).gameObject, Is.SameAs(secondSlot));
        }

        [UnityTest]
        public IEnumerator ContainerPanelSupportsEmptyCapacityWithoutClosingScreen()
        {
            _view.SetScreenVisible(true);
            _view.SetContainerPanelVisible(true);
            RaidLootPanelView panel = _view.ContainerPanel;
            Assert.That(panel.EnsureSlotCount(3), Is.True);

            var data = new List<RaidInventorySlotData>
            {
                RaidInventorySlotData.Empty,
                RaidInventorySlotData.Empty,
                RaidInventorySlotData.Empty
            };
            Assert.That(panel.Present(data, null, true, true, default), Is.True);
            yield return null;

            Assert.That(_view.IsOpen, Is.True);
            Assert.That(panel.SlotCount, Is.EqualTo(3));
            Assert.That(panel.gameObject.activeSelf, Is.True);
        }

        [UnityTest]
        public IEnumerator OccupiedSlot_MapsLeftToSingleUnitAndRightToFullStack()
        {
            _instance.SetActive(true);
            _view.SetScreenVisible(true);
            _view.SetContainerPanelVisible(true);
            RaidLootPanelView panel = _view.ContainerPanel;
            Assert.That(panel.EnsureSlotCount(1), Is.True);
            var data = new List<RaidInventorySlotData>
            {
                RaidInventorySlotData.Create(new LootEntry(new LootId("coin"), 4), null, null)
            };
            Assert.That(panel.Present(data, null, false, true, default), Is.True);
            yield return null;

            RaidInventorySlotView slot = GetFirstActiveSlot(panel);
            Assert.That(slot, Is.Not.Null);
            Button button = slot.GetComponent<Button>();
            Assert.That(button, Is.Not.Null);

            var receivedModes = new List<LootTransferQuantityMode>();
            panel.SelectionRequested += (_, mode) => receivedModes.Add(mode);

            button.onClick.Invoke();
            slot.OnPointerClick(new PointerEventData(null)
            {
                button = PointerEventData.InputButton.Right
            });

            Assert.That(
                receivedModes,
                Is.EqualTo(new[]
                {
                    LootTransferQuantityMode.SingleUnit,
                    LootTransferQuantityMode.FullStack
                }));

            panel.RefreshInteraction(false, default);
            button.onClick.Invoke();
            slot.OnPointerClick(new PointerEventData(null)
            {
                button = PointerEventData.InputButton.Right
            });
            Assert.That(receivedModes, Has.Count.EqualTo(2));

            slot.Clear();
            slot.OnPointerClick(new PointerEventData(null)
            {
                button = PointerEventData.InputButton.Right
            });
            Assert.That(receivedModes, Has.Count.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator PersonalOccupiedSlot_RightClickEmitsContextWithoutTransfer()
        {
            _instance.SetActive(true);
            RaidLootPanelView panel = _view.PlayerPanel;
            Assert.That(panel.EnsureSlotCount(1), Is.True);
            var data = new List<RaidInventorySlotData>
            {
                RaidInventorySlotData.Create(new LootEntry(new LootId("coin"), 4), null, null)
            };
            Assert.That(
                panel.Present(
                    data,
                    null,
                    false,
                    RaidLootSlotInteractionMode.ContextMenu,
                    default),
                Is.True);
            yield return null;

            RaidInventorySlotView slot = GetFirstActiveSlot(panel);
            Assert.That(slot, Is.Not.Null);
            int transferCount = 0;
            LootId requestedLoot = default;
            RectTransform requestedAnchor = null;
            panel.SelectionRequested += (_, _) => transferCount++;
            panel.ContextRequested += (lootId, anchor) =>
            {
                requestedLoot = lootId;
                requestedAnchor = anchor;
            };

            slot.GetComponent<Button>().onClick.Invoke();
            slot.OnPointerClick(new PointerEventData(null)
            {
                button = PointerEventData.InputButton.Right,
                position = new Vector2(320f, 240f)
            });

            Assert.That(transferCount, Is.Zero);
            Assert.That(requestedLoot, Is.EqualTo(new LootId("coin")));
            Assert.That(requestedAnchor, Is.SameAs(slot.transform));
        }

        [UnityTest]
        public IEnumerator EquipmentOccupiedSlot_RightClickEmitsItsOwnAnchor()
        {
            _instance.SetActive(true);
            _view.SetScreenVisible(true);
            _view.SetEquipmentPanelVisible(true);
            var slots = new List<RaidInventorySlotData>();
            for (int index = 0; index < EquipmentSlotRules.AllSlots.Length; index++)
            {
                slots.Add(index == 2
                    ? RaidInventorySlotData.Create(
                        new LootEntry(new LootId("helmet"), 1), null, null)
                    : RaidInventorySlotData.Empty);
            }

            _view.PresentEquipmentSlots(slots, WeaponSlot.None, true);
            yield return null;

            RaidInventorySlotView helmet = null;
            RaidInventorySlotView[] views =
                _view.GetComponentsInChildren<RaidInventorySlotView>(true);
            for (int index = 0; index < views.Length; index++)
            {
                if (views[index].name == "Helmet")
                {
                    helmet = views[index];
                    break;
                }
            }

            Assert.That(helmet, Is.Not.Null);
            EquipmentSlot requestedSlot = default;
            RectTransform requestedAnchor = null;
            _view.EquipmentContextRequested += (slot, anchor) =>
            {
                requestedSlot = slot;
                requestedAnchor = anchor;
            };

            helmet.OnPointerClick(new PointerEventData(null)
            {
                button = PointerEventData.InputButton.Right
            });

            Assert.That(requestedSlot, Is.EqualTo(EquipmentSlot.Helmet));
            Assert.That(requestedAnchor, Is.SameAs(helmet.transform));
        }

        [UnityTest]
        public IEnumerator ContextMenu_RendersOrderedActionsAndClampsToCanvas()
        {
            _instance.SetActive(true);
            _view.SetScreenVisible(true);
            yield return null;

            var provider = new NoOpContextActionProvider();
            var actions = new List<LootContextActionDescriptor>
            {
                new(new LootContextActionId("test.first"), "Soltar", true, provider),
                new(new LootContextActionId("test.second"), "Soltar todo", true, provider)
            };

            var anchorObject = new GameObject("EdgeAnchor", typeof(RectTransform));
            RectTransform anchor = anchorObject.GetComponent<RectTransform>();
            anchor.SetParent(_instance.transform, false);
            anchor.anchorMin = Vector2.one;
            anchor.anchorMax = Vector2.one;
            anchor.pivot = Vector2.one;
            anchor.sizeDelta = new Vector2(40f, 40f);
            anchor.anchoredPosition = Vector2.zero;

            Assert.That(_view.ContextMenu.Show(actions, anchor), Is.True);
            yield return null;
            Assert.That(_view.ContextMenu.CurrentAnchor, Is.SameAs(anchor));

            RaidLootContextActionButton[] buttons =
                _view.ContextMenu.GetComponentsInChildren<RaidLootContextActionButton>(true);
            var visibleLabels = new List<string>();
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i].gameObject.activeSelf)
                {
                    visibleLabels.Add(buttons[i].GetComponentInChildren<TMP_Text>(true).text);
                }
            }

            Assert.That(visibleLabels, Is.EqualTo(new[] { "Soltar", "Soltar todo" }));

            LootContextActionId requestedAction = default;
            _view.ContextMenu.ActionRequested += actionId => requestedAction = actionId;
            RaidLootContextActionButton firstVisibleButton = System.Array.Find(
                buttons,
                button => button.gameObject.activeSelf);
            Assert.That(firstVisibleButton, Is.Not.Null);
            firstVisibleButton.GetComponent<Button>().onClick.Invoke();
            Assert.That(requestedAction, Is.EqualTo(actions[0].Id));

            var canvasRect = (RectTransform)_instance.transform;
            var menuRect = (RectTransform)_view.ContextMenu.transform;
            Bounds menuBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(canvasRect, menuRect);
            Assert.That(menuBounds.min.x, Is.GreaterThanOrEqualTo(canvasRect.rect.xMin - 0.01f));
            Assert.That(menuBounds.max.x, Is.LessThanOrEqualTo(canvasRect.rect.xMax + 0.01f));
            Assert.That(menuBounds.min.y, Is.GreaterThanOrEqualTo(canvasRect.rect.yMin - 0.01f));
            Assert.That(menuBounds.max.y, Is.LessThanOrEqualTo(canvasRect.rect.yMax + 0.01f));

            int dismissCount = 0;
            _view.ContextMenu.DismissRequested += () => dismissCount++;
            _view.ContextMenu.OnPointerExit(new PointerEventData(null));
            Assert.That(dismissCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator ContextMenu_DifferentSlotAnchorsProduceDifferentPositions()
        {
            _instance.SetActive(true);
            _view.SetScreenVisible(true);
            yield return null;

            var provider = new NoOpContextActionProvider();
            var actions = new List<LootContextActionDescriptor>
            {
                new(new LootContextActionId("test.anchor"), "Acción", true, provider)
            };
            RectTransform left = CreateAnchor("SlotA", new Vector2(-200f, 80f));
            RectTransform right = CreateAnchor("SlotB", new Vector2(200f, -80f));

            Assert.That(_view.ContextMenu.Show(actions, left), Is.True);
            yield return null;
            Vector3 firstPosition = _view.ContextMenu.transform.position;
            Assert.That(_view.ContextMenu.CurrentAnchor, Is.SameAs(left));

            Assert.That(_view.ContextMenu.Show(actions, right), Is.True);
            yield return null;
            Vector3 secondPosition = _view.ContextMenu.transform.position;
            Assert.That(_view.ContextMenu.CurrentAnchor, Is.SameAs(right));
            Assert.That(secondPosition, Is.Not.EqualTo(firstPosition));
        }

        [Test]
        public void TransferFeedback_ShowsClearsAndUsesPrefabReferences()
        {
            Assert.That(_view.TransferFeedbackText, Is.Not.Null);
            Assert.That(
                _view.TransferFeedbackText.transform.IsChildOf(_view.transform),
                Is.True);

            _view.ShowTransferFeedback("Inventario lleno");

            Assert.That(_view.TransferFeedbackText.text, Is.EqualTo("Inventario lleno"));
            Assert.That(_view.TransferFeedbackText.gameObject.activeSelf, Is.True);

            _view.HideTransferFeedback();

            Assert.That(_view.TransferFeedbackText.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void TakeAllButton_EmitsOnlyWhileInteractable()
        {
            int requestCount = 0;
            _view.TakeAllRequested += () => requestCount++;

            _view.SetTakeAllInteractable(false);
            _view.TakeAllButton.onClick.Invoke();
            Assert.That(requestCount, Is.Zero);

            _view.SetTakeAllInteractable(true);
            _view.TakeAllButton.onClick.Invoke();
            Assert.That(requestCount, Is.EqualTo(1));

            _view.SetContainerPanelVisible(false);
            Assert.That(_view.TakeAllButton.interactable, Is.False);
        }

        private static RaidInventorySlotView GetFirstActiveSlot(RaidLootPanelView panel)
        {
            RaidInventorySlotView[] slots =
                panel.GetComponentsInChildren<RaidInventorySlotView>(true);
            for (int index = 0; index < slots.Length; index++)
            {
                if (slots[index].gameObject.activeSelf)
                {
                    return slots[index];
                }
            }

            return null;
        }

        private RectTransform CreateAnchor(string name, Vector2 anchoredPosition)
        {
            var anchorObject = new GameObject(name, typeof(RectTransform));
            RectTransform anchor = anchorObject.GetComponent<RectTransform>();
            anchor.SetParent(_instance.transform, false);
            anchor.anchorMin = new Vector2(0.5f, 0.5f);
            anchor.anchorMax = new Vector2(0.5f, 0.5f);
            anchor.sizeDelta = new Vector2(40f, 40f);
            anchor.anchoredPosition = anchoredPosition;
            return anchor;
        }

        private sealed class NoOpContextActionProvider : ILootContextActionProvider
        {
            public void CollectActions(
                in LootContextActionContext context,
                List<LootContextActionDescriptor> actions)
            {
            }

            public bool TryExecute(
                LootContextActionId actionId,
                in LootContextActionContext context) => false;
        }
    }
}
#endif
