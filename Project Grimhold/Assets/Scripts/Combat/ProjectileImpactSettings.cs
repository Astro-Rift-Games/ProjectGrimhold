using UnityEngine;

/// <summary>
/// Validated per-projectile impact contract: re-impact policy, impact-count termination and pulse cadence.
/// Static prefab configuration; never mutated during flight.
/// </summary>
public readonly struct ProjectileImpactSettings
{
    public const int MaxImpactCountLimit = 255;

    public ProjectileReimpactPolicy Policy { get; }
    public int MaxImpactCount { get; }
    public int PulseIntervalTicks { get; }

    private ProjectileImpactSettings(ProjectileReimpactPolicy policy, int maxImpactCount, int pulseIntervalTicks)
    {
        Policy = policy;
        MaxImpactCount = maxImpactCount;
        PulseIntervalTicks = pulseIntervalTicks;
    }

    /// <summary>Legacy contract: the first accepted contact terminates the projectile.</summary>
    public static ProjectileImpactSettings SingleImpact => new ProjectileImpactSettings(
        ProjectileReimpactPolicy.OncePerTarget, 1, 0);

    /// <summary>True when continuous contact must be observed even while the projectile is inside a target.</summary>
    public bool TracksContinuousContact => Policy != ProjectileReimpactPolicy.OncePerTarget;

    public static bool TryCreate(ProjectileReimpactPolicy policy, int maxImpactCount, float pulseIntervalSeconds,
        float tickDeltaSeconds, out ProjectileImpactSettings settings, out string error)
    {
        settings = SingleImpact;
        if (!System.Enum.IsDefined(typeof(ProjectileReimpactPolicy), policy))
            error = "re-impact policy is undefined.";
        else if (maxImpactCount < 1 || maxImpactCount > MaxImpactCountLimit)
            error = $"max impact count must be in [1, {MaxImpactCountLimit}].";
        else if (policy == ProjectileReimpactPolicy.OncePerTarget && maxImpactCount > ProjectileImpactLedger.Capacity)
            error = $"once-per-target projectiles support at most {ProjectileImpactLedger.Capacity} impacts.";
        else if (policy == ProjectileReimpactPolicy.PeriodicPulse &&
                 (!AttackTiming.IsFinite(pulseIntervalSeconds) || pulseIntervalSeconds <= 0f))
            error = "periodic pulses require a finite positive pulse interval.";
        else if (policy == ProjectileReimpactPolicy.PeriodicPulse &&
                 (!AttackTiming.IsFinite(tickDeltaSeconds) || tickDeltaSeconds <= 0f))
            error = "periodic pulses require a finite positive tick delta.";
        else
            error = string.Empty;

        if (error.Length != 0) return false;
        int intervalTicks = policy == ProjectileReimpactPolicy.PeriodicPulse
            ? Mathf.Max(1, Mathf.RoundToInt(pulseIntervalSeconds / tickDeltaSeconds))
            : 0;
        settings = new ProjectileImpactSettings(policy, maxImpactCount, intervalTicks);
        return true;
    }
}
