#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Fusion;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
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
        private const string SanctuaryPrefabPath = "Assets/Prefabs/ExtractionSanctuary.prefab";

        private NetworkRunner _runner;
        private ExtractionProgressSimulationDriver _driver;
        private bool _previousIgnoreFailingMessages;
        private readonly List<ExtractionConfig> _ritualTestConfigs = new();

        private static readonly FieldInfo SanctuaryConfigField =
            typeof(ExtractionSanctuary).GetField("_config", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo RitualDurationField =
            typeof(ExtractionConfig).GetField("_ritualDurationSeconds", BindingFlags.Instance | BindingFlags.NonPublic);

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;

            var runnerObject = new GameObject("US13 Single Runner");
            _runner = runnerObject.AddComponent<NetworkRunner>();
            runnerObject.AddComponent<EntityRegistry>();
            runnerObject.AddComponent<LocalInputContext>();
            ExtractionSanctuaryAssignmentService assignmentService =
                runnerObject.AddComponent<ExtractionSanctuaryAssignmentService>();
            Assert.That(assignmentService.Initialize(_runner, GameMode.Host), Is.True);
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

            for (int i = 0; i < _ritualTestConfigs.Count; i++)
            {
                UnityEngine.Object.DestroyImmediate(_ritualTestConfigs[i]);
            }

            _ritualTestConfigs.Clear();
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

            LogAssert.Expect(UnityEngine.LogType.Error,
                "ExtractionSanctuaryAssignmentService configuration failure: NoSanctuariesConfigured.");
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
        public IEnumerator QuotaCompletion_AssignsDistinctSanctuariesAndRepeatedRequestIsIdempotent()
        {
            NetworkObject firstSanctuary = SpawnSanctuary(Vector3.left * 4f);
            NetworkObject secondSanctuary = SpawnSanctuary(Vector3.right * 4f);
            NetworkObject firstPlayer = Spawn(PlayerGuid, Vector3.zero);
            NetworkObject secondPlayer = Spawn(PlayerGuid, Vector3.up * 2f);
            PlayerExtractionProgressController firstProgress =
                firstPlayer.GetComponent<PlayerExtractionProgressController>();
            PlayerExtractionProgressController secondProgress =
                secondPlayer.GetComponent<PlayerExtractionProgressController>();
            ExtractionSanctuaryAssignmentService service =
                _runner.GetComponent<ExtractionSanctuaryAssignmentService>();

            yield return Execute(runner =>
            {
                Assert.That(firstProgress.TryApplyContribution(new ExtractionProgressContribution(
                    ExtractionProgressSourceType.Defeat,
                    new EntityId(7001),
                    long.MaxValue,
                    runner.Tick)), Is.True);
                Assert.That(secondProgress.TryApplyContribution(new ExtractionProgressContribution(
                    ExtractionProgressSourceType.Defeat,
                    new EntityId(7002),
                    long.MaxValue,
                    runner.Tick)), Is.True);
            });

            SanctuaryAssignmentResult first = service.TryGetAssignment(firstProgress.Id);
            SanctuaryAssignmentResult second = service.TryGetAssignment(secondProgress.Id);
            Assert.That(first.Success, Is.True);
            Assert.That(second.Success, Is.True);
            Assert.That(first.SanctuaryId, Is.Not.EqualTo(second.SanctuaryId));
            Assert.That(new[] { firstSanctuary.GetComponent<ExtractionSanctuary>().Id,
                secondSanctuary.GetComponent<ExtractionSanctuary>().Id }, Does.Contain(first.SanctuaryId));

            SanctuaryAssignmentResult repeated = default;
            yield return Execute(runner =>
            {
                PlayerCharacter character = firstPlayer.GetComponent<PlayerCharacter>();
                character.ApplyDamage(new DamageRequest(
                    secondProgress.Id,
                    character.Id,
                    1000f,
                    DamageType.TrueDamage,
                    Vector2.zero,
                    character.transform.position,
                    runner.Tick));
                repeated = service.TryAssign(firstProgress.Id);
            });

            Assert.That(repeated.Success, Is.True);
            Assert.That(repeated.IsExistingAssignment, Is.True);
            Assert.That(repeated.SanctuaryId, Is.EqualTo(first.SanctuaryId));
        }

        [UnityTest]
        public IEnumerator QuotaCompletion_WithoutSanctuariesPreservesProgressWithoutPartialAssignment()
        {
            NetworkObject player = Spawn(PlayerGuid, Vector3.zero);
            PlayerExtractionProgressController progress = player.GetComponent<PlayerExtractionProgressController>();
            ExtractionSanctuaryAssignmentService service =
                _runner.GetComponent<ExtractionSanctuaryAssignmentService>();
            SanctuaryAssignmentResult result = default;

            LogAssert.Expect(UnityEngine.LogType.Error,
                "ExtractionSanctuaryAssignmentService configuration failure: NoSanctuariesConfigured.");
            yield return Execute(runner =>
            {
                Assert.That(progress.TryApplyContribution(new ExtractionProgressContribution(
                    ExtractionProgressSourceType.Defeat,
                    new EntityId(7101),
                    long.MaxValue,
                    runner.Tick)), Is.True);
                result = service.TryAssign(progress.Id);
            });

            Assert.That(progress.CurrentProgress, Is.EqualTo(progress.Quota));
            Assert.That((bool)progress.AssignmentRequested, Is.True);
            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(SanctuaryAssignmentFailureReason.NoSanctuariesConfigured));
            Assert.That(service.TryGetAssignment(progress.Id).FailureReason,
                Is.EqualTo(SanctuaryAssignmentFailureReason.AssignmentNotFound));
        }

        [UnityTest]
        public IEnumerator GameplayScene_FusionSpawnsFourDistinctSanctuaryIdentities()
        {
            SceneRef gameplayScene = SceneRef.FromIndex(1);
            NetworkSceneAsyncOp load = _runner.LoadScene(gameplayScene, LoadSceneMode.Additive);
            while (!load.IsDone)
            {
                yield return null;
            }

            Assert.That(load.Error, Is.Null);
            yield return null;

            ExtractionSanctuary[] sanctuaries = UnityEngine.Object.FindObjectsByType<ExtractionSanctuary>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            Assert.That(sanctuaries.Length, Is.EqualTo(4));

            var identities = new HashSet<EntityId>();
            for (int i = 0; i < sanctuaries.Length; i++)
            {
                Assert.That(sanctuaries[i].Id.Value, Is.Not.Zero);
                Assert.That(identities.Add(sanctuaries[i].Id), Is.True);
            }

            NetworkSceneAsyncOp unload = _runner.UnloadScene(gameplayScene);
            while (!unload.IsDone)
            {
                yield return null;
            }

            Assert.That(unload.Error, Is.Null);
            EntityRegistry registry = _runner.GetComponent<EntityRegistry>();
            foreach (EntityId identity in identities)
            {
                Assert.That(registry.TryGetExtractionSanctuary(identity, out _), Is.False);
            }

            Assert.That(_runner.GetComponent<ExtractionSanctuaryAssignmentService>()
                .TryGetAssignment(new EntityId(987654321)).FailureReason,
                Is.EqualTo(SanctuaryAssignmentFailureReason.AssignmentNotFound));
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

        [UnityTest]
        public IEnumerator Ritual_OwnerIsPredictiveCandidateAndCompletionPermanentlyEnablesOwnZone()
        {
            NetworkObject sanctuaryObject = SpawnRitualSanctuary(Vector3.zero, 0.05f);
            NetworkObject ownerObject = Spawn(PlayerGuid, Vector3.zero);
            NetworkObject rivalObject = Spawn(PlayerGuid, Vector3.right);
            ExtractionSanctuary sanctuary = sanctuaryObject.GetComponent<ExtractionSanctuary>();
            ExtractionZone zone = sanctuaryObject.GetComponent<ExtractionZone>();
            PlayerExtractionProgressController ownerProgress =
                ownerObject.GetComponent<PlayerExtractionProgressController>();
            PlayerExtractionController ownerExtraction =
                ownerObject.GetComponent<PlayerExtractionController>();
            PlayerExtractionController rivalExtraction =
                rivalObject.GetComponent<PlayerExtractionController>();
            EntityId ownerId = ownerObject.GetComponent<PlayerCharacter>().Id;
            EntityId rivalId = rivalObject.GetComponent<PlayerCharacter>().Id;

            yield return Execute(runner => ownerProgress.TryApplyContribution(
                new ExtractionProgressContribution(
                    ExtractionProgressSourceType.Defeat,
                    new EntityId(8101),
                    long.MaxValue,
                    runner.Tick)));

            Assert.That(sanctuary.IsOwnedBy(ownerId), Is.True);
            Assert.That(zone.IsAvailable, Is.False);
            Assert.That(sanctuary.TryGetRitualProgress(out ExtractionRitualSnapshot notStarted), Is.True);
            Assert.That(notStarted.State, Is.EqualTo(ExtractionRitualState.NotStarted));
            Assert.That(notStarted.TotalSeconds, Is.EqualTo(0.05f));
            Assert.That(notStarted.RemainingSeconds, Is.EqualTo(0.05f));
            Assert.That(notStarted.Progress, Is.Zero);
            Assert.That(sanctuary.CanInteract(new InteractionRequest(ownerId, sanctuary.Id, _runner.Tick)), Is.True);
            Assert.That(sanctuary.CanInteract(new InteractionRequest(rivalId, sanctuary.Id, _runner.Tick)), Is.False);

            bool prematureExtraction = true;
            yield return Execute(_ => prematureExtraction = ownerExtraction.TryBeginExtraction(zone.Id));
            Assert.That(prematureExtraction, Is.False);

            InteractionResult result = default;
            yield return Execute(runner => result = sanctuary.Interact(
                new InteractionRequest(ownerId, sanctuary.Id, runner.Tick)));
            Assert.That(result.Success, Is.True);
            Assert.That(sanctuary.RitualState, Is.EqualTo(ExtractionRitualState.InProgress));
            Assert.That(sanctuary.TryGetRitualProgress(out ExtractionRitualSnapshot running), Is.True);
            Assert.That(running.TotalSeconds, Is.EqualTo(0.05f));

            float deadline = Time.realtimeSinceStartup + 2f;
            while (sanctuary.RitualState != ExtractionRitualState.Completed &&
                Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(sanctuary.RitualState, Is.EqualTo(ExtractionRitualState.Completed));
            Assert.That(zone.IsAvailable, Is.True);
            Assert.That(sanctuary.TryGetRitualProgress(out ExtractionRitualSnapshot completed), Is.True);
            Assert.That(completed.RemainingSeconds, Is.Zero);
            Assert.That(completed.Progress, Is.EqualTo(1f));
            Assert.That(sanctuary.CanUseExtraction(ownerId), Is.True);
            Assert.That(sanctuary.CanUseExtraction(rivalId), Is.False);

            yield return Execute(_ => { });
            Assert.That(ownerExtraction.State, Is.EqualTo(ExtractionState.InProgress));
            Assert.That(rivalExtraction.State, Is.EqualTo(ExtractionState.None));

            bool disabled = true;
            yield return Execute(_ => disabled = zone.TrySetAvailability(false));
            Assert.That(disabled, Is.False);
            Assert.That(zone.IsAvailable, Is.True);

            yield return null;
            yield return null;
            Assert.That(sanctuary.RitualState, Is.EqualTo(ExtractionRitualState.Completed));
            Assert.That(zone.IsAvailable, Is.True);
        }

        [UnityTest]
        public IEnumerator Ritual_DefeatedOwnerCancelsTerminallyAndCannotRestart()
        {
            NetworkObject sanctuaryObject = SpawnRitualSanctuary(Vector3.zero, 1f);
            NetworkObject ownerObject = Spawn(PlayerGuid, Vector3.zero);
            ExtractionSanctuary sanctuary = sanctuaryObject.GetComponent<ExtractionSanctuary>();
            PlayerCharacter owner = ownerObject.GetComponent<PlayerCharacter>();
            PlayerExtractionProgressController progress =
                ownerObject.GetComponent<PlayerExtractionProgressController>();

            yield return Execute(runner => progress.TryApplyContribution(
                new ExtractionProgressContribution(
                    ExtractionProgressSourceType.Defeat,
                    new EntityId(8201),
                    long.MaxValue,
                    runner.Tick)));
            yield return Execute(runner => sanctuary.Interact(
                new InteractionRequest(owner.Id, sanctuary.Id, runner.Tick)));

            yield return Execute(runner => owner.ApplyDamage(new DamageRequest(
                new EntityId(8202),
                owner.Id,
                1000f,
                DamageType.TrueDamage,
                Vector2.zero,
                owner.transform.position,
                runner.Tick)));

            int frames = 60;
            while (sanctuary.RitualState == ExtractionRitualState.InProgress && frames-- > 0)
            {
                yield return null;
            }

            Assert.That(sanctuary.RitualState, Is.EqualTo(ExtractionRitualState.Cancelled));
            Assert.That(sanctuaryObject.GetComponent<ExtractionZone>().IsAvailable, Is.False);
            Assert.That(sanctuary.TryGetRitualProgress(out ExtractionRitualSnapshot cancelled), Is.True);
            Assert.That(cancelled.RemainingSeconds, Is.EqualTo(1f));
            Assert.That(cancelled.Progress, Is.Zero);
            Assert.That(sanctuary.CanInteract(
                new InteractionRequest(owner.Id, sanctuary.Id, _runner.Tick)), Is.False);

            InteractionResult repeated = default;
            yield return Execute(runner => repeated = sanctuary.Interact(
                new InteractionRequest(owner.Id, sanctuary.Id, runner.Tick)));
            Assert.That(repeated.Success, Is.False);
            Assert.That(sanctuary.RitualState, Is.EqualTo(ExtractionRitualState.Cancelled));
        }

        [UnityTest]
        public IEnumerator Ritual_ExecutionWithoutStateAuthorityReturnsTypedRejection()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SanctuaryPrefabPath);
            GameObject instance = UnityEngine.Object.Instantiate(prefab);
            try
            {
                ExtractionSanctuary sanctuary = instance.GetComponent<ExtractionSanctuary>();
                InteractionResult result = sanctuary.Interact(new InteractionRequest(
                    new EntityId(1),
                    new EntityId(2),
                    1));

                Assert.That(result.Success, Is.False);
                Assert.That(result.FailureReason, Is.EqualTo(InteractionFailureReason.MissingStateAuthority));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator SanctuaryAndZoneCleanup_IsOrderIndependentAndRemovesColliderMappingLast()
        {
            yield return VerifyCleanupOrder(sanctuaryFirst: true, Vector3.left * 3f);
            yield return VerifyCleanupOrder(sanctuaryFirst: false, Vector3.right * 3f);
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

        private NetworkObject SpawnSanctuary(Vector3 position)
        {
            string guid = AssetDatabase.AssetPathToGUID(SanctuaryPrefabPath);
            Assert.That(guid, Is.Not.Empty);
            return Spawn(guid, position);
        }

        private NetworkObject SpawnRitualSanctuary(Vector3 position, float durationSeconds)
        {
            string guid = AssetDatabase.AssetPathToGUID(SanctuaryPrefabPath);
            NetworkObject prefab = Load(guid);
            Assert.That(SanctuaryConfigField, Is.Not.Null);
            Assert.That(RitualDurationField, Is.Not.Null);
            ExtractionConfig source = (ExtractionConfig)SanctuaryConfigField.GetValue(
                prefab.GetComponent<ExtractionSanctuary>());
            ExtractionConfig config = UnityEngine.Object.Instantiate(source);
            RitualDurationField.SetValue(config, durationSeconds);
            _ritualTestConfigs.Add(config);

            bool configured = false;
            NetworkObject spawned = _runner.Spawn(
                prefab,
                position,
                Quaternion.identity,
                inputAuthority: null,
                onBeforeSpawned: (_, instance) =>
                {
                    SanctuaryConfigField.SetValue(instance.GetComponent<ExtractionSanctuary>(), config);
                    configured = true;
                });
            Assert.That(configured, Is.True);
            Assert.That(spawned, Is.Not.Null);
            return spawned;
        }

        private IEnumerator VerifyCleanupOrder(bool sanctuaryFirst, Vector3 position)
        {
            NetworkObject spawned = SpawnSanctuary(position);
            ExtractionSanctuary sanctuary = spawned.GetComponent<ExtractionSanctuary>();
            ExtractionZone zone = spawned.GetComponent<ExtractionZone>();
            Collider2D collider = spawned.GetComponent<Collider2D>();
            EntityRegistry registry = _runner.GetComponent<EntityRegistry>();
            EntityId id = sanctuary.Id;

            Assert.That(registry.TryGetEntityId(collider, out EntityId mapped), Is.True);
            Assert.That(mapped, Is.EqualTo(id));

            if (sanctuaryFirst)
            {
                sanctuary.Despawned(_runner, true);
                Assert.That(registry.TryGetInteractable(id, out _), Is.False);
                Assert.That(registry.TryGetExtractionSanctuary(id, out _), Is.False);
                Assert.That(registry.TryGetExtractionZone(id, out _), Is.True);
                Assert.That(registry.TryGetEntityId(collider, out _), Is.True);
                zone.Despawned(_runner, true);
            }
            else
            {
                zone.Despawned(_runner, true);
                Assert.That(registry.TryGetExtractionZone(id, out _), Is.False);
                Assert.That(registry.TryGetExtractionSanctuary(id, out _), Is.True);
                Assert.That(registry.TryGetInteractable(id, out _), Is.True);
                Assert.That(registry.TryGetEntityId(collider, out _), Is.True);
                sanctuary.Despawned(_runner, true);
            }

            Assert.That(registry.TryGetInteractable(id, out _), Is.False);
            Assert.That(registry.TryGetExtractionSanctuary(id, out _), Is.False);
            Assert.That(registry.TryGetExtractionZone(id, out _), Is.False);
            Assert.That(registry.TryGetEntityId(collider, out _), Is.False);
            yield return null;
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
