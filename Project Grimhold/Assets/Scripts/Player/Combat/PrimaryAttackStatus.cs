/// <summary>
/// Immutable presentation snapshot of the primary attack's readiness and cooldown.
/// The combat controller owns and produces these values from gameplay state.
/// </summary>
public readonly struct PrimaryAttackStatus
{
    /// <summary>
    /// Gets whether the stable gameplay prerequisites currently allow an attack.
    /// </summary>
    public bool IsAvailable { get; }

    /// <summary>
    /// Gets the duration of the attack that started the observed cooldown.
    /// This value does not necessarily describe the currently assigned local strategy.
    /// </summary>
    public float CooldownDurationSeconds { get; }

    /// <summary>
    /// Gets the remaining Fusion cooldown time in seconds.
    /// </summary>
    public float CooldownRemainingSeconds { get; }

    /// <summary>
    /// Creates a read-only presentation snapshot produced by the combat controller.
    /// </summary>
    public PrimaryAttackStatus(
        bool isAvailable,
        float cooldownDurationSeconds,
        float cooldownRemainingSeconds)
    {
        IsAvailable = isAvailable;
        CooldownDurationSeconds = cooldownDurationSeconds;
        CooldownRemainingSeconds = cooldownRemainingSeconds;
    }
}
