using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Fusion;
using Assert = NUnit.Framework.Assert;

namespace Tests.EditMode.Combat
{
    public class RangedAttackTests
    {
        private static readonly AttackExecutionParameters DefaultParameters =
            new(10f, DamageType.Physical, 0.5f, 10f, 6f);

        private GameObject _gameObject;
        private RangedAttack _attack;
        private FakeProjectileSpawner _spawner;
        private RangedAttackConfig _config;

        [TearDown]
        public void TearDown()
        {
            if (_gameObject != null)
            {
                Object.DestroyImmediate(_gameObject);
            }
            if (_config != null)
            {
                Object.DestroyImmediate(_config);
            }
        }

        private static RangedAttackConfig CreateTemporaryValidConfig()
        {
            var config = ScriptableObject.CreateInstance<RangedAttackConfig>();

            SetPrivateField(config, "_projectileSpeed", 10f);
            SetPrivateField(config, "_lifetimeSeconds", 5f);
            SetPrivateField(config, "_projectileSpawnOffset", 0.7f);
            SetPrivateField(config, "_projectilePrefab", new NetworkPrefabRef("00000000000000000000000000000001"));
            SetPrivateField(config, "_impactLayerMask", new LayerMask { value = 1 });

            return config;
        }

        private static void SetPrivateField(object target, System.Type declaringType, string fieldName, object value)
        {
            FieldInfo field = declaringType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Field {declaringType.Name}.{fieldName} was not found.");
            field.SetValue(target, value);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            SetPrivateField(target, target.GetType(), fieldName, value);
        }

