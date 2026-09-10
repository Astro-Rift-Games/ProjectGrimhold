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
    private const byte CanonicalVersion = 10;  // bumped: GUID binary + short attributes
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
            writer.Write(CanonicalVersion);
            
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

            writer.Write((byte)data.ReservedLoadout.Count);
            for (int index = 0; index < data.ReservedLoadout.Count; index++)
            {
                LootEntry entry = data.ReservedLoadout[index];
                if (!TryWriteText(writer, entry.LootId.Value))
                {
                    return false;
                }

                writer.Write(entry.Amount);
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
            if (version != CanonicalVersion)
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
                entries[index] = new LootEntry(new LootId(lootIdValue), amount);
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
                activeWeaponSet);
            return data.IsValid;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (EndOfStreamException)
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
