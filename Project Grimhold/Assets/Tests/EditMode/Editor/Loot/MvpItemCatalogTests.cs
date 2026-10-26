using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Loot
{
    public sealed class MvpItemCatalogTests
    {
        private const string CatalogPath = "Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset";

        private LootDefinitionCatalog _catalog;

        private struct ExpectedItemData
        {
            public string Id;
            public LootCategory Category;
            public int ObjectValue;
            public int StackMax;
            public int MaxTransportable;
            public string Family;
            public bool CanBeUsed;
            public bool QuickSlotCompatible;
            public ItemUseMode UseMode;

            public ExpectedItemData(
                string id,
                LootCategory category,
                int objectValue,
                int stackMax,
                int maxTransportable,
                string family,
                bool canBeUsed = false,
                bool quickSlotCompatible = false,
                ItemUseMode useMode = ItemUseMode.None)
            {
                Id = id;
                Category = category;
                ObjectValue = objectValue;
                StackMax = stackMax;
                MaxTransportable = maxTransportable;
                Family = family;
                CanBeUsed = canBeUsed;
                QuickSlotCompatible = quickSlotCompatible;
                UseMode = useMode;
            }
        }

        private static readonly ExpectedItemData[] ExpectedMvpItems = new[]
        {
            // 21 Recursos
            new ExpectedItemData("RES_SLIME_GEL", LootCategory.Resource, 8, 10, 0, "CreatureRemains"),
            new ExpectedItemData("RES_SLIME_CORE", LootCategory.Resource, 24, 5, 0, "CreatureRemains"),
            new ExpectedItemData("RES_WITHERED_BONE", LootCategory.Resource, 10, 10, 0, "CreatureRemains"),
            new ExpectedItemData("RES_CORRUPT_MARROW", LootCategory.Resource, 28, 5, 0, "CreatureRemains"),
            new ExpectedItemData("RES_ANCIENT_ARMOR", LootCategory.Resource, 30, 5, 0, "CreatureRemains"),
            new ExpectedItemData("RES_GOBLIN_SCRAP", LootCategory.Resource, 10, 10, 0, "CreatureRemains"),
            new ExpectedItemData("RES_GOBLIN_INSIGNIA", LootCategory.Resource, 32, 5, 0, "CreatureRemains"),
            new ExpectedItemData("RES_GOBLIN_ARROWHEAD", LootCategory.Resource, 14, 10, 0, "CreatureRemains"),
            new ExpectedItemData("RES_BAT_WING", LootCategory.Resource, 12, 10, 0, "CreatureRemains"),
            new ExpectedItemData("RES_BAT_FANG", LootCategory.Resource, 22, 5, 0, "CreatureRemains"),

            new ExpectedItemData("RES_SPECTRAL_ESSENCE", LootCategory.Resource, 38, 5, 0, "ArcaneComponents"),
            new ExpectedItemData("RES_SOUL_FRAGMENT", LootCategory.Resource, 65, 3, 0, "ArcaneComponents"),
            new ExpectedItemData("RES_GOLEM_CORE", LootCategory.Resource, 70, 3, 0, "ArcaneComponents"),
            new ExpectedItemData("RES_ETHER_DUST", LootCategory.Resource, 18, 10, 0, "ArcaneComponents"),
            new ExpectedItemData("RES_RUNE_FRAGMENT", LootCategory.Resource, 20, 10, 0, "ArcaneComponents"),

            new ExpectedItemData("RES_BLACK_IRON", LootCategory.Resource, 15, 10, 0, "CommonMaterials"),
            new ExpectedItemData("RES_TANNED_LEATHER", LootCategory.Resource, 10, 10, 0, "CommonMaterials"),
            new ExpectedItemData("RES_ANCIENT_ROOT", LootCategory.Resource, 12, 10, 0, "CommonMaterials"),

            new ExpectedItemData("RES_ATTUNEMENT_GEM", LootCategory.Resource, 70, 3, 0, "Enhancement"),

            new ExpectedItemData("RES_LOST_GRIMOIRE_PAGE", LootCategory.Resource, 35, 5, 0, "NpcCollectibles"),
            new ExpectedItemData("RES_CUSTODIAN_SEAL", LootCategory.Resource, 45, 5, 0, "NpcCollectibles"),

            // 8 Tesoros (no stackables)
            new ExpectedItemData("TRE_CHALICE", LootCategory.Treasure, 200, 1, 0, "ConventionalTreasure"),
            new ExpectedItemData("TRE_CROWN", LootCategory.Treasure, 350, 1, 0, "ConventionalTreasure"),
            new ExpectedItemData("TRE_FAMINE_IDOL", LootCategory.Treasure, 400, 1, 0, "CursedTreasure"),
            new ExpectedItemData("TRE_WANDERER_EYE", LootCategory.Treasure, 450, 1, 0, "CursedTreasure"),
            new ExpectedItemData("TRE_PILGRIM_LIGHT", LootCategory.Treasure, 300, 1, 0, "DivineTreasure"),
            new ExpectedItemData("TRE_SANCTUARY_HEART", LootCategory.Treasure, 350, 1, 0, "DivineTreasure"),
            new ExpectedItemData("TRE_CHAOS_COIN", LootCategory.Treasure, 400, 1, 0, "ChaosTreasure"),
            new ExpectedItemData("TRE_VOID_BELL", LootCategory.Treasure, 450, 1, 0, "ChaosTreasure"),

            // 8 Consumibles
            new ExpectedItemData("CON_HEALTH_POTION", LootCategory.Consumable, 20, 5, 0, "Potions", true, true, ItemUseMode.Channeled),
            new ExpectedItemData("CON_MANA_POTION", LootCategory.Consumable, 20, 5, 0, "Potions", true, true, ItemUseMode.Channeled),
            new ExpectedItemData("CON_MANA_FRAGMENT", LootCategory.Consumable, 8, 10, 0, "Potions", true, true, ItemUseMode.Instant),
            new ExpectedItemData("CON_EXPLOSIVE_BOMB", LootCategory.Consumable, 30, 5, 0, "Offensive", true, true, ItemUseMode.AimedQuickSlot),
            new ExpectedItemData("CON_THROWING_DAGGER", LootCategory.Consumable, 12, 10, 0, "Offensive", true, true, ItemUseMode.AimedQuickSlot),
            new ExpectedItemData("CON_SMOKE_BOMB", LootCategory.Consumable, 45, 3, 0, "Offensive", true, true, ItemUseMode.Instant),
            new ExpectedItemData("CON_LOCKPICK", LootCategory.Consumable, 25, 5, 0, "UtilityConsumable", true, true, ItemUseMode.Instant),
            new ExpectedItemData("CON_SELF_REVIVE", LootCategory.Consumable, 300, 1, 1, "Revive", true, true, ItemUseMode.Instant),

            // 2 Utilidades
            new ExpectedItemData("UTL_RUNE_COMPASS", LootCategory.Utility, 140, 1, 1, "Utility"),
            new ExpectedItemData("UTL_ARCANE_DETECTOR", LootCategory.Utility, 180, 1, 1, "Utility")
        };

        [SetUp]
        public void SetUp()
        {
            _catalog = AssetDatabase.LoadAssetAtPath<LootDefinitionCatalog>(CatalogPath);
            Assert.That(_catalog, Is.Not.Null, $"Failed to load catalog at '{CatalogPath}'.");
        }

        [Test]
        public void Catalog_PassesValidation()
        {
            bool valid = _catalog.TryValidate(out string error);
            Assert.That(valid, Is.True, $"Catalog validation failed: {error}");
        }

        [Test]
        public void All39MvpItems_AreRegisteredInCatalog()
        {
            Assert.That(ExpectedMvpItems.Length, Is.EqualTo(39), "Expected exactly 39 MVP items.");

            foreach (var expected in ExpectedMvpItems)
            {
                bool found = _catalog.TryGet(expected.Id, out LootDefinition item);
                Assert.That(found, Is.True, $"Item '{expected.Id}' not found in catalog.");
                Assert.That(item, Is.Not.Null, $"Item '{expected.Id}' returned null from catalog.");
            }
        }

        [Test]
        public void All39MvpItems_PassValidationAndHaveSprites()
        {
            foreach (var expected in ExpectedMvpItems)
            {
                Assert.That(_catalog.TryGet(expected.Id, out LootDefinition item), Is.True, expected.Id);
                Assert.That(item.TryValidate(out string error), Is.True, $"{expected.Id} validation failed: {error}");
                Assert.That(item.Icon, Is.Not.Null, $"{expected.Id} Icon is null.");
                Assert.That(item.WorldSprite, Is.Not.Null, $"{expected.Id} WorldSprite is null.");
            }
        }

        [Test]
        public void All39MvpItems_MatchDoc14MasterTableSpecifications()
        {
            foreach (var expected in ExpectedMvpItems)
            {
                Assert.That(_catalog.TryGet(expected.Id, out LootDefinition item), Is.True, expected.Id);

                Assert.That(item.Category, Is.EqualTo(expected.Category), $"{expected.Id} category mismatch.");
                Assert.That(item.ObjectValue, Is.EqualTo(expected.ObjectValue), $"{expected.Id} ObjectValue mismatch.");
                Assert.That(item.StackMax, Is.EqualTo(expected.StackMax), $"{expected.Id} StackMax mismatch.");
                Assert.That(item.MaxTransportable, Is.EqualTo(expected.MaxTransportable), $"{expected.Id} MaxTransportable mismatch.");
                Assert.That(item.Family, Is.EqualTo(expected.Family), $"{expected.Id} Family mismatch.");
                Assert.That(item.CanBeUsed, Is.EqualTo(expected.CanBeUsed), $"{expected.Id} CanBeUsed mismatch.");
                Assert.That(item.QuickSlotCompatible, Is.EqualTo(expected.QuickSlotCompatible), $"{expected.Id} QuickSlotCompatible mismatch.");
                Assert.That(item.UseMode, Is.EqualTo(expected.UseMode), $"{expected.Id} UseMode mismatch.");

                // Operational integrity
                Assert.That(item.Tradable, Is.True, $"{expected.Id} should be tradable.");
                Assert.That(item.CanBeTransferred, Is.True, $"{expected.Id} should be transferable.");
                Assert.That(item.CanEnterDungeon, Is.True, $"{expected.Id} should enter dungeon.");
                Assert.That(item.CanBeFoundInDungeon, Is.True, $"{expected.Id} should be found in dungeon.");
                Assert.That(item.CanBeDiscarded, Is.True, $"{expected.Id} should be discardable.");
            }
        }

        [Test]
        public void Treasures_AreStrictlyNonStackable()
        {
            foreach (var expected in ExpectedMvpItems)
            {
                if (expected.Category == LootCategory.Treasure)
                {
                    Assert.That(_catalog.TryGet(expected.Id, out LootDefinition item), Is.True, expected.Id);
                    Assert.That(item.StackMax, Is.EqualTo(1), $"Treasure '{expected.Id}' must have StackMax = 1.");
                }
            }
        }

        [Test]
        public void HealthPotion_HasNormalizedConfiguration()
        {
            Assert.That(_catalog.TryGet("CON_HEALTH_POTION", out LootDefinition healthPotion), Is.True);
            Assert.That(healthPotion.ObjectValue, Is.EqualTo(20));
            Assert.That(healthPotion.StackMax, Is.EqualTo(5));
            Assert.That(healthPotion.UseMode, Is.EqualTo(ItemUseMode.Channeled));
            Assert.That(healthPotion.ConsumableDefinition, Is.Not.Null);
            Assert.That(healthPotion.ConsumableDefinition.ConsumptionDurationSeconds, Is.EqualTo(2.5f));
        }
    }
}
