using UnityEngine;

/// <summary>
/// Contract representing an active smoke zone in the simulation or presentation (Doc 14 TASK-233).
/// </summary>
public interface ISmokeZone
{
    Vector2 Position { get; }
    float Radius { get; }
    bool IsActive { get; }
    bool ContainsPoint(Vector2 point);
    bool BlocksLine(Vector2 start, Vector2 end, float closeVisionDistance = 1.2f);
}
