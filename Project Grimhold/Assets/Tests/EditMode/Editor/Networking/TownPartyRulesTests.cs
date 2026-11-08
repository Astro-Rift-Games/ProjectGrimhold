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
    public void RaidCreation_AdmitsOnlyCreatorEvenWhenPartyIsDuo()
    {
        var partyHost = new ProfileId("party-host");
        var creator = new ProfileId("creator");
        var party = new TownPartySnapshot(10, partyHost, new[] { partyHost, creator }, 4);
        Assert.That(TownPartyRules.TryCreateRaidCreatorRoster(party, creator, out var roster), Is.True);
        Assert.That(roster, Is.EqualTo(new[] { creator }));
    }

    [Test]
    public void AwaitingPartyCompletion_HoldsWhileDuoPartnerRemainsInDungeonAndLiftsOnRestore()
    {
        var host = new ProfileId("host");
        var client = new ProfileId("client");
        Assert.That(TownPartyContinuationContext.TryCreate(host, new[] { host, client }, out var duo), Is.True);
        var claims = new TownPartyContinuationClaimRegistry();

        // The Client extracts first: it is back in Town while the Host still sustains the Dungeon.
        Assert.That(claims.Submit(client, duo), Is.EqualTo(TownPartyContinuationClaimResult.Pending));
        Assert.That(TownPartyRules.IsAwaitingPartyCompletion(duo), Is.True);

        // The Host finishes and returns: the Party restores and the coordinator clears the continuation.
        Assert.That(claims.Submit(host, duo), Is.EqualTo(TownPartyContinuationClaimResult.ReadyToRestore));
        Assert.That(TownPartyRules.IsAwaitingPartyCompletion(null), Is.False);
    }

    [Test]
    public void Continuation_IsSupersededOnlyWhenAnotherMemberAlreadyBelongsToAParty()
    {
        var host = new ProfileId("host");
        var client = new ProfileId("client");
        Assert.That(TownPartyContinuationContext.TryCreate(host, new[] { host, client }, out var duo), Is.True);

        // The partner left the Duo (main menu or relaunch) and already owns a fresh Solo Party.
        Assert.That(TownPartyRules.IsContinuationSuperseded(duo, host, profile => profile == client), Is.True);
        // The partner is still in the Dungeon or not yet grouped: the claim keeps waiting.
        Assert.That(TownPartyRules.IsContinuationSuperseded(duo, host, _ => false), Is.False);
        // The claimant's own Party is resolved by the matching path, not by supersession.
        Assert.That(TownPartyRules.IsContinuationSuperseded(duo, host, profile => profile == host), Is.False);
    }

    [Test]
    public void AwaitingPartyCompletion_NeverHoldsForSoloContinuation()
    {
        var solo = new ProfileId("solo");
        Assert.That(TownPartyContinuationContext.TryCreate(solo, new[] { solo }, out var context), Is.True);
        Assert.That(TownPartyRules.IsAwaitingPartyCompletion(context), Is.False);
    }
}
