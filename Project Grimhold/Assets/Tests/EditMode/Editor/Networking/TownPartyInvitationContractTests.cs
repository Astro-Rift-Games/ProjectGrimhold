using System;
using NUnit.Framework;

public sealed class TownPartyInvitationContractTests
{
    [Test]
    public void PendingEntry_UsesOnlyPartyIdentityAndRevisions()
    {
        Type entryType = typeof(TownPartyInvitationNetworkEntry);

        Assert.That(entryType.GetField(nameof(TownPartyInvitationNetworkEntry.InviterPartyId)), Is.Not.Null);
        Assert.That(entryType.GetField(nameof(TownPartyInvitationNetworkEntry.InviterPartyRevision)), Is.Not.Null);
        Assert.That(entryType.GetField(nameof(TownPartyInvitationNetworkEntry.RecipientPartyId)), Is.Not.Null);
        Assert.That(entryType.GetField(nameof(TownPartyInvitationNetworkEntry.RecipientPartyRevision)), Is.Not.Null);
        Assert.That(entryType.GetField("PreparationNetworkId"), Is.Null);
        Assert.That(entryType.GetField("MembershipRevision"), Is.Null);
        Assert.That(entryType.GetField("RaidCode"), Is.Null);
        Assert.That(entryType.GetField("SnapshotRevision"), Is.Null);
        Assert.That(entryType.GetField("Ready"), Is.Null);
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
