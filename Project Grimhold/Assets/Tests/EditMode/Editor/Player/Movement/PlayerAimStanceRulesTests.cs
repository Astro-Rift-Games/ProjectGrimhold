using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class PlayerAimStanceRulesTests
{
    private const string Definitions = "Assets/Scriptable Objects/Loot/Definitions/";
    private const string AimModeProperty = "_presentation._aimMode";

    private WeaponDefinition _weapon;

    [TearDown]
    public void TearDown()
    {
        if (_weapon != null)
        {
            Object.DestroyImmediate(_weapon);
            _weapon = null;
        }
    }

    [Test]
    public void IsAccepted_WhenEveryConditionHolds()
    {
        Assert.That(Accept(), Is.True);
    }

    [Test]
    public void IsAccepted_NeedsTheSecondaryActionHeld()
    {
        Assert.That(Accept(secondaryHeld: false), Is.False);
    }

    [Test]
    public void IsAccepted_NeedsAnAimStanceWeapon()
    {
        Assert.That(Accept(weaponAllows: false), Is.False);
    }

    [Test]
    public void IsAccepted_NeedsTheGameplayPhaseActive()
    {
        Assert.That(Accept(gameplayPhaseActive: false), Is.False);
    }

    [Test]
    public void IsAccepted_NeedsAnAlivePlayer()
    {
        Assert.That(Accept(isAlive: false), Is.False);
    }

    [Test]
    public void IsAccepted_NeedsAPlayerThatIsNotDowned()
    {
        Assert.That(Accept(isDowned: true), Is.False);
    }

    [Test]
    public void IsAccepted_NeverOverlapsAnActiveShieldDefense()
    {
        // Shield defense needs an active shield; the aim stance needs none, so the two cannot both hold.
        Assert.That(Accept(hasActiveShield: true), Is.False);
    }

    [Test]
    public void WeaponAllows_TwoHandedAimStanceWeaponOnly()
    {
        _weapon = Load("LongBow");
        SetAimMode(_weapon, WeaponAimMode.AimStance);
        Assert.That(_weapon.Handedness, Is.EqualTo(WeaponHandedness.TwoHanded));
        Assert.That(PlayerAimStanceRules.WeaponAllows(_weapon), Is.True);

        SetAimMode(_weapon, WeaponAimMode.FreeAim);
        Assert.That(PlayerAimStanceRules.WeaponAllows(_weapon), Is.False, "The free arc has no stance.");

        SetAimMode(_weapon, WeaponAimMode.BakedFacing);
        Assert.That(PlayerAimStanceRules.WeaponAllows(_weapon), Is.False);
        Assert.That(PlayerAimStanceRules.WeaponAllows(null), Is.False);
    }

    [Test]
    public void WeaponAllows_RejectsAOneHandedWeapon()
    {
        _weapon = Load("MagicWand");
        Assert.That(_weapon.Handedness, Is.EqualTo(WeaponHandedness.OneHanded));
        SetAimMode(_weapon, WeaponAimMode.AimStance);

        Assert.That(PlayerAimStanceRules.WeaponAllows(_weapon), Is.False);
        Assert.That(_weapon.TryValidate(out string error), Is.False);
        Assert.That(error, Does.Contain("aim stance"));
    }

    [Test]
    public void AimStanceOnAMeleeWeapon_IsRejected()
    {
        _weapon = Load("Zweihander");
        Assert.That(_weapon.Handedness, Is.EqualTo(WeaponHandedness.TwoHanded));
        SetAimMode(_weapon, WeaponAimMode.AimStance);

        Assert.That(_weapon.TryValidate(out string error), Is.False);
        Assert.That(error, Does.Contain("ranged"));
    }

    private static bool Accept(
        bool secondaryHeld = true,
        bool weaponAllows = true,
        bool gameplayPhaseActive = true,
        bool isAlive = true,
        bool isDowned = false,
        bool hasActiveShield = false)
    {
        return PlayerAimStanceRules.IsAccepted(
            secondaryHeld, weaponAllows, gameplayPhaseActive, isAlive, isDowned, hasActiveShield);
    }

    private static WeaponDefinition Load(string name)
    {
        var asset = AssetDatabase.LoadAssetAtPath<WeaponDefinition>($"{Definitions}{name}WeaponDefinition.asset");
        Assert.That(asset, Is.Not.Null, name);
        return Object.Instantiate(asset);
    }

    private static void SetAimMode(WeaponDefinition weapon, WeaponAimMode mode)
    {
        var serialized = new SerializedObject(weapon);
        serialized.FindProperty(AimModeProperty).intValue = (int)mode;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
