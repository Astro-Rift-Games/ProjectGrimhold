#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;

namespace Tests.EditMode.Loot
{
    /// <summary>Doc 09 §15.2: a training weapon is never generated as corpse loot.</summary>
    public sealed class CorpseLootProjectionTests
    {
        private static readonly LootId Training = new("training_dagger");
        private static readonly LootId Sword = new("arming_sword");
        private static readonly LootId Bone = new("bone");
        private LootDefinitionCatalog _catalog;
        private RaidLootOrigin _player;

        [SetUp]
        public void SetUp()
        {
            _catalog = AssetDatabase.LoadAssetAtPath<LootDefinitionCatalog>(
                "Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset");
            Assert.That(RaidParticipantId.TryCreate(1, out RaidParticipantId participant), Is.True);
            Assert.That(RaidLootOrigin.TryCreatePlayer(participant, out _player), Is.True);
        }

        [Test]
        public void ExcludeTrainingWeapons_DropsTheirEntriesOriginsAndWeaponUnits()
        {
            var unit = new ExpeditionWeaponUnit(Sword, _player, default);
            CorpseLootProjection.ExcludeTrainingWeapons(
                new[] { new LootEntry(Training, 1, isEquipmentUnit: true), new LootEntry(Bone, 2), new LootEntry(Sword, 1, isEquipmentUnit: true) },
                new[] { new RaidLootOriginEntry(Training, _player, 1), new RaidLootOriginEntry(Bone, RaidLootOrigin.Dungeon, 2), new RaidLootOriginEntry(Sword, _player, 1) },
                new[] { new ExpeditionWeaponUnit(Training, _player, default), unit },
                _catalog,
                out IReadOnlyList<LootEntry> loot,
                out IReadOnlyList<RaidLootOriginEntry> origins,
                out IReadOnlyList<ExpeditionWeaponUnit> units);

            Assert.That(loot, Is.EqualTo(new[] { new LootEntry(Bone, 2), new LootEntry(Sword, 1, isEquipmentUnit: true) }));
            Assert.That(origins, Is.EqualTo(new[] { new RaidLootOriginEntry(Bone, RaidLootOrigin.Dungeon, 2), new RaidLootOriginEntry(Sword, _player, 1) }));
            Assert.That(units, Is.EqualTo(new[] { unit }));
        }

        [Test]
        public void ExcludeTrainingWeapons_WithOnlyATrainingWeapon_LeavesAnEmptyCorpse()
        {
            CorpseLootProjection.ExcludeTrainingWeapons(
                new[] { new LootEntry(Training, 1, isEquipmentUnit: true) },
                new[] { new RaidLootOriginEntry(Training, _player, 1) },
                new[] { new ExpeditionWeaponUnit(Training, _player, default) },
                _catalog,
                out IReadOnlyList<LootEntry> loot,
                out IReadOnlyList<RaidLootOriginEntry> origins,
                out IReadOnlyList<ExpeditionWeaponUnit> units);

            Assert.That(loot, Is.Empty);
            Assert.That(origins, Is.Empty);
            Assert.That(units, Is.Empty);
        }

        [Test]
        public void ExcludeTrainingWeapons_WithoutTrainingWeapons_KeepsTheExactContent()
        {
            var loot = new[] { new LootEntry(Bone, 2), new LootEntry(Sword, 1, isEquipmentUnit: true) };
            var origins = new[] { new RaidLootOriginEntry(Bone, RaidLootOrigin.Dungeon, 2), new RaidLootOriginEntry(Sword, _player, 1) };
            var units = new[] { new ExpeditionWeaponUnit(Sword, _player, default) };

            CorpseLootProjection.ExcludeTrainingWeapons(loot, origins, units, _catalog,
                out IReadOnlyList<LootEntry> corpseLoot,
                out IReadOnlyList<RaidLootOriginEntry> corpseOrigins,
                out IReadOnlyList<ExpeditionWeaponUnit> corpseUnits);

            Assert.That(corpseLoot, Is.EqualTo(loot));
            Assert.That(corpseOrigins, Is.EqualTo(origins));
            Assert.That(corpseUnits, Is.EqualTo(units));
        }
    }
}
#endif
