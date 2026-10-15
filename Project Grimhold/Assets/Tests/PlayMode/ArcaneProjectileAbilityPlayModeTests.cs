#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Reflection;
using Fusion;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Assert = NUnit.Framework.Assert;
using LogType = UnityEngine.LogType;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Abilities
{
    public sealed class ArcaneProjectileAbilityPlayModeTests
    {
        private const string PlayerGuid = "fea3a7b256f965a4eb9b965832939741";
        private const string ParticipantGuid = "c39d451563bae6e43934008a0dadc6d6";
        private const string EnemyGuid = "5deca87613df0fa409d98702aec643d4";
        private const string ProjectileGuid = "5cf65ecb3f382c147875a551556fad82";
        private NetworkRunner _runner;
        private PlayerAbilityRuntimeSimulationDriver _driver;
        private ArcaneInputDriver _input;
        private PlayerAbilityRuntimeNetworkController _runtime;
        private AbilityExecutionBehaviour _behaviour;
        private RecordingSpawner _spawner;
        private GameObject _wall;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_wall != null) Object.Destroy(_wall);
            if (_runner == null) yield break;
            var shutdown = _runner.Shutdown();
            while (!shutdown.IsCompleted) yield return null;
            Object.Destroy(_runner.gameObject);
        }

        [UnityTest]
        public IEnumerator Preparing_PaysOnceCapturesFacingAndSpawnsOnceAtDeadlineEvenWithoutInput()
        {
            yield return Begin();
            // Walk left to set the facing, then stop: the cursor aim keeps pointing right.
            _input.Move = Vector2.left;
            yield return WaitTicks();
            _input.Move = Vector2.zero;
            yield return WaitTicks();
            float mana = Mana();
            yield return Press();
            Assert.That(_runtime.TryGetExecutionSnapshot(UniversalAbilitySlot.Slot1, out var accepted), Is.True);
            Assert.That(accepted.Phase, Is.EqualTo(AbilityExecutionPhase.Preparing));
            Assert.That(accepted.AimDirection, Is.EqualTo(Vector2.left), "Arcane follows the facing, not the cursor.");
            Assert.That(mana - Mana(), Is.EqualTo(15f).Within(0.001f));
            Assert.That(_spawner.CallCount, Is.Zero);
            int deadline = (int)accepted.PhaseDeadline.TargetTick;
            Assert.That(deadline - _runner.Tick.Raw, Is.GreaterThan(0));
            _input.Move = Vector2.up;
            _input.Aim = Vector2.down * 10f;
            yield return WaitTicks();
            _input.Move = Vector2.zero;
            _input.SupplyInput = false;
            yield return WaitUntil(() => _spawner.CallCount == 1);
            Assert.That(_spawner.LastRequest.SimulationTick, Is.GreaterThanOrEqualTo(deadline));
            Assert.That(_spawner.LastRequest.Direction, Is.EqualTo(accepted.AimDirection));
            Assert.That(_spawner.LastRequest.ProjectilePrefab, Is.EqualTo(new NetworkPrefabRef(ProjectileGuid)));
            Assert.That(ReadRequestFlag("IsUnbounded"), Is.True);
            Assert.That(ReadRequestFlag("EnemyOnly"), Is.True);
            yield return WaitTicks();
            yield return InSimulation(() =>
            {
                _behaviour.Rebind(Context(accepted.AimDirection), accepted);
                _behaviour.Simulate(Context(accepted.AimDirection), accepted);
            });
            Assert.That(_spawner.CallCount, Is.EqualTo(1), "Resolution/rebind cannot repeat a consumed sequence.");
            Assert.That(Mana(), Is.EqualTo(mana - 15f).Within(0.001f));
            Assert.That(_runtime.IsOnCooldown(UniversalAbilitySlot.Slot1), Is.True);
        }

        [UnityTest]
        public IEnumerator Preparation_KnockbackStopsBeforeSpawnAndRetainsCostAndCooldown()
        {
            yield return AssertInterrupted(AbilityExecutionStopReason.Knockback);
        }

        [UnityTest]
        public IEnumerator Preparation_StunStopsBeforeSpawnAndRetainsCostAndCooldown()
        {
            yield return AssertInterrupted(AbilityExecutionStopReason.Stun);
        }

        [UnityTest]
        public IEnumerator Preparation_DownedStopsBeforeSpawnAndRetainsCostAndCooldown()
        {
            yield return AssertInterrupted(AbilityExecutionStopReason.Downed);
        }

        [UnityTest]
        public IEnumerator Preparation_ReducesActualVoluntaryMovementAndRestoresItAfterInterruption()
        {
            yield return Begin();
            _input.Move = Vector2.up;
            yield return WaitTicks();
            float walking = 0f;
            yield return InSimulation(() => walking = _runtime.GetComponent<PlayerMovementNetworkController>().LastAppliedDisplacement.magnitude);
            yield return Press();
            yield return WaitTicks();
            float preparing = 0f;
            yield return InSimulation(() => preparing = _runtime.GetComponent<PlayerMovementNetworkController>().LastAppliedDisplacement.magnitude);
            Assert.That(walking, Is.GreaterThan(0f));
            Assert.That(preparing, Is.GreaterThan(0f).And.LessThan(walking));
            yield return InSimulation(() => Assert.That(_runtime.TryInterrupt(
                UniversalAbilitySlot.Slot1, 1, AbilityExecutionStopReason.Stun), Is.True));
            yield return WaitTicks();
            float restored = 0f;
            yield return InSimulation(() => restored = _runtime.GetComponent<PlayerMovementNetworkController>().LastAppliedDisplacement.magnitude);
            Assert.That(restored, Is.EqualTo(walking).Within(0.0001f));
        }

        [UnityTest]
        public IEnumerator Projectile_IgnoresOtherPlayerAndConsumesFirstEnemyWithoutPiercing()
        {
            yield return Begin(realSpawner: true);
            PlayerCharacter otherPlayer = SpawnOtherPlayer(new Vector3(1f, 0f, 0f));
            EnemyCharacter first = SpawnEnemy(new Vector3(2f, 0f, 0f));
            EnemyCharacter second = SpawnEnemy(new Vector3(4f, 0f, 0f));
            float playerHealth = otherPlayer.Health;
            float firstHealth = first.Health;
            float secondHealth = second.Health;
            yield return Press();
            yield return WaitUntil(() => first.Health < firstHealth);
            float afterImpact = first.Health;
            yield return WaitSimulationSeconds(2.5f);
            Assert.That(otherPlayer.Health, Is.EqualTo(playerHealth));
            Assert.That(first.Health, Is.EqualTo(afterImpact), "A consumed impact cannot apply damage twice.");
            Assert.That(second.Health, Is.EqualTo(secondHealth), "The projectile never pierces the first valid enemy.");
            Assert.That(_spawner.CallCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator Projectile_AfterCasterDespawnStillReachesAndDamagesEnemy()
        {
            yield return Begin(realSpawner: true);
            EnemyCharacter enemy = SpawnEnemy(new Vector3(4f, 0f, 0f));
            float health = enemy.Health;
            yield return Press();
            yield return WaitUntil(() => _spawner.CallCount == 1);
            yield return InSimulation(() => _runner.Despawn(_runtime.Object));
            yield return WaitUntil(() => enemy.Health < health);
            // This tests avatar removal, not a full Extraction/defeat orchestration or attribution reward.
        }

        [UnityTest]
        public IEnumerator Projectile_UnboundedRequestKeepsTravellingWithZeroRangeAndLifetime()
        {
            yield return Begin(realSpawner: true);
            yield return Press();
            yield return WaitUntil(() => _spawner.CallCount == 1);
            Assert.That(_spawner.LastRequest.LifetimeSeconds, Is.Zero);
            Assert.That(_spawner.LastRequest.MaximumRange, Is.Zero);
            NetworkProjectile projectile = null;
            foreach (NetworkProjectile candidate in Object.FindObjectsByType<NetworkProjectile>(FindObjectsSortMode.None))
                if (candidate.Runner == _runner) projectile = candidate;
            Assert.That(projectile, Is.Not.Null);
            NetworkObject networkObject = projectile.Object;
            Vector2 start = projectile.transform.position;
            yield return WaitSimulationSeconds(1f);
            Assert.That(networkObject != null && networkObject.IsValid, Is.True,
                "Arcane cannot expire from its disabled range or lifetime limits.");
            Assert.That(Vector2.Distance(start, projectile.transform.position), Is.GreaterThan(1.5f));
        }

        [UnityTest]
        public IEnumerator Projectile_EnvironmentConsumesItBeforeEnemyWithoutApplyingDamage()
        {
            yield return Begin(realSpawner: true);
            _wall = new GameObject("ArcaneBlockingWall");
            _wall.layer = LayerMask.NameToLayer("WorldCollision");
            _wall.transform.position = Vector3.right;
            _wall.AddComponent<BoxCollider2D>().size = new Vector2(0.2f, 4f);
            EnemyCharacter enemy = SpawnEnemy(new Vector3(3f, 0f, 0f));
            float health = enemy.Health;
            Physics2D.SyncTransforms();
            yield return Press();
            yield return WaitUntil(() => _spawner.CallCount == 1);
            yield return WaitSimulationSeconds(2f);
            Assert.That(enemy.Health, Is.EqualTo(health));
            foreach (NetworkProjectile projectile in Object.FindObjectsByType<NetworkProjectile>(FindObjectsSortMode.None))
                Assert.That(projectile.Runner == _runner, Is.False, "A wall consumes the projectile rather than stopping it indefinitely.");
        }

        private IEnumerator AssertInterrupted(AbilityExecutionStopReason reason)
        {
            yield return Begin();
            float mana = Mana();
            yield return Press();
            _runtime.TryGetExecutionSnapshot(UniversalAbilitySlot.Slot1, out var accepted);
            Assert.That(accepted.Phase, Is.EqualTo(AbilityExecutionPhase.Preparing));
            yield return InSimulation(() =>
            {
                if (reason == AbilityExecutionStopReason.Downed)
                {
                    _runtime.Object.AssignInputAuthority(PlayerRef.None);
                    Assert.That(_runtime.GetComponent<PlayerDownedStateNetworkController>().TryEnterDowned(), Is.True);
                }
                else if (reason == AbilityExecutionStopReason.Knockback)
                {
                    _runtime.GetComponent<PlayerMovementNetworkController>().ApplyKnockbackImpulse(Vector2.up, 1f);
                }
                else Assert.That(_runtime.TryInterrupt(UniversalAbilitySlot.Slot1, accepted.Sequence, reason), Is.True);
            });
            yield return WaitSimulationSeconds(0.7f);
            Assert.That(_runtime.HasActiveExecution, Is.False);
            Assert.That(_spawner.CallCount, Is.Zero);
            Assert.That(Mana(), Is.EqualTo(mana - 15f).Within(0.001f));
            Assert.That(_runtime.TryGetExecutionSnapshot(UniversalAbilitySlot.Slot1, out var stopped), Is.True);
            Assert.That(stopped.Sequence, Is.EqualTo(accepted.Sequence));
            Assert.That(stopped.Cooldown.TargetTick, Is.EqualTo(accepted.Cooldown.TargetTick));
        }

        private IEnumerator Begin(bool realSpawner = false)
        {
            Type type = typeof(AbilityExecutionBehaviour).Assembly.GetType("ArcaneProjectileAbilityBehaviour");
            Assert.That(type, Is.Not.Null, "TASK202 requires ArcaneProjectileAbilityBehaviour.");
            var runnerObject = new GameObject("ArcaneProjectileTestRunner");
            _runner = runnerObject.AddComponent<NetworkRunner>();
            runnerObject.AddComponent<EntityRegistry>();
            _driver = runnerObject.AddComponent<PlayerAbilityRuntimeSimulationDriver>();
            _input = runnerObject.AddComponent<ArcaneInputDriver>();
            _runner.AddCallbacks(_input);
            _runner.ProvideInput = true;
            var start = _runner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Single,
                SessionName = $"ability-arcane-{Guid.NewGuid():N}",
                SceneManager = runnerObject.AddComponent<NetworkSceneManagerDefault>(),
                ObjectProvider = runnerObject.AddComponent<NetworkObjectProviderDefault>()
            });
            while (!start.IsCompleted) yield return null;
            Assert.That(start.Result.Ok, Is.True, start.Result.ShutdownReason.ToString());
            Assert.That(CharacterAttributeState.TryCreate(20, 20, 20, 20, 20, 20, 0, out var attributes), Is.True);
            Assert.That(RaidParticipantId.TryCreate(1, out var id), Is.True);
            var participant = _runner.Spawn(LoadPrefab(ParticipantGuid), onBeforeSpawned: (_, instance) =>
                instance.GetComponent<NetworkRaidParticipant>().Initialize("ability-arcane", id, attributes,
                    ExperienceCurve.InitialLevel, 0, "ability-test",
                    preparedAbilities: new PreparedAbilityLoadout(new AbilityId("arcane_projectile"), default)))
                .GetComponent<NetworkRaidParticipant>();
            ExpectAvatarValidationError();
            NetworkObject avatar = _runner.Spawn(LoadPrefab(PlayerGuid), Vector3.zero, Quaternion.identity,
                _runner.LocalPlayer, onBeforeSpawned: (_, instance) =>
                {
                    instance.GetComponent<RaidAvatarParticipantLink>().Initialize(participant.Object);
                    _runtime = instance.GetComponent<PlayerAbilityRuntimeNetworkController>();
                    var catalog = (AbilityDefinitionCatalog)typeof(PlayerAbilityRuntimeNetworkController)
                        .GetField("_catalog", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_runtime);
                    Assert.That(catalog.TryGet(new AbilityId("arcane_projectile"), out var definition), Is.True);
                    // Arcane leaves along the facing: start facing the targets placed to the right of the caster.
                    SetField(instance.GetComponent<PlayerMovementNetworkController>(), "_defaultFacingDirection", Vector2.right);
                    _behaviour = instance.GetComponent(type) as AbilityExecutionBehaviour;
                    if (_behaviour == null) _behaviour = (AbilityExecutionBehaviour)instance.gameObject.AddComponent(type);
                    _spawner = instance.gameObject.AddComponent<RecordingSpawner>();
                    _spawner.UseRealSpawner = realSpawner;
                    SetField(_behaviour, "_definition", definition, typeof(AbilityExecutionBehaviour));
                    SetField(_behaviour, "_projectileSpawnerSource", _spawner);
                    SetField(_behaviour, "_projectilePrefab", new NetworkPrefabRef(ProjectileGuid));
                    SetField(_behaviour, "_impactLayerMask", new LayerMask
                        { value = LayerMask.GetMask("Character", "WorldCollision", "Obstacles") });
                    SetField(_behaviour, "_projectileSpeed", 2f);
                    SetField(_behaviour, "_damage", 5f);
                    SetField(_behaviour, "_preparationSeconds", 0.5f);
                    SetField(_behaviour, "_preparationMovementSpeedMultiplier", 0.4f);
                    SetField(_runtime, "_executionBehaviours", new AbilityExecutionBehaviour[] { _behaviour });
                });
            Assert.That(participant.TrySetCurrentAvatar(avatar), Is.True);
            yield return WaitUntil(() => _runtime.IsInitialized && _runtime.GetComponent<PlayerManaNetworkController>().IsInitialized);
        }

        private EnemyCharacter SpawnEnemy(Vector3 position)
        {
            NetworkObject enemy = _runner.Spawn(LoadPrefab(EnemyGuid), position, Quaternion.identity);
            foreach (NetworkBehaviour component in enemy.GetComponents<NetworkBehaviour>())
                if (component is EnemyFSM || component is EnemyMovementAIController || component is EnemyCombatAIController)
                    component.enabled = false;
            Physics2D.SyncTransforms();
            return enemy.GetComponent<EnemyCharacter>();
        }

        private PlayerCharacter SpawnOtherPlayer(Vector3 position)
        {
            ExpectAvatarValidationError();
            NetworkObject avatar = _runner.Spawn(LoadPrefab(PlayerGuid), position, Quaternion.identity, inputAuthority: null);
            Physics2D.SyncTransforms();
            return avatar.GetComponent<PlayerCharacter>();
        }

        private AbilityExecutionContext Context(Vector2 aim)
        {
            ConstructorInfo constructor = typeof(AbilityExecutionContext).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)[0];
            _runtime.TryGetSlot(UniversalAbilitySlot.Slot1, out var slot);
            return (AbilityExecutionContext)constructor.Invoke(new object[]
                { _runner, _runtime.GetComponent<PlayerCharacter>(), UniversalAbilitySlot.Slot1, slot.Definition,
                    default(CharacterAttributeState), null, aim });
        }

        private bool ReadRequestFlag(string name)
        {
            PropertyInfo property = typeof(ProjectileSpawnRequest).GetProperty(name);
            Assert.That(property, Is.Not.Null);
            return (bool)property.GetValue(_spawner.LastRequest);
        }

        private NetworkObject LoadPrefab(string guid) =>
            _runner.Config.PrefabTable.Load(_runner.Config.PrefabTable.GetId(NetworkObjectGuid.Parse(guid)), true);

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

        private IEnumerator WaitSimulationSeconds(float seconds)
        {
            int until = _runner.Tick.Raw + Mathf.CeilToInt(seconds / _runner.DeltaTime);
            yield return WaitUntil(() => _runner.Tick.Raw >= until);
        }

        private IEnumerator InSimulation(Action operation)
        {
            int previous = _driver.CompletionSequence;
            _driver.RequestOperation(operation);
            yield return WaitUntil(() => _driver.CompletionSequence != previous);
        }

        private static IEnumerator WaitUntil(Func<bool> predicate)
        {
            float deadline = Time.realtimeSinceStartup + 8f;
            while (!predicate() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(predicate(), Is.True, "The expected authoritative state was not reached.");
        }

        private float Mana() => _runtime.GetComponent<PlayerManaNetworkController>().CurrentMana;

        private static void ExpectAvatarValidationError() => LogAssert.Expect(LogType.Error,
            "PlayerExtractionProgressController requires character, extraction controller, " +
            "registry, assignment service, and valid receiver/reader registrations.");

        private static void SetField(object target, string name, object value, Type declaringType = null)
        {
            FieldInfo field = (declaringType ?? target.GetType()).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Required configuration field {name} was not found.");
            field.SetValue(target, value);
        }

        private sealed class RecordingSpawner : MonoBehaviour, IProjectileSpawner
        {
            public bool UseRealSpawner;
            public int CallCount;
            public ProjectileSpawnRequest LastRequest;
            public ProjectileSpawnResult Spawn(in ProjectileSpawnRequest request)
            {
                CallCount++;
                LastRequest = request;
                return UseRealSpawner ? GetComponent<FusionProjectileSpawner>().Spawn(request) : new ProjectileSpawnResult(true);
            }
        }

        private sealed class ArcaneInputDriver : NetworkRunnerCallbacksAdapter
        {
            public NetworkButtons Buttons;
            public Vector2 Aim = Vector2.right * 10f;
            public Vector2 Move;
            public bool SupplyInput = true;
            public override void OnInput(NetworkRunner runner, NetworkInput input)
            {
                if (SupplyInput) input.Set(new PlayerNetworkInput { Buttons = Buttons, AimWorldPosition = Aim, MoveDirection = Move });
            }
        }
    }
}
#endif
