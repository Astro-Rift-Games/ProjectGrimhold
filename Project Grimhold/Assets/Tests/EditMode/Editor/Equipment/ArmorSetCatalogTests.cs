#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Equipment
{
    public sealed class ArmorSetCatalogTests
    {
        private const string DefinitionFolder = "Assets/Scriptable Objects/Loot/Definitions/";
        private const string CatalogPath =
            "Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset";
        private const string ProductiveLootTablePath =
            "Assets/Scriptable Objects/Loot/Tables/DefaultLootContainerContentTable.asset";
        private const string CharacterArtFolder = "Assets/Art/Character";

        private static readonly string[] Directions = { "N", "NE", "NW", "S", "SE", "SW" };
        private static readonly MotionSpec[] Motions =
        {
            new("Idle", 6),
            new("Walk", 8)
        };

        private static readonly DefinitionSpec[] Definitions =
        {
            new("ArcaneMageHat", "placeholder_helmet", LootCategory.Helmet, EquipmentSlot.Helmet,
                "ArcaneMage", "Hat", new PartSpec("Head", "Hat")),
            new("ArcaneMageGarb", "placeholder_armor", LootCategory.Armor, EquipmentSlot.Armor,
                "ArcaneMage", "Garb", new PartSpec("Body", "Garb")),
            new("ArcaneMageGloves", "placeholder_gloves", LootCategory.Gloves, EquipmentSlot.Gloves,
                "ArcaneMage", "RightGlove", new PartSpec("LeftHand", "LeftGlove"),
                new PartSpec("RightHand", "RightGlove")),
            new("ArcaneMageTrousers", "placeholder_boots", LootCategory.Boots, EquipmentSlot.Boots,
                "ArcaneMage", "Trousers", new PartSpec("Legs", "Trousers")),
            new("ArmyRangerHat", "army_ranger_hat", LootCategory.Helmet, EquipmentSlot.Helmet,
                "ArmyRanger", "Hat", new PartSpec("Head", "Hat")),
            new("ArmyRangerArmor", "army_ranger_armor", LootCategory.Armor, EquipmentSlot.Armor,
                "ArmyRanger", "Armor", new PartSpec("Body", "Armor")),
            new("ArmyRangerGloves", "army_ranger_gloves", LootCategory.Gloves, EquipmentSlot.Gloves,
                "ArmyRanger", "RightGlove", new PartSpec("LeftHand", "LeftGlove"),
                new PartSpec("RightHand", "RightGlove")),
            new("ArmyRangerTrousers", "army_ranger_trousers", LootCategory.Boots, EquipmentSlot.Boots,
                "ArmyRanger", "Trousers", new PartSpec("Legs", "Trousers")),
            new("HeavyArmorHelmet", "heavy_armor_helmet", LootCategory.Helmet, EquipmentSlot.Helmet,
                "HeavyArmor", "Helmet", new PartSpec("Head", "Helmet")),
            new("HeavyArmorBreastplate", "heavy_armor_breastplate", LootCategory.Armor, EquipmentSlot.Armor,
                "HeavyArmor", "Breastplate", new PartSpec("Body", "Breastplate")),
            new("HeavyArmorGauntlets", "heavy_armor_gauntlets", LootCategory.Gloves, EquipmentSlot.Gloves,
                "HeavyArmor", "RightGauntlet", new PartSpec("LeftHand", "LeftGauntlet"),
                new PartSpec("RightHand", "RightGauntlet")),
            new("HeavyArmorLegPlate", "heavy_armor_leg_plate", LootCategory.Boots, EquipmentSlot.Boots,
                "HeavyArmor", "LegPlate", new PartSpec("Legs", "LegPlate")),
            new("LightArmorOpenSallet", "light_armor_open_sallet", LootCategory.Helmet, EquipmentSlot.Helmet,
                "LightArmor", "OpenSallet", new PartSpec("Head", "OpenSallet")),
            new("LightArmorChainMailArmor", "light_armor_chain_mail_armor", LootCategory.Armor, EquipmentSlot.Armor,
                "LightArmor", "ChainMailArmor", new PartSpec("Body", "ChainMailArmor")),
            new("LightArmorGloves", "light_armor_gloves", LootCategory.Gloves, EquipmentSlot.Gloves,
                "LightArmor", "RightGlove", new PartSpec("LeftHand", "LeftGlove"),
                new PartSpec("RightHand", "RightGlove")),
            new("LightArmorChainMailTrousers", "light_armor_chain_mail_trousers", LootCategory.Boots, EquipmentSlot.Boots,
                "LightArmor", "ChainMailTrousers", new PartSpec("Legs", "ChainMailTrousers")),
            new("ForestRangerHood", "forest_ranger_hood", LootCategory.Helmet, EquipmentSlot.Helmet,
                "ForestRanger", "Hood", new PartSpec("Head", "Hood")),
            new("ForestRangerLeatherArmor", "forest_ranger_leather_armor", LootCategory.Armor, EquipmentSlot.Armor,
                "ForestRanger", "LeatherArmor", new PartSpec("Body", "LeatherArmor")),
            new("ForestRangerGloves", "forest_ranger_gloves", LootCategory.Gloves, EquipmentSlot.Gloves,
                "ForestRanger", "RightGlove", new PartSpec("LeftHand", "LeftGlove"),
                new PartSpec("RightHand", "RightGlove")),
            new("ForestRangerTrousers", "forest_ranger_trousers", LootCategory.Boots, EquipmentSlot.Boots,
                "ForestRanger", "Trousers", new PartSpec("Legs", "Trousers")),
            new("FireMageHood", "fire_mage_hood", LootCategory.Helmet, EquipmentSlot.Helmet,
                "FireMage", "Hood", new PartSpec("Head", "Hood")),
            new("FireMageGarb", "fire_mage_garb", LootCategory.Armor, EquipmentSlot.Armor,
                "FireMage", "Garb", new PartSpec("Body", "Garb")),
            new("FireMageGloves", "fire_mage_gloves", LootCategory.Gloves, EquipmentSlot.Gloves,
                "FireMage", "RightGlove", new PartSpec("LeftHand", "LeftGlove"),
                new PartSpec("RightHand", "RightGlove")),
            new("FireMageTrousers", "fire_mage_trousers", LootCategory.Boots, EquipmentSlot.Boots,
                "FireMage", "Trousers", new PartSpec("Legs", "Trousers"))
        };

        [Test]
        public void Definitions_AreValidAndResolveToTheirExpectedSlots()
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var piecesPerSet = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (DefinitionSpec spec in Definitions)
            {
                LootDefinition definition = LoadDefinition(spec);

                Assert.That(definition.TryValidate(out string error), Is.True, $"{spec.AssetName}: {error}");
                Assert.That(definition.Id, Is.EqualTo(spec.Id), spec.AssetName);
                Assert.That(ids.Add(definition.Id), Is.True, $"Duplicate item ID: {definition.Id}");
                Assert.That(definition.Category, Is.EqualTo(spec.Category), spec.AssetName);
                Assert.That(definition.Icon, Is.Not.Null, spec.AssetName);
                Assert.That(definition.WorldSprite, Is.SameAs(definition.Icon), spec.AssetName);
                Assert.That(definition.WeaponDefinition, Is.Null, spec.AssetName);
                Assert.That(definition.EquipmentVisualDefinition, Is.Not.Null, spec.AssetName);
                Assert.That(definition.EquipmentVisualDefinition.UsesBaseSpritesAsPlaceholder, Is.False,
                    spec.AssetName);
                Assert.That(EquipmentSlotRules.ResolveFixedSlot(definition.Category), Is.EqualTo(spec.Slot),
                    spec.AssetName);

                Sprite expectedIcon = LoadEquipmentSprite(spec, "Idle", "S", spec.IconPart, 0);
                Assert.That(definition.Icon, Is.SameAs(expectedIcon), $"{spec.AssetName} icon");

                piecesPerSet.TryGetValue(spec.SetName, out int setCount);
                piecesPerSet[spec.SetName] = setCount + 1;
            }

            Assert.That(piecesPerSet.Keys,
                Is.EquivalentTo(new[]
                {
                    "ArcaneMage", "ArmyRanger", "HeavyArmor",
                    "LightArmor", "ForestRanger", "FireMage"
                }));
            foreach (int count in piecesPerSet.Values)
            {
                Assert.That(count, Is.EqualTo(4));
            }
        }

        [Test]
        public void Catalog_PreservesLegacyAssetsAndAppendsNewDefinitions()
        {
            LootDefinitionCatalog catalog = LoadCatalog();
            var serializedCatalog = new SerializedObject(catalog);
            SerializedProperty entries = serializedCatalog.FindProperty("_definitions");

            Assert.That(entries, Is.Not.Null);
            Assert.That(entries.arraySize, Is.EqualTo(34));

            AssertCatalogEntry(entries, 10, "ArcaneMageHat", "a1b2c3d400014e7f9a0b0c0d0e0f1001");
            AssertCatalogEntry(entries, 11, "ArcaneMageGarb", "a1b2c3d400024e7f9a0b0c0d0e0f1002");
            AssertCatalogEntry(entries, 12, "ArcaneMageGloves", "a1b2c3d400034e7f9a0b0c0d0e0f1003");
            AssertCatalogEntry(entries, 13, "ArcaneMageTrousers", "a1b2c3d400044e7f9a0b0c0d0e0f1004");

            for (int index = 4; index < Definitions.Length; index++)
            {
                Assert.That(entries.GetArrayElementAtIndex(index + 10).objectReferenceValue,
                    Is.SameAs(LoadDefinition(Definitions[index])), Definitions[index].AssetName);
            }
        }

        [Test]
        public void Catalog_ResolvesEveryDefinitionByStableIdAndDeterministicIndex()
        {
            LootDefinitionCatalog catalog = LoadCatalog();
            Assert.That(catalog.TryValidate(out string error), Is.True, error);

            foreach (DefinitionSpec spec in Definitions)
            {
                Assert.That(catalog.TryGet(spec.Id, out LootDefinition definition), Is.True, spec.Id);
                Assert.That(definition, Is.SameAs(LoadDefinition(spec)), spec.Id);
                Assert.That(catalog.TryGetIndex(definition.LootId, out int index), Is.True, spec.Id);
                Assert.That(catalog.TryGetByIndex(index, out LootDefinition roundTrip), Is.True, spec.Id);
                Assert.That(roundTrip, Is.SameAs(definition), spec.Id);
            }
        }

        [Test]
        public void ProductiveLootTable_ContainsEveryArmorSetDefinition()
        {
            LootContainerContentTable table =
                AssetDatabase.LoadAssetAtPath<LootContainerContentTable>(ProductiveLootTablePath);
            Assert.That(table, Is.Not.Null, ProductiveLootTablePath);

            var configuredDefinitions = new HashSet<LootDefinition>();
            foreach (LootContainerContentTableEntry entry in table.Entries)
            {
                Assert.That(entry.Definition, Is.Not.Null);
                Assert.That(configuredDefinitions.Add(entry.Definition), Is.True,
                    $"Duplicate loot-table definition: {entry.Definition.Id}");
            }

            foreach (DefinitionSpec spec in Definitions)
            {
                LootDefinition definition = LoadDefinition(spec);
                Assert.That(configuredDefinitions, Does.Contain(definition), spec.AssetName);

                LootContainerContentTableEntry entry = FindEntry(table, definition);
                Assert.That(entry.Weight, Is.GreaterThan(0), spec.AssetName);
                Assert.That(entry.MinimumAmount, Is.EqualTo(1), spec.AssetName);
                Assert.That(entry.MaximumAmount, Is.EqualTo(1), spec.AssetName);
            }
        }

        [Test]
        public void VisualMappings_CoverEveryExpectedFrameExactly()
        {
            int totalMappings = 0;
            var mappingsPerSet = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (DefinitionSpec spec in Definitions)
            {
                LootDefinition definition = LoadDefinition(spec);
                Dictionary<Sprite, Sprite> mappings = ReadMappings(definition, spec);
                int expectedCount = 84 * spec.Parts.Length;

                Assert.That(mappings.Count, Is.EqualTo(expectedCount), spec.AssetName);
                AssertExpectedMappings(spec, definition, mappings);

                totalMappings += mappings.Count;
                mappingsPerSet.TryGetValue(spec.SetName, out int setCount);
                mappingsPerSet[spec.SetName] = setCount + mappings.Count;
            }

            Assert.That(totalMappings, Is.EqualTo(2520));
            Assert.That(mappingsPerSet.Count, Is.EqualTo(6));
            foreach (int count in mappingsPerSet.Values)
            {
                Assert.That(count, Is.EqualTo(420));
            }
        }

        [Test]
        public void Catalog_CoversAllSixEquipmentSlotsWithRealContent()
        {
            LootDefinitionCatalog catalog = LoadCatalog();
            var covered = new HashSet<EquipmentSlot>();

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

            Assert.That(covered, Is.EquivalentTo(PlayerWeaponEquipmentNetworkController.AllSlots));
        }

        private static Dictionary<Sprite, Sprite> ReadMappings(LootDefinition definition, DefinitionSpec spec)
        {
            var serializedDefinition = new SerializedObject(definition);
            SerializedProperty visual = serializedDefinition.FindProperty("_equipmentVisualDefinition");
            SerializedProperty entries = visual.FindPropertyRelative("_spriteMappings");
            var mappings = new Dictionary<Sprite, Sprite>();
            var equipmentSprites = new HashSet<Sprite>();

            Assert.That(entries, Is.Not.Null, spec.AssetName);
            for (int index = 0; index < entries.arraySize; index++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(index);
                var baseSprite = entry.FindPropertyRelative("BaseSprite").objectReferenceValue as Sprite;
                var equipmentSprite = entry.FindPropertyRelative("EquipmentSprite").objectReferenceValue as Sprite;

                Assert.That(baseSprite, Is.Not.Null, $"{spec.AssetName} base mapping {index}");
                Assert.That(equipmentSprite, Is.Not.Null, $"{spec.AssetName} equipment mapping {index}");
                Assert.That(mappings.ContainsKey(baseSprite), Is.False,
                    $"{spec.AssetName} duplicates base sprite {baseSprite.name}");
                Assert.That(equipmentSprites.Add(equipmentSprite), Is.True,
                    $"{spec.AssetName} duplicates equipment sprite {equipmentSprite.name}");
                mappings.Add(baseSprite, equipmentSprite);
            }

            return mappings;
        }

        private static void AssertExpectedMappings(
            DefinitionSpec spec,
            LootDefinition definition,
            IReadOnlyDictionary<Sprite, Sprite> mappings)
        {
            foreach (PartSpec part in spec.Parts)
            {
                foreach (MotionSpec motion in Motions)
                {
                    foreach (string direction in Directions)
                    {
                        for (int frame = 0; frame < motion.FrameCount; frame++)
                        {
                            Sprite baseSprite = LoadBaseSprite(part.BasePart, motion.Name, direction, frame);
                            Sprite expected = LoadEquipmentSprite(
                                spec, motion.Name, direction, part.EquipmentPart, frame);

                            Assert.That(mappings.TryGetValue(baseSprite, out Sprite mapped), Is.True,
                                $"{spec.AssetName}: {part.BasePart} {motion.Name}-{direction} frame {frame}");
                            Assert.That(mapped, Is.SameAs(expected),
                                $"{spec.AssetName}: {part.BasePart} {motion.Name}-{direction} frame {frame}");
                            Assert.That(definition.EquipmentVisualDefinition.ResolveSprite(baseSprite),
                                Is.SameAs(expected),
                                $"{spec.AssetName}: resolver {part.BasePart} {motion.Name}-{direction} frame {frame}");
                        }
                    }
                }
            }
        }

        private static Sprite LoadBaseSprite(string part, string motion, string direction, int frame)
        {
            string stem = $"Player-{part}-{motion}-{direction}-Sheet";
            string path = $"{CharacterArtFolder}/{part}/{motion}/{stem}.png";
            return LoadSprite(path, $"{stem}_{frame}");
        }

        private static Sprite LoadEquipmentSprite(
            DefinitionSpec spec,
            string motion,
            string direction,
            string part,
            int frame)
        {
            string stem = $"Set-{spec.SetName}-{part}-{motion}-{direction}-Sheet";
            string path = $"{CharacterArtFolder}/Sets/Sets/{spec.SetName}/{motion}/{stem}.png";
            return LoadSprite(path, $"{stem}_{frame}");
        }

        private static Sprite LoadSprite(string assetPath, string spriteName)
        {
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath))
            {
                if (asset is Sprite sprite && sprite.name == spriteName)
                {
                    return sprite;
                }
            }

            Assert.Fail($"Sprite '{spriteName}' was not found at '{assetPath}'.");
            return null;
        }

        private static LootDefinition LoadDefinition(DefinitionSpec spec)
        {
            LootDefinition definition = AssetDatabase.LoadAssetAtPath<LootDefinition>(
                $"{DefinitionFolder}{spec.AssetName}.asset");
            Assert.That(definition, Is.Not.Null, spec.AssetName);
            return definition;
        }

        private static LootDefinitionCatalog LoadCatalog()
        {
            LootDefinitionCatalog catalog =
                AssetDatabase.LoadAssetAtPath<LootDefinitionCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null, CatalogPath);
            return catalog;
        }

        private static LootContainerContentTableEntry FindEntry(
            LootContainerContentTable table,
            LootDefinition definition)
        {
            foreach (LootContainerContentTableEntry entry in table.Entries)
            {
                if (ReferenceEquals(entry.Definition, definition))
                {
                    return entry;
                }
            }

            Assert.Fail($"Definition '{definition.Id}' was not found in the productive loot table.");
            return default;
        }

        private static void AssertCatalogEntry(
            SerializedProperty entries,
            int index,
            string assetName,
            string expectedGuid)
        {
            string assetPath = $"{DefinitionFolder}{assetName}.asset";
            Assert.That(AssetDatabase.AssetPathToGUID(assetPath), Is.EqualTo(expectedGuid), assetName);
            Assert.That(entries.GetArrayElementAtIndex(index).objectReferenceValue,
                Is.SameAs(AssetDatabase.LoadAssetAtPath<LootDefinition>(assetPath)), assetName);
        }

        private readonly struct MotionSpec
        {
            public MotionSpec(string name, int frameCount)
            {
                Name = name;
                FrameCount = frameCount;
            }

            public string Name { get; }
            public int FrameCount { get; }
        }

        private readonly struct PartSpec
        {
            public PartSpec(string basePart, string equipmentPart)
            {
                BasePart = basePart;
                EquipmentPart = equipmentPart;
            }

            public string BasePart { get; }
            public string EquipmentPart { get; }
        }

        private sealed class DefinitionSpec
        {
            public DefinitionSpec(
                string assetName,
                string id,
                LootCategory category,
                EquipmentSlot slot,
                string setName,
                string iconPart,
                params PartSpec[] parts)
            {
                AssetName = assetName;
                Id = id;
                Category = category;
                Slot = slot;
                SetName = setName;
                IconPart = iconPart;
                Parts = parts;
            }

            public string AssetName { get; }
            public string Id { get; }
            public LootCategory Category { get; }
            public EquipmentSlot Slot { get; }
            public string SetName { get; }
            public string IconPart { get; }
            public PartSpec[] Parts { get; }
        }
    }
}
#endif
