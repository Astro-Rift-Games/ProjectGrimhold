#if UNITY_INCLUDE_TESTS
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Combat
{
    public sealed class DamageResolverFeedbackTests
    {
        [Test]
        public void Resolve_ForwardsExactAppliedResultWithoutChangingIt()
        {
            var registryHolder = new GameObject("DamageRegistry");
            var resolverHolder = new GameObject("DamageResolver");
            try
            {
                EntityRegistry registry = registryHolder.AddComponent<EntityRegistry>();
                var sink = resolverHolder.AddComponent<ResolvedDamageSink>();
                DamageResolver resolver = resolverHolder.AddComponent<DamageResolver>();
                MethodInfo cacheFeedbackSinkMethod = typeof(DamageResolver).GetMethod(
                    "CacheFeedbackSink",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(cacheFeedbackSinkMethod, Is.Not.Null);
                cacheFeedbackSinkMethod.Invoke(resolver, null);

                FieldInfo registryField = typeof(DamageResolver).GetField(
                    "_registry",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(registryField, Is.Not.Null);
                registryField.SetValue(resolver, registry);

                var targetId = new EntityId(22);
                var damageable = new ExactDamageable(targetId, 7f);
                Assert.That(
                    registry.TryRegisterEntity(targetId, damageable, Array.Empty<Collider2D>()),
                    Is.True);
                var request = new DamageRequest(
                    new EntityId(11),
                    targetId,
                    10f,
                    DamageType.Physical,
                    Vector2.right,
                    new Vector2(2f, 3f),
                    44);

                DamageResult result = resolver.Resolve(request);

                Assert.That(result.AppliedDamage, Is.EqualTo(7f));
                Assert.That(sink.CallCount, Is.EqualTo(1));
                Assert.That(sink.LastEvent.Result.AppliedDamage, Is.EqualTo(7f));
                Assert.That(sink.LastEvent.Request.HitPoint, Is.EqualTo(new Vector2(2f, 3f)));
                Assert.That(sink.LastEvent.Request.SimulationTick, Is.EqualTo(44));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(resolverHolder);
                UnityEngine.Object.DestroyImmediate(registryHolder);
            }
        }

        private sealed class ExactDamageable : IDamageable
        {
            private readonly float _appliedDamage;

            public ExactDamageable(EntityId id, float appliedDamage)
            {
                Id = id;
                _appliedDamage = appliedDamage;
            }

            public EntityId Id { get; }
            public bool CanReceiveDamage => true;

            public DamageResult ApplyDamage(in DamageRequest request)
            {
                return new DamageResult(
                    Id,
                    true,
                    _appliedDamage,
                    13f,
                    false,
                    DamageFailureReason.None);
            }
        }

        private sealed class ResolvedDamageSink : MonoBehaviour, IResolvedDamageFeedbackSink
        {
            public int CallCount { get; private set; }
            public DamageResolvedEvent LastEvent { get; private set; }

            public void RecordResolvedDamage(in DamageResolvedEvent resolvedDamage)
            {
                CallCount++;
                LastEvent = resolvedDamage;
            }
        }
    }
}
#endif
