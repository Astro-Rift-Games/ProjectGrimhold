using System.Collections.Generic;
using UnityEngine;

namespace Grimhold.Combat.Presentation
{
    public sealed class CombatVfxPool : MonoBehaviour
    {
        private readonly Dictionary<CombatVfxDefinition, Queue<CombatVfxInstance>> _pools = new();

        public void Prewarm(CombatVfxDefinition definition)
        {
            if (definition == null || definition.Prefab == null) return;

            if (!_pools.TryGetValue(definition, out Queue<CombatVfxInstance> pool))
            {
                pool = new Queue<CombatVfxInstance>(definition.PoolCapacity);
                _pools[definition] = pool;

                for (int i = 0; i < definition.PoolCapacity; i++)
                {
                    pool.Enqueue(CreateInstance(definition));
                }
            }
        }

        public void Play(CombatVfxDefinition definition, Vector3 position, Quaternion rotation, bool mirror, Color tint, Transform parent = null)
        {
            if (definition == null || definition.Prefab == null) return;

            if (!_pools.TryGetValue(definition, out Queue<CombatVfxInstance> pool))
            {
                pool = new Queue<CombatVfxInstance>(definition.PoolCapacity);
                _pools[definition] = pool;
            }

            CombatVfxInstance instance = pool.Count > 0 ? pool.Dequeue() : null;
            if (instance == null)
            {
                // To avoid dynamically allocating during hot path, we optionally skip 
                // playing VFX if the pool is exhausted, per TASK-173 constraints.
                // Or we can just log a warning and return.
                Debug.LogWarning($"[CombatVfxPool] Pool exhausted for {definition.name}. Skipping VFX.");
                return;
            }

            instance.Play(
                onComplete: ReturnToPool,
                position,
                rotation,
                mirror,
                tint,
                definition.SortingOrder,
                parent
            );

            void ReturnToPool(CombatVfxInstance returnedInstance)
            {
                returnedInstance.transform.SetParent(transform);
                if (_pools.TryGetValue(definition, out Queue<CombatVfxInstance> targetPool))
                {
                    targetPool.Enqueue(returnedInstance);
                }
                else
                {
                    Destroy(returnedInstance.gameObject);
                }
            }
        }

        private CombatVfxInstance CreateInstance(CombatVfxDefinition definition)
        {
            CombatVfxInstance instance = Instantiate(definition.Prefab, transform);
            instance.gameObject.SetActive(false);
            return instance;
        }
        
        private void OnDestroy()
        {
            _pools.Clear();
        }
    }
}
