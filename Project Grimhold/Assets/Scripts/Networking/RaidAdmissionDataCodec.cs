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
    private const byte CanonicalVersion = 6;
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

            if (!TryWriteText(writer, data.ProfileId.Value) ||
                !TryWriteText(writer, data.ReservationId))
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
            }

            IReadOnlyList<int> indices = data.EntryIndicesPlusOne;
            for (int index = 0; index < indices.Count; index++)
            {
                writer.Write((byte)indices[index]);
            }

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

            if (!TryReadText(reader, out string profileId) ||
                !TryReadText(reader, out string reservationId))
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

            if (stream.Position != stream.Length)
            {
                return false;
            }

            data = new RaidAdmissionData(
                raidCode,
                new ProfileId(profileId),
                reservationId,
                entries,
                indices);
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
