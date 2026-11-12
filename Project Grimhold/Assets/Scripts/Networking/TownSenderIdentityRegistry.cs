using System;
using System.Collections.Generic;
using Fusion;
using Grimhold.Backend;
using UnityEngine;

/// <summary>
/// Town State Authority view of who each network sender is. A profile is bound to the PlayerRef that
/// physically delivered its TownSessionTicket to this peer (the RPC source), never to a replicated or
/// self-declared identity. Only a verified binding blocks other claimants: unverified claims are kept as
/// candidates, definitively rejected ones are evicted, and among genuine claims of one profile the
/// earliest registration owns it. A bound sender cannot switch to another profile.
/// </summary>
public sealed class TownSenderIdentityRegistry
{
    private readonly Dictionary<PlayerRef, Binding> _bindings = new();
    private readonly List<PlayerRef> _evictedSenders = new();
    private long _nextSequence;

    private enum VerificationState
    {
        Pending,
        Valid,
        Invalid
    }

    public TownSenderRegistrationResult Register(
        PlayerRef sender,
        string rawTicket,
        ITownSessionTicketVerifier verifier,
        Func<PlayerRef, bool> isSenderActive)
    {
        if (sender.IsNone || isSenderActive == null)
        {
            return TownSenderRegistrationResult.RejectedInvalidTicket;
        }

        if (string.IsNullOrWhiteSpace(rawTicket) ||
            !TownSessionTicketCodec.TryDecode(rawTicket, out TownSessionTicket ticket) ||
            !ticket.ProfileId.IsValid)
        {
            Debug.LogWarning($"[SECURITY] Town sender registration rejected: {sender} provided an unparseable SessionTicket.");
            return TownSenderRegistrationResult.RejectedInvalidTicket;
        }

        ProfileId claimed = ticket.ProfileId;
        bool isRefresh = _bindings.TryGetValue(sender, out Binding existing);
        if (isRefresh && existing.Profile != claimed)
        {
            Debug.LogWarning($"[SECURITY] Town sender registration rejected: {sender} is already bound to '{existing.Profile.Value}' and cannot claim '{claimed.Value}'.");
            return TownSenderRegistrationResult.RejectedSenderBoundToAnotherProfile;
        }

        _evictedSenders.Clear();
        foreach (KeyValuePair<PlayerRef, Binding> pair in _bindings)
        {
            if (pair.Key == sender || pair.Value.Profile != claimed)
            {
                continue;
            }

            if (!isSenderActive(pair.Key))
            {
                _evictedSenders.Add(pair.Key);
                continue;
            }

            VerificationState state = Evaluate(pair.Value, verifier);
            if (state == VerificationState.Valid)
            {
                Debug.LogWarning($"[SECURITY] Town sender registration rejected: {sender} claimed '{claimed.Value}', which is already bound to active sender {pair.Key}.");
                _evictedSenders.Clear();
                return TownSenderRegistrationResult.RejectedProfileBoundToAnotherSender;
            }

            if (state == VerificationState.Invalid)
            {
                _evictedSenders.Add(pair.Key);
            }
        }

        RemoveEvicted();
        long sequence = isRefresh ? existing.Sequence : ++_nextSequence;
        _bindings[sender] = new Binding(claimed, rawTicket, sequence);
        return TownSenderRegistrationResult.Accepted;
    }

    /// <summary>
    /// Resolves the verified profile bound to this exact sender; fails closed while verification is pending
    /// or while an earlier genuine claim of the same profile owns it.
    /// </summary>
    public bool TryResolve(PlayerRef sender, ITownSessionTicketVerifier verifier, out ProfileId profileId)
    {
        profileId = default;
        if (sender.IsNone || !_bindings.TryGetValue(sender, out Binding binding))
        {
            return false;
        }

        if (!TownSenderIdentityValidator.TryValidateSenderIdentity(
                binding.Profile.Value,
                binding.RawTicket,
                verifier,
                sender.ToString(),
                out ProfileId verified))
        {
            return false;
        }

        if (HasEarlierOwner(sender, binding, verifier, out PlayerRef owner))
        {
            Debug.LogWarning($"[SECURITY] Town sender resolution rejected: {sender} claims '{binding.Profile.Value}', owned by earlier sender {owner}.");
            return false;
        }

        profileId = verified;
        return true;
    }

    /// <summary>Finds the sender that currently owns a verified profile.</summary>
    public bool TryFindSender(ProfileId profileId, ITownSessionTicketVerifier verifier, out PlayerRef sender)
    {
        sender = PlayerRef.None;
        if (!profileId.IsValid)
        {
            return false;
        }

        long ownerSequence = long.MaxValue;
        foreach (KeyValuePair<PlayerRef, Binding> pair in _bindings)
        {
            if (pair.Value.Profile == profileId && pair.Value.Sequence < ownerSequence &&
                Evaluate(pair.Value, verifier) == VerificationState.Valid)
            {
                sender = pair.Key;
                ownerSequence = pair.Value.Sequence;
            }
        }

        return !sender.IsNone;
    }

    public bool Remove(PlayerRef sender) => _bindings.Remove(sender);

    public void Clear() => _bindings.Clear();

    private bool HasEarlierOwner(PlayerRef sender, in Binding binding, ITownSessionTicketVerifier verifier, out PlayerRef owner)
    {
        owner = PlayerRef.None;
        _evictedSenders.Clear();
        foreach (KeyValuePair<PlayerRef, Binding> pair in _bindings)
        {
            if (pair.Key == sender || pair.Value.Profile != binding.Profile)
            {
                continue;
            }

            VerificationState state = Evaluate(pair.Value, verifier);
            if (state == VerificationState.Invalid)
            {
                _evictedSenders.Add(pair.Key);
            }
            else if (state == VerificationState.Valid && pair.Value.Sequence < binding.Sequence)
            {
                owner = pair.Key;
            }
        }

        RemoveEvicted();
        return !owner.IsNone;
    }

    /// <summary>Quiet verification probe; transport failures and pending checks are never treated as rejection.</summary>
    private static VerificationState Evaluate(in Binding binding, ITownSessionTicketVerifier verifier)
    {
        if (binding.ExpiresAt > 0 && DateTimeOffset.UtcNow.ToUnixTimeSeconds() > binding.ExpiresAt)
        {
            return VerificationState.Invalid;
        }

        if (verifier == null)
        {
            return VerificationState.Pending;
        }

        var task = verifier.VerifyTownTicketAsync(binding.RawTicket);
        if (task == null || !task.IsCompleted || task.IsFaulted || task.IsCanceled)
        {
            return VerificationState.Pending;
        }

        var result = task.Result;
        if (result.valid && result.profileId == binding.Profile)
        {
            return VerificationState.Valid;
        }

        return BackendErrorUtility.IsTransportFailure(result.error) ? VerificationState.Pending : VerificationState.Invalid;
    }

    private void RemoveEvicted()
    {
        for (int index = 0; index < _evictedSenders.Count; index++)
        {
            _bindings.Remove(_evictedSenders[index]);
        }

        _evictedSenders.Clear();
    }

    private readonly struct Binding
    {
        public Binding(ProfileId profile, string rawTicket, long sequence)
        {
            Profile = profile;
            RawTicket = rawTicket;
            Sequence = sequence;
            ExpiresAt = TownSessionTicketCodec.TryDecode(rawTicket, out TownSessionTicket ticket) ? ticket.ExpiresAt : 0;
        }

        public ProfileId Profile { get; }
        public string RawTicket { get; }
        public long Sequence { get; }
        public long ExpiresAt { get; }
    }
}
