using Fusion;
using UnityEngine;

/// <summary>
/// Owns the assisted recovery session that lives on the Downed avatar (DownedAndReviveArchitecture
/// section 7). State Authority opens, validates, interrupts and completes it; clients only read.
///
/// Reviver identity is the reviver avatar's <see cref="NetworkId"/>, resolved on the Host with
/// <c>Runner.TryFindObject</c>. The "one session per reviver" rule is answered by a reviver-side
/// <see cref="RevivingTargetId"/> that is only trusted while the target's session still names the
/// reviver, so a stale value can never block anyone.
///
/// A reviver disconnect is detected without extra state: the Host despawns the avatar (the id stops
/// resolving), or the avatar's participant leaves <see cref="RaidParticipantState.Raiding"/>. Other
/// networked ids are not remapped by Host Migration, so a restored session fails to resolve its
/// reviver and is interrupted safely.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerDownedRecoveryNetworkController :
    NetworkBehaviour,
    IDownedDrainGate,
    IDownedDamageObserver
{
    [Header("Assisted Recovery")]
    [SerializeField, Min(0.01f)]
    private float _reviveDurationSeconds = 4f;

    [SerializeField, Min(0.01f)]
    private float _reviveRange = 1.5f;

    /// <summary>Health granted when an assisted recovery completes (fixed by Game Design).</summary>
    private const float RestoredHealth = 1f;

    private PlayerCharacter _playerCharacter;
    private PlayerDownedStateNetworkController _downedState;
    private PlayerMovementNetworkController _movement;
    private PlayerAbilityRuntimeNetworkController _abilityRuntime;
    private bool _isConfigurationValid;
    private bool _reportedInvalidConfiguration;
    private float _previousReviverHealth;
    private bool _hasPreviousReviverHealth;

    [Networked]
    public RecoveryKind Kind { get; private set; }

    [Networked]
    public NetworkId ReviverId { get; private set; }

    [Networked]
    public int SessionCycle { get; private set; }

    [Networked]
    public TickTimer Completion { get; private set; }

    /// <summary>Reviver-side: the Downed avatar this avatar is reviving, valid only while that session names it.</summary>
    [Networked]
    public NetworkId RevivingTargetId { get; private set; }

    /// <summary>How the last session ended; presentation record read by both participants.</summary>
    [Networked]
    public RecoveryOutcomeKind LastOutcomeKind { get; private set; }

    [Networked]
    public DownedRecoveryInterruptReason LastOutcomeReason { get; private set; }

    /// <summary>Reviver of the last session, so the reviver can recognise an outcome as its own.</summary>
    [Networked]
    public NetworkId LastOutcomeReviverId { get; private set; }

    /// <summary>Monotonic counter, bumped once per outcome; presenters edge-detect on it.</summary>
    [Networked]
    public int LastOutcomeSequence { get; private set; }

    public float ReviveDurationSeconds => _reviveDurationSeconds;

    public float ReviveRange => _reviveRange;

    /// <summary>Test seam: overrides the initial-team check, which needs a Raid launch context.</summary>
    internal bool? TestTeamOverride { get; set; }

    /// <summary>Derived: a valid session pauses the reserve drain.</summary>
    public bool IsDrainPaused => HasValidSession;

    /// <summary>True while the session matches the current Downed cycle of a Downed avatar.</summary>
    public bool HasValidSession =>
        _downedState != null &&
        Object != null && Object.IsValid &&
        DownedRecoveryRules.IsSessionValid(Kind, SessionCycle, _downedState.DownedCycle, _downedState.IsDowned);

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
            Kind = RecoveryKind.None;
            ReviverId = default;
            SessionCycle = 0;
            Completion = TickTimer.None;
            RevivingTargetId = default;
            LastOutcomeKind = RecoveryOutcomeKind.None;
            LastOutcomeReason = DownedRecoveryInterruptReason.None;
            LastOutcomeReviverId = default;
            LastOutcomeSequence = 0;
        }
    }

    /// <summary>
    /// Presentation read: revive progress in [0, 1] while a valid session runs. Derived from the
    /// replicated <see cref="Completion"/> timer, so it adds no state.
    /// </summary>
    public bool TryGetProgress(out float progress)
    {
        progress = 0f;
        if (!HasValidSession || Runner == null)
        {
            return false;
        }

        float remaining = Completion.RemainingTime(Runner) ?? _reviveDurationSeconds;
        progress = DownedRecoveryFeedbackRules.ProgressFraction(remaining, _reviveDurationSeconds);
        return true;
    }

    /// <summary>
    /// Opens an assisted session for <paramref name="reviver"/>. Refused unless authoritative and the
    /// reviver is an Active initial teammate in range who is not already reviving someone.
    /// </summary>
    public bool TryBeginAssisted(PlayerCharacter reviver)
    {
        if (!HasStateAuthority || !CanBeginAssisted(reviver, out PlayerDownedRecoveryNetworkController reviverRecovery))
        {
            return false;
        }

        if (Kind != RecoveryKind.None && !HasValidSession)
        {
            ClearSession();
        }

        Kind = RecoveryKind.Assisted;
        ReviverId = reviver.Object.Id;
        SessionCycle = _downedState.DownedCycle;
        Completion = TickTimer.CreateFromSeconds(Runner, _reviveDurationSeconds);
        reviverRecovery.RevivingTargetId = Object.Id;
        _previousReviverHealth = reviver.Health;
        _hasPreviousReviverHealth = true;
        return true;
    }

    /// <summary>
    /// Side-effect-free start check shared by the interaction entry point and
    /// <see cref="TryBeginAssisted"/>: Active initial teammate, in range, target Downed with no valid
    /// session, reviver not already reviving someone else.
    /// </summary>
    public bool CanBeginAssisted(PlayerCharacter reviver)
    {
        return CanBeginAssisted(reviver, out _);
    }

    private bool CanBeginAssisted(
        PlayerCharacter reviver,
        out PlayerDownedRecoveryNetworkController reviverRecovery)
    {
        reviverRecovery = null;
        if (!_isConfigurationValid || _downedState == null ||
            reviver == null || reviver == _playerCharacter ||
            reviver.Object == null || !reviver.Object.IsValid ||
            !reviver.TryGetComponent(out reviverRecovery) ||
            (reviverRecovery._abilityRuntime != null && reviverRecovery._abilityRuntime.HasActiveExecution))
        {
            return false;
        }

        float distance = Vector2.Distance(transform.position, reviver.transform.position);
        return DownedRecoveryRules.CanStartAssisted(
            reviver.IsAlive,
            reviver.IsDowned,
            IsInitialTeammate(reviver),
            distance,
            _reviveRange,
            _downedState.IsDowned,
            HasValidSession,
            reviverRecovery.IsRevivingAnotherAvatar);
    }

    /// <summary>
    /// Reviver-side: interrupts the session this avatar is running on a Downed teammate, if any.
    /// State Authority only; a stale <see cref="RevivingTargetId"/> is ignored.
    /// </summary>
    internal bool InterruptRevivedTarget(DownedRecoveryInterruptReason reason)
    {
        if (!HasStateAuthority || !IsRevivingAnotherAvatar ||
            !Runner.TryFindObject(RevivingTargetId, out NetworkObject targetObject) ||
            !targetObject.TryGetBehaviour(out PlayerDownedRecoveryNetworkController target))
        {
            return false;
        }

        target.Interrupt(reason);
        return true;
    }

    /// <summary>Interrupts the session because the Downed avatar took damage (reserve already reduced).</summary>
    public void NotifyDownedDamaged()
    {
        Interrupt(DownedRecoveryInterruptReason.DownedDamaged);
    }

    /// <summary>Discards any active session and its progress. State Authority only.</summary>
    internal void Interrupt(DownedRecoveryInterruptReason reason)
    {
        if (!HasStateAuthority || reason == DownedRecoveryInterruptReason.None || Kind == RecoveryKind.None)
        {
            return;
        }

        RecordOutcome(RecoveryOutcomeKind.Interrupted, reason);
        ClearSession();
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || Kind == RecoveryKind.None)
        {
            return;
        }

        if (!TryBuildSnapshot(out DownedRecoveryInterruptionSnapshot snapshot, out PlayerCharacter reviver))
        {
            RecordOutcome(RecoveryOutcomeKind.Interrupted, DownedRecoveryInterruptReason.DownedDefeated);
            ClearSession();
            return;
        }

        DownedRecoveryInterruptReason interruption = DownedRecoveryRules.EvaluateInterruption(snapshot);
        if (interruption != DownedRecoveryInterruptReason.None)
        {
            RecordOutcome(RecoveryOutcomeKind.Interrupted, interruption);
            ClearSession();
            return;
        }

        _previousReviverHealth = reviver.Health;
        if (!Completion.Expired(Runner))
        {
            return;
        }

        // Success and cleanup share this tick, so a second completion is impossible.
        if (_playerCharacter.TryRestoreFromDowned(RestoredHealth))
        {
            RecordOutcome(RecoveryOutcomeKind.Completed, DownedRecoveryInterruptReason.None);
        }

        ClearSession();
    }

    private bool TryBuildSnapshot(
        out DownedRecoveryInterruptionSnapshot snapshot,
        out PlayerCharacter reviver)
    {
        reviver = null;
        snapshot = default;
        if (_downedState == null || _playerCharacter == null)
        {
            return false;
        }

        bool downedIsDowned = _downedState.IsDowned;
        int downedCycle = _downedState.DownedCycle;
        bool connected = TryResolveConnectedReviver(out reviver);
        if (!connected)
        {
            snapshot = new DownedRecoveryInterruptionSnapshot(
                false, false, false, float.NaN, _reviveRange, 0f, 0f, false, false,
                false, false, downedIsDowned, SessionCycle, downedCycle);
            return true;
        }

        if (!_hasPreviousReviverHealth)
        {
            _previousReviverHealth = reviver.Health;
            _hasPreviousReviverHealth = true;
        }

        bool held = reviver.TryGetComponent(out PlayerInteractionNetworkController interaction) &&
                    interaction.IsInteractHeld;
        bool reviverMoving = reviver.TryGetComponent(out PlayerMovementNetworkController reviverMovement) &&
                             reviverMovement.IsMoving;
        bool downedMoving = _movement != null && _movement.IsMoving;

        snapshot = new DownedRecoveryInterruptionSnapshot(
            held,
            reviverMoving,
            downedMoving,
            Vector2.Distance(transform.position, reviver.transform.position),
            _reviveRange,
            _previousReviverHealth,
            reviver.Health,
            reviver.IsDowned,
            true,
            false,
            false,
            downedIsDowned,
            SessionCycle,
            downedCycle);
        return true;
    }

    private bool TryResolveConnectedReviver(out PlayerCharacter reviver)
    {
        reviver = null;
        if (!ReviverId.IsValid ||
            !Runner.TryFindObject(ReviverId, out NetworkObject reviverObject) ||
            reviverObject == null || !reviverObject.IsValid ||
            !reviverObject.TryGetBehaviour(out reviver))
        {
            return false;
        }

        if (reviver.TryGetComponent(out RaidAvatarParticipantLink link) &&
            link.TryResolveParticipant(out NetworkRaidParticipant participant) &&
            participant.State != RaidParticipantState.Raiding)
        {
            return false;
        }

        return true;
    }

    private void RecordOutcome(RecoveryOutcomeKind kind, DownedRecoveryInterruptReason reason)
    {
        LastOutcomeKind = kind;
        LastOutcomeReason = reason;
        LastOutcomeReviverId = ReviverId;
        LastOutcomeSequence++;
    }

    private void ClearSession()
    {
        NetworkId reviverId = ReviverId;
        Kind = RecoveryKind.None;
        ReviverId = default;
        SessionCycle = 0;
        Completion = TickTimer.None;
        _hasPreviousReviverHealth = false;

        if (reviverId.IsValid &&
            Runner.TryFindObject(reviverId, out NetworkObject reviverObject) &&
            reviverObject != null && reviverObject.IsValid &&
            reviverObject.TryGetBehaviour(out PlayerDownedRecoveryNetworkController reviverRecovery) &&
            reviverRecovery.RevivingTargetId == Object.Id)
        {
            reviverRecovery.RevivingTargetId = default;
        }
    }

    /// <summary>Reviver-side: true while the target named here still has a valid session naming this avatar.</summary>
    internal bool IsRevivingAnotherAvatar =>
        RevivingTargetId.IsValid &&
        Runner != null &&
        Runner.TryFindObject(RevivingTargetId, out NetworkObject targetObject) &&
        targetObject != null && targetObject.IsValid &&
        targetObject.TryGetBehaviour(out PlayerDownedRecoveryNetworkController target) &&
        target.HasValidSession &&
        target.ReviverId == Object.Id;

    private bool IsInitialTeammate(PlayerCharacter reviver)
    {
        if (TestTeamOverride.HasValue)
        {
            return TestTeamOverride.Value;
        }

        NetworkSpawnManager spawnManager = Runner != null ? Runner.GetComponent<NetworkSpawnManager>() : null;
        return spawnManager != null &&
               spawnManager.TryGetRaidInitialAffiliations(out RaidInitialAffiliationSnapshot affiliations) &&
               TryGetParticipantId(_playerCharacter, out RaidParticipantId targetId) &&
               TryGetParticipantId(reviver, out RaidParticipantId reviverId) &&
               affiliations.TryAreInitialTeammates(targetId, reviverId, out bool areTeammates) &&
               areTeammates;
    }

    private static bool TryGetParticipantId(PlayerCharacter character, out RaidParticipantId participantId)
    {
        participantId = default;
        if (!character.TryGetComponent(out RaidAvatarParticipantLink link) ||
            !link.TryResolveParticipant(out NetworkRaidParticipant participant))
        {
            return false;
        }

        participantId = participant.RaidParticipantId;
        return participantId.IsValid;
    }

    private void CacheDependencies()
    {
        _playerCharacter ??= GetComponent<PlayerCharacter>();
        _downedState ??= GetComponent<PlayerDownedStateNetworkController>();
        _movement ??= GetComponent<PlayerMovementNetworkController>();
        _abilityRuntime ??= GetComponent<PlayerAbilityRuntimeNetworkController>();
    }

    private void ValidateConfiguration()
    {
        _isConfigurationValid =
            IsFinitePositive(_reviveDurationSeconds) &&
            IsFinitePositive(_reviveRange) &&
            _playerCharacter != null &&
            _downedState != null;

        if (!_isConfigurationValid && !_reportedInvalidConfiguration)
        {
            Debug.LogError(
                $"{nameof(PlayerDownedRecoveryNetworkController)} has an invalid recovery configuration " +
                $"or is missing {nameof(PlayerCharacter)} / {nameof(PlayerDownedStateNetworkController)}.",
                this);
            _reportedInvalidConfiguration = true;
        }
    }

    private static bool IsFinitePositive(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        CacheDependencies();
    }
#endif
}
