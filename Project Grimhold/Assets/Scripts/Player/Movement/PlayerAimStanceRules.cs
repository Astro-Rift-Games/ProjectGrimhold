/// <summary>
/// Acceptance of the aim stance for two-handed ranged weapons. Like the accepted shield defense it is decided from
/// authoritative state, so every peer derives the same aim-driven facing.
/// </summary>
internal static class PlayerAimStanceRules
{
    /// <summary>
    /// The stance needs the secondary action held, a weapon that allows it, an active gameplay phase and a player
    /// who is alive and not downed. It never applies with an active shield, whose defense uses the same input.
    /// </summary>
    internal static bool IsAccepted(
        bool secondaryHeld,
        bool weaponAllowsAimStance,
        bool gameplayPhaseActive,
        bool isAlive,
        bool isDowned,
        bool hasActiveShield)
    {
        return secondaryHeld && weaponAllowsAimStance && gameplayPhaseActive && isAlive && !isDowned &&
            !hasActiveShield;
    }

    /// <summary>Only a two-handed weapon with the aim-stance policy allows the stance.</summary>
    internal static bool WeaponAllows(WeaponDefinition weapon)
    {
        return weapon != null &&
            weapon.Presentation.AimMode == WeaponAimMode.AimStance &&
            weapon.Handedness == WeaponHandedness.TwoHanded;
    }
}
