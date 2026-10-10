#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEditor;
using UnityEngine.InputSystem;

namespace Tests.EditMode.Presentation
{
    public sealed class RaidWeaponSlotKeyLabelTests
    {
        private const string InputActionsPath = "Assets/Input/PlayerInputActions.inputactions";

        [Test]
        public void Label_MatchesTheRealPrimaryAttackBinding()
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            Assert.That(asset, Is.Not.Null, "The player Input Actions asset must exist.");
            InputAction action = asset.FindAction("Gameplay/PrimaryAttack");
            Assert.That(action, Is.Not.Null, "Gameplay/PrimaryAttack must exist in the Input Actions asset.");

            string path = null;
            foreach (InputBinding binding in action.bindings)
            {
                if (!string.IsNullOrEmpty(binding.path) && !binding.isComposite && !binding.isPartOfComposite)
                {
                    path = binding.path;
                    break;
                }
            }

            Assert.That(path, Is.Not.Null, "Gameplay/PrimaryAttack must have a binding.");
            Assert.That(RaidWeaponSlotKeyLabel.Label, Is.Not.Empty);
            Assert.That(
                RaidWeaponSlotKeyLabel.FromBindingPath(path),
                Is.EqualTo(RaidWeaponSlotKeyLabel.Label),
                "The weapon slot key label drifted from the real primary attack binding.");
        }

        [TestCase("<Mouse>/leftButton", "LMB")]
        [TestCase("<Mouse>/rightButton", "RMB")]
        [TestCase("<Mouse>/middleButton", "MMB")]
        [TestCase("<Keyboard>/q", "Q")]
        [TestCase("<Keyboard>/space", "SPACE")]
        public void FromBindingPath_UsesShortLabels(string path, string expected)
        {
            Assert.That(RaidWeaponSlotKeyLabel.FromBindingPath(path), Is.EqualTo(expected));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("<Gamepad>/buttonSouth")]
        public void FromBindingPath_UnsupportedPath_ReturnsEmpty(string path)
        {
            Assert.That(RaidWeaponSlotKeyLabel.FromBindingPath(path), Is.Empty);
        }
    }
}
#endif
