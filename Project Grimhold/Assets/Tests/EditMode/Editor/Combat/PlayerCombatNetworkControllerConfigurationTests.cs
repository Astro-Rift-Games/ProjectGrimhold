#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Assert = NUnit.Framework.Assert;

namespace Tests.EditMode.Combat
{
    public sealed class PlayerCombatNetworkControllerConfigurationTests
    {
        private const string BasePrefabPath = "Assets/Prefabs/NetworkPlayer.prefab";
        private const string MeleePrefabPath = "Assets/Prefabs/NetworkPlayerMelee.prefab";
        private const string RangedPrefabPath = "Assets/Prefabs/NetworkPlayerRanged.prefab";
        private const string ControllerSourcePath =
            "Assets/Scripts/Player/Combat/PlayerCombatNetworkController.cs";

        [Test]
        public void BasePrefab_IsNeutralAndContainsBothDynamicallyConfigurableStrategies()
        {
            GameObject prefab = LoadPrefab(BasePrefabPath);
            PlayerCombatNetworkController controller =
                prefab.GetComponent<PlayerCombatNetworkController>();

            Assert.That(controller, Is.Not.Null);
            AssertStructuralReferences(controller, BasePrefabPath);
            AssertNoMissingScripts(prefab, BasePrefabPath);
            Assert.That(ReadAttackSource(controller), Is.Null);
            Assert.That(prefab.GetComponents<MeleeAttack>(), Has.Length.EqualTo(1));
            Assert.That(prefab.GetComponents<RangedAttack>(), Has.Length.EqualTo(1));
            Assert.That(prefab.GetComponents<FusionProjectileSpawner>(), Has.Length.EqualTo(1));

            PlayerWeaponEquipmentNetworkController equipment =
                prefab.GetComponent<PlayerWeaponEquipmentNetworkController>();
            Assert.That(equipment, Is.Not.Null);
            AssertEquipmentReferences(equipment, prefab);
        }

        [TestCase(MeleePrefabPath, typeof(MeleeAttack))]
        [TestCase(RangedPrefabPath, typeof(RangedAttack))]
        public void PlayerVariant_PreservesItsSerializedAttackStrategy(
            string prefabPath,
            System.Type expectedAttackType)
        {
            GameObject prefab = LoadPrefab(prefabPath);
            PlayerCombatNetworkController controller =
                prefab.GetComponent<PlayerCombatNetworkController>();

            Assert.That(controller, Is.Not.Null, prefabPath);
            AssertStructuralReferences(controller, prefabPath);
            AssertNoMissingScripts(prefab, prefabPath);

            Object attackSource = ReadAttackSource(controller);
            Assert.That(attackSource, Is.Not.Null, prefabPath);
            Assert.That(attackSource.GetType(), Is.EqualTo(expectedAttackType), prefabPath);
            Assert.That(attackSource, Is.SameAs(prefab.GetComponent(expectedAttackType)), prefabPath);
        }

        [Test]
        public void FreshAuthoritativeInitialization_IsGuardedFromHostMigrationRestore()
        {
            string source = File.ReadAllText(ControllerSourcePath);
            Match match = Regex.Match(
                source,
                @"if\s*\(HasStateAuthority\s*&&\s*!HostMigrationRestoreUtility\.IsRestoreSpawn\(this\)\)\s*\{(?<body>.*?)\n\s*\}",
                RegexOptions.Singleline);

            Assert.That(match.Success, Is.True);
            string body = match.Groups["body"].Value;
            Assert.That(body, Does.Contain("HasActiveAttack = _activeAttack != null;"));
            Assert.That(body, Does.Contain("AttackCooldown = TickTimer.None;"));
            Assert.That(body, Does.Contain("AttackCooldownDurationSeconds = 0f;"));
            Assert.That(body, Does.Contain("IsAttackEnabled ="));
        }

        private static GameObject LoadPrefab(string prefabPath)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefab, Is.Not.Null, prefabPath);
            return prefab;
        }

        private static Object ReadAttackSource(PlayerCombatNetworkController controller)
        {
            var serializedController = new SerializedObject(controller);
            SerializedProperty property = serializedController.FindProperty("_activeAttackSource");
            Assert.That(property, Is.Not.Null);
            return property.objectReferenceValue;
        }

        private static void AssertStructuralReferences(
            PlayerCombatNetworkController controller,
            string prefabPath)
        {
            var serializedController = new SerializedObject(controller);
            Assert.That(
                serializedController.FindProperty("_characterSource").objectReferenceValue,
                Is.Not.Null,
                prefabPath);
            Assert.That(
                serializedController.FindProperty("_attackOrigin").objectReferenceValue,
                Is.Not.Null,
                prefabPath);
            Assert.That(
                serializedController.FindProperty("_movementController").objectReferenceValue,
                Is.Not.Null,
                prefabPath);
        }

        private static void AssertNoMissingScripts(GameObject prefab, string prefabPath)
        {
            MonoBehaviour[] behaviours = prefab.GetComponentsInChildren<MonoBehaviour>(true);
            for (int index = 0; index < behaviours.Length; index++)
            {
                Assert.That(behaviours[index], Is.Not.Null, $"{prefabPath} contains a Missing Script.");
            }
        }

        private static void AssertEquipmentReferences(
            PlayerWeaponEquipmentNetworkController equipment,
            GameObject prefab)
        {
            var serializedEquipment = new SerializedObject(equipment);
            Assert.That(serializedEquipment.FindProperty("_lootCatalog").objectReferenceValue, Is.Not.Null);
            Assert.That(serializedEquipment.FindProperty("_lootReceiver").objectReferenceValue,
                Is.SameAs(prefab.GetComponent<PlayerLootReceiver>()));
            Assert.That(serializedEquipment.FindProperty("_combatController").objectReferenceValue,
                Is.SameAs(prefab.GetComponent<PlayerCombatNetworkController>()));
            Assert.That(serializedEquipment.FindProperty("_meleeAttack").objectReferenceValue,
                Is.SameAs(prefab.GetComponent<MeleeAttack>()));
            Assert.That(serializedEquipment.FindProperty("_rangedAttack").objectReferenceValue,
                Is.SameAs(prefab.GetComponent<RangedAttack>()));
            Assert.That(serializedEquipment.FindProperty("_projectileSpawner").objectReferenceValue,
                Is.SameAs(prefab.GetComponent<FusionProjectileSpawner>()));
        }

    }
}
#endif
