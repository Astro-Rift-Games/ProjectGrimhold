using Fusion;
using UnityEngine;

/// <summary>
/// Owns the player's networked Stamina state and advances its temporal rules.
/// Maximum Stamina is derived from the admitted Raid attribute snapshot and is not replicated.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-11)]
public sealed class PlayerStaminaNetworkController : NetworkBehaviour
{
    private const float MissingAttributeSourceDiagnosticDelaySeconds = 5f;

    [Header("Stamina Balance (Temporary)")]
    [SerializeField, Min(0f)]
    private float _regenerationPerSecond = 15f;

    [SerializeField, Min(0f)]
    private float _regenerationDelaySeconds = 1f;

    [SerializeField, Range(0f, 1f)]
    private float _exhaustionRecoveryThreshold = 0.25f;

    [Header("Dependencies")]
    [SerializeField]
    private RaidAvatarParticipantLink _participantLink;

    [Networked]
    public float CurrentStamina { get; private set; }

    [Networked]
    public NetworkBool IsExhausted { get; private set; }

    [Networked]
    private NetworkBool IsInitialized { get; set; }

    [Networked]
    private TickTimer RegenerationDelay { get; set; }

    private bool _isRestoreSpawn;
    private bool _reportedMissingParticipantLink;
    private bool _reportedInvalidDerivedStatistics;
    private bool _reportedUnresolvedAttributeSource;
    private float _unresolvedAttributeSourceSeconds;

    private void Awake()
    {
        CacheDependencies();
    }

    public override void Spawned()
    {
        CacheDependencies();
        _isRestoreSpawn = HostMigrationRestoreUtility.IsRestoreSpawn(this);

        if (_participantLink == null && !_reportedMissingParticipantLink)
        {
            Debug.LogError(
                $"{nameof(PlayerStaminaNetworkController)} requires {nameof(RaidAvatarParticipantLink)}.",
                this);
            _reportedMissingParticipantLink = true;
        }

        if (HasStateAuthority && !_isRestoreSpawn)
        {
            TryInitializeFreshState();
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!CanMutateSimulationState())
        {
            return;
        }

        if (!TryGetMaximumStamina(out float maximumStamina))
        {
            TrackUnresolvedAttributeSource();
            return;
        }

        _unresolvedAttributeSourceSeconds = 0f;

        if (!IsInitialized)
        {
            if (HasStateAuthority && !_isRestoreSpawn)
            {
                InitializeFreshState(maximumStamina);
            }

            return;
        }

        CurrentStamina = StaminaResourceRules.ClampCurrent(CurrentStamina, maximumStamina);

        if (CurrentStamina < maximumStamina &&
            RegenerationDelay.ExpiredOrNotRunning(Runner))
        {
            CurrentStamina = StaminaResourceRules.Regenerate(
                CurrentStamina,
                maximumStamina,
                _regenerationPerSecond,
                Runner.DeltaTime);
        }

        if (IsExhausted && StaminaResourceRules.HasRecoveredFromExhaustion(
                CurrentStamina,
                maximumStamina,
                _exhaustionRecoveryThreshold))
        {
            IsExhausted = false;
        }
    }

    /// <summary>
    /// Resolves Max Stamina from the frozen Raid attributes without creating another RES source.
    /// </summary>
    public bool TryGetMaximumStamina(out float maximumStamina)
    {
        maximumStamina = 0f;
        if (_participantLink == null ||
            !_participantLink.TryGetCharacterAttributeState(out CharacterAttributeState attributes))
        {
            return false;
        }

        if (!CharacterDerivedStatisticsCalculator.TryCalculate(
                attributes,
                ProgressionBalanceDefaults.InitialCharacterDerivedStatisticsConfiguration,
                out CharacterDerivedStatistics statistics,
                out CharacterDerivedStatisticsCalculationFailure failure))
        {
            if (!_reportedInvalidDerivedStatistics)
            {
                Debug.LogError(
                    $"{nameof(PlayerStaminaNetworkController)} could not derive maximum Stamina " +
                    $"from the admitted character attributes. Failure={failure}.",
                    this);
                _reportedInvalidDerivedStatistics = true;
            }

            return false;
        }

        maximumStamina = statistics.MaximumStamina;
        return true;
    }

