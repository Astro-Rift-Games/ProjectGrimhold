using UnityEngine;

/// <summary>Bounded, axis-aligned pixel-art deformation; never rotates or shears a sprite.</summary>
public static class HitReactionMath
{
    public static Vector2 NormalizeDirection(Vector2 direction)
    {
        if (!IsFinite(direction.x) || !IsFinite(direction.y)) return Vector2.zero;
        float largest = Mathf.Max(Mathf.Abs(direction.x), Mathf.Abs(direction.y));
        if (largest < .00001f) return Vector2.zero;
        // Scale first so even finite very large input cannot overflow sqrMagnitude.
        return (direction / largest).normalized;
    }

    public static Vector3 Scale(Vector2 direction, float compression, float stretchRatio, float weight)
    {
        direction = NormalizeDirection(direction);
        float squash = SnapScale(1f - compression * weight);
        float stretch = SnapScale(1f + compression * stretchRatio * weight);
        return Mathf.Abs(direction.x) > Mathf.Abs(direction.y)
            ? new Vector3(squash, stretch, 1f) : new Vector3(stretch, squash, 1f);
    }

    public static Vector3 Offset(Vector2 direction, int pixels, float pixelsPerUnit, float weight)
    {
        Vector2 offsetPixels = NormalizeDirection(direction) * (pixels * weight);
        return new Vector3(Mathf.Round(offsetPixels.x) / pixelsPerUnit,
            Mathf.Round(offsetPixels.y) / pixelsPerUnit, 0f);
    }

    private static float SnapScale(float scale) => Mathf.Clamp(Mathf.Round(scale * 16f) / 16f, .75f, 1.25f);
    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
