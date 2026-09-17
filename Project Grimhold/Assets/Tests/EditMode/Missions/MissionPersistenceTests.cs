using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

public class MissionPersistenceTests
{
    private MissionDefinitionCatalog _catalog;
    private MissionDefinition _testMission;

    [SetUp]
    public void SetUp()
    {
        _testMission = ScriptableObject.CreateInstance<MissionDefinition>();
        typeof(MissionDefinition).GetField("_id", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(_testMission, "mission_test_01");
        _testMission.Type = MissionType.Normal;
        _testMission.RankRequired = MissionRank.E;
        _testMission.Rewards = new List<RewardDefinition>
        {
            new RewardDefinition { Type = RewardDefinition.RewardType.Gold, Amount = 100 },
            new RewardDefinition { Type = RewardDefinition.RewardType.Experience, Amount = 50 },
            new RewardDefinition { Type = RewardDefinition.RewardType.Item, ReferenceId = "item_test", Amount = 2 }
        };

        _catalog = ScriptableObject.CreateInstance<MissionDefinitionCatalog>();
        // Using reflection to set the internal list if needed, or we can just mock the catalog if possible.
        // Actually, for LocalProfileStore tests, if we don't strictly need the catalog for TryClaimMission (it uses the passed definition directly), we can pass null or an empty catalog.
    }

    [Test]
    public void Codec_EncodesAndDecodes_ActiveMissions()
    {
        var originalSnapshot = new LocalProfileSnapshot { ProfileId = new ProfileId("test_profile") };
        var mission = new MissionInstanceState(new MissionId("mission_1"), MissionState.Activa);
        mission.CurrentPhaseIndex = 1;
        mission.ObjectiveProgress[0] = new ObjectiveProgressState(5);
        originalSnapshot.ActiveMissions.Add(mission);

        string json = LocalProfileSaveCodec.Encode(originalSnapshot);
        
        bool success = LocalProfileSaveCodec.TryDecode(json, new ProfileId("test_profile"), ScriptableObject.CreateInstance<LootDefinitionCatalog>(), out var decodedSnapshot, out var status, out string error);

        Assert.IsTrue(success, $"Decode failed: {error}");
        Assert.AreEqual(LocalProfilePersistenceStatus.Ready, status);
        Assert.AreEqual(1, decodedSnapshot.ActiveMissions.Count);
        
        var decodedMission = decodedSnapshot.ActiveMissions[0];
        Assert.AreEqual("mission_1", decodedMission.MissionId.Value);
        Assert.AreEqual(MissionState.Activa, decodedMission.State);
        Assert.AreEqual(1, decodedMission.CurrentPhaseIndex);
        Assert.IsTrue(decodedMission.ObjectiveProgress.ContainsKey(0));
        Assert.AreEqual(5, decodedMission.ObjectiveProgress[0].CurrentAmount);
    }

    [Test]
    public void Store_TryAcceptMission_AddsMissionToActiveList()
    {
        var repository = new InMemoryLocalProfileRepository();
        repository.Initialize(new ProfileId("test_profile"), ScriptableObject.CreateInstance<LootDefinitionCatalog>());
        repository.TrySave(new LocalProfileSnapshot { ProfileId = new ProfileId("test_profile") }, out _);
        var store = new LocalProfileStore(repository, new ProfileId("test_profile"));

        var result = store.TryAcceptMission(_testMission);
        
        Assert.AreEqual(StashOperationResult.Success, result);
        var activeMissions = store.GetActiveMissions();
        Assert.AreEqual(1, activeMissions.Count);
        Assert.AreEqual("mission_test_01", activeMissions[0].MissionId.Value);
        Assert.AreEqual(MissionState.Activa, activeMissions[0].State);
    }

    [Test]
    public void Store_TryAbandonMission_ChangesStateToAbandonada()
    {
        var repository = new InMemoryLocalProfileRepository();
        repository.Initialize(new ProfileId("test_profile"), ScriptableObject.CreateInstance<LootDefinitionCatalog>());
        var snapshot = new LocalProfileSnapshot { ProfileId = new ProfileId("test_profile") };
        snapshot.ActiveMissions.Add(new MissionInstanceState(new MissionId("mission_test_01"), MissionState.Activa));
        repository.TrySave(snapshot, out _);
        var store = new LocalProfileStore(repository, new ProfileId("test_profile"));

        var result = store.TryAbandonMission(new MissionId("mission_test_01"));

        Assert.AreEqual(StashOperationResult.Success, result);
        var activeMissions = store.GetActiveMissions();
        Assert.AreEqual(MissionState.Abandonada, activeMissions[0].State);
    }

    [Test]
    public void Store_TryClaimMission_GrantsRewardsAndChangesState()
    {
        var repository = new InMemoryLocalProfileRepository();
        repository.Initialize(new ProfileId("test_profile"), ScriptableObject.CreateInstance<LootDefinitionCatalog>());
        var snapshot = new LocalProfileSnapshot { ProfileId = new ProfileId("test_profile") };
        snapshot.ActiveMissions.Add(new MissionInstanceState(new MissionId("mission_test_01"), MissionState.PendienteDeReclamar));
        repository.TrySave(snapshot, out _);
        var store = new LocalProfileStore(repository, new ProfileId("test_profile"));

        var result = store.TryClaimMission(_testMission);

        Assert.AreEqual(StashOperationResult.Success, result);
        var activeMissions = store.GetActiveMissions();
        Assert.AreEqual(MissionState.Reclamada, activeMissions[0].State);
        
        Assert.AreEqual(100, store.GetCurrency());
        Assert.AreEqual(50, store.GetCurrentExperience());
        
        var stash = store.GetStash();
        Assert.AreEqual(1, stash.Count);
        Assert.AreEqual("item_test", stash[0].LootId.Value);
        Assert.AreEqual(2, stash[0].Amount);
    }
}
