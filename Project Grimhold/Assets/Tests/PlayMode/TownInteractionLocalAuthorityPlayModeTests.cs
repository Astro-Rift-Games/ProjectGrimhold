#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using Fusion;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Assert = NUnit.Framework.Assert;

public sealed class TownInteractionLocalAuthorityPlayModeTests
{
    private const string SocialPlayerPrefabGuid = "b58bec13d63beb74ca61349f7d983c36";
    private NetworkRunner _runner;

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        LogAssert.ignoreFailingMessages = false;
        if (_runner != null && _runner.IsRunning)
        {
            var shutdown = _runner.Shutdown();
            while (!shutdown.IsCompleted)
            {
                yield return null;
            }
        }

        if (_runner != null)
        {
            UnityEngine.Object.DestroyImmediate(_runner.gameObject);
        }
    }

    [UnityTest]
    public IEnumerator LocalStateAndInputAuthority_InteractionWithoutTargetPublishesExactlyOnce()
    {
        LogAssert.ignoreFailingMessages = true;
        var runnerObject = new GameObject("TownInteractionLocalAuthorityRunner");
        _runner = runnerObject.AddComponent<NetworkRunner>();
        runnerObject.AddComponent<EntityRegistry>();
        runnerObject.AddComponent<LocalInputContext>();
        var joinContext = runnerObject.AddComponent<LocalPlayerJoinContext>();
        joinContext.Initialize(new PlayerJoinData(
            new ProfileId("11111111111111111111111111111111")));
        var inputDriver = runnerObject.AddComponent<TownInteractionInputDriver>();
        _runner.AddCallbacks(inputDriver);
        _runner.ProvideInput = true;

        var start = _runner.StartGame(new StartGameArgs
        {
            GameMode = GameMode.Single,
            SessionName = $"town-interaction-{Guid.NewGuid():N}",
            SceneManager = runnerObject.AddComponent<NetworkSceneManagerDefault>(),
            ObjectProvider = runnerObject.AddComponent<NetworkObjectProviderDefault>()
        });
        while (!start.IsCompleted)
        {
            yield return null;
        }

        Assert.That(start.Result.Ok, Is.True, start.Result.ShutdownReason.ToString());
        NetworkPrefabId prefabId = _runner.Config.PrefabTable.GetId(NetworkObjectGuid.Parse(SocialPlayerPrefabGuid));
        NetworkObject prefab = _runner.Config.PrefabTable.Load(prefabId, true);
        NetworkObject player = _runner.Spawn(prefab, Vector3.zero, Quaternion.identity, _runner.LocalPlayer);
        PlayerInteractionNetworkController controller = player.GetComponent<PlayerInteractionNetworkController>();
        Assert.That(controller.HasStateAuthority, Is.True);
        Assert.That(controller.HasInputAuthority, Is.True);

        int resultCount = 0;
        InteractionPresentationEvent received = default;
        controller.InteractionResolved += result =>
        {
            resultCount++;
            received = result;
        };

        inputDriver.SendInteraction = true;
        int framesRemaining = 120;
        while (resultCount == 0 && framesRemaining-- > 0)
        {
            yield return null;
        }

        yield return null;
        Assert.That(resultCount, Is.EqualTo(1));
        Assert.That(received.Success, Is.False);
        Assert.That(received.FailureReason, Is.EqualTo(InteractionFailureReason.InvalidTarget));
    }

    private sealed class TownInteractionInputDriver : NetworkRunnerCallbacksAdapter
    {
        public bool SendInteraction { get; set; }

        public override void OnInput(NetworkRunner runner, NetworkInput input)
        {
            PlayerNetworkInput playerInput = default;
            if (SendInteraction)
            {
                playerInput.Buttons.Set(PlayerInputButton.Interact, true);
                SendInteraction = false;
            }

            input.Set(playerInput);
        }
    }
}
#endif
