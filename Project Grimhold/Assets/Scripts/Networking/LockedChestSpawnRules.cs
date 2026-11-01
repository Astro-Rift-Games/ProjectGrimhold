/// <summary>Chooses a chest variant without consuming the container's content roll.</summary>
public static class LockedChestSpawnRules
{
    public static bool ShouldSpawnLocked(ulong containerSeed, int chancePercent,
        int spawnIndex, bool guaranteeFirstForTesting)
    {
        return (guaranteeFirstForTesting && chancePercent > 0 && spawnIndex == 0) ||
            ShouldSpawnLocked(containerSeed, chancePercent);
    }

    public static bool ShouldSpawnLocked(ulong containerSeed, int chancePercent)
    {
        if (chancePercent <= 0)
        {
            return false;
        }

        if (chancePercent >= 100)
        {
            return true;
        }

        // A domain-separated SplitMix64 avalanche leaves the content seed untouched.
        // Selection therefore remains stable on retries without advancing any loot RNG.
        unchecked
        {
            ulong roll = containerSeed ^ 0x4C4F434B43484553UL;
            roll += 0x9E3779B97F4A7C15UL;
            roll = (roll ^ (roll >> 30)) * 0xBF58476D1CE4E5B9UL;
            roll = (roll ^ (roll >> 27)) * 0x94D049BB133111EBUL;
            roll ^= roll >> 31;
            return roll % 100UL < (ulong)chancePercent;
        }
    }
}
