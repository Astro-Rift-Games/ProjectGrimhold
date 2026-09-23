#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using Fusion;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Assert = NUnit.Framework.Assert;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Progression
{
    public sealed class KillExperienceSourcePlayModeTests
    {
        private const string PlayerPrefabGuid = "fea3a7b256f965a4eb9b965832939741";
        private const string ParticipantPrefabGuid = "c39d451563bae6e43934008a0dadc6d6";
        private const string BlueSlimePrefabGuid = "a559f183b6c025f41bdca5fdd17eda02";
        private const string GreenSlimePrefabGuid = "5deca87613df0fa409d98702aec643d4";
        private const string RedSlimePrefabGuid = "e67f9247cfa1c6948b390f8306c1a8af";
        private const string RangedEnemyPrefabGuid = "6f7ab2fe6d6193a4ea17a843ff58f94b";

        private NetworkRunner _runner;
        private EnemyFatalDamageSimulationDriver _damageDriver;
        private ExpeditionExperienceSimulationDriver _experienceDriver;
        private NetworkObject _firstPlayer;
        private NetworkObject _secondPlayer;
        private NetworkRaidParticipant _firstParticipant;
        private NetworkRaidParticipant _secondParticipant;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
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
                Object.DestroyImmediate(_runner.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator FatalRewardsExactValuesOnceAndIsolatesParticipants()
        {
            yield return StartRunnerAndSpawnParticipants();
            NetworkObject slime = SpawnEnemy(GreenSlimePrefabGuid, Vector3.zero);
            NetworkObject ranged = SpawnEnemy(RangedEnemyPrefabGuid, Vector3.right * 2f);

            yield return ResolveDamage(slime, _firstPlayer, 1000f);
            AssertLedger(_firstParticipant, 10);
            AssertLedger(_secondParticipant, 0);
            Assert.That((bool)slime.GetComponent<KillExperienceSource>().IsGranted, Is.True);

            yield return ResolveDamage(slime, _secondPlayer, 1000f);
            Assert.That(_damageDriver.LastResult.IsApplied, Is.False);
            AssertLedger(_firstParticipant, 10);
            AssertLedger(_secondParticipant, 0);

            yield return ResolveDamage(ranged, _secondPlayer, 1000f);
            AssertLedger(_firstParticipant, 10);
            AssertLedger(_secondParticipant, 15);
            Assert.That((bool)ranged.GetComponent<KillExperienceSource>().IsGranted, Is.True);
        }

        [UnityTest]
        public IEnumerator AssistExperienceIsAwardedToContributorsExceptKiller()
        {
            yield return StartRunnerAndSpawnParticipants();
            NetworkObject slime = SpawnEnemy(GreenSlimePrefabGuid, Vector3.zero);
            
            // First player contributes (non-fatal)
            yield return ResolveDamage(slime, _firstPlayer, 1f);
            
            // Second player delivers fatal blow
            yield return ResolveDamage(slime, _secondPlayer, 1000f);

            // GreenSlime has 10 Kill Experience.
            AssertLedger(_firstParticipant, expectedKill: 0, expectedAssist: 5);
            AssertLedger(_secondParticipant, expectedKill: 10, expectedAssist: 0);
            
            Assert.That((bool)slime.GetComponent<KillExperienceSource>().IsAssistResolutionCompleted, Is.True);
        }

        [UnityTest]
        public IEnumerator AssistYieldsHalfOfOddKillExperience()
        {
            yield return StartRunnerAndSpawnParticipants();
            NetworkObject ranged = SpawnEnemy(RangedEnemyPrefabGuid, Vector3.zero);
            
            yield return ResolveDamage(ranged, _firstPlayer, 1f);
            yield return ResolveDamage(ranged, _secondPlayer, 1000f);

            // Ranged has 15 Kill Experience. 15 / 2 = 7 (integer division)
            AssertLedger(_firstParticipant, expectedKill: 0, expectedAssist: 7);
            AssertLedger(_secondParticipant, expectedKill: 15, expectedAssist: 0);
        }

        [UnityTest]
        public IEnumerator MultipleContributionsFromSamePlayerYieldOneAssist()
        {
            yield return StartRunnerAndSpawnParticipants();
            NetworkObject slime = SpawnEnemy(GreenSlimePrefabGuid, Vector3.zero);
            
            yield return ResolveDamage(slime, _firstPlayer, 1f);
            yield return ResolveDamage(slime, _firstPlayer, 1f);
            yield return ResolveDamage(slime, _firstPlayer, 1f);
            
            yield return ResolveDamage(slime, _secondPlayer, 1000f);

            AssertLedger(_firstParticipant, expectedKill: 0, expectedAssist: 5);
            AssertLedger(_secondParticipant, expectedKill: 10, expectedAssist: 0);
        }

        [UnityTest]
        public IEnumerator ContributorMustBeInsideTemporalWindow()
        {
            yield return StartRunnerAndSpawnParticipants();
            NetworkObject slime = SpawnEnemy(GreenSlimePrefabGuid, Vector3.zero);
            
            // Resolve non-fatal damage
            yield return ResolveDamage(slime, _firstPlayer, 1f);
            
            // Advance time manually precisely to out of window
            int windowTicks = Mathf.CeilToInt(10f / _runner.DeltaTime); // Assist window is 10s
            for (int i = 0; i < windowTicks + 1; i++)
            {
                yield return new WaitForFixedUpdate();
            }
            
            yield return ResolveDamage(slime, _secondPlayer, 1000f);

            // P1 is precisely 1 tick out of window, so 0 assist.
            AssertLedger(_firstParticipant, expectedKill: 0, expectedAssist: 0);
        }

        [UnityTest]
        public IEnumerator NonContributorReceivesNoAssist()
        {
            yield return StartRunnerAndSpawnParticipants();
            NetworkObject slime = SpawnEnemy(GreenSlimePrefabGuid, Vector3.zero);
            
            // P1 does nothing.
            // P2 kills.
            yield return ResolveDamage(slime, _secondPlayer, 1000f);

            AssertLedger(_firstParticipant, expectedKill: 0, expectedAssist: 0);
            AssertLedger(_secondParticipant, expectedKill: 10, expectedAssist: 0);
        }

        [UnityTest]
        public IEnumerator StaleAvatarDoesNotRecordContribution()
        {
            yield return StartRunnerAndSpawnParticipants();
            NetworkObject slime = SpawnEnemy(GreenSlimePrefabGuid, Vector3.zero);
            
            // Use existing helper to spawn an avatar linked to firstParticipant,
            // but NOT registered as CurrentAvatar
            NetworkObject staleAvatar = SpawnAvatarWithoutCurrentParticipation(_firstParticipant.Object, Vector3.up);
            
            yield return ResolveDamage(slime, staleAvatar, 1f);
            
            yield return ResolveDamage(slime, _secondPlayer, 1000f);

            // Stale avatar shouldn't record contribution
            AssertLedger(_firstParticipant, expectedKill: 0, expectedAssist: 0);
        }

        [UnityTest]
        public IEnumerator DoubleResolutionDoesNotDuplicateExperience()
        {
            yield return StartRunnerAndSpawnParticipants();
            NetworkObject slime = SpawnEnemy(GreenSlimePrefabGuid, Vector3.zero);
            
            yield return ResolveDamage(slime, _firstPlayer, 1f);
            yield return ResolveDamage(slime, _secondPlayer, 1000f);
            
            KillExperienceSource source = slime.GetComponent<KillExperienceSource>();
            PlayerExpeditionExperienceLedger p1Ledger = _firstParticipant.GetComponent<PlayerExpeditionExperienceLedger>();
            PlayerExpeditionExperienceLedger p2Ledger = _secondParticipant.GetComponent<PlayerExpeditionExperienceLedger>();

            // Simulate glitch where resolving attempts to grant again
            source.InitializeAssistCandidates(source.EligibleAssistMask);
            source.TryGrantAssistTo(_firstParticipant.RaidParticipantId, p1Ledger);
            source.TryGrantTo(p2Ledger);
            
            yield return new WaitForFixedUpdate();

            AssertLedger(_firstParticipant, expectedKill: 0, expectedAssist: 5);
            AssertLedger(_secondParticipant, expectedKill: 10, expectedAssist: 0);
        }

        [UnityTest]
        public IEnumerator LedgerRejectionFreezesAssistUntilRetried()
        {
            yield return StartRunnerAndSpawnParticipants();
            NetworkObject slime = SpawnEnemy(GreenSlimePrefabGuid, Vector3.zero);
            
            yield return ResolveDamage(slime, _firstPlayer, 1f);
            
            // Introduce a third player by spawning a third participant and avatar
            var (thirdParticipant, thirdAvatar) = SpawnParticipantAndAvatar("third-profile", Vector3.forward * 2f, 3);
            yield return ResolveDamage(slime, thirdAvatar, 1f);

            // Lock first participant's ledger by simulating a network failure (setting participant ref to null via reflection)
            PlayerExpeditionExperienceLedger p1Ledger = _firstParticipant.GetComponent<PlayerExpeditionExperienceLedger>();
            var participantField = typeof(PlayerExpeditionExperienceLedger).GetField("_participant", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            participantField.SetValue(p1Ledger, null);
            
            // Second player kills
            yield return ResolveDamage(slime, _secondPlayer, 1000f);

            // P1 is locked -> pending. P3 is unlocked -> granted. P2 is killer.
            AssertLedger(_firstParticipant, expectedKill: 0, expectedAssist: 0); 
            AssertLedger(thirdParticipant, expectedKill: 0, expectedAssist: 5); 
            
            KillExperienceSource source = slime.GetComponent<KillExperienceSource>();
            Assert.That((bool)source.IsAssistResolutionCompleted, Is.False); 
            Assert.That((int)source.GrantedAssistMask, Is.EqualTo(4)); // P3 granted
            Assert.That(((int)source.GrantedAssistMask & 1), Is.EqualTo(0)); // P1 not granted
            
            // Now unlock P1 and wait for automatic tick-driven retry
            participantField.SetValue(p1Ledger, _firstParticipant);
            
            // AssistRetryIntervalSeconds is 1f, so wait a bit more than 1 second
            int waitTicks = Mathf.CeilToInt(1.2f / _runner.DeltaTime);
            for (int i = 0; i < waitTicks; i++)
            {
                yield return new WaitForFixedUpdate();
            }
            
            // Verify P1 got rewarded
            AssertLedger(_firstParticipant, expectedKill: 0, expectedAssist: 5);
            AssertLedger(thirdParticipant, expectedKill: 0, expectedAssist: 5);
            Assert.That((bool)source.IsAssistResolutionCompleted, Is.True);
            Assert.That((int)source.GrantedAssistMask, Is.EqualTo(source.EligibleAssistMask));

            // Wait some more to ensure no extra XP is awarded
            for (int i = 0; i < waitTicks; i++)
            {
                yield return new WaitForFixedUpdate();
            }

            AssertLedger(_firstParticipant, expectedKill: 0, expectedAssist: 5);
            AssertLedger(thirdParticipant, expectedKill: 0, expectedAssist: 5);
        }

        [UnityTest]
        public IEnumerator HostMigrationPreservesAssistState()
        {
            yield return StartRunnerAndSpawnParticipants();
            
            // Spawn an actual third participant
            var (participant3, avatar3) = SpawnParticipantAndAvatar("profile-3", Vector3.up, 3);
            
            NetworkObject slime = SpawnEnemy(GreenSlimePrefabGuid, Vector3.zero);
            KillExperienceSource source = slime.GetComponent<KillExperienceSource>();
            
            source.InitializeAssistCandidates(5); // Binary 101 -> participants 1 and 3
            var p3ledger = participant3.GetComponent<PlayerExpeditionExperienceLedger>();
            
            RaidParticipantId.TryCreate(3, out RaidParticipantId p3id);
            source.TryGrantAssistTo(p3id, p3ledger); // Grant to participant 3

            Assert.That((int)source.EligibleAssistMask, Is.EqualTo(5));
            Assert.That((int)source.GrantedAssistMask, Is.EqualTo(4));
            Assert.That((bool)source.IsAssistResolutionCompleted, Is.False);

            // Simulate Host Migration state copy
            NetworkObject newSlime = SpawnEnemy(GreenSlimePrefabGuid, Vector3.right);
            KillExperienceSource newSource = newSlime.GetComponent<KillExperienceSource>();
            newSource.CopyStateFrom(source);
            
            Assert.That((int)newSource.EligibleAssistMask, Is.EqualTo(5));
            Assert.That((int)newSource.GrantedAssistMask, Is.EqualTo(4));
            Assert.That((bool)newSource.IsAssistResolutionCompleted, Is.False);
        }

        [UnityTest]
        public IEnumerator CombatContributionTrackerCopyStateFromPreservesTicks()
        {
            yield return StartRunnerAndSpawnParticipants();
            NetworkObject slime = SpawnEnemy(GreenSlimePrefabGuid, Vector3.zero);
            CombatContributionTracker tracker = slime.GetComponent<CombatContributionTracker>();
            
            // Record P1 at tick 100
            int p1Tick = _runner.Tick;
            tracker.TryRecordContribution(_firstParticipant.RaidParticipantId, p1Tick);
            
            // Advance some ticks
            for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();
            
            // Record P2 at tick 105
            int p2Tick = _runner.Tick;
            tracker.TryRecordContribution(_secondParticipant.RaidParticipantId, p2Tick);

            // Spawn a new enemy to simulate host migration target
            NetworkObject restoredSlime = SpawnEnemy(GreenSlimePrefabGuid, Vector3.right);
            CombatContributionTracker restoredTracker = restoredSlime.GetComponent<CombatContributionTracker>();
            
            // Copy state
            restoredTracker.CopyStateFrom(slime.GetComponent<CombatContributionTracker>());
            
            var contributors = new System.Collections.Generic.HashSet<RaidParticipantId>();
            
            // Both should be valid if we are close to p2Tick
            restoredTracker.GetValidContributors(p2Tick + 2, contributors);
            Assert.That(contributors.Contains(_firstParticipant.RaidParticipantId), Is.True);
            Assert.That(contributors.Contains(_secondParticipant.RaidParticipantId), Is.True);
            
            // Wait until p1 is out of window (window is 10 seconds, let's assume DeltaTime is 1/60, so 600 ticks)
            int windowTicks = Mathf.CeilToInt(10f / _runner.DeltaTime);
            contributors.Clear();
            
            // At exactly window limit for P1
            restoredTracker.GetValidContributors(p1Tick + windowTicks, contributors);
            Assert.That(contributors.Contains(_firstParticipant.RaidParticipantId), Is.True, "Participant should be valid exactly at the window limit");
            Assert.That(contributors.Contains(_secondParticipant.RaidParticipantId), Is.True);
            
            // 1 tick out of window for P1
            contributors.Clear();
            restoredTracker.GetValidContributors(p1Tick + windowTicks + 1, contributors);
            Assert.That(contributors.Contains(_firstParticipant.RaidParticipantId), Is.False, "Participant out of window should not be returned");
            Assert.That(contributors.Contains(_secondParticipant.RaidParticipantId), Is.True);
        }

        [UnityTest]
        public IEnumerator NonFatalAndInvalidAttackerPreserveReward()
        {
            yield return StartRunnerAndSpawnParticipants();
            NetworkObject nonFatalTarget = SpawnEnemy(RedSlimePrefabGuid, Vector3.zero);
            KillExperienceSource nonFatalSource = nonFatalTarget.GetComponent<KillExperienceSource>();

            yield return ResolveDamage(nonFatalTarget, _firstPlayer, 1f);
            Assert.That(_damageDriver.LastResult.IsFatal, Is.False);
            Assert.That(nonFatalSource.IsAvailable, Is.True);
            AssertLedger(_firstParticipant, 0);

            NetworkObject invalidAttackerTarget = SpawnEnemy(BlueSlimePrefabGuid, Vector3.right * 2f);
            KillExperienceSource invalidSource = invalidAttackerTarget.GetComponent<KillExperienceSource>();
            yield return ResolveDamage(
                invalidAttackerTarget,
                new EntityId(int.MaxValue),
                1000f);

            Assert.That(_damageDriver.LastResult.IsFatal, Is.True);
            Assert.That(invalidSource.IsAvailable, Is.True);
            AssertLedger(_firstParticipant, 0);
            AssertLedger(_secondParticipant, 0);

            NetworkObject enemyAttacker = SpawnEnemy(GreenSlimePrefabGuid, Vector3.left * 2f);
            NetworkObject nonPlayerTarget = SpawnEnemy(BlueSlimePrefabGuid, Vector3.up * 2f);
            yield return ResolveDamage(
                nonPlayerTarget,
                enemyAttacker.GetComponent<EnemyCharacter>().Id,
                1000f);

            Assert.That(nonPlayerTarget.GetComponent<KillExperienceSource>().IsAvailable, Is.True);
            AssertLedger(_firstParticipant, 0);
            AssertLedger(_secondParticipant, 0);
        }

        [UnityTest]
        public IEnumerator LedgerRejectionDoesNotConsumeSource()
        {
            yield return StartRunnerAndSpawnParticipants();
            yield return SetParticipantState(_firstParticipant, RaidParticipantState.Defeated);
            NetworkObject slime = SpawnEnemy(BlueSlimePrefabGuid, Vector3.zero);
            KillExperienceSource source = slime.GetComponent<KillExperienceSource>();

            yield return ResolveDamage(slime, _firstPlayer, 1000f);

            Assert.That(_damageDriver.LastResult.IsFatal, Is.True);
            Assert.That(source.IsAvailable, Is.True);
            Assert.That((bool)source.IsGranted, Is.False);
            AssertLedger(_firstParticipant, 0);
        }

        [UnityTest]
        public IEnumerator InvalidLinkAndStaleParticipantAvatarDoNotConsumeSources()
        {
            yield return StartRunnerAndSpawnParticipants();

            NetworkObject unlinkedAvatar = SpawnAvatarWithoutCurrentParticipation(
                participantObject: null,
                Vector3.up * 2f);
            NetworkObject unlinkedTarget = SpawnEnemy(BlueSlimePrefabGuid, Vector3.zero);
            yield return ResolveDamage(unlinkedTarget, unlinkedAvatar, 1000f);
            Assert.That(unlinkedTarget.GetComponent<KillExperienceSource>().IsAvailable, Is.True);

            NetworkObject staleAvatar = SpawnAvatarWithoutCurrentParticipation(
                _firstParticipant.Object,
                Vector3.down * 2f);
            NetworkObject staleTarget = SpawnEnemy(BlueSlimePrefabGuid, Vector3.right * 2f);
            yield return ResolveDamage(staleTarget, staleAvatar, 1000f);
            Assert.That(staleTarget.GetComponent<KillExperienceSource>().IsAvailable, Is.True);

            AssertLedger(_firstParticipant, 0);
            AssertLedger(_secondParticipant, 0);
        }

        [UnityTest]
        public IEnumerator CopyStateFromPreservesAvailableAndGrantedOneShotState()
        {
            yield return StartRunnerAndSpawnParticipants();
            NetworkObject grantedObject = SpawnEnemy(BlueSlimePrefabGuid, Vector3.zero);
            NetworkObject availableObject = SpawnEnemy(BlueSlimePrefabGuid, Vector3.right * 2f);

            yield return ResolveDamage(grantedObject, _firstPlayer, 1000f);
            KillExperienceSource granted = grantedObject.GetComponent<KillExperienceSource>();
            KillExperienceSource available = availableObject.GetComponent<KillExperienceSource>();
            Assert.That((bool)granted.IsGranted, Is.True);
            Assert.That((bool)available.IsGranted, Is.False);

            available.CopyStateFrom(granted);
            Assert.That((bool)available.IsGranted, Is.True);
            Assert.That(available.TryGrantTo(GetLedger(_secondParticipant)), Is.False);
            AssertLedger(_secondParticipant, 0);

            NetworkObject freshAvailableObject = SpawnEnemy(BlueSlimePrefabGuid, Vector3.right * 4f);
            granted.CopyStateFrom(freshAvailableObject.GetComponent<KillExperienceSource>());
            Assert.That((bool)granted.IsGranted, Is.False);
            Assert.That(granted.IsAvailable, Is.True);
        }

        [UnityTest]
        public IEnumerator ParticipantCopyStateFromPreservesStableRaidParticipantIdWithoutReassignment()
        {
            yield return StartRunnerAndSpawnParticipants();
            var sourceAbilities = new PreparedAbilityLoadout(
                new AbilityId(new string('a', AbilityId.MaximumLength)),
                new AbilityId("trap"));
            NetworkObject sourceObject = _runner.Spawn(
                LoadPrefab(ParticipantPrefabGuid),
                Vector3.up * 2f,
                Quaternion.identity,
                inputAuthority: null,
                onBeforeSpawned: (_, instance) =>
                    instance.GetComponent<NetworkRaidParticipant>().Initialize(
                        "source-profile",
                        CreateParticipantId(15),
                        ProgressionBalanceDefaults.InitialCharacterAttributeState,
                        ExperienceCurve.InitialLevel,
                        0,
                        "source-generation",
                        preparedAbilities: sourceAbilities));
            NetworkRaidParticipant source = sourceObject.GetComponent<NetworkRaidParticipant>();
            Assert.That(source.TryGetPreparedAbilityLoadout(out PreparedAbilityLoadout freshSource), Is.True);
            Assert.That(freshSource, Is.EqualTo(sourceAbilities));

            var replacementAbilities = new PreparedAbilityLoadout(
                default,
                new AbilityId("replacement_ability"));
            NetworkObject restoredObject = _runner.Spawn(
                LoadPrefab(ParticipantPrefabGuid),
                Vector3.up * 4f,
                Quaternion.identity,
                inputAuthority: null,
                onBeforeSpawned: (_, instance) =>
                    instance.GetComponent<NetworkRaidParticipant>().Initialize(
                        "replacement-profile",
                        CreateParticipantId(16),
                        ProgressionBalanceDefaults.InitialCharacterAttributeState,
                        ExperienceCurve.InitialLevel,
                        0,
                        "replacement-generation",
                        preparedAbilities: replacementAbilities));
            NetworkRaidParticipant restored =
                restoredObject.GetComponent<NetworkRaidParticipant>();
            Assert.That(restored.TryGetPreparedAbilityLoadout(out PreparedAbilityLoadout freshReplacement), Is.True);
            Assert.That(freshReplacement, Is.EqualTo(replacementAbilities));

            restored.CopyStateFrom(source);

            Assert.That(restored.RaidParticipantId, Is.EqualTo(source.RaidParticipantId));
            Assert.That(restored.ProfileId, Is.EqualTo(source.ProfileId));
            Assert.That(restored.RaidParticipantId.Value, Is.EqualTo(15));
            Assert.That(restored.TryGetPreparedAbilityLoadout(out PreparedAbilityLoadout restoredAbilities), Is.True);
            Assert.That(restoredAbilities, Is.EqualTo(sourceAbilities));
            Assert.That(restoredAbilities, Is.Not.EqualTo(replacementAbilities));
            Assert.That(source.TryGetPreparedAbilityLoadout(out PreparedAbilityLoadout unchangedSource), Is.True);
            Assert.That(unchangedSource, Is.EqualTo(sourceAbilities));
        }

        private IEnumerator StartRunnerAndSpawnParticipants()
        {
            var runnerObject = new GameObject("KillExperienceSourceTestRunner");
            _runner = runnerObject.AddComponent<NetworkRunner>();
            runnerObject.AddComponent<EntityRegistry>();
            _damageDriver = runnerObject.AddComponent<EnemyFatalDamageSimulationDriver>();
            _experienceDriver = runnerObject.AddComponent<ExpeditionExperienceSimulationDriver>();

            var start = _runner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Single,
                SessionName = $"task-130-{Guid.NewGuid():N}",
                SceneManager = runnerObject.AddComponent<NetworkSceneManagerDefault>(),
                ObjectProvider = runnerObject.AddComponent<NetworkObjectProviderDefault>()
            });
            while (!start.IsCompleted)
            {
                yield return null;
            }

            Assert.That(start.Result.Ok, Is.True, start.Result.ShutdownReason.ToString());
            (_firstParticipant, _firstPlayer) = SpawnParticipantAndAvatar("first-profile", Vector3.left);
            (_secondParticipant, _secondPlayer) = SpawnParticipantAndAvatar("second-profile", Vector3.right);
            yield return null;
        }

        private (NetworkRaidParticipant participant, NetworkObject avatar) SpawnParticipantAndAvatar(
            string profileId,
            Vector3 position,
            int explicitParticipantId = 0)
        {
            int assignedId = explicitParticipantId > 0 ? explicitParticipantId : (position.x < 0f ? 1 : 2);
            PlayerRef fakePlayer = PlayerRef.FromEncoded(assignedId);
            
            NetworkObject participantObject = _runner.Spawn(
                LoadPrefab(ParticipantPrefabGuid),
                position,
                Quaternion.identity,
                inputAuthority: null,
                onBeforeSpawned: (_, instance) =>
                    instance.GetComponent<NetworkRaidParticipant>().Initialize(
                        profileId,
                        CreateParticipantId(assignedId),
                        ProgressionBalanceDefaults.InitialCharacterAttributeState,
                        ExperienceCurve.InitialLevel,
                        0,
                        "task-130-generation"));
            NetworkRaidParticipant participant =
                participantObject.GetComponent<NetworkRaidParticipant>();

            _runner.GetComponent<NetworkSpawnManager>().Test_RegisterParticipant(fakePlayer, participantObject);

            ExpectBasePrefabExtractionProgressValidationError();
            NetworkObject avatar = _runner.Spawn(
                LoadPrefab(PlayerPrefabGuid),
                position,
                Quaternion.identity,
                inputAuthority: null,
                onBeforeSpawned: (_, instance) =>
                    instance.GetComponent<RaidAvatarParticipantLink>().Initialize(participantObject));
            Assert.That(participant.TrySetCurrentAvatar(avatar), Is.True);
            return (participant, avatar);
        }

        private static RaidParticipantId CreateParticipantId(int value)
        {
            RaidParticipantId.TryCreate(value, out RaidParticipantId participantId);
            return participantId;
        }

        private NetworkObject SpawnEnemy(string prefabGuid, Vector3 position)
        {
            NetworkObject enemy = _runner.Spawn(
                LoadPrefab(prefabGuid),
                position,
                Quaternion.identity);
            Assert.That(enemy, Is.Not.Null);
            return enemy;
        }

        private NetworkObject SpawnAvatarWithoutCurrentParticipation(
            NetworkObject participantObject,
            Vector3 position)
        {
            ExpectBasePrefabExtractionProgressValidationError();
            return _runner.Spawn(
                LoadPrefab(PlayerPrefabGuid),
                position,
                Quaternion.identity,
                inputAuthority: null,
                onBeforeSpawned: participantObject == null
                    ? null
                    : (_, instance) =>
                        instance.GetComponent<RaidAvatarParticipantLink>()
                            .Initialize(participantObject));
        }

        private IEnumerator ResolveDamage(
            NetworkObject target,
            NetworkObject attacker,
            float amount)
        {
            yield return ResolveDamage(
                target,
                attacker.GetComponent<PlayerCharacter>().Id,
                amount);
        }

        private IEnumerator ResolveDamage(NetworkObject target, EntityId attackerId, float amount)
        {
            int tick = _runner.Tick;
            _damageDriver.Target = target.GetComponent<EnemyCharacter>();
            _damageDriver.Resolver = target.GetComponent<DamageResolver>();
            _damageDriver.AttackerId = attackerId;
            _damageDriver.DamageAmount = amount;
            _damageDriver.IsRequested = true;
            while (_damageDriver.IsRequested && _runner.Tick < tick + 20)
            {
                yield return null;
            }

            Assert.That(_damageDriver.IsRequested, Is.False, "Damage request did not execute.");
        }

        private IEnumerator SetParticipantState(
            NetworkRaidParticipant participant,
            RaidParticipantState state)
        {
            int previousSequence = _experienceDriver.CompletionSequence;
            _experienceDriver.RequestSetParticipantState(participant, state);
            while (_experienceDriver.CompletionSequence == previousSequence)
            {
                yield return null;
            }
        }

        private NetworkObject LoadPrefab(string guid)
        {
            NetworkPrefabId prefabId =
                _runner.Config.PrefabTable.GetId(NetworkObjectGuid.Parse(guid));
            Assert.That(prefabId.IsValid, Is.True, guid);
            NetworkObject prefab = _runner.Config.PrefabTable.Load(prefabId, true);
            Assert.That(prefab, Is.Not.Null, guid);
            return prefab;
        }

        private static PlayerExpeditionExperienceLedger GetLedger(
            NetworkRaidParticipant participant) =>
            participant.GetComponent<PlayerExpeditionExperienceLedger>();

        private static void AssertLedger(NetworkRaidParticipant participant, long expectedKill, long expectedAssist = 0)
        {
            PlayerExpeditionExperienceLedger ledger = GetLedger(participant);
            ExpeditionExperienceSnapshot snapshot = ledger.Snapshot;
            Assert.That(snapshot.KillExperience, Is.EqualTo(expectedKill));
            Assert.That(snapshot.AssistExperience, Is.EqualTo(expectedAssist));
            Assert.That(snapshot.TotalExperience, Is.EqualTo(expectedKill + expectedAssist));
            
            Assert.That(ledger.PveKillCount, Is.EqualTo(expectedKill > 0 ? 1 : 0));
            Assert.That(ledger.PveAssistCount, Is.EqualTo(expectedAssist > 0 ? 1 : 0));
            Assert.That(ledger.PvpKillCount, Is.Zero);
            Assert.That(ledger.PvpAssistCount, Is.Zero);
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
