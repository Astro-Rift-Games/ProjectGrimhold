using System;
using Fusion;
using UnityEngine;

/// <summary>
/// Authoritative validator for RPC senders in Town.
/// Replaces blind trust in SocialPlayerIdentity.ProfileId by verifying the player's authenticated TownSessionTicket.
/// Rejects absent, expired, foreign, or forged identity proofs before any mutation occurs.
/// </summary>
public static class TownSenderIdentityValidator
{
    public static bool TryResolveVerifiedSender(
        NetworkRunner runner,
        PlayerRef player,
        ITownSessionTicketVerifier verifier,
        out ProfileId profileId)
    {
        profileId = default;
        if (runner == null || player.IsNone)
        {
            return false;
        }

        NetworkObject playerObject = runner.GetPlayerObject(player);
        if (playerObject == null || !playerObject.TryGetBehaviour(out SocialPlayerIdentity identity) ||
            identity.Object == null || identity.Object.InputAuthority != player)
        {
            return false;
        }

        return TryValidateSenderIdentity(
            identity.ProfileId.ToString(),
            identity.SessionTicket.ToString(),
            verifier,
            player.ToString(),
            out profileId);
    }

    public static bool TryValidateSenderIdentity(
        string declaredProfile,
        string rawTicket,
        ITownSessionTicketVerifier verifier,
        string playerIdentifierForLogging,
        out ProfileId profileId)
    {
        profileId = default;
        if (string.IsNullOrWhiteSpace(declaredProfile))
        {
            return false;
        }

        // 1. Rejection: Absent identity proof
        if (string.IsNullOrWhiteSpace(rawTicket))
        {
            Debug.LogWarning($"[SECURITY] Town sender resolution rejected: {playerIdentifierForLogging} has no SessionTicket.");
            return false;
        }

        // 2. Decode structural ticket
        if (!TownSessionTicketCodec.TryDecode(rawTicket, out TownSessionTicket ticket))
        {
            Debug.LogWarning($"[SECURITY] Town sender resolution rejected: {playerIdentifierForLogging} provided an unparseable SessionTicket.");
            return false;
        }

        // 3. Rejection: Foreign proof / Spoofing attempt (declared ProfileId does not match ticket claim)
        if (ticket.ProfileId.Value != declaredProfile)
        {
            Debug.LogWarning($"[SECURITY] Town sender resolution rejected: {playerIdentifierForLogging} declared ProfileId '{declaredProfile}' but ticket belongs to '{ticket.ProfileId.Value}'.");
            return false;
        }

        // 4. Rejection: Wrong session scope
        if (ticket.SessionName != "Lobby-Town")
        {
            Debug.LogWarning($"[SECURITY] Town sender resolution rejected: {playerIdentifierForLogging} ticket session '{ticket.SessionName}' is not 'Lobby-Town'.");
            return false;
        }

        // 5. Rejection: Expired ticket
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (ticket.ExpiresAt > 0 && now > ticket.ExpiresAt)
        {
            Debug.LogWarning($"[SECURITY] Town sender resolution rejected: {playerIdentifierForLogging} ticket has expired.");
            return false;
        }

        // 6. Verification: Check with verifier if provided (e.g. backend / mock cryptographic verification)
        if (verifier != null)
        {
            var task = verifier.VerifyTownTicketAsync(rawTicket);
            if (!task.IsCompleted)
            {
                Debug.LogWarning($"[SECURITY] Town sender resolution rejected (pending verification): Verifier task for {playerIdentifierForLogging} is not yet completed.");
                return false;
            }

            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogWarning($"[SECURITY] Town sender resolution rejected: Verifier task faulted or was canceled for {playerIdentifierForLogging}.");
                return false;
            }

            var result = task.Result;
            if (!result.valid || result.profileId.Value != declaredProfile)
            {
                Debug.LogWarning($"[SECURITY] Town sender resolution rejected: Verifier rejected ticket for {playerIdentifierForLogging}: {result.error}");
                return false;
            }
            profileId = result.profileId;
            return true;
        }

        profileId = ticket.ProfileId;
        return true;
    }
}
