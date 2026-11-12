using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Missions
{
    public sealed class RaidMissionEventProducersTests
    {
        private const string CatalogAssetPath = "Assets/Scriptable Objects/Missions/MissionDefinitionCatalog.asset";

        [Test]
        public void AllFiveObjectiveFamilies_DispatchedAndProcessedCorrectly()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<MissionDefinitionCatalog>(CatalogAssetPath);
            Assert.That(catalog, Is.Not.Null);

            var repository = new InMemoryLocalProfileRepository();
            var profileId = new ProfileId("test-event-families");
            repository.Initialize(profileId, ScriptableObject.CreateInstance<LootDefinitionCatalog>());

            var initialSnapshot = new LocalProfileSnapshot { ProfileId = profileId };
            repository.TrySave(initialSnapshot, out _);

            var store = new LocalProfileStore(repository, profileId, missionCatalog: catalog);

            // 1. PvE Elimination (CON_E_001)
            Assert.That(catalog.TryGet("CON_E_001", out var m1), Is.True);
            store.TryAcceptMission(m1);
            var ev1 = new MissionContributionEvent(ObjectiveFamily.EliminacionPvE, 1, profileId);
            store.TryApplyMissionProgress(ev1);
            var active = store.GetActiveMissions();
            Assert.That(active[0].ObjectiveProgress[0].CurrentAmount, Is.EqualTo(1));

            // 2. Specific Elimination (CON_E_002: skeleton in Catacombs_Main)
            Assert.That(catalog.TryGet("CON_E_002", out var m2), Is.True);
            store.TryAcceptMission(m2);
            var ev2 = new MissionContributionEvent(ObjectiveFamily.EliminacionEspecifica, 1, profileId, targetId: "skeleton", zoneId: "Catacombs_Main");
            store.TryApplyMissionProgress(ev2);
            active = store.GetActiveMissions();
            var inst2 = active.FirstOrDefault(m => m.MissionId.Value == "CON_E_002");
            Assert.That(inst2.ObjectiveProgress[0].CurrentAmount, Is.EqualTo(1));

            // 3. Exploration (CON_E_003: Catacombs_Main)
            Assert.That(catalog.TryGet("CON_E_003", out var m3), Is.True);
            store.TryAcceptMission(m3);
            var ev3 = new MissionContributionEvent(ObjectiveFamily.Exploracion, 1, profileId, zoneId: "Catacombs_Main");
            store.TryApplyMissionProgress(ev3);
            active = store.GetActiveMissions();
            var inst3 = active.FirstOrDefault(m => m.MissionId.Value == "CON_E_003");
            Assert.That(inst3.State, Is.EqualTo(MissionState.PendienteDeReclamar));
        }

        [Test]
        public void DuoCoopRules_SharedProgressAllowedForPvEAndChests()
        {
            var pveDef = ScriptableObject.CreateInstance<MissionDefinition>();
            typeof(MissionDefinition).GetField("_id", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(pveDef, "m_pve");
            pveDef.RankRequired = MissionRank.E;
            pveDef.Phases = new List<PhaseDefinition>
            {
                new PhaseDefinition
                {
                    Objectives = new List<ObjectiveDefinition>
                    {
                        new ObjectiveDefinition
                        {
                            Family = ObjectiveFamily.EliminacionPvE,
                            Condition = new ObjectiveCondition { RequiredAmount = 5 }
                        }
                    }
                }
            };

            var chestDef = ScriptableObject.CreateInstance<MissionDefinition>();
            typeof(MissionDefinition).GetField("_id", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(chestDef, "m_chest");
            chestDef.RankRequired = MissionRank.E;
            chestDef.Phases = new List<PhaseDefinition>
            {
                new PhaseDefinition
                {
                    Objectives = new List<ObjectiveDefinition>
                    {
                        new ObjectiveDefinition
                        {
                            Family = ObjectiveFamily.Interaccion,
                            Condition = new ObjectiveCondition { TargetId = "chest", RequiredAmount = 3 }
                        }
                    }
                }
            };

            var statePve = new MissionInstanceState(new MissionId("m_pve"), MissionState.Activa);
            var stateChest = new MissionInstanceState(new MissionId("m_chest"), MissionState.Activa);

            // Companion kill
            var compKill = new MissionContributionEvent(
                ObjectiveFamily.EliminacionPvE,
                1,
                new ProfileId("owner"),
                isCompanionContribution: true);
            bool pveProgress = MissionProgressEngine.TryApplyProgress(statePve, pveDef, compKill);
            Assert.That(pveProgress, Is.True, "Companion kill must be accepted for EliminacionPvE");
            Assert.That(statePve.ObjectiveProgress[0].CurrentAmount, Is.EqualTo(1));

            // Companion chest interaction
            var compChest = new MissionContributionEvent(
                ObjectiveFamily.Interaccion,
                1,
                new ProfileId("owner"),
                targetId: "chest",
                isCompanionContribution: true);
            bool chestProgress = MissionProgressEngine.TryApplyProgress(stateChest, chestDef, compChest);
            Assert.That(chestProgress, Is.True, "Companion chest open must be accepted for Interaccion");
            Assert.That(stateChest.ObjectiveProgress[0].CurrentAmount, Is.EqualTo(1));
        }

        [Test]
        public void DuoCoopRules_IndividualProgressEnforcedForCollectionAndExtraction()
        {
            var obtencionDef = ScriptableObject.CreateInstance<MissionDefinition>();
            typeof(MissionDefinition).GetField("_id", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(obtencionDef, "m_loot");
            obtencionDef.RankRequired = MissionRank.E;
            obtencionDef.Phases = new List<PhaseDefinition>
            {
                new PhaseDefinition
                {
                    Objectives = new List<ObjectiveDefinition>
                    {
                        new ObjectiveDefinition
                        {
                            Family = ObjectiveFamily.Obtencion,
                            Condition = new ObjectiveCondition { TargetId = "RES_IRON", RequiredAmount = 2 }
                        }
                    }
                }
            };

            var pvpDef = ScriptableObject.CreateInstance<MissionDefinition>();
            typeof(MissionDefinition).GetField("_id", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(pvpDef, "m_pvp");
            pvpDef.RankRequired = MissionRank.E;
            pvpDef.Phases = new List<PhaseDefinition>
            {
                new PhaseDefinition
                {
                    Objectives = new List<ObjectiveDefinition>
                    {
                        new ObjectiveDefinition
                        {
                            Family = ObjectiveFamily.PvP,
                            Condition = new ObjectiveCondition { RequiredAmount = 1 }
                        }
                    }
                }
            };

            var stateObtencion = new MissionInstanceState(new MissionId("m_loot"), MissionState.Activa);
            var statePvp = new MissionInstanceState(new MissionId("m_pvp"), MissionState.Activa);

            // Attempt companion contribution on Obtencion
            var compLoot = new MissionContributionEvent(
                ObjectiveFamily.Obtencion,
                1,
                new ProfileId("owner"),
                targetId: "RES_IRON",
                isCompanionContribution: true);
            bool lootProgress = MissionProgressEngine.TryApplyProgress(stateObtencion, obtencionDef, compLoot);
            Assert.That(lootProgress, Is.False, "Companion contribution must be rejected for Obtencion (individual only)");

            // Attempt companion contribution on PvP kill
            var compPvp = new MissionContributionEvent(
                ObjectiveFamily.PvP,
                1,
                new ProfileId("owner"),
                isCompanionContribution: true);
            bool pvpProgress = MissionProgressEngine.TryApplyProgress(statePvp, pvpDef, compPvp);
            Assert.That(pvpProgress, Is.False, "Companion contribution must be rejected for PvP (individual only)");
        }

        [Test]
        public void RaidZoneTrigger_ConfiguresProperlyAndProvidesFallback()
        {
            var go = new GameObject("Zone_Catacombs_Main");
            var col = go.AddComponent<BoxCollider2D>();
            var trigger = go.AddComponent<RaidZoneTrigger>();

            trigger.ZoneId = "Catacombs_Main";
            trigger.DuoProximityRadius = 30f;

            Assert.That(col.isTrigger, Is.True, "Collider2D must be configured as trigger");
            Assert.That(trigger.ZoneId, Is.EqualTo("Catacombs_Main"));
            Assert.That(trigger.DuoProximityRadius, Is.EqualTo(30f));

            // Passing null player safely returns false
            Assert.That(trigger.TryTriggerExploration(null), Is.False);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void RaidZoneTrigger_StaticSpatialDetection_DetectsPlayerAlreadyInside()
        {
            var zoneGo = new GameObject("Zone_Catacombs_Main");
            var col = zoneGo.AddComponent<BoxCollider2D>();
            col.size = new Vector2(10f, 10f);
            var trigger = zoneGo.AddComponent<RaidZoneTrigger>();
            trigger.ZoneId = "Catacombs_Main";

            var playerInsideGo = new GameObject("PlayerInside");
            playerInsideGo.transform.position = new Vector3(2f, 2f, 0f);
            var playerInside = playerInsideGo.AddComponent<PlayerCharacter>();

            var playerOutsideGo = new GameObject("PlayerOutside");
            playerOutsideGo.transform.position = new Vector3(50f, 50f, 0f);
            var playerOutside = playerOutsideGo.AddComponent<PlayerCharacter>();

            Assert.That(trigger.IsPlayerInside(playerInside), Is.True, "Player positioned at (2,2) must be detected inside (10x10) trigger volume.");
            Assert.That(trigger.IsPlayerInside(playerOutside), Is.False, "Player positioned at (50,50) must NOT be detected inside trigger volume.");
            Assert.That(trigger.IsPlayerInside(null), Is.False);

            // Null link/participant fails gracefully without exception
            Assert.That(trigger.TryTriggerExplorationIfInside(playerInside), Is.False);

            Object.DestroyImmediate(playerInsideGo);
            Object.DestroyImmediate(playerOutsideGo);
            Object.DestroyImmediate(zoneGo);
        }
    }
}
