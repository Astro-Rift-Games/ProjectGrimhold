using System.Text;

/// <summary>
/// Codec versionado responsable de serializar y deserializar los datos de conexión del jugador.
/// </summary>
public static class PlayerJoinDataCodec
{
    private const byte Version = 2;

    /// <summary>
    /// Determina si un PlayerClassId es soportado para jugar.
    /// </summary>
    public static bool IsSupported(PlayerClassId classId)
    {
        return classId == PlayerClassId.Melee || classId == PlayerClassId.Ranged;
    }

    /// <summary>
    /// Intenta codificar los datos de unión en un token de bytes.
    /// </summary>
    public static bool TryEncode(in PlayerJoinData data, out byte[] token)
    {
        if (!IsSupported(data.ClassId))
        {
            token = null;
            return false;
        }

        byte[] profileBytes = data.ProfileId.IsValid 
            ? Encoding.UTF8.GetBytes(data.ProfileId.Value) 
            : System.Array.Empty<byte>();

        if (profileBytes.Length > byte.MaxValue)
        {
            // Profile ID is too long to send as a simple byte length prefix
            token = null;
            return false;
        }

        token = new byte[3 + profileBytes.Length];
        token[0] = Version;
        token[1] = (byte)data.ClassId;
        token[2] = (byte)profileBytes.Length;
        
        if (profileBytes.Length > 0)
        {
            System.Buffer.BlockCopy(profileBytes, 0, token, 3, profileBytes.Length);
        }
        
        return true;
    }

    /// <summary>
    /// Intenta decodificar los datos de unión desde un token de bytes.
    /// </summary>
    public static bool TryDecode(byte[] token, out PlayerJoinData data)
    {
        data = new PlayerJoinData(PlayerClassId.None, new ProfileId(string.Empty));

        if (token == null || token.Length < 3)
        {
            return false;
        }

        if (token[0] != Version)
        {
            return false;
        }

        PlayerClassId classId = (PlayerClassId)token[1];
        if (!IsSupported(classId))
        {
            return false;
        }

        byte profileLength = token[2];
        if (token.Length < 3 + profileLength)
        {
            return false;
        }

        string profileIdValue = profileLength > 0 
            ? Encoding.UTF8.GetString(token, 3, profileLength) 
            : string.Empty;

        data = new PlayerJoinData(classId, new ProfileId(profileIdValue));
        return true;
    }
}
