using System;
using System.Reflection;
using System.Text.RegularExpressions;
using Fusion;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Assert = NUnit.Framework.Assert;
using Object = UnityEngine.Object;

namespace Tests.EditMode.Abilities
{
    /// <summary>Integration contracts only; existing ProtectiveOrbsRulesTests own the pure mask/orbit rules.</summary>
    public sealed class ProtectiveOrbsIntegrationTests
    {
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private GameObject _local;

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_local);

        [Test]
        public void Definition_RetainsExistingManaThirtyAndTwentyFiveSecondCooldown()
        {
            AbilityDefinition definition = Definition();
            Assert.That(definition.Resource, Is.EqualTo(AbilityResourceType.Mana));
            Assert.That(definition.Cost, Is.EqualTo(30));
            Assert.That(definition.CooldownSeconds, Is.EqualTo(25f));
        }

        [Test]
        public void ProductiveAvatar_ComposesInstantBehaviourAndSnapshotEffectOnTheSameRoot()
        {
            GameObject prefab = Prefab();
            Component effect = prefab.GetComponent(RequiredType("ProtectiveOrbsNetworkEffect"));
            var behaviour = prefab.GetComponent(RequiredType("ProtectiveOrbsAbilityBehaviour")) as AbilityExecutionBehaviour;
            Assert.That(effect, Is.Not.Null);
            Assert.That(behaviour, Is.Not.Null);
            Assert.That(effect, Is.InstanceOf<NetworkBehaviour>());
            Assert.That(effect.gameObject, Is.SameAs(prefab));
            Assert.That(Field(behaviour, "_effect"), Is.SameAs(effect));
            Assert.That(Field(effect, "_playerCharacter"), Is.SameAs(prefab.GetComponent<PlayerCharacter>()));
            Assert.That(Field(effect, "_damageResolverSource"), Is.SameAs(prefab.GetComponent<DamageResolver>()));
            Assert.That(Field(behaviour, "_definition", typeof(AbilityExecutionBehaviour)), Is.SameAs(Definition()));
            Assert.That((AbilityExecutionBehaviour[])Field(prefab.GetComponent<PlayerAbilityRuntimeNetworkController>(),
                "_executionBehaviours"), Does.Contain(behaviour));
            Assert.That(Field(behaviour, "_orbCount"), Is.EqualTo(8));
            Assert.That(Field(behaviour, "_lifetimeSeconds"), Is.EqualTo(15f));
        }

        [Test]
        public void OrbPrefab_IsLocalGeometryAndPresentationNotAnotherNetworkObject()
        {
            Component effect = Prefab().GetComponent(RequiredType("ProtectiveOrbsNetworkEffect"));
            Assert.That(effect, Is.Not.Null);
            var orb = Field(effect, "_orbPrefab") as Component;
            Assert.That(orb, Is.Not.Null);
            Assert.That(orb.GetType(), Is.EqualTo(RequiredType("ProtectiveOrbCollider")));
            Assert.That(orb.GetComponentInChildren<NetworkObject>(true), Is.Null);
            Assert.That(orb.GetComponentsInChildren<NetworkBehaviour>(true), Is.Empty);
            Collider2D collider = orb.GetComponent<Collider2D>();
            Assert.That(collider, Is.Not.Null);
            Assert.That(collider.isTrigger, Is.True);
            Assert.That(orb.gameObject.layer, Is.EqualTo(LayerMask.NameToLayer("Character")));
            SpriteRenderer renderer = orb.GetComponentInChildren<SpriteRenderer>(true);
            Assert.That(renderer, Is.Not.Null);
            Assert.That(renderer.sprite, Is.Not.Null);
        }

        [TestCase("ActiveMask", typeof(uint))]
        [TestCase("InitialCount", typeof(int))]
        [TestCase("StartTick", typeof(int))]
        [TestCase("LifetimeTimer", typeof(TickTimer))]
        [TestCase("OrbitRadius", typeof(float))]
        [TestCase("AngularSpeedDegreesPerSecond", typeof(float))]
        [TestCase("ContactDamage", typeof(float))]
        [TestCase("ContactDamageType", typeof(DamageType))]
        public void Effect_ReplicatesConsumptionAndOriginalTimingInFusionStorage(string name, Type expectedType)
        {
            PropertyInfo property = RequiredType("ProtectiveOrbsNetworkEffect").GetProperty(name, Members);
            Assert.That(property, Is.Not.Null);
            Assert.That(property.PropertyType, Is.EqualTo(expectedType));
            Assert.That(property.GetCustomAttribute<NetworkedAttribute>(), Is.Not.Null,
                "A local field cannot survive CopyStateFrom; deriving a new full mask on restore is forbidden.");
        }

