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
        private const string PlayerPrefabPath = "Assets/Prefabs/NetworkPlayer.prefab";

        private GameObject _instance;
        private RaidInventoryView _view;

        [SetUp]
        public void SetUp()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            Assert.That(prefab, Is.Not.Null);

            _instance = Object.Instantiate(prefab);
            _instance.SetActive(false);
            _view = _instance.GetComponentInChildren<RaidInventoryView>(true);
            Assert.That(_view, Is.Not.Null);
            Assert.That(_view.PlayerPanel, Is.Not.Null);
            Assert.That(_view.ContainerPanel, Is.Not.Null);
            Assert.That(_view.TakeAllButton, Is.Not.Null);
            Assert.That(_view.ContextMenu, Is.Not.Null);
            Assert.That(_instance.GetComponent<PlayerLootDropNetworkController>(), Is.Not.Null);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_instance);
        }

        [UnityTest]
        public IEnumerator PlayerPanelPoolIsStableAndClearKeepsCapacity()
        {
            RaidLootPanelView panel = _view.PlayerPanel;
            Assert.That(panel.EnsureSlotCount(2), Is.True);
            yield return null;

            Transform slots = panel.transform.Find("SlotsGrid");
            Assert.That(slots, Is.Not.Null);
            GameObject firstSlot = slots.GetChild(slots.childCount - 2).gameObject;
            GameObject secondSlot = slots.GetChild(slots.childCount - 1).gameObject;

            Assert.That(panel.EnsureSlotCount(2), Is.True);
            var data = new List<RaidInventorySlotData>
            {
                RaidInventorySlotData.Create(new LootEntry(new LootId("coin"), 4), null, null),
                RaidInventorySlotData.Empty
            };

            Assert.That(panel.Present(data, 40, false, false, default), Is.True);
            panel.ClearContent();

            Assert.That(panel.SlotCount, Is.EqualTo(2));
            Assert.That(slots.GetChild(slots.childCount - 2).gameObject, Is.SameAs(firstSlot));
            Assert.That(slots.GetChild(slots.childCount - 1).gameObject, Is.SameAs(secondSlot));
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
            RaidLootPanelView panel = _view.ContainerPanel;
            Assert.That(panel.EnsureSlotCount(1), Is.True);
            var data = new List<RaidInventorySlotData>
            {
                RaidInventorySlotData.Create(new LootEntry(new LootId("coin"), 4), null, null)
            };
            Assert.That(panel.Present(data, null, false, true, default), Is.True);
            yield return null;

            Transform slots = panel.transform.Find("SlotsGrid");
            Assert.That(slots, Is.Not.Null);
            RaidInventorySlotView slot = slots.GetChild(slots.childCount - 1)
                .GetComponent<RaidInventorySlotView>();
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

            RaidInventorySlotView slot = panel.transform.Find("SlotsGrid")
                .GetChild(panel.transform.Find("SlotsGrid").childCount - 1)
                .GetComponent<RaidInventorySlotView>();
            int transferCount = 0;
            LootId requestedLoot = default;
            Vector2 requestedPosition = default;
            panel.SelectionRequested += (_, _) => transferCount++;
            panel.ContextRequested += (lootId, position) =>
            {
                requestedLoot = lootId;
                requestedPosition = position;
            };

            slot.GetComponent<Button>().onClick.Invoke();
            slot.OnPointerClick(new PointerEventData(null)
            {
                button = PointerEventData.InputButton.Right,
                position = new Vector2(320f, 240f)
            });

            Assert.That(transferCount, Is.Zero);
            Assert.That(requestedLoot, Is.EqualTo(new LootId("coin")));
            Assert.That(requestedPosition, Is.EqualTo(new Vector2(320f, 240f)));
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

            Assert.That(_view.ContextMenu.Show(actions, new Vector2(100000f, 100000f)), Is.True);
            yield return null;

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
            var menuRect = (RectTransform)_view.ContextMenu.transform;
            var canvasRect = (RectTransform)menuRect.parent;
            Assert.That(menuRect.anchoredPosition.x + menuRect.rect.width * 0.5f,
                Is.LessThanOrEqualTo(canvasRect.rect.xMax + 0.01f));
            Assert.That(menuRect.anchoredPosition.y + menuRect.rect.height * 0.5f,
                Is.LessThanOrEqualTo(canvasRect.rect.yMax + 0.01f));

            int dismissCount = 0;
            _view.ContextMenu.DismissRequested += () => dismissCount++;
            _view.ContextMenu.OnPointerExit(new PointerEventData(null));
            Assert.That(dismissCount, Is.Zero);
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
