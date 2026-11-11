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
    public CharacterAttributeState CharacterAttributes { get; }
    public PreparedAbilityLoadout PreparedAbilities { get; }
    public WeaponSetSlot ActiveWeaponSet { get; }

    /// <summary>
    /// Display-only character name resolved by the State Authority from the verified ticket.
    /// Never serialized by <see cref="RaidAdmissionDataCodec"/> and never identity.
    /// </summary>
    public string DisplayName => _displayName ?? string.Empty;
    private readonly string _displayName;
    private readonly LootEntry[] _reservedLoadout;

    /// <summary>
    /// One reserved-loadout reference per Equipment slot, in <see cref="EquipmentSlotRules.AllSlots"/>
    /// order. Zero means the slot is empty; any other value is the entry index plus one.
    /// </summary>
    private readonly int[] _entryIndicesPlusOne;

    public IReadOnlyList<LootEntry> ReservedLoadout => _reservedLoadout ?? Array.Empty<LootEntry>();
    public IReadOnlyList<int> EntryIndicesPlusOne =>
        _entryIndicesPlusOne ?? EmptyIndices;

    public int WeaponSetAMainHandEntryIndexPlusOne => GetEntryIndexPlusOne(EquipmentSlot.WeaponSetAMainHand);
    public int WeaponSetAOffHandEntryIndexPlusOne => GetEntryIndexPlusOne(EquipmentSlot.WeaponSetAOffHand);
    public int WeaponSetBMainHandEntryIndexPlusOne => GetEntryIndexPlusOne(EquipmentSlot.WeaponSetBMainHand);
    public int WeaponSetBOffHandEntryIndexPlusOne => GetEntryIndexPlusOne(EquipmentSlot.WeaponSetBOffHand);
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
                           PreparedAbilityLoadout.TryValidateTransportShape(PreparedAbilities, out _) &&
                           IsValidActiveWeaponSet() &&
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
        in CharacterAttributeState characterAttributes,
        IReadOnlyList<int> entryIndicesPlusOne = null,
        int level = ExperienceCurve.InitialLevel,
        long currentExperience = 0,
        int lastAppliedProgressionResultSequence = 0,
        WeaponSetSlot activeWeaponSet = WeaponSetSlot.None,
        PreparedAbilityLoadout preparedAbilities = default)
    {
        RaidCode = raidCode;
        ProfileId = profileId;
        ReservationId = reservationId;
        Level = level;
        CurrentExperience = currentExperience;
        LastAppliedProgressionResultSequence = lastAppliedProgressionResultSequence;
        CharacterAttributes = characterAttributes;
        PreparedAbilities = preparedAbilities;
        _reservedLoadout = CopyLoadout(reservedLoadout);
        _entryIndicesPlusOne = CopyIndices(entryIndicesPlusOne);
        ActiveWeaponSet = NormalizeActiveWeaponSet(activeWeaponSet, _entryIndicesPlusOne);
        _displayName = null;
    }

    private RaidAdmissionData(in RaidAdmissionData source, string displayName)
    {
        RaidCode = source.RaidCode;
        ProfileId = source.ProfileId;
        ReservationId = source.ReservationId;
        Level = source.Level;
        CurrentExperience = source.CurrentExperience;
        LastAppliedProgressionResultSequence = source.LastAppliedProgressionResultSequence;
        CharacterAttributes = source.CharacterAttributes;
        PreparedAbilities = source.PreparedAbilities;
        ActiveWeaponSet = source.ActiveWeaponSet;
        _reservedLoadout = source._reservedLoadout;
        _entryIndicesPlusOne = source._entryIndicesPlusOne;
        _displayName = displayName;
    }

    /// <summary>Returns a copy carrying a sanitized, display-only character name.</summary>
    public RaidAdmissionData WithDisplayName(string displayName) =>
        new RaidAdmissionData(this, RaidDisplayNamePolicy.Sanitize(displayName));

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
        in CharacterAttributeState characterAttributes,
        int level,
        long currentExperience,
        int lastAppliedProgressionResultSequence,
        out RaidAdmissionData data)
    {
        return TryCreate(
            raidCode,
            profileId,
            reservation,
            characterAttributes,
            default,
            level,
            currentExperience,
            lastAppliedProgressionResultSequence,
            out data);
    }

    public static bool TryCreate(
        RaidCode raidCode,
        ProfileId profileId,
        PendingLoadoutReservation reservation,
        in CharacterAttributeState characterAttributes,
        in PreparedAbilityLoadout preparedAbilities,
        int level,
        long currentExperience,
        int lastAppliedProgressionResultSequence,
        out RaidAdmissionData data)
    {
        data = default;
        if (reservation == null ||
            !PreparedAbilityLoadout.TryValidateTransportShape(preparedAbilities, out _))
        {
            return false;
        }

        if (!ProfileInventoryRules.TryAggregate(reservation.Items, out List<LootEntry> entries))
            return false;

        if (!TryAppendPreparedEquipment(entries, reservation.PreparedEquipment, out int[] indices)) return false;

        data = new RaidAdmissionData(
            raidCode, profileId, reservation.ReservationId, entries, characterAttributes, indices,
            level, currentExperience, lastAppliedProgressionResultSequence,
            (reservation.PreparedEquipment.HasWeaponSetAMainHand || reservation.PreparedEquipment.WeaponSetAOffHand.IsValid)
                ? WeaponSetSlot.SetA : WeaponSetSlot.SetB, preparedAbilities);
        return data.IsValid;
    }

    internal static bool TryAppendPreparedEquipment(List<LootEntry> entries, in PreparedEquipmentLoadout prepared,
        out int[] indices)
    {
        EquipmentSlot[] slots = EquipmentSlotRules.AllSlots;
        indices = new int[slots.Length];
        for (int index = 0; index < slots.Length; index++)
        {
            LootId eqLootId = prepared.Get(slots[index]);
            EquipmentInstance instance = prepared.GetInstance(slots[index]);
            if (eqLootId.IsValid && instance != null)
            {
                entries.Add(new LootEntry(eqLootId, 1, instance.Modifiers, isEquipmentUnit: true));
                indices[index] = entries.Count;
                continue;
            }
            if (eqLootId.IsValid)
            {
                int entryIndex = FindEntryIndexPlusOne(entries, eqLootId) - 1;
                if (entryIndex >= 0)
                {
                    if (entries[entryIndex].Amount == int.MaxValue) return false;
                    entries[entryIndex] = new LootEntry(eqLootId, entries[entryIndex].Amount + 1);
                }
                else
                {
                    entries.Add(new LootEntry(eqLootId, 1));
                }
                indices[index] = FindEntryIndexPlusOne(entries, eqLootId);
            }
            else
            {
                indices[index] = 0;
            }
        }

        return true;
    }

    /// <summary>
    /// Projects combined ownership into its separate physical destinations without changing the
    /// transported references. Each Equipment reference consumes exactly one reserved unit.
    /// </summary>
    public bool TryProjectLoadout(
        LootDefinitionCatalog catalog,
        int backpackCapacity,
        out LootEntry[] backpack,
        out LootEntry[] equipment,
        out string error)
    {
        backpack = equipment = null;
        error = null;
        if (!IsValid || !LootInventoryRules.IsValidSlotCapacity(
                backpackCapacity, LocalProfileSnapshot.MaxLoadoutSlots))
        {
            error = "Raid admission or backpack capacity is invalid.";
            return false;
        }
        if (!RaidLoadoutRules.TryValidate(ReservedLoadout, catalog,
                RaidLoadoutRules.MaximumEntries, out error)) return false;

        var referenced = new int[ReservedLoadout.Count];
        foreach (int reference in EntryIndicesPlusOne)
            if (reference > 0) referenced[reference - 1]++;

        var inventoryEntries = new List<LootEntry>();
        var equipmentEntries = new List<LootEntry>();
        var transported = new Dictionary<LootId, int>();
        int physicalSlots = 0;
        for (int index = 0; index < ReservedLoadout.Count; index++)
        {
            LootEntry entry = ReservedLoadout[index];
            catalog.TryGet(entry.LootId.Value, out LootDefinition definition);
            int prepared = referenced[index];
            bool isEquipment = EquipmentInstance.IsEquipment(definition);
            if (prepared > 0 && !isEquipment)
            {
                error = "Prepared references must identify Equipment.";
                return false;
            }
            int total = transported.TryGetValue(entry.LootId, out int previous)
                ? previous + entry.Amount : entry.Amount;
            transported[entry.LootId] = total;
            if (definition.MaxTransportable > 0 && total > definition.MaxTransportable)
            {
                error = "Loadout exceeds item transport limits.";
                return false;
            }
            if (prepared > 0)
                equipmentEntries.Add(new LootEntry(entry.LootId, prepared, entry.Modifiers,
                    isEquipmentUnit: entry.IsEquipmentUnit));
            int remaining = entry.Amount - prepared;
            if (remaining <= 0) continue;
            inventoryEntries.Add(new LootEntry(entry.LootId, remaining, entry.Modifiers,
                isEquipmentUnit: entry.IsEquipmentUnit));
            int stackMax = definition.StackMax > 0 ? definition.StackMax : 1;
            physicalSlots += isEquipment ? remaining : (remaining - 1) / stackMax + 1;
        }
        if (physicalSlots > backpackCapacity)
        {
            error = "Loadout exceeds physical backpack capacity.";
            return false;
        }
        if (!RaidLoadoutRules.TryValidate(inventoryEntries, catalog, backpackCapacity, out error) ||
            !RaidLoadoutRules.TryValidate(equipmentEntries, catalog, backpackCapacity, out error))
            return false;

        backpack = inventoryEntries.ToArray();
        equipment = equipmentEntries.ToArray();
        return true;
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
            if (entries[index].LootId == lootId && !entries[index].IsEquipmentUnit)
            {
                return index + 1;
            }
        }

        return -1;
    }

    private bool IsValidActiveWeaponSet()
    {
        return ActiveWeaponSet switch
        {
            WeaponSetSlot.SetA => WeaponSetAMainHandEntryIndexPlusOne > 0 || WeaponSetAOffHandEntryIndexPlusOne > 0,
            WeaponSetSlot.SetB => WeaponSetBMainHandEntryIndexPlusOne > 0 || WeaponSetBOffHandEntryIndexPlusOne > 0,
            WeaponSetSlot.None => WeaponSetAMainHandEntryIndexPlusOne == 0 &&
                                  WeaponSetBMainHandEntryIndexPlusOne == 0 && WeaponSetAOffHandEntryIndexPlusOne == 0 && WeaponSetBOffHandEntryIndexPlusOne == 0,
            _ => false
        };
    }

    private static WeaponSetSlot NormalizeActiveWeaponSet(
        WeaponSetSlot requested,
        IReadOnlyList<int> indices)
    {
        int setAMainIndex = Array.IndexOf(EquipmentSlotRules.AllSlots, EquipmentSlot.WeaponSetAMainHand);
        int setBMainIndex = Array.IndexOf(EquipmentSlotRules.AllSlots, EquipmentSlot.WeaponSetBMainHand);
        int setAOffIndex = Array.IndexOf(EquipmentSlotRules.AllSlots, EquipmentSlot.WeaponSetAOffHand);
        int setBOffIndex = Array.IndexOf(EquipmentSlotRules.AllSlots, EquipmentSlot.WeaponSetBOffHand);
        bool hasSetA = indices != null && ((indices.Count > setAMainIndex && indices[setAMainIndex] > 0) ||
            (indices.Count > setAOffIndex && indices[setAOffIndex] > 0));
        bool hasSetB = indices != null && ((indices.Count > setBMainIndex && indices[setBMainIndex] > 0) ||
            (indices.Count > setBOffIndex && indices[setBOffIndex] > 0));
        if (requested == WeaponSetSlot.SetA && hasSetA || requested == WeaponSetSlot.SetB && hasSetB)
        {
            return requested;
        }

        return hasSetA ? WeaponSetSlot.SetA : hasSetB ? WeaponSetSlot.SetB : WeaponSetSlot.None;
    }
}

