using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

/// <summary>
/// Versioned, bounded binary codec for the private raid admission token.
/// The payload contains only the local player's reserved loadout.
/// </summary>
public static class RaidAdmissionDataCodec
{
    private const byte CanonicalVersion = 11;
    private const byte InstanceVersion = 12;
    private static readonly Encoding Utf8 = new UTF8Encoding(false, true);

    public static bool TryEncode(in RaidAdmissionData data, out byte[] token)
    {
        token = null;
        if (!data.IsValid)
        {
            return false;
        }

        try
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream, Utf8, true);
            bool instanceFormat = false;
            foreach (LootEntry entry in data.ReservedLoadout) instanceFormat |= entry.IsEquipmentUnit;
            writer.Write(instanceFormat ? InstanceVersion : CanonicalVersion);
            
            if (!TryWriteText(writer, data.RaidCode.Value))
            {
                return false;
            }

            if (!TryWriteText(writer, data.ProfileId.Value))
            {
                return false;
            }

            // Write ReservationId as raw GUID bytes (16 bytes) instead of 32-char string
            if (!Guid.TryParseExact(data.ReservationId, "N", out Guid reservationGuid))
            {
                return false;
            }
            writer.Write(reservationGuid.ToByteArray());

            writer.Write(data.Level);
            writer.Write(data.CurrentExperience);
            writer.Write(data.LastAppliedProgressionResultSequence);
            // Write CharacterAttributes as shorts (max value is well under 32767)
            writer.Write((short)data.CharacterAttributes.Vitality);
            writer.Write((short)data.CharacterAttributes.Resistance);
            writer.Write((short)data.CharacterAttributes.Strength);
            writer.Write((short)data.CharacterAttributes.Dexterity);
            writer.Write((short)data.CharacterAttributes.Intelligence);
            writer.Write((short)data.CharacterAttributes.Luck);
            writer.Write((short)data.CharacterAttributes.AvailablePoints);

            if (!TryWriteOptionalAbilityId(writer, data.PreparedAbilities.Slot1) ||
                !TryWriteOptionalAbilityId(writer, data.PreparedAbilities.Slot2))
            {
                return false;
            }

            writer.Write((byte)data.ReservedLoadout.Count);
            for (int index = 0; index < data.ReservedLoadout.Count; index++)
            {
                LootEntry entry = data.ReservedLoadout[index];
                if (!TryWriteText(writer, entry.LootId.Value))
                {
                    return false;
                }

                writer.Write(entry.Amount);
                if (instanceFormat)
                {
                    writer.Write(entry.IsEquipmentUnit);
                    writer.Write(entry.Modifiers.HasValue);
                    if (entry.Modifiers.HasValue)
                    {
                        var dto = Grimhold.Backend.WeaponInstanceModifiersData.FromModifiers(entry.Modifiers.Value);
                        if (!dto.TryToTransportModifiers(out _, out _)) return false;
                        writer.Write(Newtonsoft.Json.JsonConvert.SerializeObject(dto));
                    }
                }
            }

            IReadOnlyList<int> indices = data.EntryIndicesPlusOne;
            for (int index = 0; index < indices.Count; index++)
            {
                writer.Write((byte)indices[index]);
            }
            writer.Write((byte)data.ActiveWeaponSet);

            writer.Flush();
            if (stream.Length > RaidLoadoutRules.MaximumTokenBytes)
            {
                return false;
            }

