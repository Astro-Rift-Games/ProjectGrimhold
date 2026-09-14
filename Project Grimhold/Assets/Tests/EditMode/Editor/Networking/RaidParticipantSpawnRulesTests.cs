using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Spawning;
using UnityEngine;
using Assert = NUnit.Framework.Assert;

public sealed class RaidParticipantSpawnRulesTests
{
    private readonly List<GameObject> _spawnObjects = new();

    [TearDown]
    public void TearDown()
    {
        for (int index = 0; index < _spawnObjects.Count; index++)
        {
            Object.DestroyImmediate(_spawnObjects[index]);
        }

        _spawnObjects.Clear();
    }

    [Test]
    public void FreshSpawnPreflight_AcceptsValidSoloAndDuo()
    {
        PlayerSpawnAreaDefinition[] areas = CreateAreas(2, 2);

        Assert.That(
            RaidParticipantSpawnRules.ValidateFreshSpawnPreflight(
                CreateContext(Participant("solo", 1)), areas, out string soloFailure),
            Is.True,
            soloFailure);
        Assert.That(
            RaidParticipantSpawnRules.ValidateFreshSpawnPreflight(
                CreateContext(Participant("host", 1), Participant("member", 1)),
                areas,
                out string duoFailure),
            Is.True,
            duoFailure);
    }

    [Test]
    public void FreshSpawnPreflight_RejectsMissingAreasInsufficientAreasAndTeamCapacity()
    {
        RaidLaunchContext duo = CreateContext(Participant("host", 1), Participant("member", 1));
        RaidLaunchContext twoTeams = CreateContext(Participant("host", 7), Participant("member", 2));

        Assert.That(RaidParticipantSpawnRules.ValidateFreshSpawnPreflight(duo, null, out _), Is.False);
        Assert.That(
            RaidParticipantSpawnRules.ValidateFreshSpawnPreflight(twoTeams, CreateAreas(1, 2), out _),
            Is.False);
        Assert.That(
            RaidParticipantSpawnRules.ValidateFreshSpawnPreflight(duo, CreateAreas(1, 1), out _),
            Is.False);
    }

    [Test]
    public void FreshSpawnPreflight_RejectsNullRepeatedTransformAndRepeatedPositionAcrossAreas()
    {
        RaidLaunchContext context = CreateContext(Participant("host", 1));
        Transform first = CreateSpawnPoint("first", Vector3.zero);
        Transform samePosition = CreateSpawnPoint("same-position", Vector3.zero);

        Assert.That(
            RaidParticipantSpawnRules.ValidateFreshSpawnPreflight(
                context,
                new[] { new PlayerSpawnAreaDefinition(new Transform[] { null }) },
                out _),
            Is.False);
        Assert.That(
            RaidParticipantSpawnRules.ValidateFreshSpawnPreflight(
                context,
                new[]
                {
                    new PlayerSpawnAreaDefinition(new[] { first }),
                    new PlayerSpawnAreaDefinition(new[] { first })
                },
                out _),
            Is.False);
        Assert.That(
            RaidParticipantSpawnRules.ValidateFreshSpawnPreflight(
                context,
                new[]
                {
                    new PlayerSpawnAreaDefinition(new[] { first }),
                    new PlayerSpawnAreaDefinition(new[] { samePosition })
                },
                out _),
            Is.False);
    }

    [Test]
    public void FreshSpawnPreflight_RejectsInvalidParticipantOrTeam()
    {
        RaidLaunchParticipant invalidProfile = new(default, Team(1));
        RaidLaunchParticipant invalidTeam = new(new ProfileId("host"), default);

        Assert.That(
            RaidParticipantSpawnRules.ValidateFreshSpawnPreflight(
                CreateUncheckedContext(invalidProfile), CreateAreas(1, 1), out _),
            Is.False);
        Assert.That(
            RaidParticipantSpawnRules.ValidateFreshSpawnPreflight(
                CreateUncheckedContext(invalidTeam), CreateAreas(1, 1), out _),
            Is.False);
    }

    [Test]
    public void FreshSpawnPreflight_RejectsWholeContextWhenAnyParticipantCannotResolve()
    {
        RaidLaunchContext context = CreateContext(
            Participant("host", 1),
            Participant("member", 1),
            Participant("other-team", 2));
        PlayerSpawnAreaDefinition[] areas =
        {
            new(new[]
            {
                CreateSpawnPoint("area-0-point-0", new Vector3(0f, 0f)),
                CreateSpawnPoint("area-0-point-1", new Vector3(1f, 0f))
            })
        };

        Assert.That(
            RaidParticipantSpawnRules.ValidateFreshSpawnPreflight(context, areas, out _),
            Is.False);
    }

    [Test]
    public void Resolver_AssignsDuoToDistinctPositionsInSameAreaAndIsStable()
    {
        RaidLaunchParticipant[] participants =
        {
            Participant("host", 1),
            Participant("member", 1)
        };
        PlayerSpawnAreaDefinition[] areas = CreateAreas(1, 2);

        AssertResolved(participants, new ProfileId("host"), areas, 0, 0);
        AssertResolved(participants, new ProfileId("member"), areas, 0, 1);
        AssertResolved(participants, new ProfileId("member"), areas, 0, 1);
    }

