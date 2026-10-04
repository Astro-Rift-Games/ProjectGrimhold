using Fusion;

/// <summary>Copied simulation state of one slot; resource balances remain character-owned.</summary>
public struct AbilityExecutionSnapshot : INetworkStruct
{
    public AbilityExecutionPhase Phase;
    public uint Sequence;
    public TickTimer Cooldown;
    public TickTimer PhaseDeadline;

    public bool IsActive => Phase != AbilityExecutionPhase.Idle;
}
