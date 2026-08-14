using UnityEngine;

/// <summary>
/// Provides deterministic, presentation-only calculations for the player's
/// weapon presentation.
/// </summary>
internal static class PlayerWeaponPresentationMath
{
    internal static Vector2 CalculateWeaponPivotPosition(
        Vector2 anchorLocalPosition,
        Vector2 canonicalFacing,
        Vector2 weaponOrbit,
        Vector2 weaponStanceOffset)
    {
        return anchorLocalPosition
            + Vector2.Scale(canonicalFacing, weaponOrbit)
            + weaponStanceOffset;
    }

    internal static Vector2 CalculateAnchorLocalPosition(
        Transform weaponOrbitAnchor,
        Transform weaponPivotParent)
    {
        Vector3 anchorLocalPosition =
            weaponPivotParent.InverseTransformPoint(weaponOrbitAnchor.position);
        return new Vector2(anchorLocalPosition.x, anchorLocalPosition.y);
    }

    internal static float CalculateFacingAngleDegrees(Vector2 safeFacing)
    {
        return Mathf.Atan2(safeFacing.y, safeFacing.x) * Mathf.Rad2Deg;
    }

    internal static bool ShouldMirror(Vector2 safeFacing)
    {
        return safeFacing.x < 0f;
    }

    internal static Vector2 CalculateGripAlignedWeaponPosition(
        Vector2 weaponGripPoint,
        Vector2 weaponScale,
        float weaponAngleCorrection)
    {
        Vector2 scaledGrip = Vector2.Scale(weaponGripPoint, weaponScale);
        float angleRadians = weaponAngleCorrection * Mathf.Deg2Rad;
        float cosine = Mathf.Cos(angleRadians);
        float sine = Mathf.Sin(angleRadians);
        Vector2 rotatedGrip = new Vector2(
            scaledGrip.x * cosine - scaledGrip.y * sine,
            scaledGrip.x * sine + scaledGrip.y * cosine);

        return -rotatedGrip;
    }
}
