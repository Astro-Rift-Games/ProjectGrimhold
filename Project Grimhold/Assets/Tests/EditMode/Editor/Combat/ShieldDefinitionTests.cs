#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Combat
{
    public sealed class ShieldDefinitionTests
    {
        private const string ShieldLootPath =
            "Assets/Scriptable Objects/Loot/Definitions/Shield.asset";
        private const string LootContentTablePath =
            "Assets/Scriptable Objects/Loot/Tables/DefaultLootContainerContentTable.asset";

        [Test]
        public void Shield_HasValidatedTaskConfiguration()
        {
            LootDefinition loot = AssetDatabase.LoadAssetAtPath<LootDefinition>(ShieldLootPath);

            Assert.That(loot, Is.Not.Null);
            Assert.That(loot.Category, Is.EqualTo(LootCategory.Shield));
            Assert.That(loot.WeaponDefinition, Is.Null);
            Assert.That(loot.ShieldDefinition, Is.Not.Null);
            Assert.That(loot.TryValidate(out string error), Is.True, error);
            Assert.That(loot.ShieldDefinition.DamageReduction, Is.EqualTo(0.5f));
            Assert.That(loot.ShieldDefinition.DefensiveConeDegrees, Is.EqualTo(120f));
        }

        [Test]
        public void Shield_IsObtainableFromRaidLootContainers()
        {
            LootDefinition shield = AssetDatabase.LoadAssetAtPath<LootDefinition>(ShieldLootPath);
            LootContainerContentTable table =
                AssetDatabase.LoadAssetAtPath<LootContainerContentTable>(LootContentTablePath);

            Assert.That(shield, Is.Not.Null);
            Assert.That(table, Is.Not.Null);

            SerializedProperty entries = new SerializedObject(table).FindProperty("_entries");
            for (int index = 0; index < entries.arraySize; index++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(index);
                if (entry.FindPropertyRelative("_definition").objectReferenceValue != shield)
                {
                    continue;
                }

                Assert.That(entry.FindPropertyRelative("_weight").intValue, Is.EqualTo(6));
                Assert.That(entry.FindPropertyRelative("_minimumAmount").intValue, Is.EqualTo(1));
                Assert.That(entry.FindPropertyRelative("_maximumAmount").intValue, Is.EqualTo(1));
                return;
            }

            Assert.Fail("Shield is not present in the Raid loot container table.");
        }

        [TestCase(0f, 120f)]
        [TestCase(1f, 120f)]
        [TestCase(float.NaN, 120f)]
        [TestCase(0.5f, 0f)]
        [TestCase(0.5f, 361f)]
        [TestCase(0.5f, float.PositiveInfinity)]
        public void InvalidConfiguration_IsRejected(float reduction, float cone)
        {
            ShieldDefinition definition = ScriptableObject.CreateInstance<ShieldDefinition>();
            try
            {
                SetPrivateField(definition, "_damageReduction", reduction);
                SetPrivateField(definition, "_defensiveConeDegrees", cone);

                Assert.That(definition.TryValidate(out _), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(definition);
            }
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }
    }
}
#endif
