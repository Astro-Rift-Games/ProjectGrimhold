#if UNITY_INCLUDE_TESTS
using System.Reflection;
using Fusion;

/// <summary>Executes focused expedition-experience mutations inside a Fusion simulation tick.</summary>
public sealed class ExpeditionExperienceSimulationDriver : SimulationBehaviour
{
    private enum RequestedOperation : byte
    {
        None = 0,
        RegisterReward = 1,
        SetParticipantState = 2,
        ConfigureExtraction = 3,
        RegisterExtractedLootReward = 4,
        ConfigureProgressionFinalization = 5,
        FinalizeProgression = 6,
        ConfigureProgressionBaseline = 7,
        ConfigureSourceCount = 8,
        CopyProgressionState = 9,
        ConfirmProgressionCommit = 10
    }

    private static readonly PropertyInfo ParticipantStateProperty =
        typeof(NetworkRaidParticipant).GetProperty(nameof(NetworkRaidParticipant.State));
    private static readonly PropertyInfo ResultSequenceProperty =
        typeof(NetworkRaidParticipant).GetProperty(nameof(NetworkRaidParticipant.ResultSequence));
    private static readonly PropertyInfo ExtractionPhaseProperty =
        typeof(NetworkRaidParticipant).GetProperty(
            nameof(NetworkRaidParticipant.ExtractionExperiencePhase));
    private static readonly PropertyInfo FinalizationCauseProperty =
        typeof(NetworkRaidParticipant).GetProperty(
            nameof(NetworkRaidParticipant.FinalizationCause));
    private static readonly PropertyInfo BaselineLevelProperty =
        typeof(PlayerExpeditionProgressionResolver).GetProperty(
            nameof(PlayerExpeditionProgressionResolver.BaselineLevel));
    private static readonly PropertyInfo BaselineExperienceProperty =
        typeof(PlayerExpeditionProgressionResolver).GetProperty(
            nameof(PlayerExpeditionProgressionResolver.BaselineExperience));
    private static readonly PropertyInfo CandidateEligibleValueProperty =
        typeof(NetworkRaidParticipant).GetProperty(
            nameof(NetworkRaidParticipant.ExtractedLootCandidateEligibleValue));
    private static readonly PropertyInfo CandidateExperienceProperty =
        typeof(NetworkRaidParticipant).GetProperty(
            nameof(NetworkRaidParticipant.ExtractedLootCandidateExperience));
    private static readonly PropertyInfo ExtractedLootExperienceProperty =
        typeof(PlayerExpeditionExperienceLedger).GetProperty(
            nameof(PlayerExpeditionExperienceLedger.ExtractedLootExperience));
    private static readonly PropertyInfo ExtractedLootSequenceProperty =
        typeof(PlayerExpeditionExperienceLedger).GetProperty(
            nameof(PlayerExpeditionExperienceLedger.ExtractedLootResolvedResultSequence));

    private RequestedOperation _operation;
    private PlayerExpeditionExperienceLedger _ledger;
    private NetworkRaidParticipant _participant;
    private ExpeditionExperienceSource _source;
    private long _amount;
    private RaidParticipantState _participantState;
    private int _resultSequence;
    private bool _isExtractionConfirmed;
    private ExpeditionProgressionFinalizationCause _finalizationCause;
    private ExtractionExperienceTransactionPhase _extractionPhase;
    private PlayerExpeditionProgressionResolver _resolver;
    private int _baselineLevel;
    private long _baselineExperience;
    private long _eligibleExtractedLootValue;
    private long _extractedLootExperience;
    private int _sourceCount;
    private PlayerExpeditionExperienceLedger _sourceLedger;
    private PlayerExpeditionProgressionResolver _sourceResolver;

    public int CompletionSequence { get; private set; }
    public bool LastResult { get; private set; }
    public ExpeditionExperienceLedgerFailure LastFailure { get; private set; }
    public PlayerExpeditionProgressionFinalizationResult LastProgressionResult { get; private set; }

    public void RequestRegisterReward(
        PlayerExpeditionExperienceLedger ledger,
        ExpeditionExperienceSource source,
        long amount)
    {
        _ledger = ledger;
        _source = source;
        _amount = amount;
        _operation = RequestedOperation.RegisterReward;
    }

    public void RequestSetParticipantState(
        NetworkRaidParticipant participant,
        RaidParticipantState state)
    {
        _participant = participant;
        _participantState = state;
        _operation = RequestedOperation.SetParticipantState;
    }

