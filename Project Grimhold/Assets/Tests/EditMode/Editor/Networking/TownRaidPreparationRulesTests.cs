using NUnit.Framework;
using Assert = NUnit.Framework.Assert;

public sealed class TownRaidPreparationRulesTests
{
    [TestCase(1)]
    [TestCase(4)]
    [TestCase(5)]
    [TestCase(15)]
    [TestCase(16)]
    public void WaitingSnapshot_AcceptsSupportedMemberCounts(int count)
    {
        TownRaidPreparationSnapshot snapshot = CreateWaiting("100001", count, false);

        Assert.That(TownRaidPreparationRules.IsValidSnapshot(snapshot), Is.True);
    }

    [Test]
    public void WaitingSnapshot_RejectsSeventeenInvalidDuplicateAndMissingHost()
    {
        TownRaidPreparationSnapshot seventeen = CreateWaiting("100001", 17, false);
        TownRaidPreparationSnapshot valid = CreateWaiting("100002", 2, false);
        var invalidMembers = new[]
        {
            valid.Members[0],
            new TownRaidPreparationMember(default)
        };
        var duplicateMembers = new[] { valid.Members[0], valid.Members[0] };
        var missingHostMembers = new[] { new TownRaidPreparationMember(new ProfileId("other")) };

        Assert.That(TownRaidPreparationRules.IsValidSnapshot(seventeen), Is.False);
        Assert.That(TownRaidPreparationRules.IsValidSnapshot(CreateSnapshot(valid, invalidMembers)), Is.False);
        Assert.That(TownRaidPreparationRules.IsValidSnapshot(CreateSnapshot(valid, duplicateMembers)), Is.False);
        Assert.That(TownRaidPreparationRules.IsValidSnapshot(CreateSnapshot(valid, missingHostMembers)), Is.False);
    }

    [Test]
    public void NewMember_IsNotReady_AndAllReadyIsDerived()
    {
        TownRaidPreparationSnapshot snapshot = CreateWaiting("100001", 1, false);
        ProfileId client = new ProfileId("client");

        Assert.That(TownRaidPreparationRules.TryAddMember(snapshot, client, out TownRaidPreparationSnapshot added), Is.True);
        Assert.That(added.Members[1].IsReady, Is.False);
        Assert.That(TownRaidPreparationRules.AreAllMembersReady(added.Members), Is.False);
        Assert.That(TownRaidPreparationRules.TrySetReady(added, added.HostProfileId, true, out TownRaidPreparationSnapshot hostReady), Is.True);
        Assert.That(TownRaidPreparationRules.AreAllMembersReady(hostReady.Members), Is.False);
        Assert.That(TownRaidPreparationRules.TrySetReady(hostReady, client, true, out TownRaidPreparationSnapshot allReady), Is.True);
        Assert.That(TownRaidPreparationRules.AreAllMembersReady(allReady.Members), Is.True);
    }

    [Test]
    public void Start_RequiresHostRequesterAndEveryMemberReady()
    {
        TownRaidPreparationSnapshot notReady = CreateWaiting("100001", 2, false);
        TownRaidPreparationSnapshot allReady = CreateWaiting("100002", 2, true);

        Assert.That(TownRaidPreparationRules.CanStart(notReady, notReady.HostProfileId), Is.False);
        Assert.That(TownRaidPreparationRules.CanStart(allReady, allReady.Members[1].ProfileId), Is.False);
        Assert.That(TownRaidPreparationRules.CanStart(allReady, allReady.HostProfileId), Is.True);
    }

    [Test]
    public void Starting_RejectsJoinLeaveReadyMutationAndSecondStart()
    {
        TownRaidPreparationSnapshot waiting = CreateWaiting("100001", 2, true);
        Assert.That(TownRaidPreparationRules.TryFreeze(waiting, waiting.HostProfileId, 7, out TownRaidPreparationSnapshot frozen), Is.True);

        Assert.That(TownRaidPreparationRules.CanJoin(frozen, new ProfileId("late")), Is.False);
        Assert.That(TownRaidPreparationRules.CanLeave(frozen, frozen.Members[1].ProfileId), Is.False);
        Assert.That(TownRaidPreparationRules.CanSetReady(frozen, frozen.Members[1].ProfileId), Is.False);
        Assert.That(TownRaidPreparationRules.CanStart(frozen, frozen.HostProfileId), Is.False);
        Assert.That(TownRaidPreparationRules.TryFreeze(frozen, frozen.HostProfileId, 8, out _), Is.False);
    }

    [Test]
    public void FrozenSnapshot_RequiresCompleteMatchingRevisionAndValidMembership()
    {
        TownRaidPreparationSnapshot waiting = CreateWaiting("100001", 2, true);
        Assert.That(TownRaidPreparationRules.TryFreeze(waiting, waiting.HostProfileId, 7, out TownRaidPreparationSnapshot frozen), Is.True);

        var wrongRevisionMembers = new[]
        {
            frozen.Members[0],
            frozen.Members[1].WithLaunchRevision(6)
        };
        var duplicateMembers = new[]
        {
            frozen.Members[0],
            new TownRaidPreparationMember(frozen.Members[0].ProfileId, true, 7)
        };

        Assert.That(TownRaidPreparationRules.IsCompleteFrozenSnapshot(frozen), Is.True);
        Assert.That(TownRaidPreparationRules.IsCompleteFrozenSnapshot(CreateFrozen(frozen, frozen.Members, 1)), Is.False);
        Assert.That(TownRaidPreparationRules.IsCompleteFrozenSnapshot(CreateFrozen(frozen, wrongRevisionMembers, 2)), Is.False);
        Assert.That(TownRaidPreparationRules.IsCompleteFrozenSnapshot(CreateFrozen(frozen, duplicateMembers, 2)), Is.False);
        Assert.That(
            TownRaidPreparationRules.IsCompleteFrozenSnapshot(
                new TownRaidPreparationSnapshot(
                    frozen.RaidCode,
                    new ProfileId("missing-host"),
                    TownRaidPreparationState.Starting,
                    frozen.Members,
                    frozen.SnapshotRevision,
                    frozen.LaunchRevision,
                    frozen.FrozenMemberCount)),
            Is.False);
    }

