#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pure placement maths for the sandbox spawner. No Unity scene or Fusion dependencies.
/// </summary>
public static class SandboxSpawnPlanner
{
    public const int DefaultMaxCount = 50;
    public const float MinimumSpacing = 0.5f;

    public static int ClampCount(int count, int maxCount)
    {
        int upper = Math.Max(1, maxCount);
        return Math.Clamp(count, 1, upper);
    }

    /// <summary>
    /// Returns world positions for <paramref name="count"/> entities (clamped to 1..maxCount)
    /// laid out around <paramref name="center"/>. Invalid spacing falls back to
    /// <see cref="MinimumSpacing"/>.
    /// </summary>
    public static IReadOnlyList<Vector2> Plan(
        Vector2 center,
        int count,
        SandboxSpawnPattern pattern,
        float spacing,
        int maxCount = DefaultMaxCount)
    {
        int total = ClampCount(count, maxCount);
        float step = SanitizeSpacing(spacing);
        var positions = new List<Vector2>(total);

        if (total == 1)
        {
            positions.Add(center);
            return positions;
        }

        switch (pattern)
        {
            case SandboxSpawnPattern.Line:
                PlanLine(center, total, step, positions);
                break;
            case SandboxSpawnPattern.Grid:
                PlanGrid(center, total, step, positions);
                break;
            default:
                PlanRing(center, total, step, positions);
                break;
        }

        return positions;
    }

    private static float SanitizeSpacing(float spacing)
    {
        if (!float.IsFinite(spacing))
        {
            return MinimumSpacing;
        }

        return Mathf.Max(spacing, MinimumSpacing);
    }

    private static void PlanLine(Vector2 center, int total, float step, List<Vector2> positions)
    {
        float origin = -(total - 1) * 0.5f * step;
        for (int i = 0; i < total; i++)
        {
            positions.Add(center + new Vector2(origin + i * step, 0f));
        }
    }

    private static void PlanGrid(Vector2 center, int total, float step, List<Vector2> positions)
    {
        int columns = Mathf.CeilToInt(Mathf.Sqrt(total));
        int rows = Mathf.CeilToInt(total / (float)columns);
        for (int i = 0; i < total; i++)
        {
            int row = i / columns;
            int column = i % columns;
            float x = (column - (columns - 1) * 0.5f) * step;
            float y = ((rows - 1) * 0.5f - row) * step;
            positions.Add(center + new Vector2(x, y));
        }
    }

    private static void PlanRing(Vector2 center, int total, float step, List<Vector2> positions)
    {
        // Radius keeps neighbours at least `step` apart (chord = 2 * r * sin(pi / n)).
        float radius = Mathf.Max(step, step / (2f * Mathf.Sin(Mathf.PI / total)));
        float angleStep = Mathf.PI * 2f / total;
        for (int i = 0; i < total; i++)
        {
            float angle = i * angleStep;
            positions.Add(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
        }
    }
}
#endif
