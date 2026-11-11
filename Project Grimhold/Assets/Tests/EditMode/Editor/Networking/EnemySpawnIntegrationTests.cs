using System;
using System.Collections.Generic;
using NUnit.Framework;
using Spawning;
using UnityEngine;
using ProjectGrimhold.Gameplay.Enemies.Specter;

namespace Grimhold.Tests.Networking
{
    [TestFixture]
    public sealed class EnemySpawnIntegrationTests
    {
        [Test]
        public void DungeonEnvironmentResolver_ResolvesKnownAndCustomScenes()
        {
            Assert.That(DungeonEnvironmentResolver.ResolveEnvironment("Gameplay_Catacombs_01"), Is.EqualTo(DungeonEnvironmentResolver.Catacombs));
            Assert.That(DungeonEnvironmentResolver.ResolveEnvironment("Crypt_Level_2"), Is.EqualTo(DungeonEnvironmentResolver.Catacombs));
            Assert.That(DungeonEnvironmentResolver.ResolveEnvironment("Dungeon_Main"), Is.EqualTo(DungeonEnvironmentResolver.Catacombs));
            Assert.That(DungeonEnvironmentResolver.ResolveEnvironment("Town_Hub"), Is.EqualTo("town"));
            Assert.That(DungeonEnvironmentResolver.ResolveEnvironment("LobbyScene"), Is.EqualTo("town"));
            Assert.That(DungeonEnvironmentResolver.ResolveEnvironment("Forest_Zone"), Is.EqualTo("forest_zone"));
            Assert.That(DungeonEnvironmentResolver.ResolveEnvironment(null), Is.EqualTo(DungeonEnvironmentResolver.Catacombs));
            Assert.That(DungeonEnvironmentResolver.ResolveEnvironment(""), Is.EqualTo(DungeonEnvironmentResolver.Catacombs));
            Assert.That(DungeonEnvironmentResolver.ResolveEnvironment("   "), Is.EqualTo(DungeonEnvironmentResolver.Catacombs));
        }

        [Test]
        public void EnemyThreatCost_ReturnsCorrectCost_ForStandardAndSpecter()
        {
            Assert.That(EnemyThreatCost.DefaultCost, Is.EqualTo(1));
            Assert.That(EnemyThreatCost.SpecterCost, Is.EqualTo(2));

            Assert.That(EnemyThreatCost.GetCost("specter"), Is.EqualTo(2));
            Assert.That(EnemyThreatCost.GetCost("Specter"), Is.EqualTo(2));
            Assert.That(EnemyThreatCost.GetCost("SPECTER"), Is.EqualTo(2));

            Assert.That(EnemyThreatCost.GetCost("slime"), Is.EqualTo(1));
            Assert.That(EnemyThreatCost.GetCost("skeleton"), Is.EqualTo(1));
            Assert.That(EnemyThreatCost.GetCost("goblin"), Is.EqualTo(1));
            Assert.That(EnemyThreatCost.GetCost((string)null), Is.EqualTo(1));
            Assert.That(EnemyThreatCost.GetCost(""), Is.EqualTo(1));
        }

