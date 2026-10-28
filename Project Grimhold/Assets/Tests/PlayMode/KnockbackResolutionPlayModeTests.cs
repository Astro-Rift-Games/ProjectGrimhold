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
    public sealed class KnockbackResolutionPlayModeTests
    {
        private const string PlayerPrefabGuid = "fea3a7b256f965a4eb9b965832939741";
        private const string EnemyPrefabGuid = "5deca87613df0fa409d98702aec643d4";
        private NetworkRunner _runner;
        private PlayerAbilityRuntimeSimulationDriver _driver;
        private PlayerCharacter _player;
        private PlayerMovementNetworkController _movement;
        private KnockbackCurveConfig _config;
        private GameObject _wall;
        private Vector2 Position => _player.transform.position;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_wall != null) Object.Destroy(_wall);
            if (_runner != null)
            {
                var shutdown = _runner.Shutdown();
                while (!shutdown.IsCompleted) yield return null;
                Object.Destroy(_runner.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator T03_T05_StrongerPush_ReplacesFromCurrentPositionAlongNewDirection()
        {
            yield return Begin();
            yield return InSimulation(() => Assert.That(_movement.TryApplyKnockback(Vector2.right, 20f, true), Is.EqualTo(KnockbackDecision.Started)));
            yield return WaitTicks(3);
            Vector2 replacedAt = default;
            yield return InSimulation(() =>
            {
                Assert.That(_movement.IsKnockbackActive, Is.True);
                Assert.That(_movement.TryApplyKnockback(Vector2.up, 90f, true), Is.EqualTo(KnockbackDecision.Replaced));
                Assert.That(_movement.ActiveKnockbackEffectivePower, Is.EqualTo(90f));
                replacedAt = Position;
            });
            yield return FinishTravel(90f);
            Assert.That(Position.x, Is.EqualTo(replacedAt.x).Within(0.05f));
            Assert.That(Position.y - replacedAt.y, Is.EqualTo(Distance(90f)).Within(0.05f));
        }

        [UnityTest]
        public IEnumerator T04_T06_WeakerAndEqualPush_KeepPowerAndSequence()
        {
            yield return Begin();
            yield return InSimulation(() =>
            {
                Assert.That(_movement.TryApplyKnockback(Vector2.right, 90f, true), Is.EqualTo(KnockbackDecision.Started));
                int sequence = _movement.KnockbackSequence;
                Assert.That(_movement.TryApplyKnockback(Vector2.up, 20f, true), Is.EqualTo(KnockbackDecision.KeptActive));
                Assert.That(_movement.TryApplyKnockback(Vector2.up, 90f, true), Is.EqualTo(KnockbackDecision.KeptActive));
                Assert.That(_movement.ActiveKnockbackEffectivePower, Is.EqualTo(90f));
                Assert.That(_movement.KnockbackSequence, Is.EqualTo(sequence));
            });
            yield return FinishTravel(90f);
            Assert.That(Position.x, Is.EqualTo(Distance(90f)).Within(0.05f));
            Assert.That(Position.y, Is.EqualTo(0f).Within(0.05f));
        }

        [UnityTest]
        public IEnumerator T06_SameTickPerpendicularEqualPushes_DoNotAccumulateVectors()
        {
            yield return Begin();
            yield return InSimulation(() =>
            {
                Assert.That(_movement.TryApplyKnockback(Vector2.right, 20f, true), Is.EqualTo(KnockbackDecision.Started));
                Assert.That(_movement.TryApplyKnockback(Vector2.up, 20f, true), Is.EqualTo(KnockbackDecision.KeptActive));
            });
            yield return FinishTravel(20f);
            Assert.That(Position.x, Is.EqualTo(Distance(20f)).Within(0.05f));
            Assert.That(Position.y, Is.EqualTo(0f).Within(0.05f));
        }

        [UnityTest]
        public IEnumerator T07_FatalDamageWithPush_EntersDownedAndCompletesTravel()
        {
            yield return Begin();
            yield return InSimulation(() =>
            {
                Hit(_player, 100000f, 20f);
                Assert.That(_player.IsDowned, Is.True);
                Assert.That(_movement.IsKnockbackActive, Is.True);
            });
            yield return FinishTravel(20f);
            Assert.That(_player.IsDowned, Is.True);
            Assert.That(Position.x, Is.EqualTo(Distance(20f)).Within(0.05f));
        }

        [UnityTest]
        public IEnumerator T08_T12_T13_AlreadyDownedHit_DoesNotStartOrReplaceTravel()
        {
            yield return Begin();
            yield return InSimulation(() =>
            {
                Hit(_player, 100000f, 20f);
                int sequence = _movement.KnockbackSequence;
                Hit(_player, 1f, 90f);
                Assert.That(_movement.ActiveKnockbackEffectivePower, Is.EqualTo(20f));
                Assert.That(_movement.KnockbackSequence, Is.EqualTo(sequence));
            });
            yield return FinishTravel(20f);
            Vector2 stopped = Position;
            yield return InSimulation(() =>
            {
                int sequence = _movement.KnockbackSequence;
                Hit(_player, 1f, 90f);
                Assert.That(_movement.IsKnockbackActive, Is.False);
                Assert.That(_movement.KnockbackSequence, Is.EqualTo(sequence));
            });
            yield return FinishTravel(90f);
            Assert.That(Vector2.Distance(Position, stopped), Is.LessThan(0.01f));
            Assert.That(_player.IsDowned, Is.True);
        }

        [UnityTest]
        public IEnumerator T11_LaterDamageWithoutPush_EntersDownedWithoutInterruptingTravel()
        {
            yield return Begin();
            yield return InSimulation(() => _movement.TryApplyKnockback(Vector2.right, 20f, true));
            yield return WaitTicks(3);
            yield return InSimulation(() =>
            {
                Assert.That(_movement.IsKnockbackActive, Is.True);
                Hit(_player, 100000f, 0f);
                Assert.That(_player.IsDowned, Is.True);
                Assert.That(_movement.IsKnockbackActive, Is.True);
            });
            yield return FinishTravel(20f);
            Assert.That(Position.x, Is.EqualTo(Distance(20f)).Within(0.05f));
        }

        [UnityTest]
        public IEnumerator T14_DefinitiveDefeat_ClearsTravelWithoutFurtherDisplacement()
        {
            yield return Begin();
            yield return InSimulation(() => _movement.TryApplyKnockback(Vector2.right, 20f, true));
            yield return WaitTicks(3);
            Vector2 defeatedAt = default;
            yield return InSimulation(() =>
            {
                Assert.That(_movement.IsKnockbackActive, Is.True);
                // Same immediate-defeat seam as PlayerCorpseGenerationSimulationDriver.
                _player.GetComponent<PlayerDownedStateNetworkController>().TestDisableEntry = true;
                Hit(_player, 100000f, 0f);
                Assert.That(_player.IsAlive, Is.False);
                defeatedAt = Position;
            });
            yield return WaitTicks(1);
            Assert.That(_movement.IsKnockbackActive, Is.False, "Defeat clears travel on the first movement tick.");
            yield return FinishTravel(20f);
            Assert.That(Vector2.Distance(Position, defeatedAt), Is.LessThan(0.01f));
        }

        [UnityTest]
        public IEnumerator T01_T02_T10_ResistanceAbsorbsEqualAndLowerPower_ButDamageApplies()
        {
            yield return Begin();
            var spawned = _runner.Spawn(LoadPrefab(EnemyPrefabGuid), new Vector3(0f, 60f, 0f), Quaternion.identity);
            var enemy = spawned.GetComponent<EnemyCharacter>();
            foreach (var behaviour in spawned.GetComponents<NetworkBehaviour>())
            {
                if (behaviour is EnemyFSM || behaviour is EnemyCombatAIController) behaviour.enabled = false;
            }
            // Enemy receiver RE injection avoids coupling this integration test to inventory/participant setup.
            SetField(enemy, "_knockbackResistance", 20f);
            var motor = enemy.GetComponent<EnemyMovementAIController>();
            motor.TrySetControlEnabled(false);
            Vector3 start = enemy.transform.position;
            float health = enemy.Health;
            yield return InSimulation(() =>
            {
                Hit(enemy, 1f, 10f);
                Hit(enemy, 1f, 20f);
                Assert.That(motor.IsKnockbackActive, Is.False);
                Assert.That(enemy.Health, Is.EqualTo(health - 2f).Within(0.001f));
            });
            yield return FinishTravel(20f);
            Assert.That(Vector3.Distance(enemy.transform.position, start), Is.LessThan(0.01f));
        }

        [UnityTest]
        public IEnumerator T09_KnockbackIntoStaticWall_NeverPassesThrough()
        {
            yield return Begin();
            _wall = new GameObject("KnockbackTestWall") { layer = LayerMask.NameToLayer("WorldCollision") };
            _wall.transform.position = new Vector3(1.5f, 0f, 0f);
            _wall.AddComponent<BoxCollider2D>().size = new Vector2(0.2f, 6f);
            Physics2D.SyncTransforms();
            yield return InSimulation(() => _movement.TryApplyKnockback(Vector2.right, 90f, true));
            int until = _runner.Tick.Raw + Duration(90f) + 2;
            while (_runner.Tick.Raw < until)
            {
                Assert.That(Position.x, Is.LessThan(1.4f), "The avatar must remain before the near wall face on every observed frame.");
                yield return null;
            }
            Assert.That(Position.x, Is.GreaterThan(0.5f));
            Assert.That(Position.x, Is.LessThan(1.4f));
            Assert.That(_movement.IsKnockbackActive, Is.False);
        }

        private IEnumerator Begin()
        {
            var runnerObject = new GameObject("KnockbackResolutionTestRunner");
            _runner = runnerObject.AddComponent<NetworkRunner>();
            PlayModeRunnerComposition.AddLocalInputContext(_runner);
            runnerObject.AddComponent<EntityRegistry>();
            _driver = runnerObject.AddComponent<PlayerAbilityRuntimeSimulationDriver>();
            var start = _runner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Single,
                SessionName = $"knockback-{Guid.NewGuid():N}",
                SceneManager = runnerObject.AddComponent<NetworkSceneManagerDefault>(),
                ObjectProvider = runnerObject.AddComponent<NetworkObjectProviderDefault>()
            });
            while (!start.IsCompleted) yield return null;
            Assert.That(start.Result.Ok, Is.True, start.Result.ShutdownReason.ToString());
            var spawned = _runner.Spawn(LoadPrefab(PlayerPrefabGuid), Vector3.zero, Quaternion.identity);
            ExtractionProgressTestAssertions.AssertRegistered(_runner, spawned);
            Assert.That(spawned.HasStateAuthority, Is.True);
            _player = spawned.GetComponent<PlayerCharacter>();
            _movement = spawned.GetComponent<PlayerMovementNetworkController>();
            SetField(spawned.GetComponent<PlayerDownedStateNetworkController>(), "_downedDrainPerSecond", 0f);
            _config = (KnockbackCurveConfig)typeof(PlayerMovementNetworkController).GetField(
                "_knockbackCurveConfig", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_movement);
            Assert.That(_config, Is.Not.Null);
            Physics2D.SyncTransforms();
        }

        private void Hit(CharacterBase target, float amount, float power)
        {
            var resolver = _player.GetComponent<DamageResolver>();
            Assert.That(resolver, Is.Not.Null);
            var result = resolver.Resolve(new DamageRequest(new EntityId(int.MaxValue), target.Id,
                amount, DamageType.TrueDamage, Vector2.right, target.transform.position, _runner.Tick, power));
            Assert.That(result.IsApplied, Is.True);
        }

        private NetworkObject LoadPrefab(string guid)
        {
            var id = _runner.Config.PrefabTable.GetId(NetworkObjectGuid.Parse(guid));
            return _runner.Config.PrefabTable.Load(id, true);
        }

        private float Distance(float power)
        {
            Assert.That(_config.TryResolveProfile(power, _runner.DeltaTime, out float distance, out _), Is.True);
            return distance;
        }

        private int Duration(float power)
        {
            Assert.That(_config.TryResolveProfile(power, _runner.DeltaTime, out _, out int ticks), Is.True);
            return ticks;
        }

        private IEnumerator FinishTravel(float power)
        {
            yield return WaitTicks(Duration(power) + 2);
            Assert.That(_movement.IsKnockbackActive, Is.False);
        }

        private IEnumerator WaitTicks(int count)
        {
            int until = _runner.Tick.Raw + count;
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
            float deadline = Time.realtimeSinceStartup + 20f;
            while (!predicate() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(predicate(), Is.True, "The expected simulation state was not reached.");
        }

        private static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(target, value);
        }
    }
}
#endif
