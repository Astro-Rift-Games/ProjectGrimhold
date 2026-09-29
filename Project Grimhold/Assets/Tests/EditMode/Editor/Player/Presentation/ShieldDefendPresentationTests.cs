using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class ShieldDefendPresentationTests
{
    private const string OffHand = "LeftHandPivot/LeftHand";
    private const string OffHandGrip = OffHand + "/OffHandGrip";
    private const string SourcePath = "Assets/Animations/Weapons/Shield_Block.anim";
    private const string OutputRoot = "Assets/Animations/Weapons/Directional/Shield/Shield_Defend_";
    private const string SpriteSetPath = "Assets/Scriptable Objects/Loot/ShieldSpriteSets/Shield.asset";
    private const string SpriteRoot = "Assets/Art/Weapons/Shield/Shield-";
    private static readonly string[] Directions = { "N", "NE", "NW", "S", "SE", "SW" };
    // Each facing's turn from the south source, matching the Main Hand attack bake.
    private static readonly float[] Angles = { 180f, 135f, -135f, 0f, 45f, -45f };

    [Test]
    public void Generation_LeavesTheAuthoredSouthSourceUntouched()
    {
        byte[] before = File.ReadAllBytes(SourcePath);
        DirectionalAnimationGenerator.GenerateShieldDefendAssets();

        Assert.That(File.ReadAllBytes(SourcePath), Is.EqualTo(before));
        AnimationClip source = Source();
        Assert.That(source.isLooping, Is.True);
        Assert.That(source.length, Is.EqualTo(0.4f).Within(0.0001f));
        Assert.That(source.frameRate, Is.EqualTo(20f));
        Assert.That(AnimationUtility.GetCurveBindings(source).Select(binding => binding.path), Is.All.EqualTo(OffHand));
    }

    [Test]
    public void Generation_ProducesSixCompleteOffHandClips()
    {
        DirectionalAnimationGenerator.GenerateShieldDefendAssets();
        EditorCurveBinding[] expectedBindings = AnimationUtility.GetCurveBindings(Source())
            .Concat(new[] { "x", "y", "z" }.Select(axis =>
                EditorCurveBinding.FloatCurve(OffHandGrip, typeof(Transform), "m_LocalPosition." + axis)))
            .Append(EditorCurveBinding.FloatCurve(OffHand, typeof(SpriteRenderer), "m_SortingOrder"))
            .ToArray();

        foreach (string direction in Directions)
        {
            AnimationClip clip = Output(direction);
            Assert.That(clip, Is.Not.Null, direction);
            Assert.That(clip.name, Is.EqualTo($"Shield_Defend_{direction}"));
            Assert.That(AnimationUtility.GetCurveBindings(clip), Is.EquivalentTo(expectedBindings), direction);
            Assert.That(AnimationUtility.GetObjectReferenceCurveBindings(clip), Is.EquivalentTo(new[]
            {
                EditorCurveBinding.PPtrCurve(OffHand, typeof(SpriteRenderer), "m_Sprite")
            }), direction);
            Assert.That(AnimationUtility.GetCurveBindings(clip).Any(binding => binding.path.StartsWith("RightHandPivot",
                StringComparison.Ordinal)), Is.False, direction);
        }
    }

    [Test]
    public void Outputs_HoldTheShieldOnTheFacingsDrawnHand()
    {
        DirectionalAnimationGenerator.GenerateShieldDefendAssets();

        foreach (string direction in Directions)
        {
            AnimationClip idle = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                $"Assets/Animations/Player/Idle/LeftHand/LeftHand_Idle_{direction}.anim");
            EditorCurveBinding spriteBinding = EditorCurveBinding.PPtrCurve(OffHand, typeof(SpriteRenderer), "m_Sprite");
            Sprite idleSprite = (Sprite)AnimationUtility.GetObjectReferenceCurve(idle, spriteBinding)[0].value;
            Vector2 anchor = DirectionalAnimationGenerator.ResolveSpriteAnchor(idleSprite);
            AnimationClip clip = Output(direction);

            ObjectReferenceKeyframe[] sprites = AnimationUtility.GetObjectReferenceCurve(clip, spriteBinding);
            Assert.That(sprites, Has.Length.EqualTo(1), direction);
            Assert.That(sprites[0].time, Is.Zero, direction);
            Assert.That(sprites[0].value, Is.SameAs(idleSprite), direction);
            foreach (float time in new[] { 0f, 0.2f, clip.length })
            {
                Assert.That(GripCurve(clip, "x").Evaluate(time), Is.EqualTo(anchor.x).Within(0.0001f), direction);
                Assert.That(GripCurve(clip, "y").Evaluate(time), Is.EqualTo(anchor.y).Within(0.0001f), direction);
                Assert.That(GripCurve(clip, "z").Evaluate(time), Is.Zero, direction);
            }
        }
    }

    [Test]
    public void SouthOutput_PreservesEveryAuthoredCurve()
    {
        DirectionalAnimationGenerator.GenerateShieldDefendAssets();
        AnimationClip source = Source();
        AnimationClip south = Output("S");

        foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(source))
        {
            Assert.That(AnimationUtility.GetEditorCurve(south, binding).keys,
                Is.EqualTo(AnimationUtility.GetEditorCurve(source, binding).keys), binding.propertyName);
        }
    }

    [Test]
    public void Outputs_TurnThePositionWithTheFacingAndKeepTheAuthoredRotationArt()
    {
        DirectionalAnimationGenerator.GenerateShieldDefendAssets();
        AnimationClip source = Source();
        Keyframe[] sourceX = Curve(source, "m_LocalPosition.x").keys;
        Keyframe[] sourceY = Curve(source, "m_LocalPosition.y").keys;

        for (int i = 0; i < Directions.Length; i++)
        {
            AnimationClip clip = Output(Directions[i]);
            Keyframe[] x = Curve(clip, "m_LocalPosition.x").keys;
            Keyframe[] y = Curve(clip, "m_LocalPosition.y").keys;
            Assert.That(x.Select(key => key.time), Is.EqualTo(sourceX.Select(key => key.time)), Directions[i]);
            for (int k = 0; k < x.Length; k++)
            {
                Vector2 expected = Rotate(new Vector2(sourceX[k].value, sourceY[k].value), Angles[i]);
                Vector2 expectedOut = Rotate(new Vector2(sourceX[k].outTangent, sourceY[k].outTangent), Angles[i]);
                Assert.That(x[k].value, Is.EqualTo(expected.x).Within(0.0001f), $"{Directions[i]}/{k}");
                Assert.That(y[k].value, Is.EqualTo(expected.y).Within(0.0001f), $"{Directions[i]}/{k}");
                Assert.That(x[k].outTangent, Is.EqualTo(expectedOut.x).Within(0.0001f), $"{Directions[i]}/{k}");
                Assert.That(y[k].outTangent, Is.EqualTo(expectedOut.y).Within(0.0001f), $"{Directions[i]}/{k}");
            }
            Assert.That(Curve(clip, "m_LocalPosition.z").keys, Is.EqualTo(Curve(source, "m_LocalPosition.z").keys));
            foreach (string axis in new[] { "x", "y", "z" })
            {
                Assert.That(Curve(clip, "localEulerAnglesRaw." + axis).keys,
                    Is.EqualTo(Curve(source, "localEulerAnglesRaw." + axis).keys), $"{Directions[i]}/{axis}");
            }
        }
    }

    [Test]
    public void Outputs_DoNotLoopAndHoldTheFinalAuthoredPose()
    {
        DirectionalAnimationGenerator.GenerateShieldDefendAssets();
        AnimationClip source = Source();

        foreach (string direction in Directions)
        {
            AnimationClip clip = Output(direction);
            Assert.That(clip.isLooping, Is.False, direction);
            Assert.That(AnimationUtility.GetAnimationClipSettings(clip).loopTime, Is.False, direction);
            Assert.That(clip.length, Is.EqualTo(source.length).Within(0.0001f), direction);
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip)
                .Where(binding => binding.path == OffHand && binding.type == typeof(Transform)))
            {
                AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                Keyframe last = curve.keys[curve.length - 1];
                Assert.That(last.time, Is.EqualTo(clip.length).Within(0.0001f), $"{direction}/{binding.propertyName}");
                Assert.That(curve.Evaluate(clip.length), Is.EqualTo(last.value).Within(0.0001f));
                Assert.That(last.inTangent, Is.EqualTo(0f).Within(0.0001f), $"{direction}/{binding.propertyName}");
            }
        }
    }

    // N and NE share the north main hand's order behind the body.
    [TestCase("N", -2)]
    [TestCase("NE", -2)]
    [TestCase("NW", DirectionalAnimationGenerator.OffHandSortingOrder)]
    [TestCase("S", DirectionalAnimationGenerator.OffHandSortingOrder)]
    [TestCase("SE", DirectionalAnimationGenerator.OffHandSortingOrder)]
    [TestCase("SW", DirectionalAnimationGenerator.OffHandOverMainHandSortingOrder)]
    public void Outputs_DrawTheRaisedHandAtItsFacingDepth(string direction, int expected)
    {
        DirectionalAnimationGenerator.GenerateShieldDefendAssets();
        AnimationCurve sorting = AnimationUtility.GetEditorCurve(Output(direction),
            EditorCurveBinding.FloatCurve(OffHand, typeof(SpriteRenderer), "m_SortingOrder"));

        Assert.That(sorting, Is.Not.Null, direction);
        Assert.That(sorting.keys.Select(key => key.value), Is.All.EqualTo((float)expected), direction);
        Assert.That(sorting.keys.All(key => float.IsPositiveInfinity(key.outTangent)), Is.True, "stepped");
    }

    [Test]
    public void OffHandDepths_FitThePlayerRendererLayout()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/NetworkPlayer.prefab");
        int Order(string name) => prefab.GetComponentsInChildren<SpriteRenderer>(true)
            .Single(renderer => renderer.name == name).sortingOrder;

        Assert.That(Order("LeftHand"), Is.EqualTo(DirectionalAnimationGenerator.OffHandSortingOrder));
        // Behind the body: below the legs, the lowest body renderer.
        Assert.That(DirectionalAnimationGenerator.ResolveOffHandSortingOrder("N"), Is.LessThan(Order("Legs")));
        Assert.That(DirectionalAnimationGenerator.ResolveOffHandSortingOrder("NE"), Is.LessThan(Order("Legs")));
        // Over the main hand and its glove, leaving the next slot for the left glove.
        Assert.That(DirectionalAnimationGenerator.OffHandOverMainHandSortingOrder,
            Is.GreaterThan(Order("RightGloveVisual")));
    }

    [Test]
    public void Regeneration_IsStable()
    {
        DirectionalAnimationGenerator.GenerateShieldDefendAssets();
        byte[][] first = Directions.Select(direction => File.ReadAllBytes(OutputPath(direction))).ToArray();
        string[] guids = Directions.Select(direction => AssetDatabase.AssetPathToGUID(OutputPath(direction))).ToArray();

        DirectionalAnimationGenerator.GenerateShieldDefendAssets();

        for (int i = 0; i < Directions.Length; i++)
        {
            Assert.That(File.ReadAllBytes(OutputPath(Directions[i])), Is.EqualTo(first[i]), Directions[i]);
            Assert.That(AssetDatabase.AssetPathToGUID(OutputPath(Directions[i])), Is.EqualTo(guids[i]), Directions[i]);
            AnimationClip expected = DirectionalAnimationGenerator.CreateOffHandClip(Source(), Directions[i],
                $"Shield_Defend_{Directions[i]}");
            try
            {
                foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(expected))
                {
                    Assert.That(AnimationUtility.GetEditorCurve(Output(Directions[i]), binding).keys,
                        Is.EqualTo(AnimationUtility.GetEditorCurve(expected, binding).keys), $"{Directions[i]}/{binding.propertyName}");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(expected);
            }
        }
    }

    [Test]
    public void OffHandBake_RejectsASourceThatAnimatesOutsideTheOffHandTransform()
    {
        AnimationClip source = UnityEngine.Object.Instantiate(Source());
        try
        {
            AnimationUtility.SetEditorCurve(source,
                EditorCurveBinding.FloatCurve("RightHandPivot/RightHand", typeof(Transform), "m_LocalPosition.x"),
                AnimationCurve.Constant(0f, 0.4f, 0f));

            Assert.Throws<ArgumentException>(() =>
                DirectionalAnimationGenerator.CreateOffHandClip(source, "S", "Shield_Defend_S"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(source);
        }
    }

    [TestCase(CharacterVisualDirection.North, "N")]
    [TestCase(CharacterVisualDirection.NorthEast, "NE")]
    [TestCase(CharacterVisualDirection.NorthWest, "NW")]
    [TestCase(CharacterVisualDirection.South, "S")]
    [TestCase(CharacterVisualDirection.SouthEast, "SE")]
    [TestCase(CharacterVisualDirection.SouthWest, "SW")]
    public void ShieldSpriteSet_ResolvesEachFacingToItsAuthoredSprite(CharacterVisualDirection direction, string suffix)
    {
        DirectionalShieldSpriteSet set = AssetDatabase.LoadAssetAtPath<DirectionalShieldSpriteSet>(SpriteSetPath);
        Assert.That(set, Is.Not.Null);
        Assert.That(set.TryValidate(out string error), Is.True, error);

        Sprite expected = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteRoot}{suffix}.png");
        Assert.That(expected, Is.Not.Null);
        Assert.That(set.GetSprite(direction), Is.SameAs(expected));
    }

    [Test]
    public void ShieldSpriteSet_ReportsAnIncompleteSet()
    {
        DirectionalShieldSpriteSet set = ScriptableObject.CreateInstance<DirectionalShieldSpriteSet>();
        try
        {
            Assert.That(set.IsComplete, Is.False);
            Assert.That(set.TryValidate(out string error), Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(set.GetSprite((CharacterVisualDirection)99), Is.Null);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(set);
        }
    }

    [Test]
    public void GameplayShieldDefinition_DoesNotOwnDirectionalPresentation()
    {
        FieldInfo[] fields = typeof(ShieldDefinition).GetFields(BindingFlags.Instance | BindingFlags.Public |
            BindingFlags.NonPublic);

        Assert.That(fields.Any(field => field.FieldType == typeof(DirectionalShieldSpriteSet)), Is.False);
    }

    private static AnimationClip Source()
    {
        AnimationClip source = AssetDatabase.LoadAssetAtPath<AnimationClip>(SourcePath);
        Assert.That(source, Is.Not.Null);
        return source;
    }

    private static string OutputPath(string direction) => $"{OutputRoot}{direction}.anim";

    private static AnimationClip Output(string direction) =>
        AssetDatabase.LoadAssetAtPath<AnimationClip>(OutputPath(direction));

    private static AnimationCurve Curve(AnimationClip clip, string property) =>
        AnimationUtility.GetEditorCurve(clip, EditorCurveBinding.FloatCurve(OffHand, typeof(Transform), property));

    private static AnimationCurve GripCurve(AnimationClip clip, string axis) =>
        AnimationUtility.GetEditorCurve(clip, EditorCurveBinding.FloatCurve(OffHandGrip, typeof(Transform),
            "m_LocalPosition." + axis));

    private static Vector2 Rotate(Vector2 value, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(radians);
        float sin = Mathf.Sin(radians);
        return new Vector2(cos * value.x - sin * value.y, sin * value.x + cos * value.y);
    }
}
