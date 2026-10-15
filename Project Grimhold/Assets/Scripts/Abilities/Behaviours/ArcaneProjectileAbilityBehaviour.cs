using Fusion;
using UnityEngine;

/// <summary>
/// PvE Arcane Projectile: prepare using the accepted facing, then spawn an independent shared projectile once.
/// Payment, deadlines and interruption belong to the runtime; flight belongs to NetworkProjectile.
/// </summary>
[DisallowMultipleComponent]
public sealed class ArcaneProjectileAbilityBehaviour : AbilityExecutionBehaviour
{
    [Header("Preparation")]
    [SerializeField, Min(0.01f)] private float _preparationSeconds = 0.5f;
    [Tooltip("Provisional balance: voluntary movement only, not forced displacement or knockback.")]
    [SerializeField, Range(0.01f, 1f)] private float _preparationMovementSpeedMultiplier = 0.5f;

    [Header("Projectile")]
    [Tooltip("Explicit ability prefab; must compose NetworkProjectile and a projectile-local DamageResolver.")]
    [SerializeField] private NetworkPrefabRef _projectilePrefab;
    [SerializeField] private LayerMask _impactLayerMask;
    [SerializeField] private MonoBehaviour _projectileSpawnerSource;
    [Tooltip("Provisional balance value, not a finalized Game Design number.")]
    [SerializeField, Min(0.01f)] private float _projectileSpeed = 6f;
    [Tooltip("Provisional balance value, not a finalized Game Design number.")]
    [SerializeField, Min(0.01f)] private float _damage = 5f;
    [SerializeField] private DamageType _damageType = DamageType.Magical;

    private uint _lastResolvedSequence;

    public override bool TryPlanStart(in AbilityExecutionContext context, out AbilityExecutionPlan plan)
    {
        plan = new AbilityExecutionPlan(AbilityExecutionPhase.Idle);
        if (!TryValidateConfiguration()) return true; // Configuration error: invalid plan before payment.
        if (context.Character == null || context.Character.gameObject != gameObject ||
            !AttackTiming.IsFinite((Vector2)context.Character.transform.position) ||
            !PlayerAimMath.TryNormalizeDirection(context.AimDirection, out _)) return false;
        if (_projectileSpawnerSource is FusionProjectileSpawner spawner &&
            !spawner.CanSpawnIndependentProjectile(_projectilePrefab, _impactLayerMask.value))
        {
            Debug.LogError($"{nameof(ArcaneProjectileAbilityBehaviour)} requires a registered projectile prefab with a local {nameof(DamageResolver)} and a runner registry.", this);
            return true;
        }
        // Directional: no target search or trajectory-clearance rejection.
        plan = new AbilityExecutionPlan(AbilityExecutionPhase.Preparing, _preparationSeconds);
        return true;
    }

    public override void Begin(in AbilityExecutionContext context, in AbilityExecutionSnapshot snapshot)
    {
        // Acceptance only starts the runtime-owned preparation. Nothing is spawned here.
    }

    public override AbilityExecutionPlan Simulate(in AbilityExecutionContext context, in AbilityExecutionSnapshot snapshot)
    {
        var idle = new AbilityExecutionPlan(AbilityExecutionPhase.Idle);
        if (snapshot.Phase != AbilityExecutionPhase.Preparing || snapshot.Sequence == _lastResolvedSequence) return idle;
        if (context.Runner == null || !context.Runner.IsForward || context.Character == null ||
            !context.Character.HasStateAuthority || !snapshot.PhaseDeadline.Expired(context.Runner))
            return new AbilityExecutionPlan(snapshot.Phase);

        // Consume before the irreversible call. A failed spawn is not retried and never refunds payment.
        _lastResolvedSequence = snapshot.Sequence;
        if (!TryValidateConfiguration()) return idle;
        var request = new ProjectileSpawnRequest(context.Character.Id, context.Character.transform.position,
            snapshot.AimDirection, _damage, _damageType, _projectileSpeed, 0f, 0f, context.Runner.Tick.Raw,
            projectilePrefab: _projectilePrefab, impactLayerMask: _impactLayerMask.value,
            isUnbounded: true, enemyOnly: true);
        if (!request.TryValidate() || !((IProjectileSpawner)_projectileSpawnerSource).Spawn(request).WasSpawned)
            Debug.LogError($"{nameof(ArcaneProjectileAbilityBehaviour)} could not spawn its projectile; cost and cooldown remain spent.", this);
        return idle;
    }

    public override bool CanInterrupt(AbilityExecutionStopReason reason) =>
        reason == AbilityExecutionStopReason.Knockback || reason == AbilityExecutionStopReason.Stun;

    public override float GetVoluntaryMovementSpeedMultiplier(in AbilityExecutionSnapshot snapshot) =>
        snapshot.Phase == AbilityExecutionPhase.Preparing ? _preparationMovementSpeedMultiplier : 1f;

    public override void Stop(in AbilityExecutionContext context, in AbilityExecutionSnapshot snapshot,
        AbilityExecutionStopReason reason)
    {
        // No held movement state to release. Never delete an already-independent projectile.
    }

    public override void Rebind(in AbilityExecutionContext context, in AbilityExecutionSnapshot snapshot)
    {
        // References are serialized; copied aim/deadline/phase are sufficient. Never replay a spawn.
    }

    private bool TryValidateConfiguration()
    {
        if (_projectilePrefab.IsValid && _impactLayerMask.value != 0 &&
            _projectileSpawnerSource != null && _projectileSpawnerSource.enabled && _projectileSpawnerSource.gameObject == gameObject &&
            _projectileSpawnerSource is IProjectileSpawner &&
            AttackTiming.IsFinite(_preparationSeconds) && _preparationSeconds > 0f &&
            AttackTiming.IsFinite(_preparationMovementSpeedMultiplier) && _preparationMovementSpeedMultiplier > 0f &&
            _preparationMovementSpeedMultiplier <= 1f && AttackTiming.IsFinite(_damage) && _damage > 0f &&
            AttackTiming.IsFinite(_projectileSpeed) && _projectileSpeed > 0f &&
            System.Enum.IsDefined(typeof(DamageType), _damageType)) return true;
        Debug.LogError($"{nameof(ArcaneProjectileAbilityBehaviour)} configuration is invalid: assign an explicit prefab, impact mask and avatar-local spawner, with finite positive preparation, speed, damage and a movement multiplier in (0, 1].", this);
        return false;
    }
}
