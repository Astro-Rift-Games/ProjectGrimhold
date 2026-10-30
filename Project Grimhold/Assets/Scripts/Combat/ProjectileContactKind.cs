/// <summary>
/// Classified projectile contact. The declaration order is the deterministic tie-break for
/// contacts at the same distance: defenses first, then entity targets, then world blockers.
/// </summary>
public enum ProjectileContactKind : byte
{
    /// <summary>Defensive interceptor (Protective Orb slot) that terminates the projectile when consumed.</summary>
    Interceptor = 0,

    /// <summary>Eligible damage target resolved through the projectile's re-impact policy.</summary>
    Target = 1,

    /// <summary>Solid scenario geometry or a registered non-target world object; always terminates.</summary>
    Blocker = 2
}
