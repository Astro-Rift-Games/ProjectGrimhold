using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Missions
{
    [TestFixture]
    public sealed class MissionDeduplicationTests
    {
        private const string CatalogAssetPath = "Assets/Scriptable Objects/Missions/MissionDefinitionCatalog.asset";

        [Test]
        public void TryApplyMissionProgress_DiscardsDuplicateEventId_IdempotentlyWithoutDuplicateProgress()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<MissionDefinitionCatalog>(CatalogAssetPath);
            Assert.That(catalog.TryGet("CON_E_001", out var m1), Is.True);

            var repository = new InMemoryLocalProfileRepository();
            var profileId = new ProfileId("test-dedup-profile");
            repository.Initialize(profileId, ScriptableObject.CreateInstance<LootDefinitionCatalog>());

            var store = new LocalProfileStore(repository, profileId, missionCatalog: catalog);
            var acceptResult = store.TryAcceptMission(m1);
            Assert.That(acceptResult, Is.EqualTo(StashOperationResult.Success));

            // Initial progress 0
            var active = store.GetActiveMissions();
            Assert.That(active[0].ObjectiveProgress.TryGetValue(0, out var p0) ? p0.CurrentAmount : 0, Is.EqualTo(0));

            // 1. Send first contribution with EventId "evt_test_001"
            var ev1 = new MissionContributionEvent(
                ObjectiveFamily.EliminacionPvE,
                1,
                profileId,
                targetId: "enemy",
                zoneId: null,
                isCompanionContribution: false,
                eventId: "evt_test_001");

            var res1 = store.TryApplyMissionProgress(ev1);
            Assert.That(res1, Is.EqualTo(StashOperationResult.Success));

            active = store.GetActiveMissions();
            Assert.That(active[0].ObjectiveProgress[0].CurrentAmount, Is.EqualTo(1), "First event should increment progress to 1");

            // 2. Resend the EXACT same event (duplicate delivery / retransmitted RPC)
            var resDuplicate = store.TryApplyMissionProgress(ev1);
            Assert.That(resDuplicate, Is.EqualTo(StashOperationResult.Success));

            active = store.GetActiveMissions();
            Assert.That(active[0].ObjectiveProgress[0].CurrentAmount, Is.EqualTo(1), "Duplicate event must be discarded idempotently; progress must remain 1");

            // 3. Send a distinct event with EventId "evt_test_002"
            var ev2 = new MissionContributionEvent(
                ObjectiveFamily.EliminacionPvE,
                1,
                profileId,
                targetId: "enemy",
                zoneId: null,
                isCompanionContribution: false,
                eventId: "evt_test_002");

            var res2 = store.TryApplyMissionProgress(ev2);
            Assert.That(res2, Is.EqualTo(StashOperationResult.Success));

            active = store.GetActiveMissions();
            Assert.That(active[0].ObjectiveProgress[0].CurrentAmount, Is.EqualTo(2), "Distinct event must increment progress to 2");
        }

        [Test]
        public void CON_E_002_RequiresCatacombsMainZone_AndFiltersOutOtherZones()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<MissionDefinitionCatalog>(CatalogAssetPath);
            Assert.That(catalog.TryGet("CON_E_002", out var m2), Is.True);
            Assert.That(m2.Phases[0].Objectives[0].Condition.ZoneId, Is.EqualTo("Catacombs_Main"));

            var repository = new InMemoryLocalProfileRepository();
            var profileId = new ProfileId("test-zone-filter");
            repository.Initialize(profileId, ScriptableObject.CreateInstance<LootDefinitionCatalog>());

            var store = new LocalProfileStore(repository, profileId, missionCatalog: catalog);
            store.TryAcceptMission(m2);

            // 1. Elimination in wrong zone ("Forest_01") -> must be filtered out
            var wrongZoneEv = new MissionContributionEvent(
                ObjectiveFamily.EliminacionEspecifica,
                1,
                profileId,
                targetId: "skeleton",
                zoneId: "Forest_01");

            store.TryApplyMissionProgress(wrongZoneEv);
            var active = store.GetActiveMissions();
            Assert.That(active[0].ObjectiveProgress.TryGetValue(0, out var p0) ? p0.CurrentAmount : 0, Is.EqualTo(0),
                "Kill in Forest_01 must not count towards Catacombs_Main objective");

            // 2. Elimination in correct zone ("Catacombs_Main") -> progress counts
            var correctZoneEv = new MissionContributionEvent(
                ObjectiveFamily.EliminacionEspecifica,
                1,
                profileId,
                targetId: "skeleton",
                zoneId: "Catacombs_Main");

            store.TryApplyMissionProgress(correctZoneEv);
            active = store.GetActiveMissions();
            Assert.That(active[0].ObjectiveProgress[0].CurrentAmount, Is.EqualTo(1),
                "Kill in Catacombs_Main must increment progress to 1");
        }
    }
}
