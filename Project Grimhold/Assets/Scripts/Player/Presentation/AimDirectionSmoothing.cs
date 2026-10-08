using UnityEngine;

/// <summary>
/// Deterministic, presentation-only resolution of the aim a free-aim weapon renders. Smoothing works on the
/// angle, so a remote aim sample that flips to the opposite side turns through the shortest arc instead of
/// collapsing through a zero-length vector.
/// </summary>
internal static class AimDirectionSmoothing
{
    private const float MinimumSqrMagnitude = 0.0001f;

    /// <summary>
    /// Returns a unit direction. A near-zero or non-finite aim falls back to the facing, then to the previous
    /// direction, then to down. With smoothing and a previous sample, the result advances toward the target by at
    /// most <paramref name="turnRateDegreesPerSecond"/> times <paramref name="deltaTime"/>.
    /// </summary>
    internal static Vector2 Resolve(
        Vector2 aim,
        Vector2 facingFallback,
        Vector2 previous,
        bool hasPrevious,
        float deltaTime,
        float turnRateDegreesPerSecond,
        bool smooth)
    {
        Vector2 target;
        if (IsUsable(aim))
        {
            target = aim.normalized;
        }
        else if (IsUsable(facingFallback))
        {
            target = facingFallback.normalized;
        }
        else if (hasPrevious && IsUsable(previous))
        {
            return previous.normalized;
        }
        else
        {
            return Vector2.down;
        }

        if (!smooth || !hasPrevious || !IsUsable(previous))
        {
            return target;
        }

        float previousAngle = Mathf.Atan2(previous.y, previous.x) * Mathf.Rad2Deg;
        float targetAngle = Mathf.Atan2(target.y, target.x) * Mathf.Rad2Deg;
        float maxStep = Mathf.Max(0f, turnRateDegreesPerSecond) * Mathf.Max(0f, deltaTime);
        float angle = Mathf.MoveTowardsAngle(previousAngle, targetAngle, maxStep) * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
    }

    private static bool IsUsable(Vector2 value)
    {
        return !float.IsNaN(value.x) && !float.IsNaN(value.y) &&
            !float.IsInfinity(value.x) && !float.IsInfinity(value.y) &&
            value.sqrMagnitude >= MinimumSqrMagnitude;
    }
}
