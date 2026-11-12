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

        TownSenderIdentityContext context = runner.GetComponent<TownSenderIdentityContext>();
        if (context == null)
        {
            Debug.LogWarning($"[SECURITY] Town sender resolution rejected: runner has no {nameof(TownSenderIdentityContext)}.");
            return false;
        }

        if (!context.Registry.TryResolve(player, verifier, out ProfileId bound) ||
            !TryResolveDeclaredProfile(runner, player, out ProfileId declared) || declared != bound)
        {
            return false;
        }

        profileId = bound;
        return true;
    }

    /// <summary>Replicated, unauthenticated claim. For presentation only; never authorize with it.</summary>
    public static bool TryResolveDeclaredProfile(NetworkRunner runner, PlayerRef player, out ProfileId profileId)
    {
        profileId = default;
        NetworkObject playerObject = runner != null && !player.IsNone ? runner.GetPlayerObject(player) : null;
        if (playerObject == null || !playerObject.TryGetBehaviour(out SocialPlayerIdentity identity) ||
            identity.Object == null || identity.Object.InputAuthority != player ||
            string.IsNullOrWhiteSpace(identity.ProfileId.ToString()))
        {
            return false;
        }

        profileId = new ProfileId(identity.ProfileId.ToString());
        return true;
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

        // 6. Claims are not authentication: a configured verifier is mandatory.
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

        Debug.LogWarning($"[SECURITY] Town sender resolution rejected: no ticket verifier configured for {playerIdentifierForLogging}.");
        return false;
    }
}
