using Fusion;
using UnityEngine;

/// <summary>
/// Persistent-in-session identity for one admitted raid player.
///
/// This is the Fusion PlayerObject. State Authority owns its terminal result and
/// its current avatar reference; the avatar remains a separate network object so a
/// defeated body can remain in the raid after its owner returns to Town.
/// </summary>
[DisallowMultipleComponent]
public sealed class NetworkRaidParticipant : NetworkBehaviour, IInputAuthorityGained
{
    private const float ProgressionCommitRetryIntervalSeconds = 1f;
    [Networked]
    public NetworkString<_32> ProfileId { get; private set; }

    [Networked]
    public RaidParticipantId RaidParticipantId { get; private set; }

    /// <summary>Identifies the runner-scoped raid generation that owns this participant.</summary>
    [Networked]
    public NetworkString<_32> RaidGenerationId { get; private set; }

    [Networked]
    public NetworkString<_64> LoadoutReservationId { get; private set; }

    [Networked]
    public RaidParticipantState State { get; private set; }

    [Networked]
    public NetworkId CurrentAvatarId { get; private set; }

    [Networked]
    public int ResultSequence { get; private set; }

    [Networked]
    public ExtractionExperienceTransactionPhase ExtractionExperiencePhase { get; private set; }

    [Networked]
    public long ExtractedLootCandidateEligibleValue { get; private set; }

    [Networked]
    public long ExtractedLootCandidateExperience { get; private set; }

    [Networked]
    public ExpeditionProgressionFinalizationCause FinalizationCause { get; private set; }

    [Networked]
    public NetworkBool IsReturnAuthorized { get; private set; }



    [Networked]
    private int CharacterVitality { get; set; }

    [Networked]
    private int CharacterResistance { get; set; }

    [Networked]
    private int CharacterStrength { get; set; }

    [Networked]
    private int CharacterDexterity { get; set; }

    [Networked]
    private int CharacterIntelligence { get; set; }

    [Networked]
    private int CharacterLuck { get; set; }

    [Networked]
    private int CharacterAvailableAttributePoints { get; set; }

    [Networked]
    private NetworkBool IsCharacterAttributeStateInitialized { get; set; }

    public bool IsExtractionCommitConfirmed =>
        ExtractionExperiencePhase >= ExtractionExperienceTransactionPhase.ExtractedLootPending;

    public bool IsExtractionProgressionComplete =>
        ExtractionExperiencePhase == ExtractionExperienceTransactionPhase.Complete;

    private PlayerExpeditionProgressionResolver _progressionResolver;
    private ApplicationStashContext _localStashContext;
    private int _localProgressionResultSequence;
    private float _nextProgressionCommitRetryAt;

    /// <summary>
    /// Reads the immutable attribute snapshot admitted for this participation.
    /// No persistent source is consulted after participant initialization.
    /// </summary>
    public bool TryGetCharacterAttributeState(out CharacterAttributeState state)
    {
        state = default;
        if (Object == null || !Object.IsValid)
        {
            return false;
        }

        return TryBuildCharacterAttributeState(
            IsCharacterAttributeStateInitialized,
            CharacterVitality,
            CharacterResistance,
            CharacterStrength,
            CharacterDexterity,
            CharacterIntelligence,
            CharacterLuck,
            CharacterAvailableAttributePoints,
            out state);
    }

    internal static bool TryBuildCharacterAttributeState(
        bool initialized,
        int vitality,
        int resistance,
        int strength,
        int dexterity,
        int intelligence,
        int luck,
        int availablePoints,
        out CharacterAttributeState state)
    {
        state = default;
        return initialized && CharacterAttributeState.TryCreate(
            vitality,
            resistance,
            strength,
            dexterity,
            intelligence,
            luck,
            availablePoints,
            out state);
    }

    /// <summary>
    /// Resolves the current avatar without changing simulation state.
    /// </summary>
    public bool TryResolveCurrentAvatar(out NetworkObject avatar)
    {
        avatar = null;
        return Runner != null && CurrentAvatarId.IsValid &&
            Runner.TryFindObject(CurrentAvatarId, out avatar) && avatar != null;
    }

