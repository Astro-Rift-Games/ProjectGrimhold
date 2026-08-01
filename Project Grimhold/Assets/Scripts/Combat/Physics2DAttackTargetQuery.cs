using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// Implementation of IAttackTargetQuery based on Physics2D and EntityRegistry.
/// </summary>
[DisallowMultipleComponent]
public sealed class Physics2DAttackTargetQuery : NetworkBehaviour, IAttackTargetQuery
{
    [Header("Performance Configuration")]
    [SerializeField]
    private int _colliderBufferSize = 64;

    private Collider2D[] _colliderBuffer;
    private EntityRegistry _registry;

    // Internal struct to perform deduplication and sorting without allocations
    private struct CandidateTarget
    {
        public EntityId Id;
        public IDamageable Damageable;
        public Vector2 HitPoint;
        public float SqrDistance;
    }

    private readonly List<CandidateTarget> _candidates = new();
    private readonly List<AttackTarget> _results = new();
    private readonly Dictionary<EntityId, int> _entityIdToCandidateIndex = new();

    private void Awake()
    {
        _colliderBuffer = new Collider2D[_colliderBufferSize];
    }

    public override void Spawned()
    {
        _registry = Runner.GetComponent<EntityRegistry>();
        if (_registry == null)
        {
            Debug.LogError($"{nameof(Physics2DAttackTargetQuery)}: EntityRegistry component was not found on the NetworkRunner GameObject.", this);
        }
    }

    /// <summary>
    /// Queries targets inside the melee attack circular area.
    /// Deduplicates by EntityId, sorts by distance to the origin, and limits to MaximumTargets.
    /// </summary>
    public IReadOnlyList<AttackTarget> FindTargets(in AttackTargetQuery query)
    {
        _results.Clear();

        if (_registry == null)
        {
            return _results;
        }

        Vector2 attackCenter = query.Origin + query.Direction * query.Range;

        // Filter configuration for OverlapCircle
        ContactFilter2D filter = new ContactFilter2D();
        filter.useLayerMask = true;
        filter.SetLayerMask(query.TargetLayerMask);
        filter.useTriggers = true; // Allow trigger colliders based on configuration

        int hitCount = Physics2D.OverlapCircle(
            attackCenter,
            query.Radius,
            filter,
            _colliderBuffer
        );

        _candidates.Clear();
        _entityIdToCandidateIndex.Clear();

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D col = _colliderBuffer[i];
            if (col == null)
            {
                continue;
            }

            // Retrieve EntityId from registry (avoiding global component searches)
            if (!_registry.TryGetEntityId(col, out EntityId targetId))
            {
                continue;
            }

            if (!_registry.IsDamageCollider(targetId, col))
            {
                continue;
            }

            // Exclude the attacker
            if (targetId == query.AttackerId)
            {
                continue;
            }

            // Retrieve IDamageable to validate alive status and damage availability
            if (!_registry.TryGetDamageable(targetId, out IDamageable damageable))
            {
                continue;
            }

            // Exclude if cannot receive damage
            if (!damageable.CanReceiveDamage)
            {
                continue;
            }

            // Exclude dead characters
            if (damageable is ICharacter character && !character.IsAlive)
            {
                continue;
            }

            // Hit point: closest point to the attack origin on the collider
            Vector2 hitPoint = col.ClosestPoint(query.Origin);

            float sqrDistance = (hitPoint - query.Origin).sqrMagnitude;

            // Deduplicate before sorting: retain the closest hit per EntityId
            if (_entityIdToCandidateIndex.TryGetValue(targetId, out int existingIndex))
            {
                if (sqrDistance < _candidates[existingIndex].SqrDistance)
                {
                    _candidates[existingIndex] = new CandidateTarget
                    {
                        Id = targetId,
                        Damageable = damageable,
                        HitPoint = hitPoint,
                        SqrDistance = sqrDistance
                    };
                }
            }
            else
            {
                _entityIdToCandidateIndex[targetId] = _candidates.Count;
                _candidates.Add(new CandidateTarget
                {
                    Id = targetId,
                    Damageable = damageable,
                    HitPoint = hitPoint,
                    SqrDistance = sqrDistance
                });
            }
        }

        // Clear the processed colliders buffer
        Array.Clear(_colliderBuffer, 0, hitCount);
        _entityIdToCandidateIndex.Clear();

        if (_candidates.Count == 0)
        {
            return _results;
        }

        // Sort by closest squared distance to origin. Stable tie-break using EntityId.
        // We only sort after processing and deduplicating all hits.
        _candidates.Sort(CompareCandidates);

        // Limit maximum selected target count after candidate deduplication and sorting
        int targetsToTake = Mathf.Min(_candidates.Count, query.MaximumTargets);
        for (int i = 0; i < targetsToTake; i++)
        {
            CandidateTarget candidate = _candidates[i];
            _results.Add(new AttackTarget(candidate.Id, candidate.HitPoint));
        }

        return _results;
    }

    private static int CompareCandidates(CandidateTarget a, CandidateTarget b)
    {
        int distanceCompare = a.SqrDistance.CompareTo(b.SqrDistance);
        if (distanceCompare != 0)
        {
            return distanceCompare;
        }
        return a.Id.Value.CompareTo(b.Id.Value);
    }
}
