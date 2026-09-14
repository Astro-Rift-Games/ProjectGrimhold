using System;
using NUnit.Framework;

public sealed class TownPartyInvitationContractTests
{
    [Test]
    public void PendingEntry_UsesOneCanonicalPreparationIdentity()
    {
        Type entryType = typeof(TownPartyInvitationNetworkEntry);

        Assert.That(entryType.GetField(nameof(TownPartyInvitationNetworkEntry.PreparationNetworkId)), Is.Not.Null);
        Assert.That(entryType.GetField(nameof(TownPartyInvitationNetworkEntry.MembershipRevision)), Is.Not.Null);
        Assert.That(entryType.GetField("RaidCode"), Is.Null);
        Assert.That(entryType.GetField("SnapshotRevision"), Is.Null);
    }

    [Test]
    public void PublicResults_AreTheApprovedTypedContract()
    {
        CollectionAssert.AreEquivalent(
            new[]
            {
                nameof(TownPartyInvitationResult.Accepted),
                nameof(TownPartyInvitationResult.Rejected),
                nameof(TownPartyInvitationResult.Expired),
                nameof(TownPartyInvitationResult.AlreadyGrouped),
                nameof(TownPartyInvitationResult.PartyFull),
                nameof(TownPartyInvitationResult.Busy),
                nameof(TownPartyInvitationResult.Cooldown),
                nameof(TownPartyInvitationResult.Unavailable)
            },
            Enum.GetNames(typeof(TownPartyInvitationResult)));
    }
}
