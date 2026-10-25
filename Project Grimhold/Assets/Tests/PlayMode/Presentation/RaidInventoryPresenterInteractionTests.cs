#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using Assert = NUnit.Framework.Assert;

namespace Tests.PlayMode.Presentation
{
    public sealed class RaidInventoryPresenterInteractionTests
    {
        private const string PlayerPrefabPath = "Assets/Prefabs/NetworkPlayer.prefab";
        private const string ChestPrefabPath = "Assets/Prefabs/LootContainer.prefab";
        private const string EnemyPrefabPath = "Assets/Prefabs/Enemies/NetworkEnemy.prefab";

        private GameObject _playerInstance;
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

        [SetUp]
        public void SetUp()
        {
            _inputTestFixtureType = Type.GetType(
                "UnityEngine.InputSystem.InputTestFixture, Unity.InputSystem.TestFramework",
                true);
            _inputTestFixture = Activator.CreateInstance(_inputTestFixtureType);
            _inputTestFixtureType.GetMethod("Setup").Invoke(_inputTestFixture, null);

            _keyboard = InputSystem.AddDevice<Keyboard>();

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

            SetPresenterField(_presenter, "_inputReader", _inputReader);

            PlayerInputActions actions = ReadInputActions(_inputReader);
            actions.asset.devices = new InputDevice[] { _keyboard };
            EnsureInputEnabled(_inputReader);

            _chestInstance = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ChestPrefabPath));
            _chestInstance.SetActive(false);

            _enemyInstance = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath));
            _enemyInstance.SetActive(false);
        }

        [TearDown]
        public void TearDown()
        {
            UnsubscribeLocalInteract();
            if (_playerInstance != null) UnityEngine.Object.DestroyImmediate(_playerInstance);
            if (_inputReaderHolder != null) UnityEngine.Object.DestroyImmediate(_inputReaderHolder);
            if (_chestInstance != null) UnityEngine.Object.DestroyImmediate(_chestInstance);
            if (_enemyInstance != null) UnityEngine.Object.DestroyImmediate(_enemyInstance);

            InputSystem.RemoveDevice(_keyboard);
            _inputTestFixtureType.GetMethod("TearDown").Invoke(_inputTestFixture, null);
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
            if (mode == 1) Assert.That(_view.SortButton.interactable, Is.True);
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
        public void RaidSort_OnlyPersonalMode_SortsOnceAndAppendsNewLoot()
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
            SetPresenterMode(_presenter, 2);
            InvokeMethod(_presenter, "OnSortRequested");
            var field = typeof(RaidInventoryPresenter).GetField("_displayOrder", BindingFlags.Instance | BindingFlags.NonPublic);
            var order = (RaidInventoryDisplayOrder)field.GetValue(_presenter);
            Assert.That(order.Entries, Is.Empty, "Container mode must reject Sort.");
            SetPresenterMode(_presenter, 1);
            InvokeMethod(_presenter, "OnSortRequested");
            Assert.That(order.Entries[0].LootId, Is.EqualTo(aFirst ? a.LootId : b.LootId));
            source.Items.RemoveAt(1);
            InvokeMethod(_presenter, "RefreshPlayerPanel");
            source.Items.Insert(0, aFirst ? a : b);
            InvokeMethod(_presenter, "RefreshPlayerPanel");
            Assert.That(order.Entries[1].LootId, Is.EqualTo(aFirst ? a.LootId : b.LootId));
            SetPresenterField(_presenter, "_gameplayMutationsBlocked", true);
            InvokeMethod(_presenter, "OnSortRequested");
            Assert.That(order.Entries[1].LootId, Is.EqualTo(aFirst ? a.LootId : b.LootId));
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
