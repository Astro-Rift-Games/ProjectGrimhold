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
    /// <summary>End-to-end proof of the caster-centered area contract on the real avatar and real creatures.</summary>
    public sealed class AbilityAreaTargetingPlayModeTests
    {
        private const string PlayerPrefabGuid = "fea3a7b256f965a4eb9b965832939741";
        private const string ParticipantPrefabGuid = "c39d451563bae6e43934008a0dadc6d6";
        private const string GreenSlimePrefabGuid = "5deca87613df0fa409d98702aec643d4";
        private static readonly Vector3 Far = new Vector3(40f, 0f, 0f);

        private NetworkRunner _runner;
        private PlayerAbilityRuntimeSimulationDriver _driver;
        private AreaInputDriver _input;
        private PlayerAbilityRuntimeNetworkController _avatar;
        private TestAreaAbilityExecutionBehaviour _behaviour;

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
        public IEnumerator Start_WithoutAValidTarget_IsRejectedWithoutPaymentSequenceOrCooldown()
        {
            yield return Begin();
            // A player (Character layer) next to the caster is not a valid enemy.
            SpawnPlayerAvatar(new Vector3(1f, 0f, 0f));
            float stamina = Stamina(_avatar);

            yield return Press();

            Assert.That(_avatar.GetLastActivationFailure(UniversalAbilitySlot.Slot1), Is.EqualTo(AbilityActivationFailure.BehaviourRejected));
            Assert.That(Stamina(_avatar), Is.EqualTo(stamina));
            _avatar.TryGetExecutionSnapshot(UniversalAbilitySlot.Slot1, out var snapshot);
            Assert.That(snapshot.Sequence, Is.Zero);
            Assert.That(snapshot.Cooldown.IsRunning, Is.False);
            Assert.That(_behaviour.Begins, Is.Zero);
        }

        [UnityTest]
        public IEnumerator Start_IgnoresDeadAndOutOfRangeCreatures()
        {
            yield return Begin();
            var dead = SpawnEnemy(new Vector3(1f, 0f, 0f));
            yield return InSimulation(() => SetNetworked(dead, "Health", 0f, typeof(CharacterBase)));
            SpawnEnemy(new Vector3(8f, 0f, 0f));

            yield return Press();

            Assert.That(_avatar.GetLastActivationFailure(UniversalAbilitySlot.Slot1), Is.EqualTo(AbilityActivationFailure.BehaviourRejected));
            Assert.That(_behaviour.Begins, Is.Zero);
        }

        [UnityTest]
        public IEnumerator Start_WithAValidCreature_IsAcceptedAndPaysOnce()
        {
            yield return Begin();
            var enemy = SpawnEnemy(new Vector3(1f, 0f, 0f));
            float stamina = Stamina(_avatar);

            yield return Press();

            Assert.That(_avatar.GetLastActivationFailure(UniversalAbilitySlot.Slot1), Is.EqualTo(AbilityActivationFailure.None));
            Assert.That(Stamina(_avatar), Is.LessThan(stamina));
            _avatar.TryGetExecutionSnapshot(UniversalAbilitySlot.Slot1, out var snapshot);
            Assert.That(snapshot.Sequence, Is.EqualTo(1));
            Assert.That(snapshot.Cooldown.IsRunning, Is.True);
            Assert.That(_behaviour.Begins, Is.EqualTo(1));
            Assert.That(_behaviour.StartTargetIds, Is.EqualTo(new[] { enemy.Id.Value }));
        }

        [UnityTest]
        public IEnumerator Resolution_RebuildsTargets_EnteringIsAffectedAndLeavingIsNot()
        {
            yield return Begin();
            _behaviour.PreparingSeconds = 1.5f;
            var leaving = SpawnEnemy(new Vector3(1f, 0f, 0f));
            var entering = SpawnEnemy(Far);

            yield return Press();
            Assert.That(_behaviour.Begins, Is.EqualTo(1));
            Assert.That(_behaviour.StartTargetIds, Is.EqualTo(new[] { leaving.Id.Value }));
            yield return Teleport(leaving, Far + new Vector3(10f, 0f, 0f));
            yield return Teleport(entering, new Vector3(0f, 1f, 0f));
            yield return WaitUntil(() => _behaviour.Resolutions > 0);

            Assert.That(_behaviour.ResolvedTargetIds, Is.EqualTo(new[] { entering.Id.Value }));
        }

        [UnityTest]
        public IEnumerator Resolution_FollowsTheCasterCurrentPosition()
        {
            yield return Begin();
            _behaviour.PreparingSeconds = 1.5f;
            var enemy = SpawnEnemy(new Vector3(1f, 0f, 0f));

            yield return Press();
            // The creature stays put, but the caster walks away: the area must be rebuilt around the caster.
            yield return Teleport(_avatar.GetComponent<PlayerCharacter>(), new Vector3(20f, 0f, 0f));
            yield return WaitUntil(() => _behaviour.Resolutions > 0);

            Assert.That(_behaviour.StartTargetIds, Is.EqualTo(new[] { enemy.Id.Value }));
            Assert.That(_behaviour.ResolvedTargetIds, Is.Empty);
        }

        [UnityTest]
        public IEnumerator Resolution_WithoutTargets_FinishesNormallyWithoutRefundAndResolvesOnce()
        {
            yield return Begin();
            _behaviour.PreparingSeconds = 1.5f;
            var enemy = SpawnEnemy(new Vector3(1f, 0f, 0f));
            float stamina = Stamina(_avatar);

            yield return Press();
            float afterPayment = Stamina(_avatar);
            yield return Teleport(enemy, Far);
            yield return WaitUntil(() => _behaviour.Resolutions > 0);
            yield return WaitTicks();
            yield return WaitTicks();

            Assert.That(_behaviour.ResolvedTargetIds, Is.Empty);
            Assert.That(_behaviour.Resolutions, Is.EqualTo(1), "Resolution applies once per accepted sequence.");
            Assert.That(_behaviour.Stops, Is.EqualTo(1));
            Assert.That(_behaviour.LastStop, Is.EqualTo(AbilityExecutionStopReason.Completed));
            Assert.That(_avatar.HasActiveExecution, Is.False);
            Assert.That(Stamina(_avatar), Is.EqualTo(afterPayment).And.LessThan(stamina), "Cost is not refunded.");
            _avatar.TryGetExecutionSnapshot(UniversalAbilitySlot.Slot1, out var snapshot);
            Assert.That(snapshot.Sequence, Is.EqualTo(1));
            Assert.That(snapshot.Cooldown.IsRunning, Is.True, "Cooldown is not refunded.");
        }

        // ---- Fixture ---------------------------------------------------------------------

        private IEnumerator Begin()
        {
            var runnerObject = new GameObject("AbilityAreaTestRunner");
            _runner = runnerObject.AddComponent<NetworkRunner>();
            runnerObject.AddComponent<EntityRegistry>();
            _driver = runnerObject.AddComponent<PlayerAbilityRuntimeSimulationDriver>();
            _input = runnerObject.AddComponent<AreaInputDriver>();
            _runner.AddCallbacks(_input);
            _runner.ProvideInput = true;
            var start = _runner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Single,
                SessionName = $"ability-area-{Guid.NewGuid():N}",
                SceneManager = runnerObject.AddComponent<NetworkSceneManagerDefault>(),
                ObjectProvider = runnerObject.AddComponent<NetworkObjectProviderDefault>()
            });
            while (!start.IsCompleted) yield return null;
            Assert.That(start.Result.Ok, Is.True, start.Result.ShutdownReason.ToString());

            Assert.That(CharacterAttributeState.TryCreate(20, 20, 20, 20, 20, 20, 0, out var attributes), Is.True);
            RaidParticipantId.TryCreate(1, out var participantId);
            var participant = _runner.Spawn(LoadPrefab(ParticipantPrefabGuid),
                onBeforeSpawned: (_, instance) => instance.GetComponent<NetworkRaidParticipant>().Initialize(
                    "ability-area", participantId, attributes, ExperienceCurve.InitialLevel, 0, "ability-test",
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
                    var behaviour = instance.gameObject.AddComponent<TestAreaAbilityExecutionBehaviour>();
                    SetField(behaviour, "_definition", definition, typeof(AbilityExecutionBehaviour));
                    SetField(runtime, "_executionBehaviours", new AbilityExecutionBehaviour[] { behaviour });
                    _behaviour = behaviour;
                });
            Assert.That(participant.TrySetCurrentAvatar(avatar), Is.True);
            _avatar = avatar.GetComponent<PlayerAbilityRuntimeNetworkController>();
            yield return WaitUntil(() => _avatar.IsInitialized);
        }

        private EnemyCharacter SpawnEnemy(Vector3 position)
        {
            var enemy = _runner.Spawn(LoadPrefab(GreenSlimePrefabGuid), position, Quaternion.identity);
            // Keep the creature still: only the test moves it.
            foreach (var behaviour in enemy.GetComponents<NetworkBehaviour>())
            {
                if (behaviour is EnemyFSM || behaviour is EnemyMovementAIController || behaviour is EnemyCombatAIController)
                    behaviour.enabled = false;
            }
            Physics2D.SyncTransforms();
            return enemy.GetComponent<EnemyCharacter>();
        }

        private void SpawnPlayerAvatar(Vector3 position)
        {
            ExpectAvatarValidationError();
            _runner.Spawn(LoadPrefab(PlayerPrefabGuid), position, Quaternion.identity, inputAuthority: null);
            Physics2D.SyncTransforms();
        }

        private static void ExpectAvatarValidationError() =>
            LogAssert.Expect(UnityEngine.LogType.Error,
                "PlayerExtractionProgressController requires character, extraction controller, " +
                "registry, assignment service, and valid receiver/reader registrations.");

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

        private static float Stamina(PlayerAbilityRuntimeNetworkController runtime) =>
            runtime.GetComponent<PlayerStaminaNetworkController>().CurrentStamina;

        private static void SetField(object target, string name, object value, Type declaringType = null) =>
            (declaringType ?? target.GetType()).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);

        private static void SetNetworked(object target, string name, object value, Type declaringType = null) =>
            (declaringType ?? target.GetType()).GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .SetValue(target, value);

        private sealed class AreaInputDriver : NetworkRunnerCallbacksAdapter
        {
            public NetworkButtons Buttons;

            public override void OnInput(NetworkRunner runner, NetworkInput input) =>
                input.Set(new PlayerNetworkInput { Buttons = Buttons });
        }
    }
}
#endif
