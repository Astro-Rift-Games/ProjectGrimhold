#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.EditMode.Player.Presentation
{
    public sealed class MeleeImpactParticleEmitterTests
    {
        private ScriptableObject _profile;
        private object _emitter;
        private Type _type;

        [SetUp]
        public void SetUp()
        {
            _type = typeof(WeaponDefinition).Assembly.GetType("MeleeImpactParticleEmitter");
            Assert.That(_type, Is.Not.Null, "A controller-owned reusable emitter is required.");
            _profile = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Scriptable Objects/Combat/ImpactParticles/Medium.asset");
            Assert.That(_profile, Is.Not.Null);
            _emitter = Activator.CreateInstance(_type);
        }

        [TearDown]
        public void TearDown() => (_emitter as IDisposable)?.Dispose();

        [TestCase(1f, 0f)]
        [TestCase(0f, -1f)]
        [TestCase(3f, 4f)]
        public void Samples_SnapToPixelGrid_AndRespectConeSpeedSizeAndLifetime(float x, float y)
        {
            var random = new System.Random(123);
            var so = new SerializedObject(_profile);
            Vector2 direction = new Vector2(x, y).normalized;
            for (int i = 0; i < 200; i++)
            {
                var sample = (ParticleSystem.EmitParams)_type.GetMethod("Sample").Invoke(null,
                    new object[] { _profile, new Vector2(1.03f, -2.09f), direction, random });
                Assert.That(sample.position, Is.EqualTo(new Vector3(1f, -2.0625f, 0f)));
                Assert.That(Vector2.Angle(direction, sample.velocity), Is.LessThanOrEqualTo(so.FindProperty("_spreadDegrees").floatValue / 2f + .001f));
                Assert.That(sample.velocity.magnitude, Is.InRange(so.FindProperty("_speedMin").floatValue, so.FindProperty("_speedMax").floatValue));
                Assert.That(sample.startSize * 16f, Is.EqualTo(Mathf.Round(sample.startSize * 16f)));
                Assert.That(sample.startSize * 16f, Is.InRange(so.FindProperty("_sizePixelsMin").intValue, so.FindProperty("_sizePixelsMax").intValue));
                Assert.That(sample.startLifetime, Is.InRange(so.FindProperty("_lifetimeMin").floatValue, so.FindProperty("_lifetimeMax").floatValue));
                Assert.That(sample.startColor, Is.EqualTo((Color32)so.FindProperty("_color").colorValue));
                int count = (int)_type.GetMethod("SampleCount").Invoke(null, new object[] { _profile, random });
                Assert.That(count, Is.InRange(so.FindProperty("_countMin").intValue, so.FindProperty("_countMax").intValue));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ZeroOrInvalidDirection_EmitsInEveryQuadrant(bool invalid)
        {
            var random = new System.Random(456);
            int quadrants = 0;
            for (int i = 0; i < 100; i++)
            {
                var sample = (ParticleSystem.EmitParams)_type.GetMethod("Sample").Invoke(null,
                    new object[] { _profile, Vector2.zero, invalid ? new Vector2(float.NaN, 0f) : Vector2.zero, random });
                quadrants |= 1 << ((sample.velocity.x >= 0f ? 1 : 0) + (sample.velocity.y >= 0f ? 2 : 0));
            }
            Assert.That(quadrants, Is.EqualTo(15));
        }

        [Test]
        public void SimultaneousAndConsecutiveHits_ReuseOneUnparentedSystem_AndDisposeIsIdempotent()
        {
            Assert.That(Emit(_profile, Vector2.one), Is.True);
            var instance = FindInstance();
            Assert.That(instance, Is.Not.Null);
            Assert.That(instance.transform.parent, Is.Null);
            int first = instance.particleCount;
            Assert.That(first, Is.GreaterThan(0));
            Assert.That(Emit(_profile, Vector2.left), Is.True);
            Assert.That(FindInstance(), Is.SameAs(instance));
            Assert.That(instance.particleCount, Is.GreaterThan(first));
            int combined = instance.particleCount;
            instance.Simulate(.01f, false, false);
            Assert.That(Emit(_profile, Vector2.up), Is.True);
            Assert.That(instance.particleCount, Is.GreaterThan(combined));
            Assert.That(_type.GetProperty("InstanceCount").GetValue(_emitter), Is.EqualTo(1));
            ((IDisposable)_emitter).Dispose();
            ((IDisposable)_emitter).Dispose();
            Assert.That(instance == null, Is.True);
            Assert.That(_type.GetProperty("InstanceCount").GetValue(_emitter), Is.Zero);
        }

        [Test]
        public void InvalidInput_DoesNotCreateAnInstance()
        {
            Assert.That(Emit(null, Vector2.zero), Is.False);
            Assert.That(Emit(_profile, new Vector2(float.NaN, 0f)), Is.False);
            Assert.That(Emit(_profile, new Vector2(0f, float.PositiveInfinity)), Is.False);
            Assert.That(_type.GetProperty("InstanceCount").GetValue(_emitter), Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Emit_UsesOptionalTargetColor_WithoutChangingProfile(bool useOverride)
        {
            var method = _type.GetMethod("Emit");
            Assert.That(method.GetParameters().Length, Is.EqualTo(4), "Emit must accept an optional color override.");
            var profile = (MeleeImpactParticleProfile)_profile;
            Color target = new Color(.208f, .631f, .757f, 1f);
            Assert.That(method.Invoke(_emitter, new object[] { profile, Vector2.zero, Vector2.right,
                useOverride ? (Color?)target : null }), Is.True);
            var particles = FindInstance();
            var samples = new ParticleSystem.Particle[particles.main.maxParticles];
            int count = particles.GetParticles(samples);
            Assert.That(count, Is.InRange(profile.CountMin, profile.CountMax));
            for (int i = 0; i < count; i++)
                Assert.That(samples[i].startColor, Is.EqualTo((Color32)(useOverride ? target : profile.Color)));
            Assert.That(profile.Color, Is.EqualTo(new Color(1f, .92f, .65f, 1f)));
        }

        private bool Emit(ScriptableObject profile, Vector2 point)
        {
            var method = _type.GetMethod("Emit");
            return (bool)method.Invoke(_emitter, method.GetParameters().Length == 4
                ? new object[] { profile, point, Vector2.right, null }
                : new object[] { profile, point, Vector2.right });
        }

        private ParticleSystem FindInstance()
        {
            foreach (var ps in Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
                if (ps.name == "MeleeImpactParticles (Local)") return ps;
            return null;
        }
    }
}
#endif