        [Test]
        public void EnemySpawnCandidateResolver_ResolvesSpecterComponent_WithCatacombsAndCostTwo()
        {
            var go = new GameObject("TestSpecterPrefab");
            try
            {
                go.AddComponent<SpecterAIController>();
                var candidate = EnemySpawnCandidateResolver.ResolveCandidateFromGameObject("specter_ref", go);

                Assert.That(candidate.EnemyId, Is.EqualTo("specter"));
                Assert.That(candidate.Weight, Is.EqualTo(10));
                Assert.That(candidate.ThreatCost, Is.EqualTo(2));
                Assert.That(candidate.RequiredEnvironment, Is.EqualTo("catacombs"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void EnemySpawnCandidateResolver_ResolvesAuthoredMetadata_WhenAttached()
        {
            var go = new GameObject("TestCustomEnemy");
            try
            {
                var meta = go.AddComponent<EnemySpawnMetadata>();
                meta.Configure("blood_golem", 20, 3, "crypt");

                var candidate = EnemySpawnCandidateResolver.ResolveCandidateFromGameObject("golem_ref", go);

                Assert.That(candidate.EnemyId, Is.EqualTo("blood_golem"));
                Assert.That(candidate.Weight, Is.EqualTo(20));
                Assert.That(candidate.ThreatCost, Is.EqualTo(3));
                Assert.That(candidate.RequiredEnvironment, Is.EqualTo("crypt"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void EnemySpawnCandidateResolver_FallbackDefault_WhenNoComponentsFound()
        {
            var go = new GameObject("TestPlainEnemy");
            try
            {
                var candidate = EnemySpawnCandidateResolver.ResolveCandidateFromGameObject("plain_ref", go);

                Assert.That(candidate.EnemyId, Is.EqualTo("enemy"));
                Assert.That(candidate.Weight, Is.EqualTo(45));
                Assert.That(candidate.ThreatCost, Is.EqualTo(1));
                Assert.That(candidate.RequiredEnvironment, Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ThreatBudget_Simulation_EnsuresEncounterNeverExceedsRoomBudget()
        {
            var candidates = new List<EnemySpawnCandidate<string>>
            {
                new EnemySpawnCandidate<string>("slime", "slime", weight: 45, threatCost: 1),
                new EnemySpawnCandidate<string>("skeleton", "skeleton", weight: 45, threatCost: 1),
                new EnemySpawnCandidate<string>("specter", "specter", weight: 10, threatCost: 2, requiredEnvironment: "catacombs")
            };

            var random = new System.Random(1337);
            const int roomThreatBudget = 7;
            const int maxRequestedAmount = 10;

            for (int run = 0; run < 100; run++)
            {
                int remainingBudget = roomThreatBudget;
                int totalThreatSpawned = 0;
                int countSpawned = 0;

                for (int i = 0; i < maxRequestedAmount; i++)
                {
                    if (remainingBudget <= 0)
                    {
                        break;
                    }

                    if (WeightedEnemySelector.TrySelect(candidates, "catacombs", remainingBudget, random, out var selected))
                    {
                        totalThreatSpawned += selected.ThreatCost;
                        remainingBudget -= selected.ThreatCost;
                        countSpawned++;
                    }
                    else
                    {
                        break;
                    }
                }

                Assert.That(totalThreatSpawned, Is.LessThanOrEqualTo(roomThreatBudget),
                    $"Run {run} exceeded budget: spawned {totalThreatSpawned} threat vs max {roomThreatBudget}");
                Assert.That(countSpawned, Is.GreaterThan(0), "At least one enemy should spawn with budget 7");
            }
        }

        [Test]
        public void ThreatBudget_Simulation_InNonCatacombs_NeverSpawnsSpecterEvenWithLargeBudget()
        {
            var candidates = new List<EnemySpawnCandidate<string>>
            {
                new EnemySpawnCandidate<string>("slime", "slime", weight: 45, threatCost: 1),
                new EnemySpawnCandidate<string>("skeleton", "skeleton", weight: 45, threatCost: 1),
                new EnemySpawnCandidate<string>("specter", "specter", weight: 10, threatCost: 2, requiredEnvironment: "catacombs")
            };

            var random = new System.Random(999);
            const int roomThreatBudget = 50;

            int remainingBudget = roomThreatBudget;
            int specterCount = 0;

            while (remainingBudget > 0)
            {
                if (WeightedEnemySelector.TrySelect(candidates, "graveyard", remainingBudget, random, out var selected))
                {
                    if (selected.EnemyId == "specter")
                    {
                        specterCount++;
                    }
                    remainingBudget -= selected.ThreatCost;
                }
                else
                {
                    break;
                }
            }

            Assert.That(specterCount, Is.EqualTo(0), "Specter must never spawn outside catacombs environment");
        }
    }
}
