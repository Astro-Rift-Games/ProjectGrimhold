using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectGrimhold.Gameplay.Enemies.Specter
{
    /// <summary>
    /// Modelo autoritativo de agresión por daño efectivo acumulado (Hate List) para el Espectro (US-78 / TASK-274).
    /// </summary>
    public sealed class SpecterHateList
    {
        public readonly struct CandidateTarget
        {
            public readonly EntityId Id;
            public readonly Vector2 Position;

            public CandidateTarget(EntityId id, Vector2 position)
            {
                Id = id;
                Position = position;
            }
        }

        private readonly Dictionary<EntityId, float> _accumulatedDamage = new();

        public int Count => _accumulatedDamage.Count;

        /// <summary>
        /// Registra daño efectivo acumulado de un atacante (§6).
        /// </summary>
        public void RecordDamage(EntityId attacker, float effectiveDamage)
        {
            if (attacker.Value == 0 || effectiveDamage <= 0f) return;

            _accumulatedDamage.TryGetValue(attacker, out float current);
            _accumulatedDamage[attacker] = current + effectiveDamage;
        }

        /// <summary>
        /// Obtiene el daño acumulado por una entidad específica.
        /// </summary>
        public float GetDamage(EntityId attacker)
        {
            return _accumulatedDamage.TryGetValue(attacker, out float damage) ? damage : 0f;
        }

        /// <summary>
        /// Selecciona al objetivo prioritario (§6):
        /// 1. Si hay jugadores con daño acumulado, selecciona al de mayor daño (desempate por cercanía a <paramref name="specterPosition"/>).
        /// 2. Si ninguno tiene daño acumulado, selecciona por proximidad directa.
        /// 3. Si ninguno es elegible, retorna EntityId default (0).
        /// </summary>
        public EntityId SelectTarget(
            Vector2 specterPosition,
            IReadOnlyList<EntityId> candidates,
            Func<EntityId, (bool isEligible, Vector2 position)> query)
        {
            if (candidates == null || candidates.Count == 0 || query == null)
            {
                return default;
            }

            EntityId bestTarget = default;
            float highestDamage = -1f;
            float shortestDistanceSqr = float.MaxValue;
            bool foundDamagingTarget = false;

            for (int i = 0; i < candidates.Count; i++)
            {
                EntityId id = candidates[i];
                if (id.Value == 0) continue;

                var (isEligible, pos) = query(id);
                if (!isEligible) continue;

                float distSqr = (pos - specterPosition).sqrMagnitude;
                _accumulatedDamage.TryGetValue(id, out float dmg);

                if (dmg > 0f)
                {
                    if (!foundDamagingTarget || dmg > highestDamage || (Mathf.Approximately(dmg, highestDamage) && distSqr < shortestDistanceSqr))
                    {
                        foundDamagingTarget = true;
                        highestDamage = dmg;
                        shortestDistanceSqr = distSqr;
                        bestTarget = id;
                    }
                }
                else if (!foundDamagingTarget)
                {
                    // Si aún no hay nadie con daño, desempatar por distancia mínima
                    if (distSqr < shortestDistanceSqr)
                    {
                        shortestDistanceSqr = distSqr;
                        bestTarget = id;
                    }
                }
            }

            return bestTarget;
        }

        /// <summary>
        /// Remueve entidades que hayan dejado de ser válidas o hayan muerto.
        /// </summary>
        public void Prune(Func<EntityId, bool> isEligible)
        {
            if (isEligible == null) return;

            var toRemove = new List<EntityId>();
            foreach (var kvp in _accumulatedDamage)
            {
                if (!isEligible(kvp.Key))
                {
                    toRemove.Add(kvp.Key);
                }
            }

            for (int i = 0; i < toRemove.Count; i++)
            {
                _accumulatedDamage.Remove(toRemove[i]);
            }
        }

        /// <summary>
        /// Selecciona al objetivo prioritario (§6) a partir de una lista de candidatos directos.
        /// </summary>
        public bool SelectTarget(IReadOnlyList<CandidateTarget> candidates, Vector2 specterPosition, out EntityId selectedId)
        {
            selectedId = default;
            if (candidates == null || candidates.Count == 0) return false;

            float highestDamage = -1f;
            float shortestDistanceSqr = float.MaxValue;
            bool foundDamagingTarget = false;

            for (int i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (candidate.Id.Value == 0) continue;

                float distSqr = (candidate.Position - specterPosition).sqrMagnitude;
                _accumulatedDamage.TryGetValue(candidate.Id, out float dmg);

                if (dmg > 0f)
                {
                    if (!foundDamagingTarget || dmg > highestDamage || (Mathf.Approximately(dmg, highestDamage) && distSqr < shortestDistanceSqr))
                    {
                        foundDamagingTarget = true;
                        highestDamage = dmg;
                        shortestDistanceSqr = distSqr;
                        selectedId = candidate.Id;
                    }
                }
                else if (!foundDamagingTarget)
                {
                    if (distSqr < shortestDistanceSqr)
                    {
                        shortestDistanceSqr = distSqr;
                        selectedId = candidate.Id;
                    }
                }
            }

            return selectedId.Value != 0;
        }

        /// <summary>
        /// Remueve una entidad específica de la tabla de odio.
        /// </summary>
        public bool Remove(EntityId attacker)
        {
            return _accumulatedDamage.Remove(attacker);
        }

        /// <summary>
        /// Reinicia la tabla de agresión (al completar retorno o finalizar encuentro).
        /// </summary>
        public void Reset()
        {
            _accumulatedDamage.Clear();
        }

        /// <summary>
        /// Limpia todas las entradas de la tabla de odio (alias de Reset).
        /// </summary>
        public void Clear()
        {
            Reset();
        }
    }
}