    public void RequestConfigureExtraction(
        NetworkRaidParticipant participant,
        int resultSequence,
        bool isConfirmed)
    {
        _participant = participant;
        _resultSequence = resultSequence;
        _isExtractionConfirmed = isConfirmed;
        _operation = RequestedOperation.ConfigureExtraction;
    }

    public void RequestRegisterExtractedLootReward(
        PlayerExpeditionExperienceLedger ledger,
        int resultSequence,
        long amount)
    {
        _ledger = ledger;
        _resultSequence = resultSequence;
        _amount = amount;
        _operation = RequestedOperation.RegisterExtractedLootReward;
    }

    public void RequestConfigureProgressionFinalization(
        NetworkRaidParticipant participant,
        RaidParticipantState state,
        ExpeditionProgressionFinalizationCause cause,
        ExtractionExperienceTransactionPhase extractionPhase =
            ExtractionExperienceTransactionPhase.None,
        long eligibleExtractedLootValue = 0,
        long extractedLootExperience = 0)
    {
        _participant = participant;
        _participantState = state;
        _finalizationCause = cause;
        _extractionPhase = extractionPhase;
        _eligibleExtractedLootValue = eligibleExtractedLootValue;
        _extractedLootExperience = extractedLootExperience;
        _operation = RequestedOperation.ConfigureProgressionFinalization;
    }

    public void RequestFinalizeProgression(
        PlayerExpeditionProgressionResolver resolver,
        ExpeditionProgressionFinalizationCause cause)
    {
        _resolver = resolver;
        _finalizationCause = cause;
        _operation = RequestedOperation.FinalizeProgression;
    }

    public void RequestConfigureProgressionBaseline(
        PlayerExpeditionProgressionResolver resolver,
        int level,
        long experience)
    {
        _resolver = resolver;
        _baselineLevel = level;
        _baselineExperience = experience;
        _operation = RequestedOperation.ConfigureProgressionBaseline;
    }

    public void RequestConfigureSourceCount(
        PlayerExpeditionExperienceLedger ledger,
        ExpeditionExperienceSource source,
        int count)
    {
        _ledger = ledger;
        _source = source;
        _sourceCount = count;
        _operation = RequestedOperation.ConfigureSourceCount;
    }

    public void RequestCopyProgressionState(
        PlayerExpeditionExperienceLedger targetLedger,
        PlayerExpeditionProgressionResolver targetResolver,
        PlayerExpeditionExperienceLedger sourceLedger,
        PlayerExpeditionProgressionResolver sourceResolver)
    {
        _ledger = targetLedger;
        _resolver = targetResolver;
        _sourceLedger = sourceLedger;
        _sourceResolver = sourceResolver;
        _operation = RequestedOperation.CopyProgressionState;
    }

    public void RequestConfirmProgressionCommit(
        NetworkRaidParticipant participant)
    {
        _participant = participant;
        _resolver = participant != null
            ? participant.GetComponent<PlayerExpeditionProgressionResolver>()
            : null;
        _operation = RequestedOperation.ConfirmProgressionCommit;
    }

