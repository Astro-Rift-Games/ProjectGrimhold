#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
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
        private const string SharedInventoryPrefabPath = "Assets/Prefabs/UI/PlayerUI/RaidInventoryUI.prefab";

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

        [Test]
        public void ReservedOffHand_IsVisualOnly_AndResetsWhenReleased()
        {
            var root = new GameObject("Reserved slot", typeof(RectTransform), typeof(RaidInventorySlotView));
            root.transform.SetParent(_canvasObject.transform);
            var slot = root.GetComponent<RaidInventorySlotView>();
            var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            icon.transform.SetParent(root.transform);
            var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            label.transform.SetParent(root.transform);
            var background = root.AddComponent<Image>();
            var button = root.AddComponent<Button>();
            typeof(RaidInventorySlotView).GetField("_button", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(slot, button);
            typeof(RaidInventorySlotView).GetField("_icon", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(slot, icon);
            typeof(RaidInventorySlotView).GetField("_nameText", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(slot, label);
            typeof(RaidInventorySlotView).GetField("_background", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(slot, background);
            var sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.zero);
            try
            {
                var data = RaidInventorySlotData.Create(new LootEntry(new LootId("Greatsword"), 1), null, sprite);
                int actions = 0;
                slot.TooltipRequested += (_, __) => actions++;
                slot.ContextRequested += (_, __) => actions++;
                slot.DragStarted += _ => actions++;
                slot.DropReceived += (_, __) => actions++;
                slot.SetDragLocation(DragSlotLocation.Equipment, EquipmentSlot.WeaponSetAOffHand);
                slot.PresentEquipmentSlot(EquipmentSlot.WeaponSetAOffHand, in data, true, true, true);
                Assert.That(icon.sprite, Is.SameAs(sprite));
                Assert.That(icon.color.a, Is.LessThan(1f));
                Assert.That(label.text, Does.Contain("Reservado por Greatsword"));
                Assert.That(slot.IsOccupied, Is.False, "Reservation is not another item");
                slot.OnPointerEnter(null);
                slot.OnPointerClick(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Right });
                slot.OnBeginDrag(new PointerEventData(EventSystem.current));
                slot.OnDrop(null);
                Assert.That(actions, Is.Zero);
                slot.SetDropHighlight(DropHighlightState.Valid);
                Assert.That(background.color.r, Is.GreaterThan(background.color.g));
                slot.PresentEquipmentSlot(EquipmentSlot.WeaponSetAOffHand, in data, false, true);
                Assert.That(icon.color.a, Is.EqualTo(1f));
                Assert.That(slot.IsOccupied, Is.True);
            }
            finally { Object.DestroyImmediate(sprite); }
        }

        [Test]
        public void ReservedProjection_UsesCorrespondingMainHandData()
        {
            var data = new RaidInventorySlotData[EquipmentSlotRules.AllSlots.Length];
            for (int i = 0; i < data.Length; i++)
            {
                var slot = EquipmentSlotRules.AllSlots[i];
                if (EquipmentSlotRules.IsMainHandSlot(slot))
                    data[i] = RaidInventorySlotData.Create(new LootEntry(new LootId(slot.ToString()), 1), null, null);
            }
            _view.PresentEquipmentSlots(data, WeaponSetSlot.SetA, true, true, true);
            var views = (RaidInventorySlotView[])typeof(RaidInventoryView)
                .GetField("_equipmentSlotViews", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_view);
            foreach (var offHand in new[] { EquipmentSlot.WeaponSetAOffHand, EquipmentSlot.WeaponSetBOffHand })
            {
                var view = views[System.Array.IndexOf(EquipmentSlotRules.AllSlots, offHand)];
                var text = (TMP_Text)typeof(RaidInventorySlotView).GetField("_nameText", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
                Assert.That(text.text, Does.Contain("Reservado por " + EquipmentSlotRules.GetMainHandSlot(EquipmentSlotRules.GetWeaponSet(offHand))));
            }
        }

        [Test]
        public void WeaponSets_HighlightBothHands_DimInactive_AndSwapOrNoneResets()
        {
            var data = new RaidInventorySlotData[EquipmentSlotRules.AllSlots.Length];
            _view.PresentEquipmentSlots(data, WeaponSetSlot.SetA, true);
            var slots = (RaidInventorySlotView[])typeof(RaidInventoryView)
                .GetField("_equipmentSlotViews", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_view);
            var colors = new Color[slots.Length];
            for (int i = 0; i < slots.Length; i++)
                colors[i] = ((Image)typeof(RaidInventorySlotView).GetField("_background", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(slots[i])).color;
            _view.PresentEquipmentSlots(data, WeaponSetSlot.SetB, true);
            for (int i = 0; i < slots.Length; i++)
            {
                if (EquipmentSlotRules.GetWeaponSet(EquipmentSlotRules.AllSlots[i]) == WeaponSetSlot.None) continue;
                var image = (Image)typeof(RaidInventorySlotView).GetField("_background", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(slots[i]);
                Assert.That(image.color, Is.Not.EqualTo(colors[i]));
                if (EquipmentSlotRules.GetWeaponSet(EquipmentSlotRules.AllSlots[i]) == WeaponSetSlot.SetA)
                    Assert.That(image.color.a, Is.LessThan(colors[i].a));
            }
            _view.PresentEquipmentSlots(data, WeaponSetSlot.None, true);
            Color? neutral = null;
            for (int i = 0; i < slots.Length; i++)
            {
                if (EquipmentSlotRules.GetWeaponSet(EquipmentSlotRules.AllSlots[i]) == WeaponSetSlot.None) continue;
                var image = (Image)typeof(RaidInventorySlotView).GetField("_background", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(slots[i]);
                if (neutral.HasValue) Assert.That(image.color, Is.EqualTo(neutral.Value));
                neutral = image.color;
            }
        }

        [UnityTest]
        public IEnumerator SortButton_EmitsOnlyWhenEnabled_AndUnhooksOnDestroy()
        {
            var staging = new GameObject("Inactive sort view staging");
            staging.transform.SetParent(_canvasObject.transform, false);
            staging.SetActive(false);
            var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SharedInventoryPrefabPath), staging.transform);
            var view = instance.GetComponent<RaidInventoryView>();
            var buttonObject = new GameObject("Sort", typeof(RectTransform), typeof(Button));
            buttonObject.transform.SetParent(staging.transform, false);
            var button = buttonObject.GetComponent<Button>();
            typeof(RaidInventoryView).GetField("_sortButton", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(view, button);
            int requests = 0;
            view.SortRequested += () => requests++;
            instance.SetActive(true);
            staging.SetActive(true);
            view.SetScreenVisible(true);
            yield return null;
            view.SetSortInteractable(false);
            button.onClick.Invoke();
            Assert.That(requests, Is.Zero);
            view.SetSortInteractable(true);
            button.onClick.Invoke();
            Assert.That(requests, Is.EqualTo(1));
            Object.DestroyImmediate(instance);
            button.onClick.Invoke();
            Assert.That(requests, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator InventoryAndContainerSlots_ShowAndDismissSharedTooltip()
        {
            LootDefinition definition = CreateTooltipDefinition("Mineral", LootCategory.Material);
            RaidInventorySlotData data = RaidInventorySlotData.Create(
                new LootEntry(new LootId("mineral"), 2),
                definition,
                null);
            Object.DestroyImmediate(definition);

            RaidLootPanelView[] panels = { _view.PlayerPanel, _view.ContainerPanel };
            for (int index = 0; index < panels.Length; index++)
            {
                RaidLootPanelView panel = panels[index];
                panel.SetVisible(true);
                Assert.That(panel.EnsureSlotCount(1), Is.True);
                Assert.That(panel.Present(new[] { data }, null, false, false, default), Is.True);
                yield return null;

                RaidInventorySlotView slot = GetFirstActiveSlot(panel);
                slot.OnPointerEnter(new PointerEventData(null));
                yield return null;

                Assert.That(_view.TooltipView.IsOpen, Is.True);
                Assert.That(_view.TooltipView.CurrentAnchor, Is.SameAs(slot.transform));
                Assert.That(_view.TooltipView.ContentText.text, Does.Contain("<b>Mineral</b>"));
                Assert.That(_view.TooltipView.ContentText.text, Does.Contain("Sin estadísticas de equipamiento"));

                slot.OnPointerExit(new PointerEventData(null));
                Assert.That(_view.TooltipView.IsOpen, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator EquipmentSlot_HoverShowsTooltipAndContextRequestHidesIt()
        {
            LootDefinition definition = CreateTooltipDefinition("Casco roto", LootCategory.Helmet);
            RaidInventorySlotData helmetData = RaidInventorySlotData.Create(
                new LootEntry(new LootId("broken_helmet"), 1),
                definition,
                null);
            Object.DestroyImmediate(definition);
            var slots = new List<RaidInventorySlotData>();
            for (int index = 0; index < EquipmentSlotRules.AllSlots.Length; index++)
            {
                slots.Add(index == 2 ? helmetData : RaidInventorySlotData.Empty);
            }

            _view.SetScreenVisible(true);
            _view.SetEquipmentPanelVisible(true);
            _view.PresentEquipmentSlots(slots, WeaponSetSlot.None, true);
            yield return null;

            RaidInventorySlotView helmet = System.Array.Find(
                _view.GetComponentsInChildren<RaidInventorySlotView>(true),
                candidate => candidate.name == "Helmet");
            Assert.That(helmet, Is.Not.Null);
            helmet.OnPointerEnter(new PointerEventData(null));
            Assert.That(_view.TooltipView.IsOpen, Is.True);
            Assert.That(_view.TooltipView.ContentText.text, Does.Contain("Casco roto"));
            Assert.That(_view.TooltipView.ContentText.text, Does.Contain("configuración inválida"));

            helmet.OnPointerClick(new PointerEventData(null)
            {
                button = PointerEventData.InputButton.Right
            });
            Assert.That(_view.TooltipView.IsOpen, Is.False);
        }

        [UnityTest]
        public IEnumerator Tooltip_ClampsToCanvasAndClearRemovesState()
        {
            _instance.SetActive(true);
            _view.SetScreenVisible(true);
            yield return null;

            var presentation = new EquipmentTooltipPresentation(
                EquipmentTooltipPresentationStatus.FunctionalStatistics,
                "Objeto",
                "Daño base: 30\nIntervalo: 0,75 s");
            RectTransform canvasRect = (RectTransform)_instance.transform;
            Vector2[] edgePositions =
            {
                new(-300f, -220f),
                new(-300f, 220f),
                new(300f, -220f),
                new(300f, 220f)
            };
            for (int index = 0; index < edgePositions.Length; index++)
            {
                RectTransform anchor = CreateAnchor($"TooltipEdge{index}", edgePositions[index]);
                Assert.That(_view.TooltipView.Show(in presentation, anchor), Is.True);
                yield return null;

                Bounds tooltipBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                    canvasRect,
                    _view.TooltipView.transform as RectTransform);
                Assert.That(tooltipBounds.min.x, Is.GreaterThanOrEqualTo(canvasRect.rect.xMin - 0.01f));
                Assert.That(tooltipBounds.max.x, Is.LessThanOrEqualTo(canvasRect.rect.xMax + 0.01f));
                Assert.That(tooltipBounds.min.y, Is.GreaterThanOrEqualTo(canvasRect.rect.yMin - 0.01f));
                Assert.That(tooltipBounds.max.y, Is.LessThanOrEqualTo(canvasRect.rect.yMax + 0.01f));
            }

            _view.ClearContent();
            Assert.That(_view.TooltipView.IsOpen, Is.False);
            Assert.That(_view.TooltipView.CurrentAnchor, Is.Null);
        }

        [UnityTest]
        public IEnumerator ReplacingSlotContentAndClosingScreenDismissTooltip()
        {
            LootDefinition firstDefinition = CreateTooltipDefinition("Mineral", LootCategory.Material);
            LootDefinition secondDefinition = CreateTooltipDefinition("Madera", LootCategory.Material);
            RaidInventorySlotData first = RaidInventorySlotData.Create(
                new LootEntry(new LootId("mineral"), 1), firstDefinition, null);
            RaidInventorySlotData second = RaidInventorySlotData.Create(
                new LootEntry(new LootId("wood"), 1), secondDefinition, null);
            Object.DestroyImmediate(firstDefinition);
            Object.DestroyImmediate(secondDefinition);

            RaidLootPanelView panel = _view.PlayerPanel;
            Assert.That(panel.EnsureSlotCount(1), Is.True);
            Assert.That(panel.Present(new[] { first }, null, false, false, default), Is.True);
            RaidInventorySlotView slot = GetFirstActiveSlot(panel);
            slot.OnPointerEnter(new PointerEventData(null));
            Assert.That(_view.TooltipView.IsOpen, Is.True);

            Assert.That(panel.Present(new[] { second }, null, false, false, default), Is.True);
            Assert.That(_view.TooltipView.IsOpen, Is.False);

            slot.OnPointerEnter(new PointerEventData(null));
            Assert.That(_view.TooltipView.IsOpen, Is.True);
            _view.SetScreenVisible(false);
            Assert.That(_view.TooltipView.IsOpen, Is.False);
            yield return null;
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
        public IEnumerator PlayerPanelSupportsThirtyLogicalSlots()
        {
            RaidLootPanelView panel = _view.PlayerPanel;
            Assert.That(panel.AuthoredSlotCount, Is.EqualTo(LocalProfileSnapshot.MaxLoadoutSlots));
            Assert.That(panel.EnsureSlotCount(LocalProfileSnapshot.MaxLoadoutSlots), Is.True);
            yield return null;

            Transform slots = panel.transform.Find("SlotsGrid");
            Assert.That(slots, Is.Not.Null);
            Assert.That(panel.SlotCount, Is.EqualTo(30));
            Assert.That(slots.childCount, Is.EqualTo(30));
            for (int index = 0; index < slots.childCount; index++)
            {
                Assert.That(slots.GetChild(index).gameObject.activeSelf, Is.True, $"Slot {index + 1}");
            }
        }

        [UnityTest]
        public IEnumerator OccupiedContainerSlot_LeftClickDoesNotTransfer_RightClickRetainsFullStack()
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
                Is.EqualTo(new[] { LootTransferQuantityMode.FullStack }),
                "Do not restore obsolete left-click transfer while adding addressed gestures.");

            panel.RefreshInteraction(false, default);
            button.onClick.Invoke();
            slot.OnPointerClick(new PointerEventData(null)
            {
                button = PointerEventData.InputButton.Right
            });
            Assert.That(receivedModes, Has.Count.EqualTo(1));

            slot.Clear();
            slot.OnPointerClick(new PointerEventData(null)
            {
                button = PointerEventData.InputButton.Right
            });
            Assert.That(receivedModes, Has.Count.EqualTo(1));
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

            _view.PresentEquipmentSlots(slots, WeaponSetSlot.None, true);
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

        [TestCase(false)]
        [TestCase(true)]
        public void PhysicalPanel_SecondDuplicateOwnsSelectionContextAndDrag(bool differentOrigin)
        {
            RaidLootPanelView panel = _view.PlayerPanel;
            Assert.That(panel.EnsureSlotCount(30), Is.True);
            panel.SetDragLocation(DragSlotLocation.Inventory);
            RaidInventorySlotData[] data = PhysicalData(19, differentOrigin);
            Assert.That(panel.Present(data, 13, false, RaidLootSlotInteractionMode.ContextMenu,
                new LootId("bone"), 7), Is.True);
            RaidInventorySlotView[] slots = AuthoredSlots(panel);
            for (int index = 0; index < 30; index++)
            {
                Assert.That(slots[index].gameObject.activeSelf, Is.True);
                Assert.That(ReadData(slots[index]).SourceInventorySlotIndex, Is.EqualTo(index));
                Assert.That(ReadData(slots[index]).ExpectedInventoryRevision, Is.EqualTo(19));
                Assert.That(typeof(RaidInventorySlotView).GetField("_isSelected", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(slots[index]), Is.EqualTo(index == 7), $"Selection leaked to physical slot {index}.");
            }
            Assert.That(slots[1].IsOccupied, Is.False, "An interior hole must not be packed away.");
            Assert.That(ReadData(slots[0]).Amount, Is.EqualTo(10));
            Assert.That(ReadData(slots[7]).Amount, Is.EqualTo(3));
            Assert.That(ReadData(slots[7]).Origin, Is.EqualTo(data[7].Origin));
            int legacyContexts = 0;
            int transfers = 0;
            RaidInventorySlotData captured = default;
            RectTransform anchor = null;
            DragPayload drag = default;
            panel.ContextRequested += (_, __) => legacyContexts++;
            panel.SelectionRequested += (_, __) => transfers++;
            panel.InventoryContextRequested += (selected, selectedAnchor) => { captured = selected; anchor = selectedAnchor; };
            panel.DragStarted += payload => drag = payload;
            slots[7].GetComponent<Button>().onClick.Invoke();
            slots[7].OnPointerClick(new PointerEventData(null) { button = PointerEventData.InputButton.Left });
            Assert.That(captured.IsOccupied, Is.False);
            slots[7].OnPointerClick(new PointerEventData(null) { button = PointerEventData.InputButton.Right });
            Assert.That(captured.SourceInventorySlotIndex, Is.EqualTo(7));
            Assert.That(captured.ExpectedInventoryRevision, Is.EqualTo(19));
            Assert.That(captured.Amount, Is.EqualTo(3));
            Assert.That(captured.Origin, Is.EqualTo(data[7].Origin));
            Assert.That(anchor, Is.SameAs(slots[7].transform));
            Assert.That(legacyContexts, Is.Zero, "Do not also dispatch an ID-only action for a physical slot.");
            Assert.That(transfers, Is.Zero);
            slots[7].OnBeginDrag(new PointerEventData(null) { button = PointerEventData.InputButton.Left });
            Assert.That(drag.SourceInventorySlotIndex, Is.EqualTo(7));
            Assert.That(drag.ExpectedInventoryRevision, Is.EqualTo(19));
            Assert.That(drag.Amount, Is.EqualTo(3));
            Assert.That(drag.LootId, Is.EqualTo(new LootId("bone")));
            Assert.That(drag.Source, Is.EqualTo(DragSlotLocation.Inventory));
        }

        [Test]
        public void PhysicalTransferGesture_RightClickCarriesSelectedSnapshot_WithoutLegacyOrLeftClickDispatch()
        {
            RaidLootPanelView panel = _view.PlayerPanel;
            Assert.That(panel.EnsureSlotCount(30), Is.True);
            Assert.That(panel.Present(PhysicalData(19, false), null, false,
                RaidLootSlotInteractionMode.Transfer, default), Is.True);
            RaidInventorySlotData selected = default;
            LootTransferQuantityMode mode = default;
            int addressed = 0;
            int legacy = 0;
            panel.InventorySelectionRequested += (data, quantityMode) => { selected = data; mode = quantityMode; addressed++; };
            panel.SelectionRequested += (_, __) => legacy++;
            RaidInventorySlotView slot = AuthoredSlots(panel)[7];
            slot.GetComponent<Button>().onClick.Invoke();
            slot.OnPointerClick(new PointerEventData(null) { button = PointerEventData.InputButton.Left });
            Assert.That(addressed, Is.Zero);
            slot.OnPointerClick(new PointerEventData(null) { button = PointerEventData.InputButton.Right });
            Assert.That(addressed, Is.EqualTo(1));
            Assert.That(legacy, Is.Zero);
            Assert.That(mode, Is.EqualTo(LootTransferQuantityMode.FullStack));
            Assert.That(selected.SourceInventorySlotIndex, Is.EqualTo(7));
            Assert.That(selected.ExpectedInventoryRevision, Is.EqualTo(19));
            Assert.That(selected.Amount, Is.EqualTo(3));
        }

        [Test]
        public void PhysicalPanel_IdentityOnlySuccessCannotPulseFirstMatchingStack_ContainerStillCan()
        {
            RaidLootPanelView panel = _view.PlayerPanel;
            Assert.That(panel.EnsureSlotCount(30), Is.True);
            Assert.That(panel.Present(PhysicalData(19, true), null, false, false, default), Is.True);
            _view.ShowTransferFeedback("Transferencia confirmada");
            Assert.That(panel.ShowTransferSuccess(new LootId("bone")), Is.False);
            foreach (RaidInventorySlotView slot in AuthoredSlots(panel))
                Assert.That(slot.IsTransferFeedbackActive, Is.False);
            Assert.That(_view.TransferFeedbackText.text, Is.EqualTo("Transferencia confirmada"));
            RaidLootPanelView container = _view.ContainerPanel;
            Assert.That(container.EnsureSlotCount(1), Is.True);
            Assert.That(container.Present(new[] { RaidInventorySlotData.Create(
                new LootEntry(new LootId("bone"), 13), null, null) }, null, false, false, default), Is.True);
            Assert.That(container.ShowTransferSuccess(new LootId("bone")), Is.True);
            Assert.That(AuthoredSlots(container)[0].IsTransferFeedbackActive, Is.True);
        }

        [UnityTest]
        public IEnumerator PhysicalSlot_SameItemWithNewRevisionDismissesStaleTooltip()
        {
            LootDefinition definition = CreateTooltipDefinition("Bone", LootCategory.Material);
            SetPrivateField(definition, "_id", "bone");
            RaidLootPanelView panel = _view.PlayerPanel;
            Assert.That(panel.EnsureSlotCount(1), Is.True);
            var first = RaidInventorySlotData.CreateInventorySlot(new LootEntry(new LootId("bone"), 3),
                definition, null, 0, 19, RaidLootOrigin.Dungeon);
            var second = RaidInventorySlotData.CreateInventorySlot(new LootEntry(new LootId("bone"), 3),
                definition, null, 0, 20, RaidLootOrigin.Dungeon);
            Object.DestroyImmediate(definition);
            Assert.That(panel.Present(new[] { first }, null, false, false, default), Is.True);
            RaidInventorySlotView slot = AuthoredSlots(panel)[0];
            slot.OnPointerEnter(new PointerEventData(null));
            Assert.That(_view.TooltipView.IsOpen, Is.True);
            Assert.That(panel.Present(new[] { second }, null, false, false, default), Is.True);
            Assert.That(_view.TooltipView.IsOpen, Is.False, "Matching LootId does not make an old physical snapshot current.");
            yield return null;
        }

        private static RaidInventorySlotData[] PhysicalData(int revision, bool differentOrigin)
        {
            var result = new RaidInventorySlotData[30];
            for (int index = 0; index < result.Length; index++)
                result[index] = RaidInventorySlotData.EmptyInventorySlot(index, revision);
            RaidParticipantId.TryCreate(2, out RaidParticipantId participant);
            RaidLootOrigin.TryCreatePlayer(participant, out RaidLootOrigin playerOrigin);
            var id = new LootId("bone");
            result[0] = RaidInventorySlotData.CreateInventorySlot(new LootEntry(id, 10), null, null,
                0, revision, RaidLootOrigin.Dungeon);
            result[7] = RaidInventorySlotData.CreateInventorySlot(new LootEntry(id, 3), null, null,
                7, revision, differentOrigin ? playerOrigin : RaidLootOrigin.Dungeon);
            return result;
        }

        private static RaidInventorySlotView[] AuthoredSlots(RaidLootPanelView panel) =>
            (RaidInventorySlotView[])typeof(RaidLootPanelView).GetField("_authoredSlots",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(panel);

        private static RaidInventorySlotData ReadData(RaidInventorySlotView slot) =>
            (RaidInventorySlotData)typeof(RaidInventorySlotView).GetField("_slotData",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(slot);

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
