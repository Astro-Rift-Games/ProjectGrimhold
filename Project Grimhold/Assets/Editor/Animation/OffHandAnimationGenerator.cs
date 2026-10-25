using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Derives left-hand clips from assigned right-hand clips, never from weapon names.</summary>
public static class OffHandAnimationGenerator
{
    private const string Main = "RightHandPivot/RightHand";
    private const string Off = "LeftHandPivot/LeftHand";
    private static readonly string[] Directions = { "N", "NE", "NW", "S", "SE", "SW" };
    private static readonly string[] ClipFields = { "_attackN", "_attackNE", "_attackNW", "_attackS", "_attackSE", "_attackSW" };
    // Bump when the derivation changes so the catalog validator detects stale output.
    private const string FormatVersion = "off-hand-retarget/5-facing-axis-slash-recovery";

    [MenuItem("Tools/Animations/Generate Off Hand Attacks")]
    public static void GenerateAll()
    {
        foreach (WeaponDefinition weapon in Weapons()) Generate(weapon);
        if (!TryValidateCatalog(out string error)) throw new InvalidOperationException(error);
    }

    [MenuItem("Tools/Animations/Validate Off Hand Attacks")]
    public static void ValidateAll()
    {
        if (!TryValidateCatalog(out string error)) throw new InvalidOperationException(error);
        Debug.Log("Off Hand attack catalog is complete and up to date.");
    }

    public static void GenerateAffectedBy(IEnumerable<string> mainClipPaths)
    {
        var changed = new HashSet<string>(mainClipPaths, StringComparer.Ordinal);
        foreach (WeaponDefinition weapon in Weapons())
            if (Enumerable.Range(0, 6).Any(i => changed.Contains(AssetDatabase.GetAssetPath(weapon.Presentation.GetAttackClip(i)))))
                Generate(weapon);
    }

