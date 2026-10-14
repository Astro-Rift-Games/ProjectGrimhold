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
            Assert.That(_view.TooltipView, Is.Not.Null);
            Assert.That(
                _view.GetComponentsInChildren<EquipmentTooltipView>(true),
                Has.Length.EqualTo(1));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_canvasObject);
        }

        [UnityTest]
        public IEnumerator StashLoadoutAndEquipmentSlotsShareOneTooltip()
        {
            LootDefinition definition = CreateTooltipDefinition("Mineral", LootCategory.Material);
            RaidInventorySlotData material = RaidInventorySlotData.Create(
                new LootEntry(new LootId("mineral"), 2),
                definition,
                null);
            Object.DestroyImmediate(definition);

            _view.DisplayStash(new[] { material });
            _view.DisplayLoadout(new[] { material });
            var equipment = new RaidInventorySlotData[EquipmentSlotRules.AllSlots.Length];
            equipment[2] = material;
            _view.DisplayPreparedEquipment(equipment);
            yield return null;

            RaidInventorySlotView stashSlot = FindOccupiedSlot(_stashPanel);
            stashSlot.OnPointerEnter(new PointerEventData(null));
            Assert.That(_view.TooltipView.IsOpen, Is.True);
            Assert.That(_view.TooltipView.CurrentAnchor, Is.SameAs(stashSlot.transform));

            RaidInventorySlotView loadoutSlot = FindOccupiedSlot(_loadoutPanel);
            loadoutSlot.OnPointerEnter(new PointerEventData(null));
            Assert.That(_view.TooltipView.CurrentAnchor, Is.SameAs(loadoutSlot.transform));

            RaidInventorySlotView helmet = Array.Find(
                _view.GetComponentsInChildren<RaidInventorySlotView>(true),
                candidate => candidate.name == "Helmet");
            Assert.That(helmet, Is.Not.Null);
            helmet.OnPointerEnter(new PointerEventData(null));
            Assert.That(_view.TooltipView.CurrentAnchor, Is.SameAs(helmet.transform));

            _instance.SetActive(false);
            Assert.That(_view.TooltipView.IsOpen, Is.False);
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

        [UnityTest]
        public IEnumerator StashPagination_AcceptedExcessRemainsReachableWithCorrectIdentityAfterBackAndReopen()
        {
            var items = new RaidInventorySlotData[70];
            LootDefinition definition = CreateTooltipDefinition("Capacity item", LootCategory.Material);
            for (int i = 0; i < items.Length; i++)
                items[i] = RaidInventorySlotData.Create(new LootEntry(new LootId($"capacity_{i}"), i + 1), definition, null);
            Object.DestroyImmediate(definition);
            _view.DisplayStash(items);
            yield return null;
            Button previous = Array.Find(_instance.GetComponentsInChildren<Button>(true), b => b.name == "StashPreviousPage");
            Button next = Array.Find(_instance.GetComponentsInChildren<Button>(true), b => b.name == "StashNextPage");
            TMP_Text label = Array.Find(_instance.GetComponentsInChildren<TMP_Text>(true), t => t.name == "StashPageLabel");
            Assert.That(previous, Is.Not.Null, "The authored prefab must expose previous-page navigation.");
            Assert.That(next, Is.Not.Null, "The authored prefab must expose next-page navigation.");
            Assert.That(label, Is.Not.Null);
            Assert.That(_stashPanel.GetComponentsInChildren<RaidInventorySlotView>(true), Has.Length.EqualTo(16));
            LootId requested = default;
            _view.TransferRequested += (id, fromStash, mode) => { Assert.That(fromStash, Is.True); requested = id; };
            for (int cycle = 0; cycle < 2; cycle++)
            {
                Assert.That(previous.interactable, Is.False, "Opening the screen must reset to the first page.");
                Assert.That(label.text, Is.EqualTo("1 / 5"));
                for (int i = 0; i < 4; i++) { Assert.That(next.interactable, Is.True); next.onClick.Invoke(); }
                yield return null;
                Assert.That(label.text, Is.Not.Empty);
                RaidInventorySlotView last = Array.Find(_stashPanel.GetComponentsInChildren<RaidInventorySlotView>(true),
                    s => s.IsOccupied && s.LootId == new LootId("capacity_69"));
                Assert.That(last, Is.Not.Null, "Accepted server excess must not be truncated to 60 or the authored 16 slots.");
                last.OnPointerEnter(new PointerEventData(null));
                Assert.That(_view.TooltipView.CurrentAnchor, Is.SameAs(last.transform));
                last.OnPointerClick(new PointerEventData(null) { button = PointerEventData.InputButton.Right });
                GetFirstVisibleContextAction().GetComponent<Button>().onClick.Invoke();
                Assert.That(requested, Is.EqualTo(new LootId("capacity_69")));
                previous.onClick.Invoke();
                next.onClick.Invoke();
                _instance.SetActive(false);
                _instance.SetActive(true);
                _view.DisplayStash(items);
                yield return null;
                Assert.That(previous.interactable, Is.False, "Reopening must reset to page one, not retain page five.");
                Assert.That(label.text, Is.EqualTo("1 / 5"));
                Assert.That(FindOccupiedSlot(_stashPanel).LootId, Is.EqualTo(new LootId("capacity_0")));
            }
        }

        [UnityTest]
        public IEnumerator StashPagination_NavigationFitsBetweenGridAndTakeAllAfterLayout()
        {
            _view.DisplayStash(Array.Empty<RaidInventorySlotData>());
            yield return null;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)_instance.transform);
            Canvas.ForceUpdateCanvases();

            GridLayoutGroup grid = _stashPanel.GetComponentInChildren<GridLayoutGroup>(true);
            Assert.That(grid, Is.Not.Null, "The real Stash panel must contain its authored grid.");
            Rect gridRect = WorldRect((RectTransform)grid.transform);
            Rect takeAllRect = WorldRect((RectTransform)GetSerializedField<Button>("_takeAllButton").transform);
            foreach (string name in new[] { "StashPreviousPage", "StashNextPage", "StashPageLabel" })
            {
                RectTransform control = Array.Find(_instance.GetComponentsInChildren<RectTransform>(true), r => r.name == name);
                Assert.That(control, Is.Not.Null, name);
                Rect navigationRect = WorldRect(control);
                Assert.That(navigationRect.yMax, Is.LessThanOrEqualTo(gridRect.yMin + 0.01f),
                    $"{name} must not overlap the authored Stash grid.");
                Assert.That(navigationRect.yMin, Is.GreaterThanOrEqualTo(takeAllRect.yMax - 0.01f),
                    $"{name} must remain above Take All.");
            }
        }

        private static Rect WorldRect(RectTransform transform)
        {
            var corners = new Vector3[4];
            transform.GetWorldCorners(corners);
            Vector2 min = corners[0];
            Vector2 max = corners[0];
            for (int i = 1; i < corners.Length; i++)
            {
                min = Vector2.Min(min, corners[i]);
                max = Vector2.Max(max, corners[i]);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        [UnityTest]
        public IEnumerator StashPagination_CustomCapacityPadsOnlyToCapacityAndClampsAfterRemoval()
        {
            var items = new RaidInventorySlotData[33];
            for (int i = 0; i < items.Length; i++)
                items[i] = RaidInventorySlotData.Create(new LootEntry(new LootId($"capacity_{i}"), 1), null, null);
            _view.DisplayStash(items, 17);
            yield return null;
            Button next = Array.Find(_instance.GetComponentsInChildren<Button>(true), b => b.name == "StashNextPage");
            Button previous = Array.Find(_instance.GetComponentsInChildren<Button>(true), b => b.name == "StashPreviousPage");
            Assert.That(next, Is.Not.Null);
            Assert.That(previous, Is.Not.Null);
            next.onClick.Invoke();
            next.onClick.Invoke();
            Assert.That(_stashPanel.SlotCount, Is.EqualTo(1));
            _view.DisplayStash(Array.Empty<RaidInventorySlotData>(), 17);
            Assert.That(_stashPanel.SlotCount, Is.EqualTo(1)); // Clamped to second/last page, one configured empty slot.
            Assert.That(next.interactable, Is.False);
            Assert.That(previous.interactable, Is.True);
            previous.onClick.Invoke();
            Assert.That(_stashPanel.SlotCount, Is.EqualTo(16));
            Assert.That(previous.interactable, Is.False);
        }

        private void OpenContextMenu(RaidLootPanelView panel)
        {
            RaidInventorySlotView occupied = FindOccupiedSlot(panel);
            Assert.That(occupied, Is.Not.Null);

            occupied.OnPointerEnter(new PointerEventData(null));
            Assert.That(_view.TooltipView.IsOpen, Is.False,
                "Un unresolved definition must not invent tooltip content.");

            occupied.OnPointerClick(new PointerEventData(null)
            {
                button = PointerEventData.InputButton.Right
            });
            Assert.That(_contextMenu.IsOpen, Is.True);
            Assert.That(_view.TooltipView.IsOpen, Is.False);
        }

        private static RaidInventorySlotView FindOccupiedSlot(RaidLootPanelView panel)
        {
            RaidInventorySlotView[] slots = panel.GetComponentsInChildren<RaidInventorySlotView>(true);
            return Array.Find(slots, slot => slot.IsOccupied);
        }

        private static LootDefinition CreateTooltipDefinition(string displayName, LootCategory category)
        {
            LootDefinition definition = ScriptableObject.CreateInstance<LootDefinition>();
            SetPrivateField(definition, "_displayName", displayName);
            SetPrivateField(definition, "_category", category);
            return definition;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
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
