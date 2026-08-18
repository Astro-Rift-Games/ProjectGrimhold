using System;
using System.Collections.Generic;

/// <summary>
/// Immutable connection payload used only when joining a manifest-backed raid.
/// Town continues to use <see cref="PlayerJoinData"/> and its existing codec.
/// </summary>
public readonly struct RaidAdmissionData
{
    public RaidCode RaidCode { get; }
    public ProfileId ProfileId { get; }
    public string ReservationId { get; }
    private readonly LootEntry[] _reservedLoadout;
    public IReadOnlyList<LootEntry> ReservedLoadout => _reservedLoadout ?? Array.Empty<LootEntry>();

    public bool IsValid => RaidCode.IsValid &&
                           !string.IsNullOrWhiteSpace(ReservationId) &&
                           ProfileId.IsValid &&
                           RaidLoadoutRules.TryValidateShape(ReservedLoadout, out _);

    public RaidAdmissionData(
        RaidCode raidCode,
        ProfileId profileId,
        string reservationId,
        IReadOnlyList<LootEntry> reservedLoadout)
    {
        RaidCode = raidCode;
        ProfileId = profileId;
        ReservationId = reservationId;
        _reservedLoadout = CopyLoadout(reservedLoadout);
    }

    public PlayerJoinData ToPlayerJoinData()
    {
        return new PlayerJoinData(ProfileId);
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
