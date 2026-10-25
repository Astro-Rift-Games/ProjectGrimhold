#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.EditMode.Combat
{
    public sealed class OffHandMirroredStrokeTests
    {
        private const string Sets = "Assets/Scriptable Objects/Loot/AttackAnimationSets/";
        private const string Definitions = "Assets/Scriptable Objects/Loot/Definitions/";
        private static readonly string[] Directions = { "N", "NE", "NW", "S", "SE", "SW" };

        // Reflection deliberately keeps this RED suite compilable before the authored contract exists.
        private static SerializedProperty Stroke(SerializedObject data)
        {
            var property = data.FindProperty("_offHandStroke");
            Assert.That(property, Is.Not.Null, "Add the authored _offHandStroke enum (Reanchored = 0, Mirrored = 1).");
            Assert.That(property.propertyType, Is.EqualTo(SerializedPropertyType.Enum));
            return property;
        }

        private static void SetStroke(DirectionalAttackAnimationSet set, bool mirrored)
        {
            using var data = new SerializedObject(set);
            var property = Stroke(data);
            Assert.That(property.enumNames, Is.EqualTo(new[] { "Reanchored", "Mirrored" }));
            property.enumValueIndex = mirrored ? 1 : 0;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static int ReadStroke(DirectionalAttackAnimationSet set)
        {
            using var data = new SerializedObject(set);
            return Stroke(data).enumValueIndex;
        }

        private static AnimationClip Derive(AnimationClip source, int direction, bool mirrored, out Vector2 offset)
        {
            var method = typeof(OffHandAnimationGenerator).GetMethods(BindingFlags.Static | BindingFlags.Public)
                .SingleOrDefault(m => m.Name == "CreateClip" && m.GetParameters().Length == 4 &&
                    m.GetParameters()[2].ParameterType.IsEnum && m.GetParameters()[3].IsOut);
            Assert.That(method, Is.Not.Null, "Expose CreateClip(source, direction, OffHandStroke, out originOffset).");
            var mode = Enum.Parse(method.GetParameters()[2].ParameterType, mirrored ? "Mirrored" : "Reanchored");
            object[] args = { source, direction, mode, Vector2.zero };
            var set = AssetDatabase.FindAssets("t:DirectionalAttackAnimationSet")
                .Select(g => AssetDatabase.LoadAssetAtPath<DirectionalAttackAnimationSet>(AssetDatabase.GUIDToAssetPath(g)))
                .FirstOrDefault(s => s.CanonicalSlash && Enumerable.Range(0, 6).Any(i => s.GetAttackClip(i) == source));
            var clip = set != null ? OffHandAnimationGenerator.CreateClip(source, direction,
                mirrored ? OffHandStroke.Mirrored : OffHandStroke.Reanchored, out _, set) : (AnimationClip)method.Invoke(null, args);
            if (set != null)
            {
                using var neutral = new AttackPreviewRig(); Neutral(neutral.VisualRoot, direction);
                args[3] = (Vector2)(Local(neutral, neutral.OffHandGrip.position) - Local(neutral, neutral.MainHandGrip.position));
            }
            offset = (Vector2)args[3];
            return clip;
        }

        [Test]
        public void NewSet_DefaultsToReanchored()
        {
            var set = ScriptableObject.CreateInstance<DirectionalAttackAnimationSet>();
            try { Assert.That(ReadStroke(set), Is.Zero); }
            finally { Object.DestroyImmediate(set); }
        }

        [Test]
        public void SourceSignature_ChangesWhenOnlyStrokeChanges()
        {
            var set = Object.Instantiate(AssetDatabase.LoadAssetAtPath<DirectionalAttackAnimationSet>(Sets + "Sword1H.asset"));
            try
            {
                var method = typeof(OffHandAnimationGenerator).GetMethod("SourceSignature", BindingFlags.NonPublic | BindingFlags.Static);
                Assert.That(method, Is.Not.Null);
                SetStroke(set, false);
                string before = (string)method.Invoke(null, new object[] { set });
                SetStroke(set, true);
                Assert.That((string)method.Invoke(null, new object[] { set }), Is.Not.EqualTo(before),
                    "Hash the authored value explicitly, including unsaved in-memory changes.");
            }
            finally { Object.DestroyImmediate(set); }
        }

        [TestCase("ArmingSword", 1)]
        [TestCase("MagicSword", 1)]
        [TestCase("Rapier", 0)]
        [TestCase("RondelDagger", 0)]
        [TestCase("MagicCinquedea", 0)]
        [TestCase("MagicWand", 0)]
        public void AuthoredCatalog_UsesExpectedStrokeAndDerivedSetCopiesIt(string name, int expected)
        {
            var weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(Definitions + name + "WeaponDefinition.asset");
            Assert.That(ReadStroke(weapon.Presentation.AttackAnimationSet), Is.EqualTo(expected));
            Assert.That(ReadStroke(weapon.Presentation.OffHandAttackAnimationSet), Is.EqualTo(expected));
        }

        [Test]
        public void MirroredFreeAim_IsRejectedByWeaponValidation()
        {
            using var fixture = new Fixture("MagicWand", true);
            Assert.That(fixture.Weapon.TryValidate(out string error), Is.False);
            Assert.That(error, Does.Contain("mirrored").IgnoreCase.And.Contain("free aim").IgnoreCase);
        }

        [Test]
        public void MirroredRotation_PreservesSourceCubicKeysTangentsAndBetweenKeyEasing()
        {
            var set = AssetDatabase.LoadAssetAtPath<DirectionalAttackAnimationSet>(Sets + "Sword1H.asset");
            AnimationClip source = set.GetAttackClip(0);
            AnimationClip derived = Derive(source, 0, true, out _);
            try
            {
                var binding = EditorCurveBinding.FloatCurve("RightHandPivot/RightHand", typeof(Transform), "localEulerAnglesRaw.z");
                var original = AnimationUtility.GetEditorCurve(source, binding);
                binding.path = "LeftHandPivot/LeftHand";
                var reflected = AnimationUtility.GetEditorCurve(derived, binding);
                Assert.That(reflected, Is.Not.Null, "Keep the source's Euler interpolation rather than linear quaternion resampling.");
                Assert.That(reflected.length, Is.EqualTo(original.length));
                for (int i = 0; i < original.length; i++)
                {
                    Assert.That(reflected.keys[i].time, Is.EqualTo(original.keys[i].time));
                    Assert.That(reflected.keys[i].inTangent, Is.EqualTo(-original.keys[i].inTangent));
                    Assert.That(reflected.keys[i].outTangent, Is.EqualTo(-original.keys[i].outTangent));
                    Assert.That(reflected.keys[i].weightedMode, Is.EqualTo(original.keys[i].weightedMode));
                    Assert.That(reflected.keys[i].inWeight, Is.EqualTo(original.keys[i].inWeight));
                    Assert.That(reflected.keys[i].outWeight, Is.EqualTo(original.keys[i].outWeight));
                }
                foreach (float t in new[] { 0.005f, 0.00708f, 0.019f, 0.071f, 0.217f })
                    Assert.That(reflected.Evaluate(t) - reflected.Evaluate(0f),
                        Is.EqualTo(-(original.Evaluate(t) - original.Evaluate(0f))).Within(0.0001f), $"at {t}");
            }
            finally { Object.DestroyImmediate(derived); }
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        public void MirroredClip_ReflectsGripAndRotationPreservesScaleSpriteTimingAndIsIdempotent(int direction)
        {
            using var fixture = new Fixture("ArmingSword", true);
            using var main = new AttackPreviewRig();
            using var off = new AttackPreviewRig();
            Neutral(main.VisualRoot, direction);
            Neutral(off.VisualRoot, direction);
            Vector3 mn = Local(main, main.MainHandGrip.position), on = Local(off, off.OffHandGrip.position);
            Quaternion mr = main.MainHandGrip.parent.localRotation, or = off.OffHandGrip.parent.localRotation;
            Vector3 ms = main.MainHandGrip.parent.localScale, os = off.OffHandGrip.parent.localScale;
            Sprite sprite = off.HandRenderers[1].sprite;
            var source = fixture.Weapon.Presentation.GetAttackClip(direction);
            var clip = fixture.Weapon.Presentation.GetOffHandAttackClip(direction);
            var again = Derive(source, direction, true, out Vector2 offset);
            try
            {
                Assert.That(EditorJsonUtility.ToJson(again), Is.EqualTo(EditorJsonUtility.ToJson(clip)));
                Assert.That(Vector2.Distance(offset, on - mn), Is.LessThan(0.0001f));
                Assert.That(clip.length, Is.EqualTo(source.length).Within(0.0001f));
                // Includes the exact gameplay release, endpoints, and sub-frame interpolation samples.
                var times = Enumerable.Range(0, 121).Select(i => source.length * i / 120f)
                    .Append(fixture.Weapon.AttackReleaseSeconds);
                foreach (float t in times)
                {
                    Neutral(main.VisualRoot, direction); Neutral(off.VisualRoot, direction);
                    source.SampleAnimation(main.VisualRoot.gameObject, t);
                    clip.SampleAnimation(off.VisualRoot.gameObject, t);
                    AssertVector(Local(off, off.OffHandGrip.position),
                        OffPoint(Local(main, main.MainHandGrip.position), mn, on, true, direction, t,
                            fixture.Weapon.Presentation.AttackAnimationSet), $"grip {direction}/{t}");
                    Quaternion delta = main.MainHandGrip.parent.localRotation * Quaternion.Inverse(mr);
                    Quaternion reflected = new Quaternion(delta.x, -delta.y, -delta.z, delta.w);
                    Assert.That(Quaternion.Angle(off.OffHandGrip.parent.localRotation * Quaternion.Inverse(or), reflected),
                        Is.LessThan(0.1f), $"rotation {direction}/{t}");
                    Vector3 scale = main.MainHandGrip.parent.localScale;
                    AssertVector(off.OffHandGrip.parent.localScale,
                        Vector3.Scale(os, new Vector3(scale.x / ms.x, scale.y / ms.y, scale.z / ms.z)), "scale");
                    Assert.That(off.HandRenderers[1].sprite, Is.EqualTo(sprite));
                }
            }
            finally { Object.DestroyImmediate(again); }
        }

        [TestCase("ArmingSword", true)] [TestCase("MagicSword", true)]
        [TestCase("Rapier", false)] [TestCase("RondelDagger", false)]
        public void RealPrefab_BladeTipAndVfxFollowTheStrokeContract(string name, bool mirrored)
        {
            using var fixture = new Fixture(name, mirrored);
            using var main = new AttackPreviewRig();
            using var off = new AttackPreviewRig();
            for (int direction = 0; direction < 6; direction++)
            {
                Neutral(main.VisualRoot, direction); Neutral(off.VisualRoot, direction);
                Vector3 mn = Local(main, main.MainHandGrip.position), on = Local(off, off.OffHandGrip.position);
                main.Configure(new AttackPreviewCombination(fixture.Loot, null, AttackPreviewAction.MainAttack, direction));
                off.Configure(new AttackPreviewCombination(null, fixture.Loot, AttackPreviewAction.OffAttack, direction));
                bool sawVfx = false;
                float end = Mathf.Max(main.Duration, off.Duration);
                float clipEnd = fixture.Weapon.Presentation.GetAttackClip(direction).length;
                for (int frame = 0; frame <= 120; frame++)
                {
                    float t = end * frame / 120f;
                    main.Evaluate(t); off.Evaluate(t);
                    if (t < clipEnd)
                    {
                        Vector3 tip = BladeTip(main, fixture.Weapon, 0);
                        AssertVector(BladeTip(off, fixture.Weapon, 1), OffPoint(tip, mn, on, mirrored, direction, t,
                            fixture.Weapon.Presentation.AttackAnimationSet),
                            $"blade {name}/{direction}/{t}");
                    }
                    var mv = main.AttackVfxRenderers[0];
                    if (!mv.enabled || mv.sprite == null) continue;
                    sawVfx = true;
                    Assert.That(off.AttackVfxRenderers.Count, Is.EqualTo(2));
                    var ov = off.AttackVfxRenderers[1];
                    Assert.That(ov.enabled, Is.True);
                    Assert.That(ov.sprite, Is.EqualTo(mv.sprite));
                    AssertVector(Local(off, ov.transform.position), fixture.Weapon.Presentation.AttackAnimationSet.CanonicalSlash
                        ? Reflect(Local(main, mv.transform.position), mirrored, direction)
                        : on + Reflect(Local(main, mv.transform.position) - mn, mirrored, direction), "VFX position");
                    Vector3 axis = main.VisualRoot.InverseTransformDirection(mv.transform.right);
                    AssertVector(off.VisualRoot.InverseTransformDirection(ov.transform.right), Reflect(axis, mirrored, direction), "VFX rotation");
                    Assert.That(ov.transform.localScale.y, Is.EqualTo(mv.transform.localScale.y * (mirrored ? -1 : 1)).Within(0.001f), "VFX Mirrored flag");
                }
                Assert.That(sawVfx, Is.True, $"VFX was never exercised: {name}/{direction}");
            }
        }

        private static Vector3 BladeTip(AttackPreviewRig rig, WeaponDefinition weapon, int hand)
        {
            // The authored grip-to-tip endpoint, not the sprite's transparent rectangle.
            using var data = new SerializedObject(weapon);
            Vector3 tip = data.FindProperty("_presentation").FindPropertyRelative("_bladeTip").vector2Value;
            return Local(rig, rig.WeaponRenderers[hand].transform.TransformPoint(tip));
        }

        private static Vector3 Local(AttackPreviewRig rig, Vector3 point) => rig.VisualRoot.InverseTransformPoint(point);
        // Canonical slash mirrors about the facing through the body, not screen X or a grip anchor.
        private static Vector3 Reflect(Vector3 v, bool mirror, int direction) => mirror
            ? PlayerWeaponPresentationMath.ReflectAcrossFacing(v, direction) : v;
        private static Vector3 OffPoint(Vector3 main, Vector3 mn, Vector3 on, bool mirror, int direction,
            float seconds, DirectionalAttackAnimationSet set)
        {
            if (!set.CanonicalSlash) return on + Reflect(main - mn, mirror, direction);
            Vector2 window = set.SlashStrokeWindow;
            float weight = seconds < window.x ? Mathf.SmoothStep(0, 1, seconds / window.x) :
                seconds > window.y ? 1 - Mathf.SmoothStep(0, 1, (seconds - window.y) / (set.SlashRecoveryEndSeconds - window.y)) : 1;
            return Reflect(main, mirror, direction) + (on - Reflect(mn, mirror, direction)) * (1 - weight);
        }
        private static void AssertVector(Vector3 actual, Vector3 expected, string context) =>
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(0.002f), context);

        private static void Neutral(Transform root, int direction)
        {
            foreach (string hand in new[] { "RightHandPivot/RightHand", "LeftHandPivot/LeftHand" })
            {
                var node = root.Find(hand);
                node.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity); node.localScale = Vector3.one;
                var grip = node.Find(hand.StartsWith("Right", StringComparison.Ordinal) ? "MainHandGrip" : "OffHandGrip");
                grip.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity); grip.localScale = Vector3.one;
            }
            string facing = Directions[direction];
            foreach (string path in new[] { $"Assets/Animations/Player/Idle/Idle_{facing}.anim",
                $"Assets/Animations/Player/Idle/RightHand/RightHand_Idle_{facing}.anim",
                $"Assets/Animations/Player/Idle/LeftHand/LeftHand_Idle_{facing}.anim" })
                AssetDatabase.LoadAssetAtPath<AnimationClip>(path).SampleAnimation(root.gameObject, 0f);
        }

        private sealed class Fixture : IDisposable
        {
            private readonly List<Object> _owned = new List<Object>();
            public LootDefinition Loot { get; }
            public WeaponDefinition Weapon { get; }
            public Fixture(string name, bool mirrored)
            {
                try
                {
                    Loot = Own(Object.Instantiate(AssetDatabase.LoadAssetAtPath<LootDefinition>(Definitions + name + ".asset")));
                    Weapon = Own(Object.Instantiate(Loot.WeaponDefinition));
                    var source = Own(Object.Instantiate(Weapon.Presentation.AttackAnimationSet));
                    SetStroke(source, mirrored);
                    var derived = Own(ScriptableObject.CreateInstance<DirectionalAttackAnimationSet>());
                    var offsets = new Vector2[6];
                    var clips = new AnimationClip[6];
                    for (int i = 0; i < 6; i++)
                        clips[i] = Own(Derive(source.GetAttackClip(i), i, mirrored, out offsets[i]));
                    OffHandAnimationGenerator.ConfigureDerivedSet(source, derived, clips, offsets);
                    Assert.That(ReadStroke(derived), Is.EqualTo(ReadStroke(source)), "Generation must copy the source stroke.");
                    Assert.That(derived.HasMirrorMetadata, Is.True);
                    using (var data = new SerializedObject(Weapon))
                    {
                        var p = data.FindProperty("_presentation");
                        p.FindPropertyRelative("_attackAnimationSet").objectReferenceValue = source;
                        p.FindPropertyRelative("_offHandAttackAnimationSet").objectReferenceValue = derived;
                        data.ApplyModifiedPropertiesWithoutUndo();
                    }
                    using (var data = new SerializedObject(Loot))
                    {
                        data.FindProperty("_weaponDefinition").objectReferenceValue = Weapon;
                        data.ApplyModifiedPropertiesWithoutUndo();
                    }
                }
                catch { Dispose(); throw; }
            }
            private T Own<T>(T value) where T : Object { _owned.Add(value); return value; }
            public void Dispose() { for (int i = _owned.Count - 1; i >= 0; i--) Object.DestroyImmediate(_owned[i]); _owned.Clear(); }
        }
    }
}
#endif
