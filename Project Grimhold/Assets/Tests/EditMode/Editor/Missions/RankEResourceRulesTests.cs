using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Missions
{
    public sealed class RankEResourceRulesTests
    {
        private const string CatalogAssetPath = "Assets/Scriptable Objects/Missions/MissionDefinitionCatalog.asset";
        private const string IronLootId = "RES_BLACK_IRON";

        [Test]
        public void E05_01_DynamicPossession_ReflectsLoadoutChangesUpAndDown()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<MissionDefinitionCatalog>(CatalogAssetPath);
            Assert.That(catalog.TryGet("CON_E_005", out var m5), Is.True);

            var repository = new InMemoryLocalProfileRepository();
            var profileId = new ProfileId("test-e05-01");
            repository.Initialize(profileId, ScriptableObject.CreateInstance<LootDefinitionCatalog>());

            var initialSnapshot = new LocalProfileSnapshot { ProfileId = profileId };
            // Start with 2 iron in Stash, 0 in Loadout
            initialSnapshot.Stash.Add(new StashItem(new LootId(IronLootId), 2, 0));
            repository.TrySave(initialSnapshot, out _);

            var store = new LocalProfileStore(repository, profileId, missionCatalog: catalog);

            // Accept CON_E_005
            var acceptResult = store.TryAcceptMission(m5);
            Assert.That(acceptResult, Is.EqualTo(StashOperationResult.Success));

            var active = store.GetActiveMissions();
            Assert.That(active.Count, Is.EqualTo(1));
            Assert.That(active[0].State, Is.EqualTo(MissionState.Activa));
            Assert.That(active[0].ObjectiveProgress[0].CurrentAmount, Is.EqualTo(0), "Initial Loadout has 0 iron -> 0/2");

            // 1) Transfer 1 iron from Stash to Loadout -> progress becomes 1/2
            var transfer1 = store.TryTransferToLoadout(new LootId(IronLootId), 1);
            Assert.That(transfer1, Is.EqualTo(StashOperationResult.Success));
            active = store.GetActiveMissions();
            Assert.That(active[0].ObjectiveProgress[0].CurrentAmount, Is.EqualTo(1), "Loadout has 1 iron -> 1/2");
            Assert.That(active[0].State, Is.EqualTo(MissionState.Activa));

            // 2) Transfer that 1 iron back to Stash -> progress drops to 0/2
            var transferBack = store.TryTransferToStash(new LootId(IronLootId), 1);
            Assert.That(transferBack, Is.EqualTo(StashOperationResult.Success));
            active = store.GetActiveMissions();
            Assert.That(active[0].ObjectiveProgress[0].CurrentAmount, Is.EqualTo(0), "Loadout has 0 iron -> 0/2");
            Assert.That(active[0].State, Is.EqualTo(MissionState.Activa));

            // 3) Transfer 2 iron from Stash to Loadout -> progress becomes 2/2 -> transitions to PendienteDeReclamar
            var transfer2 = store.TryTransferToLoadout(new LootId(IronLootId), 2);
            Assert.That(transfer2, Is.EqualTo(StashOperationResult.Success));
            active = store.GetActiveMissions();
            Assert.That(active[0].ObjectiveProgress[0].CurrentAmount, Is.EqualTo(2), "Loadout has 2 iron -> 2/2");
            Assert.That(active[0].State, Is.EqualTo(MissionState.PendienteDeReclamar), "Full possession transitions to PendienteDeReclamar");

            // 4) Transfer 1 iron back to Stash -> progress drops to 1/2 -> reverts to Activa
            var transferDown = store.TryTransferToStash(new LootId(IronLootId), 1);
            Assert.That(transferDown, Is.EqualTo(StashOperationResult.Success));
            active = store.GetActiveMissions();
            Assert.That(active[0].ObjectiveProgress[0].CurrentAmount, Is.EqualTo(1), "Loadout has 1 iron -> 1/2");
            Assert.That(active[0].State, Is.EqualTo(MissionState.Activa), "Lost possession reverts from PendienteDeReclamar to Activa");
        }

        [Test]
        public void E05_02_DynamicPossession_IgnoresUnrelatedLootIds()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<MissionDefinitionCatalog>(CatalogAssetPath);
            Assert.That(catalog.TryGet("CON_E_005", out var m5), Is.True);

            var repository = new InMemoryLocalProfileRepository();
            var profileId = new ProfileId("test-e05-02");
            repository.Initialize(profileId, ScriptableObject.CreateInstance<LootDefinitionCatalog>());

            var initialSnapshot = new LocalProfileSnapshot { ProfileId = profileId };
            // Add unrelated items into Loadout
            initialSnapshot.Loadout.Add(new StashItem(new LootId("healthpotion"), 5, 0));
            initialSnapshot.Loadout.Add(new StashItem(new LootId("wood_plank"), 10, 1));
            repository.TrySave(initialSnapshot, out _);

            var store = new LocalProfileStore(repository, profileId, missionCatalog: catalog);
            store.TryAcceptMission(m5);

            var active = store.GetActiveMissions();
            Assert.That(active[0].ObjectiveProgress[0].CurrentAmount, Is.EqualTo(0), "Unrelated items must not count for RES_BLACK_IRON");
            Assert.That(active[0].State, Is.EqualTo(MissionState.Activa));
        }

        [Test]
        public void E05_03_Claim_RejectsWhenMaterialsAreOnlyInStash()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<MissionDefinitionCatalog>(CatalogAssetPath);
            Assert.That(catalog.TryGet("CON_E_005", out var m5), Is.True);

            var repository = new InMemoryLocalProfileRepository();
            var profileId = new ProfileId("test-e05-03");
            repository.Initialize(profileId, ScriptableObject.CreateInstance<LootDefinitionCatalog>());

            var initialSnapshot = new LocalProfileSnapshot { ProfileId = profileId };
            // 5 iron in Stash, but 0 in Loadout
            initialSnapshot.Stash.Add(new StashItem(new LootId(IronLootId), 5, 0));
            repository.TrySave(initialSnapshot, out _);

            var store = new LocalProfileStore(repository, profileId, missionCatalog: catalog);
            store.TryAcceptMission(m5);

            // CanClaimMission check
            bool canClaim = store.CanClaimMission(m5, out string failureReason);
            Assert.That(canClaim, Is.False);
            Assert.That(failureReason, Does.Contain("reclamación").Or.Contain("Alijo"));

            // TryClaimMission check
            var claimResult = store.TryClaimMission(m5);
            Assert.That(claimResult, Is.EqualTo(StashOperationResult.InvalidInventory));

            // Stash must remain untouched
            Assert.That(store.GetStash()[0].Amount, Is.EqualTo(5));
            Assert.That(store.GetActiveMissions()[0].State, Is.EqualTo(MissionState.Activa));
        }

        [Test]
        public void E06_01_SingleExpeditionExtraction_CompletesWhenRequiredExtractedInSameExpedition()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<MissionDefinitionCatalog>(CatalogAssetPath);
            Assert.That(catalog.TryGet("CON_E_006", out var m6), Is.True);

            var repository = new InMemoryLocalProfileRepository();
            var profileId = new ProfileId("test-e06-01");
            repository.Initialize(profileId, ScriptableObject.CreateInstance<LootDefinitionCatalog>());

            var initialSnapshot = new LocalProfileSnapshot { ProfileId = profileId };
            repository.TrySave(initialSnapshot, out _);

            var store = new LocalProfileStore(repository, profileId, missionCatalog: catalog);
            store.TryAcceptMission(m6);

            // Commit an extraction that extracted 2 iron in raid "raid-alpha"
            var receipt = new ExtractionReceipt("raid-alpha", profileId, 1);
            var items = new List<StashItem> { new StashItem(new LootId(IronLootId), 2) };

            var extractionResult = store.TryCommitExtraction(receipt, items, new PreparedEquipmentLoadout(), 0, 1, 0);
            Assert.That(extractionResult, Is.EqualTo(StashOperationResult.Success));

            var active = store.GetActiveMissions();
            Assert.That(active[0].ObjectiveProgress[0].CurrentAmount, Is.EqualTo(2));
            Assert.That(active[0].ObjectiveProgress[0].ExpeditionId, Is.EqualTo("raid-alpha"));
            Assert.That(active[0].State, Is.EqualTo(MissionState.PendienteDeReclamar));
        }

        [Test]
        public void E06_02_SingleExpeditionExtraction_RejectsMineralsFromDifferentExpeditions()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<MissionDefinitionCatalog>(CatalogAssetPath);
            Assert.That(catalog.TryGet("CON_E_006", out var m6), Is.True);

            var repository = new InMemoryLocalProfileRepository();
            var profileId = new ProfileId("test-e06-02");
            repository.Initialize(profileId, ScriptableObject.CreateInstance<LootDefinitionCatalog>());

            var initialSnapshot = new LocalProfileSnapshot { ProfileId = profileId };
            repository.TrySave(initialSnapshot, out _);

            var store = new LocalProfileStore(repository, profileId, missionCatalog: catalog);
            store.TryAcceptMission(m6);

            // Raid 1 extracts 1 iron
            var receipt1 = new ExtractionReceipt("raid-001", profileId, 1);
            var items1 = new List<StashItem> { new StashItem(new LootId(IronLootId), 1) };
            store.TryCommitExtraction(receipt1, items1, new PreparedEquipmentLoadout(), 0, 1, 0);

            var active = store.GetActiveMissions();
            Assert.That(active[0].ObjectiveProgress[0].CurrentAmount, Is.EqualTo(1));
            Assert.That(active[0].ObjectiveProgress[0].ExpeditionId, Is.EqualTo("raid-001"));
            Assert.That(active[0].State, Is.EqualTo(MissionState.Activa));

            // Move the extracted iron to Stash so Loadout is empty for next raid admission
            store.TryTransferToStash(new LootId(IronLootId), 1);

            // Raid 2 extracts 1 iron from a different expedition "raid-002"
            var receipt2 = new ExtractionReceipt("raid-002", profileId, 2);
            var items2 = new List<StashItem> { new StashItem(new LootId(IronLootId), 1) };
            store.TryCommitExtraction(receipt2, items2, new PreparedEquipmentLoadout(), 0, 1, 0);

            active = store.GetActiveMissions();
            // Must NOT accumulate to 2! Different expeditions are rejected; counter resets to 1 for raid-002.
            Assert.That(active[0].ObjectiveProgress[0].CurrentAmount, Is.EqualTo(1));
            Assert.That(active[0].ObjectiveProgress[0].ExpeditionId, Is.EqualTo("raid-002"));
            Assert.That(active[0].State, Is.EqualTo(MissionState.Activa), "Minerals from different expeditions must not satisfy single-expedition extraction");
        }

        [Test]
        public void E06_03_SingleExpeditionExtraction_RecordsExpeditionId_AndSerializesProperly()
        {
            var profile = new LocalProfileSnapshot { ProfileId = new ProfileId("test-codec-expedition") };
            var instance = new MissionInstanceState(new MissionId("CON_E_006"), MissionState.Activa);
            instance.ObjectiveProgress[0] = new ObjectiveProgressState(2, "raid-test-uuid-42");
            profile.ActiveMissions.Add(instance);

            string encoded = LocalProfileSaveCodec.Encode(profile);
            Assert.That(encoded, Does.Contain("raid-test-uuid-42"));

            var lootCatalog = ScriptableObject.CreateInstance<LootDefinitionCatalog>();
            var decodedStatus = LocalProfileSaveCodec.TryDecode(encoded, profile.ProfileId, lootCatalog, out var decoded, out var status, out string error);
            Assert.That(decodedStatus, Is.True, error);
            Assert.That(status, Is.EqualTo(LocalProfilePersistenceStatus.Ready));
            Assert.That(decoded.ActiveMissions.Count, Is.EqualTo(1));
            Assert.That(decoded.ActiveMissions[0].ObjectiveProgress[0].CurrentAmount, Is.EqualTo(2));
            Assert.That(decoded.ActiveMissions[0].ObjectiveProgress[0].ExpeditionId, Is.EqualTo("raid-test-uuid-42"));
        }

        [Test]
        public void MAT_01_ConcurrentMissions_ClaimConsumesLoadoutAndRecalculatesOtherMissions()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<MissionDefinitionCatalog>(CatalogAssetPath);
            Assert.That(catalog.TryGet("CON_E_005", out var m5), Is.True);

            // Create a second mission requiring 2 iron
            var mSecond = ScriptableObject.CreateInstance<MissionDefinition>();
            typeof(MissionDefinition).GetField("_id", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(mSecond, "CON_SECOND_IRON");
            mSecond.Title = "Segunda misión de hierro";
            mSecond.RankRequired = MissionRank.E;
            mSecond.Phases = new List<PhaseDefinition>
            {
                new PhaseDefinition
                {
                    Objectives = new List<ObjectiveDefinition>
                    {
                        new ObjectiveDefinition
                        {
                            Family = ObjectiveFamily.Obtencion,
                            Condition = new ObjectiveCondition { TargetId = IronLootId, RequiredAmount = 2 }
                        }
                    }
                }
            };
            mSecond.Rewards = new List<RewardDefinition>
            {
                new RewardDefinition { Type = RewardDefinition.RewardType.Gold, Amount = 10 }
            };

            // Register second mission in a test catalog
            var testCatalog = ScriptableObject.CreateInstance<MissionDefinitionCatalog>();
            typeof(MissionDefinitionCatalog).GetField("_definitions", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(testCatalog, new List<MissionDefinition> { m5, mSecond });

            var repository = new InMemoryLocalProfileRepository();
            var profileId = new ProfileId("test-mat-01");
            repository.Initialize(profileId, ScriptableObject.CreateInstance<LootDefinitionCatalog>());

            var initialSnapshot = new LocalProfileSnapshot { ProfileId = profileId };
            // Exactly 2 iron in Loadout
            initialSnapshot.Loadout.Add(new StashItem(new LootId(IronLootId), 2, 0));
            repository.TrySave(initialSnapshot, out _);

            var store = new LocalProfileStore(repository, profileId, missionCatalog: testCatalog);
            store.TryAcceptMission(m5);
            store.TryAcceptMission(mSecond);

            var active = store.GetActiveMissions();
            Assert.That(active.Count, Is.EqualTo(2));
            Assert.That(active[0].State, Is.EqualTo(MissionState.PendienteDeReclamar));
            Assert.That(active[1].State, Is.EqualTo(MissionState.PendienteDeReclamar));

            // Claim CON_E_005 -> consumes the 2 iron from Loadout
            var claimResult = store.TryClaimMission(m5);
            Assert.That(claimResult, Is.EqualTo(StashOperationResult.Success));
            Assert.That(store.GetLoadout().Count, Is.EqualTo(0), "Loadout must be empty after handing over the 2 iron");

            // Recalculation on concurrent mission: mSecond must immediately reflect 0 iron and revert to Activa!
            active = store.GetActiveMissions();
            var secondInstance = active.FirstOrDefault(m => m.MissionId.Value == "CON_SECOND_IRON");
            Assert.That(secondInstance.ObjectiveProgress[0].CurrentAmount, Is.EqualTo(0), "Shared resource consumption immediately recalculates concurrent mission to 0");
            Assert.That(secondInstance.State, Is.EqualTo(MissionState.Activa), "Concurrent mission must revert to Activa");

            // Attempting to claim mSecond now must fail
            bool canClaimSecond = store.CanClaimMission(mSecond, out string failureReason);
            Assert.That(canClaimSecond, Is.False);
            Assert.That(store.TryClaimMission(mSecond), Is.EqualTo(StashOperationResult.InvalidInventory));
        }

        [Test]
        public void MAT_02_AtomicClaim_DeductsItemsAndGrantsRewardsOrRollsBackCompletely()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<MissionDefinitionCatalog>(CatalogAssetPath);
            Assert.That(catalog.TryGet("CON_E_005", out var m5), Is.True);

            var repository = new InMemoryLocalProfileRepository();
            var profileId = new ProfileId("test-mat-02");
            repository.Initialize(profileId, ScriptableObject.CreateInstance<LootDefinitionCatalog>());

            var initialSnapshot = new LocalProfileSnapshot
            {
                ProfileId = profileId,
                Currency = 0,
                GuildReputation = 10,
                Level = 1,
                CurrentExperience = 0
            };
            initialSnapshot.Loadout.Add(new StashItem(new LootId(IronLootId), 2, 0));
            repository.TrySave(initialSnapshot, out _);

            var store = new LocalProfileStore(repository, profileId, missionCatalog: catalog);
            store.TryAcceptMission(m5);

            // Claim CON_E_005 (Rewards: 50 XP, 20 Gold, 5 Rep)
            var claimResult = store.TryClaimMission(m5);
            Assert.That(claimResult, Is.EqualTo(StashOperationResult.Success));

            Assert.That(store.GetLoadout().Count, Is.EqualTo(0), "Materials removed from Loadout");
            Assert.That(store.GetCurrency(), Is.EqualTo(20), "20 Gold credited");
            Assert.That(store.GuildReputation, Is.EqualTo(15), "10 + 5 Rep credited");
            Assert.That(store.GetCurrentExperience(), Is.EqualTo(50), "50 XP credited");
            Assert.That(store.GetActiveMissions()[0].State, Is.EqualTo(MissionState.Reclamada));

            // Test rollback when Stash is full for item reward
            var itemMission = ScriptableObject.CreateInstance<MissionDefinition>();
            typeof(MissionDefinition).GetField("_id", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(itemMission, "CON_ITEM_REWARD");
            itemMission.Title = "Misión con recompensa de item";
            itemMission.RankRequired = MissionRank.E;
            itemMission.Phases = new List<PhaseDefinition>
            {
                new PhaseDefinition
                {
                    Objectives = new List<ObjectiveDefinition>
                    {
                        new ObjectiveDefinition
                        {
                            Family = ObjectiveFamily.Obtencion,
                            Condition = new ObjectiveCondition { TargetId = IronLootId, RequiredAmount = 1 }
                        }
                    }
                }
            };
            itemMission.Rewards = new List<RewardDefinition>
            {
                new RewardDefinition { Type = RewardDefinition.RewardType.Item, Amount = 1, ReferenceId = "reward_sword" }
            };

            var testCatalog = ScriptableObject.CreateInstance<MissionDefinitionCatalog>();
            typeof(MissionDefinitionCatalog).GetField("_definitions", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(testCatalog, new List<MissionDefinition> { itemMission });

            var repoFull = new InMemoryLocalProfileRepository();
            var profileFullId = new ProfileId("test-full-stash");
            repoFull.Initialize(profileFullId, ScriptableObject.CreateInstance<LootDefinitionCatalog>());

            var fullSnapshot = new LocalProfileSnapshot { ProfileId = profileFullId };
            // Fill Stash with 60 distinct items to reach DefaultStashCapacity
            for (int i = 0; i < 60; i++)
            {
                fullSnapshot.Stash.Add(new StashItem(new LootId($"dummy_item_{i}"), 1, i));
            }
            // 1 iron in Loadout
            fullSnapshot.Loadout.Add(new StashItem(new LootId(IronLootId), 1, 0));
            repoFull.TrySave(fullSnapshot, out _);

            var storeFull = new LocalProfileStore(repoFull, profileFullId, missionCatalog: testCatalog);
            storeFull.TryAcceptMission(itemMission);

            Assert.That(storeFull.GetActiveMissions()[0].State, Is.EqualTo(MissionState.PendienteDeReclamar));

            // Claim should fail because Stash is full for the item reward
            bool canClaim = storeFull.CanClaimMission(itemMission, out string failReason);
            Assert.That(canClaim, Is.False);
            Assert.That(failReason, Does.Contain("Alijo"));

            var claimFullResult = storeFull.TryClaimMission(itemMission);
            Assert.That(claimFullResult, Is.EqualTo(StashOperationResult.InventoryFull));

            // Atomic rollback verification: material must NOT have been deducted!
            Assert.That(storeFull.GetLoadout().Count, Is.EqualTo(1));
            Assert.That(storeFull.GetLoadout()[0].LootId.Value, Is.EqualTo(IronLootId));
            Assert.That(storeFull.GetActiveMissions()[0].State, Is.EqualTo(MissionState.PendienteDeReclamar));
        }
    }
}