    private static IEnumerable<WeaponDefinition> Weapons() =>
        AssetDatabase.FindAssets("t:WeaponDefinition").OrderBy(guid => guid, StringComparer.Ordinal)
            .Select(guid => AssetDatabase.LoadAssetAtPath<WeaponDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(weapon => weapon.Handedness == WeaponHandedness.OneHanded && weapon.Presentation.HasGenericAttack);

    public static void Generate(WeaponDefinition weapon)
    {
        if (weapon == null || weapon.Handedness != WeaponHandedness.OneHanded || !weapon.Presentation.HasGenericAttack)
            throw new ArgumentException("A one-handed weapon with a complete assigned Main Hand set is required.", nameof(weapon));
        DirectionalAttackAnimationSet mainSet = weapon.Presentation.AttackAnimationSet;
        if (mainSet.OffHandStroke == OffHandStroke.Mirrored && weapon.Presentation.AimMode == WeaponAimMode.FreeAim)
            throw new ArgumentException("Mirrored Off Hand strokes do not support FreeAim.", nameof(weapon));
        string setPath = DerivedPath(AssetDatabase.GetAssetPath(mainSet));
        var offsets = new Vector2[6];
        var clips = new AnimationClip[6];
        for (int i = 0; i < 6; i++)
        {
            AnimationClip source = mainSet.GetAttackClip(i);
            string path = DerivedPath(AssetDatabase.GetAssetPath(source));
            DirectionalAnimationGenerator.WriteGenerated(CreateClip(source, i, mainSet.OffHandStroke, out offsets[i], mainSet), path);
            clips[i] = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        }

        var set = AssetDatabase.LoadAssetAtPath<DirectionalAttackAnimationSet>(setPath);
        if (set == null)
        {
            set = ScriptableObject.CreateInstance<DirectionalAttackAnimationSet>();
            AssetDatabase.CreateAsset(set, setPath);
        }
        set.name = Path.GetFileNameWithoutExtension(setPath);
        ConfigureDerivedSet(mainSet, set, clips, offsets);
        EditorUtility.SetDirty(set);
        AssetDatabase.SaveAssetIfDirty(set);
        var definition = new SerializedObject(weapon);
        definition.FindProperty("_presentation").FindPropertyRelative("_offHandAttackAnimationSet").objectReferenceValue = set;
        definition.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssetIfDirty(weapon);
    }

    /// <summary>Populates a derived set, including metadata needed by runtime presentation. Also supports inert test sets.</summary>
    public static void ConfigureDerivedSet(DirectionalAttackAnimationSet mainSet, DirectionalAttackAnimationSet set,
        AnimationClip[] clips, Vector2[] offsets)
    {
        using var serialized = new SerializedObject(set);
        serialized.FindProperty("_offHandStroke").enumValueIndex = (int)mainSet.OffHandStroke;
        serialized.FindProperty("_canonicalSlash").boolValue = mainSet.CanonicalSlash;
        serialized.FindProperty("_slashStrokeWindow").vector2Value = mainSet.SlashStrokeWindow;
        serialized.FindProperty("_slashRecoveryEndSeconds").floatValue = mainSet.SlashRecoveryEndSeconds;
        for (int i = 0; i < 6; i++) serialized.FindProperty(ClipFields[i]).objectReferenceValue = clips[i];
        SerializedProperty origins = serialized.FindProperty("_gripOriginOffsets");
        origins.arraySize = 6;
        for (int i = 0; i < 6; i++) origins.GetArrayElementAtIndex(i).vector2Value = offsets[i];
        var positions = serialized.FindProperty("_mainGripNeutralPositions");
        var rotations = serialized.FindProperty("_mirroredWeaponBasisRotations");
        positions.arraySize = rotations.arraySize = 6;
        using var rig = new SampleRig();
        for (int i = 0; i < 6; i++)
        {
            SampleNeutral(rig, i);
            positions.GetArrayElementAtIndex(i).vector3Value = rig.MainGrip.position;
            // OffGrip * basis * inverse(main weapon angle) = horizontal mirror of MainGrip * main weapon angle.
            rotations.GetArrayElementAtIndex(i).quaternionValue = Quaternion.Inverse(rig.OffGrip.rotation) *
                PlayerWeaponPresentationMath.ReflectedRotation(rig.MainGrip.rotation, i);
        }
        serialized.FindProperty("_retargetSourceSignature").stringValue = SourceSignature(mainSet);
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    public static bool TryValidateCatalog(out string error) => TryValidateCatalog(
        AssetDatabase.FindAssets("t:WeaponDefinition").OrderBy(guid => guid, StringComparer.Ordinal)
            .Select(guid => AssetDatabase.LoadAssetAtPath<WeaponDefinition>(AssetDatabase.GUIDToAssetPath(guid))), out error);

    public static bool TryValidateCatalog(IEnumerable<WeaponDefinition> weapons, out string error)
    {
        foreach (WeaponDefinition weapon in weapons)
        {
            DirectionalAttackAnimationSet set = weapon.Presentation.OffHandAttackAnimationSet;
            if (weapon.Handedness == WeaponHandedness.TwoHanded && set != null)
            {
                error = $"{weapon.name}: a two-handed weapon must not reference an Off Hand set.";
                return false;
            }
            if (weapon.Handedness != WeaponHandedness.OneHanded || !weapon.Presentation.HasGenericAttack) continue;
            if (!weapon.TryValidate(out error)) return false;
            if (set == null || !set.TryValidate(out _) || !set.HasGripOriginOffsets || !set.HasMirrorMetadata ||
                set.OffHandStroke != weapon.Presentation.AttackAnimationSet.OffHandStroke ||
                set.RetargetSourceSignature != SourceSignature(weapon.Presentation.AttackAnimationSet))
            {
                error = $"{weapon.name}: missing or stale Off Hand set. Run Tools/Animations/Generate Off Hand Attacks.";
                return false;
            }
            for (int i = 0; i < 6; i++)
            {
                AnimationClip clip = set.GetAttackClip(i);
                if (Mathf.Abs(clip.length - weapon.Presentation.GetAttackClip(i).length) > 0.0001f ||
                    AnimationUtility.GetCurveBindings(clip).Any(b => b.path != Off && b.path != Off + "/OffHandGrip") ||
                    AnimationUtility.GetObjectReferenceCurveBindings(clip).Any(b => b.path != Off))
                {
                    error = $"{weapon.name}: Off Hand clip {clip.name} violates its isolated binding/timing contract.";
                    return false;
                }
            }
        }
        error = null;
        return true;
    }

    private static string DerivedPath(string source)
    {
        if (string.IsNullOrEmpty(source) || !source.StartsWith("Assets/", StringComparison.Ordinal))
            throw new ArgumentException("The source must be a saved project asset.");
        return source.Substring(0, source.Length - Path.GetExtension(source).Length) + "_OffHand" + Path.GetExtension(source);
    }

    private static string SourceSignature(DirectionalAttackAnimationSet source)
    {
        string data = FormatVersion + "|" + source.OffHandStroke + "|" + AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source)) + "|" +
            AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(source));
        foreach (string direction in Directions)
            foreach (string path in NeutralPaths(direction)) data += "|" + AssetDatabase.GetAssetDependencyHash(path);
        return Hash128.Compute(data).ToString();
    }

    private static IEnumerable<string> NeutralPaths(string direction)
    {
        yield return $"Assets/Animations/Player/Idle/Idle_{direction}.anim";
        yield return $"Assets/Animations/Player/Idle/RightHand/RightHand_Idle_{direction}.anim";
        yield return $"Assets/Animations/Player/Idle/LeftHand/LeftHand_Idle_{direction}.anim";
    }

    public static AnimationClip CreateClip(AnimationClip source, int direction, out Vector2 originOffset) =>
        CreateClip(source, direction, OffHandStroke.Reanchored, out originOffset);

    public static AnimationClip CreateClip(AnimationClip source, int direction, OffHandStroke stroke, out Vector2 originOffset)
        => CreateClip(source, direction, stroke, out originOffset, null);

    public static AnimationClip CreateClip(AnimationClip source, int direction, OffHandStroke stroke, out Vector2 originOffset,
        DirectionalAttackAnimationSet sourceSet)
    {
        if (source == null || direction < 0 || direction >= 6) throw new ArgumentException("A source clip and valid direction are required.");
        if (stroke != OffHandStroke.Reanchored && stroke != OffHandStroke.Mirrored)
            throw new ArgumentOutOfRangeException(nameof(stroke));
        using var rig = new SampleRig();
        SampleNeutral(rig, direction);
        Vector3 mainNeutral = rig.MainGrip.position;
        Vector3 offNeutral = rig.OffGrip.position;
        originOffset = offNeutral - mainNeutral;
        Sprite leftSprite = rig.Off.GetComponent<SpriteRenderer>().sprite;
        if (leftSprite == null) throw new InvalidOperationException("A drawn left neutral hand is required.");
        var neutralMain = new PoseState(rig.Main);
        var neutralMainGrip = new PoseState(rig.MainGrip);
        var neutralOff = new PoseState(rig.Off);
        var neutralOffGrip = new PoseState(rig.OffGrip);
        var curves = new AnimationCurve[20];
        for (int i = 0; i < curves.Length; i++) curves[i] = new AnimationCurve();
        // Bake the nonlinear pivot compensation densely; include every authored key and exact clip endpoints.
        var times = new SortedSet<float> { 0f, source.length };
        int frames = Mathf.CeilToInt(source.length * Mathf.Max(240f, source.frameRate));
        for (int i = 1; i < frames; i++) times.Add(i * source.length / frames);
        foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(source))
            foreach (Keyframe key in AnimationUtility.GetEditorCurve(source, binding).keys) times.Add(key.time);
        foreach (float t in times)
        {
            neutralMain.Apply(rig.Main);
            neutralMainGrip.Apply(rig.MainGrip);
            neutralOff.Apply(rig.Off);
            neutralOffGrip.Apply(rig.OffGrip);
            source.SampleAnimation(rig.Root, t);
            Quaternion handDelta = rig.Main.localRotation * Quaternion.Inverse(neutralMain.Rotation);
            if (stroke == OffHandStroke.Mirrored) handDelta = ReflectRotation(handDelta);
            rig.Off.localRotation = handDelta * neutralOff.Rotation;
            rig.Off.localScale = Vector3.Scale(neutralOff.Scale, Divide(rig.Main.localScale, neutralMain.Scale));
            Quaternion gripDelta = rig.MainGrip.localRotation * Quaternion.Inverse(neutralMainGrip.Rotation);
            if (stroke == OffHandStroke.Mirrored) gripDelta = ReflectRotation(gripDelta);
            rig.OffGrip.localRotation = gripDelta * neutralOffGrip.Rotation;
            rig.OffGrip.localScale = Vector3.Scale(neutralOffGrip.Scale, Divide(rig.MainGrip.localScale, neutralMainGrip.Scale));
            // Move the LEFT sprite's own grip, not its transparent-canvas origin, along the canonical stroke.
            Vector3 displacement = rig.MainGrip.position - mainNeutral;
            if (stroke == OffHandStroke.Mirrored) displacement = PlayerWeaponPresentationMath.ReflectAcrossFacing(displacement, direction);
            Vector3 target = offNeutral + displacement;
            if (stroke == OffHandStroke.Mirrored && sourceSet != null && sourceSet.CanonicalSlash)
            {
                Vector2 window = sourceSet.SlashStrokeWindow;
                float weight = t < window.x ? Mathf.SmoothStep(0, 1, t / window.x) :
                    t > window.y ? 1 - Mathf.SmoothStep(0, 1, (t - window.y) / (sourceSet.SlashRecoveryEndSeconds - window.y)) : 1;
                Vector3 correction = offNeutral - PlayerWeaponPresentationMath.ReflectAcrossFacing(mainNeutral, direction);
                target = PlayerWeaponPresentationMath.ReflectAcrossFacing(rig.MainGrip.position, direction) + correction * (1 - weight);
            }
            rig.Off.position += target - rig.OffGrip.position;
            AddPoseKeys(curves, 0, rig.Off, t);
            AddPoseKeys(curves, 10, rig.OffGrip, t);
        }
        var result = new AnimationClip { name = source.name + "_OffHand", frameRate = source.frameRate };
        try
        {
            WritePose(result, Off, curves, 0);
            WritePose(result, Off + "/OffHandGrip", curves, 10);
            PreservePlanarRotation(source, result, Main, Off, neutralMain.Rotation, neutralOff.Rotation, stroke);
            PreservePlanarRotation(source, result, Main + "/MainHandGrip", Off + "/OffHandGrip",
                neutralMainGrip.Rotation, neutralOffGrip.Rotation, stroke);
            AnimationUtility.SetObjectReferenceCurve(result, Bind(Off, typeof(SpriteRenderer), "m_Sprite"),
                new[] { new ObjectReferenceKeyframe { time = 0f, value = leftSprite } });
            AnimationUtility.SetEditorCurve(result, Bind(Off, typeof(SpriteRenderer), "m_SortingOrder"),
                AnimationCurve.Constant(0f, source.length, DirectionalAnimationGenerator.ResolveOffHandSortingOrder(Directions[direction])));
            result.EnsureQuaternionContinuity();
            return result;
        }
        catch { Object.DestroyImmediate(result); throw; }
    }

    // For planar rotations M R(theta) M = R(-theta). Applying that affine map to the
    // original angle keys AND tangents preserves the complete Hermite/weighted interpolation.
    // Resampling Euler curves as linear quaternion curves does not preserve their easing.
    private static void PreservePlanarRotation(AnimationClip source, AnimationClip result, string mainPath,
        string offPath, Quaternion mainNeutral, Quaternion offNeutral, OffHandStroke stroke)
    {
        var binding = Bind(mainPath, typeof(Transform), "localEulerAnglesRaw.z");
        AnimationCurve original = AnimationUtility.GetEditorCurve(source, binding);
        if (original == null) return; // Quaternion-authored clips retain the sampled fallback.
        float sign = stroke == OffHandStroke.Mirrored ? -1f : 1f;
        float offset = PlanarAngle(offNeutral) - sign * PlanarAngle(mainNeutral);
        Keyframe[] keys = original.keys;
        for (int i = 0; i < keys.Length; i++)
        {
            keys[i].value = sign * keys[i].value + offset;
            // Infinity encodes a step, not a derivative; retain its sentinel unchanged.
            if (!float.IsInfinity(keys[i].inTangent)) keys[i].inTangent *= sign;
            if (!float.IsInfinity(keys[i].outTangent)) keys[i].outTangent *= sign;
        }
        foreach (string axis in new[] { "x", "y", "z", "w" })
            AnimationUtility.SetEditorCurve(result, Bind(offPath, typeof(Transform), "m_LocalRotation." + axis), null);
        var reflected = new AnimationCurve(keys) { preWrapMode = original.preWrapMode, postWrapMode = original.postWrapMode };
        AnimationUtility.SetEditorCurve(result, Bind(offPath, typeof(Transform), "localEulerAnglesRaw.z"), reflected);
        foreach (string axis in new[] { "x", "y" })
            AnimationUtility.SetEditorCurve(result, Bind(offPath, typeof(Transform), "localEulerAnglesRaw." + axis),
                AnimationCurve.Constant(0f, source.length, 0f));
    }

    private static float PlanarAngle(Quaternion rotation)
    {
        Vector3 axis = rotation * Vector3.right;
        return Mathf.Atan2(axis.y, axis.x) * Mathf.Rad2Deg;
    }

    // M R M for M = diag(-1, 1, 1); a reflection is not a quaternion rotation itself.
    private static Quaternion ReflectRotation(Quaternion q) => new Quaternion(q.x, -q.y, -q.z, q.w);

    private static void SampleNeutral(SampleRig rig, int direction)
    {
        foreach (Transform node in new[] { rig.Main, rig.MainGrip, rig.Off, rig.OffGrip })
        {
            node.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            node.localScale = Vector3.one;
        }
        foreach (string path in NeutralPaths(Directions[direction]))
        {
            AnimationClip neutral = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (neutral == null) throw new InvalidOperationException($"Missing neutral pose: {path}");
            neutral.SampleAnimation(rig.Root, 0f);
        }
    }

    private static Vector3 Divide(Vector3 value, Vector3 divisor)
    {
        if (Mathf.Abs(divisor.x) < 0.00001f || Mathf.Abs(divisor.y) < 0.00001f || Mathf.Abs(divisor.z) < 0.00001f)
            throw new InvalidOperationException("Neutral hand scale must be invertible.");
        return new Vector3(value.x / divisor.x, value.y / divisor.y, value.z / divisor.z);
    }

    private static void AddPoseKeys(AnimationCurve[] curves, int offset, Transform node, float time)
    {
        Vector3 p = node.localPosition, s = node.localScale;
        Quaternion q = node.localRotation;
        float[] values = { p.x, p.y, p.z, q.x, q.y, q.z, q.w, s.x, s.y, s.z };
        for (int i = 0; i < values.Length; i++) curves[offset + i].AddKey(time, values[i]);
    }

    private static void WritePose(AnimationClip clip, string path, AnimationCurve[] curves, int offset)
    {
        string[] properties = { "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z", "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w", "m_LocalScale.x", "m_LocalScale.y", "m_LocalScale.z" };
        for (int i = 0; i < properties.Length; i++)
        {
            AnimationCurve curve = curves[offset + i];
            for (int k = 0; k < curve.length; k++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, k, AnimationUtility.TangentMode.Linear);
                AnimationUtility.SetKeyRightTangentMode(curve, k, AnimationUtility.TangentMode.Linear);
            }
            AnimationUtility.SetEditorCurve(clip, Bind(path, typeof(Transform), properties[i]), curve);
        }
    }

    private static EditorCurveBinding Bind(string path, Type type, string property) => new EditorCurveBinding { path = path, type = type, propertyName = property };

    private readonly struct PoseState
    {
        private readonly Vector3 _position;
        public readonly Quaternion Rotation;
        public readonly Vector3 Scale;
        public PoseState(Transform t) { _position = t.localPosition; Rotation = t.localRotation; Scale = t.localScale; }
        public void Apply(Transform t) { t.SetLocalPositionAndRotation(_position, Rotation); t.localScale = Scale; }
    }

    private sealed class SampleRig : IDisposable
    {
        public readonly GameObject Root = new GameObject("OffHandRetargetSample") { hideFlags = HideFlags.HideAndDontSave };
        public readonly Transform Main, MainGrip, Off, OffGrip;
        public SampleRig()
        {
            Root.AddComponent<Animator>();
            Main = Hand("RightHandPivot", "RightHand");
            MainGrip = Child(Main, "MainHandGrip");
            Off = Hand("LeftHandPivot", "LeftHand");
            OffGrip = Child(Off, "OffHandGrip");
        }
        private Transform Hand(string pivot, string name)
        {
            Transform hand = Child(Child(Root.transform, pivot), name);
            hand.gameObject.AddComponent<SpriteRenderer>().enabled = false;
            return hand;
        }
        private static Transform Child(Transform parent, string name)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }
        public void Dispose() => Object.DestroyImmediate(Root);
    }
}
