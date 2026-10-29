#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Reflection;
using Fusion;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using Assert = NUnit.Framework.Assert;

namespace Tests.PlayMode.Presentation
{
    public sealed class RaidInventoryPresenterInteractionTests
    {
        private const string PlayerPrefabPath = "Assets/Prefabs/NetworkPlayer.prefab";
        private const string ChestPrefabPath = "Assets/Prefabs/LootContainer.prefab";
        private const string EnemyPrefabPath = "Assets/Prefabs/Enemies/NetworkEnemy.prefab";
        private const string PhysicalUiCategory = "PhysicalRaidUi";

        private GameObject _playerInstance;
        private GameObject _inventoryCanvas;
        private GameObject _inputReaderHolder;
        private GameObject _chestInstance;
        private GameObject _enemyInstance;
        private PlayerInputReader _inputReader;
        private RaidInventoryPresenter _presenter;
        private RaidInventoryView _view;
        private Keyboard _keyboard;
        private Action _localInteractHandler;
        private object _inputTestFixture;
        private Type _inputTestFixtureType;
        private NetworkRunner _uiRunner;
        private ExtractionProgressSimulationDriver _uiDriver;
        private PlayerLootReceiver _uiReceiver;
        private LootDefinitionCatalog _uiCatalog;
        private bool _createdEquipmentContent;
        private bool _fixtureNeedsCleanup;
        private static readonly LootId Bone = new("bone");

        [SetUp]
        public void SetUp()
        {
            _fixtureNeedsCleanup = true;
            _inputTestFixture = null;
            _keyboard = null;
            // Keep legacy keyboard tests isolated. Runner-backed gesture tests do not reset
            // the Input System underneath the production prefab's DefaultInputActions modules.
            if (!Equals(TestContext.CurrentContext.Test.Properties.Get("Category"), PhysicalUiCategory))
            {
                _inputTestFixtureType = Type.GetType(
                    "UnityEngine.InputSystem.InputTestFixture, Unity.InputSystem.TestFramework", true);
                _inputTestFixture = Activator.CreateInstance(_inputTestFixtureType);
                _inputTestFixtureType.GetMethod("Setup").Invoke(_inputTestFixture, null);
                _keyboard = InputSystem.AddDevice<Keyboard>();
            }
            else
            {
                LogAssert.ignoreFailingMessages = false;
            }

            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            Assert.That(playerPrefab, Is.Not.Null);

            _playerInstance = InstantiateWithoutUiInputModules(playerPrefab);
            _playerInstance.SetActive(false);

            _inputReaderHolder = new GameObject("PlayerInputReaderHolder");
            _inputReader = _inputReaderHolder.AddComponent<PlayerInputReader>();
            _presenter = _playerInstance.GetComponentInChildren<RaidInventoryPresenter>(true);
            _view = _playerInstance.GetComponentInChildren<RaidInventoryView>(true);

            Assert.That(_inputReader, Is.Not.Null);
            Assert.That(_presenter, Is.Not.Null);
            Assert.That(_view, Is.Not.Null);

            // Keep the network-player shell inactive and the presenter manually driven, but
            // give the real view an active hierarchy. Otherwise Show cannot run OnEnable on
            // an already-initialized transition, and pooled context buttons never run Awake
            // to install their real Button.onClick listeners.
            Assert.That(_presenter.gameObject, Is.SameAs(_view.gameObject));
            _presenter.enabled = false;
            _view.SetScreenVisible(false);
            _inventoryCanvas = new GameObject("RaidInventoryInteractionTestCanvas", typeof(RectTransform), typeof(Canvas));
            _inventoryCanvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            _view.transform.SetParent(_inventoryCanvas.transform, false);
            _view.gameObject.SetActive(true);
            Assert.That(_view.isActiveAndEnabled, Is.True);
            Assert.That(_view.IsOpen, Is.False);

            SetPresenterField(_presenter, "_inputReader", _inputReader);

            PlayerInputActions actions = ReadInputActions(_inputReader);
            if (_keyboard != null) actions.asset.devices = new InputDevice[] { _keyboard };
            EnsureInputEnabled(_inputReader);

            _chestInstance = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ChestPrefabPath));
            _chestInstance.SetActive(false);

