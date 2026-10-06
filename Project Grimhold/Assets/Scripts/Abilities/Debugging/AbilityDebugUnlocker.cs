/// <summary>
/// Unlocks abilities through the normal <see cref="LocalProfileStore"/> transaction. No production
/// source unlocks abilities yet, so developer tools use this to fill the repertoire for testing.
/// </summary>
public static class AbilityDebugUnlocker
{
    public static AbilityUnlockResult Unlock(LocalProfileStore store, AbilityDefinition definition)
    {
        if (store == null)
        {
            return AbilityUnlockResult.ProfileUnavailable;
        }

        return definition == null
            ? AbilityUnlockResult.InvalidAbility
            : store.TryUnlockAbility(definition.AbilityId);
    }

    /// <summary>Unlocks every catalog ability and returns how many were newly unlocked.</summary>
    public static int UnlockAll(LocalProfileStore store, AbilityDefinitionCatalog catalog)
    {
        if (store == null || catalog == null)
        {
            return 0;
        }

        int unlocked = 0;
        foreach (AbilityDefinition definition in catalog.Definitions)
        {
            if (Unlock(store, definition) == AbilityUnlockResult.Success)
            {
                unlocked++;
            }
        }

        return unlocked;
    }
}
