using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// Trap (Trampa): an instant cast that places a caster-owned <see cref="NetworkTrap"/> ahead of the caster along the
/// aim captured when the ability was accepted. The placement must be free of ground-blocking colliders; otherwise
/// the start is a normal rejection (no trap, no payment, no sequence, no cooldown). Once spawned, the trap is an
/// independent entity: completing, interrupting or rebinding the cast never deletes it. The trap owns its trigger
/// and its effect parameters as networked state; this behaviour only validates, places and spawns it.
///
/// Residual risk: if the spawn fails after payment (for example spawns are blocked while the raid closes), the cost
/// and cooldown stay spent, the error is logged and the spawn is not retried.
/// </summary>
[DisallowMultipleComponent]
public sealed class TrapAbilityBehaviour : AbilityExecutionBehaviour
{
    private const int BlockerBufferSize = 4;
    private const int EvictionScratchCapacity = 8;

    [Header("Placement")]
    [Tooltip("Distance ahead of the caster, along the aim captured at the start.")]
    [SerializeField, Min(0.01f)] private float _placementDistance = 1.5f;
    [Tooltip("Radius of the footprint that must be free of ground-blocking colliders.")]
    [SerializeField, Min(0.01f)] private float _placementClearanceRadius = 0.25f;
    [Tooltip("Layers that make a position invalid: walls, props and anything else that blocks placing the entity.")]
    [SerializeField] private LayerMask _groundBlockingMask;

    [Header("Trap")]
    [SerializeField] private NetworkPrefabRef _trapPrefab;
    [SerializeField, Min(0.01f)] private float _lifetimeSeconds = 20f;
    [SerializeField, Min(1)] private int _maxTrapsPerCaster = 3;
    [SerializeField, Min(0.01f)] private float _triggerRadius = 0.75f;
    [Tooltip("Layers holding the creatures' damage colliders; only valid enemies trigger the trap.")]
    [SerializeField] private LayerMask _targetLayerMask;

    [Header("Effect")]
    [SerializeField, Min(0.01f)] private float _immobilizeSeconds = 3f;
    [Tooltip("Zero is allowed: the enemy is still immobilized, without damage.")]
    [SerializeField, Min(0f)] private float _periodicDamage = 2f;
    [SerializeField, Min(0.01f)] private float _tickIntervalSeconds = 1f;
    [SerializeField] private DamageType _damageType = DamageType.Physical;
    [SerializeField] private bool _purifiable = true;

    private readonly Collider2D[] _blockers = new Collider2D[BlockerBufferSize];
    private readonly RaycastHit2D[] _lineHits = new RaycastHit2D[BlockerBufferSize];
    private readonly List<NetworkTrap> _evictionScratch = new(EvictionScratchCapacity);
    private uint _lastResolvedSequence;

    public override bool TryPlanStart(in AbilityExecutionContext context, out AbilityExecutionPlan plan)
    {
        // An unusable configuration is not a normal rejection: it yields an invalid plan (a configuration error).
        // An invalid position is the normal rejection and must stay free of side effects.
        plan = new AbilityExecutionPlan(AbilityExecutionPhase.Idle);
        if (!IsConfigurationValid()) return true;
        if (context.Character == null) return false;

        Vector2 origin = context.Character.transform.position;
        if (!TrapRules.TryResolvePlacement(origin, context.AimDirection, _placementDistance, out Vector2 position) ||
            !IsPlacementFree(origin, position)) return false;

        // Instant cast: no preparation or lasting execution; Simulate completes it on the next tick.
        plan = new AbilityExecutionPlan(AbilityExecutionPhase.Executing);
        return true;
    }

