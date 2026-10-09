#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections;
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
    /// <summary>End-to-end proof of Seismic Strike on the real avatar, the real area finder and real creatures.</summary>
    public sealed class SeismicStrikeAbilityPlayModeTests
    {
        private const string PlayerPrefabGuid = "fea3a7b256f965a4eb9b965832939741";
        private const string ParticipantPrefabGuid = "c39d451563bae6e43934008a0dadc6d6";
        private const string GreenSlimePrefabGuid = "5deca87613df0fa409d98702aec643d4";
        private const float Radius = 3f;
        private const float Damage = 5f;
        private const float KnockbackForce = 8f;
        private static readonly Vector3 Far = new Vector3(40f, 0f, 0f);

        private NetworkRunner _runner;
        private PlayerAbilityRuntimeSimulationDriver _driver;
        private SeismicInputDriver _input;
        private PlayerAbilityRuntimeNetworkController _avatar;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_runner != null)
            {
                var shutdown = _runner.Shutdown();
                while (!shutdown.IsCompleted) yield return null;
                Object.Destroy(_runner.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator Start_WithoutAValidEnemy_IsRejectedWithoutPaymentSequenceOrCooldown()
        {
            yield return Begin();
            SpawnEnemy(new Vector3(8f, 0f, 0f)); // Out of range.
            float stamina = Stamina();

            yield return Press();

            Assert.That(_avatar.GetLastActivationFailure(UniversalAbilitySlot.Slot1), Is.EqualTo(AbilityActivationFailure.BehaviourRejected));
            Assert.That(Stamina(), Is.EqualTo(stamina));
            _avatar.TryGetExecutionSnapshot(UniversalAbilitySlot.Slot1, out var snapshot);
            Assert.That(snapshot.Sequence, Is.Zero);
            Assert.That(snapshot.Cooldown.IsRunning, Is.False);
            Assert.That(_avatar.HasActiveExecution, Is.False);
        }

        [UnityTest]
        public IEnumerator Start_WithOnlyAPlayerInTheArea_IsRejectedWithoutPayment()
        {
            yield return Begin();
            SpawnPlayerAvatar(new Vector3(1f, 0f, 0f));
            float stamina = Stamina();

            yield return Press();

            Assert.That(_avatar.GetLastActivationFailure(UniversalAbilitySlot.Slot1), Is.EqualTo(AbilityActivationFailure.BehaviourRejected));
            Assert.That(Stamina(), Is.EqualTo(stamina));
            _avatar.TryGetExecutionSnapshot(UniversalAbilitySlot.Slot1, out var snapshot);
            Assert.That(snapshot.Cooldown.IsRunning, Is.False);
        }

        [UnityTest]
        public IEnumerator Start_WithAValidEnemy_ResolvesAfterThePreparationHurtingAndPushingItAwayFromTheCasterOnce()
        {
            yield return Begin(preparationSeconds: 0.75f);
            var enemy = SpawnEnemy(new Vector3(-1.5f, 0f, 0f));
            float health = enemy.Health;
            float stamina = Stamina();
            _avatar.TryGetSlot(UniversalAbilitySlot.Slot1, out var slot);

            yield return Press();

            Assert.That(_avatar.GetLastActivationFailure(UniversalAbilitySlot.Slot1), Is.EqualTo(AbilityActivationFailure.None));
            Assert.That(stamina - Stamina(), Is.EqualTo(slot.Definition.Cost).Within(0.001f), "Cost is paid once.");
            _avatar.TryGetExecutionSnapshot(UniversalAbilitySlot.Slot1, out var snapshot);
            Assert.That(snapshot.Sequence, Is.EqualTo(1));
            Assert.That(snapshot.Phase, Is.EqualTo(AbilityExecutionPhase.Preparing));
            Assert.That(snapshot.Cooldown.IsRunning, Is.True);
            Assert.That(enemy.Health, Is.EqualTo(health), "Nothing is applied while preparing.");

            yield return WaitForCompletion();
            yield return WaitTicks();

            Assert.That(enemy.Health, Is.EqualTo(health - Damage).Within(0.001f), "Damage is applied exactly once.");
            Assert.That(Knockback(enemy), Is.EqualTo(new Vector2(-KnockbackForce, 0f)).Using(new Vector2EqualityComparer(0.01f)),
                "The push is radial, away from the caster.");
            Assert.That(_avatar.IsOnCooldown(UniversalAbilitySlot.Slot1), Is.True);
        }

        [UnityTest]
        public IEnumerator Resolution_HitsEveryEnemyInTheAreaOnceInTheSameResolution()
        {
            yield return Begin();
            var left = SpawnEnemy(new Vector3(-1.5f, 0f, 0f));
            var up = SpawnEnemy(new Vector3(0f, 1.5f, 0f));
            var right = SpawnEnemy(new Vector3(2f, 0f, 0f));
            var outside = SpawnEnemy(new Vector3(0f, -6f, 0f));
            float leftHealth = left.Health;
            float upHealth = up.Health;
            float rightHealth = right.Health;
            float outsideHealth = outside.Health;

            yield return Press();
            yield return WaitForCompletion();
            yield return WaitTicks();

            Assert.That(left.Health, Is.EqualTo(leftHealth - Damage).Within(0.001f));
            Assert.That(up.Health, Is.EqualTo(upHealth - Damage).Within(0.001f));
            Assert.That(right.Health, Is.EqualTo(rightHealth - Damage).Within(0.001f));
            Assert.That(outside.Health, Is.EqualTo(outsideHealth), "An enemy outside the radius is not affected.");
            Assert.That(Knockback(left), Is.EqualTo(new Vector2(-KnockbackForce, 0f)).Using(new Vector2EqualityComparer(0.01f)));
            Assert.That(Knockback(up), Is.EqualTo(new Vector2(0f, KnockbackForce)).Using(new Vector2EqualityComparer(0.01f)));
            Assert.That(Knockback(right), Is.EqualTo(new Vector2(KnockbackForce, 0f)).Using(new Vector2EqualityComparer(0.01f)));
            Assert.That(Knockback(outside), Is.EqualTo(Vector2.zero));
        }

        [UnityTest]
        public IEnumerator Resolution_AnEnemyThatEntersDuringThePreparationIsAffected()
        {
            yield return Begin(preparationSeconds: 1.5f);
            var initial = SpawnEnemy(new Vector3(1f, 0f, 0f));
            var entering = SpawnEnemy(Far);
            float enteringHealth = entering.Health;
            float initialHealth = initial.Health;

            yield return Press();
            yield return Teleport(entering, new Vector3(0f, 1.5f, 0f));
            yield return WaitForCompletion();
            yield return WaitTicks();

            Assert.That(entering.Health, Is.EqualTo(enteringHealth - Damage).Within(0.001f));
            Assert.That(Knockback(entering), Is.EqualTo(new Vector2(0f, KnockbackForce)).Using(new Vector2EqualityComparer(0.01f)));
            Assert.That(initial.Health, Is.EqualTo(initialHealth - Damage).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator Resolution_AnEnemyThatLeavesBeforeResolutionIsNotAffected()
        {
            yield return Begin(preparationSeconds: 1.5f);
            var staying = SpawnEnemy(new Vector3(1f, 0f, 0f));
            var leaving = SpawnEnemy(new Vector3(-1f, 0f, 0f));
            float leavingHealth = leaving.Health;
            float stayingHealth = staying.Health;

            yield return Press();
            yield return Teleport(leaving, Far);
            yield return WaitForCompletion();
            yield return WaitTicks();

            Assert.That(leaving.Health, Is.EqualTo(leavingHealth));
            Assert.That(Knockback(leaving), Is.EqualTo(Vector2.zero));
            Assert.That(staying.Health, Is.EqualTo(stayingHealth - Damage).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator Resolution_WhenTheOnlyTargetLeaves_FinishesNormallyKeepingCostAndCooldown()
        {
            yield return Begin(preparationSeconds: 1.5f);
            var enemy = SpawnEnemy(new Vector3(1f, 0f, 0f));
            float health = enemy.Health;

            yield return Press();
            float afterPayment = Stamina();
            yield return Teleport(enemy, Far);
            yield return WaitForCompletion();
            yield return WaitTicks();

            Assert.That(enemy.Health, Is.EqualTo(health));
            Assert.That(Knockback(enemy), Is.EqualTo(Vector2.zero));
            Assert.That(Stamina(), Is.EqualTo(afterPayment), "Cost is not refunded.");
            _avatar.TryGetExecutionSnapshot(UniversalAbilitySlot.Slot1, out var snapshot);
            Assert.That(snapshot.Sequence, Is.EqualTo(1));
            Assert.That(snapshot.Cooldown.IsRunning, Is.True, "Cooldown is not refunded.");
            Assert.That(_avatar.HasActiveExecution, Is.False);
        }

        [UnityTest]
        public IEnumerator Preparation_InterruptedByKnockback_AppliesNothingAndKeepsCostAndCooldown()
        {
            yield return Begin(preparationSeconds: 1.5f);
            var enemy = SpawnEnemy(new Vector3(1f, 0f, 0f));
            float health = enemy.Health;

            yield return Press();
            float afterPayment = Stamina();
            bool interrupted = false;
            yield return InSimulation(() => interrupted =
                _avatar.TryInterrupt(UniversalAbilitySlot.Slot1, 1, AbilityExecutionStopReason.Knockback));
            Assert.That(interrupted, Is.True, "The preparation accepts Knockback interruptions.");
            yield return WaitSeconds(2f);

            Assert.That(_avatar.HasActiveExecution, Is.False);
            Assert.That(enemy.Health, Is.EqualTo(health));
            Assert.That(Knockback(enemy), Is.EqualTo(Vector2.zero));
            Assert.That(Stamina(), Is.EqualTo(afterPayment), "Cost is not refunded.");
            _avatar.TryGetExecutionSnapshot(UniversalAbilitySlot.Slot1, out var snapshot);
            Assert.That(snapshot.Cooldown.IsRunning, Is.True, "Cooldown is not refunded.");
        }

        [UnityTest]
        public IEnumerator Preparation_InterruptedByDowned_AppliesNothingAndKeepsCostAndCooldown()
        {
            yield return Begin(preparationSeconds: 1.5f);
            var enemy = SpawnEnemy(new Vector3(1f, 0f, 0f));
            float health = enemy.Health;

            yield return Press();
            float afterPayment = Stamina();
            yield return InSimulation(() =>
            {
                _avatar.Object.AssignInputAuthority(PlayerRef.None);
                Assert.That(_avatar.GetComponent<PlayerDownedStateNetworkController>().TryEnterDowned(), Is.True);
            });
            yield return WaitSeconds(2f);

            Assert.That(_avatar.HasActiveExecution, Is.False);
            Assert.That(enemy.Health, Is.EqualTo(health));
            Assert.That(Knockback(enemy), Is.EqualTo(Vector2.zero));
            Assert.That(Stamina(), Is.EqualTo(afterPayment));
            _avatar.TryGetExecutionSnapshot(UniversalAbilitySlot.Slot1, out var snapshot);
            Assert.That(snapshot.Cooldown.IsRunning, Is.True);
        }

        [UnityTest]
        public IEnumerator Resolution_WithZeroDamage_StillPushesWithoutHurting()
        {
            yield return Begin(damage: 0f);
            var enemy = SpawnEnemy(new Vector3(-1.5f, 0f, 0f));
            float health = enemy.Health;

            yield return Press();
            yield return WaitForCompletion();
            yield return WaitTicks();

            Assert.That(enemy.Health, Is.EqualTo(health));
            Assert.That(Knockback(enemy), Is.EqualTo(new Vector2(-KnockbackForce, 0f)).Using(new Vector2EqualityComparer(0.01f)));
        }

        [UnityTest]
        public IEnumerator Resolution_IgnoresAPlayerInTheAreaAndStillHitsTheEnemy()
        {
            yield return Begin();
            var enemy = SpawnEnemy(new Vector3(-1.5f, 0f, 0f));
            var other = SpawnPlayerAvatar(new Vector3(1.5f, 0f, 0f));
            float enemyHealth = enemy.Health;
            float playerHealth = other.Health;

            yield return Press();
            yield return WaitForCompletion();
            yield return WaitTicks();

            Assert.That(enemy.Health, Is.EqualTo(enemyHealth - Damage).Within(0.001f));
            Assert.That(other.Health, Is.EqualTo(playerHealth), "Players are not valid enemies.");
        }

        // ---- Fixture ---------------------------------------------------------------------

        private IEnumerator Begin(float preparationSeconds = 0.75f, float damage = Damage)
        {
            var runnerObject = new GameObject("SeismicStrikeTestRunner");
            _runner = runnerObject.AddComponent<NetworkRunner>();
            runnerObject.AddComponent<EntityRegistry>();
            _driver = runnerObject.AddComponent<PlayerAbilityRuntimeSimulationDriver>();
            _input = runnerObject.AddComponent<SeismicInputDriver>();
            _runner.AddCallbacks(_input);
            _runner.ProvideInput = true;
            var start = _runner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Single,
                SessionName = $"ability-seismic-{Guid.NewGuid():N}",
                SceneManager = runnerObject.AddComponent<NetworkSceneManagerDefault>(),
                ObjectProvider = runnerObject.AddComponent<NetworkObjectProviderDefault>()
            });
            while (!start.IsCompleted) yield return null;
            Assert.That(start.Result.Ok, Is.True, start.Result.ShutdownReason.ToString());

            Assert.That(CharacterAttributeState.TryCreate(20, 20, 20, 20, 20, 20, 0, out var attributes), Is.True);
            RaidParticipantId.TryCreate(1, out var participantId);
            var participant = _runner.Spawn(LoadPrefab(ParticipantPrefabGuid),
                onBeforeSpawned: (_, instance) => instance.GetComponent<NetworkRaidParticipant>().Initialize(
                    "ability-seismic", participantId, attributes, ExperienceCurve.InitialLevel, 0, "ability-test",
                    preparedAbilities: new PreparedAbilityLoadout(new AbilityId("seismic_strike"), default)))
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
                    Assert.That(catalog.TryGet(new AbilityId("seismic_strike"), out var definition), Is.True);
                    var behaviour = instance.gameObject.AddComponent<SeismicStrikeAbilityBehaviour>();
                    SetField(behaviour, "_definition", definition, typeof(AbilityExecutionBehaviour));
                    SetField(behaviour, "_radius", Radius);
                    SetField(behaviour, "_damage", damage);
                    SetField(behaviour, "_knockbackForce", KnockbackForce);
                    SetField(behaviour, "_preparationSeconds", preparationSeconds);
                    SetField(runtime, "_executionBehaviours", new AbilityExecutionBehaviour[] { behaviour });
                });
            Assert.That(participant.TrySetCurrentAvatar(avatar), Is.True);
            _avatar = avatar.GetComponent<PlayerAbilityRuntimeNetworkController>();
            yield return WaitUntil(() => _avatar.IsInitialized);
        }

        private EnemyCharacter SpawnEnemy(Vector3 position)
        {
            var enemy = _runner.Spawn(LoadPrefab(GreenSlimePrefabGuid), position, Quaternion.identity);
            // Keep the creature still: only the ability may act on it.
            foreach (var behaviour in enemy.GetComponents<NetworkBehaviour>())
            {
                if (behaviour is EnemyFSM || behaviour is EnemyMovementAIController || behaviour is EnemyCombatAIController)
                    behaviour.enabled = false;
            }
            Physics2D.SyncTransforms();
            return enemy.GetComponent<EnemyCharacter>();
        }

        private PlayerCharacter SpawnPlayerAvatar(Vector3 position)
        {
            ExpectAvatarValidationError();
            var spawned = _runner.Spawn(LoadPrefab(PlayerPrefabGuid), position, Quaternion.identity, inputAuthority: null);
            Physics2D.SyncTransforms();
            return spawned.GetComponent<PlayerCharacter>();
        }

        private static void ExpectAvatarValidationError() =>
            LogAssert.Expect(UnityEngine.LogType.Error,
                "PlayerExtractionProgressController requires character, extraction controller, " +
                "registry, assignment service, and valid receiver/reader registrations.");

        private static Vector2 Knockback(Component enemy) => (Vector2)typeof(EnemyMovementAIController)
            .GetProperty("KnockbackVelocity", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .GetValue(enemy.GetComponent<EnemyMovementAIController>());

        // Networked objects overwrite plain transform writes on the next Render; teleport inside simulation.
        private IEnumerator Teleport(Component target, Vector3 position)
        {
            yield return InSimulation(() =>
            {
                var networkTransform = target.GetComponent<NetworkTransform>();
                if (networkTransform != null) networkTransform.Teleport(position);
                target.transform.position = position;
                Physics2D.SyncTransforms();
            });
        }

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
        }

        /// <summary>Waits for the accepted sequence to finish.</summary>
        private IEnumerator WaitForCompletion()
        {
            float deadline = Time.realtimeSinceStartup + 8f;
            while (_avatar.HasActiveExecution && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(_avatar.HasActiveExecution, Is.False, "The ability did not finish.");
        }

        private IEnumerator WaitSeconds(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until) yield return null;
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

        private sealed class SeismicInputDriver : NetworkRunnerCallbacksAdapter
        {
            public NetworkButtons Buttons;

            public override void OnInput(NetworkRunner runner, NetworkInput input) =>
                input.Set(new PlayerNetworkInput { Buttons = Buttons });
        }
    }
}
#endif
