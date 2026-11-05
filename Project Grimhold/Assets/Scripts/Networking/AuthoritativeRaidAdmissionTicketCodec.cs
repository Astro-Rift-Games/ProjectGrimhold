using System;
using System.Linq;
using System.Text;
using Grimhold.Backend;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>Decodes the ID-free admission shape. Authenticity remains the backend verifier's responsibility.</summary>
public static class AuthoritativeRaidAdmissionTicketCodec
{
    private sealed class TicketPayloadJson
    {
        public string characterId;
        public string characterName;
        public string raidCode;
        public string reservationId;
        public AuthoritativeRaidAdmissionSnapshotDto snapshot;
        public long issuedAt;
        public long expiresAt;
    }

    public static bool TryDecode(byte[] payloadBytes, out AuthoritativeRaidAdmissionTicket ticket)
    {
        ticket = default;
        return payloadBytes != null && payloadBytes.Length > 0 &&
            payloadBytes.Length <= RaidAdmissionPayloadChunking.MaxPayloadBytes &&
            TryDecode(Encoding.UTF8.GetString(payloadBytes), out ticket);
    }

    public static bool TryDecode(string rawTicket, out AuthoritativeRaidAdmissionTicket ticket)
    {
        ticket = default;
        if (string.IsNullOrWhiteSpace(rawTicket) || Encoding.UTF8.GetByteCount(rawTicket) > RaidAdmissionPayloadChunking.MaxPayloadBytes)
            return false;
        int dotIndex = rawTicket.IndexOf('.');
        if (dotIndex <= 0 || dotIndex >= rawTicket.Length - 1 || rawTicket.IndexOf('.', dotIndex + 1) >= 0) return false;
        try
        {
            string body = rawTicket.Substring(0, dotIndex).Replace('-', '+').Replace('_', '/');
            body = body.PadRight((body.Length + 3) / 4 * 4, '=');
            var json = JObject.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(body)));
            // Do not silently discard identity fields while retaining them in RawTicket.
            if (json["snapshot"] is not JObject snapshot || snapshot.Descendants().OfType<JProperty>()
                .Any(property => property.Name == "instanceId" || property.Name == "instance")) return false;
            var data = json.ToObject<TicketPayloadJson>();
            if (data == null || string.IsNullOrWhiteSpace(data.characterId) || string.IsNullOrWhiteSpace(data.reservationId) ||
                !RaidCode.TryParse(data.raidCode, out var code) ||
                !RaidAdmissionSnapshotMapper.TryProject(data.snapshot, out var attributes, out var abilities,
                    out var items, out var equipment)) return false;
            ticket = new AuthoritativeRaidAdmissionTicket(rawTicket, new ProfileId(data.characterId), code,
                data.reservationId, data.snapshot.level > 0 ? data.snapshot.level : 1, data.snapshot.experience,
                attributes, abilities, items, equipment, data.snapshot.lastAppliedProgressionResultSequence, data.expiresAt);
            return ticket.IsValid;
        }
        catch (FormatException) { return false; }
        catch (ArgumentException) { return false; }
        catch (JsonException) { return false; }
    }

    public static string CreateTestTicket(ProfileId profileId, RaidCode raidCode, string reservationId,
        int level, long experience, in CharacterAttributeState attributes, in PreparedAbilityLoadout abilities,
        LootEntry[] items, in PreparedEquipmentLoadout equipment, int sequence = 0, long ttlSeconds = 600)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var prepared = new PreparedEquipmentDto();
        foreach (var slot in EquipmentSlotRules.AllSlots)
        {
            prepared.Definitions.Set(slot, equipment.Get(slot).Value);
            var instance = equipment.GetInstance(slot);
            if (instance == null) continue;
            prepared.Units ??= new System.Collections.Generic.Dictionary<EquipmentSlot, WeaponInstanceModifiersData>();
            prepared.Units[slot] = instance.Modifiers.HasValue ? WeaponInstanceModifiersData.FromModifiers(instance.Modifiers.Value) : null;
        }
        var itemData = new LootEntryDto[items?.Length ?? 0];
        for (int index = 0; index < itemData.Length; index++)
            itemData[index] = new LootEntryDto { lootId = items[index].LootId.Value, amount = items[index].Amount,
                modifiers = items[index].Modifiers.HasValue ? WeaponInstanceModifiersData.FromModifiers(items[index].Modifiers.Value) : null };
        var payload = new TicketPayloadJson { characterId = profileId.Value, characterName = profileId.Value,
            raidCode = raidCode.Value, reservationId = reservationId, issuedAt = now, expiresAt = now + ttlSeconds,
            snapshot = new AuthoritativeRaidAdmissionSnapshotDto { level = level, experience = experience,
                characterAttributes = new CharacterAttributeStateDto { vitality = (short)attributes.Vitality,
                    resistance = (short)attributes.Resistance, strength = (short)attributes.Strength, dexterity = (short)attributes.Dexterity,
                    intelligence = (short)attributes.Intelligence, luck = (short)attributes.Luck, availablePoints = (short)attributes.AvailablePoints },
                preparedAbilities = new PreparedAbilitiesDto { slot1 = abilities.Slot1.IsValid ? abilities.Slot1.Value : null,
                    slot2 = abilities.Slot2.IsValid ? abilities.Slot2.Value : null }, items = itemData,
                preparedEquipment = prepared, lastAppliedProgressionResultSequence = sequence } };
        string json = JsonConvert.SerializeObject(payload, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
        string body = Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return body + "." + new string('0', 64);
    }
}
