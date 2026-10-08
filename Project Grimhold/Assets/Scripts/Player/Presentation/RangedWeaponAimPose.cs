using UnityEngine;

/// <summary>
/// Presentation-only pose of a free-aim ranged weapon, expressed in the space of the weapon pivot's parent.
/// </summary>
internal readonly struct RangedWeaponAimPose
{
    internal RangedWeaponAimPose(
        float pivotAngleDegrees,
        bool mirrored,
        float weaponAngleCorrection,
        Vector2 weaponLocalPosition,
        Vector2 mainHandPosition,
        Vector2 secondaryHandPosition,
        bool frontFacing)
    {
        PivotAngleDegrees = pivotAngleDegrees;
        Mirrored = mirrored;
        WeaponAngleCorrection = weaponAngleCorrection;
        WeaponLocalPosition = weaponLocalPosition;
        MainHandPosition = mainHandPosition;
        SecondaryHandPosition = secondaryHandPosition;
        FrontFacing = frontFacing;
    }

    /// <summary>Z rotation of the weapon pivot toward the aim.</summary>
    internal float PivotAngleDegrees { get; }

    /// <summary>The pivot flips its Y scale when aiming left, as the baked pose does.</summary>
    internal bool Mirrored { get; }

    /// <summary>Z rotation of the weapon visual under the pivot, already resolved for the mirror.</summary>
    internal float WeaponAngleCorrection { get; }

    /// <summary>Position of the weapon visual under the pivot that keeps the grip point on the pivot origin.</summary>
    internal Vector2 WeaponLocalPosition { get; }

    /// <summary>Position of the grip hand in the pivot's parent space (the anchor).</summary>
    internal Vector2 MainHandPosition { get; }

    /// <summary>Position of the second hand on the secondary grip point in the pivot's parent space.</summary>
    internal Vector2 SecondaryHandPosition { get; }

    /// <summary>True when the weapon draws over the body (aiming toward the camera or exactly sideways).</summary>
    internal bool FrontFacing { get; }
}
