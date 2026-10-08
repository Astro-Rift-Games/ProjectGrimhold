/// <summary>
/// Selects how a weapon's held visual follows the player's aim.
/// Static presentation data; nothing is replicated.
/// </summary>
public enum WeaponAimMode : byte
{
    /// <summary>The visual follows the baked six-bucket animation facing.</summary>
    BakedFacing = 0,

    /// <summary>The visual orbits the player and follows the continuous 360 degree aim direction.</summary>
    FreeAim = 1,

    /// <summary>
    /// A two-handed ranged weapon that follows the aim only while the player holds the aim stance or an attack
    /// faces the aim. Otherwise the baked pose plays unchanged.
    /// </summary>
    AimStance = 2
}
