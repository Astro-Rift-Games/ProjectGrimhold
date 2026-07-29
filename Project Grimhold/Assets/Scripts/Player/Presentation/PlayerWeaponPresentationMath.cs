using UnityEngine;

/// <summary>
/// Provides deterministic, presentation-only calculations for the player's
/// shared hand and weapon composition.
/// </summary>
internal static class PlayerWeaponPresentationMath
{
    private const float MinimumDirectionSqrMagnitude = 0.0001f;

    internal static Vector2 ResolveSafeFacing(Vector2 facing, Vector2 previousSafeFacing)
    {
        if (IsFiniteNonZero(facing))
        {
            return facing.normalized;
        }

        if (IsFiniteNonZero(previousSafeFacing))
        {
            return previousSafeFacing.normalized;
        }

        return Vector2.down;
    }

    internal static Vector2 CalculateHandPosition(
        Vector2 anchorLocalPosition,
        Vector2 safeFacing,
        Vector2 handOrbit,
        Vector2 weaponStanceOffset)
    {
        return anchorLocalPosition
            + Vector2.Scale(safeFacing, handOrbit)
            + weaponStanceOffset;
    }

    internal static Vector2 CalculateAnchorLocalPosition(
        Transform handOrbitAnchor,
        Transform handPivotParent)
    {
        Vector3 anchorLocalPosition =
            handPivotParent.InverseTransformPoint(handOrbitAnchor.position);
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

    internal static int CalculateWeaponSortingOrder(
        Vector2 safeFacing,
        int frontOrder,
        int backOrder)
    {
        return safeFacing.y > 0f ? backOrder : frontOrder;
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

    private static bool IsFiniteNonZero(Vector2 direction)
    {
        if (float.IsNaN(direction.x)
            || float.IsInfinity(direction.x)
            || float.IsNaN(direction.y)
            || float.IsInfinity(direction.y))
        {
            return false;
        }

        float sqrMagnitude = direction.sqrMagnitude;
        return !float.IsNaN(sqrMagnitude)
            && !float.IsInfinity(sqrMagnitude)
            && sqrMagnitude >= MinimumDirectionSqrMagnitude;
    }
}