            token = stream.ToArray();
            return true;
        }
        catch (EncoderFallbackException)
        {
            return false;
        }
    }

    public static bool TryDecode(byte[] token, out RaidAdmissionData data)
    {
        data = default;
        if (token == null || token.Length == 0 || token.Length > RaidLoadoutRules.MaximumTokenBytes)
        {
            return false;
        }

        try
        {
            using var stream = new MemoryStream(token, false);
            using var reader = new BinaryReader(stream, Utf8, true);
            byte version = reader.ReadByte();
            if (version != CanonicalVersion && version != InstanceVersion)
            {
                return false;
            }

            if (!TryReadText(reader, out string codeValue) ||
                !RaidCode.TryParse(codeValue, out RaidCode raidCode))
            {
                return false;
            }

            if (!TryReadText(reader, out string profileId))
            {
                return false;
            }

            // Read ReservationId from raw GUID bytes
            byte[] guidBytes = reader.ReadBytes(16);
            if (guidBytes.Length != 16)
            {
                return false;
            }
            string reservationId = new Guid(guidBytes).ToString("N");

            int level = reader.ReadInt32();
            long currentExperience = reader.ReadInt64();
            int lastAppliedProgressionResultSequence = reader.ReadInt32();
            // Read CharacterAttributes as shorts
            if (!CharacterAttributeState.TryCreate(
                    reader.ReadInt16(),
                    reader.ReadInt16(),
                    reader.ReadInt16(),
                    reader.ReadInt16(),
                    reader.ReadInt16(),
                    reader.ReadInt16(),
                    reader.ReadInt16(),
                    out CharacterAttributeState characterAttributes))
            {
                return false;
            }

            if (!TryReadOptionalAbilityId(reader, out AbilityId abilitySlot1) ||
                !TryReadOptionalAbilityId(reader, out AbilityId abilitySlot2))
            {
                return false;
            }
            var preparedAbilities = new PreparedAbilityLoadout(abilitySlot1, abilitySlot2);
            if (!PreparedAbilityLoadout.TryValidateTransportShape(preparedAbilities, out _))
            {
                return false;
            }

            int entryCount = reader.ReadByte();
            if (entryCount > RaidLoadoutRules.MaximumEntries)
            {
                return false;
            }

            var entries = new LootEntry[entryCount];
            for (int index = 0; index < entryCount; index++)
            {
                if (!TryReadText(reader, out string lootIdValue))
                {
                    return false;
                }

                int amount = reader.ReadInt32();
                bool isUnit = false;
                WeaponInstanceModifiers? modifiers = null;
                if (version == InstanceVersion)
                {
                    int unitFlag = reader.ReadByte();
                    int modifierFlag = reader.ReadByte();
                    if (unitFlag > 1 || modifierFlag > 1 || modifierFlag == 1 && unitFlag != 1) return false;
                    isUnit = unitFlag == 1;
                    if (modifierFlag == 1)
                    {
                        var dto = Newtonsoft.Json.JsonConvert.DeserializeObject<Grimhold.Backend.WeaponInstanceModifiersData>(reader.ReadString());
                        if (dto == null || !dto.TryToTransportModifiers(out var parsed, out _)) return false;
                        modifiers = parsed;
                    }
                }
                entries[index] = new LootEntry(new LootId(lootIdValue), amount, modifiers, isUnit);
            }

            var indices = new int[EquipmentSlotRules.AllSlots.Length];
            for (int index = 0; index < indices.Length; index++)
            {
                indices[index] = reader.ReadByte();
            }
            WeaponSetSlot activeWeaponSet = (WeaponSetSlot)reader.ReadByte();

            if (stream.Position != stream.Length)
            {
                return false;
            }

            data = new RaidAdmissionData(
                raidCode,
                new ProfileId(profileId),
                reservationId,
                entries,
                characterAttributes,
                indices,
                level,
                currentExperience,
                lastAppliedProgressionResultSequence,
                activeWeaponSet,
                preparedAbilities);
            return data.IsValid;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (Newtonsoft.Json.JsonException)
        {
            return false;
        }
    }

    private static bool TryWriteText(BinaryWriter writer, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        byte[] bytes = Utf8.GetBytes(value);
        if (bytes.Length > RaidLoadoutRules.MaximumTextBytes)
        {
            return false;
        }

        writer.Write((byte)bytes.Length);
        writer.Write(bytes);
        return true;
    }

    private static bool TryWriteOptionalAbilityId(BinaryWriter writer, AbilityId abilityId)
    {
        writer.Write(abilityId.IsValid);
        return !abilityId.IsValid || TryWriteText(writer, abilityId.Value);
    }

    private static bool TryReadOptionalAbilityId(BinaryReader reader, out AbilityId abilityId)
    {
        abilityId = default;
        int presence = reader.ReadByte();
        if (presence == 0)
        {
            return true;
        }

        return presence == 1 &&
               TryReadText(reader, out string value) &&
               AbilityId.TryCreate(value, out abilityId);
    }

    private static bool TryReadText(BinaryReader reader, out string value)
    {
        value = null;
        int length = reader.ReadByte();
        if (length <= 0 || length > RaidLoadoutRules.MaximumTextBytes)
        {
            return false;
        }

        byte[] bytes = reader.ReadBytes(length);
        if (bytes.Length != length)
        {
            return false;
        }

        value = Utf8.GetString(bytes);
        return !string.IsNullOrWhiteSpace(value);
    }
}