        [Test]
        public void AttackTarget_OldConstructorRemainsUnshieldedAndNewConstructorRetainsExactOrb()
        {
            Type orbType = RequiredType("ProtectiveOrbCollider");
            PropertyInfo property = typeof(AttackTarget).GetProperty("ProtectiveOrb");
            Assert.That(property, Is.Not.Null);
            Assert.That(property.PropertyType, Is.EqualTo(orbType));
            Assert.That(property.GetValue(new AttackTarget(new EntityId(7), Vector2.up)), Is.Null);
            _local = new GameObject("SelectedOrb");
            _local.SetActive(false);
            _local.AddComponent<CircleCollider2D>(); // Supply a concrete shape before the Collider2D requirement.
            Component orb = _local.AddComponent(orbType);
            ConstructorInfo constructor = typeof(AttackTarget).GetConstructor(new[]
                { typeof(EntityId), typeof(Vector2), orbType });
            Assert.That(constructor, Is.Not.Null);
            var target = (AttackTarget)constructor.Invoke(new object[] { new EntityId(7), Vector2.up, orb });
            Assert.That(target.TargetId, Is.EqualTo(new EntityId(7)));
            Assert.That(target.HitPoint, Is.EqualTo(Vector2.up));
            Assert.That(property.GetValue(target), Is.SameAs(orb));
        }

        [Test]
        public void UnspawnedNonAuthority_CannotConsumeOrAccessUninitializedNetworkStorage()
        {
            // This is the no-authority safety boundary, NOT a simulated proxy or a multi-runner claim.
            _local = new GameObject("UnspawnedOrbEffect");
            _local.SetActive(false);
            var effect = _local.AddComponent(RequiredType("ProtectiveOrbsNetworkEffect")) as NetworkBehaviour;
            Assert.That(effect, Is.Not.Null);
            Assert.That(effect.Object, Is.Null);
            MethodInfo method = effect.GetType().GetMethod("TryConsumeEnemyImpact", Members, null,
                new[] { typeof(int), typeof(EntityId) }, null);
            Assert.That(method, Is.Not.Null);
            Assert.That(method.Invoke(effect, new object[] { 0, new EntityId(7) }), Is.False,
                "Authority/running-state guards must precede all networked reads and writes.");
        }

        [TestCase(0)]
        [TestCase(33)]
        public void Plan_InvalidCountFailsBeforeActivationWithoutSearchingForATarget(int count)
        {
            // Inactive prefab clone provides the real explicit dependencies without a scene or runner.
            _local = new GameObject("InactiveOrbPlanFixture");
            _local.SetActive(false);
            GameObject clone = Object.Instantiate(Prefab(), _local.transform);
            var behaviour = clone.GetComponent(RequiredType("ProtectiveOrbsAbilityBehaviour")) as AbilityExecutionBehaviour;
            Assert.That(behaviour, Is.Not.Null);
            SetField(behaviour, "_orbCount", count);
            LogAssert.Expect(UnityEngine.LogType.Error, new Regex("^ProtectiveOrbsAbilityBehaviour.*"));
            var context = new AbilityExecutionContext(null, clone.GetComponent<PlayerCharacter>(),
                UniversalAbilitySlot.Slot1, Definition(), default, null, Vector2.right);
            Assert.That(behaviour.TryPlanStart(context, out var plan), Is.True);
            Assert.That(plan.IsValidStart, Is.False);
        }

        private static GameObject Prefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/NetworkPlayer.prefab");
            Assert.That(prefab, Is.Not.Null);
            return prefab;
        }

        private static AbilityDefinition Definition()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AbilityDefinitionCatalog>(
                "Assets/Scriptable Objects/Abilities/Catalogs/AbilityDefinitionCatalog.asset");
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.TryGet(new AbilityId("protective_orbs"), out var definition), Is.True);
            return definition;
        }

        private static Type RequiredType(string name)
        {
            Type type = typeof(AbilityExecutionBehaviour).Assembly.GetType(name);
            Assert.That(type, Is.Not.Null, $"TASK203 requires {name}.");
            return type;
        }

        private static object Field(object target, string name, Type declaringType = null)
        {
            FieldInfo field = (declaringType ?? target.GetType()).GetField(name, Members);
            Assert.That(field, Is.Not.Null, $"Missing explicit configuration/dependency {name}.");
            return field.GetValue(target);
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, Members);
            Assert.That(field, Is.Not.Null);
            field.SetValue(target, value);
        }
    }
}
