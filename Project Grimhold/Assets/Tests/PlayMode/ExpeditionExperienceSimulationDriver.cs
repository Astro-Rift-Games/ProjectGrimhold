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
        RegisterExtractedLootReward = 4
    }

    private static readonly PropertyInfo ParticipantStateProperty =
        typeof(NetworkRaidParticipant).GetProperty(nameof(NetworkRaidParticipant.State));
    private static readonly PropertyInfo ResultSequenceProperty =
        typeof(NetworkRaidParticipant).GetProperty(nameof(NetworkRaidParticipant.ResultSequence));
    private static readonly PropertyInfo ExtractionConfirmedProperty =
        typeof(NetworkRaidParticipant).GetProperty(nameof(NetworkRaidParticipant.IsExtractionCommitConfirmed));

    private RequestedOperation _operation;
    private PlayerExpeditionExperienceLedger _ledger;
    private NetworkRaidParticipant _participant;
    private ExpeditionExperienceCategory _category;
    private long _amount;
    private RaidParticipantState _participantState;
    private int _resultSequence;
    private bool _isExtractionConfirmed;

    public int CompletionSequence { get; private set; }
    public bool LastResult { get; private set; }
    public ExpeditionExperienceLedgerFailure LastFailure { get; private set; }

    public void RequestRegisterReward(
        PlayerExpeditionExperienceLedger ledger,
        ExpeditionExperienceCategory category,
        long amount)
    {
        _ledger = ledger;
        _category = category;
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
                    _category,
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
                ExtractionConfirmedProperty.SetValue(_participant, (NetworkBool)_isExtractionConfirmed);
                LastResult = true;
                LastFailure = ExpeditionExperienceLedgerFailure.None;
                break;
            case RequestedOperation.RegisterExtractedLootReward:
                LastResult = _ledger.TryRegisterConfirmedExtractedLootReward(
                    _resultSequence,
                    _amount,
                    out ExpeditionExperienceLedgerFailure extractedFailure);
                LastFailure = extractedFailure;
                break;
        }

        CompletionSequence++;
    }
}
#endif
