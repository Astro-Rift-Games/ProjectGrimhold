using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class DirectionalAnimationGenerator
{
    private const string Hand = "RightHandPivot/RightHand";
    private const string Grip = Hand + "/MainHandGrip";
    private const string SecondHand = "LeftHandPivot/LeftHand";
    private const string OffHandGrip = SecondHand + "/OffHandGrip";
    private const string WeaponPose = "WeaponPose";
    private const string AngleZ = "localEulerAnglesRaw.z";
    private const string OutputRoot = "Assets/Animations/Weapons/Directional";
    private const int NorthHandSortingOrder = -2;
    // A two-handed second hand holds the handle over the weapon and under the main hand: between the
    // front weapon (20) / attack VFX (21) and main hand (30), or between the back weapon (-10) and the
    // north main hand (-2). The next slot of each stays free for its glove.
    private const int FrontSecondHandSortingOrder = 25;
    private const int BackSecondHandSortingOrder = -5;
    // A raised off hand keeps the LeftHand renderer's base order (4, between the torso and the head) where it
    // stays beside the body, goes behind the body like the north main hand where it turns away from the view
    // (N, NE), and crosses in front of the main hand (30) and its glove (31) in SW. Its glove follows one above.
    public const int OffHandSortingOrder = 4;
    public const int OffHandOverMainHandSortingOrder = 32;
    // The presenter points the held weapon along the facing, which is -90 degrees for south.
    private const float SouthFacingAngle = -90f;
    private const float DerivativeStep = 0.0005f;
    // A weapon-driven source starts and ends in its authored ready pose, away from the locomotion rest, and the
    // Attack transitions are instant: its bake eases in from and back out to the facing's idle pose over this time.
    public const float WeaponDrivenBlendSeconds = 0.1f;
    private static readonly string[] Directions = { "N", "NE", "NW", "S", "SE", "SW" };
    private static readonly float[] Angles = { 180f, 135f, -135f, 0f, 45f, -45f };

    [MenuItem("Tools/Animations/Generate ArmingSword Directional Attacks")]
    private static void GenerateArmingSwordAssets() => GenerateAssets("ArmingSword");

    [MenuItem("Tools/Animations/Generate Rapier Directional Attacks")]
    public static void GenerateRapierAssets() => GenerateAssets("Rapier");

    [MenuItem("Tools/Animations/Generate Rondel Dagger Directional Attacks")]
    public static void GenerateRondelDaggerAssets() => GenerateAssets("RondelDagger");

    [MenuItem("Tools/Animations/Generate Magic Wand Directional Attacks")]
    public static void GenerateMagicWandAssets() => GenerateAssets("MagicWand");

    [MenuItem("Tools/Animations/Generate Magic Sword Directional Attacks")]
    public static void GenerateMagicSwordAssets() => GenerateAssets("MagicSword");

    [MenuItem("Tools/Animations/Generate LongSword Directional Attacks")]
    public static void GenerateLongSwordAssets() => GenerateAssets("LongSword",
        RequireWeapon("Assets/Scriptable Objects/Loot/Definitions/LongSwordCombatDefinition.asset"));

    [MenuItem("Tools/Animations/Generate Zweihander Directional Attacks")]
    public static void GenerateZweihanderAssets() => GenerateAssets("Zweihander",
        RequireWeapon("Assets/Scriptable Objects/Loot/Definitions/ZweihanderWeaponDefinition.asset"));

    [MenuItem("Tools/Animations/Generate Great Hammer Directional Attacks")]
    public static void GenerateGreatHammerAssets() => GenerateAssets("GreatHammer",
        RequireWeapon("Assets/Scriptable Objects/Loot/Definitions/GreatHammerWeaponDefinition.asset"));

    [MenuItem("Tools/Animations/Generate Magic Staff Directional Attacks")]
    public static void GenerateMagicStaffAssets() => GenerateAssets("MagicStaff",
        RequireWeapon("Assets/Scriptable Objects/Loot/Definitions/MagicStaffWeaponDefinition.asset"));

    [MenuItem("Tools/Animations/Generate Long Bow Directional Attacks")]
    public static void GenerateLongBowAssets() => GenerateAssets("LongBow",
        RequireWeapon("Assets/Scriptable Objects/Loot/Definitions/LongBowWeaponDefinition.asset"));

    // Compound Bow's south source is authored under the name of its RecurveBow art.
    [MenuItem("Tools/Animations/Generate Compound Bow Directional Attacks")]
    public static void GenerateCompoundBowAssets() => GenerateAssets("CompoundBow",
        RequireWeapon("Assets/Scriptable Objects/Loot/Definitions/CompoundBowWeaponDefinition.asset"), "RecurveBow");

    [MenuItem("Tools/Animations/Generate Shield Directional Defend")]
    public static void GenerateShieldDefendAssets() => GenerateOffHandAssets("Shield", "Block", "Defend");

    // Outside attacks, OffHandGrip and a weapon-driven WeaponPose rest in the drawn left hand.
    // Every Idle and Walk body clip keys both at each matching LeftHand sprite frame's opaque-pixel
    // centroid, stepped like the authored MainHandGrip keys. WeaponPose also rests unrotated.
    [MenuItem("Tools/Animations/Generate Weapon Pose Locomotion")]
    public static void GenerateWeaponPoseLocomotion()
    {
        foreach (string motion in new[] { "Idle", "Walk" })
        {
            foreach (string direction in Directions)
            {
                string path = $"Assets/Animations/Player/{motion}/{motion}_{direction}.anim";
                AnimationClip body = RequireClip(path);
                AnimationClip hand = RequireClip($"Assets/Animations/Player/{motion}/LeftHand/LeftHand_{motion}_{direction}.anim");
                WriteWeaponPoseLocomotion(body, hand);
                EditorUtility.SetDirty(body);
            }
        }
        AssetDatabase.SaveAssets();
    }

    public static void WriteWeaponPoseLocomotion(AnimationClip body, AnimationClip hand)
    {
        if (body == null) throw new ArgumentNullException(nameof(body));
        if (hand == null) throw new ArgumentNullException(nameof(hand));
        ObjectReferenceKeyframe[] frames = AnimationUtility.GetObjectReferenceCurve(hand,
            Binding(SecondHand, typeof(SpriteRenderer), "m_Sprite"));
        if (frames == null || frames.Length == 0 || frames[0].time != 0f)
            throw new InvalidOperationException($"Missing LeftHand sprite frames at time zero in {hand.name}.");

        var times = new System.Collections.Generic.List<float>();
        var anchors = new System.Collections.Generic.List<Vector2>();
        foreach (ObjectReferenceKeyframe frame in frames)
        {
            if (!(frame.value is Sprite sprite))
                throw new InvalidOperationException($"LeftHand frame at {frame.time} in {hand.name} is not a sprite.");
            times.Add(frame.time);
            anchors.Add(ResolveSpriteAnchor(sprite));
        }
        // The body loop holds the last frame until its end, as its MainHandGrip keys do.
        if (body.length - times[times.Count - 1] > 0.0001f)
        {
            times.Add(body.length);
            anchors.Add(anchors[anchors.Count - 1]);
        }

        foreach (string axis in new[] { "x", "y", "z" })
        {
            var curve = new AnimationCurve(times.Select((time, i) => new Keyframe(time,
                axis == "x" ? anchors[i].x : axis == "y" ? anchors[i].y : 0f)).ToArray());
            for (int i = 0; i < curve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Constant);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Constant);
            }
            AnimationUtility.SetEditorCurve(body, Binding(WeaponPose, typeof(Transform), "m_LocalPosition." + axis), curve);
            AnimationUtility.SetEditorCurve(body, Binding(OffHandGrip, typeof(Transform), "m_LocalPosition." + axis), curve);
            AnimationUtility.SetEditorCurve(body, Binding(WeaponPose, typeof(Transform), "localEulerAnglesRaw." + axis),
                Constant(0f, body.length));
        }
    }

    // A two-handed weapon passes its definition: its second-hand presentation selects whether its handle
    // geometry places the second hand or the hand keeps its authored motion. The south source defaults to
    // <weaponName>_Attack.anim; a source authored under another name is passed explicitly.
    public static void GenerateAssets(string weaponName, WeaponDefinition weapon = null, string sourceName = null)
    {
        ValidateWeaponName(weaponName);
        sourceName ??= weaponName;
        ValidateWeaponName(sourceName);
        WeaponHandedness handedness = weapon != null ? weapon.Handedness : WeaponHandedness.OneHanded;
        AnimationClip original = RequireClip($"Assets/Animations/Weapons/{sourceName}_Attack.anim");
        foreach (string direction in Directions)
        {
            RequireClip($"Assets/Animations/Player/Idle/Idle_{direction}.anim");
            RequireClip($"Assets/Animations/Player/Idle/RightHand/RightHand_Idle_{direction}.anim");
            if (handedness == WeaponHandedness.TwoHanded)
                RequireClip($"Assets/Animations/Player/Idle/LeftHand/LeftHand_Idle_{direction}.anim");
        }

        if (!AssetDatabase.IsValidFolder($"{OutputRoot}/{weaponName}"))
            AssetDatabase.CreateFolder(OutputRoot, weaponName);
        foreach (string direction in Directions)
        {
            Bake(original, direction,
                $"{OutputRoot}/{weaponName}/{weaponName}_Attack_{direction}.anim",
                weaponName, weapon);
        }
    }

    public static void Bake(AnimationClip original, string direction, string path, string weaponName,
        WeaponDefinition weapon = null)
    {
        if (original == null) throw new ArgumentNullException(nameof(original));
        ValidateWeaponName(weaponName);
        ValidateOutputPath(original, path);
        WriteGenerated(CreateClip(original, direction, weaponName, weapon), path);
    }

    // An off-hand item's south source <itemName>_<sourceMotion>.anim drives only the LeftHand transform, which
    // carries OffHandGrip. Each facing gets <itemName>_<outputMotion>_<direction>.anim.
    public static void GenerateOffHandAssets(string itemName, string sourceMotion, string outputMotion)
    {
        ValidateWeaponName(itemName);
        ValidateWeaponName(sourceMotion);
        ValidateWeaponName(outputMotion);
        AnimationClip original = RequireClip($"Assets/Animations/Weapons/{itemName}_{sourceMotion}.anim");
        if (!AssetDatabase.IsValidFolder($"{OutputRoot}/{itemName}"))
            AssetDatabase.CreateFolder(OutputRoot, itemName);
        foreach (string direction in Directions)
        {
            string name = $"{itemName}_{outputMotion}_{direction}";
            string path = $"{OutputRoot}/{itemName}/{name}.anim";
            ValidateOutputPath(original, path);
            WriteGenerated(CreateOffHandClip(original, direction, name), path);
        }
    }

    // The off-hand position trajectory turns with the facing under the main hand's rule, while its rotation art,
    // depth and key times stay authored, so south reproduces the source. As MainHandGrip does for the main hand,
    // OffHandGrip holds the facing's drawn LeftHand idle anchor, so the held item follows the drawn hand, and the
    // hand keeps that idle sprite. The output never loops: a state that keeps playing it holds the final pose.
    public static AnimationClip CreateOffHandClip(AnimationClip original, string direction, string clipName)
    {
        if (original == null) throw new ArgumentNullException(nameof(original));
        ValidateWeaponName(clipName);
        int index = Array.IndexOf(Directions, direction);
        if (index < 0) throw new ArgumentException("Unknown facing direction.", nameof(direction));
        if (AnimationUtility.GetObjectReferenceCurveBindings(original).Length > 0 ||
            AnimationUtility.GetCurveBindings(original).Any(binding => !IsSecondHandTransform(binding)))
            throw new ArgumentException($"Off-hand source {original.name} may animate only the {SecondHand} transform.",
                nameof(original));

        Sprite handSprite = RequireIdleSprite(
            RequireClip($"Assets/Animations/Player/Idle/LeftHand/LeftHand_Idle_{direction}.anim"), SecondHand, direction);
        Vector2 anchor = ResolveSpriteAnchor(handSprite);
        AnimationClip result = UnityEngine.Object.Instantiate(original);
        try
        {
            result.name = clipName;
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(result);
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(result, settings);
            WriteTrajectory(result, SecondHand, RotateTrajectory(original, SecondHand, Angles[index] * Mathf.Deg2Rad));
            foreach (string axis in new[] { "x", "y", "z" })
            {
                AnimationUtility.SetEditorCurve(result, Binding(OffHandGrip, typeof(Transform), "m_LocalPosition." + axis),
                    Constant(axis == "x" ? anchor.x : axis == "y" ? anchor.y : 0f, original.length));
            }
            AnimationUtility.SetObjectReferenceCurve(result, Binding(SecondHand, typeof(SpriteRenderer), "m_Sprite"),
                new[] { new ObjectReferenceKeyframe { time = 0f, value = handSprite } });
            AnimationUtility.SetEditorCurve(result, Binding(SecondHand, typeof(SpriteRenderer), "m_SortingOrder"),
                Constant(ResolveOffHandSortingOrder(direction), original.length));
            return result;
        }
        catch
        {
            UnityEngine.Object.DestroyImmediate(result);
            throw;
        }
    }

    public static int ResolveOffHandSortingOrder(string direction) => direction switch
    {
        "N" or "NE" => NorthHandSortingOrder,
        "SW" => OffHandOverMainHandSortingOrder,
        _ => OffHandSortingOrder
    };

    private static void ValidateOutputPath(AnimationClip original, string path)
    {
        if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal) ||
            !path.EndsWith(".anim", StringComparison.Ordinal))
            throw new ArgumentException("Expected an asset animation path.", nameof(path));
        string sourcePath = AssetDatabase.GetAssetPath(original);
        if (!string.IsNullOrEmpty(sourcePath) && string.Equals(path, sourcePath, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Destination cannot overwrite the source animation clip.", nameof(path));
    }

    private static void WriteGenerated(AnimationClip generated, string path)
    {
        try
        {
            AnimationClip destination = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (destination == null)
            {
                AssetDatabase.CreateAsset(new AnimationClip(), path);
                destination = RequireClip(path);
            }
            EditorUtility.CopySerialized(generated, destination);
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(generated))
            {
                AnimationCurve curve = AnimationUtility.GetEditorCurve(generated, binding);
                AnimationUtility.SetEditorCurve(destination, binding, null);
                AnimationUtility.SetEditorCurve(destination, binding, curve);
            }
            foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(generated))
            {
                ObjectReferenceKeyframe[] curve = AnimationUtility.GetObjectReferenceCurve(generated, binding);
                AnimationUtility.SetObjectReferenceCurve(destination, binding, null);
                AnimationUtility.SetObjectReferenceCurve(destination, binding, curve);
            }
            destination.name = generated.name;
            EditorUtility.SetDirty(destination);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(generated);
        }
    }

    public static AnimationClip CreateClip(AnimationClip original, string direction, string weaponName,
        WeaponDefinition weapon = null)
    {
        if (original == null) throw new ArgumentNullException(nameof(original));
        ValidateWeaponName(weaponName);
        int index = Array.IndexOf(Directions, direction);
        if (index < 0) throw new ArgumentException("Unknown facing direction.", nameof(direction));
        bool twoHanded = weapon != null && weapon.Handedness == WeaponHandedness.TwoHanded;
        bool holdsSecondaryGrip = twoHanded &&
            weapon.Presentation.SecondHand == SecondHandPresentation.HoldsSecondaryGrip;
        if (holdsSecondaryGrip && weapon.Presentation.SecondaryGripPoint == weapon.Presentation.GripPoint)
            throw new ArgumentException($"Two-handed weapon {weapon.name} needs a secondary grip point.", nameof(weapon));

        AnimationClip idle = RequireClip($"Assets/Animations/Player/Idle/Idle_{direction}.anim");
        AnimationClip handIdle = RequireClip($"Assets/Animations/Player/Idle/RightHand/RightHand_Idle_{direction}.anim");
        AnimationClip result = UnityEngine.Object.Instantiate(original);
        try
        {
            result.name = $"{weaponName}_Attack_{direction}";
            // Main Hand attacks drive only the RightHand hierarchy; LeftHand carries OffHandGrip and
            // belongs to off-hand clips. A two-handed weapon blocks the Off Hand, so its authored
            // LeftHand transform is its second hand and stays in the output.
            RemoveBindingsOutsideContract(result, twoHanded);
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(result);
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(result, settings);

            float radians = Angles[index] * Mathf.Deg2Rad;
            WriteTrajectory(result, Hand, RotateTrajectory(original, Hand, radians));

            foreach (string axis in new[] { "x", "y", "z" })
            {
                EditorCurveBinding grip = Binding(Grip, typeof(Transform), "m_LocalPosition." + axis);
                float value = RequireCurve(idle, grip).Evaluate(0f);
                AnimationUtility.SetEditorCurve(result, grip, Constant(value, original.length));
            }
            EditorCurveBinding sprite = Binding(Hand, typeof(SpriteRenderer), "m_Sprite");
            AnimationUtility.SetObjectReferenceCurve(result, sprite,
                new[] { new ObjectReferenceKeyframe { time = 0f, value = RequireIdleSprite(handIdle, Hand, direction) } });

            bool north = direction.StartsWith("N", StringComparison.Ordinal);
            EditorCurveBinding sorting = Binding(Hand, typeof(SpriteRenderer), "m_SortingOrder");
            AnimationUtility.SetEditorCurve(result, sorting,
                north ? Constant(NorthHandSortingOrder, original.length) : null);

            if (twoHanded && weapon.Presentation.Rig == WeaponRig.WeaponDriven)
            {
                ApplyWeaponDrivenRig(original, result, direction, radians);
                BlendFromAndToLocomotion(result, idle, original.length);
                return result;
            }
            else if (holdsSecondaryGrip)
            {
                ApplySecondHand(original, result, direction, radians, idle, weapon.Presentation);
                AnimationUtility.SetEditorCurve(result, Binding(SecondHand, typeof(SpriteRenderer), "m_SortingOrder"),
                    Constant(north ? BackSecondHandSortingOrder : FrontSecondHandSortingOrder, original.length));
            }
            else if (twoHanded)
            {
                // A second hand that does not hold the weapon keeps its own authored motion under the same
                // rule as the main hand: its trajectory turns with the facing, while its rotation art, depth,
                // key times and LeftHand-layer sorting stay authored.
                WriteTrajectory(result, SecondHand, RotateTrajectory(original, SecondHand, radians));
            }
            return result;
        }
        catch
        {
            UnityEngine.Object.DestroyImmediate(result);
            throw;
        }
    }

    /// <summary>
    /// Opaque-pixel centroid of a hand sprite in its local units, measured from the sprite pivot.
    /// It is where the drawn hand sits relative to its animated transform.
    /// </summary>
    public static Vector2 ResolveSpriteAnchor(Sprite sprite)
    {
        if (sprite == null) throw new ArgumentNullException(nameof(sprite));
        string path = AssetDatabase.GetAssetPath(sprite.texture);
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            // Decode the source file so sampling does not depend on the importer's Read/Write flag.
            if (!texture.LoadImage(File.ReadAllBytes(path)) ||
                texture.width != sprite.texture.width || texture.height != sprite.texture.height)
                throw new InvalidOperationException($"Cannot sample source pixels of {sprite.name} at {path}.");
            Rect rect = sprite.rect;
            Color32[] pixels = texture.GetPixels32();
            double sumX = 0d, sumY = 0d;
            int count = 0;
            for (int y = (int)rect.y; y < (int)rect.yMax; y++)
            {
                for (int x = (int)rect.x; x < (int)rect.xMax; x++)
                {
                    if (pixels[y * texture.width + x].a == 0) continue;
                    sumX += x + 0.5d - rect.x;
                    sumY += y + 0.5d - rect.y;
                    count++;
                }
            }
            if (count == 0) throw new InvalidOperationException($"Hand sprite {sprite.name} has no opaque pixels.");
            return new Vector2(
                (float)((sumX / count - sprite.pivot.x) / sprite.pixelsPerUnit),
                (float)((sumY / count - sprite.pivot.y) / sprite.pixelsPerUnit));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    // The second hand keeps its authored key times and rotation art; its position holds the weapon's
    // secondary grip point. That point follows the same path the presenter gives the held weapon:
    // MainHandGrip turned by the main-hand rotation, then the facing (mirrored when facing left) and the
    // weapon's angle correction resolved for that mirror by PlayerWeaponPresentationMath, so the handle
    // mirrors across the weapon's own art axis exactly as the presenter draws it. The facing's LeftHand idle sprite anchor is
    // removed so the drawn hand, not its transform origin, lands on the handle.
    private static void ApplySecondHand(AnimationClip original, AnimationClip result, string direction,
        float radians, AnimationClip idle, WeaponDefinition.PresentationConfig presentation)
    {
        Vector2 anchor = ResolveSpriteAnchor(RequireIdleSprite(
            RequireClip($"Assets/Animations/Player/Idle/LeftHand/LeftHand_Idle_{direction}.anim"), SecondHand, direction));
        float facing = radians + SouthFacingAngle * Mathf.Deg2Rad;
        bool mirrored = Mathf.Cos(facing) < -0.0001f;
        Vector2 handle = Rotate(presentation.SecondaryGripPoint - presentation.GripPoint,
            PlayerWeaponPresentationMath.ResolveAngleCorrection(presentation.AngleCorrection, mirrored) * Mathf.Deg2Rad);
        if (mirrored) handle.y = -handle.y;
        Vector2 secondGrip = IdleGrip(idle) + Rotate(handle, facing);
        AnimationCurve mainX = RequireCurve(original, Binding(Hand, typeof(Transform), "m_LocalPosition.x"));
        AnimationCurve mainY = RequireCurve(original, Binding(Hand, typeof(Transform), "m_LocalPosition.y"));
        AnimationCurve mainAngle = RequireCurve(original, Binding(Hand, typeof(Transform), AngleZ));
        AnimationCurve secondX = RequireCurve(original, Binding(SecondHand, typeof(Transform), "m_LocalPosition.x"));
        AnimationCurve secondY = RequireCurve(original, Binding(SecondHand, typeof(Transform), "m_LocalPosition.y"));
        AnimationCurve secondAngle = RequireCurve(original, Binding(SecondHand, typeof(Transform), AngleZ));

        // Validates the authored second-hand keys like the main hand's before re-deriving them.
        RotateTrajectory(original, SecondHand, radians);
        // The re-derived position follows the hand rotation along an arc, so it is keyed at every authored
        // key time plus the clip's frame grid; sparse authored keys alone would cut across fast turns.
        float lastKey = secondX.keys[secondX.length - 1].time;
        float[] times = secondX.keys.Select(key => key.time)
            .Concat(Enumerable.Range(0, Mathf.FloorToInt(lastKey * original.frameRate + 0.001f) + 1)
                .Select(frame => frame / original.frameRate))
            .OrderBy(time => time)
            .Aggregate(new System.Collections.Generic.List<float>(), (list, time) =>
            {
                if (list.Count == 0 || time - list[list.Count - 1] > 0.001f) list.Add(time);
                return list;
            })
            .ToArray();
        WriteResampled(result, SecondHand, times, secondX, secondY, time =>
        {
            Vector2 mainHand = Rotate(new Vector2(mainX.Evaluate(time), mainY.Evaluate(time)), radians);
            return mainHand + Rotate(secondGrip, mainAngle.Evaluate(time) * Mathf.Deg2Rad) -
                Rotate(anchor, secondAngle.Evaluate(time) * Mathf.Deg2Rad);
        });
    }

    // Weapon-driven rig: the weapon owns its pose and the hands are placed on it. The south source is read as
    // drawn positions (hand transform plus its idle sprite anchor turned by the hand's rotation): the authored
    // left hand is the weapon grip, so it becomes the WeaponPose trajectory, and the authored right hand is the
    // target on the weapon that the other hand follows. Both turn with the facing about the aim center, the
    // source's string-hand height at its authored draw hold on the body axis, so a south forward motion becomes
    // a forward motion in every facing while the aim height stays put. Each runtime hand is then keyed so its
    // drawn centroid lands exactly on its point; rotation art stays authored and WeaponPose takes the bow arm's.
    private static void ApplyWeaponDrivenRig(AnimationClip original, AnimationClip result, string direction, float radians)
    {
        Vector2 mainSouth = HandIdleAnchor("RightHand", Hand, "S");
        Vector2 secondSouth = HandIdleAnchor("LeftHand", SecondHand, "S");
        Vector2 mainAnchor = HandIdleAnchor("RightHand", Hand, direction);
        Vector2 secondAnchor = HandIdleAnchor("LeftHand", SecondHand, direction);
        AnimationCurve mainX = RequireCurve(original, Binding(Hand, typeof(Transform), "m_LocalPosition.x"));
        AnimationCurve mainY = RequireCurve(original, Binding(Hand, typeof(Transform), "m_LocalPosition.y"));
        AnimationCurve mainAngle = AnimationUtility.GetEditorCurve(original, Binding(Hand, typeof(Transform), AngleZ));
        AnimationCurve secondX = RequireCurve(original, Binding(SecondHand, typeof(Transform), "m_LocalPosition.x"));
        AnimationCurve secondY = RequireCurve(original, Binding(SecondHand, typeof(Transform), "m_LocalPosition.y"));
        AnimationCurve secondAngle = AnimationUtility.GetEditorCurve(original, Binding(SecondHand, typeof(Transform), AngleZ));
        // Validates both authored trajectories like any rotated hand before re-deriving them.
        RotateTrajectory(original, Hand, radians);
        RotateTrajectory(original, SecondHand, radians);

        float Angle(AnimationCurve curve, float time) => curve != null ? curve.Evaluate(time) * Mathf.Deg2Rad : 0f;
        Vector2 DrawnMain(float time) =>
            new Vector2(mainX.Evaluate(time), mainY.Evaluate(time)) + Rotate(mainSouth, Angle(mainAngle, time));
        Vector2 DrawnSecond(float time) =>
            new Vector2(secondX.Evaluate(time), secondY.Evaluate(time)) + Rotate(secondSouth, Angle(secondAngle, time));
        Vector2 aim = new Vector2(0f, DrawnMain(ResolveDrawHold(mainX, mainY)).y);
        Vector2 Turn(Vector2 point) => aim + Rotate(point - aim, radians);

        float[] times = KeyAndFrameTimes(original, mainX, secondX);
        WriteResampled(result, WeaponPose, times, secondX, secondY, time => Turn(DrawnSecond(time)));
        WriteResampled(result, SecondHand, times, secondX, secondY, time =>
            Turn(DrawnSecond(time)) - Rotate(secondAnchor, Angle(secondAngle, time)));
        WriteResampled(result, Hand, times, mainX, mainY, time =>
            Turn(DrawnMain(time)) - Rotate(mainAnchor, Angle(mainAngle, time)));
        AnimationUtility.SetEditorCurve(result, Binding(WeaponPose, typeof(Transform), "m_LocalPosition.z"),
            Constant(0f, original.length));
        foreach (string axis in new[] { "x", "y", "z" })
        {
            AnimationCurve authored = AnimationUtility.GetEditorCurve(original,
                Binding(SecondHand, typeof(Transform), "localEulerAnglesRaw." + axis));
            AnimationUtility.SetEditorCurve(result, Binding(WeaponPose, typeof(Transform), "localEulerAnglesRaw." + axis),
                authored ?? Constant(0f, original.length));
        }
    }

    // Delays the whole authored motion by one blend and keys the facing's idle pose on both sides of it, so the
    // hands and WeaponPose ease out of and back into locomotion instead of snapping at the instant transitions.
    // Other curves keep their first and last values across the added time.
    private static void BlendFromAndToLocomotion(AnimationClip result, AnimationClip idle, float authoredLength)
    {
        float end = authoredLength + 2f * WeaponDrivenBlendSeconds;
        foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(result))
        {
            AnimationCurve curve = AnimationUtility.GetEditorCurve(result, binding);
            Keyframe[] keys = curve.keys;
            for (int i = 0; i < keys.Length; i++)
                keys[i].time += WeaponDrivenBlendSeconds;
            bool posed = binding.type == typeof(Transform) &&
                (binding.path == Hand || binding.path == SecondHand || binding.path == WeaponPose);
            // The locomotion rest: idle keys WeaponPose, and leaves both hand transforms at their origin.
            AnimationCurve rest = posed ? AnimationUtility.GetEditorCurve(idle, binding) : null;
            float restValue = rest != null ? rest.Evaluate(0f) : 0f;
            var padded = new System.Collections.Generic.List<Keyframe>(keys.Length + 2)
            {
                new Keyframe(0f, posed ? restValue : keys[0].value, 0f, 0f)
            };
            padded.AddRange(keys);
            padded.Add(new Keyframe(end, posed ? restValue : keys[keys.Length - 1].value, 0f, 0f));
            AnimationUtility.SetEditorCurve(result, binding,
                new AnimationCurve(padded.ToArray()) { preWrapMode = curve.preWrapMode, postWrapMode = curve.postWrapMode });
        }
    }

    // The authored draw hold: the first two consecutive string-hand keys at the same position.
    private static float ResolveDrawHold(AnimationCurve x, AnimationCurve y)
    {
        Keyframe[] xs = x.keys;
        Keyframe[] ys = y.keys;
        for (int i = 0; i + 1 < xs.Length; i++)
        {
            if (Mathf.Approximately(xs[i].value, xs[i + 1].value) && Mathf.Approximately(ys[i].value, ys[i + 1].value))
                return xs[i].time;
        }
        throw new InvalidOperationException("A weapon-driven attack needs an authored draw hold on the main hand.");
    }

    // Every authored key time of both hands plus the clip's frame grid, so derived arcs follow fast turns.
    private static float[] KeyAndFrameTimes(AnimationClip original, AnimationCurve first, AnimationCurve second)
    {
        float lastKey = Mathf.Max(first.keys[first.length - 1].time, second.keys[second.length - 1].time);
        return first.keys.Select(key => key.time)
            .Concat(second.keys.Select(key => key.time))
            .Concat(Enumerable.Range(0, Mathf.FloorToInt(lastKey * original.frameRate + 0.001f) + 1)
                .Select(frame => frame / original.frameRate))
            .OrderBy(time => time)
            .Aggregate(new System.Collections.Generic.List<float>(), (list, time) =>
            {
                if (list.Count == 0 || time - list[list.Count - 1] > 0.001f) list.Add(time);
                return list;
            })
            .ToArray();
    }

    private static Vector2 HandIdleAnchor(string hand, string path, string direction) =>
        ResolveSpriteAnchor(RequireIdleSprite(
            RequireClip($"Assets/Animations/Player/Idle/{hand}/{hand}_Idle_{direction}.anim"), path, direction));

    private static (Keyframe[] x, Keyframe[] y, AnimationCurve sourceX, AnimationCurve sourceY) RotateTrajectory(
        AnimationClip original, string path, float radians)
    {
        AnimationCurve sourceX = RequireCurve(original, Binding(path, typeof(Transform), "m_LocalPosition.x"));
        AnimationCurve sourceY = RequireCurve(original, Binding(path, typeof(Transform), "m_LocalPosition.y"));
        Keyframe[] xKeys = sourceX.keys;
        Keyframe[] yKeys = sourceY.keys;
        if (xKeys.Length != yKeys.Length || xKeys.Where((key, i) => key.time != yKeys[i].time).Any())
            throw new InvalidOperationException($"South {path} position axes must share key times.");

        float cos = Mathf.Cos(radians);
        float sin = Mathf.Sin(radians);
        for (int i = 0; i < xKeys.Length; i++)
        {
            Keyframe a = xKeys[i];
            Keyframe b = yKeys[i];
            if (a.weightedMode != WeightedMode.None || b.weightedMode != WeightedMode.None ||
                a.inWeight != b.inWeight || a.outWeight != b.outWeight ||
                AnimationUtility.GetKeyLeftTangentMode(sourceX, i) != AnimationUtility.GetKeyLeftTangentMode(sourceY, i) ||
                AnimationUtility.GetKeyRightTangentMode(sourceX, i) != AnimationUtility.GetKeyRightTangentMode(sourceY, i))
                throw new InvalidOperationException($"Incompatible {path} position tangent weights or modes at key {i}.");
            float originalX = a.value;
            a.value = cos * originalX - sin * b.value;
            b.value = sin * originalX + cos * b.value;
            float inX = a.inTangent;
            a.inTangent = cos * inX - sin * b.inTangent;
            b.inTangent = sin * inX + cos * b.inTangent;
            float outX = a.outTangent;
            a.outTangent = cos * outX - sin * b.outTangent;
            b.outTangent = sin * outX + cos * b.outTangent;
            xKeys[i] = a;
            yKeys[i] = b;
        }
        return (xKeys, yKeys, sourceX, sourceY);
    }

    private static void WriteTrajectory(AnimationClip result, string path,
        (Keyframe[] x, Keyframe[] y, AnimationCurve sourceX, AnimationCurve sourceY) trajectory)
    {
        AnimationUtility.SetEditorCurve(result, Binding(path, typeof(Transform), "m_LocalPosition.x"),
            new AnimationCurve(trajectory.x) { preWrapMode = trajectory.sourceX.preWrapMode, postWrapMode = trajectory.sourceX.postWrapMode });
        AnimationUtility.SetEditorCurve(result, Binding(path, typeof(Transform), "m_LocalPosition.y"),
            new AnimationCurve(trajectory.y) { preWrapMode = trajectory.sourceY.preWrapMode, postWrapMode = trajectory.sourceY.postWrapMode });
    }

    // Keys a derived x/y position at the given times with one-sided slopes of the derived function.
    private static void WriteResampled(AnimationClip result, string path, float[] times,
        AnimationCurve sourceX, AnimationCurve sourceY, Func<float, Vector2> position)
    {
        var xKeys = new Keyframe[times.Length];
        var yKeys = new Keyframe[times.Length];
        for (int i = 0; i < times.Length; i++)
        {
            float time = times[i];
            Vector2 value = position(time);
            Vector2 inSlope = (value - position(time - DerivativeStep)) / DerivativeStep;
            Vector2 outSlope = (position(time + DerivativeStep) - value) / DerivativeStep;
            xKeys[i] = new Keyframe(time, value.x, inSlope.x, outSlope.x);
            yKeys[i] = new Keyframe(time, value.y, inSlope.y, outSlope.y);
        }
        AnimationUtility.SetEditorCurve(result, Binding(path, typeof(Transform), "m_LocalPosition.x"),
            new AnimationCurve(xKeys) { preWrapMode = sourceX.preWrapMode, postWrapMode = sourceX.postWrapMode });
        AnimationUtility.SetEditorCurve(result, Binding(path, typeof(Transform), "m_LocalPosition.y"),
            new AnimationCurve(yKeys) { preWrapMode = sourceY.preWrapMode, postWrapMode = sourceY.postWrapMode });
    }

    private static void RemoveBindingsOutsideContract(AnimationClip clip, bool twoHanded)
    {
        foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
        {
            if (!IsHandBinding(binding) && !(twoHanded && IsSecondHandTransform(binding)))
                AnimationUtility.SetEditorCurve(clip, binding, null);
        }
        foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
        {
            if (!IsHandBinding(binding))
                AnimationUtility.SetObjectReferenceCurve(clip, binding, null);
        }
    }

    private static bool IsHandBinding(EditorCurveBinding binding) =>
        binding.path == Hand || binding.path.StartsWith(Hand + "/", StringComparison.Ordinal);

    // Only the second hand's own transform: its sprite stays with the LeftHand layer and OffHandGrip
    // stays empty while a two-handed weapon blocks the Off Hand.
    private static bool IsSecondHandTransform(EditorCurveBinding binding) =>
        binding.path == SecondHand && binding.type == typeof(Transform);

    private static Vector2 IdleGrip(AnimationClip idle) => new Vector2(
        RequireCurve(idle, Binding(Grip, typeof(Transform), "m_LocalPosition.x")).Evaluate(0f),
        RequireCurve(idle, Binding(Grip, typeof(Transform), "m_LocalPosition.y")).Evaluate(0f));

    private static WeaponDefinition RequireWeapon(string path) =>
        AssetDatabase.LoadAssetAtPath<WeaponDefinition>(path) ??
        throw new InvalidOperationException($"Missing weapon definition: {path}");

    private static Sprite RequireIdleSprite(AnimationClip handIdle, string path, string direction)
    {
        ObjectReferenceKeyframe[] sprites =
            AnimationUtility.GetObjectReferenceCurve(handIdle, Binding(path, typeof(SpriteRenderer), "m_Sprite"));
        if (sprites == null || sprites.Length == 0 || sprites[0].time != 0f || !(sprites[0].value is Sprite sprite))
            throw new InvalidOperationException($"Missing idle {path} sprite at time zero for {direction}.");
        return sprite;
    }

    private static Vector2 Rotate(Vector2 value, float radians)
    {
        float cos = Mathf.Cos(radians);
        float sin = Mathf.Sin(radians);
        return new Vector2(cos * value.x - sin * value.y, sin * value.x + cos * value.y);
    }

    private static AnimationCurve Constant(float value, float duration) =>
        new AnimationCurve(
            new Keyframe(0f, value, float.PositiveInfinity, float.PositiveInfinity),
            new Keyframe(duration, value, float.PositiveInfinity, float.PositiveInfinity));

    private static EditorCurveBinding Binding(string path, Type type, string property) =>
        new EditorCurveBinding { path = path, type = type, propertyName = property };

    private static AnimationCurve RequireCurve(AnimationClip clip, EditorCurveBinding binding) =>
        AnimationUtility.GetEditorCurve(clip, binding) ??
        throw new InvalidOperationException($"Missing {binding.path}/{binding.propertyName} in {clip.name}.");

    private static AnimationClip RequireClip(string path) =>
        AssetDatabase.LoadAssetAtPath<AnimationClip>(path) ??
        throw new InvalidOperationException($"Missing animation clip: {path}");

    private static void ValidateWeaponName(string weaponName)
    {
        if (string.IsNullOrEmpty(weaponName) || weaponName.IndexOfAny(new[] { '/', '\\' }) >= 0)
            throw new ArgumentException("Expected a simple weapon name.", nameof(weaponName));
    }
}