    /// <summary>Checks whether a complete discrete cost can currently be paid.</summary>
    public bool CanSpend(float amount)
    {
        if (!StaminaResourceRules.IsValidCost(amount))
        {
            return false;
        }

        if (amount == 0f)
        {
            return true;
        }

        return IsInitialized &&
               TryGetMaximumStamina(out _) &&
               StaminaResourceRules.CanSpend(CurrentStamina, IsExhausted, amount);
    }

    /// <summary>
    /// Attempts an all-or-nothing discrete spend. Insufficient Stamina does not cause Exhaustion.
    /// </summary>
    public bool TrySpend(float amount)
    {
        if (!StaminaResourceRules.IsValidCost(amount))
        {
            return false;
        }

        if (amount == 0f)
        {
            return true;
        }

        if (!CanMutateSimulationState() || !CanSpend(amount) ||
            !StaminaResourceRules.TrySpend(
                CurrentStamina,
                IsExhausted,
                amount,
                exhaustOnFailure: false,
                exhaustWhenDepleted: false,
                out float resultingStamina,
                out bool resultingExhaustion))
        {
            return false;
        }

        CommitSpend(resultingStamina, resultingExhaustion);
        return true;
    }

    /// <summary>
    /// Attempts an all-or-nothing continuous spend. An unaffordable cost preserves the
    /// remainder and causes Exhaustion; a complete payment that depletes Stamina succeeds
    /// for the current tick and causes Exhaustion for subsequent ticks.
    /// </summary>
    public bool TrySpendContinuous(float amount)
    {
        if (!StaminaResourceRules.IsValidCost(amount))
        {
            return false;
        }

        if (amount == 0f)
        {
            return true;
        }

        if (!CanMutateSimulationState() || !IsInitialized ||
            !TryGetMaximumStamina(out _) || IsExhausted)
        {
            return false;
        }

        bool wasSpent = StaminaResourceRules.TrySpend(
            CurrentStamina,
            IsExhausted,
            amount,
            exhaustOnFailure: true,
            exhaustWhenDepleted: true,
            out float resultingStamina,
            out bool resultingExhaustion);

        CurrentStamina = resultingStamina;
        IsExhausted = resultingExhaustion;
        if (wasSpent)
        {
            RestartRegenerationDelay();
        }

        return wasSpent;
    }

    private bool TryInitializeFreshState()
    {
        if (IsInitialized || !TryGetMaximumStamina(out float maximumStamina))
        {
            return false;
        }

        InitializeFreshState(maximumStamina);
        return true;
    }

    private void InitializeFreshState(float maximumStamina)
    {
        CurrentStamina = Mathf.Max(0f, maximumStamina);
        IsExhausted = false;
        RegenerationDelay = TickTimer.None;
        IsInitialized = true;
    }

    private void CommitSpend(float resultingStamina, bool resultingExhaustion)
    {
        CurrentStamina = resultingStamina;
        IsExhausted = resultingExhaustion;
        RestartRegenerationDelay();
    }

    private void RestartRegenerationDelay()
    {
        RegenerationDelay = _regenerationDelaySeconds > 0f
            ? TickTimer.CreateFromSeconds(Runner, _regenerationDelaySeconds)
            : TickTimer.None;
    }

    private bool CanMutateSimulationState()
    {
        return Runner != null && Runner.IsSimulationUpdating &&
               (HasStateAuthority || HasInputAuthority);
    }

    private void TrackUnresolvedAttributeSource()
    {
        if (_reportedUnresolvedAttributeSource)
        {
            return;
        }

        _unresolvedAttributeSourceSeconds += Runner.DeltaTime;
        if (_unresolvedAttributeSourceSeconds < MissingAttributeSourceDiagnosticDelaySeconds)
        {
            return;
        }

        Debug.LogWarning(
            $"{nameof(PlayerStaminaNetworkController)} is waiting for the admitted Raid " +
            "participant attribute snapshot. Stamina state remains frozen until it resolves.",
            this);
        _reportedUnresolvedAttributeSource = true;
    }

    private void CacheDependencies()
    {
        if (_participantLink == null)
        {
            _participantLink = GetComponent<RaidAvatarParticipantLink>();
        }
    }

#if UNITY_EDITOR
    private void Reset()
    {
        CacheDependencies();
    }

    private void OnValidate()
    {
        _regenerationPerSecond = Mathf.Max(0f, _regenerationPerSecond);
        _regenerationDelaySeconds = Mathf.Max(0f, _regenerationDelaySeconds);
        _exhaustionRecoveryThreshold = Mathf.Clamp01(_exhaustionRecoveryThreshold);
        CacheDependencies();
    }
#endif
}
