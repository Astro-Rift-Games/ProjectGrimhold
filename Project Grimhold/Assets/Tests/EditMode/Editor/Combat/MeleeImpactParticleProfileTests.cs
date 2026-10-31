#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.EditMode.Combat
{
    public sealed class MeleeImpactParticleProfileTests
    {
        public static ScriptableObject Load(string weight) => AssetDatabase.LoadAssetAtPath<ScriptableObject>(
            $"Assets/Scriptable Objects/Combat/ImpactParticles/{weight}.asset");

        [TestCase("RondelDaggerWeaponDefinition", "Light")]
        [TestCase("MagicCinquedeaWeaponDefinition", "Light")]
        [TestCase("ArmingSwordWeaponDefinition", "Medium")]
        [TestCase("MagicSwordWeaponDefinition", "Medium")]
        [TestCase("RapierWeaponDefinition", "Medium")]
        [TestCase("SpellbookWeaponDefinition", "Medium")]
        [TestCase("LongSwordCombatDefinition", "Heavy")]
        [TestCase("ZweihanderWeaponDefinition", "Heavy")]
        [TestCase("GreatHammerWeaponDefinition", "Heavy")]
        public void Weapon_UsesItsWeightProfile(string name, string weight)
        {
            var weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>($"Assets/Scriptable Objects/Loot/Definitions/{name}.asset");
            var property = new SerializedObject(weapon).FindProperty("_presentation._impactParticles");
            Assert.That(property, Is.Not.Null, "Weapon presentation must expose impact particles.");
            Assert.That(Load(weight), Is.Not.Null);
            Assert.That(property.objectReferenceValue, Is.EqualTo(Load(weight)));
            Assert.That(weapon.TryValidate(out string error), Is.True, error);
        }

        // A viscous splatter: drops leave fast, decelerate with linear drag (v = v0 e^(-kt)) and spend most of
        // their life resting on the floor, not over the ~1 u body they started on.
        [TestCase("Light")]
        [TestCase("Medium")]
        [TestCase("Heavy")]
        public void Profile_SplatterLeavesTheBodyEarly_AndRestsOnTheFloorMostOfItsLife(string weight)
        {
            var profile = (MeleeImpactParticleProfile)Load(weight);
            var limit = profile.EmitterPrefab.limitVelocityOverLifetime;
            Assert.That(limit.enabled, Is.True, "Drag makes drops decelerate like a viscous splash.");
            Assert.That(limit.multiplyDragByParticleSize, Is.False);
            Assert.That(limit.multiplyDragByParticleVelocity, Is.False);
            float k = limit.drag.constant;
            Assert.That(k, Is.GreaterThan(0f));
            float v0 = profile.SpeedMin, life = profile.LifetimeMin;
            float settled = v0 / k * (1f - Mathf.Exp(-k * life));
            Assert.That(settled, Is.GreaterThanOrEqualTo(1.25f), $"{weight}: slowest drop must land at least 20 px away.");
            Assert.That(v0 / k, Is.GreaterThan(1f), $"{weight}: slowest drop must be able to leave the body.");
            float exitTime = -Mathf.Log(1f - k / v0) / k; // time to travel 1 u
            Assert.That(exitTime, Is.LessThanOrEqualTo(.4f * life), $"{weight}: drop must leave the body within 40% of its life.");
        }

        // Sparks must keep the target's authored color at full brightness in a dark, 2D-lit dungeon and stay
        // solid for most of their short life: an unlit material and an opaque alpha until the final fade.
        [Test]
        public void Emitter_IsUnlit_AndStaysOpaqueUntilItsFinalFade()
        {
            var profile = (MeleeImpactParticleProfile)Load("Medium");
            ParticleSystem emitter = profile.EmitterPrefab;
            var renderer = emitter.GetComponent<ParticleSystemRenderer>();
            Assert.That(renderer.sharedMaterial, Is.Not.Null);
            Assert.That(renderer.sharedMaterial.shader.name, Does.Contain("Unlit"),
                "2D lights must not darken the target-colored sparks.");
            var colorOverLifetime = emitter.colorOverLifetime;
            Gradient gradient = colorOverLifetime.color.gradient;
            for (float t = 0f; t <= .75f; t += .05f)
                Assert.That(gradient.Evaluate(t).a, Is.EqualTo(1f).Within(.001f), $"alpha at {t:0.00}");
            Assert.That(gradient.Evaluate(1f).a, Is.LessThan(.5f), "Sparks still fade out at the end.");
        }

        [Test]
        public void RangedWeapons_RetainNoImpactProfile_AndOptionalPresentationIsValid()
        {
            int ranged = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:WeaponDefinition"))
            {
                var weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (weapon.PrimaryAttack is not RangedAttackConfig) continue;
                ranged++;
                var property = new SerializedObject(weapon).FindProperty("_presentation._impactParticles");
                Assert.That(property, Is.Not.Null);
                Assert.That(property.objectReferenceValue, Is.Null, weapon.name);
            }
            Assert.That(ranged, Is.GreaterThan(0));
            WeaponDefinition.PresentationConfig presentation = default;
            Assert.That(presentation.TryValidate(out _), Is.True);
        }

        // Sparks start on the target's near surface over a ~1 unit body that is flashing damage red at the
        // same moment. Slow red sparks die inside it, camouflaged; every spark must leave the body and contrast.
        [TestCase("Light")]
        [TestCase("Medium")]
        [TestCase("Heavy")]
        public void Profile_SparksTravelPastTheBody_AndContrastWithTheDamageFlash(string weight)
        {
            var profile = (MeleeImpactParticleProfile)Load(weight);
            Assert.That(profile, Is.Not.Null, weight);
            Assert.That(profile.SpeedMin * profile.LifetimeMin, Is.GreaterThanOrEqualTo(.75f),
                $"{weight}: the shortest spark must travel at least 12 px (0.75 u).");
            Assert.That(Mathf.Min(profile.Color.g, profile.Color.b), Is.GreaterThanOrEqualTo(.6f),
                $"{weight}: spark color {profile.Color} must not be red-dominant like the damage flash.");
            Assert.That(profile.Color.a, Is.EqualTo(1f));
        }

        [TestCase("Light", 2, 2)]
        [TestCase("Medium", 2, 3)]
        [TestCase("Heavy", 3, 4)]
        public void Profiles_UseReadableWholePixelSizes(string weight, int min, int max)
        {
            var profile = (MeleeImpactParticleProfile)Load(weight);
            Assert.That(profile.SizePixelsMin, Is.EqualTo(min));
            Assert.That(profile.SizePixelsMax, Is.EqualTo(max));
        }

        [Test]
        public void Profiles_AreValid_IncreasingInIntensity_AndShareOneCrispWorldEmitter()
        {
            SerializedObject previous = null;
            Object prefab = null;
            foreach (string weight in new[] { "Light", "Medium", "Heavy" })
            {
                var profile = Load(weight);
                Assert.That(profile, Is.Not.Null, weight);
                var args = new object[] { null };
                Assert.That(profile.GetType().GetMethod("TryValidate").Invoke(profile, args), Is.True, args[0] as string);
                var current = new SerializedObject(profile);
                Assert.That(current.FindProperty("_pixelsPerUnit").floatValue, Is.EqualTo(16f));
                var emitter = (ParticleSystem)current.FindProperty("_emitterPrefab").objectReferenceValue;
                Assert.That(emitter, Is.Not.Null);
                if (prefab != null) Assert.That(emitter, Is.EqualTo(prefab));
                prefab = emitter;
                if (previous != null)
                {
                    foreach (string field in new[] { "_countMin", "_countMax", "_sizePixelsMin", "_sizePixelsMax" })
                        Assert.That(current.FindProperty(field).intValue, Is.GreaterThanOrEqualTo(previous.FindProperty(field).intValue), field);
                    foreach (string field in new[] { "_speedMin", "_speedMax", "_lifetimeMin", "_lifetimeMax" })
                        Assert.That(current.FindProperty(field).floatValue, Is.GreaterThan(previous.FindProperty(field).floatValue), field);
                    Assert.That(current.FindProperty("_countMin").intValue, Is.GreaterThan(previous.FindProperty("_countMin").intValue));
                }
                previous = current;
                Assert.That(emitter.main.simulationSpace, Is.EqualTo(ParticleSystemSimulationSpace.World));
                Assert.That(emitter.main.playOnAwake, Is.False);
                Assert.That(emitter.main.loop, Is.False);
                Assert.That(emitter.main.maxParticles, Is.EqualTo(256));
                Assert.That(emitter.emission.enabled, Is.False);
                Assert.That(emitter.main.stopAction, Is.EqualTo(ParticleSystemStopAction.None));
                Assert.That(emitter.colorOverLifetime.enabled, Is.True);
                var renderer = emitter.GetComponent<ParticleSystemRenderer>();
                Assert.That(renderer.renderMode, Is.EqualTo(ParticleSystemRenderMode.Billboard));
                Assert.That(renderer.sharedMaterial, Is.EqualTo(AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath("9dfc825aed78fcd4ba02077103263b40"))));
                var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/NetworkPlayer.prefab");
                foreach (var ps in player.GetComponentsInChildren<ParticleSystem>(true))
                {
                    if (ps.name != "DamageParticles") continue;
                    var generic = ps.GetComponent<ParticleSystemRenderer>();
                    Assert.That(renderer.sortingLayerID, Is.EqualTo(generic.sortingLayerID));
                    Assert.That(renderer.sortingOrder, Is.EqualTo(generic.sortingOrder));
                }
            }
        }

        [TestCase("_speedMin", -1f)]
        [TestCase("_speedMax", float.NaN)]
        [TestCase("_speedMax", float.PositiveInfinity)]
        [TestCase("_spreadDegrees", -1f)]
        [TestCase("_spreadDegrees", 361f)]
        [TestCase("_spreadDegrees", float.NaN)]
        [TestCase("_pixelsPerUnit", 0f)]
        [TestCase("_pixelsPerUnit", float.PositiveInfinity)]
        [TestCase("_lifetimeMin", 0f)]
        [TestCase("_lifetimeMin", -.1f)]
        [TestCase("_lifetimeMax", .61f)]
        [TestCase("_lifetimeMax", float.NaN)]
        public void Profile_RejectsInvalidFloat(string field, float value) => Invalid(field, value);

        [TestCase("_countMin", 0)]
        [TestCase("_countMin", 257)]
        [TestCase("_countMax", 257)]
        [TestCase("_countMax", 0)]
        [TestCase("_sizePixelsMin", 0)]
        [TestCase("_sizePixelsMax", 5)]
        [TestCase("_sizePixelsMax", 0)]
        public void Profile_RejectsInvalidInteger(string field, int value) => Invalid(field, value);

        [TestCase("_countMin", "_countMax")]
        [TestCase("_speedMin", "_speedMax")]
        [TestCase("_sizePixelsMin", "_sizePixelsMax")]
        [TestCase("_lifetimeMin", "_lifetimeMax")]
        public void Profile_RejectsReversedRange(string min, string max)
        {
            var profile = CloneProfile();
            try
            {
                var so = new SerializedObject(profile);
                var low = so.FindProperty(min);
                var high = so.FindProperty(max);
                if (low.propertyType == SerializedPropertyType.Integer) low.intValue = high.intValue + 1;
                else low.floatValue = high.floatValue + .01f;
                so.ApplyModifiedPropertiesWithoutUndo();
                AssertInvalid(profile);
            }
            finally { Object.DestroyImmediate(profile); }
        }

        [Test]
        public void Profile_RequiresPrefab_AndNormalizedColor()
        {
            var profile = CloneProfile();
            try
            {
                var so = new SerializedObject(profile);
                so.FindProperty("_color").colorValue = new Color(float.NaN, 1f, 1f, 1f);
                so.ApplyModifiedPropertiesWithoutUndo();
                AssertInvalid(profile);
                so.FindProperty("_color").colorValue = Color.white;
                so.FindProperty("_emitterPrefab").objectReferenceValue = null;
                so.ApplyModifiedPropertiesWithoutUndo();
                AssertInvalid(profile);
            }
            finally { Object.DestroyImmediate(profile); }
        }

        private static ScriptableObject CloneProfile()
        {
            Assert.That(Load("Light"), Is.Not.Null, "Light profile must exist.");
            return Object.Instantiate(Load("Light"));
        }

        private static void Invalid(string field, object value)
        {
            var profile = CloneProfile();
            try
            {
                var so = new SerializedObject(profile);
                if (value is int integer) so.FindProperty(field).intValue = integer;
                else so.FindProperty(field).floatValue = (float)value;
                so.ApplyModifiedPropertiesWithoutUndo();
                AssertInvalid(profile);
            }
            finally { Object.DestroyImmediate(profile); }
        }

        private static void AssertInvalid(ScriptableObject profile)
        {
            var args = new object[] { null };
            Assert.That(profile.GetType().GetMethod("TryValidate").Invoke(profile, args), Is.False);
            Assert.That(args[0], Is.Not.Null.And.Not.Empty);
        }
    }
}
#endif
