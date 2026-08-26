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
    public int Level { get; }
    public long CurrentExperience { get; }
    public int LastAppliedProgressionResultSequence { get; }
    private readonly LootEntry[] _reservedLoadout;

    /// <summary>
    /// One reserved-loadout reference per Equipment slot, in <see cref="EquipmentSlotRules.AllSlots"/>
    /// order. Zero means the slot is empty; any other value is the entry index plus one.
    /// </summary>
    private readonly int[] _entryIndicesPlusOne;

    public IReadOnlyList<LootEntry> ReservedLoadout => _reservedLoadout ?? Array.Empty<LootEntry>();
    public IReadOnlyList<int> EntryIndicesPlusOne =>
        _entryIndicesPlusOne ?? EmptyIndices;

    public int WeaponSlot1EntryIndexPlusOne => GetEntryIndexPlusOne(EquipmentSlot.WeaponSlot1);
    public int WeaponSlot2EntryIndexPlusOne => GetEntryIndexPlusOne(EquipmentSlot.WeaponSlot2);
    public int HelmetEntryIndexPlusOne => GetEntryIndexPlusOne(EquipmentSlot.Helmet);
    public int ArmorEntryIndexPlusOne => GetEntryIndexPlusOne(EquipmentSlot.Armor);
    public int GlovesEntryIndexPlusOne => GetEntryIndexPlusOne(EquipmentSlot.Gloves);
    public int BootsEntryIndexPlusOne => GetEntryIndexPlusOne(EquipmentSlot.Boots);

    private static readonly int[] EmptyIndices = new int[EquipmentSlotCount];
    private static int EquipmentSlotCount => EquipmentSlotRules.AllSlots.Length;

    public bool IsValid => RaidCode.IsValid &&
                           !string.IsNullOrWhiteSpace(ReservationId) &&
                           ProfileId.IsValid &&
                           PlayerExpeditionProgressionResolver.IsValidBaseline(Level, CurrentExperience) &&
                           LastAppliedProgressionResultSequence >= 0 &&
                           LastAppliedProgressionResultSequence < int.MaxValue &&
                           RaidLoadoutRules.TryValidateShape(ReservedLoadout, out _) &&
                           RaidLoadoutRules.TryValidatePreparedEquipmentReferences(
                               ReservedLoadout,
                               EntryIndicesPlusOne,
                               requireWeapon: true,
                               out _);

    public RaidAdmissionData(
        RaidCode raidCode,
        ProfileId profileId,
        string reservationId,
        IReadOnlyList<LootEntry> reservedLoadout,
        IReadOnlyList<int> entryIndicesPlusOne = null,
        int level = ExperienceCurve.InitialLevel,
        long currentExperience = 0,
        int lastAppliedProgressionResultSequence = 0)
    {
        RaidCode = raidCode;
        ProfileId = profileId;
        ReservationId = reservationId;
        Level = level;
        CurrentExperience = currentExperience;
        LastAppliedProgressionResultSequence = lastAppliedProgressionResultSequence;
        _reservedLoadout = CopyLoadout(reservedLoadout);
        _entryIndicesPlusOne = CopyIndices(entryIndicesPlusOne);
    }

    /// <summary>Resolves the reserved-loadout reference of one Equipment slot.</summary>
    public int GetEntryIndexPlusOne(EquipmentSlot slot)
    {
        EquipmentSlot[] slots = EquipmentSlotRules.AllSlots;
        IReadOnlyList<int> indices = EntryIndicesPlusOne;
        for (int index = 0; index < slots.Length; index++)
        {
            if (slots[index] == slot)
            {
                return index < indices.Count ? indices[index] : 0;
            }
        }

        return 0;
    }

    public static bool TryCreate(
        RaidCode raidCode,
        ProfileId profileId,
        PendingLoadoutReservation reservation,
        int level,
        long currentExperience,
        int lastAppliedProgressionResultSequence,
        out RaidAdmissionData data)
    {
        data = default;
        if (reservation == null)
        {
            return false;
        }

        var entries = new List<LootEntry>(reservation.Items.Count);
        for (int index = 0; index < reservation.Items.Count; index++)
        {
            entries.Add(new LootEntry(reservation.Items[index].LootId, reservation.Items[index].Amount));
        }

        EquipmentSlot[] slots = EquipmentSlotRules.AllSlots;
        var indices = new int[slots.Length];
        for (int index = 0; index < slots.Length; index++)
        {
            indices[index] = FindEntryIndexPlusOne(
                entries,
                reservation.PreparedEquipment.Get(slots[index]));
        }

        data = new RaidAdmissionData(
            raidCode,
            profileId,
            reservation.ReservationId,
            entries,
            indices,
            level,
            currentExperience,
            lastAppliedProgressionResultSequence);
        return data.IsValid;
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

    /// <summary>Normalizes the references to exactly one entry per Equipment slot.</summary>
    private static int[] CopyIndices(IReadOnlyList<int> source)
    {
        var copy = new int[EquipmentSlotRules.AllSlots.Length];
        if (source == null)
        {
            return copy;
        }

        int count = Math.Min(copy.Length, source.Count);
        for (int index = 0; index < count; index++)
        {
            copy[index] = source[index];
        }

        return copy;
    }

    private static int FindEntryIndexPlusOne(IReadOnlyList<LootEntry> entries, LootId lootId)
    {
        if (!lootId.IsValid)
        {
            return 0;
        }

        for (int index = 0; index < entries.Count; index++)
        {
            if (entries[index].LootId == lootId)
            {
                return index + 1;
            }
        }

        return -1;
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

    /// <summary>
    /// Validates the Equipment references of an admission against the reserved loadout. Indices
    /// arrive in <see cref="EquipmentSlotRules.AllSlots"/> order; an entry referenced by several
    /// slots requires one reserved unit per reference. Slot compatibility is not decided here:
    /// State Authority resolves it against the catalog when it equips the spawned player.
    /// </summary>
    public static bool TryValidatePreparedEquipmentReferences(
        IReadOnlyList<LootEntry> entries,
        IReadOnlyList<int> entryIndicesPlusOne,
        bool requireWeapon,
        out string error)
    {
        error = null;
        EquipmentSlot[] slots = EquipmentSlotRules.AllSlots;
        if (entries == null || entryIndicesPlusOne == null || entryIndicesPlusOne.Count != slots.Length)
        {
            error = "Prepared equipment references are missing.";
            return false;
        }

        bool hasWeapon = false;
        for (int index = 0; index < slots.Length; index++)
        {
            int reference = entryIndicesPlusOne[index];
            if (reference < 0 || reference > entries.Count)
            {
                error = "Prepared equipment reference is outside the reserved loadout.";
                return false;
            }

            if (reference > 0 && EquipmentSlotRules.IsWeaponSlot(slots[index]))
            {
                hasWeapon = true;
            }
        }

        if (requireWeapon && !hasWeapon)
        {
            error = "At least one prepared weapon reference is required.";
            return false;
        }

        for (int index = 0; index < slots.Length; index++)
        {
            int reference = entryIndicesPlusOne[index];
            if (reference <= 0)
            {
                continue;
            }

            int references = CountReferences(entryIndicesPlusOne, reference);
            if (entries[reference - 1].Amount < references)
            {
                error = "Prepared slots reference more units than the reserved loadout owns.";
                return false;
            }
        }

        return true;
    }

    private static int CountReferences(IReadOnlyList<int> entryIndicesPlusOne, int reference)
    {
        int count = 0;
        for (int index = 0; index < entryIndicesPlusOne.Count; index++)
        {
            if (entryIndicesPlusOne[index] == reference)
            {
                count++;
            }
        }

        return count;
    }
}
