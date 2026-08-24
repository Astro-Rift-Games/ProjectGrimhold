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
        SetParticipantState = 2
    }

    private static readonly PropertyInfo ParticipantStateProperty =
        typeof(NetworkRaidParticipant).GetProperty(nameof(NetworkRaidParticipant.State));

    private RequestedOperation _operation;
    private PlayerExpeditionExperienceLedger _ledger;
    private NetworkRaidParticipant _participant;
    private ExpeditionExperienceCategory _category;
    private long _amount;
    private RaidParticipantState _participantState;

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
        }

        CompletionSequence++;
    }
}
#endif
