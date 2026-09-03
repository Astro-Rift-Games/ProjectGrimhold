#if UNITY_INCLUDE_TESTS
using Fusion;

/// <summary>Executes Stamina mutations inside Fusion simulation for PlayMode tests.</summary>
public sealed class PlayerStaminaSimulationDriver : SimulationBehaviour
{
    private enum RequestedOperation
    {
        None,
        Spend,
        SpendContinuous,
        CopyState
    }

    private RequestedOperation _operation;
    private PlayerStaminaNetworkController _target;
    private PlayerStaminaNetworkController _source;
    private float _amount;

    public int CompletionSequence { get; private set; }
    public bool LastResult { get; private set; }

    public void RequestSpend(PlayerStaminaNetworkController target, float amount)
    {
        Request(RequestedOperation.Spend, target, null, amount);
    }

    public void RequestSpendContinuous(PlayerStaminaNetworkController target, float amount)
    {
        Request(RequestedOperation.SpendContinuous, target, null, amount);
    }

    public void RequestCopyState(
        PlayerStaminaNetworkController target,
        PlayerStaminaNetworkController source)
    {
        Request(RequestedOperation.CopyState, target, source, 0f);
    }

    public override void FixedUpdateNetwork()
    {
        if (_operation == RequestedOperation.None || _target == null)
        {
            return;
        }

        RequestedOperation operation = _operation;
        _operation = RequestedOperation.None;

        switch (operation)
        {
            case RequestedOperation.Spend:
                LastResult = _target.TrySpend(_amount);
                break;
            case RequestedOperation.SpendContinuous:
                LastResult = _target.TrySpendContinuous(_amount);
                break;
            case RequestedOperation.CopyState:
                // NetworkObject.CopyStateFrom requires identical NetworkIds, which only the
                // Host Migration resume spawn supplies. Copy the behaviour storage here so
                // this isolated runner can verify the exact Stamina payload it will traverse.
                _target.CopyStateFrom(_source);
                LastResult = true;
                break;
        }

        CompletionSequence++;
    }

    private void Request(
        RequestedOperation operation,
        PlayerStaminaNetworkController target,
        PlayerStaminaNetworkController source,
        float amount)
    {
        _operation = operation;
        _target = target;
        _source = source;
        _amount = amount;
    }
}
#endif
