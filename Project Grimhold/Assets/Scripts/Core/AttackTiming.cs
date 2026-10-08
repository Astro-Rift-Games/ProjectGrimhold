using UnityEngine;

/// <summary>Tick-owned release deadlines and presentation phase mapping; no wall-clock timers.</summary>
public static class AttackTiming
{
    public static bool TryGetReleaseTick(int acceptedTick, float seconds, float deltaTime, out int releaseTick)
    {
        releaseTick = acceptedTick;
        if (!IsFinite(seconds) || seconds < 0f || !IsFinite(deltaTime) || deltaTime <= 0f)
            return false;
        float ticks = Mathf.Ceil(seconds / deltaTime);
        if (!IsFinite(ticks) || ticks >= int.MaxValue || (long)acceptedTick + (long)ticks > int.MaxValue)
            return false;
        releaseTick = acceptedTick + (int)ticks;
        return true;
    }

    public static float ElapsedSeconds(double renderTime, int acceptedTick, float deltaTime) =>
        (float)(renderTime - (double)acceptedTick * deltaTime);

    // Wind-up reaches the authored release exactly at the rounded gameplay deadline.
    // Recovery keeps its authored speed. Melee does not use this mapping.
    public static float ClipSeconds(float elapsed, float scheduledWindup, float authoredRelease) =>
        scheduledWindup > 0f && elapsed < scheduledWindup
            ? Mathf.Max(0f, elapsed) * authoredRelease / scheduledWindup
            : Mathf.Max(0f, elapsed - scheduledWindup + authoredRelease);

    // An aimed shot starts its wind-up already at the drawn frame and reaches the authored release at the rounded
    // aimed deadline, so there is no second draw. Recovery keeps its authored speed.
    public static float AimedClipSeconds(float elapsed, float scheduledWindup, float authoredRelease, float drawnSeconds) =>
        scheduledWindup > 0f && elapsed < scheduledWindup
            ? drawnSeconds + Mathf.Max(0f, elapsed) * (authoredRelease - drawnSeconds) / scheduledWindup
            : Mathf.Max(0f, elapsed - scheduledWindup + authoredRelease);

    public static float ReleaseVfxSeconds(float elapsed, float scheduledWindup, float leadSeconds) =>
        elapsed - scheduledWindup + leadSeconds;

    public static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    public static bool IsFinite(Vector2 value) => IsFinite(value.x) && IsFinite(value.y);
}
