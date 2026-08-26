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
    internal bool TryRegisterConfirmedExtractedLootReward(
        int resultSequence,
        long amount,
        out ExpeditionExperienceLedgerFailure failure)
    {
        if (!HasStateAuthority)
        {
            failure = ExpeditionExperienceLedgerFailure.MissingStateAuthority;
            return false;
        }

        if (_participant == null)
        {
            failure = ExpeditionExperienceLedgerFailure.MissingParticipant;
            return false;
        }

        if (_participant.State != RaidParticipantState.Extracted)
        {
            failure = ExpeditionExperienceLedgerFailure.ParticipantNotExtracted;
            return false;
        }

        if (resultSequence <= 0 || resultSequence != _participant.ResultSequence)
        {
            failure = ExpeditionExperienceLedgerFailure.ResultSequenceMismatch;
            return false;
        }

        if (!_participant.IsExtractionCommitConfirmed)
        {
            failure = ExpeditionExperienceLedgerFailure.ExtractionNotConfirmed;
            return false;
        }

        if (!ExpeditionExperienceRules.TryApplyExtractedLootReward(
                Snapshot,
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
