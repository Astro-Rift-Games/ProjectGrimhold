#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
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
    /// End-to-end proof of the Trap ability on the real avatar prefab (the behaviour and the trap prefab reference are
    /// the prefab's own), a real runner, real creatures and the real damage pipeline.
    /// </summary>
    public sealed class TrapAbilityPlayModeTests
    {
        private const string PlayerPrefabGuid = "fea3a7b256f965a4eb9b965832939741";
        private const string ParticipantPrefabGuid = "c39d451563bae6e43934008a0dadc6d6";
        private const string GreenSlimePrefabGuid = "5deca87613df0fa409d98702aec643d4";
        private const float PlacementDistance = 1.5f;
        private const float Tolerance = 0.05f;

        private NetworkRunner _runner;
        private PlayerAbilityRuntimeSimulationDriver _driver;
        private TrapInputDriver _input;
        private PlayerAbilityRuntimeNetworkController _avatar;
        private PlayerMovementNetworkController _movement;
        private TrapAbilityBehaviour _behaviour;
        private CharacterAttributeState _attributes;
        private readonly List<GameObject> _scenery = new();
        private readonly List<(UniversalAbilitySlot Slot, AbilityActivationFailure Failure)> _rejections = new();

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
        public IEnumerator ValidCast_SpawnsExactlyOneTrapAheadAlongTheAim_PaysOnceAndStartsTheCooldown()
        {
            yield return Begin();
            yield return SetAim(Vector2.right);
            float stamina = Stamina();
            _avatar.TryGetSlot(UniversalAbilitySlot.Slot1, out var slot);

            yield return Press();
            yield return WaitForCompletion();
            yield return WaitTicks();

            Assert.That(_avatar.GetLastActivationFailure(UniversalAbilitySlot.Slot1), Is.EqualTo(AbilityActivationFailure.None));
            var traps = Traps();
            Assert.That(traps, Has.Count.EqualTo(1));
            Vector2 position = traps[0].transform.position;
            Assert.That(position.x, Is.EqualTo(PlacementDistance).Within(Tolerance));
            Assert.That(position.y, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(traps[0].CasterId, Is.EqualTo(_avatar.GetComponent<PlayerCharacter>().Id));
            Assert.That(stamina - Stamina(), Is.EqualTo(slot.Definition.Cost).Within(0.001f), "The cost is paid once.");
            Assert.That(_avatar.IsOnCooldown(UniversalAbilitySlot.Slot1), Is.True);
            Assert.That(_rejections, Is.Empty);
        }

        [UnityTest]
        public IEnumerator AimChangedAfterTheStart_DoesNotMoveTheTrap()
        {
            yield return Begin();
            yield return SetAim(Vector2.right);

            yield return Press();
            yield return SetAim(Vector2.up);
            yield return WaitForCompletion();
            yield return WaitTicks();

            var traps = Traps();
            Assert.That(traps, Has.Count.EqualTo(1));
            Vector2 position = traps[0].transform.position;
            Assert.That(position.x, Is.EqualTo(PlacementDistance).Within(Tolerance));
            Assert.That(position.y, Is.EqualTo(0f).Within(Tolerance), "The captured aim places it, never the later one.");
        }

        [UnityTest]
        public IEnumerator BlockedPosition_IsRejectedWithoutTrapPaymentSequenceOrCooldown_AndNotifiesTheOwner()
        {
            yield return Begin();
            AddWall(new Vector2(PlacementDistance, 0f), new Vector2(0.2f, 6f));
            yield return SetAim(Vector2.right);
            float stamina = Stamina();

            yield return Press();
            yield return WaitTicks();
            yield return WaitTicks();

            Assert.That(_avatar.GetLastActivationFailure(UniversalAbilitySlot.Slot1), Is.EqualTo(AbilityActivationFailure.BehaviourRejected));
            Assert.That(Traps(), Is.Empty, "No trap is created.");
            Assert.That(Stamina(), Is.EqualTo(stamina), "No Stamina is consumed.");
            _avatar.TryGetExecutionSnapshot(UniversalAbilitySlot.Slot1, out var snapshot);
            Assert.That(snapshot.Sequence, Is.Zero, "No execution was accepted.");
            Assert.That(_avatar.IsOnCooldown(UniversalAbilitySlot.Slot1), Is.False, "No cooldown starts.");
            Assert.That(_rejections, Is.EqualTo(new[] { (UniversalAbilitySlot.Slot1, AbilityActivationFailure.BehaviourRejected) }),
                "The owner receives the rejection once.");
        }

        [UnityTest]
        public IEnumerator ObstacleBetweenCasterAndPosition_IsRejected()
        {
            yield return Begin();
            AddWall(new Vector2(0.75f, 0f), new Vector2(0.1f, 6f));
            yield return SetAim(Vector2.right);
            float stamina = Stamina();

            yield return Press();
            yield return WaitTicks();

            Assert.That(_avatar.GetLastActivationFailure(UniversalAbilitySlot.Slot1), Is.EqualTo(AbilityActivationFailure.BehaviourRejected),
                "A position behind a wall is out of reach and never valid.");
            Assert.That(Traps(), Is.Empty);
            Assert.That(Stamina(), Is.EqualTo(stamina));
        }

        [UnityTest]
        public IEnumerator Caster_StandingOnTheTrap_NeverTriggersIt()
        {
            yield return Begin();
            SetField(_behaviour, "_placementDistance", 0.2f);
            yield return SetAim(Vector2.right);

            yield return Press();
            yield return WaitForCompletion();
            yield return WaitTicks();
            yield return WaitTicks();
            yield return WaitTicks();

            var traps = Traps();
            Assert.That(traps, Has.Count.EqualTo(1));
            Assert.That(traps[0].IsTriggered, Is.False);
            Assert.That(Vector2.Distance(traps[0].transform.position, Position), Is.LessThan(0.5f), "The caster stands on it.");
        }

        [UnityTest]
        public IEnumerator Enemy_EnteringTheTrap_TriggersItOnce_ImmobilizesAndDamagesPeriodicallyAttributedToTheCaster()
        {
            yield return Begin();
            SetField(_behaviour, "_immobilizeSeconds", 1f);
            SetField(_behaviour, "_periodicDamage", 2f);
            SetField(_behaviour, "_tickIntervalSeconds", 0.25f);
            SetField(_behaviour, "_damageType", DamageType.Magical);
            yield return SetAim(Vector2.right);
            yield return Press();
            yield return WaitForCompletion();
            var trap = Traps()[0];
            var trapId = trap.Object.Id;
            var casterId = _avatar.GetComponent<PlayerCharacter>().Id;
            var enemy = SpawnEnemy(new Vector3(PlacementDistance + 0.3f, 0f, 0f));
            var effect = enemy.GetComponent<ImmobilizeEffect>();
            float health = enemy.Health;

            yield return WaitUntil(() => effect.IsActive);
            Assert.That(effect.SourceId, Is.EqualTo(casterId));
            Assert.That(effect.IsPurifiable, Is.True);
            Assert.That((bool)enemy.GetComponent<EnemyMovementAIController>().IsImmobilized, Is.True);
            yield return WaitUntil(() => !effect.IsActive);
            yield return WaitTicks();

            Assert.That(_runner.TryFindObject(trapId, out _), Is.False, "The trap is consumed by its trigger.");
            Assert.That(Traps(), Is.Empty);
            Assert.That(enemy.Health, Is.EqualTo(health - 8f).Within(0.001f),
                "Four ticks of 2 damage: one per interval for the whole immobilization, once.");
            Assert.That((bool)enemy.GetComponent<EnemyMovementAIController>().IsImmobilized, Is.False);
        }

        [UnityTest]
        public IEnumerator FourthCast_WithALimitOfThree_EvictsTheTrapWithTheLeastRemainingLifetimeOnly()
        {
            yield return Begin();
            var ids = new List<NetworkId>();
            var aims = new[] { Vector2.right, Vector2.up, Vector2.left, Vector2.down };
            for (int i = 0; i < aims.Length; i++)
            {
                yield return InSimulation(() => Assert.That(_avatar.SandboxResetCooldowns(), Is.True));
                yield return SetAim(aims[i]);
                yield return Press();
                yield return WaitForCompletion();
                yield return WaitTicks();
                var known = new HashSet<NetworkId>(ids);
                foreach (var trap in Traps())
                    if (!known.Contains(trap.Object.Id)) ids.Add(trap.Object.Id);
                if (i < 3) Assert.That(Traps(), Has.Count.EqualTo(i + 1));
            }

            Assert.That(ids, Has.Count.EqualTo(4));
            Assert.That(Traps(), Has.Count.EqualTo(3), "The limit holds.");
            Assert.That(_runner.TryFindObject(ids[0], out _), Is.False, "The oldest, with the least remaining lifetime, is removed.");
            Assert.That(_runner.TryFindObject(ids[1], out _), Is.True);
            Assert.That(_runner.TryFindObject(ids[2], out _), Is.True);
            Assert.That(_runner.TryFindObject(ids[3], out _), Is.True, "The new trap is kept.");
        }

        [UnityTest]
        public IEnumerator Trap_SurvivesTheCastCompletingAndTheCasterBeingDowned()
        {
            yield return Begin();
            yield return SetAim(Vector2.right);

            yield return Press();
            yield return WaitForCompletion();
            Assert.That(Traps(), Has.Count.EqualTo(1), "Completing the cast does not delete the trap.");

            yield return InSimulation(() =>
            {
                _avatar.Object.AssignInputAuthority(PlayerRef.None);
                Assert.That(_avatar.GetComponent<PlayerDownedStateNetworkController>().TryEnterDowned(), Is.True);
            });
            yield return WaitTicks();
            yield return WaitTicks();

            Assert.That(Traps(), Has.Count.EqualTo(1), "Neither Downed nor any stop reason deletes it.");
        }

        [UnityTest]
        public IEnumerator Stop_And_Rebind_NeverSpawnOrDeleteTraps()
        {
            yield return Begin();
            yield return SetAim(Vector2.right);
            yield return Press();
            yield return WaitForCompletion();
            yield return WaitTicks();
            _avatar.TryGetExecutionSnapshot(UniversalAbilitySlot.Slot1, out var snapshot);
            _avatar.TryGetSlot(UniversalAbilitySlot.Slot1, out var slot);
            var context = new AbilityExecutionContext(_runner, _avatar.GetComponent<PlayerCharacter>(),
                UniversalAbilitySlot.Slot1, slot.Definition, _attributes, _avatar.GetComponent<AbilityAreaTargetFinder>(),
                Vector2.right);

            yield return InSimulation(() =>
            {
                foreach (AbilityExecutionStopReason reason in Enum.GetValues(typeof(AbilityExecutionStopReason)))
                    _behaviour.Stop(context, snapshot, reason);
                _behaviour.Rebind(context, snapshot);
                _behaviour.Rebind(context, snapshot);
            });
            yield return WaitTicks();

            Assert.That(Traps(), Has.Count.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator Cooldown_BlocksASecondCast_NoSecondTrapIsCreated()
        {
            yield return Begin();
            yield return SetAim(Vector2.right);
            yield return Press();
            yield return WaitForCompletion();
            yield return WaitTicks();
            float afterFirst = Stamina();

            yield return Press();
            yield return WaitTicks();

            Assert.That(_avatar.GetLastActivationFailure(UniversalAbilitySlot.Slot1), Is.EqualTo(AbilityActivationFailure.Cooldown));
            Assert.That(Traps(), Has.Count.EqualTo(1));
            Assert.That(Stamina(), Is.EqualTo(afterFirst));
            _avatar.TryGetExecutionSnapshot(UniversalAbilitySlot.Slot1, out var snapshot);
            Assert.That(snapshot.Sequence, Is.EqualTo(1));
        }

        // ---- Fixture ---------------------------------------------------------------------

        private IEnumerator Begin()
        {
            _rejections.Clear();
            var runnerObject = new GameObject("TrapAbilityTestRunner");
            _runner = runnerObject.AddComponent<NetworkRunner>();
            runnerObject.AddComponent<EntityRegistry>();
            _driver = runnerObject.AddComponent<PlayerAbilityRuntimeSimulationDriver>();
            _input = runnerObject.AddComponent<TrapInputDriver>();
            _runner.AddCallbacks(_input);
            _runner.ProvideInput = true;
            var start = _runner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Single,
                SessionName = $"ability-trap-{Guid.NewGuid():N}",
                SceneManager = runnerObject.AddComponent<NetworkSceneManagerDefault>(),
                ObjectProvider = runnerObject.AddComponent<NetworkObjectProviderDefault>()
            });
            while (!start.IsCompleted) yield return null;
            Assert.That(start.Result.Ok, Is.True, start.Result.ShutdownReason.ToString());

            Assert.That(CharacterAttributeState.TryCreate(20, 20, 20, 20, 20, 20, 0, out var attributes), Is.True);
            _attributes = attributes;
            RaidParticipantId.TryCreate(1, out var participantId);
            var participant = _runner.Spawn(LoadPrefab(ParticipantPrefabGuid),
                onBeforeSpawned: (_, instance) => instance.GetComponent<NetworkRaidParticipant>().Initialize(
                    "ability-trap", participantId, attributes, ExperienceCurve.InitialLevel, 0, "ability-test",
                    preparedAbilities: new PreparedAbilityLoadout(new AbilityId("trap"), default)))
                .GetComponent<NetworkRaidParticipant>();
            ExpectAvatarValidationError();
            var avatar = _runner.Spawn(LoadPrefab(PlayerPrefabGuid), Vector3.zero, Quaternion.identity,
                _runner.LocalPlayer, onBeforeSpawned: (_, instance) =>
                {
                    instance.GetComponent<RaidAvatarParticipantLink>().Initialize(participant.Object);
                    // Only the stamina regeneration tuning is neutralized so cost assertions stay exact.
                    SetField(instance.GetComponent<PlayerStaminaNetworkController>(), "_regenerationPerSecond", 0f);
                });
            Assert.That(participant.TrySetCurrentAvatar(avatar), Is.True);
            _avatar = avatar.GetComponent<PlayerAbilityRuntimeNetworkController>();
            _movement = avatar.GetComponent<PlayerMovementNetworkController>();
            _behaviour = avatar.GetComponent<TrapAbilityBehaviour>();
            Assert.That(_behaviour, Is.Not.Null, "The prefab composes the Trap behaviour.");
            _avatar.ActivationRejected += (slot, failure) => _rejections.Add((slot, failure));
            yield return WaitUntil(() => _avatar.IsInitialized);
        }

        private Vector2 Position => _avatar.transform.position;

        private List<NetworkTrap> Traps()
        {
            var traps = new List<NetworkTrap>();
            _runner.GetAllBehaviours(traps);
            return traps;
        }

        private EnemyCharacter SpawnEnemy(Vector3 position)
        {
            var enemy = _runner.Spawn(LoadPrefab(GreenSlimePrefabGuid), position, Quaternion.identity);
            // Keep the creature still: only the trap may act on it.
            foreach (var behaviour in enemy.GetComponents<NetworkBehaviour>())
            {
                if (behaviour is EnemyFSM || behaviour is EnemyMovementAIController || behaviour is EnemyCombatAIController)
                    behaviour.enabled = false;
            }
            Physics2D.SyncTransforms();
            return enemy.GetComponent<EnemyCharacter>();
        }

        private void AddWall(Vector2 position, Vector2 size)
        {
            var wall = new GameObject("TrapTestWall") { layer = LayerMask.NameToLayer("WorldCollision") };
            wall.transform.position = position;
            wall.AddComponent<BoxCollider2D>().size = size;
            _scenery.Add(wall);
            Physics2D.SyncTransforms();
        }

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
        }

        private IEnumerator WaitForCompletion()
        {
            float deadline = Time.realtimeSinceStartup + 8f;
            while (_avatar.HasActiveExecution && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(_avatar.HasActiveExecution, Is.False, "The ability did not finish.");
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

        private sealed class TrapInputDriver : NetworkRunnerCallbacksAdapter
        {
            public NetworkButtons Buttons;

            public override void OnInput(NetworkRunner runner, NetworkInput input) =>
                input.Set(new PlayerNetworkInput { Buttons = Buttons });
        }
    }
}
#endif
