using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>Authoritative Town source of truth for Solo/Duo Parties, invitations and restoration.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public sealed class TownPartyDirectory : NetworkBehaviour, IStateAuthorityChanged
{
    private const float InvitationTimeoutSeconds = 20f;
    private const float InvitationCooldownSeconds = 5f;

    [Networked] private int NextPartyId { get; set; }
    [Networked] private int NextInvitationId { get; set; }
    [Networked] public int StateEpoch { get; private set; }
    [Networked, Capacity(RaidSessionRules.MaxParticipants)] private NetworkArray<TownPartyNetworkEntry> Parties => default;
    [Networked, Capacity(RaidSessionRules.MaxParticipants)] private NetworkArray<TownPartyInvitationNetworkEntry> Invitations => default;
    [Networked, Capacity(RaidSessionRules.MaxParticipants)] private NetworkArray<TownPartyInvitationCooldownEntry> InvitationCooldowns => default;

    private readonly Dictionary<PlayerRef, ProfileId> _profileByPlayer = new();
    private readonly Queue<TownPartyInvitationResultEvent> _resultEvents = new();
    private readonly TownPartyContinuationClaimRegistry _continuationClaims = new();
    private int _observedEpoch = -1;
    private bool _localRequestPending;

    public event Action<TownPartyInvitationResultEvent> InvitationResultReceived;
    public int PartyCount { get { int count = 0; for (int i = 0; i < RaidSessionRules.MaxParticipants; i++) if (Parties[i].IsOccupied) count++; return count; } }

    public override void Spawned()
    {
        Runner.GetComponent<TownPartyDirectoryContext>()?.Register(this);
        if (HasStateAuthority) AdvanceEpoch();
        _localRequestPending = true;
    }

    public override void Render()
    {
        if (_observedEpoch != StateEpoch)
        {
            _observedEpoch = StateEpoch;
            _localRequestPending = true;
        }
        ObserveLocalParty();
        while (_resultEvents.Count > 0) InvitationResultReceived?.Invoke(_resultEvents.Dequeue());
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;
        for (int index = 0; index < RaidSessionRules.MaxParticipants; index++)
        {
            TownPartyInvitationNetworkEntry invitation = Invitations[index];
            if (invitation.IsPending && invitation.ExpiresAt.Expired(Runner))
                ResolveInvitation(index, invitation, TownPartyInvitationResult.Expired);
            TownPartyInvitationCooldownEntry cooldown = InvitationCooldowns[index];
            if (cooldown.IsActive && cooldown.ExpiresAt.Expired(Runner)) InvitationCooldowns.Set(index, default);
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        runner.GetComponent<TownPartyDirectoryContext>()?.Unregister(this);
        _profileByPlayer.Clear();
        _resultEvents.Clear();
        _continuationClaims.Clear();
        _localRequestPending = false;
    }

    public void StateAuthorityChanged()
    {
        if (!HasStateAuthority) return;
        _continuationClaims.Clear();
        AdvanceEpoch();
    }

    public bool RequestInvite(ProfileId recipient) { if (!CanSend || !recipient.IsValid) return false; RPC_RequestInvite(recipient.Value); return true; }
    public bool RequestRespondToInvitation(int invitationId, bool accept) { if (!CanSend || invitationId <= 0) return false; RPC_RequestInvitationResponse(invitationId, accept); return true; }
    public bool RequestLeaveParty() { if (!CanSend) return false; RPC_RequestLeaveParty(); return true; }

    public bool TryGetParty(ProfileId profileId, out TownPartySnapshot snapshot)
    {
        if (profileId.IsValid)
        {
            for (int index = 0; index < RaidSessionRules.MaxParticipants; index++)
            {
                TownPartyNetworkEntry entry = Parties[index];
                if (entry.IsOccupied && EntryContains(entry, profileId))
                {
                    snapshot = ToSnapshot(entry);
                    return TownPartyRules.IsValid(snapshot);
                }
            }
        }
        snapshot = default;
        return false;
    }

    public bool TryGetPendingInvitation(ProfileId profileId, out TownPartyInvitationSnapshot invitation)
    {
        for (int index = 0; index < RaidSessionRules.MaxParticipants; index++)
        {
            TownPartyInvitationNetworkEntry entry = Invitations[index];
            if (entry.IsPending && (entry.InviterProfileId.ToString() == profileId.Value || entry.RecipientProfileId.ToString() == profileId.Value))
            {
                invitation = new TownPartyInvitationSnapshot(entry);
                return true;
            }
        }
        invitation = default;
        return false;
    }

    public bool TryGetDisplayName(ProfileId profileId, out string displayName)
    {
        displayName = null;
        if (!TryResolvePlayer(profileId, out PlayerRef player)) return false;
        NetworkObject playerObject = Runner.GetPlayerObject(player);
        if (playerObject == null || !playerObject.TryGetBehaviour(out SocialPlayerIdentity identity)) return false;
        displayName = identity.DisplayName.ToString();
        return !string.IsNullOrWhiteSpace(displayName);
    }

    public bool TryResolvePlayer(ProfileId profileId, out PlayerRef player)
    {
        player = PlayerRef.None;
        foreach (KeyValuePair<PlayerRef, ProfileId> pair in _profileByPlayer)
        {
            if (pair.Value == profileId) { player = pair.Key; return true; }
        }
        if (Runner == null) return false;
        foreach (PlayerRef candidate in Runner.ActivePlayers)
        {
            if (!TryResolveSender(candidate, out ProfileId candidateProfile)) continue;
            _profileByPlayer[candidate] = candidateProfile;
            if (candidateProfile == profileId) { player = candidate; return true; }
        }
        return false;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private RpcInvokeInfo RPC_RequestEnsureParty(RpcInfo info = default)
    {
        if (!CanMutate || !TryResolveSender(info.Source, out ProfileId profileId) || TryGetParty(profileId, out _)) return default;
        NetworkObject playerObject = Runner.GetPlayerObject(info.Source);
        if (playerObject != null && playerObject.TryGetBehaviour(out SocialPlayerIdentity identity) && identity.HasPendingPartyContinuation) return default;
        _profileByPlayer[info.Source] = profileId;
        TryCreateSoloParty(profileId);
        return default;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private RpcInvokeInfo RPC_RequestInvite(NetworkString<_32> requestedRecipient, RpcInfo info = default)
    {
        ProfileId recipient = new(requestedRecipient.ToString());
        if (!CanMutate || !TryResolveSender(info.Source, out ProfileId inviter) || !recipient.IsValid || inviter == recipient ||
            !TryResolvePlayer(recipient, out _) || HasPendingInvitation(inviter) || HasPendingInvitation(recipient))
        { SendInvitationResult(info.Source, TownPartyInvitationResult.Unavailable); return default; }

        if (!TryGetPartyEntry(inviter, out _, out TownPartyNetworkEntry inviterParty) ||
            !TryGetPartyEntry(recipient, out _, out TownPartyNetworkEntry recipientParty))
        { SendInvitationResult(info.Source, TownPartyInvitationResult.Busy); return default; }

        TownPartySnapshot inviterSnapshot = ToSnapshot(inviterParty);
        TownPartySnapshot recipientSnapshot = ToSnapshot(recipientParty);
        if (!TownPartyRules.IsSolo(inviterSnapshot)) { SendInvitationResult(info.Source, TownPartyInvitationResult.PartyFull); return default; }
        if (!TownPartyRules.IsSolo(recipientSnapshot)) { SendInvitationResult(info.Source, TownPartyInvitationResult.AlreadyGrouped); return default; }
        if (HasActiveCooldown(inviter, recipient) || !HasReservedCooldownCapacity())
        { SendInvitationResult(info.Source, TownPartyInvitationResult.Cooldown); return default; }

        for (int index = 0; index < RaidSessionRules.MaxParticipants; index++)
        {
            if (Invitations[index].IsPending) continue;
            int id = NextInvitationId == int.MaxValue ? 1 : NextInvitationId + 1;
            NextInvitationId = id;
            Invitations.Set(index, new TownPartyInvitationNetworkEntry
            {
                InvitationId = id,
                InviterProfileId = inviter.Value,
                RecipientProfileId = recipient.Value,
                InviterPartyId = inviterParty.PartyId,
                InviterPartyRevision = inviterParty.Revision,
                RecipientPartyId = recipientParty.PartyId,
                RecipientPartyRevision = recipientParty.Revision,
                ExpiresAt = TickTimer.CreateFromSeconds(Runner, InvitationTimeoutSeconds)
            });
            return default;
        }
        SendInvitationResult(info.Source, TownPartyInvitationResult.Busy);
        return default;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private RpcInvokeInfo RPC_RequestInvitationResponse(int invitationId, NetworkBool accept, RpcInfo info = default)
    {
        if (!CanMutate || !TryResolveSender(info.Source, out ProfileId recipient) ||
            !TryFindInvitation(invitationId, out int invitationIndex, out TownPartyInvitationNetworkEntry invitation) ||
            invitation.RecipientProfileId.ToString() != recipient.Value)
        { SendInvitationResult(info.Source, TownPartyInvitationResult.Unavailable); return default; }
        if (invitation.ExpiresAt.Expired(Runner)) { ResolveInvitation(invitationIndex, invitation, TownPartyInvitationResult.Expired); return default; }
        if (!accept) { ResolveInvitation(invitationIndex, invitation, TownPartyInvitationResult.Rejected); return default; }

        if (!TryGetPartyEntry(new ProfileId(invitation.InviterProfileId.ToString()), out int inviterIndex, out TownPartyNetworkEntry inviterParty) ||
            !TryGetPartyEntry(recipient, out int recipientIndex, out TownPartyNetworkEntry recipientParty) ||
            inviterParty.PartyId != invitation.InviterPartyId || inviterParty.Revision != invitation.InviterPartyRevision ||
            recipientParty.PartyId != invitation.RecipientPartyId || recipientParty.Revision != invitation.RecipientPartyRevision ||
            !TownPartyRules.CanMerge(ToSnapshot(inviterParty), ToSnapshot(recipientParty)))
        { ResolveInvitation(invitationIndex, invitation, TownPartyInvitationResult.Busy); return default; }

        inviterParty.SecondMemberProfileId = recipient.Value;
        inviterParty.MemberCount = 2;
        inviterParty.Revision++;
        Parties.Set(inviterIndex, inviterParty);
        Parties.Set(recipientIndex, default);
        CancelOtherInvitations(new ProfileId(invitation.InviterProfileId.ToString()), recipient, invitationId);
        ResolveInvitation(invitationIndex, invitation, TownPartyInvitationResult.Accepted);
        return default;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private RpcInvokeInfo RPC_RequestLeaveParty(RpcInfo info = default)
    {
        if (!CanMutate || !TryResolveSender(info.Source, out ProfileId leaver) ||
            !TryGetPartyEntry(leaver, out int partyIndex, out TownPartyNetworkEntry party) || party.MemberCount != 2 ||
            !TryFindFreePartySlot(out int freeIndex)) return default;

        ProfileId remaining = new(party.FirstMemberProfileId.ToString() == leaver.Value
            ? party.SecondMemberProfileId.ToString()
            : party.FirstMemberProfileId.ToString());
        party.HostProfileId = remaining.Value;
        party.FirstMemberProfileId = remaining.Value;
        party.SecondMemberProfileId = default;
        party.MemberCount = 1;
        party.Revision++;
        Parties.Set(partyIndex, party);
        Parties.Set(freeIndex, CreateEntry(NextPartyIdentifier(), leaver, leaver));
        CancelOtherInvitations(leaver, remaining, 0);
        return default;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private RpcInvokeInfo RPC_RequestRestoreContinuation(
        NetworkString<_32> hostValue, int memberCount, NetworkString<_32> firstValue, NetworkString<_32> secondValue,
        RpcInfo info = default)
    {
        if (!CanMutate || !TryResolveSender(info.Source, out ProfileId claimant) ||
            !TryDecodeContext(hostValue, memberCount, firstValue, secondValue, out TownPartyContinuationContext context)) return default;

        if (TryGetParty(claimant, out TownPartySnapshot existing))
        {
            if (context.Matches(existing)) RPC_AcknowledgeContinuation(info.Source, hostValue, memberCount, firstValue, secondValue);
            else RPC_InvalidateContinuation(info.Source, hostValue, memberCount, firstValue, secondValue);
            return default;
        }

        TownPartyContinuationClaimResult result = _continuationClaims.Submit(claimant, context);
        if (result == TownPartyContinuationClaimResult.Rejected)
        { RPC_InvalidateContinuation(info.Source, hostValue, memberCount, firstValue, secondValue); return default; }
        if (result == TownPartyContinuationClaimResult.ReadyToRestore && TryRestoreParty(context)) _continuationClaims.MarkRestored(context);
        RPC_AcknowledgeContinuation(info.Source, hostValue, memberCount, firstValue, secondValue);
        return default;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_AcknowledgeContinuation([RpcTarget] PlayerRef target, NetworkString<_32> host, int count, NetworkString<_32> first, NetworkString<_32> second)
    {
        SessionConnectionCoordinator coordinator = SessionConnectionCoordinator.Instance;
        if (coordinator != null && coordinator.TryGetPendingPartyContinuation(out TownPartyContinuationContext pending) &&
            TryDecodeContext(host, count, first, second, out TownPartyContinuationContext received) && pending.Equals(received) &&
            TryGetParty(LocalProfileProvider.GetOrCreateLocalProfile(), out TownPartySnapshot party) && pending.Matches(party))
            coordinator.ConfirmPartyContinuationRestored(party);
        _localRequestPending = true;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_InvalidateContinuation([RpcTarget] PlayerRef target, NetworkString<_32> host, int count, NetworkString<_32> first, NetworkString<_32> second)
    {
        SessionConnectionCoordinator coordinator = SessionConnectionCoordinator.Instance;
        if (coordinator != null && coordinator.TryGetPendingPartyContinuation(out TownPartyContinuationContext pending) &&
            TryDecodeContext(host, count, first, second, out TownPartyContinuationContext received) && pending.Equals(received))
            coordinator.AbandonPartyContinuation(pending);
        _localRequestPending = true;
    }

    private void ObserveLocalParty()
    {
        if (!_localRequestPending || Runner == null || Runner.GetPlayerObject(Runner.LocalPlayer) == null) return;
        ProfileId local = LocalProfileProvider.GetOrCreateLocalProfile();
        SessionConnectionCoordinator coordinator = SessionConnectionCoordinator.Instance;
        if (coordinator != null && coordinator.TryGetPendingPartyContinuation(out TownPartyContinuationContext context))
        {
            if (TryGetParty(local, out TownPartySnapshot existing))
            {
                if (context.Matches(existing)) coordinator.ConfirmPartyContinuationRestored(existing);
                else coordinator.AbandonPartyContinuation(context);
                _localRequestPending = true;
                return;
            }
            _localRequestPending = false;
            RPC_RequestRestoreContinuation(context.HostProfileId.Value, context.Members.Count, context.Members[0].Value,
                context.Members.Count > 1 ? context.Members[1].Value : string.Empty);
            return;
        }
        if (TryGetParty(local, out _)) { _localRequestPending = false; return; }
        _localRequestPending = false;
        RPC_RequestEnsureParty();
    }

    private bool TryRestoreParty(TownPartyContinuationContext context)
    {
        for (int index = 0; index < context.Members.Count; index++)
            if (!TryResolvePlayer(context.Members[index], out _) || TryGetParty(context.Members[index], out _)) return false;
        if (!TryFindFreePartySlot(out int slot)) return false;
        TownPartyNetworkEntry entry = CreateEntry(NextPartyIdentifier(), context.HostProfileId, context.Members[0]);
        if (context.Members.Count == 2) { entry.SecondMemberProfileId = context.Members[1].Value; entry.MemberCount = 2; }
        Parties.Set(slot, entry);
        return true;
    }

    private bool TryCreateSoloParty(ProfileId profile)
    {
        if (!TryFindFreePartySlot(out int slot)) return false;
        Parties.Set(slot, CreateEntry(NextPartyIdentifier(), profile, profile));
        return true;
    }

    private static TownPartyNetworkEntry CreateEntry(int partyId, ProfileId host, ProfileId first) => new()
    { PartyId = partyId, HostProfileId = host.Value, FirstMemberProfileId = first.Value, MemberCount = 1, Revision = 1 };

    private int NextPartyIdentifier() { NextPartyId = NextPartyId == int.MaxValue ? 1 : NextPartyId + 1; return NextPartyId; }
    private void AdvanceEpoch() { StateEpoch = StateEpoch == int.MaxValue ? 1 : StateEpoch + 1; }

    private bool TryGetPartyEntry(ProfileId profile, out int entryIndex, out TownPartyNetworkEntry entry)
    {
        for (int index = 0; index < RaidSessionRules.MaxParticipants; index++)
        { TownPartyNetworkEntry candidate = Parties[index]; if (candidate.IsOccupied && EntryContains(candidate, profile)) { entryIndex = index; entry = candidate; return true; } }
        entryIndex = -1; entry = default; return false;
    }

    private bool TryFindFreePartySlot(out int slot)
    { for (int index = 0; index < RaidSessionRules.MaxParticipants; index++) if (!Parties[index].IsOccupied) { slot = index; return true; } slot = -1; return false; }

    private static bool EntryContains(in TownPartyNetworkEntry entry, ProfileId profile) =>
        entry.FirstMemberProfileId.ToString() == profile.Value || (entry.MemberCount == 2 && entry.SecondMemberProfileId.ToString() == profile.Value);

    private static TownPartySnapshot ToSnapshot(in TownPartyNetworkEntry entry)
    {
        var members = new ProfileId[entry.MemberCount];
        if (entry.MemberCount > 0) members[0] = new ProfileId(entry.FirstMemberProfileId.ToString());
        if (entry.MemberCount > 1) members[1] = new ProfileId(entry.SecondMemberProfileId.ToString());
        return new TownPartySnapshot(entry.PartyId, new ProfileId(entry.HostProfileId.ToString()), members, entry.Revision);
    }

    private bool TryResolveSender(PlayerRef player, out ProfileId profile)
    {
        profile = default;
        NetworkObject playerObject = Runner != null && !player.IsNone ? Runner.GetPlayerObject(player) : null;
        if (playerObject == null || !playerObject.TryGetBehaviour(out SocialPlayerIdentity identity) || identity.Object.InputAuthority != player ||
            string.IsNullOrWhiteSpace(identity.ProfileId.ToString())) return false;
        profile = new ProfileId(identity.ProfileId.ToString());
        return true;
    }

    private bool TryFindInvitation(int id, out int invitationIndex, out TownPartyInvitationNetworkEntry invitation)
    { for (int index = 0; index < RaidSessionRules.MaxParticipants; index++) { TownPartyInvitationNetworkEntry candidate = Invitations[index]; if (candidate.IsPending && candidate.InvitationId == id) { invitationIndex = index; invitation = candidate; return true; } } invitationIndex = -1; invitation = default; return false; }
    private bool HasPendingInvitation(ProfileId profile)
    { for (int index = 0; index < RaidSessionRules.MaxParticipants; index++) { TownPartyInvitationNetworkEntry entry = Invitations[index]; if (entry.IsPending && (entry.InviterProfileId.ToString() == profile.Value || entry.RecipientProfileId.ToString() == profile.Value)) return true; } return false; }

    private void CancelOtherInvitations(ProfileId first, ProfileId second, int except)
    {
        for (int index = 0; index < RaidSessionRules.MaxParticipants; index++)
        { TownPartyInvitationNetworkEntry entry = Invitations[index]; if (entry.IsPending && entry.InvitationId != except &&
            (entry.InviterProfileId.ToString() == first.Value || entry.RecipientProfileId.ToString() == first.Value || entry.InviterProfileId.ToString() == second.Value || entry.RecipientProfileId.ToString() == second.Value))
            ResolveInvitation(index, entry, TownPartyInvitationResult.Busy); }
    }

    private void ResolveInvitation(int index, in TownPartyInvitationNetworkEntry invitation, TownPartyInvitationResult result)
    {
        Invitations.Set(index, default);
        ProfileId inviter = new(invitation.InviterProfileId.ToString());
        ProfileId recipient = new(invitation.RecipientProfileId.ToString());
        AddCooldown(inviter, recipient);
        SendInvitationResult(inviter, result);
        SendInvitationResult(recipient, result);
    }

    private void AddCooldown(ProfileId first, ProfileId second)
    {
        NormalizePair(first, second, out first, out second);
        for (int index = 0; index < RaidSessionRules.MaxParticipants; index++)
        {
            TownPartyInvitationCooldownEntry entry = InvitationCooldowns[index];
            if (entry.IsActive && !entry.ExpiresAt.Expired(Runner)) continue;
            InvitationCooldowns.Set(index, new TownPartyInvitationCooldownEntry { FirstProfileId = first.Value, SecondProfileId = second.Value, ExpiresAt = TickTimer.CreateFromSeconds(Runner, InvitationCooldownSeconds) });
            return;
        }
    }

    private bool HasActiveCooldown(ProfileId first, ProfileId second)
    {
        NormalizePair(first, second, out first, out second);
        for (int index = 0; index < RaidSessionRules.MaxParticipants; index++)
        { TownPartyInvitationCooldownEntry entry = InvitationCooldowns[index]; if (entry.IsActive && !entry.ExpiresAt.Expired(Runner) && entry.FirstProfileId.ToString() == first.Value && entry.SecondProfileId.ToString() == second.Value) return true; }
        return false;
    }

    private bool HasReservedCooldownCapacity()
    { for (int index = 0; index < RaidSessionRules.MaxParticipants; index++) { TownPartyInvitationCooldownEntry entry = InvitationCooldowns[index]; if (!entry.IsActive || entry.ExpiresAt.Expired(Runner)) return true; } return false; }

    private static void NormalizePair(ProfileId first, ProfileId second, out ProfileId normalizedFirst, out ProfileId normalizedSecond)
    { if (string.CompareOrdinal(first.Value, second.Value) <= 0) { normalizedFirst = first; normalizedSecond = second; } else { normalizedFirst = second; normalizedSecond = first; } }

    private void SendInvitationResult(ProfileId profile, TownPartyInvitationResult result) { if (TryResolvePlayer(profile, out PlayerRef player)) SendInvitationResult(player, result); }
    private void SendInvitationResult(PlayerRef player, TownPartyInvitationResult result) { if (!player.IsNone) RPC_ReceiveInvitationResult(player, result); }
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)] private void RPC_ReceiveInvitationResult([RpcTarget] PlayerRef target, TownPartyInvitationResult result) => _resultEvents.Enqueue(new TownPartyInvitationResultEvent(result));

    private static bool TryDecodeContext(NetworkString<_32> host, int count, NetworkString<_32> first, NetworkString<_32> second, out TownPartyContinuationContext context)
    {
        if (count < 1 || count > TownPartyRules.MaxMembers) { context = null; return false; }
        var members = new ProfileId[count]; members[0] = new ProfileId(first.ToString()); if (count == 2) members[1] = new ProfileId(second.ToString());
        return TownPartyContinuationContext.TryCreate(new ProfileId(host.ToString()), members, out context);
    }

    private bool CanSend => Runner != null && Object != null && Object.IsValid && SessionConnectionCoordinator.Instance?.State == SessionConnectionState.Town;
    private bool CanMutate => HasStateAuthority && Runner != null && Object != null && Object.IsValid;
}
