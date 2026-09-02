using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Fusion;
using System.Reflection;
using Assert = NUnit.Framework.Assert;

namespace Tests.EditMode.Combat
{
    public class RangedAttackConfigTests
    {
        private RangedAttackConfig _config;

        [TearDown]
        public void TearDown()
        {
            if (_config != null)
            {
                Object.DestroyImmediate(_config);
            }
        }

        private const string RealConfigPath = "Assets/Scriptable Objects/RangePlayerAttackConfig.asset";

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

        private static void SetPrivateField<T>(object target, System.Type declaringType, string fieldName, T value)
        {
            FieldInfo field = declaringType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Field {declaringType.Name}.{fieldName} was not found.");
            field.SetValue(target, value);
        }

        private static void SetPrivateField<T>(object target, string fieldName, T value)
        {
            SetPrivateField(target, target.GetType(), fieldName, value);
        }

        [Test]
        public void TryValidate_WithInvalidSpeed_Fails()
        {
            _config = CreateTemporaryValidConfig();
            SetPrivateField(_config, "_projectileSpeed", 0f);

            bool result = _config.TryValidate(out string error);
            Assert.IsFalse(result);
            Assert.That(error, Does.Contain("ProjectileSpeed"));
        }

        [Test]
        public void TryValidate_WithInvalidLifetime_Fails()
        {
            _config = CreateTemporaryValidConfig();
            SetPrivateField(_config, "_lifetimeSeconds", -1f);

            bool result = _config.TryValidate(out string error);
            Assert.IsFalse(result);
            Assert.That(error, Does.Contain("LifetimeSeconds"));
        }

        [Test]
        public void TryValidate_WithNegativeOffset_Fails()
        {
            _config = CreateTemporaryValidConfig();
            SetPrivateField(_config, "_projectileSpawnOffset", -0.5f);

            bool result = _config.TryValidate(out string error);
            Assert.IsFalse(result);
            Assert.That(error, Does.Contain("ProjectileSpawnOffset"));
        }

        [Test]
        public void TryValidate_WithEmptyLayerMask_Fails()
        {
            _config = CreateTemporaryValidConfig();
            SetPrivateField(_config, "_impactLayerMask", new LayerMask { value = 0 });

            bool result = _config.TryValidate(out string error);
            Assert.IsFalse(result);
            Assert.That(error, Does.Contain("ImpactLayerMask"));
        }

        [Test]
        public void TryValidate_WithRealConfigAsset_Passes()
        {
            RangedAttackConfig source = AssetDatabase.LoadAssetAtPath<RangedAttackConfig>(RealConfigPath);
            Assert.That(source, Is.Not.Null, "Ranged attack config asset was not found at the expected project path.");
            _config = Object.Instantiate(source);
            bool result = _config.TryValidate(out string error);
            Assert.IsTrue(result, _config.name + " " + error);
        }

        [Test]
        public void RealConfigAsset_IncludesWorldCollisionLayer()
        {
            RangedAttackConfig source = AssetDatabase.LoadAssetAtPath<RangedAttackConfig>(RealConfigPath);
            Assert.That(source, Is.Not.Null, "Ranged attack config asset was not found at the expected project path.");
            int worldCollisionLayer = LayerMask.NameToLayer("WorldCollision");
            Assert.That(worldCollisionLayer, Is.Not.EqualTo(-1), "WorldCollision layer must exist in TagManager.");
            int expectedBit = 1 << worldCollisionLayer;
            Assert.That((source.ImpactLayerMask.value & expectedBit) != 0, Is.True, "RangedAttackConfig ImpactLayerMask must include WorldCollision layer.");
        }
    }
}
