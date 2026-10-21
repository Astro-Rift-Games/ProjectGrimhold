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
        private static readonly ProfileId TestProfileId =
            new("33333333333333333333333333333333");
        private static readonly RaidParticipantId TestParticipantId = CreateTestParticipantId();
        private static readonly ProfileId FirstTeamProfile = new("11111111111111111111111111111111");
        private static readonly ProfileId SecondTeamProfile = new("22222222222222222222222222222222");

        private const string PlayerGuid = "fea3a7b256f965a4eb9b965832939741";
        private const string MatchControllerGuid = "b91f8c7e96a4d784a92c3bd1a88dfec8";
        private const string ParticipantGuid = "c39d451563bae6e43934008a0dadc6d6";
        private const string EnemyMeleeGuid = "5deca87613df0fa409d98702aec643d4";
        private const string EnemyRangedGuid = "6f7ab2fe6d6193a4ea17a843ff58f94b";
        private const string ContainerGuid = "2c19a78647c64b84a765ff0280706b7d";
        private const string PickupGuid = "5d26f13a358d7894b9419465e4ba1869";
        private const string SanctuaryPrefabPath = "Assets/Prefabs/ExtractionSanctuary.prefab";
        private const int GameplaySceneBuildIndex = 1;

        private NetworkRunner _runner;
        private ExtractionProgressSimulationDriver _driver;
        private RaidLaunchContext _launch;
        private Scene _ownedGameplayScene;
        private bool _previousIgnoreFailingMessages;
        private readonly List<ExtractionConfig> _ritualTestConfigs = new();

        private static readonly FieldInfo SanctuaryConfigField =
            typeof(ExtractionSanctuary).GetField("_config", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo RitualDurationField =
            typeof(ExtractionConfig).GetField("_ritualDurationSeconds", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo SessionSeedField =
            typeof(ExtractionSanctuaryAssignmentService).GetField("_sessionSeed", BindingFlags.Instance | BindingFlags.NonPublic);

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

        /// <summary>
        /// Composes the authoritative team pool hosted by <see cref="NetworkMatchController"/>. Only
        /// progress tests compose it: the manager would also react to the Gameplay scene load.
        /// </summary>
        private IEnumerator ComposeTeamProgress(RaidLaunchContext launch = null)
        {
            _launch = launch;
            Assert.That(_runner.gameObject.AddComponent<NetworkSpawnManager>().InitializeForRunner(_runner,
                default, default, Array.Empty<NetworkPrefabRef>(), SessionStartupContext.FreshSession, launch), Is.True);
            TeamExtractionProgress teamProgress =
                Spawn(MatchControllerGuid, Vector3.zero).GetComponent<TeamExtractionProgress>();

            // Without a launch roster the pool initializes the fallback solo team on its first tick.
            Assert.That(RaidTeamId.TryCreate(1, out RaidTeamId firstTeam), Is.True);
            yield return Execute(_ => { });
            Assert.That(teamProgress.IsAssignedToTeam(firstTeam), Is.True);
            Assert.That(_runner.GetComponent<EntityRegistry>().TryGetTeamExtractionProgress(
                out ITeamExtractionProgress registered), Is.True);
            Assert.That(registered, Is.SameAs(teamProgress));
        }

        /// <summary>Freezes two solo teams: <see cref="FirstTeamProfile"/> in Team 1 and <see cref="SecondTeamProfile"/> in Team 2.</summary>
        private static RaidLaunchContext CreateTwoTeamLaunch()
        {
            Assert.That(RaidCode.TryParse("123456", out RaidCode code), Is.True);
            Assert.That(RaidTeamId.TryCreate(1, out RaidTeamId firstTeam), Is.True);
            Assert.That(RaidTeamId.TryCreate(2, out RaidTeamId secondTeam), Is.True);
            var roster = new[]
            {
                new RaidLaunchParticipant(FirstTeamProfile, firstTeam),
                new RaidLaunchParticipant(SecondTeamProfile, secondTeam)
            };
            Assert.That(RaidLaunchContext.TryCreate(
                code, FirstTeamProfile, roster, FirstTeamProfile, 1, out RaidLaunchContext launch), Is.True);
            return launch;
        }

        /// <summary>Spawns the avatar of a frozen launch participant so team resolution reads its affiliation.</summary>
        private NetworkObject SpawnAffiliatedPlayer(ProfileId profileId, Vector3 position)
        {
            Assert.That(_launch, Is.Not.Null, "Compose the launch roster before spawning affiliated players.");
            var profiles = new ProfileId[_launch.Participants.Count];
            for (int i = 0; i < profiles.Length; i++)
            {
                profiles[i] = _launch.Participants[i].ProfileId;
            }

            Assert.That(RaidParticipantIdAssignment.TryResolve(
                profiles, profileId, out RaidParticipantId participantId), Is.True);
            NetworkObject participantObject = _runner.Spawn(
                Load(ParticipantGuid),
                position,
                Quaternion.identity,
                inputAuthority: null,
                onBeforeSpawned: (_, instance) =>
                    instance.GetComponent<NetworkRaidParticipant>().Initialize(
                        profileId.Value,
                        participantId,
                        ProgressionBalanceDefaults.InitialCharacterAttributeState,
                        ExperienceCurve.InitialLevel,
                        0,
                        "us13-generation"));
            // Ritual continuation counts the team's Raiding members through the manager's tracked roster.
            _runner.GetComponent<NetworkSpawnManager>().Test_RegisterParticipant(
                PlayerRef.FromEncoded(participantId.Value), participantObject);
            NetworkObject avatar = _runner.Spawn(
                Load(PlayerGuid),
                position,
                Quaternion.identity,
                inputAuthority: null,
                onBeforeSpawned: (_, instance) =>
                    instance.GetComponent<RaidAvatarParticipantLink>().Initialize(participantObject));
            Assert.That(participantObject.GetComponent<NetworkRaidParticipant>().TrySetCurrentAvatar(avatar), Is.True);
            return avatar;
        }

        /// <summary>
        /// Any seed is a legitimate session. This one makes the selection policy pick the same
        /// candidate index for both teams, so Sanctuary exclusivity is exercised on every run
        /// instead of only when the random session seed happens to collide.
        /// </summary>
        private static void ForceCollidingSessionSeed(
            ExtractionSanctuaryAssignmentService service,
            int tick,
            RaidTeamId firstTeam,
            RaidTeamId secondTeam,
            int sanctuaryCount)
        {
            Assert.That(SessionSeedField, Is.Not.Null);
            for (ulong seed = 1; seed <= 1024; seed++)
            {
                if (SanctuarySelectionPolicy.SelectIndex(seed, tick, firstTeam, sanctuaryCount) ==
                    SanctuarySelectionPolicy.SelectIndex(seed, tick, secondTeam, sanctuaryCount))
                {
                    SessionSeedField.SetValue(service, seed);
                    return;
                }
            }

            Assert.Fail("No colliding session seed was found for the selection policy.");
        }

        /// <summary>
        /// A fatal hit leaves an avatar Downed and still alive; damage that depletes its reserve is
        /// the definitive Defeat (GD 06 and DownedAndReviveArchitecture T1/T4).
        /// </summary>
        private IEnumerator DefeatDefinitively(NetworkObject avatar, Func<NetworkRunner, DamageResult> hit)
        {
            PlayerCharacter character = avatar.GetComponent<PlayerCharacter>();
            DamageResult downed = default;
            yield return Execute(runner => downed = hit(runner));
            Assert.That(downed.IsApplied, Is.True);
            Assert.That(PlayerDownedGate.IsDowned(character), Is.True, "A fatal hit must leave the avatar Downed.");
            Assert.That(character.IsAlive, Is.True);

            DamageResult defeated = default;
            yield return Execute(runner => defeated = hit(runner));
            Assert.That(defeated.IsApplied && defeated.IsFatal, Is.True);
            Assert.That(character.IsAlive, Is.False);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            SceneManager.sceneLoaded -= CaptureOwnedGameplayScene;
            LogAssert.ignoreFailingMessages = _previousIgnoreFailingMessages;
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

            // The test body can fail before its normal Fusion unload. Shutdown alone
            // does not unload additive scenes in single-peer mode.
            if (_ownedGameplayScene.IsValid() && _ownedGameplayScene.isLoaded)
            {
                AsyncOperation unload = SceneManager.UnloadSceneAsync(_ownedGameplayScene);
                while (unload != null && !unload.isDone)
                {
                    yield return null;
                }
            }

            for (int i = 0; i < _ritualTestConfigs.Count; i++)
            {
                UnityEngine.Object.DestroyImmediate(_ritualTestConfigs[i]);
            }

            _ritualTestConfigs.Clear();
            Assert.That(_ownedGameplayScene.isLoaded, Is.False,
                "The fixture must release its Gameplay scene even when the test body fails.");
            _ownedGameplayScene = default;
        }

        private void CaptureOwnedGameplayScene(Scene scene, LoadSceneMode mode)
        {
            if (scene.buildIndex == GameplaySceneBuildIndex && mode == LoadSceneMode.Additive)
            {
                _ownedGameplayScene = scene;
            }
        }

        [UnityTest]
        public IEnumerator TeamProgress_AllowsSameTickContributionsSaturatesAndPersistsAcrossAvatars()
        {
            yield return ComposeTeamProgress();
            SpawnSanctuary(Vector3.left * 4f);
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

            // Expedition progress is owned by the team (GD 06), so another avatar of the same team
            // observes the shared pool instead of starting from zero.
            NetworkObject teammate = Spawn(PlayerGuid, Vector3.right * 2f);
            PlayerExtractionProgressController teammateProgress =
                teammate.GetComponent<PlayerExtractionProgressController>();
            Assert.That(teammateProgress.CurrentProgress, Is.EqualTo(15));

            PlayerCharacter teammateCharacter = teammate.GetComponent<PlayerCharacter>();
            yield return DefeatDefinitively(teammate, runner => teammateCharacter.ApplyDamage(new DamageRequest(
                new EntityId(900),
                teammateCharacter.Id,
                1000f,
                DamageType.TrueDamage,
                Vector2.down,
                teammate.transform.position,
                runner.Tick)));
            bool acceptedAfterDefeat = true;
            yield return Execute(runner => acceptedAfterDefeat = teammateProgress.TryApplyContribution(
                new ExtractionProgressContribution(
                    ExtractionProgressSourceType.Defeat,
                    new EntityId(901),
                    10,
                    runner.Tick)));
            Assert.That(acceptedAfterDefeat, Is.False);
            Assert.That(progress.CurrentProgress, Is.EqualTo(15));

            yield return Execute(runner => progress.TryApplyContribution(new ExtractionProgressContribution(
                ExtractionProgressSourceType.LootFirstAcquisition, new EntityId(503), long.MaxValue, runner.Tick)));
            Assert.That(progress.CurrentProgress, Is.EqualTo(progress.Quota));
            Assert.That(progress.Quota, Is.EqualTo(100));
            Assert.That((bool)progress.AssignmentRequested, Is.True);
        }

        [UnityTest]
        public IEnumerator QuotaCompletion_AssignsDistinctSanctuariesAndRepeatedRequestIsIdempotent()
        {
            yield return ComposeTeamProgress(CreateTwoTeamLaunch());
            NetworkObject firstSanctuary = SpawnSanctuary(Vector3.left * 4f);
            NetworkObject secondSanctuary = SpawnSanctuary(Vector3.right * 4f);
            NetworkObject firstPlayer = SpawnAffiliatedPlayer(FirstTeamProfile, Vector3.zero);
            NetworkObject secondPlayer = SpawnAffiliatedPlayer(SecondTeamProfile, Vector3.up * 2f);
            PlayerExtractionProgressController firstProgress =
                firstPlayer.GetComponent<PlayerExtractionProgressController>();
            PlayerExtractionProgressController secondProgress =
                secondPlayer.GetComponent<PlayerExtractionProgressController>();
            Assert.That(firstProgress.TryGetTeamId(out RaidTeamId firstTeam), Is.True);
            Assert.That(secondProgress.TryGetTeamId(out RaidTeamId secondTeam), Is.True);
            Assert.That(firstTeam, Is.Not.EqualTo(secondTeam));
            ExtractionSanctuaryAssignmentService service =
                _runner.GetComponent<ExtractionSanctuaryAssignmentService>();

            yield return Execute(runner =>
            {
                ForceCollidingSessionSeed(service, runner.Tick, firstTeam, secondTeam, 2);
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

            SanctuaryAssignmentResult first = service.TryGetAssignment(firstTeam);
            SanctuaryAssignmentResult second = service.TryGetAssignment(secondTeam);
            Assert.That(first.Success, Is.True);
            Assert.That(second.Success, Is.True);
            Assert.That(first.SanctuaryId, Is.Not.EqualTo(second.SanctuaryId),
                "A Sanctuary is reserved for its owning team; selection must choose among free ones (GD 06 section 10).");
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
                repeated = service.TryAssign(firstTeam);
            });

            Assert.That(repeated.Success, Is.True);
            Assert.That(repeated.IsExistingAssignment, Is.True);
            Assert.That(repeated.SanctuaryId, Is.EqualTo(first.SanctuaryId));
        }

        [UnityTest]
        public IEnumerator QuotaCompletion_WithoutSanctuariesPreservesProgressWithoutPartialAssignment()
        {
            yield return ComposeTeamProgress();
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

            // The pending request is retried every tick; the configuration failure is reported once.
            yield return AssertAssignmentRetriesAreSilent(5);
        }

        [UnityTest]
        public IEnumerator QuotaCompletion_WithMoreTeamsThanSanctuariesRejectsTheLaterTeamWithoutSharing()
        {
            yield return ComposeTeamProgress(CreateTwoTeamLaunch());
            NetworkObject sanctuary = SpawnSanctuary(Vector3.left * 4f);
            PlayerExtractionProgressController firstProgress = SpawnAffiliatedPlayer(FirstTeamProfile, Vector3.zero)
                .GetComponent<PlayerExtractionProgressController>();
            PlayerExtractionProgressController secondProgress = SpawnAffiliatedPlayer(SecondTeamProfile, Vector3.up * 2f)
                .GetComponent<PlayerExtractionProgressController>();
            Assert.That(firstProgress.TryGetTeamId(out RaidTeamId firstTeam), Is.True);
            Assert.That(secondProgress.TryGetTeamId(out RaidTeamId secondTeam), Is.True);
            ExtractionSanctuaryAssignmentService service =
                _runner.GetComponent<ExtractionSanctuaryAssignmentService>();
            SanctuaryAssignmentResult rejected = default;

            LogAssert.Expect(UnityEngine.LogType.Error,
                "ExtractionSanctuaryAssignmentService configuration failure: NoFreeSanctuary.");
            yield return Execute(runner =>
            {
                Assert.That(firstProgress.TryApplyContribution(new ExtractionProgressContribution(
                    ExtractionProgressSourceType.Defeat, new EntityId(7201), long.MaxValue, runner.Tick)), Is.True);
                Assert.That(secondProgress.TryApplyContribution(new ExtractionProgressContribution(
                    ExtractionProgressSourceType.Defeat, new EntityId(7202), long.MaxValue, runner.Tick)), Is.True);
                rejected = service.TryAssign(secondTeam);
            });

            SanctuaryAssignmentResult owner = service.TryGetAssignment(firstTeam);
            Assert.That(owner.Success, Is.True);
            Assert.That(owner.SanctuaryId, Is.EqualTo(sanctuary.GetComponent<ExtractionSanctuary>().Id));
            Assert.That(rejected.Success, Is.False);
            Assert.That(rejected.FailureReason, Is.EqualTo(SanctuaryAssignmentFailureReason.NoFreeSanctuary));
            Assert.That(service.TryGetAssignment(secondTeam).FailureReason,
                Is.EqualTo(SanctuaryAssignmentFailureReason.AssignmentNotFound));
            Assert.That((bool)secondProgress.AssignmentRequested, Is.True, "The later team keeps its pending request.");

            yield return AssertAssignmentRetriesAreSilent(5);
            Assert.That(service.TryGetAssignment(secondTeam).FailureReason,
                Is.EqualTo(SanctuaryAssignmentFailureReason.AssignmentNotFound));
        }

        [UnityTest]
        public IEnumerator GameplayScene_FusionSpawnsFourDistinctSanctuaryIdentities()
        {
            SceneRef gameplayScene = SceneRef.FromIndex(GameplaySceneBuildIndex);
            SceneManager.sceneLoaded += CaptureOwnedGameplayScene;
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
            yield return ComposeTeamProgress();
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

            // An enemy player grants PvP progress only at definitive Defeat, never while Downed (GD 06).
            NetworkObject victim = Spawn(PlayerGuid, Vector3.left * 3f);
            DamageResult downed = default;
            yield return Execute(runner => downed = resolver.Resolve(FatalRequest(attacker, victim, runner.Tick)));
            Assert.That(downed.IsApplied, Is.True);
            Assert.That(PlayerDownedGate.IsDowned(victim.GetComponent<PlayerCharacter>()), Is.True);
            Assert.That(progress.CurrentProgress, Is.EqualTo(25));

            yield return Execute(runner => resolver.Resolve(FatalRequest(attacker, victim, runner.Tick)));
            Assert.That(victim.GetComponent<PlayerCharacter>().IsAlive, Is.False);
            Assert.That(progress.CurrentProgress, Is.EqualTo(55));
        }

        [UnityTest]
        public IEnumerator Containers_ResolveFirstOpenAndMixedProvenanceWithoutDuplication()
        {
            yield return ComposeTeamProgress();
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
                CommitPlayerOrigin(emptyContainer, deposit);
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
                CommitPlayerOrigin(naturalContainer, creditedDeposit);

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

        private static void CommitPlayerOrigin(
            NetworkLootContainer container,
            in LootTransferRequest request)
        {
            Assert.That(
                RaidLootOriginTransfer.TryCreatePlayer(
                    TestParticipantId,
                    request.RequestedAmount,
                    out RaidLootOriginTransfer transfer),
                Is.True);
            Assert.That(
                container.ValidateRaidLootOriginReceive(request, transfer),
                Is.EqualTo(LootTransferFailureReason.None));
            container.CommitRaidLootReceive(request, transfer);
        }

        private static RaidParticipantId CreateTestParticipantId()
        {
            RaidParticipantId.TryCreate(1, out RaidParticipantId participantId);
            return participantId;
        }

        [UnityTest]
        public IEnumerator Pickups_AwardNaturalUnitsAndNeverAwardCreditedDrops()
        {
            yield return ComposeTeamProgress();
            NetworkObject player = Spawn(PlayerGuid, Vector3.zero);
            PlayerCharacter character = player.GetComponent<PlayerCharacter>();
            PlayerExtractionProgressController progress = player.GetComponent<PlayerExtractionProgressController>();
            NetworkObject natural = SpawnPickup(new LootEntry(new LootId("bone"), 2), 2, Vector3.right);
            NetworkLootPickup naturalPickup = natural.GetComponent<NetworkLootPickup>();
            // The catalog owns the per-unit value; every eligible natural unit contributes it once.
            int valuePerUnit = naturalPickup.LootDefinition.ExtractionValuePerUnit;
            Assert.That(valuePerUnit, Is.Positive);
            int naturalProgress = 2 * valuePerUnit;

            yield return Execute(runner => naturalPickup.Interact(
                new InteractionRequest(character.Id, naturalPickup.Id, runner.Tick)));
            Assert.That(progress.CurrentProgress, Is.EqualTo(naturalProgress));

            NetworkObject credited = SpawnPickup(new LootEntry(new LootId("bone"), 2), 0, Vector3.left);
            NetworkLootPickup creditedPickup = credited.GetComponent<NetworkLootPickup>();
            yield return Execute(runner => creditedPickup.Interact(
                new InteractionRequest(character.Id, creditedPickup.Id, runner.Tick)));
            Assert.That(progress.CurrentProgress, Is.EqualTo(naturalProgress));
        }

        [UnityTest]
        public IEnumerator Ritual_OwnerIsPredictiveCandidateAndCompletionPermanentlyEnablesOwnZone()
        {
            // The rival belongs to another team, which cannot appropriate the owner's Sanctuary (GD 06).
            yield return ComposeTeamProgress(CreateTwoTeamLaunch());
            NetworkObject sanctuaryObject = SpawnRitualSanctuary(Vector3.zero, 0.05f);
            NetworkObject ownerObject = SpawnAffiliatedPlayer(FirstTeamProfile, Vector3.zero);
            NetworkObject rivalObject = SpawnAffiliatedPlayer(SecondTeamProfile, Vector3.right);
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
            yield return ComposeTeamProgress(CreateTwoTeamLaunch());
            NetworkObject sanctuaryObject = SpawnRitualSanctuary(Vector3.zero, 1f);
            NetworkObject ownerObject = SpawnAffiliatedPlayer(FirstTeamProfile, Vector3.zero);
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

            // A Downed initiator does not cancel the ritual; the definitive Defeat of every team
            // member does (GD 06). This team has a single member.
            yield return DefeatDefinitively(ownerObject, runner =>
            {
                DamageResult hit = owner.ApplyDamage(new DamageRequest(
                    new EntityId(8202),
                    owner.Id,
                    1000f,
                    DamageType.TrueDamage,
                    Vector2.zero,
                    owner.transform.position,
                    runner.Tick));
                if (PlayerDownedGate.IsDowned(owner))
                {
                    Assert.That(sanctuary.RitualState, Is.EqualTo(ExtractionRitualState.InProgress));
                }

                return hit;
            });

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

        /// <summary>
        /// Advances simulation ticks while the team pool retries a pending assignment and fails if the
        /// assignment service or a Sanctuary logs a warning or error for any of those attempts.
        /// </summary>
        private IEnumerator AssertAssignmentRetriesAreSilent(int ticks)
        {
            var repeated = new List<string>();
            void Capture(string condition, string stackTrace, UnityEngine.LogType type)
            {
                if ((type == UnityEngine.LogType.Error || type == UnityEngine.LogType.Warning) &&
                    (condition.Contains(nameof(ExtractionSanctuaryAssignmentService)) ||
                     condition.Contains(nameof(ExtractionSanctuary))))
                {
                    repeated.Add(condition);
                }
            }

            Application.logMessageReceived += Capture;
            try
            {
                for (int i = 0; i < ticks; i++)
                {
                    yield return Execute(_ => { });
                }
            }
            finally
            {
                Application.logMessageReceived -= Capture;
            }

            Assert.That(repeated, Is.Empty, "Retried assignment attempts must not log again.");
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
