using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Registry tracking active smoke zones in the match to evaluate line-of-sight
/// occlusion for enemy AI perception and player visibility (Doc 14 TASK-233).
/// </summary>
public static class SmokeZoneRegistry
{
    private static readonly List<ISmokeZone> _activeZones = new();

    public static IReadOnlyList<ISmokeZone> ActiveZones => _activeZones;

    public static void Register(ISmokeZone zone)
    {
        if (zone != null && !_activeZones.Contains(zone))
        {
            _activeZones.Add(zone);
        }
    }

    public static void Unregister(ISmokeZone zone)
    {
        if (zone != null)
        {
            _activeZones.Remove(zone);
        }
    }

    public static void Clear()
    {
        _activeZones.Clear();
    }

    /// <summary>
    /// Checks whether the line between start and end intersects any active smoke zone.
    /// If both points are within the same smoke zone, vision is preserved only if the distance
    /// between them is within <paramref name="closeVisionDistance"/> (default 1.2m).
    /// </summary>
    public static bool IsLineOccluded(Vector2 start, Vector2 end, float closeVisionDistance = 1.2f)
    {
        for (int i = 0; i < _activeZones.Count; i++)
        {
            ISmokeZone zone = _activeZones[i];
            if (zone != null && zone.IsActive && zone.BlocksLine(start, end, closeVisionDistance))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Checks whether the specified point is inside any active smoke zone.
    /// </summary>
    public static bool IsPointInSmoke(Vector2 point)
    {
        for (int i = 0; i < _activeZones.Count; i++)
        {
            ISmokeZone zone = _activeZones[i];
            if (zone != null && zone.IsActive && zone.ContainsPoint(point))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Evaluates ray occlusion against active smoke zones for vision fan calculations.
    /// Returns the distance to the smoke perimeter, or <paramref name="maxDistance"/> if clear.
    /// </summary>
    public static float CastOcclusion(Vector2 origin, Vector2 direction, float maxDistance, float closeVisionDistance = 1.2f)
    {
        if (maxDistance <= 0f) return 0f;
        float nearest = maxDistance;

        for (int i = 0; i < _activeZones.Count; i++)
        {
            ISmokeZone zone = _activeZones[i];
            if (zone == null || !zone.IsActive) continue;

            Vector2 center = zone.Position;
            float radius = zone.Radius;
            Vector2 toCenter = origin - center;
            float distSq = toCenter.sqrMagnitude;

            // If viewer is inside the smoke, vision is capped to closeVisionDistance
            if (distSq <= radius * radius)
            {
                if (closeVisionDistance < nearest)
                {
                    nearest = closeVisionDistance;
                }
                continue;
            }

            // Ray-circle intersection from outside
            float b = Vector2.Dot(direction, toCenter);
            float c = distSq - radius * radius;
            float disc = b * b - c;
            if (disc >= 0f)
            {
                float sqrtDisc = Mathf.Sqrt(disc);
                float t = -b - sqrtDisc;
                if (t > 0f && t < nearest)
                {
                    nearest = t;
                }
            }
        }

        return nearest;
    }

    /// <summary>
    /// Standard geometric helper to test if a circular zone blocks a line segment.
    /// </summary>
    public static bool CircleBlocksLine(Vector2 center, float radius, Vector2 start, Vector2 end, float closeVisionDistance = 1.2f)
    {
        float rSq = radius * radius;
        bool startInside = (start - center).sqrMagnitude <= rSq;
        bool endInside = (end - center).sqrMagnitude <= rSq;

        if (startInside && endInside)
        {
            return (end - start).sqrMagnitude > (closeVisionDistance * closeVisionDistance);
        }

        if (startInside || endInside)
        {
            return true;
        }

        Vector2 d = end - start;
        float lenSq = d.sqrMagnitude;
        if (lenSq < 0.0001f)
        {
            return false;
        }

        float t = Mathf.Clamp01(Vector2.Dot(center - start, d) / lenSq);
        Vector2 closest = start + t * d;
        return (closest - center).sqrMagnitude <= rSq;
    }
}
