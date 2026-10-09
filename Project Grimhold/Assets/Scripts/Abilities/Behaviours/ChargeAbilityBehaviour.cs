using Fusion;
using UnityEngine;

/// <summary>
/// Charge (Embestida): a straight, fast forced displacement along the aim captured when the ability was
/// accepted. It ends at the full distance, at the first Environment obstacle, or at the first valid enemy,
/// which it damages and pushes through the existing damage pipeline. It never passes through an enemy.
/// The movement controller owns the displacement; this behavior only starts it, watches it and ends it.
/// Stun is deliberately not applied: no stun system exists yet.
/// </summary>
[DisallowMultipleComponent]
public sealed class ChargeAbilityBehaviour : AbilityExecutionBehaviour
{
    private const int HitBufferSize = 32;
    private const float MinimumSweepDistance = 0.0001f;

    [Header("Charge")]
    [SerializeField, Min(0.01f)] private float _distance = 4f;
    [SerializeField, Min(0.01f)] private float _speed = 18f;

    [Header("Impact")]
    [Tooltip("Zero is allowed: the enemy is still pushed, without damage.")]
    [SerializeField, Min(0f)] private float _damage = 5f;
    [SerializeField] private DamageType _damageType = DamageType.Physical;
    [SerializeField, Min(0f)] private float _knockbackForce = 8f;
    [Tooltip("Layers holding the enemies' damage colliders.")]
    [SerializeField] private LayerMask _targetLayerMask;

    private readonly RaycastHit2D[] _hits = new RaycastHit2D[HitBufferSize];
    private readonly ChargeHitCandidate[] _candidates = new ChargeHitCandidate[HitBufferSize];
    private readonly IDamageable[] _candidateDamageables = new IDamageable[HitBufferSize];

    private PlayerMovementNetworkController _movement;
    private Collider2D _collider;
    private IDamageResolver _damageResolver;
    private EntityRegistry _registry;
    private NetworkRunner _registryRunner;
    private uint _lastResolvedSequence;

    public override bool TryPlanStart(in AbilityExecutionContext context, out AbilityExecutionPlan plan)
    {
        // An unusable configuration is not a normal rejection: it yields an invalid plan (a configuration error).
        plan = new AbilityExecutionPlan(AbilityExecutionPhase.Idle);
        if (!IsConfigurationValid()) return true;
        ChargeRules.TryComputeDuration(_distance, _speed, out float seconds);
        plan = new AbilityExecutionPlan(AbilityExecutionPhase.Executing, seconds);
        return true;
    }

    public override void Begin(in AbilityExecutionContext context, in AbilityExecutionSnapshot snapshot)
    {
        if (!TryResolveReferences(context) || !IsConfigurationValid()) return;
        // The direction was captured with the sequence and never re-read, so later aiming cannot bend the charge.
        if (!_movement.TryBeginForcedDisplacement(snapshot.AimDirection, _speed))
        {
            Debug.LogError($"{nameof(ChargeAbilityBehaviour)} could not start the forced displacement; the charge completes without moving.", this);
        }
    }

    public override AbilityExecutionPlan Simulate(in AbilityExecutionContext context, in AbilityExecutionSnapshot snapshot)
    {
        var idle = new AbilityExecutionPlan(AbilityExecutionPhase.Idle);
        if (!TryResolveReferences(context)) return idle;

        bool active = _movement.IsForcedDisplacementActive;
        int hit = active && snapshot.Sequence != _lastResolvedSequence ? FindFirstEnemy(context) : -1;
        ChargeOutcome outcome = ChargeRules.Decide(active, hit >= 0, _movement.WasForcedDisplacementBlocked,
            snapshot.PhaseDeadline.Expired(context.Runner));
        if (outcome == ChargeOutcome.Continue) return new AbilityExecutionPlan(snapshot.Phase);

        if (outcome == ChargeOutcome.EnemyHit)
        {
            _lastResolvedSequence = snapshot.Sequence;
            ApplyImpact(context, snapshot, hit);
        }
        return idle;
    }

    public override bool CanInterrupt(AbilityExecutionStopReason reason) =>
        reason == AbilityExecutionStopReason.Knockback || reason == AbilityExecutionStopReason.Stun;

    public override void Stop(in AbilityExecutionContext context, in AbilityExecutionSnapshot snapshot,
        AbilityExecutionStopReason reason)
    {
        // Idempotent and unconditional: every stop reason releases the displacement.
        if (TryResolveReferences(context)) _movement.EndForcedDisplacement();
    }

