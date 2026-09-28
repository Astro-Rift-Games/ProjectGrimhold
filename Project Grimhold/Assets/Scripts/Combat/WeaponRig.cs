/// <summary>
/// Selects what owns the spatial pose of the equipped Main Hand weapon's single held visual.
/// Static presentation data; equipment ownership stays Main Hand and nothing is replicated.
/// </summary>
public enum WeaponRig : byte
{
    /// <summary>The main hand owns the pose: the visual follows RightHand through MainHandGrip.</summary>
    HandHeld = 0,

    /// <summary>
    /// The weapon owns its pose: the visual follows the WeaponPose transform, and the baked attack places the
    /// hands on the weapon (the grip hand on its grip, the other hand on its authored target) instead of the
    /// hands carrying it.
    /// </summary>
    WeaponDriven = 1
}
