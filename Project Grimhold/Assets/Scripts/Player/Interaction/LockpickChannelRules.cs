using UnityEngine;

/// <summary>Deterministic authoring and movement checks for contextual lockpicking.</summary>
public static class LockpickChannelRules
{
    public static bool IsValidDuration(float seconds) => float.IsFinite(seconds) && seconds > 0f;

    public static bool HasMoved(Vector2 start, Vector2 current, Vector2 movementIntent) =>
        !float.IsFinite(current.x) || !float.IsFinite(current.y) ||
        !float.IsFinite(movementIntent.x) || !float.IsFinite(movementIntent.y) ||
        movementIntent.sqrMagnitude > 0.000001f || (current - start).sqrMagnitude > 0.000001f;
}
