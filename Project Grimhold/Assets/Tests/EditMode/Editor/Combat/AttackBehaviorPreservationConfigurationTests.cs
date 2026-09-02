#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Combat
{
    public sealed class AttackBehaviorPreservationConfigurationTests
    {
        [TestCase("Assets/Prefabs/Enemies/Slimes/RedSlime.prefab")]
        [TestCase("Assets/Prefabs/Enemies/Slimes/GreenSlime.prefab")]
        [TestCase("Assets/Prefabs/Enemies/Slimes/BlueSlime.prefab")]
        [TestCase("Assets/Prefabs/NetworkPlayerMelee.prefab")]
        public void LegacyMeleeConsumers_PreserveEffectiveParameters(string prefabPath)
        {
            GameObject prefab = LoadPrefab(prefabPath);
            MeleeAttack attack = prefab.GetComponent<MeleeAttack>();
            Assert.That(attack, Is.Not.Null, prefabPath);

            AssertParameters(attack, 10f, DamageType.Physical, 0.5f, 1.5f, 7f, prefabPath);
            var serializedAttack = new SerializedObject(attack);
            var config = serializedAttack.FindProperty("_config").objectReferenceValue as MeleeAttackConfig;
            Assert.That(config, Is.Not.Null, prefabPath);
            Assert.That(config.Radius, Is.EqualTo(0.5f), prefabPath);
        }

        [TestCase("Assets/Prefabs/Enemies/NetworkEnemyRanged.prefab")]
        [TestCase("Assets/Prefabs/NetworkPlayerRanged.prefab")]
        public void LegacyRangedConsumers_PreserveEffectiveParameters(string prefabPath)
        {
            GameObject prefab = LoadPrefab(prefabPath);
            RangedAttack attack = prefab.GetComponent<RangedAttack>();
            Assert.That(attack, Is.Not.Null, prefabPath);

            AssertParameters(attack, 10f, DamageType.Physical, 0.5f, 10f, 6f, prefabPath);
        }

        [Test]
        public void DartTrap_PreservesEffectiveDamageTypeRangeAndZeroKnockback()
        {
            const string prefabPath = "Assets/Prefabs/Traps/DartTrap.prefab";
            GameObject prefab = LoadPrefab(prefabPath);
            DartTrap trap = prefab.GetComponent<DartTrap>();
            Assert.That(trap, Is.Not.Null, prefabPath);

            var serializedTrap = new SerializedObject(trap);
            var info = serializedTrap.FindProperty("trapInfo").objectReferenceValue as TrapInfo;
            Assert.That(info, Is.Not.Null, prefabPath);
            Assert.That(info.damage, Is.EqualTo(5f));
            Assert.That(info.DamageType, Is.EqualTo(DamageType.Physical));
            Assert.That(serializedTrap.FindProperty("_maximumRange").floatValue, Is.EqualTo(5f));

            string source = System.IO.File.ReadAllText("Assets/Scripts/Scenario/Traps/DartTrap.cs");
            Assert.That(source, Does.Not.Contain("KnockbackForce"));
        }

        private static GameObject LoadPrefab(string prefabPath)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefab, Is.Not.Null, prefabPath);
            return prefab;
        }

        private static void AssertParameters(
            MonoBehaviour attack,
            float damage,
            DamageType damageType,
            float cooldown,
            float range,
            float knockback,
            string context)
        {
            SerializedProperty parameters =
                new SerializedObject(attack).FindProperty("_defaultParameters");
            Assert.That(parameters.FindPropertyRelative("_damage").floatValue, Is.EqualTo(damage), context);
            Assert.That(parameters.FindPropertyRelative("_damageType").enumValueIndex, Is.EqualTo((int)damageType), context);
            Assert.That(parameters.FindPropertyRelative("_cooldownSeconds").floatValue, Is.EqualTo(cooldown), context);
            Assert.That(parameters.FindPropertyRelative("_range").floatValue, Is.EqualTo(range), context);
            Assert.That(parameters.FindPropertyRelative("_knockbackForce").floatValue, Is.EqualTo(knockback), context);
        }
    }
}
#endif
