using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class CastFlashPresentationTests
{
    private const string PrefabPath = "Assets/Prefabs/NetworkPlayer.prefab";
    private const string WandPath = "Assets/Scriptable Objects/Loot/Definitions/MagicWandWeaponDefinition.asset";
    private const string WandLootPath = "Assets/Scriptable Objects/Loot/Definitions/MagicWand.asset";
    private const string WandCastFlashPath = "Assets/Scriptable Objects/Loot/Definitions/MagicWandCastFlashAttackVfx.asset";
    private const string CastFlashVisualPath = "Assets/Scriptable Objects/Loot/Definitions/CastFlashVfxVisual.asset";
    private const string CastFlashClipPath = "Assets/Art/VFX/CastFlashVfx.anim";
    private const string CastFlashTexturePath = "Assets/Art/VFX/VFX-WandAttack.png";
    private const string SwordPath = "Assets/Scriptable Objects/Loot/Definitions/ArmingSwordWeaponDefinition.asset";
    private const string RapierPath = "Assets/Scriptable Objects/Loot/Definitions/RapierWeaponDefinition.asset";
    private const string PresenterScriptPath = "Assets/Scripts/Player/Presentation/PlayerAttackVfxPresenter.cs";
    // The wand flicks forward until the tip stops at 0.35s, on the clip's 20 fps grid, then recovers.
    private const float CastSeconds = 0.35f;
    // The tight sprite mesh pads the art by up to two pixels at the project's 16 PPU.
    private const float MeshPadding = 0.125f;
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

    private void Perform(LootDefinition loot, Vector2 direction)
    {
        Assert.That(_catalog.TryGetIndex(loot.LootId, out int index), Is.True);
        var attack = new AttackPerformedEvent(default, AttackType.Ranged, Vector2.zero, direction, 0, index + 1);
        typeof(PlayerAttackVfxPresenter).GetMethod("OnAttackPerformed", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(_presenter, new object[] { attack });
    }

    private static CastFlashVfxVisualDefinition CastFlash(AttackVfxDefinition vfx)
    {
        Assert.That(vfx.Visual, Is.InstanceOf<CastFlashVfxVisualDefinition>(), vfx.name);
        return (CastFlashVfxVisualDefinition)vfx.Visual;
    }

    private static Rect MeshBounds(Sprite sprite)
    {
        Vector2 min = Vector2.positiveInfinity;
        Vector2 max = Vector2.negativeInfinity;
        foreach (Vector2 vertex in sprite.vertices)
        {
            min = Vector2.Min(min, vertex);
            max = Vector2.Max(max, vertex);
        }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    [Test]
    public void CastFlashVisual_HasExclusiveChildSpriteBindingAndCenterMatchingItsArt()
    {
        var visual = AssetDatabase.LoadAssetAtPath<CastFlashVfxVisualDefinition>(CastFlashVisualPath);
        Assert.That(visual, Is.Not.Null);
        Assert.That(visual.TryValidate(out string error), Is.True, error);
        Assert.That(AssetDatabase.GetAssetPath(visual.Clip), Is.EqualTo(CastFlashClipPath));
        Assert.That(visual.Clip.length, Is.EqualTo(0.2f).Within(0.0001f));
        Assert.That(visual.Clip.isLooping, Is.False);
        EditorCurveBinding[] bindings = AnimationUtility.GetObjectReferenceCurveBindings(visual.Clip);
        Assert.That(bindings, Has.Length.EqualTo(1));
        Assert.That(bindings[0].path, Is.EqualTo("AttackVfx"));
        Assert.That(bindings[0].type, Is.EqualTo(typeof(SpriteRenderer)));
        Assert.That(bindings[0].propertyName, Is.EqualTo("m_Sprite"));
        Assert.That(AnimationUtility.GetCurveBindings(visual.Clip), Is.Empty);

        ObjectReferenceKeyframe[] frames = AnimationUtility.GetObjectReferenceCurve(visual.Clip, bindings[0]);
        Assert.That(frames, Has.Length.EqualTo(4));
        var sizes = new float[frames.Length];
        for (int i = 0; i < frames.Length; i++)
        {
            var sprite = (Sprite)frames[i].value;
            Assert.That(frames[i].time, Is.EqualTo(i * 0.05f).Within(0.0001f));
            Assert.That(sprite.name, Is.EqualTo($"VFX-WandAttack_{i}"));
            Assert.That(AssetDatabase.GetAssetPath(sprite), Is.EqualTo(CastFlashTexturePath));
            // Uniform 96 px cells with centered pivots keep every frame on the same canvas.
            Assert.That(sprite.rect, Is.EqualTo(new Rect(i * 96f, 0f, 96f, 96f)), sprite.name);
            Assert.That(sprite.pivot, Is.EqualTo(new Vector2(48f, 48f)), sprite.name);
            Assert.That(sprite.pixelsPerUnit, Is.EqualTo(16f), sprite.name);
            Rect bounds = MeshBounds(sprite);
            // The flash grows and shrinks around one point: every frame is centered on it.
            Assert.That((bounds.center - visual.Center).magnitude, Is.LessThanOrEqualTo(MeshPadding), $"{sprite.name} centered on the flash");
            sizes[i] = Mathf.Max(bounds.width, bounds.height);
        }
        // Measured from the art: the flash core is the pixel left of and below the cell center.
        Assert.That(visual.Center, Is.EqualTo(new Vector2(-0.03125f, -0.03125f)));
        // Ignition, burst, contraction and fade, in cell order.
        Assert.That(sizes[1], Is.GreaterThan(sizes[2]));
        Assert.That(sizes[2], Is.GreaterThan(sizes[0]));
        Assert.That(sizes[0], Is.GreaterThanOrEqualTo(sizes[3]));
    }

    // Magic Wand winds up until 0.2s, flicks forward until its tip stops at 0.35s and recovers until 0.6s,
    // with the same phase in every facing. Each anchor is the grip at that stop and each rotation the grip to
    // tip axis, so the flash lands on the tip of any wand length. Starting at 0.325s ignites frame 0 as the
    // tip arrives and bursts frame 1 on the hold; frames 2-3 fade where the cast happened.
    [Test]
    public void MagicWandVfxConfiguration_AlignsCastFlashWithItsOwnCast()
    {
        WeaponDefinition wand = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(WandPath);
        AttackVfxDefinition vfx = wand.Presentation.AttackVfx;
        Assert.That(vfx, Is.Not.Null);
        Assert.That(AssetDatabase.GetAssetPath(vfx), Is.EqualTo(WandCastFlashPath));
        Assert.That(AssetDatabase.GetAssetPath(CastFlash(vfx)), Is.EqualTo(CastFlashVisualPath));
        Assert.That(wand.TryValidate(out string error), Is.True, error);
        // MagicWand.png is 4x16 px with a centered pivot: the tip is its single top pixel, right of center.
        Assert.That(wand.Presentation.BladeTip, Is.EqualTo(new Vector2(0.03125f, 0.46875f)));
        Assert.That(wand.Presentation.BladeReach, Is.EqualTo(0.8443f).Within(0.0001f));
        Assert.That(vfx.StartSeconds, Is.EqualTo(0.325f));
        // Index order: N, NE, NW, S, SE, SW. Sorting follows the facing, like the held weapon.
        var positions = new[]
        {
            new Vector3(0.39f, 0.06f, 0f), new Vector3(0.47f, -0.07f, 0f), new Vector3(0.17f, 0.11f, 0f),
            new Vector3(-0.33f, -0.5f, 0f), new Vector3(-0.13f, -0.49f, 0f), new Vector3(-0.43f, -0.31f, 0f)
        };
        var rotations = new[] { 104f, 59f, 153.5f, -76f, -31f, -116.5f };
        var sortingOrders = new[] { -9, -9, -9, 21, 21, 21 };
        for (int i = 0; i < 6; i++)
        {
            AttackVfxDefinition.DirectionalPose pose = vfx.GetPose(i);
            Assert.That(pose.Position, Is.EqualTo(positions[i]), $"pose {i}");
            Assert.That(Quaternion.Angle(pose.Rotation, Quaternion.Euler(0f, 0f, rotations[i])), Is.EqualTo(0f).Within(0.001f), $"pose {i}");
            Assert.That(pose.ReachOffset, Is.EqualTo(0f), $"pose {i}");
            Assert.That(pose.Mirrored, Is.False, $"pose {i}");
            Assert.That(pose.SortingOrder, Is.EqualTo(sortingOrders[i]), $"pose {i}");
            Assert.That(vfx.StartSeconds, Is.LessThan(CastSeconds), $"pose {i} ignites before the cast");
            Assert.That(vfx.StartSeconds + vfx.Clip.length,
                Is.LessThanOrEqualTo(wand.Presentation.GetAttackClip(i).length), $"clip {i}");
        }
    }

    [Test]
    public void CastFlashPose_CentersNativeArtOnCastPointForAnyReach()
    {
        AttackVfxDefinition vfx = AssetDatabase.LoadAssetAtPath<AttackVfxDefinition>(WandCastFlashPath);
        CastFlashVfxVisualDefinition visual = CastFlash(vfx);
        for (int i = 0; i < 6; i++)
        {
            AttackVfxDefinition.DirectionalPose source = vfx.GetPose(i);
            Vector3 axis = source.Rotation * Vector3.right;
            foreach (float reach in new[] { 0.5f, 0.8443f, 1.5f })
            {
                Assert.That(vfx.TryResolvePose(i, reach, out AttackVfxDefinition.ResolvedPose pose), Is.True, $"pose {i}");
                // Point art is neither stretched by the reach nor turned or mirrored by the pose.
                Assert.That(pose.Scale, Is.EqualTo(Vector3.one), $"pose {i}");
                Assert.That(Quaternion.Angle(pose.Rotation, Quaternion.identity), Is.EqualTo(0f), $"pose {i}");
                Assert.That(pose.SortingOrder, Is.EqualTo(source.SortingOrder), $"pose {i}");
                Vector3 center = pose.Position + (Vector3)visual.Center;
                Vector3 castPoint = source.Position + axis * (source.ReachOffset + reach);
                Assert.That((center - castPoint).magnitude, Is.LessThan(0.0001f), $"pose {i} reach {reach}");
            }
            Assert.That(vfx.TryResolvePose(i, -source.ReachOffset, out _), Is.False, $"pose {i} zero reach");
        }
        Assert.That(vfx.TryResolvePose(6, 1f, out _), Is.False);
        Assert.That(vfx.TryResolvePose(0, float.NaN, out _), Is.False);
    }

    [TestCase(CharacterVisualDirection.North, 0)]
    [TestCase(CharacterVisualDirection.NorthEast, 1)]
    [TestCase(CharacterVisualDirection.NorthWest, 2)]
    [TestCase(CharacterVisualDirection.South, 3)]
    [TestCase(CharacterVisualDirection.SouthEast, 4)]
    [TestCase(CharacterVisualDirection.SouthWest, 5)]
    public void ConfirmedWandAttack_AppliesCastFlashPoseResolvedFromBladeReach(CharacterVisualDirection direction, int index)
    {
        LootDefinition loot = AssetDatabase.LoadAssetAtPath<LootDefinition>(WandLootPath);
        WeaponDefinition.PresentationConfig presentation = loot.WeaponDefinition.Presentation;
        Assert.That(presentation.AttackVfx.TryResolvePose(index, presentation.BladeReach,
            out AttackVfxDefinition.ResolvedPose pose), Is.True);
        Perform(loot, CharacterVisualDirectionResolver.GetCanonicalVector(direction));
        Assert.That(typeof(PlayerAttackVfxPresenter).GetField("_pending", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(_presenter), Is.True);
        Assert.That(_renderer.transform.localPosition, Is.EqualTo(pose.Position));
        Assert.That(Quaternion.Angle(_renderer.transform.localRotation, pose.Rotation), Is.EqualTo(0f).Within(0.001f));
        Assert.That(_renderer.transform.localScale, Is.EqualTo(pose.Scale));
        Assert.That(_renderer.sortingOrder, Is.EqualTo(pose.SortingOrder));
        Assert.That(_renderer.enabled, Is.False);
    }

    // Spatial contract: the flash sits on the wand tip at the cast stop, and the ignition and burst frames
    // contain the tip while they play. The wand is posed with the production grip/facing math, so a longer
    // wand on the same grip and axis marks its own tip without asset changes.
    [TestCase(CharacterVisualDirection.North, 0)]
    [TestCase(CharacterVisualDirection.NorthEast, 1)]
    [TestCase(CharacterVisualDirection.NorthWest, 2)]
    [TestCase(CharacterVisualDirection.South, 3)]
    [TestCase(CharacterVisualDirection.SouthEast, 4)]
    [TestCase(CharacterVisualDirection.SouthWest, 5)]
    public void CastFlashVfx_MarksWandTipAtCastForWandAndLongerTip(CharacterVisualDirection direction, int index)
    {
        WeaponDefinition wand = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(WandPath);
        WeaponDefinition longerTip = Object.Instantiate(wand);
        longerTip.name = $"{wand.name} longer tip";
        var serialized = new SerializedObject(longerTip);
        // Half again as long along the same grip to tip axis.
        Vector2 grip = wand.Presentation.GripPoint;
        serialized.FindProperty("_presentation._bladeTip").vector2Value = grip + (wand.Presentation.BladeTip - grip) * 1.5f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        try
        {
            Assert.That(longerTip.Presentation.BladeReach, Is.GreaterThan(wand.Presentation.BladeReach));
            foreach (WeaponDefinition weapon in new[] { wand, longerTip })
            {
                Assert.That(weapon.TryValidate(out string error), Is.True, error);
                AssertFlashMarksTip(weapon, direction, index);
            }
        }
        finally
        {
            Object.DestroyImmediate(longerTip);
        }
    }

    private void AssertFlashMarksTip(WeaponDefinition weapon, CharacterVisualDirection direction, int index)
    {
        // Half a pixel at the project's 16 PPU.
        const float MaxCastError = 0.03125f;
        WeaponDefinition.PresentationConfig presentation = weapon.Presentation;
        AttackVfxDefinition vfx = presentation.AttackVfx;
        CastFlashVfxVisualDefinition visual = CastFlash(vfx);
        Assert.That(vfx.TryResolvePose(index, presentation.BladeReach, out AttackVfxDefinition.ResolvedPose pose), Is.True);
        Vector2 center = pose.Position + pose.Rotation * Vector3.Scale(pose.Scale, visual.Center);
        Transform root = _renderer.transform.parent;
        Transform held = PoseHeldWeapon(presentation, CharacterVisualDirectionResolver.GetCanonicalVector(direction));
        AnimationClip attack = presentation.GetAttackClip(index);
        string context = $"{weapon.name} {direction}";

        float castError = (SampleTip(attack, root, held, presentation, CastSeconds) - center).magnitude;
        Assert.That(castError, Is.LessThanOrEqualTo(MaxCastError), $"{context}: tip {castError:F3} off the flash at the cast");

        EditorCurveBinding[] bindings = AnimationUtility.GetObjectReferenceCurveBindings(vfx.Clip);
        ObjectReferenceKeyframe[] frames = AnimationUtility.GetObjectReferenceCurve(vfx.Clip, bindings[0]);
        // Ignition and burst: the frames on screen while the tip arrives at and holds the cast point.
        for (int i = 0; i < 2; i++)
        {
            float frameEnd = frames[i + 1].time;
            float midpoint = vfx.StartSeconds + (frames[i].time + frameEnd) * 0.5f;
            Rect bounds = MeshBounds((Sprite)frames[i].value);
            // Measure the drawn pixels, not the tight mesh padding around them.
            float drawnRadius = Mathf.Min(bounds.width, bounds.height) * 0.5f - MeshPadding;
            float distance = (SampleTip(attack, root, held, presentation, midpoint) - center).magnitude;
            Assert.That(distance, Is.LessThanOrEqualTo(drawnRadius), $"{context} frame {i}: tip {distance:F3} outside flash radius {drawnRadius:F3}");
        }
    }

    private static Vector2 SampleTip(AnimationClip attack, Transform root, Transform held,
        WeaponDefinition.PresentationConfig presentation, float seconds)
    {
        attack.SampleAnimation(root.gameObject, seconds);
        Matrix4x4 weaponToRoot = root.worldToLocalMatrix * held.localToWorldMatrix;
        return weaponToRoot.MultiplyPoint3x4(presentation.BladeTip);
    }

    [Test]
    public void CastFlash_IsSeparateArchetypeFromSlashAndThrust()
    {
        AttackVfxDefinition wand = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(WandPath).Presentation.AttackVfx;
        AttackVfxDefinition sword = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(SwordPath).Presentation.AttackVfx;
        AttackVfxDefinition rapier = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(RapierPath).Presentation.AttackVfx;
        Assert.That(sword.Visual, Is.InstanceOf<SlashVfxVisualDefinition>());
        Assert.That(rapier.Visual, Is.InstanceOf<ThrustVfxVisualDefinition>());
        Assert.That(CastFlash(wand).Clip, Is.Not.SameAs(sword.Clip));
        Assert.That(wand.Clip, Is.Not.SameAs(rapier.Clip));
    }

    // Placement belongs to the visual and alignment assets: the presenter stays archetype- and weapon-agnostic.
    [Test]
    public void Presenter_KnowsNoArchetypeOrWeapon()
    {
        string source = AssetDatabase.LoadAssetAtPath<MonoScript>(PresenterScriptPath).text;
        foreach (string term in new[] { "CastFlash", "Slash", "Thrust", "MagicWand", "Wand" })
        {
            Assert.That(source, Does.Not.Contain(term), term);
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
}
