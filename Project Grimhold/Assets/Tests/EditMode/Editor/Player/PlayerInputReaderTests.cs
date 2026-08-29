using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Player
{
    public sealed class PlayerInputReaderTests
    {
        private GameObject _holder;
        private PlayerInputReader _reader;

        [SetUp]
        public void SetUp()
        {
            _holder = new GameObject("PlayerInputReaderHolder");
            _reader = _holder.AddComponent<PlayerInputReader>();
            InvokeLifecycle("Awake");
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_holder);
        }

        [Test]
        public void Suppression_ProducesDefaultPayload()
        {
            using IDisposable suppression = _reader.AcquireGameplayInputSuppression();

            Assert.That(_reader.ConsumeNetworkInput().Equals(default(PlayerNetworkInput)), Is.True);
        }

        [Test]
        public void NestedSuppressions_RequireEveryOwnerToRelease()
        {
            IDisposable first = _reader.AcquireGameplayInputSuppression();
            IDisposable second = _reader.AcquireGameplayInputSuppression();

            first.Dispose();
            Assert.That(_reader.IsGameplayInputSuppressed, Is.True);

            second.Dispose();
            Assert.That(_reader.IsGameplayInputSuppressed, Is.False);
        }

        [Test]
        public void DuplicateRelease_IsInnocuous()
        {
            IDisposable suppression = _reader.AcquireGameplayInputSuppression();

            suppression.Dispose();
            suppression.Dispose();

            Assert.That(_reader.IsGameplayInputSuppressed, Is.False);
        }

        [Test]
        public void CallbackReentrancy_DoesNotLeakInteractButtonToPendingInputWhenReleasedInCallback()
        {
            IDisposable suppression = _reader.AcquireGameplayInputSuppression();
            _reader.InteractPressedLocally += () => suppression.Dispose();

            InvokeOnInteractPerformed();

            Assert.That(_reader.ConsumeNetworkInput().Buttons.IsSet(PlayerInputButton.Interact), Is.False);
        }

        [Test]
        public void NestedSuppressions_ReleasingOneTokenKeepsGameplaySuppressedAndDoesNotTransportInteract()
        {
            IDisposable first = _reader.AcquireGameplayInputSuppression();
            IDisposable second = _reader.AcquireGameplayInputSuppression();

            _reader.InteractPressedLocally += () => first.Dispose();

            InvokeOnInteractPerformed();

            Assert.That(_reader.IsGameplayInputSuppressed, Is.True);
            Assert.That(_reader.ConsumeNetworkInput().Buttons.IsSet(PlayerInputButton.Interact), Is.False);
            second.Dispose();
        }

        private void InvokeOnInteractPerformed()
        {
            MethodInfo method = typeof(PlayerInputReader).GetMethod(
                "OnInteractPerformed",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(_reader, new object[] { default(UnityEngine.InputSystem.InputAction.CallbackContext) });
        }

        private void InvokeLifecycle(string methodName)
        {
            MethodInfo method = typeof(PlayerInputReader).GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(_reader, null);
        }
    }
}
