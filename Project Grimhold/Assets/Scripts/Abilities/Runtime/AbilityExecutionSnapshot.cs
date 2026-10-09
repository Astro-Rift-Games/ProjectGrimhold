using Fusion;
using UnityEngine;

/// <summary>Copied simulation state of one slot; resource balances remain character-owned.</summary>
public struct AbilityExecutionSnapshot : INetworkStruct
{
    public AbilityExecutionPhase Phase;
    public uint Sequence;
    public TickTimer Cooldown;
    public TickTimer PhaseDeadline;

    /// <summary>
    /// Normalized aim captured once, with the sequence, when the execution is accepted. It is never
    /// re-read afterwards, survives rebind and Host Migration, and is zero while no execution is active.
    /// </summary>
    public Vector2 AimDirection;

    public bool IsActive => Phase != AbilityExecutionPhase.Idle;
}
