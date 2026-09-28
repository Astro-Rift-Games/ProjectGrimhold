using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public sealed class WeaponAttackSpriteAnimationTests
{
    private const string DefinitionsRoot = "Assets/Scriptable Objects/Loot/Definitions/";
    private const string LongBowPath = DefinitionsRoot + "LongBowWeaponDefinition.asset";
    private const string LongBowLootPath = DefinitionsRoot + "LongBow.asset";
    private const string StringingPath = DefinitionsRoot + "LongBowStringingAttackSpriteAnimation.asset";
    private const string StringingSheetPath = "Assets/Art/Weapons/Animations Spritesheets/Weapon-LongBow-Stringing.png";
    private const string PrefabPath = "Assets/Prefabs/NetworkPlayer.prefab";
    private const string AnimatorControllerPath = "Assets/Animations/Player/Character.controller";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    // The weapon-driven bake eases in for 0.1 s, so the authored draw starts there. The hands stop drawing at
    // 0.4 s, hold the full draw until 0.45 s, and the string hand releases from 0.45 s.
    private const float DrawStartSeconds = 0.1f;
    private const float FullDrawSeconds = 0.4f;
    private const float ReleaseSeconds = 0.45f;

    [Test]
    public void TryGetSprite_ShowsEachFrameOnlyInsideItsWindow()
    {
        Sprite first = CreateSprite();
        Sprite second = CreateSprite();
        WeaponAttackSpriteAnimation animation = CreateAnimation(0.2f, (first, 0.1f), (second, 0.3f));
        try
        {
            Assert.That(animation.TryValidate(out string error), Is.True, error);
            Assert.That(animation.EndSeconds, Is.EqualTo(0.6f).Within(0.00001f));
            Assert.That(animation.TryGetSprite(0.19f, out _), Is.False, "Before the start.");
            Assert.That(animation.TryGetSprite(0.2f, out Sprite sprite) && sprite == first, Is.True);
            Assert.That(animation.TryGetSprite(0.29f, out sprite) && sprite == first, Is.True);
            Assert.That(animation.TryGetSprite(0.3f, out sprite) && sprite == second, Is.True);
            Assert.That(animation.TryGetSprite(0.59f, out sprite) && sprite == second, Is.True);
            Assert.That(animation.TryGetSprite(0.6f, out _), Is.False, "After the last frame.");
            Assert.That(animation.TryGetSprite(float.NaN, out _), Is.False);
        }
        finally
        {
            Destroy(animation, first, second);
        }
    }

    [Test]
    public void TryValidate_RejectsEmptyFramesMissingSpritesAndNonPositiveDurations()
    {
        Sprite sprite = CreateSprite();
        WeaponAttackSpriteAnimation empty = CreateAnimation(0f);
        WeaponAttackSpriteAnimation missingSprite = CreateAnimation(0f, (null, 0.1f));
        WeaponAttackSpriteAnimation zeroDuration = CreateAnimation(0f, (sprite, 0f));
        WeaponAttackSpriteAnimation tooLong = CreateAnimation(0.5f, (sprite, 0.5f));
        try
        {
            Assert.That(empty.TryValidate(out _), Is.False);
            Assert.That(missingSprite.TryValidate(out _), Is.False);
            Assert.That(zeroDuration.TryValidate(out _), Is.False);
            Assert.That(tooLong.TryValidate(out string error), Is.True, error);
            DirectionalAttackAnimationSet bowSet = LongBow().Presentation.AttackAnimationSet;
            Assert.That(tooLong.TryValidateAttackSet(bowSet, out _), Is.False,
                "A sequence that outlasts the 0.9 s attack clip is rejected.");
            Assert.That(tooLong.TryValidateAttackSet(null, out _), Is.False);
        }
        finally
        {
            Destroy(empty, missingSprite, zeroDuration, tooLong, sprite);
        }
    }

    [Test]
    public void EveryWeaponExceptLongBow_HasNoAttackSpriteAnimation()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(WeaponDefinition)))
        {
            WeaponDefinition weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (AssetDatabase.GetAssetPath(weapon) == LongBowPath) continue;
            Assert.That(weapon.Presentation.AttackSpriteAnimation, Is.Null, weapon.name);
        }
    }

    [Test]
    public void LongBow_DrawsHoldsAndReleasesTheStringWithItsBakedAttack()
    {
        WeaponDefinition bow = LongBow();
        WeaponAttackSpriteAnimation stringing = bow.Presentation.AttackSpriteAnimation;
        Assert.That(stringing, Is.SameAs(AssetDatabase.LoadAssetAtPath<WeaponAttackSpriteAnimation>(StringingPath)));
        Assert.That(bow.TryValidate(out string error), Is.True, error);
        Assert.That(bow.Presentation.Rig, Is.EqualTo(WeaponRig.WeaponDriven), "The weapon-driven pose is unchanged.");
        Assert.That(bow.Presentation.AngleCorrection, Is.EqualTo(-90f));
        Assert.That(bow.Presentation.GripPoint, Is.EqualTo(new Vector2(0f, 0.125f)));

        Sprite[] frames = StringingFrames();
        Assert.That(stringing.FrameCount, Is.EqualTo(frames.Length));
        for (int i = 0; i < frames.Length; i++)
        {
            Assert.That(stringing.GetFrame(i).Sprite, Is.SameAs(frames[i]), frames[i].name);
        }

        Assert.That(stringing.StartSeconds, Is.EqualTo(DrawStartSeconds).Within(0.00001f));
        Assert.That(stringing.EndSeconds, Is.EqualTo(ReleaseSeconds).Within(0.00001f));
        AssertFrame(stringing, DrawStartSeconds, frames[0]);
        AssertFrame(stringing, 0.2f, frames[1]);
        AssertFrame(stringing, 0.3f, frames[2]);
        AssertFrame(stringing, FullDrawSeconds, frames[3]);
        AssertFrame(stringing, ReleaseSeconds - 0.001f, frames[3]);
        Assert.That(stringing.TryGetSprite(ReleaseSeconds, out _), Is.False,
            "The release returns the string to rest: the world sprite.");
    }

    [Test]
    public void LongBowStringingFrames_ShareTheWorldSpriteFrameAndGrip()
    {
        Sprite world = AssetDatabase.LoadAssetAtPath<LootDefinition>(LongBowLootPath).WorldSprite;
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        var worldTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            Assert.That(texture.LoadImage(System.IO.File.ReadAllBytes(StringingSheetPath)), Is.True);
            Assert.That(worldTexture.LoadImage(System.IO.File.ReadAllBytes(AssetDatabase.GetAssetPath(world))), Is.True);
            Vector2 grip = LongBow().Presentation.GripPoint;
            Sprite[] frames = StringingFrames();
            foreach (Sprite frame in frames)
            {
                Assert.That(frame.pixelsPerUnit, Is.EqualTo(world.pixelsPerUnit), frame.name);
                Assert.That(frame.texture.filterMode, Is.EqualTo(FilterMode.Point), frame.name);
                // Like the world sprite, the limb spans the frame and the handle is the center of the limb's
                // three-row run at the top of the center column, so the same grip point holds every frame.
                Rect rect = frame.rect;
                Assert.That((int)rect.width % 2, Is.EqualTo(1), frame.name);
                int column = (int)rect.x + (int)rect.width / 2;
                int top = (int)rect.yMax - 1;
                Assert.That(texture.GetPixel(column, top).a, Is.GreaterThan(0f), frame.name);
                Assert.That(texture.GetPixel(column, top - 2).a, Is.GreaterThan(0f), frame.name);
                Assert.That(texture.GetPixel(column, top - 3).a, Is.EqualTo(0f), frame.name);
                Vector2 handle = new Vector2(column + 0.5f - rect.x, top - 1 + 0.5f - rect.y);
                Vector2 expected = (handle - frame.pivot) / frame.pixelsPerUnit;
                Assert.That(Vector2.Distance(grip, expected), Is.LessThan(0.0001f), frame.name);
            }

            // The rest frame is the world sprite itself, so leaving the sequence never pops.
            Rect rest = frames[0].rect;
            Assert.That(rest.size, Is.EqualTo(world.rect.size));
            Assert.That(frames[0].pivot, Is.EqualTo(world.pivot));
            for (int y = 0; y < (int)rest.height; y++)
            for (int x = 0; x < (int)rest.width; x++)
            {
                Assert.That(texture.GetPixel((int)rest.x + x, (int)rest.y + y),
                    Is.EqualTo(worldTexture.GetPixel((int)world.rect.x + x, (int)world.rect.y + y)), $"{x},{y}");
            }
        }
        finally
        {
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(worldTexture);
        }
    }

    [Test]
    public void ResolveMainHandSprite_UsesAFrameOnlyDuringTheConfiguredAttackWindow()
    {
        Sprite world = CreateSprite();
        Sprite frame = CreateSprite();
        WeaponAttackSpriteAnimation animation = CreateAnimation(0.1f, (frame, 0.2f));
        try
        {
            Assert.That(PlayerWeaponPresentationMath.ResolveMainHandSprite(world, null, true, 0.15f), Is.SameAs(world),
                "A weapon without an attack sprite animation keeps its world sprite.");
            Assert.That(PlayerWeaponPresentationMath.ResolveMainHandSprite(world, animation, false, 0.15f), Is.SameAs(world),
                "Outside its confirmed attack the weapon keeps its world sprite.");
            Assert.That(PlayerWeaponPresentationMath.ResolveMainHandSprite(world, animation, true, 0.15f), Is.SameAs(frame));
            Assert.That(PlayerWeaponPresentationMath.ResolveMainHandSprite(world, animation, true, 0.05f), Is.SameAs(world));
            Assert.That(PlayerWeaponPresentationMath.ResolveMainHandSprite(world, animation, true, 0.3f), Is.SameAs(world));
            Assert.That(PlayerWeaponPresentationMath.ResolveMainHandSprite(null, animation, true, 0.15f), Is.Null,
                "An unarmed hand stays empty.");
        }
        finally
        {
            Destroy(animation, world, frame);
        }
    }

    [TestCase(0f, false)]
    [TestCase(0.05f, false)]
    [TestCase(0.1f, true)]
    [TestCase(0.3f, true)]
    [TestCase(0.44f, true)]
    [TestCase(0.46f, false)]
    [TestCase(0.85f, false)]
    public void Presenter_SwapsOnlyTheSpriteInSyncWithTheConfirmedAttackClip(float attackSeconds, bool animated)
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            PlayerWeaponPresenter presenter = contents.GetComponentInChildren<PlayerWeaponPresenter>(true);
            var serialized = new SerializedObject(presenter);
            var view = (PlayerAnimatorView)serialized.FindProperty("_animatorView").objectReferenceValue;
            var renderer = (SpriteRenderer)serialized.FindProperty("_mainHandRenderer").objectReferenceValue;
            var visual = (Transform)serialized.FindProperty("_mainHandWeaponVisual").objectReferenceValue;
            Animator animator = view.GetComponent<Animator>();
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(AnimatorControllerPath);
            LootDefinition bow = AssetDatabase.LoadAssetAtPath<LootDefinition>(LongBowLootPath);
            PinAttack(view, animator, bow);

            typeof(PlayerWeaponPresenter).GetMethod("CaptureBaseState", Private).Invoke(presenter, null);
            typeof(PlayerWeaponPresenter).GetMethod("ApplyMainHandDefinition", Private).Invoke(presenter, new object[] { bow });
            Vector3 position = visual.localPosition;
            Quaternion rotation = visual.localRotation;
            Vector3 scale = visual.localScale;

            int layer = animator.GetLayerIndex("RightHand");
            animator.Play("Attack", layer, attackSeconds / bow.WeaponDefinition.Presentation.GetAttackClip(3).length);
            animator.Update(0f);
            Assert.That(view.TryGetPresentedAttackSeconds(out float seconds), Is.True);
            Assert.That(seconds, Is.EqualTo(attackSeconds).Within(0.0001f));

            typeof(PlayerWeaponPresenter).GetMethod("RefreshMainHandSprite", Private).Invoke(presenter, null);
            WeaponAttackSpriteAnimation stringing = bow.WeaponDefinition.Presentation.AttackSpriteAnimation;
            Sprite expected = animated && stringing.TryGetSprite(attackSeconds, out Sprite frame) ? frame : bow.WorldSprite;
            Assert.That(renderer.sprite, Is.SameAs(expected));
            Assert.That(renderer.sprite != bow.WorldSprite, Is.EqualTo(animated));
            Assert.That(visual.localPosition, Is.EqualTo(position), "The sprite swap never moves the weapon.");
            Assert.That(visual.localRotation, Is.EqualTo(rotation), "The sprite swap never turns the weapon.");
            Assert.That(visual.localScale, Is.EqualTo(scale), "The sprite swap never mirrors the weapon.");

            animator.Play("RightHand-Idle", layer, 0f);
            animator.Update(0f);
            Assert.That(view.TryGetPresentedAttackSeconds(out _), Is.False, "Idle is outside the attack window.");
            typeof(PlayerWeaponPresenter).GetMethod("RefreshMainHandSprite", Private).Invoke(presenter, null);
            Assert.That(renderer.sprite, Is.SameAs(bow.WorldSprite));
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    [Test]
    public void PresentedAttackSeconds_IgnoresClipsThatDoNotBelongToThePinnedWeapon()
    {
        var gameObject = new GameObject(nameof(PresentedAttackSeconds_IgnoresClipsThatDoNotBelongToThePinnedWeapon));
        try
        {
            Animator animator = gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(AnimatorControllerPath);
            PlayerAnimatorView view = gameObject.AddComponent<PlayerAnimatorView>();
            LootDefinition bow = AssetDatabase.LoadAssetAtPath<LootDefinition>(LongBowLootPath);
            LootDefinition sword = AssetDatabase.LoadAssetAtPath<LootDefinition>(DefinitionsRoot + "ArmingSword.asset");
            Assert.That(sword, Is.Not.Null);
            PinAttack(view, animator, sword);
            // The sword's clips play, but the pinned identity says Long Bow: its stringing must not play.
            typeof(PlayerAnimatorView).GetField("_confirmedAttackWeapon", Private).SetValue(view, bow);
            animator.Play("Attack", animator.GetLayerIndex("RightHand"), 0.3f);
            animator.Update(0f);
            Assert.That(view.TryGetPresentedAttackSeconds(out _), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
        }
    }

    [TestCase("Assets/Scripts/Player/Presentation/PlayerWeaponPresenter.cs")]
    [TestCase("Assets/Scripts/Player/Presentation/PlayerWeaponPresentationMath.cs")]
    [TestCase("Assets/Scripts/Player/Presentation/PlayerAnimatorView.cs")]
    [TestCase("Assets/Scripts/Combat/WeaponAttackSpriteAnimation.cs")]
    public void Runtime_DoesNotDependOnWeaponIdentity(string scriptPath)
    {
        string source = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath).text;
        foreach (string term in new[] { "LongBow", "Long Bow", "long_bow", "Bow", "Stringing", "LootId" })
        {
            Assert.That(source, Does.Not.Contain(term), term);
        }
    }

    private static void PinAttack(PlayerAnimatorView view, Animator animator, LootDefinition weapon)
    {
        typeof(PlayerAnimatorView).GetField("_confirmedAttackWeapon", Private).SetValue(view, weapon);
        typeof(PlayerAnimatorView).GetField("_attackWeaponPinned", Private).SetValue(view, true);
        typeof(PlayerAnimatorView).GetMethod("RefreshAttackOverrides", Private)
            .Invoke(view, new object[] { weapon.WeaponDefinition });
        typeof(PlayerAnimatorView).GetField("_mainHandCombatLayerIndex", Private)
            .SetValue(view, animator.GetLayerIndex("RightHand"));
    }

    private static void AssertFrame(WeaponAttackSpriteAnimation animation, float seconds, Sprite expected)
    {
        Assert.That(animation.TryGetSprite(seconds, out Sprite sprite), Is.True, $"{seconds}s");
        Assert.That(sprite, Is.SameAs(expected), $"{seconds}s");
    }

    private static WeaponDefinition LongBow() => AssetDatabase.LoadAssetAtPath<WeaponDefinition>(LongBowPath);

    private static Sprite[] StringingFrames()
    {
        var frames = new Sprite[4];
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(StringingSheetPath))
        {
            if (asset is Sprite sprite)
            {
                frames[int.Parse(sprite.name.Substring(sprite.name.LastIndexOf('_') + 1))] = sprite;
            }
        }
        Assert.That(frames, Has.None.Null);
        return frames;
    }

    private static Sprite CreateSprite()
    {
        var texture = new Texture2D(1, 1);
        return Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), Vector2.one * 0.5f);
    }

    private static WeaponAttackSpriteAnimation CreateAnimation(
        float startSeconds,
        params (Sprite sprite, float duration)[] frames)
    {
        var animation = ScriptableObject.CreateInstance<WeaponAttackSpriteAnimation>();
        var serialized = new SerializedObject(animation);
        serialized.FindProperty("_startSeconds").floatValue = startSeconds;
        SerializedProperty array = serialized.FindProperty("_frames");
        array.arraySize = frames.Length;
        for (int i = 0; i < frames.Length; i++)
        {
            SerializedProperty frame = array.GetArrayElementAtIndex(i);
            frame.FindPropertyRelative("_sprite").objectReferenceValue = frames[i].sprite;
            frame.FindPropertyRelative("_durationSeconds").floatValue = frames[i].duration;
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return animation;
    }

    private static void Destroy(params Object[] objects)
    {
        foreach (Object value in objects)
        {
            if (value is Sprite sprite && sprite.texture != null) Object.DestroyImmediate(sprite.texture);
            if (value != null) Object.DestroyImmediate(value);
        }
    }
}
