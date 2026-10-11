using System;
using System.Reflection;
using System.Text.RegularExpressions;
using Fusion;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Assert = NUnit.Framework.Assert;
using LogType = UnityEngine.LogType;
using Object = UnityEngine.Object;

namespace Tests.EditMode.Abilities
{
    public sealed class ArcaneProjectileAbilityTests
    {
        private GameObject _caster;
        private GameObject _wall;
        private AbilityExecutionBehaviour _behaviour;
        private RecordingSpawner _spawner;

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_caster);
            Object.DestroyImmediate(_wall);
        }

        [Test]
        public void Definition_RetainsIntelligenceTenManaFifteenAndFiveSecondCooldown()
        {
            AbilityDefinition definition = Definition();
            Assert.That(definition.Resource, Is.EqualTo(AbilityResourceType.Mana));
            Assert.That(definition.Cost, Is.EqualTo(15));
            Assert.That(definition.CooldownSeconds, Is.EqualTo(5f));
            Assert.That(definition.AttributeRequirements.Requirements, Has.Count.EqualTo(1));
            Assert.That(definition.AttributeRequirements.Requirements[0].Attribute, Is.EqualTo(CharacterAttribute.Intelligence));
            Assert.That(definition.AttributeRequirements.Requirements[0].MinimumValue, Is.EqualTo(10));
        }

        [Test]
        public void Plan_WithoutAnyEnemy_StartsHalfSecondPreparationWithoutSpawning()
        {
            CreateBehaviour();
            AbilityExecutionContext context = Context();

            Assert.That(_behaviour.TryPlanStart(context, out var plan), Is.True);
            Assert.That(plan.IsValidStart, Is.True);
            Assert.That(plan.Phase, Is.EqualTo(AbilityExecutionPhase.Preparing));
            Assert.That(plan.DurationSeconds, Is.EqualTo(0.5f));
            Assert.That(_spawner.CallCount, Is.Zero);
        }

        [Test]
        public void Plan_PreparationIsAuthoredConfigurationNotAHardcodedDeadline()
        {
            CreateBehaviour();
            SetField(_behaviour, "_preparationSeconds", 0.8f);

            Assert.That(_behaviour.TryPlanStart(Context(), out var plan), Is.True);
            Assert.That(plan.IsValidStart, Is.True);
            Assert.That(plan.DurationSeconds, Is.EqualTo(0.8f));
        }

        [Test]
        public void Plan_BlockedAimIsStillAValidDirectionalStart()
        {
            CreateBehaviour();
            _wall = new GameObject("WallAhead");
            _wall.transform.position = Vector3.right * 0.2f;
            _wall.AddComponent<BoxCollider2D>();
            Physics2D.SyncTransforms();

            Assert.That(_behaviour.TryPlanStart(Context(), out var plan), Is.True);
            Assert.That(plan.IsValidStart, Is.True, "A blocked trajectory resolves by impact after preparation, not by rejecting payment.");
            Assert.That(_spawner.CallCount, Is.Zero);
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void Plan_InvalidPreparationYieldsInvalidPlanBeforeAnySpawn(float seconds)
        {
            CreateBehaviour();
            SetField(_behaviour, "_preparationSeconds", seconds);
            LogAssert.Expect(LogType.Error, new Regex("^ArcaneProjectileAbilityBehaviour.*"));

            Assert.That(_behaviour.TryPlanStart(Context(), out var plan), Is.True);
            Assert.That(plan.IsValidStart, Is.False);
            Assert.That(_spawner.CallCount, Is.Zero);
        }

        [Test]
        public void Plan_MissingExplicitProjectilePrefabCannotFallBackToEquippedWeapon()
        {
            CreateBehaviour();
            SetField(_behaviour, "_projectilePrefab", default(NetworkPrefabRef));
            LogAssert.Expect(LogType.Error, new Regex("^ArcaneProjectileAbilityBehaviour.*"));

            Assert.That(_behaviour.TryPlanStart(Context(), out var plan), Is.True);
            Assert.That(plan.IsValidStart, Is.False);
            Assert.That(_spawner.CallCount, Is.Zero);
        }

        [Test]
        public void Plan_EmptyImpactMaskYieldsInvalidPlanBeforePayment()
        {
            CreateBehaviour();
            SetField(_behaviour, "_impactLayerMask", new LayerMask { value = 0 });
            LogAssert.Expect(LogType.Error, new Regex("^ArcaneProjectileAbilityBehaviour.*"));

            Assert.That(_behaviour.TryPlanStart(Context(), out var plan), Is.True);
            Assert.That(plan.IsValidStart, Is.False);
        }

        [TestCase(AbilityExecutionStopReason.Knockback)]
        [TestCase(AbilityExecutionStopReason.Stun)]
        public void Preparation_AllowsInterruptionBeforeProjectileSpawn(AbilityExecutionStopReason reason)
        {
            CreateBehaviour();
            Assert.That(_behaviour.CanInterrupt(reason), Is.True);
            Assert.That(_spawner.CallCount, Is.Zero);
        }

        [Test]
        public void BeginRebindAndStop_NeverInstantiateAProjectileDuringPreparation()
        {
            CreateBehaviour();
            AbilityExecutionContext context = Context();
            var snapshot = new AbilityExecutionSnapshot
            {
                Phase = AbilityExecutionPhase.Preparing, Sequence = 1, AimDirection = Vector2.right
            };

            _behaviour.Begin(context, snapshot);
            _behaviour.Rebind(context, snapshot);
            _behaviour.Stop(context, snapshot, AbilityExecutionStopReason.Knockback);

            Assert.That(_spawner.CallCount, Is.Zero, "Only forward-authoritative preparation resolution may spawn.");
        }

        [Test]
        public void VoluntarySpeedReduction_IsPreparationOnlyAndConfigurable()
        {
            CreateBehaviour();
            SetField(_behaviour, "_preparationMovementSpeedMultiplier", 0.4f);
            Assert.That(MovementMultiplier(AbilityExecutionPhase.Preparing), Is.EqualTo(0.4f));
            Assert.That(MovementMultiplier(AbilityExecutionPhase.Executing), Is.EqualTo(1f));
            Assert.That(MovementMultiplier(AbilityExecutionPhase.Idle), Is.EqualTo(1f));
        }

        private float MovementMultiplier(AbilityExecutionPhase phase)
        {
            MethodInfo method = typeof(AbilityExecutionBehaviour).GetMethod("GetVoluntaryMovementSpeedMultiplier");
            Assert.That(method, Is.Not.Null,
                "Add a read-only virtual GetVoluntaryMovementSpeedMultiplier(in AbilityExecutionSnapshot) seam, defaulting to 1.");
            return (float)method.Invoke(_behaviour, new object[] { new AbilityExecutionSnapshot { Phase = phase, Sequence = 1 } });
        }

        private void CreateBehaviour()
        {
            Type type = typeof(AbilityExecutionBehaviour).Assembly.GetType("ArcaneProjectileAbilityBehaviour");
            Assert.That(type, Is.Not.Null, "TASK202 requires ArcaneProjectileAbilityBehaviour.");
            Assert.That(typeof(AbilityExecutionBehaviour).IsAssignableFrom(type), Is.True);
            _caster = new GameObject("ArcaneCaster");
            _caster.SetActive(false);
            _caster.AddComponent<PlayerCharacter>();
            _spawner = _caster.AddComponent<RecordingSpawner>();
            _behaviour = (AbilityExecutionBehaviour)_caster.AddComponent(type);
            SetField(_behaviour, "_definition", Definition(), typeof(AbilityExecutionBehaviour));
            SetField(_behaviour, "_projectilePrefab", new NetworkPrefabRef("cdc42ba57b303264fb49354983542d59"));
            SetField(_behaviour, "_impactLayerMask", new LayerMask { value = 1 });
            SetField(_behaviour, "_projectileSpawnerSource", _spawner);
        }

        private AbilityExecutionContext Context() => new AbilityExecutionContext(null,
            _caster.GetComponent<PlayerCharacter>(), UniversalAbilitySlot.Slot1, Definition(),
            default, null, Vector2.right);

        private static AbilityDefinition Definition()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AbilityDefinitionCatalog>(
                "Assets/Scriptable Objects/Abilities/Catalogs/AbilityDefinitionCatalog.asset");
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.TryGet(new AbilityId("arcane_projectile"), out var definition), Is.True);
            return definition;
        }

        private static void SetField(object target, string name, object value, Type declaringType = null)
        {
            FieldInfo field = (declaringType ?? target.GetType()).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Required configuration field {name} was not found.");
            field.SetValue(target, value);
        }

        private sealed class RecordingSpawner : MonoBehaviour, IProjectileSpawner
        {
            public int CallCount { get; private set; }
            public ProjectileSpawnResult Spawn(in ProjectileSpawnRequest request)
            {
                CallCount++;
                return new ProjectileSpawnResult(true);
            }
        }
    }
}
