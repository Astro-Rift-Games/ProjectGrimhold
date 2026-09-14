#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using Fusion;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Assert = NUnit.Framework.Assert;

public sealed class TownPartyLocalAuthorityPlayModeTests
{
    private const string SocialPlayerPrefabGuid = "b58bec13d63beb74ca61349f7d983c36";
    private const string TownRaidNpcPrefabGuid = "a9ffa00805946954c9c2e987819f1857";
    private NetworkRunner _runner;

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        LogAssert.ignoreFailingMessages = false;
        if (_runner != null && _runner.IsRunning)
        {
            var shutdown = _runner.Shutdown();
            while (!shutdown.IsCompleted) yield return null;
        }
        if (_runner != null) UnityEngine.Object.DestroyImmediate(_runner.gameObject);
    }

    [UnityTest]
    public IEnumerator LocalPlayer_GetsSoloPartyAndOnlyInputAuthorityShowsPartyHud()
    {
        LogAssert.ignoreFailingMessages = true;
        var runnerObject = new GameObject("TownPartyLocalAuthorityRunner");
        _runner = runnerObject.AddComponent<NetworkRunner>();
        runnerObject.AddComponent<EntityRegistry>();
        runnerObject.AddComponent<LocalInputContext>();
        runnerObject.AddComponent<TownRaidPreparationDirectoryContext>();
        runnerObject.AddComponent<TownPartyDirectoryContext>();
        var joinContext = runnerObject.AddComponent<LocalPlayerJoinContext>();
        var localProfile = new ProfileId("11111111111111111111111111111111");
        joinContext.Initialize(new PlayerJoinData(localProfile));

        var start = _runner.StartGame(new StartGameArgs
        {
            GameMode = GameMode.Single,
            SessionName = $"town-party-{Guid.NewGuid():N}",
            SceneManager = runnerObject.AddComponent<NetworkSceneManagerDefault>(),
            ObjectProvider = runnerObject.AddComponent<NetworkObjectProviderDefault>()
        });
        while (!start.IsCompleted) yield return null;
        Assert.That(start.Result.Ok, Is.True, start.Result.ShutdownReason.ToString());

        NetworkObject npcPrefab = LoadPrefab(TownRaidNpcPrefabGuid);
        NetworkObject playerPrefab = LoadPrefab(SocialPlayerPrefabGuid);
        NetworkObject npc = _runner.Spawn(npcPrefab);
        NetworkObject localPlayer = _runner.Spawn(playerPrefab, Vector3.zero, Quaternion.identity, _runner.LocalPlayer);
        NetworkObject proxy = _runner.Spawn(playerPrefab, Vector3.right, Quaternion.identity, PlayerRef.None);

        TownPartyDirectory directory = npc.GetComponent<TownPartyDirectory>();
        int framesRemaining = 120;
        while (!directory.TryGetParty(localProfile, out _) && framesRemaining-- > 0) yield return null;

        Assert.That(directory.TryGetParty(localProfile, out TownPartySnapshot party), Is.True);
        Assert.That(party.Members, Is.EqualTo(new[] { localProfile }));
        Assert.That(localPlayer.GetComponentInChildren<TownPartyHudView>(true).gameObject.activeSelf, Is.True);
        Assert.That(proxy.GetComponentInChildren<TownPartyHudView>(true).gameObject.activeSelf, Is.False);
    }

    private NetworkObject LoadPrefab(string guid)
    {
        NetworkPrefabId prefabId = _runner.Config.PrefabTable.GetId(NetworkObjectGuid.Parse(guid));
        return _runner.Config.PrefabTable.Load(prefabId, true);
    }
}
#endif