        private IEnumerator CreateValidAttack(bool spawnSucceeds = true)
        {
            _gameObject = new GameObject("RangedAttackTests");
            _gameObject.SetActive(false);

            _spawner = _gameObject.AddComponent<FakeProjectileSpawner>();
            _spawner.SpawnSucceeds = spawnSucceeds;

            _attack = _gameObject.AddComponent<RangedAttack>();
            _config = CreateTemporaryValidConfig();

            SetPrivateField(_attack, typeof(RangedAttack), "_config", _config);
            SetPrivateField(_attack, typeof(RangedAttack), "_defaultParameters", DefaultParameters);
            SetPrivateField(_attack, typeof(RangedAttack), "_projectileSpawnerSource", _spawner);
            Assert.That(_attack.TryConfigure(_config, DefaultParameters), Is.True);

            _gameObject.SetActive(true);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Execute_WithValidDirection_DelegatesToSpawnerWithNormalizedParams()
        {
            yield return CreateValidAttack(spawnSucceeds: true);

            var request = new AttackRequest(new EntityId(1), new Vector2(2f, 3f), new Vector2(3f, 4f), 10);
            var result = _attack.Execute(request);

            Assert.IsTrue(result.WasExecuted);
            Assert.IsTrue(_spawner.WasCalled);

            var spawnRequest = _spawner.LastRequest;
            Assert.AreEqual(new EntityId(1), spawnRequest.OwnerId);
            Assert.AreEqual(DefaultParameters.Damage, spawnRequest.Damage);
            Assert.AreEqual(DefaultParameters.DamageType, spawnRequest.DamageType);
            Assert.AreEqual(_config.ProjectileSpeed, spawnRequest.Speed);
            Assert.AreEqual(_config.LifetimeSeconds, spawnRequest.LifetimeSeconds);
            Assert.AreEqual(DefaultParameters.Range, spawnRequest.MaximumRange);
            Assert.AreEqual(DefaultParameters.KnockbackForce, spawnRequest.KnockbackForce);
            Assert.AreEqual(10, spawnRequest.SimulationTick);

            Vector2 expectedDirection = new Vector2(3f, 4f).normalized;
            Assert.AreEqual(expectedDirection.x, spawnRequest.Direction.x, 0.0001f);
            Assert.AreEqual(expectedDirection.y, spawnRequest.Direction.y, 0.0001f);

            Vector2 expectedOrigin = new Vector2(2f, 3f) + expectedDirection * _config.ProjectileSpawnOffset;
            Assert.AreEqual(expectedOrigin.x, spawnRequest.Origin.x, 0.0001f);
            Assert.AreEqual(expectedOrigin.y, spawnRequest.Origin.y, 0.0001f);
        }

        [UnityTest]
        public IEnumerator TryConfigure_UpdatesAllRuntimeParametersWithoutMutatingConfig()
        {
            yield return CreateValidAttack(spawnSucceeds: true);
            float projectileSpeed = _config.ProjectileSpeed;
            var parameters = new AttackExecutionParameters(18.25f, DamageType.Magical, 1.25f, 7f, 15f);

            Assert.That(_attack.TryConfigure(_config, parameters), Is.True);

            AttackResult result = _attack.Execute(
                new AttackRequest(new EntityId(1), Vector2.zero, Vector2.right, 10));

            Assert.That(result.WasExecuted, Is.True);
            Assert.That(_spawner.LastRequest.Damage, Is.EqualTo(18.25f));
            Assert.That(_spawner.LastRequest.DamageType, Is.EqualTo(DamageType.Magical));
            Assert.That(_spawner.LastRequest.MaximumRange, Is.EqualTo(7f));
            Assert.That(_spawner.LastRequest.KnockbackForce, Is.EqualTo(15f));
            Assert.That(_attack.CooldownSeconds, Is.EqualTo(1.25f));
            Assert.That(_config.ProjectileSpeed, Is.EqualTo(projectileSpeed));
        }

        [UnityTest]
        public IEnumerator Execute_WithZeroDirection_IsRejected()
        {
            yield return CreateValidAttack(spawnSucceeds: true);

            var request = new AttackRequest(new EntityId(1), Vector2.zero, Vector2.zero, 10);
            var result = _attack.Execute(request);

            Assert.IsFalse(result.WasExecuted);
            Assert.AreEqual(AttackFailureReason.InvalidDirection, result.FailureReason);
            Assert.IsFalse(_spawner.WasCalled);
        }

        [UnityTest]
        public IEnumerator Execute_WithMissingConfig_IsRejected()
        {
            _gameObject = new GameObject("RangedAttackTests");
            _gameObject.SetActive(false);

            _spawner = _gameObject.AddComponent<FakeProjectileSpawner>();
            _attack = _gameObject.AddComponent<RangedAttack>();

            SetPrivateField(_attack, typeof(RangedAttack), "_config", null);
            SetPrivateField(_attack, typeof(RangedAttack), "_projectileSpawnerSource", _spawner);

            _gameObject.SetActive(true);
            yield return null;

            var logEnabled = Debug.unityLogger.logEnabled;
            Debug.unityLogger.logEnabled = false;
            AttackResult result;
            try
            {
                var request = new AttackRequest(new EntityId(1), Vector2.zero, Vector2.right, 10);
                result = _attack.Execute(request);
            }
            finally
            {
                Debug.unityLogger.logEnabled = logEnabled;
            }

            Assert.IsFalse(result.WasExecuted);
            Assert.AreEqual(AttackFailureReason.MissingConfiguration, result.FailureReason);
        }

        [UnityTest]
        public IEnumerator Execute_WithMissingSpawner_IsRejected()
        {
            _gameObject = new GameObject("RangedAttackTests");
            _gameObject.SetActive(false);

            _attack = _gameObject.AddComponent<RangedAttack>();
            _config = CreateTemporaryValidConfig();

            SetPrivateField(_attack, typeof(RangedAttack), "_config", _config);
            SetPrivateField(_attack, typeof(RangedAttack), "_projectileSpawnerSource", null);

            _gameObject.SetActive(true);
            yield return null;

            var logEnabled = Debug.unityLogger.logEnabled;
            Debug.unityLogger.logEnabled = false;
            AttackResult result;
            try
            {
                var request = new AttackRequest(new EntityId(1), Vector2.zero, Vector2.right, 10);
                result = _attack.Execute(request);
            }
            finally
            {
                Debug.unityLogger.logEnabled = logEnabled;
            }

            Assert.IsFalse(result.WasExecuted);
            Assert.AreEqual(AttackFailureReason.MissingConfiguration, result.FailureReason);
        }

        [UnityTest]
        public IEnumerator Execute_WhenSpawnFails_ReturnsExecutionFailed()
        {
            yield return CreateValidAttack(spawnSucceeds: false);

            var request = new AttackRequest(new EntityId(1), Vector2.zero, Vector2.right, 10);
            var result = _attack.Execute(request);

            Assert.IsFalse(result.WasExecuted);
            Assert.AreEqual(AttackFailureReason.ExecutionFailed, result.FailureReason);
            Assert.IsTrue(_spawner.WasCalled);
        }

        [UnityTest]
        public IEnumerator AcceptedShot_IsIndependentOfSharedExecutorAndAssetReconfiguration()
        {
            yield return CreateValidAttack();
            var parameters = new AttackExecutionParameters(28f, DamageType.Magical, 0.9f, 6f, 2f, 0.45f);
            Assert.That(_attack.TryConfigure(_config, parameters), Is.True);
            var request = new AttackRequest(new EntityId(1), Vector2.zero, Vector2.right, 100);
            Assert.That(_attack.TryAcceptRelease(request, 0.02f, out RangedAttackRelease accepted), Is.True);
            Assert.That(_spawner.WasCalled, Is.False, "Acceptance must not spawn.");
            NetworkPrefabRef originalPrefab = _config.ProjectilePrefab;
            SetPrivateField(_config, "_projectilePrefab", new NetworkPrefabRef("00000000000000000000000000000002"));
            SetPrivateField(_config, "_projectileSpeed", 20f);
            SetPrivateField(_config, "_projectileSpawnOffset", 2f);
            SetPrivateField(_config, "_impactLayerMask", new LayerMask { value = 2 });
            Assert.That(_attack.TryConfigure(_config, DefaultParameters), Is.True);
            Assert.That(accepted.TryConsume(123, new EntityId(1), Vector2.one, out var shot), Is.True);
            Assert.That(shot.Damage, Is.EqualTo(28f));
            Assert.That(shot.DamageType, Is.EqualTo(DamageType.Magical));
            Assert.That(shot.Speed, Is.EqualTo(10f));
            Assert.That(shot.Origin, Is.EqualTo(Vector2.one + Vector2.right * 0.7f));
            Assert.That(shot.ImpactLayerMask, Is.EqualTo(1));
            Assert.That(shot.ProjectilePrefab, Is.EqualTo(originalPrefab));
            Assert.That(shot.MaximumRange, Is.EqualTo(6f));
            Assert.That(shot.KnockbackForce, Is.EqualTo(2f));
        }

        [UnityTest]
        public IEnumerator Execute_WithConfiguredWindup_RemainsImmediateForExistingConsumers()
        {
            yield return CreateValidAttack();
            Assert.That(_attack.TryConfigure(_config,
                new AttackExecutionParameters(10f, DamageType.Physical, 1f, 6f, 0f, 0.45f)), Is.True);
            Assert.That(_attack.Execute(new AttackRequest(new EntityId(1), Vector2.zero, Vector2.right, 10)).WasExecuted, Is.True);
            Assert.That(_spawner.WasCalled, Is.True);
            Assert.That(_spawner.LastRequest.SimulationTick, Is.EqualTo(10));
        }

        private sealed class FakeProjectileSpawner : MonoBehaviour, IProjectileSpawner
        {
            public bool SpawnSucceeds { get; set; } = true;
            public bool WasCalled { get; private set; }
            public ProjectileSpawnRequest LastRequest { get; private set; }

            public ProjectileSpawnResult Spawn(in ProjectileSpawnRequest request)
            {
                WasCalled = true;
                LastRequest = request;
                return new ProjectileSpawnResult(SpawnSucceeds);
            }
        }
    }
}
