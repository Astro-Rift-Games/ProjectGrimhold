using System;

/// <summary>
/// Authenticated session ticket issued by the authoritative backend for Town.
/// Binds a player peer in Town to a verified backend CharacterId without exposing backend secrets in client builds.
/// </summary>
public readonly struct TownSessionTicket : IEquatable<TownSessionTicket>
{
    public string RawTicket { get; }
    public ProfileId ProfileId { get; }
    public string CharacterName { get; }
    public string SessionName { get; }
    public long ExpiresAt { get; }

    public bool IsValid =>
        !string.IsNullOrWhiteSpace(RawTicket) &&
        ProfileId.IsValid &&
        !string.IsNullOrWhiteSpace(CharacterName);

    public bool IsExpired(long currentUnixSeconds) =>
        ExpiresAt > 0 && currentUnixSeconds > ExpiresAt;

    public TownSessionTicket(
        string rawTicket,
        ProfileId profileId,
        string characterName,
        string sessionName,
        long expiresAt)
    {
        RawTicket = rawTicket ?? string.Empty;
        ProfileId = profileId;
        CharacterName = characterName ?? string.Empty;
        SessionName = sessionName ?? string.Empty;
        ExpiresAt = expiresAt;
    }

    public bool Equals(TownSessionTicket other) =>
        string.Equals(RawTicket, other.RawTicket, StringComparison.Ordinal) &&
        ProfileId.Equals(other.ProfileId) &&
        string.Equals(SessionName, other.SessionName, StringComparison.Ordinal);

    public override bool Equals(object obj) =>
        obj is TownSessionTicket other && Equals(other);

    public override int GetHashCode() =>
        HashCode.Combine(RawTicket, ProfileId);

    public static bool operator ==(TownSessionTicket left, TownSessionTicket right) => left.Equals(right);
    public static bool operator !=(TownSessionTicket left, TownSessionTicket right) => !left.Equals(right);

    public override string ToString() => $"[TownSessionTicket Profile={ProfileId.Value} Name={CharacterName} Session={SessionName}]";
}
