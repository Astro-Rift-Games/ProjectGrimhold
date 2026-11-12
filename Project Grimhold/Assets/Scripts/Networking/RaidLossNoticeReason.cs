/// <summary>Why a Raid ended with the carried loadout lost and no Results screen in the Raid.</summary>
public enum RaidLossNoticeReason
{
    /// <summary>Bootstrap failure or expected-admission deadline closed the Raid before it started.</summary>
    RaidStartFailed = 0,

    /// <summary>The local runner shut down for good while the Raid was running.</summary>
    ConnectionLost = 1
}
