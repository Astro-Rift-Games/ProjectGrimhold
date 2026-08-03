#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Assert = NUnit.Framework.Assert;

namespace Tests.PlayMode.Extraction
{
    public sealed class ExtractionProgressPlayModeTests
    {
        private const string PlayerGuid = "982f360e5acbdd344a8a75bbc0af94ec";
        private const string EnemyMeleeGuid = "5deca87613df0fa409d98702aec643d4";
        private const string EnemyRangedGuid = "6f7ab2fe6d6193a4ea17a843ff58f94b";
        private const string ContainerGuid = "2c19a78647c64b84a765ff0280706b7d";
        private const string PickupGuid = "5d26f13a358d7894b9419465e4ba1869";

        private NetworkRunner _runner;
        private ExtractionProgressSimulationDriver _driver;
        private bool _previousIgnoreFailingMessages;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;

            var runnerObject = new GameObject("US13 Single Runner");
            _runner = runnerObject.AddComponent<NetworkRunner>();
            runnerObject.AddComponent<EntityRegistry>();
            _driver = runnerObject.AddComponent<ExtractionProgressSimulationDriver>();
            var sceneManager = runnerObject.AddComponent<NetworkSceneManagerDefault>();
            var objectProvider = runnerObject.AddComponent<NetworkObjectProviderDefault>();
            var startTask = _runner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Single,
                SessionName = $"us13-{Guid.NewGuid():N}",
                SceneManager = sceneManager,
                ObjectProvider = objectProvider
            });

            while (!startTask.IsCompleted)
            {
                yield return null;
            }

            Assert.That(startTask.IsFaulted, Is.False, startTask.Exception?.ToString());
            Assert.That(startTask.Result.Ok, Is.True, startTask.Result.ShutdownReason.ToString());
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            LogAssert.ignoreFailingMessages = _previousIgnoreFailingMessages;
            if (_runner != null && _runner.IsRunning)
            {
                _runner.Shutdown();
                while (_runner != null && _runner.IsRunning)
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
        public IEnumerator IndividualProgress_AllowsSameTickContributionsSaturatesAndResetsOnNewInstance()
        {
            NetworkObject player = Spawn(PlayerGuid, Vector3.zero);
            PlayerExtractionProgressController progress = player.GetComponent<PlayerExtractionProgressController>();

            yield return Execute(runner =>
            {
                Assert.That(progress.TryApplyContribution(new ExtractionProgressContribution(
                    ExtractionProgressSourceType.Defeat, new EntityId(501), 10, runner.Tick)), Is.True);
                Assert.That(progress.TryApplyContribution(new ExtractionProgressContribution(
                    ExtractionProgressSourceType.ContainerFirstOpen, new EntityId(502), 5, runner.Tick)), Is.True);
            });

            Assert.That(progress.CurrentProgress, Is.EqualTo(15));
            Assert.That((bool)progress.AssignmentRequested, Is.False);

            yield return Execute(runner => progress.TryApplyContribution(new ExtractionProgressContribution(
                ExtractionProgressSourceType.LootFirstAcquisition, new EntityId(503), long.MaxValue, runner.Tick)));
            Assert.That(progress.CurrentProgress, Is.EqualTo(100));
            Assert.That((bool)progress.AssignmentRequested, Is.True);

            _runner.Despawn(player);
            NetworkObject replacement = Spawn(PlayerGuid, Vector3.right * 2f);
            PlayerExtractionProgressController replacementProgress =
                replacement.GetComponent<PlayerExtractionProgressController>();
            Assert.That(replacementProgress.CurrentProgress, Is.Zero);
            Assert.That((bool)replacementProgress.AssignmentRequested, Is.False);

            DamageResult defeatResult = default;
            bool acceptedAfterDefeat = true;
            yield return Execute(runner =>
            {
                PlayerCharacter replacementCharacter = replacement.GetComponent<PlayerCharacter>();
                defeatResult = replacementCharacter.ApplyDamage(new DamageRequest(
                    new EntityId(900),
                    replacementCharacter.Id,
                    1000f,
                    DamageType.TrueDamage,
                    Vector2.down,
                    replacement.transform.position,
                    runner.Tick));
                acceptedAfterDefeat = replacementProgress.TryApplyContribution(
                    new ExtractionProgressContribution(
                        ExtractionProgressSourceType.Defeat,
                        new EntityId(901),
                        10,
                        runner.Tick));
            });

            Assert.That(defeatResult.IsApplied && defeatResult.IsFatal, Is.True);
            Assert.That(acceptedAfterDefeat, Is.False);
            Assert.That(replacementProgress.CurrentProgress, Is.Zero);
        }

        [UnityTest]
        public IEnumerator FatalDamage_AwardsConfiguredMeleeRangedAndPlayerRewardsOnlyOnce()
        {
            NetworkObject attacker = Spawn(PlayerGuid, Vector3.zero);
            NetworkObject melee = Spawn(EnemyMeleeGuid, Vector3.right * 2f);
            NetworkObject ranged = Spawn(EnemyRangedGuid, Vector3.right * 4f);
            PlayerExtractionProgressController progress = attacker.GetComponent<PlayerExtractionProgressController>();
            DamageResolver resolver = attacker.GetComponent<DamageResolver>();
            DamageResult meleeResult = default;
            DamageResult rangedResult = default;

            yield return Execute(runner =>
            {
                meleeResult = resolver.Resolve(FatalRequest(attacker, melee, runner.Tick));
                rangedResult = resolver.Resolve(FatalRequest(attacker, ranged, runner.Tick));
            });

            Assert.That(meleeResult.IsApplied && meleeResult.IsFatal, Is.True);
            Assert.That(rangedResult.IsApplied && rangedResult.IsFatal, Is.True);
            Assert.That(progress.CurrentProgress, Is.EqualTo(25));

            yield return Execute(runner => resolver.Resolve(FatalRequest(attacker, melee, runner.Tick)));
            Assert.That(progress.CurrentProgress, Is.EqualTo(25));

            NetworkObject victim = Spawn(PlayerGuid, Vector3.left * 3f);
            yield return Execute(runner => resolver.Resolve(FatalRequest(attacker, victim, runner.Tick)));
            Assert.That(progress.CurrentProgress, Is.EqualTo(55));
        }

        [UnityTest]
        public IEnumerator Containers_ResolveFirstOpenAndMixedProvenanceWithoutDuplication()
        {
            NetworkObject player = Spawn(PlayerGuid, Vector3.zero);
            PlayerExtractionProgressController progress = player.GetComponent<PlayerExtractionProgressController>();
            PlayerLootReceiver inventory = player.GetComponent<PlayerLootReceiver>();
            NetworkObject natural = SpawnContainer(new[] { new LootEntry(new LootId("bone"), 4) }, Vector3.right);
            NetworkLootContainer naturalContainer = natural.GetComponent<NetworkLootContainer>();
            NetworkLootContainerInteractable naturalInteractable = natural.GetComponent<NetworkLootContainerInteractable>();

            yield return Execute(runner =>
            {
                InteractionRequest open = new InteractionRequest(player.GetComponent<PlayerCharacter>().Id, naturalContainer.Id, runner.Tick);
                naturalInteractable.Interact(open);
                naturalInteractable.Interact(open);
            });
            Assert.That(progress.CurrentProgress, Is.EqualTo(5));

            NetworkObject empty = SpawnContainer(Array.Empty<LootEntry>(), Vector3.left);
            NetworkLootContainer emptyContainer = empty.GetComponent<NetworkLootContainer>();
            NetworkLootContainerInteractable emptyInteractable = empty.GetComponent<NetworkLootContainerInteractable>();
            yield return Execute(runner =>
            {
                EntityId playerId = player.GetComponent<PlayerCharacter>().Id;
                emptyInteractable.Interact(new InteractionRequest(playerId, emptyContainer.Id, runner.Tick));
                var deposit = new LootTransferRequest(playerId, emptyContainer.Id, new LootId("bone"), 2, runner.Tick);
                Assert.That(emptyContainer.ValidateReceive(deposit), Is.EqualTo(LootTransferFailureReason.None));
                emptyContainer.CommitReceive(deposit);
                emptyInteractable.Interact(new InteractionRequest(playerId, emptyContainer.Id, runner.Tick));
            });
            Assert.That(progress.CurrentProgress, Is.EqualTo(5));

            LootFirstAcquisitionResult rejectedAcquisition = default;
            LootFirstAcquisitionResult firstAcquisition = default;
            LootFirstAcquisitionResult secondAcquisition = default;
            yield return Execute(runner =>
            {
                EntityId playerId = player.GetComponent<PlayerCharacter>().Id;
                var creditedDeposit = new LootTransferRequest(playerId, naturalContainer.Id, new LootId("bone"), 6, runner.Tick);
                Assert.That(naturalContainer.ValidateReceive(creditedDeposit), Is.EqualTo(LootTransferFailureReason.None));
                naturalContainer.CommitReceive(creditedDeposit);

                var request = new LootTransferRequest(naturalContainer.Id, playerId, new LootId("bone"), 3, runner.Tick);
                LootTransferResult rejected = LootTransferTransaction.Execute(
                    naturalContainer, new RejectingReceiver(playerId), request, out rejectedAcquisition);
                Assert.That(rejected.Success, Is.False);
                Assert.That(naturalContainer.GetLootAmount(new LootId("bone")), Is.EqualTo(10));

                Assert.That(LootTransferTransaction.Execute(
                    naturalContainer, inventory, request, out firstAcquisition).Success, Is.True);
                var second = new LootTransferRequest(naturalContainer.Id, playerId, new LootId("bone"), 3, runner.Tick);
                Assert.That(LootTransferTransaction.Execute(
                    naturalContainer, inventory, second, out secondAcquisition).Success, Is.True);
            });

            Assert.That(rejectedAcquisition.EligibleAmount, Is.Zero);
            Assert.That(firstAcquisition.EligibleAmount, Is.EqualTo(3));
            Assert.That(secondAcquisition.EligibleAmount, Is.EqualTo(1));
            Assert.That(naturalContainer.GetLootAmount(new LootId("bone")), Is.EqualTo(4));
        }

        [UnityTest]
        public IEnumerator Pickups_AwardNaturalUnitsAndNeverAwardCreditedDrops()
        {
            NetworkObject player = Spawn(PlayerGuid, Vector3.zero);
            PlayerCharacter character = player.GetComponent<PlayerCharacter>();
            PlayerExtractionProgressController progress = player.GetComponent<PlayerExtractionProgressController>();
            NetworkObject natural = SpawnPickup(new LootEntry(new LootId("bone"), 2), 2, Vector3.right);
            NetworkLootPickup naturalPickup = natural.GetComponent<NetworkLootPickup>();

            yield return Execute(runner => naturalPickup.Interact(
                new InteractionRequest(character.Id, naturalPickup.Id, runner.Tick)));
            Assert.That(progress.CurrentProgress, Is.EqualTo(20));

            NetworkObject credited = SpawnPickup(new LootEntry(new LootId("bone"), 2), 0, Vector3.left);
            NetworkLootPickup creditedPickup = credited.GetComponent<NetworkLootPickup>();
            yield return Execute(runner => creditedPickup.Interact(
                new InteractionRequest(character.Id, creditedPickup.Id, runner.Tick)));
            Assert.That(progress.CurrentProgress, Is.EqualTo(20));
        }

        private IEnumerator Execute(Action<NetworkRunner> action)
        {
            _driver.ClearException();
            _driver.PendingAction = action;
            int frames = 120;
            while (_driver.PendingAction != null && frames-- > 0)
            {
                yield return null;
            }

            Assert.That(_driver.PendingAction, Is.Null);
            Assert.That(_driver.LastException, Is.Null, _driver.LastException?.ToString());
        }

        private NetworkObject Spawn(string guid, Vector3 position)
        {
            NetworkObject prefab = Load(guid);
            NetworkObject spawned = _runner.Spawn(prefab, position, Quaternion.identity, inputAuthority: null);
            Assert.That(spawned, Is.Not.Null);
            return spawned;
        }

        private NetworkObject SpawnContainer(IReadOnlyList<LootEntry> content, Vector3 position)
        {
            NetworkObject prefab = Load(ContainerGuid);
            bool initialized = false;
            NetworkObject spawned = _runner.Spawn(
                prefab, position, Quaternion.identity, inputAuthority: null,
                onBeforeSpawned: (runner, instance) => initialized =
                    instance.GetComponent<NetworkLootContainer>().TrySetInitialContentOverride(runner, instance, content));
            Assert.That(initialized, Is.True);
            return spawned;
        }

        private NetworkObject SpawnPickup(LootEntry entry, int eligibleAmount, Vector3 position)
        {
            NetworkObject prefab = Load(PickupGuid);
            bool initialized = false;
            NetworkObject spawned = _runner.Spawn(
                prefab, position, Quaternion.identity, inputAuthority: null,
                onBeforeSpawned: (runner, instance) => initialized =
                    instance.GetComponent<NetworkLootPickup>().TrySetSpawnContentOverride(
                        runner, instance, entry, true, eligibleAmount));
            Assert.That(initialized, Is.True);
            return spawned;
        }

        private NetworkObject Load(string guid)
        {
            NetworkPrefabId id = _runner.Config.PrefabTable.GetId(NetworkObjectGuid.Parse(guid));
            Assert.That(id.IsValid, Is.True);
            return _runner.Config.PrefabTable.Load(id, true);
        }

        private static DamageRequest FatalRequest(NetworkObject attacker, NetworkObject target, int tick)
        {
            return new DamageRequest(
                attacker.GetComponent<PlayerCharacter>().Id,
                target.GetComponent<ICharacter>().Id,
                1000f,
                DamageType.TrueDamage,
                Vector2.right,
                target.transform.position,
                tick);
        }

        private sealed class RejectingReceiver : ILootReceiver
        {
            public RejectingReceiver(EntityId id) => Id = id;
            public EntityId Id { get; }
            public LootTransferFailureReason ValidateReceive(in LootTransferRequest request) =>
                LootTransferFailureReason.InventoryFull;
            public void CommitReceive(in LootTransferRequest request) =>
                throw new InvalidOperationException("Rejected destination must not commit.");
        }
    }
}
#endif
