#if UNITY_INCLUDE_TESTS
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Assert = NUnit.Framework.Assert;

namespace Assets.Tests.PlayMode.Player
{
    public sealed class PlayerInputReaderPlayModeTests
    {
        private GameObject _cameraHolder;
        private GameObject _holder;
        private PlayerInputReader _reader;
        private Keyboard _keyboard;
        private Mouse _mouse;
        private object _inputTestFixture;
        private Type _inputTestFixtureType;

        [SetUp]
        public void SetUp()
        {
            // The package test fixture is not auto-referenced by Unity's predefined
            // test assembly, so load it without changing the production assembly layout.
            _inputTestFixtureType = Type.GetType(
                "UnityEngine.InputSystem.InputTestFixture, Unity.InputSystem.TestFramework",
                true);
            _inputTestFixture = Activator.CreateInstance(_inputTestFixtureType);
            _inputTestFixtureType.GetMethod("Setup").Invoke(_inputTestFixture, null);

            _keyboard = InputSystem.AddDevice<Keyboard>();
            _mouse = InputSystem.AddDevice<Mouse>();

            _cameraHolder = new GameObject("InputTestCamera");
            _cameraHolder.tag = "MainCamera";
            _cameraHolder.transform.position = new Vector3(0f, 0f, -10f);
            Camera camera = _cameraHolder.AddComponent<Camera>();
            camera.orthographic = true;

            _holder = new GameObject("PlayerInputReaderHolder");
            _reader = _holder.AddComponent<PlayerInputReader>();
            ReadInputActions().asset.devices = new InputDevice[] { _keyboard, _mouse };
            EnsureReaderInputEnabled();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_holder);
            UnityEngine.Object.DestroyImmediate(_cameraHolder);
            InputSystem.RemoveDevice(_mouse);
            InputSystem.RemoveDevice(_keyboard);
            _inputTestFixtureType.GetMethod("TearDown").Invoke(_inputTestFixture, null);
        }

        [Test]
        public void Suppression_ProducesDefaultPayload()
        {
            SetKey(Key.W, true);
            SetMousePosition(new Vector2(200f, 150f));

            using IDisposable suppression = _reader.AcquireGameplayInputSuppression();

            Assert.That(_reader.ConsumeNetworkInput().Equals(default(PlayerNetworkInput)), Is.True);
        }

        [Test]
        public void ReleasingSuppression_RestoresHeldMovementImmediately()
        {
            SetKey(Key.W, true);
            Assert.That(_keyboard.wKey.isPressed, Is.True);
            Assert.That(ReadInputActions().Gameplay.Move.ReadValue<Vector2>(), Is.EqualTo(Vector2.up));
            IDisposable suppression = _reader.AcquireGameplayInputSuppression();
            Assert.That(_reader.ConsumeNetworkInput().MoveDirection, Is.EqualTo(Vector2.zero));

            suppression.Dispose();

            Assert.That(_reader.ConsumeNetworkInput().MoveDirection, Is.EqualTo(Vector2.up));
        }

        [Test]
        public void ReleasingSuppression_RecalculatesAimWithoutNewPointerMovement()
        {
            SetMousePosition(new Vector2(250f, 180f));
            Vector2 expectedAim = _reader.ConsumeNetworkInput().AimWorldPosition;
            IDisposable suppression = _reader.AcquireGameplayInputSuppression();
            Assert.That(_reader.ConsumeNetworkInput().AimWorldPosition, Is.EqualTo(Vector2.zero));

            suppression.Dispose();

            Assert.That(_reader.ConsumeNetworkInput().AimWorldPosition, Is.EqualTo(expectedAim));
        }

        [Test]
        public void DiscreteButtonsPressedDuringSuppression_AreDiscardedUntilNewPress()
        {
            IDisposable suppression = _reader.AcquireGameplayInputSuppression();
            SetMouseLeft(true);
            SetKey(Key.E, true);

            suppression.Dispose();
            PlayerNetworkInput heldAfterClose = _reader.ConsumeNetworkInput();
            Assert.That(heldAfterClose.Buttons.IsSet(PlayerInputButton.PrimaryAttack), Is.False);
            Assert.That(heldAfterClose.Buttons.IsSet(PlayerInputButton.Interact), Is.False);

            SetMouseLeft(false);
            SetKey(Key.E, false);
            SetMouseLeft(true);
            SetKey(Key.E, true);
            InvokeReaderLifecycle("Update");

            PlayerNetworkInput newPress = _reader.ConsumeNetworkInput();
            Assert.That(newPress.Buttons.IsSet(PlayerInputButton.PrimaryAttack), Is.True);
            Assert.That(newPress.Buttons.IsSet(PlayerInputButton.Interact), Is.True);
        }

