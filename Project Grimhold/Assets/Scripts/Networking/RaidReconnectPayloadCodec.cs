using System;
using System.IO;
using System.Text;

/// <summary>
/// Binary codec for RaidReconnectPayload. Used to transport identity during reconnection.
/// </summary>
public static class RaidReconnectPayloadCodec
{
    private const byte CanonicalVersion = 1;
    private static readonly Encoding Utf8 = new UTF8Encoding(false, true);

    public static bool TryEncode(RaidReconnectPayload payload, out byte[] token)
    {
        token = null;
        if (!payload.IsValid)
        {
            return false;
        }

        try
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream, Utf8, true);

            writer.Write(CanonicalVersion);
            if (!TryWriteText(writer, payload.RaidCode.Value) ||
                !TryWriteText(writer, payload.ProfileId.Value) ||
                !TryWriteText(writer, payload.RaidGenerationId))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(payload.ReservationId))
            {
                writer.Write((byte)1);
                if (!TryWriteText(writer, payload.ReservationId))
                {
                    return false;
                }
            }
            else
            {
                writer.Write((byte)0);
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

    public static bool TryDecode(byte[] token, out RaidReconnectPayload payload)
    {
        payload = default;
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
                !RaidCode.TryParse(codeValue, out RaidCode raidCode) ||
                !TryReadText(reader, out string profileId) ||
                !TryReadText(reader, out string generationId))
            {
                return false;
            }

            string reservationId = null;
            if (stream.Position < stream.Length)
            {
                byte hasRes = reader.ReadByte();
                if (hasRes == 1)
                {
                    TryReadText(reader, out reservationId);
                }
            }

            payload = new RaidReconnectPayload(raidCode, new ProfileId(profileId), generationId, reservationId);
            return payload.IsValid;
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
