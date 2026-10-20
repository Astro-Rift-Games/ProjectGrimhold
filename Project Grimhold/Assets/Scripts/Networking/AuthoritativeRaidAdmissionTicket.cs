using System;
using System.Collections.Generic;

/// <summary>
/// Authoritative raid admission ticket signed and verified by the backend.
/// Contains the true snapshot of inventory, equipment, level, XP, attributes and abilities.
/// Prevents clients from entering with fabricated or inflated attributes/items.
/// </summary>
public readonly struct AuthoritativeRaidAdmissionTicket : IEquatable<AuthoritativeRaidAdmissionTicket>
{
    public string RawTicket { get; }
    public ProfileId ProfileId { get; }
    public RaidCode RaidCode { get; }
    public string ReservationId { get; }
    public int Level { get; }
    public long Experience { get; }
    public CharacterAttributeState CharacterAttributes { get; }
    public PreparedAbilityLoadout PreparedAbilities { get; }
    public LootEntry[] ReservedItems { get; }
    public PreparedEquipmentLoadout PreparedEquipment { get; }
    public int LastAppliedProgressionResultSequence { get; }
    public long ExpiresAt { get; }

    public bool IsValid =>
        !string.IsNullOrWhiteSpace(RawTicket) &&
        ProfileId.IsValid &&
        RaidCode.IsValid &&
        !string.IsNullOrWhiteSpace(ReservationId);

    public AuthoritativeRaidAdmissionTicket(
        string rawTicket,
        ProfileId profileId,
        RaidCode raidCode,
        string reservationId,
        int level,
        long experience,
        CharacterAttributeState characterAttributes,
        PreparedAbilityLoadout preparedAbilities,
        LootEntry[] reservedItems,
        PreparedEquipmentLoadout preparedEquipment,
        int lastAppliedProgressionResultSequence,
        long expiresAt)
    {
        RawTicket = rawTicket ?? string.Empty;
        ProfileId = profileId;
        RaidCode = raidCode;
        ReservationId = reservationId ?? string.Empty;
        Level = level;
        Experience = experience;
        CharacterAttributes = characterAttributes;
        PreparedAbilities = preparedAbilities;
        ReservedItems = reservedItems ?? Array.Empty<LootEntry>();
        PreparedEquipment = preparedEquipment;
        LastAppliedProgressionResultSequence = lastAppliedProgressionResultSequence;
        ExpiresAt = expiresAt;
    }

    public AuthoritativeRaidAdmissionTicket(
        string rawTicket,
        ProfileId profileId,
        RaidCode raidCode,
        string reservationId,
        int level,
        long experience,
        CharacterAttributeState characterAttributes,
        PreparedAbilityLoadout preparedAbilities,
        LootEntry[] reservedItems,
        int lastAppliedProgressionResultSequence,
        long expiresAt)
        : this(
            rawTicket,
            profileId,
            raidCode,
            reservationId,
            level,
            experience,
            characterAttributes,
            preparedAbilities,
            reservedItems,
            default,
            lastAppliedProgressionResultSequence,
            expiresAt)
    {
    }

    public bool TryToRaidAdmissionData(out RaidAdmissionData data)
    {
        data = default;
        if (!IsValid)
        {
            return false;
        }

        var entries = new List<LootEntry>(ReservedItems != null ? ReservedItems.Length : 0);
        if (ReservedItems != null)
        {
            entries.AddRange(ReservedItems);
        }

        EquipmentSlot[] slots = EquipmentSlotRules.AllSlots;
        var indices = new int[slots.Length];
        for (int index = 0; index < slots.Length; index++)
        {
            LootId eqLootId = PreparedEquipment.Get(slots[index]);
            if (eqLootId.IsValid)
            {
                int entryIndex = FindEntryIndexPlusOne(entries, eqLootId) - 1;
                if (entryIndex >= 0)
                {
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

        data = new RaidAdmissionData(
            RaidCode,
            ProfileId,
            ReservationId,
            entries,
            CharacterAttributes,
            indices,
            Level,
            Experience,
            LastAppliedProgressionResultSequence,
            PreparedEquipment.HasWeaponSetAMainHand
                ? WeaponSetSlot.SetA
                : WeaponSetSlot.SetB,
            PreparedAbilities);
        return data.IsValid;
    }

    private static int FindEntryIndexPlusOne(IReadOnlyList<LootEntry> entries, LootId lootId)
    {
        if (!lootId.IsValid || entries == null)
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

    public bool Equals(AuthoritativeRaidAdmissionTicket other) =>
        string.Equals(RawTicket, other.RawTicket, StringComparison.Ordinal) &&
        ProfileId.Equals(other.ProfileId) &&
        RaidCode.Equals(other.RaidCode) &&
        string.Equals(ReservationId, other.ReservationId, StringComparison.Ordinal);

    public override bool Equals(object obj) =>
        obj is AuthoritativeRaidAdmissionTicket other && Equals(other);

    public override int GetHashCode() =>
        HashCode.Combine(RawTicket, ProfileId, RaidCode, ReservationId);

    public static bool operator ==(AuthoritativeRaidAdmissionTicket left, AuthoritativeRaidAdmissionTicket right) => left.Equals(right);
    public static bool operator !=(AuthoritativeRaidAdmissionTicket left, AuthoritativeRaidAdmissionTicket right) => !left.Equals(right);

    public override string ToString() => $"[AuthoritativeRaidAdmissionTicket Profile={ProfileId.Value} RaidCode={RaidCode.Value} Res={ReservationId} Level={Level}]";
}
