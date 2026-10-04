using System;
using System.IO;
using NUnit.Framework;

public sealed class RaidPlayerDepartureTests
{
    private const string SpawnManagerPath =
        "Assets/Scripts/Networking/NetworkSpawnManager.cs";

    [Test]
    public void RaidingParticipantWithDownedAvatar_IsRetained()
    {
        Assert.That(
            RaidPlayerDeparturePolicy.ShouldRetainDownedRaider(
                RaidParticipantState.Raiding,
                avatarIsDowned: true),
            Is.True);
    }

    [Test]
    public void RaidingParticipantWithHealthyAvatar_IsNotRetained()
    {
        Assert.That(
            RaidPlayerDeparturePolicy.ShouldRetainDownedRaider(
                RaidParticipantState.Raiding,
                avatarIsDowned: false),
            Is.False);
    }

    [TestCase(RaidParticipantState.Extracted)]
    [TestCase(RaidParticipantState.Defeated)]
    [TestCase(RaidParticipantState.Aborted)]
    public void NonRaidingParticipant_IsNeverRetainedAsDownedRaider(RaidParticipantState state)
    {
        Assert.That(
            RaidPlayerDeparturePolicy.ShouldRetainDownedRaider(state, avatarIsDowned: true),
            Is.False);
    }

    [TestCase(RaidParticipantState.Raiding, false)]
    [TestCase(RaidParticipantState.Defeated, true)]
    [TestCase(RaidParticipantState.Extracted, true)]
    [TestCase(RaidParticipantState.Aborted, true)]
    public void RetainedDownedRaider_IsResolvedOnceItLeavesRaiding(
        RaidParticipantState state,
        bool expectedResolved)
    {
        Assert.That(
            RaidPlayerDeparturePolicy.IsRetainedDownedRaiderResolved(state),
            Is.EqualTo(expectedResolved));
    }

    [Test]
    public void OnPlayerLeft_RetainsDownedRaiderBeforeAnyDespawnOrDisconnectFinalization()
    {
        string source = File.ReadAllText(SpawnManagerPath);
        string method = ReadBlock(source, "public override void OnPlayerLeft", "private bool TryResolveHostMigrationProfile");

        int retain = method.IndexOf("ShouldRetainDownedRaider", StringComparison.Ordinal);
        Assert.That(retain, Is.GreaterThanOrEqualTo(0));
        Assert.That(
            retain,
            Is.LessThan(method.IndexOf("runner.Despawn(avatarObject)", StringComparison.Ordinal)));
        Assert.That(
            retain,
            Is.LessThan(method.IndexOf(
                "TryFinalizeDefinitiveDisconnectAfterMaterialClosure",
                StringComparison.Ordinal)));

        string branch = method.Substring(retain, method.IndexOf("return;", retain, StringComparison.Ordinal) - retain);
        Assert.That(branch, Does.Contain("AssignInputAuthority(PlayerRef.None)"));
        Assert.That(branch, Does.Contain("_retainedDownedParticipants"));
        Assert.That(branch, Does.Not.Contain("Despawn"));
    }

    [Test]
    public void RetainedDownedRaider_KeepsRaidOpenAndIsAbortedOnClosure()
    {
        string source = File.ReadAllText(SpawnManagerPath);

        string raiding = ReadBlock(
            source,
            "public bool HasRaidingParticipants",
            "/// <summary>Returns whether any participant still awaits");
        Assert.That(raiding, Does.Contain("_retainedDownedParticipants"));

        string abort = ReadBlock(
            source,
            "public void AbortRaidingParticipantsForClosure",
            "/// <summary>\n    /// Cleans gameplay world state");
        Assert.That(abort, Does.Contain("_retainedDownedParticipants"));
    }

    [Test]
    public void RetainedDownedRaider_DoesNotCountAsConnectedRemoteParticipant()
    {
        string source = File.ReadAllText(SpawnManagerPath);
        string remote = ReadBlock(
            source,
            "public bool HasConnectedRemoteParticipants",
            "internal bool IsResultsReturnPhaseCompatible");

        Assert.That(remote, Does.Not.Contain("_retainedDownedParticipants"));
    }

    private static string ReadBlock(string source, string startMarker, string endMarker)
    {
        string normalized = source.Replace("\r\n", "\n");
        int start = normalized.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.That(start, Is.GreaterThanOrEqualTo(0), startMarker);
        int end = normalized.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
        Assert.That(end, Is.GreaterThan(start), endMarker);
        return normalized.Substring(start, end - start);
    }
}
