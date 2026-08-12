using NUnit.Framework;
using Assert = NUnit.Framework.Assert;

public sealed class TownRaidPreparationIndexTests
{
    [Test]
    public void ConcurrentPreparations_HaveUniqueCodesAndExclusiveMembership()
    {
        TownRaidPreparationSnapshot preparationA = CreateWaiting("100001", "host-a");
        TownRaidPreparationSnapshot preparationB = CreateWaiting("100002", "host-b");
        var index = new TownRaidPreparationIndex();

        Assert.That(index.TryRegister(preparationA), Is.True);
        Assert.That(index.TryRegister(preparationB), Is.True);
        Assert.That(index.TryGetByRaidCode(preparationA.RaidCode, out TownRaidPreparationSnapshot byCode), Is.True);
        Assert.That(byCode.HostProfileId, Is.EqualTo(preparationA.HostProfileId));
        Assert.That(index.TryGetByProfile(preparationB.HostProfileId, out TownRaidPreparationSnapshot byProfile), Is.True);
        Assert.That(byProfile.RaidCode, Is.EqualTo(preparationB.RaidCode));
    }

    [Test]
    public void Register_RejectsDuplicateCodeAndProfileAcrossPreparations()
    {
        TownRaidPreparationSnapshot preparationA = CreateWaiting("100001", "host-a");
        TownRaidPreparationSnapshot duplicateCode = CreateWaiting("100001", "host-b");
        TownRaidPreparationSnapshot duplicateProfile = CreateWaiting("100002", "host-a");
        var index = new TownRaidPreparationIndex();

        Assert.That(index.TryRegister(preparationA), Is.True);
        Assert.That(index.TryRegister(duplicateCode), Is.False);
        Assert.That(index.TryRegister(duplicateProfile), Is.False);
        Assert.That(index.Count, Is.EqualTo(1));
    }

    [Test]
    public void RemovePreparation_DoesNotModifyOtherPreparation()
    {
        TownRaidPreparationSnapshot preparationA = CreateWaiting("100001", "host-a");
        TownRaidPreparationSnapshot preparationB = CreateWaiting("100002", "host-b");
        var index = new TownRaidPreparationIndex();
        index.TryRegister(preparationA);
        index.TryRegister(preparationB);

        Assert.That(index.Remove(preparationA.RaidCode), Is.True);
        Assert.That(index.TryGetByProfile(preparationA.HostProfileId, out _), Is.False);
        Assert.That(index.TryGetByProfile(preparationB.HostProfileId, out TownRaidPreparationSnapshot remaining), Is.True);
        Assert.That(remaining.RaidCode, Is.EqualTo(preparationB.RaidCode));
    }

    [Test]
    public void UpdateAndRebuild_PreserveIndependentReadyAndStartEligibility()
    {
        TownRaidPreparationSnapshot preparationA = CreateWaiting("100001", "host-a");
        TownRaidPreparationSnapshot preparationB = CreateWaiting("100002", "host-b");
        var index = new TownRaidPreparationIndex();
        index.TryRegister(preparationA);
        index.TryRegister(preparationB);

        Assert.That(
            TownRaidPreparationRules.TrySetReady(
                preparationA,
                preparationA.HostProfileId,
                true,
                out TownRaidPreparationSnapshot readyA),
            Is.True);
        Assert.That(index.TryUpdate(readyA), Is.True);
        Assert.That(index.TryGetByProfile(preparationB.HostProfileId, out TownRaidPreparationSnapshot unchangedB), Is.True);
        Assert.That(unchangedB.Members[0].IsReady, Is.False);
        Assert.That(TownRaidPreparationRules.CanStart(readyA, readyA.HostProfileId), Is.True);
        Assert.That(TownRaidPreparationRules.CanStart(unchangedB, unchangedB.HostProfileId), Is.False);

        var rebuilt = new TownRaidPreparationIndex();
        Assert.That(rebuilt.TryRebuild(new[] { readyA, unchangedB }), Is.True);
        Assert.That(rebuilt.TryGetByProfile(readyA.HostProfileId, out TownRaidPreparationSnapshot rebuiltA), Is.True);
        Assert.That(rebuiltA.Members[0].IsReady, Is.True);
    }

    [Test]
    public void FailedRebuild_DoesNotReplaceExistingValidIndex()
    {
        TownRaidPreparationSnapshot preparationA = CreateWaiting("100001", "host-a");
        TownRaidPreparationSnapshot conflicting = CreateWaiting("100002", "host-a");
        var index = new TownRaidPreparationIndex();
        index.TryRegister(preparationA);

        Assert.That(index.TryRebuild(new[] { preparationA, conflicting }), Is.False);
        Assert.That(index.Count, Is.EqualTo(1));
        Assert.That(index.TryGetByProfile(preparationA.HostProfileId, out _), Is.True);
    }

    private static TownRaidPreparationSnapshot CreateWaiting(string codeValue, string hostValue)
    {
        RaidCode.TryParse(codeValue, out RaidCode code);
        var host = new ProfileId(hostValue);
        return new TownRaidPreparationSnapshot(
            code,
            host,
            TownRaidPreparationState.Waiting,
            new[] { new TownRaidPreparationMember(host) },
            1);
    }
}
