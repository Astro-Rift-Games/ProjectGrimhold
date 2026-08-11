public enum TownRaidQueueState
{
    Empty = 0,
    WaitingForPlayers = 1,
    Starting = 2,

    // Compatibility aliases for the pre-cohort naming used by the existing
    // network controller and serialized tests.
    Forming = WaitingForPlayers,
    Launching = Starting
}
