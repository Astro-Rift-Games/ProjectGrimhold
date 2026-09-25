using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class ArmingSwordDirectionalAnimationGeneratorTests
{
    private const string Hand = "RightHandPivot/RightHand";
    private const string Grip = Hand + "/MainHandGrip";
    private const string Root = "Assets/Animations/Weapons/Directional/ArmingSword/ArmingSword_Attack_";

    [TestCase("N", 180f)]
    [TestCase("NE", 135f)]
    [TestCase("NW", -135f)]
    [TestCase("S", 0f)]
    [TestCase("SE", 45f)]
    [TestCase("SW", -45f)]
    public void CreateClip_RotatesSouthTrajectoryAndAppliesDirectionalIdleState(string direction, float angle)
    {
        AnimationClip south = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Weapons/ArmingSword_Attack.anim");
        Assert.That(south, Is.Not.Null);
        AnimationClip result = ArmingSwordDirectionalAnimationGenerator.CreateClip(south, direction);
        try
        {
            float radians = angle * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);
            AnimationCurve sx = Curve(south, Hand, "m_LocalPosition.x");
            AnimationCurve sy = Curve(south, Hand, "m_LocalPosition.y");
            AnimationCurve rx = Curve(result, Hand, "m_LocalPosition.x");
            AnimationCurve ry = Curve(result, Hand, "m_LocalPosition.y");
            for (int i = 0; i < sx.length; i++)
            {
                Assert.That(rx.keys[i].time, Is.EqualTo(sx.keys[i].time));
                Assert.That(rx.keys[i].value, Is.EqualTo(cos * sx.keys[i].value - sin * sy.keys[i].value).Within(0.00001f));
                Assert.That(ry.keys[i].value, Is.EqualTo(sin * sx.keys[i].value + cos * sy.keys[i].value).Within(0.00001f));
                Assert.That(rx.keys[i].inTangent, Is.EqualTo(cos * sx.keys[i].inTangent - sin * sy.keys[i].inTangent).Within(0.00001f));
                Assert.That(ry.keys[i].outTangent, Is.EqualTo(sin * sx.keys[i].outTangent + cos * sy.keys[i].outTangent).Within(0.00001f));
                Assert.That(rx.keys[i].weightedMode, Is.EqualTo(sx.keys[i].weightedMode));
                Assert.That(ry.keys[i].inWeight, Is.EqualTo(sy.keys[i].inWeight));
                Assert.That(rx.keys[i].outWeight, Is.EqualTo(sx.keys[i].outWeight));
                if (i + 1 == sx.length) continue;
                for (int sample = 1; sample <= 3; sample++)
                {
                    float time = Mathf.Lerp(sx.keys[i].time, sx.keys[i + 1].time, sample / 4f);
                    Assert.That(rx.Evaluate(time), Is.EqualTo(cos * sx.Evaluate(time) - sin * sy.Evaluate(time)).Within(0.00002f));
                    Assert.That(ry.Evaluate(time), Is.EqualTo(sin * sx.Evaluate(time) + cos * sy.Evaluate(time)).Within(0.00002f));
                }
            }

            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(south)
                .Where(b => b.path != Grip && !(b.path == Hand && b.propertyName.StartsWith("m_LocalPosition.", StringComparison.Ordinal))))
            {
                Assert.That(AnimationUtility.GetEditorCurve(result, binding).keys,
                    Is.EqualTo(AnimationUtility.GetEditorCurve(south, binding).keys), binding.propertyName);
            }
            AnimationClip idle = AssetDatabase.LoadAssetAtPath<AnimationClip>($"Assets/Animations/Player/Idle/Idle_{direction}.anim");
            foreach (string axis in new[] { "x", "y", "z" })
            {
                string property = "m_LocalPosition." + axis;
                float expected = Curve(idle, Grip, property).Evaluate(0f);
                Assert.That(Curve(result, Grip, property).Evaluate(0f), Is.EqualTo(expected).Within(0.00001f));
                Assert.That(Curve(result, Grip, property).Evaluate(result.length), Is.EqualTo(expected).Within(0.00001f));
            }
            AnimationClip handIdle = AssetDatabase.LoadAssetAtPath<AnimationClip>($"Assets/Animations/Player/Idle/RightHand/RightHand_Idle_{direction}.anim");
            var sprite = new EditorCurveBinding { path = Hand, type = typeof(SpriteRenderer), propertyName = "m_Sprite" };
            Assert.That(AnimationUtility.GetObjectReferenceCurve(result, sprite).Single().value,
                Is.SameAs(AnimationUtility.GetObjectReferenceCurve(handIdle, sprite)[0].value));
            bool north = direction.StartsWith("N", StringComparison.Ordinal);
            AnimationCurve sorting = Curve(result, Hand, "m_SortingOrder");
            Assert.That(sorting == null, Is.EqualTo(!north));
            if (north) Assert.That(sorting.Evaluate(result.length), Is.EqualTo(-2f));
            Assert.That(Curve(south, Hand, "m_SortingOrder"), Is.Null, "Generating must not mutate south.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(result);
        }
    }

    [TestCase("N", 180f)]
    [TestCase("NE", 135f)]
    [TestCase("NW", -135f)]
    [TestCase("S", 0f)]
    [TestCase("SE", 45f)]
    [TestCase("SW", -45f)]
    public void GeneratedAsset_PersistsFloatAndSpriteBindings(string direction, float angle)
    {
        AnimationClip south = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Weapons/ArmingSword_Attack.anim");
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Root + direction + ".anim");
        Assert.That(south, Is.Not.Null);
        Assert.That(clip, Is.Not.Null);
        Assert.That(clip.length, Is.EqualTo(south.length).Within(0.00001f));
        Assert.That(AnimationUtility.GetAnimationClipSettings(clip).loopTime,
            Is.EqualTo(AnimationUtility.GetAnimationClipSettings(south).loopTime));

        float radians = angle * Mathf.Deg2Rad;
        float cos = Mathf.Cos(radians);
        float sin = Mathf.Sin(radians);
        AnimationCurve sx = Curve(south, Hand, "m_LocalPosition.x");
        AnimationCurve sy = Curve(south, Hand, "m_LocalPosition.y");
        AnimationCurve rx = Curve(clip, Hand, "m_LocalPosition.x");
        AnimationCurve ry = Curve(clip, Hand, "m_LocalPosition.y");
        Assert.That(rx, Is.Not.Null);
        Assert.That(ry, Is.Not.Null);
        Assert.That(rx.length, Is.EqualTo(sx.length));
        Assert.That(ry.length, Is.EqualTo(sy.length));
        for (int i = 0; i < sx.length; i++)
        {
            float time = sx.keys[i].time;
            Assert.That(ry.keys[i].time, Is.EqualTo(time));
            Assert.That(rx.keys[i].time, Is.EqualTo(time));
            Assert.That(rx.keys[i].value, Is.EqualTo(cos * sx.keys[i].value - sin * sy.keys[i].value).Within(0.00002f));
            Assert.That(ry.keys[i].value, Is.EqualTo(sin * sx.keys[i].value + cos * sy.keys[i].value).Within(0.00002f));
            Assert.That(rx.keys[i].inTangent, Is.EqualTo(cos * sx.keys[i].inTangent - sin * sy.keys[i].inTangent).Within(0.00002f));
            Assert.That(ry.keys[i].inTangent, Is.EqualTo(sin * sx.keys[i].inTangent + cos * sy.keys[i].inTangent).Within(0.00002f));
            Assert.That(rx.keys[i].outTangent, Is.EqualTo(cos * sx.keys[i].outTangent - sin * sy.keys[i].outTangent).Within(0.00002f));
            Assert.That(ry.keys[i].outTangent, Is.EqualTo(sin * sx.keys[i].outTangent + cos * sy.keys[i].outTangent).Within(0.00002f));
            for (int sample = 0; i + 1 < sx.length && sample < 4; sample++)
            {
                float t = Mathf.Lerp(time, sx.keys[i + 1].time, sample / 4f);
                Assert.That(rx.Evaluate(t), Is.EqualTo(cos * sx.Evaluate(t) - sin * sy.Evaluate(t)).Within(0.00002f));
                Assert.That(ry.Evaluate(t), Is.EqualTo(sin * sx.Evaluate(t) + cos * sy.Evaluate(t)).Within(0.00002f));
            }
        }
        Assert.That(rx.Evaluate(sx.keys[sx.length - 1].time), Is.EqualTo(cos * sx.Evaluate(south.length) - sin * sy.Evaluate(south.length)).Within(0.00002f));
        Assert.That(ry.Evaluate(sy.keys[sy.length - 1].time), Is.EqualTo(sin * sx.Evaluate(south.length) + cos * sy.Evaluate(south.length)).Within(0.00002f));

        AnimationClip idle = AssetDatabase.LoadAssetAtPath<AnimationClip>($"Assets/Animations/Player/Idle/Idle_{direction}.anim");
        Assert.That(idle, Is.Not.Null);
        foreach (string axis in new[] { "x", "y", "z" })
        {
            AnimationCurve grip = Curve(clip, Grip, "m_LocalPosition." + axis);
            Assert.That(grip, Is.Not.Null);
            float expected = Curve(idle, Grip, "m_LocalPosition." + axis).Evaluate(0f);
            Assert.That(grip.Evaluate(0f), Is.EqualTo(expected).Within(0.00002f));
            Assert.That(grip.Evaluate(clip.length), Is.EqualTo(expected).Within(0.00002f));
        }
        AnimationClip handIdle = AssetDatabase.LoadAssetAtPath<AnimationClip>($"Assets/Animations/Player/Idle/RightHand/RightHand_Idle_{direction}.anim");
        Assert.That(handIdle, Is.Not.Null);
        var sprite = new EditorCurveBinding { path = Hand, type = typeof(SpriteRenderer), propertyName = "m_Sprite" };
        Assert.That(AnimationUtility.GetObjectReferenceCurveBindings(clip).Any(binding => SameBinding(binding, sprite)), Is.True);
        ObjectReferenceKeyframe[] sprites = AnimationUtility.GetObjectReferenceCurve(clip, sprite);
        Assert.That(sprites, Has.Length.EqualTo(1));
        Assert.That(sprites[0].time, Is.EqualTo(0f));
        Assert.That(sprites[0].value, Is.SameAs(AnimationUtility.GetObjectReferenceCurve(handIdle, sprite)[0].value));
        AnimationCurve sorting = Curve(clip, Hand, "m_SortingOrder");
        Assert.That(sorting == null, Is.EqualTo(!direction.StartsWith("N", StringComparison.Ordinal)));
        if (sorting != null)
        {
            Assert.That(sorting.Evaluate(0f), Is.EqualTo(-2f));
            Assert.That(sorting.Evaluate(clip.length), Is.EqualTo(-2f));
        }
    }

    [Test]
    public void SouthRetainsEveryArtisticCurveFromOriginal()
    {
        AnimationClip original = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Weapons/ArmingSword_Attack.anim");
        AnimationClip south = AssetDatabase.LoadAssetAtPath<AnimationClip>(Root + "S.anim");
        Assert.That(AssetDatabase.GetAssetPath(original), Is.EqualTo("Assets/Animations/Weapons/ArmingSword_Attack.anim"));
        Assert.That(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(original)), Is.Not.Empty);
        foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(original))
        {
            AnimationCurve source = AnimationUtility.GetEditorCurve(original, binding);
            AnimationCurve actual = AnimationUtility.GetEditorCurve(south, binding);
            Assert.That(actual.keys, Is.EqualTo(source.keys), binding.propertyName);
            for (int i = 0; i + 1 < source.length; i++)
                Assert.That(actual.Evaluate((source.keys[i].time + source.keys[i + 1].time) / 2f),
                    Is.EqualTo(source.Evaluate((source.keys[i].time + source.keys[i + 1].time) / 2f)).Within(0.00001f));
        }
    }

    [Test]
    public void CreateClip_ReconstructsSouthWithoutReadingDestinationAndReflectsSourceEdits()
    {
        AnimationClip original = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Weapons/ArmingSword_Attack.anim");
        AnimationClip copy = UnityEngine.Object.Instantiate(original);
        var binding = new EditorCurveBinding { path = Hand, type = typeof(Transform), propertyName = "m_LocalPosition.x" };
        try
        {
            AnimationCurve changed = AnimationUtility.GetEditorCurve(copy, binding);
            Keyframe[] keys = changed.keys;
            keys[0].value += 0.125f;
            changed.keys = keys;
            AnimationUtility.SetEditorCurve(copy, binding, changed);
            AnimationClip generated = ArmingSwordDirectionalAnimationGenerator.CreateClip(copy, "S");
            try
            {
                Assert.That(Curve(generated, Hand, "m_LocalPosition.x").keys[0].value,
                    Is.EqualTo(keys[0].value).Within(0.00001f));
                Assert.That(Curve(original, Hand, "m_LocalPosition.x").keys[0].value,
                    Is.Not.EqualTo(keys[0].value));
            }
            finally { UnityEngine.Object.DestroyImmediate(generated); }
        }
        finally { UnityEngine.Object.DestroyImmediate(copy); }
    }

    [Test]
    public void CreateClip_RejectsUnknownDirectionWithoutChangingSource()
    {
        AnimationClip south = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Weapons/ArmingSword_Attack.anim");
        Assert.Throws<ArgumentException>(() => ArmingSwordDirectionalAnimationGenerator.CreateClip(south, "E"));
        Assert.That(Curve(south, Hand, "m_SortingOrder"), Is.Null);
    }

    [Test]
    public void Bake_RejectsOriginalAssetAsDestinationWithoutChangingIt()
    {
        const string sourcePath = "Assets/Animations/Weapons/ArmingSword_Attack.anim";
        AnimationClip original = AssetDatabase.LoadAssetAtPath<AnimationClip>(sourcePath);
        Assert.That(original, Is.Not.Null);
        string guid = AssetDatabase.AssetPathToGUID(sourcePath);
        float value = Curve(original, Hand, "m_LocalPosition.x").keys[0].value;

        Assert.Throws<ArgumentException>(() => ArmingSwordDirectionalAnimationGenerator.Bake(original, "S", sourcePath));
        Assert.That(AssetDatabase.AssetPathToGUID(sourcePath), Is.EqualTo(guid));
        Assert.That(Curve(original, Hand, "m_LocalPosition.x").keys[0].value, Is.EqualTo(value));
    }

    [TestCase("N")]
    [TestCase("NE")]
    [TestCase("NW")]
    [TestCase("S")]
    [TestCase("SE")]
    [TestCase("SW")]
    public void Bake_CreatesImportedBindingsAndPreservesGuidOnRepeat(string direction)
    {
        const string folder = "Assets/Tests/EditMode/Editor/Player/Presentation/ArmingSwordDirectionalGenerationScratch";
        string path = folder + "/ArmingSword_Attack_" + direction + ".anim";
        AnimationClip original = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Weapons/ArmingSword_Attack.anim");
        try
        {
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets/Tests/EditMode/Editor/Player/Presentation", "ArmingSwordDirectionalGenerationScratch");
            Assert.That(AssetDatabase.LoadAssetAtPath<AnimationClip>(path), Is.Null);
            ArmingSwordDirectionalAnimationGenerator.Bake(original, direction, path);
            string guid = AssetDatabase.AssetPathToGUID(path);
            Assert.That(guid, Is.Not.Empty);
            AssertImportedBindings(path, direction);
            ArmingSwordDirectionalAnimationGenerator.Bake(original, direction, path);
            Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guid));
            AssertImportedBindings(path, direction);
        }
        finally
        {
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.DeleteAsset(folder);
        }
    }

    [Test]
    public void Bake_RecreatesDeletedSouthAndPersistsEditedSourceWithoutMutatingOriginal()
    {
        const string folder = "Assets/Tests/EditMode/Editor/Player/Presentation/ArmingSwordDirectionalGenerationScratch";
        string path = folder + "/ArmingSword_Attack_S.anim";
        AnimationClip original = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Weapons/ArmingSword_Attack.anim");
        AnimationClip copy = UnityEngine.Object.Instantiate(original);
        var binding = new EditorCurveBinding { path = Hand, type = typeof(Transform), propertyName = "m_LocalPosition.x" };
        float originalValue = AnimationUtility.GetEditorCurve(original, binding).keys[0].value;
        try
        {
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets/Tests/EditMode/Editor/Player/Presentation", "ArmingSwordDirectionalGenerationScratch");
            ArmingSwordDirectionalAnimationGenerator.Bake(original, "S", path);
            Assert.That(AssetDatabase.DeleteAsset(path), Is.True);
            Assert.That(AssetDatabase.LoadAssetAtPath<AnimationClip>(path), Is.Null);
            ArmingSwordDirectionalAnimationGenerator.Bake(original, "S", path);
            AssertImportedBindings(path, "S");
            float runtimeSouthValue = Curve(AssetDatabase.LoadAssetAtPath<AnimationClip>(Root + "S.anim"), Hand, "m_LocalPosition.x").keys[0].value;
            AnimationCurve changed = AnimationUtility.GetEditorCurve(copy, binding);
            Keyframe[] keys = changed.keys;
            keys[0].value += 0.125f;
            changed.keys = keys;
            AnimationUtility.SetEditorCurve(copy, binding, changed);
            ArmingSwordDirectionalAnimationGenerator.Bake(copy, "S", path);
            AnimationClip imported = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            Assert.That(Curve(imported, Hand, "m_LocalPosition.x").keys[0].value, Is.EqualTo(keys[0].value).Within(0.00001f));
            Assert.That(Curve(original, Hand, "m_LocalPosition.x").keys[0].value, Is.EqualTo(originalValue));
            Assert.That(Curve(AssetDatabase.LoadAssetAtPath<AnimationClip>(Root + "S.anim"), Hand, "m_LocalPosition.x").keys[0].value,
                Is.EqualTo(runtimeSouthValue));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(copy);
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.DeleteAsset(folder);
        }
    }

    private static void AssertImportedBindings(string path, string direction)
    {
        AnimationClip imported = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        Assert.That(imported, Is.Not.Null);
        foreach (string axis in new[] { "x", "y", "z" })
        {
            Assert.That(Curve(imported, Hand, "m_LocalPosition." + axis), Is.Not.Null);
            Assert.That(Curve(imported, Grip, "m_LocalPosition." + axis), Is.Not.Null);
        }
        var sprite = new EditorCurveBinding { path = Hand, type = typeof(SpriteRenderer), propertyName = "m_Sprite" };
        Assert.That(AnimationUtility.GetObjectReferenceCurve(imported, sprite), Has.Length.EqualTo(1));
        Assert.That(Curve(imported, Hand, "m_SortingOrder") == null,
            Is.EqualTo(!direction.StartsWith("N", StringComparison.Ordinal)));
    }

    private static bool SameBinding(EditorCurveBinding actual, EditorCurveBinding expected) =>
        actual.path == expected.path && actual.type == expected.type && actual.propertyName == expected.propertyName;

    private static AnimationCurve Curve(AnimationClip clip, string path, string property) =>
        AnimationUtility.GetEditorCurve(clip,
            new EditorCurveBinding { path = path, type = property == "m_SortingOrder" ? typeof(SpriteRenderer) : typeof(Transform), propertyName = property });
}
