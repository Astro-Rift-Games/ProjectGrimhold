using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class DirectionalAnimationGenerator
{
    private const string Hand = "RightHandPivot/RightHand";
    private const string Grip = Hand + "/MainHandGrip";
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

    public static void GenerateAssets(string weaponName)
    {
        ValidateWeaponName(weaponName);
        AnimationClip original = RequireClip($"Assets/Animations/Weapons/{weaponName}_Attack.anim");
        foreach (string direction in Directions)
        {
            RequireClip($"Assets/Animations/Player/Idle/Idle_{direction}.anim");
            RequireClip($"Assets/Animations/Player/Idle/RightHand/RightHand_Idle_{direction}.anim");
        }

        foreach (string direction in Directions)
        {
            Bake(original, direction,
                $"Assets/Animations/Weapons/Directional/{weaponName}/{weaponName}_Attack_{direction}.anim",
                weaponName);
        }
    }

    public static void Bake(AnimationClip original, string direction, string path, string weaponName)
    {
        if (original == null) throw new ArgumentNullException(nameof(original));
        ValidateWeaponName(weaponName);
        if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal) ||
            !path.EndsWith(".anim", StringComparison.Ordinal))
            throw new ArgumentException("Expected an asset animation path.", nameof(path));
        string sourcePath = AssetDatabase.GetAssetPath(original);
        if (!string.IsNullOrEmpty(sourcePath) && string.Equals(path, sourcePath, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Destination cannot overwrite the source animation clip.", nameof(path));

        AnimationClip generated = CreateClip(original, direction, weaponName);
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

    public static AnimationClip CreateClip(AnimationClip original, string direction, string weaponName)
    {
        if (original == null) throw new ArgumentNullException(nameof(original));
        ValidateWeaponName(weaponName);
        int index = Array.IndexOf(Directions, direction);
        if (index < 0) throw new ArgumentException("Unknown facing direction.", nameof(direction));

        AnimationClip idle = RequireClip($"Assets/Animations/Player/Idle/Idle_{direction}.anim");
        AnimationClip handIdle = RequireClip($"Assets/Animations/Player/Idle/RightHand/RightHand_Idle_{direction}.anim");
        AnimationClip result = UnityEngine.Object.Instantiate(original);
        try
        {
            result.name = $"{weaponName}_Attack_{direction}";
            // Main Hand attacks are one-shot and drive only the RightHand hierarchy; other source
            // curves (for example LeftHand, which carries OffHandGrip) are outside the output contract.
            RemoveBindingsOutsideHand(result);
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(result);
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(result, settings);

            EditorCurveBinding x = Binding(Hand, typeof(Transform), "m_LocalPosition.x");
            EditorCurveBinding y = Binding(Hand, typeof(Transform), "m_LocalPosition.y");
            AnimationCurve sourceX = RequireCurve(original, x);
            AnimationCurve sourceY = RequireCurve(original, y);
            Keyframe[] xKeys = sourceX.keys;
            Keyframe[] yKeys = sourceY.keys;
            if (xKeys.Length != yKeys.Length || xKeys.Where((key, i) => key.time != yKeys[i].time).Any())
                throw new InvalidOperationException("South hand position axes must share key times.");

            float radians = Angles[index] * Mathf.Deg2Rad;
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
                    throw new InvalidOperationException($"Incompatible hand position tangent weights or modes at key {i}.");
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
            AnimationUtility.SetEditorCurve(result, x, new AnimationCurve(xKeys) { preWrapMode = sourceX.preWrapMode, postWrapMode = sourceX.postWrapMode });
            AnimationUtility.SetEditorCurve(result, y, new AnimationCurve(yKeys) { preWrapMode = sourceY.preWrapMode, postWrapMode = sourceY.postWrapMode });

            foreach (string axis in new[] { "x", "y", "z" })
            {
                EditorCurveBinding grip = Binding(Grip, typeof(Transform), "m_LocalPosition." + axis);
                float value = RequireCurve(idle, grip).Evaluate(0f);
                AnimationUtility.SetEditorCurve(result, grip, Constant(value, original.length));
            }
            EditorCurveBinding sprite = Binding(Hand, typeof(SpriteRenderer), "m_Sprite");
            ObjectReferenceKeyframe[] sprites = AnimationUtility.GetObjectReferenceCurve(handIdle, sprite);
            if (sprites == null || sprites.Length == 0 || sprites[0].time != 0f || sprites[0].value == null)
                throw new InvalidOperationException($"Missing idle hand sprite at time zero for {direction}.");
            AnimationUtility.SetObjectReferenceCurve(result, sprite,
                new[] { new ObjectReferenceKeyframe { time = 0f, value = sprites[0].value } });

            EditorCurveBinding sorting = Binding(Hand, typeof(SpriteRenderer), "m_SortingOrder");
            AnimationUtility.SetEditorCurve(result, sorting,
                direction.StartsWith("N", StringComparison.Ordinal) ? Constant(-2f, original.length) : null);
            return result;
        }
        catch
        {
            UnityEngine.Object.DestroyImmediate(result);
            throw;
        }
    }

    private static void RemoveBindingsOutsideHand(AnimationClip clip)
    {
        foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
        {
            if (!IsHandBinding(binding))
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
