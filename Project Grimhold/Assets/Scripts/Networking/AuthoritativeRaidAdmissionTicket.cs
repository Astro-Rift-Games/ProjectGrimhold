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

    public bool IsExpired(long currentUnixSeconds) =>
        ExpiresAt > 0 && currentUnixSeconds > ExpiresAt;

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

    public bool TryToRaidAdmissionData(out RaidAdmissionData data, PreparedAbilityLoadout fallbackAbilities = default,
        LootDefinitionCatalog catalog = null)
    {
        data = default;
        if (!IsValid)
        {
            return false;
        }

        List<LootEntry> entries;
        if (catalog == null) entries = new List<LootEntry>(ReservedItems ?? Array.Empty<LootEntry>());
        else
        {
            // Classify plain equipment against the owning catalog; the ID-free JSON intentionally
            // has no durable instance marker. Reuse the Town-to-Raid projection for ordering and folding.
            var physical = new StashItem[ReservedItems?.Length ?? 0];
            for (int index = 0; index < physical.Length; index++)
            {
                LootEntry entry = ReservedItems[index];
                if (!entry.IsValid || !catalog.TryGet(entry.LootId.Value, out var definition)) return false;
                EquipmentInstance instance = null;
                if (EquipmentInstance.IsEquipment(definition))
                {
                    if (entry.Amount != 1) return false;
                    instance = entry.Modifiers.HasValue ? new EquipmentInstance(null, entry.Modifiers) : EquipmentInstance.PlainCache(definition);
                }
                else if (entry.IsEquipmentUnit || entry.Modifiers.HasValue) return false;
                physical[index] = new StashItem(entry.LootId, entry.Amount, index, instance);
            }
            if (!ProfileInventoryRules.TryAggregate(physical, out entries)) return false;
        }

        if (!RaidAdmissionData.TryAppendPreparedEquipment(entries, PreparedEquipment, out int[] indices))
            return false;

        PreparedAbilityLoadout effectiveAbilities =
            (PreparedAbilities.Slot1.IsValid || PreparedAbilities.Slot2.IsValid)
                ? PreparedAbilities
                : fallbackAbilities;

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
            (PreparedEquipment.HasWeaponSetAMainHand || PreparedEquipment.WeaponSetAOffHand.IsValid)
                ? WeaponSetSlot.SetA
                : WeaponSetSlot.SetB,
            effectiveAbilities);
        return data.IsValid;
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
