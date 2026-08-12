using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Assert = NUnit.Framework.Assert;

public sealed class RaidParticipantSpawnRulesTests
{
    [Test]
    public void FrozenProfileOrder_ProducesStableSequentialIndices()
    {
        var profiles = new[]
        {
            new ProfileId("A"),
            new ProfileId("B"),
            new ProfileId("C"),
            new ProfileId("D"),
            new ProfileId("E")
        };

        for (int index = 0; index < profiles.Length; index++)
        {
            Assert.That(RaidParticipantSpawnRules.TryGetSpawnIndex(profiles, profiles[index], out int actual), Is.True);
            Assert.That(actual, Is.EqualTo(index));
        }

        Assert.That(RaidParticipantSpawnRules.TryGetSpawnIndex(profiles, new ProfileId("outsider"), out _), Is.False);
    }

    [TestCase(1)]
    [TestCase(5)]
    [TestCase(8)]
    [TestCase(16)]
    public void SpawnPreflight_AcceptsEnoughUniquePositions(int count)
    {
        var positions = new List<Vector3>(count);
        for (int index = 0; index < count; index++)
        {
            positions.Add(new Vector3(index, index * 2f, 0f));
        }

        Assert.That(RaidParticipantSpawnRules.ValidateSpawnPositions(positions, count, out _), Is.True);
    }

    [Test]
    public void SpawnPreflight_RejectsInsufficientAndDuplicatePositions()
    {
        var fifteen = new List<Vector3>();
        for (int index = 0; index < 15; index++)
        {
            fifteen.Add(new Vector3(index, 0f, 0f));
        }

        Assert.That(RaidParticipantSpawnRules.ValidateSpawnPositions(fifteen, 16, out _), Is.False);
        Assert.That(
            RaidParticipantSpawnRules.ValidateSpawnPositions(
                new[] { Vector3.zero, Vector3.zero },
                2,
                out _),
            Is.False);
    }

    [Test]
    public void SpawnPreflight_RejectsNullPointBeforeSpawning()
    {
        var validObject = new GameObject("valid-spawn");
        try
        {
            Assert.That(
                RaidParticipantSpawnRules.ValidateSpawnPoints(
                    new Transform[] { validObject.transform, null },
                    2,
                    out _),
                Is.False);
        }
        finally
        {
            Object.DestroyImmediate(validObject);
        }
    }
}