        [Test]
        public void InventoryToggle_RemainsAvailableDuringSuppression()
        {
            int toggleCount = 0;
            _reader.InventoryToggleRequested += () => toggleCount++;
            using IDisposable suppression = _reader.AcquireGameplayInputSuppression();

            SetKey(Key.Tab, true);

            Assert.That(toggleCount, Is.EqualTo(1));
            Assert.That(_reader.ConsumeNetworkInput().Equals(default(PlayerNetworkInput)), Is.True);
        }

        [Test]
        public void AttributesToggle_RemainsAvailableDuringSuppression()
        {
            int toggleCount = 0;
            _reader.AttributesToggleRequested += () => toggleCount++;
            using IDisposable suppression = _reader.AcquireGameplayInputSuppression();

            SetKey(Key.C, true);

            Assert.That(toggleCount, Is.EqualTo(1));
            Assert.That(_reader.ConsumeNetworkInput().Equals(default(PlayerNetworkInput)), Is.True);
        }

        [Test]
        public void InventoryClose_RemainsAvailableDuringSuppression()
        {
            int closeCount = 0;
            _reader.InventoryCloseRequested += () => { closeCount++; return true; };
            using IDisposable suppression = _reader.AcquireGameplayInputSuppression();

            SetKey(Key.Escape, true);

            Assert.That(closeCount, Is.EqualTo(1));
            Assert.That(_reader.ConsumeNetworkInput().Equals(default(PlayerNetworkInput)), Is.True);
        }

        [Test]
        public void InteractPressedLocally_FiresDuringSuppressionAndDoesNotTransport()
        {
            int localPressCount = 0;
            _reader.InteractPressedLocally += () => localPressCount++;
            using IDisposable suppression = _reader.AcquireGameplayInputSuppression();

            SetKey(Key.E, true);

            Assert.That(localPressCount, Is.EqualTo(1));
            Assert.That(_reader.ConsumeNetworkInput().Buttons.IsSet(PlayerInputButton.Interact), Is.False);
        }

        [Test]
        public void ReleasingSuppressionWhileHoldingInteract_RequiresPhysicalReleaseBeforeNewInteractTransport()
        {
            IDisposable suppression = _reader.AcquireGameplayInputSuppression();
            SetKey(Key.E, true);

            suppression.Dispose();

            Assert.That(_reader.ConsumeNetworkInput().Buttons.IsSet(PlayerInputButton.Interact), Is.False);

            SetKey(Key.E, false);
            SetKey(Key.E, true);
            InvokeReaderLifecycle("Update");

            Assert.That(_reader.ConsumeNetworkInput().Buttons.IsSet(PlayerInputButton.Interact), Is.True);
        }

        [Test]
        public void DisableEnable_ClearsPendingDiscreteInputAndKeepsMapsUsable()
        {
            SetKey(Key.E, true);
            _reader.enabled = false;
            _reader.enabled = true;

            Assert.That(
                _reader.ConsumeNetworkInput().Buttons.IsSet(PlayerInputButton.Interact),
                Is.False);

            SetKey(Key.E, false);
            SetKey(Key.E, true);

            Assert.That(
                _reader.ConsumeNetworkInput().Buttons.IsSet(PlayerInputButton.Interact),
                Is.True);
        }

        private void SetKey(Key key, bool pressed)
        {
            SetControl(_keyboard[key], pressed ? 1f : 0f);
        }

        private void SetMousePosition(Vector2 position)
        {
            SetControl(_mouse.position, position);
        }

        private void SetMouseLeft(bool pressed)
        {
            SetControl(_mouse.leftButton, pressed ? 1f : 0f);
        }

        private static void SetControl<TValue>(
            InputControl<TValue> control,
            TValue value)
            where TValue : struct
        {
            using (DeltaStateEvent.From(control, out InputEventPtr eventPtr))
            {
                eventPtr.time = InputState.currentTime;
                control.WriteValueIntoEvent(value, eventPtr);
                InputSystem.QueueEvent(eventPtr);
            }

            InputSystem.Update();
        }

        private void EnsureReaderInputEnabled()
        {
            PlayerInputActions actions = ReadInputActions();

            if (actions.Gameplay.enabled)
            {
                InvokeReaderLifecycle("OnDisable");
            }

            InvokeReaderLifecycle("OnEnable");
        }

        private void InvokeReaderLifecycle(string methodName)
        {
            MethodInfo method = typeof(PlayerInputReader).GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(_reader, null);
        }

        private PlayerInputActions ReadInputActions()
        {
            FieldInfo actionsField = typeof(PlayerInputReader).GetField(
                "_inputActions",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(actionsField, Is.Not.Null);
            var actions = (PlayerInputActions)actionsField.GetValue(_reader);
            Assert.That(actions, Is.Not.Null);
            return actions;
        }
    }
}
#endif
