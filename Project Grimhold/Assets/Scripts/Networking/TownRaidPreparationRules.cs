using System.Collections.Generic;

/// <summary>
/// Pure deterministic rules for one Town Raid preparation and its frozen cohort.
/// </summary>
public static class TownRaidPreparationRules
{
    /// <summary>Validates one complete preparation observation.</summary>
    public static bool IsValidSnapshot(in TownRaidPreparationSnapshot snapshot)
    {
        if (!snapshot.RaidCode.IsValid || snapshot.SnapshotRevision <= 0 ||
            !IsValidMembership(snapshot.HostProfileId, snapshot.Members))
        {
            return false;
        }

        switch (snapshot.State)
        {
            case TownRaidPreparationState.Waiting:
                return snapshot.LaunchRevision >= 0 && snapshot.FrozenMemberCount == 0 &&
                       MembersHaveLaunchRevision(snapshot.Members, 0);
            case TownRaidPreparationState.Starting:
                return IsCompleteFrozenSnapshot(snapshot);
            default:
                return false;
        }
    }

    /// <summary>Validates capacity, stable identities, uniqueness and Host membership.</summary>
    public static bool IsValidMembership(
        ProfileId hostProfileId,
        IReadOnlyList<TownRaidPreparationMember> members)
    {
        if (!hostProfileId.IsValid || members == null ||
            members.Count < 1 || members.Count > RaidSessionRules.MaxParticipants)
        {
            return false;
        }

        bool containsHost = false;
        for (int index = 0; index < members.Count; index++)
        {
            TownRaidPreparationMember member = members[index];
            if (!member.IsValid)
            {
                return false;
            }

            if (member.ProfileId == hostProfileId)
            {
                containsHost = true;
            }

            for (int other = index + 1; other < members.Count; other++)
            {
                if (member.ProfileId == members[other].ProfileId)
                {
                    return false;
                }
            }
        }

        return containsHost;
    }

    /// <summary>Returns whether a new profile may join this preparation.</summary>
    public static bool CanJoin(in TownRaidPreparationSnapshot snapshot, ProfileId profileId)
    {
        return snapshot.State == TownRaidPreparationState.Waiting &&
               IsValidSnapshot(snapshot) && profileId.IsValid &&
               snapshot.Members.Count < RaidSessionRules.MaxParticipants &&
               FindMember(snapshot.Members, profileId) < 0;
    }

    /// <summary>Returns whether a member may voluntarily leave before Start.</summary>
    public static bool CanLeave(in TownRaidPreparationSnapshot snapshot, ProfileId profileId)
    {
        return snapshot.State == TownRaidPreparationState.Waiting &&
               IsValidSnapshot(snapshot) && FindMember(snapshot.Members, profileId) >= 0;
    }

    /// <summary>Returns whether a member may mutate Ready before Start.</summary>
    public static bool CanSetReady(in TownRaidPreparationSnapshot snapshot, ProfileId profileId)
    {
        return snapshot.State == TownRaidPreparationState.Waiting &&
               IsValidSnapshot(snapshot) && FindMember(snapshot.Members, profileId) >= 0;
    }

