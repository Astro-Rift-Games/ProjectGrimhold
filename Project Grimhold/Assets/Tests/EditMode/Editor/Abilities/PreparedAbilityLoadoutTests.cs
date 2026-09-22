#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

[Category("TASK420")]
public sealed class PreparedAbilityLoadoutTests
{
    private static readonly ProfileId Profile = new("93939393939393939393939393939393");

    private LootDefinitionCatalog _lootCatalog;
    private AbilityDefinition _charge;
    private AbilityDefinition _trap;
    private AbilityDefinitionCatalog _abilityCatalog;

    [SetUp]
    public void SetUp()
    {
        _lootCatalog = AssetDatabase.LoadAssetAtPath<LootDefinitionCatalog>(
            "Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset");
        Assert.That(_lootCatalog, Is.Not.Null);

        _charge = AbilityTestFactory.CreateDefinition(
            "charge",
            requirements: new CharacterAttributeRequirement(CharacterAttribute.Strength, 10));
        _trap = AbilityTestFactory.CreateDefinition("trap");
        _abilityCatalog = AbilityTestFactory.CreateCatalog(_charge, _trap);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_abilityCatalog);
        Object.DestroyImmediate(_charge);
        Object.DestroyImmediate(_trap);
    }

    [Test]
    public void SetAndClear_TwoEquivalentOptionalSlotsCommitOnlyConfirmedChanges()
    {
        LocalProfileStore store = CreateStore(out _);
        Unlock(store, "charge", "trap");
        store.ForceCharacterAttributeState(AbilityTestFactory.CreateAttributes(strength: 10));
        int commits = 0;
        store.ProfileCommitted += _ => commits++;

        Assert.That(
            store.TrySetPreparedAbility(UniversalAbilitySlot.Slot2, new AbilityId("trap")),
            Is.EqualTo(AbilityPreparationResult.Success));
        Assert.That(
            store.TrySetPreparedAbility(UniversalAbilitySlot.Slot1, new AbilityId("charge")),
            Is.EqualTo(AbilityPreparationResult.Success));

        Assert.That(store.GetPreparedAbilities().Slot1, Is.EqualTo(new AbilityId("charge")));
        Assert.That(store.GetPreparedAbilities().Slot2, Is.EqualTo(new AbilityId("trap")));
        Assert.That(
            store.TryClearPreparedAbility(UniversalAbilitySlot.Slot1),
            Is.EqualTo(AbilityPreparationResult.Success));
        Assert.That(
            store.TryClearPreparedAbility(UniversalAbilitySlot.Slot1),
            Is.EqualTo(AbilityPreparationResult.Success));
        Assert.That(store.GetPreparedAbilities().Slot1.IsValid, Is.False);
        Assert.That(store.GetPreparedAbilities().Slot2, Is.EqualTo(new AbilityId("trap")));
        Assert.That(commits, Is.EqualTo(3));
    }

    [Test]
    public void Set_RejectsLockedDuplicateUnknownAndUnmetRequirementAtomically()
    {
        LocalProfileStore store = CreateStore(out InMemoryLocalProfileRepository repository);
        Assert.That(store.TryUnlockAbility(new AbilityId("trap")), Is.EqualTo(AbilityUnlockResult.Success));
        LocalProfileSnapshot before = repository.Snapshot;
        int commits = 0;
        store.ProfileCommitted += _ => commits++;

        Assert.That(
            store.TrySetPreparedAbility(UniversalAbilitySlot.Slot1, new AbilityId("charge")),
            Is.EqualTo(AbilityPreparationResult.AbilityNotUnlocked));
        Assert.That(
            store.TrySetPreparedAbility(UniversalAbilitySlot.Slot1, new AbilityId("missing")),
            Is.EqualTo(AbilityPreparationResult.UnknownAbility));
        Assert.That(
            store.TrySetPreparedAbility(UniversalAbilitySlot.Slot1, new AbilityId("trap")),
            Is.EqualTo(AbilityPreparationResult.Success));
        LocalProfileSnapshot afterFirst = repository.Snapshot;
        Assert.That(
            store.TrySetPreparedAbility(UniversalAbilitySlot.Slot2, new AbilityId("trap")),
            Is.EqualTo(AbilityPreparationResult.DuplicateAbility));
        Assert.That(repository.Snapshot, Is.SameAs(afterFirst));
        Assert.That(repository.Snapshot, Is.Not.SameAs(before));
        Assert.That(commits, Is.EqualTo(1));

        Assert.That(store.TryUnlockAbility(new AbilityId("charge")), Is.EqualTo(AbilityUnlockResult.Success));
        Assert.That(
            store.TrySetPreparedAbility(UniversalAbilitySlot.Slot2, new AbilityId("charge")),
            Is.EqualTo(AbilityPreparationResult.AttributeRequirementsNotMet));
    }

    [Test]
    public void AttributeMutation_ClearsOnlyNowInvalidPreparedAbilitiesInSameCommit()
    {
        LocalProfileStore store = CreateStore(out _);
        Unlock(store, "charge", "trap");
        store.ForceCharacterAttributeState(AbilityTestFactory.CreateAttributes(strength: 10));
        Assert.That(store.TrySetPreparedAbility(UniversalAbilitySlot.Slot1, new AbilityId("charge")),
            Is.EqualTo(AbilityPreparationResult.Success));
        Assert.That(store.TrySetPreparedAbility(UniversalAbilitySlot.Slot2, new AbilityId("trap")),
            Is.EqualTo(AbilityPreparationResult.Success));
        int commits = 0;
        store.ProfileCommitted += _ => commits++;

        store.ForceCharacterAttributeState(AbilityTestFactory.CreateAttributes(strength: 9));

        Assert.That(store.TryGetCharacterAttributeState(out CharacterAttributeState attributes), Is.True);
        Assert.That(attributes.Strength, Is.EqualTo(9));
        Assert.That(store.GetPreparedAbilities().Slot1.IsValid, Is.False);
        Assert.That(store.GetPreparedAbilities().Slot2, Is.EqualTo(new AbilityId("trap")));
        Assert.That(store.IsAbilityUnlocked(new AbilityId("charge")), Is.True);
        Assert.That(commits, Is.EqualTo(1));
    }

    [Test]
    public void ProgressionSync_ClearsRequirementInvalidSlotAndPreservesValidSlot()
    {
        LocalProfileStore store = CreateStore(out _);
        Unlock(store, "charge", "trap");
        store.ForceCharacterAttributeState(AbilityTestFactory.CreateAttributes(strength: 10));
        Assert.That(store.TrySetPreparedAbility(UniversalAbilitySlot.Slot1, new AbilityId("charge")),
            Is.EqualTo(AbilityPreparationResult.Success));
        Assert.That(store.TrySetPreparedAbility(UniversalAbilitySlot.Slot2, new AbilityId("trap")),
            Is.EqualTo(AbilityPreparationResult.Success));

        StashOperationResult result = store.TrySyncProgression(
            1,
            0,
            new Grimhold.Backend.CharacterAttributesData
            {
                vitality = 5,
                resistance = 5,
                strength = 9,
                dexterity = 5,
                intelligence = 5,
                luck = 5,
                availablePoints = 0
            });

        Assert.That(result, Is.EqualTo(StashOperationResult.Success));
        Assert.That(store.GetPreparedAbilities().Slot1.IsValid, Is.False);
        Assert.That(store.GetPreparedAbilities().Slot2, Is.EqualTo(new AbilityId("trap")));
    }

    [Test]
    public void Hydration_ClearsRequirementInvalidSlotWithConfirmedAttributes()
    {
        var snapshot = new LocalProfileSnapshot
        {
            ProfileId = Profile,
            CharacterAttributes = AbilityTestFactory.CreateAttributes(strength: 10),
            PreparedAbilities = new PreparedAbilityLoadout(new AbilityId("charge"), new AbilityId("trap"))
        };
        snapshot.UnlockedAbilities.Add(new AbilityId("charge"));
        snapshot.UnlockedAbilities.Add(new AbilityId("trap"));
        var progression = new Grimhold.Backend.ProgressionData
        {
            level = 1,
            characterAttributes = new Grimhold.Backend.CharacterAttributesData
            {
                vitality = 5,
                resistance = 5,
                strength = 9,
                dexterity = 5,
                intelligence = 5,
                luck = 5,
                availablePoints = 0
            }
        };
        MethodInfo method = typeof(ApplicationStashServiceBootstrapper).GetMethod(
            "HydrateSnapshot",
            BindingFlags.NonPublic | BindingFlags.Static);

        method.Invoke(null, new object[]
        {
            Profile,
            snapshot,
            (Grimhold.Backend.InventoryData?)null,
            progression,
            _lootCatalog,
            _abilityCatalog
        });

        Assert.That(snapshot.CharacterAttributes.Strength, Is.EqualTo(9));
        Assert.That(snapshot.PreparedAbilities.Slot1.IsValid, Is.False);
        Assert.That(snapshot.PreparedAbilities.Slot2, Is.EqualTo(new AbilityId("trap")));
    }

    [Test]
    public void Codec_RoundTripsValidPreparedSlotsAndMigratesPreviousSchemaToEmpty()
    {
        var snapshot = new LocalProfileSnapshot
        {
            ProfileId = Profile,
            PreparedAbilities = new PreparedAbilityLoadout(new AbilityId("trap"), default)
        };
        snapshot.UnlockedAbilities.Add(new AbilityId("trap"));
        string json = LocalProfileSaveCodec.Encode(snapshot);

        Assert.That(LocalProfileSaveCodec.TryDecode(
            json,
            Profile,
            _lootCatalog,
            _abilityCatalog,
            out LocalProfileSnapshot restored,
            out _,
            out string error), Is.True, error);
        Assert.That(restored.PreparedAbilities, Is.EqualTo(snapshot.PreparedAbilities));

        string legacyJson = json
            .Replace(
                $"\"schemaVersion\": {LocalProfileSnapshot.CurrentSchemaVersion}",
                $"\"schemaVersion\": {LocalProfileSnapshot.CurrentSchemaVersion - 1}")
            .Replace("\"preparedAbilitySlot1\"", "\"legacyPreparedAbilitySlot1\"")
            .Replace("\"preparedAbilitySlot2\"", "\"legacyPreparedAbilitySlot2\"");
        Assert.That(LocalProfileSaveCodec.TryDecode(
            legacyJson,
            Profile,
            _lootCatalog,
            _abilityCatalog,
            out LocalProfileSnapshot migrated,
            out _,
            out error), Is.True, error);
        Assert.That(migrated.PreparedAbilities, Is.EqualTo(default(PreparedAbilityLoadout)));
    }

    [Test]
    public void Codec_RoundTripsPreparedAbilityWithAttributeRequirements()
    {
        var attributes = AbilityTestFactory.CreateAttributes(strength: 10);
        var snapshot = new LocalProfileSnapshot
        {
            ProfileId = Profile,
            CharacterAttributes = attributes,
            PreparedAbilities = new PreparedAbilityLoadout(new AbilityId("charge"), default)
        };
        snapshot.UnlockedAbilities.Add(new AbilityId("charge"));
        string json = LocalProfileSaveCodec.Encode(snapshot);

        Assert.That(LocalProfileSaveCodec.TryDecode(
            json,
            Profile,
            _lootCatalog,
            _abilityCatalog,
            out LocalProfileSnapshot restored,
            out _,
            out string error), Is.True, error);
        Assert.That(restored.CharacterAttributes.Strength, Is.EqualTo(10));
        Assert.That(restored.PreparedAbilities.Slot1, Is.EqualTo(new AbilityId("charge")));
    }

    [TestCase("missing", null, "unknown")]
    [TestCase("trap", "trap", "duplicate")]
    public void Codec_RejectsUnknownAndDuplicatePreparedState(
        string slot1,
        string slot2,
        string expectedError)
    {
        var snapshot = new LocalProfileSnapshot { ProfileId = Profile };
        snapshot.UnlockedAbilities.Add(new AbilityId("trap"));
        string json = LocalProfileSaveCodec.Encode(snapshot)
            .Replace("\"preparedAbilitySlot1\": \"\"", $"\"preparedAbilitySlot1\": \"{slot1}\"")
            .Replace("\"preparedAbilitySlot2\": \"\"", $"\"preparedAbilitySlot2\": \"{slot2}\"");

        Assert.That(LocalProfileSaveCodec.TryDecode(
            json,
            Profile,
            _lootCatalog,
            _abilityCatalog,
            out _,
            out _,
            out string error), Is.False);
        Assert.That(error, Does.Contain(expectedError).IgnoreCase);
    }

    [Test]
    public void PersistenceFailure_PreservesSnapshotAndDoesNotPublish()
    {
        var snapshot = new LocalProfileSnapshot { ProfileId = Profile };
        snapshot.UnlockedAbilities.Add(new AbilityId("trap"));
        var repository = new RejectingRepository(snapshot);
        var store = new LocalProfileStore(repository, Profile, abilityCatalog: _abilityCatalog);
        int commits = 0;
        store.ProfileCommitted += _ => commits++;
        LogAssert.Expect(LogType.Error, new Regex("\\[LocalProfileStore\\] Commit failed\\."));

        Assert.That(
            store.TrySetPreparedAbility(UniversalAbilitySlot.Slot1, new AbilityId("trap")),
            Is.EqualTo(AbilityPreparationResult.PersistenceFailed));
        Assert.That(repository.Snapshot, Is.SameAs(snapshot));
        Assert.That(repository.Snapshot.PreparedAbilities, Is.EqualTo(default(PreparedAbilityLoadout)));
        Assert.That(commits, Is.Zero);
    }

    private LocalProfileStore CreateStore(out InMemoryLocalProfileRepository repository)
    {
        repository = new InMemoryLocalProfileRepository();
        Assert.That(repository.Initialize(Profile, _lootCatalog), Is.True);
        return new LocalProfileStore(
            repository,
            Profile,
            lootCatalog: _lootCatalog,
            abilityCatalog: _abilityCatalog);
    }

    private static void Unlock(LocalProfileStore store, params string[] ids)
    {
        foreach (string id in ids)
        {
            Assert.That(store.TryUnlockAbility(new AbilityId(id)), Is.EqualTo(AbilityUnlockResult.Success));
        }
    }

    private sealed class RejectingRepository : ILocalProfileRepository
    {
        public RejectingRepository(LocalProfileSnapshot snapshot) => Snapshot = snapshot;
        public LocalProfilePersistenceStatus Status => LocalProfilePersistenceStatus.Ready;
        public string LastError => "Simulated persistence failure.";
        public LocalProfileSnapshot Snapshot { get; }
        public bool Initialize(ProfileId profileId, LootDefinitionCatalog catalog) => false;
        public bool TrySave(LocalProfileSnapshot snapshot, out string error)
        {
            error = LastError;
            return false;
        }
    }
}
#endif
