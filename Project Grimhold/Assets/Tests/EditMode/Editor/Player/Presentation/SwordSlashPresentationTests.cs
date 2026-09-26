using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class SwordSlashPresentationTests
{
    private const string PrefabPath = "Assets/Prefabs/NetworkPlayer.prefab";
    private const string SwordPath = "Assets/Scriptable Objects/Loot/Definitions/ArmingSwordWeaponDefinition.asset";
    private const string SwordLootPath = "Assets/Scriptable Objects/Loot/Definitions/ArmingSword.asset";
    private const string MagicSwordPath = "Assets/Scriptable Objects/Loot/Definitions/MagicSwordWeaponDefinition.asset";
    private const string MagicSwordLootPath = "Assets/Scriptable Objects/Loot/Definitions/MagicSword.asset";
    private GameObject _contents;
    private PlayerAttackVfxPresenter _presenter;
    private SpriteRenderer _renderer;
    private LootDefinitionCatalog _catalog;

    [SetUp]
    public void SetUp()
    {
        _contents = PrefabUtility.LoadPrefabContents(PrefabPath);
        _presenter = _contents.GetComponentInChildren<PlayerAttackVfxPresenter>(true);
        Assert.That(_presenter, Is.Not.Null);
        _renderer = (SpriteRenderer)new SerializedObject(_presenter).FindProperty("_vfxRenderer").objectReferenceValue;
        var equipment = _contents.GetComponent<PlayerWeaponEquipmentNetworkController>();
        _catalog = (LootDefinitionCatalog)new SerializedObject(equipment).FindProperty("_lootCatalog").objectReferenceValue;
        Assert.That(_catalog, Is.Not.Null);
    }

    [TearDown]
    public void TearDown()
    {
        if (_contents != null) PrefabUtility.UnloadPrefabContents(_contents);
    }

    private int IndexPlusOne(string path)
    {
        LootDefinition loot = AssetDatabase.LoadAssetAtPath<LootDefinition>(path);
        Assert.That(loot, Is.Not.Null, path);
        Assert.That(_catalog.TryGetIndex(loot.LootId, out int index), Is.True);
        return index + 1;
    }

    private void Perform(int index, Vector2 direction, AttackType type = AttackType.Melee,
        PlayerAttackVfxPresenter presenter = null)
    {
        var attack = new AttackPerformedEvent(default, type, Vector2.zero, direction, 0, index);
        typeof(PlayerAttackVfxPresenter).GetMethod("OnAttackPerformed", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(presenter != null ? presenter : _presenter, new object[] { attack });
    }

    private static object State(PlayerAttackVfxPresenter presenter, string name) =>
        typeof(PlayerAttackVfxPresenter).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(presenter);

    [TestCase(CharacterVisualDirection.North, 0)]
    [TestCase(CharacterVisualDirection.NorthEast, 1)]
    [TestCase(CharacterVisualDirection.NorthWest, 2)]
    [TestCase(CharacterVisualDirection.South, 3)]
    [TestCase(CharacterVisualDirection.SouthEast, 4)]
    [TestCase(CharacterVisualDirection.SouthWest, 5)]
    public void ConfirmedSwordAttack_AppliesPoseResolvedFromBladeReach(CharacterVisualDirection direction, int index)
    {
        foreach ((string weaponPath, string lootPath) in new[] { (SwordPath, SwordLootPath), (MagicSwordPath, MagicSwordLootPath) })
        {
            WeaponDefinition.PresentationConfig presentation =
                AssetDatabase.LoadAssetAtPath<WeaponDefinition>(weaponPath).Presentation;
            Assert.That(presentation.AttackVfx.TryResolvePose(index, presentation.BladeReach,
                out AttackVfxDefinition.ResolvedPose pose), Is.True, weaponPath);
            Perform(IndexPlusOne(lootPath), CharacterVisualDirectionResolver.GetCanonicalVector(direction));
            Assert.That(State(_presenter, "_pending"), Is.True, weaponPath);
            Assert.That(_renderer.transform.name, Is.EqualTo("AttackVfx"));
            Assert.That(_renderer.transform.parent.name, Is.EqualTo("VisualRoot"));
            Assert.That(_renderer.transform.localPosition, Is.EqualTo(pose.Position), weaponPath);
            Assert.That(Quaternion.Angle(_renderer.transform.localRotation, pose.Rotation), Is.EqualTo(0f).Within(0.001f), weaponPath);
            Assert.That(_renderer.transform.localScale, Is.EqualTo(pose.Scale), weaponPath);
            Assert.That(_renderer.sortingOrder, Is.EqualTo(pose.SortingOrder), weaponPath);
            Assert.That(_renderer.enabled, Is.False);
        }
    }

    // Functional contract: once the presenter applies the configured directional pose, the VFX sprite
    // sequence must sweep in the same rotational sense as the weapon swing during the VFX window.
    // Both sweeps are measured in the Animator root space, so no mirror axis or weapon is assumed.
    [TestCase(CharacterVisualDirection.North, 0)]
    [TestCase(CharacterVisualDirection.NorthEast, 1)]
    [TestCase(CharacterVisualDirection.NorthWest, 2)]
    [TestCase(CharacterVisualDirection.South, 3)]
    [TestCase(CharacterVisualDirection.SouthEast, 4)]
    [TestCase(CharacterVisualDirection.SouthWest, 5)]
    public void AttackVfx_SweepsInSameRotationalSenseAsWeaponSwing(CharacterVisualDirection direction, int index)
    {
        var weaponPresenter = _contents.GetComponentInChildren<PlayerWeaponPresenter>(true);
        Assert.That(weaponPresenter, Is.Not.Null);
        Transform grip = (Transform)new SerializedObject(weaponPresenter).FindProperty("_mainHandGrip").objectReferenceValue;
        Transform root = _renderer.transform.parent;
        Assert.That(grip, Is.Not.Null);
        Assert.That(grip.IsChildOf(root), Is.True);

        var checkedWeapons = new System.Collections.Generic.List<string>();
        for (int catalogIndex = 0; catalogIndex < _catalog.DefinitionCount; catalogIndex++)
        {
            if (!_catalog.TryGetByIndex(catalogIndex, out LootDefinition loot) || loot.WeaponDefinition == null) continue;
            WeaponDefinition.PresentationConfig presentation = loot.WeaponDefinition.Presentation;
            AttackVfxDefinition vfx = presentation.AttackVfx;
            if (vfx == null || !presentation.HasGenericAttack) continue;

            Perform(catalogIndex + 1, CharacterVisualDirectionResolver.GetCanonicalVector(direction));
            Assert.That(State(_presenter, "_pending"), Is.True, loot.name);
            float vfxSweep = MeasureVfxSweep(vfx.Clip, root.worldToLocalMatrix * _renderer.transform.localToWorldMatrix);
            float weaponSweep = MeasureWeaponSweep(presentation.GetAttackClip(index), root, grip,
                vfx.StartSeconds, vfx.StartSeconds + vfx.Clip.length);

            string context = $"{loot.name} {direction}: VFX sweep {vfxSweep:F1}°, weapon sweep {weaponSweep:F1}°";
            Assert.That(Mathf.Abs(vfxSweep), Is.GreaterThan(1f), context);
            Assert.That(Mathf.Abs(weaponSweep), Is.GreaterThan(1f), context);
            Assert.That(Mathf.Sign(vfxSweep), Is.EqualTo(Mathf.Sign(weaponSweep)), context);
            checkedWeapons.Add(loot.name);
        }
        // Both swords share the Slash visual but swing in opposite senses, so each must be checked.
        Assert.That(checkedWeapons, Does.Contain("ArmingSword").And.Contain("MagicSword"));
    }

    /// <summary>Signed degrees swept by the frame centroids of the VFX sprite sequence, in root space.</summary>
    private float MeasureVfxSweep(AnimationClip clip, Matrix4x4 vfxToRoot)
    {
        EditorCurveBinding[] bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
        Assert.That(bindings, Has.Length.EqualTo(1));
        ObjectReferenceKeyframe[] frames = AnimationUtility.GetObjectReferenceCurve(clip, bindings[0]);
        Assert.That(frames.Length, Is.GreaterThanOrEqualTo(2));
        Vector2 flip = new Vector2(_renderer.flipX ? -1f : 1f, _renderer.flipY ? -1f : 1f);
        float sweep = 0f;
        float previous = 0f;
        for (int i = 0; i < frames.Length; i++)
        {
            var sprite = frames[i].value as Sprite;
            Assert.That(sprite, Is.Not.Null, $"frame {i}");
            Vector2 centroid = Vector2.Scale(CalculateMeshCentroid(sprite), flip);
            float angle = Angle(vfxToRoot.MultiplyVector(centroid));
            if (i > 0) sweep += Mathf.DeltaAngle(previous, angle);
            previous = angle;
        }
        return sweep;
    }

    /// <summary>Signed degrees swept by the main-hand grip axis while the attack clip plays the window.</summary>
    private static float MeasureWeaponSweep(AnimationClip attack, Transform root, Transform grip, float start, float end)
    {
        Assert.That(attack, Is.Not.Null);
        const int Samples = 40;
        float sweep = 0f;
        float previous = 0f;
        for (int i = 0; i <= Samples; i++)
        {
            attack.SampleAnimation(root.gameObject, Mathf.Lerp(start, end, i / (float)Samples));
            float angle = Angle((root.worldToLocalMatrix * grip.localToWorldMatrix).MultiplyVector(Vector3.right));
            if (i > 0) sweep += Mathf.DeltaAngle(previous, angle);
            previous = angle;
        }
        return sweep;
    }

    private static Vector2 CalculateMeshCentroid(Sprite sprite)
    {
        Vector2[] vertices = sprite.vertices;
        ushort[] triangles = sprite.triangles;
        Vector2 weighted = Vector2.zero;
        float area = 0f;
        for (int i = 0; i < triangles.Length; i += 3)
        {
            Vector2 a = vertices[triangles[i]];
            Vector2 b = vertices[triangles[i + 1]];
            Vector2 c = vertices[triangles[i + 2]];
            float triangleArea = Mathf.Abs((b.x - a.x) * (c.y - a.y) - (c.x - a.x) * (b.y - a.y)) * 0.5f;
            weighted += (a + b + c) / 3f * triangleArea;
            area += triangleArea;
        }
        Assert.That(area, Is.GreaterThan(0f), sprite.name);
        return weighted / area;
    }

    private static float Angle(Vector3 vector) => Mathf.Atan2(vector.y, vector.x) * Mathf.Rad2Deg;

    // Spatial contract: the resolved pose centers the sprite sequence on the swing arc and sizes it so
    // its tip radius follows the real blade tip, and each frame's arc covers the tip while it plays.
    // The blade is posed with the production grip/facing math, so a variant geometry sharing the same
    // Attack VFX must align without code or VFX changes. Magic Sword checks the same shared Slash
    // visual against its own, differently shaped swing.
    [TestCase(CharacterVisualDirection.North, 0)]
    [TestCase(CharacterVisualDirection.NorthEast, 1)]
    [TestCase(CharacterVisualDirection.NorthWest, 2)]
    [TestCase(CharacterVisualDirection.South, 3)]
    [TestCase(CharacterVisualDirection.SouthEast, 4)]
    [TestCase(CharacterVisualDirection.SouthWest, 5)]
    public void AttackVfx_TracesBladeTipForSwordsAndVariantGeometries(CharacterVisualDirection direction, int index)
    {
        WeaponDefinition sword = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(SwordPath);
        WeaponDefinition magicSword = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(MagicSwordPath);
        WeaponDefinition shortBlade = CreateGeometryVariant(sword, new Vector2(0f, -0.4f), new Vector2(0f, 0.6f));
        WeaponDefinition longBlade = CreateGeometryVariant(sword, new Vector2(0.1f, -0.8f), new Vector2(0.1f, 1.4f));
        try
        {
            Assert.That(shortBlade.Presentation.AttackVfx, Is.SameAs(sword.Presentation.AttackVfx));
            Assert.That(longBlade.Presentation.AttackVfx, Is.SameAs(sword.Presentation.AttackVfx));
            foreach (WeaponDefinition weapon in new[] { sword, shortBlade, longBlade, magicSword })
            {
                Assert.That(weapon.TryValidate(out string error), Is.True, error);
                AssertSlashTracesBladeTip(weapon, direction, index);
            }
        }
        finally
        {
            Object.DestroyImmediate(shortBlade);
            Object.DestroyImmediate(longBlade);
        }
    }

    private static WeaponDefinition CreateGeometryVariant(WeaponDefinition source, Vector2 gripPoint, Vector2 bladeTip)
    {
        WeaponDefinition variant = Object.Instantiate(source);
        variant.name = $"{source.name} reach {Vector2.Distance(gripPoint, bladeTip):F2}";
        var serialized = new SerializedObject(variant);
        serialized.FindProperty("_presentation._gripPoint").vector2Value = gripPoint;
        serialized.FindProperty("_presentation._bladeTip").vector2Value = bladeTip;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return variant;
    }

    private void AssertSlashTracesBladeTip(WeaponDefinition weapon, CharacterVisualDirection direction, int index)
    {
        // About 1.6 px at the project's 16 PPU: the swing is not a perfect circle, so the tip wobbles
        // around the fitted arc by roughly one pixel for any blade reach.
        const float MaxMeanRadialError = 0.1f;
        const float AngularToleranceDegrees = 30f;
        WeaponDefinition.PresentationConfig presentation = weapon.Presentation;
        AttackVfxDefinition vfx = presentation.AttackVfx;
        Assert.That(vfx.TryResolvePose(index, presentation.BladeReach, out AttackVfxDefinition.ResolvedPose pose), Is.True);
        Transform root = _renderer.transform.parent;
        Transform visual = PoseHeldWeapon(presentation, CharacterVisualDirectionResolver.GetCanonicalVector(direction));
        Matrix4x4 vfxToRoot = Matrix4x4.TRS(pose.Position, pose.Rotation, pose.Scale);
        Vector2 center = pose.Position;
        float expectedRadius = vfx.TipRadius * Mathf.Abs(pose.Scale.x);
        AnimationClip attack = presentation.GetAttackClip(index);

        const int Samples = 40;
        float radialError = 0f;
        for (int i = 0; i <= Samples; i++)
        {
            float time = vfx.StartSeconds + vfx.Clip.length * i / Samples;
            radialError += Mathf.Abs((SampleBladeTip(attack, time, root, visual, presentation.BladeTip) - center).magnitude - expectedRadius);
        }
        radialError /= Samples + 1;
        string context = $"{weapon.name} {direction}";
        Assert.That(radialError, Is.LessThanOrEqualTo(MaxMeanRadialError),
            $"{context}: mean tip radial error {radialError:F3} for slash tip radius {expectedRadius:F3}");

        EditorCurveBinding[] bindings = AnimationUtility.GetObjectReferenceCurveBindings(vfx.Clip);
        ObjectReferenceKeyframe[] frames = AnimationUtility.GetObjectReferenceCurve(vfx.Clip, bindings[0]);
        for (int i = 0; i < frames.Length; i++)
        {
            var sprite = (Sprite)frames[i].value;
            float frameEnd = i + 1 < frames.Length ? frames[i + 1].time : vfx.Clip.length;
            float midpoint = vfx.StartSeconds + (frames[i].time + frameEnd) * 0.5f;
            float reference = Angle(vfxToRoot.MultiplyVector(CalculateMeshCentroid(sprite)));
            float min = float.MaxValue;
            float max = float.MinValue;
            foreach (Vector2 vertex in sprite.vertices)
            {
                float offset = Mathf.DeltaAngle(reference, Angle(vfxToRoot.MultiplyVector(vertex)));
                min = Mathf.Min(min, offset);
                max = Mathf.Max(max, offset);
            }
            float tip = Mathf.DeltaAngle(reference,
                Angle(SampleBladeTip(attack, midpoint, root, visual, presentation.BladeTip) - center));
            Assert.That(tip, Is.InRange(min - AngularToleranceDegrees, max + AngularToleranceDegrees),
                $"{context} frame {i}: tip at {tip:F1}° outside slash arc [{min:F1}°, {max:F1}°]");
        }
    }

    /// <summary>Applies the production grip alignment and facing pose to the main-hand weapon transforms.</summary>
    private Transform PoseHeldWeapon(WeaponDefinition.PresentationConfig presentation, Vector2 facing)
    {
        var weaponPresenter = _contents.GetComponentInChildren<PlayerWeaponPresenter>(true);
        var serialized = new SerializedObject(weaponPresenter);
        var pivot = (Transform)serialized.FindProperty("_mainHandWeaponPivot").objectReferenceValue;
        var visual = (Transform)serialized.FindProperty("_mainHandWeaponVisual").objectReferenceValue;
        pivot.localPosition = Vector3.zero;
        pivot.localRotation = Quaternion.Euler(0f, 0f, PlayerWeaponPresentationMath.CalculateFacingAngleDegrees(facing));
        pivot.localScale = new Vector3(1f, PlayerWeaponPresentationMath.ShouldMirror(facing) ? -1f : 1f, 1f);
        Vector2 aligned = PlayerWeaponPresentationMath.CalculateGripAlignedWeaponPosition(
            presentation.GripPoint, visual.localScale, presentation.AngleCorrection);
        visual.localPosition = new Vector3(aligned.x, aligned.y, visual.localPosition.z);
        visual.localRotation = Quaternion.Euler(0f, 0f, presentation.AngleCorrection);
        return visual;
    }

    private static Vector2 SampleBladeTip(AnimationClip attack, float time, Transform root, Transform visual, Vector2 bladeTip)
    {
        attack.SampleAnimation(root.gameObject, time);
        return (root.worldToLocalMatrix * visual.localToWorldMatrix).MultiplyPoint3x4(bladeTip);
    }

    [Test]
    public void SharedAttackVfx_ResizesFromBladeReachWithoutMovingPlacement()
    {
        AttackVfxDefinition vfx = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(SwordPath).Presentation.AttackVfx;
        for (int i = 0; i < 6; i++)
        {
            AttackVfxDefinition.DirectionalPose source = vfx.GetPose(i);
            Assert.That(vfx.TryResolvePose(i, 1f, out AttackVfxDefinition.ResolvedPose shorter), Is.True);
            Assert.That(vfx.TryResolvePose(i, 2f, out AttackVfxDefinition.ResolvedPose longer), Is.True);
            float expectedShort = (source.ReachOffset + 1f) / vfx.TipRadius;
            float expectedLong = (source.ReachOffset + 2f) / vfx.TipRadius;
            float mirror = source.Mirrored ? -1f : 1f;
            Assert.That(shorter.Scale.x, Is.EqualTo(expectedShort).Within(0.0001f), $"pose {i}");
            Assert.That(shorter.Scale.y, Is.EqualTo(mirror * expectedShort).Within(0.0001f), $"pose {i}");
            Assert.That(longer.Scale.x, Is.EqualTo(expectedLong).Within(0.0001f), $"pose {i}");
            Assert.That(longer.Scale.y, Is.EqualTo(mirror * expectedLong).Within(0.0001f), $"pose {i}");
            Assert.That(longer.Position, Is.EqualTo(shorter.Position), $"pose {i}");
            Assert.That(longer.Rotation, Is.EqualTo(shorter.Rotation), $"pose {i}");
            Assert.That(longer.SortingOrder, Is.EqualTo(shorter.SortingOrder), $"pose {i}");
            Assert.That(vfx.TryResolvePose(i, -source.ReachOffset, out _), Is.False, $"pose {i} zero size");
        }
        Assert.That(vfx.TryResolvePose(6, 1f, out _), Is.False);
        Assert.That(vfx.TryResolvePose(0, float.NaN, out _), Is.False);
    }

    [Test]
    public void WeaponWithAttackVfx_RequiresBladeTipDistinctFromGrip()
    {
        WeaponDefinition sword = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(SwordPath);
        WeaponDefinition invalid = CreateGeometryVariant(sword, sword.Presentation.GripPoint, sword.Presentation.GripPoint);
        try
        {
            Assert.That(invalid.TryValidate(out string error), Is.False);
            Assert.That(error, Does.Contain("blade tip"));
        }
        finally
        {
            Object.DestroyImmediate(invalid);
        }
    }

    [Test]
    public void VfxConfiguration_HasExclusiveChildSpriteBindingAndFourOrderedFrames()
    {
        WeaponDefinition sword = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(SwordPath);
        AttackVfxDefinition vfx = sword.Presentation.AttackVfx;
        Assert.That(vfx, Is.Not.Null);
        Assert.That(vfx.TryValidate(out string error), Is.True, error);
        Assert.That(vfx.StartSeconds, Is.EqualTo(0.1f));
        Assert.That(vfx.Clip.length, Is.EqualTo(0.4f).Within(0.0001f));
        var bindings = AnimationUtility.GetObjectReferenceCurveBindings(vfx.Clip);
        Assert.That(bindings, Has.Length.EqualTo(1));
        Assert.That(bindings[0].path, Is.EqualTo("AttackVfx"));
        Assert.That(bindings[0].type, Is.EqualTo(typeof(SpriteRenderer)));
        Assert.That(bindings[0].propertyName, Is.EqualTo("m_Sprite"));
        Assert.That(AnimationUtility.GetCurveBindings(vfx.Clip), Is.Empty);
        var frames = AnimationUtility.GetObjectReferenceCurve(vfx.Clip, bindings[0]);
        Assert.That(frames, Has.Length.EqualTo(4));
        for (int i = 0; i < 4; i++)
        {
            Assert.That(frames[i].time, Is.EqualTo(i * 0.1f).Within(0.0001f));
            Assert.That(frames[i].value.name, Is.EqualTo($"VFX-Slash_{i}"));
        }
        Assert.That(vfx.TipRadius, Is.EqualTo(1.05f));
        Assert.That(sword.Presentation.BladeTip, Is.EqualTo(new Vector2(0f, 0.8125f)));
        Assert.That(sword.Presentation.BladeReach, Is.EqualTo(1.4375f).Within(0.0001f));
        // Index order: N, NE, NW, S, SE, SW.
        var positions = new[]
        {
            new Vector3(-0.04f, 0.52f, 0f), new Vector3(0.34f, 0.4f, 0f), new Vector3(-0.44f, 0.31f, 0f),
            new Vector3(0.05f, -0.53f, 0f), new Vector3(0.39f, -0.35f, 0f), new Vector3(-0.31f, -0.45f, 0f)
        };
        var rotations = new[] { 87f, 41f, 141f, -93f, -51f, -131f };
        var reachOffsets = new[] { -0.31f, -0.09f, -0.4f, 0.12f, -0.12f, 0.16f };
        AssertPoses(vfx, positions, rotations, reachOffsets, mirrored: true);
        WeaponDefinition rapier = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(
            "Assets/Scriptable Objects/Loot/Definitions/RapierWeaponDefinition.asset");
        Assert.That(rapier.Presentation.AttackVfx, Is.Null);
    }

    // Magic Sword winds up counterclockwise until 0.3s and strikes clockwise until 0.55s around the
    // body center, so its four Slash frames span apex to strike end from 0.25s and play unmirrored.
    [Test]
    public void MagicSwordVfxConfiguration_AlignsSharedSlashWithItsOwnSwing()
    {
        WeaponDefinition magicSword = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(MagicSwordPath);
        AttackVfxDefinition vfx = magicSword.Presentation.AttackVfx;
        Assert.That(vfx, Is.Not.Null);
        Assert.That(magicSword.TryValidate(out string error), Is.True, error);
        Assert.That(vfx.StartSeconds, Is.EqualTo(0.25f));
        Assert.That(magicSword.Presentation.BladeTip, Is.EqualTo(new Vector2(0f, 0.75f)));
        Assert.That(magicSword.Presentation.BladeReach, Is.EqualTo(1.3125f).Within(0.0001f));
        var positions = new[]
        {
            new Vector3(0f, 0.11f, 0f), new Vector3(0.07f, 0.08f, 0f), new Vector3(-0.07f, 0.07f, 0f),
            new Vector3(0f, -0.11f, 0f), new Vector3(0.08f, -0.08f, 0f), new Vector3(-0.07f, -0.06f, 0f)
        };
        var rotations = new[] { 133f, 88f, -173f, -45f, -4f, -82f };
        var reachOffsets = new[] { -0.06f, 0.16f, -0.15f, 0.37f, 0.13f, 0.43f };
        AssertPoses(vfx, positions, rotations, reachOffsets, mirrored: false);
        for (int i = 0; i < 6; i++)
        {
            Assert.That(vfx.StartSeconds + vfx.Clip.length,
                Is.LessThanOrEqualTo(magicSword.Presentation.GetAttackClip(i).length), $"clip {i}");
        }
    }

    private static void AssertPoses(AttackVfxDefinition vfx, Vector3[] positions, float[] rotations,
        float[] reachOffsets, bool mirrored)
    {
        // Index order: N, NE, NW, S, SE, SW. Sorting follows the facing, like the held weapon.
        var sortingOrders = new[] { -9, -9, -9, 21, 21, 21 };
        for (int i = 0; i < 6; i++)
        {
            var pose = vfx.GetPose(i);
            Assert.That(pose.Position, Is.EqualTo(positions[i]), $"pose {i}");
            Assert.That(Quaternion.Angle(pose.Rotation, Quaternion.Euler(0f, 0f, rotations[i])), Is.EqualTo(0f).Within(0.001f), $"pose {i}");
            Assert.That(pose.ReachOffset, Is.EqualTo(reachOffsets[i]), $"pose {i}");
            Assert.That(pose.Mirrored, Is.EqualTo(mirrored), $"pose {i}");
            Assert.That(pose.SortingOrder, Is.EqualTo(sortingOrders[i]), $"pose {i}");
        }
    }

    [Test]
    public void SwordSlashWeapons_ShareOneVisualWithIndependentAlignments()
    {
        AttackVfxDefinition sword = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(SwordPath).Presentation.AttackVfx;
        AttackVfxDefinition magicSword = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(MagicSwordPath).Presentation.AttackVfx;
        Assert.That(magicSword, Is.Not.SameAs(sword));
        Assert.That(magicSword.Visual, Is.SameAs(sword.Visual));
        Assert.That(AssetDatabase.GetAssetPath(sword.Clip), Is.EqualTo("Assets/Art/VFX/SwordSlashVfx.anim"));
        Assert.That(magicSword.Clip, Is.SameAs(sword.Clip));
        Assert.That(magicSword.TipRadius, Is.EqualTo(sword.TipRadius));
        Assert.That(magicSword.StartSeconds, Is.Not.EqualTo(sword.StartSeconds));
        Assert.That(magicSword.GetPose(3).Position, Is.Not.EqualTo(sword.GetPose(3).Position));
        Assert.That(magicSword.GetPose(3).Mirrored, Is.Not.EqualTo(sword.GetPose(3).Mirrored));
    }

    [Test]
    public void ChangingOneSwordAlignment_LeavesTheOtherSwordUnchanged()
    {
        WeaponDefinition.PresentationConfig magicSword =
            AssetDatabase.LoadAssetAtPath<WeaponDefinition>(MagicSwordPath).Presentation;
        AttackVfxDefinition sword = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(SwordPath).Presentation.AttackVfx;
        float magicStart = magicSword.AttackVfx.StartSeconds;
        var magicPoses = new AttackVfxDefinition.ResolvedPose[6];
        for (int i = 0; i < 6; i++)
            Assert.That(magicSword.AttackVfx.TryResolvePose(i, magicSword.BladeReach, out magicPoses[i]), Is.True);

        // Edits the Arming Sword alignment in memory only; the original values are restored below.
        var serialized = new SerializedObject(sword);
        float originalStart = sword.StartSeconds;
        var originalPoses = new AttackVfxDefinition.DirectionalPose[6];
        for (int i = 0; i < 6; i++) originalPoses[i] = sword.GetPose(i);
        try
        {
            serialized.FindProperty("_startSeconds").floatValue = originalStart + 0.05f;
            for (int i = 0; i < 6; i++)
            {
                SerializedProperty pose = serialized.FindProperty("_poses").GetArrayElementAtIndex(i);
                pose.FindPropertyRelative("_position").vector3Value = originalPoses[i].Position + Vector3.one * 0.5f;
                pose.FindPropertyRelative("_reachOffset").floatValue = originalPoses[i].ReachOffset + 0.2f;
                pose.FindPropertyRelative("_mirrored").boolValue = !originalPoses[i].Mirrored;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(sword.StartSeconds, Is.EqualTo(originalStart + 0.05f).Within(0.0001f));

            Assert.That(magicSword.AttackVfx.StartSeconds, Is.EqualTo(magicStart));
            for (int i = 0; i < 6; i++)
            {
                Assert.That(magicSword.AttackVfx.TryResolvePose(i, magicSword.BladeReach, out var pose), Is.True);
                Assert.That(pose.Position, Is.EqualTo(magicPoses[i].Position), $"pose {i}");
                Assert.That(pose.Rotation, Is.EqualTo(magicPoses[i].Rotation), $"pose {i}");
                Assert.That(pose.Scale, Is.EqualTo(magicPoses[i].Scale), $"pose {i}");
            }
        }
        finally
        {
            serialized.Update();
            serialized.FindProperty("_startSeconds").floatValue = originalStart;
            for (int i = 0; i < 6; i++)
            {
                SerializedProperty pose = serialized.FindProperty("_poses").GetArrayElementAtIndex(i);
                pose.FindPropertyRelative("_position").vector3Value = originalPoses[i].Position;
                pose.FindPropertyRelative("_reachOffset").floatValue = originalPoses[i].ReachOffset;
                pose.FindPropertyRelative("_mirrored").boolValue = originalPoses[i].Mirrored;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.ClearDirty(sword);
        }
    }

    [Test]
    public void RejectedAttack_ClearsPendingEffect()
    {
        int sword = IndexPlusOne(SwordLootPath);
        int rapier = IndexPlusOne("Assets/Scriptable Objects/Loot/Definitions/Rapier.asset");
        foreach (int rejected in new[] { 0, _catalog.DefinitionCount + 1, rapier })
        {
            Perform(sword, Vector2.down);
            Perform(rejected, Vector2.down);
            Assert.That(State(_presenter, "_pending"), Is.False);
            Assert.That(_renderer.sprite, Is.Null);
            Assert.That(_renderer.enabled, Is.False);
        }
        Perform(sword, Vector2.down, AttackType.Ranged);
        Assert.That(State(_presenter, "_pending"), Is.True, "Configured effect accepts confirmed non-melee attack types");
    }

    [Test]
    public void DisablingPresenter_ClearsEffect()
    {
        Perform(IndexPlusOne(SwordLootPath), Vector2.up);
        _renderer.enabled = true;
        typeof(PlayerAttackVfxPresenter).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(_presenter, null);
        Assert.That(State(_presenter, "_pending"), Is.False);
        Assert.That(_renderer.enabled, Is.False);
        Assert.That(_renderer.sprite, Is.Null);
    }

    // Steps are 50 ms: Arming Sword starts at 0.1s and Magic Sword at 0.25s, so the same Slash
    // frames appear at different hand phases and clear after 0.4s of Slash playback.
    [TestCase(SwordPath, SwordLootPath, 3, 6, 10)]
    [TestCase(MagicSwordPath, MagicSwordLootPath, 6, 9, 13)]
    public void SwordAttack_SamplesClipAtMatchingHandPhaseAndClearsAtEnd(string weaponPath, string lootPath,
        int firstFrameStep, int thirdFrameStep, int endStep)
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject player = null;
        AnimatorOverrideController overrides = null;
        try
        {
            player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), scene);
            Animator animator = player.GetComponentInChildren<Animator>(true);
            var presenter = player.GetComponentInChildren<PlayerAttackVfxPresenter>(true);
            SpriteRenderer renderer = (SpriteRenderer)new SerializedObject(presenter).FindProperty("_vfxRenderer").objectReferenceValue;
            var sword = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(weaponPath);
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/Player/Character.controller");
            var placeholder = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Player/Attack/GenericAttack_S.anim");
            overrides = new AnimatorOverrideController(controller);
            overrides[placeholder] = sword.Presentation.GetAttackClip(3);
            animator.runtimeAnimatorController = overrides;
            animator.SetFloat("MoveX", 0f);
            animator.SetFloat("MoveY", -1f);
            animator.SetBool("IsMoving", false);
            animator.SetBool("HasGenericAttack", true);
            animator.SetInteger("WeaponAnimationCategory", (int)sword.Presentation.AnimationCategory);
            typeof(PlayerAttackVfxPresenter).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(presenter, null);
            animator.SetTrigger("OnAttack");
            Perform(IndexPlusOne(lootPath), Vector2.down, AttackType.Melee, presenter);
            MethodInfo update = typeof(PlayerAttackVfxPresenter).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
            animator.Update(0f);
            int layer = animator.GetLayerIndex("RightHand");
            Assert.That(animator.GetCurrentAnimatorStateInfo(layer).IsTag("Attack"), Is.True);
            for (int step = 1; step <= endStep; step++)
            {
                animator.Update(0.05f);
                update.Invoke(presenter, null);
                if (step == firstFrameStep - 2)
                    Assert.That(renderer.enabled, Is.False, "Slash must wait for its start offset");
                if (step == firstFrameStep || step == thirdFrameStep || step == endStep)
                {
                    AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(layer);
                    Assert.That(state.IsTag("Attack"), Is.True);
                    Assert.That(state.normalizedTime * state.length, Is.EqualTo(step * 0.05f).Within(0.01f));
                    if (step != endStep)
                    {
                        Assert.That(renderer.enabled, Is.True);
                        Assert.That(renderer.sprite?.name, Is.EqualTo(step == firstFrameStep ? "VFX-Slash_0" : "VFX-Slash_2"));
                    }
                }
            }
            Assert.That(renderer.enabled, Is.False);
            Assert.That(renderer.sprite, Is.Null);
        }
        finally
        {
            if (player != null) Object.DestroyImmediate(player);
            if (overrides != null) Object.DestroyImmediate(overrides);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }

    [Test]
    public void Prefab_HasExplicitExistingAnimatorRootReferences()
    {
        Assert.That(_renderer.transform.parent.name, Is.EqualTo("VisualRoot"));
        Assert.That(_renderer.transform.name, Is.EqualTo("AttackVfx"));
        Assert.That(_renderer.sortingLayerName, Is.EqualTo("Characters"));
        Assert.That(_renderer.enabled, Is.False);
        SerializedObject serialized = new SerializedObject(_presenter);
        foreach (string field in new[] { "_combatController", "_equipmentSource", "_character", "_animator", "_vfxTransform", "_vfxRenderer" })
            Assert.That(serialized.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
        Animator animator = (Animator)serialized.FindProperty("_animator").objectReferenceValue;
        Assert.That(animator.transform, Is.EqualTo(_renderer.transform.parent));
    }
}
