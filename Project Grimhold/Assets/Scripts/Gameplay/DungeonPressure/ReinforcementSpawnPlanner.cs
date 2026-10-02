using UnityEngine;
using System.Collections.Generic;

namespace Spawning
{
    public enum ReinforcementRejection
    {
        None,
        Disabled,
        IntervalNotElapsed,
        PopulationBudgetExhausted,
        GlobalCapReached,
        NoReinforcementPoints,
        AllPointsTooCloseToPlayer,
        SpawnFailed
    }

    public struct ReinforcementPlan
    {
        public int Count;
        public ReinforcementRejection Rejection;
    }

    public static class ReinforcementSpawnPlanner
    {
        public static ReinforcementPlan Plan(
            ReinforcementPolicy policy, 
            int capacityBudget, 
            int capacityGlobal, 
            bool intervalElapsed, 
            bool hasValidPoints)
        {
            if (policy.PopulationBudget <= 0)
                return new ReinforcementPlan { Count = 0, Rejection = ReinforcementRejection.Disabled };

            if (!intervalElapsed)
                return new ReinforcementPlan { Count = 0, Rejection = ReinforcementRejection.IntervalNotElapsed };

            if (capacityBudget <= 0)
                return new ReinforcementPlan { Count = 0, Rejection = ReinforcementRejection.PopulationBudgetExhausted };

            if (capacityGlobal <= 0)
                return new ReinforcementPlan { Count = 0, Rejection = ReinforcementRejection.GlobalCapReached };

            if (!hasValidPoints)
                return new ReinforcementPlan { Count = 0, Rejection = ReinforcementRejection.NoReinforcementPoints };

            int cost = 1; // 1 for now (EnemyThreatCost)
            int affordableCount = capacityBudget / cost;
            int count = Mathf.Min(policy.MaxSpawnsPerAttempt, affordableCount, capacityGlobal);

            return new ReinforcementPlan { Count = count, Rejection = ReinforcementRejection.None };
        }

        public static bool IsPointValid(
            Transform point, 
            IEnumerable<Vector3> playerPositions, 
            float minDistanceToPlayer)
        {
            if (point == null)
            {
                return false;
            }

            Vector3 pointPos = point.position;
            foreach (var playerPos in playerPositions)
            {
                if (Vector3.Distance(pointPos, playerPos) < minDistanceToPlayer)
                {
                    return false;
                }
            }

            return true;
        }

        public static Transform EvaluateAndSelectPoint(
            IReadOnlyList<Transform> availablePoints, 
            IEnumerable<Vector3> playerPositions, 
            float minDistanceToPlayer,
            List<Transform> validPointsBuffer)
        {
            if (availablePoints == null || availablePoints.Count == 0 || validPointsBuffer == null)
            {
                return null;
            }

            validPointsBuffer.Clear();
            
            foreach (var point in availablePoints)
            {
                if (IsPointValid(point, playerPositions, minDistanceToPlayer))
                {
                    validPointsBuffer.Add(point);
                }
            }

            if (validPointsBuffer.Count == 0)
            {
                return null;
            }

            return validPointsBuffer[Random.Range(0, validPointsBuffer.Count)];
        }
    }
}
