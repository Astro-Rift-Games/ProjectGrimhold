/// <summary>
/// Outcome of one projectile resolution event.
/// </summary>
public readonly struct ProjectileContactResolution
{
    public bool IsTerminated { get; }

    /// <summary>Index of the terminating contact in the ordered buffer, or -1 when not terminated.</summary>
    public int TerminalIndex { get; }

    /// <summary>Total impacts applied during the projectile's life, including this event.</summary>
    public int ImpactsApplied { get; }

    public ProjectileContactResolution(bool isTerminated, int terminalIndex, int impactsApplied)
    {
        IsTerminated = isTerminated;
        TerminalIndex = terminalIndex;
        ImpactsApplied = impactsApplied;
    }
}
