using System;
using System.Collections.Generic;

/// <summary>
/// Immutable connection payload used only when joining a manifest-backed raid.
/// Town continues to use <see cref="PlayerJoinData"/> and its existing codec.
/// </summary>
public readonly struct RaidAdmissionData
{
    public string RaidId { get; }
    public string AccessSecret { get; }
    public ProfileId ProfileId { get; }
    public PlayerClassId SelectedBuild { get; }
    public string ReservationId { get; }
    private readonly LootEntry[] _reservedLoadout;
    public IReadOnlyList<LootEntry> ReservedLoadout => _reservedLoadout ?? Array.Empty<LootEntry>();

    public bool IsValid => !string.IsNullOrWhiteSpace(RaidId) &&
                           !string.IsNullOrWhiteSpace(AccessSecret) &&
                           !string.IsNullOrWhiteSpace(ReservationId) &&
                           ProfileId.IsValid &&
                           PlayerJoinDataCodec.IsSupported(SelectedBuild) &&
                           RaidLoadoutRules.TryValidateShape(ReservedLoadout, out _);

    public RaidAdmissionData(
        string raidId,
        string accessSecret,
        ProfileId profileId,
        PlayerClassId selectedBuild,
        string reservationId,
        IReadOnlyList<LootEntry> reservedLoadout)
    {
        RaidId = raidId;
        AccessSecret = accessSecret;
        ProfileId = profileId;
        SelectedBuild = selectedBuild;
        ReservationId = reservationId;
        _reservedLoadout = CopyLoadout(reservedLoadout);
    }

    [Obsolete("Use the constructor that supplies a reservation and loadout.")]
    public RaidAdmissionData(string raidId, string accessSecret, ProfileId profileId, PlayerClassId selectedBuild)
        : this(raidId, accessSecret, profileId, selectedBuild, "legacy-development", Array.Empty<LootEntry>())
    {
    }

    public PlayerJoinData ToPlayerJoinData()
    {
        return new PlayerJoinData(SelectedBuild, ProfileId);
    }

    private static LootEntry[] CopyLoadout(IReadOnlyList<LootEntry> source)
    {
        if (source == null)
        {
            return null;
        }

        var copy = new LootEntry[source.Count];
        for (int index = 0; index < source.Count; index++)
        {
            copy[index] = source[index];
        }

        return copy;
    }
}

/// <summary>
/// Shared shape and catalog rules for loadouts crossing the raid admission boundary.
/// </summary>
public static class RaidLoadoutRules
{
    public const int MaximumEntries = LocalProfileSnapshot.MaxLoadoutSlots;
    public const int MaximumAmountPerEntry = 9999;
    public const int MaximumTokenBytes = 512;
    public const int MaximumTextBytes = 64;

    public static bool TryValidateShape(IReadOnlyList<LootEntry> entries, out string error)
    {
        error = null;
        if (entries == null)
        {
            error = "Loadout is missing.";
            return false;
        }

        if (entries.Count > MaximumEntries)
        {
            error = "Loadout exceeds the maximum number of entries.";
            return false;
        }

        var seen = new HashSet<LootId>();
        for (int index = 0; index < entries.Count; index++)
        {
            LootEntry entry = entries[index];
            if (!entry.IsValid || entry.Amount > MaximumAmountPerEntry)
            {
                error = "Loadout contains an invalid quantity or item.";
                return false;
            }

            if (!seen.Add(entry.LootId))
            {
                error = "Loadout contains duplicate loot IDs.";
                return false;
            }
        }

        return true;
    }

    public static bool TryValidate(
        IReadOnlyList<LootEntry> entries,
        LootDefinitionCatalog catalog,
        int slotCapacity,
        out string error)
    {
        if (!TryValidateShape(entries, out error))
        {
            return false;
        }

        if (!LootInventoryRules.IsValidSlotCapacity(slotCapacity, MaximumEntries))
        {
            error = "Loadout slot capacity is invalid.";
            return false;
        }

        if (entries.Count > slotCapacity)
        {
            error = "Loadout exceeds the receiver capacity.";
            return false;
        }

        if (catalog == null)
        {
            error = "Loot catalog is missing.";
            return false;
        }

        for (int index = 0; index < entries.Count; index++)
        {
            if (!catalog.TryGet(entries[index].LootId.Value, out _))
            {
                error = $"Loadout references unknown loot '{entries[index].LootId.Value}'.";
                return false;
            }
        }

        return true;
    }
}
