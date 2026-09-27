using System;

/// <summary>
/// Pure rules for deriving a deterministic, clearly-marked offline development
/// <see cref="ProfileId"/> for <see cref="DirectRaidDevelopmentStarter"/>. Holds no state and
/// never touches authentication, the backend, or Unity APIs.
/// </summary>
public static class DevelopmentProfileIdentity
{
    public const string Prefix = "dev-local-";

    private const int MaxKeyLength = 32;

    /// <summary>
    /// Accepts a non-empty key, trimmed to 1-32 characters, containing only lowercase ASCII
    /// letters, digits, and hyphens. Produces the deterministic <c>"dev-local-" + key</c> identity.
    /// </summary>
    public static bool TryCreate(string key, out ProfileId profileId)
    {
        profileId = default;
        if (string.IsNullOrEmpty(key))
        {
            return false;
        }

        string trimmed = key.Trim();
        if (trimmed.Length < 1 || trimmed.Length > MaxKeyLength)
        {
            return false;
        }

        for (int index = 0; index < trimmed.Length; index++)
        {
            char character = trimmed[index];
            bool isLowercaseLetter = character >= 'a' && character <= 'z';
            bool isDigit = character >= '0' && character <= '9';
            bool isHyphen = character == '-';
            if (!isLowercaseLetter && !isDigit && !isHyphen)
            {
                return false;
            }
        }

        profileId = new ProfileId(Prefix + trimmed);
        return true;
    }

    /// <summary>True when the given identity is a valid offline development profile.</summary>
    public static bool IsDevelopmentProfile(ProfileId profileId) =>
        profileId.IsValid && profileId.Value.StartsWith(Prefix, StringComparison.Ordinal);

    /// <summary>
    /// True only in Editor or Development builds. Release builds must never bootstrap an
    /// offline development identity.
    /// </summary>
    public static bool IsAvailableInThisBuild
    {
        get
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return true;
#else
            return false;
#endif
        }
    }
}