    public override void Rebind(in AbilityExecutionContext context, in AbilityExecutionSnapshot snapshot) =>
        TryResolveReferences(context);

    private bool IsConfigurationValid()
    {
        if (ChargeRules.TryValidateConfiguration(_distance, _speed, _damage, _knockbackForce, _targetLayerMask.value,
                out string error)) return true;
        Debug.LogError($"{nameof(ChargeAbilityBehaviour)} configuration is invalid: {error}", this);
        return false;
    }

    private bool TryResolveReferences(in AbilityExecutionContext context)
    {
        PlayerCharacter character = context.Character;
        if (character == null) return false;
        if (_movement == null || _movement.gameObject != character.gameObject)
        {
            _movement = character.GetComponent<PlayerMovementNetworkController>();
            _collider = character.GetComponent<Collider2D>();
            _damageResolver = character.GetComponent<IDamageResolver>() ??
                              character.GetComponentInChildren<IDamageResolver>() ??
                              character.GetComponentInParent<IDamageResolver>();
        }
        if (_registry == null || _registryRunner != context.Runner)
        {
            _registry = context.Runner != null ? context.Runner.GetComponent<EntityRegistry>() : null;
            _registryRunner = context.Runner;
        }
        return _movement != null;
    }

    /// <summary>
    /// Sweeps the path the caster covered during the last motor step and returns the index of the first valid
    /// enemy in <see cref="_candidates"/>, or -1. The motor does not collide with entities, so this is the only
    /// place an enemy can stop the charge.
    /// </summary>
    private int FindFirstEnemy(in AbilityExecutionContext context)
    {
        if (_collider == null || _registry == null) return -1;
        Vector2 step = _movement.LastAppliedDisplacement;
        float length = step.magnitude;
        if (!(length > MinimumSweepDistance)) return -1;

        var filter = new ContactFilter2D { useLayerMask = true, useTriggers = true };
        filter.SetLayerMask(_targetLayerMask);
        Vector2 end = _collider.transform.TransformPoint(_collider.offset);
        int hitCount = Physics2D.BoxCast(end - step, _collider.bounds.size, 0f, step / length, filter, _hits, length);

        EntityId casterId = context.Character.Id;
        int candidateCount = 0;
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D collider = _hits[i].collider;
            if (collider == null || !_registry.TryGetEntityId(collider, out EntityId id) ||
                !_registry.IsDamageCollider(id, collider) ||
                !_registry.TryGetDamageable(id, out IDamageable damageable) ||
                !AbilityTargetPredicate.IsValidEnemy(casterId, damageable)) continue;
            _candidates[candidateCount] = new ChargeHitCandidate(id, _hits[i].distance, i);
            _candidateDamageables[candidateCount] = damageable;
            candidateCount++;
        }
        return ChargeRules.SelectFirstHit(_candidates, candidateCount);
    }

    private void ApplyImpact(in AbilityExecutionContext context, in AbilityExecutionSnapshot snapshot, int candidateIndex)
    {
        ChargeHitCandidate candidate = _candidates[candidateIndex];
        IDamageable target = _candidateDamageables[candidateIndex];
        Vector2 direction = snapshot.AimDirection.normalized;
        Vector2 hitPoint = _hits[candidate.HitIndex].collider.ClosestPoint(_collider.bounds.center);

        if (ChargeRules.UsesDamagePipeline(_damage))
        {
            if (_damageResolver == null)
            {
                Debug.LogError($"{nameof(ChargeAbilityBehaviour)} requires an {nameof(IDamageResolver)} on the caster to apply damage.", this);
                return;
            }
            _damageResolver.Resolve(new DamageRequest(context.Character.Id, candidate.Id, _damage, _damageType,
                direction, hitPoint, context.Runner.Tick.Raw, _knockbackForce));
        }
        else if (_knockbackForce > 0f && target is IKnockbackReceiver receiver)
        {
            receiver.ReceiveKnockback(direction, _knockbackForce);
        }
    }

#if UNITY_EDITOR
    private void Reset()
    {
        _targetLayerMask = LayerMask.GetMask("Character");
    }

    private void OnValidate()
    {
        _distance = Mathf.Max(0.01f, _distance);
        _speed = Mathf.Max(0.01f, _speed);
        _damage = Mathf.Max(0f, _damage);
        _knockbackForce = Mathf.Max(0f, _knockbackForce);
    }
#endif
}
