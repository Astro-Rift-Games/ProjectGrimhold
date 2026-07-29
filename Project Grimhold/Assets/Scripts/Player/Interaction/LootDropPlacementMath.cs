using UnityEngine;

/// <summary>
/// Produces the stable facing-relative candidate order used for world drops.
/// </summary>
public static class LootDropPlacementMath
{
    public const int CandidateCount = 8;

    private static readonly float[] Angles =
    {
        0f,
        45f,
        -45f,
        90f,
        -90f,
        135f,
        -135f,
        180f
    };

    public static Vector2 ResolveFacing(Vector2 facing)
    {
        if (!IsFinite(facing) || facing.sqrMagnitude < 0.0001f)
        {
            return Vector2.down;
        }

        return facing.normalized;
    }

    public static Vector2 GetCandidate(
        Vector2 origin,
        Vector2 facing,
        float distance,
        int index)
    {
        if (index < 0 || index >= CandidateCount)
        {
            throw new System.ArgumentOutOfRangeException(nameof(index));
        }

        Vector2 direction = Quaternion.Euler(0f, 0f, Angles[index]) * ResolveFacing(facing);
        return origin + direction * Mathf.Max(0f, distance);
    }

    private static bool IsFinite(Vector2 value) =>
        !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
        !float.IsNaN(value.y) && !float.IsInfinity(value.y);
}
