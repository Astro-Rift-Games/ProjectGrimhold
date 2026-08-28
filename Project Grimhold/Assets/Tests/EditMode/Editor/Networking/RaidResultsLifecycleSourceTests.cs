using System;
using System.IO;
using NUnit.Framework;

public sealed class RaidResultsLifecycleSourceTests
{
    private const string SpawnManagerPath =
        "Assets/Scripts/Networking/NetworkSpawnManager.cs";
    private const string CoordinatorPath =
        "Assets/Scripts/Networking/SessionConnectionCoordinator.cs";
    private const string PresenterPath =
        "Assets/Scripts/Player/Presentation/RaidMenuPresenter.cs";

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

    [Test]
    public void HostCancellationObserver_ArmsOnlyTheAuthoritativeCancellationClosure()
    {
        string observer = ReadMethod(
            File.ReadAllText(CoordinatorPath),
            "private void ObserveHostCancellationClosure");

        Assert.That(observer, Does.Contain("ShouldArmHostCancellationReturn"));
        Assert.That(observer, Does.Contain("_hostCancellationReturnArmed = true"));
        Assert.That(observer, Does.Contain("_hostResultsReturnRequested = true"));
        Assert.That(observer, Does.Not.Contain("ReturnParticipantToTownAsync"));

        string armingRule = ReadMethod(
            File.ReadAllText(CoordinatorPath),
            "internal static bool ShouldArmHostCancellationReturn");
        Assert.That(armingRule, Does.Contain("RaidClosureReason.HostCancellation"));
        Assert.That(armingRule, Does.Contain("SessionConnectionState.Raid"));
    }

    [Test]
    public void ParticipantReturnObserver_ExcludesTheOperationalHost()
    {
        string observer = ReadMethod(
            File.ReadAllText(PresenterPath),
            "private void ObserveParticipantState");

        int guard = observer.IndexOf("ShouldStartParticipantReturn", StringComparison.Ordinal);
        int start = observer.IndexOf("ReturnToTownAsync();", StringComparison.Ordinal);
        Assert.That(guard, Is.GreaterThanOrEqualTo(0));
        Assert.That(start, Is.GreaterThan(guard));
        Assert.That(observer, Does.Contain("TryResolveOperationalLocalRole(out bool isOperationalHost)"));
    }

    [Test]
    public void ParticipantReturn_ReleasesItsLatchOnANonSucceededTransition()
    {
        string transition = ReadMethod(
            File.ReadAllText(PresenterPath),
            "private async void ReturnToTownAsync");

        Assert.That(transition, Does.Contain("SessionTransitionResult.Succeeded"));
        Assert.That(transition, Does.Contain("RestorePresentationAfterFailedReturn"));

        string restore = ReadMethod(
            File.ReadAllText(PresenterPath),
            "private void RestorePresentationAfterFailedReturn");
        Assert.That(restore, Does.Contain("_returnStarted = false"));
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
