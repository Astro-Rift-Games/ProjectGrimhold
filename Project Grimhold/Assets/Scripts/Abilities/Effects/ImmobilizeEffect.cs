using Fusion;
using UnityEngine;

/// <summary>
/// Immobilize plus periodic damage on an enemy, owned by the enemy it affects. Under State Authority it holds the
/// enemy in place through <see cref="EnemyMovementAIController.TrySetImmobilized"/> (voluntary movement only: the
/// enemy can still attack, and knockback still moves it) and deals one damage tick per interval through the
/// shared <see cref="IDamageResolver"/>, attributed to the source. Damage lasts exactly as long as the immobilization;
/// ending the effect (expiry, purification, cancellation, target death) ends both together and never reverts the
/// damage already dealt. It owns no ability, placement or trigger rules.
///
/// All state is [Networked] (including both timers), so a Host Migration restore resumes the same effect in the
/// resumed tick domain without recreating timers. Only the source <see cref="EntityId"/> needs remapping; see
/// HostMigrationSnapshotRestorer.
/// </summary>
[DisallowMultipleComponent]
public sealed class ImmobilizeEffect : NetworkBehaviour
{
    [Networked] private TickTimer DurationTimer { get; set; }
    [Networked] private TickTimer NextTickTimer { get; set; }
    [Networked] private int SourceEntityIdValue { get; set; }
    [Networked] private float DamagePerTick { get; set; }
    [Networked] private float TickIntervalSeconds { get; set; }
    [Networked] private NetworkBool Purifiable { get; set; }
    [Networked] private NetworkBool Active { get; set; }
    [Networked] private int DamageTypeValue { get; set; }

    private EnemyMovementAIController _movement;
    private CharacterBase _character;
    private IDamageResolver _damageResolver;

    /// <summary>True while the enemy is held and being damaged by this effect.</summary>
    public bool IsActive => Object != null && Object.IsValid && Active;

    /// <summary>Whether the running effect can be ended by <see cref="TryPurify"/>.</summary>
    public bool IsPurifiable => IsActive && Purifiable;

    /// <summary>Entity credited with the damage of the running effect; default when none runs.</summary>
    public EntityId SourceId => new EntityId(SourceEntityIdValue);

    public override void Spawned()
    {
        // Every value is networked: a fresh spawn starts inactive and a Host Migration restore keeps the copied
        // effect (timers included), so nothing is written here.
        ResolveDependencies();
    }

    /// <summary>
    /// Immobilizes this enemy and starts its periodic damage, the first tick one interval from now. Returns false
    /// without any change when this peer has no State Authority, the configuration or the source is invalid, or the
    /// enemy is already dead. Applying to an enemy already under the effect refreshes it: both timers restart, and
    /// the source, damage, interval, type and purifiable flag of the latest application replace the previous ones,
    /// so there is never more than one effect (and never a doubled tick) on an enemy.
    /// </summary>
    public bool TryApply(EntityId sourceId, float durationSeconds, float damagePerTick, float tickIntervalSeconds,
        DamageType damageType, bool purifiable)
    {
        if (Object == null || !Object.IsValid || !HasStateAuthority) return false;

        if (sourceId.Value == 0)
        {
            Debug.LogError($"{nameof(ImmobilizeEffect)} requires a valid source entity.", this);
            return false;
        }

        if (!ImmobilizeRules.TryValidateConfiguration(durationSeconds, damagePerTick, tickIntervalSeconds, out string error))
        {
            Debug.LogError($"{nameof(ImmobilizeEffect)} rejected its configuration: {error}", this);
            return false;
        }

        ResolveDependencies();
        if (_movement == null || _character == null)
        {
            Debug.LogError($"{nameof(ImmobilizeEffect)} requires {nameof(EnemyMovementAIController)} and {nameof(CharacterBase)} on the same enemy.", this);
            return false;
        }

        if (damagePerTick > 0f && _damageResolver == null)
        {
            Debug.LogError($"{nameof(ImmobilizeEffect)} requires an {nameof(IDamageResolver)} on the enemy to apply periodic damage.", this);
            return false;
        }

        if (!IsTargetAlive() || !_movement.TrySetImmobilized(true)) return false;

        SourceEntityIdValue = sourceId.Value;
        DamagePerTick = damagePerTick;
        TickIntervalSeconds = tickIntervalSeconds;
        DamageTypeValue = (int)damageType;
        Purifiable = purifiable;
        DurationTimer = TickTimer.CreateFromSeconds(Runner, durationSeconds);
        NextTickTimer = TickTimer.CreateFromSeconds(Runner, tickIntervalSeconds);
        Active = true;
        return true;
    }

