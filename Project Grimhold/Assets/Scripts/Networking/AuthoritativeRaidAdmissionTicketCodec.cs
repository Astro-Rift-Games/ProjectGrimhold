using System;
using System.Text;
using UnityEngine;

/// <summary>
/// Codec to decode and serialize AuthoritativeRaidAdmissionTicket tokens in Unity.
/// </summary>
public static class AuthoritativeRaidAdmissionTicketCodec
{
    [Serializable]
    private struct TicketPayloadJson
    {
        public string characterId;
        public string characterName;
        public string raidCode;
        public string reservationId;
        public SnapshotJson snapshot;
        public long issuedAt;
        public long expiresAt;
    }

    [Serializable]
    private struct SnapshotJson
    {
        public int level;
        public long experience;
        public AttributesJson characterAttributes;
        public AbilitiesJson preparedAbilities;
        public ItemJson[] items;
        public EquipmentJson preparedEquipment;
        public int lastAppliedProgressionResultSequence;
    }

    [Serializable]
    private struct AttributesJson
    {
        public short vitality;
        public short resistance;
        public short strength;
        public short dexterity;
        public short intelligence;
        public short luck;
        public short availablePoints;
    }

    [Serializable]
    private struct AbilitiesJson
    {
        public string slot1;
        public string slot2;
    }

    [Serializable]
    private struct ItemJson
    {
        public string lootId;
        public int amount;
    }

    [Serializable]
    private struct EquipmentJson
    {
        public string weaponSlot1;
        public string weaponSlot2;
        public string offHand1;
        public string helmet;
        public string armor;
        public string gloves;
        public string boots;
        public string offHand2;
    }

    public static bool TryDecode(byte[] payloadBytes, out AuthoritativeRaidAdmissionTicket ticket)
    {
        ticket = default;
        if (payloadBytes == null || payloadBytes.Length == 0)
        {
            return false;
        }

        try
        {
            string rawTicket = Encoding.UTF8.GetString(payloadBytes);
            return TryDecode(rawTicket, out ticket);
        }
        catch
        {
            return false;
        }
    }

    public static bool TryDecode(string rawTicket, out AuthoritativeRaidAdmissionTicket ticket)
    {
        ticket = default;
        if (string.IsNullOrWhiteSpace(rawTicket))
        {
            return false;
        }

        int dotIndex = rawTicket.IndexOf('.');
        if (dotIndex <= 0 || dotIndex >= rawTicket.Length - 1)
        {
            return false;
        }

        string payloadB64 = rawTicket.Substring(0, dotIndex);
        try
        {
            string json = Base64UrlDecode(payloadB64);
            var data = JsonUtility.FromJson<TicketPayloadJson>(json);
            if (string.IsNullOrWhiteSpace(data.characterId) ||
                string.IsNullOrWhiteSpace(data.raidCode) ||
                string.IsNullOrWhiteSpace(data.reservationId))
            {
                return false;
            }

            if (!RaidCode.TryParse(data.raidCode, out RaidCode parsedRaidCode))
            {
                return false;
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

            ticket = new AuthoritativeRaidAdmissionTicket(
                rawTicket,
                new ProfileId(data.characterId),
                parsedRaidCode,
                data.reservationId,
                snapshot.level > 0 ? snapshot.level : 1,
                snapshot.experience,
                attributes,
                abilities,
                items,
                preparedEquipment,
                snapshot.lastAppliedProgressionResultSequence,
                data.expiresAt);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static string CreateTestTicket(
        ProfileId profileId,
        RaidCode raidCode,
        string reservationId,
        int level,
        long experience,
        in CharacterAttributeState attributes,
        in PreparedAbilityLoadout abilities,
        LootEntry[] items,
        in PreparedEquipmentLoadout equipment,
        int sequence = 0,
        long ttlSeconds = 600)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var itemJsons = new ItemJson[items != null ? items.Length : 0];
        if (items != null)
        {
            for (int i = 0; i < items.Length; i++)
            {
                itemJsons[i] = new ItemJson { lootId = items[i].LootId.Value, amount = items[i].Amount };
            }
        }

        var payload = new TicketPayloadJson
        {
            characterId = profileId.Value,
            characterName = profileId.Value,
            raidCode = raidCode.Value,
            reservationId = reservationId,
            issuedAt = now,
            expiresAt = now + ttlSeconds,
            snapshot = new SnapshotJson
            {
                level = level,
                experience = experience,
                lastAppliedProgressionResultSequence = sequence,
                characterAttributes = new AttributesJson
                {
                    vitality = (short)attributes.Vitality,
                    resistance = (short)attributes.Resistance,
                    strength = (short)attributes.Strength,
                    dexterity = (short)attributes.Dexterity,
                    intelligence = (short)attributes.Intelligence,
                    luck = (short)attributes.Luck,
                    availablePoints = (short)attributes.AvailablePoints
                },
                preparedAbilities = new AbilitiesJson
                {
                    slot1 = abilities.Slot1.IsValid ? abilities.Slot1.Value : null,
                    slot2 = abilities.Slot2.IsValid ? abilities.Slot2.Value : null
                },
                items = itemJsons,
                preparedEquipment = new EquipmentJson
                {
                    weaponSlot1 = equipment.WeaponSetAMainHand.IsValid ? equipment.WeaponSetAMainHand.Value : null,
                    weaponSlot2 = equipment.WeaponSetBMainHand.IsValid ? equipment.WeaponSetBMainHand.Value : null,
                    offHand1 = equipment.WeaponSetAOffHand.IsValid ? equipment.WeaponSetAOffHand.Value : null,
                    helmet = equipment.Helmet.IsValid ? equipment.Helmet.Value : null,
                    armor = equipment.Armor.IsValid ? equipment.Armor.Value : null,
                    gloves = equipment.Gloves.IsValid ? equipment.Gloves.Value : null,
                    boots = equipment.Boots.IsValid ? equipment.Boots.Value : null,
                    offHand2 = equipment.WeaponSetBOffHand.IsValid ? equipment.WeaponSetBOffHand.Value : null
                }
            }
        };

        string json = JsonUtility.ToJson(payload);
        string b64 = Base64UrlEncode(Encoding.UTF8.GetBytes(json));
        string dummySig = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        return $"{b64}.{dummySig}";
    }

    private static string Base64UrlDecode(string input)
    {
        string output = input.Replace('-', '+').Replace('_', '/');
        switch (output.Length % 4)
        {
            case 2: output += "=="; break;
            case 3: output += "="; break;
        }
        byte[] bytes = Convert.FromBase64String(output);
        return Encoding.UTF8.GetString(bytes);
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static LootId ParseOptionalLootId(string str) =>
        string.IsNullOrWhiteSpace(str) ? default : new LootId(str);
}
