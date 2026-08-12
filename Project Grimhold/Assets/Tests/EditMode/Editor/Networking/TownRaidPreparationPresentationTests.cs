using NUnit.Framework;
using Assert = NUnit.Framework.Assert;

public sealed class TownRaidPreparationPresentationTests
{
    [Test]
    public void ProfileWithoutPreparation_HasNoPreparationPresentation()
    {
        TownRaidPreparationSnapshot snapshot = CreateSnapshot("host", "client", true);

        Assert.That(TownRaidPreparationPresentation.TryCreate(
            snapshot, new ProfileId("absent"), out _), Is.False);
    }

    [Test]
    public void Member_SeesOnlyResolvedPreparationAndSixteenCapacitySource()
    {
        TownRaidPreparationSnapshot snapshotA = CreateSnapshot("host-a", "client-a", true);
        TownRaidPreparationSnapshot snapshotB = CreateSnapshot("host-b", "client-b", false);

        Assert.That(TownRaidPreparationPresentation.TryCreate(
            snapshotA, new ProfileId("client-a"), out TownRaidPreparationPresentation presentation), Is.True);
        Assert.That(presentation.Snapshot.RaidCode, Is.EqualTo(snapshotA.RaidCode));
        Assert.That(presentation.LocalReady, Is.True);
        Assert.That(presentation.IsHost, Is.False);
        Assert.That(TownRaidPreparationPresentation.TryCreate(
            snapshotB, new ProfileId("client-a"), out _), Is.False);
        Assert.That(RaidSessionRules.MaxParticipants, Is.EqualTo(16));
    }

    [Test]
    public void HostStart_IsDerivedFromHostAndAllReady()
    {
        TownRaidPreparationSnapshot ready = CreateSnapshot("host", "client", true);
        TownRaidPreparationSnapshot notReady = CreateSnapshot("host", "client", false);

        Assert.That(TownRaidPreparationPresentation.TryCreate(
            ready, ready.HostProfileId, out TownRaidPreparationPresentation canStart), Is.True);
        Assert.That(canStart.CanStart, Is.True);
        Assert.That(TownRaidPreparationPresentation.TryCreate(
            notReady, notReady.HostProfileId, out TownRaidPreparationPresentation blocked), Is.True);
        Assert.That(blocked.CanStart, Is.False);
    }

    private static TownRaidPreparationSnapshot CreateSnapshot(string hostValue, string clientValue, bool ready)
    {
        RaidCode.TryParse(hostValue == "host-a" ? "100001" : "100002", out RaidCode code);
        var host = new ProfileId(hostValue);
        return new TownRaidPreparationSnapshot(
            code,
            host,
            TownRaidPreparationState.Waiting,
            new[]
            {
                new TownRaidPreparationMember(host, ready),
                new TownRaidPreparationMember(new ProfileId(clientValue), ready)
            },
            1);
    }
}
