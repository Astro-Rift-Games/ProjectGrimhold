using System;
using System.Collections.Generic;

namespace Spawning
{
    /// <summary>
    /// Pure, deterministic weighted selection engine for enemy spawning.
    /// Evaluates environmental eligibility and threat budget constraints,
    /// ensuring fair and balanced enemy group compositions.
    /// </summary>
    public static class WeightedEnemySelector
    {
        public static bool TrySelect<T>(
            IReadOnlyList<EnemySpawnCandidate<T>> candidates,
            string currentEnvironment,
            int remainingThreatBudget,
            Random random,
            out EnemySpawnCandidate<T> selected)
        {
            selected = default;

            if (candidates == null || candidates.Count == 0 || remainingThreatBudget <= 0)
            {
                return false;
            }

            // 1. Filter candidates by environmental eligibility and remaining threat budget
            int totalWeight = 0;
            int eligibleCount = 0;

            for (int i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (candidate.Weight <= 0) continue;
                if (candidate.ThreatCost > remainingThreatBudget) continue;
                if (!candidate.IsEnvironmentEligible(currentEnvironment)) continue;

                totalWeight += candidate.Weight;
                eligibleCount++;
            }

            if (eligibleCount == 0 || totalWeight <= 0)
            {
                return false;
            }

            // 2. Cumulative roll
            int roll = (random != null) ? random.Next(totalWeight) : UnityEngine.Random.Range(0, totalWeight);
            int accumulated = 0;

            for (int i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (candidate.Weight <= 0) continue;
                if (candidate.ThreatCost > remainingThreatBudget) continue;
                if (!candidate.IsEnvironmentEligible(currentEnvironment)) continue;

                accumulated += candidate.Weight;
                if (roll < accumulated)
                {
                    selected = candidate;
                    return true;
                }
            }

            // Fallback (e.g. edge rounding or single candidate)
            for (int i = candidates.Count - 1; i >= 0; i--)
            {
                var candidate = candidates[i];
                if (candidate.Weight > 0 &&
                    candidate.ThreatCost <= remainingThreatBudget &&
                    candidate.IsEnvironmentEligible(currentEnvironment))
                {
                    selected = candidate;
                    return true;
                }
            }

            return false;
        }

        public static List<EnemySpawnCandidate<T>> GenerateEncounter<T>(
            IReadOnlyList<EnemySpawnCandidate<T>> candidates,
            string currentEnvironment,
            int maxThreatBudget,
            int maxEntityCount = int.MaxValue,
            Random random = null)
        {
            var result = new List<EnemySpawnCandidate<T>>();

            if (candidates == null || candidates.Count == 0 || maxThreatBudget <= 0)
            {
                return result;
            }

            int remainingBudget = maxThreatBudget;
            int countLimit = maxEntityCount > 0 ? maxEntityCount : int.MaxValue;

            while (remainingBudget > 0 && result.Count < countLimit)
            {
                if (!TrySelect(candidates, currentEnvironment, remainingBudget, random, out var chosen))
                {
                    // No further candidate fits within the remaining threat budget or environment
                    break;
                }

                result.Add(chosen);
                remainingBudget -= chosen.ThreatCost;
            }

            return result;
        }
    }
}