    /// <summary>
    /// Ends the effect, immobilization and periodic damage together, when it is purifiable. Returns whether it ended.
    /// Damage already dealt stays dealt.
    /// </summary>
    public bool TryPurify()
    {
        if (!IsActive || !HasStateAuthority || !Purifiable) return false;

        EndEffect();
        return true;
    }

    /// <summary>Ends the effect, immobilization and periodic damage together, whether or not it is purifiable.</summary>
    public void Cancel()
    {
        if (!IsActive || !HasStateAuthority) return;

        EndEffect();
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || !Runner.IsForward || !Active) return;

        // A timer that stopped running while the effect is active cannot be trusted: treat it as expired.
        ImmobilizeStep step = ImmobilizeRules.Decide(true, IsTargetAlive(),
            DurationTimer.ExpiredOrNotRunning(Runner), NextTickTimer.Expired(Runner));

        if ((step & ImmobilizeStep.ApplyTick) != 0)
        {
            ApplyTick();
            // Re-armed from the tick that fired it, so intervals do not drift; one tick per simulation tick at most.
            NextTickTimer = TickTimer.CreateFromSeconds(Runner, TickIntervalSeconds);
            if (!IsTargetAlive()) step |= ImmobilizeStep.End;
        }

        if ((step & ImmobilizeStep.End) != 0) EndEffect();
    }

    private void ApplyTick()
    {
        if (DamagePerTick <= 0f || _damageResolver == null) return;

        var request = new DamageRequest(new EntityId(SourceEntityIdValue), _character.Id, DamagePerTick,
            (DamageType)DamageTypeValue, Vector2.zero, transform.position, Runner.Tick.Raw);
        _damageResolver.Resolve(in request);
    }

    private void EndEffect()
    {
        Active = false;
        DurationTimer = TickTimer.None;
        NextTickTimer = TickTimer.None;
        SourceEntityIdValue = 0;
        DamagePerTick = 0f;
        Purifiable = false;
        ResolveDependencies();
        _movement?.TrySetImmobilized(false);
    }

    private bool IsTargetAlive() => _character != null && _character.IsAlive && _character.CanReceiveDamage;

    private void ResolveDependencies()
    {
        if (_movement == null) _movement = GetComponent<EnemyMovementAIController>();
        if (_character == null) _character = GetComponent<CharacterBase>();
        if (_damageResolver == null)
        {
            _damageResolver = GetComponent<IDamageResolver>() ??
                              GetComponentInChildren<IDamageResolver>() ??
                              GetComponentInParent<IDamageResolver>();
        }
    }

    // Host Migration remap hooks, used by HostMigrationSnapshotRestorer only.
    internal bool HasRestoredActiveEffect => Object != null && Object.IsValid && Active;
    internal int GetRestoredSourceEntityIdValue() => SourceEntityIdValue;
    internal void SetRestoredSourceEntityId(EntityId newSourceId) => SourceEntityIdValue = newSourceId.Value;

    /// <summary>Safely ends a restored effect whose source could not be remapped, so no damage is credited to a stale id.</summary>
    internal void CancelRestoredEffect()
    {
        if (!HasRestoredActiveEffect) return;

        EndEffect();
    }
}
