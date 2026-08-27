using System;
using System.IO;
using NUnit.Framework;

public sealed class RaidResultsLifecycleSourceTests
{
    private const string SpawnManagerPath =
        "Assets/Scripts/Networking/NetworkSpawnManager.cs";
    private const string CoordinatorPath =
        "Assets/Scripts/Networking/SessionConnectionCoordinator.cs";

    [Test]
    public void ResultsWorldCleanup_RetainsParticipantRoutingAndIsOneShot()
    {
        string cleanup = ReadMethod(
            File.ReadAllText(SpawnManagerPath),
            "public bool TryCleanupRaidWorldForResults");

        Assert.That(cleanup, Does.Contain("_resultsWorldCleanupAttempted"));
        Assert.That(cleanup, Does.Contain("IsRetainedResultsObject"));
        Assert.That(cleanup, Does.Not.Contain("_spawnedPlayers.Clear"));
        Assert.That(cleanup, Does.Not.Contain("_spawnedAvatars.Clear"));
        Assert.That(cleanup, Does.Not.Contain("_controlledReturns.Clear"));
    }

    [Test]
    public void ConnectedRemoteQuery_UsesRoutingAndRunnerMembership()
    {
        string source = File.ReadAllText(SpawnManagerPath);
        int propertyStart = source.IndexOf(
            "public bool HasConnectedRemoteParticipants",
            StringComparison.Ordinal);
        int nextProperty = source.IndexOf(
            "public NetworkPrefabRef LootContainerPrefab",
            propertyStart,
            StringComparison.Ordinal);
        string property = source.Substring(propertyStart, nextProperty - propertyStart);

        Assert.That(property, Does.Contain("_spawnedPlayers.Keys"));
        Assert.That(property, Does.Contain("_runner.ActivePlayers"));
    }

    [Test]
    public void PlayerLeft_IsAuthoritativeRoutingRemovalBoundary()
    {
        string callback = ReadMethod(
            File.ReadAllText(SpawnManagerPath),
            "public override void OnPlayerLeft");

        Assert.That(callback, Does.Contain("_spawnedPlayers.Remove(player)"));
    }

    [Test]
    public void ControlledReturn_AcceptsFinishedResultsPhase()
    {
        string registration = ReadMethod(
            File.ReadAllText(SpawnManagerPath),
            "internal bool TryRegisterControlledReturn");

        Assert.That(registration, Does.Contain("MatchPhase.Finished"));
    }

    [Test]
    public void ClientReturnRpc_UsesTerminalResultsPolicyAtStateAuthority()
    {
        const string participantPath =
            "Assets/Scripts/Networking/NetworkRaidParticipant.cs";
        string request = ReadMethod(
            File.ReadAllText(participantPath),
            "private void RPC_RequestReturn");

        Assert.That(request, Does.Contain("RaidResultsReturnPolicy.CanRequestClientReturn"));
        Assert.That(request, Does.Contain("TryGetProgressionResult"));
        Assert.That(request, Does.Contain("IsResultsReturnPhaseCompatible"));
    }

    [Test]
    public void HostObserver_RequiresExplicitRequestBeforeStartingReturn()
    {
        string observer = ReadMethod(
            File.ReadAllText(CoordinatorPath),
            "private void ObservePendingHostResultsReturn");

        int requestGuard = observer.IndexOf("if (!_hostResultsReturnRequested)", StringComparison.Ordinal);
        int transition = observer.IndexOf("CompletePendingHostResultsReturnAsync", StringComparison.Ordinal);
        Assert.That(requestGuard, Is.GreaterThanOrEqualTo(0));
        Assert.That(transition, Is.GreaterThan(requestGuard));
        Assert.That(observer, Does.Contain("HasConnectedRemoteParticipants"));
        Assert.That(observer, Does.Contain("ResetHostResultsReturn()"));
    }

    private static string ReadMethod(string source, string signature)
    {
        int start = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.That(start, Is.GreaterThanOrEqualTo(0));
        int nextMethod = source.IndexOf("\n    private ", start + signature.Length, StringComparison.Ordinal);
        if (nextMethod < 0)
        {
            nextMethod = source.Length;
        }

        return source.Substring(start, nextMethod - start);
    }
}
