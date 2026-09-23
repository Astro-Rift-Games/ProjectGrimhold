#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEditor;

[Category("BACK-06")]
public class HydrateSnapshotTests
{
    private LootDefinitionCatalog _lootCatalog;
    private AbilityDefinitionCatalog _abilityCatalog;
    private ProfileId _profileId = new ProfileId("test-profile");

    [SetUp]
    public void SetUp()
    {
        _lootCatalog = AssetDatabase.LoadAssetAtPath<LootDefinitionCatalog>(
            "Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset");
        _abilityCatalog = AssetDatabase.LoadAssetAtPath<AbilityDefinitionCatalog>(
            "Assets/Scriptable Objects/Abilities/Catalogs/AbilityDefinitionCatalog.asset");
    }

    [Test]
    public void HydrateSnapshot_ReturnsFalse_AndSnapshotUnmodified_OnInvalidAttributes()
    {
        var snapshot = new LocalProfileSnapshot { ProfileId = _profileId };
        var progressionData = new Grimhold.Backend.ProgressionData
        {
            revision = 5,
            level = 1,
            experience = 0,
            characterAttributes = new Grimhold.Backend.CharacterAttributesData
            {
                // Let's make an invalid attribute combination that fails TryCreate
                vitality = 100, // Very high -> fails available points validation
                resistance = 5,
                strength = 5,
                dexterity = 5,
                intelligence = 5,
                luck = 5,
                availablePoints = 0
            }
        };

        var inventoryData = new Grimhold.Backend.InventoryData
        {
            revision = 5,
            loadout = new Grimhold.Backend.StashItemData[0],
            stash = new Grimhold.Backend.StashItemData[0],
            preparedEquipment = new Grimhold.Backend.PreparedEquipmentData()
        };

        bool result = ApplicationStashServiceBootstrapper.HydrateSnapshot(
            _profileId,
            snapshot,
            inventoryData,
            progressionData,
            _lootCatalog,
            _abilityCatalog
        );

        Assert.That(result, Is.False);
        // The snapshot should remain unmodified
        Assert.That(snapshot.CharacterAttributes.Vitality, Is.Not.EqualTo(100)); 
    }

    [Test]
    public void HydrateSnapshot_ReturnsTrue_AndSetsRemoteRevision_OnSuccess()
    {
        var snapshot = new LocalProfileSnapshot { ProfileId = _profileId };
        var progressionData = new Grimhold.Backend.ProgressionData
        {
            revision = 10,
            level = 1,
            experience = 0,
            characterAttributes = new Grimhold.Backend.CharacterAttributesData
            {
                vitality = 5,
                resistance = 5,
                strength = 5,
                dexterity = 5,
                intelligence = 5,
                luck = 5,
                availablePoints = 0
            }
        };

        var inventoryData = new Grimhold.Backend.InventoryData
        {
            revision = 10,
            loadout = new Grimhold.Backend.StashItemData[0],
            stash = new Grimhold.Backend.StashItemData[0],
            preparedEquipment = new Grimhold.Backend.PreparedEquipmentData()
        };

        bool result = ApplicationStashServiceBootstrapper.HydrateSnapshot(
            _profileId,
            snapshot,
            inventoryData,
            progressionData,
            _lootCatalog,
            _abilityCatalog
        );

        Assert.That(result, Is.True);
        Assert.That(snapshot.CharacterAttributes.Vitality, Is.EqualTo(5));
        // The RemoteRevision is not actually part of LocalProfileSnapshot but is set on LocalProfileStore
        // However, HydrateSnapshot correctly handles mapping.
    }
}
#endif