    public override void FixedUpdateNetwork()
    {
        RequestedOperation operation = _operation;
        if (operation == RequestedOperation.None)
        {
            return;
        }

        _operation = RequestedOperation.None;
        switch (operation)
        {
            case RequestedOperation.RegisterReward:
                if (_ledger == null)
                {
                    LastResult = false;
                    LastFailure = ExpeditionExperienceLedgerFailure.MissingParticipant;
                    break;
                }

                LastResult = _ledger.TryRegisterNormalReward(
                    _source,
                    _amount,
                    out ExpeditionExperienceLedgerFailure failure);
                LastFailure = failure;
                break;
            case RequestedOperation.SetParticipantState:
                ParticipantStateProperty.SetValue(_participant, _participantState);
                LastResult = true;
                LastFailure = ExpeditionExperienceLedgerFailure.None;
                break;
            case RequestedOperation.ConfigureExtraction:
                ParticipantStateProperty.SetValue(_participant, RaidParticipantState.Extracted);
                ResultSequenceProperty.SetValue(_participant, _resultSequence);
                ExtractionPhaseProperty.SetValue(
                    _participant,
                    _isExtractionConfirmed
                        ? ExtractionExperienceTransactionPhase.ExtractedLootPending
                        : ExtractionExperienceTransactionPhase.AwaitingPersistenceAck);
                LastResult = true;
                LastFailure = ExpeditionExperienceLedgerFailure.None;
                break;
            case RequestedOperation.RegisterExtractedLootReward:
                ExtractedLootExperienceRegistrationStatus registrationStatus =
                    _ledger.TryRegisterConfirmedExtractedLootReward(
                        _resultSequence,
                        _amount,
                        out ExpeditionExperienceLedgerFailure extractedFailure);
                LastResult = registrationStatus !=
                    ExtractedLootExperienceRegistrationStatus.Failed;
                LastFailure = extractedFailure;
                break;
            case RequestedOperation.ConfigureProgressionFinalization:
                ParticipantStateProperty.SetValue(_participant, _participantState);
                FinalizationCauseProperty.SetValue(_participant, _finalizationCause);
                ExtractionPhaseProperty.SetValue(_participant, _extractionPhase);
                if (_extractionPhase == ExtractionExperienceTransactionPhase.ProgressionPending)
                {
                    int sequence = (int)ResultSequenceProperty.GetValue(_participant);
                    if (sequence <= 0)
                    {
                        sequence = 1;
                        ResultSequenceProperty.SetValue(_participant, sequence);
                    }

                    CandidateEligibleValueProperty.SetValue(
                        _participant,
                        _eligibleExtractedLootValue);
                    CandidateExperienceProperty.SetValue(
                        _participant,
                        _extractedLootExperience);
                    PlayerExpeditionExperienceLedger ledger =
                        _participant.GetComponent<PlayerExpeditionExperienceLedger>();
                    ExtractedLootExperienceProperty.SetValue(ledger, _extractedLootExperience);
                    ExtractedLootSequenceProperty.SetValue(ledger, sequence);
                }
                LastResult = true;
                LastFailure = ExpeditionExperienceLedgerFailure.None;
                break;
            case RequestedOperation.FinalizeProgression:
                LastProgressionResult = _resolver.TryFinalize(_finalizationCause);
                LastResult = LastProgressionResult.IsCompleted;
                LastFailure = ExpeditionExperienceLedgerFailure.None;
                break;
            case RequestedOperation.ConfigureProgressionBaseline:
                BaselineLevelProperty.SetValue(_resolver, _baselineLevel);
                BaselineExperienceProperty.SetValue(_resolver, _baselineExperience);
                LastResult = true;
                LastFailure = ExpeditionExperienceLedgerFailure.None;
                break;
            case RequestedOperation.ConfigureSourceCount:
                PropertyInfo countProperty = _source switch
                {
                    ExpeditionExperienceSource.PveKill =>
                        typeof(PlayerExpeditionExperienceLedger).GetProperty(
                            nameof(PlayerExpeditionExperienceLedger.PveKillCount)),
                    ExpeditionExperienceSource.PvpKill =>
                        typeof(PlayerExpeditionExperienceLedger).GetProperty(
                            nameof(PlayerExpeditionExperienceLedger.PvpKillCount)),
                    ExpeditionExperienceSource.PveAssist =>
                        typeof(PlayerExpeditionExperienceLedger).GetProperty(
                            nameof(PlayerExpeditionExperienceLedger.PveAssistCount)),
                    ExpeditionExperienceSource.PvpAssist =>
                        typeof(PlayerExpeditionExperienceLedger).GetProperty(
                            nameof(PlayerExpeditionExperienceLedger.PvpAssistCount)),
                    ExpeditionExperienceSource.FirstOpenChest =>
                        typeof(PlayerExpeditionExperienceLedger).GetProperty(
                            nameof(PlayerExpeditionExperienceLedger.FirstOpenChestCount)),
                    _ => null
                };
                countProperty?.SetValue(_ledger, _sourceCount);
                LastResult = countProperty != null;
                LastFailure = ExpeditionExperienceLedgerFailure.None;
                break;
            case RequestedOperation.CopyProgressionState:
                _ledger.CopyStateFrom(_sourceLedger);
                _resolver.CopyStateFrom(_sourceResolver);
                LastResult = true;
                LastFailure = ExpeditionExperienceLedgerFailure.None;
                break;
            case RequestedOperation.ConfirmProgressionCommit:
                LastResult = true;
                LastFailure = ExpeditionExperienceLedgerFailure.None;
                break;
        }

        CompletionSequence++;
    }
}
#endif
