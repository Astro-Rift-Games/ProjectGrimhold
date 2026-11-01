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
    /// TASK-236: real avatar Self-revive and tap/hold handoff sessions on State Authority.
    /// Inventory setup and damage use the existing simulation drivers; movement and the
    /// contextual Self-revive press use real Fusion input. This is not multi-peer proof.
    /// </summary>
    public sealed class PlayerSelfRevivePlayModeTests
    {
        private const string PlayerPrefabGuid = "fea3a7b256f965a4eb9b965832939741";
        private static readonly LootId SelfReviver = PlayerDownedRecoveryNetworkController.SelfReviverLootId;

        private NetworkRunner _runner;
        private NetworkObject _playerPrefab;
        private PlayerCorpseGenerationSimulationDriver _driver;
        private PlayerEquipmentSimulationDriver _inventory;
        private SelfReviveInputDriver _input;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
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
        }

        [UnityTest]
        public IEnumerator Self_CompletesOnce_ConsumesItemRestores25HealthAndDepletesStamina()
        {
            yield return StartRunnerAndLoadPlayer();
            Avatar target = SpawnAvatar(Vector3.zero);
            Assert.That(GetField<float>(target.Recovery, "_selfReviveDurationSeconds"), Is.EqualTo(5f));
            Assert.That(GetField<float>(target.Recovery, "_selfReviveRestoredHealth"), Is.EqualTo(25f));
            Assert.That(GetField<float>(target.Recovery, "_handoffDurationSeconds"), Is.EqualTo(1f));
            Assert.That(GetField<float>(target.Recovery, "_handoffTapWindowSeconds"), Is.EqualTo(0.3f));
            SetField(target.Recovery, "_selfReviveDurationSeconds", 0.6f);
            yield return GiveSelfReviver(target);
            SetNetworkedProperty(target.Stamina, nameof(PlayerStaminaNetworkController.CurrentStamina), 50f);
            yield return Down(target);
            Assert.That(target.Stamina.CurrentStamina, Is.GreaterThan(0f));
            Assert.That(target.Recovery.CanBeginSelf(), Is.True);
            Assert.That(target.Recovery.TryBeginSelf(), Is.True);
            AssertSession(target, RecoveryKind.Self, true);
            Assert.That(target.Recovery.IsSelfReviveChanneling, Is.True);
            Assert.That(target.Recovery.TryBeginSelf(), Is.False, "Cannot start a competing Self session.");
            int inventoryRevision = target.Receiver.Revision;
            float reserve = target.Downed.DownedHealth;
            yield return WaitFrames(8);
            Assert.That(target.Downed.DownedHealth, Is.EqualTo(reserve), "Self pauses actual reserve drain.");
            Assert.That(ItemCount(target), Is.EqualTo(1), "No consumption before completion.");
            Assert.That(target.Receiver.Revision, Is.EqualTo(inventoryRevision));
            Assert.That((bool)target.Recovery.SelfReviveUsed, Is.False);
            AssertOutcome(target, RecoveryOutcomeKind.None, DownedRecoveryInterruptReason.None, RecoveryKind.None, 0);

            yield return WaitUntil(() => !target.Character.IsDowned);
            AssertSelfCompleted(target, 1);
            Assert.That(target.Receiver.Revision, Is.EqualTo(inventoryRevision + 1));
            yield return WaitFrames(4);
            Assert.That(target.Recovery.LastOutcomeSequence, Is.EqualTo(1), "Completion is recorded once.");
            Assert.That(target.Receiver.Revision, Is.EqualTo(inventoryRevision + 1), "Consumption is committed once.");
        }

        [UnityTest]
        public IEnumerator Self_WithoutItem_RejectsWithoutSessionOrOutcomeAndDrainContinues()
        {
            yield return StartRunnerAndLoadPlayer();
            Avatar target = SpawnAvatar(Vector3.zero);
            yield return Down(target);
            float reserve = target.Downed.DownedHealth;
            int revision = target.Receiver.Revision;
            Assert.That(target.Recovery.CarriesSelfReviver, Is.False);
            Assert.That(target.Recovery.CanBeginSelf(), Is.False);
            Assert.That(target.Recovery.TryBeginSelf(), Is.False);
            yield return WaitFrames(4);

            AssertSession(target, RecoveryKind.None, false);
            AssertDowned(target, 0);
            Assert.That((bool)target.Recovery.SelfReviveUsed, Is.False);
            Assert.That(target.Receiver.Revision, Is.EqualTo(revision));
            Assert.That(target.Downed.DownedHealth, Is.LessThan(reserve));
            AssertOutcome(target, RecoveryOutcomeKind.None, DownedRecoveryInterruptReason.None, RecoveryKind.None, 0);
        }

        [UnityTest]
        public IEnumerator Self_DownedDamage_InterruptsPreservesItemAndQuotaAndAllowsRetry()
        {
            yield return StartRunnerAndLoadPlayer();
            Avatar target = SpawnAvatar(Vector3.zero);
            SetField(target.Recovery, "_selfReviveDurationSeconds", 30f);
            yield return GiveSelfReviver(target);
            yield return Down(target);
            Assert.That(target.Recovery.TryBeginSelf(), Is.True);
            float reserve = target.Downed.DownedHealth;
            int revision = target.Receiver.Revision;
            yield return WaitFrames(4);
            Assert.That(target.Downed.DownedHealth, Is.EqualTo(reserve));
            // Isolate damage from drain for an exact reserve assertion, then restore the drain.
            SetField(target.Downed, "_downedDrainPerSecond", 0f);
            SetField(target.Downed, "_downedDamageMultiplier", 1f);
            yield return Hit(target, 10f);

            AssertSession(target, RecoveryKind.None, false);
            AssertDowned(target, 1);
            Assert.That(target.Downed.DownedHealth, Is.EqualTo(reserve - 10f).Within(0.001f));
            Assert.That((bool)target.Recovery.SelfReviveUsed, Is.False);
            Assert.That(target.Receiver.Revision, Is.EqualTo(revision));
            AssertOutcome(target, RecoveryOutcomeKind.Interrupted, DownedRecoveryInterruptReason.DownedDamaged, RecoveryKind.Self, 1);
            SetField(target.Downed, "_downedDrainPerSecond", 6f);
            float afterDamage = target.Downed.DownedHealth;
            yield return WaitFrames(4);
            Assert.That(target.Downed.DownedHealth, Is.LessThan(afterDamage), "Drain resumes after interruption.");
            Assert.That(target.Recovery.CanBeginSelf(), Is.True);
            Assert.That(target.Recovery.TryBeginSelf(), Is.True);
            AssertSession(target, RecoveryKind.Self, true);
            Assert.That(target.Recovery.Completion.RemainingTime(_runner), Is.GreaterThan(29f), "Retry has full duration.");
            Assert.That(ItemCount(target), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator Self_DefinitiveDefeat_ClearsSessionWithoutConsumingTheRecoveryItem()
        {
            yield return StartRunnerAndLoadPlayer();
            Avatar target = SpawnAvatar(Vector3.zero);
            SetField(target.Recovery, "_selfReviveDurationSeconds", 30f);
            yield return GiveSelfReviver(target);
            yield return Down(target);
            Assert.That(target.Recovery.TryBeginSelf(), Is.True);
            AssertSession(target, RecoveryKind.Self, true);
            yield return Hit(target, 100000f);
            yield return WaitFrames(3);

            Assert.That(target.Character.IsAlive, Is.False);
            Assert.That(target.Character.IsDowned, Is.False);
            Assert.That(target.Character.Health, Is.Zero);
            Assert.That(target.Downed.DownedHealth, Is.Zero);
            AssertSession(target, RecoveryKind.None, false);
            Assert.That((bool)target.Recovery.SelfReviveUsed, Is.False);
            NetworkLootContainer corpse = target.Object.GetComponent<NetworkLootContainer>();
            Assert.That(corpse, Is.Not.Null);
            Assert.That(ItemCount(target) + corpse.GetLootAmount(SelfReviver), Is.EqualTo(1),
                "Defeat may move the item into the corpse, but recovery must not consume it.");
            Assert.That(target.Recovery.LastOutcomeKind, Is.EqualTo(RecoveryOutcomeKind.Interrupted));
            Assert.That(target.Recovery.LastOutcomeSessionKind, Is.EqualTo(RecoveryKind.Self));
            Assert.That(target.Recovery.LastOutcomeSequence, Is.EqualTo(1));
            Assert.That(target.Recovery.LastOutcomeReason,
                Is.EqualTo(DownedRecoveryInterruptReason.DownedDefeated).Or.EqualTo(DownedRecoveryInterruptReason.DownedDamaged));
            Assert.That(target.Recovery.TryBeginSelf(), Is.False);
        }

        [UnityTest]
        public IEnumerator Self_ExpeditionQuota_RejectsSecondUseDespiteNewItemAndDownedCycle()
        {
            yield return StartRunnerAndLoadPlayer();
            Avatar target = SpawnAvatar(Vector3.zero);
            SetField(target.Recovery, "_selfReviveDurationSeconds", 0.3f);
            yield return GiveSelfReviver(target);
            yield return Down(target);
            int cycle = target.Downed.DownedCycle;
            Assert.That(target.Recovery.TryBeginSelf(), Is.True);
            yield return WaitUntil(() => !target.Character.IsDowned);
            AssertSelfCompleted(target, 1);
            yield return GiveSelfReviver(target);
            yield return Down(target);
            Assert.That(target.Downed.DownedCycle, Is.EqualTo(cycle + 1));
            float reserve = target.Downed.DownedHealth;
            int revision = target.Receiver.Revision;

            Assert.That(target.Recovery.CanBeginSelf(), Is.False);
            Assert.That(target.Recovery.TryBeginSelf(), Is.False);
            yield return WaitFrames(4);
            AssertDowned(target, 1);
            AssertSession(target, RecoveryKind.None, false);
            Assert.That((bool)target.Recovery.SelfReviveUsed, Is.True);
            Assert.That(target.Receiver.Revision, Is.EqualTo(revision));
            Assert.That(target.Downed.DownedHealth, Is.LessThan(reserve));
            AssertOutcome(target, RecoveryOutcomeKind.Completed, DownedRecoveryInterruptReason.None, RecoveryKind.Self, 1);
        }

        [UnityTest]
        public IEnumerator Self_RealInteractPress_LocksVoluntaryMovementUntilCompletion()
        {
            yield return StartRunnerAndLoadPlayer();
            Avatar target = SpawnAvatar(Vector3.zero, localInput: true);
            SetField(target.Recovery, "_selfReviveDurationSeconds", 0.8f);
            yield return GiveSelfReviver(target);
            yield return Down(target);
            // Positive control: the same input moves this Downed avatar before the channel.
            Vector3 beforeCrawl = target.Object.transform.position;
            _input.Move = Vector2.up;
            yield return WaitFrames(6);
            Assert.That(Vector3.Distance(target.Object.transform.position, beforeCrawl), Is.GreaterThan(0.01f));
            _input.Move = Vector2.zero;
            yield return WaitFrames(3);
            _input.InteractHeld = true;
            yield return WaitUntil(() => target.Recovery.IsSelfReviveChanneling);
            AssertSession(target, RecoveryKind.Self, true);
            Vector3 channelPosition = target.Object.transform.position;
            float reserve = target.Downed.DownedHealth;
            _input.Move = Vector2.up;
            yield return WaitFrames(10);

            Assert.That(Vector3.Distance(target.Object.transform.position, channelPosition), Is.LessThan(0.001f));
            Assert.That((bool)target.Movement.IsMoving, Is.False);
            Assert.That(target.Movement.LastAppliedDisplacement, Is.EqualTo(Vector2.zero));
            Assert.That(target.Recovery.IsSelfReviveChanneling, Is.True);
            AssertSession(target, RecoveryKind.Self, true);
            AssertDowned(target, 1);
            Assert.That(target.Downed.DownedHealth, Is.EqualTo(reserve));
            Assert.That((bool)target.Recovery.SelfReviveUsed, Is.False);
            AssertOutcome(target, RecoveryOutcomeKind.None, DownedRecoveryInterruptReason.None, RecoveryKind.None, 0);
            _input.Move = Vector2.zero;
            _input.InteractHeld = false;
            yield return WaitUntil(() => !target.Character.IsDowned);
            AssertSelfCompleted(target, 1);
            _input.Move = Vector2.up;
            yield return WaitFrames(4);
            Assert.That(Vector3.Distance(target.Object.transform.position, channelPosition), Is.GreaterThan(0.01f),
                "Movement unlocks after the channel.");
        }

        [UnityTest]
        public IEnumerator Handoff_Tap_TransfersWithoutPausingDrainThenAutomaticallySelfRevives()
        {
            yield return StartRunnerAndLoadPlayer();
            Avatar target = SpawnAvatar(Vector3.zero);
            Avatar giver = SpawnAvatar(Vector3.right);
            SetField(target.Recovery, "_selfReviveDurationSeconds", 0.6f);
            yield return GiveSelfReviver(giver);
            yield return Down(target);
            Assert.That(target.Recovery.CanOfferHandoff(giver.Character), Is.True);
            int giverRevision = giver.Receiver.Revision;
            int targetRevision = target.Receiver.Revision;
            yield return TapIntoHandoff(target, giver);
            AssertDowned(target, 0);
            float reserve = target.Downed.DownedHealth;
            yield return WaitFrames(5);
            AssertSession(target, RecoveryKind.Handoff, false);
            Assert.That(target.Downed.DownedHealth, Is.LessThan(reserve), "Handoff does not pause actual drain.");
            Assert.That(ItemCount(giver), Is.EqualTo(1), "Transfer waits for handoff completion.");
            Assert.That(giver.Receiver.Revision, Is.EqualTo(giverRevision));
            Assert.That(target.Receiver.Revision, Is.EqualTo(targetRevision));
            Assert.That((bool)target.Recovery.SelfReviveUsed, Is.False);
            AssertOutcome(target, RecoveryOutcomeKind.None, DownedRecoveryInterruptReason.None, RecoveryKind.None, 0);
            yield return WaitUntil(() => target.Recovery.Kind == RecoveryKind.Self);

            AssertSession(target, RecoveryKind.Self, true);
            AssertDowned(target, 1);
            Assert.That(ItemCount(giver), Is.Zero);
            Assert.That(giver.Receiver.Revision, Is.EqualTo(giverRevision + 1));
            Assert.That(target.Receiver.Revision, Is.EqualTo(targetRevision + 1));
            Assert.That((bool)target.Recovery.SelfReviveUsed, Is.False);
            Assert.That(giver.Recovery.RevivingTargetId.IsValid, Is.False);
            Assert.That(target.Recovery.LastOutcomeReviverId, Is.EqualTo(giver.Object.Id));
            AssertOutcome(target, RecoveryOutcomeKind.Completed, DownedRecoveryInterruptReason.None, RecoveryKind.Handoff, 1);
            float selfReserve = target.Downed.DownedHealth;
            yield return WaitFrames(5);
            Assert.That(target.Downed.DownedHealth, Is.EqualTo(selfReserve));
            yield return WaitUntil(() => !target.Character.IsDowned);
            AssertSelfCompleted(target, 2);
            Assert.That(ItemCount(giver), Is.Zero);
            Assert.That(target.Receiver.Revision, Is.EqualTo(targetRevision + 2));
            Assert.That((bool)giver.Recovery.SelfReviveUsed, Is.False);
        }

        [UnityTest]
        public IEnumerator Handoff_HoldPastTapWindow_RemainsAssistedRestoresOneHealthAndKeepsGiverItem()
        {
            yield return StartRunnerAndLoadPlayer();
            Avatar target = SpawnAvatar(Vector3.zero);
            Avatar giver = SpawnAvatar(Vector3.right);
            SetField(target.Recovery, "_reviveDurationSeconds", 0.8f);
            yield return GiveSelfReviver(giver);
            yield return Down(target);
            int revision = giver.Receiver.Revision;
            SetHeld(giver, true);
            Assert.That(target.Recovery.TryBeginAssisted(giver.Character), Is.True);
            float reserve = target.Downed.DownedHealth;
            yield return WaitFrames(Mathf.CeilToInt(0.3f / _runner.DeltaTime) + 2);
            AssertSession(target, RecoveryKind.Assisted, true);
            AssertDowned(target, 0);
            Assert.That(target.Downed.DownedHealth, Is.EqualTo(reserve));
            Assert.That(ItemCount(giver), Is.EqualTo(1));
            yield return WaitUntil(() => !target.Character.IsDowned);

            Assert.That(target.Character.IsAlive, Is.True);
            Assert.That(target.Character.Health, Is.EqualTo(1f));
            Assert.That(target.Stamina.CurrentStamina, Is.Zero);
            Assert.That(target.Downed.DownedHealth, Is.Zero);
            AssertSession(target, RecoveryKind.None, false);
            Assert.That(ItemCount(target), Is.Zero);
            Assert.That(ItemCount(giver), Is.EqualTo(1));
            Assert.That(giver.Receiver.Revision, Is.EqualTo(revision));
            Assert.That((bool)target.Recovery.SelfReviveUsed, Is.False);
            Assert.That((bool)giver.Recovery.SelfReviveUsed, Is.False);
            AssertOutcome(target, RecoveryOutcomeKind.Completed, DownedRecoveryInterruptReason.None, RecoveryKind.Assisted, 1);
        }

        [UnityTest]
        public IEnumerator Handoff_TargetAlreadyCarriesItemOrSpentQuota_TapInterruptsWithoutTransfer()
        {
            yield return StartRunnerAndLoadPlayer();
            Avatar target = SpawnAvatar(Vector3.zero);
            Avatar giver = SpawnAvatar(Vector3.right);
            yield return GiveSelfReviver(target);
            yield return GiveSelfReviver(giver);
            Assert.That(target.Receiver.TryResolveDefinition(SelfReviver, out LootDefinition definition), Is.True);
            Assert.That(definition.MaxTransportable, Is.EqualTo(1));
            yield return Down(target);
            Assert.That(target.Recovery.CanOfferHandoff(giver.Character), Is.False);
            yield return AssertBlockedTap(target, giver, 1, false, 1);

            // Spend the target's quota through the public recovery path, not a networked-property write.
            SetField(target.Recovery, "_selfReviveDurationSeconds", 0.3f);
            Assert.That(target.Recovery.TryBeginSelf(), Is.True);
            yield return WaitUntil(() => !target.Character.IsDowned);
            AssertSelfCompleted(target, 2);
            yield return Down(target);
            Assert.That(ItemCount(target), Is.Zero, "Quota alone blocks the second handoff.");
            Assert.That(target.Recovery.CanOfferHandoff(giver.Character), Is.False);
            yield return AssertBlockedTap(target, giver, 0, true, 3);
        }

        [UnityTest]
        public IEnumerator Handoff_GiverLeavesRangeOrTakesDamage_InterruptsWithoutTransfer()
        {
            yield return StartRunnerAndLoadPlayer();
            Avatar target = SpawnAvatar(Vector3.zero);
            Avatar giver = SpawnAvatar(Vector3.right);
            SetField(target.Recovery, "_handoffDurationSeconds", 2f);
            yield return GiveSelfReviver(giver);
            yield return Down(target);
            int giverRevision = giver.Receiver.Revision;
            int targetRevision = target.Receiver.Revision;
            yield return TapIntoHandoff(target, giver);
            giver.Object.GetComponent<NetworkTransform>().Teleport(new Vector3(4f, 0f, 0f));
            yield return WaitUntil(() => target.Recovery.Kind == RecoveryKind.None);
            AssertHandoffInterrupted(target, giver, DownedRecoveryInterruptReason.OutOfRange, 1, giverRevision, targetRevision);
            float reserve = target.Downed.DownedHealth;
            yield return WaitFrames(4);
            Assert.That(target.Downed.DownedHealth, Is.LessThan(reserve));

            giver.Object.GetComponent<NetworkTransform>().Teleport(Vector3.right);
            yield return WaitFrames(3);
            yield return TapIntoHandoff(target, giver);
            float giverHealth = giver.Character.Health;
            yield return Hit(giver, 1f);
            yield return WaitUntil(() => target.Recovery.Kind == RecoveryKind.None);
            Assert.That(giver.Character.Health, Is.EqualTo(giverHealth - 1f));
            AssertHandoffInterrupted(target, giver, DownedRecoveryInterruptReason.ReviverDamaged, 2, giverRevision, targetRevision);
        }

        private IEnumerator AssertBlockedTap(Avatar target, Avatar giver, int targetItems, bool quotaUsed, int sequence)
        {
            int targetRevision = target.Receiver.Revision;
            int giverRevision = giver.Receiver.Revision;
            SetHeld(giver, true);
            Assert.That(target.Recovery.TryBeginAssisted(giver.Character), Is.True);
            AssertSession(target, RecoveryKind.Assisted, true);
            SetHeld(giver, false);
            yield return WaitUntil(() => target.Recovery.Kind == RecoveryKind.None);
            AssertSession(target, RecoveryKind.None, false);
            AssertDowned(target, targetItems);
            Assert.That(ItemCount(giver), Is.EqualTo(1));
            Assert.That(target.Receiver.Revision, Is.EqualTo(targetRevision));
            Assert.That(giver.Receiver.Revision, Is.EqualTo(giverRevision));
            Assert.That((bool)target.Recovery.SelfReviveUsed, Is.EqualTo(quotaUsed));
            AssertOutcome(target, RecoveryOutcomeKind.Interrupted, DownedRecoveryInterruptReason.InteractReleased, RecoveryKind.Assisted, sequence);
            float reserve = target.Downed.DownedHealth;
            yield return WaitFrames(4);
            Assert.That(target.Downed.DownedHealth, Is.LessThan(reserve));
        }

        private IEnumerator TapIntoHandoff(Avatar target, Avatar giver)
        {
            SetHeld(giver, true);
            Assert.That(target.Recovery.TryBeginAssisted(giver.Character), Is.True);
            AssertSession(target, RecoveryKind.Assisted, true);
            SetHeld(giver, false);
            yield return WaitUntil(() => target.Recovery.Kind != RecoveryKind.Assisted);
            AssertSession(target, RecoveryKind.Handoff, false);
            Assert.That(target.Recovery.IsSelfReviveChanneling, Is.False);
            Assert.That(target.Recovery.ReviverId, Is.EqualTo(giver.Object.Id));
        }

        private static void AssertHandoffInterrupted(Avatar target, Avatar giver,
            DownedRecoveryInterruptReason reason, int sequence, int giverRevision, int targetRevision)
        {
            AssertSession(target, RecoveryKind.None, false);
            AssertDowned(target, 0);
            Assert.That(ItemCount(giver), Is.EqualTo(1));
            Assert.That(giver.Receiver.Revision, Is.EqualTo(giverRevision));
            Assert.That(target.Receiver.Revision, Is.EqualTo(targetRevision));
            Assert.That((bool)target.Recovery.SelfReviveUsed, Is.False);
            Assert.That(giver.Character.IsAlive, Is.True);
            Assert.That(giver.Character.IsDowned, Is.False);
            Assert.That(giver.Recovery.RevivingTargetId.IsValid, Is.False);
            AssertOutcome(target, RecoveryOutcomeKind.Interrupted, reason, RecoveryKind.Handoff, sequence);
        }

        private static void AssertSession(Avatar target, RecoveryKind kind, bool drainPaused)
        {
            Assert.That(target.Recovery.Kind, Is.EqualTo(kind));
            Assert.That(target.Recovery.ActiveSessionKind, Is.EqualTo(kind));
            Assert.That(target.Recovery.HasValidSession, Is.EqualTo(kind != RecoveryKind.None));
            Assert.That(target.Recovery.IsDrainPaused, Is.EqualTo(drainPaused));
            Assert.That(target.Recovery.IsSelfReviveChanneling, Is.EqualTo(kind == RecoveryKind.Self));
        }

        private static void AssertDowned(Avatar target, int items)
        {
            Assert.That(target.Character.IsDowned, Is.True);
            Assert.That(target.Character.IsAlive, Is.True);
            Assert.That(target.Character.Health, Is.Zero);
            Assert.That(target.Downed.DownedHealth, Is.GreaterThan(0f));
            Assert.That(ItemCount(target), Is.EqualTo(items));
        }

        private static void AssertSelfCompleted(Avatar target, int sequence)
        {
            Assert.That(target.Character.IsDowned, Is.False);
            Assert.That(target.Character.IsAlive, Is.True);
            Assert.That(target.Character.Health, Is.EqualTo(25f));
            Assert.That(target.Stamina.CurrentStamina, Is.Zero);
            Assert.That(target.Downed.DownedHealth, Is.Zero);
            Assert.That(ItemCount(target), Is.Zero);
            Assert.That((bool)target.Recovery.SelfReviveUsed, Is.True);
            AssertSession(target, RecoveryKind.None, false);
            AssertOutcome(target, RecoveryOutcomeKind.Completed, DownedRecoveryInterruptReason.None, RecoveryKind.Self, sequence);
        }

        private static void AssertOutcome(Avatar target, RecoveryOutcomeKind outcome,
            DownedRecoveryInterruptReason reason, RecoveryKind sessionKind, int sequence)
        {
            Assert.That(target.Recovery.LastOutcomeKind, Is.EqualTo(outcome));
            Assert.That(target.Recovery.LastOutcomeReason, Is.EqualTo(reason));
            Assert.That(target.Recovery.LastOutcomeSessionKind, Is.EqualTo(sessionKind));
            Assert.That(target.Recovery.LastOutcomeSequence, Is.EqualTo(sequence));
        }

        private static int ItemCount(Avatar avatar) => avatar.Receiver.GetLootAmount(SelfReviver);

        private struct Avatar
        {
            internal NetworkObject Object;
            internal PlayerCharacter Character;
            internal PlayerDownedStateNetworkController Downed;
            internal PlayerDownedRecoveryNetworkController Recovery;
            internal PlayerInteractionNetworkController Interaction;
            internal PlayerMovementNetworkController Movement;
            internal PlayerStaminaNetworkController Stamina;
            internal PlayerLootReceiver Receiver;
        }

        private Avatar SpawnAvatar(Vector3 position, bool localInput = false)
        {
            NetworkObject playerObject = localInput
                ? _runner.Spawn(_playerPrefab, position, Quaternion.identity, _runner.LocalPlayer)
                : _runner.Spawn(_playerPrefab, position, Quaternion.identity);
            ExtractionProgressTestAssertions.AssertRegistered(_runner, playerObject);
            var avatar = new Avatar
            {
                Object = playerObject,
                Character = playerObject.GetComponent<PlayerCharacter>(),
                Downed = playerObject.GetComponent<PlayerDownedStateNetworkController>(),
                Recovery = playerObject.GetComponent<PlayerDownedRecoveryNetworkController>(),
                Interaction = playerObject.GetComponent<PlayerInteractionNetworkController>(),
                Movement = playerObject.GetComponent<PlayerMovementNetworkController>(),
                Stamina = playerObject.GetComponent<PlayerStaminaNetworkController>(),
                Receiver = playerObject.GetComponent<PlayerLootReceiver>()
            };
            Assert.That(avatar.Recovery, Is.Not.Null);
            avatar.Recovery.TestTeamOverride = true;
            SetField(avatar.Downed, "_downedDrainPerSecond", 6f);
            SetField(avatar.Recovery, "_reviveDurationSeconds", 30f);
            _driver.AllowDowned = true;
            return avatar;
        }

        private IEnumerator GiveSelfReviver(Avatar avatar)
        {
            int sequence = _inventory.CompletionSequence;
            _inventory.RequestInitializeLoadout(avatar.Receiver, new[] { new LootEntry(SelfReviver, 1) });
            yield return WaitUntil(() => _inventory.CompletionSequence != sequence);
            Assert.That(_inventory.LastResult, Is.True, _inventory.LastError);
            Assert.That(ItemCount(avatar), Is.EqualTo(1));
        }

        private IEnumerator Down(Avatar target)
        {
            yield return Hit(target, 100000f);
            Assert.That(target.Character.IsDowned, Is.True);
            Assert.That(target.Character.Health, Is.Zero);
        }

        private IEnumerator Hit(Avatar avatar, float amount)
        {
            _driver.Target = avatar.Character;
            _driver.Receiver = avatar.Receiver;
            _driver.SetEntries(Array.Empty<LootEntry>());
            _driver.RequestedDamageAmount = amount;
            _driver.IsRequested = true;
            yield return WaitUntil(() => !_driver.IsRequested);
            yield return WaitFrames(2);
        }

        private static void SetHeld(Avatar avatar, bool held)
        {
            SetNetworkedProperty(avatar.Interaction, nameof(PlayerInteractionNetworkController.IsInteractHeld), (NetworkBool)held);
        }

        private IEnumerator WaitFrames(int ticks)
        {
            int targetTick = _runner.Tick.Raw + ticks;
            float deadline = Time.realtimeSinceStartup + 10f;
            while (_runner.Tick.Raw < targetTick && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            Assert.That(_runner.Tick.Raw, Is.GreaterThanOrEqualTo(targetTick), "Simulation did not advance.");
        }

        private static IEnumerator WaitUntil(Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + 8f;
            while (!condition() && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            Assert.That(condition(), Is.True, "Timed out waiting for the expected simulation state.");
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(target, value);
        }

        private static T GetField<T>(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            return (T)field.GetValue(target);
        }

        private static void SetNetworkedProperty(object target, string name, object value)
        {
            PropertyInfo property = target.GetType().GetProperty(name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null, name);
            property.SetValue(target, value);
        }

        private IEnumerator StartRunnerAndLoadPlayer()
        {
            var runnerObject = new GameObject("PlayerSelfReviveTestRunner");
            _runner = runnerObject.AddComponent<NetworkRunner>();
            PlayModeRunnerComposition.AddLocalInputContext(_runner);
            runnerObject.AddComponent<EntityRegistry>();
            _driver = runnerObject.AddComponent<PlayerCorpseGenerationSimulationDriver>();
            _inventory = runnerObject.AddComponent<PlayerEquipmentSimulationDriver>();
            _input = runnerObject.AddComponent<SelfReviveInputDriver>();
            _runner.AddCallbacks(_input);
            _runner.ProvideInput = true;
            var startTask = _runner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Single,
                SessionName = $"self-revive-{Guid.NewGuid():N}",
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

        private sealed class SelfReviveInputDriver : NetworkRunnerCallbacksAdapter
        {
            public bool InteractHeld { get; set; }
            public Vector2 Move { get; set; }

            public override void OnInput(NetworkRunner runner, NetworkInput input)
            {
                PlayerNetworkInput playerInput = default;
                playerInput.MoveDirection = Move;
                playerInput.AimWorldPosition = new Vector2(10f, 0f);
                playerInput.Buttons.Set(PlayerInputButton.Interact, InteractHeld);
                input.Set(playerInput);
            }
        }
    }
}
#endif
