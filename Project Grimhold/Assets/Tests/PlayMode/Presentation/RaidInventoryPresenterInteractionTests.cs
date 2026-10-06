#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
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

            _playerInstance = UnityEngine.Object.Instantiate(playerPrefab);
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
