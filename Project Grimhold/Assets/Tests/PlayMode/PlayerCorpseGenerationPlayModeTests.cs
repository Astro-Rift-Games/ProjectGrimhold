#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Assert = NUnit.Framework.Assert;

namespace Tests.PlayMode.Loot
{
    public sealed class PlayerCorpseGenerationPlayModeTests
    {
        private const string PlayerPrefabGuid = "fea3a7b256f965a4eb9b965832939741";
        private NetworkRunner _runner;
        private NetworkObject _playerPrefab;
        private PlayerCorpseGenerationSimulationDriver _driver;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            yield return ShutdownRunner(_runner);
        }

        [UnityTest]
        public IEnumerator EmptyInventory_MakesTheCoLocatedContainerAvailableWithoutSpawningAnotherObject()
        {
            yield return StartRunnerAndLoadPlayer();
            ExpectBasePrefabCombatValidationError();
            NetworkObject playerObject = _runner.Spawn(_playerPrefab, Vector3.zero, Quaternion.identity);
            PlayerCharacter player = playerObject.GetComponent<PlayerCharacter>();
            PlayerLootReceiver receiver = playerObject.GetComponent<PlayerLootReceiver>();
            NetworkLootContainer container = playerObject.GetComponent<NetworkLootContainer>();
            NetworkLootContainerInteractable interactable =
                playerObject.GetComponent<NetworkLootContainerInteractable>();

            AssertAliveContainerState(playerObject, player, receiver, container, interactable);

            _driver.Target = player;
            _driver.Receiver = receiver;
            _driver.SetEntries(Array.Empty<LootEntry>());
            _driver.IsRequested = true;
            yield return WaitForDefeat(player);

            Assert.That(container.Object, Is.SameAs(playerObject));
            Assert.That((bool)container.IsAvailable, Is.True);
            Assert.That(container.IsEmpty, Is.True);
            Assert.That(interactable.Id, Is.EqualTo(player.Id));
            Assert.That(interactable.CanInteract(new InteractionRequest(
                new EntityId(int.MaxValue),
                player.Id,
                _runner.Tick)), Is.True);
            Assert.That(UnityEngine.Object.FindObjectsByType<NetworkObject>(FindObjectsInactive.Exclude), Has.Length.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator MultipleStacks_AreLoadedThenThePlayerInventoryIsClearedOnce()
        {
            yield return StartRunnerAndLoadPlayer();
            ExpectBasePrefabCombatValidationError();
            NetworkObject playerObject = _runner.Spawn(_playerPrefab, Vector3.zero, Quaternion.identity);
            PlayerCharacter player = playerObject.GetComponent<PlayerCharacter>();
            PlayerLootReceiver receiver = playerObject.GetComponent<PlayerLootReceiver>();
            NetworkLootContainer container = playerObject.GetComponent<NetworkLootContainer>();
            NetworkLootContainerInteractable interactable =
                playerObject.GetComponent<NetworkLootContainerInteractable>();
            var entries = new[] { new LootEntry(new LootId("bone"), 2), new LootEntry(new LootId("coins"), 7) };

            AssertAliveContainerState(playerObject, player, receiver, container, interactable);
            Transform body = playerObject.transform.Find("Body");
            Assert.That(body, Is.Not.Null);
            SpriteRenderer[] bodyRenderers = body.GetComponentsInChildren<SpriteRenderer>(true);
            Assert.That(bodyRenderers, Is.Not.Empty);

            _driver.Target = player;
            _driver.Receiver = receiver;
            _driver.SetEntries(entries);
            _driver.RepeatFatalDamage = true;
            _driver.IsRequested = true;
            yield return WaitForDefeat(player);
            yield return null;

            Assert.That(_driver.FirstResult.IsFatal, Is.True);
            Assert.That(_driver.SecondResult.IsFatal, Is.False);
            Assert.That((bool)container.IsAvailable, Is.True);
            Assert.That(container.TryGetLootContent(out IReadOnlyList<LootEntry> content), Is.True);
            Assert.That(content, Is.EquivalentTo(entries));
            Assert.That(receiver.TryGetLootContent(out IReadOnlyList<LootEntry> inventory), Is.True);
            Assert.That(inventory, Is.Empty);
            Assert.That(receiver.LootChangeSequence, Is.EqualTo(entries.Length + 1));
            Assert.That(player.Id, Is.EqualTo(receiver.Id));
            Assert.That(player.Id, Is.EqualTo(container.Id));
            Assert.That(player.Id, Is.EqualTo(interactable.Id));
            Assert.That(player.Object, Is.SameAs(playerObject));
            Assert.That(receiver.Object, Is.SameAs(playerObject));
            Assert.That(container.Object, Is.SameAs(playerObject));
            Assert.That(interactable.Object, Is.SameAs(playerObject));
            Assert.That(interactable.CanInteract(new InteractionRequest(
                new EntityId(int.MaxValue),
                player.Id,
                _runner.Tick)), Is.True);
            Assert.That(body.gameObject.activeSelf, Is.True);
            for (int index = 0; index < bodyRenderers.Length; index++)
            {
                Assert.That(bodyRenderers[index].enabled, Is.True);
            }
        }

        [UnityTest]
        public IEnumerator ClearFailure_RollsBackTheUnavailableContainerAndPreservesInventory()
        {
            yield return StartRunnerAndLoadPlayer();
            ExpectBasePrefabCombatValidationError();
            NetworkObject playerObject = _runner.Spawn(_playerPrefab, Vector3.zero, Quaternion.identity);
            PlayerCharacter player = playerObject.GetComponent<PlayerCharacter>();
            PlayerLootReceiver receiver = playerObject.GetComponent<PlayerLootReceiver>();
            NetworkLootContainer container = playerObject.GetComponent<NetworkLootContainer>();
            PlayerCorpseGenerationController controller = playerObject.GetComponent<PlayerCorpseGenerationController>();
            var entries = new[] { new LootEntry(new LootId("bone"), 4) };

            controller.TestShouldClearPlayerInventory = () => false;
            _driver.Target = player;
            _driver.Receiver = receiver;
            _driver.SetEntries(entries);
            _driver.IsRequested = true;
            LogAssert.ignoreFailingMessages = true;
            yield return WaitForDefeat(player);
            yield return null;
            LogAssert.ignoreFailingMessages = false;

            Assert.That((bool)container.IsAvailable, Is.False);
            Assert.That(container.IsEmpty, Is.True);
            Assert.That(receiver.TryGetLootContent(out IReadOnlyList<LootEntry> inventory), Is.True);
            Assert.That(inventory, Is.EquivalentTo(entries));
        }

        private IEnumerator StartRunnerAndLoadPlayer()
        {
            var runnerObject = new GameObject("PlayerCorpseLootTestRunner");
            _runner = runnerObject.AddComponent<NetworkRunner>();
            runnerObject.AddComponent<EntityRegistry>();
            _driver = runnerObject.AddComponent<PlayerCorpseGenerationSimulationDriver>();
            var startTask = _runner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Single,
                SessionName = $"task36-{Guid.NewGuid():N}",
                SceneManager = runnerObject.AddComponent<NetworkSceneManagerDefault>(),
                ObjectProvider = runnerObject.AddComponent<NetworkObjectProviderDefault>()
            });
            while (!startTask.IsCompleted)
            {
                yield return null;
            }

            Assert.That(startTask.Result.Ok, Is.True, startTask.Result.ShutdownReason.ToString());
            NetworkPrefabId playerId = _runner.Config.PrefabTable.GetId(NetworkObjectGuid.Parse(PlayerPrefabGuid));
            _playerPrefab = _runner.Config.PrefabTable.Load(playerId, true);
        }

