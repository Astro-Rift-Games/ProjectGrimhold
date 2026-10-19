using System;
using System.IO;
using System.Text;

/// <summary>
/// Versioned, bounded binary codec for the initial connection handshake token.
/// Serializes only identity references to guarantee &lt;= 128 bytes (Fusion limit).
/// </summary>
public static class RaidAdmissionTicketCodec
{
    public const byte CanonicalVersion = 21;
    public const int MaximumTokenBytes = 128;
    private const int MaximumTextBytes = 64;
    private static readonly Encoding Utf8 = new UTF8Encoding(false, true);

    public static bool TryEncode(in RaidAdmissionTicket ticket, out byte[] token)
    {
        token = null;
        if (!ticket.IsValid)
        {
            return false;
        }

        try
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream, Utf8, true);
            writer.Write(CanonicalVersion);

            if (!TryWriteText(writer, ticket.RaidCode.Value))
            {
                return false;
            }

            if (!TryWriteText(writer, ticket.ProfileId.Value))
            {
                return false;
            }

            if (!Guid.TryParseExact(ticket.ReservationId, "N", out Guid reservationGuid))
            {
                writer.Write((byte)1); // text marker
                if (!TryWriteText(writer, ticket.ReservationId))
                {
                    return false;
                }
            }
            else
            {
                writer.Write((byte)0); // raw GUID marker
                writer.Write(reservationGuid.ToByteArray());
            }

            writer.Flush();
            if (stream.Length > MaximumTokenBytes)
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

    public static bool TryDecode(byte[] token, out RaidAdmissionTicket ticket)
    {
        ticket = default;
        if (token == null || token.Length == 0 || token.Length > MaximumTokenBytes)
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

            byte reservationKind = reader.ReadByte();
            string reservationId;
            if (reservationKind == 0)
            {
                byte[] guidBytes = reader.ReadBytes(16);
                if (guidBytes.Length != 16)
                {
                    return false;
                }
                reservationId = new Guid(guidBytes).ToString("N");
            }
            else if (reservationKind == 1)
            {
                if (!TryReadText(reader, out reservationId))
                {
                    return false;
                }
            }
            else
            {
                return false;
            }

            if (stream.Position != stream.Length)
            {
                return false;
            }

            ticket = new RaidAdmissionTicket(
                raidCode,
                new ProfileId(profileId),
                reservationId);
            return ticket.IsValid;
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
        if (bytes.Length > MaximumTextBytes)
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
        if (length <= 0 || length > MaximumTextBytes)
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
