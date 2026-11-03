using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Missions
{
    public sealed class RankEContractsAndGuildReputationTests
    {
        private const string CatalogAssetPath = "Assets/Scriptable Objects/Missions/MissionDefinitionCatalog.asset";

        [Test]
        public void ProductiveMissionCatalog_ValidatesCleanly_AndContainsAll6CanonicalRankEContracts()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<MissionDefinitionCatalog>(CatalogAssetPath);
            Assert.That(catalog, Is.Not.Null, $"MissionDefinitionCatalog must exist at '{CatalogAssetPath}'.");

            bool isValid = catalog.TryValidate(out string error);
            Assert.That(isValid, Is.True, $"Catalog failed validation: {error}");
            Assert.That(catalog.DefinitionCount, Is.EqualTo(6), "Catalog must contain exactly the 6 Rank E contracts.");

            // CON_E_001
            Assert.That(catalog.TryGet("CON_E_001", out var m1), Is.True);
            Assert.That(m1.Title, Is.EqualTo("Limpieza de amenazas"));
            Assert.That(m1.RankRequired, Is.EqualTo(MissionRank.E));
            Assert.That(m1.Phases.Count, Is.EqualTo(1));
            Assert.That(m1.Phases[0].Objectives.Count, Is.EqualTo(1));
            Assert.That(m1.Phases[0].Objectives[0].Family, Is.EqualTo(ObjectiveFamily.EliminacionPvE));
            Assert.That(m1.Phases[0].Objectives[0].Condition.RequiredAmount, Is.EqualTo(3));
            AssertReward(m1.Rewards, RewardDefinition.RewardType.Experience, 50);
            AssertReward(m1.Rewards, RewardDefinition.RewardType.Gold, 20);
            AssertReward(m1.Rewards, RewardDefinition.RewardType.GuildReputation, 5);

            // CON_E_002
            Assert.That(catalog.TryGet("CON_E_002", out var m2), Is.True);
            Assert.That(m2.Title, Is.EqualTo("Purga de las catacumbas"));
            Assert.That(m2.RankRequired, Is.EqualTo(MissionRank.E));
            Assert.That(m2.Phases[0].Objectives[0].Family, Is.EqualTo(ObjectiveFamily.EliminacionEspecifica));
            Assert.That(m2.Phases[0].Objectives[0].Condition.TargetId, Is.EqualTo("skeleton"));
            Assert.That(m2.Phases[0].Objectives[0].Condition.RequiredAmount, Is.EqualTo(2));
            AssertReward(m2.Rewards, RewardDefinition.RewardType.Experience, 75);
            AssertReward(m2.Rewards, RewardDefinition.RewardType.Gold, 30);
            AssertReward(m2.Rewards, RewardDefinition.RewardType.GuildReputation, 7);

            // CON_E_003
            Assert.That(catalog.TryGet("CON_E_003", out var m3), Is.True);
            Assert.That(m3.Title, Is.EqualTo("Reconocimiento de territorio"));
            Assert.That(m3.RankRequired, Is.EqualTo(MissionRank.E));
            Assert.That(m3.Phases[0].Objectives[0].Family, Is.EqualTo(ObjectiveFamily.Exploracion));
            Assert.That(m3.Phases[0].Objectives[0].Condition.ZoneId, Is.EqualTo("Catacombs_Main"));
            Assert.That(m3.Phases[0].Objectives[0].Condition.RequiredAmount, Is.EqualTo(1));
            AssertReward(m3.Rewards, RewardDefinition.RewardType.Experience, 40);
            AssertReward(m3.Rewards, RewardDefinition.RewardType.Gold, 15);
            AssertReward(m3.Rewards, RewardDefinition.RewardType.GuildReputation, 5);

            // CON_E_004
            Assert.That(catalog.TryGet("CON_E_004", out var m4), Is.True);
            Assert.That(m4.Title, Is.EqualTo("Suministros olvidados"));
            Assert.That(m4.RankRequired, Is.EqualTo(MissionRank.E));
            Assert.That(m4.Phases[0].Objectives[0].Family, Is.EqualTo(ObjectiveFamily.Interaccion));
            Assert.That(m4.Phases[0].Objectives[0].Condition.TargetId, Is.EqualTo("chest"));
            Assert.That(m4.Phases[0].Objectives[0].Condition.RequiredAmount, Is.EqualTo(2));
            AssertReward(m4.Rewards, RewardDefinition.RewardType.Experience, 50);
            AssertReward(m4.Rewards, RewardDefinition.RewardType.Gold, 20);
            AssertReward(m4.Rewards, RewardDefinition.RewardType.GuildReputation, 5);

            // CON_E_005
            Assert.That(catalog.TryGet("CON_E_005", out var m5), Is.True);
            Assert.That(m5.Title, Is.EqualTo("Recolección de hierro negro"));
            Assert.That(m5.RankRequired, Is.EqualTo(MissionRank.E));
            Assert.That(m5.Phases[0].Objectives[0].Family, Is.EqualTo(ObjectiveFamily.Obtencion));
            Assert.That(m5.Phases[0].Objectives[0].Condition.TargetId, Is.EqualTo("RES_BLACK_IRON"));
            Assert.That(m5.Phases[0].Objectives[0].Condition.RequiredAmount, Is.EqualTo(2));
            AssertReward(m5.Rewards, RewardDefinition.RewardType.Experience, 50);
            AssertReward(m5.Rewards, RewardDefinition.RewardType.Gold, 20);
            AssertReward(m5.Rewards, RewardDefinition.RewardType.GuildReputation, 5);

            // CON_E_006
            Assert.That(catalog.TryGet("CON_E_006", out var m6), Is.True);
            Assert.That(m6.Title, Is.EqualTo("Recuperación de hierro negro"));
            Assert.That(m6.RankRequired, Is.EqualTo(MissionRank.E));
            Assert.That(m6.Phases[0].Objectives[0].Family, Is.EqualTo(ObjectiveFamily.ExtraccionDeObjetos));
            Assert.That(m6.Phases[0].Objectives[0].Condition.TargetId, Is.EqualTo("RES_BLACK_IRON"));
            Assert.That(m6.Phases[0].Objectives[0].Condition.RequiredAmount, Is.EqualTo(2));
            AssertReward(m6.Rewards, RewardDefinition.RewardType.Experience, 80);
            AssertReward(m6.Rewards, RewardDefinition.RewardType.Gold, 35);
            AssertReward(m6.Rewards, RewardDefinition.RewardType.GuildReputation, 8);
        }

        [Test]
        public void GuildRank_CalculatesFromReputationThresholds()
        {
            var snapshot = new LocalProfileSnapshot();
            Assert.That(snapshot.GuildReputation, Is.EqualTo(0));
            Assert.That(snapshot.GuildRank, Is.EqualTo(MissionRank.E));

            snapshot.GuildReputation = 50;
            Assert.That(snapshot.GuildRank, Is.EqualTo(MissionRank.E));

            snapshot.GuildReputation = 99;
            Assert.That(snapshot.GuildRank, Is.EqualTo(MissionRank.E));

            snapshot.GuildReputation = 100;
            Assert.That(snapshot.GuildRank, Is.EqualTo(MissionRank.D));

            snapshot.GuildReputation = 250;
            Assert.That(snapshot.GuildRank, Is.EqualTo(MissionRank.D));
        }

        [Test]
        public void LocalProfileSaveCodec_EncodesAndDecodesGuildReputation_AndMigratesLegacySaves()
        {
            var profileId = new ProfileId("test-reputation-player");
            var lootCatalog = ScriptableObject.CreateInstance<LootDefinitionCatalog>();

            // 1. Current SchemaVersion 10 roundtrip
            var snapshot = new LocalProfileSnapshot
            {
                ProfileId = profileId,
                GuildReputation = 42
            };

            string json = LocalProfileSaveCodec.Encode(snapshot);
            Assert.That(json.Contains("\"guildReputation\":42"), Is.True);

            bool success = LocalProfileSaveCodec.TryDecode(json, profileId, lootCatalog, out var decoded, out var status, out string error);
            Assert.That(success, Is.True, error);
            Assert.That(status, Is.EqualTo(LocalProfilePersistenceStatus.Ready));
            Assert.That(decoded.GuildReputation, Is.EqualTo(42));
            Assert.That(decoded.GuildRank, Is.EqualTo(MissionRank.E));

            // 2. Backward compatibility with SchemaVersion 9 (without guildReputation field)
            string legacyJson = json.Replace("\"schemaVersion\":10", "\"schemaVersion\":9")
                                    .Replace("\"guildReputation\":42,", "");
            bool legacySuccess = LocalProfileSaveCodec.TryDecode(legacyJson, profileId, lootCatalog, out var legacyDecoded, out var legacyStatus, out string legacyError);
            Assert.That(legacySuccess, Is.True, legacyError);
            Assert.That(legacyStatus, Is.EqualTo(LocalProfilePersistenceStatus.Ready));
            Assert.That(legacyDecoded.GuildReputation, Is.EqualTo(0), "Legacy saves without guildReputation must default to 0.");
            Assert.That(legacyDecoded.GuildRank, Is.EqualTo(MissionRank.E));
        }

        [Test]
        public void LocalProfileStore_TryClaimMission_AccumulatesGuildReputationAtomics()
        {
            var repository = new InMemoryLocalProfileRepository();
            var profileId = new ProfileId("test-guild-claim");
            repository.Initialize(profileId, ScriptableObject.CreateInstance<LootDefinitionCatalog>());

            var initialSnapshot = new LocalProfileSnapshot
            {
                ProfileId = profileId,
                GuildReputation = 95
            };
            var missionId = new MissionId("test_rep_contract");
            initialSnapshot.ActiveMissions.Add(new MissionInstanceState(missionId, MissionState.PendienteDeReclamar));
            repository.TrySave(initialSnapshot, out _);

            var store = new LocalProfileStore(repository, profileId);
            Assert.That(store.GuildReputation, Is.EqualTo(95));
            Assert.That(store.GuildRank, Is.EqualTo(MissionRank.E));

            var missionDef = ScriptableObject.CreateInstance<MissionDefinition>();
            typeof(MissionDefinition).GetField("_id", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(missionDef, "test_rep_contract");
            missionDef.Rewards = new List<RewardDefinition>
            {
                new RewardDefinition { Type = RewardDefinition.RewardType.GuildReputation, Amount = 10 },
                new RewardDefinition { Type = RewardDefinition.RewardType.Experience, Amount = 25 },
                new RewardDefinition { Type = RewardDefinition.RewardType.Gold, Amount = 15 }
            };

            var claimResult = store.TryClaimMission(missionDef);
            Assert.That(claimResult, Is.EqualTo(StashOperationResult.Success));

            // 95 + 10 = 105 -> Promotes to Rank D!
            Assert.That(store.GuildReputation, Is.EqualTo(105));
            Assert.That(store.GuildRank, Is.EqualTo(MissionRank.D));
        }

        [Test]
        public void LocalProfileStore_TryAcceptMission_ValidatesRankRequired()
        {
            var repository = new InMemoryLocalProfileRepository();
            var profileId = new ProfileId("test-rank-check");
            repository.Initialize(profileId, ScriptableObject.CreateInstance<LootDefinitionCatalog>());

            var initialSnapshot = new LocalProfileSnapshot
            {
                ProfileId = profileId,
                GuildReputation = 50 // Rank E
            };
            repository.TrySave(initialSnapshot, out _);
            var store = new LocalProfileStore(repository, profileId);

            var rankDMission = ScriptableObject.CreateInstance<MissionDefinition>();
            typeof(MissionDefinition).GetField("_id", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(rankDMission, "contract_d_01");
            rankDMission.RankRequired = MissionRank.D;

            // Player at Rank E cannot accept Rank D mission
            var result = store.TryAcceptMission(rankDMission);
            Assert.That(result, Is.EqualTo(StashOperationResult.InvalidInventory));

            var rankEMission = ScriptableObject.CreateInstance<MissionDefinition>();
            typeof(MissionDefinition).GetField("_id", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(rankEMission, "contract_e_01");
            rankEMission.RankRequired = MissionRank.E;

            // Player at Rank E CAN accept Rank E mission
            var resultE = store.TryAcceptMission(rankEMission);
            Assert.That(resultE, Is.EqualTo(StashOperationResult.Success));
        }

        [Test]
        public void MissionDefinitionCatalog_GetAvailableDefinitionsForRank_FiltersCorrectly()
        {
            var catalog = ScriptableObject.CreateInstance<MissionDefinitionCatalog>();

            var mE = ScriptableObject.CreateInstance<MissionDefinition>();
            typeof(MissionDefinition).GetField("_id", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(mE, "m_rank_e");
            mE.RankRequired = MissionRank.E;

            var mD = ScriptableObject.CreateInstance<MissionDefinition>();
            typeof(MissionDefinition).GetField("_id", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(mD, "m_rank_d");
            mD.RankRequired = MissionRank.D;

            typeof(MissionDefinitionCatalog).GetField("_definitions", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(catalog, new List<MissionDefinition> { mE, mD });

            var rankEAvailable = catalog.GetAvailableDefinitionsForRank(MissionRank.E);
            Assert.That(rankEAvailable.Count, Is.EqualTo(1));
            Assert.That(rankEAvailable[0].Id, Is.EqualTo("m_rank_e"));

            var rankDAvailable = catalog.GetAvailableDefinitionsForRank(MissionRank.D);
            Assert.That(rankDAvailable.Count, Is.EqualTo(2));
        }

        private static void AssertReward(List<RewardDefinition> rewards, RewardDefinition.RewardType type, int expectedAmount)
        {
            for (int i = 0; i < rewards.Count; i++)
            {
                if (rewards[i].Type == type)
                {
                    Assert.That(rewards[i].Amount, Is.EqualTo(expectedAmount), $"Reward of type {type} has unexpected amount.");
                    return;
                }
            }
            Assert.Fail($"Reward of type {type} not found in rewards list.");
        }
    }
}
