#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEditor;
using UnityEngine.InputSystem;

namespace Tests.EditMode.Presentation
{
    public sealed class InteractionPromptTextTests
    {
        private const string InputActionsPath = "Assets/Input/PlayerInputActions.inputactions";
        private const string KeyboardPrefix = "<Keyboard>/";

        [Test]
        public void KeyLabel_MatchesTheRealInteractKeyboardBinding()
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            Assert.That(asset, Is.Not.Null, "The player Input Actions asset must exist.");
            InputAction action = asset.FindAction("Gameplay/Interact");
            Assert.That(action, Is.Not.Null, "Gameplay/Interact must exist in the Input Actions asset.");

            string keyboardPath = null;
            foreach (InputBinding binding in action.bindings)
            {
                if (binding.path != null && binding.path.StartsWith(KeyboardPrefix, System.StringComparison.Ordinal))
                {
                    keyboardPath = binding.path;
                    break;
                }
            }

            Assert.That(keyboardPath, Is.Not.Null, "Gameplay/Interact must have a keyboard binding.");
            Assert.That(
                KeyboardPrefix + InteractionPromptText.KeyLabel.ToLowerInvariant(),
                Is.EqualTo(keyboardPath),
                "The interaction prompt key label drifted from the real binding.");
        }

        [Test]
        public void Format_ShowsKeyBadgeAndTheSpanishActionText()
        {
            Assert.That(InteractionPromptText.Format("Abrir cofre"), Is.EqualTo("[F] Abrir cofre"));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void Format_WithoutAction_FallsBackToInteractuar(string action)
        {
            Assert.That(InteractionPromptText.Format(action), Is.EqualTo("[F] Interactuar"));
        }
    }
}
#endif
