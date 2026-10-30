/// <summary>
/// Doc 18 §5.2 re-impact contract declared by each projectile type.
/// Every policy still deduplicates a target within one resolution event.
/// </summary>
public enum ProjectileReimpactPolicy : byte
{
    /// <summary>A target can be impacted at most once during the projectile's life.</summary>
    OncePerTarget = 0,

    /// <summary>One impact per continuous contact; leaving and re-entering allows a new impact.</summary>
    OncePerContact = 1,

    /// <summary>Targets in contact receive one impact on each periodic pulse.</summary>
    PeriodicPulse = 2
}
