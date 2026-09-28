using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class DirectionalAnimationGeneratorTests
{
    private const string Hand = "RightHandPivot/RightHand";
    private const string Grip = Hand + "/MainHandGrip";
    private const string Root = "Assets/Animations/Weapons/Directional/ArmingSword/ArmingSword_Attack_";
    private const string RapierRoot = "Assets/Animations/Weapons/Directional/Rapier/Rapier_Attack_";
    private const string RondelRoot = "Assets/Animations/Weapons/Directional/RondelDagger/RondelDagger_Attack_";
    private const string WandRoot = "Assets/Animations/Weapons/Directional/MagicWand/MagicWand_Attack_";
    private const string MagicSwordRoot = "Assets/Animations/Weapons/Directional/MagicSword/MagicSword_Attack_";
    private const string SecondHand = "LeftHandPivot/LeftHand";
    private const string WeaponPose = "WeaponPose";
    private const string LongSwordSource = "Assets/Animations/Weapons/LongSword_Attack.anim";
    private const string LongSwordDefinitionPath = "Assets/Scriptable Objects/Loot/Definitions/LongSwordCombatDefinition.asset";
    private const string ZweihanderDefinitionPath = "Assets/Scriptable Objects/Loot/Definitions/ZweihanderWeaponDefinition.asset";
    private const string MagicStaffDefinitionPath = "Assets/Scriptable Objects/Loot/Definitions/MagicStaffWeaponDefinition.asset";
    private const string LongBowDefinitionPath = "Assets/Scriptable Objects/Loot/Definitions/LongBowWeaponDefinition.asset";

    [TestCase("LongSword")]
    [TestCase("Zweihander")]
    [TestCase("MagicStaff")]
    [TestCase("LongBow")]
    public void TwoHandedOutputs_BakeFromSouthAndRemainStableOnRepeat(string weapon)
    {
        AnimationClip source = TwoHandedSource(weapon);
        Assert.That(source, Is.Not.Null);
        string[] directions = { "N", "NE", "NW", "S", "SE", "SW" };
        if (weapon == "LongSword") DirectionalAnimationGenerator.GenerateLongSwordAssets();
        else if (weapon == "Zweihander") DirectionalAnimationGenerator.GenerateZweihanderAssets();
        else if (weapon == "MagicStaff") DirectionalAnimationGenerator.GenerateMagicStaffAssets();
        else DirectionalAnimationGenerator.GenerateLongBowAssets();
        foreach (string direction in directions)
        {
            string path = TwoHandedOutput(weapon, direction);
            string guid = AssetDatabase.AssetPathToGUID(path);
            Assert.That(guid, Is.Not.Empty, direction);
            AnimationClip expected = DirectionalAnimationGenerator.CreateClip(source, direction, weapon, TwoHandedDefinition(weapon));
            AnimationClip repeated = DirectionalAnimationGenerator.CreateClip(source, direction, weapon, TwoHandedDefinition(weapon));
            try
            {
                AnimationClip actual = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                Assert.That(actual, Is.Not.Null, direction);
                Assert.That(actual.isLooping, Is.False, direction);
                Assert.That(AnimationUtility.GetCurveBindings(actual), Is.EquivalentTo(AnimationUtility.GetCurveBindings(expected)), direction);
                Assert.That(AnimationUtility.GetCurveBindings(repeated), Is.EquivalentTo(AnimationUtility.GetCurveBindings(expected)), direction);
                foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(expected))
                {
                    Keyframe[] keys = AnimationUtility.GetEditorCurve(expected, binding).keys;
                    Assert.That(AnimationUtility.GetEditorCurve(actual, binding).keys, Is.EqualTo(keys), $"{direction}/{binding.propertyName}");
                    Assert.That(AnimationUtility.GetEditorCurve(repeated, binding).keys, Is.EqualTo(keys), $"{direction}/{binding.propertyName}/repeat");
                }
                foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(expected))
                    Assert.That(AnimationUtility.GetObjectReferenceCurve(actual, binding),
                        Is.EqualTo(AnimationUtility.GetObjectReferenceCurve(expected, binding)), $"{direction}/{binding.propertyName}");
                AssertImportedBindings(path, direction);
                foreach (string axis in new[] { "x", "y", "z" })
                    Assert.That(Curve(actual, SecondHand, "m_LocalPosition." + axis), Is.Not.Null, direction);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(expected);
                UnityEngine.Object.DestroyImmediate(repeated);
            }
            DirectionalAnimationGenerator.Bake(source, direction, path, weapon, TwoHandedDefinition(weapon));
            Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guid), direction);
        }
    }

    [TestCase("S", "Left", 0.375f, -0.25f)]
    [TestCase("S", "Right", -0.375f, -0.25f)]
    [TestCase("N", "Left", -0.375f, -0.25f)]
    [TestCase("SE", "Left", 0.3125f, -0.125f)]
    public void ResolveSpriteAnchor_MeasuresTheDrawnHandFromItsPivot(string direction, string hand, float x, float y)
    {
        string path = hand == "Left" ? SecondHand : Hand;
        AnimationClip idle = AssetDatabase.LoadAssetAtPath<AnimationClip>(
            $"Assets/Animations/Player/Idle/{hand}Hand/{hand}Hand_Idle_{direction}.anim");
        var sprite = new EditorCurveBinding { path = path, type = typeof(SpriteRenderer), propertyName = "m_Sprite" };
        Vector2 anchor = DirectionalAnimationGenerator.ResolveSpriteAnchor(
            (Sprite)AnimationUtility.GetObjectReferenceCurve(idle, sprite)[0].value);
        Assert.That(anchor.x, Is.EqualTo(x).Within(0.001f));
        Assert.That(anchor.y, Is.EqualTo(y).Within(0.001f));
    }

    [TestCase("LongSword", "N", 180f)]
    [TestCase("LongSword", "NE", 135f)]
    [TestCase("LongSword", "NW", -135f)]
    [TestCase("LongSword", "S", 0f)]
    [TestCase("LongSword", "SE", 45f)]
    [TestCase("LongSword", "SW", -45f)]
    [TestCase("Zweihander", "N", 180f)]
    [TestCase("Zweihander", "NE", 135f)]
    [TestCase("Zweihander", "NW", -135f)]
    [TestCase("Zweihander", "S", 0f)]
    [TestCase("Zweihander", "SE", 45f)]
    [TestCase("Zweihander", "SW", -45f)]
    public void TwoHandedOutput_DirectionalizesBothHandsAndKeepsTheSecondHandOnTheGrip(string weapon, string direction, float angle)
    {
        AnimationClip source = TwoHandedSource(weapon);
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(TwoHandedOutput(weapon, direction));
        Assert.That(source, Is.Not.Null);
        Assert.That(clip, Is.Not.Null);

        // The authored source stays untouched: it loops and carries no second-hand sprite or sorting.
        Assert.That(AnimationUtility.GetAnimationClipSettings(source).loopTime, Is.True);
        Assert.That(Curve(source, SecondHand, "m_SortingOrder"), Is.Null);
        Assert.That(AnimationUtility.GetObjectReferenceCurveBindings(source).Any(b => b.path == SecondHand), Is.False);

        Assert.That(clip.isLooping, Is.False);
        Assert.That(clip.length, Is.EqualTo(source.length).Within(0.00001f));
        EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
        Assert.That(bindings.All(b => b.path == Hand || b.path == Grip ||
            (b.path == SecondHand && (b.type == typeof(Transform) || b.propertyName == "m_SortingOrder"))), Is.True);
        Assert.That(AnimationUtility.GetObjectReferenceCurveBindings(clip).All(b => b.path == Hand), Is.True,
            "The LeftHand layer keeps owning the second-hand sprite.");

        foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(source)
            .Where(b => (b.path == Hand || b.path == SecondHand) && !b.propertyName.StartsWith("m_LocalPosition.", StringComparison.Ordinal)))
        {
            Assert.That(AnimationUtility.GetEditorCurve(clip, binding).keys,
                Is.EqualTo(AnimationUtility.GetEditorCurve(source, binding).keys), $"{binding.path}/{binding.propertyName}");
        }
        Assert.That(Curve(clip, SecondHand, "m_LocalPosition.z").keys, Is.EqualTo(Curve(source, SecondHand, "m_LocalPosition.z").keys));

        float radians = angle * Mathf.Deg2Rad;
        AnimationCurve sx = Curve(source, Hand, "m_LocalPosition.x");
        AnimationCurve sy = Curve(source, Hand, "m_LocalPosition.y");
        for (int i = 0; i < sx.length; i++)
        {
            Vector2 rotated = Rotate(new Vector2(sx.keys[i].value, sy.keys[i].value), radians);
            Assert.That(Curve(clip, Hand, "m_LocalPosition.x").keys[i].value, Is.EqualTo(rotated.x).Within(0.00002f));
            Assert.That(Curve(clip, Hand, "m_LocalPosition.y").keys[i].value, Is.EqualTo(rotated.y).Within(0.00002f));
        }

        // Timing is authored: every south key time stays keyed; the frame grid only adds in-between keys.
        AnimationCurve secondX = Curve(source, SecondHand, "m_LocalPosition.x");
        float[] outputTimes = Curve(clip, SecondHand, "m_LocalPosition.x").keys.Select(key => key.time).ToArray();
        foreach (Keyframe key in secondX.keys)
            Assert.That(outputTimes.Any(time => Mathf.Abs(time - key.time) < 0.0001f), Is.True, $"{direction}@{key.time}");
        Assert.That(outputTimes.Max(), Is.EqualTo(secondX.keys.Last().time).Within(0.0001f));

        // Grip placement: seen from MainHandGrip turned by the main hand, the drawn second hand sits on the
        // weapon's secondary grip point, placed like the presenter places the held weapon for this facing.
        WeaponDefinition.PresentationConfig presentation = TwoHandedDefinition(weapon).Presentation;
        Vector2 grip = IdleGrip(direction);
        Vector2 anchor = SecondHandAnchor(direction);
        float facingDegrees = angle - 90f;
        Vector2 facingVector = new Vector2(Mathf.Cos(facingDegrees * Mathf.Deg2Rad), Mathf.Sin(facingDegrees * Mathf.Deg2Rad));
        Vector2 handle = Rotate(presentation.SecondaryGripPoint - presentation.GripPoint, presentation.AngleCorrection * Mathf.Deg2Rad);
        if (facingVector.x < -0.0001f) handle.y = -handle.y;
        Vector2 expected = Rotate(handle, facingDegrees * Mathf.Deg2Rad);
        Assert.That(expected.magnitude, Is.GreaterThan(0.1f));
        for (int step = 0; step <= 60; step++)
        {
            float time = secondX.keys.Last().time * step / 60f;
            Vector2 relation = GripRelation(clip, time, grip, anchor);
            // Keys land exactly; between frame-grid keys it stays within half a 16 PPU pixel.
            Assert.That(relation.x, Is.EqualTo(expected.x).Within(0.5f / 16f), $"{direction}@{time}");
            Assert.That(relation.y, Is.EqualTo(expected.y).Within(0.5f / 16f), $"{direction}@{time}");
        }
        foreach (Keyframe key in Curve(clip, SecondHand, "m_LocalPosition.x").keys)
        {
            Vector2 relation = GripRelation(clip, key.time, grip, anchor);
            Assert.That((relation - expected).magnitude, Is.LessThan(0.0002f), $"{direction}@{key.time}");
        }

        // The second hand draws over the handle and under the main hand in every facing.
        bool north = direction.StartsWith("N", StringComparison.Ordinal);
        AnimationCurve sorting = Curve(clip, SecondHand, "m_SortingOrder");
        Assert.That(sorting.Evaluate(0f), Is.EqualTo(north ? -5f : 25f));
        Assert.That(sorting.Evaluate(clip.length), Is.EqualTo(north ? -5f : 25f));
        if (north) Assert.That(Curve(clip, Hand, "m_SortingOrder").Evaluate(0f), Is.EqualTo(-2f));
    }

    [TestCase("LongSword")]
    [TestCase("Zweihander")]
    public void TwoHandedSouth_RetainsEveryAuthoredCurveExceptTheRealignedSecondHandPosition(string weapon)
    {
        AnimationClip original = TwoHandedSource(weapon);
        AnimationClip south = AssetDatabase.LoadAssetAtPath<AnimationClip>(TwoHandedOutput(weapon, "S"));
        foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(original)
            .Where(b => !(b.path == SecondHand && (b.propertyName == "m_LocalPosition.x" || b.propertyName == "m_LocalPosition.y"))))
        {
            AnimationCurve source = AnimationUtility.GetEditorCurve(original, binding);
            Assert.That(AnimationUtility.GetEditorCurve(south, binding).keys, Is.EqualTo(source.keys),
                $"{binding.path}/{binding.propertyName}");
        }
    }

    [Test]
    public void CreateClip_TwoHandedWeaponWithoutSecondaryGripIsRejected()
    {
        AnimationClip source = AssetDatabase.LoadAssetAtPath<AnimationClip>(LongSwordSource);
        WeaponDefinition invalid = UnityEngine.Object.Instantiate(LongSwordDefinition());
        try
        {
            var serialized = new SerializedObject(invalid);
            serialized.FindProperty("_presentation._secondaryGripPoint").vector2Value =
                serialized.FindProperty("_presentation._gripPoint").vector2Value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.Throws<ArgumentException>(() => DirectionalAnimationGenerator.CreateClip(source, "S", "LongSword", invalid));
            Assert.That(invalid.TryValidate(out string error), Is.False);
            Assert.That(error, Does.Contain("secondary grip point"));
        }
        finally { UnityEngine.Object.DestroyImmediate(invalid); }
    }

    [Test]
    public void HeldWeaponVisual_KeepsUnitScaleAssumedByTheSecondGrip()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/NetworkPlayer.prefab");
        var presenter = prefab.GetComponentInChildren<PlayerWeaponPresenter>(true);
        var serialized = new SerializedObject(presenter);
        var visual = (Transform)serialized.FindProperty("_mainHandWeaponVisual").objectReferenceValue;
        var pivot = (Transform)serialized.FindProperty("_mainHandWeaponPivot").objectReferenceValue;
        Assert.That(visual.localScale, Is.EqualTo(Vector3.one));
        Assert.That(pivot.localScale, Is.EqualTo(Vector3.one));
    }

    [Test]
    public void CreateClip_OneHandedContractStillDropsAuthoredLeftHandCurves()
    {
        AnimationClip source = AssetDatabase.LoadAssetAtPath<AnimationClip>(LongSwordSource);
        AnimationClip oneHanded = DirectionalAnimationGenerator.CreateClip(source, "SE", "LongSword");
        try
        {
            Assert.That(AnimationUtility.GetCurveBindings(oneHanded).Any(b => b.path == SecondHand), Is.False);
        }
        finally { UnityEngine.Object.DestroyImmediate(oneHanded); }
    }

    [TestCase("MagicStaff", false)]
    [TestCase("LongSword", true)]
    [TestCase("Zweihander", true)]
    public void TwoHandedSource_SecondHandRotationShowsWhetherItHoldsTheWeapon(string weapon, bool holds)
    {
        // A hand on the handle turns rigidly with the main hand; the staff's left hand gestures on its own.
        AnimationClip source = TwoHandedSource(weapon);
        bool rigid = Curve(source, SecondHand, "localEulerAnglesRaw.z").keys
            .SequenceEqual(Curve(source, Hand, "localEulerAnglesRaw.z").keys);
        Assert.That(rigid, Is.EqualTo(holds));
        Assert.That(TwoHandedDefinition(weapon).Presentation.SecondHand, Is.EqualTo(holds
            ? SecondHandPresentation.HoldsSecondaryGrip
            : SecondHandPresentation.FollowsAuthoredMotion));
    }

    [TestCase("N", 180f)]
    [TestCase("NE", 135f)]
    [TestCase("NW", -135f)]
    [TestCase("S", 0f)]
    [TestCase("SE", 45f)]
    [TestCase("SW", -45f)]
    public void MagicStaffOutput_TurnsBothAuthoredHandsWithTheFacingAndKeepsTheirArt(string direction, float angle)
    {
        AnimationClip source = TwoHandedSource("MagicStaff");
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(TwoHandedOutput("MagicStaff", direction));
        Assert.That(source, Is.Not.Null);
        Assert.That(clip, Is.Not.Null);

        // The authored source stays untouched: it loops and keeps its second-hand motion.
        Assert.That(AnimationUtility.GetAnimationClipSettings(source).loopTime, Is.True);
        Assert.That(Curve(source, SecondHand, "localEulerAnglesRaw.z"), Is.Not.Null);

        Assert.That(clip.isLooping, Is.False);
        Assert.That(clip.length, Is.EqualTo(source.length).Within(0.00001f));
        Assert.That(AnimationUtility.GetCurveBindings(clip).All(b => b.path == Hand || b.path == Grip ||
            (b.path == SecondHand && b.type == typeof(Transform))), Is.True,
            "A second hand off the weapon keeps LeftHand-layer sorting.");
        Assert.That(AnimationUtility.GetObjectReferenceCurveBindings(clip).All(b => b.path == Hand), Is.True,
            "The LeftHand layer keeps owning the second-hand sprite.");

        // Rotation art, depth and timing of both hands are authored.
        foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(source)
            .Where(b => (b.path == Hand || b.path == SecondHand) &&
                b.propertyName != "m_LocalPosition.x" && b.propertyName != "m_LocalPosition.y"))
        {
            Assert.That(AnimationUtility.GetEditorCurve(clip, binding).keys,
                Is.EqualTo(AnimationUtility.GetEditorCurve(source, binding).keys), $"{binding.path}/{binding.propertyName}");
        }

        // Both trajectories turn with the facing at their authored key times, values and tangents alike.
        float radians = angle * Mathf.Deg2Rad;
        foreach (string path in new[] { Hand, SecondHand })
        {
            Keyframe[] sx = Curve(source, path, "m_LocalPosition.x").keys;
            Keyframe[] sy = Curve(source, path, "m_LocalPosition.y").keys;
            Keyframe[] cx = Curve(clip, path, "m_LocalPosition.x").keys;
            Keyframe[] cy = Curve(clip, path, "m_LocalPosition.y").keys;
            Assert.That(cx.Select(key => key.time), Is.EqualTo(sx.Select(key => key.time)), path);
            Assert.That(cy.Select(key => key.time), Is.EqualTo(sy.Select(key => key.time)), path);
            for (int i = 0; i < sx.Length; i++)
            {
                Vector2 value = Rotate(new Vector2(sx[i].value, sy[i].value), radians);
                Vector2 inTangent = Rotate(new Vector2(sx[i].inTangent, sy[i].inTangent), radians);
                Vector2 outTangent = Rotate(new Vector2(sx[i].outTangent, sy[i].outTangent), radians);
                Assert.That(cx[i].value, Is.EqualTo(value.x).Within(0.00002f), $"{path}@{sx[i].time}");
                Assert.That(cy[i].value, Is.EqualTo(value.y).Within(0.00002f), $"{path}@{sx[i].time}");
                Assert.That(cx[i].inTangent, Is.EqualTo(inTangent.x).Within(0.0001f), $"{path}@{sx[i].time}");
                Assert.That(cy[i].inTangent, Is.EqualTo(inTangent.y).Within(0.0001f), $"{path}@{sx[i].time}");
                Assert.That(cx[i].outTangent, Is.EqualTo(outTangent.x).Within(0.0001f), $"{path}@{sx[i].time}");
                Assert.That(cy[i].outTangent, Is.EqualTo(outTangent.y).Within(0.0001f), $"{path}@{sx[i].time}");
            }
        }

        AssertImportedBindings(TwoHandedOutput("MagicStaff", direction), direction);
        if (direction == "S")
        {
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(source))
                Assert.That(AnimationUtility.GetEditorCurve(clip, binding).keys,
                    Is.EqualTo(AnimationUtility.GetEditorCurve(source, binding).keys), $"{binding.path}/{binding.propertyName}");
        }
    }

    [Test]
    public void CreateClip_SecondHandPresentationIsSelectedByWeaponDataNotIdentity()
    {
        AnimationClip source = AssetDatabase.LoadAssetAtPath<AnimationClip>(LongSwordSource);
        WeaponDefinition authored = UnityEngine.Object.Instantiate(LongSwordDefinition());
        try
        {
            var serialized = new SerializedObject(authored);
            serialized.FindProperty("_presentation._secondHand").intValue = (int)SecondHandPresentation.FollowsAuthoredMotion;
            // A hand that does not hold the weapon needs no secondary grip point.
            serialized.FindProperty("_presentation._secondaryGripPoint").vector2Value =
                serialized.FindProperty("_presentation._gripPoint").vector2Value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(authored.TryValidate(out string error), Is.True, error);

            AnimationClip clip = DirectionalAnimationGenerator.CreateClip(source, "SE", "LongSword", authored);
            try
            {
                Assert.That(Curve(clip, SecondHand, "m_SortingOrder"), Is.Null);
                Keyframe[] sx = Curve(source, SecondHand, "m_LocalPosition.x").keys;
                Keyframe[] sy = Curve(source, SecondHand, "m_LocalPosition.y").keys;
                Keyframe[] cx = Curve(clip, SecondHand, "m_LocalPosition.x").keys;
                Assert.That(cx, Has.Length.EqualTo(sx.Length));
                for (int i = 0; i < sx.Length; i++)
                    Assert.That(cx[i].value, Is.EqualTo(Rotate(new Vector2(sx[i].value, sy[i].value), 45f * Mathf.Deg2Rad).x).Within(0.00002f));
            }
            finally { UnityEngine.Object.DestroyImmediate(clip); }
        }
        finally { UnityEngine.Object.DestroyImmediate(authored); }
    }

    [Test]
    public void OneHandedWeapon_RejectsASecondHandPresentation()
    {
        WeaponDefinition oneHanded = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<WeaponDefinition>(
            "Assets/Scriptable Objects/Loot/Definitions/MagicWandWeaponDefinition.asset"));
        try
        {
            Assert.That(oneHanded.Handedness, Is.EqualTo(WeaponHandedness.OneHanded));
            var serialized = new SerializedObject(oneHanded);
            serialized.FindProperty("_presentation._secondHand").intValue = (int)SecondHandPresentation.FollowsAuthoredMotion;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(oneHanded.TryValidate(out string error), Is.False);
            Assert.That(error, Does.Contain("second-hand presentation"));
        }
        finally { UnityEngine.Object.DestroyImmediate(oneHanded); }
    }

    [TestCase("N", 180f)]
    [TestCase("NE", 135f)]
    [TestCase("NW", -135f)]
    [TestCase("S", 0f)]
    [TestCase("SE", 45f)]
    [TestCase("SW", -45f)]
    public void LongBowOutput_WeaponPoseOwnsTheBowAndPlacesBothHandsOnIt(string direction, float angle)
    {
        AnimationClip source = TwoHandedSource("LongBow");
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(TwoHandedOutput("LongBow", direction));
        Assert.That(source, Is.Not.Null);
        Assert.That(clip, Is.Not.Null);

        // The authored source stays untouched: it loops, the left hand is the bow arm and the right hand draws.
        Assert.That(AnimationUtility.GetAnimationClipSettings(source).loopTime, Is.True);
        Assert.That(Curve(source, Hand, "localEulerAnglesRaw.z"), Is.Null);
        Assert.That(Curve(source, SecondHand, "localEulerAnglesRaw.z"), Is.Not.Null);

        // The authored motion plays whole between an ease out of locomotion and an ease back into it.
        const float blend = DirectionalAnimationGenerator.WeaponDrivenBlendSeconds;
        Assert.That(clip.isLooping, Is.False);
        Assert.That(clip.length, Is.EqualTo(source.length + 2f * blend).Within(0.00001f));
        Assert.That(AnimationUtility.GetCurveBindings(clip).All(b => b.path == Hand || b.path == Grip ||
            (b.type == typeof(Transform) && (b.path == SecondHand || b.path == WeaponPose))), Is.True,
            "Only both hands and the weapon pose move; the presenter owns the bow hand's sorting.");
        Assert.That(AnimationUtility.GetObjectReferenceCurveBindings(clip).All(b => b.path == Hand), Is.True,
            "The LeftHand layer keeps owning the second-hand sprite.");

        // Rotation art and depth stay authored on each hand; the bow takes the bow arm's rotation art.
        foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(source)
            .Where(b => (b.path == Hand || b.path == SecondHand) && !b.propertyName.StartsWith("m_LocalPosition.x", StringComparison.Ordinal) &&
                !b.propertyName.StartsWith("m_LocalPosition.y", StringComparison.Ordinal)))
        {
            AnimationCurve authored = AnimationUtility.GetEditorCurve(source, binding);
            AnimationCurve baked = AnimationUtility.GetEditorCurve(clip, binding);
            Assert.That(baked.keys.Skip(1).Take(authored.length).Select(key => key.time - blend),
                Is.EqualTo(authored.keys.Select(key => key.time)).Within(0.00001f), $"{binding.path}/{binding.propertyName}");
            foreach (Keyframe key in authored.keys)
                Assert.That(baked.Evaluate(key.time + blend), Is.EqualTo(key.value).Within(0.00001f), $"{binding.path}/{binding.propertyName}@{key.time}");
        }
        foreach (Keyframe key in Curve(source, SecondHand, "localEulerAnglesRaw.z").keys)
            Assert.That(Curve(clip, WeaponPose, "localEulerAnglesRaw.z").Evaluate(key.time + blend), Is.EqualTo(key.value).Within(0.00001f));

        // Every authored key time stays keyed on both hands and the weapon pose.
        foreach (string path in new[] { Hand, SecondHand, WeaponPose })
        {
            float[] times = Curve(clip, path, "m_LocalPosition.x").keys.Select(key => key.time).ToArray();
            foreach (Keyframe key in Curve(source, SecondHand, "m_LocalPosition.x").keys)
                Assert.That(times.Any(time => Mathf.Abs(time - key.time - blend) < 0.0001f), Is.True, $"{path}@{key.time}");
        }

        // Both ends rest in the facing's locomotion pose: WeaponPose in the idle drawn left hand, the hands at
        // their transform origin, nothing rotated, so the instant Idle/Attack transitions do not snap.
        AnimationClip idle = AssetDatabase.LoadAssetAtPath<AnimationClip>($"Assets/Animations/Player/Idle/Idle_{direction}.anim");
        foreach (float time in new[] { 0f, clip.length })
        {
            Assert.That(Vector2.Distance(Position(clip, WeaponPose, time), Position(idle, WeaponPose, 0f)), Is.LessThan(0.00001f), $"@{time}");
            Assert.That(Position(clip, SecondHand, time).magnitude, Is.LessThan(0.00001f), $"@{time}");
            Assert.That(Position(clip, Hand, time).magnitude, Is.LessThan(0.00001f), $"@{time}");
            Assert.That(Curve(clip, WeaponPose, "localEulerAnglesRaw.z").Evaluate(time), Is.EqualTo(0f).Within(0.00001f), $"@{time}");
        }

        // The bow pose and the string-hand target are the south drawn points turned about the aim center.
        Vector2 aim = AimCenter(source);
        float radians = angle * Mathf.Deg2Rad;
        Vector2 leftAnchor = IdleHandAnchor("Left", direction);
        Vector2 rightAnchor = IdleHandAnchor("Right", direction);
        for (float time = 0f; time <= clip.length + 0.0001f; time += 0.025f)
        {
            string at = $"{direction}@{time:0.000}";
            float bowAngle = Curve(clip, WeaponPose, "localEulerAnglesRaw.z").Evaluate(time) * Mathf.Deg2Rad;
            Vector2 bow = Position(clip, WeaponPose, time);
            Vector2 left = Position(clip, SecondHand, time) + Rotate(leftAnchor, bowAngle);
            // The bow hand holds the grip in every frame, blends included.
            Assert.That(Vector2.Distance(left, bow), Is.LessThan(0.001f), $"The bow hand holds the grip {at}.");
            float authoredTime = time - blend;
            if (authoredTime < -0.0001f || authoredTime > source.length + 0.0001f) continue;
            Vector2 right = Position(clip, Hand, time) + rightAnchor;
            Vector2 expectedBow = aim + Rotate(SouthDrawn(source, SecondHand, authoredTime) - aim, radians);
            Vector2 expectedRight = aim + Rotate(SouthDrawn(source, Hand, authoredTime) - aim, radians);
            Assert.That(Vector2.Distance(bow, expectedBow), Is.LessThan(0.001f), $"The bow follows its own pose {at}.");
            Assert.That(Vector2.Distance(right, expectedRight), Is.LessThan(0.001f), $"The string hand follows its target {at}.");
        }

        AssertImportedBindings(TwoHandedOutput("LongBow", direction), direction);
        if (direction != "S") return;
        // South reconstructs the authored art: every drawn hand stays where the source draws it.
        for (float time = 0f; time <= source.length + 0.0001f; time += 0.025f)
        {
            foreach (string path in new[] { Hand, SecondHand })
            {
                Assert.That(Vector2.Distance(Position(clip, path, time + blend), Position(source, path, time)), Is.LessThan(0.001f),
                    $"{path}@{time:0.000}");
            }
        }
    }

    [TestCase("N", 0f, 1f)]
    [TestCase("NE", 1f, 1f)]
    [TestCase("NW", -1f, 1f)]
    [TestCase("S", 0f, -1f)]
    [TestCase("SE", 1f, -1f)]
    [TestCase("SW", -1f, -1f)]
    public void LongBowOutput_ProjectsTheBowTowardTheTargetAndDrawsTheStringBehindIt(string direction, float x, float y)
    {
        AnimationClip source = TwoHandedSource("LongBow");
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(TwoHandedOutput("LongBow", direction));
        Vector2 facing = new Vector2(x, y).normalized;
        Vector2 aim = AimCenter(source);
        Vector2 rightAnchor = IdleHandAnchor("Right", direction);
        const float blend = DirectionalAnimationGenerator.WeaponDrivenBlendSeconds;
        const float draw = 0.3f + blend;
        const float release = 0.4f + blend;
        // The bow never migrates toward the feet: through the whole authored motion it stays forward of the body
        // origin; only the blends travel from and back to the locomotion rest.
        const float minimumForwardDistance = 0.3f;
        for (float time = blend; time <= source.length + blend + 0.0001f; time += 0.01f)
        {
            Assert.That(Vector2.Dot(Position(clip, WeaponPose, time), facing), Is.GreaterThan(minimumForwardDistance),
                $"The bow stays forward of the body @{time:0.00}.");
        }
        Vector2 rest = Position(clip, WeaponPose, blend);
        foreach (float time in new[] { draw, release })
        {
            Vector2 bow = Position(clip, WeaponPose, time);
            Vector2 drawHand = Position(clip, Hand, time) + rightAnchor;
            Assert.That(Vector2.Dot(bow - aim, facing), Is.GreaterThan(0.5f), $"The bow reaches toward the target @{time}.");
            Assert.That(Vector2.Dot(bow - rest, facing), Is.GreaterThan(0.1f), $"The draw pushes the bow toward the target @{time}.");
            Assert.That(Vector2.Dot(drawHand - bow, facing), Is.LessThan(-0.5f), $"The string hand stays behind the bow @{time}.");
        }
    }

    [TestCase("Idle", "N")]
    [TestCase("Idle", "NE")]
    [TestCase("Idle", "NW")]
    [TestCase("Idle", "S")]
    [TestCase("Idle", "SE")]
    [TestCase("Idle", "SW")]
    [TestCase("Walk", "N")]
    [TestCase("Walk", "NE")]
    [TestCase("Walk", "NW")]
    [TestCase("Walk", "S")]
    [TestCase("Walk", "SE")]
    [TestCase("Walk", "SW")]
    public void WeaponPoseLocomotion_RestsInEveryDrawnLeftHandFrame(string motion, string direction)
    {
        AnimationClip body = AssetDatabase.LoadAssetAtPath<AnimationClip>($"Assets/Animations/Player/{motion}/{motion}_{direction}.anim");
        AnimationClip hand = AssetDatabase.LoadAssetAtPath<AnimationClip>(
            $"Assets/Animations/Player/{motion}/LeftHand/LeftHand_{motion}_{direction}.anim");
        Assert.That(body, Is.Not.Null);
        Assert.That(hand, Is.Not.Null);
        ObjectReferenceKeyframe[] frames = AnimationUtility.GetObjectReferenceCurve(hand,
            new EditorCurveBinding { path = SecondHand, type = typeof(SpriteRenderer), propertyName = "m_Sprite" });

        AnimationClip regenerated = UnityEngine.Object.Instantiate(body);
        try
        {
            DirectionalAnimationGenerator.WriteWeaponPoseLocomotion(regenerated, hand);
            foreach (string property in new[] { "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z",
                "localEulerAnglesRaw.x", "localEulerAnglesRaw.y", "localEulerAnglesRaw.z" })
            {
                Keyframe[] keys = Curve(body, WeaponPose, property)?.keys;
                Assert.That(keys, Is.Not.Null, $"{motion}_{direction} misses its WeaponPose {property} curve.");
                Assert.That(keys, Is.EqualTo(Curve(regenerated, WeaponPose, property).keys), property);
                Assert.That(keys[keys.Length - 1].time, Is.EqualTo(body.length).Within(0.0001f), property);
                if (property.StartsWith("localEulerAnglesRaw", StringComparison.Ordinal))
                    Assert.That(keys.All(key => key.value == 0f), Is.True, $"{property} rests unrotated.");
                else
                    Assert.That(keys.All(key => float.IsPositiveInfinity(key.outTangent)), Is.True, $"{property} is stepped.");
            }
            foreach (ObjectReferenceKeyframe frame in frames)
            {
                Vector2 anchor = DirectionalAnimationGenerator.ResolveSpriteAnchor((Sprite)frame.value);
                Vector2 pose = Position(body, WeaponPose, frame.time + 0.0001f);
                Assert.That(pose.x, Is.EqualTo(anchor.x).Within(0.00001f), $"{frame.time}");
                Assert.That(pose.y, Is.EqualTo(anchor.y).Within(0.00001f), $"{frame.time}");
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(regenerated); }
    }

    [TestCase("LongSword")]
    [TestCase("Zweihander")]
    [TestCase("MagicStaff")]
    public void HandHeldOutputs_DoNotAnimateTheWeaponPose(string weapon)
    {
        Assert.That(TwoHandedDefinition(weapon).Presentation.Rig, Is.EqualTo(WeaponRig.HandHeld));
        AnimationClip clip = DirectionalAnimationGenerator.CreateClip(TwoHandedSource(weapon), "SE", weapon, TwoHandedDefinition(weapon));
        try
        {
            Assert.That(AnimationUtility.GetCurveBindings(clip).Any(b => b.path == WeaponPose), Is.False);
        }
        finally { UnityEngine.Object.DestroyImmediate(clip); }
    }

    [Test]
    public void WeaponDrivenRig_RequiresATwoHandedWeaponWhoseSecondHandFollowsItsAuthoredMotion()
    {
        WeaponDefinition oneHanded = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<WeaponDefinition>(
            "Assets/Scriptable Objects/Loot/Definitions/MagicWandWeaponDefinition.asset"));
        WeaponDefinition holdsGrip = UnityEngine.Object.Instantiate(LongSwordDefinition());
        try
        {
            foreach (WeaponDefinition weapon in new[] { oneHanded, holdsGrip })
            {
                var serialized = new SerializedObject(weapon);
                serialized.FindProperty("_presentation._rig").intValue = (int)WeaponRig.WeaponDriven;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(weapon.TryValidate(out string error), Is.False, weapon.name);
                Assert.That(error, Does.Contain("weapon-driven rig"), weapon.name);
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(oneHanded);
            UnityEngine.Object.DestroyImmediate(holdsGrip);
        }
    }

    // South drawn hand: its transform plus its south idle sprite anchor turned by the hand's rotation.
    private static Vector2 SouthDrawn(AnimationClip source, string path, float time)
    {
        AnimationCurve angle = Curve(source, path, "localEulerAnglesRaw.z");
        float radians = angle != null ? angle.Evaluate(time) * Mathf.Deg2Rad : 0f;
        return Position(source, path, time) + Rotate(IdleHandAnchor(path == Hand ? "Right" : "Left", "S"), radians);
    }

    // The aim center: on the body axis at the south string hand's height during its authored draw hold.
    private static Vector2 AimCenter(AnimationClip source)
    {
        Keyframe[] x = Curve(source, Hand, "m_LocalPosition.x").keys;
        Keyframe[] y = Curve(source, Hand, "m_LocalPosition.y").keys;
        int hold = Enumerable.Range(0, x.Length - 1).First(i => x[i].value == x[i + 1].value && y[i].value == y[i + 1].value);
        return new Vector2(0f, SouthDrawn(source, Hand, x[hold].time).y);
    }

    [Test]
    public void MagicWandOutputs_BakeFromSouthAndRemainStableOnRepeat()
    {
        AnimationClip source = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Weapons/MagicWand_Attack.anim");
        Assert.That(source, Is.Not.Null);
        string[] directions = { "N", "NE", "NW", "S", "SE", "SW" };
        // The production menu and this check share the same Bake path. Re-running must retain asset identities.
        DirectionalAnimationGenerator.GenerateMagicWandAssets();
        foreach (string direction in directions)
        {
            string path = WandRoot + direction + ".anim";
            string guid = AssetDatabase.AssetPathToGUID(path);
            Assert.That(guid, Is.Not.Empty, direction);
            AnimationClip expected = DirectionalAnimationGenerator.CreateClip(source, direction, "MagicWand");
            try
            {
                AnimationClip actual = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                Assert.That(actual, Is.Not.Null, direction);
                Assert.That(actual.isLooping, Is.False, direction);
                Assert.That(AnimationUtility.GetCurveBindings(actual), Is.EquivalentTo(AnimationUtility.GetCurveBindings(expected)), direction);
                foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(expected))
                    Assert.That(AnimationUtility.GetEditorCurve(actual, binding).keys,
                        Is.EqualTo(AnimationUtility.GetEditorCurve(expected, binding).keys), $"{direction}/{binding.propertyName}");
                foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(expected))
                    Assert.That(AnimationUtility.GetObjectReferenceCurve(actual, binding),
                        Is.EqualTo(AnimationUtility.GetObjectReferenceCurve(expected, binding)), $"{direction}/{binding.propertyName}");
                AssertImportedBindings(path, direction);
            }
            finally { UnityEngine.Object.DestroyImmediate(expected); }
            DirectionalAnimationGenerator.Bake(source, direction, path, "MagicWand");
            Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guid), direction);
        }
    }

    [Test]
    public void MagicSwordOutputs_BakeFromSouthAndRemainStableOnRepeat()
    {
        AnimationClip source = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Weapons/MagicSword_Attack.anim");
        Assert.That(source, Is.Not.Null);
        string[] directions = { "N", "NE", "NW", "S", "SE", "SW" };
        DirectionalAnimationGenerator.GenerateMagicSwordAssets();
        foreach (string direction in directions)
        {
            string path = MagicSwordRoot + direction + ".anim";
            string guid = AssetDatabase.AssetPathToGUID(path);
            Assert.That(guid, Is.Not.Empty, direction);
            AnimationClip expected = DirectionalAnimationGenerator.CreateClip(source, direction, "MagicSword");
            try
            {
                AnimationClip actual = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                Assert.That(actual, Is.Not.Null, direction);
                Assert.That(AnimationUtility.GetCurveBindings(actual), Is.EquivalentTo(AnimationUtility.GetCurveBindings(expected)), direction);
                foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(expected))
                    Assert.That(AnimationUtility.GetEditorCurve(actual, binding).keys,
                        Is.EqualTo(AnimationUtility.GetEditorCurve(expected, binding).keys), $"{direction}/{binding.propertyName}");
                foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(expected))
                    Assert.That(AnimationUtility.GetObjectReferenceCurve(actual, binding),
                        Is.EqualTo(AnimationUtility.GetObjectReferenceCurve(expected, binding)), $"{direction}/{binding.propertyName}");
                AssertImportedBindings(path, direction);
            }
            finally { UnityEngine.Object.DestroyImmediate(expected); }
            DirectionalAnimationGenerator.Bake(source, direction, path, "MagicSword");
            Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guid), direction);
        }
    }

    [TestCase("N", 180f)]
    [TestCase("NE", 135f)]
    [TestCase("NW", -135f)]
    [TestCase("S", 0f)]
    [TestCase("SE", 45f)]
    [TestCase("SW", -45f)]
    public void MagicSwordOutput_KeepsRightHandArtAndDropsCurvesOutsideMainHandContract(string direction, float angle)
    {
        const string sourcePath = "Assets/Animations/Weapons/MagicSword_Attack.anim";
        AnimationClip source = AssetDatabase.LoadAssetAtPath<AnimationClip>(sourcePath);
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(MagicSwordRoot + direction + ".anim");
        Assert.That(source, Is.Not.Null);
        Assert.That(clip, Is.Not.Null);

        // The source stays untouched: it still loops and still carries LeftHand curves.
        Assert.That(AnimationUtility.GetAnimationClipSettings(source).loopTime, Is.True);
        Assert.That(AnimationUtility.GetCurveBindings(source).Any(b => b.path == "LeftHandPivot/LeftHand"), Is.True);

        Assert.That(clip.isLooping, Is.False);
        Assert.That(AnimationUtility.GetAnimationClipSettings(clip).loopTime, Is.False);
        Assert.That(clip.length, Is.EqualTo(source.length).Within(0.00001f));
        Assert.That(AnimationUtility.GetCurveBindings(clip).Concat(AnimationUtility.GetObjectReferenceCurveBindings(clip))
            .All(b => b.path == Hand || b.path == Grip), Is.True);

        foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(source)
            .Where(b => b.path == Hand && !b.propertyName.StartsWith("m_LocalPosition.", StringComparison.Ordinal)))
        {
            Assert.That(AnimationUtility.GetEditorCurve(clip, binding).keys,
                Is.EqualTo(AnimationUtility.GetEditorCurve(source, binding).keys), binding.propertyName);
        }

        float radians = angle * Mathf.Deg2Rad;
        float cos = Mathf.Cos(radians);
        float sin = Mathf.Sin(radians);
        AnimationCurve sx = Curve(source, Hand, "m_LocalPosition.x");
        AnimationCurve sy = Curve(source, Hand, "m_LocalPosition.y");
        AnimationCurve rx = Curve(clip, Hand, "m_LocalPosition.x");
        AnimationCurve ry = Curve(clip, Hand, "m_LocalPosition.y");
        Assert.That(rx.length, Is.EqualTo(sx.length));
        for (int i = 0; i < sx.length; i++)
        {
            Assert.That(rx.keys[i].time, Is.EqualTo(sx.keys[i].time));
            Assert.That(rx.keys[i].value, Is.EqualTo(cos * sx.keys[i].value - sin * sy.keys[i].value).Within(0.00002f));
            Assert.That(ry.keys[i].value, Is.EqualTo(sin * sx.keys[i].value + cos * sy.keys[i].value).Within(0.00002f));
            Assert.That(rx.keys[i].outTangent, Is.EqualTo(cos * sx.keys[i].outTangent - sin * sy.keys[i].outTangent).Within(0.00002f));
            Assert.That(ry.keys[i].inTangent, Is.EqualTo(sin * sx.keys[i].inTangent + cos * sy.keys[i].inTangent).Within(0.00002f));
        }

        AnimationClip idle = AssetDatabase.LoadAssetAtPath<AnimationClip>($"Assets/Animations/Player/Idle/Idle_{direction}.anim");
        foreach (string axis in new[] { "x", "y", "z" })
        {
            float expected = Curve(idle, Grip, "m_LocalPosition." + axis).Evaluate(0f);
            Assert.That(Curve(clip, Grip, "m_LocalPosition." + axis).Evaluate(clip.length), Is.EqualTo(expected).Within(0.00002f));
        }
        AnimationClip handIdle = AssetDatabase.LoadAssetAtPath<AnimationClip>($"Assets/Animations/Player/Idle/RightHand/RightHand_Idle_{direction}.anim");
        var sprite = new EditorCurveBinding { path = Hand, type = typeof(SpriteRenderer), propertyName = "m_Sprite" };
        Assert.That(AnimationUtility.GetObjectReferenceCurve(clip, sprite).Single().value,
            Is.SameAs(AnimationUtility.GetObjectReferenceCurve(handIdle, sprite)[0].value));
    }

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
        AnimationClip result = DirectionalAnimationGenerator.CreateClip(south, direction, "ArmingSword");
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
            AnimationClip generated = DirectionalAnimationGenerator.CreateClip(copy, "S", "ArmingSword");
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
        Assert.Throws<ArgumentException>(() => DirectionalAnimationGenerator.CreateClip(south, "E", "ArmingSword"));
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

        Assert.Throws<ArgumentException>(() => DirectionalAnimationGenerator.Bake(original, "S", sourcePath, "ArmingSword"));
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
            DirectionalAnimationGenerator.Bake(original, direction, path, "ArmingSword");
            string guid = AssetDatabase.AssetPathToGUID(path);
            Assert.That(guid, Is.Not.Empty);
            AssertImportedBindings(path, direction);
            DirectionalAnimationGenerator.Bake(original, direction, path, "ArmingSword");
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
            DirectionalAnimationGenerator.Bake(original, "S", path, "ArmingSword");
            Assert.That(AssetDatabase.DeleteAsset(path), Is.True);
            Assert.That(AssetDatabase.LoadAssetAtPath<AnimationClip>(path), Is.Null);
            DirectionalAnimationGenerator.Bake(original, "S", path, "ArmingSword");
            AssertImportedBindings(path, "S");
            float runtimeSouthValue = Curve(AssetDatabase.LoadAssetAtPath<AnimationClip>(Root + "S.anim"), Hand, "m_LocalPosition.x").keys[0].value;
            AnimationCurve changed = AnimationUtility.GetEditorCurve(copy, binding);
            Keyframe[] keys = changed.keys;
            keys[0].value += 0.125f;
            changed.keys = keys;
            AnimationUtility.SetEditorCurve(copy, binding, changed);
            DirectionalAnimationGenerator.Bake(copy, "S", path, "ArmingSword");
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

    [Test]
    public void RapierOutputs_AreReproducibleFromSingleSouthSource()
    {
        AnimationClip source = AssetDatabase.LoadAssetAtPath<AnimationClip>(
            "Assets/Animations/Weapons/Rapier_Attack.anim");
        Assert.That(source, Is.Not.Null);
        string[] directions = { "N", "NE", "NW", "S", "SE", "SW" };
        foreach (string direction in directions)
        {
            AnimationClip expected = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                $"Assets/Animations/Weapons/Directional/Rapier/Rapier_Attack_{direction}.anim");
            AnimationClip first = DirectionalAnimationGenerator.CreateClip(source, direction, "Rapier");
            AnimationClip second = DirectionalAnimationGenerator.CreateClip(source, direction, "Rapier");
            try
            {
                Assert.That(expected, Is.Not.Null, direction);
                Assert.That(first.name, Is.EqualTo(expected.name));
                Assert.That(AnimationUtility.GetCurveBindings(first), Is.EquivalentTo(
                    AnimationUtility.GetCurveBindings(second)), direction);
                foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(first))
                {
                    Assert.That(AnimationUtility.GetEditorCurve(first, binding).keys,
                        Is.EqualTo(AnimationUtility.GetEditorCurve(second, binding).keys),
                        $"{direction}/{binding.propertyName}");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(second);
            }
        }
    }

    [Test]
    public void RondelDaggerOutputs_AreReproducibleFromSingleSouthSource()
    {
        AnimationClip source = AssetDatabase.LoadAssetAtPath<AnimationClip>(
            "Assets/Animations/Weapons/RondelDagger_Attack.anim");
        Assert.That(source, Is.Not.Null);
        string[] directions = { "N", "NE", "NW", "S", "SE", "SW" };
        foreach (string direction in directions)
        {
            AnimationClip expected = AssetDatabase.LoadAssetAtPath<AnimationClip>(RondelRoot + direction + ".anim");
            AnimationClip first = DirectionalAnimationGenerator.CreateClip(source, direction, "RondelDagger");
            AnimationClip second = DirectionalAnimationGenerator.CreateClip(source, direction, "RondelDagger");
            try
            {
                Assert.That(expected, Is.Not.Null, direction);
                Assert.That(first.name, Is.EqualTo(expected.name));
                Assert.That(AnimationUtility.GetCurveBindings(first), Is.EquivalentTo(
                    AnimationUtility.GetCurveBindings(second)), direction);
                Assert.That(AnimationUtility.GetObjectReferenceCurveBindings(first), Is.EquivalentTo(
                    AnimationUtility.GetObjectReferenceCurveBindings(second)), direction);
                foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(first))
                {
                    Assert.That(AnimationUtility.GetEditorCurve(first, binding).keys,
                        Is.EqualTo(AnimationUtility.GetEditorCurve(second, binding).keys),
                        $"{direction}/{binding.propertyName}");
                    Assert.That(AnimationUtility.GetEditorCurve(first, binding).keys,
                        Is.EqualTo(AnimationUtility.GetEditorCurve(expected, binding).keys),
                        $"{direction}/{binding.propertyName}/output");
                }
                foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(first))
                {
                    Assert.That(AnimationUtility.GetObjectReferenceCurve(first, binding),
                        Is.EqualTo(AnimationUtility.GetObjectReferenceCurve(second, binding)),
                        $"{direction}/{binding.propertyName}");
                    Assert.That(AnimationUtility.GetObjectReferenceCurve(first, binding),
                        Is.EqualTo(AnimationUtility.GetObjectReferenceCurve(expected, binding)),
                        $"{direction}/{binding.propertyName}/output");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(second);
            }
        }
    }

    [Test]
    public void RapierNorthGrip_IsDerivedFromNorthIdleInsteadOfLegacyOutput()
    {
        AnimationClip source = AssetDatabase.LoadAssetAtPath<AnimationClip>(
            "Assets/Animations/Weapons/Rapier_Attack.anim");
        AnimationClip idle = AssetDatabase.LoadAssetAtPath<AnimationClip>(
            "Assets/Animations/Player/Idle/Idle_N.anim");
        AnimationClip result = DirectionalAnimationGenerator.CreateClip(source, "N", "Rapier");
        try
        {
            string property = "m_LocalPosition.x";
            EditorCurveBinding binding = new EditorCurveBinding
            {
                path = Grip, type = typeof(Transform), propertyName = property
            };
            float expected = Curve(idle, Grip, property).Evaluate(0f);
            Assert.That(Curve(result, Grip, property).Evaluate(0f), Is.EqualTo(expected).Within(0.00001f));
            Assert.That(Curve(result, Grip, property).Evaluate(result.length), Is.EqualTo(expected).Within(0.00001f));
            Assert.That(AnimationUtility.GetEditorCurve(result, binding), Is.Not.Null);
        }
        finally { UnityEngine.Object.DestroyImmediate(result); }
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

    // Drawn second hand position expressed in the main hand's rotated grip frame at a clip time.
    private static Vector2 GripRelation(AnimationClip clip, float time, Vector2 grip, Vector2 anchor)
    {
        float mainAngle = Curve(clip, Hand, "localEulerAnglesRaw.z").Evaluate(time) * Mathf.Deg2Rad;
        float secondAngle = Curve(clip, SecondHand, "localEulerAnglesRaw.z").Evaluate(time) * Mathf.Deg2Rad;
        Vector2 main = new Vector2(Curve(clip, Hand, "m_LocalPosition.x").Evaluate(time), Curve(clip, Hand, "m_LocalPosition.y").Evaluate(time));
        Vector2 second = new Vector2(Curve(clip, SecondHand, "m_LocalPosition.x").Evaluate(time), Curve(clip, SecondHand, "m_LocalPosition.y").Evaluate(time));
        Vector2 drawnSecond = second + Rotate(anchor, secondAngle);
        Vector2 gripPoint = main + Rotate(grip, mainAngle);
        return Rotate(drawnSecond - gripPoint, -mainAngle);
    }

    private static WeaponDefinition LongSwordDefinition() =>
        AssetDatabase.LoadAssetAtPath<WeaponDefinition>(LongSwordDefinitionPath);

    private static WeaponDefinition TwoHandedDefinition(string weapon) =>
        AssetDatabase.LoadAssetAtPath<WeaponDefinition>(weapon == "LongSword" ? LongSwordDefinitionPath :
            weapon == "Zweihander" ? ZweihanderDefinitionPath :
            weapon == "MagicStaff" ? MagicStaffDefinitionPath : LongBowDefinitionPath);

    private static AnimationClip TwoHandedSource(string weapon) =>
        AssetDatabase.LoadAssetAtPath<AnimationClip>($"Assets/Animations/Weapons/{weapon}_Attack.anim");

    private static string TwoHandedOutput(string weapon, string direction) =>
        $"Assets/Animations/Weapons/Directional/{weapon}/{weapon}_Attack_{direction}.anim";

    private static Vector2 IdleGrip(string direction)
    {
        AnimationClip idle = AssetDatabase.LoadAssetAtPath<AnimationClip>($"Assets/Animations/Player/Idle/Idle_{direction}.anim");
        return new Vector2(Curve(idle, Grip, "m_LocalPosition.x").Evaluate(0f), Curve(idle, Grip, "m_LocalPosition.y").Evaluate(0f));
    }

    private static Vector2 IdleHandAnchor(string hand, string direction)
    {
        string path = hand == "Left" ? SecondHand : Hand;
        AnimationClip idle = AssetDatabase.LoadAssetAtPath<AnimationClip>(
            $"Assets/Animations/Player/Idle/{hand}Hand/{hand}Hand_Idle_{direction}.anim");
        var sprite = new EditorCurveBinding { path = path, type = typeof(SpriteRenderer), propertyName = "m_Sprite" };
        return DirectionalAnimationGenerator.ResolveSpriteAnchor((Sprite)AnimationUtility.GetObjectReferenceCurve(idle, sprite)[0].value);
    }

    private static Vector2 Position(AnimationClip clip, string path, float time) => new Vector2(
        Curve(clip, path, "m_LocalPosition.x").Evaluate(time), Curve(clip, path, "m_LocalPosition.y").Evaluate(time));

    private static Vector2 SecondHandAnchor(string direction)
    {
        AnimationClip idle = AssetDatabase.LoadAssetAtPath<AnimationClip>($"Assets/Animations/Player/Idle/LeftHand/LeftHand_Idle_{direction}.anim");
        var sprite = new EditorCurveBinding { path = SecondHand, type = typeof(SpriteRenderer), propertyName = "m_Sprite" };
        return DirectionalAnimationGenerator.ResolveSpriteAnchor((Sprite)AnimationUtility.GetObjectReferenceCurve(idle, sprite)[0].value);
    }

    private static Vector2 Rotate(Vector2 value, float radians) => new Vector2(
        Mathf.Cos(radians) * value.x - Mathf.Sin(radians) * value.y,
        Mathf.Sin(radians) * value.x + Mathf.Cos(radians) * value.y);

    private static bool SameBinding(EditorCurveBinding actual, EditorCurveBinding expected) =>
        actual.path == expected.path && actual.type == expected.type && actual.propertyName == expected.propertyName;

    private static AnimationCurve Curve(AnimationClip clip, string path, string property) =>
        AnimationUtility.GetEditorCurve(clip,
            new EditorCurveBinding { path = path, type = property == "m_SortingOrder" ? typeof(SpriteRenderer) : typeof(Transform), propertyName = property });
}
