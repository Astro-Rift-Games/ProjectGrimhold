using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// Master-client-authoritative Shared Mode queue for one prospective raid cohort.
/// Presentation calls its request methods; only State Authority mutates the replicated queue.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public sealed class TownRaidQueueNetworkController : NetworkBehaviour, IPlayerLeft, IStateAuthorityChanged
{
    private const byte ManifestIdentityReceived = 1 << 0;
    private const byte ManifestCredentialReceived = 1 << 1;
    private const byte ManifestMembers01Received = 1 << 2;
    private const byte ManifestMembers23Received = 1 << 3;
    private const byte CompleteManifestMask = ManifestIdentityReceived | ManifestCredentialReceived |
                                               ManifestMembers01Received | ManifestMembers23Received;

    private struct QueueMemberNetwork : INetworkStruct
    {
        public NetworkString<_32> ProfileId;
        public PlayerRef Player;
        public NetworkBool IsReady;

        public bool IsOccupied => !string.IsNullOrEmpty(ProfileId.ToString());
    }

    private struct ManifestDeliveryBuffer
    {
        public int Sequence;
        public byte ReceivedMask;
        public string RaidId;
        public string SessionName;
        public string Secret;
        public string HostProfileId;
        public string Profile0;
        public string Profile1;
        public string Profile2;
        public string Profile3;
    }

    [SerializeField, Range(1, RaidLaunchManifest.MaximumMembers)]
    private int _maximumMembers = RaidLaunchManifest.MaximumMembers;

    [SerializeField, Min(1f)]
    private float _coordinatedReleaseTimeoutSeconds = 15f;

    [Networked]
    public TownRaidQueueState State { get; private set; }

    [Networked]
    public NetworkString<_32> HostProfileId { get; private set; }

    [Networked]
    public NetworkString<_8> RaidCodeValue { get; private set; }

    [Networked]
    public int LaunchSequence { get; private set; }

    [Networked]
    public int SnapshotRevision { get; private set; }

    [Networked]
    private QueueMemberNetwork Member0 { get; set; }

    [Networked]
    private QueueMemberNetwork Member1 { get; set; }

    [Networked]
    private QueueMemberNetwork Member2 { get; set; }

    [Networked]
    private QueueMemberNetwork Member3 { get; set; }

    private readonly HashSet<string> _launchAcknowledgements = new();
    private readonly HashSet<string> _expectedRemoteProfiles = new();
    private readonly HashSet<string> _releasedRemoteProfiles = new();
    private readonly HashSet<string> _departedRemoteProfiles = new();
    private RaidLaunchManifest _pendingManifest;
    private ManifestDeliveryBuffer _manifestDelivery;
    private int _deliveredManifestSequence;
    private bool _queueInteractionRequested;
    private bool _cancelLaunchRequested;
    private bool _releaseDispatched;
    private bool _hostReleaseRequested;
    private float _releaseDeadline;

    public event Action QueueInteractionRequested;

    public TownRaidQueueSnapshot Snapshot => BuildSnapshot();

    /// <summary>Sends a local request to create the authoritative cohort.</summary>
    /// <returns>Whether Fusion accepted local invocation or transport.</returns>
    public bool RequestCreate() => CanSendRequest && TrySend(RPC_RequestCreate());

    /// <summary>Sends a local request to join the current cohort.</summary>
    /// <returns>Whether Fusion accepted local invocation or transport.</returns>
    public bool RequestJoin(string code) => CanSendRequest && RaidCode.TryParse(code, out _) && TrySend(RPC_RequestJoin(code));

    /// <summary>Sends a local request to leave the current cohort.</summary>
    /// <returns>Whether Fusion accepted local invocation or transport.</returns>
    public bool RequestLeave() => CanSendRequest && TrySend(RPC_RequestLeave());

    /// <summary>Sends a local request to update the member's Ready state.</summary>
    /// <returns>Whether Fusion accepted local invocation or transport.</returns>
    public bool RequestSetReady(bool isReady) => CanSendRequest && TrySend(RPC_RequestSetReady(isReady));

    /// <summary>Sends the Host's local request to freeze and launch the cohort.</summary>
    /// <returns>Whether Fusion accepted local invocation or transport.</returns>
    public bool RequestLaunch() => CanSendRequest && TrySend(RPC_RequestLaunch());

    public void NotifyLocalQueueRequested()
    {
        _queueInteractionRequested = true;
    }

    public override void Spawned()
    {
        ResetManifestDelivery();
        if (HasStateAuthority && State == TownRaidQueueState.Empty)
        {
            ResetQueue();
        }
    }

    public override void Render()
    {
        if (!_queueInteractionRequested)
        {
            return;
        }

        _queueInteractionRequested = false;
        QueueInteractionRequested?.Invoke();
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority)
        {
            return;
        }

        if (State == TownRaidQueueState.Launching && _releaseDispatched && !_hostReleaseRequested &&
            _releaseDeadline > 0f && Time.time >= _releaseDeadline)
        {
            LogTransition("Coordinated release timeout; cancelling launch");
            _cancelLaunchRequested = true;
            _releaseDeadline = 0f;
        }

        if (!_cancelLaunchRequested)
        {
            return;
        }

        _cancelLaunchRequested = false;
        if (State == TownRaidQueueState.Launching)
        {
            CancelLaunchingCohort();
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        _queueInteractionRequested = false;
        _cancelLaunchRequested = false;
        ResetManifestDelivery();
    }

    /// <summary>
    /// Removes disconnected members under the current Master Client authority.
    /// A Host departure or any departure during launch cancels the entire cohort.
    /// </summary>
    public void PlayerLeft(PlayerRef player)
    {
        if (!HasStateAuthority)
        {
            return;
        }

        int memberIndex = FindMember(player);
        if (memberIndex < 0)
        {
            return;
        }

        QueueMemberNetwork member = GetMember(memberIndex);
        LogTransition($"PlayerLeft observed profile={member.ProfileId} player={player}");
        bool isHost = string.Equals(
            member.ProfileId.ToString(),
            HostProfileId.ToString(),
            StringComparison.Ordinal);
        if (TownRaidQueueRules.ShouldDissolveAfterDeparture(State, isHost))
        {
            if (State == TownRaidQueueState.Launching)
            {
                if (_hostReleaseRequested && isHost)
                {
                    return;
                }

                _cancelLaunchRequested = true;
            }
            else
            {
                ResetQueue();
            }

            return;
        }

        if (State == TownRaidQueueState.Launching)
        {
            string departingProfile = member.ProfileId.ToString();
            if (_expectedRemoteProfiles.Contains(departingProfile))
            {
                if (_releasedRemoteProfiles.Contains(departingProfile))
                {
                    _departedRemoteProfiles.Add(departingProfile);
                    TryReleaseHostAfterRemoteDepartures();
                }
                else
                {
                    _cancelLaunchRequested = true;
                }
            }

            return;
        }

        SetMember(memberIndex, default);
        SnapshotRevision++;
    }

    /// <summary>
    /// Recovers a MasterClientObject whose non-networked launch envelope could not transfer.
    /// Forming state remains usable; an in-flight launch is explicitly cancelled.
    /// </summary>
    public void StateAuthorityChanged()
    {
        LogTransition("StateAuthorityChanged");
        if (HasStateAuthority && State == TownRaidQueueState.Launching && !_hostReleaseRequested &&
            TownRaidQueueRules.ShouldCancelAfterAuthorityTransfer(State))
        {
            _cancelLaunchRequested = true;
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private RpcInvokeInfo RPC_RequestCreate(RpcInfo info = default)
    {
        if (State != TownRaidQueueState.Empty || !TryResolveSender(info.Source, out ProfileId profileId))
        {
            return default;
        }

        State = TownRaidQueueState.Forming;
        HostProfileId = profileId.Value;
        RaidCodeValue = GenerateRaidCode();
        SetMember(0, new QueueMemberNetwork { ProfileId = profileId.Value, Player = info.Source, IsReady = false });
        SnapshotRevision++;
        return default;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private RpcInvokeInfo RPC_RequestJoin(NetworkString<_8> requestedCode, RpcInfo info = default)
    {
        if (!string.Equals(requestedCode.ToString(), RaidCodeValue.ToString(), StringComparison.Ordinal) ||
            !TryResolveSender(info.Source, out ProfileId profileId) ||
            !TownRaidQueueRules.CanJoin(State, MemberCount, _maximumMembers, FindMember(profileId) >= 0))
        {
            return default;
        }

        int emptySlot = FindEmptySlot();
        if (emptySlot >= 0)
        {
            SetMember(emptySlot, new QueueMemberNetwork { ProfileId = profileId.Value, Player = info.Source, IsReady = false });
            SnapshotRevision++;
        }

        return default;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private RpcInvokeInfo RPC_RequestLeave(RpcInfo info = default)
    {
        if (State != TownRaidQueueState.Forming || !TryResolveSender(info.Source, out ProfileId profileId))
        {
            return default;
        }

        if (string.Equals(HostProfileId.ToString(), profileId.Value, StringComparison.Ordinal))
        {
            ResetQueue();
            return default;
        }

        int index = FindMember(profileId);
        if (index >= 0)
        {
            SetMember(index, default);
            SnapshotRevision++;
        }

        return default;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private RpcInvokeInfo RPC_RequestSetReady(NetworkBool isReady, RpcInfo info = default)
    {
        if (State != TownRaidQueueState.Forming || !TryResolveSender(info.Source, out ProfileId profileId))
        {
            return default;
        }

        int index = FindMember(profileId);
        if (index < 0)
        {
            return default;
        }

        QueueMemberNetwork member = GetMember(index);
        member.IsReady = isReady;
        SetMember(index, member);
        SnapshotRevision++;
        return default;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private RpcInvokeInfo RPC_RequestLaunch(RpcInfo info = default)
    {
        if (!TryResolveSender(info.Source, out ProfileId profileId) ||
            !TownRaidQueueRules.CanLaunch(
                State,
                string.Equals(profileId.Value, HostProfileId.ToString(), StringComparison.Ordinal),
                MemberCount,
                AreAllMembersReady()))
        {
            return default;
        }

        LaunchSequence++;
        _pendingManifest = CreateManifest(LaunchSequence);
        if (!_pendingManifest.IsValid)
        {
            return default;
        }

        State = TownRaidQueueState.Launching;
        _launchAcknowledgements.Clear();
        SnapshotRevision++;
        LogTransition($"RequestLaunch accepted sequence={LaunchSequence} members={MemberCount}");
        DeliverManifestToMembers(_pendingManifest);
        return default;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_AcknowledgeLaunch(int sequence, RpcInfo info = default)
    {
        if (State != TownRaidQueueState.Launching || !_pendingManifest.IsValid ||
            sequence != _pendingManifest.LaunchSequence || !TryResolveSender(info.Source, out ProfileId profileId) ||
            !_pendingManifest.Contains(profileId))
        {
            return;
        }

        _launchAcknowledgements.Add(profileId.Value);
        LogTransition($"Launch ACK received profile={profileId.Value} acks={_launchAcknowledgements.Count}/{_pendingManifest.AdmittedProfiles.Count}");
        if (_launchAcknowledgements.Count != _pendingManifest.AdmittedProfiles.Count)
        {
            return;
        }

        PrepareCoordinatedRelease(_pendingManifest);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All, Channel = RpcChannel.Reliable)]
    private void RPC_DeliverManifestIdentity(
        [RpcTarget] PlayerRef target,
        NetworkString<_32> raidId,
        NetworkString<_64> sessionName,
        int sequence)
    {
        if (!TryBeginManifestFragment(sequence))
        {
            return;
        }

        _manifestDelivery.RaidId = raidId.ToString();
        _manifestDelivery.SessionName = sessionName.ToString();
        _manifestDelivery.ReceivedMask |= ManifestIdentityReceived;
        TryCompleteManifestDelivery();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All, Channel = RpcChannel.Reliable)]
    private void RPC_DeliverManifestCredential(
        [RpcTarget] PlayerRef target,
        NetworkString<_32> secret,
        NetworkString<_32> hostProfileId,
        int sequence)
    {
        if (!TryBeginManifestFragment(sequence))
        {
            return;
        }

        _manifestDelivery.Secret = secret.ToString();
        _manifestDelivery.HostProfileId = hostProfileId.ToString();
        _manifestDelivery.ReceivedMask |= ManifestCredentialReceived;
        TryCompleteManifestDelivery();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All, Channel = RpcChannel.Reliable)]
    private void RPC_DeliverManifestMembers01(
        [RpcTarget] PlayerRef target,
        NetworkString<_32> profile0,
        NetworkString<_32> profile1,
        int sequence)
    {
        if (!TryBeginManifestFragment(sequence))
        {
            return;
        }

        _manifestDelivery.Profile0 = profile0.ToString();
        _manifestDelivery.Profile1 = profile1.ToString();
        _manifestDelivery.ReceivedMask |= ManifestMembers01Received;
        TryCompleteManifestDelivery();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All, Channel = RpcChannel.Reliable)]
    private void RPC_DeliverManifestMembers23(
        [RpcTarget] PlayerRef target,
        NetworkString<_32> profile2,
        NetworkString<_32> profile3,
        int sequence)
    {
        if (!TryBeginManifestFragment(sequence))
        {
            return;
        }

        _manifestDelivery.Profile2 = profile2.ToString();
        _manifestDelivery.Profile3 = profile3.ToString();
        _manifestDelivery.ReceivedMask |= ManifestMembers23Received;
        TryCompleteManifestDelivery();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ReleaseLaunch([RpcTarget] PlayerRef target, int sequence)
    {
        LogTransition($"Release received target={target} sequence={sequence}");
        SessionConnectionCoordinator.Instance?.BeginAcknowledgedRaidLaunch(sequence);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_CancelLaunch([RpcTarget] PlayerRef target, int sequence)
    {
        SessionConnectionCoordinator.Instance?.CancelPendingRaidLaunch(sequence);
    }

    private void DeliverManifestToMembers(in RaidLaunchManifest manifest)
    {
        for (int index = 0; index < RaidLaunchManifest.MaximumMembers; index++)
        {
            QueueMemberNetwork member = GetMember(index);
            if (!member.IsOccupied)
            {
                continue;
            }

            RPC_DeliverManifestIdentity(
                member.Player, manifest.RaidId, manifest.SessionName, manifest.LaunchSequence);
            RPC_DeliverManifestCredential(
                member.Player, manifest.AccessSecret, manifest.HostProfileId.Value, manifest.LaunchSequence);
            RPC_DeliverManifestMembers01(
                member.Player, GetProfile(0), GetProfile(1), manifest.LaunchSequence);
            RPC_DeliverManifestMembers23(
                member.Player, GetProfile(2), GetProfile(3), manifest.LaunchSequence);
        }
    }

    private bool TryBeginManifestFragment(int sequence)
    {
        if (sequence <= 0 || sequence < _manifestDelivery.Sequence || sequence < _deliveredManifestSequence)
        {
            return false;
        }

        if (sequence == _deliveredManifestSequence)
        {
            RPC_AcknowledgeLaunch(sequence);
            return false;
        }

        if (sequence > _manifestDelivery.Sequence)
        {
            _manifestDelivery = new ManifestDeliveryBuffer { Sequence = sequence };
        }

        return true;
    }

    private void TryCompleteManifestDelivery()
    {
        if (_manifestDelivery.ReceivedMask != CompleteManifestMask)
        {
            return;
        }

        RaidLaunchManifest manifest = CreateManifestFromRpc(
            _manifestDelivery.RaidId,
            _manifestDelivery.SessionName,
            _manifestDelivery.Secret,
            _manifestDelivery.HostProfileId,
            _manifestDelivery.Profile0,
            _manifestDelivery.Profile1,
            _manifestDelivery.Profile2,
            _manifestDelivery.Profile3,
            _manifestDelivery.Sequence);
        if (!manifest.IsValid || !TryGetLocalProfile(out ProfileId localProfile) || !manifest.Contains(localProfile))
        {
            return;
        }

        SessionConnectionCoordinator coordinator = SessionConnectionCoordinator.Instance;
        if (coordinator == null || !coordinator.ReceiveRaidLaunchManifest(manifest))
        {
            return;
        }

        _deliveredManifestSequence = manifest.LaunchSequence;
        _manifestDelivery = default;
        RPC_AcknowledgeLaunch(manifest.LaunchSequence);
    }

    private bool TryGetLocalProfile(out ProfileId profileId)
    {
        profileId = default;
        LocalPlayerJoinContext context = Runner != null ? Runner.GetComponent<LocalPlayerJoinContext>() : null;
        if (context == null || !context.JoinData.ProfileId.IsValid)
        {
            return false;
        }

        profileId = context.JoinData.ProfileId;
        return true;
    }

    private void ResetManifestDelivery()
    {
        _manifestDelivery = default;
        _deliveredManifestSequence = 0;
    }

    private void PrepareCoordinatedRelease(in RaidLaunchManifest manifest)
    {
        _expectedRemoteProfiles.Clear();
        _releasedRemoteProfiles.Clear();
        _departedRemoteProfiles.Clear();
        _releaseDispatched = true;
        _releaseDeadline = Time.time + Mathf.Max(1f, _coordinatedReleaseTimeoutSeconds);
        LogTransition($"All context ACKs complete; releasing remotes expected={manifest.AdmittedProfiles.Count - 1}");

        for (int index = 0; index < RaidLaunchManifest.MaximumMembers; index++)
        {
            QueueMemberNetwork member = GetMember(index);
            if (!member.IsOccupied)
            {
                continue;
            }

            if (string.Equals(member.ProfileId.ToString(), manifest.HostProfileId.Value, StringComparison.Ordinal))
            {
                continue;
            }

            _expectedRemoteProfiles.Add(member.ProfileId.ToString());
            _releasedRemoteProfiles.Add(member.ProfileId.ToString());
            LogTransition($"Release sent profile={member.ProfileId} player={member.Player}");
            RPC_ReleaseLaunch(member.Player, manifest.LaunchSequence);
        }

        TryReleaseHostAfterRemoteDepartures();
    }

    private void TryReleaseHostAfterRemoteDepartures()
    {
        if (!_releaseDispatched || _hostReleaseRequested ||
            _departedRemoteProfiles.Count < _expectedRemoteProfiles.Count)
        {
            return;
        }

        _hostReleaseRequested = true;
        _releaseDeadline = 0f;
        LogTransition("All remote members departed; releasing Host");
        SessionConnectionCoordinator.Instance?.BeginAcknowledgedRaidLaunch(_pendingManifest.LaunchSequence);
    }

    private void LogTransition(string message)
    {
        string localProfile = TryGetLocalProfile(out ProfileId profile) ? profile.Value : "<unknown>";
        Debug.Log(
            $"[TOWN-RAID-TRANSITION] {message} localProfile={localProfile} " +
            $"player={Runner?.LocalPlayer} code={RaidCodeValue} sequence={LaunchSequence} " +
            $"state={State} authority={HasStateAuthority}",
            this);
    }

    private RaidLaunchManifest CreateManifest(int sequence)
    {
        if (!RaidCode.TryParse(RaidCodeValue.ToString(), out RaidCode raidCode))
        {
            return default;
        }

        var profiles = new List<ProfileId>(MemberCount);
        for (int index = 0; index < RaidLaunchManifest.MaximumMembers; index++)
        {
            QueueMemberNetwork member = GetMember(index);
            if (member.IsOccupied)
            {
                profiles.Add(new ProfileId(member.ProfileId.ToString()));
            }
        }

        return new RaidLaunchManifest(
            raidCode.RaidId,
            raidCode.SessionName,
            raidCode.Value,
            new ProfileId(HostProfileId.ToString()),
            profiles,
            sequence);
    }

    private static RaidLaunchManifest CreateManifestFromRpc(
        string raidId, string sessionName, string secret, string hostProfileId,
        string profile0, string profile1, string profile2, string profile3, int sequence)
    {
        var profiles = new List<ProfileId>(RaidLaunchManifest.MaximumMembers);
        AddProfile(profile0, profiles);
        AddProfile(profile1, profiles);
        AddProfile(profile2, profiles);
        AddProfile(profile3, profiles);
        return new RaidLaunchManifest(raidId, sessionName, secret, new ProfileId(hostProfileId), profiles, sequence);
    }

    private static void AddProfile(string value, List<ProfileId> profiles)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            profiles.Add(new ProfileId(value));
        }
    }

    private bool TryResolveSender(PlayerRef player, out ProfileId profileId)
    {
        profileId = default;
        if (player.IsNone || Runner.GetPlayerObject(player) == null ||
            !Runner.GetPlayerObject(player).TryGetBehaviour(out SocialPlayerIdentity identity) ||
            identity == null || identity.Object.InputAuthority != player || string.IsNullOrWhiteSpace(identity.ProfileId.ToString()))
        {
            return false;
        }

        profileId = new ProfileId(identity.ProfileId.ToString());
        return true;
    }

    private TownRaidQueueSnapshot BuildSnapshot()
    {
        var members = new List<TownRaidQueueMember>(MemberCount);
        for (int index = 0; index < RaidLaunchManifest.MaximumMembers; index++)
        {
            QueueMemberNetwork member = GetMember(index);
            if (member.IsOccupied)
            {
                members.Add(new TownRaidQueueMember(new ProfileId(member.ProfileId.ToString()), member.Player, member.IsReady));
            }
        }

        RaidCode.TryParse(RaidCodeValue.ToString(), out RaidCode raidCode);
        return new TownRaidQueueSnapshot(State, new ProfileId(HostProfileId.ToString()), raidCode, LaunchSequence, members);
    }

    private int MemberCount
    {
        get
        {
            int count = 0;
            for (int index = 0; index < RaidLaunchManifest.MaximumMembers; index++)
            {
                if (GetMember(index).IsOccupied)
                {
                    count++;
                }
            }

            return count;
        }
    }

    private bool AreAllMembersReady()
    {
        if (MemberCount == 0)
        {
            return false;
        }

        for (int index = 0; index < RaidLaunchManifest.MaximumMembers; index++)
        {
            QueueMemberNetwork member = GetMember(index);
            if (member.IsOccupied && !member.IsReady)
            {
                return false;
            }
        }

        return true;
    }

    private int FindMember(ProfileId profileId)
    {
        for (int index = 0; index < RaidLaunchManifest.MaximumMembers; index++)
        {
            if (string.Equals(GetMember(index).ProfileId.ToString(), profileId.Value, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private int FindMember(PlayerRef player)
    {
        for (int index = 0; index < RaidLaunchManifest.MaximumMembers; index++)
        {
            QueueMemberNetwork member = GetMember(index);
            if (member.IsOccupied && member.Player == player)
            {
                return index;
            }
        }

        return -1;
    }

    private int FindEmptySlot()
    {
        for (int index = 0; index < RaidLaunchManifest.MaximumMembers; index++)
        {
            if (!GetMember(index).IsOccupied)
            {
                return index;
            }
        }

        return -1;
    }

    private string GetProfile(int index)
    {
        return GetMember(index).ProfileId.ToString();
    }

    private QueueMemberNetwork GetMember(int index)
    {
        return index switch
        {
            0 => Member0,
            1 => Member1,
            2 => Member2,
            3 => Member3,
            _ => default
        };
    }

    private void SetMember(int index, QueueMemberNetwork member)
    {
        switch (index)
        {
            case 0: Member0 = member; break;
            case 1: Member1 = member; break;
            case 2: Member2 = member; break;
            case 3: Member3 = member; break;
        }
    }

    private void ResetQueue()
    {
        State = TownRaidQueueState.Empty;
        HostProfileId = default;
        RaidCodeValue = default;
        Member0 = default;
        Member1 = default;
        Member2 = default;
        Member3 = default;
        _pendingManifest = default;
        _launchAcknowledgements.Clear();
        _expectedRemoteProfiles.Clear();
        _releasedRemoteProfiles.Clear();
        _departedRemoteProfiles.Clear();
        _releaseDispatched = false;
        _hostReleaseRequested = false;
        _releaseDeadline = 0f;
        SnapshotRevision++;
    }

    private static NetworkString<_8> GenerateRaidCode()
    {
        return UnityEngine.Random.Range(0, 1_000_000).ToString("D6");
    }

    private void CancelLaunchingCohort()
    {
        int sequence = LaunchSequence;
        if (State == TownRaidQueueState.Launching && sequence > 0)
        {
            for (int index = 0; index < RaidLaunchManifest.MaximumMembers; index++)
            {
                QueueMemberNetwork member = GetMember(index);
                if (member.IsOccupied && !member.Player.IsNone)
                {
                    RPC_CancelLaunch(member.Player, sequence);
                }
            }
        }

        ResetQueue();
    }

    private bool TrySend(in RpcInvokeInfo invokeInfo)
    {
        return Object != null && Object.IsValid && Runner != null &&
            (invokeInfo.SendMessageResult == RpcSendMessageResult.Sent ||
             invokeInfo.LocalInvokeResult == RpcLocalInvokeResult.Invoked);
    }

    private bool CanSendRequest => Object != null && Object.IsValid && Runner != null;
}
