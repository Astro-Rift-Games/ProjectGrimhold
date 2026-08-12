using System.Collections.Generic;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;

public sealed class TownRaidPreparationDirectoryCacheTests
{
    private sealed class Preparation { }

    [Test]
    public void ConcurrentPreparations_RegisterResolveAndDespawnIndependently()
    {
        var first = new Preparation();
        var second = new Preparation();
        TownRaidPreparationSnapshot snapshotA = CreateWaiting("100001", "host-a", "client-a");
        TownRaidPreparationSnapshot snapshotB = CreateWaiting("100002", "host-b");
        var cache = new TownRaidPreparationDirectoryCache<Preparation>();

        Assert.That(cache.RegisterOrUpdate(first, snapshotA), Is.True);
        Assert.That(cache.RegisterOrUpdate(second, snapshotB), Is.True);
        Assert.That(cache.TryResolve(snapshotA.RaidCode, out Preparation byCode), Is.True);
        Assert.That(byCode, Is.SameAs(first));
        Assert.That(cache.TryResolve(snapshotB.HostProfileId, out Preparation byProfile), Is.True);
        Assert.That(byProfile, Is.SameAs(second));

        Assert.That(cache.Unregister(first), Is.True);
        Assert.That(cache.TryResolve(snapshotA.RaidCode, out _), Is.False);
        Assert.That(cache.TryResolve(snapshotB.RaidCode, out Preparation remaining), Is.True);
        Assert.That(remaining, Is.SameAs(second));
    }

    [Test]
    public void SnapshotUpdate_ReindexesOnlyChangedPreparationAndAllowsLaterJoinElsewhere()
    {
        var first = new Preparation();
        var second = new Preparation();
        TownRaidPreparationSnapshot snapshotA = CreateWaiting("100001", "host-a", "client");
        TownRaidPreparationSnapshot snapshotB = CreateWaiting("100002", "host-b");
        var cache = new TownRaidPreparationDirectoryCache<Preparation>();
        cache.RegisterOrUpdate(first, snapshotA);
        cache.RegisterOrUpdate(second, snapshotB);

        Assert.That(TownRaidPreparationRules.TryRemoveMember(
            snapshotA, new ProfileId("client"), out TownRaidPreparationSnapshot leftA), Is.True);
        Assert.That(cache.RegisterOrUpdate(first, leftA), Is.True);
        Assert.That(cache.TryResolve(new ProfileId("client"), out _), Is.False);

        Assert.That(TownRaidPreparationRules.TryAddMember(
            snapshotB, new ProfileId("client"), out TownRaidPreparationSnapshot joinedB), Is.True);
        Assert.That(cache.RegisterOrUpdate(second, joinedB), Is.True);
        Assert.That(cache.TryResolve(new ProfileId("client"), out Preparation resolved), Is.True);
        Assert.That(resolved, Is.SameAs(second));
        Assert.That(cache.TryResolve(snapshotA.HostProfileId, out Preparation unchanged), Is.True);
        Assert.That(unchanged, Is.SameAs(first));
    }

    [Test]
    public void DuplicateCodeOrProfile_IsUnresolvedAndRecoversAfterClaimRemoval()
    {
        var first = new Preparation();
        var second = new Preparation();
        TownRaidPreparationSnapshot snapshotA = CreateWaiting("100001", "host-a", "shared");
        TownRaidPreparationSnapshot snapshotB = CreateWaiting("100001", "host-b", "shared");
        var cache = new TownRaidPreparationDirectoryCache<Preparation>();
        cache.RegisterOrUpdate(first, snapshotA);
        cache.RegisterOrUpdate(second, snapshotB);

        Assert.That(cache.IsConsistent, Is.False);
        Assert.That(cache.TryResolve(snapshotA.RaidCode, out _), Is.False);
        Assert.That(cache.TryResolve(new ProfileId("shared"), out _), Is.False);

        cache.Unregister(second);
        Assert.That(cache.IsConsistent, Is.True);
        Assert.That(cache.TryResolve(snapshotA.RaidCode, out Preparation recovered), Is.True);
        Assert.That(recovered, Is.SameAs(first));
    }

    [Test]
    public void AuthorityRebuild_RecreatesEquivalentMappingsAndFailsClosedOnConflict()
    {
        var first = new Preparation();
        var second = new Preparation();
        TownRaidPreparationSnapshot snapshotA = CreateWaiting("100001", "host-a");
        TownRaidPreparationSnapshot snapshotB = CreateWaiting("100002", "host-b");
        var cache = new TownRaidPreparationDirectoryCache<Preparation>();

        Assert.That(cache.Rebuild(new[]
        {
            new KeyValuePair<Preparation, TownRaidPreparationSnapshot>(first, snapshotA),
            new KeyValuePair<Preparation, TownRaidPreparationSnapshot>(second, snapshotB)
        }), Is.True);
        Assert.That(cache.TryResolve(snapshotB.HostProfileId, out Preparation resolved), Is.True);
        Assert.That(resolved, Is.SameAs(second));

        TownRaidPreparationSnapshot conflicting = CreateWaiting("100003", "host-a");
        Assert.That(cache.Rebuild(new[]
        {
            new KeyValuePair<Preparation, TownRaidPreparationSnapshot>(first, snapshotA),
            new KeyValuePair<Preparation, TownRaidPreparationSnapshot>(second, conflicting)
        }), Is.False);
        Assert.That(cache.IsConsistent, Is.False);
        Assert.That(cache.TryResolve(snapshotA.HostProfileId, out _), Is.False);
    }

    private static TownRaidPreparationSnapshot CreateWaiting(string codeValue, params string[] members)
    {
        RaidCode.TryParse(codeValue, out RaidCode code);
        var roster = new TownRaidPreparationMember[members.Length];
        for (int index = 0; index < members.Length; index++)
        {
            roster[index] = new TownRaidPreparationMember(new ProfileId(members[index]));
        }

        return new TownRaidPreparationSnapshot(
            code,
            roster[0].ProfileId,
            TownRaidPreparationState.Waiting,
            roster,
            1);
    }
}
