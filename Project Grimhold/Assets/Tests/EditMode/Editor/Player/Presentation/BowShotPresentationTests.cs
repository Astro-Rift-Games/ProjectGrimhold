using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class BowShotPresentationTests
{
    private const string DefinitionsRoot = "Assets/Scriptable Objects/Loot/Definitions/";
    private const string PrefabPath = "Assets/Prefabs/NetworkPlayer.prefab";
    private const string ControllerPath = "Assets/Animations/Player/Character.controller";
    private const string BowShotTexturePath = "Assets/Art/VFX/VFX-BowShot.png";
    private const string BowShotClipPath = "Assets/Art/VFX/BowShotVfx.anim";
    private const string BowShotVisualPath = DefinitionsRoot + "BowShotVfxVisual.asset";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    private static readonly CharacterVisualDirection[] Directions =
    {
        CharacterVisualDirection.North, CharacterVisualDirection.NorthEast, CharacterVisualDirection.NorthWest,
        CharacterVisualDirection.South, CharacterVisualDirection.SouthEast, CharacterVisualDirection.SouthWest
    };

    [Test]
    public void BowShotSheet_IsFourCenteredPixelArtCellsWithTheShotOriginSharedByEveryFrame()
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(BowShotTexturePath);
        Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Multiple));
        Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(16f));
        Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
        Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
        Sprite[] frames = Frames();
        for (int i = 0; i < frames.Length; i++)
        {
            Assert.That(frames[i].rect, Is.EqualTo(new Rect(i * 96f, 0f, 96f, 96f)), frames[i].name);
            Assert.That(frames[i].pivot, Is.EqualTo(new Vector2(48f, 48f)), frames[i].name);
        }

        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            Assert.That(texture.LoadImage(System.IO.File.ReadAllBytes(BowShotTexturePath)), Is.True);
            // The first frame is the release flash: its opaque bounds center is the shot origin.
            RectInt flash = OpaqueBounds(texture, 0);
            Vector2 origin = new Vector2(flash.x + flash.width * 0.5f, flash.y + flash.height * 0.5f);
            Vector2 expected = (origin - new Vector2(48f, 48f)) / 16f;
            Assert.That(BowShot().Origin, Is.EqualTo(expected), "Origin is the flash center in sprite local units.");
            for (int i = 1; i < frames.Length; i++)
            {
                RectInt bounds = OpaqueBounds(texture, i);
                Assert.That(bounds.x - i * 96, Is.GreaterThanOrEqualTo((int)origin.x),
                    $"Frame {i} travels forward along +X from the origin.");
                Assert.That(bounds.y + bounds.height * 0.5f, Is.EqualTo(origin.y).Within(0.5f),
                    $"Frame {i} stays centered on the shot axis.");
            }
        }
        finally
        {
            Object.DestroyImmediate(texture);
        }
    }

    [Test]
    public void BowShotClip_PlaysTheFourFramesOnceOnTheAttackVfxRenderer()
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(BowShotClipPath);
        Assert.That(BowShot().Clip, Is.SameAs(clip));
        Assert.That(clip.isLooping, Is.False);
        Assert.That(clip.length, Is.EqualTo(0.2f).Within(0.0001f));
        EditorCurveBinding binding = AnimationUtility.GetObjectReferenceCurveBindings(clip).Single();
        Assert.That(binding.path, Is.EqualTo("AttackVfx"));
        Assert.That(binding.type, Is.EqualTo(typeof(SpriteRenderer)));
        ObjectReferenceKeyframe[] keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
        Sprite[] frames = Frames();
        Assert.That(keys.Length, Is.EqualTo(frames.Length));
        for (int i = 0; i < keys.Length; i++)
        {
            Assert.That(keys[i].time, Is.EqualTo(i * 0.05f).Within(0.0001f));
            Assert.That(keys[i].value, Is.SameAs(frames[i]));
        }
    }

    [Test]
    public void OnlyTheBowShotVisual_IsIndependentFromWeaponReach()
    {
        Assert.That(BowShot().UsesWeaponReach, Is.False);
        foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(AttackVfxVisualDefinition)))
        {
            var visual = AssetDatabase.LoadAssetAtPath<AttackVfxVisualDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            Assert.That(visual.TryValidate(out string error), Is.True, error);
            if (visual is BowShotVfxVisualDefinition) continue;
            Assert.That(visual.UsesWeaponReach, Is.True, visual.name);
        }
    }

    [Test]
    public void ReachBasedAttackVfx_StillResolveFromReachOffsetPlusBladeReach()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(AttackVfxDefinition)))
        {
            var vfx = AssetDatabase.LoadAssetAtPath<AttackVfxDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (!vfx.UsesWeaponReach) continue;
            for (int i = 0; i < 6; i++)
            {
                AttackVfxDefinition.DirectionalPose source = vfx.GetPose(i);
                Assert.That(vfx.TryResolvePose(i, 1.25f, out AttackVfxDefinition.ResolvedPose actual), Is.True);
                Assert.That(vfx.Visual.TryResolvePose(source, source.ReachOffset + 1.25f,
                    out AttackVfxDefinition.ResolvedPose expected), Is.True);
                Assert.That(actual.Position, Is.EqualTo(expected.Position), vfx.name);
                Assert.That(actual.Rotation, Is.EqualTo(expected.Rotation), vfx.name);
                Assert.That(actual.Scale, Is.EqualTo(expected.Scale), vfx.name);
            }
        }
    }

    [TestCase("LongBow")]
    [TestCase("CompoundBow")]
    public void Bow_UsesItsOwnBowShotProfileOverTheSharedVisualWithoutABlade(string bowName)
    {
        WeaponDefinition bow = Bow(bowName);
        AttackVfxDefinition vfx = bow.Presentation.AttackVfx;
        Assert.That(vfx, Is.SameAs(AssetDatabase.LoadAssetAtPath<AttackVfxDefinition>(
            DefinitionsRoot + bowName + "BowShotAttackVfx.asset")));
        Assert.That(vfx.Visual, Is.SameAs(BowShot()), "Both bows share one Bow Shot visual.");
        Assert.That(bow.Presentation.BladeTip, Is.EqualTo(Vector2.zero), "No blade tip is configured for the bow.");
        Assert.That(bow.TryValidate(out string error), Is.True, error);
        for (int i = 0; i < 6; i++)
        {
            Assert.That(vfx.TryResolvePose(i, 0f, out AttackVfxDefinition.ResolvedPose withoutReach), Is.True);
            Assert.That(vfx.TryResolvePose(i, 7f, out AttackVfxDefinition.ResolvedPose withReach), Is.True);
            Assert.That(withReach.Position, Is.EqualTo(withoutReach.Position), "Blade reach never moves the shot.");
            Assert.That(withReach.Scale, Is.EqualTo(withoutReach.Scale), "The shot is never resized.");
            Assert.That(Mathf.Abs(withoutReach.Scale.x), Is.EqualTo(1f));
            Assert.That(Mathf.Abs(withoutReach.Scale.y), Is.EqualTo(1f));
        }
    }

    [Test]
    public void Bows_KeepDistinctBowShotProfiles()
    {
        Assert.That(Bow("CompoundBow").Presentation.AttackVfx, Is.Not.SameAs(Bow("LongBow").Presentation.AttackVfx),
            "Each bow aligns the shared visual with its own attack.");
    }

    [TestCase("LongBow", 0.45f)]
    [TestCase("CompoundBow", 0.4f)]
    public void BowShot_StartsOnTheStringingRelease(string bowName, float releaseSeconds)
    {
        WeaponDefinition bow = Bow(bowName);
        AttackVfxDefinition vfx = bow.Presentation.AttackVfx;
        // The stringing owns the release: its last frame ends when the string hand lets go.
        Assert.That(vfx.StartSeconds, Is.EqualTo(bow.Presentation.AttackSpriteAnimation.EndSeconds).Within(0.0001f));
        Assert.That(vfx.StartSeconds, Is.EqualTo(releaseSeconds).Within(0.0001f));
        for (int i = 0; i < 6; i++)
        {
            Assert.That(vfx.StartSeconds + vfx.Clip.length,
                Is.LessThanOrEqualTo(bow.Presentation.GetAttackClip(i).length), $"Clip {i}");
        }
    }

    [TestCase("LongBow")]
    [TestCase("CompoundBow")]
    public void BowShot_LeavesTheBowFrontOnItsShootingAxisInEveryFacing(string bowName)
    {
        WeaponDefinition bow = Bow(bowName);
        LootDefinition loot = AssetDatabase.LoadAssetAtPath<LootDefinition>(DefinitionsRoot + bowName + ".asset");
        AttackVfxDefinition vfx = bow.Presentation.AttackVfx;
        float bowFront = BowFrontFromGrip(loot.WorldSprite, bow.Presentation.GripPoint);
        Assert.That(bowFront, Is.EqualTo(0.09375f), "The limb's front edge is 1.5 px ahead of the grip.");

        GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            PlayerWeaponPresenter weaponPresenter = contents.GetComponentInChildren<PlayerWeaponPresenter>(true);
            var serialized = new SerializedObject(weaponPresenter);
            var view = (PlayerAnimatorView)serialized.FindProperty("_animatorView").objectReferenceValue;
            var pivot = (Transform)serialized.FindProperty("_mainHandWeaponPivot").objectReferenceValue;
            Animator animator = view.GetComponent<Animator>();
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            Pin(view, animator, loot);
            typeof(PlayerWeaponPresenter).GetMethod("CaptureBaseState", Private).Invoke(weaponPresenter, null);
            typeof(PlayerWeaponPresenter).GetMethod("ApplyMainHandDefinition", Private)
                .Invoke(weaponPresenter, new object[] { loot });
            MethodInfo applyFacing = typeof(CharacterAnimatorView).GetMethod("ApplyFacingParameters", Private);
            int layer = animator.GetLayerIndex("RightHand");

            for (int i = 0; i < Directions.Length; i++)
            {
                Vector2 facing = CharacterVisualDirectionResolver.GetCanonicalVector(Directions[i]);
                applyFacing.Invoke(view, new object[] { facing, false });
                animator.Play("Attack", layer, vfx.StartSeconds / bow.Presentation.GetAttackClip(i).length);
                animator.Update(0f);
                typeof(PlayerWeaponPresenter).GetMethod("RefreshPose", Private).Invoke(weaponPresenter, null);

                // The bow's pivot is its grip on WeaponPose, and its +X is the shooting axis.
                Vector3 grip = animator.transform.InverseTransformPoint(pivot.position);
                Vector3 axis = animator.transform.InverseTransformDirection(pivot.right);
                AttackVfxDefinition.DirectionalPose pose = vfx.GetPose(i);
                string label = Directions[i].ToString();
                Assert.That(Vector2.Distance(pose.Position, grip), Is.LessThan(0.001f), label);
                Assert.That(Vector3.Angle(pose.Rotation * Vector3.right, axis), Is.LessThan(0.01f), label);
                Assert.That(Vector2.Dot(axis, facing), Is.EqualTo(1f).Within(0.0001f), label);
                Assert.That(pose.ReachOffset, Is.EqualTo(bowFront), label);
                Assert.That(pose.Mirrored, Is.EqualTo(PlayerWeaponPresentationMath.ShouldMirror(facing)), label);
                Assert.That(pose.SortingOrder, Is.EqualTo(
                    CharacterVisualDirectionResolver.CalculateSortingOrder(Directions[i], 21, -9)), label);

                // The resolved art puts its origin on the bow front and points it along the shot.
                Assert.That(vfx.TryResolvePose(i, bow.Presentation.BladeReach,
                    out AttackVfxDefinition.ResolvedPose resolved), Is.True);
                Vector3 origin = resolved.Position + resolved.Rotation *
                    Vector3.Scale(BowShot().Origin, resolved.Scale);
                Assert.That(Vector2.Distance(origin, grip + axis * bowFront), Is.LessThan(0.001f), label);
                Assert.That(Vector3.Angle(resolved.Rotation * Vector3.right, axis), Is.LessThan(0.01f), label);
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    [TestCase("LongBow")]
    [TestCase("CompoundBow")]
    public void ConfirmedBowAttack_PlaysTheShotFromReleaseAndClearsWithoutRereadingEquipment(string bowName)
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject player = null;
        AnimatorOverrideController overrides = null;
        try
        {
            player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), scene);
            Animator animator = player.GetComponentInChildren<Animator>(true);
            var presenter = player.GetComponentInChildren<PlayerAttackVfxPresenter>(true);
            var renderer = (SpriteRenderer)new SerializedObject(presenter).FindProperty("_vfxRenderer").objectReferenceValue;
            WeaponDefinition bow = Bow(bowName);
            float release = bow.Presentation.AttackVfx.StartSeconds;
            float end = release + bow.Presentation.AttackVfx.Clip.length;
            overrides =new AnimatorOverrideController(AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath));
            overrides[AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Player/Attack/GenericAttack_S.anim")] =
                bow.Presentation.GetAttackClip(3);
            animator.runtimeAnimatorController = overrides;
            animator.SetFloat("MoveX", 0f);
            animator.SetFloat("MoveY", -1f);
            animator.SetBool("IsMoving", false);
            animator.SetBool("HasGenericAttack", true);
            animator.SetInteger("WeaponAnimationCategory", (int)bow.Presentation.AnimationCategory);
            typeof(PlayerAttackVfxPresenter).GetMethod("OnEnable", Private).Invoke(presenter, null);
            animator.SetTrigger("OnAttack");

            var equipment = player.GetComponent<PlayerWeaponEquipmentNetworkController>();
            var catalog = (LootDefinitionCatalog)new SerializedObject(equipment).FindProperty("_lootCatalog").objectReferenceValue;
            Assert.That(catalog.TryGetIndex(AssetDatabase.LoadAssetAtPath<LootDefinition>(DefinitionsRoot + bowName + ".asset").LootId,
                out int index), Is.True);
            var attack = new AttackPerformedEvent(default, AttackType.Ranged, Vector2.zero, Vector2.down, 0, index + 1);
            typeof(PlayerAttackVfxPresenter).GetMethod("OnAttackPerformed", Private).Invoke(presenter, new object[] { attack });
            AttackVfxDefinition confirmed = bow.Presentation.AttackVfx;
            Assert.That(typeof(PlayerAttackVfxPresenter).GetField("_vfx", Private).GetValue(presenter), Is.SameAs(confirmed));

            // Switching weapons mid-attack cannot reach the confirmed effect: the presenter resolved it from the
            // attack event and never looks the weapon up again.
            typeof(PlayerWeaponEquipmentNetworkController).GetField("_lootCatalog", Private).SetValue(equipment, null);

            MethodInfo update = typeof(PlayerAttackVfxPresenter).GetMethod("LateUpdate", Private);
            int layer = animator.GetLayerIndex("RightHand");
            animator.Update(0f);
            for (int step = 1; step <= 14; step++)
            {
                animator.Update(0.05f);
                update.Invoke(presenter, null);
                AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(layer);
                Assert.That(state.IsTag("Attack"), Is.True);
                float seconds = step * 0.05f;
                Assert.That(state.normalizedTime * state.length, Is.EqualTo(seconds).Within(0.01f));
                if (seconds < release - 0.001f)
                {
                    Assert.That(renderer.enabled, Is.False, $"{seconds}s is before the release");
                }
                else if (seconds < end - 0.001f)
                {
                    int frame = Mathf.RoundToInt((seconds - release) / 0.05f);
                    Assert.That(renderer.enabled, Is.True, $"{seconds}s");
                    Assert.That(renderer.sprite.name, Is.EqualTo($"VFX-BowShot_{frame}"), $"{seconds}s");
                    Assert.That(renderer.sortingOrder, Is.EqualTo(21), "South draws over the bow.");
                }
                else
                {
                    Assert.That(renderer.enabled, Is.False, $"{seconds}s is after the shot");
                    Assert.That(renderer.sprite, Is.Null);
                }
            }
        }
        finally
        {
            if (player != null) Object.DestroyImmediate(player);
            if (overrides != null) Object.DestroyImmediate(overrides);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }

    private static WeaponDefinition Bow(string bowName) =>
        AssetDatabase.LoadAssetAtPath<WeaponDefinition>(DefinitionsRoot + bowName + "WeaponDefinition.asset");

    private static BowShotVfxVisualDefinition BowShot() =>
        AssetDatabase.LoadAssetAtPath<BowShotVfxVisualDefinition>(BowShotVisualPath);

    private static Sprite[] Frames()
    {
        Sprite[] frames = AssetDatabase.LoadAllAssetsAtPath(BowShotTexturePath).OfType<Sprite>()
            .OrderBy(sprite => sprite.name).ToArray();
        Assert.That(frames.Select(sprite => sprite.name),
            Is.EqualTo(new[] { "VFX-BowShot_0", "VFX-BowShot_1", "VFX-BowShot_2", "VFX-BowShot_3" }));
        return frames;
    }

    // Opaque bounds of one 96 px cell, in texture pixels (Unity's bottom-up space).
    private static RectInt OpaqueBounds(Texture2D texture, int cell)
    {
        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        for (int y = 0; y < texture.height; y++)
        for (int x = cell * 96; x < (cell + 1) * 96; x++)
        {
            if (texture.GetPixel(x, y).a == 0f) continue;
            minX = Mathf.Min(minX, x);
            minY = Mathf.Min(minY, y);
            maxX = Mathf.Max(maxX, x);
            maxY = Mathf.Max(maxY, y);
        }
        Assert.That(maxX, Is.GreaterThanOrEqualTo(minX), $"Cell {cell} has art.");
        return new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    // The bow shoots along sprite +Y; its front is the top edge of the limb in the center column.
    private static float BowFrontFromGrip(Sprite sprite, Vector2 grip)
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            Assert.That(texture.LoadImage(System.IO.File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite.texture))), Is.True);
            Rect rect = sprite.rect;
            int column = (int)rect.x + (int)rect.width / 2;
            int top = (int)rect.yMax - 1;
            while (top >= rect.y && texture.GetPixel(column, top).a == 0f) top--;
            float frontEdge = (top + 1 - rect.y - sprite.pivot.y) / sprite.pixelsPerUnit;
            return frontEdge - grip.y;
        }
        finally
        {
            Object.DestroyImmediate(texture);
        }
    }

    private static void Pin(PlayerAnimatorView view, Animator animator, LootDefinition weapon)
    {
        typeof(PlayerAnimatorView).GetField("_confirmedAttackWeapon", Private).SetValue(view, weapon);
        typeof(PlayerAnimatorView).GetField("_attackWeaponPinned", Private).SetValue(view, true);
        typeof(PlayerAnimatorView).GetMethod("RefreshAttackOverrides", Private)
            .Invoke(view, new object[] { weapon.WeaponDefinition });
        typeof(PlayerAnimatorView).GetField("_mainHandCombatLayerIndex", Private)
            .SetValue(view, animator.GetLayerIndex("RightHand"));
    }
}
