#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using Fusion;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEditor;
using Assert = NUnit.Framework.Assert;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Combat
{
    public sealed class PlayerCombatNetworkControllerPlayModeTests
    {
        private const string BasePrefabGuid = "fea3a7b256f965a4eb9b965832939741";
        private const string MatchPrefabGuid = "b91f8c7e96a4d784a92c3bd1a88dfec8";
        private const string ParticipantPrefabGuid = "c39d451563bae6e43934008a0dadc6d6";
        private const string MissingExtractionProgressDependenciesMessage =
            "PlayerExtractionProgressController requires character, extraction controller, registry, assignment service, and valid receiver/reader registrations.";

        private static readonly PropertyInfo PreviousButtonsProperty = GetProperty("PreviousButtons");
        private static readonly PropertyInfo AttackCooldownProperty = GetProperty("AttackCooldown");
        private static readonly PropertyInfo HasActiveAttackProperty = GetProperty("HasActiveAttack");
        private static readonly PropertyInfo CooldownDurationProperty =
            GetProperty("AttackCooldownDurationSeconds");
        private static readonly PropertyInfo AttackSequenceProperty = GetProperty("AttackSequence");
        private static readonly PropertyInfo LastAttackDirectionProperty =
            GetProperty("LastAttackDirection");
        private static readonly FieldInfo ActiveAttackField = GetField("_activeAttack");
        private static readonly FieldInfo ActiveAttackSourceField = GetField("_activeAttackSource");
        private static readonly MethodInfo CacheDependenciesMethod =
            GetMethod("CacheDependencies");

        private static readonly PropertyInfo PendingReleaseProperty = GetProperty("PendingRangedRelease");
        private NetworkRunner _runner;
        private PlayerCombatInputDriver _inputDriver;
        private PlayerCombatStrategySimulationDriver _strategyDriver;

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
                Object.DestroyImmediate(_runner.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator NeutralAssignmentAndCooldownFlow_UsesAuthoritativeStrategyPresence()
        {
            yield return StartGameplayRunner();
            LogAssert.Expect(UnityEngine.LogType.Error, MissingExtractionProgressDependenciesMessage);
            NetworkObject playerObject = SpawnPlayerWithParticipant();
            PlayerCombatNetworkController controller =
                playerObject.GetComponent<PlayerCombatNetworkController>();

            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.HasStateAuthority, Is.True);
            Assert.That(controller.HasInputAuthority, Is.True);
            Assert.That(ReadHasActiveAttack(controller), Is.False);
            Assert.That(ReadCooldown(controller), Is.EqualTo(TickTimer.None));
            Assert.That(ReadCooldownDuration(controller), Is.Zero);
            Assert.That(controller.TryGetPrimaryAttackStatus(out _), Is.False);

            int feedbackCount = 0;
            int performedCount = 0;
            controller.CombatFeedbackResolved += _ => feedbackCount++;
            controller.AttackPerformed += _ => performedCount++;
            int neutralSequence = ReadAttackSequence(controller);

            _inputDriver.AttackHeld = true;
            yield return WaitUntil(
                () => ReadPreviousButtons(controller).IsSet(PlayerInputButton.PrimaryAttack),
                "Neutral combat did not retain the current button history.");
            Assert.That(ReadAttackSequence(controller), Is.EqualTo(neutralSequence));
            Assert.That(ReadCooldown(controller), Is.EqualTo(TickTimer.None));
            Assert.That(ReadCooldownDuration(controller), Is.Zero);
            Assert.That(feedbackCount, Is.Zero);
            Assert.That(performedCount, Is.Zero);
            _inputDriver.AttackHeld = false;
            yield return WaitUntil(
                () => !ReadPreviousButtons(controller).IsSet(PlayerInputButton.PrimaryAttack),
                "Neutral combat did not consume the button release.");

            PlayerCombatTestAttack firstAttack =
                playerObject.gameObject.AddComponent<PlayerCombatTestAttack>();
            firstAttack.Initialize(AttackType.Melee, 0.4f);
            PlayerCombatTestAttack zeroCooldownAttack =
                playerObject.gameObject.AddComponent<PlayerCombatTestAttack>();
            zeroCooldownAttack.Initialize(AttackType.Ranged, 0f);

            yield return SetStrategy(controller, firstAttack, true);
            Assert.That(ReadHasActiveAttack(controller), Is.True);
            Assert.That(controller.TryGetPrimaryAttackStatus(out PrimaryAttackStatus ready), Is.True);
            Assert.That(ready.IsAvailable, Is.True);

            yield return SetAttackEnabled(controller, false, true);
            Assert.That(controller.TryGetPrimaryAttackStatus(out PrimaryAttackStatus disabled), Is.True);
            Assert.That(disabled.IsAvailable, Is.False);
            yield return SetAttackEnabled(controller, true, true);

            LogAssert.Expect(
                UnityEngine.LogType.Error,
                "PlayerCombatNetworkController: Cannot set active attack to null.");
            yield return SetStrategy(controller, null, false);
            Assert.That(ReadActiveAttack(controller), Is.SameAs(firstAttack));
            Assert.That(ReadActiveAttackSource(controller), Is.SameAs(firstAttack));
            Assert.That(ReadHasActiveAttack(controller), Is.True);

            TickTimer timerBeforeInvalidAssignment = ReadCooldown(controller);
            float durationBeforeInvalidAssignment = ReadCooldownDuration(controller);
            LogAssert.Expect(
                UnityEngine.LogType.Error,
                new Regex("PlayerCombatNetworkController: The component .* does not implement IAttack\\."));
            yield return SetStrategy(controller, controller, false);
            Assert.That(ReadActiveAttack(controller), Is.SameAs(firstAttack));
            Assert.That(ReadActiveAttackSource(controller), Is.SameAs(firstAttack));
            Assert.That(ReadHasActiveAttack(controller), Is.True);
            Assert.That(ReadCooldown(controller), Is.EqualTo(timerBeforeInvalidAssignment));
            Assert.That(ReadCooldownDuration(controller), Is.EqualTo(durationBeforeInvalidAssignment));

            yield return PressAttackUntil(
                controller,
                () => firstAttack.ExecutionCount == 1,
                "The assigned attack did not execute from Fusion input.");
            int firstSequence = ReadAttackSequence(controller);
            TickTimer firstTimer = ReadCooldown(controller);
            Assert.That(firstTimer, Is.Not.EqualTo(TickTimer.None));
            Assert.That(ReadCooldownDuration(controller), Is.EqualTo(0.4f).Within(0.0001f));

            yield return ClearStrategy(controller, true);
            Assert.That(ReadHasActiveAttack(controller), Is.False);
            Assert.That(ReadActiveAttack(controller), Is.Null);
            Assert.That(ReadActiveAttackSource(controller), Is.Null);
            Assert.That(ReadCooldown(controller), Is.EqualTo(firstTimer));
            Assert.That(ReadCooldownDuration(controller), Is.EqualTo(0.4f).Within(0.0001f));
            Assert.That(controller.TryGetPrimaryAttackStatus(out _), Is.False);

            CacheDependenciesMethod.Invoke(controller, null);
            Assert.That(ReadHasActiveAttack(controller), Is.False);
            Assert.That(ReadActiveAttack(controller), Is.Null);
            Assert.That(ReadActiveAttackSource(controller), Is.Null);

            yield return SetStrategy(controller, zeroCooldownAttack, true);
            Assert.That(ReadCooldown(controller), Is.EqualTo(firstTimer));
            Assert.That(ReadCooldownDuration(controller), Is.EqualTo(0.4f).Within(0.0001f));
            Assert.That(controller.TryGetPrimaryAttackStatus(out PrimaryAttackStatus blocked), Is.True);
            Assert.That(blocked.IsAvailable, Is.False);
            Assert.That(blocked.CooldownDurationSeconds, Is.EqualTo(0.4f).Within(0.0001f));
            Assert.That(blocked.CooldownRemainingSeconds, Is.GreaterThan(0f));

            yield return PressAttackForFrames(controller, 5);
            Assert.That(zeroCooldownAttack.ExecutionCount, Is.Zero);
            Assert.That(ReadAttackSequence(controller), Is.EqualTo(firstSequence));
            Assert.That(ReadCooldown(controller), Is.EqualTo(firstTimer));

            yield return WaitUntil(
                () => controller.TryGetPrimaryAttackStatus(out PrimaryAttackStatus status) &&
                    status.IsAvailable,
                "The preserved cooldown did not expire.");
            yield return PressAttackUntil(
                controller,
                () => zeroCooldownAttack.ExecutionCount == 1,
                "The replacement attack did not execute after the prior cooldown expired.");
            Assert.That(ReadAttackSequence(controller), Is.EqualTo(firstSequence + 1));
            Assert.That(ReadCooldown(controller), Is.EqualTo(TickTimer.None));
            Assert.That(ReadCooldownDuration(controller), Is.Zero);
            Assert.That(controller.TryGetPrimaryAttackStatus(out PrimaryAttackStatus zeroStatus), Is.True);
            Assert.That(zeroStatus.IsAvailable, Is.True);
            Assert.That(zeroStatus.CooldownDurationSeconds, Is.Zero);
            Assert.That(zeroStatus.CooldownRemainingSeconds, Is.Zero);

            yield return SetStrategy(controller, firstAttack, true);
            yield return PressAttackUntil(
                controller,
                () => firstAttack.ExecutionCount == 2,
                "The first strategy did not execute a second time.");
            int sequenceBeforeMissingImplementation = ReadAttackSequence(controller);
            ActiveAttackField.SetValue(controller, null);

            Assert.That(ReadHasActiveAttack(controller), Is.True);
            Assert.That(controller.TryGetPrimaryAttackStatus(out PrimaryAttackStatus proxyStatus), Is.True);
            Assert.That(proxyStatus.IsAvailable, Is.False);
            Assert.That(proxyStatus.CooldownDurationSeconds, Is.EqualTo(0.4f).Within(0.0001f));
            yield return SetAttackEnabled(controller, false, true);
            yield return WaitUntil(
                () => controller.TryGetPrimaryAttackStatus(out PrimaryAttackStatus status) &&
                    status.CooldownRemainingSeconds <= 0f,
                "The replicated cooldown did not expire while combat was disabled.");
            Assert.That(controller.TryGetPrimaryAttackStatus(out PrimaryAttackStatus disabledProxyStatus), Is.True);
            Assert.That(disabledProxyStatus.IsAvailable, Is.False);

            yield return SetAttackEnabled(controller, true, true);
            Assert.That(controller.TryGetPrimaryAttackStatus(out PrimaryAttackStatus enabledProxyStatus), Is.True);
            Assert.That(enabledProxyStatus.IsAvailable, Is.True);

            PlayerCharacter character = playerObject.GetComponent<PlayerCharacter>();
            DisableDownedEntry(playerObject);
            TickTimer cooldownBeforeDeath = ReadCooldown(controller);
            yield return DefeatCharacter(character, true);
            Assert.That(character.IsAlive, Is.False);
            Assert.That(character.IsDowned, Is.False);
            Assert.That(ReadHasActiveAttack(controller), Is.False, "Definitive corpse conversion clears Equipment's active strategy.");
            Assert.That(controller.TryGetPrimaryAttackStatus(out _), Is.False);
            Assert.That(ReadCooldown(controller), Is.EqualTo(cooldownBeforeDeath));
            Assert.That(ReadCooldownDuration(controller), Is.EqualTo(0.4f).Within(0.0001f));

            // Isolate dead-character readiness from the neutral state produced by corpse conversion.
            // Strategy presence is assigned through the same State Authority API as the rest of this test.
            yield return SetStrategy(controller, firstAttack, true);
            ActiveAttackField.SetValue(controller, null);
            Assert.That(ReadHasActiveAttack(controller), Is.True);
            Assert.That(controller.TryGetPrimaryAttackStatus(out PrimaryAttackStatus defeatedProxyStatus), Is.True);
            Assert.That(defeatedProxyStatus.IsAvailable, Is.False);

            yield return PressAttackForFrames(controller, 5);
            Assert.That(firstAttack.ExecutionCount, Is.EqualTo(2));
            Assert.That(ReadAttackSequence(controller), Is.EqualTo(sequenceBeforeMissingImplementation));
        }

        [UnityTest]
        public IEnumerator FreshPlayer_InitializesNeutralAndEquipmentAssignsAuthoritativeAttackPresence()
        {
            yield return StartGameplayRunner();
            // The productive prefab contains extraction progress; this combat fixture deliberately
            // has no sanctuary assignment service. Expect its actual dependency diagnostic once.
            LogAssert.Expect(UnityEngine.LogType.Error, MissingExtractionProgressDependenciesMessage);
            NetworkObject player = SpawnPlayerWithParticipant();
            var combat = player.GetComponent<PlayerCombatNetworkController>();
            var equipment = player.GetComponent<PlayerWeaponEquipmentNetworkController>();
            var receiver = player.GetComponent<PlayerLootReceiver>();
            Assert.That(equipment.HasStateAuthority, Is.True);
            Assert.That(equipment.HasInputAuthority, Is.True);
            Assert.That(equipment.HasAnyEquipment, Is.False);
            Assert.That(ReadHasActiveAttack(combat), Is.False);
            Assert.That(ReadActiveAttack(combat), Is.Null);
            Assert.That(ReadActiveAttackSource(combat), Is.Null);
            Assert.That(combat.TryGetPrimaryAttackStatus(out _), Is.False);
            int neutralSequence = ReadAttackSequence(combat);
            int performedCount = 0;
            int feedbackCount = 0;
            combat.AttackPerformed += _ => performedCount++;
            combat.CombatFeedbackResolved += _ => feedbackCount++;
            yield return PressAttackUntil(combat,
                () => ReadPreviousButtons(combat).IsSet(PlayerInputButton.PrimaryAttack),
                "The fresh neutral player did not consume primary input.");
            Assert.That(ReadAttackSequence(combat), Is.EqualTo(neutralSequence));
            Assert.That(ReadCooldown(combat), Is.EqualTo(TickTimer.None));
            Assert.That(ReadCooldownDuration(combat), Is.Zero);
            Assert.That((bool)ReadPending(combat).Pending, Is.False);
            Assert.That(performedCount, Is.Zero);
            Assert.That(feedbackCount, Is.Zero);

            var sword = AssetDatabase.LoadAssetAtPath<LootDefinition>("Assets/Scriptable Objects/Loot/Definitions/ArmingSword.asset");
            var wand = AssetDatabase.LoadAssetAtPath<LootDefinition>("Assets/Scriptable Objects/Loot/Definitions/MagicWand.asset");
            Assert.That(sword, Is.Not.Null);
            Assert.That(wand, Is.Not.Null);
            var inventoryDriver = _runner.gameObject.AddComponent<PlayerEquipmentSimulationDriver>();
            _runner.AddGlobal(inventoryDriver);
            int inventorySequence = inventoryDriver.CompletionSequence;
            inventoryDriver.RequestInitializeLoadout(receiver, new[]
            {
                new LootEntry(sword.LootId, 1),
                new LootEntry(wand.LootId, 1)
            });
            yield return WaitUntil(() => inventoryDriver.CompletionSequence != inventorySequence,
                "Raid inventory setup did not run in simulation.");
            Assert.That(inventoryDriver.LastResult, Is.True, inventoryDriver.LastError);
            Assert.That(ReadHasActiveAttack(combat), Is.False, "Inventory ownership alone must not assign an attack.");

            yield return Equip(equipment, sword, EquipmentSlot.WeaponSetAMainHand);
            Assert.That(equipment.TryGetSlotDefinition(EquipmentSlot.WeaponSetAMainHand, out LootDefinition equippedSword), Is.True);
            Assert.That(equippedSword, Is.SameAs(sword));
            Assert.That(receiver.GetLootAmount(sword.LootId), Is.Zero);
            Assert.That(receiver.GetLootAmount(wand.LootId), Is.EqualTo(1));
            Assert.That(ReadHasActiveAttack(combat), Is.True);
            Assert.That(ReadActiveAttack(combat), Is.TypeOf<MeleeAttack>());
            Assert.That(ReadActiveAttackSource(combat), Is.SameAs(player.GetComponent<MeleeAttack>()));
            Assert.That(ReadActiveAttack(combat).CooldownSeconds, Is.EqualTo(sword.WeaponDefinition.AttackIntervalSeconds));
            Assert.That(combat.TryGetPrimaryAttackStatus(out PrimaryAttackStatus meleeStatus), Is.True);
            Assert.That(meleeStatus.IsAvailable, Is.True);

            // Replace the active Main Hand through a real request, not direct strategy assignment.
            yield return Equip(equipment, wand, EquipmentSlot.WeaponSetAMainHand);
            Assert.That(equipment.TryGetSlotDefinition(EquipmentSlot.WeaponSetAMainHand, out LootDefinition equippedWand), Is.True);
            Assert.That(equippedWand, Is.SameAs(wand));
            Assert.That(receiver.GetLootAmount(sword.LootId), Is.EqualTo(1));
            Assert.That(receiver.GetLootAmount(wand.LootId), Is.Zero);
            Assert.That(ReadHasActiveAttack(combat), Is.True);
            Assert.That(ReadActiveAttack(combat), Is.TypeOf<RangedAttack>());
            Assert.That(ReadActiveAttackSource(combat), Is.SameAs(player.GetComponent<RangedAttack>()));
            Assert.That(ReadActiveAttack(combat).CooldownSeconds, Is.EqualTo(wand.WeaponDefinition.AttackIntervalSeconds));
            Assert.That(combat.TryGetPrimaryAttackStatus(out PrimaryAttackStatus rangedStatus), Is.True);
            Assert.That(rangedStatus.IsAvailable, Is.True);
            Assert.That(ReadAttackSequence(combat), Is.EqualTo(neutralSequence));
            Assert.That(ReadCooldown(combat), Is.EqualTo(TickTimer.None));
            Assert.That(ReadCooldownDuration(combat), Is.Zero);
            Assert.That(performedCount, Is.Zero);
            Assert.That(feedbackCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator ContextualAttackFacing_IsConsumedByCombatInTheSameTick()
        {
            yield return StartGameplayRunner();
            LogAssert.Expect(UnityEngine.LogType.Error, MissingExtractionProgressDependenciesMessage);
            NetworkObject playerObject = SpawnPlayerWithParticipant();
            PlayerCombatNetworkController combatController =
                playerObject.GetComponent<PlayerCombatNetworkController>();
            PlayerCombatTestAttack attack =
                playerObject.gameObject.AddComponent<PlayerCombatTestAttack>();
            attack.Initialize(AttackType.Melee, 0f);
            yield return SetStrategy(combatController, attack, true);

            _inputDriver.MoveDirection = Vector2.up;
            _inputDriver.AimWorldPosition = Vector2.right * 10f;
            _inputDriver.AttackHeld = true;
            yield return WaitUntil(
                () => attack.ExecutionCount == 1,
                "Combat did not execute the contextual attack.");
            _inputDriver.AttackHeld = false;
            _inputDriver.MoveDirection = Vector2.zero;

            Vector2 attackDirection =
                (Vector2)LastAttackDirectionProperty.GetValue(combatController);
            Assert.That(attackDirection.x, Is.GreaterThan(0.99f));
            Assert.That(Mathf.Abs(attackDirection.y), Is.LessThan(0.1f));
        }

        [UnityTest]
        public IEnumerator RangedWindup_AdvancesWithoutInputOrActiveStrategyAndKeepsAcceptedPayload()
        {
            yield return StartGameplayRunner();
            LogAssert.Expect(UnityEngine.LogType.Error, MissingExtractionProgressDependenciesMessage);
            NetworkObject player = SpawnPlayerWithParticipant();
            var combat = player.GetComponent<PlayerCombatNetworkController>();
            RangedAttack ranged = ConfigureScheduledAttack(player, 0.6f, 0.05f, out RecordingProjectileSpawner spawner);
            yield return SetStrategy(combat, ranged, true);
            _inputDriver.AimWorldPosition = Vector2.right * 10f;
            yield return PressAttackUntil(combat, () => (bool)ReadPending(combat).Pending, "Wind-up was not accepted.");
            RangedAttackRelease accepted = ReadPending(combat);
            int sequence = ReadAttackSequence(combat);
            Vector2 acceptanceOrigin = spawner.AttackOrigin.position;
            Assert.That(spawner.Count, Is.Zero);
            _inputDriver.AimWorldPosition = Vector2.left * 10f;
            _inputDriver.MoveDirection = Vector2.up;
            yield return PressAttackForFrames(combat, 4);
            Assert.That(ReadAttackSequence(combat), Is.EqualTo(sequence), "An expired cooldown must not overwrite wind-up.");
            Assert.That(ReadPending(combat).ReleaseTick, Is.EqualTo(accepted.ReleaseTick));

            // This is the same shared-executor reconfiguration path used by an Equipment switch.
            var config = AssetDatabase.LoadAssetAtPath<RangedAttackConfig>("Assets/Scriptable Objects/RangePlayerAttackConfig.asset");
            Assert.That(ranged.TryConfigure(config, new AttackExecutionParameters(99f, DamageType.Magical, 0f, 12f, 8f)), Is.True);
            yield return ClearStrategy(combat, true);
            Assert.That(ReadHasActiveAttack(combat), Is.False);
            Assert.That(ReadActiveAttack(combat), Is.Null);
            Assert.That(ReadPending(combat), Is.EqualTo(accepted), "Reconfiguration and clear must retain the complete accepted payload.");
            _inputDriver.MoveDirection = Vector2.zero;
            _runner.ProvideInput = false;
            yield return WaitUntil(() => spawner.Count == 1, "Missing input/strategy stalled the accepted deadline.");
            Assert.That(spawner.WasAuthoritativeForward, Is.True);
            Assert.That(spawner.LastRequest.SimulationTick, Is.EqualTo(accepted.ReleaseTick));
            Assert.That(spawner.LastRequest.Direction.x, Is.GreaterThan(0.99f));
            Assert.That(spawner.LastRequest.Direction, Is.EqualTo(accepted.Direction));
            Assert.That(spawner.LastRequest.ProjectilePrefab, Is.EqualTo(accepted.Prefab));
            Assert.That(spawner.LastRequest.ImpactLayerMask, Is.EqualTo(accepted.ImpactMask));
            Assert.That(spawner.LastRequest.Speed, Is.EqualTo(accepted.Speed));
            Assert.That(spawner.LastRequest.LifetimeSeconds, Is.EqualTo(accepted.Lifetime));
            Assert.That(spawner.LastRequest.KnockbackForce, Is.EqualTo(accepted.Knockback));
            Assert.That(spawner.LastRequest.Damage, Is.EqualTo(28f));
            Assert.That(spawner.LastRequest.DamageType, Is.EqualTo(DamageType.Physical));
            Assert.That(spawner.LastRequest.MaximumRange, Is.EqualTo(6f));
            Assert.That(spawner.LastRequest.Origin,
                Is.EqualTo(spawner.OriginAtSpawn + spawner.LastRequest.Direction * accepted.SpawnOffset));
            Assert.That(spawner.OriginAtSpawn.y, Is.GreaterThan(acceptanceOrigin.y), "Release must sample the moved authoritative origin.");
            for (int i = 0; i < 12; i++) yield return null;
            Assert.That(spawner.Count, Is.EqualTo(1));
            Assert.That((bool)ReadPending(combat).Pending, Is.False);
        }

        [UnityTest]
        public IEnumerator RangedWindup_DisableDownedAndDeathCancelWithoutRefundingCooldown()
        {
            yield return StartGameplayRunner();
            for (int cancellation = 0; cancellation < 3; cancellation++)
            {
                LogAssert.Expect(UnityEngine.LogType.Error, MissingExtractionProgressDependenciesMessage);
                NetworkObject player = SpawnPlayerWithParticipant(Vector3.right * cancellation * 3f, cancellation + 1);
                var combat = player.GetComponent<PlayerCombatNetworkController>();
                RangedAttack ranged = ConfigureScheduledAttack(player, 0.45f, 1.5f, out RecordingProjectileSpawner spawner);
                yield return SetStrategy(combat, ranged, true);
                yield return PressAttackUntil(combat, () => (bool)ReadPending(combat).Pending, "Wind-up was not accepted.");
                TickTimer cooldown = ReadCooldown(combat);
                int deadline = ReadPending(combat).ReleaseTick;
                int acceptedTick = (int)_runner.Tick;
                int sequence = ReadAttackSequence(combat);
                Assert.That(spawner.Count, Is.Zero);
                Assert.That(cooldown.RemainingTime(_runner), Is.GreaterThan(0f));
                if (cancellation == 0) yield return SetAttackEnabled(combat, false, true);
                else
                {
                    if (cancellation == 2)
                    {
                        DisableDownedEntry(player);
                    }
                    yield return DefeatCharacter(player.GetComponent<PlayerCharacter>(), cancellation == 2);
                    Assert.That(PlayerDownedGate.IsDowned(player.GetComponent<PlayerCharacter>()), Is.EqualTo(cancellation == 1));
                    Assert.That(player.GetComponent<PlayerCharacter>().IsAlive, Is.EqualTo(cancellation == 1));
                }
                yield return WaitUntil(() => !(bool)ReadPending(combat).Pending, "Cancellation did not clear wind-up.");
                int cancellationTick = (int)GetProperty("LastAttackCancellationTick").GetValue(combat);
                Assert.That(cancellationTick, Is.GreaterThanOrEqualTo(acceptedTick));
                Assert.That(cancellationTick, Is.LessThan(deadline));
                Assert.That(cooldown.RemainingTime(_runner), Is.GreaterThan(0f), "Cancellation must not refund recovery.");
                Assert.That(ReadCooldown(combat), Is.EqualTo(cooldown));
                Assert.That(ReadCooldownDuration(combat), Is.EqualTo(1.5f));
                yield return WaitUntil(() => (int)_runner.Tick > deadline + 2, "Runner did not cross cancelled deadline.");
                Assert.That(spawner.Count, Is.Zero);
                Assert.That(ReadAttackSequence(combat), Is.EqualTo(sequence));
                Assert.That(ReadCooldown(combat), Is.EqualTo(cooldown));
                _runner.Despawn(player);
            }
        }

        [UnityTest]
        public IEnumerator RangedWindup_PhaseExitCancelsWithoutRefundingCooldown()
        {
            yield return StartRunner(includeMatchController: true);
            NetworkObject matchObject = Spawn(MatchPrefabGuid, null, Vector3.zero);
            var match = matchObject.GetComponent<NetworkMatchController>();
            var phaseDriver = _runner.gameObject.AddComponent<MatchPhaseSimulationDriver>();
            _runner.AddGlobal(phaseDriver);
            Assert.That(match.HasStateAuthority, Is.True);
            Assert.That(_runner.GetComponent<NetworkSpawnManager>().MatchController, Is.SameAs(match));
            yield return SetPhase(phaseDriver, match, NetworkMatchController.MatchPhase.InProgress);

            LogAssert.Expect(UnityEngine.LogType.Error, MissingExtractionProgressDependenciesMessage);
            NetworkObject player = SpawnPlayerWithParticipant();
            var combat = player.GetComponent<PlayerCombatNetworkController>();
            RangedAttack ranged = ConfigureScheduledAttack(player, 0.8f, 1.5f, out RecordingProjectileSpawner spawner);
            yield return SetStrategy(combat, ranged, true);
            Assert.That(GetField("_matchController").GetValue(combat), Is.SameAs(match));
            yield return PressAttackUntil(combat, () => (bool)ReadPending(combat).Pending, "Wind-up was not accepted in the active phase.");
            int deadline = ReadPending(combat).ReleaseTick;
            int sequence = ReadAttackSequence(combat);
            TickTimer cooldown = ReadCooldown(combat);
            Assert.That(spawner.Count, Is.Zero);
            Assert.That(cooldown.RemainingTime(_runner), Is.GreaterThan(0f), "Cooldown must start at acceptance.");
            Assert.That((int)GetProperty("LastAttackCancellationTick").GetValue(combat), Is.EqualTo(-1));

            // Change only the real match phase, not the combat-enabled flag or character state.
            // No input is supplied: cancellation must still run before the input gate.
            _runner.ProvideInput = false;
            yield return SetPhase(phaseDriver, match, NetworkMatchController.MatchPhase.Finished);
            yield return WaitUntil(() => !(bool)ReadPending(combat).Pending, "Phase exit did not cancel the accepted wind-up.");
            int cancellationTick = (int)GetProperty("LastAttackCancellationTick").GetValue(combat);
            Assert.That(cancellationTick, Is.GreaterThanOrEqualTo(phaseDriver.ChangedTick));
            Assert.That(cancellationTick, Is.LessThan(deadline), "The phase must exit before release is due.");
            Assert.That((bool)combat.IsAttackEnabled, Is.True, "This case must exercise phase gating, not explicit disable.");
            Assert.That(player.GetComponent<PlayerCharacter>().IsAlive, Is.True);
            Assert.That(PlayerDownedGate.IsDowned(player.GetComponent<PlayerCharacter>()), Is.False);
            Assert.That(ReadCooldown(combat), Is.EqualTo(cooldown));
            Assert.That(ReadCooldownDuration(combat), Is.EqualTo(1.5f));
            Assert.That(cooldown.RemainingTime(_runner), Is.GreaterThan(0f), "Cancellation must not refund recovery.");

            yield return WaitUntil(() => (int)_runner.Tick > deadline + 3, "Runner did not cross the cancelled release deadline.");
            Assert.That(spawner.Count, Is.Zero);
            Assert.That((bool)ReadPending(combat).Pending, Is.False);
            Assert.That(ReadAttackSequence(combat), Is.EqualTo(sequence));
            Assert.That(ReadCooldown(combat), Is.EqualTo(cooldown));
            Assert.That(ReadCooldownDuration(combat), Is.EqualTo(1.5f));
            Assert.That((int)GetProperty("LastAttackCancellationTick").GetValue(combat), Is.EqualTo(cancellationTick));
        }

        [UnityTest]
        public IEnumerator RangedWindup_DefenseAfterAcceptanceKeepsPayloadAndBlocksNewAcceptance()
        {
            yield return StartRunner();
            LogAssert.Expect(UnityEngine.LogType.Error, MissingExtractionProgressDependenciesMessage);
            NetworkObject player = SpawnPlayerWithParticipant();
            var combat = player.GetComponent<PlayerCombatNetworkController>();
            var equipment = player.GetComponent<PlayerWeaponEquipmentNetworkController>();
            var defense = player.GetComponent<PlayerShieldDefenseNetworkController>();
            var wand = AssetDatabase.LoadAssetAtPath<LootDefinition>("Assets/Scriptable Objects/Loot/Definitions/MagicWand.asset");
            var shield = AssetDatabase.LoadAssetAtPath<LootDefinition>("Assets/Scriptable Objects/Loot/Definitions/Shield.asset");
            Assert.That(wand, Is.Not.Null);
            Assert.That(shield, Is.Not.Null);

            var inventoryDriver = _runner.gameObject.AddComponent<PlayerEquipmentSimulationDriver>();
            _runner.AddGlobal(inventoryDriver);
            int inventorySequence = inventoryDriver.CompletionSequence;
            inventoryDriver.RequestInitializeLoadout(player.GetComponent<PlayerLootReceiver>(), new[]
            {
                new LootEntry(wand.LootId, 1),
                new LootEntry(shield.LootId, 1)
            });
            yield return WaitUntil(() => inventoryDriver.CompletionSequence != inventorySequence, "Raid inventory setup did not run in simulation.");
            Assert.That(inventoryDriver.LastResult, Is.True, inventoryDriver.LastError);
            yield return Equip(equipment, wand, EquipmentSlot.WeaponSetAMainHand);
            yield return Equip(equipment, shield, EquipmentSlot.WeaponSetAOffHand);
            Assert.That(equipment.TryGetActiveShieldDefinition(out ShieldDefinition activeShield), Is.True);
            Assert.That(activeShield, Is.SameAs(shield.ShieldDefinition));

            RangedAttack ranged = ConfigureScheduledAttack(player, 0.9f, 1.5f, out RecordingProjectileSpawner spawner);
            yield return SetStrategy(combat, ranged, true);
            _inputDriver.AimWorldPosition = Vector2.right * 10f;
            yield return PressAttackUntil(combat, () => (bool)ReadPending(combat).Pending, "Wind-up was not accepted before defense.");
            RangedAttackRelease accepted = ReadPending(combat);
            int sequence = ReadAttackSequence(combat);
            TickTimer cooldown = ReadCooldown(combat);
            Assert.That(spawner.Count, Is.Zero);
            Assert.That(cooldown.RemainingTime(_runner), Is.GreaterThan(0f), "Cooldown must start at acceptance.");
            Assert.That(accepted.Direction.x, Is.GreaterThan(0.99f));

            _inputDriver.AimWorldPosition = Vector2.left * 10f;
            _inputDriver.SecondaryHeld = true;
            yield return WaitUntil(() => defense.IsDefending, "The equipped shield did not accept defense input during wind-up.");
            Assert.That(defense.CanDefend(ReadPreviousButtons(combat)), Is.True);
            Assert.That((int)_runner.Tick, Is.LessThan(accepted.ReleaseTick), "Defense must be accepted before release is due.");
            Assert.That(ReadPending(combat), Is.EqualTo(accepted), "Defense must preserve the complete accepted payload and deadline.");
            Assert.That(player.GetComponent<PlayerMovementNetworkController>().FacingDirection.x, Is.LessThan(-0.99f));
            Assert.That(ReadCooldown(combat), Is.EqualTo(cooldown));
            Assert.That(ReadCooldownDuration(combat), Is.EqualTo(1.5f));
            Assert.That((int)GetProperty("LastAttackCancellationTick").GetValue(combat), Is.EqualTo(-1));
            Assert.That(spawner.Count, Is.Zero);

            yield return WaitUntil(() => spawner.Count == 1, "Defense input cancelled or stalled the accepted release.");
            Assert.That((bool)defense.IsDefending, Is.True);
            Assert.That(spawner.WasAuthoritativeForward, Is.True);
            Assert.That(spawner.LastRequest.SimulationTick, Is.EqualTo(accepted.ReleaseTick));
            Assert.That(spawner.LastRequest.Direction, Is.EqualTo(accepted.Direction));
            Assert.That(spawner.LastRequest.ProjectilePrefab, Is.EqualTo(accepted.Prefab));
            Assert.That(spawner.LastRequest.ImpactLayerMask, Is.EqualTo(accepted.ImpactMask));
            Assert.That(spawner.LastRequest.Damage, Is.EqualTo(accepted.Damage));
            Assert.That(spawner.LastRequest.DamageType, Is.EqualTo(accepted.DamageType));
            Assert.That(spawner.LastRequest.Speed, Is.EqualTo(accepted.Speed));
            Assert.That(spawner.LastRequest.LifetimeSeconds, Is.EqualTo(accepted.Lifetime));
            Assert.That(spawner.LastRequest.MaximumRange, Is.EqualTo(accepted.Range));
            Assert.That(spawner.LastRequest.KnockbackForce, Is.EqualTo(accepted.Knockback));
            Assert.That(spawner.LastRequest.Origin, Is.EqualTo(spawner.OriginAtSpawn + accepted.Direction * accepted.SpawnOffset));
            Assert.That((bool)ReadPending(combat).Pending, Is.False);
            Assert.That((int)GetProperty("LastAttackCancellationTick").GetValue(combat), Is.EqualTo(-1));
            Assert.That(ReadAttackSequence(combat), Is.EqualTo(sequence));
            Assert.That(ReadCooldown(combat), Is.EqualTo(cooldown));
            Assert.That(ReadCooldownDuration(combat), Is.EqualTo(1.5f));

            // Remove cooldown/pending as alternative blockers before pressing again under defense.
            yield return WaitUntil(
                () => combat.TryGetPrimaryAttackStatus(out PrimaryAttackStatus status) && status.IsAvailable,
                "The accepted attack's cooldown did not expire.");
            Assert.That((bool)defense.IsDefending, Is.True);
            _inputDriver.AttackHeld = true;
            yield return WaitUntil(
                () => ReadPreviousButtons(combat).IsSet(PlayerInputButton.PrimaryAttack),
                "Combat did not consume the fresh attack press under defense.");
            Assert.That(defense.CanDefend(ReadPreviousButtons(combat)), Is.True);
            int blockedTick = (int)_runner.Tick;
            yield return WaitUntil(() => (int)_runner.Tick > blockedTick + 3, "Runner did not advance while both intentions were held.");
            Assert.That(ReadAttackSequence(combat), Is.EqualTo(sequence), "Defense must block a new acceptance even after cooldown expires.");
            Assert.That((bool)ReadPending(combat).Pending, Is.False);
            Assert.That(spawner.Count, Is.EqualTo(1), "The consumed release must not replay.");
            Assert.That(ReadCooldown(combat), Is.EqualTo(cooldown));
            Assert.That(ReadCooldownDuration(combat), Is.EqualTo(1.5f));
            _inputDriver.AttackHeld = false;
            yield return WaitUntil(() => !ReadPreviousButtons(combat).IsSet(PlayerInputButton.PrimaryAttack), "Combat did not consume the blocked press release.");
            _inputDriver.SecondaryHeld = false;
            yield return WaitUntil(() => !defense.IsDefending, "Defense did not stop after releasing secondary input.");

            yield return PressAttackUntil(combat, () => (bool)ReadPending(combat).Pending, "A fresh attack was not accepted after defense ended.");
            Assert.That(ReadAttackSequence(combat), Is.EqualTo(sequence + 1));
            int secondDeadline = ReadPending(combat).ReleaseTick;
            yield return WaitUntil(() => (int)_runner.Tick > secondDeadline + 3, "Runner did not cross the second release deadline.");
            Assert.That(spawner.Count, Is.EqualTo(2));
            Assert.That((bool)ReadPending(combat).Pending, Is.False);
            Assert.That((int)GetProperty("LastAttackCancellationTick").GetValue(combat), Is.EqualTo(-1));
        }

        [UnityTest]
        public IEnumerator RangedWindup_FailedSpawnConsumesAttemptAndCannotReplay()
        {
            yield return StartGameplayRunner();
            LogAssert.Expect(UnityEngine.LogType.Error, MissingExtractionProgressDependenciesMessage);
            NetworkObject player = SpawnPlayerWithParticipant();
            var combat = player.GetComponent<PlayerCombatNetworkController>();
            RangedAttack ranged = ConfigureScheduledAttack(player, 0.2f, 1f, out RecordingProjectileSpawner spawner);
            spawner.Succeeds = false;
            AttackPerformedEvent observed = default;
            combat.AttackPerformed += attack => observed = attack;
            yield return SetStrategy(combat, ranged, true);
            yield return PressAttackUntil(combat, () => (bool)ReadPending(combat).Pending, "Wind-up was not accepted.");
            int sequence = ReadAttackSequence(combat);
            int deadline = ReadPending(combat).ReleaseTick;
            TickTimer cooldown = ReadCooldown(combat);
            Assert.That(spawner.Count, Is.Zero);
            yield return WaitUntil(() => spawner.Count == 1, "Release attempt did not reach spawner.");
            Assert.That(spawner.WasAuthoritativeForward, Is.True);
            Assert.That(spawner.LastRequest.SimulationTick, Is.EqualTo(deadline));
            int cancellationTick = (int)GetProperty("LastAttackCancellationTick").GetValue(combat);
            Assert.That(cancellationTick, Is.EqualTo(deadline));
            Assert.That(ReadCooldown(combat), Is.EqualTo(cooldown));
            Assert.That(ReadCooldownDuration(combat), Is.EqualTo(1f));
            Assert.That((bool)ReadPending(combat).Pending, Is.False);
            Assert.That(observed.HasReleaseTimeline, Is.True);

            // Simulation consumes the failed release before presentation reaches its cancellation tick.
            yield return WaitUntil(
                () => _runner.LocalRenderTime >= (double)cancellationTick * _runner.DeltaTime,
                "The authority render clock did not reach the failed release's cancellation tick.");
            Assert.That(_runner.IsRunning, Is.True);
            Assert.That(combat.Runner, Is.SameAs(_runner));
            Assert.That(combat.Object, Is.SameAs(player));
            Assert.That(player.IsValid, Is.True);
            Assert.That(combat.HasStateAuthority, Is.True);
            var character = player.GetComponent<PlayerCharacter>();
            Assert.That(character.IsAlive, Is.True);
            Assert.That(PlayerDownedGate.IsDowned(character), Is.False);
            Assert.That(ReadAttackSequence(combat), Is.EqualTo(sequence));
            Assert.That(observed.Sequence, Is.EqualTo(sequence));
            Assert.That((int)GetField("_lastObservedSequence").GetValue(combat), Is.EqualTo(sequence));
            Assert.That(observed.ReleaseTick, Is.EqualTo(deadline));
            Assert.That(_runner.LocalRenderTime, Is.GreaterThanOrEqualTo((double)observed.SimulationTick * _runner.DeltaTime));
            Assert.That((int)GetProperty("LastAttackCancellationTick").GetValue(combat), Is.EqualTo(cancellationTick));
            Assert.That((bool)ReadPending(combat).Pending, Is.False);
            Assert.That(ReadCooldown(combat), Is.EqualTo(cooldown));
            Assert.That(ReadCooldownDuration(combat), Is.EqualTo(1f));
            Assert.That(combat.TryGetAttackPresentationSeconds(observed, out _), Is.False,
                "A failed release must stop presentation as well as consume simulation state.");
            for (int i = 0; i < 12; i++) yield return null;
            Assert.That(spawner.Count, Is.EqualTo(1));
            Assert.That((bool)ReadPending(combat).Pending, Is.False);
            Assert.That(ReadAttackSequence(combat), Is.EqualTo(sequence));
            Assert.That(ReadCooldown(combat), Is.EqualTo(cooldown));
        }

        [UnityTest]
        public IEnumerator MeleeSwing_ResolvesDamageOnReleaseTickWithReleaseOriginAndAcceptanceCooldown()
        {
            yield return StartGameplayRunner();
            LogAssert.Expect(UnityEngine.LogType.Error, MissingExtractionProgressDependenciesMessage);
            NetworkObject player = SpawnPlayerWithParticipant();
            var combat = player.GetComponent<PlayerCombatNetworkController>();
            MeleeAttack melee = ConfigureMeleeAttack(player, 0.6f, 1.5f, out RecordingMeleeTargets meleeRecorder);
            AttackPerformedEvent observed = default;
            combat.AttackPerformed += attack => observed = attack;
            yield return SetStrategy(combat, melee, true);
            _inputDriver.AimWorldPosition = Vector2.right * 10f;
            yield return PressAttackUntil(combat, () => (bool)ReadPendingMelee(combat).Pending, "Melee swing was not accepted.");
            MeleeAttackRelease accepted = ReadPendingMelee(combat);
            int acceptedSequence = ReadAttackSequence(combat);
            TickTimer cooldown = ReadCooldown(combat);
            Vector2 acceptanceOrigin = meleeRecorder.AttackOrigin.position;
            Assert.That(meleeRecorder.ResolveCount, Is.Zero, "Damage must not resolve on the acceptance tick.");
            Assert.That(accepted.ReleaseTick, Is.GreaterThan((int)GetProperty("LastAttackTick").GetValue(combat)));
            Assert.That((int)GetProperty("LastAttackReleaseTick").GetValue(combat), Is.EqualTo(accepted.ReleaseTick));
            Assert.That(cooldown.RemainingTime(_runner), Is.GreaterThan(0f), "Cooldown must start at acceptance.");
            Assert.That(ReadCooldownDuration(combat), Is.EqualTo(1.5f));

            _inputDriver.MoveDirection = Vector2.up;
            yield return WaitUntil(() => meleeRecorder.ResolveCount == 1, "Melee swing did not resolve on its release tick.");
            _inputDriver.MoveDirection = Vector2.zero;
            Assert.That(meleeRecorder.WasAuthoritativeForward, Is.True);
            Assert.That(meleeRecorder.LastRequest.SimulationTick, Is.EqualTo(accepted.ReleaseTick));
            Assert.That(meleeRecorder.LastRequest.Direction, Is.EqualTo(accepted.Direction));
            Assert.That(meleeRecorder.LastRequest.Amount, Is.EqualTo(20f));
            Assert.That(meleeRecorder.LastQuery.Origin.y, Is.GreaterThan(acceptanceOrigin.y), "Release must sample the moved authoritative origin.");
            Assert.That(ReadAttackSequence(combat), Is.EqualTo(acceptedSequence));
            Assert.That(ReadCooldown(combat), Is.EqualTo(cooldown), "Release must not restart the cooldown.");
            Assert.That(observed.HasReleaseTimeline, Is.True);
            Assert.That(observed.ReleaseTick, Is.EqualTo(accepted.ReleaseTick));
            Assert.That((bool)ReadPendingMelee(combat).Pending, Is.False);
            Assert.That((int)GetProperty("LastAttackCancellationTick").GetValue(combat), Is.EqualTo(-1));
            for (int i = 0; i < 12; i++) yield return null;
            Assert.That(meleeRecorder.ResolveCount, Is.EqualTo(1), "A consumed swing must not replay.");
        }

        [UnityTest]
        public IEnumerator MeleeSwing_ZeroDelayResolvesOnTheAcceptanceTick()
        {
            yield return StartGameplayRunner();
            LogAssert.Expect(UnityEngine.LogType.Error, MissingExtractionProgressDependenciesMessage);
            NetworkObject player = SpawnPlayerWithParticipant();
            var combat = player.GetComponent<PlayerCombatNetworkController>();
            MeleeAttack melee = ConfigureMeleeAttack(player, 0f, 1f, out RecordingMeleeTargets meleeRecorder);
            yield return SetStrategy(combat, melee, true);
            _inputDriver.AimWorldPosition = Vector2.right * 10f;
            yield return PressAttackUntil(combat, () => meleeRecorder.ResolveCount == 1, "Zero-delay swing did not resolve.");
            Assert.That(meleeRecorder.LastRequest.SimulationTick,
                Is.EqualTo((int)GetProperty("LastAttackTick").GetValue(combat)));
            Assert.That((int)GetProperty("LastAttackReleaseTick").GetValue(combat),
                Is.EqualTo((int)GetProperty("LastAttackTick").GetValue(combat)));
            Assert.That((bool)ReadPendingMelee(combat).Pending, Is.False);
            Assert.That(ReadAttackSequence(combat), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator MeleeSwing_PendingSwingBlocksNewAcceptanceEvenWithoutCooldown()
        {
            yield return StartGameplayRunner();
            LogAssert.Expect(UnityEngine.LogType.Error, MissingExtractionProgressDependenciesMessage);
            NetworkObject player = SpawnPlayerWithParticipant();
            var combat = player.GetComponent<PlayerCombatNetworkController>();
            MeleeAttack melee = ConfigureMeleeAttack(player, 0.9f, 0f, out RecordingMeleeTargets meleeRecorder);
            yield return SetStrategy(combat, melee, true);
            _inputDriver.AimWorldPosition = Vector2.right * 10f;
            yield return PressAttackUntil(combat, () => (bool)ReadPendingMelee(combat).Pending, "Melee swing was not accepted.");
            MeleeAttackRelease accepted = ReadPendingMelee(combat);
            int sequence = ReadAttackSequence(combat);
            Assert.That(ReadCooldown(combat), Is.EqualTo(TickTimer.None));
            Assert.That(combat.TryGetPrimaryAttackStatus(out PrimaryAttackStatus status), Is.True);
            Assert.That(status.IsAvailable, Is.False, "A pending swing must report the attack as unavailable.");

            yield return PressAttackForFrames(combat, 4);
            Assert.That((int)_runner.Tick, Is.LessThan(accepted.ReleaseTick), "The press must happen before release is due.");
            Assert.That(ReadAttackSequence(combat), Is.EqualTo(sequence));
            Assert.That(ReadPendingMelee(combat).ReleaseTick, Is.EqualTo(accepted.ReleaseTick));
            yield return WaitUntil(() => meleeRecorder.ResolveCount == 1, "The accepted swing did not resolve.");
            Assert.That(meleeRecorder.LastRequest.SimulationTick, Is.EqualTo(accepted.ReleaseTick));
        }

        [UnityTest]
        public IEnumerator MeleeSwing_DisableCancelsWithoutDamageOrCooldownRefund()
        {
            yield return StartGameplayRunner();
            LogAssert.Expect(UnityEngine.LogType.Error, MissingExtractionProgressDependenciesMessage);
            NetworkObject player = SpawnPlayerWithParticipant();
            var combat = player.GetComponent<PlayerCombatNetworkController>();
            MeleeAttack melee = ConfigureMeleeAttack(player, 0.8f, 1.5f, out RecordingMeleeTargets meleeRecorder);
            yield return SetStrategy(combat, melee, true);
            yield return PressAttackUntil(combat, () => (bool)ReadPendingMelee(combat).Pending, "Melee swing was not accepted.");
            int deadline = ReadPendingMelee(combat).ReleaseTick;
            int acceptedTick = (int)_runner.Tick;
            int sequence = ReadAttackSequence(combat);
            TickTimer cooldown = ReadCooldown(combat);

            yield return SetAttackEnabled(combat, false, true);
            yield return WaitUntil(() => !(bool)ReadPendingMelee(combat).Pending, "Disabling combat did not cancel the swing.");
            int cancellationTick = (int)GetProperty("LastAttackCancellationTick").GetValue(combat);
            Assert.That(cancellationTick, Is.GreaterThanOrEqualTo(acceptedTick));
            Assert.That(cancellationTick, Is.LessThan(deadline));
            Assert.That(ReadCooldown(combat), Is.EqualTo(cooldown), "Cancellation must not refund recovery.");
            Assert.That(ReadCooldownDuration(combat), Is.EqualTo(1.5f));
            yield return WaitUntil(() => (int)_runner.Tick > deadline + 2, "Runner did not cross the cancelled deadline.");
            Assert.That(meleeRecorder.ResolveCount, Is.Zero);
            Assert.That(ReadAttackSequence(combat), Is.EqualTo(sequence));
        }

        [UnityTest]
        public IEnumerator MeleeSwing_WeaponChangeCancelsThePendingSwing()
        {
            yield return StartGameplayRunner();
            LogAssert.Expect(UnityEngine.LogType.Error, MissingExtractionProgressDependenciesMessage);
            NetworkObject player = SpawnPlayerWithParticipant();
            var combat = player.GetComponent<PlayerCombatNetworkController>();
            var equipment = player.GetComponent<PlayerWeaponEquipmentNetworkController>();
            var sword = AssetDatabase.LoadAssetAtPath<LootDefinition>("Assets/Scriptable Objects/Loot/Definitions/ArmingSword.asset");
            var wand = AssetDatabase.LoadAssetAtPath<LootDefinition>("Assets/Scriptable Objects/Loot/Definitions/MagicWand.asset");
            Assert.That(sword, Is.Not.Null);
            Assert.That(wand, Is.Not.Null);
            var inventoryDriver = _runner.gameObject.AddComponent<PlayerEquipmentSimulationDriver>();
            _runner.AddGlobal(inventoryDriver);
            int inventorySequence = inventoryDriver.CompletionSequence;
            inventoryDriver.RequestInitializeLoadout(player.GetComponent<PlayerLootReceiver>(), new[]
            {
                new LootEntry(sword.LootId, 1),
                new LootEntry(wand.LootId, 1)
            });
            yield return WaitUntil(() => inventoryDriver.CompletionSequence != inventorySequence,
                "Raid inventory setup did not run in simulation.");
            Assert.That(inventoryDriver.LastResult, Is.True, inventoryDriver.LastError);
            yield return Equip(equipment, sword, EquipmentSlot.WeaponSetAMainHand);
            Assert.That(ReadActiveAttack(combat), Is.TypeOf<MeleeAttack>());

            // Give the equipped sword's shared executor a release delay and a recording damage path.
            ConfigureMeleeAttack(player, 0.8f, 1.5f, out RecordingMeleeTargets meleeRecorder);
            yield return PressAttackUntil(combat, () => (bool)ReadPendingMelee(combat).Pending, "Melee swing was not accepted.");
            int deadline = ReadPendingMelee(combat).ReleaseTick;
            int acceptedTick = (int)_runner.Tick;
            Assert.That(meleeRecorder.ResolveCount, Is.Zero);

            yield return Equip(equipment, wand, EquipmentSlot.WeaponSetAMainHand);
            yield return WaitUntil(() => !(bool)ReadPendingMelee(combat).Pending, "Changing weapon did not cancel the swing.");
            int cancellationTick = (int)GetProperty("LastAttackCancellationTick").GetValue(combat);
            Assert.That(cancellationTick, Is.GreaterThanOrEqualTo(acceptedTick));
            Assert.That(cancellationTick, Is.LessThan(deadline));
            yield return WaitUntil(() => (int)_runner.Tick > deadline + 2, "Runner did not cross the cancelled deadline.");
            Assert.That(meleeRecorder.ResolveCount, Is.Zero, "A swing from the previous weapon must never deal damage.");
        }

        private static MeleeAttackRelease ReadPendingMelee(PlayerCombatNetworkController combat) =>
            (MeleeAttackRelease)GetProperty("PendingMeleeRelease").GetValue(combat);

        private static MeleeAttack ConfigureMeleeAttack(NetworkObject player, float delay, float cooldown,
            out RecordingMeleeTargets recorder)
        {
            var combat = player.GetComponent<PlayerCombatNetworkController>();
            recorder = new RecordingMeleeTargets
            {
                Combat = combat,
                AttackOrigin = (Transform)GetField("_attackOrigin").GetValue(combat)
            };
            var config = AssetDatabase.LoadAssetAtPath<MeleeAttackConfig>("Assets/Scriptable Objects/PlayerMeleeAttackConfig.asset");
            Assert.That(config, Is.Not.Null);
            MeleeAttack melee = player.GetComponent<MeleeAttack>();
            melee.Initialize(config, new AttackExecutionParameters(20f, DamageType.Physical, cooldown, 2f, 1f, delay),
                recorder, recorder);
            return melee;
        }

        private sealed class RecordingMeleeTargets : IAttackTargetQuery, IDamageResolver
        {
            private static readonly EntityId TargetId = new(9999);
            public PlayerCombatNetworkController Combat;
            public Transform AttackOrigin;
            public int ResolveCount;
            public bool WasAuthoritativeForward;
            public AttackTargetQuery LastQuery;
            public DamageRequest LastRequest;

            public System.Collections.Generic.IReadOnlyList<AttackTarget> FindTargets(in AttackTargetQuery query)
            {
                LastQuery = query;
                return new[] { new AttackTarget(TargetId, query.Origin) };
            }

            public DamageResult Resolve(in DamageRequest request)
            {
                ResolveCount++;
                LastRequest = request;
                WasAuthoritativeForward = Combat.HasStateAuthority && Combat.Runner.IsForward;
                Assert.That((bool)ReadPendingMelee(Combat).Pending, Is.False, "Consume must commit before damage.");
                return new DamageResult(request.TargetId, true, request.Amount, 1f, false, default);
            }
        }

        private static RangedAttackRelease ReadPending(PlayerCombatNetworkController combat) =>
            (RangedAttackRelease)PendingReleaseProperty.GetValue(combat);

        private static RangedAttack ConfigureScheduledAttack(NetworkObject player, float delay, float cooldown,
            out RecordingProjectileSpawner spawner)
        {
            var combat = player.GetComponent<PlayerCombatNetworkController>();
            spawner = player.gameObject.AddComponent<RecordingProjectileSpawner>();
            spawner.Combat = combat;
            spawner.AttackOrigin = (Transform)GetField("_attackOrigin").GetValue(combat);
            GetField("_releaseSpawner").SetValue(combat, spawner);
            RangedAttack ranged = player.GetComponent<RangedAttack>();
            typeof(RangedAttack).GetField("_projectileSpawnerSource", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(ranged, spawner);
            var config = AssetDatabase.LoadAssetAtPath<RangedAttackConfig>("Assets/Scriptable Objects/RangePlayerAttackConfig.asset");
            Assert.That(ranged.TryConfigure(config, new AttackExecutionParameters(28f, DamageType.Physical, cooldown, 6f, 3f, delay)), Is.True);
            return ranged;
        }

        private sealed class RecordingProjectileSpawner : MonoBehaviour, IProjectileSpawner
        {
            public PlayerCombatNetworkController Combat;
            public Transform AttackOrigin;
            public bool Succeeds = true;
            public int Count;
            public bool WasAuthoritativeForward;
            public Vector2 OriginAtSpawn;
            public ProjectileSpawnRequest LastRequest;
            public ProjectileSpawnResult Spawn(in ProjectileSpawnRequest request)
            {
                Count++;
                OriginAtSpawn = AttackOrigin.position;
                WasAuthoritativeForward = Combat.HasStateAuthority && Combat.Runner.IsForward;
                LastRequest = request;
                Assert.That((bool)ReadPending(Combat).Pending, Is.False, "Consume must commit before spawn.");
                return new ProjectileSpawnResult(Succeeds);
            }
        }

        private static void DisableDownedEntry(NetworkObject player)
        {
            // Existing production test seam refuses Downed entry, allowing the real fatal damage path.
            var downed = player.GetComponent<PlayerDownedStateNetworkController>();
            Assert.That(downed, Is.Not.Null);
            PropertyInfo disableEntry = typeof(PlayerDownedStateNetworkController).GetProperty("TestDisableEntry",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(disableEntry, Is.Not.Null);
            disableEntry.SetValue(downed, true);
        }

        private NetworkObject SpawnPlayerWithParticipant(Vector3 position = default, int participantNumber = 1)
        {
            Assert.That(RaidParticipantId.TryCreate(participantNumber, out RaidParticipantId participantId), Is.True);
            NetworkPrefabId participantPrefabId = _runner.Config.PrefabTable.GetId(NetworkObjectGuid.Parse(ParticipantPrefabGuid));
            NetworkObject participantPrefab = _runner.Config.PrefabTable.Load(participantPrefabId, true);
            Assert.That(participantPrefab, Is.Not.Null);
            NetworkObject participant = _runner.Spawn(participantPrefab, Vector3.zero, Quaternion.identity, null,
                onBeforeSpawned: (_, instance) => instance.GetComponent<NetworkRaidParticipant>().Initialize(
                    $"combat-test-profile-{participantNumber}", participantId, ProgressionBalanceDefaults.InitialCharacterAttributeState,
                    ExperienceCurve.InitialLevel, 0, "combat-test-generation"));
            NetworkPrefabId playerPrefabId = _runner.Config.PrefabTable.GetId(NetworkObjectGuid.Parse(BasePrefabGuid));
            NetworkObject playerPrefab = _runner.Config.PrefabTable.Load(playerPrefabId, true);
            Assert.That(playerPrefab, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(playerPrefab), Is.EqualTo("Assets/Prefabs/NetworkPlayer.prefab"));
            NetworkObject player = _runner.Spawn(playerPrefab, position, Quaternion.identity, _runner.LocalPlayer,
                onBeforeSpawned: (_, instance) => instance.GetComponent<RaidAvatarParticipantLink>().Initialize(participant));
            var link = player.GetComponent<RaidAvatarParticipantLink>();
            Assert.That(link.TryResolveParticipant(out NetworkRaidParticipant resolved), Is.True);
            Assert.That(resolved.Object, Is.SameAs(participant));
            Assert.That(link.TryGetCharacterAttributeState(out _), Is.True);
            Assert.That(link.TryGetCharacterAttributeRevision(out _), Is.True,
                "Equipment must observe a stable admitted attribute revision, not clear the strategy every tick.");
            var combat = player.GetComponent<PlayerCombatNetworkController>();
            Assert.That(combat.HasStateAuthority, Is.True);
            Assert.That(combat.HasInputAuthority, Is.True);
            Assert.That(player.InputAuthority, Is.EqualTo(_runner.LocalPlayer));
            Assert.That(player.GetComponent<PlayerWeaponEquipmentNetworkController>().isActiveAndEnabled, Is.True);
            Assert.That(player.GetComponent<PlayerCharacter>().IsAlive, Is.True);
            NetworkMatchController match = _runner.GetComponent<NetworkSpawnManager>()?.MatchController;
            if (match != null)
            {
                Assert.That(match.Phase, Is.EqualTo(NetworkMatchController.MatchPhase.InProgress));
                Assert.That(GetField("_matchController").GetValue(combat), Is.SameAs(match));
            }
            return player;
        }

        private static IEnumerator Equip(PlayerWeaponEquipmentNetworkController equipment, LootDefinition definition, EquipmentSlot slot)
        {
            EquipmentOperationResult result = EquipmentOperationResult.None;
            void OnResolved(EquipmentOperationResult resolved) => result = resolved;
            equipment.EquipRequestResolved += OnResolved;
            try
            {
                Assert.That(equipment.TryRequestEquip(definition.LootId, slot), Is.True);
                yield return WaitUntil(() => result != EquipmentOperationResult.None, "Equipment request was not confirmed.");
                Assert.That(result, Is.EqualTo(EquipmentOperationResult.Succeeded));
            }
            finally
            {
                equipment.EquipRequestResolved -= OnResolved;
            }
        }

        private static IEnumerator SetPhase(MatchPhaseSimulationDriver driver, NetworkMatchController match,
            NetworkMatchController.MatchPhase phase)
        {
            int previous = driver.CompletionSequence;
            driver.RequestPhase(match, phase);
            yield return WaitUntil(() => driver.CompletionSequence != previous, "Match phase change did not run in simulation.");
            Assert.That(match.Phase, Is.EqualTo(phase));
        }

        private sealed class MatchPhaseSimulationDriver : SimulationBehaviour
        {
            private NetworkMatchController _match;
            private NetworkMatchController.MatchPhase _phase;
            public int CompletionSequence { get; private set; }
            public int ChangedTick { get; private set; }

            public void RequestPhase(NetworkMatchController match, NetworkMatchController.MatchPhase phase)
            {
                _match = match;
                _phase = phase;
            }

            public override void FixedUpdateNetwork()
            {
                if (_match == null) return;
                Assert.That(_match.HasStateAuthority, Is.True);
                _match.Phase = _phase;
                _match = null;
                ChangedTick = (int)Runner.Tick;
                CompletionSequence++;
            }
        }

        private IEnumerator StartGameplayRunner()
        {
            yield return StartRunner(includeMatchController: true);
            NetworkObject matchObject = Spawn(MatchPrefabGuid, null, Vector3.zero);
            var match = matchObject.GetComponent<NetworkMatchController>();
            Assert.That(match.HasStateAuthority, Is.True);
            Assert.That(_runner.GetComponent<NetworkSpawnManager>().MatchController, Is.SameAs(match));
            var phaseDriver = _runner.gameObject.AddComponent<MatchPhaseSimulationDriver>();
            _runner.AddGlobal(phaseDriver);
            yield return SetPhase(phaseDriver, match, NetworkMatchController.MatchPhase.InProgress);
        }

        private IEnumerator StartRunner(bool includeMatchController = false)
        {
            var runnerObject = new GameObject("PlayerCombatNetworkControllerTestRunner");
            _runner = runnerObject.AddComponent<NetworkRunner>();
            runnerObject.AddComponent<EntityRegistry>();
            _inputDriver = runnerObject.AddComponent<PlayerCombatInputDriver>();
            _strategyDriver = runnerObject.AddComponent<PlayerCombatStrategySimulationDriver>();
            _runner.AddCallbacks(_inputDriver);
            _runner.ProvideInput = true;
            if (includeMatchController)
            {
                var spawnManager = runnerObject.AddComponent<NetworkSpawnManager>();
                Assert.That(spawnManager.InitializeForRunner(_runner, default, default,
                    Array.Empty<NetworkPrefabRef>(), SessionStartupContext.FreshSession, null), Is.True);
            }

            var start = _runner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Single,
                SessionName = $"cb-01-{Guid.NewGuid():N}",
                SceneManager = runnerObject.AddComponent<NetworkSceneManagerDefault>(),
                ObjectProvider = runnerObject.AddComponent<NetworkObjectProviderDefault>()
            });
            while (!start.IsCompleted)
            {
                yield return null;
            }

            Assert.That(start.Result.Ok, Is.True, start.Result.ShutdownReason.ToString());
        }

        private NetworkObject Spawn(string prefabGuid, PlayerRef? inputAuthority, Vector3 position)
        {
            NetworkPrefabId prefabId =
                _runner.Config.PrefabTable.GetId(NetworkObjectGuid.Parse(prefabGuid));
            NetworkObject prefab = _runner.Config.PrefabTable.Load(prefabId, true);
            Assert.That(prefab, Is.Not.Null, prefabGuid);
            return _runner.Spawn(prefab, position, Quaternion.identity, inputAuthority);
        }

        private IEnumerator PressAttackUntil(
            PlayerCombatNetworkController controller,
            Func<bool> predicate,
            string failureMessage)
        {
            _inputDriver.AttackHeld = true;
            yield return WaitUntil(predicate, failureMessage);
            _inputDriver.AttackHeld = false;
            yield return WaitUntil(
                () => !ReadPreviousButtons(controller).IsSet(PlayerInputButton.PrimaryAttack),
                "Combat did not consume the button release.");
        }

        private IEnumerator SetStrategy(
            PlayerCombatNetworkController controller,
            MonoBehaviour attackSource,
            bool expectedResult)
        {
            int previousSequence = _strategyDriver.CompletionSequence;
            _strategyDriver.RequestSetStrategy(controller, attackSource);
            yield return WaitUntil(
                () => _strategyDriver.CompletionSequence != previousSequence,
                "The strategy assignment was not processed during Fusion simulation.");
            Assert.That(_strategyDriver.LastResult, Is.EqualTo(expectedResult));
        }

        private IEnumerator ClearStrategy(
            PlayerCombatNetworkController controller,
            bool expectedResult)
        {
            int previousSequence = _strategyDriver.CompletionSequence;
            _strategyDriver.RequestClearStrategy(controller);
            yield return WaitUntil(
                () => _strategyDriver.CompletionSequence != previousSequence,
                "The strategy removal was not processed during Fusion simulation.");
            Assert.That(_strategyDriver.LastResult, Is.EqualTo(expectedResult));
        }

        private IEnumerator SetAttackEnabled(
            PlayerCombatNetworkController controller,
            bool enabled,
            bool expectedResult)
        {
            int previousSequence = _strategyDriver.CompletionSequence;
            _strategyDriver.RequestSetEnabled(controller, enabled);
            yield return WaitUntil(
                () => _strategyDriver.CompletionSequence != previousSequence,
                "The combat-enabled change was not processed during Fusion simulation.");
            Assert.That(_strategyDriver.LastResult, Is.EqualTo(expectedResult));
        }

        private IEnumerator DefeatCharacter(PlayerCharacter character, bool expectedResult)
        {
            int previousSequence = _strategyDriver.CompletionSequence;
            _strategyDriver.RequestDefeatCharacter(character);
            yield return WaitUntil(
                () => _strategyDriver.CompletionSequence != previousSequence,
                "The character defeat was not processed during Fusion simulation.");
            Assert.That(_strategyDriver.LastResult, Is.EqualTo(expectedResult));
        }

        private IEnumerator PressAttackForFrames(
            PlayerCombatNetworkController controller,
            int frameCount)
        {
            _inputDriver.AttackHeld = true;
            for (int index = 0; index < frameCount; index++)
            {
                yield return null;
            }

            _inputDriver.AttackHeld = false;
            yield return WaitUntil(
                () => !ReadPreviousButtons(controller).IsSet(PlayerInputButton.PrimaryAttack),
                "Combat did not consume the button release.");
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

        private static NetworkButtons ReadPreviousButtons(PlayerCombatNetworkController controller) =>
            (NetworkButtons)PreviousButtonsProperty.GetValue(controller);

        private static TickTimer ReadCooldown(PlayerCombatNetworkController controller) =>
            (TickTimer)AttackCooldownProperty.GetValue(controller);

        private static bool ReadHasActiveAttack(PlayerCombatNetworkController controller) =>
            (bool)(NetworkBool)HasActiveAttackProperty.GetValue(controller);

        private static float ReadCooldownDuration(PlayerCombatNetworkController controller) =>
            (float)CooldownDurationProperty.GetValue(controller);

        private static int ReadAttackSequence(PlayerCombatNetworkController controller) =>
            (int)AttackSequenceProperty.GetValue(controller);

        private static IAttack ReadActiveAttack(PlayerCombatNetworkController controller) =>
            ActiveAttackField.GetValue(controller) as IAttack;

        private static MonoBehaviour ReadActiveAttackSource(PlayerCombatNetworkController controller) =>
            ActiveAttackSourceField.GetValue(controller) as MonoBehaviour;

        private static PropertyInfo GetProperty(string propertyName)
        {
            PropertyInfo property = typeof(PlayerCombatNetworkController).GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null, propertyName);
            return property;
        }

        private static FieldInfo GetField(string fieldName)
        {
            FieldInfo field = typeof(PlayerCombatNetworkController).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            return field;
        }

        private static MethodInfo GetMethod(string methodName)
        {
            MethodInfo method = typeof(PlayerCombatNetworkController).GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            return method;
        }

        private sealed class PlayerCombatInputDriver : NetworkRunnerCallbacksAdapter
        {
            public bool AttackHeld { get; set; }
            public bool SecondaryHeld { get; set; }
            public Vector2 MoveDirection { get; set; }
            public Vector2 AimWorldPosition { get; set; }

            public override void OnInput(NetworkRunner runner, NetworkInput input)
            {
                PlayerNetworkInput playerInput = default;
                playerInput.MoveDirection = MoveDirection;
                playerInput.AimWorldPosition = AimWorldPosition;
                playerInput.Buttons.Set(PlayerInputButton.PrimaryAttack, AttackHeld);
                playerInput.Buttons.Set(PlayerInputButton.SecondaryAction, SecondaryHeld);
                input.Set(playerInput);
            }
        }
    }
}
#endif
