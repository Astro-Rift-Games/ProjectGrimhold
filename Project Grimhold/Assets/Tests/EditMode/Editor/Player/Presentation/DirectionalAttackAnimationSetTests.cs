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
    [TestCase("GreatHammer", "GreatHammer")]
    [TestCase("MagicStaff", "MagicStaff")]
    [TestCase("LongBow", "LongBow")]
    [TestCase("CompoundBow", "CompoundBow")]
    [TestCase("LightCrossbow", "LightCrossbow")]
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
        Assert.That(longSword.Presentation.HasGenericAttack, Is.True);
        Assert.That(longSword.Presentation.AttackAnimationSet, Is.SameAs(
            AssetDatabase.LoadAssetAtPath<DirectionalAttackAnimationSet>(Root + "LongSword.asset")));
        // LongSword.png: the 3 px handle spans rows 22-26; the main hand grips its first row under the guard and
        // the second hand its last one. The source holds the blade across the facing, hence the 180 correction.
        Assert.That(longSword.Presentation.GripPoint, Is.EqualTo(new Vector2(0f, -0.46875f)));
        Assert.That(longSword.Presentation.SecondaryGripPoint, Is.EqualTo(new Vector2(0f, -0.71875f)));
        Assert.That(longSword.Presentation.AngleCorrection, Is.EqualTo(180f));
        Assert.That(longSword.Presentation.Rig, Is.EqualTo(WeaponRig.HandHeld));
        Assert.That(longSword.Presentation.SecondHand, Is.EqualTo(SecondHandPresentation.HoldsSecondaryGrip));
        Assert.That(longSword.TryValidate(out string validationError), Is.True, validationError);
    }

    [Test]
    public void Zweihander_IsTwoHandedGenericAttackWithItsOwnHandleGeometry()
    {
        WeaponDefinition zweihander = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(
            "Assets/Scriptable Objects/Loot/Definitions/ZweihanderWeaponDefinition.asset");
        Assert.That(zweihander.Handedness, Is.EqualTo(WeaponHandedness.TwoHanded));
        Assert.That(zweihander.Presentation.HasGenericAttack, Is.True);
        Assert.That(zweihander.Presentation.AttackAnimationSet, Is.SameAs(
            AssetDatabase.LoadAssetAtPath<DirectionalAttackAnimationSet>(Root + "Zweihander.asset")));
        // Zweihander.png is 33 px tall with a centered pivot and a six-row handle between guard and pommel:
        // the main hand grips the handle row under the guard, the second hand its last row.
        Assert.That(zweihander.Presentation.GripPoint, Is.EqualTo(new Vector2(0f, -0.4375f)));
        Assert.That(zweihander.Presentation.SecondaryGripPoint, Is.EqualTo(new Vector2(0f, -0.75f)));
        Assert.That(zweihander.Presentation.AngleCorrection, Is.EqualTo(180f));
        Assert.That(zweihander.Presentation.Rig, Is.EqualTo(WeaponRig.HandHeld));
        Assert.That(zweihander.Presentation.SecondHand, Is.EqualTo(SecondHandPresentation.HoldsSecondaryGrip));
        Assert.That(zweihander.TryValidate(out string validationError), Is.True, validationError);
    }

    [Test]
    public void GreatHammer_IsTwoHandedGenericAttackWithItsOwnHandleGeometry()
    {
        WeaponDefinition hammer = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(
            "Assets/Scriptable Objects/Loot/Definitions/GreatHammerWeaponDefinition.asset");
        Assert.That(hammer.Handedness, Is.EqualTo(WeaponHandedness.TwoHanded));
        Assert.That(hammer.Presentation.HasGenericAttack, Is.True);
        Assert.That(hammer.Presentation.AttackAnimationSet, Is.SameAs(
            AssetDatabase.LoadAssetAtPath<DirectionalAttackAnimationSet>(Root + "GreatHammer.asset")));
        // GreatHammer.png is 17x33 px with a centered pivot. GreatHammer_Attack.anim holds the hammer across the
        // facing, so it keeps a 180 degree correction: the main hand grips the handle 7 px above the second
        // hand, which holds the last handle row above the collar.
        Assert.That(hammer.Presentation.GripPoint, Is.EqualTo(new Vector2(0f, -0.375f)));
        Assert.That(hammer.Presentation.SecondaryGripPoint, Is.EqualTo(new Vector2(0f, -0.8125f)));
        Assert.That(hammer.Presentation.BladeTip, Is.EqualTo(new Vector2(0f, 1f)));
        Assert.That(hammer.Presentation.AngleCorrection, Is.EqualTo(180f));
        Assert.That(hammer.Presentation.SecondHand, Is.EqualTo(SecondHandPresentation.HoldsSecondaryGrip));
        Assert.That(hammer.Presentation.Rig, Is.EqualTo(WeaponRig.HandHeld));
        Assert.That(hammer.TryValidate(out string validationError), Is.True, validationError);
    }

    [Test]
    public void GreatHammer_GripsAndTipLandOnItsDrawnHandleAndHead()
    {
        // Decoded from disk so it does not depend on the importer's Read/Write setting. Unity's pixel space is
        // bottom-up, so rows are counted from the top of the art here.
        Sprite sprite = AssetDatabase.LoadAssetAtPath<LootDefinition>(
            "Assets/Scriptable Objects/Loot/Definitions/GreatHammer.asset").WorldSprite;
        WeaponDefinition.PresentationConfig presentation = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(
            "Assets/Scriptable Objects/Loot/Definitions/GreatHammerWeaponDefinition.asset").Presentation;
        Assert.That(sprite, Is.Not.Null);
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            Assert.That(texture.LoadImage(System.IO.File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite.texture))), Is.True);
            Rect rect = sprite.rect;
            Assert.That((int)rect.width, Is.EqualTo(17));
            Assert.That((int)rect.height, Is.EqualTo(33));
            Assert.That(sprite.pixelsPerUnit, Is.EqualTo(16f));
            Assert.That(sprite.pivot, Is.EqualTo(new Vector2(8.5f, 16.5f)), "The pivot is the sprite center.");

            bool Opaque(int column, int row) =>
                texture.GetPixel((int)rect.x + column, (int)rect.yMax - 1 - row).a > 0f;
            int RowWidth(int row)
            {
                int width = 0;
                for (int column = 0; column < (int)rect.width; column++)
                    if (Opaque(column, row)) width++;
                return width;
            }
            // The art pixel under a sprite point, measured from the pivot.
            int RowOf(Vector2 point) => (int)rect.height - 1 - Mathf.FloorToInt(sprite.pivot.y + point.y * sprite.pixelsPerUnit);
            int ColumnOf(Vector2 point) => Mathf.FloorToInt(sprite.pivot.x + point.x * sprite.pixelsPerUnit);

            int gripRow = RowOf(presentation.GripPoint);
            int secondaryRow = RowOf(presentation.SecondaryGripPoint);
            Assert.That(ColumnOf(presentation.GripPoint), Is.EqualTo(8));
            Assert.That(ColumnOf(presentation.SecondaryGripPoint), Is.EqualTo(8));
            Assert.That(gripRow, Is.EqualTo(22));
            Assert.That(secondaryRow, Is.EqualTo(29));
            Assert.That(secondaryRow - gripRow, Is.EqualTo(7), "The hands sit 7 px apart.");

            // Both hands hold the 3 px wide handle, and it stays that wide between them.
            for (int row = gripRow; row <= secondaryRow; row++)
            {
                Assert.That(Opaque(8, row), Is.True, $"row {row}");
                Assert.That(RowWidth(row), Is.EqualTo(3), $"row {row} is handle");
            }
            // The second hand holds the last handle row: the next row is the 5 px wide collar.
            Assert.That(RowWidth(secondaryRow + 1), Is.EqualTo(5), "The collar starts right below the second hand.");

            // The tip is the top opaque row of the sprite, on the center column.
            Assert.That(ColumnOf(presentation.BladeTip), Is.EqualTo(8));
            Assert.That(RowOf(presentation.BladeTip), Is.EqualTo(0));
            Assert.That(Opaque(8, 0), Is.True, "The tip lands on an opaque pixel.");
        }
        finally { UnityEngine.Object.DestroyImmediate(texture); }
    }

    [Test]
    public void MagicStaff_IsTwoHandedGenericAttackWithAuthoredSecondHand()
    {
        WeaponDefinition staff = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(
            "Assets/Scriptable Objects/Loot/Definitions/MagicStaffWeaponDefinition.asset");
        Assert.That(staff.Handedness, Is.EqualTo(WeaponHandedness.TwoHanded));
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

    [TestCase("LongBow")]
    [TestCase("CompoundBow")]
    [TestCase("LightCrossbow")]
    public void Bow_IsTwoHandedGenericAttackDrivenByItsOwnPose(string bowName)
    {
        WeaponDefinition bow = Bow(bowName);
        Assert.That(bow.Handedness, Is.EqualTo(WeaponHandedness.TwoHanded));
        Assert.That(bow.Presentation.HasGenericAttack, Is.True);
        Assert.That(bow.Presentation.AttackAnimationSet, Is.SameAs(
            AssetDatabase.LoadAssetAtPath<DirectionalAttackAnimationSet>(Root + bowName + ".asset")));
        // Bow art shoots along sprite +Y, which a -90 degree correction aligns with the presenter's
        // facing axis (+X). Its grip point is derived from the art in Bow_GripPointIsTheCenterOfTheHandle.
        Assert.That(bow.Presentation.AngleCorrection, Is.EqualTo(-90f));
        // The bow owns its pose: its visual follows WeaponPose, and the baked attack places the bow arm on its
        // grip and the drawing hand on its string target.
        Assert.That(bow.Presentation.Rig, Is.EqualTo(WeaponRig.WeaponDriven));
        Assert.That(bow.Presentation.SecondHand, Is.EqualTo(SecondHandPresentation.FollowsAuthoredMotion));
        Assert.That(bow.TryValidate(out string validationError), Is.True, validationError);
    }

    [Test]
    public void LongBow_PlacesItsBowShotFromTheShootingAxis()
    {
        // The Bow Shot is placed from the bow's own shooting axis, not from a blade reach.
        WeaponDefinition bow = Bow("LongBow");
        Assert.That(bow.Presentation.AttackVfx, Is.SameAs(AssetDatabase.LoadAssetAtPath<AttackVfxDefinition>(
            "Assets/Scriptable Objects/Loot/Definitions/LongBowBowShotAttackVfx.asset")));
        Assert.That(bow.Presentation.AttackVfx.UsesWeaponReach, Is.False);
    }

    [TestCase("LongBow")]
    [TestCase("CompoundBow")]
    public void Bow_GripPointIsTheCenterOfTheHandle(string bowName)
    {
        // The bow's handle is the middle of its limb: in the sprite's center column, the first opaque run from
        // the top is the limb (outline, wood, outline) and the last one is the string. The grip is the center of
        // the limb run, measured from the sprite pivot in Unity's bottom-up pixel space.
        Sprite sprite = BowSprite(bowName);
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
            Vector2 grip = Bow(bowName).Presentation.GripPoint;
            Assert.That(grip.x, Is.EqualTo(expected.x).Within(0.00001f));
            Assert.That(grip.y, Is.EqualTo(expected.y).Within(0.00001f));
        }
        finally { UnityEngine.Object.DestroyImmediate(texture); }
    }

    // LightCrossbow.png is 16x17 px with its limbs across the shot and a stock behind them. The grip is the
    // center of the plain foregrip rows of the stock (rows 4-8 from the bottom: after the limb bar of row 9 and
    // before the band of row 3), on the center of the 2 px wide stock.
    [Test]
    public void LightCrossbow_GripPointIsTheCenterOfTheStockForegrip()
    {
        WeaponDefinition crossbow = Bow("LightCrossbow");
        Sprite sprite = BowSprite("LightCrossbow");
        Assert.That(sprite, Is.Not.Null);
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            Assert.That(texture.LoadImage(System.IO.File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite.texture))), Is.True);
            Rect rect = sprite.rect;
            int centerLeft = (int)rect.x + (int)rect.width / 2 - 1;
            for (int row = 4; row <= 8; row++)
            {
                int opaque = Enumerable.Range((int)rect.x, (int)rect.width).Count(x => texture.GetPixel(x, (int)rect.y + row).a > 0f);
                Assert.That(opaque, Is.EqualTo(4), $"Row {row} is the 4 px wide stock.");
                Assert.That(texture.GetPixel(centerLeft, (int)rect.y + row).a, Is.GreaterThan(0f));
                Assert.That(texture.GetPixel(centerLeft + 1, (int)rect.y + row).a, Is.GreaterThan(0f));
            }
            Assert.That(Enumerable.Range((int)rect.x, (int)rect.width).Count(x => texture.GetPixel(x, (int)rect.y + 9).a > 0f),
                Is.EqualTo((int)rect.width), "Row 9 is the limb bar in front of the foregrip.");
            Vector2 handle = new Vector2(rect.width * 0.5f, (4f + 9f) * 0.5f);
            Vector2 expected = (handle - sprite.pivot) / sprite.pixelsPerUnit;
            Assert.That(crossbow.Presentation.GripPoint.x, Is.EqualTo(expected.x).Within(0.00001f));
            Assert.That(crossbow.Presentation.GripPoint.y, Is.EqualTo(expected.y).Within(0.00001f));
        }
        finally { UnityEngine.Object.DestroyImmediate(texture); }
    }

    [Test]
    public void LightCrossbow_ShootsAlongSpriteUpAndLeavesItsAttackSpriteAnimationEmpty()
    {
        WeaponDefinition.PresentationConfig presentation = Bow("LightCrossbow").Presentation;
        Assert.That(presentation.AngleCorrection, Is.EqualTo(-90f));
        Assert.That(presentation.AttackSpriteAnimation, Is.Null);
        foreach (Vector2 facing in new[] { new Vector2(0f, -1f), new Vector2(1f, -1f), new Vector2(-1f, -1f),
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(-1f, 1f) })
        {
            Vector2 unit = facing.normalized;
            Vector2 shot = BowPoint(presentation, unit, presentation.GripPoint + Vector2.up) -
                BowPoint(presentation, unit, presentation.GripPoint);
            Assert.That(Vector2.Dot(shot.normalized, unit), Is.EqualTo(1f).Within(0.00001f), facing.ToString());
        }
    }

    [TestCase("LongBow", 0f, -1f)]
    [TestCase("LongBow", 1f, -1f)]
    [TestCase("LongBow", -1f, -1f)]
    [TestCase("LongBow", 0f, 1f)]
    [TestCase("LongBow", 1f, 1f)]
    [TestCase("LongBow", -1f, 1f)]
    [TestCase("CompoundBow", 0f, -1f)]
    [TestCase("CompoundBow", 1f, -1f)]
    [TestCase("CompoundBow", -1f, -1f)]
    [TestCase("CompoundBow", 0f, 1f)]
    [TestCase("CompoundBow", 1f, 1f)]
    [TestCase("CompoundBow", -1f, 1f)]
    public void Bow_PresenterHoldsTheGripAndShootsAlongTheFacing(string bowName, float x, float y)
    {
        WeaponDefinition.PresentationConfig presentation = Bow(bowName).Presentation;
        Vector2 facing = new Vector2(x, y).normalized;
        // The string is the bottom row of the art, on the center column.
        Sprite sprite = BowSprite(bowName);
        float stringY = (0.5f - sprite.pivot.y) / sprite.pixelsPerUnit;

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
    [TestCase("GreatHammerWeaponDefinition")]
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

    private static WeaponDefinition Bow(string bowName) => AssetDatabase.LoadAssetAtPath<WeaponDefinition>(
        $"Assets/Scriptable Objects/Loot/Definitions/{bowName}WeaponDefinition.asset");

    private static Sprite BowSprite(string bowName) => AssetDatabase.LoadAssetAtPath<LootDefinition>(
        $"Assets/Scriptable Objects/Loot/Definitions/{bowName}.asset").WorldSprite;

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
