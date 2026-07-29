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
        private const string EnemyPrefabPath = "Assets/Prefabs/NetworkEnemy.prefab";

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

        [Test]
        public void LocalInteractPress_WhileInContainerLootMode_ClosesScreenAndReleasesSuppression()
        {
            SetPresenterMode(_presenter, 2); // ScreenMode.ContainerLoot
            SetViewScreenVisible(_view, true);
            SetViewContainerVisible(_view, true);
            IDisposable suppression = _inputReader.AcquireGameplayInputSuppression();
            SetPresenterField(_presenter, "_inputSuppression", suppression);
            SetPresenterField(_presenter, "_inputReader", _inputReader);

            SubscribeLocalInteract();

            SetKey(_keyboard, Key.E, true);

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

            SetKey(_keyboard, Key.E, true);

            Assert.That(GetPresenterMode(_presenter), Is.EqualTo(1)); // ScreenMode.Personal
            Assert.That(_view.IsOpen, Is.True);
            Assert.That(ReadSuppressionCount(_inputReader), Is.EqualTo(1));

            suppression.Dispose();
        }

        [TestCase(1)] // ScreenMode.Personal
        [TestCase(2)] // ScreenMode.ContainerLoot
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

            SetKey(_keyboard, Key.E, true);

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

            SetKey(_keyboard, Key.E, true);

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

            SetKey(_keyboard, Key.E, true); // Press E to close

            Assert.That(GetPresenterMode(_presenter), Is.EqualTo(0));

            // While E remains held down, network input MUST NOT have Interact set
            Assert.That(_inputReader.ConsumeNetworkInput().Buttons.IsSet(PlayerInputButton.Interact), Is.False);

            // Release E physically
            SetKey(_keyboard, Key.E, false);

            // Press E physically again
            SetKey(_keyboard, Key.E, true);
            InvokeReaderLifecycle(_inputReader, "Update");

            // Now Interact should be transported!
            Assert.That(_inputReader.ConsumeNetworkInput().Buttons.IsSet(PlayerInputButton.Interact), Is.True);
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

        private static object InvokeMethod(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.That(method, Is.Not.Null);
            return method.Invoke(target, null);
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
