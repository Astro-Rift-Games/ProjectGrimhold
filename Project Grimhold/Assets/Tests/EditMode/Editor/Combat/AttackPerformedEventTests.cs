#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Combat
{
    public sealed class AttackPerformedEventTests
    {
        [Test]
        public void ConfirmedPlayerAttack_PreservesWeaponIdentityAndNormalizesDirection()
        {
            var attack = new AttackPerformedEvent(
                new EntityId(7), AttackType.Melee, Vector2.zero,
                new Vector2(3f, 0f), 42, 12);

            Assert.That(attack.WeaponCatalogIndexPlusOne, Is.EqualTo(12));
            Assert.That(attack.Direction, Is.EqualTo(Vector2.right));
            Assert.That(attack.SimulationTick, Is.EqualTo(42));
        }

        [Test]
        public void ConfirmedWeaponWithoutAudioConfig_UsesConfiguredFallback()
        {
            LootDefinitionCatalog source = AssetDatabase.LoadAssetAtPath<LootDefinitionCatalog>(
                "Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset");
            Assert.That(source.TryGet("arming_sword", out LootDefinition original), Is.True);
            WeaponDefinition weapon = Object.Instantiate(original.WeaponDefinition);
            LootDefinition loot = Object.Instantiate(original);
            LootDefinitionCatalog catalog = ScriptableObject.CreateInstance<LootDefinitionCatalog>();
            SetField(weapon, "_audioConfig", null);
            SetField(loot, "_weaponDefinition", weapon);
            SetField(catalog, "_definitions", new List<LootDefinition> { loot });

            var owner = new GameObject(nameof(ConfirmedWeaponWithoutAudioConfig_UsesConfiguredFallback));
            WeaponAudioConfig fallback = ScriptableObject.CreateInstance<WeaponAudioConfig>();
            try
            {
                PlayerWeaponEquipmentNetworkController equipment =
                    owner.AddComponent<PlayerWeaponEquipmentNetworkController>();
                SetField(equipment, "_lootCatalog", catalog);
                WeaponAudioPresenter presenter = owner.AddComponent<WeaponAudioPresenter>();
                SetField(presenter, "_equipmentSource", equipment);
                SetField(presenter, "_fallbackAudioConfig", fallback);
                MethodInfo resolve = typeof(WeaponAudioPresenter).GetMethod("ResolveAudioConfig",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(catalog.TryGetIndex(original.LootId, out int index), Is.True);
                Assert.That(resolve.Invoke(presenter, new object[] { index + 1 }), Is.SameAs(fallback));
            }
            finally
            {
                Object.DestroyImmediate(owner);
                Object.DestroyImmediate(fallback);
                Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(loot);
                Object.DestroyImmediate(weapon);
            }
        }

        private static void SetField(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);

        [Test]
        public void EnemyAttackWithoutCatalogIdentity_RemainsCompatible()
        {
            var attack = new AttackPerformedEvent(
                new EntityId(8), AttackType.Ranged, Vector2.zero, Vector2.up, 43);

            Assert.That(attack.WeaponCatalogIndexPlusOne, Is.Zero);
        }
    }
}
#endif