    [Test]
    public void Resolver_RejectsProfileOutsideFrozenRoster()
    {
        RaidLaunchParticipant[] participants = { Participant("host", 1) };

        Assert.That(
            RaidParticipantSpawnRules.TryResolveSpawnAssignment(
                participants,
                new ProfileId("outsider"),
                CreateAreas(1, 1),
                out _,
                out _,
                out _),
            Is.False);
    }

    [Test]
    public void Resolver_UsesFirstTeamAppearanceNotNonConsecutiveTeamValues()
    {
        RaidLaunchParticipant[] participants =
        {
            Participant("team-7-a", 7),
            Participant("team-2-a", 2),
            Participant("team-7-b", 7)
        };
        PlayerSpawnAreaDefinition[] areas = CreateAreas(2, 2);

        AssertResolved(participants, new ProfileId("team-7-a"), areas, 0, 0);
        AssertResolved(participants, new ProfileId("team-7-b"), areas, 0, 1);
        AssertResolved(participants, new ProfileId("team-2-a"), areas, 1, 0);
    }

    [Test]
    public void SceneConfiguration_RejectsGenericPlayersGroup()
    {
        var owner = new GameObject("spawn-configuration");
        _spawnObjects.Add(owner);
        NetworkSpawnSceneConfiguration configuration =
            owner.AddComponent<NetworkSpawnSceneConfiguration>();
        PlayerSpawnAreaDefinition[] areas = CreateAreas(1, 1);
        var playerGroup = new SpawnGroupDefinition
        {
            Group = SpawnGroupType.Players,
            SpawnPoints = new[] { areas[0].SpawnPoints[0] },
            Amount = 0
        };

        SetPrivateField(configuration, "_playerSpawnAreas", areas);
        SetPrivateField(configuration, "_spawnGroups", new[] { playerGroup });

        Assert.That(configuration.Validate(out string failure), Is.False);
        Assert.That(failure, Does.Contain("player spawn areas"));
    }

    private static void AssertResolved(
        IReadOnlyList<RaidLaunchParticipant> participants,
        ProfileId profileId,
        IReadOnlyList<PlayerSpawnAreaDefinition> areas,
        int expectedArea,
        int expectedPoint)
    {
        Assert.That(
            RaidParticipantSpawnRules.TryResolveSpawnAssignment(
                participants,
                profileId,
                areas,
                out int areaIndex,
                out int pointIndex,
                out string failure),
            Is.True,
            failure);
        Assert.That(areaIndex, Is.EqualTo(expectedArea));
        Assert.That(pointIndex, Is.EqualTo(expectedPoint));
    }

    private PlayerSpawnAreaDefinition[] CreateAreas(int areaCount, int pointsPerArea)
    {
        var areas = new PlayerSpawnAreaDefinition[areaCount];
        for (int areaIndex = 0; areaIndex < areaCount; areaIndex++)
        {
            var points = new Transform[pointsPerArea];
            for (int pointIndex = 0; pointIndex < pointsPerArea; pointIndex++)
            {
                points[pointIndex] = CreateSpawnPoint(
                    $"area-{areaIndex}-point-{pointIndex}",
                    new Vector3(areaIndex * 100f + pointIndex, areaIndex, 0f));
            }

            areas[areaIndex] = new PlayerSpawnAreaDefinition(points);
        }

        return areas;
    }

    private Transform CreateSpawnPoint(string name, Vector3 position)
    {
        var spawnObject = new GameObject(name);
        spawnObject.transform.position = position;
        _spawnObjects.Add(spawnObject);
        return spawnObject.transform;
    }

    private static RaidLaunchParticipant Participant(string profileId, int teamId) =>
        new(new ProfileId(profileId), Team(teamId));

    private static RaidTeamId Team(int value)
    {
        Assert.That(RaidTeamId.TryCreate(value, out RaidTeamId teamId), Is.True);
        return teamId;
    }

    private static RaidLaunchContext CreateContext(params RaidLaunchParticipant[] participants)
    {
        Assert.That(RaidCode.TryParse("123456", out RaidCode raidCode), Is.True);
        Assert.That(
            RaidLaunchContext.TryCreate(
                raidCode,
                participants[0].ProfileId,
                participants,
                participants[0].ProfileId,
                1,
                out RaidLaunchContext context),
            Is.True);
        return context;
    }

    private static RaidLaunchContext CreateUncheckedContext(params RaidLaunchParticipant[] participants)
    {
        Assert.That(RaidCode.TryParse("123456", out RaidCode raidCode), Is.True);
        ConstructorInfo constructor = typeof(RaidLaunchContext).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            null,
            new[]
            {
                typeof(RaidCode),
                typeof(ProfileId),
                typeof(IReadOnlyList<RaidLaunchParticipant>),
                typeof(ProfileId),
                typeof(int)
            },
            null);
        Assert.That(constructor, Is.Not.Null);
        return (RaidLaunchContext)constructor.Invoke(
            new object[] { raidCode, new ProfileId("host"), participants, new ProfileId("host"), 1 });
    }

    private static void SetPrivateField(object owner, string fieldName, object value)
    {
        FieldInfo field = owner.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        field.SetValue(owner, value);
    }
}
