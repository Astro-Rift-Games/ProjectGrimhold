using Fusion;
using UnityEngine;

/// <summary>
/// State-Authority-owned provisional experience for one raid participation.
/// Reward producers remain responsible for their own one-shot resolution.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkRaidParticipant))]
public sealed class PlayerExpeditionExperienceLedger : NetworkBehaviour
{
    [Networked]
    public long KillExperience { get; private set; }

    [Networked]
    public long AssistExperience { get; private set; }

    [Networked]
    public long ExplorationExperience { get; private set; }

    [Networked]
    public long ExtractedLootExperience { get; private set; }

    [Networked]
    public NetworkBool IsFrozen { get; private set; }

    [Networked]
    public int ExtractedLootResolvedResultSequence { get; private set; }

    private NetworkRaidParticipant _participant;

    public ExpeditionExperienceSnapshot Snapshot => new(
        KillExperience,
        AssistExperience,
        ExplorationExperience,
        ExtractedLootExperience);

    private void Awake()
    {
        _participant = GetComponent<NetworkRaidParticipant>();
    }

    public override void Spawned()
    {
        if (_participant == null)
        {
            Debug.LogError(
                $"{nameof(PlayerExpeditionExperienceLedger)} requires a co-located " +
                $"{nameof(NetworkRaidParticipant)}.",
                this);
        }
    }

    /// <summary>
    /// Applies one already-resolved normal Dungeon reward. The authoritative producer must
    /// complete its own one-shot transition immediately after this method succeeds.
    /// </summary>
    public bool TryRegisterNormalReward(
        ExpeditionExperienceCategory category,
        long amount,
        out ExpeditionExperienceLedgerFailure failure)
    {
        if (!HasStateAuthority)
        {
            failure = ExpeditionExperienceLedgerFailure.MissingStateAuthority;
            return false;
        }

        if (IsFrozen)
        {
            failure = ExpeditionExperienceLedgerFailure.LedgerFrozen;
            return false;
        }

        if (_participant == null)
        {
            failure = ExpeditionExperienceLedgerFailure.MissingParticipant;
            return false;
        }

        if (_participant.State != RaidParticipantState.Raiding)
        {
            failure = ExpeditionExperienceLedgerFailure.ParticipantNotRaiding;
            return false;
        }

        ExpeditionExperienceSnapshot current = Snapshot;
        if (!ExpeditionExperienceRules.TryApplyNormalReward(
                current,
                category,
                amount,
                out ExpeditionExperienceSnapshot candidate,
                out ExpeditionExperienceApplicationFailure applicationFailure))
        {
            failure = MapFailure(applicationFailure);
            return false;
        }

        KillExperience = candidate.KillExperience;
        AssistExperience = candidate.AssistExperience;
        ExplorationExperience = candidate.ExplorationExperience;
        ExtractedLootExperience = candidate.ExtractedLootExperience;
        failure = ExpeditionExperienceLedgerFailure.None;
        return true;
    }

    /// <summary>
    /// Applies the reward belonging to the extraction result that has already been confirmed.
    /// The extraction coordinator owns one-shot protection and calls this at most once while
    /// retaining the matching pending candidate.
    /// </summary>
    internal ExtractedLootExperienceRegistrationStatus TryRegisterConfirmedExtractedLootReward(
        int resultSequence,
        long amount,
        out ExpeditionExperienceLedgerFailure failure)
    {
        if (!HasStateAuthority)
        {
            failure = ExpeditionExperienceLedgerFailure.MissingStateAuthority;
            return ExtractedLootExperienceRegistrationStatus.Failed;
        }

        if (_participant == null)
        {
            failure = ExpeditionExperienceLedgerFailure.MissingParticipant;
            return ExtractedLootExperienceRegistrationStatus.Failed;
        }

        if (IsFrozen)
        {
            failure = ExpeditionExperienceLedgerFailure.LedgerFrozen;
            return ExtractedLootExperienceRegistrationStatus.Failed;
        }

        if (_participant.State != RaidParticipantState.Extracted)
        {
            failure = ExpeditionExperienceLedgerFailure.ParticipantNotExtracted;
            return ExtractedLootExperienceRegistrationStatus.Failed;
        }

        if (resultSequence <= 0 || resultSequence != _participant.ResultSequence)
        {
            failure = ExpeditionExperienceLedgerFailure.ResultSequenceMismatch;
            return ExtractedLootExperienceRegistrationStatus.Failed;
        }

        if (ExtractedLootResolvedResultSequence == resultSequence)
        {
            failure = ExpeditionExperienceLedgerFailure.None;
            return ExtractedLootExperienceRegistrationStatus.AlreadyResolved;
        }

        if (ExtractedLootResolvedResultSequence != 0)
        {
            failure = ExpeditionExperienceLedgerFailure.ResultSequenceMismatch;
            return ExtractedLootExperienceRegistrationStatus.Failed;
        }

        if (!_participant.IsExtractionCommitConfirmed)
        {
            failure = ExpeditionExperienceLedgerFailure.ExtractionNotConfirmed;
            return ExtractedLootExperienceRegistrationStatus.Failed;
        }

        if (!ExpeditionExperienceRules.TryApplyExtractedLootReward(
                Snapshot,
                amount,
                out ExpeditionExperienceSnapshot candidate,
                out ExpeditionExperienceApplicationFailure applicationFailure))
        {
            failure = MapFailure(applicationFailure);
            return ExtractedLootExperienceRegistrationStatus.Failed;
        }

        KillExperience = candidate.KillExperience;
        AssistExperience = candidate.AssistExperience;
        ExplorationExperience = candidate.ExplorationExperience;
        ExtractedLootExperience = candidate.ExtractedLootExperience;
        ExtractedLootResolvedResultSequence = resultSequence;
        failure = ExpeditionExperienceLedgerFailure.None;
        return ExtractedLootExperienceRegistrationStatus.Applied;
    }

    /// <summary>Final no-fail write used only after Progression preparation succeeds.</summary>
    internal void CommitFreeze()
    {
        IsFrozen = true;
    }

    private static ExpeditionExperienceLedgerFailure MapFailure(
        ExpeditionExperienceApplicationFailure failure) => failure switch
    {
        ExpeditionExperienceApplicationFailure.InvalidState =>
            ExpeditionExperienceLedgerFailure.InvalidState,
        ExpeditionExperienceApplicationFailure.InvalidCategory =>
            ExpeditionExperienceLedgerFailure.InvalidCategory,
        ExpeditionExperienceApplicationFailure.InvalidAmount =>
            ExpeditionExperienceLedgerFailure.InvalidAmount,
        ExpeditionExperienceApplicationFailure.ExtractedLootRequiresExtractionResolution =>
            ExpeditionExperienceLedgerFailure.ExtractedLootRequiresExtractionResolution,
        ExpeditionExperienceApplicationFailure.CategoryOverflow =>
            ExpeditionExperienceLedgerFailure.CategoryOverflow,
        ExpeditionExperienceApplicationFailure.TotalOverflow =>
            ExpeditionExperienceLedgerFailure.TotalOverflow,
        _ => ExpeditionExperienceLedgerFailure.InvalidState
    };
}
