#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Assert = NUnit.Framework.Assert;

namespace Tests.EditMode.Player.Presentation
{
    /// <summary>
    /// Player melee damage resolves on the weapon's attack release. The release is calibrated to the moment
    /// the attack VFX starts (plus its authored lead), so animation, VFX and damage share one clock.
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
