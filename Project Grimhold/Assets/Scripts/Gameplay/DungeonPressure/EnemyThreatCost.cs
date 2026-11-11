using System;

public static class EnemyThreatCost
{
    public const int DefaultCost = 1;
    public const int SpecterCost = 2;

    public static int GetCost()
    {
        return DefaultCost;
    }

    public static int GetCost(EnemyCharacter enemy)
    {
        if (enemy == null) return DefaultCost;
        if (enemy.SpecterController != null) return SpecterCost;
        return DefaultCost;
    }

    public static int GetCost(string enemyId)
    {
        if (string.Equals(enemyId, "specter", StringComparison.OrdinalIgnoreCase))
        {
            return SpecterCost;
        }
        return DefaultCost;
    }
}
