using System;
using System.IO;
using NUnit.Framework;

public sealed class RaidDefeatLifecycleSourceTests
{
    private const string ParticipantPath =
        "Assets/Scripts/Networking/NetworkRaidParticipant.cs";
    private const string SpawnManagerPath =
        "Assets/Scripts/Networking/NetworkSpawnManager.cs";

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
