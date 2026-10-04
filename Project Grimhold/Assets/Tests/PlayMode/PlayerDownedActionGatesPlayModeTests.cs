#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Reflection;
using Fusion;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Assert = NUnit.Framework.Assert;

namespace Tests.PlayMode.Downed
{
    /// <summary>
    /// Verifies that Downed players are refused at the combat, consumable, loot and interaction
    /// choke points. Extraction is intentionally not gated.
    /// </summary>
    public sealed class PlayerDownedActionGatesPlayModeTests
    {
        private const string PlayerPrefabGuid = "fea3a7b256f965a4eb9b965832939741";
        private const string MissingExtractionProgressDependenciesMessage =
            "PlayerExtractionProgressController requires character, extraction controller, registry, assignment service, and valid receiver/reader registrations.";

        private NetworkRunner _runner;
        private GateInputDriver _input;
        private NetworkObject _playerObject;
        private PlayerDownedStateNetworkController _downed;

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
        public IEnumerator PrimaryAttack_IsRefusedWhileDowned()
        {
            yield return StartRunnerAndSpawnPlayer();
            PlayerCombatNetworkController combat = _playerObject.GetComponent<PlayerCombatNetworkController>();
            MethodInfo failureReason = typeof(PlayerCombatNetworkController).GetMethod(
                "GetPrimaryAttackFailureReason",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(failureReason, Is.Not.Null);
            yield return WaitFrames(3);
            Assert.That(
                (AttackFailureReason)failureReason.Invoke(combat, null),
                Is.EqualTo(AttackFailureReason.None),
                "Attack must be allowed before Downed.");

            Assert.That(_downed.TryEnterDowned(), Is.True);

            Assert.That(
                (AttackFailureReason)failureReason.Invoke(combat, null),
                Is.EqualTo(AttackFailureReason.ControlDisabled));
        }

        [UnityTest]
        public IEnumerator Interaction_IsRefusedWhileDowned()
        {
            yield return StartRunnerAndSpawnPlayer();
            PlayerInteractionNetworkController interaction =
                _playerObject.GetComponent<PlayerInteractionNetworkController>();
            int resultCount = 0;
            InteractionPresentationEvent received = default;
            interaction.InteractionResolved += result =>
            {
                resultCount++;
                received = result;
            };
            Assert.That(_downed.TryEnterDowned(), Is.True);

            _input.InteractPulse = true;
            yield return WaitUntil(() => resultCount > 0, "No interaction result was published.");

            Assert.That(received.Success, Is.False);
            Assert.That(received.FailureReason, Is.EqualTo(InteractionFailureReason.InteractorUnavailable));
        }

        [UnityTest]
        public IEnumerator Consume_IsRefusedByClientAndAuthorityWhileDowned()
        {
            yield return StartRunnerAndSpawnPlayer();
            PlayerConsumableNetworkController consumable =
                _playerObject.GetComponent<PlayerConsumableNetworkController>();
            Assert.That(_downed.TryEnterDowned(), Is.True);

            var catalog = (LootDefinitionCatalog)typeof(PlayerConsumableNetworkController)
                .GetField("_lootCatalog", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(consumable);
            Assert.That(catalog.TryGetByIndex(0, out LootDefinition anyLoot), Is.True);

            Assert.That(consumable.TryRequestConsume(anyLoot.LootId), Is.False);

            MethodInfo process = typeof(PlayerConsumableNetworkController).GetMethod(
                "ProcessAuthoritativeConsume",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(process, Is.Not.Null);
            var result = (ConsumableResult)process.Invoke(consumable, new object[] { 0 });
            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(ConsumableFailureReason.TargetUnavailable));
        }

        [UnityTest]
        public IEnumerator LootTransfer_IsRefusedByClientAndAuthorityWhileDowned()
        {
            yield return StartRunnerAndSpawnPlayer();
            PlayerLootTransferNetworkController transfer =
                _playerObject.GetComponent<PlayerLootTransferNetworkController>();
            PlayerCharacter player = _playerObject.GetComponent<PlayerCharacter>();
            Assert.That(_downed.TryEnterDowned(), Is.True);

            Assert.That(
                transfer.TryRequestTransfer(
                    new EntityId(9001),
                    player.Id,
                    new LootId("bone"),
                    LootTransferQuantityMode.FullStack),
                Is.False);

            MethodInfo process = typeof(PlayerLootTransferNetworkController).GetMethod(
                "ProcessAuthoritativeRequest",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(process, Is.Not.Null);
            var identity = new LootTransferRequestIdentity(
                1,
                new EntityId(9001),
                player.Id,
                0,
                LootTransferQuantityMode.FullStack);
            var confirmation = (LootTransferConfirmation)process.Invoke(transfer, new object[] { identity });
            Assert.That(confirmation.Result.Success, Is.False);
            Assert.That(confirmation.Result.FailureReason, Is.EqualTo(LootTransferFailureReason.PlayerUnavailable));
        }

        [UnityTest]
        public IEnumerator LootDrop_IsRefusedByClientAndAuthorityWhileDowned()
        {
            yield return StartRunnerAndSpawnPlayer();
            PlayerLootDropNetworkController drop = _playerObject.GetComponent<PlayerLootDropNetworkController>();
            Assert.That(_downed.TryEnterDowned(), Is.True);

            Assert.That(drop.TryRequestDrop(new LootId("bone"), LootTransferQuantityMode.FullStack), Is.False);

            MethodInfo process = typeof(PlayerLootDropNetworkController).GetMethod(
                "ProcessAuthoritativeRequest",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(process, Is.Not.Null);
            var identity = new LootDropRequestIdentity(1, 0, LootTransferQuantityMode.FullStack);
            var confirmation = (LootDropConfirmation)process.Invoke(drop, new object[] { identity });
            Assert.That(confirmation.Result.Success, Is.False);
            Assert.That(confirmation.Result.FailureReason, Is.EqualTo(LootDropFailureReason.PlayerUnavailable));
        }

        private IEnumerator StartRunnerAndSpawnPlayer()
        {
            var runnerObject = new GameObject("PlayerDownedActionGatesRunner");
            _runner = runnerObject.AddComponent<NetworkRunner>();
            runnerObject.AddComponent<EntityRegistry>();
            _input = runnerObject.AddComponent<GateInputDriver>();
            _runner.AddCallbacks(_input);
            _runner.ProvideInput = true;

            var start = _runner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Single,
                SessionName = $"downed-gates-{Guid.NewGuid():N}",
                SceneManager = runnerObject.AddComponent<NetworkSceneManagerDefault>(),
                ObjectProvider = runnerObject.AddComponent<NetworkObjectProviderDefault>()
            });
            while (!start.IsCompleted)
            {
                yield return null;
            }

            Assert.That(start.Result.Ok, Is.True, start.Result.ShutdownReason.ToString());
            NetworkPrefabId prefabId = _runner.Config.PrefabTable.GetId(NetworkObjectGuid.Parse(PlayerPrefabGuid));
            NetworkObject prefab = _runner.Config.PrefabTable.Load(prefabId, true);
            LogAssert.Expect(UnityEngine.LogType.Error, MissingExtractionProgressDependenciesMessage);
            _playerObject = _runner.Spawn(prefab, Vector3.zero, Quaternion.identity, _runner.LocalPlayer);
            _downed = _playerObject.GetComponent<PlayerDownedStateNetworkController>();
            Assert.That(_downed, Is.Not.Null);
            Assert.That(_downed.HasStateAuthority, Is.True);
            LogAssert.ignoreFailingMessages = true;
        }

        private static IEnumerator WaitFrames(int frames)
        {
            for (int i = 0; i < frames; i++)
            {
                yield return null;
            }
        }

        private static IEnumerator WaitUntil(Func<bool> predicate, string failureMessage)
        {
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!predicate() && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(predicate(), Is.True, failureMessage);
        }

        private sealed class GateInputDriver : NetworkRunnerCallbacksAdapter
        {
            public bool InteractPulse { get; set; }

            public override void OnInput(NetworkRunner runner, NetworkInput input)
            {
                PlayerNetworkInput playerInput = default;
                if (InteractPulse)
                {
                    playerInput.Buttons.Set(PlayerInputButton.Interact, true);
                    InteractPulse = false;
                }

                input.Set(playerInput);
            }
        }
    }
}
#endif
