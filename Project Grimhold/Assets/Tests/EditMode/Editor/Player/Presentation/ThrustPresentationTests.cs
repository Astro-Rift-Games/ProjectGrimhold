using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class ThrustPresentationTests
{
    private const string PrefabPath = "Assets/Prefabs/NetworkPlayer.prefab";
    private const string RapierPath = "Assets/Scriptable Objects/Loot/Definitions/RapierWeaponDefinition.asset";
    private const string RapierLootPath = "Assets/Scriptable Objects/Loot/Definitions/Rapier.asset";
    private const string RondelPath = "Assets/Scriptable Objects/Loot/Definitions/RondelDaggerWeaponDefinition.asset";
    private const string RondelLootPath = "Assets/Scriptable Objects/Loot/Definitions/RondelDagger.asset";
    private const string SwordPath = "Assets/Scriptable Objects/Loot/Definitions/ArmingSwordWeaponDefinition.asset";
    private const string ThrustVisualPath = "Assets/Scriptable Objects/Loot/Definitions/ThrustVfxVisual.asset";
    private const string ThrustClipPath = "Assets/Art/VFX/ThrustVfx.anim";
    private const string ThrustTexturePath = "Assets/Art/VFX/VFX-Thrust.png";
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
        var attack = new AttackPerformedEvent(default, AttackType.Melee, Vector2.zero, direction, 0, index + 1);
        typeof(PlayerAttackVfxPresenter).GetMethod("OnAttackPerformed", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(_presenter, new object[] { attack });
    }

    private static ThrustVfxVisualDefinition Thrust(AttackVfxDefinition vfx)
    {
        Assert.That(vfx.Visual, Is.InstanceOf<ThrustVfxVisualDefinition>(), vfx.name);
        return (ThrustVfxVisualDefinition)vfx.Visual;
    }

    [Test]
    public void ThrustVisual_HasExclusiveChildSpriteBindingAndPathMatchingItsArt()
    {
        var visual = AssetDatabase.LoadAssetAtPath<ThrustVfxVisualDefinition>(ThrustVisualPath);
        Assert.That(visual, Is.Not.Null);
        Assert.That(visual.TryValidate(out string error), Is.True, error);
        Assert.That(AssetDatabase.GetAssetPath(visual.Clip), Is.EqualTo(ThrustClipPath));
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
        float back = float.MaxValue;
        float front = float.MinValue;
        for (int i = 0; i < frames.Length; i++)
        {
            var sprite = (Sprite)frames[i].value;
            Assert.That(frames[i].time, Is.EqualTo(i * 0.05f).Within(0.0001f));
            Assert.That(sprite.name, Is.EqualTo($"VFX-Thrust_{i}"));
            Assert.That(AssetDatabase.GetAssetPath(sprite), Is.EqualTo(ThrustTexturePath));
            // Uniform 96 px cells with centered pivots keep every frame on the same local axis.
            Assert.That(sprite.rect, Is.EqualTo(new Rect(i * 96f, 0f, 96f, 96f)), sprite.name);
            Assert.That(sprite.pivot, Is.EqualTo(new Vector2(48f, 48f)), sprite.name);
            Vector2 min = Vector2.positiveInfinity;
            Vector2 max = Vector2.negativeInfinity;
            foreach (Vector2 vertex in sprite.vertices)
            {
                min = Vector2.Min(min, vertex);
                max = Vector2.Max(max, vertex);
            }
            Assert.That(Mathf.Abs((min.y + max.y) * 0.5f), Is.LessThanOrEqualTo(MeshPadding), $"{sprite.name} centered on its axis");
            back = Mathf.Min(back, min.x);
            front = Mathf.Max(front, max.x);
        }
        // The art travels forward frame by frame, and the path ends are measured from it, not assumed.
        Assert.That(visual.BackX, Is.EqualTo(back).Within(MeshPadding + 0.0001f));
        Assert.That(visual.FrontX, Is.EqualTo(front).Within(MeshPadding + 0.0001f));
        Assert.That(visual.BackX, Is.EqualTo(-1f));
        Assert.That(visual.FrontX, Is.EqualTo(1f));
    }

    // Rapier retracts until 0.25s, thrusts along the blade axis until 0.35s and retracts to 0.45s. Its
    // grip travels 0.52 along the axis in every facing and the blade holds the facing angle, so the
    // Thrust starts at the stroke-start grip, points along the blade and ends at the extended tip.
    // Starting at 0.19s shows the glint while the blade settles onto its axis, then keeps the
    // accelerating tip inside frames 1-3 up to full extension.
    [Test]
    public void RapierVfxConfiguration_AlignsSharedThrustWithItsOwnStroke()
    {
        WeaponDefinition rapier = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(RapierPath);
        AttackVfxDefinition vfx = rapier.Presentation.AttackVfx;
        Assert.That(vfx, Is.Not.Null);
        Assert.That(AssetDatabase.GetAssetPath(vfx),
            Is.EqualTo("Assets/Scriptable Objects/Loot/Definitions/RapierThrustAttackVfx.asset"));
        Assert.That(AssetDatabase.GetAssetPath(vfx.Visual), Is.EqualTo(ThrustVisualPath));
        Assert.That(rapier.TryValidate(out string error), Is.True, error);
        // Rapier.png is 27 px tall with a centered pivot: the tip is the center of its top pixel row.
        Assert.That(rapier.Presentation.BladeTip, Is.EqualTo(new Vector2(0f, 0.8125f)));
        Assert.That(vfx.StartSeconds, Is.EqualTo(0.19f));
        // Index order: N, NE, NW, S, SE, SW. Sorting follows the facing, like the held weapon.
        var positions = new[]
        {
            new Vector3(-0.02f, -0.44f, 0f), new Vector3(-0.13f, -0.18f, 0f), new Vector3(0.18f, -0.51f, 0f),
            new Vector3(-0.04f, 0f, 0f), new Vector3(-0.24f, 0.13f, 0f), new Vector3(0.07f, -0.2f, 0f)
        };
        var rotations = new[] { 90f, 45f, 135f, -90f, -45f, -135f };
        var sortingOrders = new[] { -9, -9, -9, 21, 21, 21 };
        for (int i = 0; i < 6; i++)
        {
            AttackVfxDefinition.DirectionalPose pose = vfx.GetPose(i);
            Assert.That(pose.Position, Is.EqualTo(positions[i]), $"pose {i}");
            Assert.That(Quaternion.Angle(pose.Rotation, Quaternion.Euler(0f, 0f, rotations[i])), Is.EqualTo(0f).Within(0.001f), $"pose {i}");
            Assert.That(pose.ReachOffset, Is.EqualTo(0.52f), $"pose {i}");
            Assert.That(pose.Mirrored, Is.False, $"pose {i}");
            Assert.That(pose.SortingOrder, Is.EqualTo(sortingOrders[i]), $"pose {i}");
            Assert.That(vfx.StartSeconds + vfx.Clip.length,
                Is.LessThanOrEqualTo(rapier.Presentation.GetAttackClip(i).length), $"clip {i}");
        }
    }

    // Rondel retracts until 0.2s, thrusts until 0.3s and recoils to 0.45s, 50 ms ahead of Rapier. Its hand
    // tilts the blade 12-17 degrees off the facing through the stroke, so each Thrust axis is the facing
    // plus 14 degrees, the mean blade angle over frames 1-3. Each anchor is the stroke-start grip and the
    // 0.5 reach offset is the grip's travel along that axis, so the path ends at the extended tip for any
    // blade reach. Starting at 0.14s keeps Rapier's phase: the glint lands 60 ms before the stroke and
    // frame 3 reaches full extension. Rapier's 0.19s start would leave the art behind the dagger tip.
    [Test]
    public void RondelVfxConfiguration_AlignsSharedThrustWithItsOwnStroke()
    {
        WeaponDefinition rondel = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(RondelPath);
        AttackVfxDefinition vfx = rondel.Presentation.AttackVfx;
        Assert.That(vfx, Is.Not.Null);
        Assert.That(AssetDatabase.GetAssetPath(vfx),
            Is.EqualTo("Assets/Scriptable Objects/Loot/Definitions/RondelDaggerThrustAttackVfx.asset"));
        Assert.That(AssetDatabase.GetAssetPath(vfx.Visual), Is.EqualTo(ThrustVisualPath));
        Assert.That(rondel.TryValidate(out string error), Is.True, error);
        // RondelDagger.png is 17 px tall with a centered pivot: the tip is the center of its top pixel row.
        Assert.That(rondel.Presentation.BladeTip, Is.EqualTo(new Vector2(0f, 0.5f)));
        Assert.That(rondel.Presentation.BladeReach, Is.EqualTo(0.875f).Within(0.0001f));
        Assert.That(vfx.StartSeconds, Is.EqualTo(0.14f));
        // Index order: N, NE, NW, S, SE, SW. Sorting follows the facing, like the held weapon.
        var positions = new[]
        {
            new Vector3(0.47f, -0.41f, 0f), new Vector3(0.2f, -0.47f, 0f), new Vector3(0.55f, -0.16f, 0f),
            new Vector3(-0.42f, -0.03f, 0f), new Vector3(-0.53f, -0.22f, 0f), new Vector3(-0.18f, 0.06f, 0f)
        };
        var facings = new[] { 90f, 45f, 135f, -90f, -45f, -135f };
        var sortingOrders = new[] { -9, -9, -9, 21, 21, 21 };
        for (int i = 0; i < 6; i++)
        {
            AttackVfxDefinition.DirectionalPose pose = vfx.GetPose(i);
            Assert.That(pose.Position, Is.EqualTo(positions[i]), $"pose {i}");
            Assert.That(Quaternion.Angle(pose.Rotation, Quaternion.Euler(0f, 0f, facings[i] + 14f)), Is.EqualTo(0f).Within(0.001f), $"pose {i}");
            Assert.That(pose.ReachOffset, Is.EqualTo(0.5f), $"pose {i}");
            Assert.That(pose.Mirrored, Is.False, $"pose {i}");
            Assert.That(pose.SortingOrder, Is.EqualTo(sortingOrders[i]), $"pose {i}");
            Assert.That(vfx.StartSeconds + vfx.Clip.length,
                Is.LessThanOrEqualTo(rondel.Presentation.GetAttackClip(i).length), $"clip {i}");
        }
    }

    [Test]
    public void RapierAndRondel_ShareOneThrustVisualThroughTheirOwnAlignments()
    {
        AttackVfxDefinition rapier = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(RapierPath).Presentation.AttackVfx;
        AttackVfxDefinition rondel = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(RondelPath).Presentation.AttackVfx;
        Assert.That(rondel, Is.Not.SameAs(rapier));
        Assert.That(Thrust(rondel), Is.SameAs(AssetDatabase.LoadAssetAtPath<ThrustVfxVisualDefinition>(ThrustVisualPath)));
        Assert.That(rondel.Visual, Is.SameAs(rapier.Visual));
        Assert.That(rondel.Clip, Is.SameAs(rapier.Clip));
        Assert.That(rondel.StartSeconds, Is.Not.EqualTo(rapier.StartSeconds));
        for (int i = 0; i < 6; i++)
        {
            Assert.That(rondel.TryResolvePose(i, 1f, out AttackVfxDefinition.ResolvedPose rondelPose), Is.True);
            Assert.That(rapier.TryResolvePose(i, 1f, out AttackVfxDefinition.ResolvedPose rapierPose), Is.True);
            Assert.That(Quaternion.Angle(rondelPose.Rotation, rapierPose.Rotation), Is.GreaterThan(1f), $"pose {i}");
            Assert.That(rondelPose.Position, Is.Not.EqualTo(rapierPose.Position), $"pose {i}");
        }
    }

    [Test]
    public void ThrustAndSlash_AreSeparateVisualsWithSeparateAlignments()
    {
        AttackVfxDefinition rapier = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(RapierPath).Presentation.AttackVfx;
        AttackVfxDefinition sword = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(SwordPath).Presentation.AttackVfx;
        Assert.That(rapier, Is.Not.SameAs(sword));
        Assert.That(rapier.Visual, Is.Not.SameAs(sword.Visual));
        Assert.That(sword.Visual, Is.InstanceOf<SlashVfxVisualDefinition>());
        Assert.That(Thrust(rapier).Clip, Is.Not.SameAs(sword.Clip));
    }

    [TestCase(RapierLootPath, CharacterVisualDirection.North, 0)]
    [TestCase(RapierLootPath, CharacterVisualDirection.NorthEast, 1)]
    [TestCase(RapierLootPath, CharacterVisualDirection.NorthWest, 2)]
    [TestCase(RapierLootPath, CharacterVisualDirection.South, 3)]
    [TestCase(RapierLootPath, CharacterVisualDirection.SouthEast, 4)]
    [TestCase(RapierLootPath, CharacterVisualDirection.SouthWest, 5)]
    [TestCase(RondelLootPath, CharacterVisualDirection.North, 0)]
    [TestCase(RondelLootPath, CharacterVisualDirection.NorthEast, 1)]
    [TestCase(RondelLootPath, CharacterVisualDirection.NorthWest, 2)]
    [TestCase(RondelLootPath, CharacterVisualDirection.South, 3)]
    [TestCase(RondelLootPath, CharacterVisualDirection.SouthEast, 4)]
    [TestCase(RondelLootPath, CharacterVisualDirection.SouthWest, 5)]
    public void ConfirmedThrustAttack_AppliesPoseResolvedFromBladeReach(string lootPath, CharacterVisualDirection direction, int index)
    {
        LootDefinition loot = AssetDatabase.LoadAssetAtPath<LootDefinition>(lootPath);
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

    [TestCase(RapierPath)]
    [TestCase(RondelPath)]
    public void ThrustPose_StartsAtAnchorAndGrowsForwardWithBladeReach(string weaponPath)
    {
        AttackVfxDefinition vfx = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(weaponPath).Presentation.AttackVfx;
        ThrustVfxVisualDefinition visual = Thrust(vfx);
        for (int i = 0; i < 6; i++)
        {
            AttackVfxDefinition.DirectionalPose source = vfx.GetPose(i);
            Assert.That(vfx.TryResolvePose(i, 1f, out AttackVfxDefinition.ResolvedPose shorter), Is.True);
            Assert.That(vfx.TryResolvePose(i, 2f, out AttackVfxDefinition.ResolvedPose longer), Is.True);
            Vector3 axis = source.Rotation * Vector3.right;
            foreach ((float reach, AttackVfxDefinition.ResolvedPose pose) in new[] { (1f, shorter), (2f, longer) })
            {
                float scale = (source.ReachOffset + reach) / visual.Length;
                Assert.That(pose.Scale.x, Is.EqualTo(scale).Within(0.0001f), $"pose {i}");
                Assert.That(pose.Scale.y, Is.EqualTo(scale).Within(0.0001f), $"pose {i}");
                Vector3 back = pose.Position + pose.Rotation * Vector3.right * (visual.BackX * pose.Scale.x);
                Vector3 front = pose.Position + pose.Rotation * Vector3.right * (visual.FrontX * pose.Scale.x);
                Assert.That((back - source.Position).magnitude, Is.LessThan(0.0001f), $"pose {i} path start");
                Assert.That((front - source.Position - axis * (source.ReachOffset + reach)).magnitude,
                    Is.LessThan(0.0001f), $"pose {i} path end");
            }
            Assert.That(Quaternion.Angle(longer.Rotation, shorter.Rotation), Is.EqualTo(0f), $"pose {i}");
            Assert.That(longer.SortingOrder, Is.EqualTo(shorter.SortingOrder), $"pose {i}");
            Assert.That(vfx.TryResolvePose(i, -source.ReachOffset, out _), Is.False, $"pose {i} zero size");
        }
        Assert.That(vfx.TryResolvePose(6, 1f, out _), Is.False);
        Assert.That(vfx.TryResolvePose(0, float.NaN, out _), Is.False);
    }

    // Spatial contract: in every facing the Thrust points along the blade and each frame's art stays on
    // the blade while it plays. Frame 0 is a glint on the retracted blade; frames 1-3 carry the blade tip
    // through the strike to full extension. The blade is posed with the production grip/facing math, so
    // a shorter blade sharing the Thrust, down to dagger length, must align without asset changes.
    // Much longer blades scale the art beyond the fixed hand stroke and need their own start offset.
    [TestCase(CharacterVisualDirection.North, 0)]
    [TestCase(CharacterVisualDirection.NorthEast, 1)]
    [TestCase(CharacterVisualDirection.NorthWest, 2)]
    [TestCase(CharacterVisualDirection.South, 3)]
    [TestCase(CharacterVisualDirection.SouthEast, 4)]
    [TestCase(CharacterVisualDirection.SouthWest, 5)]
    public void ThrustVfx_FollowsBladeForRapierAndVariantGeometries(CharacterVisualDirection direction, int index)
    {
        WeaponDefinition rapier = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(RapierPath);
        WeaponDefinition daggerLength = CreateGeometryVariant(rapier, new Vector2(0f, -0.375f), new Vector2(0f, 0.5f));
        WeaponDefinition shorterBlade = CreateGeometryVariant(rapier, new Vector2(0f, -0.5f), new Vector2(0f, 0.625f));
        try
        {
            foreach (WeaponDefinition weapon in new[] { rapier, daggerLength, shorterBlade })
            {
                Assert.That(weapon.Presentation.AttackVfx, Is.SameAs(rapier.Presentation.AttackVfx));
                Assert.That(weapon.TryValidate(out string error), Is.True, error);
                AssertThrustFollowsBlade(weapon, direction, index);
            }
        }
        finally
        {
            Object.DestroyImmediate(daggerLength);
            Object.DestroyImmediate(shorterBlade);
        }
    }

    // Rondel's alignment is fitted to its own stroke. Blade length alone sizes the Thrust: a shorter and a
    // longer blade on the same grip stay aligned in every facing without asset or code changes.
    [TestCase(CharacterVisualDirection.North, 0)]
    [TestCase(CharacterVisualDirection.NorthEast, 1)]
    [TestCase(CharacterVisualDirection.NorthWest, 2)]
    [TestCase(CharacterVisualDirection.South, 3)]
    [TestCase(CharacterVisualDirection.SouthEast, 4)]
    [TestCase(CharacterVisualDirection.SouthWest, 5)]
    public void ThrustVfx_FollowsBladeForRondelAndBladeLengthVariants(CharacterVisualDirection direction, int index)
    {
        WeaponDefinition rondel = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(RondelPath);
        Vector2 grip = rondel.Presentation.GripPoint;
        WeaponDefinition shorterBlade = CreateGeometryVariant(rondel, grip, new Vector2(0f, 0.25f));
        WeaponDefinition longerBlade = CreateGeometryVariant(rondel, grip, new Vector2(0f, 0.75f));
        try
        {
            Assert.That(shorterBlade.Presentation.BladeReach, Is.LessThan(rondel.Presentation.BladeReach));
            Assert.That(longerBlade.Presentation.BladeReach, Is.GreaterThan(rondel.Presentation.BladeReach));
            foreach (WeaponDefinition weapon in new[] { rondel, shorterBlade, longerBlade })
            {
                Assert.That(weapon.Presentation.AttackVfx, Is.SameAs(rondel.Presentation.AttackVfx));
                Assert.That(weapon.TryValidate(out string error), Is.True, error);
                AssertThrustFollowsBlade(weapon, direction, index);
            }
        }
        finally
        {
            Object.DestroyImmediate(shorterBlade);
            Object.DestroyImmediate(longerBlade);
        }
    }

    private static WeaponDefinition CreateGeometryVariant(WeaponDefinition source, Vector2 gripPoint, Vector2 bladeTip)
    {
        WeaponDefinition variant = Object.Instantiate(source);
        variant.name = $"{source.name} reach {Vector2.Distance(gripPoint, bladeTip):F3}";
        var serialized = new SerializedObject(variant);
        serialized.FindProperty("_presentation._gripPoint").vector2Value = gripPoint;
        serialized.FindProperty("_presentation._bladeTip").vector2Value = bladeTip;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return variant;
    }

    private void AssertThrustFollowsBlade(WeaponDefinition weapon, CharacterVisualDirection direction, int index)
    {
        const float MaxAxisErrorDegrees = 5f;
        const float MaxLateralError = 0.1f;
        const float AlongTolerance = 0.1f;
        WeaponDefinition.PresentationConfig presentation = weapon.Presentation;
        AttackVfxDefinition vfx = presentation.AttackVfx;
        Assert.That(vfx.TryResolvePose(index, presentation.BladeReach, out AttackVfxDefinition.ResolvedPose pose), Is.True);
        float padding = MeshPadding * Mathf.Abs(pose.Scale.x);
        Transform root = _renderer.transform.parent;
        Transform visual = PoseHeldWeapon(presentation, CharacterVisualDirectionResolver.GetCanonicalVector(direction));
        Matrix4x4 vfxToRoot = Matrix4x4.TRS(pose.Position, pose.Rotation, pose.Scale);
        Vector2 origin = pose.Position;
        Vector2 axis = pose.Rotation * Vector3.right;
        Vector2 normal = new Vector2(-axis.y, axis.x);
        AnimationClip attack = presentation.GetAttackClip(index);

        EditorCurveBinding[] bindings = AnimationUtility.GetObjectReferenceCurveBindings(vfx.Clip);
        ObjectReferenceKeyframe[] frames = AnimationUtility.GetObjectReferenceCurve(vfx.Clip, bindings[0]);
        for (int i = 0; i < frames.Length; i++)
        {
            var sprite = (Sprite)frames[i].value;
            float frameEnd = i + 1 < frames.Length ? frames[i + 1].time : vfx.Clip.length;
            float midpoint = vfx.StartSeconds + (frames[i].time + frameEnd) * 0.5f;
            attack.SampleAnimation(root.gameObject, midpoint);
            Matrix4x4 weaponToRoot = root.worldToLocalMatrix * visual.localToWorldMatrix;
            Vector2 tip = weaponToRoot.MultiplyPoint3x4(presentation.BladeTip);
            Vector2 grip = weaponToRoot.MultiplyPoint3x4(presentation.GripPoint);
            string context = $"{weapon.name} {direction} frame {i}";

            float axisError = Vector2.Angle(tip - grip, axis);
            Assert.That(axisError, Is.LessThanOrEqualTo(MaxAxisErrorDegrees), $"{context}: blade axis off by {axisError:F1}°");

            float min = float.MaxValue;
            float max = float.MinValue;
            foreach (Vector2 vertex in sprite.vertices)
            {
                float along = Vector2.Dot((Vector2)vfxToRoot.MultiplyPoint3x4(vertex) - origin, axis);
                min = Mathf.Min(min, along);
                max = Mathf.Max(max, along);
            }
            // Measure the drawn pixels, not the tight mesh padding around them.
            min += padding;
            max -= padding;
            float tipAlong = Vector2.Dot(tip - origin, axis);
            float gripAlong = Vector2.Dot(grip - origin, axis);
            if (i == 0)
            {
                // The glint sits on the blade between the grip and the tip.
                Assert.That(min, Is.GreaterThanOrEqualTo(gripAlong - AlongTolerance), $"{context}: glint behind grip");
                Assert.That(max, Is.LessThanOrEqualTo(tipAlong + AlongTolerance), $"{context}: glint ahead of tip");
                continue;
            }
            float lateral = Mathf.Abs(Vector2.Dot(tip - origin, normal));
            Assert.That(lateral, Is.LessThanOrEqualTo(MaxLateralError), $"{context}: tip {lateral:F3} off the thrust axis");
            Assert.That(tipAlong, Is.InRange(min - AlongTolerance, max + AlongTolerance),
                $"{context}: tip at {tipAlong:F3} outside thrust art [{min:F3}, {max:F3}]");
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
