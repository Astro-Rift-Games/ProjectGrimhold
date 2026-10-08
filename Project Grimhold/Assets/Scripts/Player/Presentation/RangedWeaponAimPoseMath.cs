using UnityEngine;

/// <summary>
/// Deterministic, presentation-only pose for a ranged weapon that orbits an anchor toward the continuous aim.
/// Reuses <see cref="PlayerWeaponPresentationMath"/> so a free-aim pose agrees with the baked pose conventions.
/// </summary>
internal static class RangedWeaponAimPoseMath
{
    /// <summary>
    /// Resolves the pose for a normalized aim direction. The weapon pivot sits on <paramref name="anchor"/>, the
    /// grip point rests on the pivot origin, and both hands are placed on the grip and secondary grip points.
    /// </summary>
    internal static RangedWeaponAimPose Resolve(
        Vector2 aim,
        Vector2 anchor,
        Vector2 gripPoint,
        Vector2 secondaryGripPoint,
        float angleCorrection,
        Vector2 weaponScale)
    {
        float pivotAngle = PlayerWeaponPresentationMath.CalculateFacingAngleDegrees(aim);
        bool mirrored = PlayerWeaponPresentationMath.ShouldMirror(aim);
        float resolvedCorrection = PlayerWeaponPresentationMath.ResolveAngleCorrection(angleCorrection, mirrored);
        Vector2 weaponLocalPosition = PlayerWeaponPresentationMath.CalculateGripAlignedWeaponPosition(
            gripPoint,
            weaponScale,
            resolvedCorrection);

        Vector2 mainHand = anchor + ToPivotParentSpace(
            WeaponPointInPivotSpace(gripPoint, weaponScale, resolvedCorrection, weaponLocalPosition),
            pivotAngle,
            mirrored);
        Vector2 secondaryHand = anchor + ToPivotParentSpace(
            WeaponPointInPivotSpace(secondaryGripPoint, weaponScale, resolvedCorrection, weaponLocalPosition),
            pivotAngle,
            mirrored);

        return new RangedWeaponAimPose(
            pivotAngle,
            mirrored,
            resolvedCorrection,
            weaponLocalPosition,
            mainHand,
            secondaryHand,
            // The bucket resolver draws exactly horizontal aims in front, so every aim with y <= 0 is front.
            aim.y <= 0f);
    }

    private static Vector2 WeaponPointInPivotSpace(
        Vector2 weaponPoint,
        Vector2 weaponScale,
        float weaponAngleCorrection,
        Vector2 weaponLocalPosition)
    {
        return weaponLocalPosition + Rotate(Vector2.Scale(weaponPoint, weaponScale), weaponAngleCorrection);
    }

    private static Vector2 ToPivotParentSpace(Vector2 pivotPoint, float pivotAngle, bool mirrored)
    {
        Vector2 scaled = mirrored ? new Vector2(pivotPoint.x, -pivotPoint.y) : pivotPoint;
        return Rotate(scaled, pivotAngle);
    }

    private static Vector2 Rotate(Vector2 value, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float cosine = Mathf.Cos(radians);
        float sine = Mathf.Sin(radians);
        return new Vector2(
            value.x * cosine - value.y * sine,
            value.x * sine + value.y * cosine);
    }
}
