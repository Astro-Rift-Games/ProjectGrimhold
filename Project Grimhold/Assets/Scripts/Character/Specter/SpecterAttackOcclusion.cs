using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectGrimhold.Gameplay.Enemies.Specter
{
    /// <summary>
    /// Lógica de evaluación y oclusión geométrica para el ataque mágico de área del Espectro (US-78 / TASK-273 / §4).
    /// </summary>
    public static class SpecterAttackOcclusion
    {
        /// <summary>
        /// Comprueba si existe un obstáculo sólido entre el centro de la detonación y el objetivo.
        /// </summary>
        public static bool IsOccludedByWall(
            Vector2 circleCenter,
            Vector2 targetPosition,
            Func<Vector2, Vector2, bool> linecastObstacleCheck)
        {
            if (linecastObstacleCheck == null) return false;
            if (Vector2.Distance(circleCenter, targetPosition) < 0.001f) return false;

            return linecastObstacleCheck(circleCenter, targetPosition);
        }

        /// <summary>
        /// Comprueba oclusión mediante Physics2D.Linecast contra la capa de obstáculos.
        /// </summary>
        public static bool IsOccludedByWall(Vector2 circleCenter, Vector2 targetPosition, LayerMask obstacleLayerMask)
        {
            if (Vector2.Distance(circleCenter, targetPosition) < 0.001f) return false;

            RaycastHit2D hit = Physics2D.Linecast(circleCenter, targetPosition, obstacleLayerMask);
            return hit.collider != null;
        }

        /// <summary>
        /// Evalúa qué entidades dentro del radio de 1.5 unidades reciben daño, filtrando aquellas ocluidas por paredes (§4).
        /// </summary>
        public static List<EntityId> ResolveAffectedTargets(
            Vector2 center,
            float radius,
            IReadOnlyList<(EntityId id, Vector2 position)> candidateEntities,
            Func<Vector2, Vector2, bool> linecastObstacleCheck)
        {
            var affected = new List<EntityId>();
            if (candidateEntities == null || candidateEntities.Count == 0 || radius <= 0f)
            {
                return affected;
            }

            float radiusSqr = radius * radius;
            for (int i = 0; i < candidateEntities.Count; i++)
            {
                var (id, pos) = candidateEntities[i];
                if (id.Value == 0) continue;

                float distSqr = (pos - center).sqrMagnitude;
                if (distSqr <= radiusSqr)
                {
                    if (!IsOccludedByWall(center, pos, linecastObstacleCheck))
                    {
                        affected.Add(id);
                    }
                }
            }

            return affected;
        }
    }
}
