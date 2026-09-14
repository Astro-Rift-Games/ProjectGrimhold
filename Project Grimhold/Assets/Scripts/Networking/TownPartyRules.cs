using System.Collections.Generic;

public static class TownPartyRules
{
    public const int MaxMembers = 2;

    public static bool IsValid(in TownPartySnapshot snapshot)
    {
        if (snapshot.PartyId <= 0 || snapshot.Revision <= 0 || !snapshot.HostProfileId.IsValid ||
            snapshot.Members.Count < 1 || snapshot.Members.Count > MaxMembers)
        {
            return false;
        }

        bool containsHost = false;
        for (int index = 0; index < snapshot.Members.Count; index++)
        {
            ProfileId member = snapshot.Members[index];
            if (!member.IsValid) return false;
            containsHost |= member == snapshot.HostProfileId;
            for (int other = index + 1; other < snapshot.Members.Count; other++)
            {
                if (member == snapshot.Members[other]) return false;
            }
        }
        return containsHost;
    }

    public static bool IsSolo(in TownPartySnapshot snapshot) => IsValid(snapshot) && snapshot.Members.Count == 1;

    public static bool CanMerge(in TownPartySnapshot inviter, in TownPartySnapshot recipient) =>
        IsSolo(inviter) && IsSolo(recipient) && inviter.PartyId != recipient.PartyId &&
        inviter.Members[0] != recipient.Members[0];

    public static bool TryCreateInitialRaidRoster(
        in TownPartySnapshot party,
        ProfileId creator,
        out IReadOnlyList<ProfileId> roster)
    {
        roster = null;
        if (!IsValid(party) || !party.Contains(creator)) return false;

        var members = new ProfileId[party.Members.Count];
        members[0] = creator;
        int destination = 1;
        for (int index = 0; index < party.Members.Count; index++)
        {
            if (party.Members[index] != creator) members[destination++] = party.Members[index];
        }
        roster = members;
        return true;
    }
}
