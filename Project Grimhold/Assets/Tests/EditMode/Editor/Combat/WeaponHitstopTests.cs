#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEditor;
using Assert = NUnit.Framework.Assert;

namespace Tests.EditMode.Combat
{
    public sealed class WeaponHitstopTests
    {
        [TestCase("RondelDaggerWeaponDefinition", .04f)]
        [TestCase("MagicCinquedeaWeaponDefinition", .04f)]
        [TestCase("RapierWeaponDefinition", .05f)]
        [TestCase("SpellbookWeaponDefinition", .05f)]
        [TestCase("ArmingSwordWeaponDefinition", .06f)]
        [TestCase("MagicSwordWeaponDefinition", .06f)]
        [TestCase("LongSwordCombatDefinition", .08f)]
        [TestCase("ZweihanderWeaponDefinition", .10f)]
        [TestCase("GreatHammerWeaponDefinition", .12f)]
        public void InitialWeaponWeight_UsesConfiguredDuration(string name, float expected)
        {
            var weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(
                $"Assets/Scriptable Objects/Loot/Definitions/{name}.asset");
            Assert.That(weapon, Is.Not.Null);
            Assert.That(weapon.Presentation.HitstopSeconds, Is.EqualTo(expected).Within(.00001f));
        }

        [Test]
        public void Catalog_MeleeHasBoundedHitstop_RangedRemainsDisabled()
        {
            int melee = 0, ranged = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:WeaponDefinition"))
            {
                var weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (weapon.PrimaryAttack is MeleeAttackConfig)
                {
                    melee++;
                    Assert.That(weapon.Presentation.HitstopSeconds, Is.InRange(.01f, .2f), weapon.name);
                }
                else if (weapon.PrimaryAttack is RangedAttackConfig)
                {
                    ranged++;
                    Assert.That(weapon.Presentation.HitstopSeconds, Is.Zero, weapon.name);
                }
            }
            Assert.That(melee, Is.GreaterThanOrEqualTo(9));
            Assert.That(ranged, Is.GreaterThan(0));
        }

        [TestCase(-.01f)]
        [TestCase(.21f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void PresentationValidation_RejectsInvalidHitstop(float seconds)
        {
            var weapon = UnityEngine.ScriptableObject.CreateInstance<WeaponDefinition>();
            try
            {
                var serialized = new SerializedObject(weapon);
                serialized.FindProperty("_presentation._hitstopSeconds").floatValue = seconds;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(weapon.Presentation.TryValidate(out string error), Is.False);
                Assert.That(error, Is.EqualTo("hitstop seconds must be finite and between 0 and 0.2."));
            }
            finally { UnityEngine.Object.DestroyImmediate(weapon); }
        }

        [Test]
        public void DefaultPresentation_RemainsValidWithHitstopDisabled()
        {
            WeaponDefinition.PresentationConfig config = default;
            Assert.That(config.HitstopSeconds, Is.Zero);
            Assert.That(config.TryValidate(out string error), Is.True, error);
        }
    }
}
#endif