        private static IEnumerator WaitForDefeat(PlayerCharacter player)
        {
            int framesRemaining = 120;
            while (player.IsAlive && framesRemaining-- > 0)
            {
                yield return null;
            }

            Assert.That(player.IsAlive, Is.False);
        }

        private void AssertAliveContainerState(
            NetworkObject playerObject,
            PlayerCharacter player,
            PlayerLootReceiver receiver,
            NetworkLootContainer container,
            NetworkLootContainerInteractable interactable)
        {
            Assert.That(playerObject, Is.Not.Null);
            Assert.That(player, Is.Not.Null);
            Assert.That(receiver, Is.Not.Null);
            Assert.That(container, Is.Not.Null);
            Assert.That(interactable, Is.Not.Null);
            Assert.That((bool)container.IsInitialized, Is.True);
            Assert.That((bool)container.IsAvailable, Is.False);
            Assert.That(container.IsEmpty, Is.True);
            Assert.That(interactable.CanInteract(new InteractionRequest(
                new EntityId(int.MaxValue),
                player.Id,
                _runner.Tick)), Is.False);
        }

        private static IEnumerator ShutdownRunner(NetworkRunner runner)
        {
            if (runner != null && runner.IsRunning)
            {
                runner.Shutdown();
                while (runner.IsRunning)
                {
                    yield return null;
                }
            }

            if (runner != null)
            {
                UnityEngine.Object.DestroyImmediate(runner.gameObject);
            }
        }

        private static void ExpectBasePrefabCombatValidationError()
        {
            LogAssert.Expect(
                UnityEngine.LogType.Error,
                "PlayerCombatNetworkController requires a component implementing IAttack.");
        }
    }
}
#endif