            _enemyInstance = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath));
            _enemyInstance.SetActive(false);
        }

        [TearDown]
        public void TearDown()
        {
            // NUnit and Unity teardown ordering must not destroy a runner's registered
            // input reader before its asynchronous shutdown has completed.
            if (_uiRunner == null) CleanupFixture();
        }

        private void CleanupFixture()
        {
            if (!_fixtureNeedsCleanup) return;
            _fixtureNeedsCleanup = false;
            UnsubscribeLocalInteract();
            if (_playerInstance != null) UnityEngine.Object.DestroyImmediate(_playerInstance);
            if (_inventoryCanvas != null) UnityEngine.Object.DestroyImmediate(_inventoryCanvas);
            if (_inputReaderHolder != null) UnityEngine.Object.DestroyImmediate(_inputReaderHolder);
            if (_chestInstance != null) UnityEngine.Object.DestroyImmediate(_chestInstance);
            if (_enemyInstance != null) UnityEngine.Object.DestroyImmediate(_enemyInstance);

            if (_keyboard != null) InputSystem.RemoveDevice(_keyboard);
            if (_inputTestFixture != null) _inputTestFixtureType.GetMethod("TearDown").Invoke(_inputTestFixture, null);
        }

        // InputTestFixture keeps action states resolved before its Setup, so enabling the shared
        // DefaultInputActions here could bind controls of devices outside the isolated test system.
        private static GameObject InstantiateWithoutUiInputModules(GameObject prefab)
        {
            GameObject staging = new GameObject("InactivePlayerStaging");
            staging.SetActive(false);
            try
            {
                GameObject instance = UnityEngine.Object.Instantiate(prefab, staging.transform);
                foreach (InputSystemUIInputModule module in instance.GetComponentsInChildren<InputSystemUIInputModule>(true))
                {
                    module.enabled = false;
                }

                instance.transform.SetParent(null, false);
                return instance;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(staging);
            }
        }

        [Test]
        public async System.Threading.Tasks.Task TownSort_PersistsThroughSharedQueue_AllowedWhileReady()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<LootDefinitionCatalog>(
                "Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset");
            var profile = new ProfileId("49494949494949494949494949494949");
            var repository = new InMemoryLocalProfileRepository();
            Assert.That(repository.Initialize(profile, catalog), Is.True);
            var snapshot = repository.Snapshot.Clone();
            var expected = StashDisplayOrder.SortedCopy(new[] {
                new StashItem(new LootId("bone"), 2), new StashItem(new LootId("rapier"), 1) }, catalog);
            snapshot.Loadout.Add(expected[1]);
            snapshot.Loadout.Add(expected[0]);
            Assert.That(repository.TrySave(snapshot, out _), Is.True);
            var services = new GameObject("Town sort services");
            LocalLoadoutInventoryReadSource source = null;
            try
            {
                var store = new LocalProfileStore(repository, profile, catalog);
                var stash = services.AddComponent<InMemoryPlayerStashService>();
                stash.Initialize(store);
                var loadout = services.AddComponent<InMemoryPlayerLoadoutService>();
                loadout.Initialize(store);
                var remote = services.AddComponent<LobbyStashPresenterPlayModeTests.FakeRemoteComponent>();
                var context = services.AddComponent<ApplicationStashContext>();
                context.Initialize(store, stash, loadout, null, null);
                source = new LocalLoadoutInventoryReadSource(context, profile);
                SetPresenterField(_presenter, "_lootCatalog", catalog);
                _presenter.BindTown(source, source,
                    new TownEquipmentMutationEndpoint(loadout, profile, catalog, () => false),
                    _inputReader, true, null, context.GetTransferCoordinator(), () => store.PendingReservation == null);
                SetPresenterMode(_presenter, 1);
                Assert.That(typeof(RaidInventoryPresenter).GetProperty("CanSortInventory",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_presenter), Is.True,
                    "Ready blocks Equipment, not Sort.");
                var gate = new System.Threading.Tasks.TaskCompletionSource<bool>();
                remote.Gates.Enqueue(gate.Task);
                var task = (System.Threading.Tasks.Task)InvokeMethod(_presenter, "SortTownAsync");
                Assert.That(store.GetLoadout()[0], Is.EqualTo(expected[1]), "Remote first.");
                gate.SetResult(true);
                await task;
                Assert.That(store.GetLoadout(), Is.EqualTo(expected));
                Assert.That(remote.Calls, Has.Count.EqualTo(1));
                Assert.That(context.GetTransferCoordinator(), Is.SameAs(context.GetTransferCoordinator()));
                // A valid reservation requires prepared Main Hand equipment, not just carried loot.
                Assert.That(store.TryAssignPreparedEquipment(EquipmentSlot.WeaponSetAMainHand, new LootId("rapier")),
                    Is.EqualTo(StashOperationResult.Success));
                Assert.That(loadout.TryCreateLoadoutReservation(profile, "sort-pending", out _), Is.EqualTo(StashOperationResult.Success));
                InvokeMethod(_presenter, "OnSortRequested");
                Assert.That(remote.Calls, Has.Count.EqualTo(1), "Pending reservation rejects Sort.");
            }
            finally
            {
                _presenter.Unbind();
                source?.Dispose();
                UnityEngine.Object.DestroyImmediate(services);
            }
        }

        [TestCase(1)]
        [TestCase(2)]
        public void Downed_ObservesEntryAndRecovery_WithoutClosingScreen(int mode)
        {
            bool downed = false;
            SetPresenterField(_presenter, "_inventorySource", new SortSource());
            SetPresenterField(_presenter, "_isBound", true);
            SetPresenterField(_presenter, "_isRaidBinding", true);
            SetPresenterField(_presenter, "_readDowned", (Func<bool>)(() => downed));
            SetPresenterMode(_presenter, mode);
            SetViewScreenVisible(_view, true);
            InvokeMethod(_presenter, "ObserveDowned");
            var drag = new DragPayload(new LootId("bone"), DragSlotLocation.Inventory,
                default, null, 1, default, default);
            SetPresenterField(_presenter, "_activeDrag", drag);
            var takeAll = (RaidLootTakeAllState)typeof(RaidInventoryPresenter).GetField("_takeAllState",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_presenter);
            Assert.That(takeAll.TryBegin(new[] { new LootEntry(new LootId("bone"), 1) }), Is.True);
            downed = true;
            InvokeMethod(_presenter, "ObserveDowned");
            Assert.That(((DragPayload)typeof(RaidInventoryPresenter).GetField("_activeDrag",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_presenter)).IsValid, Is.False);
            Assert.That(takeAll.IsActive, Is.False);
            Assert.That(GetPresenterMode(_presenter), Is.EqualTo(mode));
            Assert.That(_presenter.IsOpen, Is.True);
            Assert.That(_view.TransferFeedbackText.text,
                Is.EqualTo("Estás abatido: el inventario es de solo lectura."));
            Assert.That(_view.TakeAllButton.interactable, Is.False);
            Assert.That(_view.SortButton.interactable, Is.False);
            _view.ShowTransferFeedback("Late confirmation");
            Assert.That(_view.TransferFeedbackText.text,
                Is.EqualTo("Estás abatido: el inventario es de solo lectura."));
            downed = false;
            InvokeMethod(_presenter, "ObserveDowned");
            Assert.That(GetPresenterMode(_presenter), Is.EqualTo(mode));
            Assert.That(_view.TransferFeedbackText.text, Is.Empty);
            if (mode == 1) Assert.That(_view.SortButton.interactable, Is.False, "Recovery does not enable Raid sorting.");
            downed = true;
            InvokeMethod(_presenter, "ObserveDowned");
            _presenter.SetGameplayMutationsBlocked(true);
            Assert.That(_presenter.IsOpen, Is.False);
            downed = true;
            InvokeMethod(_presenter, "ObserveDowned");
            downed = false;
            InvokeMethod(_presenter, "ObserveDowned");
            Assert.That(_presenter.GameplayMutationsBlocked, Is.True);
            Assert.That(_presenter.IsOpen, Is.False);
        }

        [Test]
        public void Downed_RejectsMutationEntryPoints_AndClearsOnUnbind()
        {
            SetPresenterField(_presenter, "_inventorySource", new SortSource());
            SetPresenterField(_presenter, "_isBound", true);
            SetPresenterField(_presenter, "_isRaidBinding", true);
            SetPresenterField(_presenter, "_readDowned", (Func<bool>)(() => true));
            SetPresenterMode(_presenter, 1);
            InvokeMethod(_presenter, "ObserveDowned");
            // Missing mutation endpoints deliberately make any escaped gate fail immediately.
            var drag = new DragPayload(new LootId("bone"), DragSlotLocation.Inventory,
                default, null, 1, default, default);
            InvokeMethod(_presenter, "OnDragStarted", drag);
            Assert.That(((DragPayload)typeof(RaidInventoryPresenter).GetField("_activeDrag",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_presenter)).IsValid, Is.False);
            SetPresenterField(_presenter, "_activeDrag", drag);
            InvokeMethod(_presenter, "OnDropReceived", DragSlotLocation.Equipment, EquipmentSlot.WeaponSetAMainHand);
            InvokeMethod(_presenter, "OnDropReceived", DragSlotLocation.Container, default(EquipmentSlot));
            SetPresenterField(_presenter, "_activeDrag", new DragPayload(new LootId("bone"),
                DragSlotLocation.Equipment, EquipmentSlot.WeaponSetAMainHand, null, 1, default, default));
            InvokeMethod(_presenter, "OnDropReceived", DragSlotLocation.Inventory, default(EquipmentSlot));
            InvokeMethod(_presenter, "OnPlayerSlotContextRequested", new LootId("bone"), null);
            InvokeMethod(_presenter, "OnContextActionRequested", default(LootContextActionId));
            InvokeMethod(_presenter, "OnEquipmentUnequipRequested", EquipmentSlot.WeaponSetAMainHand);
            InvokeMethod(_presenter, "OnEquipmentSlotContextRequested", EquipmentSlot.WeaponSetAMainHand, null);
            InvokeMethod(_presenter, "OnPlayerSlotSelected", new LootId("bone"), LootTransferQuantityMode.SingleUnit);
            InvokeMethod(_presenter, "OnContainerSlotSelected", new LootId("bone"), LootTransferQuantityMode.SingleUnit);
            InvokeMethod(_presenter, "OnTakeAllRequested");
            InvokeMethod(_presenter, "TryStartNextTakeAllTransfer");
            InvokeMethod(_presenter, "OnQuickSlotDropReceived", 0);
            InvokeMethod(_presenter, "OnQuickSlotClearRequested", 0);
            InvokeMethod(_presenter, "OnSortRequested");
            Assert.That(typeof(RaidInventoryPresenter).GetMethod("CanMutateQuickSlots",
                BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_presenter, null), Is.False);
            _presenter.Unbind();
            Assert.That(typeof(RaidInventoryPresenter).GetField("_downedReadOnly",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_presenter), Is.False);
        }

        [Test]
        public void Downed_TownBindingIgnoresPredicate()
        {
            SetPresenterField(_presenter, "_readDowned", (Func<bool>)(() => true));
            SetPresenterMode(_presenter, 1);
            InvokeMethod(_presenter, "ObserveDowned");
            Assert.That(typeof(RaidInventoryPresenter).GetMethod("CanMutateQuickSlots",
                BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_presenter, null), Is.True);
        }

        private sealed class SortSource : IInventoryReadSource
        {
            public readonly System.Collections.Generic.List<LootEntry> Items = new();
            public int SlotCapacity => 8;
            public int Revision => 0;
            public event Action Changed { add { } remove { } }
            public bool TryGetLootContent(out System.Collections.Generic.IReadOnlyList<LootEntry> content)
            {
                content = Items;
                return true;
            }
        }

        [Test]
        public void RaidSort_DisabledInPersonalAndContainerModes_DoesNotChangeDisplayOrSource()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<LootDefinitionCatalog>(
                "Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset");
            var a = new LootEntry(new LootId("bone"), 2);
            var b = new LootEntry(new LootId("rapier"), 1);
            var source = new SortSource();
            bool aFirst = StashDisplayOrder.Compare(new StashItem(a.LootId, 2), new StashItem(b.LootId, 1), catalog) < 0;
            source.Items.Add(aFirst ? b : a);
            source.Items.Add(aFirst ? a : b);
            SetPresenterField(_presenter, "_inventorySource", source);
            SetPresenterField(_presenter, "_lootCatalog", catalog);
            SetPresenterField(_presenter, "_isBound", true);
            SetPresenterField(_presenter, "_isRaidBinding", true);
            LootEntry[] before = source.Items.ToArray();
            var order = (RaidInventoryDisplayOrder)GetPresenterField(_presenter, "_displayOrder");
            foreach (int mode in new[] { 1, 2 })
            {
                SetPresenterMode(_presenter, mode);
                InvokeMethod(_presenter, "UpdateSortInteractable");
                Assert.That(typeof(RaidInventoryPresenter).GetProperty("CanSortInventory",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_presenter), Is.False);
                Assert.That(_view.SortButton.interactable, Is.False);
                InvokeMethod(_presenter, "OnSortRequested");
                Assert.That(order.Entries, Is.Empty, "Raid sorting may not create a local visual order.");
                Assert.That(source.Items, Is.EqualTo(before));
            }
        }

        [TestCase(2)]
        [TestCase(3)]
        public void LocalInteractPress_WhileLootIsOpenOrPending_ClosesScreenAndReleasesSuppression(int mode)
        {
            SetPresenterMode(_presenter, mode);
            SetViewScreenVisible(_view, true);
            SetViewContainerVisible(_view, true);
            IDisposable suppression = _inputReader.AcquireGameplayInputSuppression();
            SetPresenterField(_presenter, "_inputSuppression", suppression);
            SetPresenterField(_presenter, "_inputReader", _inputReader);

            SubscribeLocalInteract();

            SetKey(_keyboard, Key.F, true);

            Assert.That(GetPresenterMode(_presenter), Is.EqualTo(0)); // ScreenMode.Closed
            Assert.That(_view.IsOpen, Is.False);
            Assert.That(ReadSuppressionCount(_inputReader), Is.EqualTo(0));
        }

        [Test]
        public void LocalInteractPress_WhileInPersonalMode_DoesNotCloseOrChangeMode()
        {
            SetPresenterMode(_presenter, 1); // ScreenMode.Personal
            SetViewScreenVisible(_view, true);
            Assert.That(_view.IsOpen, Is.True, "The fixture must actually open the view before testing the input edge.");
            Assert.That(_view.ScreenRootRect.gameObject.activeInHierarchy, Is.True);
            IDisposable suppression = _inputReader.AcquireGameplayInputSuppression();
            SetPresenterField(_presenter, "_inputSuppression", suppression);
            SetPresenterField(_presenter, "_inputReader", _inputReader);

            SubscribeLocalInteract();

            SetKey(_keyboard, Key.F, true);

            Assert.That(GetPresenterMode(_presenter), Is.EqualTo(1)); // ScreenMode.Personal
            Assert.That(_view.IsOpen, Is.True);
            Assert.That(ReadSuppressionCount(_inputReader), Is.EqualTo(1));

            suppression.Dispose();
        }

        [TestCase(1)] // ScreenMode.Personal
        [TestCase(2)] // ScreenMode.ContainerLoot
        [TestCase(3)] // ScreenMode.OpeningContainer
        public void Escape_WhileInventoryIsOpen_ClosesScreenAndReleasesSuppression(int mode)
        {
            SetPresenterMode(_presenter, mode);
            SetViewScreenVisible(_view, true);
            SetViewContainerVisible(_view, mode == 2);
            IDisposable suppression = _inputReader.AcquireGameplayInputSuppression();
            SetPresenterField(_presenter, "_inputSuppression", suppression);

            Func<bool> closeHandler = () => (bool)InvokeMethod(_presenter, "OnInventoryCloseRequested");
            _inputReader.InventoryCloseRequested += closeHandler;
            try
            {
                SetKey(_keyboard, Key.Escape, true);

                Assert.That(GetPresenterMode(_presenter), Is.EqualTo(0));
                Assert.That(_view.IsOpen, Is.False);
                Assert.That(ReadSuppressionCount(_inputReader), Is.EqualTo(0));
            }
            finally
            {
                _inputReader.InventoryCloseRequested -= closeHandler;
            }
        }

        [Test]
        public void Escape_WhileInventoryIsClosed_DoesNotOpenIt()
        {
            SetPresenterMode(_presenter, 0);
            SetViewScreenVisible(_view, false);

            Func<bool> closeHandler = () => (bool)InvokeMethod(_presenter, "OnInventoryCloseRequested");
            _inputReader.InventoryCloseRequested += closeHandler;
            try
            {
                SetKey(_keyboard, Key.Escape, true);

                Assert.That(GetPresenterMode(_presenter), Is.EqualTo(0));
                Assert.That(_view.IsOpen, Is.False);
                Assert.That(ReadSuppressionCount(_inputReader), Is.EqualTo(0));
            }
            finally
            {
                _inputReader.InventoryCloseRequested -= closeHandler;
            }
        }

        [TestCase("OnInventoryToggleRequested")]
        [TestCase("OnDisable")]
        [TestCase("Unbind")]
        public void PendingOpeningCancellation_ReleasesInputWithoutReopening(string method)
        {
            SetPresenterMode(_presenter, 3);
            SetPresenterField(_presenter, "_inputSuppression", _inputReader.AcquireGameplayInputSuppression());
            InvokeMethod(_presenter, method);
            Assert.That(GetPresenterMode(_presenter), Is.EqualTo(0));
            Assert.That(_view.IsOpen, Is.False);
            Assert.That(ReadSuppressionCount(_inputReader), Is.Zero);
            Assert.That(_presenter.IsOpen, Is.False);
        }

        [Test]
        public void DefeatWhileOpening_CancelsPendingScreenAndInputSuppression()
        {
            SetPresenterMode(_presenter, 3);
            SetPresenterField(_presenter, "_inputSuppression", _inputReader.AcquireGameplayInputSuppression());
            _presenter.SetGameplayMutationsBlocked(true);
            Assert.That(_presenter.IsOpen, Is.False);
            Assert.That(ReadSuppressionCount(_inputReader), Is.Zero);
            Assert.That(_view.IsOpen, Is.False);
        }

        [Test]
        public void LostTargetWhileOpening_CancelsPendingScreenAndInputSuppression()
        {
            SetPresenterMode(_presenter, 3);
            SetPresenterField(_presenter, "_isBound", true);
            SetPresenterField(_presenter, "_isRaidBinding", true);
            SetPresenterField(_presenter, "_inventorySource", _playerInstance.GetComponent<PlayerLootReceiver>());
            SetPresenterField(_presenter, "_inputSuppression", _inputReader.AcquireGameplayInputSuppression());
            InvokeMethod(_presenter, "Update");
            Assert.That(_presenter.IsOpen, Is.False);
            Assert.That(ReadSuppressionCount(_inputReader), Is.Zero);
            Assert.That(_view.IsOpen, Is.False);
        }

        [Test]
        public void ClosingLootMode_DoesNotModifyContainerContentOrAvailability()
        {
            NetworkLootContainer chestContainer = _chestInstance.GetComponent<NetworkLootContainer>();
            NetworkLootContainerInteractable chestInteractable = _chestInstance.GetComponent<NetworkLootContainerInteractable>();
            Assert.That(chestContainer, Is.Not.Null);
            Assert.That(chestInteractable, Is.Not.Null);

            SetPresenterMode(_presenter, 2);
            SetPresenterField(_presenter, "_container", chestContainer);
            SetPresenterField(_presenter, "_containerInteractable", chestInteractable);
            IDisposable suppression = _inputReader.AcquireGameplayInputSuppression();
            SetPresenterField(_presenter, "_inputSuppression", suppression);
            SetPresenterField(_presenter, "_inputReader", _inputReader);

            SubscribeLocalInteract();

            SetKey(_keyboard, Key.F, true);

            Assert.That(GetPresenterMode(_presenter), Is.EqualTo(0));
            Assert.That(GetPresenterField(_presenter, "_container"), Is.Null);
            Assert.That(GetPresenterField(_presenter, "_containerInteractable"), Is.Null);
        }

        [Test]
        public void ChestAndPersistentEnemy_ContainMatchingContainerAndAdapterComposition()
        {
            NetworkLootContainer chestContainer = _chestInstance.GetComponent<NetworkLootContainer>();
            NetworkLootContainerInteractable chestInteractable = _chestInstance.GetComponent<NetworkLootContainerInteractable>();
            NetworkLootContainer enemyContainer = _enemyInstance.GetComponent<NetworkLootContainer>();
            NetworkLootContainerInteractable enemyInteractable = _enemyInstance.GetComponent<NetworkLootContainerInteractable>();

            Assert.That(chestContainer, Is.Not.Null);
            Assert.That(chestInteractable, Is.Not.Null);
            Assert.That(enemyContainer, Is.Not.Null);
            Assert.That(enemyInteractable, Is.Not.Null);
            Assert.That(chestInteractable.GetType(), Is.EqualTo(enemyInteractable.GetType()));
            Assert.That(chestContainer.GetType(), Is.EqualTo(enemyContainer.GetType()));
            Assert.That(enemyContainer.StartsAvailable, Is.False);
        }

        [Test]
        public void Close_IsIdempotentAndReleasesSuppressionOnce()
        {
            SetPresenterMode(_presenter, 2);
            SetViewScreenVisible(_view, true);
            IDisposable suppression = _inputReader.AcquireGameplayInputSuppression();
            SetPresenterField(_presenter, "_inputSuppression", suppression);

            _presenter.Close();
            _presenter.Close();

            Assert.That(GetPresenterMode(_presenter), Is.EqualTo(0));
            Assert.That(_view.IsOpen, Is.False);
            Assert.That(ReadSuppressionCount(_inputReader), Is.EqualTo(0));
        }

        [Test]
        public void Reentrancy_ClosingInCallbackPreventsNetworkInputLeaking()
        {
            SetPresenterMode(_presenter, 2);
            IDisposable suppression = _inputReader.AcquireGameplayInputSuppression();
            SetPresenterField(_presenter, "_inputSuppression", suppression);
            SetPresenterField(_presenter, "_inputReader", _inputReader);

            SubscribeLocalInteract();

            SetKey(_keyboard, Key.F, true);

            PlayerNetworkInput networkInput = _inputReader.ConsumeNetworkInput();
            Assert.That(networkInput.Buttons.IsSet(PlayerInputButton.Interact), Is.False);
        }

        [Test]
        public void PhysicalReleaseRequired_AfterClosingWhileEHeldDown()
        {
            SetPresenterMode(_presenter, 2);
            IDisposable suppression = _inputReader.AcquireGameplayInputSuppression();
            SetPresenterField(_presenter, "_inputSuppression", suppression);
            SetPresenterField(_presenter, "_inputReader", _inputReader);

            SubscribeLocalInteract();

            SetKey(_keyboard, Key.F, true); // Press F to close

            Assert.That(GetPresenterMode(_presenter), Is.EqualTo(0));

            // While E remains held down, network input MUST NOT have Interact set
            Assert.That(_inputReader.ConsumeNetworkInput().Buttons.IsSet(PlayerInputButton.Interact), Is.False);

            // Release E physically
            SetKey(_keyboard, Key.F, false);

            // Press F physically again
            SetKey(_keyboard, Key.F, true);
            InvokeReaderLifecycle(_inputReader, "Update");

            // Now Interact should be transported!
            Assert.That(_inputReader.ConsumeNetworkInput().Buttons.IsSet(PlayerInputButton.Interact), Is.True);
        }

        [Test]
        public void ConsumeConfirmation_ShowsSuccessFeedbackAndKeepsRejectionDistinct()
        {
            SetPresenterMode(_presenter, 1); // ScreenMode.Personal

            InvokeMethod(_presenter, "OnConsumeConfirmed", new LootId("health_potion"));

            Assert.That(_view.TransferFeedbackText.gameObject.activeSelf, Is.True);
            Assert.That(_view.TransferFeedbackText.text, Is.EqualTo("Consumible usado."));

            InvokeMethod(_presenter, "OnConsumeRejected", ConsumableFailureReason.HealthFull);

            Assert.That(_view.TransferFeedbackText.gameObject.activeSelf, Is.True);
            Assert.That(_view.TransferFeedbackText.text, Is.EqualTo("Tu salud ya está al máximo."));
        }

        [Test]
        public void ConsumePresentation_MissingCatalogDoesNotThrow()
        {
            ConsumableParticlePresenter presenter = _playerInstance.GetComponent<ConsumableParticlePresenter>();
            Assert.That(presenter, Is.Not.Null);
            typeof(ConsumableParticlePresenter)
                .GetField("_lootCatalog", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(presenter, null);

            MethodInfo callback = typeof(ConsumableParticlePresenter).GetMethod(
                "OnConsumeConfirmed",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(callback, Is.Not.Null);
            Assert.DoesNotThrow(() => callback.Invoke(presenter, new object[] { new LootId("health_potion") }));
        }

        [TestCase(LootTransferFailureReason.Uninitialized, "No se pudo retirar el loot")]
        [TestCase(LootTransferFailureReason.None, "No se pudo retirar el loot")]
        [TestCase(LootTransferFailureReason.InvalidLoot, "Loot no válido")]
        [TestCase(LootTransferFailureReason.InvalidAmount, "Cantidad no válida")]
        [TestCase(LootTransferFailureReason.SourceNotFound, "Contenedor no encontrado")]
        [TestCase(LootTransferFailureReason.DestinationNotFound, "Inventario no disponible")]
        [TestCase(LootTransferFailureReason.InsufficientAmount, "El stack ya no está disponible")]
        [TestCase(LootTransferFailureReason.InventoryFull, "Inventario lleno")]
        [TestCase(LootTransferFailureReason.OutOfRange, "Fuera de alcance")]
        [TestCase(LootTransferFailureReason.MissingAuthority, "Transferencia sin autoridad")]
        [TestCase(LootTransferFailureReason.ContainerUnavailable, "Contenedor no disponible")]
        [TestCase(LootTransferFailureReason.Overflow, "La cantidad excede el límite")]
        public void TransferFailureReason_MapsToContextualFeedback(
            LootTransferFailureReason reason,
            string expectedMessage)
        {
            MethodInfo method = typeof(RaidInventoryPresenter).GetMethod(
                "GetTransferFailureMessage",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            string message = method.Invoke(null, new object[] { reason }) as string;

            Assert.That(message, Is.EqualTo(expectedMessage));
        }

        [TestCase(LootTransferFailureReason.SourceNotFound, "Inventario no disponible")]
        [TestCase(LootTransferFailureReason.DestinationNotFound, "Contenedor no encontrado")]
        [TestCase(LootTransferFailureReason.InventoryFull, "Contenedor lleno")]
        [TestCase(LootTransferFailureReason.Uninitialized, "No se pudo depositar el loot")]
        public void DepositFailureReason_MapsToDestinationContext(
            LootTransferFailureReason reason,
            string expectedMessage)
        {
            MethodInfo method = typeof(RaidInventoryPresenter).GetMethod(
                "GetDirectionalTransferFailureMessage",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            string message = method.Invoke(null, new object[] { reason, true }) as string;

            Assert.That(message, Is.EqualTo(expectedMessage));
        }

        [TestCase(LootTransferTransportRejectionReason.BusyWithDifferentSequence, "Hay otra transferencia en curso")]
        [TestCase(LootTransferTransportRejectionReason.StaleSequence, "La solicitud de transferencia venció")]
        [TestCase(LootTransferTransportRejectionReason.DependenciesUnavailable, "Transferencia no disponible")]
        [TestCase(LootTransferTransportRejectionReason.Uninitialized, "No se pudo completar la transferencia")]
        public void TransportRejectionReason_MapsToContextualFeedback(
            LootTransferTransportRejectionReason reason,
            string expectedMessage)
        {
            MethodInfo method = typeof(RaidInventoryPresenter).GetMethod(
                "GetTransportRejectionMessage",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            string message = method.Invoke(null, new object[] { reason }) as string;

            Assert.That(message, Is.EqualTo(expectedMessage));
        }

        [UnityTest, Category(PhysicalUiCategory)]
        public IEnumerator RaidPhysicalRefresh_ShowsAllThirtyAddressesOriginsAndInteriorHole()
        {
            yield return BeginPhysicalUi();
            LootDefinition bone = Definition(Bone);
            yield return UiTick(() =>
            {
                GrantUiLoot(Bone, checked(bone.StackMax * 29 + 1), RaidLootOrigin.Dungeon);
                ExtractUiSlot(7, bone.StackMax);
                GrantUiLoot(Bone, 1, PlayerOrigin(2));
                ExtractUiSlot(11, bone.StackMax);
                EquipmentTestContent.SetField(_uiReceiver, "_slotCapacity", 2);
            });
            InvokeMethod(_presenter, "RefreshPlayerPanel");
            Assert.That(_view.PlayerPanel.SlotCount, Is.EqualTo(30), "Gameplay occupancy limits cannot truncate physical addresses.");
            RaidInventorySlotView[] views = PlayerSlots();
            for (int index = 0; index < 30; index++)
            {
                Assert.That(_uiReceiver.TryGetInventorySlot(index, out LootInventorySlot source), Is.True);
                RaidInventorySlotData displayed = SlotData(views[index]);
                Assert.That(views[index].gameObject.activeSelf, Is.True);
                Assert.That(displayed.SourceInventorySlotIndex, Is.EqualTo(index));
                Assert.That(displayed.ExpectedInventoryRevision, Is.EqualTo(_uiReceiver.Revision));
                Assert.That(displayed.IsOccupied, Is.EqualTo(!source.IsEmpty), $"Physical slot {index} was repacked.");
                Assert.That(displayed.Amount, Is.EqualTo(source.Quantity));
                if (source.IsValid)
                {
                    Assert.That(_uiCatalog.TryGetByIndex(source.CatalogIndex, out LootDefinition definition), Is.True);
                    Assert.That(displayed.LootId, Is.EqualTo(definition.LootId));
                    Assert.That(displayed.Origin, Is.EqualTo(source.Origin));
                }
                else
                {
                    Assert.That(displayed.LootId.IsValid, Is.False);
                    Assert.That(displayed.Origin, Is.Null);
                }
            }
            Assert.That(views[11].IsOccupied, Is.False);
            Assert.That(SlotData(views[7]).Origin, Is.EqualTo(PlayerOrigin(2)));
            Assert.That(SlotData(views[29]).Amount, Is.EqualTo(1));
            Assert.That(_uiReceiver.TryCalculateTotalValue(out long total), Is.True);
            Assert.That(_view.PlayerPanel.TotalValueText.text, Is.EqualTo($"Valor: {total}"));
            Assert.That(((RaidInventoryDisplayOrder)GetPresenterField(_presenter, "_displayOrder")).Entries, Is.Empty);
        }

        [UnityTest, Category(PhysicalUiCategory)]
        public IEnumerator PhysicalDragDeposit_ForwardsCapturedAddressRevisionAndSelectedFullStack()
        {
            yield return BeginPhysicalUi();
            yield return UiTick(() => GrantUiLoot(Bone, checked(Definition(Bone).StackMax + 1), RaidLootOrigin.Dungeon));
            NetworkLootContainer container = BindUiContainer();
            Assert.That(_uiReceiver.TryGetInventorySlot(0, out LootInventorySlot first), Is.True);
            int revision = _uiReceiver.Revision;
            LootTransferConfirmation? confirmed = null;
            var controller = _uiReceiver.GetComponent<PlayerLootTransferNetworkController>();
            controller.TransferConfirmed += result => confirmed = result;
            BeginUiDrag(1);
            yield return UiTick(() =>
            {
                DropUiOnContainer();
                Assert.That(controller.DebugTryGetInFlightIdentity(out LootTransferRequestIdentity sent), Is.True);
                Assert.That(sent.SourceInventorySlotIndex, Is.EqualTo(1));
                Assert.That(sent.ExpectedInventoryRevision, Is.EqualTo(revision));
                Assert.That(sent.QuantityMode, Is.EqualTo(LootTransferQuantityMode.FullStack));
            });
            yield return WaitForUi(() => confirmed.HasValue);
            Assert.That(confirmed.Value.Result.Success, Is.True);
            Assert.That(confirmed.Value.Result.TransferredAmount, Is.EqualTo(1));
            Assert.That(container.GetLootAmount(Bone), Is.EqualTo(1));
            Assert.That(_uiReceiver.TryGetInventorySlot(0, out LootInventorySlot remainingFirst), Is.True);
            Assert.That(remainingFirst, Is.EqualTo(first));
            Assert.That(_uiReceiver.TryGetInventorySlot(1, out LootInventorySlot selected), Is.True);
            Assert.That(selected.IsEmpty, Is.True);
        }

        [UnityTest, Category(PhysicalUiCategory)]
        public IEnumerator PhysicalContextDropAll_UsesSelectedPlayersOriginAndSlotQuantity()
        {
            yield return BeginPhysicalUi();
            yield return UiTick(() =>
            {
                GrantUiLoot(Bone, checked(Definition(Bone).StackMax + 1), RaidLootOrigin.Dungeon);
                GrantUiLoot(Bone, 1, PlayerOrigin(2));
            });
            Assert.That(_uiReceiver.TryGetInventorySlot(0, out LootInventorySlot first), Is.True);
            Assert.That(_uiReceiver.TryGetInventorySlot(1, out LootInventorySlot second), Is.True);
            LootDropConfirmation? confirmed = null;
            _uiReceiver.GetComponent<PlayerLootDropNetworkController>().DropConfirmed += result => confirmed = result;
            OpenUiContext(2);
            LootContextActionContext context = (LootContextActionContext)GetPresenterField(_presenter, "_contextActionContext");
            Assert.That(context.SourceInventorySlotIndex, Is.EqualTo(2));
            Assert.That(context.ExpectedInventoryRevision, Is.EqualTo(_uiReceiver.Revision));
            Assert.That(context.Entry.Amount, Is.EqualTo(1));
            yield return UiTick(() => ClickUiContextAction(LootDropContextActionProvider.DropAllId));
            yield return WaitForUi(() => confirmed.HasValue);
            Assert.That(confirmed.Value.Result.Success, Is.True);
            Assert.That(confirmed.Value.Result.DroppedAmount, Is.EqualTo(1));
            Assert.That(_uiReceiver.TryGetInventorySlot(0, out LootInventorySlot remainingFirst), Is.True);
            Assert.That(_uiReceiver.TryGetInventorySlot(1, out LootInventorySlot remainingSecond), Is.True);
            Assert.That(remainingFirst, Is.EqualTo(first));
            Assert.That(remainingSecond, Is.EqualTo(second));
            Assert.That(_uiReceiver.TryGetInventorySlot(2, out LootInventorySlot selected), Is.True);
            Assert.That(selected.IsEmpty, Is.True);
            int published = 0;
            foreach (NetworkLootPickup pickup in UnityEngine.Object.FindObjectsByType<NetworkLootPickup>(FindObjectsSortMode.None))
            {
                if (pickup.Object == null || !pickup.Object.IsValid || pickup.Runner != _uiRunner) continue;
                published++;
                Assert.That(pickup.Amount, Is.EqualTo(1));
                Assert.That(pickup.TryResolveRaidOriginTransfer(out RaidLootOriginTransfer transfer), Is.True);
                Assert.That(transfer.Count, Is.EqualTo(1));
                Assert.That(transfer.Buckets[0].Origin, Is.EqualTo(PlayerOrigin(2)));
            }
            Assert.That(published, Is.EqualTo(1));
        }

        [UnityTest, Category(PhysicalUiCategory)]
        public IEnumerator PhysicalContextEquip_SecondSameOriginSlotLeavesFirstUntouched() => EquipThroughUi(useContext: true);

        [UnityTest, Category(PhysicalUiCategory)]
        public IEnumerator PhysicalDragEquip_SecondOriginReachesEquipmentWithoutCanonicalFallback() => EquipThroughUi(useContext: false);

        private IEnumerator EquipThroughUi(bool useContext)
        {
            yield return BeginPhysicalUi();
            _createdEquipmentContent = true;
            LootDefinition helmet = EquipmentTestContent.CreateArmorDefinition("physical_ui_helmet", LootCategory.Helmet);
            _uiCatalog = EquipmentTestContent.CreateCatalog(helmet);
            EquipmentTestContent.SetField(_uiReceiver, "_lootCatalog", _uiCatalog);
            var equipment = _uiReceiver.GetComponent<PlayerWeaponEquipmentNetworkController>();
            EquipmentTestContent.SetField(equipment, "_lootCatalog", _uiCatalog);
            SetPresenterField(_presenter, "_lootCatalog", _uiCatalog);
            yield return UiTick(() =>
            {
                GrantUiLoot(helmet.LootId, 1, RaidLootOrigin.Dungeon);
                GrantUiLoot(helmet.LootId, 1, useContext ? RaidLootOrigin.Dungeon : PlayerOrigin(2));
            });
            EquipmentOperationResult result = EquipmentOperationResult.None;
            equipment.EquipRequestResolved += observed => result = observed;
            Assert.That(_uiReceiver.TryGetInventorySlot(0, out LootInventorySlot first), Is.True);
            if (useContext) OpenUiContext(1); else BeginUiDrag(1);
            yield return UiTick(() =>
            {
                if (useContext) ClickUiContextAction(new LootContextActionId("equipment.fixed-slot"));
                else ((RaidInventorySlotView)typeof(RaidInventoryView).GetField("_helmetView",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_view))
                    .OnDrop(new PointerEventData(null) { button = PointerEventData.InputButton.Left });
            });
            yield return WaitForUi(() => result != EquipmentOperationResult.None);
            Assert.That(result, Is.EqualTo(EquipmentOperationResult.Succeeded));
            Assert.That(_uiReceiver.TryGetInventorySlot(0, out LootInventorySlot remainingFirst), Is.True);
            Assert.That(remainingFirst, Is.EqualTo(first));
            Assert.That(_uiReceiver.TryGetInventorySlot(1, out LootInventorySlot selected), Is.True);
            Assert.That(selected.IsEmpty, Is.True);
            Assert.That(equipment.TryGetSlotRaidOrigin(EquipmentSlot.Helmet, out RaidLootOrigin origin), Is.True);
            Assert.That(origin, Is.EqualTo(useContext ? RaidLootOrigin.Dungeon : PlayerOrigin(2)));
        }

        [UnityTest, Category(PhysicalUiCategory)]
        public IEnumerator PhysicalContext_RevisionChangeClosesMenuAndCannotRetargetSameItem()
        {
            yield return BeginPhysicalUi();
            yield return UiTick(() => GrantUiLoot(Bone, checked(Definition(Bone).StackMax + 1), RaidLootOrigin.Dungeon));
            OpenUiContext(1);
            int captured = _uiReceiver.Revision;
            yield return UiTick(() => GrantUiLoot(new LootId("coins"), 1, RaidLootOrigin.Dungeon));
            yield return WaitForUi(() => (int)GetPresenterField(_presenter, "_observedPlayerLootSequence") == _uiReceiver.Revision);
            Assert.That(_uiReceiver.Revision, Is.Not.EqualTo(captured));
            Assert.That(_view.ContextMenu.IsOpen, Is.False);
            Assert.That(((LootContextActionContext)GetPresenterField(_presenter, "_contextActionContext")).IsValid, Is.False);
            int amount = _uiReceiver.GetLootAmount(Bone);
            yield return UiTick(() =>
            {
                // Invoke the real pooled button even though its menu root is now hidden.
                ClickUiContextAction(LootDropContextActionProvider.DropAllId);
                Assert.That(_uiReceiver.GetComponent<PlayerLootDropNetworkController>().HasRequestInFlight, Is.False);
            });
            Assert.That(_uiReceiver.GetLootAmount(Bone), Is.EqualTo(amount));
        }

        [UnityTest, Category(PhysicalUiCategory)]
        public IEnumerator PhysicalDrag_RevisionChangeCancelsPayloadBeforeDestinationAction()
        {
            yield return BeginPhysicalUi();
            yield return UiTick(() => GrantUiLoot(Bone, checked(Definition(Bone).StackMax + 1), RaidLootOrigin.Dungeon));
            NetworkLootContainer container = BindUiContainer();
            BeginUiDrag(1);
            yield return UiTick(() => GrantUiLoot(new LootId("coins"), 1, RaidLootOrigin.Dungeon));
            yield return WaitForUi(() => (int)GetPresenterField(_presenter, "_observedPlayerLootSequence") == _uiReceiver.Revision);
            Assert.That(((DragPayload)GetPresenterField(_presenter, "_activeDrag")).IsValid, Is.False);
            int amount = _uiReceiver.GetLootAmount(Bone);
            yield return UiTick(() =>
            {
                DropUiOnContainer();
                Assert.That(_uiReceiver.GetComponent<PlayerLootTransferNetworkController>().HasRequestInFlight, Is.False);
            });
            Assert.That(_uiReceiver.GetLootAmount(Bone), Is.EqualTo(amount));
            Assert.That(container.GetLootAmount(Bone), Is.Zero);
        }

        [UnityTest, Category(PhysicalUiCategory)]
        public IEnumerator PhysicalContext_StaleRevisionAtDispatchRejectsBeforePresentationRefresh()
        {
            yield return BeginPhysicalUi();
            yield return UiTick(() => GrantUiLoot(Bone, checked(Definition(Bone).StackMax + 1), RaidLootOrigin.Dungeon));
            NetworkLootContainer container = BindUiContainer();
            OpenUiContext(1);
            int captured = _uiReceiver.Revision;
            int containerRevision = container.LootChangeSequence;
            int equipmentRevision = _uiReceiver.GetComponent<PlayerWeaponEquipmentNetworkController>().EquipmentRevision;
            LootInventorySlot[] inventory = null;
            int revision = -1;
            int dropNotifications = 0;
            var drop = _uiReceiver.GetComponent<PlayerLootDropNetworkController>();
            drop.RequestInFlightChanged += _ => dropNotifications++;
            drop.DropConfirmed += _ => dropNotifications++;
            drop.TransportRejected += _ => dropNotifications++;
            yield return UiTick(() =>
            {
                GrantUiLoot(new LootId("coins"), 1, RaidLootOrigin.Dungeon);
                revision = _uiReceiver.Revision;
                inventory = ReadUiInventory();
                Assert.That(revision, Is.Not.EqualTo(captured));
                Assert.That((int)GetPresenterField(_presenter, "_observedPlayerLootSequence"), Is.EqualTo(captured));
                Assert.That(SlotData(PlayerSlots()[1]).ExpectedInventoryRevision, Is.EqualTo(captured));
                Assert.That(_view.ContextMenu.IsOpen, Is.True, "Refresh must not have dismissed this menu yet.");
                Assert.That(((LootContextActionContext)GetPresenterField(_presenter, "_contextActionContext"))
                    .ExpectedInventoryRevision, Is.EqualTo(captured));
                // No yield or refresh between the authoritative mutation and this real button click.
                ClickUiContextAction(LootDropContextActionProvider.DropAllId);
                Assert.That((int)GetPresenterField(_presenter, "_observedPlayerLootSequence"), Is.EqualTo(captured));
                Assert.That(_view.ContextMenu.IsOpen, Is.False);
                Assert.That(((LootContextActionContext)GetPresenterField(_presenter, "_contextActionContext")).IsValid, Is.False);
                AssertUiNoGameplayMutation(inventory, revision, container, containerRevision, equipmentRevision);
            });
            yield return UiTick(() => { });
            AssertUiNoGameplayMutation(inventory, revision, container, containerRevision, equipmentRevision);
            Assert.That(dropNotifications, Is.Zero, "A stale UI action must not send even an authority-rejected request.");
        }

        [UnityTest, Category(PhysicalUiCategory)]
        public IEnumerator PhysicalDrag_StaleRevisionAtDispatchRejectsBeforePresentationRefresh()
        {
            yield return BeginPhysicalUi();
            yield return UiTick(() => GrantUiLoot(Bone, checked(Definition(Bone).StackMax + 1), RaidLootOrigin.Dungeon));
            NetworkLootContainer container = BindUiContainer();
            BeginUiDrag(1);
            int captured = _uiReceiver.Revision;
            int containerRevision = container.LootChangeSequence;
            int equipmentRevision = _uiReceiver.GetComponent<PlayerWeaponEquipmentNetworkController>().EquipmentRevision;
            LootInventorySlot[] inventory = null;
            int revision = -1;
            int transferNotifications = 0;
            var transfer = _uiReceiver.GetComponent<PlayerLootTransferNetworkController>();
            transfer.RequestInFlightChanged += _ => transferNotifications++;
            transfer.TransferConfirmed += _ => transferNotifications++;
            transfer.TransportRejected += _ => transferNotifications++;
            yield return UiTick(() =>
            {
                GrantUiLoot(new LootId("coins"), 1, RaidLootOrigin.Dungeon);
                revision = _uiReceiver.Revision;
                inventory = ReadUiInventory();
                Assert.That(revision, Is.Not.EqualTo(captured));
                Assert.That((int)GetPresenterField(_presenter, "_observedPlayerLootSequence"), Is.EqualTo(captured));
                Assert.That(SlotData(PlayerSlots()[1]).ExpectedInventoryRevision, Is.EqualTo(captured));
                DragPayload payload = (DragPayload)GetPresenterField(_presenter, "_activeDrag");
                Assert.That(payload.IsValid, Is.True, "Refresh must not have canceled this drag yet.");
                Assert.That(payload.ExpectedInventoryRevision, Is.EqualTo(captured));
                // No yield or refresh between the authoritative mutation and this real destination drop.
                DropUiOnContainer();
                Assert.That((int)GetPresenterField(_presenter, "_observedPlayerLootSequence"), Is.EqualTo(captured));
                Assert.That(((DragPayload)GetPresenterField(_presenter, "_activeDrag")).IsValid, Is.False);
                Assert.That(transfer.DebugTryGetInFlightIdentity(out _), Is.False);
                AssertUiNoGameplayMutation(inventory, revision, container, containerRevision, equipmentRevision);
            });
            yield return UiTick(() => { });
            AssertUiNoGameplayMutation(inventory, revision, container, containerRevision, equipmentRevision);
            Assert.That(transferNotifications, Is.Zero, "A stale UI drop must not send even an authority-rejected request.");
        }

        [UnityTest, Category(PhysicalUiCategory)]
        public IEnumerator PhysicalContextConsume_ClickedDungeonStillSpendsDefinitionPlayerFirst()
        {
            yield return BeginPhysicalUi();
            var potion = new LootId("healthpotion");
            PlayerCharacter player = _uiReceiver.GetComponent<PlayerCharacter>();
            yield return UiTick(() =>
            {
                GrantUiLoot(potion, 1, RaidLootOrigin.Dungeon);
                GrantUiLoot(potion, 1, PlayerOrigin(2));
                player.ApplyDamage(new DamageRequest(new EntityId(123), player.Id, 20,
                    DamageType.TrueDamage, Vector2.right, Vector2.zero, _uiRunner.Tick));
            });
            Assert.That(_uiReceiver.TryGetInventorySlot(0, out LootInventorySlot dungeon), Is.True);
            bool confirmed = false;
            _uiReceiver.GetComponent<PlayerConsumableNetworkController>().ConsumeConfirmed += _ => confirmed = true;
            OpenUiContext(0);
            yield return UiTick(() => ClickUiContextAction(LootConsumeContextActionProvider.ConsumeId));
            yield return WaitForUi(() => confirmed);
            Assert.That(_uiReceiver.TryGetInventorySlot(0, out LootInventorySlot remainingDungeon), Is.True);
            Assert.That(remainingDungeon, Is.EqualTo(dungeon), "Usar is definition-based, not clicked-origin extraction.");
            Assert.That(_uiReceiver.TryGetInventorySlot(1, out LootInventorySlot spentPlayer), Is.True);
            Assert.That(spentPlayer.IsEmpty, Is.True);
            Assert.That(_uiReceiver.GetLootAmount(potion), Is.EqualTo(1));
        }

        [UnityTest, Category(PhysicalUiCategory)]
        public IEnumerator PhysicalContext_RepeatedBindDisableReenableRetainsOneWorkingDispatch()
        {
            yield return BeginPhysicalUi();
            yield return UiTick(() => GrantUiLoot(Bone, 1, RaidLootOrigin.Dungeon));
            var drop = _uiReceiver.GetComponent<PlayerLootDropNetworkController>();
            int confirmations = 0;
            drop.DropConfirmed += result =>
            {
                Assert.That(result.Result.Success, Is.True);
                Assert.That(result.Result.DroppedAmount, Is.EqualTo(1));
                confirmations++;
            };
            // The view now has an active host, so real presenter enable/disable callbacks
            // are meaningful here. Personal mode needs no fabricated container watchdog state.
            _presenter.enabled = true;
            for (int cycle = 0; cycle < 2; cycle++)
            {
                _presenter.Bind(_uiReceiver, _inputReader, _uiReceiver.GetComponent<PlayerInteractionNetworkController>(),
                    _uiReceiver.GetComponent<PlayerLootTransferNetworkController>(), drop,
                    _uiReceiver.GetComponent<PlayerConsumableNetworkController>(),
                    _uiReceiver.GetComponent<PlayerWeaponEquipmentNetworkController>(), _uiRunner, _uiReceiver.transform);
                InvokeMethod(_presenter, "OpenPersonalInventory");
                OpenUiContext(0);
                Assert.That(ReadSuppressionCount(_inputReader), Is.EqualTo(1));
                _presenter.enabled = false;
                Assert.That(_presenter.IsOpen, Is.False);
                Assert.That(_view.IsOpen, Is.False);
                Assert.That(_view.ContextMenu.IsOpen, Is.False);
                Assert.That(ReadSuppressionCount(_inputReader), Is.Zero);
                _presenter.enabled = true;
                Assert.That(_presenter.IsOpen, Is.False, "Reenable must not replay the old open intention.");
                Assert.That(_view.IsOpen, Is.False);
                yield return null;
            }
            InvokeMethod(_presenter, "OpenPersonalInventory");
            OpenUiContext(0);
            Assert.That(ReadSuppressionCount(_inputReader), Is.EqualTo(1));
            yield return UiTick(() => ClickUiContextAction(LootDropContextActionProvider.DropAllId));
            yield return WaitForUi(() => confirmations > 0);
            yield return UiTick(() => { });
            Assert.That(confirmations, Is.EqualTo(1));
            Assert.That(_uiReceiver.GetLootAmount(Bone), Is.Zero);
            Assert.That(drop.HasRequestInFlight, Is.False);
            _presenter.Close();
            Assert.That(ReadSuppressionCount(_inputReader), Is.Zero);
        }

        // Reuses the existing production prefab, runner composition and tick driver. The
        // fixture's disabled presenter drives an active real view and real authoritative endpoints.
        private IEnumerator BeginPhysicalUi()
        {
            var owner = new GameObject("PhysicalRaidUiTestRunner");
            _uiRunner = owner.AddComponent<NetworkRunner>();
            owner.AddComponent<EntityRegistry>();
            Assert.That(PlayModeRunnerComposition.AddLocalInputContext(_uiRunner).TryRegister(_inputReader), Is.True);
            PlayModeRunnerComposition.AddTeamExtractionProgress(_uiRunner);
            _uiDriver = owner.AddComponent<ExtractionProgressSimulationDriver>();
            _uiRunner.ProvideInput = true;
            var start = _uiRunner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Single, SessionName = $"physical-ui-{Guid.NewGuid():N}",
                SceneManager = owner.AddComponent<NetworkSceneManagerDefault>(),
                ObjectProvider = owner.AddComponent<NetworkObjectProviderDefault>()
            });
            while (!start.IsCompleted) yield return null;
            Assert.That(start.IsFaulted, Is.False, start.Exception?.ToString());
            Assert.That(start.Result.Ok, Is.True, start.Result.ShutdownReason.ToString());
            Assert.That(CharacterAttributeState.TryCreate(5, 5, 5, 5, 5, 5, 0, out CharacterAttributeState attributes), Is.True);
            Assert.That(RaidParticipantId.TryCreate(1, out RaidParticipantId participantId), Is.True);
            NetworkObject participant = _uiRunner.Spawn(UiPrefab("c39d451563bae6e43934008a0dadc6d6"),
                inputAuthority: null, onBeforeSpawned: (_, instance) => instance.GetComponent<NetworkRaidParticipant>()
                    .Initialize("physical-ui-profile", participantId, attributes, ExperienceCurve.InitialLevel, 0, "physical-ui-generation"));
            NetworkObject avatar = _uiRunner.Spawn(UiPrefab("fea3a7b256f965a4eb9b965832939741"),
                Vector3.zero, Quaternion.identity, _uiRunner.LocalPlayer,
                onBeforeSpawned: (_, instance) => instance.GetComponent<RaidAvatarParticipantLink>().Initialize(participant));
            ExtractionProgressTestAssertions.AssertRegistered(_uiRunner, avatar);
            _uiReceiver = avatar.GetComponent<PlayerLootReceiver>();
            _uiCatalog = _uiReceiver.LootCatalog;
            Assert.That(_uiReceiver.HasInputAuthority, Is.True);
            SetPresenterField(_presenter, "_lootCatalog", _uiCatalog);
            _presenter.Bind(_uiReceiver, _inputReader, avatar.GetComponent<PlayerInteractionNetworkController>(),
                avatar.GetComponent<PlayerLootTransferNetworkController>(), avatar.GetComponent<PlayerLootDropNetworkController>(),
                avatar.GetComponent<PlayerConsumableNetworkController>(), avatar.GetComponent<PlayerWeaponEquipmentNetworkController>(),
                _uiRunner, avatar.transform);
            InvokeMethod(_presenter, "Subscribe");
            InvokeMethod(_presenter, "OpenPersonalInventory");
            Assert.That(_view.IsOpen, Is.True);
            Assert.That(_view.ScreenRootRect.gameObject.activeInHierarchy, Is.True);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator StopPhysicalUi()
        {
            if (_uiRunner == null) yield break;
            if (_presenter != null) _presenter.Unbind();
            if (_uiRunner.IsRunning)
            {
                var shutdown = _uiRunner.Shutdown();
                while (!shutdown.IsCompleted) yield return null;
            }
            if (_uiRunner != null) UnityEngine.Object.DestroyImmediate(_uiRunner.gameObject);
            _uiRunner = null;
            if (_createdEquipmentContent) EquipmentTestContent.Cleanup();
            _createdEquipmentContent = false;
            CleanupFixture();
        }

        private NetworkObject UiPrefab(string guid)
        {
            NetworkPrefabId id = _uiRunner.Config.PrefabTable.GetId(NetworkObjectGuid.Parse(guid));
            Assert.That(id.IsValid, Is.True, guid);
            return _uiRunner.Config.PrefabTable.Load(id, true);
        }

        private LootDefinition Definition(LootId id)
        {
            Assert.That(_uiCatalog.TryGet(id.Value, out LootDefinition definition), Is.True, id.Value);
            return definition;
        }

        private void GrantUiLoot(LootId id, int amount, RaidLootOrigin origin)
        {
            var request = new LootTransferRequest(new EntityId(int.MaxValue), _uiReceiver.Id, id, amount, _uiRunner.Tick);
            Assert.That(_uiReceiver.ValidateReceive(request), Is.EqualTo(LootTransferFailureReason.None));
            Assert.That(RaidLootOriginTransfer.TryCreate(origin, amount, out RaidLootOriginTransfer transfer), Is.True);
            Assert.That(_uiReceiver.ValidateRaidLootOriginReceive(request, transfer), Is.EqualTo(LootTransferFailureReason.None));
            _uiReceiver.CommitRaidLootReceive(request, transfer);
        }

        private void ExtractUiSlot(int index, int amount)
        {
            var request = new LootTransferRequest(_uiReceiver.Id, _uiReceiver.Id, Bone, amount, _uiRunner.Tick, index, _uiReceiver.Revision);
            Assert.That(_uiReceiver.ValidateExtraction(request), Is.EqualTo(LootTransferFailureReason.None));
            Assert.That(_uiReceiver.TryResolveRaidLootOriginTransfer(request, out RaidLootOriginTransfer transfer), Is.True);
            _uiReceiver.CommitRaidLootExtraction(request, transfer);
        }

        private NetworkLootContainer BindUiContainer()
        {
            bool initialized = false;
            NetworkObject spawned = _uiRunner.Spawn(UiPrefab("2c19a78647c64b84a765ff0280706b7d"),
                Vector3.right, Quaternion.identity, inputAuthority: null,
                onBeforeSpawned: (runner, instance) => initialized = instance.GetComponent<NetworkLootContainer>()
                    .TrySetInitialContentOverride(runner, instance, Array.Empty<LootEntry>()));
            Assert.That(initialized, Is.True);
            NetworkLootContainer container = spawned.GetComponent<NetworkLootContainer>();
            SetPresenterField(_presenter, "_container", container);
            SetPresenterMode(_presenter, 2);
            Physics2D.SyncTransforms();
            InvokeMethod(_presenter, "RefreshPlayerPanel");
            // Deliberately retain the authored container pool and strict logs. Its known
            // six-versus-thirty shortage is not repaired or ignored by this player-UI test.
            InvokeMethod(_presenter, "RefreshContainerPanel");
            _view.SetContainerPanelVisible(true);
            return container;
        }

        private void OpenUiContext(int index)
        {
            SetPresenterMode(_presenter, 1);
            InvokeMethod(_presenter, "RefreshPlayerPanel");
            RaidInventorySlotView slot = PlayerSlots()[index];
            Assert.That(slot.IsOccupied, Is.True, $"Physical slot {index} is not projected.");
            slot.OnPointerClick(new PointerEventData(null) { button = PointerEventData.InputButton.Right });
            Assert.That(_view.ContextMenu.IsOpen, Is.True);
            Assert.That(_view.ContextMenu.gameObject.activeInHierarchy, Is.True,
                "Real pooled buttons need an active hierarchy to install their Awake listeners.");
        }

        private void BeginUiDrag(int index)
        {
            InvokeMethod(_presenter, "RefreshPlayerPanel");
            RaidInventorySlotView slot = PlayerSlots()[index];
            Assert.That(slot.IsOccupied, Is.True, $"Physical slot {index} is not projected.");
            slot.OnBeginDrag(new PointerEventData(null) { button = PointerEventData.InputButton.Left });
            DragPayload payload = (DragPayload)GetPresenterField(_presenter, "_activeDrag");
            Assert.That(payload.IsValid, Is.True);
            Assert.That(payload.SourceInventorySlotIndex, Is.EqualTo(index));
            Assert.That(payload.ExpectedInventoryRevision, Is.EqualTo(_uiReceiver.Revision));
        }

        private void ClickUiContextAction(LootContextActionId actionId)
        {
            foreach (RaidLootContextActionButton action in _view.ContextMenu.GetComponentsInChildren<RaidLootContextActionButton>(true))
            {
                var presented = (LootContextActionId)typeof(RaidLootContextActionButton).GetField("_actionId",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(action);
                if (!action.gameObject.activeSelf || presented != actionId) continue;
                var button = (UnityEngine.UI.Button)typeof(RaidLootContextActionButton).GetField("_button",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(action);
                Assert.That(button.interactable, Is.True, actionId.ToString());
                button.onClick.Invoke();
                return;
            }
            Assert.Fail($"Requested context action was not presented: {actionId}.");
        }

        private void DropUiOnContainer()
        {
            var slots = (RaidInventorySlotView[])typeof(RaidLootPanelView).GetField("_authoredSlots",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_view.ContainerPanel);
            slots[0].OnDrop(new PointerEventData(null) { button = PointerEventData.InputButton.Left });
        }

        private LootInventorySlot[] ReadUiInventory()
        {
            var slots = new LootInventorySlot[30];
            for (int index = 0; index < slots.Length; index++)
                Assert.That(_uiReceiver.TryGetInventorySlot(index, out slots[index]), Is.True);
            return slots;
        }

        private void AssertUiNoGameplayMutation(LootInventorySlot[] inventory, int revision,
            NetworkLootContainer container, int containerRevision, int equipmentRevision)
        {
            Assert.That(_uiReceiver.GetComponent<PlayerLootDropNetworkController>().HasRequestInFlight, Is.False);
            Assert.That(_uiReceiver.GetComponent<PlayerLootTransferNetworkController>().HasRequestInFlight, Is.False);
            Assert.That(_uiReceiver.GetComponent<PlayerConsumableNetworkController>().HasRequestInFlight, Is.False);
            var equipment = _uiReceiver.GetComponent<PlayerWeaponEquipmentNetworkController>();
            Assert.That(equipment.HasRequestInFlight, Is.False);
            Assert.That(equipment.EquipmentRevision, Is.EqualTo(equipmentRevision));
            Assert.That(equipment.HasAnyEquipment, Is.False);
            Assert.That(_uiReceiver.Revision, Is.EqualTo(revision));
            Assert.That(ReadUiInventory(), Is.EqualTo(inventory), "All thirty quantities, addresses and origins must be unchanged.");
            Assert.That(container.LootChangeSequence, Is.EqualTo(containerRevision));
            Assert.That(container.TryGetLootContent(out var content), Is.True);
            Assert.That(content, Is.Empty);
            foreach (NetworkLootPickup pickup in UnityEngine.Object.FindObjectsByType<NetworkLootPickup>(FindObjectsSortMode.None))
            {
                Assert.That(pickup.Object == null || !pickup.Object.IsValid || pickup.Runner != _uiRunner, Is.True,
                    "A stale intention must not publish a world pickup.");
            }
        }

        private RaidInventorySlotView[] PlayerSlots() =>
            (RaidInventorySlotView[])typeof(RaidLootPanelView).GetField("_authoredSlots",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_view.PlayerPanel);

        private static RaidInventorySlotData SlotData(RaidInventorySlotView view) =>
            (RaidInventorySlotData)typeof(RaidInventorySlotView).GetField("_slotData",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);

        private static RaidLootOrigin PlayerOrigin(int value)
        {
            Assert.That(RaidParticipantId.TryCreate(value, out RaidParticipantId participant), Is.True);
            Assert.That(RaidLootOrigin.TryCreatePlayer(participant, out RaidLootOrigin origin), Is.True);
            return origin;
        }

        private IEnumerator UiTick(Action action)
        {
            _uiDriver.ClearException();
            _uiDriver.PendingAction = _ => action();
            yield return WaitForUi(() => _uiDriver.PendingAction == null);
            Assert.That(_uiDriver.LastException, Is.Null, _uiDriver.LastException?.ToString());
        }

        private static IEnumerator WaitForUi(Func<bool> ready)
        {
            float deadline = Time.realtimeSinceStartup + 10f;
            while (!ready() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(ready(), Is.True, "Expected UI/authority transition did not complete.");
        }

        private static void SetKey(Keyboard keyboard, Key key, bool pressed)
        {
            using (DeltaStateEvent.From(keyboard[key], out InputEventPtr eventPtr))
            {
                eventPtr.time = InputState.currentTime;
                keyboard[key].WriteValueIntoEvent(pressed ? 1f : 0f, eventPtr);
                InputSystem.QueueEvent(eventPtr);
            }

            InputSystem.Update();
        }

        private void SubscribeLocalInteract()
        {
            if (_localInteractHandler != null)
            {
                return;
            }

            _localInteractHandler = () => InvokeMethod(_presenter, "OnInteractPressedLocally");
            _inputReader.InteractPressedLocally += _localInteractHandler;
        }

        private void UnsubscribeLocalInteract()
        {
            if (_localInteractHandler == null || _inputReader == null)
            {
                return;
            }

            _inputReader.InteractPressedLocally -= _localInteractHandler;
            _localInteractHandler = null;
        }

        private static int GetPresenterMode(RaidInventoryPresenter presenter)
        {
            FieldInfo field = typeof(RaidInventoryPresenter).GetField("_mode", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return Convert.ToInt32(field.GetValue(presenter));
        }

        private static void SetPresenterMode(RaidInventoryPresenter presenter, int mode)
        {
            FieldInfo field = typeof(RaidInventoryPresenter).GetField("_mode", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            object enumVal = Enum.ToObject(field.FieldType, mode);
            field.SetValue(presenter, enumVal);
        }

        private static object GetPresenterField(RaidInventoryPresenter presenter, string fieldName)
        {
            FieldInfo field = typeof(RaidInventoryPresenter).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return field.GetValue(presenter);
        }

        private static void SetPresenterField(RaidInventoryPresenter presenter, string fieldName, object value)
        {
            FieldInfo field = typeof(RaidInventoryPresenter).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(presenter, value);
        }

        private static void SetViewScreenVisible(RaidInventoryView view, bool visible)
        {
            view.SetScreenVisible(visible);
        }

        private static void SetViewContainerVisible(RaidInventoryView view, bool visible)
        {
            view.SetContainerPanelVisible(visible);
        }

        private static object InvokeMethod(object target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.That(method, Is.Not.Null);
            return method.Invoke(target, arguments);
        }

        private static int ReadSuppressionCount(PlayerInputReader reader)
        {
            FieldInfo field = typeof(PlayerInputReader).GetField("_gameplaySuppressionCount", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return (int)field.GetValue(reader);
        }

        private static PlayerInputActions ReadInputActions(PlayerInputReader reader)
        {
            FieldInfo actionsField = typeof(PlayerInputReader).GetField("_inputActions", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(actionsField, Is.Not.Null);
            return (PlayerInputActions)actionsField.GetValue(reader);
        }

        private static void EnsureInputEnabled(PlayerInputReader reader)
        {
            PlayerInputActions actions = ReadInputActions(reader);
            if (actions.Gameplay.enabled)
            {
                InvokeReaderLifecycle(reader, "OnDisable");
            }
            InvokeReaderLifecycle(reader, "OnEnable");
        }

        private static void InvokeReaderLifecycle(PlayerInputReader reader, string methodName)
        {
            MethodInfo method = typeof(PlayerInputReader).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(reader, null);
        }
    }
}
#endif
