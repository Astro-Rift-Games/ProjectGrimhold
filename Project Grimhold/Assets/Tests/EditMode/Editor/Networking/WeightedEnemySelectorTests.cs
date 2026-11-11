using System;
using System.Collections.Generic;
using NUnit.Framework;
using Spawning;

namespace Grimhold.Tests.Networking
{
    [TestFixture]
    public sealed class WeightedEnemySelectorTests
    {
        private List<EnemySpawnCandidate<string>> CreateStandardCandidates()
        {
            return new List<EnemySpawnCandidate<string>>
            {
                new EnemySpawnCandidate<string>("slime_prefab", "slime", weight: 45, threatCost: 1),
                new EnemySpawnCandidate<string>("skeleton_prefab", "skeleton", weight: 45, threatCost: 1),
                new EnemySpawnCandidate<string>("specter_prefab", "specter", weight: 10, threatCost: 2, requiredEnvironment: "catacombs")
            };
        }

        [Test]
        public void Selection_FollowsStatisticalDistribution_Across10000Iterations()
        {
            var candidates = CreateStandardCandidates();
            var random = new Random(42); // Fixed deterministic seed

            int slimeCount = 0;
            int skeletonCount = 0;
            int specterCount = 0;

            const int totalIterations = 10000;

            for (int i = 0; i < totalIterations; i++)
            {
                bool success = WeightedEnemySelector.TrySelect(
                    candidates,
                    currentEnvironment: "catacombs",
                    remainingThreatBudget: 10,
                    random,
                    out var selected);

                Assert.That(success, Is.True);

                switch (selected.EnemyId)
                {
                    case "slime":
                        slimeCount++;
                        break;
                    case "skeleton":
                        skeletonCount++;
                        break;
                    case "specter":
                        specterCount++;
                        break;
                    default:
                        Assert.Fail($"Unexpected enemy selected: {selected.EnemyId}");
                        break;
                }
            }

            // Specter weight is 10%, Slime 45%, Skeleton 45%
            Assert.That(specterCount, Is.LessThan(slimeCount), "Specter must appear with lower frequency than Slime.");
            Assert.That(specterCount, Is.LessThan(skeletonCount), "Specter must appear with lower frequency than Skeleton.");

            // Expected distribution: ~4500 slime, ~4500 skeleton, ~1000 specter (+/- 250 margin)
            Assert.That(specterCount, Is.InRange(750, 1250), $"Specter count {specterCount} should be near 1000 (10%).");
            Assert.That(slimeCount, Is.InRange(4200, 4800), $"Slime count {slimeCount} should be near 4500 (45%).");
            Assert.That(skeletonCount, Is.InRange(4200, 4800), $"Skeleton count {skeletonCount} should be near 4500 (45%).");
            Assert.That(slimeCount + skeletonCount + specterCount, Is.EqualTo(totalIterations));
        }

        [TestCase("forest")]
        [TestCase("town")]
        [TestCase("dungeon_graveyard")]
        [TestCase("")]
        [TestCase(null)]
        public void Selection_ExcludesSpecter_WhenEnvironmentIsNotCatacombs(string environment)
        {
            var candidates = CreateStandardCandidates();
            var random = new Random(12345);

            int specterCount = 0;
            const int totalIterations = 10000;

            for (int i = 0; i < totalIterations; i++)
            {
                bool success = WeightedEnemySelector.TrySelect(
                    candidates,
                    currentEnvironment: environment,
                    remainingThreatBudget: 10,
                    random,
                    out var selected);

                Assert.That(success, Is.True);
                if (selected.EnemyId == "specter")
                {
                    specterCount++;
                }
            }

            Assert.That(specterCount, Is.EqualTo(0), $"Specter must never spawn outside catacombs (tested '{environment}').");
        }

        [Test]
        public void Selection_CaseInsensitiveEnvironmentMatch()
        {
            var candidates = CreateStandardCandidates();
            var random = new Random(999);

            bool success = WeightedEnemySelector.TrySelect(
                candidates,
                currentEnvironment: "CATACOMBS",
                remainingThreatBudget: 10,
                random,
                out var selected);

            Assert.That(success, Is.True);
        }

        [Test]
        public void Selection_ExcludesCandidatesExceedingRemainingThreatBudget()
        {
            var candidates = CreateStandardCandidates();
            var random = new Random(777);

            const int totalIterations = 1000;
            for (int i = 0; i < totalIterations; i++)
            {
                // Budget is 1: Specter costs 2, so Specter must NEVER be selected
                bool success = WeightedEnemySelector.TrySelect(
                    candidates,
                    currentEnvironment: "catacombs",
                    remainingThreatBudget: 1,
                    random,
                    out var selected);

                Assert.That(success, Is.True);
                Assert.That(selected.ThreatCost, Is.LessThanOrEqualTo(1));
                Assert.That(selected.EnemyId, Is.Not.EqualTo("specter"));
            }
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(-10)]
        public void Selection_ReturnsFalse_WhenRemainingBudgetIsZeroOrNegative(int budget)
        {
            var candidates = CreateStandardCandidates();
            var random = new Random(100);

            bool success = WeightedEnemySelector.TrySelect(
                candidates,
                currentEnvironment: "catacombs",
                remainingThreatBudget: budget,
                random,
                out var selected);

            Assert.That(success, Is.False);
            Assert.That(selected.Item, Is.Null);
        }

        [Test]
        public void Selection_ReturnsFalse_WhenCandidateListIsEmptyOrNull()
        {
            var random = new Random(1);
            Assert.That(WeightedEnemySelector.TrySelect<string>(null, "catacombs", 5, random, out _), Is.False);
            Assert.That(WeightedEnemySelector.TrySelect(new List<EnemySpawnCandidate<string>>(), "catacombs", 5, random, out _), Is.False);
        }

        [Test]
        public void GenerateEncounter_ConsumesAndRespectsThreatBudgetLimit()
        {
            var candidates = CreateStandardCandidates();

            for (int seed = 1; seed <= 50; seed++)
            {
                var random = new Random(seed);
                int maxThreatBudget = 7;

                var encounter = WeightedEnemySelector.GenerateEncounter(
                    candidates,
                    currentEnvironment: "catacombs",
                    maxThreatBudget: maxThreatBudget,
                    maxEntityCount: 10,
                    random);

                Assert.That(encounter, Is.Not.Empty);

                int totalCost = 0;
                foreach (var enemy in encounter)
                {
                    totalCost += enemy.ThreatCost;
                }

                Assert.That(totalCost, Is.LessThanOrEqualTo(maxThreatBudget),
                    $"Encounter with seed {seed} exceeded threat budget {maxThreatBudget} (actual: {totalCost}).");
            }
        }

        [Test]
        public void GenerateEncounter_InNonCatacombs_NeverContainsSpecter()
        {
            var candidates = CreateStandardCandidates();
            var random = new Random(555);

            var encounter = WeightedEnemySelector.GenerateEncounter(
                candidates,
                currentEnvironment: "surface_forest",
                maxThreatBudget: 20,
                maxEntityCount: 15,
                random);

            Assert.That(encounter, Is.Not.Empty);
            foreach (var enemy in encounter)
            {
                Assert.That(enemy.EnemyId, Is.Not.EqualTo("specter"), "Encounter outside catacombs contained a specter!");
            }
        }
    }
}
