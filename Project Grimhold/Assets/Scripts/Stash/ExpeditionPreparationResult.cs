/// <summary>
/// Outcome of normalizing the local Loadout and prepared Weapon Equipment immediately before a
/// Town to Raid reservation. Every value other than <see cref="Success"/> is a permanent
/// rejection of the current launch revision.
/// </summary>
public enum ExpeditionPreparationResult
{
    /// <summary>A valid effective weapon is prepared in Weapon Slot 1.</summary>
    Success,

    /// <summary>The local profile aggregate is unavailable or misconfigured.</summary>
    ProfileUnavailable,

    /// <summary>A persisted prepared weapon reference does not resolve to a usable weapon unit.</summary>
    InvalidPreparedWeapon,

    /// <summary>The confirmed character attributes do not satisfy a prepared weapon.</summary>
    AttributeRequirementsNotMet,

    /// <summary>No weapon is prepared and Town has no configured recovery weapon to grant.</summary>
    RecoveryWeaponUnavailable,

    /// <summary>The recovery weapon cannot be placed because the Loadout has no free slot.</summary>
    LoadoutFull,

    /// <summary>Preparation succeeded but the raid loadout reservation was rejected.</summary>
    ReservationFailed,

    /// <summary>The normalized aggregate could not be committed.</summary>
    PersistenceFailed
}