    public override void Begin(in AbilityExecutionContext context, in AbilityExecutionSnapshot snapshot)
    {
        if (context.Runner == null || !context.Runner.IsForward || context.Character == null) return;
        // One spawn per accepted sequence, whatever re-enters Begin.
        if (snapshot.Sequence == _lastResolvedSequence) return;
        _lastResolvedSequence = snapshot.Sequence;
        if (!IsConfigurationValid()) return;

        // Recomputed from the aim captured with the sequence, by the same rule used to validate the start.
        Vector2 origin = context.Character.transform.position;
        if (!TrapRules.TryResolvePlacement(origin, snapshot.AimDirection, _placementDistance, out Vector2 position))
        {
            Debug.LogError($"{nameof(TrapAbilityBehaviour)} has no usable captured aim; no trap is placed.", this);
            return;
        }

        NetworkRunner runner = context.Runner;
        EntityId casterId = context.Character.Id;
        NetworkTrap.TryEvictForNewTrap(runner, casterId, _maxTrapsPerCaster, _evictionScratch);

        NetworkSpawnStatus status = runner.TrySpawn(_trapPrefab, out NetworkObject trapObject, position,
            Quaternion.identity, inputAuthority: null, onBeforeSpawned: (_, instance) =>
            {
                NetworkTrap trap = instance != null ? instance.GetComponent<NetworkTrap>() : null;
                if (trap == null) return;
                trap.InitializeNetworkState(casterId, _lifetimeSeconds, _triggerRadius, _targetLayerMask.value,
                    _immobilizeSeconds, _periodicDamage, _tickIntervalSeconds, _damageType, _purifiable);
            });

        if (status != NetworkSpawnStatus.Spawned || trapObject == null || trapObject.GetComponent<NetworkTrap>() == null)
        {
            Debug.LogError($"{nameof(TrapAbilityBehaviour)} could not spawn the trap (status {status}); cost and cooldown stay spent.", this);
        }
    }

    public override AbilityExecutionPlan Simulate(in AbilityExecutionContext context, in AbilityExecutionSnapshot snapshot) =>
        new AbilityExecutionPlan(AbilityExecutionPhase.Idle);

    public override bool CanInterrupt(AbilityExecutionStopReason reason) => false;

    public override void Stop(in AbilityExecutionContext context, in AbilityExecutionSnapshot snapshot,
        AbilityExecutionStopReason reason)
    {
        // Nothing to release: the placed trap is an independent entity and outlives the cast.
    }

    public override void Rebind(in AbilityExecutionContext context, in AbilityExecutionSnapshot snapshot)
    {
        // No local references to rebuild; a restored or rebound execution never spawns a second trap.
    }

    private bool IsConfigurationValid()
    {
        if (!TrapRules.TryValidateConfiguration(_placementDistance, _lifetimeSeconds, _maxTrapsPerCaster, _triggerRadius,
                _immobilizeSeconds, _periodicDamage, _tickIntervalSeconds, out string error) ||
            !TrapRules.TryValidatePlacementProbe(_placementClearanceRadius, _groundBlockingMask.value, out error))
        {
            Debug.LogError($"{nameof(TrapAbilityBehaviour)} configuration is invalid: {error}", this);
            return false;
        }
        if (!_trapPrefab.IsValid)
        {
            Debug.LogError($"{nameof(TrapAbilityBehaviour)} configuration is invalid: the trap prefab is not assigned.", this);
            return false;
        }
        return true;
    }

    /// <summary>
    /// A position is valid when its footprint overlaps no ground-blocking collider and nothing blocking lies on the
    /// straight line from the caster, so a trap is never placed behind a wall or outside the map boundary.
    /// Walkable floor has no collider of its own (the Floor tilemap is render-only), so "free of blockers" is the
    /// check the project's colliders can express.
    /// </summary>
    private bool IsPlacementFree(Vector2 origin, Vector2 position)
    {
        Physics2D.SyncTransforms();
        var filter = new ContactFilter2D { useLayerMask = true, useTriggers = false };
        filter.SetLayerMask(_groundBlockingMask);

        bool blocked = Physics2D.OverlapCircle(position, _placementClearanceRadius, filter, _blockers) > 0 ||
                       Physics2D.Linecast(origin, position, filter, _lineHits) > 0;
        System.Array.Clear(_blockers, 0, _blockers.Length);
        return !blocked;
    }

#if UNITY_EDITOR
    private void Reset()
    {
        _groundBlockingMask = LayerMask.GetMask("WorldCollision", "Obstacles");
        _targetLayerMask = LayerMask.GetMask("Character");
    }

    private void OnValidate()
    {
        _placementDistance = Mathf.Max(0.01f, _placementDistance);
        _placementClearanceRadius = Mathf.Max(0.01f, _placementClearanceRadius);
        _lifetimeSeconds = Mathf.Max(0.01f, _lifetimeSeconds);
        _maxTrapsPerCaster = Mathf.Max(1, _maxTrapsPerCaster);
        _triggerRadius = Mathf.Max(0.01f, _triggerRadius);
        _immobilizeSeconds = Mathf.Max(0.01f, _immobilizeSeconds);
        _periodicDamage = Mathf.Max(0f, _periodicDamage);
        _tickIntervalSeconds = Mathf.Max(0.01f, _tickIntervalSeconds);
    }
#endif
}
