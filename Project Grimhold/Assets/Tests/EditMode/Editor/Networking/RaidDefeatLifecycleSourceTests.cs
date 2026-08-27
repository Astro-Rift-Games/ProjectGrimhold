using System;
using System.IO;
using NUnit.Framework;

public sealed class RaidDefeatLifecycleSourceTests
{
    private const string ParticipantPath =
        "Assets/Scripts/Networking/NetworkRaidParticipant.cs";
    private const string SpawnManagerPath =
        "Assets/Scripts/Networking/NetworkSpawnManager.cs";
    private const string MenuPresenterPath =
        "Assets/Scripts/Player/Presentation/RaidMenuPresenter.cs";

    [Test]
    public void DefeatedReturn_RegistersControlledDepartureBeforePublishingAuthorization()
    {
        string rpc = ReadMethod(File.ReadAllText(ParticipantPath), "private void RPC_RequestReturn");

        Assert.That(rpc, Does.Contain("RpcInfo info = default"));
        Assert.That(rpc, Does.Contain("TryResolveReturnRequester"));
        Assert.That(
            rpc.IndexOf("TryRegisterControlledReturn", StringComparison.Ordinal),
            Is.LessThan(rpc.LastIndexOf("IsReturnAuthorized = true", StringComparison.Ordinal)));
    }

    [Test]
    public void ProgressionAck_ConfirmsPersistenceWithoutPublishingReturnAuthorization()
    {
        string acknowledgement = ReadMethod(
            File.ReadAllText(ParticipantPath),
            "internal bool TryConfirmProgressionCommit");

        Assert.That(
            acknowledgement,
            Does.Contain("IsProgressionCommitConfirmed = true"));
        Assert.That(acknowledgement, Does.Not.Contain("IsReturnAuthorized = true"));
    }

    [Test]
    public void ResultsCapture_ReadsResolverOnlyUntilFirstSuccessfulSnapshot()
    {
        string capture = ReadMethod(
            File.ReadAllText(MenuPresenterPath),
            "private bool TryCaptureProgressionResult");

        Assert.That(
            capture.IndexOf("if (_hasProgressionResultSnapshot)", StringComparison.Ordinal),
            Is.LessThan(capture.IndexOf("TryGetProgressionResult", StringComparison.Ordinal)));
        Assert.That(capture, Does.Contain("_progressionResultSnapshot = result"));
        Assert.That(capture, Does.Contain("_hasProgressionResultSnapshot = true"));
    }

    [Test]
    public void PrematureReturn_DoesNotConsumePresenterRequestLatch()
    {
        string request = ReadMethod(
            File.ReadAllText(MenuPresenterPath),
            "private void RequestTerminalReturnOnce");

        Assert.That(
            request.IndexOf("!CanIssueReturnRequest()", StringComparison.Ordinal),
            Is.LessThan(request.IndexOf("_returnRequested = true", StringComparison.Ordinal)));
        Assert.That(
            request.IndexOf("_returnRequested = true", StringComparison.Ordinal),
            Is.LessThan(request.IndexOf("_participant.RequestReturn()", StringComparison.Ordinal)));
    }

    [Test]
    public void PlayerLeft_CapturesSpawnedParticipantIdentityBeforeRoutingCleanup()
    {
        string callback = ReadMethod(
            File.ReadAllText(SpawnManagerPath),
            "public override void OnPlayerLeft");

        int mappingRead = callback.IndexOf("_spawnedPlayers.TryGetValue", StringComparison.Ordinal);
        int profileRead = callback.IndexOf("participant.ProfileId", StringComparison.Ordinal);
        int markerConsumption = callback.IndexOf("_controlledReturns.TryConsume", StringComparison.Ordinal);
        int mappingRemoval = callback.IndexOf("_spawnedPlayers.Remove", StringComparison.Ordinal);

        Assert.That(mappingRead, Is.GreaterThanOrEqualTo(0));
        Assert.That(profileRead, Is.GreaterThan(mappingRead));
        Assert.That(markerConsumption, Is.GreaterThan(profileRead));
        Assert.That(mappingRemoval, Is.GreaterThan(markerConsumption));
        Assert.That(callback, Does.Not.Contain("GetPlayerObject("));
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
