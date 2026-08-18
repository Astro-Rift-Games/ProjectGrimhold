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
        private const string ParticipantPrefabGuid = "c39d451563bae6e43934008a0dadc6d6";
        private const string ChestPrefabGuid = "2c19a78647c64b84a765ff0280706b7d";
        private const string EnemyMeleePrefabGuid = "5deca87613df0fa409d98702aec643d4";
        private static readonly LootId BoneLootId = new("bone");
        private static readonly LootId CoinsLootId = new("coins");

        private NetworkRunner _runner;
        private EntityRegistry _registry;
        private PlayerCorpseGenerationSimulationDriver _defeatDriver;
        private EnemyFatalDamageSimulationDriver _enemyDamageDriver;
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
            ClickOccupiedSlot(view.ContainerPanel, BoneLootId);
            Assert.That(transferController.HasRequestInFlight, Is.True);

            ClickOccupiedSlot(view.ContainerPanel, BoneLootId, expectInteractable: false);
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
        public IEnumerator DefeatedPlayer_CorpseAndLootSurviveParticipantDespawn()
        {
            yield return StartRunnerAndSpawnPlayers();

            NetworkObject participantPrefab = LoadPrefab(ParticipantPrefabGuid);
            NetworkObject participantObject = _runner.Spawn(
                participantPrefab,
                Vector3.zero,
                Quaternion.identity,
                inputAuthority: null,
                onBeforeSpawned: (_, instance) =>
                    instance.GetComponent<NetworkRaidParticipant>().Initialize(
                        "unrecovered-defeated-profile",
                        "hm-recovery-generation"));
            NetworkRaidParticipant participant =
                participantObject.GetComponent<NetworkRaidParticipant>();
            RaidAvatarParticipantLink link =
                _defeatedPlayerObject.GetComponent<RaidAvatarParticipantLink>();
            Assert.That(participant.TrySetCurrentAvatar(_defeatedPlayerObject), Is.True);
            link.SetRestoredParticipant(participantObject.Id);

            yield return DefeatSource(new[] { new LootEntry(BoneLootId, 2) });
            Assert.That(participant.State, Is.EqualTo(RaidParticipantState.Defeated));
            NetworkId corpseId = _defeatedPlayerObject.Id;
            EntityId corpseEntityId =
                _defeatedPlayerObject.GetComponent<PlayerCharacter>().Id;

            _runner.Despawn(participantObject);
            yield return null;

            Assert.That(
                _runner.TryFindObject(corpseId, out NetworkObject corpse),
                Is.True);
            Assert.That(corpse, Is.SameAs(_defeatedPlayerObject));
            NetworkLootContainer container = corpse.GetComponent<NetworkLootContainer>();
            Assert.That(container, Is.Not.Null);
            Assert.That((bool)container.IsAvailable, Is.True);
            Assert.That(container.GetLootAmount(BoneLootId), Is.EqualTo(2));
            Assert.That(
                _registry.TryGetInteractable(corpseEntityId, out IInteractable interactable),
                Is.True);
            Assert.That(
                interactable,
                Is.SameAs(corpse.GetComponent<NetworkLootContainerInteractable>()));
            Assert.That(ResolveInteraction(corpseEntityId, out InteractionResult result), Is.True);
            Assert.That(result.Success, Is.True);
        }

        [UnityTest]
        public IEnumerator GeneratedChest_UsesSlotClickAndSharedFullStackTransferFlow()
        {
            yield return StartRunnerAndSpawnPlayers();
            NetworkObject chest = SpawnLootSource(ChestPrefabGuid, new LootEntry(BoneLootId, 2));
            yield return null;

            yield return LootSourceThroughOccupiedSlot(chest);
        }

        [UnityTest]
        public IEnumerator DefeatedEnemy_UsesSlotClickAndSharedFullStackTransferFlow()
        {
            yield return StartRunnerAndSpawnPlayers();
            NetworkObject enemyObject = SpawnLootSource(
                EnemyMeleePrefabGuid,
                new LootEntry(BoneLootId, 2));
            EnemyCharacter enemy = enemyObject.GetComponent<EnemyCharacter>();
            NetworkLootContainer container = enemyObject.GetComponent<NetworkLootContainer>();
            Assert.That(enemy, Is.Not.Null);
            Assert.That(container, Is.Not.Null);
            Assert.That((bool)container.IsAvailable, Is.False);

            _enemyDamageDriver.Target = enemy;
            _enemyDamageDriver.IsRequested = true;
            yield return WaitUntil(
                () => !enemy.IsAlive && container.IsAvailable,
                "The enemy did not expose its existing loot container after fatal damage.");

            yield return LootSourceThroughOccupiedSlot(enemyObject);
        }

        [UnityTest]
        public IEnumerator GeneratedChest_AcceptsFullStackFromOccupiedPlayerSlot()
        {
            yield return StartRunnerAndSpawnPlayers();
            yield return GrantLooterLoot(new LootEntry(BoneLootId, 2));
            NetworkObject chest = SpawnLootSource(ChestPrefabGuid, new LootEntry(BoneLootId, 3));
            yield return null;

            yield return LootDestinationThroughOccupiedPlayerSlot(chest, 5);
        }

        [UnityTest]
        public IEnumerator DefeatedEnemy_AcceptsFullStackFromOccupiedPlayerSlot()
        {
            yield return StartRunnerAndSpawnPlayers();
            yield return GrantLooterLoot(new LootEntry(BoneLootId, 2));
            NetworkObject enemyObject = SpawnLootSource(
                EnemyMeleePrefabGuid,
                new LootEntry(BoneLootId, 3));
            EnemyCharacter enemy = enemyObject.GetComponent<EnemyCharacter>();
            NetworkLootContainer container = enemyObject.GetComponent<NetworkLootContainer>();

            _enemyDamageDriver.Target = enemy;
            _enemyDamageDriver.IsRequested = true;
            yield return WaitUntil(
                () => !enemy.IsAlive && container.IsAvailable,
                "The enemy did not expose its existing loot container after fatal damage.");

            yield return LootDestinationThroughOccupiedPlayerSlot(enemyObject, 5);
        }

        [UnityTest]
        public IEnumerator DefeatedPlayer_DepositTargetsContainerInsteadOfColocatedInventory()
        {
            yield return StartRunnerAndSpawnPlayers();
            yield return GrantLooterLoot(new LootEntry(BoneLootId, 2));
            yield return DefeatSource(new[] { new LootEntry(BoneLootId, 3) });

            PlayerLootReceiver defeatedReceiver = _defeatedPlayerObject.GetComponent<PlayerLootReceiver>();
            yield return LootDestinationThroughOccupiedPlayerSlot(_defeatedPlayerObject, 5);

            Assert.That(defeatedReceiver.GetLootAmount(BoneLootId), Is.Zero);
        }

        [UnityTest]
        public IEnumerator ContainerFull_DepositPreservesBothEndpointsAndShowsTypedFeedback()
        {
            yield return StartRunnerAndSpawnPlayers();
            yield return GrantLooterLoot(new LootEntry(BoneLootId, 2));
            NetworkObject chest = SpawnLootSource(ChestPrefabGuid, new LootEntry(CoinsLootId, 1));
            NetworkLootContainer container = chest.GetComponent<NetworkLootContainer>();
            FieldInfo capacityField = typeof(NetworkLootContainer).GetField(
                "_slotCapacity",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(capacityField, Is.Not.Null);
            capacityField.SetValue(container, 1);

            PlayerLootReceiver receiver = _looterObject.GetComponent<PlayerLootReceiver>();
            PlayerLootTransferNetworkController transferController =
                _looterObject.GetComponent<PlayerLootTransferNetworkController>();
            RaidInventoryPresenter presenter =
                _looterObject.GetComponentInChildren<RaidInventoryPresenter>(true);
            RaidInventoryView view = _looterObject.GetComponentInChildren<RaidInventoryView>(true);
            OpenFromConfirmedInteraction(presenter, container.Id);

            ClickOccupiedSlot(view.PlayerPanel, BoneLootId);
            yield return WaitUntil(
                () => !transferController.HasRequestInFlight &&
                    view.TransferFeedbackText.text == "Contenedor lleno",
                "The authoritative container-full rejection was not presented.");

            Assert.That(receiver.GetLootAmount(BoneLootId), Is.EqualTo(2));
            Assert.That(container.GetLootAmount(BoneLootId), Is.Zero);
            Assert.That(container.GetLootAmount(CoinsLootId), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator InventoryFull_ClickPreservesSourceAndShowsTypedFeedback()
        {
            yield return StartRunnerAndSpawnPlayers();
            PlayerLootReceiver receiver = _looterObject.GetComponent<PlayerLootReceiver>();
            FieldInfo capacityField = typeof(PlayerLootReceiver).GetField(
                "_slotCapacity",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(capacityField, Is.Not.Null);
            capacityField.SetValue(receiver, 1);

            ExpectBasePrefabExtractionProgressValidationError();
            NetworkObject grantDriverTarget = _runner.Spawn(
                _playerPrefab,
                new Vector3(10f, 0f, 0f),
                Quaternion.identity,
                inputAuthority: null);
            _defeatDriver.Target = grantDriverTarget.GetComponent<PlayerCharacter>();
            _defeatDriver.Receiver = receiver;
            _defeatDriver.SetEntries(new[] { new LootEntry(CoinsLootId, 1) });
            _defeatDriver.IsRequested = true;
            yield return WaitUntil(
                () => receiver.GetLootAmount(CoinsLootId) == 1,
                "The receiver could not be filled before the rejection test.");

            NetworkObject chest = SpawnLootSource(ChestPrefabGuid, new LootEntry(BoneLootId, 2));
            NetworkLootContainer container = chest.GetComponent<NetworkLootContainer>();
            PlayerLootTransferNetworkController transferController =
                _looterObject.GetComponent<PlayerLootTransferNetworkController>();
            RaidInventoryPresenter presenter =
                _looterObject.GetComponentInChildren<RaidInventoryPresenter>(true);
            RaidInventoryView view = _looterObject.GetComponentInChildren<RaidInventoryView>(true);
            Assert.That(ResolveInteraction(container.Id, out InteractionResult result), Is.True);
            Assert.That(result.Success, Is.True);
            OpenFromConfirmedInteraction(presenter, container.Id);

            ClickOccupiedSlot(view.ContainerPanel, BoneLootId);
            yield return WaitUntil(
                () => !transferController.HasRequestInFlight &&
                    view.PlayerPanel.CapacityFeedbackText != null &&
                    view.PlayerPanel.CapacityFeedbackText.text == "Lleno" &&
                    view.PlayerPanel.CapacityFeedbackText.gameObject.activeSelf,
                "The authoritative inventory-full rejection was not presented.");

            Assert.That(container.GetLootAmount(BoneLootId), Is.EqualTo(2));
            Assert.That(receiver.GetLootAmount(BoneLootId), Is.Zero);
            Assert.That(receiver.GetLootAmount(CoinsLootId), Is.EqualTo(1));
            Assert.That(view.TransferFeedbackText.gameObject.activeSelf, Is.False);
            Assert.That(view.PlayerPanel.CapacityFeedbackText.text, Is.EqualTo("Lleno"));
        }

        [UnityTest]
        public IEnumerator TakeAll_TransfersVisibleStacksInOrderThroughIndependentRequests()
        {
            yield return StartRunnerAndSpawnPlayers();
            NetworkObject chest = SpawnLootSource(
                ChestPrefabGuid,
                new[]
                {
                    new LootEntry(BoneLootId, 2),
                    new LootEntry(CoinsLootId, 3)
                });
            yield return null;

            NetworkLootContainer container = chest.GetComponent<NetworkLootContainer>();
            PlayerLootReceiver receiver = _looterObject.GetComponent<PlayerLootReceiver>();
            PlayerLootTransferNetworkController transferController =
                _looterObject.GetComponent<PlayerLootTransferNetworkController>();
            RaidInventoryPresenter presenter =
                _looterObject.GetComponentInChildren<RaidInventoryPresenter>(true);
            RaidInventoryView view = _looterObject.GetComponentInChildren<RaidInventoryView>(true);
            var confirmedOrder = new List<LootId>();
            transferController.TransferConfirmed += confirmation =>
            {
                if (confirmation.SourceId == container.Id && confirmation.ResolvedLootId.HasValue)
                {
                    confirmedOrder.Add(confirmation.ResolvedLootId.Value);
                }
            };

            OpenFromConfirmedInteraction(presenter, container.Id);
            Assert.That(view.TakeAllButton.interactable, Is.True);

            int sourceSequence = container.LootChangeSequence;
            int destinationSequence = receiver.LootChangeSequence;
            view.TakeAllButton.onClick.Invoke();

            Assert.That(view.TakeAllButton.interactable, Is.False);
            yield return WaitUntil(
                () => !transferController.HasRequestInFlight &&
                    receiver.GetLootAmount(BoneLootId) == 2 &&
                    receiver.GetLootAmount(CoinsLootId) == 3,
                "Take all did not complete both independent full-stack requests.");
            yield return null;

            Assert.That(confirmedOrder, Is.EqualTo(new[] { BoneLootId, CoinsLootId }));
            Assert.That(container.GetLootAmount(BoneLootId), Is.Zero);
            Assert.That(container.GetLootAmount(CoinsLootId), Is.Zero);
            Assert.That(container.LootChangeSequence, Is.EqualTo(sourceSequence + 2));
            Assert.That(receiver.LootChangeSequence, Is.EqualTo(destinationSequence + 2));
            Assert.That(view.TakeAllButton.interactable, Is.False);
            AssertContainerEmptyState(view.ContainerPanel);
        }

        [UnityTest]
        public IEnumerator TakeAll_InventoryFullRejectionContinuesWithCompatibleStack()
        {
            yield return StartRunnerAndSpawnPlayers();
            PlayerLootReceiver receiver = _looterObject.GetComponent<PlayerLootReceiver>();
            FieldInfo capacityField = typeof(PlayerLootReceiver).GetField(
                "_slotCapacity",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(capacityField, Is.Not.Null);
            capacityField.SetValue(receiver, 1);
            yield return GrantLooterLoot(new LootEntry(CoinsLootId, 1));

            NetworkObject chest = SpawnLootSource(
                ChestPrefabGuid,
                new[]
                {
                    new LootEntry(BoneLootId, 2),
                    new LootEntry(CoinsLootId, 3)
                });
            yield return null;

            NetworkLootContainer container = chest.GetComponent<NetworkLootContainer>();
            PlayerLootTransferNetworkController transferController =
                _looterObject.GetComponent<PlayerLootTransferNetworkController>();
            RaidInventoryPresenter presenter =
                _looterObject.GetComponentInChildren<RaidInventoryPresenter>(true);
            RaidInventoryView view = _looterObject.GetComponentInChildren<RaidInventoryView>(true);
            OpenFromConfirmedInteraction(presenter, container.Id);

            view.TakeAllButton.onClick.Invoke();
            yield return WaitUntil(
                () => !transferController.HasRequestInFlight &&
                    receiver.GetLootAmount(CoinsLootId) == 4,
                "Take all stopped after the inventory-full rejection.");
            yield return null;

            Assert.That(container.GetLootAmount(BoneLootId), Is.EqualTo(2));
            Assert.That(container.GetLootAmount(CoinsLootId), Is.Zero);
            Assert.That(receiver.GetLootAmount(BoneLootId), Is.Zero);
            Assert.That(view.TransferFeedbackText.gameObject.activeSelf, Is.False);
            Assert.That(view.PlayerPanel.CapacityFeedbackText.text, Is.EqualTo("Lleno"));
            Assert.That(view.TakeAllButton.interactable, Is.True);
        }

        [UnityTest]
        public IEnumerator TakeAll_CloseCancelsOnlyRequestsNotYetSent()
        {
            yield return StartRunnerAndSpawnPlayers();
            NetworkObject chest = SpawnLootSource(
                ChestPrefabGuid,
                new[]
                {
                    new LootEntry(BoneLootId, 2),
                    new LootEntry(CoinsLootId, 3)
                });
            yield return null;

            NetworkLootContainer container = chest.GetComponent<NetworkLootContainer>();
            PlayerLootReceiver receiver = _looterObject.GetComponent<PlayerLootReceiver>();
            PlayerLootTransferNetworkController transferController =
                _looterObject.GetComponent<PlayerLootTransferNetworkController>();
            RaidInventoryPresenter presenter =
                _looterObject.GetComponentInChildren<RaidInventoryPresenter>(true);
            RaidInventoryView view = _looterObject.GetComponentInChildren<RaidInventoryView>(true);
            OpenFromConfirmedInteraction(presenter, container.Id);

            view.TakeAllButton.onClick.Invoke();
            Assert.That(transferController.HasRequestInFlight, Is.True);
            presenter.Close();

            yield return WaitUntil(
                () => !transferController.HasRequestInFlight &&
                    receiver.GetLootAmount(BoneLootId) == 2,
                "The accepted take-all request did not complete after closing the UI.");
            yield return null;
            yield return null;

            Assert.That(view.IsOpen, Is.False);
            Assert.That(container.GetLootAmount(BoneLootId), Is.Zero);
            Assert.That(container.GetLootAmount(CoinsLootId), Is.EqualTo(3));
            Assert.That(receiver.GetLootAmount(CoinsLootId), Is.Zero);
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
            _enemyDamageDriver = runnerObject.AddComponent<EnemyFatalDamageSimulationDriver>();
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

            ExpectBasePrefabExtractionProgressValidationError();
            _defeatedPlayerObject = _runner.Spawn(
                _playerPrefab,
                Vector3.zero,
                Quaternion.identity,
                inputAuthority: null);
            ExpectBasePrefabExtractionProgressValidationError();
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

        private IEnumerator GrantLooterLoot(LootEntry entry)
        {
            PlayerLootReceiver receiver = _looterObject.GetComponent<PlayerLootReceiver>();
            ExpectBasePrefabExtractionProgressValidationError();
            NetworkObject grantDriverTarget = _runner.Spawn(
                _playerPrefab,
                new Vector3(10f, 0f, 0f),
                Quaternion.identity,
                inputAuthority: null);
            _defeatDriver.Target = grantDriverTarget.GetComponent<PlayerCharacter>();
            _defeatDriver.Receiver = receiver;
            _defeatDriver.SetEntries(new[] { entry });
            _defeatDriver.IsRequested = true;

            yield return WaitUntil(
                () => receiver.GetLootAmount(entry.LootId) == entry.Amount,
                "The looter inventory could not be prepared for the deposit test.");
        }

        private NetworkObject SpawnLootSource(string prefabGuidValue, LootEntry entry)
        {
            return SpawnLootSource(prefabGuidValue, new[] { entry });
        }

        private NetworkObject SpawnLootSource(
            string prefabGuidValue,
            IReadOnlyList<LootEntry> entries)
        {
            NetworkObject prefab = LoadPrefab(prefabGuidValue);
            bool callbackApplied = false;
            NetworkObject spawned = _runner.Spawn(
                prefab,
                new Vector3(1.5f, 0f, 0f),
                Quaternion.identity,
                inputAuthority: null,
                onBeforeSpawned: (callbackRunner, instance) =>
                {
                    NetworkLootContainer container = instance.GetComponent<NetworkLootContainer>();
                    callbackApplied = container != null && container.TrySetInitialContentOverride(
                        callbackRunner,
                        instance,
                        entries);
                });

            Assert.That(callbackApplied, Is.True);
            Assert.That(spawned, Is.Not.Null);
            Physics2D.SyncTransforms();
            return spawned;
        }

        private IEnumerator LootSourceThroughOccupiedSlot(NetworkObject sourceObject)
        {
            NetworkLootContainer container = sourceObject.GetComponent<NetworkLootContainer>();
            PlayerLootReceiver receiver = _looterObject.GetComponent<PlayerLootReceiver>();
            PlayerLootTransferNetworkController transferController =
                _looterObject.GetComponent<PlayerLootTransferNetworkController>();
            RaidInventoryPresenter presenter =
                _looterObject.GetComponentInChildren<RaidInventoryPresenter>(true);
            RaidInventoryView view = _looterObject.GetComponentInChildren<RaidInventoryView>(true);

            Assert.That(container, Is.Not.Null);
            Assert.That((bool)container.IsAvailable, Is.True);
            Assert.That(ResolveInteraction(container.Id, out InteractionResult result), Is.True);
            Assert.That(result.Success, Is.True);
            OpenFromConfirmedInteraction(presenter, container.Id);
            Assert.That(CountOccupiedSlots(view.ContainerPanel), Is.EqualTo(1));

            ClickOccupiedSlot(view.ContainerPanel, BoneLootId);
            Assert.That(transferController.HasRequestInFlight, Is.True);
            yield return WaitUntil(
                () => !transferController.HasRequestInFlight && receiver.GetLootAmount(BoneLootId) == 2,
                "The shared slot-click transfer did not complete.");
            yield return null;

            Assert.That(container.GetLootAmount(BoneLootId), Is.Zero);
            Assert.That(receiver.GetLootAmount(BoneLootId), Is.EqualTo(2));
            Assert.That(view.IsOpen, Is.True);
            Assert.That(CountOccupiedSlots(view.ContainerPanel), Is.Zero);
            Assert.That(CountOccupiedSlots(view.PlayerPanel), Is.EqualTo(1));
            AssertContainerEmptyState(view.ContainerPanel);
        }

        private IEnumerator LootDestinationThroughOccupiedPlayerSlot(
            NetworkObject destinationObject,
            int expectedContainerAmount)
        {
            NetworkLootContainer container = destinationObject.GetComponent<NetworkLootContainer>();
            PlayerLootReceiver receiver = _looterObject.GetComponent<PlayerLootReceiver>();
            PlayerLootTransferNetworkController transferController =
                _looterObject.GetComponent<PlayerLootTransferNetworkController>();
            RaidInventoryPresenter presenter =
                _looterObject.GetComponentInChildren<RaidInventoryPresenter>(true);
            RaidInventoryView view = _looterObject.GetComponentInChildren<RaidInventoryView>(true);

            Assert.That(container, Is.Not.Null);
            Assert.That((bool)container.IsAvailable, Is.True);
            OpenFromConfirmedInteraction(presenter, container.Id);
            Assert.That(CountOccupiedSlots(view.PlayerPanel), Is.EqualTo(1));

            int containerSequence = container.LootChangeSequence;
            int playerSequence = receiver.LootChangeSequence;
            ClickOccupiedSlot(view.PlayerPanel, BoneLootId);
            Assert.That(transferController.HasRequestInFlight, Is.True);
            ClickOccupiedSlot(view.ContainerPanel, BoneLootId, expectInteractable: false);

            yield return WaitUntil(
                () => !transferController.HasRequestInFlight &&
                    receiver.GetLootAmount(BoneLootId) == 0 &&
                    container.GetLootAmount(BoneLootId) == expectedContainerAmount,
                "The player-to-container full-stack request did not complete.");
            yield return null;

            Assert.That(container.LootChangeSequence, Is.GreaterThan(containerSequence));
            Assert.That(receiver.LootChangeSequence, Is.GreaterThan(playerSequence));
            Assert.That(view.IsOpen, Is.True);
            Assert.That(CountOccupiedSlots(view.PlayerPanel), Is.Zero);
            Assert.That(CountOccupiedSlots(view.ContainerPanel), Is.EqualTo(1));
        }

        private NetworkObject LoadPrefab(string prefabGuidValue)
        {
            NetworkPrefabId prefabId = _runner.Config.PrefabTable.GetId(
                NetworkObjectGuid.Parse(prefabGuidValue));
            Assert.That(prefabId.IsValid, Is.True);
            NetworkObject prefab = _runner.Config.PrefabTable.Load(prefabId, true);
            Assert.That(prefab, Is.Not.Null);
            return prefab;
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

        private static void ClickOccupiedSlot(
            RaidLootPanelView panel,
            LootId expectedLootId,
            bool expectInteractable = true)
        {
            RaidInventorySlotView[] slots =
                panel.GetComponentsInChildren<RaidInventorySlotView>(true);
            for (int index = 0; index < slots.Length; index++)
            {
                RaidInventorySlotView slot = slots[index];
                if (!slot.IsOccupied || slot.LootId != expectedLootId)
                {
                    continue;
                }

                Button button = slot.GetComponent<Button>();
                Assert.That(button, Is.Not.Null);
                Assert.That(button.interactable, Is.EqualTo(expectInteractable));
                button.onClick.Invoke();
                return;
            }

            Assert.Fail($"No occupied slot was found for '{expectedLootId.Value}'.");
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

        private static void ExpectBasePrefabExtractionProgressValidationError()
        {
            LogAssert.Expect(
                UnityEngine.LogType.Error,
                "PlayerExtractionProgressController requires character, extraction controller, registry, assignment service, and valid receiver/reader registrations.");
        }
    }
}
#endif
