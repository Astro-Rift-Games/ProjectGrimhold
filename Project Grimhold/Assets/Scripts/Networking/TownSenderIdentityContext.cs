using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// Runner-scoped Town sender binding. The Town State Authority binds each PlayerRef to the ticket that
/// PlayerRef physically delivered by RPC; tickets are never replicated to other clients.
/// </summary>
public sealed class TownSenderIdentityContext : NetworkRunnerCallbacksAdapter
{
    private readonly List<NetworkObject> _authorityObjects = new();
    private readonly Dictionary<PlayerRef, string> _deliveredTicketByAuthority = new();
    private string _deliveredTicket;

    public TownSenderIdentityRegistry Registry { get; } = new();

    /// <summary>Optional verifier override (tests); defaults to the application Town ticket verifier.</summary>
    public ITownSessionTicketVerifier TicketVerifier { get; set; }

    public void RegisterAuthorityObject(NetworkObject authorityObject)
    {
        if (authorityObject == null || _authorityObjects.Contains(authorityObject))
        {
            return;
        }

        _authorityObjects.Add(authorityObject);
    }

    public void UnregisterAuthorityObject(NetworkObject authorityObject)
    {
        _authorityObjects.Remove(authorityObject);
    }

    public TownSenderRegistrationResult AcceptRegistration(
        PlayerRef sender,
        string rawTicket,
        Func<PlayerRef, bool> isSenderActive)
    {
        ITownSessionTicketVerifier verifier = TicketVerifier ?? ApplicationAuthContext.Instance?.TownTicketVerifier;
        TownSenderRegistrationResult result = Registry.Register(sender, rawTicket, verifier, isSenderActive);
        if (result == TownSenderRegistrationResult.Accepted)
        {
            verifier?.VerifyTownTicketAsync(rawTicket);
        }

        return result;
    }

    /// <summary>Delivers the local ticket once to every distinct Town State Authority.</summary>
    public void PublishLocalTicket(NetworkRunner runner, string rawTicket)
    {
        if (runner == null || string.IsNullOrWhiteSpace(rawTicket))
        {
            return;
        }

        if (!string.Equals(_deliveredTicket, rawTicket, StringComparison.Ordinal))
        {
            _deliveredTicket = rawTicket;
            _deliveredTicketByAuthority.Clear();
        }

        for (int index = _authorityObjects.Count - 1; index >= 0; index--)
        {
            NetworkObject authorityObject = _authorityObjects[index];
            if (authorityObject == null)
            {
                _authorityObjects.RemoveAt(index);
                continue;
            }

            if (!authorityObject.IsValid)
            {
                continue;
            }

            // Single mode reports StateAuthority as None while HasStateAuthority is true.
            PlayerRef authority = authorityObject.HasStateAuthority ? runner.LocalPlayer : authorityObject.StateAuthority;
            if (authority.IsNone || _deliveredTicketByAuthority.ContainsKey(authority))
            {
                continue;
            }

            _deliveredTicketByAuthority[authority] = rawTicket;
            if (authority == runner.LocalPlayer)
            {
                AcceptRegistration(runner.LocalPlayer, rawTicket, player => IsActive(runner, player));
                continue;
            }

            SocialPlayerIdentity.RPC_RegisterTownSender(runner, authority, rawTicket);
        }
    }

    public static bool IsActive(NetworkRunner runner, PlayerRef player)
    {
        if (runner == null)
        {
            return false;
        }

        foreach (PlayerRef active in runner.ActivePlayers)
        {
            if (active == player)
            {
                return true;
            }
        }

        return false;
    }

    public override void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        Registry.Remove(player);
        _deliveredTicketByAuthority.Remove(player);
    }

    public override void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        Registry.Clear();
        _authorityObjects.Clear();
        _deliveredTicketByAuthority.Clear();
        _deliveredTicket = null;
    }
}
