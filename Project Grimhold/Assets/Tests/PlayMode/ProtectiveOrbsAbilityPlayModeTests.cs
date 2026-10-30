#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Fusion;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Assert = NUnit.Framework.Assert;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Abilities
{
    /// <summary>
    /// TASK203: real runner/avatar, actual orb geometry, existing projectile/melee/damage pipeline.
    /// Reflection keeps the RED suite compilable before the new types exist. NetworkBehaviour composition
    /// must be baked on NetworkPlayer by the parent; adding a network effect after baking is not a fixture.
    /// Single-runner behaviour state copy is not evidence of multi-process Host Migration or a real proxy.
    /// </summary>
    public sealed class ProtectiveOrbsAbilityPlayModeTests
    {
        private const string PlayerGuid = "fea3a7b256f965a4eb9b965832939741";
        private const string ParticipantGuid = "c39d451563bae6e43934008a0dadc6d6";
        private const string EnemyGuid = "5deca87613df0fa409d98702aec643d4";
        private const string ProjectileGuid = "cdc42ba57b303264fb49354983542d59";
        private const string PeerScenePath = "Assets/Settings/Scenes/URP2DSceneTemplate.unity";
        private const string EffectTypeName = "ProtectiveOrbsNetworkEffect";
        private const string OrbTypeName = "ProtectiveOrbCollider";
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private NetworkRunner _runner;
        private NetworkRunner _client;
        private PlayerAbilityRuntimeSimulationDriver _driver;
        private OrbInputDriver _input;
        private PlayerAbilityRuntimeNetworkController _runtime;
        private AbilityExecutionBehaviour _behaviour;
        private NetworkBehaviour _effect;
        private RecordingResolver _contactResolver;
        private CharacterAttributeState _attributes;
        private readonly List<Object> _localObjects = new();
        private readonly Dictionary<NetworkRunner, Task<StartGameResult>> _runnerStartups = new();
        private NetworkRunner _migrationResumeRunner;
        private Task _migrationResumeCompletion;
        private int _participantSequence;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            NetworkRunner client = _client;
            NetworkRunner runner = _runner;
            GameObject clientObject = client != null ? client.gameObject : null;
            GameObject runnerObject = runner != null ? runner.gameObject : null;
            try
            {
                // Keep fixture identities available until their startup/resume/scene work has settled.
                yield return ShutdownRunner(client);
                yield return ShutdownRunner(runner);
            }
            finally
            {
                // A failed/timed-out shutdown must still dispose every captured fixture composition.
                if (clientObject != null) Object.Destroy(clientObject);
                if (runnerObject != null) Object.Destroy(runnerObject);
                foreach (Object item in _localObjects) if (item != null) Object.Destroy(item);
                _localObjects.Clear();
                _runnerStartups.Clear();
                _migrationResumeRunner = null;
                _migrationResumeCompletion = null;
                _client = null;
                _runner = null;
            }
        }

        [UnityTest]
        public IEnumerator SelfCast_NoEnemiesPaysOnceStartsCooldownAndShowsEightIndependentSlots()
        {
            yield return Begin();
            float mana = Mana();
            _runtime.TryGetSlot(UniversalAbilitySlot.Slot1, out var slot);
            yield return Press();
            yield return WaitUntil(() => !_runtime.HasActiveExecution && ActiveMask == 255u);
            Assert.That(_runtime.GetLastActivationFailure(UniversalAbilitySlot.Slot1), Is.EqualTo(AbilityActivationFailure.None));
            Assert.That(Mana(), Is.EqualTo(mana - slot.Definition.Cost).Within(0.001f));
            Assert.That(_runtime.IsOnCooldown(UniversalAbilitySlot.Slot1), Is.True);
            Assert.That(Read<int>(_effect, "InitialCount"), Is.EqualTo(8));
            Assert.That(ActiveOrbs(), Has.Count.EqualTo(8));
            foreach (Component orb in ActiveOrbs())
            {
                Assert.That(orb.GetComponent<Collider2D>().enabled, Is.True);
                SpriteRenderer renderer = orb.GetComponentInChildren<SpriteRenderer>(true);
                Assert.That(renderer, Is.Not.Null, "Every local orb needs presentation.");
                Assert.That(renderer.enabled && renderer.gameObject.activeInHierarchy && renderer.sprite != null, Is.True);
            }
            Assert.That(_contactResolver.Requests, Is.Empty, "Self casting performs no target resolution.");
            float after = Mana();
            yield return Press();
            Assert.That(_runtime.GetLastActivationFailure(UniversalAbilitySlot.Slot1), Is.EqualTo(AbilityActivationFailure.Cooldown));
            Assert.That(Mana(), Is.EqualTo(after));
            Assert.That(ActiveMask, Is.EqualTo(255u));
            _runtime.TryGetExecutionSnapshot(UniversalAbilitySlot.Slot1, out var snapshot);
            Assert.That(snapshot.Sequence, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator MissingEffect_IsRejectedBeforePaymentSequenceOrCooldown()
        {
            yield return Begin();
            SetField(_behaviour, "_effect", null);
            float mana = Mana();
            LogAssert.Expect(UnityEngine.LogType.Error, new Regex("^ProtectiveOrbsAbilityBehaviour.*"));
            yield return Press();
            Assert.That(_runtime.GetLastActivationFailure(UniversalAbilitySlot.Slot1), Is.EqualTo(AbilityActivationFailure.InvalidPlan));
            Assert.That(Mana(), Is.EqualTo(mana));
            Assert.That(ActiveMask, Is.Zero);
            _runtime.TryGetExecutionSnapshot(UniversalAbilitySlot.Slot1, out var snapshot);
            Assert.That(snapshot.Sequence, Is.Zero);
            Assert.That(snapshot.Cooldown.IsRunning, Is.False);
        }

        [UnityTest]
        public IEnumerator AcceptedBeginReplay_StopAndRebindNeverReplenishAConsumedSlotOrRestartTime()
        {
            yield return Begin();
            EnemyCharacter enemy = SpawnEnemy(Vector3.right * 10f);
            yield return Press();
            _runtime.TryGetExecutionSnapshot(UniversalAbilitySlot.Slot1, out var accepted);
            int start = Read<int>(_effect, "StartTick");
            TickTimer lifetime = Read<TickTimer>(_effect, "LifetimeTimer");
            yield return InSimulation(() =>
            {
                Assert.That(Consume(_effect, 3, enemy.Id), Is.True);
                AbilityExecutionContext context = Context();
                _behaviour.Begin(context, accepted);
                _behaviour.Begin(context, accepted);
                foreach (AbilityExecutionStopReason reason in Enum.GetValues(typeof(AbilityExecutionStopReason)))
                    _behaviour.Stop(context, accepted, reason);
                _behaviour.Rebind(context, accepted);
                _behaviour.Rebind(context, accepted);
            });
            Assert.That(ActiveMask, Is.EqualTo(255u & ~(1u << 3)));
            Assert.That(Read<int>(_effect, "StartTick"), Is.EqualTo(start));
            Assert.That(Read<TickTimer>(_effect, "LifetimeTimer").TargetTick, Is.EqualTo(lifetime.TargetTick));
            Assert.That(Orb(3).gameObject.activeInHierarchy && Orb(3).GetComponent<Collider2D>().enabled, Is.False);
        }

        [UnityTest]
        public IEnumerator EarlierAcceptedSequence_CannotReplenishOrRestartANewerCast()
        {
            yield return Begin();
            EnemyCharacter enemy = SpawnEnemy(Vector3.right * 10f);
            yield return Press();
            _runtime.TryGetExecutionSnapshot(UniversalAbilitySlot.Slot1, out var earlier);
            yield return InSimulation(() => Assert.That(_runtime.SandboxResetCooldowns(), Is.True));
            yield return Press();
            _runtime.TryGetExecutionSnapshot(UniversalAbilitySlot.Slot1, out var newer);
            Assert.That(newer.Sequence, Is.EqualTo(earlier.Sequence + 1));
            int start = Read<int>(_effect, "StartTick");
            TickTimer timer = Read<TickTimer>(_effect, "LifetimeTimer");
            yield return InSimulation(() =>
            {
                Assert.That(Consume(_effect, 0, enemy.Id), Is.True);
                _behaviour.Begin(Context(), earlier);
            });
            Assert.That(ActiveMask, Is.EqualTo(254u));
            Assert.That(Read<int>(_effect, "StartTick"), Is.EqualTo(start));
            Assert.That(Read<TickTimer>(_effect, "LifetimeTimer").TargetTick, Is.EqualTo(timer.TargetTick));
        }

        [UnityTest]
        public IEnumerator Orbit_UsesSimulationTickAndOriginalSlotIndicesAfterConsumption()
        {
            yield return Begin(speed: 90f);
            EnemyCharacter enemy = SpawnEnemy(Vector3.right * 10f);
            yield return Press();
            yield return InSimulation(() => Assert.That(Consume(_effect, 0, enemy.Id), Is.True));
            yield return WaitTicks();
            yield return InSimulation(() =>
            {
                _effect.FixedUpdateNetwork();
                float elapsed = (_runner.Tick.Raw - Read<int>(_effect, "StartTick")) * _runner.DeltaTime;
                double radians = (360d * 4 / 8 + 90d * elapsed) * Math.PI / 180d;
                Vector2 expected = (Vector2)_runtime.transform.position +
                    new Vector2((float)Math.Cos(radians), (float)Math.Sin(radians)) * 2f;
                Assert.That(Vector2.Distance(Orb(4).transform.position, expected), Is.LessThan(0.01f),
                    "Removing slot zero must not redistribute seven survivors around a new circle.");
            });
        }

        [UnityTest]
        public IEnumerator EnemyProjectile_ConsumesOnlyCollidedSlotWithoutHealthDamageResolverOrAssist() =>
            VerifyIndependentEnemyProjectile(ShooterState.Live);

        [UnityTest]
        public IEnumerator EnemyProjectile_DeadShooterStillConsumesOnlyCollidedSlotWithoutDamageOrAssist() =>
            VerifyIndependentEnemyProjectile(ShooterState.Dead);

        [UnityTest]
        public IEnumerator EnemyProjectile_DespawnedShooterStillConsumesOnlyCollidedSlotWithoutDamageOrAssist() =>
            VerifyIndependentEnemyProjectile(ShooterState.Despawned);

        [UnityTest]
        public IEnumerator FriendlyProjectile_PassesThroughOrbsRatherThanTreatingThemAsUnregisteredWorldGeometry()
        {
            yield return Begin();
            yield return Press();
            NetworkObject projectile = null;
            var resolver = new RecordingResolver();
            // A chord across the upper orbit avoids the body; there is no future PvP/FF policy here.
            yield return InSimulation(() => projectile = SpawnProjectile(Character.Id,
                new Vector2(-3f, 2f), Vector2.right, resolver));
            yield return WaitSimulationSeconds(0.5f);
            Assert.That(projectile != null && projectile.IsValid, Is.True);
            Assert.That(projectile.transform.position.x, Is.GreaterThan(2.5f));
            Assert.That(ActiveMask, Is.EqualTo(255u));
            Assert.That(resolver.Requests, Is.Empty);
        }

        [UnityTest]
        public IEnumerator OtherPlayerAndEnemyOnlyProjectiles_PassThroughForeignOrbs()
        {
            yield return Begin();
            PlayerCharacter other = SpawnAvatar(Vector3.right * 10f, false).GetComponent<PlayerCharacter>();
            yield return Press();
            var resolver = new RecordingResolver();
            NetworkObject friendly = null;
            NetworkObject pve = null;
            yield return InSimulation(() =>
            {
                friendly = SpawnProjectile(other.Id, new Vector2(-3f, 2f), Vector2.right, resolver);
                pve = SpawnProjectile(other.Id, new Vector2(-3f, 2f), Vector2.right, resolver, enemyOnly: true);
            });
            yield return WaitSimulationSeconds(0.5f);
            Assert.That(friendly != null && friendly.IsValid && friendly.transform.position.x > 2.5f, Is.True);
            Assert.That(pve != null && pve.IsValid && pve.transform.position.x > 2.5f, Is.True);
            Assert.That(ActiveMask, Is.EqualTo(255u));
            Assert.That(resolver.Requests, Is.Empty);
        }

        [UnityTest]
        public IEnumerator TwoSameTickProjectiles_ConsumedSlotDoesNotBlockLaterBodyHit()
        {
            yield return Begin();
            EnemyCharacter enemy = SpawnEnemy(Vector3.right * 10f);
            yield return Press();
            float health = Character.Health;
            var resolver = new RecordingResolver { Inner = Character.GetComponent<DamageResolver>() };
            yield return InSimulation(() =>
            {
                Vector2 orbPosition = Orb(0).transform.position;
                Vector2 direction = ((Vector2)DamageHitbox(Character).bounds.center - orbPosition).normalized;
                Vector2 origin = orbPosition - direction * 0.2f;
                NetworkObject first = SpawnProjectile(enemy.Id, origin, direction, resolver);
                NetworkObject second = SpawnProjectile(enemy.Id, origin, direction, resolver);
                // Explicitly order two forward simulation impacts in the same tick.
                first.GetComponent<NetworkProjectile>().FixedUpdateNetwork();
                Assert.That(ActiveMask, Is.EqualTo(254u));
                second.GetComponent<NetworkProjectile>().FixedUpdateNetwork();
            });
            yield return WaitUntil(() => Character.Health < health);
            Assert.That(ActiveMask, Is.EqualTo(254u));
            Assert.That(resolver.Requests, Has.Count.EqualTo(1), "The second projectile must pass through the vanished slot.");
            Assert.That(resolver.Requests[0].TargetId, Is.EqualTo(Character.Id));
        }

        [UnityTest]
        public IEnumerator ActualMeleeOverlap_SelectsOneOrbAndDoesNotResolveBodyDamageOrAssist()
        {
            yield return Begin();
            EnemyCharacter enemy = SpawnEnemy(Vector3.right * 10f);
            yield return Press();
            float health = Character.Health;
            var resolver = new RecordingResolver { Inner = Character.GetComponent<DamageResolver>() };
            MeleeAttack melee = MakeMelee(enemy, resolver, 0.2f);
            yield return InSimulation(() =>
            {
                Vector2 origin = (Vector2)Orb(0).transform.position + Vector2.right * 0.4f;
                Assert.That(melee.Execute(new AttackRequest(enemy.Id, origin, Vector2.left, _runner.Tick)).WasExecuted, Is.True);
            });
            Assert.That(ActiveMask, Is.EqualTo(254u));
            Assert.That(Character.Health, Is.EqualTo(health));
            Assert.That(resolver.Requests, Is.Empty);
            AssertNoContributors(enemy);
        }

        [UnityTest]
        public IEnumerator BodyOnlyMeleeOverlap_DamagesNormallyWithoutConsumingDistantOrbs()
        {
            yield return Begin();
            EnemyCharacter enemy = SpawnEnemy(Vector3.right * 10f);
            yield return Press();
            float health = Character.Health;
            var resolver = new RecordingResolver { Inner = Character.GetComponent<DamageResolver>() };
            MeleeAttack melee = MakeMelee(enemy, resolver, 0.2f);
            yield return InSimulation(() =>
            {
                Collider2D body = DamageHitbox(Character);
                Vector2 origin = (Vector2)body.bounds.center + Vector2.right * 0.4f;
                melee.Execute(new AttackRequest(enemy.Id, origin, Vector2.left, _runner.Tick));
            });
            Assert.That(Character.Health, Is.LessThan(health));
            Assert.That(ActiveMask, Is.EqualTo(255u), "There is no whole-player absorption hook.");
            Assert.That(resolver.Requests, Has.Count.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator Query_DeduplicatesMultipleOrbsByOwnerAndKeepsNearestActualCollisionReference()
        {
            yield return Begin(count: 2);
            EnemyCharacter enemy = SpawnEnemy(Vector3.right * 10f);
            yield return Press();
            yield return InSimulation(() =>
            {
                Vector2 origin = Vector2.right * 3f;
                var query = new AttackTargetQuery(enemy.Id, origin, Vector2.left, 2.9f, 2.2f, 1,
                    LayerMask.GetMask("Character"));
                var results = _runtime.GetComponent<Physics2DAttackTargetQuery>().FindTargets(query);
                Assert.That(results, Has.Count.EqualTo(1));
                Assert.That(results[0].TargetId, Is.EqualTo(Character.Id));
                PropertyInfo property = typeof(AttackTarget).GetProperty("ProtectiveOrb");
                Assert.That(property, Is.Not.Null, "AttackTarget must carry the selected local interception collider.");
                Assert.That(property.GetValue(results[0]), Is.SameAs(Orb(0)));
            });
        }

        [UnityTest]
        public IEnumerator SameEnemyContactAcrossTwoSlots_ConsumesBothIndependentlyAndAttributesRealDamageToCaster()
        {
            yield return Begin();
            EnemyCharacter enemy = SpawnEnemy(Vector3.right * 10f);
            yield return Press();
            float health = enemy.Health;
            yield return InSimulation(() =>
            {
                MoveDamageHitboxTo(enemy, Orb(0).transform.position);
                _effect.FixedUpdateNetwork();
                Assert.That(ActiveMask, Is.EqualTo(254u));
                MoveDamageHitboxTo(enemy, Orb(4).transform.position);
                _effect.FixedUpdateNetwork();
                Assert.That(ActiveMask, Is.EqualTo(238u));
                _effect.FixedUpdateNetwork();
                MoveDamageHitboxTo(enemy, Vector2.right * 10f);
            });
            Assert.That(_contactResolver.Requests, Has.Count.EqualTo(2));
            Assert.That(enemy.Health, Is.EqualTo(health - 6f).Within(0.001f));
            foreach (DamageRequest request in _contactResolver.Requests)
            {
                Assert.That(request.AttackerId, Is.EqualTo(Character.Id));
                Assert.That(request.TargetId, Is.EqualTo(enemy.Id));
                Assert.That(request.DamageType, Is.EqualTo(DamageType.Magical));
            }
            var contributors = new HashSet<RaidParticipantId>();
            enemy.GetComponent<CombatContributionTracker>().GetValidContributors(_runner.Tick.Raw, contributors);
            RaidParticipantId.TryCreate(1, out var casterParticipant);
            Assert.That(contributors, Does.Contain(casterParticipant), "Actual contact damage uses existing caster contribution attribution.");
        }

        [UnityTest]
        public IEnumerator Contact_TiedEnemiesSelectLowerEntityIdAndConsumeTheSlotOnlyOnce()
        {
            yield return Begin();
            EnemyCharacter first = SpawnEnemy(Vector3.right * 10f);
            EnemyCharacter second = SpawnEnemy(Vector3.right * 12f);
            yield return Press();
            EntityId expected = first.Id.Value < second.Id.Value ? first.Id : second.Id;
            yield return InSimulation(() =>
            {
                Vector2 position = Orb(0).transform.position;
                MoveDamageHitboxTo(first, position);
                MoveDamageHitboxTo(second, position);
                _effect.FixedUpdateNetwork();
                _effect.FixedUpdateNetwork();
                MoveDamageHitboxTo(first, Vector2.right * 10f);
                MoveDamageHitboxTo(second, Vector2.right * 12f);
            });
            Assert.That(ActiveMask, Is.EqualTo(254u));
            Assert.That(_contactResolver.Requests, Has.Count.EqualTo(1));
            Assert.That(_contactResolver.Requests[0].TargetId, Is.EqualTo(expected));
        }

        [UnityTest]
        public IEnumerator Contact_CommitsConsumptionBeforeResolverReentryAndCannotDamageTwice()
        {
            yield return Begin();
            EnemyCharacter enemy = SpawnEnemy(Vector3.right * 10f);
            yield return Press();
            float health = enemy.Health;
            int callbacks = 0;
            _contactResolver.BeforeResolve = () =>
            {
                callbacks++;
                Assert.That(ActiveMask & 1u, Is.Zero, "Consumption precedes irreversible damage.");
                Assert.That(Consume(_effect, 0, enemy.Id), Is.False);
                _effect.FixedUpdateNetwork();
            };
            yield return InSimulation(() =>
            {
                MoveDamageHitboxTo(enemy, Orb(0).transform.position);
                _effect.FixedUpdateNetwork();
                _effect.FixedUpdateNetwork();
                MoveDamageHitboxTo(enemy, Vector2.right * 10f);
            });
            Assert.That(callbacks, Is.EqualTo(1));
            Assert.That(_contactResolver.Requests, Has.Count.EqualTo(1));
            Assert.That(enemy.Health, Is.EqualTo(health - 3f).Within(0.001f));
            Assert.That(ActiveMask, Is.EqualTo(254u));
        }

        [UnityTest]
        public IEnumerator EnvironmentAndOtherPlayer_NeverConsumeEvenWhenOverlappingAnOrb()
        {
            yield return Begin();
            yield return Press();
            PlayerAbilityRuntimeNetworkController other = SpawnAvatar(Vector3.right * 10f, false);
            var wall = new GameObject("OrbEnvironment") { layer = LayerMask.NameToLayer("WorldCollision") };
            _localObjects.Add(wall);
            wall.transform.position = Orb(0).transform.position;
            wall.AddComponent<BoxCollider2D>().size = Vector2.one * 0.2f;
            float health = Character.Health;
            yield return InSimulation(() =>
            {
                MoveDamageHitboxTo(other.GetComponent<PlayerCharacter>(), Orb(4).transform.position);
                Physics2D.SyncTransforms();
                Assert.That(Consume(_effect, 0, default), Is.False);
                Assert.That(Consume(_effect, 0, new EntityId(int.MaxValue)), Is.False, "Unregistered attackers are not enemies.");
                Assert.That(Consume(_effect, 0, Character.Id), Is.False);
                Assert.That(Consume(_effect, 0, other.GetComponent<PlayerCharacter>().Id), Is.False);
                _effect.FixedUpdateNetwork();
                DamageResult result = Character.GetComponent<DamageResolver>().Resolve(new DamageRequest(default,
                    Character.Id, 7f, DamageType.TrueDamage, Vector2.down, Character.transform.position, _runner.Tick));
                Assert.That(result.IsApplied, Is.True);
            });
            Assert.That(Character.Health, Is.EqualTo(health - 7f));
            Assert.That(ActiveMask, Is.EqualTo(255u));
            Assert.That(_contactResolver.Requests, Is.Empty);
        }

        [UnityTest]
        public IEnumerator CrowdControlAndRealDowned_PreserveOrbsButDefinitiveDefeatClearsThem()
        {
            yield return Begin();
            yield return Press();
            yield return InSimulation(() =>
            {
                _runtime.InterruptActiveExecutions(AbilityExecutionStopReason.Stun);
                _runtime.GetComponent<PlayerMovementNetworkController>().TryApplyKnockback(Vector2.up, 1f, true);
                _runtime.Object.AssignInputAuthority(PlayerRef.None);
                Assert.That(Character.ApplyDamage(new DamageRequest(default, Character.Id, 10000f,
                    DamageType.TrueDamage, Vector2.down, Character.transform.position, _runner.Tick)).IsApplied, Is.True);
            });
            yield return WaitTicks();
            Assert.That(Character.Health, Is.Zero);
            Assert.That(Character.IsDowned && Character.IsAlive, Is.True);
            Assert.That(ActiveMask, Is.EqualTo(255u));
            Assert.That(ActiveOrbs(), Has.Count.EqualTo(8));
            yield return InSimulation(() => Character.ApplyDamage(new DamageRequest(default, Character.Id, 10000f,
                DamageType.TrueDamage, Vector2.down, Character.transform.position, _runner.Tick)));
            yield return WaitUntil(() => ActiveMask == 0u);
            Assert.That(Character.IsAlive, Is.False);
            Assert.That(ActiveOrbs(), Is.Empty);
        }

        [UnityTest]
        public IEnumerator TimedEffect_ExpiresAtCopiedTickDeadlineNotBeforeIt()
        {
            yield return Begin(lifetime: 0.6f);
            yield return Press();
            TickTimer timer = Read<TickTimer>(_effect, "LifetimeTimer");
            Assert.That(timer.IsRunning, Is.True);
            Assert.That(ActiveMask, Is.EqualTo(255u));
            yield return WaitUntil(() => _runner.Tick.Raw >= (int)timer.TargetTick - 2);
            if (_runner.Tick.Raw < (int)timer.TargetTick)
                Assert.That(ActiveMask, Is.EqualTo(255u));
            yield return WaitUntil(() => ActiveMask == 0u);
            Assert.That(_runner.Tick.Raw, Is.GreaterThanOrEqualTo((int)timer.TargetTick));
            Assert.That(ActiveOrbs(), Is.Empty);
        }

        [UnityTest]
        public IEnumerator ZeroLifetime_IsPermanentAndLastSlotConsumptionEndsTheEffect()
        {
            yield return Begin(count: 1, lifetime: 0f);
            EnemyCharacter enemy = SpawnEnemy(Vector3.right * 10f);
            yield return Press();
            Assert.That(Read<TickTimer>(_effect, "LifetimeTimer").IsRunning, Is.False);
            yield return WaitSimulationSeconds(16f);
            Assert.That(ActiveMask, Is.EqualTo(1u), "Zero must not be interpreted as an already-expired timer.");
            yield return InSimulation(() =>
            {
                Assert.That(Consume(_effect, 0, enemy.Id), Is.True);
                Assert.That(Consume(_effect, 0, enemy.Id), Is.False);
            });
            Assert.That(Read<bool>(_effect, "IsActive"), Is.False);
            Assert.That(ActiveOrbs(), Is.Empty);
        }

        [UnityTest, Explicit("Requires configured Photon connectivity for a real Host + Client room; Single mode rejects connections.")]
        public IEnumerator HostAndClient_ProxyShowsReplicatedSlotsButCannotConsumeEvenWithARegisteredEnemy()
        {
            // Separate real peers, not two authority objects in Single mode. No credential files are read.
            NetworkProjectConfig config = CloneRunnerConfig(NetworkProjectConfig.Global);
            config.PeerMode = NetworkProjectConfig.PeerModes.Multiple;
            string room = $"orbs-proxy-{Guid.NewGuid():N}";
            NetworkSceneInfo scene = PeerSceneInfo();
            yield return Begin(mode: GameMode.Host, config: config, sessionName: room, scene: scene);
            EnemyCharacter enemy = SpawnEnemy(Vector3.right * 10f);
            yield return Press();

            var clientObject = new GameObject("ProtectiveOrbsProxyClient");
            _client = clientObject.AddComponent<NetworkRunner>();
            clientObject.AddComponent<EntityRegistry>();
            var clientDriver = clientObject.AddComponent<PlayerAbilityRuntimeSimulationDriver>();
            LogAssert.Expect(UnityEngine.LogType.Error,
                "PlayerExtractionProgressController requires character, extraction controller, " +
                "registry, assignment service, and valid receiver/reader registrations.");
            var start = _client.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Client, SessionName = room, Config = CloneRunnerConfig(config),
                EnableClientSessionCreation = false,
                Scene = scene,
                SceneManager = clientObject.AddComponent<PeerSceneManager>(),
                ObjectProvider = clientObject.AddComponent<NetworkObjectProviderDefault>()
            });
            _runnerStartups[_client] = start;
            while (!start.IsCompleted) yield return null;
            Assert.That(start.Result.Ok, Is.True, start.Result.ShutdownReason.ToString());
            yield return WaitUntil(() => _client != null && _client.IsRunning && !_client.SceneManager.IsBusy, 20f);
            Assert.That(_client.SceneInfo.SceneCount, Is.EqualTo(1));
            NetworkObject proxyObject = null;
            yield return WaitUntil(() => _client.TryFindObject(_runtime.Object.Id, out proxyObject) &&
                _client.TryFindObject(enemy.Object.Id, out _) &&
                Read<uint>(proxyObject.GetComponent(RequiredType(EffectTypeName)), "ActiveMask") == 255u, 20f);
            var proxy = (NetworkBehaviour)proxyObject.GetComponent(RequiredType(EffectTypeName));
            Assert.That(proxy.HasStateAuthority, Is.False);
            int visible = 0;
            foreach (Component orb in proxyObject.GetComponentsInChildren(RequiredType(OrbTypeName), true))
                if (orb.GetComponent<Collider2D>().enabled) visible++;
            Assert.That(visible, Is.EqualTo(8));
            int previous = clientDriver.CompletionSequence;
            clientDriver.RequestOperation(() =>
            {
                // Eligibility is true, so rejection cannot be explained away by an absent attacker or dead slot.
                var concrete = (ProtectiveOrbsNetworkEffect)proxy;
                Assert.That(concrete.CanConsumeEnemyImpact(0, enemy.Id), Is.True);
                Assert.That(Consume(proxy, 0, enemy.Id), Is.False);
                Assert.That(Read<uint>(proxy, "ActiveMask"), Is.EqualTo(255u));
            });
            yield return WaitUntil(() => clientDriver.CompletionSequence != previous);
            Assert.That(ActiveMask, Is.EqualTo(255u));
            yield return InSimulation(() => Assert.That(Consume(_effect, 0, enemy.Id), Is.True));
            yield return WaitUntil(() => Read<uint>(proxy, "ActiveMask") == 254u);
        }

        [UnityTest, Explicit("Requires configured Photon connectivity and genuine cloud Host Migration; select this test explicitly.")]
        public IEnumerator RealHostMigration_ResumeSnapshotPreservesConsumedOrbsAndContinuesOnPromotedHost()
        {
            // Targeted effect recovery, NOT the full Raid's MatchController/recovery-roster lifecycle.
            NetworkProjectConfig config = CloneRunnerConfig(NetworkProjectConfig.Global);
            config.PeerMode = NetworkProjectConfig.PeerModes.Multiple;
            Assert.That(config.HostMigration.EnableAutoUpdate, Is.True, "Host Migration must be enabled in the loaded config.");
            NetworkSceneInfo scene = PeerSceneInfo();
            string room = $"orbs-migration-{Guid.NewGuid():N}";
            yield return Begin(lifetime: 300f, mode: GameMode.Host, config: config, sessionName: room, scene: scene);
            EnemyCharacter enemy = SpawnEnemy(Vector3.right * 20f);
            NetworkId oldEnemyId = enemy.Object.Id;
            NetworkId oldAvatarId = _runtime.Object.Id;
            NetworkId oldParticipantId = _runtime.GetComponent<RaidAvatarParticipantLink>().ParticipantId;
            yield return Press();
            Assert.That(_runtime.TryGetExecutionSnapshot(UniversalAbilitySlot.Slot1, out var accepted), Is.True);
            yield return InSimulation(() =>
            {
                Assert.That(Consume(_effect, 2, enemy.Id), Is.True);
                Assert.That(Consume(_effect, 5, enemy.Id), Is.True);
            });
            const uint partialMask = 219u;
            Assert.That(ActiveMask, Is.EqualTo(partialMask));
            int startTick = Read<int>(_effect, "StartTick");
            TickTimer lifetime = Read<TickTimer>(_effect, "LifetimeTimer");
            Assert.That(lifetime.IsRunning, Is.True);

            var clientObject = new GameObject("ProtectiveOrbsMigrationClient");
            _localObjects.Add(clientObject);
            _client = clientObject.AddComponent<NetworkRunner>();
            clientObject.AddComponent<EntityRegistry>();
            var receipt = clientObject.AddComponent<MigrationTokenReceipt>();
            _client.AddCallbacks(receipt);
            LogAssert.Expect(UnityEngine.LogType.Error,
                "PlayerExtractionProgressController requires character, extraction controller, " +
                "registry, assignment service, and valid receiver/reader registrations.");
            var join = _client.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Client, SessionName = room, Config = CloneRunnerConfig(config),
                EnableClientSessionCreation = false,
                Scene = scene,
                SceneManager = clientObject.AddComponent<PeerSceneManager>(),
                ObjectProvider = clientObject.AddComponent<NetworkObjectProviderDefault>()
            });
            _runnerStartups[_client] = join;
            yield return WaitForCloudTask(join, "migration client join");
            Assert.That(join.Result.Ok, Is.True, join.Result.ShutdownReason.ToString());
            NetworkObject proxyObject = null;
            yield return WaitUntil(() => _client != null && _client.IsRunning && !_client.SceneManager.IsBusy &&
                _client.TryFindObject(oldAvatarId, out proxyObject) && _client.TryFindObject(oldEnemyId, out _) &&
                Read<uint>(proxyObject.GetComponent(RequiredType(EffectTypeName)), "ActiveMask") == partialMask, 60f);
            Assert.That(proxyObject.HasStateAuthority, Is.False);
            var push = _runner.PushHostMigrationSnapshot();
            yield return WaitForCloudTask(push, "cloud snapshot upload");
            Assert.That(push.Result, Is.True, "Photon must accept the actual partial-orb snapshot before source loss.");

            // HostMigration reason retains recovery intent rather than taking normal Return/room cleanup.
            // The only token accepted below comes from Photon/Fusion's real client callback.
            yield return ShutdownRunner(_runner, ShutdownReason.HostMigration);
            _runner = null;
            yield return WaitUntil(() => receipt.Token != null, 60f);
            HostMigrationToken token = receipt.Token;
            Assert.That(receipt.SourceRunner, Is.SameAs(_client));
            Assert.That(receipt.CallbackCount, Is.EqualTo(1));
            Assert.That(token.GameMode, Is.EqualTo(GameMode.Host), "The only surviving client must be promoted.");
            yield return ShutdownRunner(_client, ShutdownReason.HostMigration);
            _client = null;

            var replacementObject = new GameObject("ProtectiveOrbsMigrationResumeHost");
            _localObjects.Add(replacementObject);
            NetworkRunner replacementRunner = replacementObject.AddComponent<NetworkRunner>();
            _runner = replacementRunner;
            replacementObject.AddComponent<EntityRegistry>();
            _driver = replacementObject.AddComponent<PlayerAbilityRuntimeSimulationDriver>();
            var replacementScenes = replacementObject.AddComponent<PeerSceneManager>();
            // The direct callback reuses the existing object-scoped fresh-initialization guard, but does
            // not invoke the full restorer, which rightly requires a Raid MatchController/bootstrap.
            var restoreGuard = replacementObject.AddComponent<HostMigrationSnapshotRestorer>();
            restoreGuard.Initialize(replacementRunner, SessionStartupContext.HostMigrationResume, null);
            var restored = new Dictionary<NetworkId, NetworkObject>();
            int resumeCalls = 0;
            bool wasResumeInCallback = false;
            bool wasRunningInCallback = false;
            bool wasServerInCallback = false;
            GameMode gameModeInCallback = default;
            Exception resumeError = null;
            var resumeFinished = new TaskCompletionSource<bool>();
            _migrationResumeRunner = replacementRunner;
            _migrationResumeCompletion = resumeFinished.Task;
            var resume = replacementRunner.StartGame(new StartGameArgs
            {
                GameMode = token.GameMode, Config = CloneRunnerConfig(config),
                HostMigrationToken = token,
                Scene = scene,
                SceneManager = replacementScenes,
                ObjectProvider = replacementObject.AddComponent<NetworkObjectProviderDefault>(),
                HostMigrationResume = resumedRunner =>
                {
                    // Fusion can invoke this after StartGame completes. Never capture mutable fixture
                    // runner fields or let NUnit exceptions unwind its asynchronous scene postprocessor.
                    try
                    {
                        resumeCalls++;
                        Assert.That(resumeCalls, Is.EqualTo(1));
                        Assert.That(resumedRunner, Is.SameAs(replacementRunner));
                        wasResumeInCallback = resumedRunner.IsResume;
                        wasRunningInCallback = resumedRunner.IsRunning;
                        wasServerInCallback = resumedRunner.IsServer;
                        gameModeInCallback = resumedRunner.GameMode;
                        Assert.That(resumedRunner.IsResume && resumedRunner.IsRunning && resumedRunner.IsServer, Is.True);
                        Assert.That(gameModeInCallback, Is.EqualTo(GameMode.Host));
                        var snapshots = new List<NetworkObject>(resumedRunner.GetResumeSnapshotNetworkObjects());
                        Assert.That(snapshots, Has.Count.EqualTo(3), "This fixture snapshots only participant, avatar and enemy.");
                        // Restore participants first so the authored avatar Spawned paths can resolve statistics.
                        foreach (NetworkObject snapshot in snapshots)
                            if (snapshot.GetComponent<NetworkRaidParticipant>() != null)
                                RestoreMigrationObject(resumedRunner, restoreGuard, snapshot, restored);
                        foreach (NetworkObject snapshot in snapshots)
                            if (snapshot.GetComponent<NetworkRaidParticipant>() == null)
                                RestoreMigrationObject(resumedRunner, restoreGuard, snapshot, restored);
                        int sceneObjects = 0;
                        foreach (var pair in resumedRunner.GetResumeSnapshotNetworkSceneObjects())
                        {
                            pair.Item1.CopyStateFrom(pair.Item2);
                            sceneObjects++;
                        }
                        Assert.That(sceneObjects, Is.Zero, "The proven template scene has no scene NetworkObjects.");
                        Assert.That(restored.ContainsKey(oldAvatarId) && restored.ContainsKey(oldParticipantId) &&
                            restored.ContainsKey(oldEnemyId), Is.True);
                        var restoredRuntime = restored[oldAvatarId].GetComponent<PlayerAbilityRuntimeNetworkController>();
                        var restoredEffect = restoredRuntime.GetComponent<ProtectiveOrbsNetworkEffect>();
                        Assert.That(restoredEffect.Object, Is.SameAs(restoredRuntime.Object),
                            "The effect restores with its original avatar composition.");
                        Assert.That(restoredEffect.ActiveMask, Is.EqualTo(partialMask),
                            "Actual restored Spawned must not replenish consumed slots.");
                        Assert.That(restoredEffect.StartTick, Is.EqualTo(startTick));
                        Assert.That(restoredEffect.LifetimeTimer.TargetTick, Is.EqualTo(lifetime.TargetTick));
                    }
                    catch (Exception exception)
                    {
                        resumeError = exception;
                    }
                    finally
                    {
                        resumeFinished.TrySetResult(true);
                    }
                }
            });
            _runnerStartups[replacementRunner] = resume;
            yield return WaitForCloudTask(resume, "promoted host startup");
            Assert.That(resume.Result.Ok, Is.True, resume.Result.ShutdownReason.ToString());
            yield return WaitForCloudTask(resumeFinished.Task, "real resume callback completion");
            yield return WaitUntil(() => replacementRunner != null && replacementRunner.IsRunning &&
                !replacementScenes.HasPendingSceneWork && !replacementScenes.IsBusy, 60f);
            Assert.That(resumeError, Is.Null, $"Real resume callback failed: {resumeError}");
            Assert.That(resumeCalls, Is.EqualTo(1));
            // IsResume is recovery-phase evidence, not a promise that resume mode remains set afterward.
            Assert.That(wasResumeInCallback, Is.True, "The genuine Fusion callback must run in resume mode.");
            Assert.That(wasRunningInCallback && wasServerInCallback && gameModeInCallback == GameMode.Host, Is.True);
            Assert.That(replacementRunner.IsRunning && replacementRunner.IsServer && replacementRunner.GameMode == GameMode.Host, Is.True);
            _runtime = restored[oldAvatarId].GetComponent<PlayerAbilityRuntimeNetworkController>();
            _effect = _runtime.GetComponent<ProtectiveOrbsNetworkEffect>();
            _behaviour = _runtime.GetComponent<ProtectiveOrbsAbilityBehaviour>();
            yield return WaitUntil(() => _runtime.IsInitialized, 60f);
            Assert.That(_runtime.GetComponent<RaidAvatarParticipantLink>().TryResolveParticipant(out var participant), Is.True);
            Assert.That(participant.Object, Is.SameAs(restored[oldParticipantId]));
            Assert.That(participant.CurrentAvatarId, Is.EqualTo(_runtime.Object.Id));
            Assert.That(ActiveOrbs(), Has.Count.EqualTo(6));
            Assert.That(Orb(2).GetComponent<Collider2D>().enabled || Orb(5).GetComponent<Collider2D>().enabled, Is.False);
            EnemyCharacter recoveredEnemy = restored[oldEnemyId].GetComponent<EnemyCharacter>();
            Assert.That(_runner.GetComponent<EntityRegistry>().TryGetDamageable(recoveredEnemy.Id, out var registered), Is.True);
            Assert.That(registered, Is.SameAs(recoveredEnemy));
            yield return InSimulation(() =>
            {
                _effect.Spawned();
                _behaviour.Rebind(Context(), accepted);
                _behaviour.Begin(Context(), accepted);
                Assert.That(ActiveMask, Is.EqualTo(partialMask), "Rebind/Begin replay must not recreate consumed slots.");
                Assert.That(Read<int>(_effect, "StartTick"), Is.EqualTo(startTick));
                Assert.That(Read<TickTimer>(_effect, "LifetimeTimer").TargetTick, Is.EqualTo(lifetime.TargetTick));
                Assert.That(Consume(_effect, 2, recoveredEnemy.Id), Is.False);
                Assert.That(Consume(_effect, 5, recoveredEnemy.Id), Is.False);
                Assert.That(Consume(_effect, 1, recoveredEnemy.Id), Is.True);
                Assert.That(Consume(_effect, 1, recoveredEnemy.Id), Is.False);
            });
            Assert.That(ActiveMask, Is.EqualTo(217u));
            Assert.That(ActiveOrbs(), Has.Count.EqualTo(5));
        }

        [UnityTest]
        public IEnumerator ProjectileBehaviourCopyStateFrom_PreservesEnemyAffiliationAfterShooterDespawnAndSpawnedReplay()
        {
            yield return Begin();
            EnemyCharacter enemy = SpawnEnemy(Vector3.right * 10f);
            EntityId attacker = enemy.Id;
            yield return Press();
            var resolver = new RecordingResolver { Inner = Character.GetComponent<DamageResolver>() };
            NetworkObject restoredObject = null;
            int impacts = 0;
            yield return InSimulation(() =>
            {
                NetworkProjectile original = SpawnProjectile(attacker, Vector2.one * 50f, Vector2.left,
                    resolver, isUnbounded: true).GetComponent<NetworkProjectile>();
                Assert.That((bool)Read<NetworkBool>(original, "SourceWasPvEEnemy"), Is.True);
                _runner.Despawn(enemy.Object);
                Assert.That(_runner.GetComponent<EntityRegistry>().TryGetDamageable(attacker, out _), Is.False);

                restoredObject = SpawnProjectile(Character.Id, (Vector2)Orb(0).transform.position + Vector2.right,
                    Vector2.left, resolver, isUnbounded: true);
                NetworkProjectile restored = restoredObject.GetComponent<NetworkProjectile>();
                Assert.That((bool)Read<NetworkBool>(restored, "SourceWasPvEEnemy"), Is.False,
                    "EnemyOnly=false on ordinary player flight must not imply hostile affiliation.");
                // Real Fusion behaviour storage copy, not fake authority or a resume-context claim.
                restored.CopyStateFrom(original);
                restored.Spawned();
                restored.Spawned();
                Assert.That((bool)Read<NetworkBool>(restored, "SourceWasPvEEnemy"), Is.True,
                    "Spawned must not recalculate copied affiliation from a now-absent shooter.");
                Assert.That(Read<int>(restored, "OwnerEntityIdValue"), Is.EqualTo(attacker.Value));
                restored.SetRuntimeDependencies(resolver);
                restored.ImpactResolved += (_, geometry) =>
                {
                    impacts++;
                    Assert.That(geometry, Is.True);
                };
            });
            yield return WaitUntil(() => impacts > 0);
            Assert.That(impacts, Is.EqualTo(1));
            Assert.That(ActiveMask, Is.EqualTo(254u));
            Assert.That(restoredObject == null || !restoredObject.IsValid, Is.True);
            Assert.That(resolver.Requests, Is.Empty);
            Assert.That(_contactResolver.Requests, Is.Empty);
        }

        [UnityTest]
        public IEnumerator BehaviourCopyStateFrom_PreservesConsumedMaskStartDeadlineAndAllowsOnlyRemainingSlots()
        {
            yield return Begin();
            EnemyCharacter enemy = SpawnEnemy(Vector3.right * 20f);
            yield return Press();
            yield return InSimulation(() => Assert.That(Consume(_effect, 2, enemy.Id), Is.True));
            int start = Read<int>(_effect, "StartTick");
            TickTimer timer = Read<TickTimer>(_effect, "LifetimeTimer");
            PlayerAbilityRuntimeNetworkController target = SpawnAvatar(Vector3.up * 10f, false);
            var restored = (NetworkBehaviour)target.GetComponent(RequiredType(EffectTypeName));
            yield return InSimulation(() =>
            {
                // NetworkObject.CopyStateFrom requires equal NetworkIds (actual migration resume). This copies
                // the exact behaviour storage via Fusion's real API, as the existing resource fixtures do.
                restored.CopyStateFrom(_effect);
                // The production Spawned path performs no network initialization, on fresh OR restored storage.
                // Invoking that actual callback here is stronger than toggling a private test-only reset flag.
                // Single cannot supply Runner.IsResume; this is not a claim of a resume-context migration run.
                restored.Spawned();
                restored.Spawned();
                _runtime.TryGetExecutionSnapshot(UniversalAbilitySlot.Slot1, out var accepted);
                _runtime.TryGetSlot(UniversalAbilitySlot.Slot1, out var sourceSlot);
                var restoredBehaviour = target.GetComponent<ProtectiveOrbsAbilityBehaviour>();
                var restoredContext = new AbilityExecutionContext(_runner, target.GetComponent<PlayerCharacter>(),
                    UniversalAbilitySlot.Slot1, sourceSlot.Definition, _attributes, null, Vector2.right);
                restoredBehaviour.Rebind(restoredContext, accepted);
                restoredBehaviour.Begin(restoredContext, accepted);
                restored.FixedUpdateNetwork();
                Assert.That(Read<uint>(restored, "ActiveMask"), Is.EqualTo(251u));
                Assert.That(Read<int>(restored, "StartTick"), Is.EqualTo(start));
                Assert.That(Read<TickTimer>(restored, "LifetimeTimer").TargetTick, Is.EqualTo(timer.TargetTick));
                Assert.That(Consume(restored, 2, enemy.Id), Is.False);
                Assert.That(Consume(restored, 5, enemy.Id), Is.True);
            });
            Assert.That(Read<uint>(restored, "ActiveMask"), Is.EqualTo(219u));
            Assert.That(ActiveMask, Is.EqualTo(251u), "Restored writes cannot mutate the source mask.");
            Assert.That(Read<int>(restored, "InitialCount"), Is.EqualTo(8));
            Assert.That(Read<float>(restored, "OrbitRadius"), Is.EqualTo(2f));
            Assert.That(Read<float>(restored, "AngularSpeedDegreesPerSecond"), Is.Zero);
            Assert.That(Read<float>(restored, "ContactDamage"), Is.EqualTo(3f));
            Assert.That(Read<DamageType>(restored, "ContactDamageType"), Is.EqualTo(DamageType.Magical));
            Assert.That(target.GetComponentsInChildren(RequiredType(OrbTypeName), true), Has.Length.EqualTo(32),
                "All original local slots are rebuilt once; consumed indices are retained, not compacted.");
        }

        // ---- Fixture: scene-free Single; real isolated template scene for Host + Client ----
        private enum ShooterState { Live, Dead, Despawned }

        private IEnumerator VerifyIndependentEnemyProjectile(ShooterState shooterState)
        {
            yield return Begin();
            EnemyCharacter enemy = SpawnEnemy(Vector3.right * 10f);
            EntityId attacker = enemy.Id;
            NetworkObject shooterObject = enemy.Object;
            EntityRegistry registry = _runner.GetComponent<EntityRegistry>();
            yield return Press();
            float health = Character.Health;
            var resolver = new RecordingResolver { Inner = Character.GetComponent<DamageResolver>() };
            NetworkObject projectile = null;
            int impactCount = 0;
            bool geometryOnly = false;
            Vector2 orbPosition = Orb(0).transform.position;
            yield return InSimulation(() =>
            {
                Assert.That(registry.TryGetDamageable(attacker, out IDamageable source), Is.True);
                Assert.That(source, Is.InstanceOf<IAbilityEnemyTarget>());
                Assert.That(enemy.IsAlive, Is.True, "Affiliation is captured while the enemy is still alive and registered.");
                projectile = SpawnProjectile(attacker, orbPosition + Vector2.right, Vector2.left, resolver,
                    isUnbounded: true);
                NetworkProjectile flight = projectile.GetComponent<NetworkProjectile>();
                flight.ImpactResolved += (_, geometry) => { impactCount++; geometryOnly = geometry; };
                Assert.That((bool)Read<NetworkBool>(flight, "IsUnbounded"), Is.True);
                Assert.That((bool)Read<NetworkBool>(flight, "EnemyOnly"), Is.False);
                Assert.That(ActiveMask, Is.EqualTo(255u), "The projectile has not reached a slot yet.");

                // Source loss happens AFTER InitializeNetworkState, before the first flight step.
                if (shooterState == ShooterState.Dead)
                {
                    DamageResult death = enemy.ApplyDamage(new DamageRequest(default, attacker, 10000f,
                        DamageType.TrueDamage, Vector2.down, enemy.transform.position, _runner.Tick));
                    Assert.That(death.IsApplied, Is.True);
                    Assert.That(enemy.IsAlive, Is.False);
                    Assert.That(shooterObject.IsValid, Is.True, "Death retains the original enemy/corpse entity.");
                    Assert.That(registry.TryGetDamageable(attacker, out _), Is.True);
                }
                else if (shooterState == ShooterState.Despawned)
                {
                    _runner.Despawn(shooterObject);
                    Assert.That(shooterObject == null || !shooterObject.IsValid, Is.True);
                    Assert.That(registry.TryGetDamageable(attacker, out _), Is.False);
                }
                if (shooterState != ShooterState.Live)
                {
                    Assert.That(((ProtectiveOrbsNetworkEffect)_effect).CanConsumeEnemyImpact(0, attacker), Is.False,
                        "Melee/contact must retain their live-source eligibility predicate.");
                    Assert.That(Consume(_effect, 0, attacker), Is.False);
                    Assert.That(ActiveMask, Is.EqualTo(255u));
                }
                Assert.That(projectile != null && projectile.IsValid, Is.True, "Flight is independent of its shooter.");
                Assert.That(Read<int>(flight, "OwnerEntityIdValue"), Is.EqualTo(attacker.Value),
                    "Removing the shooter must not erase the projectile's committed attribution.");
            });
            // Fail on the missing slot consumption, rather than timing out if rejected orb geometry is passed.
            yield return WaitUntil(() => impactCount > 0 || (projectile != null && projectile.IsValid &&
                projectile.transform.position.x < orbPosition.x - 0.5f));
            Assert.That(ActiveMask, Is.EqualTo(254u), "Independent enemy flight must consume exactly its collided original slot.");
            Assert.That(impactCount, Is.EqualTo(1));
            Assert.That(projectile == null || !projectile.IsValid, Is.True);
            Assert.That(Character.Health, Is.EqualTo(health));
            Assert.That(resolver.Requests, Is.Empty, "Blocking must not enter Resolve, feedback or Assist contribution paths.");
            Assert.That(_contactResolver.Requests, Is.Empty);
            Assert.That(geometryOnly, Is.True, "An intercepted projectile reports a non-damaging geometry impact.");
            if (shooterState != ShooterState.Despawned) AssertNoContributors(enemy);
            yield return WaitTicks();
            Assert.That(impactCount, Is.EqualTo(1));
            Assert.That(ActiveMask, Is.EqualTo(254u));
        }

        private static NetworkObject RestoreMigrationObject(NetworkRunner runner, HostMigrationSnapshotRestorer guard,
            NetworkObject snapshot, Dictionary<NetworkId, NetworkObject> restored)
        {
            Assert.That(snapshot != null && snapshot.Id.IsValid, Is.True);
            NetworkId oldId = snapshot.Id;
            bool hasTransform = snapshot.TryGetBehaviour<NetworkTRSP>(out var trsp);
            NetworkObject instance;
            if (snapshot.GetComponent<PlayerCharacter>() != null)
                LogAssert.Expect(UnityEngine.LogType.Error,
                    "PlayerExtractionProgressController requires character, extraction controller, " +
                    "registry, assignment service, and valid receiver/reader registrations.");
            try
            {
                instance = runner.Spawn(snapshot, hasTransform ? trsp.Data.Position : Vector3.zero,
                    hasTransform ? trsp.Data.Rotation : Quaternion.identity, onBeforeSpawned: (_, spawned) =>
                    {
                        // Same object-scoped guard used by production restoration, on a REAL IsResume runner.
                        SetField(guard, "_objectBeingRestored", spawned);
                        spawned.CopyStateFrom(snapshot);
                        spawned.AssignInputAuthority(PlayerRef.None);
                        if (spawned.GetComponent<RaidAvatarParticipantLink>() != null)
                        {
                            var behaviour = spawned.GetComponent<ProtectiveOrbsAbilityBehaviour>();
                            SetField(spawned.GetComponent<PlayerAbilityRuntimeNetworkController>(), "_executionBehaviours",
                                new AbilityExecutionBehaviour[] { behaviour });
                        }
                    });
            }
            finally
            {
                SetField(guard, "_objectBeingRestored", null);
            }
            Assert.That(instance != null && instance.IsValid && instance.Runner == runner, Is.True);
            // Match the original fixture after Spawned has reconstructed each AI's local state.
            foreach (NetworkBehaviour component in instance.GetComponents<NetworkBehaviour>())
                if (component is EnemyFSM || component is EnemyMovementAIController || component is EnemyCombatAIController)
                    component.enabled = false;
            restored.Add(oldId, instance);
            if (instance.TryGetBehaviour<RaidAvatarParticipantLink>(out var link))
            {
                Assert.That(restored.TryGetValue(link.ParticipantId, out NetworkObject participantObject), Is.True);
                link.SetRestoredParticipant(participantObject.Id);
                participantObject.GetComponent<NetworkRaidParticipant>().SetRestoredCurrentAvatar(instance.Id);
            }
            ((HashSet<NetworkObject>)ReadField(guard, "_allRestoredObjects")).Add(instance);
            return instance;
        }

        private sealed class MigrationTokenReceipt : NetworkRunnerCallbacksAdapter
        {
            public HostMigrationToken Token { get; private set; }
            public NetworkRunner SourceRunner { get; private set; }
            public int CallbackCount { get; private set; }
            public override void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
            {
                CallbackCount++;
                SourceRunner = runner;
                Token = hostMigrationToken;
            }
        }

        private static IEnumerator WaitForCloudTask(Task task, string operation)
        {
            float deadline = Time.realtimeSinceStartup + 60f;
            while (!task.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(task.IsCompleted, Is.True, $"Timed out after 60 seconds: {operation}.");
            task.GetAwaiter().GetResult();
        }

        private IEnumerator ShutdownRunner(NetworkRunner runner, ShutdownReason reason = ShutdownReason.Ok)
        {
            // Failed StartGame may already have destroyed its runner. Capture the GameObject before
            // shutdown, opt out of automatic destruction, then use Unity's lifetime check afterward.
            if (runner == null) yield break;
            GameObject runnerObject = runner.gameObject;
            try
            {
                // StartGame completion is NOT resume completion. Drain both, then the full scene
                // coroutine, before Shutdown clears its manager's Runner and releases Fusion storage.
                _runnerStartups.TryGetValue(runner, out var startup);
                if (startup != null && !startup.IsCompleted)
                    yield return WaitForCloudTask(startup, "startup drain before fixture shutdown");
                if (runner == _migrationResumeRunner && _migrationResumeCompletion != null &&
                    startup != null && startup.Status == TaskStatus.RanToCompletion && startup.Result.Ok)
                    yield return WaitForCloudTask(_migrationResumeCompletion, "resume drain before fixture shutdown");
                var scenes = runner != null ? runner.GetComponent<PeerSceneManager>() : null;
                if (scenes != null)
                    yield return WaitUntil(() => scenes == null || !scenes.HasPendingSceneWork, 60f);
                if (runner != null && runner.IsRunning)
                    yield return WaitForCloudTask(runner.Shutdown(destroyGameObject: false, shutdownReason: reason),
                        "fixture runner shutdown");
            }
            finally
            {
                if (runnerObject != null) Object.Destroy(runnerObject);
            }
        }

        private static NetworkSceneInfo PeerSceneInfo()
        {
            var scene = new NetworkSceneInfo();
            scene.AddSceneRef(SceneRef.FromPath(PeerScenePath), LoadSceneMode.Additive, LocalPhysicsMode.None, true);
            return scene;
        }

        private sealed class PeerSceneManager : NetworkSceneManagerDefault
        {
            private int _activeSceneLoads;
            public bool HasPendingSceneWork => _activeSceneLoads != 0;

            protected override IEnumerator LoadSceneCoroutine(SceneRef sceneRef, NetworkLoadSceneParameters sceneParams)
            {
                _activeSceneLoads++;
                try
                {
                    Assert.That(sceneRef.IsPath(PeerScenePath), Is.True);
                    Runner.InvokeSceneLoadStart(sceneRef);
                    Scene loadedScene;
                    using (MakeLoadingScope())
                    {
                        // This shipped scene has only a camera/light, no gameplay/bootstrap/auth services.
                        // Editor loading needs no BuildSettings edits. The base postprocessor still merges
                        // the REAL scene into Fusion's isolated peer scene and registers scene objects.
                        yield return EditorSceneManager.LoadSceneAsyncInPlayMode(PeerScenePath,
                            new LoadSceneParameters(LoadSceneMode.Additive, LocalPhysicsMode.None));
                        loadedScene = SceneManager.GetSceneByPath(PeerScenePath);
                        Assert.That(loadedScene.IsValid() && loadedScene.isLoaded, Is.True);
                        MarkSceneAsOwned(sceneRef, loadedScene);
                        foreach (GameObject root in loadedScene.GetRootGameObjects()) root.SetActive(false);
                    }
                    // Keep separate tracking through postprocessing/resume. Extending IsBusy's loading
                    // scope here would block Runner.Spawn inside the actual migration callback.
                    yield return base.OnSceneLoaded(sceneRef, loadedScene, sceneParams);
                }
                finally
                {
                    _activeSceneLoads--;
                }
            }
        }

        private static NetworkProjectConfig CloneRunnerConfig(NetworkProjectConfig source)
        {
            // The installed Fusion DLL does not expose the Copy method listed in its XML publicly.
            // Only PeerMode (a value field) is changed by this fixture. A CLR shallow clone isolates that
            // field while preserving the loaded prefab table and execution-order metadata; JSON can lose them.
            MethodInfo clone = typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic);
            return (NetworkProjectConfig)clone.Invoke(source, null);
        }

        private IEnumerator Begin(int count = 8, float speed = 0f, float lifetime = 15f,
            GameMode mode = GameMode.Single, NetworkProjectConfig config = null, string sessionName = null,
            NetworkSceneInfo? scene = null)
        {
            _participantSequence = 0;
            RequiredType("ProtectiveOrbsAbilityBehaviour");
            RequiredType(EffectTypeName);
            RequiredType(OrbTypeName);
            var runnerObject = new GameObject("ProtectiveOrbsTestRunner");
            _runner = runnerObject.AddComponent<NetworkRunner>();
            PlayModeRunnerComposition.AddLocalInputContext(_runner);
            runnerObject.AddComponent<EntityRegistry>();
            _driver = runnerObject.AddComponent<PlayerAbilityRuntimeSimulationDriver>();
            _input = runnerObject.AddComponent<OrbInputDriver>();
            _runner.AddCallbacks(_input);
            _runner.ProvideInput = true;
            var start = _runner.StartGame(new StartGameArgs
            {
                GameMode = mode,
                Config = config,
                SessionName = sessionName ?? $"ability-orbs-{Guid.NewGuid():N}",
                Scene = scene,
                SceneManager = scene.HasValue ? runnerObject.AddComponent<PeerSceneManager>() :
                    runnerObject.AddComponent<NetworkSceneManagerDefault>(),
                ObjectProvider = runnerObject.AddComponent<NetworkObjectProviderDefault>()
            });
            _runnerStartups[_runner] = start;
            yield return WaitForCloudTask(start, "fixture host startup");
            Assert.That(start.Result.Ok, Is.True, start.Result.ShutdownReason.ToString());
            if (scene.HasValue)
            {
                yield return WaitUntil(() => _runner != null && _runner.IsRunning && !_runner.SceneManager.IsBusy, 20f);
                Assert.That(_runner.SceneInfo.SceneCount, Is.EqualTo(1));
            }
            Assert.That(CharacterAttributeState.TryCreate(20, 20, 20, 20, 20, 20, 0, out _attributes), Is.True);
            _runtime = SpawnAvatar(Vector3.zero, true, count, speed, lifetime);
            _effect = (NetworkBehaviour)_runtime.GetComponent(RequiredType(EffectTypeName));
            _behaviour = (AbilityExecutionBehaviour)_runtime.GetComponent(RequiredType("ProtectiveOrbsAbilityBehaviour"));
            yield return WaitUntil(() => _runtime.IsInitialized && _runtime.GetComponent<PlayerManaNetworkController>().IsInitialized);
        }

        private PlayerAbilityRuntimeNetworkController SpawnAvatar(Vector3 position, bool inputAuthority,
            int count = 8, float speed = 0f, float lifetime = 15f)
        {
            Assert.That(RaidParticipantId.TryCreate(++_participantSequence, out var id), Is.True);
            var participant = _runner.Spawn(LoadPrefab(ParticipantGuid), onBeforeSpawned: (_, instance) =>
                instance.GetComponent<NetworkRaidParticipant>().Initialize($"orb-player-{id.Value}", id, _attributes,
                    ExperienceCurve.InitialLevel, 0, "orb-test",
                    preparedAbilities: new PreparedAbilityLoadout(new AbilityId("protective_orbs"), default)))
                .GetComponent<NetworkRaidParticipant>();
            NetworkObject avatar = _runner.Spawn(LoadPrefab(PlayerGuid), position, Quaternion.identity,
                inputAuthority ? _runner.LocalPlayer : PlayerRef.None, onBeforeSpawned: (_, instance) =>
                {
                    instance.GetComponent<RaidAvatarParticipantLink>().Initialize(participant.Object);
                    var runtime = instance.GetComponent<PlayerAbilityRuntimeNetworkController>();
                    var behaviour = instance.GetComponent(RequiredType("ProtectiveOrbsAbilityBehaviour")) as AbilityExecutionBehaviour;
                    var effect = instance.GetComponent(RequiredType(EffectTypeName)) as NetworkBehaviour;
                    Assert.That(behaviour, Is.Not.Null, "NetworkPlayer must compose the instant casting behaviour.");
                    Assert.That(effect, Is.Not.Null, "NetworkPlayer must bake the effect on the same NetworkObject root.");
                    Assert.That(ReadField(behaviour, "_effect"), Is.SameAs(effect));
                    Assert.That(ReadField(effect, "_orbPrefab"), Is.Not.Null, "The parent must assign the local collider/presentation prefab.");
                    SetField(behaviour, "_orbCount", count);
                    SetField(behaviour, "_orbitRadius", 2f);
                    SetField(behaviour, "_angularSpeedDegreesPerSecond", speed);
                    SetField(behaviour, "_lifetimeSeconds", lifetime);
                    SetField(behaviour, "_contactDamage", 3f);
                    SetField(behaviour, "_damageType", DamageType.Magical);
                    SetField(runtime, "_executionBehaviours", new[] { behaviour });
                    if (inputAuthority)
                    {
                        // Narrow observation seam: still delegates all real damage to the existing resolver.
                        var source = instance.gameObject.AddComponent<RecordingResolverComponent>();
                        source.Recorder.Inner = instance.GetComponent<DamageResolver>();
                        _contactResolver = source.Recorder;
                        SetField(effect, "_damageResolverSource", source);
                    }
                });
            ExtractionProgressTestAssertions.AssertRegistered(_runner, avatar);
            Assert.That(participant.TrySetCurrentAvatar(avatar), Is.True);
            return avatar.GetComponent<PlayerAbilityRuntimeNetworkController>();
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

        private NetworkObject SpawnProjectile(EntityId attacker, Vector2 origin, Vector2 direction, IDamageResolver resolver,
            bool enemyOnly = false, bool isUnbounded = false)
        {
            var request = new ProjectileSpawnRequest(attacker, origin, direction, 5f, DamageType.Physical,
                12f, isUnbounded ? 0f : 3f, isUnbounded ? 0f : 20f, _runner.Tick,
                projectilePrefab: new NetworkPrefabRef(ProjectileGuid),
                impactLayerMask: LayerMask.GetMask("Character", "WorldCollision", "Obstacles"),
                isUnbounded: isUnbounded, enemyOnly: enemyOnly);
            NetworkObject spawned = _runner.Spawn(LoadPrefab(ProjectileGuid), origin, Quaternion.identity,
                onBeforeSpawned: (_, instance) =>
                    instance.GetComponent<NetworkProjectile>().InitializeNetworkState(request, request.ImpactLayerMask));
            // Unbounded Spawned() selects the projectile-local resolver. Install the recording/delegating
            // observation seam afterward so all three shooter-lifecycle cases observe real Resolve calls.
            spawned.GetComponent<NetworkProjectile>().SetRuntimeDependencies(resolver);
            return spawned;
        }

        private MeleeAttack MakeMelee(EnemyCharacter enemy, IDamageResolver resolver, float radius)
        {
            var config = ScriptableObject.CreateInstance<MeleeAttackConfig>();
            _localObjects.Add(config);
            SetField(config, "_radius", radius);
            SetField(config, "_maximumTargets", 1);
            SetField(config, "_targetLayerMask", new LayerMask { value = LayerMask.GetMask("Character") });
            MeleeAttack melee = enemy.GetComponent<MeleeAttack>();
            Assert.That(melee, Is.Not.Null);
            melee.Initialize(config, new AttackExecutionParameters(5f, DamageType.Physical, 0f, 0.6f, 0f),
                _runtime.GetComponent<Physics2DAttackTargetQuery>(), resolver);
            return melee;
        }

        private static Collider2D DamageHitbox(CharacterBase character) =>
            ((Collider2D[])ReadField(character, "_damageHitboxes", typeof(CharacterBase)))[0];

        private static void MoveDamageHitboxTo(CharacterBase character, Vector2 point)
        {
            Collider2D hitbox = DamageHitbox(character);
            character.transform.position += (Vector3)(point - (Vector2)hitbox.bounds.center);
            Physics2D.SyncTransforms();
        }

        private void AssertNoContributors(EnemyCharacter enemy)
        {
            var contributors = new HashSet<RaidParticipantId>();
            enemy.GetComponent<CombatContributionTracker>().GetValidContributors(_runner.Tick.Raw, contributors);
            Assert.That(contributors, Is.Empty);
        }

        private Component Orb(int slot)
        {
            foreach (Component orb in _runtime.GetComponentsInChildren(RequiredType(OrbTypeName), true))
                if (Read<int>(orb, "SlotIndex") == slot) return orb;
            Assert.Fail($"Original orb slot {slot} was not pooled.");
            return null;
        }

        private List<Component> ActiveOrbs()
        {
            var results = new List<Component>();
            foreach (Component orb in _runtime.GetComponentsInChildren(RequiredType(OrbTypeName), true))
                if (orb.gameObject.activeInHierarchy && orb.GetComponent<Collider2D>().enabled) results.Add(orb);
            return results;
        }

        private static bool Consume(NetworkBehaviour effect, int slot, EntityId attacker)
        {
            MethodInfo method = effect.GetType().GetMethod("TryConsumeEnemyImpact", Members, null,
                new[] { typeof(int), typeof(EntityId) }, null);
            Assert.That(method, Is.Not.Null);
            return (bool)method.Invoke(effect, new object[] { slot, attacker });
        }

        private AbilityExecutionContext Context()
        {
            _runtime.TryGetSlot(UniversalAbilitySlot.Slot1, out var slot);
            return new AbilityExecutionContext(_runner, Character, UniversalAbilitySlot.Slot1, slot.Definition,
                _attributes, null, Vector2.right); // Self requires no area finder.
        }

        private PlayerCharacter Character => _runtime.GetComponent<PlayerCharacter>();
        private uint ActiveMask => Read<uint>(_effect, "ActiveMask");
        private float Mana() => _runtime.GetComponent<PlayerManaNetworkController>().CurrentMana;
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

        private IEnumerator WaitTicks() => WaitSimulationSeconds(_runner.DeltaTime * 3f);
        private IEnumerator WaitSimulationSeconds(float seconds)
        {
            int until = _runner.Tick.Raw + Mathf.CeilToInt(seconds / _runner.DeltaTime);
            yield return WaitUntil(() => _runner.Tick.Raw >= until, Mathf.Max(8f, seconds * 3f));
        }

        private IEnumerator InSimulation(Action action)
        {
            int previous = _driver.CompletionSequence;
            _driver.RequestOperation(action);
            yield return WaitUntil(() => _driver.CompletionSequence != previous);
        }

        private static IEnumerator WaitUntil(Func<bool> predicate, float timeout = 8f)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!predicate() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(predicate(), Is.True, "Expected authoritative orb state was not reached.");
        }

        private static Type RequiredType(string name)
        {
            Type type = typeof(AbilityExecutionBehaviour).Assembly.GetType(name);
            Assert.That(type, Is.Not.Null, $"TASK203 requires {name}.");
            return type;
        }

        private static T Read<T>(object target, string name)
        {
            PropertyInfo property = target.GetType().GetProperty(name, Members);
            Assert.That(property, Is.Not.Null, $"Missing observable state {name}.");
            return (T)property.GetValue(target);
        }

        private static object ReadField(object target, string name, Type declaringType = null)
        {
            FieldInfo field = (declaringType ?? target.GetType()).GetField(name, Members);
            Assert.That(field, Is.Not.Null, $"Missing serialized dependency {name}.");
            return field.GetValue(target);
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, Members);
            Assert.That(field, Is.Not.Null, $"Missing authored configuration {name}.");
            field.SetValue(target, value);
        }

        private sealed class RecordingResolver : IDamageResolver
        {
            public IDamageResolver Inner;
            public Action BeforeResolve;
            public readonly List<DamageRequest> Requests = new();
            public DamageResult Resolve(in DamageRequest request)
            {
                Requests.Add(request);
                BeforeResolve?.Invoke();
                return Inner != null ? Inner.Resolve(request) : default;
            }
        }

        private sealed class RecordingResolverComponent : MonoBehaviour, IDamageResolver
        {
            public readonly RecordingResolver Recorder = new();
            public DamageResult Resolve(in DamageRequest request) => Recorder.Resolve(request);
        }

        private sealed class OrbInputDriver : NetworkRunnerCallbacksAdapter
        {
            public NetworkButtons Buttons;
            public override void OnInput(NetworkRunner runner, NetworkInput input) =>
                input.Set(new PlayerNetworkInput { Buttons = Buttons, AimWorldPosition = Vector2.right * 10f });
        }
    }
}
#endif
