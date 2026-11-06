using System;
using UnityEngine;

namespace ProjectGrimhold.Gameplay.Enemies.Specter
{
    /// <summary>
    /// Algoritmo geométrico de búsqueda y validación de destino para el Desplazamiento Espectral (US-78 / TASK-274 / §7).
    /// </summary>
    public static class SpecterTeleportRules
    {
        public const int DefaultSampleCount = 12;

        /// <summary>
        /// Busca un punto válido en un anillo alrededor del objetivo que respete el radio de Leash de la Home Area.
        /// </summary>
        public static bool TryFindDestination(
            Vector2 targetPos,
            Vector2 homeAreaPos,
            float leashRadius,
            float minRadius,
            float maxRadius,
            Func<Vector2, bool> isPointValid,
            out Vector2 destination,
            int sampleCount = DefaultSampleCount)
        {
            destination = homeAreaPos;
            if (minRadius <= 0f) minRadius = 1f;
            if (maxRadius < minRadius) maxRadius = minRadius + 1f;
            if (sampleCount <= 0) sampleCount = DefaultSampleCount;

            // 1. Muestreo angular en el anillo configurado
            float angleStep = (Mathf.PI * 2f) / sampleCount;
            float radiusRange = maxRadius - minRadius;

            for (int i = 0; i < sampleCount; i++)
            {
                // Alternar radios para cubrir la banda interior y exterior
                float t = (i % 2 == 0) ? 0.3f : 0.85f;
                float radius = minRadius + radiusRange * t;
                float angle = i * angleStep;

                Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                Vector2 candidate = targetPos + offset;

                // Verificar restricción estricta de Leash desde Home Area
                if (leashRadius > 0f && Vector2.Distance(candidate, homeAreaPos) > leashRadius)
                {
                    continue;
                }

                // Verificar navegabilidad y colisiones
                if (isPointValid == null || isPointValid(candidate))
                {
                    destination = candidate;
                    return true;
                }
            }

            // 2. Fallback progresivo: intentar en dirección al Home Area dentro del rango
            Vector2 dirToHome = (homeAreaPos - targetPos).normalized;
            if (dirToHome != Vector2.zero)
            {
                Vector2 fallbackCandidate = targetPos + dirToHome * minRadius;
                if (leashRadius <= 0f || Vector2.Distance(fallbackCandidate, homeAreaPos) <= leashRadius)
                {
                    if (isPointValid == null || isPointValid(fallbackCandidate))
                    {
                        destination = fallbackCandidate;
                        return true;
                    }
                }
            }

            // 3. Fallback final seguro: Home Area si es válida
            if (isPointValid == null || isPointValid(homeAreaPos))
            {
                destination = homeAreaPos;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Busca la mejor posición de teletransporte respetando radio de leash y obstáculos de línea de visión.
        /// </summary>
        public static Vector2 FindDestination(
            Vector2 originPos,
            Vector2 targetPos,
            Vector2 homeAreaPos,
            float leashRadius,
            Func<Vector2, Vector2, bool> linecastObstacleCheck)
        {
            Func<Vector2, bool> isValid = pt =>
            {
                if (linecastObstacleCheck != null && linecastObstacleCheck(originPos, pt))
                {
                    return false;
                }
                return true;
            };

            if (TryFindDestination(
                targetPos,
                homeAreaPos,
                leashRadius,
                SpecterRules.MinTeleportDistance,
                SpecterRules.MaxTeleportDistance,
                isValid,
                out Vector2 destination))
            {
                return destination;
            }

            return homeAreaPos;
        }
    }
}
