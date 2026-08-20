#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEditor;

namespace Tests.EditMode.Equipment
{
    /// <summary>
    /// Guards the placeholder armor content used to exercise Equipment manually. These four assets
    /// reuse the character body-part sprites and carry no stats; they exist so every armor slot can
    /// be filled in a Raid until real content is authored.
    /// </summary>
    public sealed class PlaceholderArmorCatalogTests
    {
        private const string DefinitionFolder = "Assets/Scriptable Objects/Loot/Definitions/";
        private const string CatalogPath =
            "Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset";

        [TestCase("PlaceholderHelmet", "placeholder_helmet", LootCategory.Helmet, EquipmentSlot.Helmet)]
        [TestCase("PlaceholderArmor", "placeholder_armor", LootCategory.Armor, EquipmentSlot.Armor)]
        [TestCase("PlaceholderGloves", "placeholder_gloves", LootCategory.Gloves, EquipmentSlot.Gloves)]
        [TestCase("PlaceholderBoots", "placeholder_boots", LootCategory.Boots, EquipmentSlot.Boots)]
        public void PlaceholderPiece_IsValidAndResolvesToItsOwnSlot(
            string assetName,
            string expectedId,
            LootCategory expectedCategory,
            EquipmentSlot expectedSlot)
        {
            LootDefinition definition =
                AssetDatabase.LoadAssetAtPath<LootDefinition>($"{DefinitionFolder}{assetName}.asset");

            Assert.That(definition, Is.Not.Null, assetName);
            Assert.That(definition.TryValidate(out string error), Is.True, error);
            Assert.That(definition.Id, Is.EqualTo(expectedId));
            Assert.That(definition.Category, Is.EqualTo(expectedCategory));
            Assert.That(definition.WorldSprite, Is.Not.Null, "The piece must be renderable as loot.");
            Assert.That(
                definition.WeaponDefinition,
                Is.Null,
                "Armor must never carry a WeaponDefinition; it would reach the combat strategies.");

            Assert.That(EquipmentSlotRules.ResolveFixedSlot(definition.Category), Is.EqualTo(expectedSlot));
            Assert.That(EquipmentSlotRules.IsCompatible(definition.Category, expectedSlot), Is.True);
        }

        [Test]
        public void Catalog_ExposesEveryPlaceholderPieceWithADeterministicIndex()
        {
            LootDefinitionCatalog catalog =
                AssetDatabase.LoadAssetAtPath<LootDefinitionCatalog>(CatalogPath);

            Assert.That(catalog, Is.Not.Null, CatalogPath);
            Assert.That(catalog.TryValidate(out string error), Is.True, error);

            foreach (string id in new[]
                     {
                         "placeholder_helmet", "placeholder_armor",
                         "placeholder_gloves", "placeholder_boots"
                     })
            {
                Assert.That(catalog.TryGet(id, out LootDefinition definition), Is.True, id);
                Assert.That(catalog.TryGetIndex(definition.LootId, out int index), Is.True, id);
                Assert.That(index, Is.InRange(0, catalog.DefinitionCount - 1));
            }
        }

        [Test]
        public void Catalog_CoversAllSixEquipmentSlotsWithRealContent()
        {
            LootDefinitionCatalog catalog =
                AssetDatabase.LoadAssetAtPath<LootDefinitionCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null);

            var covered = new System.Collections.Generic.HashSet<EquipmentSlot>();
            for (int index = 0; index < catalog.DefinitionCount; index++)
            {
                if (!catalog.TryGetByIndex(index, out LootDefinition definition) ||
                    !EquipmentSlotRules.IsEquippableCategory(definition.Category))
                {
                    continue;
                }

                if (definition.Category == LootCategory.Weapon)
                {
                    covered.Add(EquipmentSlot.WeaponSlot1);
                    covered.Add(EquipmentSlot.WeaponSlot2);
                    continue;
                }

                covered.Add(EquipmentSlotRules.ResolveFixedSlot(definition.Category));
            }

            Assert.That(
                covered,
                Is.EquivalentTo(PlayerWeaponEquipmentNetworkController.AllSlots),
                "Every Equipment slot needs at least one catalog definition to be testable in a Raid.");
        }
    }
}
#endif
