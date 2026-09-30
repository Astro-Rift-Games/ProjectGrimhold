using UnityEngine;
using System.Collections.Generic;

namespace Spawning
{
    public enum ReinforcementRejection
    {
        None,
        TooCloseToPlayer
    }

    public static class ReinforcementSpawnPlanner
    {
        public static bool IsPointValid(
            Transform point, 
            IEnumerable<Vector3> playerPositions, 
            float minDistanceToPlayer, 
            out ReinforcementRejection rejectionReason)
        {
            if (point == null)
            {
                rejectionReason = ReinforcementRejection.None;
                return false;
            }

            Vector3 pointPos = point.position;
            foreach (var playerPos in playerPositions)
            {
                if (Vector3.Distance(pointPos, playerPos) < minDistanceToPlayer)
                {
                    rejectionReason = ReinforcementRejection.TooCloseToPlayer;
                    return false;
                }
            }

            rejectionReason = ReinforcementRejection.None;
            return true;
        }

        public static Transform EvaluateAndSelectPoint(
            IReadOnlyList<Transform> availablePoints, 
            IEnumerable<Vector3> playerPositions, 
            float minDistanceToPlayer)
        {
            if (availablePoints == null || availablePoints.Count == 0)
            {
                return null;
            }

            List<Transform> validPoints = new List<Transform>();
            
            foreach (var point in availablePoints)
            {
                if (IsPointValid(point, playerPositions, minDistanceToPlayer, out _))
                {
                    validPoints.Add(point);
                }
            }

            if (validPoints.Count == 0)
            {
                return null;
            }

            return validPoints[Random.Range(0, validPoints.Count)];
        }
    }
}
