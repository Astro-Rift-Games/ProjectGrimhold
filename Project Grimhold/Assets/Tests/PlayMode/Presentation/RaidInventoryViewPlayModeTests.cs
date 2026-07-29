#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
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
    }
}
#endif
