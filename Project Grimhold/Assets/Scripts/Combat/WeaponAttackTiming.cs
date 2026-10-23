using Fusion;

/// <summary>One equipped hand's authoritative interval. Deadlines remain in Fusion tick space while its set is inactive.</summary>
public struct WeaponAttackTiming : INetworkStruct
{
    public int IntervalEndTick;
    public float IntervalSeconds;

    public bool IsReady(int tick) => tick >= IntervalEndTick;

    /// <summary>Validates both independent durations before changing this value.</summary>
    public bool TryStart(int tick, float deltaTime, float interval, float window, out int windowEndTick)
    {
        windowEndTick = 0;
        if (!IsReady(tick) || !AttackTiming.TryGetReleaseTick(tick, interval, deltaTime, out int intervalEnd) ||
            !AttackTiming.TryGetReleaseTick(tick, window, deltaTime, out int windowEnd)) return false;
        IntervalEndTick = intervalEnd;
        IntervalSeconds = interval;
        windowEndTick = windowEnd;
        return true;
    }
}
