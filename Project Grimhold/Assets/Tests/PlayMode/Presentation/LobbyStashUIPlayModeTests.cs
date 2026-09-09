#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Presentation
{
    public sealed class LobbyStashUIPlayModeTests
    {
        private const string StashPrefabPath = "Assets/Prefabs/UI/StashInventory.prefab";

        private GameObject _canvasObject;
        private GameObject _instance;
        private LobbyStashUI _view;
        private RaidLootPanelView _stashPanel;
        private RaidLootPanelView _loadoutPanel;
        private RaidLootContextMenuView _contextMenu;

        [SetUp]
        public void SetUp()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(StashPrefabPath);
            Assert.That(prefab, Is.Not.Null);

            _canvasObject = new GameObject(
                "LobbyStashUITestCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            _canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            _instance = Object.Instantiate(prefab, _canvasObject.transform, false);
            RectTransform root = (RectTransform)_instance.transform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.anchoredPosition = Vector2.zero;
            root.sizeDelta = Vector2.zero;
            root.localScale = Vector3.one;
            _instance.SetActive(true);

            _view = _instance.GetComponent<LobbyStashUI>();
            Assert.That(_view, Is.Not.Null);
            _stashPanel = GetSerializedField<RaidLootPanelView>("_stashPanel");
            _loadoutPanel = GetSerializedField<RaidLootPanelView>("_loadoutPanel");
            _contextMenu = GetSerializedField<RaidLootContextMenuView>("_contextMenu");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_canvasObject);
        }

        [UnityTest]
        public IEnumerator StashContextMoveAll_TransfersFullStackToInventory()
        {
            LootId lootId = new("coin");
            _view.DisplayStash(new[]
            {
                RaidInventorySlotData.Create(new LootEntry(lootId, 4), null, null)
            });
            _view.DisplayLoadout(Array.Empty<RaidInventorySlotData>());
            yield return null;

            LootId requestedLoot = default;
            bool? requestedIsFromStash = null;
            LootTransferQuantityMode? requestedMode = null;
            _view.TransferRequested += (id, isFromStash, mode) =>
            {
                requestedLoot = id;
                requestedIsFromStash = isFromStash;
                requestedMode = mode;
            };

            OpenContextMenu(_stashPanel);
            yield return null;

            RaidLootContextActionButton action = GetFirstVisibleContextAction();
            Assert.That(action.GetComponentInChildren<TMP_Text>(true).text, Is.EqualTo("Mover todo al Inventario"));
            action.GetComponent<Button>().onClick.Invoke();

            Assert.That(requestedLoot, Is.EqualTo(lootId));
            Assert.That(requestedIsFromStash, Is.True);
            Assert.That(requestedMode, Is.EqualTo(LootTransferQuantityMode.FullStack));
        }

        [UnityTest]
        public IEnumerator LoadoutContextMoveAll_TransfersFullStackToStash()
        {
            LootId lootId = new("coin");
            _view.DisplayStash(Array.Empty<RaidInventorySlotData>());
            _view.DisplayLoadout(new[]
            {
                RaidInventorySlotData.Create(new LootEntry(lootId, 4), null, null)
            });
            yield return null;

            LootId requestedLoot = default;
            bool? requestedIsFromStash = null;
            LootTransferQuantityMode? requestedMode = null;
            _view.TransferRequested += (id, isFromStash, mode) =>
            {
                requestedLoot = id;
                requestedIsFromStash = isFromStash;
                requestedMode = mode;
            };

            OpenContextMenu(_loadoutPanel);
            yield return null;

            RaidLootContextActionButton action = GetFirstVisibleContextAction();
            Assert.That(action.GetComponentInChildren<TMP_Text>(true).text, Is.EqualTo("Mover todo al Stash"));
            action.GetComponent<Button>().onClick.Invoke();

            Assert.That(requestedLoot, Is.EqualTo(lootId));
            Assert.That(requestedIsFromStash, Is.False);
            Assert.That(requestedMode, Is.EqualTo(LootTransferQuantityMode.FullStack));
        }

        private void OpenContextMenu(RaidLootPanelView panel)
        {
            RaidInventorySlotView[] slots = panel.GetComponentsInChildren<RaidInventorySlotView>(true);
            RaidInventorySlotView occupied = Array.Find(slots, slot => slot.IsOccupied);
            Assert.That(occupied, Is.Not.Null);

            occupied.OnPointerClick(new PointerEventData(null)
            {
                button = PointerEventData.InputButton.Right
            });
            Assert.That(_contextMenu.IsOpen, Is.True);
        }

        private RaidLootContextActionButton GetFirstVisibleContextAction()
        {
            RaidLootContextActionButton[] actions =
                _contextMenu.GetComponentsInChildren<RaidLootContextActionButton>(true);
            RaidLootContextActionButton action = Array.Find(actions, candidate => candidate.gameObject.activeSelf);
            Assert.That(action, Is.Not.Null);
            return action;
        }

        private T GetSerializedField<T>(string fieldName) where T : Object
        {
            FieldInfo field = typeof(LobbyStashUI).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            T value = field.GetValue(_view) as T;
            Assert.That(value, Is.Not.Null);
            return value;
        }
    }
}
#endif
