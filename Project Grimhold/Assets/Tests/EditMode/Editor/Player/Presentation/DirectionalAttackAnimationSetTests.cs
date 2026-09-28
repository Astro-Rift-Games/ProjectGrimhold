using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class DirectionalAttackAnimationSetTests
{
    private const string Root = "Assets/Scriptable Objects/Loot/AttackAnimationSets/";
    private static readonly string[] Directions = { "N", "NE", "NW", "S", "SE", "SW" };

    [TestCase("Sword1H", "ArmingSword")]
    [TestCase("Rapier", "Rapier")]
    [TestCase("Dagger", "RondelDagger")]
    [TestCase("Wand", "MagicWand")]
    [TestCase("MagicSword", "MagicSword")]
    [TestCase("LongSword", "LongSword")]
    [TestCase("Zweihander", "Zweihander")]
    [TestCase("MagicStaff", "MagicStaff")]
    [TestCase("LongBow", "LongBow")]
    public void Set_HasExactlySixMappedClipsAndIsComplete(string setName, string sourceName)
    {
        DirectionalAttackAnimationSet set = AssetDatabase.LoadAssetAtPath<DirectionalAttackAnimationSet>(Root + setName + ".asset");
        Assert.That(set, Is.Not.Null);
        Assert.That(set.IsComplete, Is.True);
        Assert.That(set.TryValidate(out string error), Is.True, error);
        SerializedObject serialized = new SerializedObject(set);
        Assert.That(serialized.GetIterator(), Is.Not.Null);
        Assert.That(Directions.Select(direction => serialized.FindProperty("_attack" + direction)).All(property => property != null), Is.True);
        for (int index = 0; index < Directions.Length; index++)
            Assert.That(set.GetAttackClip(index), Is.SameAs(AssetDatabase.LoadAssetAtPath<AnimationClip>(
                $"Assets/Animations/Weapons/Directional/{sourceName}/{sourceName}_Attack_{Directions[index]}.anim")));
        Assert.That(set.GetAttackClip(-1), Is.Null);
        Assert.That(set.GetAttackClip(6), Is.Null);
    }

    [Test]
    public void LongSword_IsTwoHandedGenericAttack()
    {
        WeaponDefinition longSword = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(
            "Assets/Scriptable Objects/Loot/Definitions/LongSwordCombatDefinition.asset");
        Assert.That(longSword.Handedness, Is.EqualTo(WeaponHandedness.TwoHanded));
        Assert.That(longSword.Presentation.AnimationCategory, Is.EqualTo(WeaponAnimationCategory.None));
        Assert.That(longSword.Presentation.HasGenericAttack, Is.True);
        Assert.That(longSword.Presentation.AttackAnimationSet, Is.SameAs(
            AssetDatabase.LoadAssetAtPath<DirectionalAttackAnimationSet>(Root + "LongSword.asset")));
        // LongSword.png: the main hand grips the handle row under the guard, the second hand its last row.
        Assert.That(longSword.Presentation.GripPoint, Is.EqualTo(new Vector2(0f, -0.46875f)));
        Assert.That(longSword.Presentation.SecondaryGripPoint, Is.EqualTo(new Vector2(0f, -0.65625f)));
        Assert.That(longSword.Presentation.SecondHand, Is.EqualTo(SecondHandPresentation.HoldsSecondaryGrip));
        Assert.That(longSword.TryValidate(out string validationError), Is.True, validationError);
    }

    [Test]
    public void Zweihander_IsTwoHandedGenericAttackWithItsOwnHandleGeometry()
    {
        WeaponDefinition zweihander = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(
            "Assets/Scriptable Objects/Loot/Definitions/ZweihanderWeaponDefinition.asset");
        Assert.That(zweihander.Handedness, Is.EqualTo(WeaponHandedness.TwoHanded));
        Assert.That(zweihander.Presentation.AnimationCategory, Is.EqualTo(WeaponAnimationCategory.None));
        Assert.That(zweihander.Presentation.HasGenericAttack, Is.True);
        Assert.That(zweihander.Presentation.AttackAnimationSet, Is.SameAs(
            AssetDatabase.LoadAssetAtPath<DirectionalAttackAnimationSet>(Root + "Zweihander.asset")));
        // Zweihander.png is 33 px tall with a centered pivot and a six-row handle between guard and pommel:
        // the main hand grips the handle row under the guard, the second hand its last row.
        Assert.That(zweihander.Presentation.GripPoint, Is.EqualTo(new Vector2(0f, -0.4375f)));
        Assert.That(zweihander.Presentation.SecondaryGripPoint, Is.EqualTo(new Vector2(0f, -0.75f)));
        Assert.That(zweihander.Presentation.SecondHand, Is.EqualTo(SecondHandPresentation.HoldsSecondaryGrip));
        Assert.That(zweihander.TryValidate(out string validationError), Is.True, validationError);
    }

    [Test]
    public void MagicStaff_IsTwoHandedGenericAttackWithAuthoredSecondHand()
    {
        WeaponDefinition staff = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(
            "Assets/Scriptable Objects/Loot/Definitions/MagicStaffWeaponDefinition.asset");
        Assert.That(staff.Handedness, Is.EqualTo(WeaponHandedness.TwoHanded));
        Assert.That(staff.Presentation.AnimationCategory, Is.EqualTo(WeaponAnimationCategory.None));
        Assert.That(staff.Presentation.HasGenericAttack, Is.True);
        Assert.That(staff.Presentation.AttackAnimationSet, Is.SameAs(
            AssetDatabase.LoadAssetAtPath<DirectionalAttackAnimationSet>(Root + "MagicStaff.asset")));
        // MagicStaff.png is 24 px tall with a centered pivot: the main hand grips 8 px below the center.
        // The authored left hand gestures on its own side instead of holding the staff, so the staff
        // needs no secondary grip point.
        Assert.That(staff.Presentation.GripPoint, Is.EqualTo(new Vector2(0f, -0.5f)));
        Assert.That(staff.Presentation.SecondHand, Is.EqualTo(SecondHandPresentation.FollowsAuthoredMotion));
        Assert.That(staff.Presentation.AttackVfx, Is.Not.Null);
        Assert.That(staff.TryValidate(out string validationError), Is.True, validationError);
    }

    [Test]
    public void LongBow_IsTwoHandedGenericAttackDrivenByItsOwnPose()
    {
        WeaponDefinition bow = LongBow();
        Assert.That(bow.Handedness, Is.EqualTo(WeaponHandedness.TwoHanded));
        Assert.That(bow.Presentation.AnimationCategory, Is.EqualTo(WeaponAnimationCategory.None));
        Assert.That(bow.Presentation.HasGenericAttack, Is.True);
        Assert.That(bow.Presentation.AttackAnimationSet, Is.SameAs(
            AssetDatabase.LoadAssetAtPath<DirectionalAttackAnimationSet>(Root + "LongBow.asset")));
        // LongBow.png shoots along sprite +Y, which a -90 degree correction aligns with the presenter's
        // facing axis (+X). Its grip point is derived from the art in LongBow_GripPointIsTheCenterOfTheHandle.
        Assert.That(bow.Presentation.AngleCorrection, Is.EqualTo(-90f));
        // The bow owns its pose: its visual follows WeaponPose, and the baked attack places the bow arm on its
        // grip and the drawing hand on its string target.
        Assert.That(bow.Presentation.Rig, Is.EqualTo(WeaponRig.WeaponDriven));
        Assert.That(bow.Presentation.SecondHand, Is.EqualTo(SecondHandPresentation.FollowsAuthoredMotion));
        // The Bow Shot is placed from the bow's own shooting axis, not from a blade reach.
        Assert.That(bow.Presentation.AttackVfx, Is.SameAs(AssetDatabase.LoadAssetAtPath<AttackVfxDefinition>(
            "Assets/Scriptable Objects/Loot/Definitions/LongBowBowShotAttackVfx.asset")));
        Assert.That(bow.Presentation.AttackVfx.UsesWeaponReach, Is.False);
        Assert.That(bow.TryValidate(out string validationError), Is.True, validationError);
    }

    [Test]
    public void LongBow_GripPointIsTheCenterOfTheHandle()
    {
        // The bow's handle is the middle of its limb: in the sprite's center column, the first opaque run from
        // the top is the limb (outline, wood, outline) and the last one is the string. The grip is the center of
        // the limb run, measured from the sprite pivot in Unity's bottom-up pixel space.
        Sprite sprite = AssetDatabase.LoadAssetAtPath<LootDefinition>(
            "Assets/Scriptable Objects/Loot/Definitions/LongBow.asset").WorldSprite;
        Assert.That(sprite, Is.Not.Null);
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            Assert.That(texture.LoadImage(System.IO.File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite.texture))), Is.True);
            Rect rect = sprite.rect;
            Assert.That((int)rect.width % 2, Is.EqualTo(1), "An odd width gives the bow a single center column.");
            int column = (int)rect.x + (int)rect.width / 2;
            int top = (int)rect.yMax - 1;
            while (top >= rect.y && texture.GetPixel(column, top).a == 0f) top--;
            int bottom = top;
            while (bottom - 1 >= rect.y && texture.GetPixel(column, bottom - 1).a > 0f) bottom--;
            Assert.That(top - bottom + 1, Is.EqualTo(3), "The limb is one wood row between two outline rows.");
            Assert.That(texture.GetPixel(column, (int)rect.y).a, Is.GreaterThan(0f), "The string closes the bottom row.");

            Vector2 handle = new Vector2(column + 0.5f - rect.x, (top + bottom + 1) * 0.5f - rect.y);
            Vector2 expected = (handle - sprite.pivot) / sprite.pixelsPerUnit;
            Vector2 grip = LongBow().Presentation.GripPoint;
            Assert.That(grip.x, Is.EqualTo(expected.x).Within(0.00001f));
            Assert.That(grip.y, Is.EqualTo(expected.y).Within(0.00001f));
        }
        finally { UnityEngine.Object.DestroyImmediate(texture); }
    }

    [TestCase(0f, -1f)]
    [TestCase(1f, -1f)]
    [TestCase(-1f, -1f)]
    [TestCase(0f, 1f)]
    [TestCase(1f, 1f)]
    [TestCase(-1f, 1f)]
    public void LongBow_PresenterHoldsTheGripAndShootsAlongTheFacing(float x, float y)
    {
        WeaponDefinition.PresentationConfig presentation = LongBow().Presentation;
        Vector2 facing = new Vector2(x, y).normalized;
        const float stringY = -0.1875f;

        Assert.That(BowPoint(presentation, facing, presentation.GripPoint).magnitude, Is.LessThan(0.00001f),
            "The grip lands on MainHandGrip.");
        Vector2 shot = BowPoint(presentation, facing, presentation.GripPoint + Vector2.up) -
            BowPoint(presentation, facing, presentation.GripPoint);
        Assert.That(Vector2.Dot(shot.normalized, facing), Is.EqualTo(1f).Within(0.00001f),
            "Sprite +Y shoots along the facing, mirrored or not.");
        Vector2 toString = BowPoint(presentation, facing, new Vector2(0f, stringY));
        Assert.That(Vector2.Dot(toString, facing), Is.LessThan(0f), "The string sits behind the grip.");
        Assert.That(Mathf.Abs(Cross(toString, facing)), Is.LessThan(0.00001f), "The string is centered on the shot.");
        Vector2 limbs = BowPoint(presentation, facing, presentation.GripPoint + Vector2.right) -
            BowPoint(presentation, facing, presentation.GripPoint);
        Assert.That(Vector2.Dot(limbs, facing), Is.EqualTo(0f).Within(0.00001f), "The limbs span across the shot.");
    }

    [TestCase("ArmingSwordWeaponDefinition")]
    [TestCase("RapierWeaponDefinition")]
    [TestCase("RondelDaggerWeaponDefinition")]
    [TestCase("MagicCinquedeaWeaponDefinition")]
    [TestCase("MagicWandWeaponDefinition")]
    [TestCase("MagicSwordWeaponDefinition")]
    [TestCase("LongSwordCombatDefinition")]
    [TestCase("ZweihanderWeaponDefinition")]
    [TestCase("MagicStaffWeaponDefinition")]
    public void OtherGenericWeapons_KeepTheirVisualInTheMainHand(string definition)
    {
        WeaponDefinition weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(
            $"Assets/Scriptable Objects/Loot/Definitions/{definition}.asset");
        Assert.That(weapon, Is.Not.Null, definition);
        Assert.That(weapon.Presentation.Rig, Is.EqualTo(WeaponRig.HandHeld), definition);
        Assert.That(weapon.TryValidate(out string error), Is.True, error);
    }

    // Position of a bow sprite point relative to WeaponPose, following the presenter: the pivot turns to the
    // facing and mirrors Y when facing left, and the visual is grip-aligned and turned by the angle correction.
    private static Vector2 BowPoint(WeaponDefinition.PresentationConfig presentation, Vector2 facing, Vector2 point)
    {
        Vector2 visual = PlayerWeaponPresentationMath.CalculateGripAlignedWeaponPosition(
            presentation.GripPoint, Vector2.one, presentation.AngleCorrection);
        Vector2 local = visual + (Vector2)(Quaternion.Euler(0f, 0f, presentation.AngleCorrection) * point);
        if (PlayerWeaponPresentationMath.ShouldMirror(facing)) local.y = -local.y;
        return Quaternion.Euler(0f, 0f, PlayerWeaponPresentationMath.CalculateFacingAngleDegrees(facing)) * local;
    }

    private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

    private static WeaponDefinition LongBow() => AssetDatabase.LoadAssetAtPath<WeaponDefinition>(
        "Assets/Scriptable Objects/Loot/Definitions/LongBowWeaponDefinition.asset");

    [Test]
    public void MissingAnyDirection_DisablesCompleteness()
    {
        DirectionalAttackAnimationSet set = ScriptableObject.CreateInstance<DirectionalAttackAnimationSet>();
        AnimationClip clip = new AnimationClip();
        try
        {
            SerializedObject serialized = new SerializedObject(set);
            foreach (string direction in Directions)
                serialized.FindProperty("_attack" + direction).objectReferenceValue = clip;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(set.TryValidate(out _), Is.True);
            foreach (string direction in Directions)
            {
                serialized.FindProperty("_attack" + direction).objectReferenceValue = null;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(set.IsComplete, Is.False, direction);
                Assert.That(set.TryValidate(out string error), Is.False);
                Assert.That(error, Does.Contain("six clips"));
                serialized.FindProperty("_attack" + direction).objectReferenceValue = clip;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(clip);
            UnityEngine.Object.DestroyImmediate(set);
        }
    }

    [Test]
    public void Definitions_ShareDaggerIdentityButOtherFamiliesAreDistinct()
    {
        string[] names = { "ArmingSword", "Rapier", "RondelDagger", "MagicCinquedea", "MagicWand", "MagicSword" };
        DirectionalAttackAnimationSet[] sets = names.Select(name =>
            AssetDatabase.LoadAssetAtPath<WeaponDefinition>(
                $"Assets/Scriptable Objects/Loot/Definitions/{name}WeaponDefinition.asset")
                .Presentation.AttackAnimationSet).ToArray();
        Assert.That(sets, Has.All.Not.Null);
        Assert.That(sets[2], Is.SameAs(sets[3]));
        Assert.That(new[] { sets[0], sets[1], sets[2], sets[4], sets[5] }.Distinct().Count(), Is.EqualTo(5));
        foreach (string name in names)
        {
            WeaponDefinition definition = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(
                $"Assets/Scriptable Objects/Loot/Definitions/{name}WeaponDefinition.asset");
            Assert.That(definition.Presentation.HasGenericAttack, Is.True, name);
            Assert.That(definition.Presentation.AttackAnimationSet.TryValidate(out _), Is.True, name);
        }
    }
}
