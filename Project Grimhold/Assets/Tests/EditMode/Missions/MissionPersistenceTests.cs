using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
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

    [Test]
    public void Store_TryClaimMission_WithXPReward_UpdatesProgressionReceipts()
    {
        var repository = new InMemoryLocalProfileRepository();
        var profileId = new ProfileId("test_profile_xp");
        repository.Initialize(profileId, ScriptableObject.CreateInstance<LootDefinitionCatalog>());
        
        var snapshot = new LocalProfileSnapshot { ProfileId = profileId };
        snapshot.Level = 1;
        snapshot.CurrentExperience = 0;
        snapshot.LastAppliedProgressionResultSequence = 10;
        var initialReceipt = new ProgressionReceipt("raid_1", profileId, 10, 0, 1);
        snapshot.LastProgressionReceipt = initialReceipt;
        snapshot.AppliedProgressionReceipts.Add(initialReceipt);
        
        snapshot.ActiveMissions.Add(new MissionInstanceState(new MissionId("mission_test_01"), MissionState.PendienteDeReclamar));
        repository.TrySave(snapshot, out _);
        
        var store = new LocalProfileStore(repository, profileId);

        var result = store.TryClaimMission(_testMission);

        Assert.AreEqual(StashOperationResult.Success, result);
        
        var nextSnapshot = repository.Snapshot;
        Assert.AreEqual(50, nextSnapshot.CurrentExperience);
        Assert.AreEqual(11, nextSnapshot.LastAppliedProgressionResultSequence);
        Assert.IsTrue(nextSnapshot.LastProgressionReceipt.HasValue);
        Assert.AreEqual(11, nextSnapshot.LastProgressionReceipt.Value.ResultSequence);
        Assert.AreEqual("mission-claim", nextSnapshot.LastProgressionReceipt.Value.RaidId);
        
        Assert.AreEqual(2, nextSnapshot.AppliedProgressionReceipts.Count);
        Assert.AreEqual(11, nextSnapshot.AppliedProgressionReceipts[1].ResultSequence);
        Assert.AreEqual(nextSnapshot.LastProgressionReceipt.Value, nextSnapshot.AppliedProgressionReceipts[1]);
    }

    [Test]
    public void Store_TryClaimMission_WithXPReward_ResultsInValidCodecRoundTrip()
    {
        var repository = new InMemoryLocalProfileRepository();
        var profileId = new ProfileId("test_profile_xp_codec");
        var catalog = ScriptableObject.CreateInstance<LootDefinitionCatalog>();
        repository.Initialize(profileId, catalog);
        
        var snapshot = new LocalProfileSnapshot { ProfileId = profileId };
        snapshot.Level = 1;
        snapshot.CurrentExperience = 0;
        snapshot.LastAppliedProgressionResultSequence = 10;
        var initialReceipt = new ProgressionReceipt("raid_1", profileId, 10, 0, 1);
        snapshot.LastProgressionReceipt = initialReceipt;
        snapshot.AppliedProgressionReceipts.Add(initialReceipt);
        
        snapshot.ActiveMissions.Add(new MissionInstanceState(new MissionId("mission_test_01"), MissionState.PendienteDeReclamar));
        repository.TrySave(snapshot, out _);
        
        var store = new LocalProfileStore(repository, profileId);

        var result = store.TryClaimMission(_testMission);
        Assert.AreEqual(StashOperationResult.Success, result);

        string json = LocalProfileSaveCodec.Encode(repository.Snapshot);
        bool decoded = LocalProfileSaveCodec.TryDecode(json, profileId, catalog, out _, out var status, out string error);
        
        Assert.IsTrue(decoded, $"Decode failed after claiming mission with XP: {error}");
        Assert.AreEqual(LocalProfilePersistenceStatus.Ready, status);
    }

    [Test]
    public void Store_TryAcceptMission_AllowsNewMission_WhenSlotsTakenByTerminalMissions()
    {
        // Arrange: catalog with 4 missions
        var catalog = ScriptableObject.CreateInstance<MissionDefinitionCatalog>();
        var setMissions = typeof(MissionDefinitionCatalog).GetField(
            "_missions", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        var missions = new List<MissionDefinition>();
        for (int i = 1; i <= 4; i++)
        {
            var def = ScriptableObject.CreateInstance<MissionDefinition>();
            typeof(MissionDefinition).GetField("_id", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(def, $"mission_{i:D2}");
            def.Type = MissionType.Normal;
            missions.Add(def);
        }
        if (setMissions != null) setMissions.SetValue(catalog, missions);

        var profileId = new ProfileId("test_slot_filter");
        var repository = new InMemoryLocalProfileRepository();
        repository.Initialize(profileId, ScriptableObject.CreateInstance<LootDefinitionCatalog>());

        var snapshot = new LocalProfileSnapshot { ProfileId = profileId };
        // 3 missions already Reclamada (terminal) — they must NOT count toward slot limit
        snapshot.ActiveMissions.Add(new MissionInstanceState(new MissionId("mission_01"), MissionState.Reclamada));
        snapshot.ActiveMissions.Add(new MissionInstanceState(new MissionId("mission_02"), MissionState.Reclamada));
        snapshot.ActiveMissions.Add(new MissionInstanceState(new MissionId("mission_03"), MissionState.Abandonada));
        repository.TrySave(snapshot, out _);

        var store = new LocalProfileStore(repository, profileId, null, default, catalog);

        // Act: accept a 4th mission — should succeed because the previous 3 are terminal
        var result = store.TryAcceptMission(missions[3]);

        Assert.AreEqual(StashOperationResult.Success, result);
        var active = store.GetActiveMissions();
        Assert.AreEqual(1, active.Count(m => m.State == MissionState.Activa));
    }
}
