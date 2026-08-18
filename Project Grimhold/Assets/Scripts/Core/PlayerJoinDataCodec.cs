using System;
using System.Text;

/// <summary>
/// Codec versionado responsable de serializar y deserializar los datos de conexión del jugador.
/// </summary>
public static class PlayerJoinDataCodec
{
    private const byte Version = 3;
    private const int MinProfileBytes = 1;
    private const int MaxProfileBytes = 64;

    private static readonly Encoding Utf8 = new UTF8Encoding(false, true);

    /// <summary>
    /// Intenta codificar los datos de unión en un token de bytes.
    /// </summary>
    public static bool TryEncode(in PlayerJoinData data, out byte[] token)
    {
        token = null;

        if (!data.ProfileId.IsValid)
        {
            return false;
        }

        byte[] profileBytes;
        try
        {
            profileBytes = Utf8.GetBytes(data.ProfileId.Value);
        }
        catch (EncoderFallbackException)
        {
            return false;
        }

        if (profileBytes.Length < MinProfileBytes || profileBytes.Length > MaxProfileBytes)
        {
            return false;
        }

        token = new byte[2 + profileBytes.Length];
        token[0] = Version;
        token[1] = (byte)profileBytes.Length;
        Buffer.BlockCopy(profileBytes, 0, token, 2, profileBytes.Length);
        
        return true;
    }

    /// <summary>
    /// Intenta decodificar los datos de unión desde un token de bytes.
    /// </summary>
    public static bool TryDecode(byte[] token, out PlayerJoinData data)
    {
        data = new PlayerJoinData(default);

        if (token == null || token.Length < 3) // Minimum length: Version(1) + Length(1) + Profile(1)
        {
            return false;
        }

        if (token[0] != Version)
        {
            return false;
        }

        byte profileLength = token[1];
        if (profileLength < MinProfileBytes || profileLength > MaxProfileBytes)
        {
            return false;
        }

        if (token.Length != 2 + profileLength)
        {
            return false;
        }

        string profileIdValue;
        try
        {
            profileIdValue = Utf8.GetString(token, 2, profileLength);
        }
        catch (DecoderFallbackException)
        {
            return false;
        }

        ProfileId profileId = new ProfileId(profileIdValue);
        if (!profileId.IsValid)
        {
            return false;
        }

        data = new PlayerJoinData(profileId);
        return true;
    }
}
