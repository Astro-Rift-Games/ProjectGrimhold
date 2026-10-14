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
                snapshot.Stash.Add(new StashItem(sword, 2));
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
                Assert.That(_store.GetStash(), Is.EqualTo(new[] { new StashItem(sword, 1) }));
                Assert.That(_store.GetLoadout(), Is.EqualTo(new[] { new StashItem(displaced, 1) }));
                Assert.That(FindOccupiedSlot(LoadoutPanel()), Is.Not.Null);
            }
        }

        [UnityTest]
        public IEnumerator MoveAllFromTheStash_ReachesTheBackendThenMovesTheStackAndRefreshesTheView()
        {
            CreateScreen(snapshot => snapshot.Stash.Add(new StashItem(Bone, 3)));
            yield return null;

            ClickMoveAll(StashPanel());
            yield return null;

            Assert.That(_remote.Calls, Is.EqualTo(new[] { "toLoadout:bone:3" }));
            Assert.That(_store.GetStash(), Is.Empty);
            Assert.That(_store.GetLoadout().Single(), Is.EqualTo(new StashItem(Bone, 3)));
            Assert.That(FindOccupiedSlot(LoadoutPanel()), Is.Not.Null, "The Loadout panel must show the moved stack.");
            Assert.That(FindOccupiedSlot(StashPanel()), Is.Null, "The Stash panel must no longer show it.");
        }

        [UnityTest]
        public IEnumerator MoveAllFromTheStash_IntoAFullLoadout_NeverReachesTheBackend()
        {
            LootId[] ids = DistinctLootIds(LocalProfileSnapshot.MaxLoadoutSlots + 1);
            CreateScreen(snapshot =>
            {
                foreach (LootId id in ids.Take(LocalProfileSnapshot.MaxLoadoutSlots))
                {
                    snapshot.Loadout.Add(new StashItem(id, 1));
                }

                snapshot.Stash.Add(new StashItem(ids[LocalProfileSnapshot.MaxLoadoutSlots], 1));
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
                snapshot.Stash.Add(new StashItem(Bone, 3));
                snapshot.Stash.Add(new StashItem(sword, 1));
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

        private void CreateScreen(Action<LocalProfileSnapshot> configure)
        {
            var profile = new ProfileId(ProfileValue);
            var repository = new InMemoryLocalProfileRepository();
            Assert.That(repository.Initialize(profile, _catalog), Is.True);
            LocalProfileSnapshot snapshot = repository.Snapshot.Clone();
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

        public sealed class FakeRemoteComponent : MonoBehaviour, IRemoteInventoryService
        {
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
