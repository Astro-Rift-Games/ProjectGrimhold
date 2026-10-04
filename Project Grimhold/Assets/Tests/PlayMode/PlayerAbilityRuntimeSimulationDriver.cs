#if UNITY_INCLUDE_TESTS
using Fusion;

/// <summary>Copies the exact runtime payload inside Fusion simulation, not a migration substitute.</summary>
public sealed class PlayerAbilityRuntimeSimulationDriver : SimulationBehaviour
{
    private PlayerAbilityRuntimeNetworkController _target;
    private PlayerAbilityRuntimeNetworkController _source;
    public int CompletionSequence { get; private set; }

    public void RequestCopyState(PlayerAbilityRuntimeNetworkController target,
        PlayerAbilityRuntimeNetworkController source)
    {
        _target = target;
        _source = source;
    }

    public override void FixedUpdateNetwork()
    {
        if (_target == null)
        {
            return;
        }

        _target.CopyStateFrom(_source);
        _target = null;
        _source = null;
        CompletionSequence++;
    }
}
#endif
