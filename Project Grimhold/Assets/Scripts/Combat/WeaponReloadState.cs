using Fusion;

/// <summary>One hand's load state and discardable reload deadline, entirely in Fusion tick space.</summary>
public struct WeaponReloadState : INetworkStruct
{
    // The default value is Loaded, including on a fresh spawn or weapon replacement.
    public NetworkBool Unloaded;
    public NetworkBool Reloading;
    public int ReloadEndTick;

    public void MarkShot()
    {
        Unloaded = true;
        Reloading = false;
        ReloadEndTick = 0;
    }

    public void Advance(int tick, float deltaTime, float reloadSeconds, bool canReload)
    {
        if (!Unloaded) return;
        if (!canReload || !AttackTiming.TryGetReleaseTick(tick, reloadSeconds, deltaTime, out int deadline))
        {
            MarkShot(); // Interruption or invalid timing discards all partial progress, not the shot.
            return;
        }
        if (!Reloading)
        {
            Reloading = true;
            ReloadEndTick = deadline;
        }
        if (tick >= ReloadEndTick) this = default;
    }
}
