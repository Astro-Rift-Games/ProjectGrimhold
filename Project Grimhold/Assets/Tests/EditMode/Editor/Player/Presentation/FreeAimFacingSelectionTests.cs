using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class FreeAimFacingSelectionTests
{
    private const string Definitions = "Assets/Scriptable Objects/Loot/Definitions/";
    private const float Arc = 45f;
    private const float Hysteresis = 5f;

    [Test]
    public void FreeAimWeapon_IsHandledByTheSelection()
    {
        bool showingAim = false;

        bool selected = FreeAimFacingSelection.TrySelect(
            Load("MagicWand"), true, Direction(-90f + 10f), Vector2.down, Arc, Hysteresis, ref showingAim, out _);

        Assert.That(selected, Is.True);
    }

    [TestCase("LongBow")]
    [TestCase("CompoundBow")]
    [TestCase("LightCrossbow")]
    [TestCase("MagicStaff")]
    public void AimStanceWeapon_KeepsThePlainMovementFacing(string weaponName)
    {
        bool showingAim = true;
        WeaponDefinition weapon = Load(weaponName);

        bool selected = FreeAimFacingSelection.TrySelect(
            weapon, true, Direction(-90f + 70f), Vector2.down, Arc, Hysteresis, ref showingAim, out _);

        Assert.That(weapon.Presentation.AimMode, Is.EqualTo(WeaponAimMode.AimStance), weaponName);
        Assert.That(selected, Is.False, "The aim-stance weapons have no hybrid arc; the replicated facing decides.");
    }

    [Test]
    public void AimInsideTheArc_KeepsTheMovementFacing()
    {
        bool showingAim = false;
        Vector2 movementFacing = new Vector2(0.2f, -1f);

        bool selected = FreeAimFacingSelection.TrySelect(
            Load("MagicWand"), true, Direction(-90f + 40f), movementFacing, Arc, Hysteresis, ref showingAim, out Vector2 facing);

        Assert.That(selected, Is.True);
        Assert.That(facing, Is.EqualTo(movementFacing));
        Assert.That(showingAim, Is.False);
    }

    [Test]
    public void AimOutsideTheArc_SwitchesToTheAimBucket()
    {
        bool showingAim = false;
        Vector2 aim = Direction(-90f + 70f);

        FreeAimFacingSelection.TrySelect(
            Load("MagicWand"), true, aim, Vector2.down, Arc, Hysteresis, ref showingAim, out Vector2 facing);

        Assert.That(facing, Is.EqualTo(aim));
        Assert.That(showingAim, Is.True);
    }

    [Test]
    public void Hysteresis_HoldsTheCurrentChoiceNearTheArcBorder()
    {
        WeaponDefinition bow = Load("MagicWand");
        Vector2 aimNearBorder = Direction(-90f + 43f);

        // Showing the movement bucket: the aim must leave the arc to switch.
        bool showingAim = false;
        FreeAimFacingSelection.TrySelect(bow, true, aimNearBorder, Vector2.down, Arc, Hysteresis, ref showingAim, out Vector2 held);
        Assert.That(held, Is.EqualTo(Vector2.down));
        Assert.That(showingAim, Is.False);

        // Showing the aim bucket: the aim must come back inside the arc by the hysteresis to return.
        showingAim = true;
        FreeAimFacingSelection.TrySelect(bow, true, aimNearBorder, Vector2.down, Arc, Hysteresis, ref showingAim, out Vector2 stayed);
        Assert.That(stayed, Is.EqualTo(aimNearBorder));
        Assert.That(showingAim, Is.True);

        FreeAimFacingSelection.TrySelect(
            bow, true, Direction(-90f + 30f), Vector2.down, Arc, Hysteresis, ref showingAim, out Vector2 returned);
        Assert.That(returned, Is.EqualTo(Vector2.down));
        Assert.That(showingAim, Is.False);
    }

    [Test]
    public void ArcOfTheNarrowestBucketHalfWidth_AlwaysPresentsTheAimBucket()
    {
        // The six buckets are not uniform (the left and right sides span 90 degrees), so the narrowest half-width
        // is 22.5 degrees; inside it the aim always shares the movement bucket.
        for (float degrees = 1f; degrees < 360f; degrees += 7f)
        {
            Vector2 aim = Direction(degrees);
            bool showingAim = false;

            FreeAimFacingSelection.TrySelect(
                Load("MagicWand"), true, aim, Vector2.down, 22.4f, 0f, ref showingAim, out Vector2 facing);

            Assert.That(
                CharacterVisualDirectionResolver.Resolve(facing),
                Is.EqualTo(CharacterVisualDirectionResolver.Resolve(aim)),
                degrees.ToString());
        }
    }

    [TestCase("ArmingSword")]
    [TestCase("Spellbook")]
    [TestCase("Rapier")]
    public void BakedFacingWeapon_KeepsTheMovementFacing(string weaponName)
    {
        bool showingAim = true;
        WeaponDefinition weapon = Load(weaponName);

        bool selected = FreeAimFacingSelection.TrySelect(
            weapon, true, Vector2.up, Vector2.down, Arc, Hysteresis, ref showingAim, out _);

        Assert.That(weapon.Presentation.AimMode, Is.EqualTo(WeaponAimMode.BakedFacing), weaponName);
        Assert.That(selected, Is.False, weaponName);
        Assert.That(showingAim, Is.False, "Leaving free aim resets the choice.");
    }

    [Test]
    public void NoWeaponOrNoAimSample_KeepsTheMovementFacing()
    {
        bool showingAim = false;

        Assert.That(
            FreeAimFacingSelection.TrySelect(null, true, Vector2.up, Vector2.down, Arc, Hysteresis, ref showingAim, out _),
            Is.False);
        Assert.That(
            FreeAimFacingSelection.TrySelect(
                Load("MagicWand"), false, Vector2.up, Vector2.down, Arc, Hysteresis, ref showingAim, out _),
            Is.False);
    }

    private static Vector2 Direction(float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
    }

    private static WeaponDefinition Load(string name)
    {
        var weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>($"{Definitions}{name}WeaponDefinition.asset");
        Assert.That(weapon, Is.Not.Null, name);
        return weapon;
    }
}
