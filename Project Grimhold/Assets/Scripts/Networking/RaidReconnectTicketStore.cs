using System;
using UnityEngine;

/// <summary>
/// Provides persistent client storage for an active <see cref="RaidReconnectTicket"/> using <see cref="PlayerPrefs"/>.
/// Preserves the reconnection opportunity across application restarts, crashes, and Play Mode reloads.
/// </summary>
public static class RaidReconnectTicketStore
{
    private const string PrefsPrefix = "Grimhold.RaidReconnect.";
    private const string KeyRaidCode = PrefsPrefix + "RaidCode";
    private const string KeyProfileId = PrefsPrefix + "ProfileId";
    private const string KeyGenerationId = PrefsPrefix + "GenerationId";
    private const string KeySessionName = PrefsPrefix + "SessionName";
    private const string KeyReservationId = PrefsPrefix + "ReservationId";
    private const string KeyCreatedAtUtcTicks = PrefsPrefix + "CreatedAtUtcTicks";

    private static readonly TimeSpan MaxTicketLifetime = TimeSpan.FromHours(1);

    public static void Save(in RaidReconnectTicket ticket)
    {
        if (!ticket.IsValid)
        {
            return;
        }

        PlayerPrefs.SetString(KeyRaidCode, ticket.RaidCode.Value);
        PlayerPrefs.SetString(KeyProfileId, ticket.ProfileId.Value);
        PlayerPrefs.SetString(KeyGenerationId, ticket.RaidGenerationId);
        PlayerPrefs.SetString(KeySessionName, ticket.SessionName);
        PlayerPrefs.SetString(KeyReservationId, ticket.ReservationId ?? string.Empty);
        PlayerPrefs.SetString(KeyCreatedAtUtcTicks, ticket.CreatedAtUtc.Ticks.ToString());
        PlayerPrefs.Save();
    }

    public static bool TryLoad(ProfileId expectedProfile, out RaidReconnectTicket ticket)
    {
        ticket = default;
        if (!PlayerPrefs.HasKey(KeyRaidCode) || !PlayerPrefs.HasKey(KeyProfileId))
        {
            return false;
        }

        string rawRaidCode = PlayerPrefs.GetString(KeyRaidCode, string.Empty);
        string rawProfileId = PlayerPrefs.GetString(KeyProfileId, string.Empty);
        string generationId = PlayerPrefs.GetString(KeyGenerationId, string.Empty);
        string sessionName = PlayerPrefs.GetString(KeySessionName, string.Empty);
        string reservationId = PlayerPrefs.GetString(KeyReservationId, string.Empty);
        string rawTicks = PlayerPrefs.GetString(KeyCreatedAtUtcTicks, string.Empty);

        if (!RaidCode.TryParse(rawRaidCode, out RaidCode raidCode) ||
            string.IsNullOrEmpty(rawProfileId) ||
            string.IsNullOrEmpty(generationId) ||
            string.IsNullOrEmpty(sessionName))
        {
            Clear();
            return false;
        }

        var profileId = new ProfileId(rawProfileId);
        if (expectedProfile.IsValid && profileId != expectedProfile)
        {
            return false;
        }

        DateTime createdAt = DateTime.UtcNow;
        if (long.TryParse(rawTicks, out long ticks))
        {
            createdAt = new DateTime(ticks, DateTimeKind.Utc);
        }

        if (DateTime.UtcNow - createdAt > MaxTicketLifetime)
        {
            Clear();
            return false;
        }

        ticket = new RaidReconnectTicket(raidCode, profileId, generationId, sessionName, reservationId, createdAt);
        return ticket.IsValid;
    }

    public static void Clear()
    {
        PlayerPrefs.DeleteKey(KeyRaidCode);
        PlayerPrefs.DeleteKey(KeyProfileId);
        PlayerPrefs.DeleteKey(KeyGenerationId);
        PlayerPrefs.DeleteKey(KeySessionName);
        PlayerPrefs.DeleteKey(KeyReservationId);
        PlayerPrefs.DeleteKey(KeyCreatedAtUtcTicks);
        PlayerPrefs.Save();
    }
}
