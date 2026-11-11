using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using UnityEngine.InputSystem;

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

        [TestCase("AbilitySlot1", "<Keyboard>/q")]
        [TestCase("AbilitySlot2", "<Keyboard>/e")]
        public void AbilityInputAsset_DefinesIndependentPressOnlyKeyboardActions(string name, string path)
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Input/PlayerInputActions.inputactions");
            Assert.That(asset, Is.Not.Null);
            InputAction action = asset.FindAction("Gameplay/" + name);
            Assert.That(action, Is.Not.Null, "The owning InputActions asset must define " + name);
            Assert.That(action.type, Is.EqualTo(InputActionType.Button));
            Assert.That(action.wantsInitialStateCheck, Is.True, "Held input must enter the action phase so release can rearm it.");
            Assert.That(action.interactions, Is.EqualTo("Press(behavior=0)"));
            Assert.That(action.bindings.Count, Is.EqualTo(1));
            Assert.That(action.bindings[0].path, Is.EqualTo(path));
        }

        [Test]
        public void InteractInputAsset_UsesTheFKey()
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Input/PlayerInputActions.inputactions");
            InputAction interact = asset.FindAction("Gameplay/Interact");

            Assert.That(interact, Is.Not.Null);
            Assert.That(interact.bindings.Count, Is.EqualTo(1));
            Assert.That(interact.bindings[0].path, Is.EqualTo("<Keyboard>/f"));
        }

        [TestCase("<Keyboard>/e", "AbilitySlot2")]
        [TestCase("<Keyboard>/f", "Interact")]
        [TestCase("<Keyboard>/q", "AbilitySlot1")]
        public void GameplayMap_BindsEachActionKeyToASingleAction(string path, string expectedAction)
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Input/PlayerInputActions.inputactions");
            InputActionMap gameplay = asset.FindActionMap("Gameplay");

            var owners = new System.Collections.Generic.List<string>();
            foreach (InputBinding binding in gameplay.bindings)
            {
                if (binding.path == path)
                {
                    owners.Add(binding.action);
                }
            }

            Assert.That(owners, Is.EqualTo(new[] { expectedAction }));
        }

        [TestCase("AbilitySlot1", "<Keyboard>/q")]
        [TestCase("AbilitySlot2", "<Keyboard>/e")]
        public void GeneratedAbilityActions_MatchOwningAsset(string name, string path)
        {
            var actions = new PlayerInputActions();
            try
            {
                InputAction action = actions.asset.FindAction("Gameplay/" + name);
                Assert.That(action, Is.Not.Null, "Unity must generate the ability action wrapper from its asset.");
                Assert.That(action.bindings[0].path, Is.EqualTo(path));
            }
            finally
            {
                // The generated Dispose uses Destroy, which is PlayMode-only.
                UnityEngine.Object.DestroyImmediate(actions.asset);
            }
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

        [Test]
        public void ScreenToAimWorldPosition_CompensatesForCameraShakeOffset()
        {
            var cameraGo = new GameObject("TestCam");
            var camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.transform.position = new Vector3(0f, 0f, -10f);

            var shakeGo = new GameObject("CameraShake");
            var shake = shakeGo.AddComponent<CameraShakeController>();
            var config = ScriptableObject.CreateInstance<CameraShakeConfig>();
            shake.Configure(config);

            try
            {
                var camField = typeof(PlayerInputReader).GetField("_worldCamera", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(camField, Is.Not.Null);
                camField.SetValue(_reader, camera);

                Vector2 screenPos = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                Vector2 aimWithoutShake = _reader.ScreenToAimWorldPosition(screenPos);

                shake.RequestShake(1f, 1f);
                shake.UpdateShake(0.01f);
                Vector3 offset = shake.ShakeOffset;
                camera.transform.position += offset;

                Vector2 aimWithShake = _reader.ScreenToAimWorldPosition(screenPos);

                Assert.That(aimWithShake.x, Is.EqualTo(aimWithoutShake.x).Within(0.001f));
                Assert.That(aimWithShake.y, Is.EqualTo(aimWithoutShake.y).Within(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cameraGo);
                UnityEngine.Object.DestroyImmediate(shakeGo);
            }
        }
    }
}
