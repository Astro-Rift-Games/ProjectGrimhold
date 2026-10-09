#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Fusion;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Utils;
using Assert = NUnit.Framework.Assert;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Abilities
{
    /// <summary>End-to-end proof of the charge on the real avatar, the real motor and real creatures.</summary>
    public sealed class ChargeAbilityPlayModeTests
    {
        private const string PlayerPrefabGuid = "fea3a7b256f965a4eb9b965832939741";
        private const string ParticipantPrefabGuid = "c39d451563bae6e43934008a0dadc6d6";
        private const string GreenSlimePrefabGuid = "5deca87613df0fa409d98702aec643d4";
        private const float Damage = 5f;
        private const float KnockbackForce = 8f;

        private NetworkRunner _runner;
        private PlayerAbilityRuntimeSimulationDriver _driver;
        private ChargeInputDriver _input;
        private PlayerAbilityRuntimeNetworkController _avatar;
        private PlayerMovementNetworkController _movement;
        private readonly List<GameObject> _scenery = new();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var item in _scenery) if (item != null) Object.Destroy(item);
            _scenery.Clear();
            if (_runner != null)
            {
                var shutdown = _runner.Shutdown();
                while (!shutdown.IsCompleted) yield return null;
                Object.Destroy(_runner.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator Charge_WithoutObstacle_RunsTheFullDistanceInTheAimDirection()
        {
            yield return Begin(distance: 4f, speed: 12f);
            yield return SetAim(Vector2.right);

            yield return Press();
            yield return WaitForCompletion();

            float tolerance = StepLength(12f) * 2f + 0.05f;
            Assert.That(Position.x, Is.EqualTo(4f).Within(tolerance));
            Assert.That(Position.y, Is.EqualTo(0f).Within(0.05f));
            Assert.That(_movement.IsForcedDisplacementActive, Is.False, "The displacement is released at the end.");
            Assert.That(_avatar.GetLastActivationFailure(UniversalAbilitySlot.Slot1), Is.EqualTo(AbilityActivationFailure.None));
        }

        [UnityTest]
        public IEnumerator Charge_AimChangedAfterStart_KeepsTheCapturedDirection()
        {
            yield return Begin(distance: 8f, speed: 8f);
            yield return SetAim(Vector2.right);

            yield return Press();
            Assert.That(_avatar.HasActiveExecution, Is.True, "The charge must still be running when the aim changes.");
            yield return SetAim(Vector2.up);
            yield return WaitForCompletion();

            _avatar.TryGetExecutionSnapshot(UniversalAbilitySlot.Slot1, out var snapshot);
            Assert.That(snapshot.AimDirection, Is.EqualTo(Vector2.zero), "The capture is cleared with the execution.");
            Assert.That(Position.x, Is.GreaterThan(7f));
            Assert.That(Position.y, Is.EqualTo(0f).Within(0.05f), "The path never bends toward the new aim.");
        }

        [UnityTest]
        public IEnumerator Charge_EnvironmentCollider_StopsItImmediately()
        {
            yield return Begin(distance: 8f, speed: 8f);
            AddWall(new Vector2(2.5f, 0f));
            yield return SetAim(Vector2.right);

            yield return Press();
            _avatar.TryGetExecutionSnapshot(UniversalAbilitySlot.Slot1, out var accepted);
            int deadlineTick = (int)accepted.PhaseDeadline.TargetTick;
            yield return WaitForCompletion();

            Assert.That(Position.x, Is.LessThan(2.5f), "It never passes through a structure.");
            Assert.That(Position.x, Is.GreaterThan(1f));
            Assert.That(_endTick, Is.LessThan(deadlineTick - 10), "The wall ends the charge before the planned duration.");
            Assert.That(_movement.IsForcedDisplacementActive, Is.False);
        }

        [UnityTest]
        public IEnumerator Charge_StartingAgainstAWall_IsStillAcceptedAndPaid()
        {
            yield return Begin(distance: 4f, speed: 12f);
            AddWall(new Vector2(0.7f, 0f));
            yield return SetAim(Vector2.right);
            float stamina = Stamina();

            yield return Press();
            yield return WaitForCompletion();

            Assert.That(_avatar.GetLastActivationFailure(UniversalAbilitySlot.Slot1), Is.EqualTo(AbilityActivationFailure.None));
            Assert.That(Stamina(), Is.LessThan(stamina), "A blocked trajectory does not invalidate the start.");
            Assert.That(Position.x, Is.LessThan(0.7f));
        }

        [UnityTest]
        public IEnumerator Charge_FirstValidEnemy_StopsTheCasterAndTakesDamageAndKnockbackOnce()
        {
            yield return Begin(distance: 8f, speed: 8f);
            var first = SpawnEnemy(new Vector2(2f, 0f));
            var second = SpawnEnemy(new Vector2(3.5f, 0f));
            float firstHealth = first.Health;
            float secondHealth = second.Health;
            yield return SetAim(Vector2.right);

            yield return Press();
            yield return WaitForCompletion();
            yield return WaitTicks();

            Assert.That(Position.x, Is.LessThan(2f), "It stops at the first enemy and does not pass through it.");
            Assert.That(Position.x, Is.GreaterThan(0.3f));
            Assert.That(first.Health, Is.EqualTo(firstHealth - Damage).Within(0.001f), "Damage is applied exactly once.");
            Assert.That(second.Health, Is.EqualTo(secondHealth), "Only the first enemy is hit.");
            Assert.That(Knockback(first), Is.EqualTo(new Vector2(KnockbackForce, 0f)).Using(new Vector2EqualityComparer(0.01f)),
                "Knockback is applied exactly once, along the charge direction.");
            Assert.That(Knockback(second), Is.EqualTo(Vector2.zero));
            Assert.That(_endTick, Is.LessThan(_avatarDeadlineTick - 10), "The enemy ends the charge before the planned duration.");
        }

        [UnityTest]
        public IEnumerator Charge_WithZeroDamage_StillPushesTheEnemyWithoutHurtingIt()
        {
            yield return Begin(distance: 8f, speed: 8f, damage: 0f);
            var enemy = SpawnEnemy(new Vector2(2f, 0f));
            float health = enemy.Health;
            yield return SetAim(Vector2.right);

            yield return Press();
            yield return WaitForCompletion();
            yield return WaitTicks();

            Assert.That(Position.x, Is.LessThan(2f));
            Assert.That(enemy.Health, Is.EqualTo(health));
            Assert.That(Knockback(enemy), Is.EqualTo(new Vector2(KnockbackForce, 0f)).Using(new Vector2EqualityComparer(0.01f)));
        }

        [UnityTest]
        public IEnumerator Charge_NonEnemyCollider_DoesNotStopIt()
        {
            yield return Begin(distance: 4f, speed: 12f);
            // Another player avatar on the target layer is not a valid enemy.
            ExpectAvatarValidationError();
            _runner.Spawn(LoadPrefab(PlayerPrefabGuid), new Vector3(2f, 0f, 0f), Quaternion.identity, inputAuthority: null);
            Physics2D.SyncTransforms();
            yield return SetAim(Vector2.right);

            yield return Press();
            yield return WaitForCompletion();

            Assert.That(Position.x, Is.GreaterThan(3f), "Only valid enemies stop the charge.");
        }

        [UnityTest]
        public IEnumerator Charge_PaysOnceAndKeepsCooldownAfterCompletion()
        {
            yield return Begin(distance: 4f, speed: 12f);
            yield return SetAim(Vector2.right);
            float stamina = Stamina();
            _avatar.TryGetSlot(UniversalAbilitySlot.Slot1, out var slot);

            yield return Press();
            yield return WaitForCompletion();

            Assert.That(stamina - Stamina(), Is.EqualTo(slot.Definition.Cost).Within(0.001f), "Cost is paid once and not refunded.");
            Assert.That(_avatar.IsOnCooldown(UniversalAbilitySlot.Slot1), Is.True);
            yield return WaitTicks();
            float afterFirst = Position.x;

            yield return Press();

            Assert.That(_avatar.GetLastActivationFailure(UniversalAbilitySlot.Slot1), Is.EqualTo(AbilityActivationFailure.Cooldown));
            _avatar.TryGetExecutionSnapshot(UniversalAbilitySlot.Slot1, out var snapshot);
            Assert.That(snapshot.Sequence, Is.EqualTo(1));
            Assert.That(Position.x, Is.EqualTo(afterFirst).Within(0.05f), "A rejected second start does not move the caster.");
        }

        [UnityTest]
        public IEnumerator Charge_Downed_ReleasesTheDisplacement()
        {
            yield return Begin(distance: 8f, speed: 8f);
            yield return SetAim(Vector2.right);

            yield return Press();
            Assert.That(_movement.IsForcedDisplacementActive, Is.True);
            yield return InSimulation(() =>
            {
                _avatar.Object.AssignInputAuthority(PlayerRef.None);
                Assert.That(_avatar.GetComponent<PlayerDownedStateNetworkController>().TryEnterDowned(), Is.True);
            });
            yield return WaitTicks();

            Assert.That(_movement.IsForcedDisplacementActive, Is.False);
            Assert.That(_avatar.HasActiveExecution, Is.False);
            float stopped = Position.x;
            yield return WaitTicks();
            Assert.That(Position.x, Is.EqualTo(stopped).Within(0.05f), "The caster keeps no residual charge.");
            Assert.That(stopped, Is.LessThan(7f));
        }

        // ---- Fixture ---------------------------------------------------------------------

        private int _endTick;
        private int _avatarDeadlineTick;

        private IEnumerator Begin(float distance, float speed, float damage = Damage)
        {
            var runnerObject = new GameObject("ChargeTestRunner");
            _runner = runnerObject.AddComponent<NetworkRunner>();
            runnerObject.AddComponent<EntityRegistry>();
            _driver = runnerObject.AddComponent<PlayerAbilityRuntimeSimulationDriver>();
            _input = runnerObject.AddComponent<ChargeInputDriver>();
            _runner.AddCallbacks(_input);
            _runner.ProvideInput = true;
            var start = _runner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Single,
                SessionName = $"ability-charge-{Guid.NewGuid():N}",
                SceneManager = runnerObject.AddComponent<NetworkSceneManagerDefault>(),
                ObjectProvider = runnerObject.AddComponent<NetworkObjectProviderDefault>()
            });
            while (!start.IsCompleted) yield return null;
            Assert.That(start.Result.Ok, Is.True, start.Result.ShutdownReason.ToString());

            Assert.That(CharacterAttributeState.TryCreate(20, 20, 20, 20, 20, 20, 0, out var attributes), Is.True);
            RaidParticipantId.TryCreate(1, out var participantId);
            var participant = _runner.Spawn(LoadPrefab(ParticipantPrefabGuid),
                onBeforeSpawned: (_, instance) => instance.GetComponent<NetworkRaidParticipant>().Initialize(
                    "ability-charge", participantId, attributes, ExperienceCurve.InitialLevel, 0, "ability-test",
                    preparedAbilities: new PreparedAbilityLoadout(new AbilityId("charge"), default)))
                .GetComponent<NetworkRaidParticipant>();
            ExpectAvatarValidationError();
            var avatar = _runner.Spawn(LoadPrefab(PlayerPrefabGuid), Vector3.zero, Quaternion.identity,
                _runner.LocalPlayer, onBeforeSpawned: (_, instance) =>
                {
                    instance.GetComponent<RaidAvatarParticipantLink>().Initialize(participant.Object);
                    var runtime = instance.GetComponent<PlayerAbilityRuntimeNetworkController>();
                    SetField(runtime, "_playerCharacter", instance.GetComponent<PlayerCharacter>());
                    SetField(runtime, "_staminaController", instance.GetComponent<PlayerStaminaNetworkController>());
                    SetField(instance.GetComponent<PlayerStaminaNetworkController>(), "_regenerationPerSecond", 0f);
                    var catalog = (AbilityDefinitionCatalog)typeof(PlayerAbilityRuntimeNetworkController)
                        .GetField("_catalog", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(runtime);
                    Assert.That(catalog.TryGet(new AbilityId("charge"), out var definition), Is.True);
                    var behaviour = instance.GetComponent<ChargeAbilityBehaviour>(); // The prefab composes it; the tuning is overridden per test.
                    SetField(behaviour, "_definition", definition, typeof(AbilityExecutionBehaviour));
                    SetField(behaviour, "_distance", distance);
                    SetField(behaviour, "_speed", speed);
                    SetField(behaviour, "_damage", damage);
                    SetField(behaviour, "_knockbackForce", KnockbackForce);
                    SetField(behaviour, "_targetLayerMask", (LayerMask)LayerMask.GetMask("Character"));
                    SetField(runtime, "_executionBehaviours", new AbilityExecutionBehaviour[] { behaviour });
                });
            Assert.That(participant.TrySetCurrentAvatar(avatar), Is.True);
            _avatar = avatar.GetComponent<PlayerAbilityRuntimeNetworkController>();
            _movement = avatar.GetComponent<PlayerMovementNetworkController>();
            yield return WaitUntil(() => _avatar.IsInitialized);
        }

        private Vector2 Position => _avatar.transform.position;

        private float StepLength(float speed) => speed * _runner.DeltaTime;

        private EnemyCharacter SpawnEnemy(Vector2 position)
        {
            var enemy = _runner.Spawn(LoadPrefab(GreenSlimePrefabGuid), position, Quaternion.identity);
            // Keep the creature still: only the charge may act on it.
            foreach (var behaviour in enemy.GetComponents<NetworkBehaviour>())
            {
                if (behaviour is EnemyFSM || behaviour is EnemyMovementAIController || behaviour is EnemyCombatAIController)
                    behaviour.enabled = false;
            }
            Physics2D.SyncTransforms();
            return enemy.GetComponent<EnemyCharacter>();
        }

        private void AddWall(Vector2 position)
        {
            var wall = new GameObject("ChargeTestWall") { layer = LayerMask.NameToLayer("WorldCollision") };
            wall.transform.position = position;
            wall.AddComponent<BoxCollider2D>().size = new Vector2(0.2f, 6f);
            _scenery.Add(wall);
            Physics2D.SyncTransforms();
        }

        private static Vector2 Knockback(Component enemy) => (Vector2)typeof(EnemyMovementAIController)
            .GetProperty("KnockbackVelocity", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .GetValue(enemy.GetComponent<EnemyMovementAIController>());

        private static void ExpectAvatarValidationError() =>
            LogAssert.Expect(UnityEngine.LogType.Error,
                "PlayerExtractionProgressController requires character, extraction controller, " +
                "registry, assignment service, and valid receiver/reader registrations.");

        private NetworkObject LoadPrefab(string guid)
        {
            var id = _runner.Config.PrefabTable.GetId(NetworkObjectGuid.Parse(guid));
            return _runner.Config.PrefabTable.Load(id, true);
        }

        private IEnumerator Press()
        {
            _input.Buttons = default;
            yield return WaitTicks();
            _input.Buttons.Set(PlayerInputButton.AbilitySlot1, true);
            yield return WaitTicks();
            _input.Buttons = default;
            _avatar.TryGetExecutionSnapshot(UniversalAbilitySlot.Slot1, out var snapshot);
            if (snapshot.IsActive) _avatarDeadlineTick = (int)snapshot.PhaseDeadline.TargetTick;
        }

        /// <summary>Waits for the accepted sequence to finish and records the tick at which it did.</summary>
        private IEnumerator WaitForCompletion()
        {
            float deadline = Time.realtimeSinceStartup + 8f;
            while (_avatar.HasActiveExecution && Time.realtimeSinceStartup < deadline) yield return null;
            _endTick = _runner.Tick.Raw;
            Assert.That(_avatar.HasActiveExecution, Is.False, "The charge did not finish.");
        }

        private IEnumerator SetAim(Vector2 aim)
        {
            yield return InSimulation(() =>
            {
                SetNetworked(_movement, nameof(PlayerMovementNetworkController.AimDirection), aim);
                SetNetworked(_movement, nameof(PlayerMovementNetworkController.FacingDirection), aim);
            });
        }

        private IEnumerator WaitTicks()
        {
            int until = _runner.Tick.Raw + 3;
            yield return WaitUntil(() => _runner.Tick.Raw >= until);
        }

        private static IEnumerator WaitUntil(Func<bool> predicate, float timeoutSeconds = 8f)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!predicate() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(predicate(), Is.True, "The expected state was not reached.");
        }

        private IEnumerator InSimulation(Action operation)
        {
            int previous = _driver.CompletionSequence;
            _driver.RequestOperation(operation);
            yield return WaitUntil(() => _driver.CompletionSequence != previous);
        }

        private float Stamina() => _avatar.GetComponent<PlayerStaminaNetworkController>().CurrentStamina;

        private static void SetField(object target, string name, object value, Type declaringType = null) =>
            (declaringType ?? target.GetType()).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);

        private static void SetNetworked(object target, string name, object value) =>
            target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .SetValue(target, value);

        private sealed class ChargeInputDriver : NetworkRunnerCallbacksAdapter
        {
            public NetworkButtons Buttons;

            public override void OnInput(NetworkRunner runner, NetworkInput input) =>
                input.Set(new PlayerNetworkInput { Buttons = Buttons });
        }
    }
}
#endif
