#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Fusion;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Assert = NUnit.Framework.Assert;

namespace Tests.PlayMode.Loot
{
    public sealed class DefeatedPlayerLootIntegrationPlayModeTests
    {
        private const string PlayerPrefabGuid = "fea3a7b256f965a4eb9b965832939741";
        private static readonly LootId BoneLootId = new("bone");

        private NetworkRunner _runner;
        private EntityRegistry _registry;
        private PlayerCorpseGenerationSimulationDriver _defeatDriver;
        private PlayerInputReader _inputReader;
        private GameObject _inputReaderObject;
        private NetworkObject _playerPrefab;
        private NetworkObject _defeatedPlayerObject;
        private NetworkObject _looterObject;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_runner != null && _runner.IsRunning)
            {
                _runner.Shutdown();
                while (_runner.IsRunning)
                {
                    yield return null;
                }
            }

            if (_runner != null)
            {
                UnityEngine.Object.DestroyImmediate(_runner.gameObject);
            }

            if (_inputReaderObject != null)
            {
                UnityEngine.Object.DestroyImmediate(_inputReaderObject);
            }
        }

        [UnityTest]
        public IEnumerator DefeatedPlayer_UsesExistingInteractionUiAndFullStackTransferFlow()
        {
            yield return StartRunnerAndSpawnPlayers();
            yield return DefeatSource(new[] { new LootEntry(BoneLootId, 2) });

            PlayerCharacter defeatedPlayer = _defeatedPlayerObject.GetComponent<PlayerCharacter>();
            NetworkLootContainer container = _defeatedPlayerObject.GetComponent<NetworkLootContainer>();
            PlayerLootReceiver looterReceiver = _looterObject.GetComponent<PlayerLootReceiver>();
            PlayerLootTransferNetworkController transferController =
                _looterObject.GetComponent<PlayerLootTransferNetworkController>();
            RaidInventoryPresenter presenter =
                _looterObject.GetComponentInChildren<RaidInventoryPresenter>(true);
            RaidInventoryView view = _looterObject.GetComponentInChildren<RaidInventoryView>(true);

            Assert.That(ResolveInteraction(defeatedPlayer.Id, out InteractionResult interactionResult), Is.True);
            Assert.That(interactionResult.Success, Is.True);
            OpenFromConfirmedInteraction(presenter, defeatedPlayer.Id);

            Assert.That(ReadPresenterModeName(presenter), Is.EqualTo("ContainerLoot"));
            Assert.That(view.IsOpen, Is.True);
            Assert.That(CountOccupiedSlots(view.ContainerPanel), Is.EqualTo(1));
            Assert.That(CountOccupiedSlots(view.PlayerPanel), Is.Zero);

            int sourceSequenceBeforeTransfer = container.LootChangeSequence;
            int receiverSequenceBeforeTransfer = looterReceiver.LootChangeSequence;
            Assert.That(transferController.TryRequestFullStack(container.Id, BoneLootId), Is.True);
            Assert.That(transferController.HasRequestInFlight, Is.True);

            yield return WaitUntil(
                () => !transferController.HasRequestInFlight &&
                    looterReceiver.GetLootAmount(BoneLootId) == 2,
                "The existing full-stack request did not complete.");
            yield return null;

            Assert.That(container.GetLootAmount(BoneLootId), Is.Zero);
            Assert.That(looterReceiver.GetLootAmount(BoneLootId), Is.EqualTo(2));
            Assert.That(container.LootChangeSequence, Is.GreaterThan(sourceSequenceBeforeTransfer));
            Assert.That(looterReceiver.LootChangeSequence, Is.GreaterThan(receiverSequenceBeforeTransfer));
            Assert.That(transferController.HasRequestInFlight, Is.False);
            Assert.That((bool)container.IsAvailable, Is.True);
            Assert.That(_runner.TryFindObject(_defeatedPlayerObject.Id, out NetworkObject resolved), Is.True);
            Assert.That(resolved, Is.SameAs(_defeatedPlayerObject));
            Assert.That(ReadPresenterModeName(presenter), Is.EqualTo("ContainerLoot"));
            Assert.That(view.IsOpen, Is.True);
            Assert.That(CountOccupiedSlots(view.ContainerPanel), Is.Zero);
            Assert.That(CountOccupiedSlots(view.PlayerPanel), Is.EqualTo(1));
            AssertContainerEmptyState(view.ContainerPanel);
        }

        [UnityTest]
        public IEnumerator DefeatedPlayer_MovingOutOfRangeClosesExistingScreen()
        {
            yield return StartRunnerAndSpawnPlayers();
            yield return DefeatSource(Array.Empty<LootEntry>());

            RaidInventoryPresenter presenter =
                _looterObject.GetComponentInChildren<RaidInventoryPresenter>(true);
            RaidInventoryView view = _looterObject.GetComponentInChildren<RaidInventoryView>(true);
            EntityId sourceId = _defeatedPlayerObject.GetComponent<PlayerCharacter>().Id;
            OpenFromConfirmedInteraction(presenter, sourceId);
            Assert.That(view.IsOpen, Is.True);

            _looterObject.transform.position = new Vector3(20f, 20f, 0f);
            Physics2D.SyncTransforms();
            yield return null;

            Assert.That(view.IsOpen, Is.False);
            Assert.That(ReadPresenterMode(presenter), Is.Zero);
            Assert.That(ReadSuppressionCount(_inputReader), Is.Zero);
        }

        [UnityTest]
        public IEnumerator DefeatedPlayer_DespawnClosesScreenAndUnregistersLootComposition()
        {
            yield return StartRunnerAndSpawnPlayers();
            yield return DefeatSource(Array.Empty<LootEntry>());

            RaidInventoryPresenter presenter =
                _looterObject.GetComponentInChildren<RaidInventoryPresenter>(true);
            RaidInventoryView view = _looterObject.GetComponentInChildren<RaidInventoryView>(true);
            EntityId sourceId = _defeatedPlayerObject.GetComponent<PlayerCharacter>().Id;
            Collider2D[] interactionColliders = FindInteractionColliders(_defeatedPlayerObject);
            OpenFromConfirmedInteraction(presenter, sourceId);

            Assert.That(view.IsOpen, Is.True);
            Assert.That(ReadSuppressionCount(_inputReader), Is.EqualTo(1));
            Assert.That(_registry.TryGetLootSource(sourceId, out _, out _), Is.True);
            Assert.That(_registry.TryGetInteractable(sourceId, out _), Is.True);

            _runner.Despawn(_defeatedPlayerObject);
            yield return null;

            Assert.That(view.IsOpen, Is.False);
            Assert.That(ReadPresenterMode(presenter), Is.Zero);
            Assert.That(ReadSuppressionCount(_inputReader), Is.Zero);
            Assert.That(_registry.TryGetLootSource(sourceId, out _, out _), Is.False);
            Assert.That(_registry.TryGetInteractable(sourceId, out _), Is.False);
            for (int index = 0; index < interactionColliders.Length; index++)
            {
                Assert.That(_registry.TryGetEntityId(interactionColliders[index], out _), Is.False);
            }
        }

        [UnityTest]
        public IEnumerator ShutdownWithDefeatedPlayerScreenOpen_ReleasesLocalPresentationState()
        {
            yield return StartRunnerAndSpawnPlayers();
            yield return DefeatSource(Array.Empty<LootEntry>());

            RaidInventoryPresenter presenter =
                _looterObject.GetComponentInChildren<RaidInventoryPresenter>(true);
            RaidInventoryView view = _looterObject.GetComponentInChildren<RaidInventoryView>(true);
            EntityId sourceId = _defeatedPlayerObject.GetComponent<PlayerCharacter>().Id;
            OpenFromConfirmedInteraction(presenter, sourceId);

            Assert.That(view.IsOpen, Is.True);
            Assert.That(ReadSuppressionCount(_inputReader), Is.EqualTo(1));

            _runner.Shutdown();
            while (_runner.IsRunning)
            {
                yield return null;
            }
            yield return null;

            Assert.That(presenter == null || ReadPresenterMode(presenter) == 0, Is.True);
            Assert.That(view == null || !view.IsOpen, Is.True);
            Assert.That(ReadSuppressionCount(_inputReader), Is.Zero);
        }

        private IEnumerator StartRunnerAndSpawnPlayers()
        {
            var runnerObject = new GameObject("DefeatedPlayerLootIntegrationRunner");
            _runner = runnerObject.AddComponent<NetworkRunner>();
            _registry = runnerObject.AddComponent<EntityRegistry>();
            _defeatDriver = runnerObject.AddComponent<PlayerCorpseGenerationSimulationDriver>();
            LocalInputContext inputContext = runnerObject.AddComponent<LocalInputContext>();

            _inputReaderObject = new GameObject("DefeatedPlayerLootInputReader");
            _inputReader = _inputReaderObject.AddComponent<PlayerInputReader>();
            Assert.That(inputContext.TryRegister(_inputReader), Is.True);

            var startTask = _runner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Single,
                SessionName = $"defeated-player-loot-{Guid.NewGuid():N}",
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

            ExpectBasePrefabCombatValidationError();
            _defeatedPlayerObject = _runner.Spawn(
                _playerPrefab,
                Vector3.zero,
                Quaternion.identity,
                inputAuthority: null);
            ExpectBasePrefabCombatValidationError();
            _looterObject = _runner.Spawn(
                _playerPrefab,
                new Vector3(1f, 0f, 0f),
                Quaternion.identity,
                _runner.LocalPlayer);
            Assert.That(_defeatedPlayerObject, Is.Not.Null);
            Assert.That(_looterObject, Is.Not.Null);
            yield return null;

            Assert.That(_looterObject.HasInputAuthority, Is.True);
            Assert.That(
                _looterObject.GetComponentInChildren<RaidInventoryPresenter>(true),
                Is.Not.Null);
        }

        private IEnumerator DefeatSource(IReadOnlyList<LootEntry> entries)
        {
            PlayerCharacter player = _defeatedPlayerObject.GetComponent<PlayerCharacter>();
            PlayerLootReceiver receiver = _defeatedPlayerObject.GetComponent<PlayerLootReceiver>();
            NetworkLootContainer container = _defeatedPlayerObject.GetComponent<NetworkLootContainer>();

            _defeatDriver.Target = player;
            _defeatDriver.Receiver = receiver;
            _defeatDriver.SetEntries(entries);
            _defeatDriver.IsRequested = true;

            yield return WaitUntil(
                () => !player.IsAlive && container.IsAvailable,
                "The source player did not complete its defeat loot conversion.");
            Physics2D.SyncTransforms();
        }

        private bool ResolveInteraction(EntityId targetId, out InteractionResult result)
        {
            PlayerCharacter looter = _looterObject.GetComponent<PlayerCharacter>();
            Physics2DInteractionTargetQuery query =
                _looterObject.GetComponent<Physics2DInteractionTargetQuery>();
            var targetQuery = new InteractionTargetQuery(
                looter.Id,
                _looterObject.transform.position,
                2f,
                1 << 8);
            IReadOnlyList<InteractionTarget> candidates = query.FindTargets(targetQuery);
            bool resolved = InteractionResolver.TryResolve(
                looter.Id,
                _runner.Tick,
                2f,
                candidates,
                _registry.TryGetInteractable,
                out InteractionRequest request,
                out result);

            Assert.That(request.TargetId, Is.EqualTo(targetId));
            return resolved;
        }

        private void OpenFromConfirmedInteraction(
            RaidInventoryPresenter presenter,
            EntityId targetId)
        {
            PlayerLootReceiver looterReceiver = _looterObject.GetComponent<PlayerLootReceiver>();
            PlayerInteractionNetworkController interactionController =
                _looterObject.GetComponent<PlayerInteractionNetworkController>();
            var interactionEvent = new InteractionPresentationEvent(
                interactionController.CurrentInteractionSequence + 1,
                looterReceiver.Id,
                targetId,
                _runner.Tick,
                true,
                false,
                InteractionFailureReason.None);

            MethodInfo method = typeof(RaidInventoryPresenter).GetMethod(
                "OnInteractionResolved",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(presenter, new object[] { interactionEvent });
        }

        private static IEnumerator WaitUntil(Func<bool> predicate, string failureMessage)
        {
            int framesRemaining = 180;
            while (!predicate() && framesRemaining-- > 0)
            {
                yield return null;
            }

            Assert.That(predicate(), Is.True, failureMessage);
        }

        private static int CountOccupiedSlots(RaidLootPanelView panel)
        {
            RaidInventorySlotView[] slots =
                panel.GetComponentsInChildren<RaidInventorySlotView>(true);
            int occupied = 0;
            for (int index = 0; index < slots.Length; index++)
            {
                if (slots[index].IsOccupied)
                {
                    occupied++;
                }
            }

            return occupied;
        }

        private static void AssertContainerEmptyState(RaidLootPanelView panel)
        {
            FieldInfo field = typeof(RaidLootPanelView).GetField(
                "_emptyRoot",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            GameObject emptyRoot = field.GetValue(panel) as GameObject;
            Assert.That(emptyRoot, Is.Not.Null);
            Assert.That(emptyRoot.activeSelf, Is.True);
            TMP_Text label = emptyRoot.GetComponentInChildren<TMP_Text>(true);
            Assert.That(label, Is.Not.Null);
            Assert.That(label.text, Is.EqualTo("Contenedor vacío"));

            RaidInventorySlotView[] slots =
                panel.GetComponentsInChildren<RaidInventorySlotView>(true);
            Assert.That(slots, Is.Not.Empty);
            for (int index = 0; index < slots.Length; index++)
            {
                Assert.That(slots[index].IsOccupied, Is.False);
                Button button = slots[index].GetComponentInChildren<Button>(true);
                Assert.That(button, Is.Not.Null);
                Assert.That(button.interactable, Is.False);
            }
        }

        private static Collider2D[] FindInteractionColliders(NetworkObject networkObject)
        {
            Collider2D[] allColliders = networkObject.GetComponentsInChildren<Collider2D>(true);
            var interactionColliders = new List<Collider2D>();
            for (int index = 0; index < allColliders.Length; index++)
            {
                Collider2D collider = allColliders[index];
                if (collider.gameObject.layer == 8 && collider.isTrigger)
                {
                    interactionColliders.Add(collider);
                }
            }

            Assert.That(interactionColliders, Is.Not.Empty);
            return interactionColliders.ToArray();
        }

        private static int ReadPresenterMode(RaidInventoryPresenter presenter)
        {
            FieldInfo field = typeof(RaidInventoryPresenter).GetField(
                "_mode",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return Convert.ToInt32(field.GetValue(presenter));
        }

        private static string ReadPresenterModeName(RaidInventoryPresenter presenter)
        {
            FieldInfo field = typeof(RaidInventoryPresenter).GetField(
                "_mode",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return field.GetValue(presenter)?.ToString();
        }

        private static int ReadSuppressionCount(PlayerInputReader reader)
        {
            FieldInfo field = typeof(PlayerInputReader).GetField(
                "_gameplaySuppressionCount",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return (int)field.GetValue(reader);
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