    [Test]
    public void FrozenSnapshot_MaterializesDeterministicAuthoritativeOrder()
    {
        RaidCode.TryParse("100001", out RaidCode code);
        var host = new ProfileId("host-z");
        var members = new[]
        {
            new TownRaidPreparationMember(host, true),
            new TownRaidPreparationMember(new ProfileId("client-a"), true),
            new TownRaidPreparationMember(new ProfileId("client-m"), true)
        };
        var waiting = new TownRaidPreparationSnapshot(
            code, host, TownRaidPreparationState.Waiting, members, 1);
        Assert.That(TownRaidPreparationRules.TryFreeze(waiting, host, 3, out TownRaidPreparationSnapshot frozen), Is.True);

        Assert.That(TownRaidPreparationRules.TryCreateLaunchContext(frozen, members[1].ProfileId, out RaidLaunchContext first), Is.True);
        Assert.That(TownRaidPreparationRules.TryCreateLaunchContext(frozen, members[1].ProfileId, out RaidLaunchContext second), Is.True);
        Assert.That(first.ParticipantProfileIds, Is.EqualTo(new[] { host, members[1].ProfileId, members[2].ProfileId }));
        Assert.That(second.ParticipantProfileIds, Is.EqualTo(first.ParticipantProfileIds));
        Assert.That(first.Participants, Has.Count.EqualTo(3));
        Assert.That(first.Participants, Has.All.Matches<RaidLaunchParticipant>(
            participant => participant.TeamId.IsValid && participant.TeamId.Value == 1));
        Assert.That(first.LaunchRevision, Is.EqualTo(3));
        Assert.That(TownRaidPreparationRules.TryCreateLaunchContext(frozen, new ProfileId("absent"), out _), Is.False);
    }

    [Test]
    public void LaunchAcknowledgement_IsRevisionBoundIdempotentAndRecoverableFromSnapshot()
    {
        TownRaidPreparationSnapshot waiting = CreateWaiting("100001", 2, true);
        TownRaidPreparationRules.TryFreeze(waiting, waiting.HostProfileId, 7, out TownRaidPreparationSnapshot frozen);
        ProfileId host = frozen.HostProfileId;
        ProfileId client = frozen.Members[1].ProfileId;

        Assert.That(TownRaidPreparationRules.AreAllLaunchAcknowledged(frozen), Is.False);
        Assert.That(TownRaidPreparationRules.TryAcknowledgeLaunch(frozen, client, 6, out _), Is.False);
        Assert.That(TownRaidPreparationRules.TryAcknowledgeLaunch(frozen, new ProfileId("outsider"), 7, out _), Is.False);
        Assert.That(TownRaidPreparationRules.TryAcknowledgeLaunch(frozen, client, 7, out TownRaidPreparationSnapshot clientAck), Is.True);
        Assert.That(TownRaidPreparationRules.TryAcknowledgeLaunch(clientAck, client, 7, out TownRaidPreparationSnapshot duplicate), Is.True);
        Assert.That(duplicate.SnapshotRevision, Is.EqualTo(clientAck.SnapshotRevision));
        Assert.That(TownRaidPreparationRules.AreAllLaunchAcknowledged(duplicate), Is.False);
        Assert.That(TownRaidPreparationRules.TryAcknowledgeLaunch(duplicate, host, 7, out TownRaidPreparationSnapshot allAck), Is.True);
        Assert.That(TownRaidPreparationRules.AreAllLaunchAcknowledged(allAck), Is.True);
        Assert.That(allAck.Members, Has.All.Matches<TownRaidPreparationMember>(member => member.LaunchAcknowledged));
    }

    private static TownRaidPreparationSnapshot CreateWaiting(string codeValue, int count, bool ready)
    {
        RaidCode.TryParse(codeValue, out RaidCode code);
        var members = new TownRaidPreparationMember[count];
        for (int index = 0; index < count; index++)
        {
            members[index] = new TownRaidPreparationMember(new ProfileId($"profile-{index}"), ready);
        }

        ProfileId host = count > 0 ? members[0].ProfileId : default;
        return new TownRaidPreparationSnapshot(
            code, host, TownRaidPreparationState.Waiting, members, 1);
    }

    private static TownRaidPreparationSnapshot CreateSnapshot(
        in TownRaidPreparationSnapshot source,
        TownRaidPreparationMember[] members)
    {
        return new TownRaidPreparationSnapshot(
            source.RaidCode,
            source.HostProfileId,
            source.State,
            members,
            source.SnapshotRevision,
            source.LaunchRevision,
            source.FrozenMemberCount);
    }

    private static TownRaidPreparationSnapshot CreateFrozen(
        in TownRaidPreparationSnapshot source,
        System.Collections.Generic.IReadOnlyList<TownRaidPreparationMember> members,
        int frozenMemberCount)
    {
        return new TownRaidPreparationSnapshot(
            source.RaidCode,
            source.HostProfileId,
            TownRaidPreparationState.Starting,
            members,
            source.SnapshotRevision,
            source.LaunchRevision,
            frozenMemberCount);
    }
}
