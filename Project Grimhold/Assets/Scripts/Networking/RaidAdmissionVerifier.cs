using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Grimhold.Backend;

public sealed class BackendRaidAdmissionVerifier : IRaidAdmissionVerifier
{
    private readonly BackendConfiguration _config;
    private readonly string _bearerToken;
    private readonly Dictionary<string, Task<(bool valid, AuthoritativeRaidAdmissionTicket ticket, string error)>> _verificationCache =
        new Dictionary<string, Task<(bool, AuthoritativeRaidAdmissionTicket, string)>>(StringComparer.Ordinal);

    public BackendRaidAdmissionVerifier(BackendConfiguration config, string bearerToken)
    {
        _config = config;
        _bearerToken = bearerToken;
    }

    public Task<(bool valid, AuthoritativeRaidAdmissionTicket ticket, string error)> VerifyRaidAdmissionTicketAsync(
        string rawTicket,
        RaidCode expectedRaidCode)
    {
        if (string.IsNullOrWhiteSpace(rawTicket))
        {
            return Task.FromResult((false, default(AuthoritativeRaidAdmissionTicket), "EMPTY_TICKET"));
        }

        string cacheKey = $"{expectedRaidCode.Value}:{rawTicket}";
        if (_verificationCache.TryGetValue(cacheKey, out var existingTask))
        {
            if (!existingTask.IsCompleted)
            {
                return existingTask;
            }

            if (!existingTask.IsFaulted && !existingTask.IsCanceled)
            {
                var cachedResult = existingTask.Result;
                if (cachedResult.valid || !BackendErrorUtility.IsTransportFailure(cachedResult.error))
                {
                    return existingTask;
                }
            }

            _verificationCache.Remove(cacheKey);
        }

        var verificationTask = VerifyRaidAdmissionTicketInternalAsync(rawTicket, expectedRaidCode);
        _verificationCache[cacheKey] = verificationTask;
        return verificationTask;
    }

    private async Task<(bool valid, AuthoritativeRaidAdmissionTicket ticket, string error)> VerifyRaidAdmissionTicketInternalAsync(
        string rawTicket,
        RaidCode expectedRaidCode)
    {
        var (success, data, error) = await CharacterClient.VerifyAuthoritativeRaidAdmissionTicketAsync(
            _config,
            _bearerToken,
            rawTicket,
            expectedRaidCode.Value);

        if (!success || !data.valid)
        {
            return (false, default, data.error ?? error.error ?? "VERIFICATION_FAILED");
        }

        var snapshot = data.snapshot;
        CharacterAttributeState.TryCreate(
            snapshot.characterAttributes.vitality,
            snapshot.characterAttributes.resistance,
            snapshot.characterAttributes.strength,
            snapshot.characterAttributes.dexterity,
            snapshot.characterAttributes.intelligence,
            snapshot.characterAttributes.luck,
            snapshot.characterAttributes.availablePoints,
            out CharacterAttributeState attributes);

        AbilityId.TryCreate(snapshot.preparedAbilities.slot1, out AbilityId slot1Id);
        AbilityId.TryCreate(snapshot.preparedAbilities.slot2, out AbilityId slot2Id);
        var abilities = new PreparedAbilityLoadout(slot1Id, slot2Id);

        var items = new LootEntry[snapshot.items != null ? snapshot.items.Length : 0];
        if (snapshot.items != null)
        {
            for (int i = 0; i < snapshot.items.Length; i++)
            {
                items[i] = new LootEntry(new LootId(snapshot.items[i].lootId), snapshot.items[i].amount);
            }
        }

        LootId weapon1 = ParseOptionalLootId(snapshot.preparedEquipment.weaponSlot1);
        LootId weapon2 = ParseOptionalLootId(snapshot.preparedEquipment.weaponSlot2);
        LootId offHand1 = ParseOptionalLootId(snapshot.preparedEquipment.offHand1);
        LootId helmet = ParseOptionalLootId(snapshot.preparedEquipment.helmet);
        LootId armor = ParseOptionalLootId(snapshot.preparedEquipment.armor);
        LootId gloves = ParseOptionalLootId(snapshot.preparedEquipment.gloves);
        LootId boots = ParseOptionalLootId(snapshot.preparedEquipment.boots);
        LootId offHand2 = ParseOptionalLootId(snapshot.preparedEquipment.offHand2);
        var preparedEquipment = new PreparedEquipmentLoadout(
            weaponSetAMainHand: weapon1,
            weaponSetBMainHand: weapon2,
            helmet: helmet,
            armor: armor,
            gloves: gloves,
            boots: boots,
            weaponSetAOffHand: offHand1,
            weaponSetBOffHand: offHand2);

        RaidCode.TryParse(data.raidCode, out RaidCode parsedRaidCode);
        if (!parsedRaidCode.IsValid)
        {
            parsedRaidCode = expectedRaidCode;
        }

        var ticket = new AuthoritativeRaidAdmissionTicket(
            rawTicket,
            new ProfileId(data.characterId),
            parsedRaidCode,
            data.reservationId,
            snapshot.level,
            snapshot.experience,
            attributes,
            abilities,
            items,
            preparedEquipment,
            snapshot.lastAppliedProgressionResultSequence,
            data.expiresAt);

        return (true, ticket, null);
    }

    private static LootId ParseOptionalLootId(string str) =>
        string.IsNullOrWhiteSpace(str) ? default : new LootId(str);
}

public sealed class LocalDeterministicRaidAdmissionVerifier : IRaidAdmissionVerifier
{
    private readonly Dictionary<string, (bool valid, AuthoritativeRaidAdmissionTicket ticket, string error)> _registeredTickets =
        new Dictionary<string, (bool, AuthoritativeRaidAdmissionTicket, string)>(StringComparer.Ordinal);

    public void RegisterTicket(string rawTicket, in AuthoritativeRaidAdmissionTicket ticket, bool valid = true, string error = null)
    {
        _registeredTickets[rawTicket] = (valid, ticket, error);
    }

    public Task<(bool valid, AuthoritativeRaidAdmissionTicket ticket, string error)> VerifyRaidAdmissionTicketAsync(
        string rawTicket,
        RaidCode expectedRaidCode)
    {
        if (string.IsNullOrWhiteSpace(rawTicket))
        {
            return Task.FromResult((false, default(AuthoritativeRaidAdmissionTicket), "EMPTY_TICKET"));
        }

        if (_registeredTickets.TryGetValue(rawTicket, out var entry))
        {
            if (entry.valid && entry.ticket.RaidCode != expectedRaidCode)
            {
                return Task.FromResult((false, default(AuthoritativeRaidAdmissionTicket), "RAID_CODE_MISMATCH"));
            }

            return Task.FromResult((entry.valid, entry.ticket, entry.error));
        }

        return Task.FromResult((false, default(AuthoritativeRaidAdmissionTicket), "UNREGISTERED_TICKET"));
    }
}
