/// <summary>
/// Synchronous State Authority notifications for the lifetime of one Downed cycle.
/// Authoritative collaborators use direct calls, never presentation events.
/// </summary>
public interface IDownedCycleObserver
{
    void NotifyDownedEntered(int cycle, EntityId causerId, int simulationTick);
    void NotifyDownedCycleDefeated(int cycle);
    void NotifyDownedCycleRecovered(int cycle);
}
