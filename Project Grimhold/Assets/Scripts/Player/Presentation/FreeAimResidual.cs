using UnityEngine;

/// <summary>
/// The extra turn a free-aim weapon takes beyond its bucket. The body and hands play the authored animation for
/// the aim's six-direction bucket; the weapon pivot adds this residual so it points at the exact aim.
/// </summary>
internal static class FreeAimResidual
{
    /// <summary>
    /// Signed angle from the bucket's canonical facing to the aim, wrapped to the shortest turn in
    /// [-180, 180] degrees. Unusable vectors give no residual.
    /// </summary>
    internal static float AngleDegrees(Vector2 bucketFacing, Vector2 aim)
    {
        if (!IsUsable(bucketFacing) || !IsUsable(aim))
        {
            return 0f;
        }

        float bucketAngle = Mathf.Atan2(bucketFacing.y, bucketFacing.x) * Mathf.Rad2Deg;
        float aimAngle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
        return Mathf.DeltaAngle(bucketAngle, aimAngle);
    }

    private static bool IsUsable(Vector2 value)
    {
        return !float.IsNaN(value.x) && !float.IsNaN(value.y) &&
            !float.IsInfinity(value.x) && !float.IsInfinity(value.y) &&
            value.sqrMagnitude > 0.0001f;
    }
}

/// <summary>Rotation of a point around another point, in degrees counterclockwise.</summary>
internal static class PointRotation
{
    internal static Vector2 About(Vector2 point, Vector2 pivot, float degrees)
    {
        if (degrees == 0f)
        {
            return point;
        }

        float radians = degrees * Mathf.Deg2Rad;
        float cosine = Mathf.Cos(radians);
        float sine = Mathf.Sin(radians);
        Vector2 offset = point - pivot;
        return pivot + new Vector2(
            offset.x * cosine - offset.y * sine,
            offset.x * sine + offset.y * cosine);
    }
}