    /// <summary>Derives readiness from every member; no redundant aggregate state is stored.</summary>
    public static bool AreAllMembersReady(IReadOnlyList<TownRaidPreparationMember> members)
    {
        if (members == null || members.Count == 0)
        {
            return false;
        }

        for (int index = 0; index < members.Count; index++)
        {
            if (!members[index].IsReady)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Requires the Host requester and a complete Ready cohort in Waiting.</summary>
    public static bool CanStart(
        in TownRaidPreparationSnapshot snapshot,
        ProfileId requesterProfileId)
    {
        return snapshot.State == TownRaidPreparationState.Waiting &&
               requesterProfileId == snapshot.HostProfileId &&
               IsValidSnapshot(snapshot) && AreAllMembersReady(snapshot.Members);
    }

    /// <summary>Adds one NotReady member while preserving the authoritative roster order.</summary>
    public static bool TryAddMember(
        in TownRaidPreparationSnapshot snapshot,
        ProfileId profileId,
        out TownRaidPreparationSnapshot updated)
    {
        if (!CanJoin(snapshot, profileId) || snapshot.SnapshotRevision == int.MaxValue)
        {
            updated = default;
            return false;
        }

        var members = new TownRaidPreparationMember[snapshot.Members.Count + 1];
        CopyMembers(snapshot.Members, members);
        members[members.Length - 1] = new TownRaidPreparationMember(profileId);
        updated = new TownRaidPreparationSnapshot(
            snapshot.RaidCode,
            snapshot.HostProfileId,
            snapshot.State,
            members,
            snapshot.SnapshotRevision + 1,
            snapshot.LaunchRevision);
        return true;
    }

    /// <summary>Removes one non-Host member while preserving relative roster order.</summary>
    public static bool TryRemoveMember(
        in TownRaidPreparationSnapshot snapshot,
        ProfileId profileId,
        out TownRaidPreparationSnapshot updated)
    {
        int memberIndex = FindMember(snapshot.Members, profileId);
        if (!CanLeave(snapshot, profileId) || profileId == snapshot.HostProfileId || memberIndex < 0 ||
            snapshot.SnapshotRevision == int.MaxValue)
        {
            updated = default;
            return false;
        }

        var members = new TownRaidPreparationMember[snapshot.Members.Count - 1];
        int destination = 0;
        for (int source = 0; source < snapshot.Members.Count; source++)
        {
            if (source != memberIndex)
            {
                members[destination++] = snapshot.Members[source];
            }
        }

        updated = new TownRaidPreparationSnapshot(
            snapshot.RaidCode,
            snapshot.HostProfileId,
            snapshot.State,
            members,
            snapshot.SnapshotRevision + 1,
            snapshot.LaunchRevision);
        return true;
    }

    /// <summary>Returns a new snapshot with one member's Ready value updated.</summary>
    public static bool TrySetReady(
        in TownRaidPreparationSnapshot snapshot,
        ProfileId profileId,
        bool isReady,
        out TownRaidPreparationSnapshot updated)
    {
        int memberIndex = FindMember(snapshot.Members, profileId);
        if (!CanSetReady(snapshot, profileId) || memberIndex < 0 ||
            snapshot.SnapshotRevision == int.MaxValue)
        {
            updated = default;
            return false;
        }

        var members = new TownRaidPreparationMember[snapshot.Members.Count];
        CopyMembers(snapshot.Members, members);
        members[memberIndex] = members[memberIndex].WithReady(isReady);
        updated = new TownRaidPreparationSnapshot(
            snapshot.RaidCode,
            snapshot.HostProfileId,
            snapshot.State,
            members,
            snapshot.SnapshotRevision + 1,
            snapshot.LaunchRevision);
        return true;
    }

    /// <summary>
    /// Freezes the current authoritative roster order under one new launch revision.
    /// </summary>
    public static bool TryFreeze(
        in TownRaidPreparationSnapshot snapshot,
        ProfileId requesterProfileId,
        int launchRevision,
        out TownRaidPreparationSnapshot frozen)
    {
        if (!CanStart(snapshot, requesterProfileId) ||
            !RaidSessionRules.IsValidLaunchRevision(launchRevision) ||
            launchRevision <= snapshot.LaunchRevision || snapshot.SnapshotRevision == int.MaxValue)
        {
            frozen = default;
            return false;
        }

        var members = new TownRaidPreparationMember[snapshot.Members.Count];
        for (int index = 0; index < snapshot.Members.Count; index++)
        {
            members[index] = snapshot.Members[index].WithLaunchRevision(launchRevision);
        }

        frozen = new TownRaidPreparationSnapshot(
            snapshot.RaidCode,
            snapshot.HostProfileId,
            TownRaidPreparationState.Starting,
            members,
            snapshot.SnapshotRevision + 1,
            launchRevision,
            members.Length);
        return true;
    }

    /// <summary>
    /// Validates that every expected frozen member belongs to the same launch revision.
    /// </summary>
    public static bool IsCompleteFrozenSnapshot(in TownRaidPreparationSnapshot snapshot)
    {
        return snapshot.RaidCode.IsValid && snapshot.SnapshotRevision > 0 &&
               snapshot.State == TownRaidPreparationState.Starting &&
               RaidSessionRules.IsValidLaunchRevision(snapshot.LaunchRevision) &&
               snapshot.FrozenMemberCount == snapshot.Members.Count &&
               snapshot.FrozenMemberCount >= 1 &&
               snapshot.FrozenMemberCount <= RaidSessionRules.MaxParticipants &&
               IsValidMembership(snapshot.HostProfileId, snapshot.Members) &&
               AreAllMembersReady(snapshot.Members) &&
               MembersHaveLaunchRevision(snapshot.Members, snapshot.LaunchRevision);
    }

    /// <summary>
    /// Materializes the runner-independent context only from a complete frozen snapshot.
    /// </summary>
    public static bool TryCreateLaunchContext(
        in TownRaidPreparationSnapshot snapshot,
        ProfileId localProfileId,
        out RaidLaunchContext launchContext)
    {
        if (!IsCompleteFrozenSnapshot(snapshot))
        {
            launchContext = null;
            return false;
        }

        var profiles = new ProfileId[snapshot.Members.Count];
        for (int index = 0; index < snapshot.Members.Count; index++)
        {
            profiles[index] = snapshot.Members[index].ProfileId;
        }

        return RaidLaunchContext.TryCreate(
            snapshot.RaidCode,
            snapshot.HostProfileId,
            profiles,
            localProfileId,
            snapshot.LaunchRevision,
            out launchContext);
    }

    /// <summary>
    /// Records an acknowledgement only for the matching frozen profile and revision.
    /// Duplicate acknowledgements are idempotent.
    /// </summary>
    public static bool TryAcknowledgeLaunch(
        in TownRaidPreparationSnapshot snapshot,
        ProfileId profileId,
        int launchRevision,
        out TownRaidPreparationSnapshot acknowledged)
    {
        int memberIndex = FindMember(snapshot.Members, profileId);
        if (!IsCompleteFrozenSnapshot(snapshot) || memberIndex < 0 ||
            launchRevision != snapshot.LaunchRevision)
        {
            acknowledged = default;
            return false;
        }

        if (snapshot.Members[memberIndex].LaunchAcknowledged)
        {
            acknowledged = snapshot;
            return true;
        }

        if (snapshot.SnapshotRevision == int.MaxValue)
        {
            acknowledged = default;
            return false;
        }

        var members = new TownRaidPreparationMember[snapshot.Members.Count];
        CopyMembers(snapshot.Members, members);
        members[memberIndex] = members[memberIndex].WithLaunchAcknowledged(true);
        acknowledged = new TownRaidPreparationSnapshot(
            snapshot.RaidCode,
            snapshot.HostProfileId,
            snapshot.State,
            members,
            snapshot.SnapshotRevision + 1,
            snapshot.LaunchRevision,
            snapshot.FrozenMemberCount);
        return true;
    }

    /// <summary>Returns true only after every member acknowledged this frozen revision.</summary>
    public static bool AreAllLaunchAcknowledged(in TownRaidPreparationSnapshot snapshot)
    {
        if (!IsCompleteFrozenSnapshot(snapshot))
        {
            return false;
        }

        for (int index = 0; index < snapshot.Members.Count; index++)
        {
            if (!snapshot.Members[index].LaunchAcknowledged)
            {
                return false;
            }
        }

        return true;
    }

    private static bool MembersHaveLaunchRevision(
        IReadOnlyList<TownRaidPreparationMember> members,
        int launchRevision)
    {
        for (int index = 0; index < members.Count; index++)
        {
            if (members[index].LaunchRevision != launchRevision)
            {
                return false;
            }
        }

        return true;
    }

    private static int FindMember(
        IReadOnlyList<TownRaidPreparationMember> members,
        ProfileId profileId)
    {
        if (!profileId.IsValid)
        {
            return -1;
        }

        for (int index = 0; index < members.Count; index++)
        {
            if (members[index].ProfileId == profileId)
            {
                return index;
            }
        }

        return -1;
    }

    private static void CopyMembers(
        IReadOnlyList<TownRaidPreparationMember> source,
        TownRaidPreparationMember[] destination)
    {
        for (int index = 0; index < source.Count; index++)
        {
            destination[index] = source[index];
        }
    }
}
