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

    public static bool TryCreateRaidCreatorRoster(
        in TownPartySnapshot party,
        ProfileId creator,
        out IReadOnlyList<ProfileId> roster)
    {
        roster = null;
        if (!IsValid(party) || !party.Contains(creator)) return false;
        roster = new[] { creator };
        return true;
    }

    /// <summary>
    /// Returns whether a Town player still belongs to a Duo expedition that has not finished: the
    /// pending continuation is cleared only when the partner returns and the Party is restored, so an
    /// unresolved Duo continuation means the shared session's expedition is still active.
    /// </summary>
    public static bool IsAwaitingPartyCompletion(TownPartyContinuationContext pendingContinuation) =>
        pendingContinuation != null && pendingContinuation.Members.Count > 1;

    /// <summary>
    /// Returns whether a continuation can never be restored because another of its members already
    /// belongs to a Town Party (they left the Duo, for example through the main menu). Waiting on such a
    /// claim would keep the claimant awaiting a partner who is no longer in the expedition.
    /// </summary>
    public static bool IsContinuationSuperseded(
        TownPartyContinuationContext context,
        ProfileId claimant,
        System.Predicate<ProfileId> hasTownParty)
    {
        if (context == null || hasTownParty == null) return false;
        for (int index = 0; index < context.Members.Count; index++)
        {
            ProfileId member = context.Members[index];
            if (member != claimant && hasTownParty(member)) return true;
        }
        return false;
    }
}
