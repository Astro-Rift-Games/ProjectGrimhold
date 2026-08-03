#if UNITY_INCLUDE_TESTS
using System;
using Fusion;

/// <summary>
/// Executes one test operation inside an authoritative Fusion simulation tick.
/// </summary>
public sealed class ExtractionProgressSimulationDriver : SimulationBehaviour
{
    public Action<NetworkRunner> PendingAction { get; set; }
    public Exception LastException { get; private set; }

    public override void FixedUpdateNetwork()
    {
        Action<NetworkRunner> action = PendingAction;
        if (action == null)
        {
            return;
        }

        PendingAction = null;
        try
        {
            action(Runner);
        }
        catch (Exception exception)
        {
            LastException = exception;
        }
    }

    public void ClearException()
    {
        LastException = null;
    }
}
#endif
