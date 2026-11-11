#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Grimhold.Backend;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Presentation
{
    /// <summary>
    /// Drives the real Stash prefab through its context menu and checks the whole path: view event,
    /// presenter, coordinator, remote call and local profile, then the refreshed view.
    /// </summary>
    public sealed class LobbyStashPresenterPlayModeTests
    {
        private const string StashPrefabPath = "Assets/Prefabs/UI/StashInventory.prefab";
        private const string CatalogPath =
            "Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset";
        private const string ProfileValue = "4a4a4a4a4a4a4a4a4a4a4a4a4a4a4a4a";

        private static readonly LootId Bone = new("bone");

        private LootDefinitionCatalog _catalog;
        private GameObject _contextObject;
        private GameObject _canvasObject;
        private GameObject _instance;
        private LocalProfileStore _store;
        private FakeRemoteComponent _remote;
        private LobbyStashUI _view;
        private ApplicationStashContext[] _suspendedContexts;

        [SetUp]
        public void SetUp()
        {
            _catalog = AssetDatabase.LoadAssetAtPath<LootDefinitionCatalog>(CatalogPath);
            Assert.That(_catalog, Is.Not.Null);
            LocalProfileProvider.SetRemoteCharacterId(new ProfileId(ProfileValue));

            // The runtime bootstrapper keeps its own empty context alive across play sessions; the
            // presenter resolves "the" context, so it must be the only active one in this test.
            _suspendedContexts = Object.FindObjectsByType<ApplicationStashContext>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            foreach (ApplicationStashContext existing in _suspendedContexts)
            {
                existing.gameObject.SetActive(false);
            }
        }

        [TearDown]
        public void TearDown()
        {
            if (_canvasObject != null) Object.DestroyImmediate(_canvasObject);
            if (_contextObject != null) Object.DestroyImmediate(_contextObject);
            foreach (ApplicationStashContext existing in _suspendedContexts)
            {
                if (existing != null) existing.gameObject.SetActive(true);
            }

            LocalProfileProvider.ClearRemoteCharacterId();
        }

        [UnityTest]
        public IEnumerator EquipFromRealStashContext_RoutesStashSourceAndReturnsDisplacedUnitToLoadout()
        {
            var sword = new LootId("rapier");
            var displaced = new LootId("arming_sword");
            CreateScreen(snapshot =>
            {
                snapshot.Stash.Add(new StashItem(sword, 2, 0));
                snapshot.PreparedEquipment = new PreparedEquipmentLoadout(displaced, default);
            });
            yield return null;
            {
                RaidInventorySlotView occupied = FindOccupiedSlot(StashPanel());
                Assert.That(occupied, Is.Not.Null);
                occupied.OnPointerClick(new PointerEventData(null) { button = PointerEventData.InputButton.Right });
                RaidLootContextMenuView menu = _instance.GetComponentInChildren<RaidLootContextMenuView>(true);
                Assert.That(menu.IsOpen, Is.True);
                RaidLootContextActionButton action = Array.Find(
                    menu.GetComponentsInChildren<RaidLootContextActionButton>(true),
                    candidate => candidate.gameObject.activeSelf &&
                        candidate.GetComponentInChildren<TMPro.TMP_Text>(true).text == "Equipar en Set A / Main Hand");
                Assert.That(action, Is.Not.Null);
                action.GetComponent<Button>().onClick.Invoke();
                for (int frame = 0; frame < 5; frame++) yield return null;

                Assert.That(_store.GetPreparedEquipment().WeaponSetAMainHand, Is.EqualTo(sword),
                    "The real Stash menu must retain its source through the presenter.");
                Assert.That(_remote.Calls, Is.EqualTo(new[] { "toLoadout:rapier:1", "equipment" }));
                Assert.That(_store.GetStash(), Is.EqualTo(new[] { new StashItem(sword, 1, 0) }));
                Assert.That(_store.GetLoadout(), Is.EqualTo(new[] { new StashItem(displaced, 1, 0) }));
                Assert.That(FindOccupiedSlot(LoadoutPanel()), Is.Not.Null);
            }
        }

        [UnityTest]
        public IEnumerator MoveAllFromTheStash_ReachesTheBackendThenMovesTheStackAndRefreshesTheView()
        {
            CreateScreen(snapshot => snapshot.Stash.Add(new StashItem(Bone, 3, 0)));
            yield return null;

            ClickMoveAll(StashPanel());
            yield return null;

            Assert.That(_remote.Calls, Is.EqualTo(new[] { "toLoadout:bone:3" }));
            Assert.That(_store.GetStash(), Is.Empty);
            Assert.That(_store.GetLoadout().Single(), Is.EqualTo(new StashItem(Bone, 3, 0)));
            Assert.That(FindOccupiedSlot(LoadoutPanel()), Is.Not.Null, "The Loadout panel must show the moved stack.");
            Assert.That(FindOccupiedSlot(StashPanel()), Is.Null, "The Stash panel must no longer show it.");
        }

        [UnityTest]
        public IEnumerator TakeAllPointerClick_MovesStackFromStash()
        {
            CreateScreen(snapshot => snapshot.Stash.Add(new StashItem(Bone, 3, 0)));
            yield return null;

            Button button = GetButton("_takeAllButton");
            Assert.That(button.IsInteractable(), Is.True, "The authored Take All button must accept pointer clicks.");
            ClickButton(button);
            for (int frame = 0; frame < 5; frame++) yield return null;

            Assert.That(_remote.Calls, Is.EqualTo(new[] { "bulk:toLoadout" }));
            Assert.That(_store.GetStash(), Is.Empty);
            Assert.That(_store.GetLoadout(), Is.EqualTo(new[] { new StashItem(Bone, 3, 0) }));
        }

        [UnityTest]
        public IEnumerator TakeAllWithSeveralStacks_RefreshesTheViewOnceAfterTheBulkCompletes()
        {
            LootId[] ids = DistinctLootIds(3);
            CreateScreen(snapshot =>
            {
                for (int slot = 0; slot < ids.Length; slot++) snapshot.Stash.Add(new StashItem(ids[slot], 2, slot));
            });
            yield return null;
            int before = _view.StashDisplayCount;

            ClickButton(GetButton("_takeAllButton"));
            for (int frame = 0; frame < 8; frame++) yield return null;

            Assert.That(_remote.Calls, Is.EqualTo(new[] { "bulk:toLoadout" }), "Take All is one request.");
            Assert.That(_store.GetStash(), Is.Empty);
            Assert.That(_store.GetLoadout(), Has.Count.EqualTo(3));
            Assert.That(_view.StashDisplayCount - before, Is.EqualTo(1));
            Assert.That(FindOccupiedSlot(StashPanel()), Is.Null);
            Assert.That(LoadoutPanel().GetComponentsInChildren<RaidInventorySlotView>(true).Count(s => s.IsOccupied),
                Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator LeaveAllWithSeveralStacks_RefreshesTheViewOnceAfterTheBulkCompletes()
        {
            LootId[] ids = DistinctLootIds(3);
            CreateScreen(snapshot =>
            {
                for (int slot = 0; slot < ids.Length; slot++) snapshot.Loadout.Add(new StashItem(ids[slot], 2, slot));
            });
            yield return null;
            int before = _view.StashDisplayCount;

            ClickButton(GetButton("_leaveAllButton"));
            for (int frame = 0; frame < 8; frame++) yield return null;

            Assert.That(_remote.Calls, Is.EqualTo(new[] { "bulk:toStash" }), "Leave All is one request.");
            Assert.That(_store.GetLoadout(), Is.Empty);
            Assert.That(_store.GetStash(), Has.Count.EqualTo(3));
            Assert.That(_view.StashDisplayCount - before, Is.EqualTo(1));
            Assert.That(StashPanel().GetComponentsInChildren<RaidInventorySlotView>(true).Count(s => s.IsOccupied),
                Is.EqualTo(3));
            Assert.That(FindOccupiedSlot(LoadoutPanel()), Is.Null);
        }

        [UnityTest]
        public IEnumerator TakeAll_WhenALaterStackIsRefused_RefreshesOnceWithTheActualState()
        {
            LootId[] ids = DistinctLootIds(LocalProfileSnapshot.MaxLoadoutSlots + 2);
            CreateScreen(snapshot =>
            {
                for (int slot = 0; slot < LocalProfileSnapshot.MaxLoadoutSlots - 1; slot++)
                    snapshot.Loadout.Add(new StashItem(ids[slot], 1, slot));
                snapshot.Stash.Add(new StashItem(ids[LocalProfileSnapshot.MaxLoadoutSlots - 1], 1, 0));
                snapshot.Stash.Add(new StashItem(ids[LocalProfileSnapshot.MaxLoadoutSlots], 1, 1));
            });
            yield return null;
            int before = _view.StashDisplayCount;
            LogAssert.ignoreFailingMessages = true;

            ClickButton(GetButton("_takeAllButton"));
            for (int frame = 0; frame < 8; frame++) yield return null;
            LogAssert.ignoreFailingMessages = false;

            Assert.That(_store.GetLoadout(), Has.Count.EqualTo(LocalProfileSnapshot.MaxLoadoutSlots));
            Assert.That(_store.GetStash(), Has.Count.EqualTo(1));
            Assert.That(_view.StashDisplayCount - before, Is.EqualTo(1));
            Assert.That(StashPanel().GetComponentsInChildren<RaidInventorySlotView>(true).Count(s => s.IsOccupied),
                Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator SingleTransfer_RefreshesTheViewImmediatelyOnCommit()
        {
            CreateScreen(snapshot => snapshot.Stash.Add(new StashItem(Bone, 3, 0)));
            yield return null;
            int before = _view.StashDisplayCount;

            ClickMoveAll(StashPanel());
            yield return null;

            Assert.That(_view.StashDisplayCount - before, Is.EqualTo(1));
            Assert.That(FindOccupiedSlot(StashPanel()), Is.Null);
        }

        [UnityTest]
        public IEnumerator LeaveAllPointerClick_MovesStackFromLoadout()
        {
            CreateScreen(snapshot => snapshot.Loadout.Add(new StashItem(Bone, 3, 0)));
            yield return null;

            Button button = GetButton("_leaveAllButton");
            Assert.That(button.IsInteractable(), Is.True, "The authored Leave All button must accept pointer clicks.");
            ClickButton(button);
            for (int frame = 0; frame < 5; frame++) yield return null;

            Assert.That(_remote.Calls, Is.EqualTo(new[] { "bulk:toStash" }));
            Assert.That(_store.GetLoadout(), Is.Empty);
            Assert.That(_store.GetStash(), Is.EqualTo(new[] { new StashItem(Bone, 3, 0) }));
        }

        [UnityTest]
        public IEnumerator MoveAllFromTheStash_IntoAFullLoadout_NeverReachesTheBackend()
        {
            LootId[] ids = DistinctLootIds(LocalProfileSnapshot.MaxLoadoutSlots + 1);
            CreateScreen(snapshot =>
            {
                for (int slot = 0; slot < LocalProfileSnapshot.MaxLoadoutSlots; slot++)
                {
                    snapshot.Loadout.Add(new StashItem(ids[slot], 1, slot));
                }

                snapshot.Stash.Add(new StashItem(ids[LocalProfileSnapshot.MaxLoadoutSlots], 1, 0));
            });
            yield return null;
            LogAssert.Expect(LogType.Warning, new Regex("Stash action did not complete: Rejected \\(InventoryFull\\)"));

            ClickMoveAll(StashPanel());
            yield return null;

            Assert.That(_remote.Calls, Is.Empty);
            Assert.That(_store.GetStash(), Has.Count.EqualTo(1));
            Assert.That(_store.GetLoadout(), Has.Count.EqualTo(LocalProfileSnapshot.MaxLoadoutSlots));
        }

        [UnityTest]
        public IEnumerator ReopeningTheScreen_KeepsOneActionQueueWhileABackendCallIsInFlight()
        {
            var sword = new LootId("rapier");
            CreateScreen(snapshot =>
            {
                snapshot.Stash.Add(new StashItem(Bone, 3, 0));
                snapshot.Stash.Add(new StashItem(sword, 1, 1));
            });
            yield return null;
            var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _remote.Gates.Enqueue(gate.Task);

            ClickMoveAll(StashPanel(), occupiedIndex: 0);
            yield return null;
            Assert.That(_remote.Calls, Is.EqualTo(new[] { "toLoadout:bone:3" }));

            _instance.SetActive(false);
            _instance.SetActive(true);
            yield return null;
            ClickMoveAll(StashPanel(), occupiedIndex: 1);
            yield return null;
            yield return null;

            Assert.That(
                _remote.Calls,
                Is.EqualTo(new[] { "toLoadout:bone:3" }),
                "A reopened screen must queue behind the transfer still waiting for the backend.");

            gate.SetResult(true);
            for (int frame = 0; frame < 5; frame++)
            {
                yield return null;
            }

            Assert.That(_remote.Calls, Is.EqualTo(new[] { "toLoadout:bone:3", "toLoadout:rapier:1" }));
            Assert.That(_store.GetStash(), Is.Empty);
            Assert.That(_store.GetLoadout(), Has.Count.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator DisplayOrder_RealPanelsAndBulkButtonsUseStoredOrderWithoutDisplayWrites()
        {
            LootDefinitionCatalog authoredCatalog = _catalog;
            var synthetic = ScriptableObject.CreateInstance<LootDefinitionCatalog>();
            var definitions = new List<LootDefinition>();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            for (int i = 0; i < 18; i++)
            {
                var definition = Object.Instantiate(authoredCatalog.TryGetByIndex(0, out var template) ? template : null);
                typeof(LootDefinition).GetField("_id", flags).SetValue(definition, $"sorted_{i:D2}");
                typeof(LootDefinition).GetField("_displayName", flags).SetValue(definition, $"Name {i:D2}");
                typeof(LootDefinition).GetField("_category", flags).SetValue(definition, LootCategory.Material);
                definitions.Add(definition);
            }
            typeof(LootDefinitionCatalog).GetField("_definitions", flags).SetValue(synthetic, definitions);
            _catalog = synthetic;
            try
            {
                LootId[] expected = definitions.Select(d => d.LootId).Reverse().ToArray();
                CreateScreen(snapshot =>
                {
                    for (int slot = 0; slot < expected.Length; slot++)
                        snapshot.Stash.Add(new StashItem(expected[slot], 1, slot));
                    for (int slot = 0; slot < 3; slot++)
                        snapshot.Loadout.Add(new StashItem(expected[slot], 1, slot));
                });
                var presenter = _instance.GetComponent<LobbyStashPresenter>();
                _instance.SetActive(false);
                typeof(LobbyStashPresenter).GetField("_lootCatalog", flags).SetValue(presenter, synthetic);
                StashItem[] stashBefore = _store.GetStash().ToArray();
                StashItem[] loadoutBefore = _store.GetLoadout().ToArray();
                var equipmentBefore = _store.GetPreparedEquipment();
                int revision = _store.RemoteRevision;
                int commits = 0;
                _store.ProfileCommitted += _ => commits++;
                _instance.SetActive(true);
                yield return null;
                var visible = StashPanel().GetComponentsInChildren<RaidInventorySlotView>(true)
                    .Where(s => s.IsOccupied).Select(s => s.LootId).ToArray();
                int pageSize = visible.Length;
                Assert.That(pageSize, Is.LessThan(18), "Fixture must exercise a second page.");
                Assert.That(visible, Is.EqualTo(expected.Take(pageSize)));
                Assert.That(LoadoutPanel().GetComponentsInChildren<RaidInventorySlotView>(true)
                    .Where(s => s.IsOccupied).Select(s => s.LootId), Is.EqualTo(expected.Take(3)));
                ((Button)typeof(LobbyStashUI).GetField("_stashNextPage", flags).GetValue(_view)).onClick.Invoke();
                Assert.That(StashPanel().GetComponentsInChildren<RaidInventorySlotView>(true)
                    .Where(s => s.IsOccupied).Select(s => s.LootId), Is.EqualTo(expected.Skip(pageSize)));
                Assert.That(_store.GetStash(), Is.EqualTo(stashBefore));
                Assert.That(_store.GetLoadout(), Is.EqualTo(loadoutBefore));
                Assert.That(_store.GetPreparedEquipment(), Is.EqualTo(equipmentBefore));
                Assert.That(_store.RemoteRevision, Is.EqualTo(revision));
                Assert.That(commits, Is.Zero);
                ClickButton(GetButton("_takeAllButton"));
                for (int frame = 0; frame < 5; frame++) yield return null;
                Assert.That(_remote.Calls, Is.EqualTo(new[] { "bulk:toLoadout" }));
                _remote.Calls.Clear();
                ClickButton(GetButton("_leaveAllButton"));
                for (int frame = 0; frame < 5; frame++) yield return null;
                Assert.That(_remote.Calls, Is.EqualTo(new[] { "bulk:toStash" }));
            }
            finally
            {
                if (_canvasObject != null) Object.DestroyImmediate(_canvasObject);
                if (_contextObject != null) Object.DestroyImmediate(_contextObject);
                _catalog = authoredCatalog;
                foreach (var definition in definitions) Object.DestroyImmediate(definition);
                Object.DestroyImmediate(synthetic);
            }
        }

        [UnityTest]
        public IEnumerator SortButtons_PersistOnlyTheirListRemoteFirstWhileReady()
        {
            var ids = DistinctLootIds(6);
            var source = ids.Select((id, i) => new StashItem(id, i + 2, i)).ToArray();
            source = StashDisplayOrder.SortedCopy(source, _catalog).AsEnumerable().Reverse()
                .Select((item, i) => item.AtSlot(i + 2)).ToArray();
            var slotOrder = StashDisplayOrder.SortedCopy(source, _catalog).Select(item => item.Slot).ToArray();
            var sorted = StashDisplayOrder.SortedCopy(source, _catalog)
                .Select((item, i) => item.AtSlot(i)).ToArray();
            CreateScreen(snapshot =>
            {
                snapshot.Stash.AddRange(source);
                snapshot.Loadout.AddRange(source);
            });
            _instance.GetComponent<LobbyStashPresenter>().SetEquipmentGate(() => false);
            yield return null;
            foreach (InventoryList list in new[] { InventoryList.Stash, InventoryList.Loadout })
            {
                var gate = new TaskCompletionSource<bool>();
                _remote.Gates.Enqueue(gate.Task);
                Button button = GetButton(list == InventoryList.Stash ? "_stashSortButton" : "_loadoutSortButton");
                Assert.That(button.IsInteractable(), Is.True, "Ready must not gate Sort.");
                ClickButton(button);
                yield return null;
                Assert.That(button.IsInteractable(), Is.False);
                Assert.That(list == InventoryList.Stash ? _store.GetStash() : _store.GetLoadout(), Is.EqualTo(source),
                    "Local order must not change before the remote response.");
                Assert.That(_remote.Calls.Last(), Is.EqualTo($"order:{list}:{string.Join(",", slotOrder)}"));
                gate.SetResult(true);
                for (int frame = 0; frame < 5; frame++) yield return null;
                Assert.That(list == InventoryList.Stash ? _store.GetStash() : _store.GetLoadout(), Is.EqualTo(sorted));
                Assert.That(_store.GetStash(), Is.EqualTo(sorted));
                Assert.That(_store.GetLoadout(), Is.EqualTo(list == InventoryList.Stash ? source : sorted));
                RaidLootPanelView panel = list == InventoryList.Stash ? StashPanel() : LoadoutPanel();
                RaidInventorySlotView[] cells = panel.GetComponentsInChildren<RaidInventorySlotView>(true);
                for (int slot = 0; slot < sorted.Length; slot++)
                    Assert.That(cells[slot].LootId, Is.EqualTo(sorted[slot].LootId), "Only explicit Sort closes holes.");
                Assert.That(cells.Skip(sorted.Length).All(cell => !cell.IsOccupied), Is.True);
            }
        }

        [UnityTest]
        public IEnumerator ApplicationQueue_BlocksOtherConsumersWhileStashTransferIsPending()
        {
            CreateScreen(snapshot => snapshot.Stash.Add(new StashItem(new LootId("bone"), 3, 0)));
            yield return null;
            var context = _contextObject.GetComponent<ApplicationStashContext>();
            var coordinator = context.GetTransferCoordinator();
            var gate = new TaskCompletionSource<bool>();
            _remote.Gates.Enqueue(gate.Task);
            ClickMoveAll(StashPanel());
            yield return null;
            var otherConsumerSort = coordinator.SortAsync(InventoryList.Loadout, _catalog);
            _instance.SetActive(false);
            _instance.SetActive(true);
            yield return null;
            Assert.That(context.GetTransferCoordinator(), Is.SameAs(coordinator));
            Assert.That(otherConsumerSort.IsCompleted, Is.False);
            Assert.That(_remote.Calls, Is.EqualTo(new[] { "toLoadout:bone:3" }));
            gate.SetResult(true);
            for (int frame = 0; frame < 5; frame++) yield return null;
            Assert.That(otherConsumerSort.IsCompleted, Is.True);
            Assert.That(otherConsumerSort.Result.IsSuccess, Is.True);
            Assert.That(_store.GetLoadout(), Is.EqualTo(new[] { new StashItem(new LootId("bone"), 3, 0) }));
        }

        [UnityTest]
        public IEnumerator RemovingOneDuplicate_LeavesItsHoleAndTransfersOnlyTheSelectedSlot()
        {
            CreateScreen(snapshot =>
            {
                snapshot.Stash.Add(new StashItem(Bone, 2, 2));
                snapshot.Stash.Add(new StashItem(Bone, 5, 7));
            });
            yield return null;
            RaidInventorySlotView[] cells = StashPanel().GetComponentsInChildren<RaidInventorySlotView>(true);
            Assert.That(cells[0].IsOccupied, Is.False);
            Assert.That(cells[2].LootId, Is.EqualTo(Bone));
            Assert.That(cells[7].LootId, Is.EqualTo(Bone));

            ClickMoveAll(StashPanel(), occupiedIndex: 1);
            yield return null;

            Assert.That(_remote.TransferSlots, Is.EqualTo(new[] { 7 }));
            Assert.That(_remote.Calls, Is.EqualTo(new[] { "toLoadout:bone:5" }));
            Assert.That(_store.GetStash(), Is.EqualTo(new[] { new StashItem(Bone, 2, 2) }));
            Assert.That(_store.GetLoadout(), Is.EqualTo(new[] { new StashItem(Bone, 5, 0) }));
            Assert.That(cells[2].LootId, Is.EqualTo(Bone));
            Assert.That(cells[7].IsOccupied, Is.False, "Removal must not shift another entry into the hole.");
        }

        [UnityTest]
        public IEnumerator SparseStash_RendersAtPhysicalSlotOnSecondPageAndTransfersThatAddress()
        {
            CreateScreen(snapshot => snapshot.Stash.Add(new StashItem(Bone, 4, 19)));
            yield return null;
            Assert.That(FindOccupiedSlot(StashPanel()), Is.Null);
            ClickButton(GetButton("_stashNextPage"));
            RaidInventorySlotView[] cells = StashPanel().GetComponentsInChildren<RaidInventorySlotView>(true);
            Assert.That(cells[0].IsOccupied, Is.False);
            Assert.That(cells[3].LootId, Is.EqualTo(Bone));
            Assert.That(cells.Count(cell => cell.IsOccupied), Is.EqualTo(1));
            ClickMoveAll(StashPanel());
            yield return null;
            Assert.That(_remote.TransferSlots, Is.EqualTo(new[] { 19 }));
            Assert.That(_store.GetStash(), Is.Empty);
        }

        [UnityTest]
        public IEnumerator StashOverflow_UsesExistingInvalidVisualAndCanBeReduced()
        {
            CreateScreen(snapshot => snapshot.Stash.Add(new StashItem(Bone, 4, 64)));
            yield return null;
            for (int page = 0; page < 4; page++) ClickButton(GetButton("_stashNextPage"));
            RaidInventorySlotView cell = FindOccupiedSlot(StashPanel());
            Assert.That(cell, Is.Not.Null);
            var serializedCell = new SerializedObject(cell);
            var background = (UnityEngine.UI.Image)serializedCell.FindProperty("_background").objectReferenceValue;
            var invalidSprite = (Sprite)serializedCell.FindProperty("_invalidSprite").objectReferenceValue;
            Assert.That(invalidSprite, Is.Not.Null);
            Assert.That(background.sprite, Is.SameAs(invalidSprite));
            cell.OnPointerEnter(new PointerEventData(null));
            Assert.That(background.sprite, Is.SameAs(invalidSprite), "Hover must not erase the overflow marker.");
            _store.SetRemoteRevision(1);
            Assert.That(background.sprite, Is.SameAs(invalidSprite), "Confirmed refresh must preserve overflow after gem binding.");
            ClickMoveAll(StashPanel());
            yield return null;
            Assert.That(_remote.TransferSlots, Is.EqualTo(new[] { 64 }));
            Assert.That(_store.GetStash(), Is.Empty);
            Assert.That(_store.GetLoadout(), Is.EqualTo(new[] { new StashItem(Bone, 4, 0) }));
        }

        [UnityTest]
        public IEnumerator DragBetweenCells_UsesSlotMoveForEmptyMergeAndSwap()
        {
            LootId other = new("rapier");
            CreateScreen(snapshot =>
            {
                snapshot.Stash.Add(new StashItem(Bone, 2, 2));
                snapshot.Stash.Add(new StashItem(Bone, 3, 5));
                snapshot.Stash.Add(new StashItem(other, 1, 7));
            });
            yield return null;
            RaidInventorySlotView[] cells = StashPanel().GetComponentsInChildren<RaidInventorySlotView>(true);
            Drag(cells[2], cells[3]);
            yield return null;
            Assert.That(cells[2].IsOccupied, Is.False);
            Assert.That(cells[3].LootId, Is.EqualTo(Bone));
            Drag(cells[3], cells[5]);
            yield return null;
            Assert.That(cells[3].IsOccupied, Is.False);
            Assert.That(_store.GetStash(), Does.Contain(new StashItem(Bone, 5, 5)));
            Drag(cells[5], cells[7]);
            yield return null;
            Assert.That(_remote.Calls, Is.EqualTo(new[] {
                "move:Stash:2:Stash:3:bone", "move:Stash:3:Stash:5:bone", "move:Stash:5:Stash:7:bone" }));
            Assert.That(_store.GetStash(), Is.EquivalentTo(new[] {
                new StashItem(other, 1, 5), new StashItem(Bone, 5, 7) }));
            Assert.That(cells[5].LootId, Is.EqualTo(other));
            Assert.That(cells[7].LootId, Is.EqualTo(Bone));
        }

        [UnityTest]
        public IEnumerator LoadoutRemoval_KeepsTheOtherDuplicateAtItsSlot()
        {
            CreateScreen(snapshot =>
            {
                snapshot.Loadout.Add(new StashItem(Bone, 2, 3));
                snapshot.Loadout.Add(new StashItem(Bone, 4, 9));
            });
            yield return null;
            RaidInventorySlotView[] cells = LoadoutPanel().GetComponentsInChildren<RaidInventorySlotView>(true);
            Assert.That(cells[0].IsOccupied, Is.False);
            Assert.That(cells[3].LootId, Is.EqualTo(Bone));
            Assert.That(cells[9].LootId, Is.EqualTo(Bone));
            ClickMoveAll(LoadoutPanel());
            yield return null;
            Assert.That(_remote.TransferSlots, Is.EqualTo(new[] { 3 }));
            Assert.That(_remote.Calls, Is.EqualTo(new[] { "toStash:bone:2" }));
            Assert.That(_store.GetLoadout(), Is.EqualTo(new[] { new StashItem(Bone, 4, 9) }));
            Assert.That(_store.GetStash(), Is.EqualTo(new[] { new StashItem(Bone, 2, 0) }));
            Assert.That(cells[3].IsOccupied, Is.False);
            Assert.That(cells[9].LootId, Is.EqualTo(Bone));
        }

        [UnityTest]
        public IEnumerator DragFromSecondStashPage_ToLoadoutCell_PreservesBothAddresses()
        {
            CreateScreen(snapshot => snapshot.Stash.Add(new StashItem(Bone, 4, 19)));
            yield return null;
            ClickButton(GetButton("_stashNextPage"));
            var source = StashPanel().GetComponentsInChildren<RaidInventorySlotView>(true)[3];
            var target = LoadoutPanel().GetComponentsInChildren<RaidInventorySlotView>(true)[8];
            Drag(source, target);
            yield return null;
            Assert.That(_remote.Calls, Is.EqualTo(new[] { "move:Stash:19:Loadout:8:bone" }));
            Assert.That(_store.GetStash(), Is.Empty);
            Assert.That(_store.GetLoadout(), Is.EqualTo(new[] { new StashItem(Bone, 4, 8) }));
            Assert.That(target.LootId, Is.EqualTo(Bone));
        }

        [UnityTest]
        public IEnumerator EquipmentDrag_StillUsesEquipmentEndpointAndReadyGate()
        {
            LootId sword = new("rapier");
            CreateScreen(snapshot => snapshot.Loadout.Add(new StashItem(sword, 1, 4)));
            var presenter = _instance.GetComponent<LobbyStashPresenter>();
            presenter.SetEquipmentGate(() => false);
            yield return null;
            var field = typeof(LobbyStashUI).GetField("_weaponSetAMainHandView",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var equipmentCell = (RaidInventorySlotView)field.GetValue(_view);
            var source = LoadoutPanel().GetComponentsInChildren<RaidInventorySlotView>(true)[4];
            Drag(source, equipmentCell);
            yield return null;
            Assert.That(FeedbackText(), Is.EqualTo(LobbyStashPresenter.BuildLockedMessage));
            Assert.That(_remote.Calls, Is.Empty);
            Assert.That(_store.GetPreparedEquipment().WeaponSetAMainHand.IsValid, Is.False);
            Assert.That(_store.GetLoadout(), Is.EqualTo(new[] { new StashItem(sword, 1, 4) }));
            presenter.SetEquipmentGate(() => true);
            Drag(source, equipmentCell);
            yield return null;
            Assert.That(_remote.Calls, Is.EqualTo(new[] { "equipment" }));
            Assert.That(_store.GetPreparedEquipment().WeaponSetAMainHand, Is.EqualTo(sword));
            Assert.That(_store.GetLoadout(), Is.Empty);
        }

        [UnityTest]
        public IEnumerator ReadyGate_BlocksEveryStashTransferShowsTheLockedLineAndNotReadyRestoresIt()
        {
            CreateScreen(snapshot =>
            {
                snapshot.Stash.Add(new StashItem(Bone, 3, 0));
                snapshot.Loadout.Add(new StashItem(Bone, 2, 0));
            });
            var presenter = _instance.GetComponent<LobbyStashPresenter>();
            bool ready = true;
            presenter.SetEquipmentGate(() => !ready);
            yield return null;

            Button takeAll = GetButton("_takeAllButton");
            Button leaveAll = GetButton("_leaveAllButton");
            Assert.That(takeAll.IsInteractable(), Is.False, "Ready must close Take All.");
            Assert.That(leaveAll.IsInteractable(), Is.False, "Ready must close Leave All.");
            Assert.That(GetButton("_stashSortButton").IsInteractable(), Is.True, "Ready must not gate Sort.");
            Assert.That(FeedbackText(), Is.EqualTo(LobbyStashPresenter.BuildLockedMessage));

            RaidInventorySlotView[] stashCells = StashPanel().GetComponentsInChildren<RaidInventorySlotView>(true);
            RaidInventorySlotView[] loadoutCells = LoadoutPanel().GetComponentsInChildren<RaidInventorySlotView>(true);
            Drag(stashCells[0], loadoutCells[5]);
            yield return null;
            ClickMoveAll(StashPanel());
            yield return null;
            Assert.That(_remote.Calls, Is.Empty, "No Stash transfer may reach the backend while Ready.");
            Assert.That(_store.GetStash(), Is.EqualTo(new[] { new StashItem(Bone, 3, 0) }));
            Assert.That(_store.GetLoadout(), Is.EqualTo(new[] { new StashItem(Bone, 2, 0) }));

            ready = false;
            yield return null;
            Assert.That(takeAll.IsInteractable(), Is.True, "NotReady must reopen transfers immediately.");
            Assert.That(FeedbackText(), Is.Empty);
            ClickButton(takeAll);
            for (int frame = 0; frame < 5; frame++) yield return null;
            Assert.That(_remote.Calls, Is.EqualTo(new[] { "bulk:toLoadout" }));
            Assert.That(_store.GetStash(), Is.Empty);
        }

        private string FeedbackText()
        {
            var field = typeof(LobbyStashUI).GetField("_gemFeedbackText",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var text = (TMPro.TMP_Text)field.GetValue(_view);
            return text.gameObject.activeSelf ? text.text : string.Empty;
        }

        [UnityTest]
        public IEnumerator LoadoutOverflow_ReportsTheAuthoredShortageWithoutCompactingIntoAnotherCell()
        {
            LogAssert.Expect(LogType.Error, new Regex(
                @"needs physical slots 0\.\.30 but only 30 slots are authored in the prefab; Loadout paging is not authored\."));
            CreateScreen(snapshot => snapshot.Loadout.Add(new StashItem(Bone, 4, 30)));
            yield return null;
            Assert.That(LoadoutPanel().AuthoredSlotCount, Is.EqualTo(30));
            Assert.That(FindOccupiedSlot(LoadoutPanel()), Is.Null,
                "Overflow must not silently take the position of an in-capacity cell.");
            Assert.That(_store.GetLoadout(), Is.EqualTo(new[] { new StashItem(Bone, 4, 30) }));
            Assert.That(_remote.Calls, Is.Empty);
        }

        [UnityTest]
        public IEnumerator AuthoredCellHierarchy_MatchesPanelPoolUsedForSlotAddressing()
        {
            CreateScreen(_ => { });
            yield return null;
            foreach (RaidLootPanelView panel in new[] { StashPanel(), LoadoutPanel() })
            {
                var pool = new SerializedObject(panel).FindProperty("_authoredSlots");
                var cells = panel.GetComponentsInChildren<RaidInventorySlotView>(true);
                Assert.That(cells.Length, Is.EqualTo(pool.arraySize));
                for (int i = 0; i < cells.Length; i++)
                    Assert.That(cells[i], Is.SameAs(pool.GetArrayElementAtIndex(i).objectReferenceValue));
            }
        }

        [UnityTest]
        public IEnumerator TownSplit_ConfirmsExactAmountInBothListsRemoteFirst()
        {
            CreateScreen(snapshot =>
            {
                snapshot.Stash.Add(new StashItem(Bone, 5, 2));
                snapshot.Loadout.Add(new StashItem(Bone, 5, 3));
            });
            yield return null;
            foreach (var list in new[] { InventoryList.Stash, InventoryList.Loadout })
            {
                var panel = list == InventoryList.Stash ? StashPanel() : LoadoutPanel();
                var action = SplitAction(panel);
                Assert.That(action.IsInteractable(), Is.True);
                action.onClick.Invoke();
                var dialog = _canvasObject.GetComponentInChildren<QuantitySelectorDialogView>(true);
                Assert.That(dialog, Is.Not.Null);
                Assert.That(dialog.IsOpen, Is.True);
                DialogField<TMPro.TMP_InputField>(dialog, "_input").text = "2";
                var gate = new TaskCompletionSource<bool>();
                _remote.Gates.Enqueue(gate.Task);
                DialogField<Button>(dialog, "_confirmButton").onClick.Invoke();
                yield return null;
                int source = list == InventoryList.Stash ? 2 : 3;
                Assert.That(_remote.Calls.Last(), Is.EqualTo($"split:{list}:{source}:2:0:bone"));
                Assert.That(list == InventoryList.Stash ? _store.GetStash() : _store.GetLoadout(),
                    Is.EqualTo(new[] { new StashItem(Bone, 5, source) }));
                gate.SetResult(true);
                for (int frame = 0; frame < 5; frame++) yield return null;
                Assert.That(list == InventoryList.Stash ? _store.GetStash() : _store.GetLoadout(),
                    Is.EqualTo(new[] { new StashItem(Bone, 2, 0), new StashItem(Bone, 3, source) }));
                Assert.That(dialog.IsOpen, Is.False);
            }
            Assert.That(_remote.Calls.Count, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator TownSplit_CancelAndRemoteFailureNeverMutate()
        {
            CreateScreen(snapshot => snapshot.Stash.Add(new StashItem(Bone, 5, 2)));
            yield return null;
            SplitAction(StashPanel()).onClick.Invoke();
            var dialog = _canvasObject.GetComponentInChildren<QuantitySelectorDialogView>(true);
            Assert.That(dialog, Is.Not.Null);
            DialogField<Button>(dialog, "_cancelButton").onClick.Invoke();
            Assert.That(dialog.IsOpen, Is.False);
            Assert.That(_remote.Calls, Is.Empty);
            Assert.That(_store.GetStash(), Is.EqualTo(new[] { new StashItem(Bone, 5, 2) }));
            SplitAction(StashPanel()).onClick.Invoke();
            _remote.FailSplit = true;
            LogAssert.Expect(LogType.Warning, new Regex("Stash action did not complete: RecoveryFailed"));
            DialogField<TMPro.TMP_InputField>(dialog, "_input").text = "2";
            DialogField<Button>(dialog, "_confirmButton").onClick.Invoke();
            yield return null;
            Assert.That(_remote.Calls, Is.EqualTo(new[] { "split:Stash:2:2:0:bone" }));
            Assert.That(_store.GetStash(), Is.EqualTo(new[] { new StashItem(Bone, 5, 2) }));
            Assert.That(_store.GetLoadout(), Is.Empty);
            Assert.That(_canvasObject.GetComponentsInChildren<TMPro.TMP_Text>(true).Any(t =>
                t.gameObject.activeSelf && t.text == "No se pudo dividir el stack"), Is.True);
        }

        [UnityTest]
        public IEnumerator TownSplit_MenuDisablesSingleUnitsEquipmentFullCapacityAndPending()
        {
            CreateScreen(snapshot =>
            {
                snapshot.Stash.Add(new StashItem(Bone, 1, 0));
                snapshot.Stash.Add(new StashItem(new LootId("rapier"), 2, 1));
                for (int slot = 0; slot < LocalProfileSnapshot.MaxLoadoutSlots; slot++)
                    snapshot.Loadout.Add(new StashItem(Bone, 5, slot));
            });
            yield return null;
            Assert.That(SplitAction(StashPanel(), 0).IsInteractable(), Is.False);
            Assert.That(SplitAction(StashPanel(), 1).IsInteractable(), Is.False);
            Assert.That(SplitAction(LoadoutPanel()).IsInteractable(), Is.False);
            var gate = new TaskCompletionSource<bool>();
            _remote.Gates.Enqueue(gate.Task);
            ClickMoveAll(LoadoutPanel());
            yield return null;
            Assert.That(SplitAction(LoadoutPanel()).IsInteractable(), Is.False);
            gate.SetResult(true);
            for (int frame = 0; frame < 5; frame++) yield return null;
        }

        private Button SplitAction(RaidLootPanelView panel, int occupiedIndex = 0)
        {
            var cell = panel.GetComponentsInChildren<RaidInventorySlotView>(true).Where(s => s.IsOccupied).ElementAt(occupiedIndex);
            cell.OnPointerClick(new PointerEventData(null) { button = PointerEventData.InputButton.Right });
            var menu = _instance.GetComponentInChildren<RaidLootContextMenuView>(true);
            Assert.That(menu.IsOpen, Is.True);
            var action = menu.GetComponentsInChildren<RaidLootContextActionButton>(true).SingleOrDefault(a =>
                a.gameObject.activeSelf && a.GetComponentInChildren<TMPro.TMP_Text>(true).text == "Dividir");
            Assert.That(action, Is.Not.Null, "Both Town lists must offer the exact Dividir label.");
            return action.GetComponent<Button>();
        }

        private static T DialogField<T>(QuantitySelectorDialogView dialog, string name) where T : UnityEngine.Object =>
            (T)typeof(QuantitySelectorDialogView).GetField(name,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(dialog);

        private void SeedGemScreen(int sockets = 1)
        {
            _catalog.TryGet("rapier", out var weapon);
            Assert.That(WeaponInstanceModifiers.TryCreate(default, default, weapon.WeaponDefinition.NaturalScalingAttribute,
                sockets, default, default, out var modifiers, out _), Is.True);
            CreateScreen(snapshot =>
            {
                snapshot.InventorySchemaVersion = 3;
                snapshot.Stash.Add(new StashItem(new LootId("RES_ATTUNEMENT_GEM"), 2, 2));
                snapshot.Loadout.Add(new StashItem(new LootId("rapier"), 1, 4,
                    new EquipmentInstance("123456789012345678901234", modifiers)));
            });
        }

        [UnityTest]
        public IEnumerator GemDrop_ConfirmsBeforeRemote_LocksUntilSnapshot_RefreshesInstanceTooltip()
        {
            SeedGemScreen(); yield return null;
            var source = StashPanel().GetComponentsInChildren<RaidInventorySlotView>(true)[2];
            var target = LoadoutPanel().GetComponentsInChildren<RaidInventorySlotView>(true)[4];
            Drag(source, target);
            var modal = _instance.GetComponentInChildren<GemInstallConfirmationView>(true);
            Assert.That(modal, Is.Not.Null);
            Assert.That(modal.IsOpen, Is.True);
            Assert.That(modal.GetComponentsInChildren<TMPro.TMP_Text>(true).Any(t =>
                t.text.StartsWith("¿Incrustar ") && t.text.EndsWith("? Es permanente.")), Is.True);
            Assert.That(_remote.Calls, Is.Empty);
            Assert.That(_store.GetStash().Single().Amount, Is.EqualTo(2));
            var gate = new TaskCompletionSource<bool>(); _remote.Gates.Enqueue(gate.Task);
            modal.transform.Find("Modal/Confirm").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.That(_remote.Calls, Is.EqualTo(new[] { "gem:Stash:2:Loadout:4" }));
            Assert.That(source.GetComponent<Button>().IsInteractable(), Is.False);
            Assert.That(_store.GetStash().Single().Amount, Is.EqualTo(2));
            gate.SetResult(true);
            for (int frame = 0; frame < 5; frame++) yield return null;
            Assert.That(_store.GetStash().Single().Amount, Is.EqualTo(1));
            Assert.That(_store.GetLoadout().Single().Instance.Modifiers.Value.TryGetGemSocket(0, out var socket), Is.True);
            Assert.That(socket.GemLootId, Is.EqualTo(new LootId("RES_ATTUNEMENT_GEM")));
            Assert.That(modal.IsOpen, Is.False);
            var tooltip = (EquipmentTooltipView)typeof(LobbyStashUI).GetField("_tooltipView",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(_view);
            target.OnPointerEnter(new PointerEventData(null));
            Assert.That(tooltip.gameObject.activeSelf, Is.True);
        }

        [UnityTest]
        public IEnumerator GemContext_SelectsHighlightedTarget_CancelDoesNotSend_ExistingMoveContextPreserved()
        {
            SeedGemScreen(); yield return null;
            var source = StashPanel().GetComponentsInChildren<RaidInventorySlotView>(true)[2];
            var target = LoadoutPanel().GetComponentsInChildren<RaidInventorySlotView>(true)[4];
            source.OnPointerClick(new PointerEventData(null) { button = PointerEventData.InputButton.Right });
            var menu = _instance.GetComponentInChildren<RaidLootContextMenuView>(true);
            var action = menu.GetComponentsInChildren<RaidLootContextActionButton>(true).FirstOrDefault(a =>
                a.gameObject.activeSelf && a.GetComponentInChildren<TMPro.TMP_Text>(true).text == "Incrustar");
            Assert.That(action, Is.Not.Null);
            Assert.That(menu.GetComponentsInChildren<TMPro.TMP_Text>(true).Any(t => t.text == "Mover todo al Inventario"), Is.True);
            action.GetComponent<Button>().onClick.Invoke();
            var background = (UnityEngine.UI.Image)new SerializedObject(target).FindProperty("_background").objectReferenceValue;
            var hovered = (Sprite)new SerializedObject(target).FindProperty("_hoveredSprite").objectReferenceValue;
            Assert.That(background.sprite, Is.SameAs(hovered));
            target.OnPointerClick(new PointerEventData(null) { button = PointerEventData.InputButton.Left });
            var modal = _instance.GetComponentInChildren<GemInstallConfirmationView>(true);
            Assert.That(modal.IsOpen, Is.True);
            modal.transform.Find("Modal/Cancel").GetComponent<Button>().onClick.Invoke();
            Assert.That(modal.IsOpen, Is.False);
            Assert.That(_remote.Calls, Is.Empty);
            Assert.That(_store.GetStash().Single().Amount, Is.EqualTo(2));
            Assert.That(source.GetComponent<Button>().IsInteractable(), Is.True);
        }

        [UnityTest]
        public IEnumerator GemDrop_NoSockets_ShowsBriefNotice_NoMoveOrConfirmation()
        {
            SeedGemScreen(0); yield return null;
            var source = StashPanel().GetComponentsInChildren<RaidInventorySlotView>(true)[2];
            var target = LoadoutPanel().GetComponentsInChildren<RaidInventorySlotView>(true)[4];
            Drag(source, target);
            Assert.That(_remote.Calls, Is.Empty);
            var modal = _instance.GetComponentInChildren<GemInstallConfirmationView>(true);
            Assert.That(modal != null && modal.IsOpen, Is.False);
            Assert.That(_instance.GetComponentsInChildren<TMPro.TMP_Text>(true)
                .Any(t => t.gameObject.activeSelf && t.text == "Esta arma no tiene ranuras libres"), Is.True);
            Assert.That(_store.GetStash().Single().Amount, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator TownPersonalInventory_GemDropConfirms_CancelRestoresInputs_UnbindClosesDialog()
        {
            _catalog.TryGet("rapier", out var weapon);
            WeaponInstanceModifiers.TryCreate(default, default, weapon.WeaponDefinition.NaturalScalingAttribute,
                1, default, default, out var modifiers, out _);
            CreateScreen(snapshot =>
            {
                snapshot.InventorySchemaVersion = 3;
                snapshot.Loadout.Add(new StashItem(new LootId("RES_ATTUNEMENT_GEM"), 2, 2));
                snapshot.Loadout.Add(new StashItem(new LootId("rapier"), 1, 4,
                    new EquipmentInstance("123456789012345678901234", modifiers)));
            });
            _instance.SetActive(false);
            var context = _contextObject.GetComponent<ApplicationStashContext>();
            var source = new LocalLoadoutInventoryReadSource(context, context.ProfileId);
            var session = new TownGemInstallSession(_store, context.GetTransferCoordinator(), _catalog);
            var town = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/UI/PlayerUI/RaidInventoryUI.prefab"), _canvasObject.transform);
            var reader = _contextObject.AddComponent<PlayerInputReader>();
            var presenter = town.GetComponent<RaidInventoryPresenter>();
            var view = town.GetComponent<RaidInventoryView>();
            try
            {
                presenter.BindTown(source, source, new TownEquipmentMutationEndpoint(context.LoadoutService,
                    context.ProfileId, _catalog, () => true), reader, true, null,
                    context.GetTransferCoordinator(), () => true, source.ReadConfirmedAttributes, session);
                presenter.ShowHosted();
                yield return null;
                Drag(view.PlayerPanel.GetAuthoredSlot(2), view.PlayerPanel.GetAuthoredSlot(4));
                var modal = town.GetComponentInChildren<GemInstallConfirmationView>(true);
                Assert.That(modal, Is.Not.Null);
                Assert.That(modal.IsOpen, Is.True);
                Assert.That(_remote.Calls, Is.Empty);
                modal.transform.Find("Modal/Cancel").GetComponent<Button>().onClick.Invoke();
                Assert.That(session.LocksMutations, Is.False);
                Assert.That(view.PlayerPanel.GetAuthoredSlot(2).GetComponent<Button>().IsInteractable(), Is.True);
                presenter.Unbind();
                Assert.That(modal.IsOpen, Is.False);
            }
            finally { source.Dispose(); session.Dispose(); Object.DestroyImmediate(town); }
        }

        [UnityTest]
        public IEnumerator GemContext_PagingKeepsEligibleHighlightsAndExactTargetSlot()
        {
            _catalog.TryGet("rapier", out var weapon);
            WeaponInstanceModifiers.TryCreate(default, default, weapon.WeaponDefinition.NaturalScalingAttribute,
                1, default, default, out var modifiers, out _);
            CreateScreen(snapshot =>
            {
                snapshot.InventorySchemaVersion = 3;
                snapshot.Loadout.Add(new StashItem(new LootId("RES_ATTUNEMENT_GEM"), 2, 2));
                snapshot.Stash.Add(new StashItem(new LootId("rapier"), 1, 19,
                    new EquipmentInstance("123456789012345678901234", modifiers)));
            });
            yield return null;
            LoadoutPanel().GetAuthoredSlot(2).OnPointerClick(new PointerEventData(null) { button = PointerEventData.InputButton.Right });
            var menu = _instance.GetComponentInChildren<RaidLootContextMenuView>(true);
            var action = menu.GetComponentsInChildren<RaidLootContextActionButton>(true).Single(a =>
                a.gameObject.activeSelf && a.GetComponentInChildren<TMPro.TMP_Text>(true).text == "Incrustar");
            action.GetComponent<Button>().onClick.Invoke();
            ClickButton(GetButton("_stashNextPage"));
            var target = StashPanel().GetAuthoredSlot(3);
            var serialized = new SerializedObject(target);
            Assert.That(((UnityEngine.UI.Image)serialized.FindProperty("_background").objectReferenceValue).sprite,
                Is.SameAs(serialized.FindProperty("_hoveredSprite").objectReferenceValue));
            target.OnPointerClick(new PointerEventData(null) { button = PointerEventData.InputButton.Left });
            var modal = _instance.GetComponentInChildren<GemInstallConfirmationView>(true);
            Assert.That(modal.IsOpen, Is.True);
            modal.transform.Find("Modal/Confirm").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.That(_remote.Calls.Single(), Is.EqualTo("gem:Loadout:2:Stash:19"));
            Assert.That(_store.GetLoadout().Single().Amount, Is.EqualTo(1));
        }

        private static void Drag(RaidInventorySlotView source, RaidInventorySlotView target)
        {
            var pointer = new PointerEventData(null) { button = PointerEventData.InputButton.Left };
            source.OnBeginDrag(pointer);
            target.OnDrop(pointer);
            source.OnEndDrag(pointer);
        }

        private void CreateScreen(Action<LocalProfileSnapshot> configure)
        {
            var profile = new ProfileId(ProfileValue);
            var repository = new InMemoryLocalProfileRepository();
            Assert.That(repository.Initialize(profile, _catalog), Is.True);
            LocalProfileSnapshot snapshot = repository.Snapshot.Clone();
            snapshot.InventorySchemaVersion = 2;
            configure(snapshot);
            Assert.That(repository.TrySave(snapshot, out string error), Is.True, error);
            _store = new LocalProfileStore(repository, profile, _catalog);

            _contextObject = new GameObject("ApplicationStashContext (test)");
            var context = _contextObject.AddComponent<ApplicationStashContext>();
            var stashService = _contextObject.AddComponent<InMemoryPlayerStashService>();
            stashService.Initialize(_store);
            var loadoutService = _contextObject.AddComponent<InMemoryPlayerLoadoutService>();
            loadoutService.Initialize(_store);
            _remote = _contextObject.AddComponent<FakeRemoteComponent>();
            context.Initialize(_store, stashService, loadoutService, null, null);

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(StashPrefabPath);
            Assert.That(prefab, Is.Not.Null);
            _canvasObject = new GameObject(
                "LobbyStashPresenterTestCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            _canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            _instance = Object.Instantiate(prefab, _canvasObject.transform, false);
            _instance.SetActive(true);
            _view = _instance.GetComponent<LobbyStashUI>();
            Assert.That(_view, Is.Not.Null);
        }

        private LootId[] DistinctLootIds(int count)
        {
            var ids = new List<LootId>();
            for (int index = 0; index < _catalog.DefinitionCount && ids.Count < count; index++)
            {
                if (_catalog.TryGetByIndex(index, out LootDefinition definition))
                {
                    ids.Add(definition.LootId);
                }
            }

            Assert.That(ids, Has.Count.EqualTo(count));
            return ids.ToArray();
        }

        private RaidLootPanelView StashPanel() => GetPanel("_stashPanel");

        private RaidLootPanelView LoadoutPanel() => GetPanel("_loadoutPanel");

        private RaidLootPanelView GetPanel(string fieldName)
        {
            var field = typeof(LobbyStashUI).GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            return (RaidLootPanelView)field.GetValue(_view);
        }

        private Button GetButton(string fieldName)
        {
            var field = typeof(LobbyStashUI).GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            var button = (Button)field.GetValue(_view);
            Assert.That(button, Is.Not.Null, fieldName);
            return button;
        }

        private void ClickButton(Button button)
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                var eventSystemObject = new GameObject("LobbyStashPresenterTestEventSystem", typeof(EventSystem));
                eventSystemObject.transform.SetParent(_canvasObject.transform, false);
                eventSystem = eventSystemObject.GetComponent<EventSystem>();
            }

            Assert.That(button.IsActive(), Is.True, "The button must be active to receive pointer clicks.");
            // Exercise Button's interactability guard, not UnityEvent.Invoke; no raycast or physical mouse is simulated.
            ExecuteEvents.Execute(button.gameObject,
                new PointerEventData(eventSystem) { button = PointerEventData.InputButton.Left },
                ExecuteEvents.pointerClickHandler);
        }

        private void ClickMoveAll(RaidLootPanelView panel, int occupiedIndex = 0)
        {
            RaidInventorySlotView[] occupiedSlots = Array.FindAll(
                panel.GetComponentsInChildren<RaidInventorySlotView>(true),
                slot => slot.IsOccupied);
            Assert.That(occupiedSlots.Length, Is.GreaterThan(occupiedIndex), "The panel must show the stack to move.");
            RaidInventorySlotView occupied = occupiedSlots[occupiedIndex];
            occupied.OnPointerClick(new PointerEventData(null) { button = PointerEventData.InputButton.Right });

            RaidLootContextMenuView menu = _instance.GetComponentInChildren<RaidLootContextMenuView>(true);
            Assert.That(menu.IsOpen, Is.True);
            RaidLootContextActionButton action = Array.Find(
                menu.GetComponentsInChildren<RaidLootContextActionButton>(true),
                candidate => candidate.gameObject.activeSelf);
            Assert.That(action, Is.Not.Null);
            action.GetComponent<Button>().onClick.Invoke();
        }

        private static RaidInventorySlotView FindOccupiedSlot(RaidLootPanelView panel) =>
            Array.Find(panel.GetComponentsInChildren<RaidInventorySlotView>(true), slot => slot.IsOccupied);

        public sealed class FakeRemoteComponent : MonoBehaviour, IRemoteInventoryService, ISlotRemoteInventoryService, IRemoteGemInstallService, IRemoteBulkTransferService
        {
            public readonly List<int> TransferSlots = new();

            /// <summary>Models the backend: the same per-stack rules, in source slot order, on a scratch profile.</summary>
            public async Task<(bool success, TransferAllResult data, BackendError error)> TransferAllAsync(bool fromStash)
            {
                await Run($"bulk:{(fromStash ? "toLoadout" : "toStash")}");
                var store = GetComponent<ApplicationStashContext>().Store;
                var profile = new ProfileId(ProfileValue);
                var repository = new InMemoryLocalProfileRepository();
                Assert.That(repository.Initialize(profile, store.LootCatalog), Is.True);
                LocalProfileSnapshot scratch = repository.Snapshot.Clone();
                scratch.InventorySchemaVersion = store.InventorySchemaVersion;
                scratch.Stash.AddRange(store.GetStash());
                scratch.Loadout.AddRange(store.GetLoadout());
                scratch.PreparedEquipment = store.GetPreparedEquipment();
                Assert.That(repository.TrySave(scratch, out string error), Is.True, error);
                var backend = new LocalProfileStore(repository, profile, store.LootCatalog);
                var results = new List<TransferAllStackResult>();
                foreach (StashItem item in (fromStash ? backend.GetStash() : backend.GetLoadout()).OrderBy(i => i.Slot).ToArray())
                {
                    StashOperationResult rule = backend.TryTransfer(item.Slot, item.LootId, item.Amount, fromStash);
                    results.Add(new TransferAllStackResult
                    {
                        slot = item.Slot, lootId = item.LootId.Value, amount = item.Amount,
                        moved = rule == StashOperationResult.Success,
                        errorCode = rule == StashOperationResult.Success ? null
                            : rule == StashOperationResult.InventoryFull ? "INVENTORY_FULL" : "VALIDATION_FAILED"
                    });
                }

                InventoryItemData[] Map(IReadOnlyList<StashItem> items) => items.Select(item => new InventoryItemData
                {
                    slot = item.Slot, lootId = item.LootId.Value, amount = item.Amount,
                    instance = EquipmentInstanceData.FromInstance(item.Instance)
                }).ToArray();
                return (true, new TransferAllResult
                {
                    stash = Map(backend.GetStash()), loadout = Map(backend.GetLoadout()), results = results.ToArray(),
                    revision = store.RemoteRevision + (results.Any(r => r.moved) ? 1 : 0)
                }, default);
            }
            public async Task<(bool success, MoveItemResult data, BackendError error)> InstallGemAsync(InstallGemRequest request)
            {
                await Run($"gem:{request.gemList}:{request.gemSlot}:{request.weaponList}:{request.weaponSlot}");
                var store = GetComponent<ApplicationStashContext>().Store;
                var catalog = store.LootCatalog;
                catalog.TryGet(request.gemLootId, out var gem);
                InventoryItemData[] Map(IReadOnlyList<StashItem> items, InventoryList list) => items.Select(item =>
                {
                    var instance = item.Instance;
                    int amount = item.Amount;
                    if (list == request.weaponList && item.Slot == request.weaponSlot)
                    {
                        instance.Modifiers.Value.TryInstallGem(gem, out var installed, out _);
                        instance = new EquipmentInstance(instance.InstanceId, installed);
                    }
                    if (list == request.gemList && item.Slot == request.gemSlot) amount--;
                    return new InventoryItemData { slot = item.Slot, lootId = item.LootId.Value,
                        amount = amount, instance = EquipmentInstanceData.FromInstance(instance) };
                }).Where(item => item.amount > 0).ToArray();
                return (true, new MoveItemResult { stash = Map(store.GetStash(), InventoryList.Stash),
                    loadout = Map(store.GetLoadout(), InventoryList.Loadout), revision = store.RemoteRevision + 1 }, default);
            }


            public bool FailSplit;
            public async Task<(bool success, BackendError error)> SplitSlotAsync(
                InventoryList list, int sourceSlot, int amount, int destinationSlot, LootId lootId)
            {
                var result = await Run($"split:{list}:{sourceSlot}:{amount}:{destinationSlot}:{lootId.Value}");
                return FailSplit ? (false, new BackendError { error = "FEATURE_DISABLED" }) : result;
            }

            public Task<(bool success, BackendError error)> TransferAsync(int sourceSlot, LootId id, int amount, bool fromStash)
            {
                TransferSlots.Add(sourceSlot);
                return Run($"{(fromStash ? "toLoadout" : "toStash")}:{id.Value}:{amount}");
            }

            public Task<(bool success, BackendError error)> OrderSlotsAsync(InventoryList list, IReadOnlyList<int> slots) =>
                Run($"order:{list}:{string.Join(",", slots)}");

            public Task<(bool success, BackendError error)> MoveSlotsAsync(
                InventoryList from, int fromSlot, InventoryList to, int toSlot, LootId id) =>
                Run($"move:{from}:{fromSlot}:{to}:{toSlot}:{id.Value}");
            public Task<(bool success, BackendError error)> OrderAsync(InventoryList list, IReadOnlyList<LootId> lootIds) =>
                Run($"order:{list}:{string.Join(",", lootIds.Select(id => id.Value))}");
            public readonly List<string> Calls = new();
            public readonly Queue<Task> Gates = new();

            public Task<(bool success, BackendError error)> MoveToLoadoutAsync(LootId lootId, int amount) =>
                Run($"toLoadout:{lootId.Value}:{amount}");

            public Task<(bool success, BackendError error)> MoveToStashAsync(LootId lootId, int amount) =>
                Run($"toStash:{lootId.Value}:{amount}");

            public Task<(bool success, BackendError error)> UpdatePreparedEquipmentAsync(
                PreparedEquipmentLoadout equipment) => Run("equipment");

            private async Task<(bool success, BackendError error)> Run(string call)
            {
                Calls.Add(call);
                if (Gates.Count > 0)
                {
                    await Gates.Dequeue();
                }

                return (true, default);
            }
        }
    }
}
#endif
