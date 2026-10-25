using NUnit.Framework;

public sealed class TownRaidPreparationInspectionRulesTests
{
    [TestCase("host", "client")]
    [TestCase("client", "host")]
    public void FindsRemoteMember(string local, string remote)
    {
        var presentation = CreatePresentation(local, false);
        Assert.That(TownRaidPreparationInspectionRules.TryFindInspectable(
            presentation, new ProfileId(remote), out var member), Is.True);
        Assert.That(member.ProfileId, Is.EqualTo(new ProfileId(remote)));
        Assert.That(member.IsLocal, Is.False);
    }

    [Test]
    public void RejectsLocalMember()
    {
        var presentation = CreatePresentation("host", false);
        Assert.That(TownRaidPreparationInspectionRules.TryFindInspectable(
            presentation, new ProfileId("host"), out _), Is.False);
    }

    [Test]
    public void RejectsAbsentMember()
    {
        var presentation = CreatePresentation("host", false);
        Assert.That(TownRaidPreparationInspectionRules.TryFindInspectable(
            presentation, new ProfileId("absent"), out _), Is.False);
        Assert.That(TownRaidPreparationInspectionRules.TryFindInspectable(
            default, new ProfileId("client"), out _), Is.False);
    }

    [Test]
    public void NewPresentationReturnsRefreshedMemberData()
    {
        var first = CreatePresentation("host", false);
        var refreshed = CreatePresentation("host", true);
        var remote = new ProfileId("client");
        Assert.That(TownRaidPreparationInspectionRules.TryFindInspectable(first, remote, out var before), Is.True);
        Assert.That(TownRaidPreparationInspectionRules.TryFindInspectable(refreshed, remote, out var after), Is.True);
        Assert.That(before.IsReady, Is.False);
        Assert.That(after.IsReady, Is.True);
    }

    private static TownRaidPreparationPresentation CreatePresentation(string local, bool ready)
    {
        RaidCode.TryParse("100002", out RaidCode code);
        var host = new ProfileId("host");
        var snapshot = new TownRaidPreparationSnapshot(code, host, TownRaidPreparationState.Waiting,
            new[]
            {
                new TownRaidPreparationMember(host, true),
                new TownRaidPreparationMember(new ProfileId("client"), ready)
            }, ready ? 2 : 1);
        Assert.That(TownRaidPreparationPresentation.TryCreate(snapshot, new ProfileId(local), out var presentation), Is.True);
        return presentation;
    }
}
