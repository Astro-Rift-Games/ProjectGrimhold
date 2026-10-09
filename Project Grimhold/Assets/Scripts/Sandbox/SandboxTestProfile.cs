#if UNITY_EDITOR || DEVELOPMENT_BUILD
/// <summary>
/// Pure construction of the synthetic player the ability sandbox runs on. The profile is fixed and
/// deterministic: a dedicated identity, every attribute at <see cref="AttributeValue"/>, every catalog
/// ability unlocked, empty stash and loadout, and the configured recovery weapon prepared so the raid
/// admission accepts it. It is only ever held by an <see cref="InMemoryLocalProfileRepository"/>, so
/// nothing is read from or written to a real profile, to disk, or to the backend.
/// </summary>
public static class SandboxTestProfile
{
    public const string ProfileIdValue = "sandbox-test-player";

    /// <summary>High enough that no ability or weapon attribute requirement can fail.</summary>
    public const int AttributeValue = 30;

    public static ProfileId Id => new ProfileId(ProfileIdValue);

    public static bool IsSandboxProfile(ProfileId profileId) => profileId.IsValid && profileId.Value == ProfileIdValue;

    public static bool TryCreateSnapshot(
        AbilityDefinitionCatalog abilityCatalog,
        LootId recoveryWeaponLootId,
        out LocalProfileSnapshot snapshot,
        out string error)
    {
        snapshot = null;
        error = null;

        if (abilityCatalog == null || abilityCatalog.Definitions.Count == 0)
        {
            error = "The ability catalog is missing or empty.";
            return false;
        }

        if (!recoveryWeaponLootId.IsValid)
        {
            error = "No recovery weapon is configured, so the test player cannot be admitted to a raid.";
            return false;
        }

        if (!CharacterAttributeState.TryCreate(
                AttributeValue, AttributeValue, AttributeValue, AttributeValue, AttributeValue, AttributeValue, 0,
                out CharacterAttributeState attributes))
        {
            error = "The test attribute state is invalid.";
            return false;
        }

        var created = new LocalProfileSnapshot
        {
            ProfileId = Id,
            CharacterAttributes = attributes,
            PreparedEquipment = new PreparedEquipmentLoadout(recoveryWeaponLootId, default)
        };

        foreach (AbilityDefinition definition in abilityCatalog.Definitions)
        {
            if (definition != null && definition.AbilityId.IsValid && !created.UnlockedAbilities.Contains(definition.AbilityId))
            {
                created.UnlockedAbilities.Add(definition.AbilityId);
            }
        }

        snapshot = created;
        return true;
    }

    /// <summary>Builds a store whose only persistence is process memory.</summary>
    public static bool TryCreateStore(
        AbilityDefinitionCatalog abilityCatalog,
        LocalProfilePersistenceConfiguration configuration,
        out LocalProfileStore store,
        out string error)
    {
        store = null;
        if (configuration == null || configuration.LootCatalog == null)
        {
            error = "The local profile configuration or its loot catalog is missing.";
            return false;
        }

        if (!TryCreateSnapshot(abilityCatalog, configuration.RecoveryWeaponLootId, out LocalProfileSnapshot snapshot, out error))
        {
            return false;
        }

        var repository = new InMemoryLocalProfileRepository();
        if (!repository.Initialize(Id, configuration.LootCatalog) || !repository.TrySave(snapshot, out error))
        {
            error ??= "The in-memory test profile could not be initialized.";
            return false;
        }

        store = new LocalProfileStore(
            repository,
            Id,
            configuration.LootCatalog,
            configuration.RecoveryWeaponLootId,
            configuration.MissionCatalog,
            abilityCatalog);
        return true;
    }
}
#endif
