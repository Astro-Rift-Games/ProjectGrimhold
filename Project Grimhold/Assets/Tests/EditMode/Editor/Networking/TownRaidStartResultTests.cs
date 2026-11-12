using System;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;

public sealed class TownRaidStartResultTests
{
    private static readonly ProfileId Host = new("host");
    private static readonly ProfileId Partner = new("partner");
    private static readonly ProfileId Solo = new("solo");

    [Test]
    public void PublicResults_AreTheApprovedTypedContract()
    {
        CollectionAssert.AreEquivalent(
            new[]
            {
                nameof(TownRaidStartResult.Started),
                nameof(TownRaidStartResult.PartyUnavailable),
                nameof(TownRaidStartResult.Rejected)
            },
            Enum.GetNames(typeof(TownRaidStartResult)));
    }

    [Test]
    public void TryStart_PartyDirectoryUnavailable_IsPartyUnavailableAndDoesNotFreeze()
    {
        TownRaidPreparationSnapshot waiting = CreateWaiting(Host, Partner, Solo);

        TownRaidStartResult result = TownRaidPreparationRules.TryStart(
            waiting, Host, 3, null, out TownRaidPreparationSnapshot frozen, out ProfileId unresolved);

        Assert.That(result, Is.EqualTo(TownRaidStartResult.PartyUnavailable));
        Assert.That(frozen.State, Is.Not.EqualTo(TownRaidPreparationState.Launching));
        Assert.That(unresolved.IsValid, Is.False);
    }

    [Test]
    public void TryStart_MemberWithoutParty_IsPartyUnavailableAndReportsThatMember()
    {
        TownRaidPreparationSnapshot waiting = CreateWaiting(Host, Partner, Solo);
        TownRaidPreparationRules.PartyIdResolver resolver = (ProfileId profile, out int partyId) =>
        {
            partyId = 7;
            return profile != Partner;
        };

        TownRaidStartResult result = TownRaidPreparationRules.TryStart(
            waiting, Host, 3, resolver, out TownRaidPreparationSnapshot frozen, out ProfileId unresolved);

        Assert.That(result, Is.EqualTo(TownRaidStartResult.PartyUnavailable));
        Assert.That(unresolved, Is.EqualTo(Partner));
        Assert.That(frozen.State, Is.Not.EqualTo(TownRaidPreparationState.Launching));
    }

    [Test]
    public void TryStart_AllPartiesResolved_StartsAndFreezesTeamsFromParties()
    {
        TownRaidPreparationSnapshot waiting = CreateWaiting(Host, Partner, Solo);
        TownRaidPreparationRules.PartyIdResolver resolver = (ProfileId profile, out int partyId) =>
        {
            partyId = profile == Solo ? 9 : 4;
            return true;
        };

        TownRaidStartResult result = TownRaidPreparationRules.TryStart(
            waiting, Host, 3, resolver, out TownRaidPreparationSnapshot frozen, out ProfileId unresolved);

        Assert.That(result, Is.EqualTo(TownRaidStartResult.Started));
        Assert.That(unresolved.IsValid, Is.False);
        Assert.That(frozen.State, Is.EqualTo(TownRaidPreparationState.Launching));
        Assert.That(frozen.Members[0].TeamOrdinal, Is.EqualTo(frozen.Members[1].TeamOrdinal));
        Assert.That(frozen.Members[2].TeamOrdinal, Is.Not.EqualTo(frozen.Members[0].TeamOrdinal));
    }

    [Test]
    public void TryStart_NonHostRequester_IsRejectedBeforeAnyPartyLookup()
    {
        TownRaidPreparationSnapshot waiting = CreateWaiting(Host, Partner, Solo);
        int lookups = 0;
        TownRaidPreparationRules.PartyIdResolver resolver = (ProfileId profile, out int partyId) =>
        {
            lookups++;
            partyId = 1;
            return true;
        };

        TownRaidStartResult result = TownRaidPreparationRules.TryStart(
            waiting, Partner, 3, resolver, out _, out _);

        Assert.That(result, Is.EqualTo(TownRaidStartResult.Rejected));
        Assert.That(lookups, Is.Zero);
    }

    [Test]
    public void TryStart_NotAllReady_IsRejectedEvenWithoutParties()
    {
        TownRaidPreparationSnapshot waiting = CreateWaiting(Host, Partner, Solo);
        TownRaidPreparationRules.TrySetReady(waiting, Solo, false, out TownRaidPreparationSnapshot notReady);

        Assert.That(TownRaidPreparationRules.TryStart(notReady, Host, 3, null, out _, out _),
            Is.EqualTo(TownRaidStartResult.Rejected));
    }

    private static TownRaidPreparationSnapshot CreateWaiting(ProfileId host, params ProfileId[] others)
    {
        RaidCode.TryParse("100001", out RaidCode code);
        var members = new TownRaidPreparationMember[others.Length + 1];
        members[0] = new TownRaidPreparationMember(host, true);
        for (int index = 0; index < others.Length; index++)
        {
            members[index + 1] = new TownRaidPreparationMember(others[index], true);
        }

        return new TownRaidPreparationSnapshot(code, host, TownRaidPreparationState.Waiting, members, 1);
    }
}
