using NUnit.Framework;
using Assert = NUnit.Framework.Assert;

public sealed class TownPartyRulesTests
{
    [Test]
    public void SoloAndDuo_AreValidAndRosterIsCappedAtTwo()
    {
        var host = new ProfileId("host");
        var client = new ProfileId("client");
        Assert.That(TownPartyRules.IsValid(new TownPartySnapshot(1, host, new[] { host }, 1)), Is.True);
        Assert.That(TownPartyRules.IsValid(new TownPartySnapshot(1, host, new[] { host, client }, 2)), Is.True);
        Assert.That(TownPartyRules.IsValid(new TownPartySnapshot(1, host, new[] { host, client, new ProfileId("third") }, 3)), Is.False);
        Assert.That(TownPartyRules.MaxMembers, Is.EqualTo(2));
    }

    [Test]
    public void Merge_AcceptsOnlyTwoDistinctSoloParties()
    {
        var inviter = new ProfileId("inviter");
        var recipient = new ProfileId("recipient");
        var inviterParty = new TownPartySnapshot(10, inviter, new[] { inviter }, 1);
        var recipientParty = new TownPartySnapshot(11, recipient, new[] { recipient }, 1);
        var duo = new TownPartySnapshot(12, inviter, new[] { inviter, recipient }, 2);
        Assert.That(TownPartyRules.CanMerge(inviterParty, recipientParty), Is.True);
        Assert.That(TownPartyRules.CanMerge(duo, recipientParty), Is.False);
    }

    [Test]
    public void RaidCreation_CopiesPartyWithCreatorFirstAndDoesNotReusePartyHost()
    {
        var partyHost = new ProfileId("party-host");
        var creator = new ProfileId("creator");
        var party = new TownPartySnapshot(10, partyHost, new[] { partyHost, creator }, 4);
        Assert.That(TownPartyRules.TryCreateInitialRaidRoster(party, creator, out var roster), Is.True);
        Assert.That(roster, Is.EqualTo(new[] { creator, partyHost }));
    }
}
