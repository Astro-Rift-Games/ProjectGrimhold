#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEditor;
using UnityEngine.InputSystem;

namespace Tests.EditMode.Presentation
{
    public sealed class TownAbilitySlotKeyLabelsTests
    {
        private const string InputActionsPath = "Assets/Input/PlayerInputActions.inputactions";
        private const string KeyboardPrefix = "<Keyboard>/";

        [TestCase(UniversalAbilitySlot.Slot1, "Gameplay/AbilitySlot1")]
        [TestCase(UniversalAbilitySlot.Slot2, "Gameplay/AbilitySlot2")]
        public void Label_MatchesTheRealKeyboardBinding(UniversalAbilitySlot slot, string actionPath)
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            Assert.That(asset, Is.Not.Null, "The player Input Actions asset must exist.");
            InputAction action = asset.FindAction(actionPath);
            Assert.That(action, Is.Not.Null, $"{actionPath} must exist in the Input Actions asset.");

            string keyboardPath = null;
            foreach (InputBinding binding in action.bindings)
            {
                if (binding.path != null && binding.path.StartsWith(KeyboardPrefix, System.StringComparison.Ordinal))
                {
                    keyboardPath = binding.path;
                    break;
                }
            }

            Assert.That(keyboardPath, Is.Not.Null, $"{actionPath} must have a keyboard binding.");
            Assert.That(
                KeyboardPrefix + TownAbilitySlotKeyLabels.For(slot).ToLowerInvariant(),
                Is.EqualTo(keyboardPath),
                "The Abilities tab key label drifted from the real binding.");
        }

        [Test]
        public void Label_ForAnUnknownSlot_IsEmpty()
        {
            Assert.That(TownAbilitySlotKeyLabels.For((UniversalAbilitySlot)99), Is.Empty);
        }
    }
}
#endif
