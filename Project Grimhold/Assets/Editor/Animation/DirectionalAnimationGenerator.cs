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
    private const string AngleZ = "localEulerAnglesRaw.z";
    private const string OutputRoot = "Assets/Animations/Weapons/Directional";
    private const int NorthHandSortingOrder = -2;
    // A two-handed second hand holds the handle over the weapon and under the main hand: between the
    // front weapon (20) / attack VFX (21) and main hand (30), or between the back weapon (-10) and the
    // north main hand (-2). The next slot of each stays free for its glove.
    private const int FrontSecondHandSortingOrder = 25;
    private const int BackSecondHandSortingOrder = -5;
    // The presenter points the held weapon along the facing, which is -90 degrees for south.
    private const float SouthFacingAngle = -90f;
    private const float DerivativeStep = 0.0005f;
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

    [MenuItem("Tools/Animations/Generate Magic Staff Directional Attacks")]
    public static void GenerateMagicStaffAssets() => GenerateAssets("MagicStaff",
        RequireWeapon("Assets/Scriptable Objects/Loot/Definitions/MagicStaffWeaponDefinition.asset"));

    // A two-handed weapon passes its definition: its second-hand presentation selects whether its handle
    // geometry places the second hand or the hand keeps its authored motion.
    public static void GenerateAssets(string weaponName, WeaponDefinition weapon = null)
    {
        ValidateWeaponName(weaponName);
        WeaponHandedness handedness = weapon != null ? weapon.Handedness : WeaponHandedness.OneHanded;
        AnimationClip original = RequireClip($"Assets/Animations/Weapons/{weaponName}_Attack.anim");
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
        if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal) ||
            !path.EndsWith(".anim", StringComparison.Ordinal))
            throw new ArgumentException("Expected an asset animation path.", nameof(path));
        string sourcePath = AssetDatabase.GetAssetPath(original);
        if (!string.IsNullOrEmpty(sourcePath) && string.Equals(path, sourcePath, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Destination cannot overwrite the source animation clip.", nameof(path));

        AnimationClip generated = CreateClip(original, direction, weaponName, weapon);
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

            if (holdsSecondaryGrip)
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
    // MainHandGrip turned by the main-hand rotation, then the facing (blade along the facing, mirrored
    // when facing left) and the weapon's angle correction. The facing's LeftHand idle sprite anchor is
    // removed so the drawn hand, not its transform origin, lands on the handle.
    private static void ApplySecondHand(AnimationClip original, AnimationClip result, string direction,
        float radians, AnimationClip idle, WeaponDefinition.PresentationConfig presentation)
    {
        Vector2 anchor = ResolveSpriteAnchor(RequireIdleSprite(
            RequireClip($"Assets/Animations/Player/Idle/LeftHand/LeftHand_Idle_{direction}.anim"), SecondHand, direction));
        float facing = radians + SouthFacingAngle * Mathf.Deg2Rad;
        Vector2 handle = Rotate(presentation.SecondaryGripPoint - presentation.GripPoint,
            presentation.AngleCorrection * Mathf.Deg2Rad);
        if (Mathf.Cos(facing) < -0.0001f) handle.y = -handle.y;
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