/// <summary>
/// Shared shape and catalog rules for loadouts crossing the raid admission boundary.
/// </summary>
public static class RaidLoadoutRules
{
    public const int MaximumEntries = LocalProfileSnapshot.MaxLoadoutSlots + 8;
    public const int MaximumAmountPerEntry = 9999;
    public const int MaximumTokenBytes = RaidAdmissionPayloadChunking.MaxPayloadBytes;
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
                // Only explicit amount-one equipment units may repeat a template.
                for (int previous = 0; previous < index; previous++)
                    if (entries[previous].LootId == entry.LootId &&
                        (!entry.IsEquipmentUnit || !entries[previous].IsEquipmentUnit))
                    {
                        error = "Loadout contains duplicate loot IDs.";
                        return false;
                    }
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

        int physicalSlotsUsed = 0;
        for (int index = 0; index < entries.Count; index++)
        {
            if (!catalog.TryGet(entries[index].LootId.Value, out LootDefinition definition) || definition == null)
            {
                error = $"Loadout references unknown loot '{entries[index].LootId.Value}'.";
                return false;
            }
            LootEntry entry = entries[index];
            if (entry.IsEquipmentUnit && !EquipmentInstance.IsEquipment(definition))
            {
                error = "Loadout equipment unit references non-equipment loot.";
                return false;
            }
            if (entry.Modifiers.HasValue && (definition.WeaponDefinition == null ||
                !WeaponInstanceModifiersNetworkData.TryCreate(entry.Modifiers.Value,
                    definition.WeaponDefinition.NaturalScalingAttribute, catalog, out _, out error)))
            {
                error ??= "Loadout modifiers require a weapon.";
                return false;
            }

            int stackMax = definition.StackMax > 0 ? definition.StackMax : 1;
            int slotsForEntry = (entry.Amount + stackMax - 1) / stackMax;
            physicalSlotsUsed += slotsForEntry;
        }

        if (physicalSlotsUsed > slotCapacity)
        {
            error = "Loadout exceeds physical stack capacity or item transport limits.";
            return false;
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

            if (reference > 0 && EquipmentSlotRules.IsHandSlot(slots[index]))
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
