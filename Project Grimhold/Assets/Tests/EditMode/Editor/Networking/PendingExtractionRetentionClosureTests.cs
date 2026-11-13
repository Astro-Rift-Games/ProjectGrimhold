#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.IO;
using NUnit.Framework;

public sealed class PendingExtractionRetentionClosureTests
{
    private const string SpawnManagerPath =
        "Assets/Scripts/Networking/NetworkSpawnManager.cs";
    private const string MatchControllerPath =
        "Assets/Scripts/Networking/NetworkMatchController.cs";

    [Test]
    public void SpawnManager_HasDedicatedRetainedPendingExtractionTracking()
    {
        string source = File.ReadAllText(SpawnManagerPath);

        // NetworkSpawnManager must have collections to store retained pending extraction participants & avatars
        Assert.That(source, Does.Contain("_retainedPendingExtractionParticipants"),
            "NetworkSpawnManager must maintain a dedicated collection for retained pending extraction participants.");
        Assert.That(source, Does.Contain("_retainedPendingExtractionAvatars"),
            "NetworkSpawnManager must maintain a dedicated collection for retained pending extraction avatars.");
    }

    [Test]
    public void OnPlayerLeft_RetainsExtractedPendingParticipantAndAvatar()
    {
        string source = File.ReadAllText(SpawnManagerPath);
        int onPlayerLeftIndex = source.IndexOf("public override void OnPlayerLeft", StringComparison.Ordinal);
        Assert.That(onPlayerLeftIndex, Is.GreaterThanOrEqualTo(0));

        string methodBody = ReadMethod(source, "public override void OnPlayerLeft");

        // The Extracted branch with pending progression must store participant and avatar in retained collections
        Assert.That(methodBody, Does.Contain("_retainedPendingExtractionParticipants"),
            "OnPlayerLeft Extracted branch must retain participant in _retainedPendingExtractionParticipants.");
    }

    [Test]
    public void HasPendingExtractionCommits_ChecksRetainedPendingExtractionParticipants()
    {
        string source = File.ReadAllText(SpawnManagerPath);
        string propertyBody = ReadProperty(source, "public bool HasPendingExtractionCommits");

        // HasPendingExtractionCommits must not only inspect _spawnedPlayers, but also retained disconnected participants
        Assert.That(propertyBody, Does.Contain("_retainedPendingExtractionParticipants"),
            "HasPendingExtractionCommits must check _retainedPendingExtractionParticipants so disconnected extractors gate raid closure.");
    }

    [Test]
    public void IsRetainedResultsObject_ProtectsRetainedPendingExtractionParticipantsAndAvatars()
    {
        string source = File.ReadAllText(SpawnManagerPath);
        string methodBody = ReadMethod(source, "private bool IsRetainedResultsObject");

        // Results world cleanup must protect retained pending extraction objects from despawn
        Assert.That(methodBody, Does.Contain("_retainedPendingExtractionParticipants"),
            "IsRetainedResultsObject must protect _retainedPendingExtractionParticipants from despawning.");
        Assert.That(methodBody, Does.Contain("_retainedPendingExtractionAvatars"),
            "IsRetainedResultsObject must protect _retainedPendingExtractionAvatars from despawning.");
    }

    [Test]
    public void HostMigration_KeepObjectsTerminal_RetainsInPendingExtractionCollections()
    {
        string source = File.ReadAllText(SpawnManagerPath);
        string methodBody = ReadMethod(source, "private void FinalizeUnrecoveredHostMigrationProfiles");

        // Under KeepObjectsTerminal, objects must be added to retained pending extraction collections
        Assert.That(methodBody, Does.Contain("_retainedPendingExtractionParticipants"),
            "FinalizeUnrecoveredHostMigrationProfiles must add unrecovered Extracted participants to _retainedPendingExtractionParticipants.");
    }

    [Test]
    public void MatchController_AwaitingPersistence_HasBoundedDeadline()
    {
        string source = File.ReadAllText(MatchControllerPath);

        // NetworkMatchController must define a bounded persistence deadline
        Assert.That(source, Does.Contain("PersistenceDeadline"),
            "NetworkMatchController must declare a PersistenceDeadline to bound AwaitingPersistence.");

        string advanceClosure = ReadMethod(source, "private void AdvanceClosure");
        Assert.That(advanceClosure, Does.Contain("PersistenceDeadline"),
            "AdvanceClosure must consult PersistenceDeadline before leaving AwaitingPersistence.");
    }

    private static string ReadMethod(string source, string signature)
    {
        int start = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.That(start, Is.GreaterThanOrEqualTo(0), $"Signature '{signature}' not found.");
        int nextMethod = source.IndexOf("\n    private ", start + signature.Length, StringComparison.Ordinal);
        if (nextMethod < 0)
        {
            nextMethod = source.IndexOf("\n    public ", start + signature.Length, StringComparison.Ordinal);
        }
        if (nextMethod < 0)
        {
            nextMethod = source.Length;
        }

        return source.Substring(start, nextMethod - start);
    }

    private static string ReadProperty(string source, string signature)
    {
        int start = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.That(start, Is.GreaterThanOrEqualTo(0), $"Property '{signature}' not found.");
        int nextMember = source.IndexOf("\n    public ", start + signature.Length, StringComparison.Ordinal);
        if (nextMember < 0)
        {
            nextMember = source.IndexOf("\n    internal ", start + signature.Length, StringComparison.Ordinal);
        }
        if (nextMember < 0)
        {
            nextMember = source.IndexOf("\n    private ", start + signature.Length, StringComparison.Ordinal);
        }
        if (nextMember < 0)
        {
            nextMember = source.Length;
        }

        return source.Substring(start, nextMember - start);
    }
}
#endif
