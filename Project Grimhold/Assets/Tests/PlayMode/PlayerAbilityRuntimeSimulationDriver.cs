#if UNITY_INCLUDE_TESTS
using Fusion;

/// <summary>Copies the exact runtime payload inside Fusion simulation, not a migration substitute.</summary>
public sealed class PlayerAbilityRuntimeSimulationDriver : SimulationBehaviour, IAfterTick
{
    private PlayerAbilityRuntimeNetworkController _target;
    private PlayerAbilityRuntimeNetworkController _source;
    public int CompletionSequence { get; private set; }
    public PlayerAbilityRuntimeNetworkController ObservedRuntime { get; set; }
    public int Slot1Requests { get; private set; }
    public int Slot2Requests { get; private set; }
    public int MissingInputTicks { get; private set; }

    public void AfterTick()
    {
        if (ObservedRuntime == null) return;
        if (!Runner.TryGetInputForPlayer<PlayerNetworkInput>(ObservedRuntime.Object.InputAuthority, out _))
            MissingInputTicks++;
        if (ObservedRuntime.WasActivationRequested(UniversalAbilitySlot.Slot1)) Slot1Requests++;
        if (ObservedRuntime.WasActivationRequested(UniversalAbilitySlot.Slot2)) Slot2Requests++;
    }

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