    /// <summary>
    /// Initializes this participant during the State Authority spawn callback.
    /// </summary>
    internal void Initialize(
        string profileId,
        RaidParticipantId raidParticipantId,
        in CharacterAttributeState characterAttributes,
        int baselineLevel,
        long baselineExperience,
        string raidGenerationId = null,
        string loadoutReservationId = null,
        int baselineResultSequence = 0)
    {
        if (!raidParticipantId.IsValid)
        {
            throw new System.ArgumentException("Raid participant identity must be valid.", nameof(raidParticipantId));
        }

        if (baselineResultSequence < 0 || baselineResultSequence == int.MaxValue)
        {
            throw new System.ArgumentOutOfRangeException(
                nameof(baselineResultSequence),
                "The progression watermark must allow exactly one following result.");
        }

        _progressionResolver ??= GetComponent<PlayerExpeditionProgressionResolver>();
        if (_progressionResolver == null ||
            !_progressionResolver.TryInitializeBaseline(baselineLevel, baselineExperience))
        {
            throw new System.ArgumentException(
                "Progression baseline must be valid for the current curve.",
                nameof(baselineLevel));
        }

        ProfileId = profileId;
        RaidParticipantId = raidParticipantId;
        RaidGenerationId = raidGenerationId ?? string.Empty;
        LoadoutReservationId = loadoutReservationId ?? string.Empty;
        CharacterVitality = characterAttributes.Vitality;
        CharacterResistance = characterAttributes.Resistance;
        CharacterStrength = characterAttributes.Strength;
        CharacterDexterity = characterAttributes.Dexterity;
        CharacterIntelligence = characterAttributes.Intelligence;
        CharacterLuck = characterAttributes.Luck;
        CharacterAvailableAttributePoints = characterAttributes.AvailablePoints;
        State = RaidParticipantState.Raiding;
        CurrentAvatarId = default;
        ResultSequence = baselineResultSequence;
        ExtractionExperiencePhase = ExtractionExperienceTransactionPhase.None;
        ExtractedLootCandidateEligibleValue = 0;
        ExtractedLootCandidateExperience = 0;
        FinalizationCause = ExpeditionProgressionFinalizationCause.None;
        IsReturnAuthorized = false;
        IsCharacterAttributeStateInitialized = true;
    }

    private void Awake()
    {
        _progressionResolver = GetComponent<PlayerExpeditionProgressionResolver>();
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || _progressionResolver == null)
        {
            return;
        }

        if (State == RaidParticipantState.Defeated)
        {
            _progressionResolver.TryFinalize(
                ExpeditionProgressionFinalizationCause.DefeatConfirmed);
            return;
        }

        if (FinalizationCause ==
            ExpeditionProgressionFinalizationCause.VoluntaryAbandonConfirmed)
        {
            TryAdvanceVoluntaryAbandon();
            return;
        }

