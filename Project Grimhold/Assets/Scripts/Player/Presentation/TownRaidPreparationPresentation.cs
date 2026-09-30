using System;
using System.Collections.Generic;

/// <summary>Pure local presentation projection for one player's current Town preparation.</summary>
public readonly struct TownRaidPreparationPresentation
{
    public readonly struct Member
    {
        public string DisplayName { get; }
        public bool IsHost { get; }
        public bool IsReady { get; }

        public Member(string displayName, bool isHost, bool isReady)
        {
            DisplayName = displayName;
            IsHost = isHost;
            IsReady = isReady;
        }
    }

    public delegate bool DisplayNameResolver(ProfileId profileId, out string displayName);

    public TownRaidPreparationSnapshot Snapshot { get; }
    public IReadOnlyList<Member> Members { get; }
    public bool IsHost { get; }
    public bool LocalReady { get; }
    public bool CanStart { get; }

    private TownRaidPreparationPresentation(
        in TownRaidPreparationSnapshot snapshot,
        IReadOnlyList<Member> members,
        bool isHost,
        bool localReady,
        bool canStart)
    {
        Snapshot = snapshot;
        Members = members;
        IsHost = isHost;
        LocalReady = localReady;
        CanStart = canStart;
    }

    public static bool TryCreate(
        in TownRaidPreparationSnapshot snapshot,
        ProfileId localProfileId,
        out TownRaidPreparationPresentation presentation)
    {
        return TryCreate(snapshot, localProfileId, null, out presentation);
    }

    public static bool TryCreate(
        in TownRaidPreparationSnapshot snapshot,
        ProfileId localProfileId,
        DisplayNameResolver displayNameResolver,
        out TownRaidPreparationPresentation presentation)
    {
        presentation = default;
        if (!localProfileId.IsValid || !TownRaidPreparationRules.IsValidSnapshot(snapshot))
        {
            return false;
        }

        IReadOnlyList<TownRaidPreparationMember> members = snapshot.Members;
        var presentedMembers = new Member[members.Count];
        bool found = false;
        bool localReady = false;
        for (int index = 0; index < members.Count; index++)
        {
            TownRaidPreparationMember member = members[index];
            string displayName = member.ProfileId.Value;
            if (displayNameResolver != null &&
                displayNameResolver(member.ProfileId, out string resolvedName) &&
                !string.IsNullOrWhiteSpace(resolvedName))
            {
                displayName = resolvedName;
            }

            presentedMembers[index] = new Member(
                displayName,
                member.ProfileId == snapshot.HostProfileId,
                member.IsReady);

            if (member.ProfileId != localProfileId)
            {
                continue;
            }

            found = true;
            localReady = member.IsReady;
        }

        if (!found)
        {
            return false;
        }

        bool isHost = snapshot.HostProfileId == localProfileId;
        presentation = new TownRaidPreparationPresentation(
            snapshot,
            Array.AsReadOnly(presentedMembers),
            isHost,
            localReady,
            TownRaidPreparationRules.CanStart(snapshot, localProfileId));
        return true;
    }
}
