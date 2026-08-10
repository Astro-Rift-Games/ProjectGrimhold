using System;
using System.Text;

/// <summary>
/// Versioned binary codec for the private raid admission token.
/// </summary>
public static class RaidAdmissionDataCodec
{
    private const byte Version = 1;

    public static bool TryEncode(in RaidAdmissionData data, out byte[] token)
    {
        token = null;
        if (!data.IsValid)
        {
            return false;
        }

        byte[] raidBytes = Encoding.UTF8.GetBytes(data.RaidId);
        byte[] secretBytes = Encoding.UTF8.GetBytes(data.AccessSecret);
        byte[] profileBytes = Encoding.UTF8.GetBytes(data.ProfileId.Value);
        if (raidBytes.Length > byte.MaxValue || secretBytes.Length > byte.MaxValue || profileBytes.Length > byte.MaxValue)
        {
            return false;
        }

        token = new byte[5 + raidBytes.Length + secretBytes.Length + profileBytes.Length];
        int offset = 0;
        token[offset++] = Version;
        token[offset++] = (byte)data.SelectedBuild;
        token[offset++] = (byte)raidBytes.Length;
        Buffer.BlockCopy(raidBytes, 0, token, offset, raidBytes.Length);
        offset += raidBytes.Length;
        token[offset++] = (byte)secretBytes.Length;
        Buffer.BlockCopy(secretBytes, 0, token, offset, secretBytes.Length);
        offset += secretBytes.Length;
        token[offset++] = (byte)profileBytes.Length;
        Buffer.BlockCopy(profileBytes, 0, token, offset, profileBytes.Length);
        return true;
    }

    public static bool TryDecode(byte[] token, out RaidAdmissionData data)
    {
        data = default;
        if (token == null || token.Length < 5 || token[0] != Version)
        {
            return false;
        }

        PlayerClassId build = (PlayerClassId)token[1];
        if (!PlayerJoinDataCodec.IsSupported(build))
        {
            return false;
        }

        int offset = 2;
        if (!TryReadString(token, ref offset, out string raidId) ||
            !TryReadString(token, ref offset, out string secret) ||
            !TryReadString(token, ref offset, out string profileId) ||
            offset != token.Length)
        {
            return false;
        }

        data = new RaidAdmissionData(raidId, secret, new ProfileId(profileId), build);
        return data.IsValid;
    }

    private static bool TryReadString(byte[] token, ref int offset, out string value)
    {
        value = null;
        if (offset >= token.Length)
        {
            return false;
        }

        int length = token[offset++];
        if (offset + length > token.Length)
        {
            return false;
        }

        value = Encoding.UTF8.GetString(token, offset, length);
        offset += length;
        return true;
    }
}
