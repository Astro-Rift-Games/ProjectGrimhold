#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Reflection;
using Fusion;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Assert = NUnit.Framework.Assert;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Abilities
{
    /// <summary>
    /// Proves the productive avatar prefab composes Charge and Seismic Strike as authored: no test behavior is
    /// added and the serialized behaviour bindings, definitions and layer masks are exactly the prefab's.
    /// </summary>
    public sealed class AbilityPrefabCompositionPlayModeTests
    {
        private const string PlayerPrefabGuid = "fea3a7b256f965a4eb9b965832939741";
        private const string ParticipantPrefabGuid = "c39d451563bae6e43934008a0dadc6d6";
        private const string GreenSlimePrefabGuid = "5deca87613df0fa409d98702aec643d4";

        private NetworkRunner _runner;
        private PlayerAbilityRuntimeSimulationDriver _driver;
        private CompositionInputDriver _input;
        private PlayerAbilityRuntimeNetworkController _avatar;
        private PlayerMovementNetworkController _movement;

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
        public IEnumerator Charge_OnThePrefab_IsAcceptedPaysAndMovesTheAvatar()
        {
            yield return Begin();
            yield return SetAim(Vector2.right);
            float stamina = Stamina();
            _avatar.TryGetSlot(UniversalAbilitySlot.Slot1, out var slot);

            yield return Press(PlayerInputButton.AbilitySlot1);
            yield return WaitForCompletion();

            Assert.That(_avatar.GetLastActivationFailure(UniversalAbilitySlot.Slot1), Is.EqualTo(AbilityActivationFailure.None));
            Assert.That(stamina - Stamina(), Is.EqualTo(slot.Definition.Cost).Within(0.001f), "The cost is paid once.");
            Assert.That(Position.x, Is.GreaterThan(1f), "The avatar moved along the captured aim.");
            Assert.That(Position.y, Is.EqualTo(0f).Within(0.05f));
            Assert.That(_movement.IsForcedDisplacementActive, Is.False);
            Assert.That(_avatar.IsOnCooldown(UniversalAbilitySlot.Slot1), Is.True);
        }

        [UnityTest]
        public IEnumerator SeismicStrike_OnThePrefab_WithAnEnemyInRangeResolvesDamageOnce()
        {
            yield return Begin();
            var enemy = SpawnEnemy(new Vector3(-1.5f, 0f, 0f));
            float health = enemy.Health;
            float stamina = Stamina();
            _avatar.TryGetSlot(UniversalAbilitySlot.Slot2, out var slot);

            yield return Press(PlayerInputButton.AbilitySlot2);
            Assert.That(_avatar.GetLastActivationFailure(UniversalAbilitySlot.Slot2), Is.EqualTo(AbilityActivationFailure.None));
            Assert.That(stamina - Stamina(), Is.EqualTo(slot.Definition.Cost).Within(0.001f), "The cost is paid once.");
            yield return WaitForCompletion();
            yield return WaitTicks();

            float expected = ReadBehaviourField<float>(typeof(SeismicStrikeAbilityBehaviour), "_damage");
            Assert.That(expected, Is.GreaterThan(0f));
            Assert.That(enemy.Health, Is.EqualTo(health - expected).Within(0.001f), "Damage is applied exactly once.");
            float afterResolution = enemy.Health;
            yield return WaitTicks();
            Assert.That(enemy.Health, Is.EqualTo(afterResolution), "The resolution does not repeat.");
        }

        [UnityTest]
        public IEnumerator SeismicStrike_OnThePrefab_KnockbackOnTheCasterDuringPreparationInterruptsWithoutRefund()
        {
            yield return Begin();
            var enemy = SpawnEnemy(new Vector3(-1.5f, 0f, 0f));
            float health = enemy.Health;

            yield return Press(PlayerInputButton.AbilitySlot2);
            Assert.That(_avatar.HasActiveExecution, Is.True, "The preparation is running.");
            float afterPayment = Stamina();
            yield return InSimulation(() => _movement.ApplyKnockbackImpulse(Vector2.right, 4f));
            yield return WaitSeconds(1.2f);

            Assert.That(_avatar.HasActiveExecution, Is.False);
            Assert.That(enemy.Health, Is.EqualTo(health), "An interrupted preparation applies no effect.");
            Assert.That(Stamina(), Is.EqualTo(afterPayment), "Cost is not refunded.");
            _avatar.TryGetExecutionSnapshot(UniversalAbilitySlot.Slot2, out var snapshot);
            Assert.That(snapshot.Sequence, Is.EqualTo(1));
            Assert.That(snapshot.Cooldown.IsRunning, Is.True, "Cooldown is not refunded.");
        }

        // ---- Fixture ---------------------------------------------------------------------

        private IEnumerator Begin()
        {
            var runnerObject = new GameObject("AbilityCompositionTestRunner");
            _runner = runnerObject.AddComponent<NetworkRunner>();
            runnerObject.AddComponent<EntityRegistry>();
            _driver = runnerObject.AddComponent<PlayerAbilityRuntimeSimulationDriver>();
            _input = runnerObject.AddComponent<CompositionInputDriver>();
            _runner.AddCallbacks(_input);
            _runner.ProvideInput = true;
            var start = _runner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Single,
                SessionName = $"ability-composition-{Guid.NewGuid():N}",
                SceneManager = runnerObject.AddComponent<NetworkSceneManagerDefault>(),
                ObjectProvider = runnerObject.AddComponent<NetworkObjectProviderDefault>()
            });
            while (!start.IsCompleted) yield return null;
            Assert.That(start.Result.Ok, Is.True, start.Result.ShutdownReason.ToString());

            Assert.That(CharacterAttributeState.TryCreate(20, 20, 20, 20, 20, 20, 0, out var attributes), Is.True);
            RaidParticipantId.TryCreate(1, out var participantId);
            var participant = _runner.Spawn(LoadPrefab(ParticipantPrefabGuid),
                onBeforeSpawned: (_, instance) => instance.GetComponent<NetworkRaidParticipant>().Initialize(
                    "ability-composition", participantId, attributes, ExperienceCurve.InitialLevel, 0, "ability-test",
                    preparedAbilities: new PreparedAbilityLoadout(new AbilityId("charge"), new AbilityId("seismic_strike"))))
                .GetComponent<NetworkRaidParticipant>();
            ExpectAvatarValidationError();
            var avatar = _runner.Spawn(LoadPrefab(PlayerPrefabGuid), Vector3.zero, Quaternion.identity,
                _runner.LocalPlayer, onBeforeSpawned: (_, instance) =>
                {
                    instance.GetComponent<RaidAvatarParticipantLink>().Initialize(participant.Object);
                    // Only the stamina regeneration tuning is neutralized so cost assertions stay exact.
                    var stamina = instance.GetComponent<PlayerStaminaNetworkController>();
                    typeof(PlayerStaminaNetworkController).GetField("_regenerationPerSecond",
                        BindingFlags.Instance | BindingFlags.NonPublic).SetValue(stamina, 0f);
                });
            Assert.That(participant.TrySetCurrentAvatar(avatar), Is.True);
            _avatar = avatar.GetComponent<PlayerAbilityRuntimeNetworkController>();
            _movement = avatar.GetComponent<PlayerMovementNetworkController>();
            yield return WaitUntil(() => _avatar.IsInitialized);
        }

        private Vector2 Position => _avatar.transform.position;

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

        private T ReadBehaviourField<T>(Type behaviourType, string name) =>
            (T)behaviourType.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(_avatar.GetComponent(behaviourType));

        private static void ExpectAvatarValidationError() =>
            LogAssert.Expect(UnityEngine.LogType.Error,
                "PlayerExtractionProgressController requires character, extraction controller, " +
                "registry, assignment service, and valid receiver/reader registrations.");

        private NetworkObject LoadPrefab(string guid)
        {
            var id = _runner.Config.PrefabTable.GetId(NetworkObjectGuid.Parse(guid));
            return _runner.Config.PrefabTable.Load(id, true);
        }

        private IEnumerator Press(PlayerInputButton button)
        {
            _input.Buttons = default;
            yield return WaitTicks();
            _input.Buttons.Set(button, true);
            yield return WaitTicks();
            _input.Buttons = default;
        }

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

        private static void SetNetworked(object target, string name, object value) =>
            target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .SetValue(target, value);

        private sealed class CompositionInputDriver : NetworkRunnerCallbacksAdapter
        {
            public NetworkButtons Buttons;

            public override void OnInput(NetworkRunner runner, NetworkInput input) =>
                input.Set(new PlayerNetworkInput { Buttons = Buttons });
        }
    }
}
#endif
