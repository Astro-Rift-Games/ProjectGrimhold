using UnityEngine;

/// <summary>
/// Provides deterministic, presentation-only calculations for the player's
/// weapon presentation.
/// </summary>
internal static class PlayerWeaponPresentationMath
{
    internal static float CalculateFacingAngleDegrees(Vector2 facing)
    {
        return Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg;
    }

    internal static bool ShouldMirror(Vector2 facing)
    {
        return facing.x < 0f;
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
