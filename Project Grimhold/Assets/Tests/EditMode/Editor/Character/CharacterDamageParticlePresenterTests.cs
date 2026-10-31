#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Character
{
    public sealed class CharacterDamageParticlePresenterTests
    {
        private GameObject _root;
        private CharacterDamageParticlePresenter _presenter;
        private ParticleSystem _particles;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Generic damage particles fixture");
            _presenter = _root.AddComponent<CharacterDamageParticlePresenter>();
            _particles = _root.AddComponent<ParticleSystem>();
            var main = _particles.main;
            main.playOnAwake = false;
            main.startLifetime = 10f;
            _particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            Set("_damageParticles", _particles);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_root);

        [Test]
        public void ConfirmationBeforeHealth_AbsorbsOnlyOneDrop_NotAnIndependentFollowingDrop()
        {
            Absorb();
            Drop();
            Assert.That(_particles.isPlaying, Is.False);
            Drop();
            Assert.That(_particles.isPlaying, Is.True);
        }

        [Test]
        public void ConfirmationAfterHealth_ClearsJustPlayedBurst_ButDoesNotArmAnotherAbsorb()
        {
            Drop();
            _particles.Emit(4);
            Assert.That(_particles.particleCount, Is.GreaterThan(0));
            Absorb();
            Assert.That(_particles.particleCount, Is.Zero);
            Assert.That(_particles.isPlaying, Is.False);
            Drop();
            Assert.That(_particles.isPlaying, Is.True);
        }

        [Test]
        public void ExpiredAbsorb_DoesNotHideIndependentDamage()
        {
            Absorb();
            Set("_absorbUntil", Time.time - 1f);
            Drop();
            Assert.That(_particles.isPlaying, Is.True);
        }

        [Test]
        public void OlderBurst_IsNotClearedByLateConfirmation()
        {
            Drop();
            _particles.Emit(4);
            Set("_genericStartedAt", Time.time - 1f);
            Absorb();
            Assert.That(_particles.particleCount, Is.GreaterThan(0));
        }

        [Test]
        public void NoMeleeConfirmation_PreservesGenericDamageBehavior()
        {
            Drop();
            Assert.That(_particles.isPlaying, Is.True);
        }

        [Test]
        public void Disable_ClearsParticlesAndPendingAbsorption()
        {
            Absorb();
            typeof(CharacterDamageParticlePresenter).GetMethod("OnDisable", Private).Invoke(_presenter, null);
            Drop();
            Assert.That(_particles.isPlaying, Is.True);
        }

        [Test]
        public void IndependentDamageInsideWindow_LargerThanConfirmedMelee_StillPlaysGeneric()
        {
            Absorb(10f);
            Drop(25f);
            Assert.That(_particles.isPlaying, Is.True, "A drop the melee damage cannot explain includes another source.");
        }

        [Test]
        public void GenericBurstThatIncludedOtherDamage_IsNotClearedByMeleeConfirmation()
        {
            Drop(25f);
            _particles.Emit(4);
            Absorb(10f);
            Assert.That(_particles.particleCount, Is.GreaterThan(0));
        }

        [Test]
        public void SimultaneousMeleeHitsOnOneTarget_AbsorbTheirCombinedDrop_ThenIndependentDamagePlays()
        {
            Absorb(10f);
            Absorb(8f);
            Drop(18f);
            Assert.That(_particles.isPlaying, Is.False);
            Drop(5f);
            Assert.That(_particles.isPlaying, Is.True);
        }

        [Test]
        public void MeleeColorOverride_IsOptionalAndReturnsConfiguredColor()
        {
            var method = typeof(CharacterDamageParticlePresenter).GetMethod("TryGetMeleeImpactColor");
            Assert.That(method, Is.Not.Null);
            var args = new object[] { default(Color) };
            Assert.That(method.Invoke(_presenter, args), Is.False);
            Color color = new Color(.82f, .82f, .85f, 1f);
            var so = new SerializedObject(_presenter);
            so.FindProperty("_overrideMeleeImpactColor").boolValue = true;
            so.FindProperty("_meleeImpactColor").colorValue = color;
            so.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(method.Invoke(_presenter, args), Is.True);
            Assert.That(args[0], Is.EqualTo(color));
        }

        [TestCase("Enemies/NetworkEnemy", true, .82f, .82f, .85f)]
        [TestCase("Enemies/NetworkEnemyRanged", true, .82f, .82f, .85f)]
        [TestCase("Enemies/Slimes/BlueSlime", true, .208f, .631f, .757f)]
        [TestCase("Enemies/Slimes/GreenSlime", true, .349f, .757f, .208f)]
        [TestCase("Enemies/Slimes/RedSlime", true, .757f, .208f, .369f)]
        [TestCase("NetworkPlayer", false, 1f, 1f, 1f)]
        public void Prefabs_ConfigureTargetColor_AndRangedInheritsSkeleton(string path, bool enabled, float r, float g, float b)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/{path}.prefab");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            try
            {
                var presenter = instance.GetComponentInChildren<CharacterDamageParticlePresenter>(true);
                var method = typeof(CharacterDamageParticlePresenter).GetMethod("TryGetMeleeImpactColor");
                Assert.That(method, Is.Not.Null);
                var args = new object[] { default(Color) };
                Assert.That(method.Invoke(presenter, args), Is.EqualTo(enabled));
                var so = new SerializedObject(presenter);
                if (enabled) Assert.That(args[0], Is.EqualTo(new Color(r, g, b, 1f)));
                if (path == "Enemies/NetworkEnemyRanged")
                {
                    Assert.That(so.FindProperty("_overrideMeleeImpactColor").prefabOverride, Is.False);
                    Assert.That(so.FindProperty("_meleeImpactColor").prefabOverride, Is.False);
                }
                if (path.Contains("Slimes/"))
                {
                    // Inspect the variant asset, not the scene-instance link to that variant.
                    var variant = new SerializedObject(prefab.GetComponentInChildren<CharacterDamageParticlePresenter>(true));
                    Assert.That(variant.FindProperty("_meleeImpactColor").prefabOverride, Is.True);
                }
            }
            finally { Object.DestroyImmediate(instance); }
        }

        private void Absorb(float appliedDamage = 10f)
        {
            var method = typeof(CharacterDamageParticlePresenter).GetMethod("AbsorbConfirmedMeleeImpact");
            Assert.That(method, Is.Not.Null, "Confirmed local melee must replace one nearby generic burst.");
            method.Invoke(_presenter, new object[] { appliedDamage });
        }

        // Exercise the Health-drop presentation handler without writing Fusion state in EditMode.
        private void Drop(float amount = 10f)
        {
            var method = typeof(CharacterDamageParticlePresenter).GetMethod("TriggerDamageParticles", Private);
            Assert.That(method, Is.Not.Null);
            method.Invoke(_presenter, new object[] { amount });
        }

        private void Set(string name, object value) => typeof(CharacterDamageParticlePresenter).GetField(name, Private).SetValue(_presenter, value);
    }
}
#endif
