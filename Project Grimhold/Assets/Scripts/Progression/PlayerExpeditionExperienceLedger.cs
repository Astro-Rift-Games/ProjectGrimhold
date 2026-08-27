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
    public int PveKillCount { get; private set; }

    [Networked]
    public int PvpKillCount { get; private set; }

    [Networked]
    public int PveAssistCount { get; private set; }

    [Networked]
    public int PvpAssistCount { get; private set; }

    [Networked]
    public int FirstOpenChestCount { get; private set; }

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
        ExpeditionExperienceSource source,
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

        if (!TryResolveSource(
                source,
                out ExpeditionExperienceCategory category,
                out int currentCount))
        {
            failure = ExpeditionExperienceLedgerFailure.InvalidSource;
            return false;
        }

        if (currentCount == int.MaxValue)
        {
            failure = ExpeditionExperienceLedgerFailure.SourceCountOverflow;
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
        CommitSourceCount(source, currentCount + 1);
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

    private bool TryResolveSource(
        ExpeditionExperienceSource source,
        out ExpeditionExperienceCategory category,
        out int currentCount)
    {
        category = default;
        currentCount = 0;
        switch (source)
        {
            case ExpeditionExperienceSource.PveKill:
                category = ExpeditionExperienceCategory.Kill;
                currentCount = PveKillCount;
                return currentCount >= 0;
            case ExpeditionExperienceSource.PvpKill:
                category = ExpeditionExperienceCategory.Kill;
                currentCount = PvpKillCount;
                return currentCount >= 0;
            case ExpeditionExperienceSource.PveAssist:
                category = ExpeditionExperienceCategory.Assist;
                currentCount = PveAssistCount;
                return currentCount >= 0;
            case ExpeditionExperienceSource.PvpAssist:
                category = ExpeditionExperienceCategory.Assist;
                currentCount = PvpAssistCount;
                return currentCount >= 0;
            case ExpeditionExperienceSource.FirstOpenChest:
                category = ExpeditionExperienceCategory.Exploration;
                currentCount = FirstOpenChestCount;
                return currentCount >= 0;
            default:
                return false;
        }
    }

    private void CommitSourceCount(ExpeditionExperienceSource source, int count)
    {
        switch (source)
        {
            case ExpeditionExperienceSource.PveKill:
                PveKillCount = count;
                break;
            case ExpeditionExperienceSource.PvpKill:
                PvpKillCount = count;
                break;
            case ExpeditionExperienceSource.PveAssist:
                PveAssistCount = count;
                break;
            case ExpeditionExperienceSource.PvpAssist:
                PvpAssistCount = count;
                break;
            case ExpeditionExperienceSource.FirstOpenChest:
                FirstOpenChestCount = count;
                break;
        }
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
