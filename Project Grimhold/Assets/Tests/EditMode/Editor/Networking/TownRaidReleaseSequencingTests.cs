#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;

/// <summary>
/// Pins the staged Town release decision (FIX-04): the Raid Host leaves Town alone first, the other
/// non-authority members follow once its departure is observed, and the Town authority leaves last.
/// The Fusion RPC dispatch and PlayerLeft callbacks stay thin glue over these rules.
/// </summary>
public sealed class TownRaidReleaseSequencingTests
{
    private static readonly ProfileId Host = new("host");
    private static readonly ProfileId Authority = new("authority");
    private static readonly ProfileId MemberA = new("member-a");
    private static readonly ProfileId MemberB = new("member-b");

    [Test]
    public void InitialRelease_WithDistinctTownAuthority_TargetsOnlyTheRaidHost()
    {
        var order = new[] { Host, MemberA, MemberB };

        var batch = TownRaidPreparationRules.SelectReleaseBatch(Host, order, Set(), Set());

        Assert.That(batch, Is.EqualTo(new[] { Host }));
    }

    [Test]
    public void OtherMembers_AreHeldBackWhileTheHostIsReleasedButNotObservedLeaving()
    {
        var order = new[] { Host, MemberA, MemberB };

        var batch = TownRaidPreparationRules.SelectReleaseBatch(Host, order, Set(Host), Set());

        Assert.That(batch, Is.Empty);
    }

    [Test]
    public void OtherMembers_AreReleasedOnlyAfterTheHostDepartureIsObserved()
    {
        var order = new[] { Host, MemberA, MemberB };

        var batch = TownRaidPreparationRules.SelectReleaseBatch(Host, order, Set(Host), Set(Host));

        Assert.That(batch, Is.EqualTo(new[] { MemberA, MemberB }));
    }

    [Test]
    public void ReleaseBatch_NeverRepeatsAlreadyReleasedProfiles()
    {
        var order = new[] { Host, MemberA, MemberB };

        var batch = TownRaidPreparationRules.SelectReleaseBatch(
            Host, order, Set(Host, MemberA, MemberB), Set(Host));

        Assert.That(batch, Is.Empty);
    }

    [Test]
    public void ReleaseBatch_WithTownAuthorityAsRaidHost_ReleasesAllClientsAtOnce()
    {
        // Host is the Town authority: it is not an initial departure, so the clients leave first
        // exactly as before and the Host-first staging does not apply.
        var order = new[] { MemberA, MemberB };

        var batch = TownRaidPreparationRules.SelectReleaseBatch(Host, order, Set(), Set());

        Assert.That(batch, Is.EqualTo(new[] { MemberA, MemberB }));
        Assert.That(
            TownRaidPreparationRules.SelectReleaseBatch(Host, order, Set(MemberA, MemberB), Set()),
            Is.Empty);
    }

    [Test]
    public void ReleaseBatch_NeverIncludesTheTownAuthority_BecauseItIsNotInTheOrder()
    {
        TownRaidPreparationSnapshot frozen = CreateFrozen(Host, Authority, MemberA, MemberB);
        Assert.That(TownRaidPreparationRules.TryCreateReleaseOrder(frozen, Authority, out var order), Is.True);

        var first = TownRaidPreparationRules.SelectReleaseBatch(Host, order, Set(), Set());
        var second = TownRaidPreparationRules.SelectReleaseBatch(Host, order, Set(Host), Set(Host));

        Assert.That(first, Is.EqualTo(new[] { Host }));
        Assert.That(second, Is.EqualTo(new[] { MemberA, MemberB }));
        Assert.That(first, Has.None.EqualTo(Authority));
        Assert.That(second, Has.None.EqualTo(Authority));
    }

    [Test]
    public void TerminalAuthority_IsReleasedOnlyAfterEveryExpectedDepartureLeft()
    {
        var expected = Set(Host, MemberA, MemberB);

        Assert.That(TownRaidPreparationRules.CanReleaseTerminalAuthority(
            expected, Set(Host), Set()), Is.False);
        Assert.That(TownRaidPreparationRules.CanReleaseTerminalAuthority(
            expected, Set(Host, MemberA, MemberB), Set(Host)), Is.False,
            "Host gone but members still in Town: the authority must stay.");
        Assert.That(TownRaidPreparationRules.CanReleaseTerminalAuthority(
            expected, Set(Host, MemberA, MemberB), Set(Host, MemberA)), Is.False);
        Assert.That(TownRaidPreparationRules.CanReleaseTerminalAuthority(
            expected, Set(Host, MemberA, MemberB), Set(Host, MemberA, MemberB)), Is.True);
    }

    [Test]
    public void TerminalAuthority_WithNoOtherParticipants_IsReleasedImmediately()
    {
        Assert.That(TownRaidPreparationRules.CanReleaseTerminalAuthority(Set(), Set(), Set()), Is.True);
    }

    [Test]
    public void TerminalAuthority_IsNotReleasedWhileAnExpectedMemberWasNeverReleased()
    {
        var expected = Set(Host, MemberA);

        Assert.That(TownRaidPreparationRules.CanReleaseTerminalAuthority(
            expected, Set(Host), Set(Host, MemberA)), Is.False);
    }

    [Test]
    public void Departure_OfAReleasedMember_IsExpected()
    {
        var expected = Set(Host, MemberA);

        Assert.That(TownRaidPreparationRules.IsExpectedReleaseDeparture(expected, Set(Host), Host), Is.True);
    }

    [Test]
    public void Departure_OfAnUnreleasedMember_IsPrematureAndCancelsAsToday()
    {
        var expected = Set(Host, MemberA);

        Assert.That(TownRaidPreparationRules.IsExpectedReleaseDeparture(expected, Set(Host), MemberA), Is.False);
    }

    [Test]
    public void Departure_OfAProfileOutsideTheRelease_IsPremature()
    {
        var expected = Set(Host, MemberA);

        Assert.That(TownRaidPreparationRules.IsExpectedReleaseDeparture(
            expected, Set(Host, MemberA), new ProfileId("stranger")), Is.False);
    }

    private static HashSet<ProfileId> Set(params ProfileId[] profiles) => new(profiles);

    private static TownRaidPreparationSnapshot CreateFrozen(params ProfileId[] profiles)
    {
        RaidCode.TryParse("654321", out RaidCode code);
        var members = new TownRaidPreparationMember[profiles.Length];
        for (int index = 0; index < profiles.Length; index++)
        {
            members[index] = new TownRaidPreparationMember(profiles[index], true, 9, true);
        }

        return new TownRaidPreparationSnapshot(
            code, profiles[0], TownRaidPreparationState.Launching, members, 4, 9, profiles.Length);
    }
}
#endif
