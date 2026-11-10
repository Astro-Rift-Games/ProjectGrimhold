using System;
using UnityEngine;

namespace ProjectGrimhold.Gameplay.Enemies.Specter
{
    /// <summary>
    /// Algoritmo geométrico de búsqueda y validación de destino para el Desplazamiento Espectral (US-78 / TASK-274 / TASK-285 / §7).
    /// </summary>
    public static class SpecterTeleportRules
    {
        public const int DefaultSampleCount = 16;

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

            return false;
        }

        /// <summary>
        /// Busca la mejor posición de teletransporte sin exigir línea de visión previa desde el origen,
        /// con ensanchado progresivo de franja anular y repliegue seguro a Home Area ante bloqueo (TASK-285).
        /// </summary>
        public static Vector2 FindDestination(
            Vector2 originPos,
            Vector2 targetPos,
            Vector2 homeAreaPos,
            float leashRadius,
            Func<Vector2, bool> isPointBlocked)
        {
            Func<Vector2, bool> isValid = pt => isPointBlocked == null || !isPointBlocked(pt);

            // Franjas de búsqueda progresivas (§7 / TASK-285):
            // Banda 0: Anillo estándar inicial [3.0m, 5.0m]
            // Banda 1: Ensanchado moderado [5.0m, 7.5m]
            // Banda 2: Ensanchado máximo [7.5m, 10.0m]
            (float min, float max)[] searchBands = new[]
            {
                (SpecterRules.MinTeleportDistance, SpecterRules.MaxTeleportDistance),
                (SpecterRules.MaxTeleportDistance, SpecterRules.MaxTeleportDistance * 1.5f),
                (SpecterRules.MaxTeleportDistance * 1.5f, SpecterRules.MaxTeleportDistance * 2.0f)
            };

            for (int b = 0; b < searchBands.Length; b++)
            {
                if (TryFindDestination(
                    targetPos,
                    homeAreaPos,
                    leashRadius,
                    searchBands[b].min,
                    searchBands[b].max,
                    isValid,
                    out Vector2 destination,
                    DefaultSampleCount))
                {
                    return destination;
                }
            }

            // Fallback seguro: retornar a Home Area ante fallo total de ubicación
            return homeAreaPos;
        }

        /// <summary>
        /// Sobrecarga compatible para comprobaciones basadas en trazado de línea.
        /// </summary>
        public static Vector2 FindDestination(
            Vector2 originPos,
            Vector2 targetPos,
            Vector2 homeAreaPos,
            float leashRadius,
            Func<Vector2, Vector2, bool> linecastObstacleCheck)
        {
            return FindDestination(
                originPos,
                targetPos,
                homeAreaPos,
                leashRadius,
                pt => linecastObstacleCheck != null && linecastObstacleCheck(originPos, pt));
        }
    }
}
