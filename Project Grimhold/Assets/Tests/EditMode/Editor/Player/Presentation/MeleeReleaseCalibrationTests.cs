#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Assert = NUnit.Framework.Assert;

namespace Tests.EditMode.Player.Presentation
{
    /// <summary>
    /// Player melee damage resolves at each weapon's measured visual contact, not at VFX ignition.
    /// VFX start = release - lead preserves the authored VFX clip moment on the shared release clock.
    /// </summary>
    public sealed class MeleeReleaseCalibrationTests
    {
        private const float Tolerance = 0.00001f;

        private static IEnumerable<string> MeleeWeaponPaths()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:WeaponDefinition"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(path);
                if (weapon != null && weapon.PrimaryAttack is MeleeAttackConfig)
                {
                    yield return path;
                }
            }
        }

        [Test]
        public void MeleeWeapons_AreDiscovered()
        {
            Assert.That(new List<string>(MeleeWeaponPaths()), Is.Not.Empty);
        }

        [TestCase("Assets/Scriptable Objects/Loot/Definitions/ArmingSwordWeaponDefinition.asset", 0.265f)]
        [TestCase("Assets/Scriptable Objects/Loot/Definitions/MagicSwordWeaponDefinition.asset", 0.48f)]
        [TestCase("Assets/Scriptable Objects/Loot/Definitions/LongSwordCombatDefinition.asset", 0.35f)]
        [TestCase("Assets/Scriptable Objects/Loot/Definitions/ZweihanderWeaponDefinition.asset", 0.50f)]
        [TestCase("Assets/Scriptable Objects/Loot/Definitions/GreatHammerWeaponDefinition.asset", 0.55f)]
        [TestCase("Assets/Scriptable Objects/Loot/Definitions/RapierWeaponDefinition.asset", 0.35f)]
        [TestCase("Assets/Scriptable Objects/Loot/Definitions/RondelDaggerWeaponDefinition.asset", 0.30f)]
        [TestCase("Assets/Scriptable Objects/Loot/Definitions/MagicCinquedeaWeaponDefinition.asset", 0.30f)]
        [TestCase("Assets/Scriptable Objects/Loot/Definitions/SpellbookWeaponDefinition.asset", 0.65f)]
        public void MeleeWeapon_ReleaseIsItsVisualContact(string path, float contactSeconds)
        {
            var weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(path);
            Assert.That(weapon, Is.Not.Null, path);
            AttackVfxDefinition vfx = weapon.Presentation.AttackVfx;
            Assert.That(vfx, Is.Not.Null, path);
            Assert.That(weapon.AttackReleaseSeconds, Is.EqualTo(contactSeconds).Within(Tolerance),
                $"{weapon.name} damage must resolve at its own visual contact.");
            Assert.That(weapon.AttackReleaseSeconds, Is.GreaterThan(vfx.StartSeconds),
                $"{weapon.name} must not apply damage at VFX ignition during anticipation.");
        }

        [TestCaseSource(nameof(MeleeWeaponPaths))]
        public void MeleeWeapon_ReleaseMatchesVfxStartPlusLeadAndEndsInsideEveryClip(string path)
        {
            var weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(path);
            Assert.That(weapon.TryValidate(out string error), Is.True, error);

            AttackVfxDefinition vfx = weapon.Presentation.AttackVfx;
            Assert.That(vfx, Is.Not.Null, $"{weapon.name} has no attack VFX to calibrate its release against.");
            Assert.That(weapon.AttackReleaseSeconds,
                Is.EqualTo(vfx.StartSeconds + vfx.ReleaseLeadSeconds).Within(Tolerance),
                $"{weapon.name} release must equal VFX start plus lead so the VFX moment is unchanged.");

            for (int direction = 0; direction < 6; direction++)
            {
                AnimationClip clip = weapon.Presentation.GetAttackClip(direction);
                Assert.That(clip, Is.Not.Null, $"{weapon.name} direction {direction}");
                Assert.That(weapon.AttackReleaseSeconds, Is.LessThan(clip.length),
                    $"{weapon.name} release must happen inside {clip.name}.");
            }
        }
    }
}
#endif
