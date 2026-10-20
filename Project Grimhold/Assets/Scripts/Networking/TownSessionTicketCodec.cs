using System;
using UnityEngine;

/// <summary>
/// Codec to decode and validate TownSessionTicket payloads in Unity.
/// </summary>
public static class TownSessionTicketCodec
{
    [Serializable]
    private struct TicketPayloadJson
    {
        public string characterId;
        public string characterName;
        public string sessionType;
        public string sessionName;
        public long issuedAt;
        public long expiresAt;
    }

    public static bool TryDecode(string rawTicket, out TownSessionTicket ticket)
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
            if (data.characterId == null || string.IsNullOrWhiteSpace(data.characterId))
            {
                return false;
            }

            ticket = new TownSessionTicket(
                rawTicket,
                new ProfileId(data.characterId),
                data.characterName,
                data.sessionName,
                data.expiresAt);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static string CreateTestTicket(ProfileId profileId, string characterName, string sessionName = "Lobby-Town", long ttlSeconds = 1800)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var payload = new TicketPayloadJson
        {
            characterId = profileId.Value,
            characterName = characterName ?? profileId.Value,
            sessionType = "Town",
            sessionName = sessionName,
            issuedAt = now,
            expiresAt = now + ttlSeconds
        };

        string json = JsonUtility.ToJson(payload);
        string b64 = Base64UrlEncode(System.Text.Encoding.UTF8.GetBytes(json));
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
        return System.Text.Encoding.UTF8.GetString(bytes);
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