        if (State == RaidParticipantState.Aborted &&
            FinalizationCause ==
                ExpeditionProgressionFinalizationCause.DefinitiveDisconnectConfirmed)
        {
            _progressionResolver.TryFinalize(
                ExpeditionProgressionFinalizationCause.DefinitiveDisconnectConfirmed);
        }
    }

    public override void Render()
    {
        if (!HasInputAuthority ||
            _progressionResolver == null || ResultSequence <= 0 ||
            FinalizationCause ==
                ExpeditionProgressionFinalizationCause.DefinitiveDisconnectConfirmed ||
            !_progressionResolver.TryGetResolution(out ExpeditionExperienceResolution resolution) ||
            !_progressionResolver.TryGetApplication(out ConsolidatedExperienceApplication application))
        {
            return;
        }

        // Legacy local progression commit has been removed.
    }



    /// <summary>
    /// Associates the only controllable avatar with this participant.
    /// </summary>
    internal bool TrySetCurrentAvatar(NetworkObject avatar)
    {
        if (!HasStateAuthority || State != RaidParticipantState.Raiding || avatar == null || !avatar.Id.IsValid)
        {
            return false;
        }

        if (CurrentAvatarId.IsValid)
        {
            return CurrentAvatarId == avatar.Id;
        }

        CurrentAvatarId = avatar.Id;
        return true;
    }

    /// <summary>
    /// Completes the defeated result only after the avatar has produced its lootable body.
    /// </summary>
    internal bool TryMarkDefeated(NetworkObject avatar)
    {
        if (!HasStateAuthority || !IsCurrentRaidingAvatar(avatar))
        {
            return false;
        }

        State = RaidParticipantState.Defeated;
        CurrentAvatarId = default;
        ResultSequence++;
        FinalizationCause = ExpeditionProgressionFinalizationCause.None;
        _progressionResolver?.TryFinalize(
            ExpeditionProgressionFinalizationCause.DefeatConfirmed);
        return true;
    }

    /// <summary>
    /// Records extraction. TASK-80 must subsequently confirm the matching sequence before return is allowed.
    /// </summary>
    internal bool TryMarkExtracted(NetworkObject avatar)
    {
        if (!HasStateAuthority || !IsCurrentRaidingAvatar(avatar))
        {
            return false;
        }

        State = RaidParticipantState.Extracted;
        ResultSequence++;
        ExtractionExperiencePhase =
            ExtractionExperienceTransactionPhase.AwaitingExperiencePreparation;
        return true;
    }

    /// <summary>
    /// Marks an active participant as aborted during an authoritative raid-wide close.
    /// No inventory or stash operation is performed by this transition.
    /// </summary>
    internal bool TryAbortForClosure()
    {
        return TryTransitionToAborted(
            ExpeditionProgressionFinalizationCause.None,
            authorizeReturn: true);
    }

    public void InputAuthorityGained()
    {
        Runner?.GetComponent<NetworkSpawnManager>()?
            .NotifyHostMigrationAuthorityChanged();
    }

    /// <summary>
    /// Terminalizes a raiding participant that did not recover a peer during Host Migration.
    /// This is not a voluntary Return and therefore never publishes Return authorization.
    /// </summary>
    internal PlayerExpeditionProgressionFinalizationResult
        TryFinalizeDefinitiveDisconnectAfterMaterialClosure()
    {
        if (State == RaidParticipantState.Raiding)
        {
            if (!TryTransitionToAborted(
                    ExpeditionProgressionFinalizationCause.DefinitiveDisconnectConfirmed,
                    authorizeReturn: false))
            {
                return PlayerExpeditionProgressionFinalizationResult.FromStatus(
                    PlayerExpeditionProgressionFinalizationStatus.IncompatibleLifecycle);
            }
        }
        else if (!HasStateAuthority || State != RaidParticipantState.Aborted ||
                 FinalizationCause !=
                    ExpeditionProgressionFinalizationCause.DefinitiveDisconnectConfirmed)
        {
            return PlayerExpeditionProgressionFinalizationResult.FromStatus(
                PlayerExpeditionProgressionFinalizationStatus.IncompatibleLifecycle);
        }

        return _progressionResolver != null
            ? _progressionResolver.TryFinalize(
                ExpeditionProgressionFinalizationCause.DefinitiveDisconnectConfirmed)
            : PlayerExpeditionProgressionFinalizationResult.FromStatus(
                PlayerExpeditionProgressionFinalizationStatus.IncompatibleLifecycle);
    }

    /// <summary>
    /// Called by TASK-80 after an idempotent local Loadout commit has been acknowledged.
    /// </summary>
    internal bool TryConfirmExtractionCommit(int resultSequence)
    {
        if (!HasStateAuthority || State != RaidParticipantState.Extracted ||
            resultSequence != ResultSequence ||
            ExtractionExperiencePhase !=
                ExtractionExperienceTransactionPhase.AwaitingPersistenceAck)
        {
            return false;
        }

        ExtractionExperiencePhase = ExtractionExperienceTransactionPhase.ExtractedLootPending;
        return true;
    }

    internal bool TryStoreExtractedLootCandidate(
        in ExtractedLootExperienceCandidate candidate)
    {
        if (!HasStateAuthority || State != RaidParticipantState.Extracted ||
            ExtractionExperiencePhase !=
                ExtractionExperienceTransactionPhase.AwaitingExperiencePreparation ||
            !candidate.Matches(ResultSequence))
        {
            return false;
        }

        ExtractedLootCandidateEligibleValue = candidate.EligibleValue;
        ExtractedLootCandidateExperience = candidate.AwardedExperience;
        ExtractionExperiencePhase =
            ExtractionExperienceTransactionPhase.AwaitingPersistenceAck;
        return true;
    }

    internal bool TryGetExtractedLootCandidate(out ExtractedLootExperienceCandidate candidate)
    {
        candidate = default;
        if (State != RaidParticipantState.Extracted || ResultSequence <= 0 ||
            ExtractionExperiencePhase <
                ExtractionExperienceTransactionPhase.AwaitingPersistenceAck ||
            ExtractionExperiencePhase >
                ExtractionExperienceTransactionPhase.ProgressionPending)
        {
            return false;
        }

        candidate = new ExtractedLootExperienceCandidate(
            ResultSequence,
            new ExtractedLootExperienceCalculation(
                ExtractedLootCandidateEligibleValue,
                ExtractedLootCandidateExperience));
        return candidate.IsValid;
    }

    internal bool TryAdvanceToProgressionPending(int resultSequence)
    {
        if (!HasStateAuthority || State != RaidParticipantState.Extracted ||
            resultSequence != ResultSequence ||
            ExtractionExperiencePhase !=
                ExtractionExperienceTransactionPhase.ExtractedLootPending)
        {
            return false;
        }

        ExtractionExperiencePhase = ExtractionExperienceTransactionPhase.ProgressionPending;
        return true;
    }

    internal PlayerExpeditionProgressionFinalizationResult TryFinalizeExtractionProgression()
    {
        if (_progressionResolver == null)
        {
            return PlayerExpeditionProgressionFinalizationResult.FromStatus(
                PlayerExpeditionProgressionFinalizationStatus.IncompatibleLifecycle);
        }

        PlayerExpeditionProgressionFinalizationResult result =
            _progressionResolver.TryFinalize(
                ExpeditionProgressionFinalizationCause.ExtractionConfirmed);
        if (result.IsCompleted &&
            ExtractionExperiencePhase ==
                ExtractionExperienceTransactionPhase.ProgressionPending)
        {
            ExtractedLootCandidateEligibleValue = 0;
            ExtractedLootCandidateExperience = 0;
            ExtractionExperiencePhase = ExtractionExperienceTransactionPhase.Complete;
        }

        return result;
    }

    /// <summary>
    /// Applies the dynamic-object remap produced by Host Migration restoration.
    /// </summary>
    internal void SetRestoredCurrentAvatar(NetworkId avatarId)
    {
        if (HasStateAuthority)
        {
            CurrentAvatarId = avatarId;
        }
    }

    /// <summary>
    /// Requests an authoritative abandonment. This is a discrete request, never a simulation tick RPC.
    /// </summary>
    public void RequestAbandon()
    {
        if (HasInputAuthority)
        {
            RPC_RequestAbandon();
        }
    }

    /// <summary>
    /// Requests permission to leave the raid after a terminal participant result.
    /// </summary>
    public void RequestReturn()
    {
        if (HasInputAuthority)
        {
            Debug.Log(
                $"[RAID-SPECTATOR] NetworkRaidParticipant.RequestReturn. " +
                $"State={State}, IsServer={Runner != null && Runner.IsServer}.",
                this);
            RPC_RequestReturn();
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestAbandon(RpcInfo info = default)
    {
        if (State != RaidParticipantState.Raiding)
        {
            return;
        }

        NetworkSpawnManager spawnManager = Runner != null
            ? Runner.GetComponent<NetworkSpawnManager>()
            : null;
        string rejectionReason = null;
        if (spawnManager == null ||
            !spawnManager.TryResolveReturnRequester(
                this,
                info.Source,
                out bool requesterIsHost,
                out rejectionReason) ||
            requesterIsHost)
        {
            Debug.LogWarning(
                $"[HM-MULTI] Abandon rejected for the operational Host or an invalid requester. " +
                $"Reason={rejectionReason ?? "Operational Host cannot abandon during an active Raid"}.",
                this);
            return;
        }

        FinalizationCause =
            ExpeditionProgressionFinalizationCause.VoluntaryAbandonConfirmed;
        TryAdvanceVoluntaryAbandon();
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestReturn(RpcInfo info = default)
    {
        if (IsReturnAuthorized)
        {
            return;
        }

        NetworkSpawnManager spawnManager = Runner != null
            ? Runner.GetComponent<NetworkSpawnManager>()
            : null;
        string rejectionReason = null;
        if (spawnManager == null ||
            !spawnManager.TryResolveReturnRequester(
                this,
                info.Source,
                out bool requesterIsHost,
                out rejectionReason))
        {
            Debug.LogWarning(
                $"[HM-MULTI] Return rejected because requester identity could not be resolved. " +
                $"Reason={rejectionReason ?? "Missing NetworkSpawnManager"}.",
                this);
            return;
        }

        if (requesterIsHost)
        {
            Debug.LogWarning(
                "[HM-MULTI] Operational Host Return rejected while the Host must sustain the Raid.",
                this);
            return;
        }

        bool hasProgressionResultSnapshot = _progressionResolver != null &&
            _progressionResolver.TryGetProgressionResult(out ExpeditionProgressionResult _);
        if (!RaidResultsReturnPolicy.CanRequestClientReturn(
                State,
                FinalizationCause,
                IsExtractionProgressionComplete,
                hasProgressionResultSnapshot,
                spawnManager.IsResultsReturnPhaseCompatible))
        {
            return;
        }

        if (State == RaidParticipantState.Defeated)
        {
            if (!spawnManager.TryRegisterControlledReturn(this, out rejectionReason))
            {
                Debug.LogWarning(
                    $"[RAID-SPECTATOR] Client Return rejected. Reason={rejectionReason}.",
                    this);
                return;
            }
        }

        IsReturnAuthorized = true;
    }

    private bool IsCurrentRaidingAvatar(NetworkObject avatar)
    {
        return State == RaidParticipantState.Raiding && avatar != null &&
            avatar.Id.IsValid && avatar.Id == CurrentAvatarId;
    }

    private void TryAdvanceVoluntaryAbandon()
    {
        if (!HasStateAuthority ||
            FinalizationCause !=
                ExpeditionProgressionFinalizationCause.VoluntaryAbandonConfirmed ||
            IsReturnAuthorized)
        {
            return;
        }

        NetworkObject avatar = null;
        if (State == RaidParticipantState.Raiding)
        {
            if (!TryResolveCurrentAvatar(out avatar) || avatar == null ||
                !avatar.TryGetBehaviour(out PlayerCorpseGenerationController corpseGeneration) ||
                !corpseGeneration.TryConvertInventoryToCorpseLoot(Runner.Tick))
            {
                return;
            }

            State = RaidParticipantState.Aborted;
            CurrentAvatarId = default;
            ResultSequence++;
            if (!avatar.InputAuthority.IsNone)
            {
                avatar.AssignInputAuthority(PlayerRef.None);
            }
        }

        if (State != RaidParticipantState.Aborted || _progressionResolver == null)
        {
            return;
        }

        if (_progressionResolver.Committed)
        {
            return;
        }

        PlayerExpeditionProgressionFinalizationResult result =
            _progressionResolver.TryFinalize(
                ExpeditionProgressionFinalizationCause.VoluntaryAbandonConfirmed);
        if (result.IsCompleted)
        {
            Debug.Log(
                $"[HM-MULTI] Client abandon progression resolved; durable local commit remains pending. State={State}.",
                this);
        }
    }

    private bool TryTransitionToAborted(
        ExpeditionProgressionFinalizationCause cause,
        bool authorizeReturn)
    {
        if (!HasStateAuthority || State != RaidParticipantState.Raiding)
        {
            return false;
        }

        State = RaidParticipantState.Aborted;
        CurrentAvatarId = default;
        ResultSequence++;
        FinalizationCause = cause;
        IsReturnAuthorized = authorizeReturn;
        return true;
    }
}
