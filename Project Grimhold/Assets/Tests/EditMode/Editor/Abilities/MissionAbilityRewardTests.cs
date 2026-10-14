#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

[Category("TASK206")]
public sealed class MissionAbilityRewardTests
{
    private const string MissionIdValue = "mission_ability_reward";

    private static readonly ProfileId Profile = new("95959595959595959595959595959595");

    private LootDefinitionCatalog _lootCatalog;
    private AbilityDefinition _charge;
    private AbilityDefinition _trap;
    private AbilityDefinitionCatalog _abilityCatalog;
    private MissionDefinition _mission;

    [SetUp]
    public void SetUp()
    {
        _lootCatalog = ScriptableObject.CreateInstance<LootDefinitionCatalog>();
        _charge = AbilityTestFactory.CreateDefinition(
            "charge",
            requirements: new CharacterAttributeRequirement(CharacterAttribute.Strength, 10));
        _trap = AbilityTestFactory.CreateDefinition("trap");
        _abilityCatalog = AbilityTestFactory.CreateCatalog(_charge, _trap);

        _mission = ScriptableObject.CreateInstance<MissionDefinition>();
        typeof(MissionDefinition)
            .GetField("_id", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(_mission, MissionIdValue);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_mission);
        Object.DestroyImmediate(_abilityCatalog);
        Object.DestroyImmediate(_charge);
        Object.DestroyImmediate(_trap);
        Object.DestroyImmediate(_lootCatalog);
    }

    [Test]
    public void TryClaimMission_WithAbilityReward_UnlocksAbilityInTheSameCommit()
    {
        SetRewards(Gold(100), Ability("trap"));
        LocalProfileStore store = CreateStore(out _);
        int commits = 0;
        store.ProfileCommitted += _ => commits++;

        StashOperationResult result = store.TryClaimMission(_mission);

        Assert.That(result, Is.EqualTo(StashOperationResult.Success));
        Assert.That(store.GetUnlockedAbilities(), Is.EqualTo(new[] { new AbilityId("trap") }));
        Assert.That(store.GetCurrency(), Is.EqualTo(100L));
        Assert.That(store.GetActiveMissions()[0].State, Is.EqualTo(MissionState.Reclamada));
        Assert.That(commits, Is.EqualTo(1));
    }

    [Test]
    public void TryClaimMission_WithAlreadyUnlockedAbility_ClaimsTheRestWithoutDuplicating()
    {
        SetRewards(Gold(100), Ability("trap"));
        LocalProfileStore store = CreateStore(out _);
        Assert.That(store.TryUnlockAbility(new AbilityId("trap")), Is.EqualTo(AbilityUnlockResult.Success));

        StashOperationResult result = store.TryClaimMission(_mission);

        Assert.That(result, Is.EqualTo(StashOperationResult.Success));
        Assert.That(store.GetUnlockedAbilities(), Is.EqualTo(new[] { new AbilityId("trap") }));
        Assert.That(store.GetCurrency(), Is.EqualTo(100L));
        Assert.That(store.GetActiveMissions()[0].State, Is.EqualTo(MissionState.Reclamada));
    }

    [Test]
    public void TryClaimMission_WithRepeatedAbilityReward_UnlocksOnce()
    {
        SetRewards(Ability("trap"), Ability("trap"));
        LocalProfileStore store = CreateStore(out _);

        StashOperationResult result = store.TryClaimMission(_mission);

        Assert.That(result, Is.EqualTo(StashOperationResult.Success));
        Assert.That(store.GetUnlockedAbilities(), Is.EqualTo(new[] { new AbilityId("trap") }));
    }

    [Test]
    public void TryClaimMission_WithAbilityReward_IgnoresAttributeRequirements()
    {
        SetRewards(Ability("charge"));
        LocalProfileStore store = CreateStore(out _);

        StashOperationResult result = store.TryClaimMission(_mission);

        Assert.That(result, Is.EqualTo(StashOperationResult.Success));
        Assert.That(store.IsAbilityUnlocked(new AbilityId("charge")), Is.True);
    }

    [TestCase("missing_ability")]
    [TestCase("")]
    [TestCase(null)]
    public void TryClaimMission_WithInvalidAbilityReward_RejectsTheWholeClaim(string referenceId)
    {
        SetRewards(Gold(100), Ability(referenceId));
        LocalProfileStore store = CreateStore(out InMemoryLocalProfileRepository repository);
        LocalProfileSnapshot before = repository.Snapshot;
        int commits = 0;
        store.ProfileCommitted += _ => commits++;

        StashOperationResult result = store.TryClaimMission(_mission);

        Assert.That(result, Is.Not.EqualTo(StashOperationResult.Success));
        Assert.That(repository.Snapshot, Is.SameAs(before));
        Assert.That(store.GetCurrency(), Is.Zero);
        Assert.That(store.GetUnlockedAbilities(), Is.Empty);
        Assert.That(store.GetActiveMissions()[0].State, Is.EqualTo(MissionState.PendienteDeReclamar));
        Assert.That(commits, Is.Zero);
    }

    private LocalProfileStore CreateStore(out InMemoryLocalProfileRepository repository)
    {
        repository = new InMemoryLocalProfileRepository();
        repository.Initialize(Profile, _lootCatalog);
        var snapshot = new LocalProfileSnapshot { ProfileId = Profile };
        snapshot.ActiveMissions.Add(
            new MissionInstanceState(new MissionId(MissionIdValue), MissionState.PendienteDeReclamar));
        Assert.That(repository.TrySave(snapshot, out _), Is.True);
        return new LocalProfileStore(repository, Profile, _lootCatalog, abilityCatalog: _abilityCatalog);
    }

    private void SetRewards(params RewardDefinition[] rewards) =>
        _mission.Rewards = new List<RewardDefinition>(rewards);

    private static RewardDefinition Gold(int amount) =>
        new() { Type = RewardDefinition.RewardType.Gold, Amount = amount };

    private static RewardDefinition Ability(string abilityId) =>
        new() { Type = RewardDefinition.RewardType.Ability, ReferenceId = abilityId, Amount = 1 };
}
#endif
