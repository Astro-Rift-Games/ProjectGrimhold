using Fusion;
using UnityEngine;

/// <summary>
/// Owns the player's networked Downed state: a temporary health reserve that drains over time
/// and absorbs damage once the regular Health has reached zero. Only State Authority mutates it.
/// Depletion clears the Downed state in the same tick, before notifying the owner, so the
/// definitive defeat is resolved exactly once.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerDownedStateNetworkController : NetworkBehaviour
{
    [Header("Downed Balance (Temporary)")]
    [SerializeField, Min(0.01f)]
    private float _initialDownedHealth = 75f;

    [SerializeField, Min(0f)]
    private float _downedDrainPerSecond = 2.5f;

    [SerializeField, Min(0f)]
    private float _downedDamageMultiplier = 1f;

    [SerializeField, Range(0.01f, 1f)]
    private float _downedMovementSpeedMultiplier = 0.35f;

    [Header("Dependencies")]
    [SerializeField]
    private PlayerCharacter _playerCharacter;

    private IDownedDrainGate _drainGate;
    private IDownedDamageObserver _damageObserver;
    private bool _isConfigurationValid;
    private bool _reportedInvalidConfiguration;

    [Networked]
    public NetworkBool IsDowned { get; private set; }

    [Networked]
    public float DownedHealth { get; private set; }

    [Networked]
    public int DownedCycle { get; private set; }

    [Networked]
    public int DownedCauserEntityId { get; private set; }

    [Networked]
    public int DefeatCauserEntityId { get; private set; }

    [Networked]
    public int DefeatedCycle { get; private set; }

    /// <summary>Multiplier applied to voluntary movement speed while Downed.</summary>
    public float DownedMovementSpeedMultiplier => _downedMovementSpeedMultiplier;

    /// <summary>Test seam: when true, <see cref="TryEnterDowned"/> refuses so legacy defeat flows run.</summary>
    internal bool TestDisableEntry { get; set; }

    private void Awake()
    {
        CacheDependencies();
        ValidateConfiguration();
    }

    public override void Spawned()
    {
        CacheDependencies();
        ValidateConfiguration();

        if (HasStateAuthority && !HostMigrationRestoreUtility.IsRestoreSpawn(this))
        {
            IsDowned = false;
            DownedHealth = 0f;
            DownedCycle = 0;
            DownedCauserEntityId = 0;
            DefeatCauserEntityId = 0;
            DefeatedCycle = 0;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || !IsDowned)
        {
            return;
        }

        if (_drainGate != null && _drainGate.IsDrainPaused)
        {
            return;
        }

        DownedHealth = DownedHealthRules.Drain(DownedHealth, _downedDrainPerSecond, Runner.DeltaTime);
        if (DownedHealthRules.IsDepleted(DownedHealth))
        {
            ClearDownedState();
            if (_playerCharacter != null)
            {
                _playerCharacter.ResolveDefinitiveDefeatFromDowned();
            }
        }
    }

    /// <summary>Enters Downed with a full reserve. Refused unless authoritative and not already Downed.</summary>
    public bool TryEnterDowned(EntityId attackerId = default)
    {
        if (!HasStateAuthority || IsDowned || TestDisableEntry || !_isConfigurationValid ||
            !DownedHealthRules.TryCreateReserve(_initialDownedHealth, out float reserve))
        {
            return false;
        }

        DownedCauserEntityId = attackerId.Value;
        DefeatCauserEntityId = 0;
        DefeatedCycle = 0;
        IsDowned = true;
        DownedHealth = reserve;
        DownedCycle++;
        return true;
    }

    /// <summary>
    /// Applies already-mitigated damage to the reserve, scaled by the Downed multiplier.
    /// Depletion clears the Downed state before returning; the caller resolves defeat.
    /// </summary>
    public bool TryApplyDownedDamage(
        float mitigatedDamage,
        EntityId attackerId,
        out float applied,
        out bool depleted)
    {
        applied = 0f;
        depleted = false;
        if (!HasStateAuthority || !IsDowned)
        {
            return false;
        }

        float previous = DownedHealth;
        DownedHealth = DownedHealthRules.ApplyDamage(previous, mitigatedDamage, _downedDamageMultiplier);
        applied = previous - DownedHealth;
        depleted = DownedHealthRules.IsDepleted(DownedHealth);
        if (depleted)
        {
            ClearDownedState(attackerId);
        }

        if (applied > 0f)
        {
            _damageObserver?.NotifyDownedDamaged();
        }

        return true;
    }

    /// <summary>Clears Downed state without notifying anyone (used by forced definitive defeat).</summary>
    internal void ClearDownedState(EntityId defeatCauserId = default)
    {
        if (!HasStateAuthority)
        {
            return;
        }

        if (IsDowned)
        {
            DefeatCauserEntityId = defeatCauserId.Value;
            DefeatedCycle = DownedCycle;
        }

        IsDowned = false;
        DownedHealth = 0f;
    }

    /// <summary>
    /// Leaves Downed for Active after a completed recovery: clears the flag and discards the
    /// reserve while keeping <see cref="DownedCycle"/> so a later entry starts a new cycle.
    /// </summary>
    internal bool TryExitDownedToActive()
    {
        if (!HasStateAuthority || !IsDowned)
        {
            return false;
        }

        IsDowned = false;
        DownedHealth = 0f;
        DefeatCauserEntityId = 0;
        DefeatedCycle = 0;
        return true;
    }

    private void CacheDependencies()
    {
        if (_playerCharacter == null)
        {
            _playerCharacter = GetComponent<PlayerCharacter>();
        }

        // Optional collaborators: absent means the drain is never paused and nobody is notified.
        _drainGate ??= GetComponent<IDownedDrainGate>();
        _damageObserver ??= GetComponent<IDownedDamageObserver>();
    }

    private void ValidateConfiguration()
    {
        _isConfigurationValid =
            DownedHealthRules.TryCreateReserve(_initialDownedHealth, out _) &&
            IsFiniteNonNegative(_downedDrainPerSecond) &&
            IsFiniteNonNegative(_downedDamageMultiplier) &&
            _downedMovementSpeedMultiplier > 0f && _downedMovementSpeedMultiplier <= 1f;

        if (!_isConfigurationValid && !_reportedInvalidConfiguration)
        {
            Debug.LogError(
                $"{nameof(PlayerDownedStateNetworkController)} has an invalid Downed configuration.",
                this);
            _reportedInvalidConfiguration = true;
        }

        if (_playerCharacter == null)
        {
            Debug.LogError(
                $"{nameof(PlayerDownedStateNetworkController)} requires {nameof(PlayerCharacter)}.",
                this);
        }
    }

    private static bool IsFiniteNonNegative(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        CacheDependencies();
    }
#endif
}
