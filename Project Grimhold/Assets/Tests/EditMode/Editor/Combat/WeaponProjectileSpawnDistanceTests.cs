using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Combat
{
    public sealed class WeaponProjectileSpawnDistanceTests
    {
        private const string DefinitionsFolder = "Assets/Scriptable Objects/Loot/Definitions/";
        private const string OverrideProperty = "_overrideProjectileSpawnDistance";
        private const string DistanceProperty = "_projectileSpawnDistance";

        private WeaponDefinition _weapon;

        [TearDown]
        public void TearDown()
        {
            if (_weapon != null)
            {
                Object.DestroyImmediate(_weapon);
                _weapon = null;
            }
        }

        [Test]
        public void ExistingWeapons_DoNotOverrideTheConfigSpawnOffset()
        {
            WeaponDefinition spellbook = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(
                DefinitionsFolder + "SpellbookWeaponDefinition.asset");

            Assert.That(spellbook.HasProjectileSpawnDistance, Is.False);
        }

        [Test]
        public void Override_OnRangedWeapon_IsValidAndExposed()
        {
            _weapon = Load("LongBow");

            Set(_weapon, true, 0.9f);

            Assert.That(_weapon.TryValidate(out string error), Is.True, error);
            Assert.That(_weapon.HasProjectileSpawnDistance, Is.True);
            Assert.That(_weapon.ProjectileSpawnDistance, Is.EqualTo(0.9f));
        }

        [Test]
        public void ZeroDistance_IsValidWhenOverridden()
        {
            _weapon = Load("LongBow");

            Set(_weapon, true, 0f);

            Assert.That(_weapon.TryValidate(out string error), Is.True, error);
        }

        [TestCase(-0.5f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidDistance_IsRejected(float distance)
        {
            _weapon = Load("LongBow");

            Set(_weapon, true, distance);

            Assert.That(_weapon.TryValidate(out string error), Is.False);
            Assert.That(error, Does.Contain("spawn distance"));
        }

        [Test]
        public void Override_OnMeleeWeapon_IsRejected()
        {
            _weapon = Load("ArmingSword");
            Assert.That(_weapon.PrimaryAttack, Is.InstanceOf<MeleeAttackConfig>());

            Set(_weapon, true, 0.5f);

            Assert.That(_weapon.TryValidate(out string error), Is.False);
            Assert.That(error, Does.Contain("spawn distance"));
        }

        private static WeaponDefinition Load(string name)
        {
            WeaponDefinition asset = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(
                $"{DefinitionsFolder}{name}WeaponDefinition.asset");
            Assert.That(asset, Is.Not.Null, name);
            return Object.Instantiate(asset);
        }

        private static void Set(WeaponDefinition weapon, bool overrideDistance, float distance)
        {
            var serialized = new SerializedObject(weapon);
            serialized.FindProperty(OverrideProperty).boolValue = overrideDistance;
            serialized.FindProperty(DistanceProperty).floatValue = distance;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
