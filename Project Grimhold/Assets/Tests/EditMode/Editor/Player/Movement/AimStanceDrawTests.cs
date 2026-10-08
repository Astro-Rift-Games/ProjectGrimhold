using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class AimStanceDrawTests
{
    private const string Definitions = "Assets/Scriptable Objects/Loot/Definitions/";
    private const string AimModeProperty = "_presentation._aimMode";
    private const float Dt = 0.02f;

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
    public void NextStartTick_EnteringTheStance_StartsAtTheCurrentTick()
    {
        Assert.That(AimStanceDraw.NextStartTick(-1, true, 100), Is.EqualTo(100));
    }

    [Test]
    public void NextStartTick_HoldingTheStance_KeepsTheStart()
    {
        Assert.That(AimStanceDraw.NextStartTick(100, true, 130), Is.EqualTo(100));
    }

    [Test]
    public void NextStartTick_LeavingTheStance_ResetsIt()
    {
        Assert.That(AimStanceDraw.NextStartTick(100, false, 130), Is.EqualTo(AimStanceDraw.NoStart));
        Assert.That(AimStanceDraw.NextStartTick(AimStanceDraw.NoStart, false, 131), Is.EqualTo(AimStanceDraw.NoStart));
    }

    [Test]
    public void NextStartTick_ReenteringAfterLeaving_RestartsTheDraw()
    {
        int start = AimStanceDraw.NextStartTick(-1, true, 100);
        start = AimStanceDraw.NextStartTick(start, false, 120);
        start = AimStanceDraw.NextStartTick(start, true, 125);

        Assert.That(start, Is.EqualTo(125));
    }

    [Test]
    public void IsFullyDrawn_IsFalseJustBeforeTheDrawTime_AndTrueOnTheBoundaryTick()
    {
        // 0.45 s at 0.02 s per tick rounds up to 23 ticks.
        Assert.That(AimStanceDraw.IsFullyDrawn(122, 100, 0.45f, Dt), Is.False);
        Assert.That(AimStanceDraw.IsFullyDrawn(123, 100, 0.45f, Dt), Is.True);
        Assert.That(AimStanceDraw.IsFullyDrawn(124, 100, 0.45f, Dt), Is.True);
    }

    [Test]
    public void IsFullyDrawn_ZeroDrawTimeIsDrawnAtOnce()
    {
        Assert.That(AimStanceDraw.IsFullyDrawn(100, 100, 0f, Dt), Is.True);
    }

    [Test]
    public void IsFullyDrawn_NeedsAStartedStance()
    {
        Assert.That(AimStanceDraw.IsFullyDrawn(500, AimStanceDraw.NoStart, 0.45f, Dt), Is.False);
    }

    [TestCase(float.NaN)]
    [TestCase(-0.1f)]
    [TestCase(float.PositiveInfinity)]
    public void IsFullyDrawn_RejectsAnInvalidDrawTime(float drawSeconds)
    {
        Assert.That(AimStanceDraw.IsFullyDrawn(500, 100, drawSeconds, Dt), Is.False);
    }

    [Test]
    public void ReleaseSeconds_UsesTheAimedDelayOnlyWhenFullyDrawn()
    {
        _weapon = Load("LongBow");
        Assert.That(AimStanceDraw.TrySelectAimedReleaseSeconds(_weapon, true, out float aimed), Is.True);
        Assert.That(aimed, Is.EqualTo(_weapon.AimedReleaseSeconds));

        Assert.That(AimStanceDraw.TrySelectAimedReleaseSeconds(_weapon, false, out _), Is.False,
            "Before it is fully drawn the normal release timing applies.");
    }

    [Test]
    public void ReleaseSeconds_AimedDelayIsShorterThanTheNormalRelease()
    {
        _weapon = Load("LongBow");

        Assert.That(_weapon.AimedReleaseSeconds, Is.LessThan(_weapon.AttackReleaseSeconds));
        Assert.That(_weapon.AimStanceDrawSeconds, Is.EqualTo(_weapon.AttackReleaseSeconds).Within(0.0001f));
    }

    [TestCase("MagicWand")]
    [TestCase("ArmingSword")]
    [TestCase("Zweihander")]
    public void ReleaseSeconds_WandAndMeleeAreUnaffected(string weaponName)
    {
        _weapon = Load(weaponName);

        Assert.That(AimStanceDraw.TrySelectAimedReleaseSeconds(_weapon, true, out _), Is.False, weaponName);
    }

    [Test]
    public void DrawClipSeconds_DrawsInOverTheDrawTimeThenHoldsTheDrawnFrame()
    {
        Assert.That(AimStanceDraw.DrawClipSeconds(0f, 0.5f, 0.44f), Is.EqualTo(0f));
        Assert.That(AimStanceDraw.DrawClipSeconds(0.25f, 0.5f, 0.44f), Is.EqualTo(0.22f).Within(0.00001f));
        Assert.That(AimStanceDraw.DrawClipSeconds(0.5f, 0.5f, 0.44f), Is.EqualTo(0.44f).Within(0.00001f));
        Assert.That(AimStanceDraw.DrawClipSeconds(5f, 0.5f, 0.44f), Is.EqualTo(0.44f).Within(0.00001f));
    }

    [Test]
    public void DrawClipSeconds_ZeroDrawTimeHoldsTheDrawnFrameAtOnce()
    {
        Assert.That(AimStanceDraw.DrawClipSeconds(0f, 0f, 0.44f), Is.EqualTo(0.44f).Within(0.00001f));
    }

    [Test]
    public void DrawClipSeconds_NegativeElapsedStaysAtTheStart()
    {
        Assert.That(AimStanceDraw.DrawClipSeconds(-1f, 0.5f, 0.44f), Is.EqualTo(0f));
    }

    [TestCase("LongBow", 0.44f)]
    [TestCase("CompoundBow", 0.39f)]
    [TestCase("LightCrossbow", 0.2f)]
    [TestCase("MagicStaff", 0.8f)]
    public void DrawnFrame_IsTheFrameJustBeforeTheRelease(string weaponName, float expected)
    {
        _weapon = Load(weaponName);

        Assert.That(_weapon.AimStanceDrawnClipSeconds, Is.EqualTo(expected).Within(0.0001f), weaponName);
        Assert.That(_weapon.AimStanceDrawnClipSeconds, Is.LessThan(_weapon.AttackReleaseSeconds), weaponName);
        Assert.That(_weapon.TryValidate(out string error), Is.True, error);
    }

    [TestCase("LongBow")]
    [TestCase("CompoundBow")]
    public void DrawnFrame_ShowsTheDrawnStringFrame(string weaponName)
    {
        _weapon = Load(weaponName);
        WeaponAttackSpriteAnimation stringing = _weapon.Presentation.AttackSpriteAnimation;

        Assert.That(stringing.TryGetSprite(_weapon.AimStanceDrawnClipSeconds, out Sprite sprite), Is.True, weaponName);
        Assert.That(sprite.name, Does.EndWith("_3"), "The held pose shows the last stringing frame.");
    }

    [Test]
    public void DrawnFrame_MustNotExceedTheReleaseOrBeNegative()
    {
        foreach (float bad in new[] { -0.1f, float.NaN, 5f })
        {
            _weapon = Load("LongBow");
            Set(_weapon, "_aimStanceDrawnClipSeconds", bad);

            Assert.That(_weapon.TryValidate(out string error), Is.False, bad.ToString());
            Assert.That(error, Does.Contain("aim stance"));
            Object.DestroyImmediate(_weapon);
            _weapon = null;
        }
    }

    [Test]
    public void DrawnFrame_IsOnlyAllowedOnAimStanceWeapons()
    {
        _weapon = Load("MagicWand");
        Set(_weapon, "_aimStanceDrawnClipSeconds", 0.2f);

        Assert.That(_weapon.TryValidate(out string error), Is.False);
        Assert.That(error, Does.Contain("aim stance"));
    }

    [Test]
    public void ReleaseSeconds_NoWeaponIsUnaffected()
    {
        Assert.That(AimStanceDraw.TrySelectAimedReleaseSeconds(null, true, out _), Is.False);
    }

    [Test]
    public void Fields_AreValidatedAsFiniteAndNonNegative()
    {
        foreach (string field in new[] { "_aimStanceDrawSeconds", "_aimedReleaseSeconds" })
        {
            foreach (float bad in new[] { -0.1f, float.NaN, float.PositiveInfinity })
            {
                _weapon = Load("LongBow");
                Set(_weapon, field, bad);

                Assert.That(_weapon.TryValidate(out string error), Is.False, field + bad);
                Assert.That(error, Does.Contain("aim stance"));
                Object.DestroyImmediate(_weapon);
                _weapon = null;
            }
        }
    }

    [Test]
    public void Fields_AreOnlyAllowedOnAimStanceWeapons()
    {
        _weapon = Load("MagicWand");
        Assert.That(_weapon.TryValidate(out _), Is.True, "The default values are accepted everywhere.");

        Set(_weapon, "_aimedReleaseSeconds", 0.05f);

        Assert.That(_weapon.TryValidate(out string error), Is.False);
        Assert.That(error, Does.Contain("aim stance"));
    }

    [Test]
    public void Fields_AreAcceptedOnAnAimStanceWeapon()
    {
        _weapon = Load("LongBow");
        SetAimMode(_weapon, WeaponAimMode.AimStance);
        Set(_weapon, "_aimedReleaseSeconds", 0.05f);
        Set(_weapon, "_aimStanceDrawSeconds", 0.6f);

        Assert.That(_weapon.TryValidate(out string error), Is.True, error);
    }

    private static WeaponDefinition Load(string name)
    {
        var asset = AssetDatabase.LoadAssetAtPath<WeaponDefinition>($"{Definitions}{name}WeaponDefinition.asset");
        Assert.That(asset, Is.Not.Null, name);
        return Object.Instantiate(asset);
    }

    private static void Set(WeaponDefinition weapon, string field, float value)
    {
        var serialized = new SerializedObject(weapon);
        serialized.FindProperty(field).floatValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetAimMode(WeaponDefinition weapon, WeaponAimMode mode)
    {
        var serialized = new SerializedObject(weapon);
        serialized.FindProperty(AimModeProperty).intValue = (int)mode;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
