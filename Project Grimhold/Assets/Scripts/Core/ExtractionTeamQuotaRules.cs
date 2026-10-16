using System;

/// <summary>
/// Pure deterministic rules for deriving and maintaining team extraction progress quotas.
/// Enforces that quota is established strictly from initial team size (Solo or Duo)
/// and is never reduced or recalculated due to mid-raid composition changes.
/// </summary>
public static class ExtractionTeamQuotaRules
{
    /// <summary>
    /// Resolves the extraction quota for an initial team size from the given configuration.
    /// </summary>
    public static bool TryGetQuota(int initialTeamSize, ExtractionConfig config, out int quota)
    {
        quota = 0;
        if (config == null)
        {
            return false;
        }

        return TryGetQuota(initialTeamSize, config.SoloProgressQuota, config.DuoProgressQuota, out quota);
    }

    /// <summary>
    /// Resolves the extraction quota for an initial team size from raw solo/duo quota values.
    /// </summary>
    public static bool TryGetQuota(int initialTeamSize, int soloQuota, int duoQuota, out int quota)
    {
        quota = 0;
        if (soloQuota <= 0 || duoQuota <= 0)
        {
            return false;
        }

        if (initialTeamSize == 1)
        {
            quota = soloQuota;
            return true;
        }

        if (initialTeamSize == 2)
        {
            quota = duoQuota;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Returns the extraction quota for an initial team size or throws if the team size is invalid.
    /// </summary>
    public static int GetQuota(int initialTeamSize, int soloQuota, int duoQuota)
    {
        if (TryGetQuota(initialTeamSize, soloQuota, duoQuota, out int quota))
        {
            return quota;
        }

        throw new ArgumentOutOfRangeException(
            nameof(initialTeamSize),
            $"Initial team size {initialTeamSize} is invalid. Only Solo (1) and Duo (2) are supported.");
    }
}
