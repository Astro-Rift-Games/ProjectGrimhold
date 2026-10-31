#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Combat
{
    public sealed class MeleeHitReactionProfileTests
    {
        [Test]
        public void Catalog_OnlyMeleeWeaponsReferenceReactionProfiles()
        {
            int melee = 0, ranged = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:WeaponDefinition"))
            {
                var weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                var property = new SerializedObject(weapon).FindProperty("_presentation._hitReaction");
                Assert.That(property, Is.Not.Null, "Weapon presentation must expose an optional melee reaction.");
                if (weapon.PrimaryAttack is MeleeAttackConfig)
                {
                    melee++;
                    Assert.That(property.objectReferenceValue, Is.Not.Null, weapon.name);
                }
                else if (weapon.PrimaryAttack is RangedAttackConfig)
                {
                    ranged++;
                    Assert.That(property.objectReferenceValue, Is.Null, weapon.name);
                }
            }
            Assert.That(melee, Is.GreaterThanOrEqualTo(9));
            Assert.That(ranged, Is.GreaterThan(0));
        }

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
            Assert.That(weapon.Presentation.HitReaction, Is.EqualTo(Load(weight)));
        }

        [Test]
        public void Profiles_UseSupportedWhiteUnlitMaterial_AndHeavierFlashLastsAtLeastAsLong()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Art/Shaders/HitFlashSprite.shader");
            Assert.That(shader, Is.Not.Null);
            Assert.That(shader.isSupported, Is.True);
            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False);
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Shaders/HitFlash.mat");
            Assert.That(material, Is.Not.Null);
            Assert.That(material.shader, Is.EqualTo(shader));
            Color hdr = material.GetColor("_FlashColor");
            Assert.That(hdr.r, Is.GreaterThan(1f));
            Assert.That(hdr.g, Is.EqualTo(hdr.r));
            Assert.That(hdr.b, Is.EqualTo(hdr.r));
            Assert.That(hdr.a, Is.EqualTo(1f));
            float previous = 0f;
            foreach (string weight in new[] { "Light", "Medium", "Heavy" })
            {
                var profile = Load(weight);
                var property = new SerializedObject(profile).FindProperty("_flashMaterial");
                Assert.That(property, Is.Not.Null);
                Assert.That(property.objectReferenceValue, Is.EqualTo(material));
                Assert.That(profile.FlashColor, Is.EqualTo(Color.white));
                var go = new GameObject("Flash duration fixture");
                try
                {
                    var renderer = go.AddComponent<SpriteRenderer>();
                    var playback = new HitReactionPlayback(go.transform, new[] { renderer });
                    playback.Play(profile, Vector2.right, 0f, .1f);
                    float duration = 0f;
                    while (playback.IsActive)
                    {
                        if (renderer.sharedMaterial == material) duration += .001f;
                        playback.Tick(.001f);
                    }
                    Assert.That(duration, Is.GreaterThanOrEqualTo(previous), weight);
                    previous = duration;
                }
                finally { Object.DestroyImmediate(go); }
            }
        }

        [Test]
        public void Profiles_AreValid_AndIntensityIncreasesFromLightToHeavy()
        {
            var profiles = new[] { Load("Light"), Load("Medium"), Load("Heavy") };
            for (int i = 0; i < profiles.Length; i++)
            {
                var current = profiles[i];
                Assert.That(current, Is.Not.Null);
                Assert.That(current.TryValidate(out string error), Is.True, error);
                Assert.That(current.PixelsPerUnit, Is.EqualTo(16f));
                if (i == 0) continue;
                var previous = profiles[i - 1];
                Assert.That(current.FlashStrength, Is.GreaterThanOrEqualTo(previous.FlashStrength));
                Assert.That(current.Compression, Is.GreaterThan(previous.Compression));
                Assert.That(current.StretchRatio, Is.GreaterThanOrEqualTo(previous.StretchRatio));
                Assert.That(current.NudgePixels, Is.GreaterThanOrEqualTo(previous.NudgePixels));
                Assert.That(current.RecoverySeconds, Is.GreaterThan(previous.RecoverySeconds));
                Assert.That(current.FlashColor, Is.EqualTo(previous.FlashColor));
            }
        }

        [TestCase("_flashStrength", -.1f)]
        [TestCase("_flashStrength", 1.1f)]
        [TestCase("_compression", -.01f)]
        [TestCase("_compression", .26f)]
        [TestCase("_compression", float.NaN)]
        [TestCase("_stretchRatio", -.01f)]
        [TestCase("_stretchRatio", 1.01f)]
        [TestCase("_pixelsPerUnit", 0f)]
        [TestCase("_pixelsPerUnit", float.PositiveInfinity)]
        [TestCase("_recoverySeconds", 0f)]
        [TestCase("_recoverySeconds", -.1f)]
        [TestCase("_recoverySeconds", float.NaN)]
        public void Profile_RejectsInvalidValues(string field, float value)
        {
            var profile = ScriptableObject.CreateInstance<MeleeHitReactionProfile>();
            try
            {
                var serialized = new SerializedObject(profile);
                serialized.FindProperty(field).floatValue = value;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(profile.TryValidate(out string error), Is.False);
                Assert.That(error, Is.Not.Empty);
            }
            finally { Object.DestroyImmediate(profile); }
        }

        [TestCase(-1)]
        [TestCase(5)]
        public void Profile_RejectsInvalidPixelCount(int pixels)
        {
            var profile = ScriptableObject.CreateInstance<MeleeHitReactionProfile>();
            try
            {
                var serialized = new SerializedObject(profile);
                serialized.FindProperty("_nudgePixels").intValue = pixels;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(profile.TryValidate(out _), Is.False);
            }
            finally { Object.DestroyImmediate(profile); }
        }

        [Test]
        public void Profile_AllowsBoundaries_AndOptionalWeaponReferenceRemainsValid()
        {
            var profile = ScriptableObject.CreateInstance<MeleeHitReactionProfile>();
            try
            {
                Assert.That(profile.TryValidate(out _), Is.True);
                var serialized = new SerializedObject(profile);
                foreach (float boundary in new[] { 0f, 1f })
                {
                    serialized.FindProperty("_flashStrength").floatValue = boundary;
                    serialized.FindProperty("_compression").floatValue = boundary * .25f;
                    serialized.FindProperty("_stretchRatio").floatValue = boundary;
                    serialized.FindProperty("_nudgePixels").intValue = (int)(boundary * 4);
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    Assert.That(profile.TryValidate(out _), Is.True);
                }
                WeaponDefinition.PresentationConfig presentation = default;
                Assert.That(presentation.HitReaction, Is.Null);
                Assert.That(presentation.TryValidate(out _), Is.True);
            }
            finally { Object.DestroyImmediate(profile); }
        }

        private static MeleeHitReactionProfile Load(string weight) =>
            AssetDatabase.LoadAssetAtPath<MeleeHitReactionProfile>($"Assets/Scriptable Objects/Combat/HitReactions/{weight}.asset");

        [Test]
        public void LegacyCombatFeedback_HasZeroDirection()
        {
            var feedback = new CombatPresentationEvent(1, CombatFeedbackKind.ConfirmedImpact,
                default, Vector2.one, 5f, 1, AttackFailureReason.None);
            var direction = typeof(CombatPresentationEvent).GetProperty("HitDirection");
            Assert.That(direction, Is.Not.Null, "Confirmed feedback must carry the resolved hit direction.");
            Assert.That(direction.GetValue(feedback), Is.EqualTo(Vector2.zero));
        }
    }
}
#endif
