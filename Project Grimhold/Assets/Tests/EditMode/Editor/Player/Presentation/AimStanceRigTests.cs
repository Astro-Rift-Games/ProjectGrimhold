using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The aim stance turns the left hand, the weapon and the right hand rigidly about the weapon's torso pivot by the
/// residual, instead of swinging the weapon about the holding hand. Without the stance the pose is the baked one.
/// </summary>
public sealed class AimStanceRigTests
{
    private const string Definitions = "Assets/Scriptable Objects/Loot/Definitions/";
    private const string PlayerPrefab = "Assets/Prefabs/NetworkPlayer.prefab";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private const float Tolerance = 0.001f;

    private static readonly string[] Weapons = { "LongBow", "CompoundBow", "LightCrossbow", "MagicStaff" };

    private static readonly CharacterVisualDirection[] Buckets =
    {
        CharacterVisualDirection.North, CharacterVisualDirection.NorthEast, CharacterVisualDirection.NorthWest,
        CharacterVisualDirection.South, CharacterVisualDirection.SouthEast, CharacterVisualDirection.SouthWest
    };

    private GameObject _player;
    private PlayerWeaponPresenter _presenter;
    private PlayerAnimatorView _view;
    private Animator _animator;
    private Transform _visualRoot;
    private Transform _weaponPivot;
    private Transform _leftHandPivot;
    private Transform _rightHandPivot;
    private Transform _leftHand;
    private Transform _rightHand;
    private WeaponDefinition _weaponCopy;
    private LootDefinition _lootCopy;

    [TearDown]
    public void TearDown()
    {
        if (_weaponCopy != null)
        {
            Object.DestroyImmediate(_weaponCopy);
        }

        if (_lootCopy != null)
        {
            Object.DestroyImmediate(_lootCopy);
        }

        if (_player != null)
        {
            Object.DestroyImmediate(_player);
            _player = null;
        }
    }

    [TestCaseSource(nameof(Weapons))]
    public void Stance_MovesTheBlockRigidlyAboutTheTorsoPivot(string weaponName)
    {
        WeaponDefinition weapon = Setup(weaponName);
        Vector2 pivot = weapon.AimStanceTorsoPivot;

        for (int bucket = 0; bucket < Buckets.Length; bucket++)
        {
            Vector2 facing = CharacterVisualDirectionResolver.GetCanonicalVector(Buckets[bucket]);
            Vector2 bakedGrip = Pose(weapon, bucket, facing, followsAim: false, facing);
            Vector2 bakedLeft = Local(_leftHand.position);
            Vector2 bakedRight = Local(_rightHand.position);

            foreach (float residual in new[] { -40f, -15f, 20f, 45f })
            {
                Vector2 aim = Rotate(facing, residual);
                Vector2 grip = Pose(weapon, bucket, facing, followsAim: true, aim);

                Assert.That(Vector2.Distance(grip, pivot),
                    Is.EqualTo(Vector2.Distance(bakedGrip, pivot)).Within(Tolerance),
                    $"{weaponName} bucket {bucket} residual {residual}: the weapon keeps its distance from the torso pivot");
                Assert.That(Vector2.Distance(Local(_leftHand.position), grip),
                    Is.EqualTo(Vector2.Distance(bakedLeft, bakedGrip)).Within(Tolerance),
                    $"{weaponName} bucket {bucket} residual {residual}: the left hand keeps its place on the weapon");
                Assert.That(Vector2.Distance(Local(_rightHand.position), grip),
                    Is.EqualTo(Vector2.Distance(bakedRight, bakedGrip)).Within(Tolerance),
                    $"{weaponName} bucket {bucket} residual {residual}: the right hand keeps its place on the weapon");
            }
        }
    }

    [TestCaseSource(nameof(Weapons))]
    public void Stance_TurnsTheWeaponByTheResidualAboutThePivotNotTheHoldingHand(string weaponName)
    {
        WeaponDefinition weapon = Setup(weaponName);
        Vector2 pivot = weapon.AimStanceTorsoPivot;
        int bucket = 3;
        Vector2 facing = CharacterVisualDirectionResolver.GetCanonicalVector(Buckets[bucket]);
        Vector2 bakedGrip = Pose(weapon, bucket, facing, followsAim: false, facing);

        Vector2 grip = Pose(weapon, bucket, facing, followsAim: true, Rotate(facing, 30f));

        Vector2 expected = PointRotation.About(bakedGrip, pivot, 30f);
        Assert.That(Vector2.Distance(grip, expected), Is.LessThan(Tolerance), weaponName);
        Assert.That(Vector2.Distance(grip, bakedGrip), Is.GreaterThan(0.05f),
            "The weapon travels around the torso pivot instead of staying on its grip.");
    }

    [TestCaseSource(nameof(Weapons))]
    public void NoStance_PosesTheBakedPoseAndLeavesTheHandPivotsAlone(string weaponName)
    {
        WeaponDefinition weapon = Setup(weaponName);

        for (int bucket = 0; bucket < Buckets.Length; bucket++)
        {
            Vector2 facing = CharacterVisualDirectionResolver.GetCanonicalVector(Buckets[bucket]);
            Pose(weapon, bucket, facing, followsAim: true, Rotate(facing, 40f));
            Pose(weapon, bucket, facing, followsAim: false, Rotate(facing, 40f));

            Assert.That(_leftHandPivot.localPosition, Is.EqualTo(Vector3.zero), weaponName);
            Assert.That(_rightHandPivot.localPosition, Is.EqualTo(Vector3.zero), weaponName);
            Assert.That(Quaternion.Angle(_leftHandPivot.localRotation, Quaternion.identity), Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(_rightHandPivot.localRotation, Quaternion.identity), Is.LessThan(0.001f));
            float bakedAngle = PlayerWeaponPresentationMath.CalculateFacingAngleDegrees(facing);
            Assert.That(Quaternion.Angle(_weaponPivot.localRotation, Quaternion.Euler(0f, 0f, bakedAngle)),
                Is.LessThan(0.001f), weaponName);
        }
    }

    [Test]
    public void OutwardOffsetKnob_PushesTheBlockAlongTheAimByTheBlend()
    {
        WeaponDefinition weapon = Setup("LongBow");
        int bucket = 3;
        Vector2 facing = CharacterVisualDirectionResolver.GetCanonicalVector(Buckets[bucket]);
        Vector2 aim = Rotate(facing, 25f);
        SetOffset(weapon, 0.4f);
        ReEquip();

        SetBlend(0f);
        Vector2 none = Pose(weapon, bucket, facing, true, aim);
        SetBlend(1f);
        Vector2 full = Pose(weapon, bucket, facing, true, aim);
        SetBlend(0.5f);
        Vector2 half = Pose(weapon, bucket, facing, true, aim);

        Assert.That(Vector2.Distance(full - none, aim.normalized * 0.4f), Is.LessThan(Tolerance));
        Assert.That(Vector2.Distance(half - none, aim.normalized * 0.2f), Is.LessThan(Tolerance));
    }

    [Test]
    public void ZeroOffsetKnob_IsPureRigidRotation()
    {
        WeaponDefinition weapon = Setup("LongBow");
        Assert.That(weapon.AimStanceOutwardOffset, Is.EqualTo(0f));
        int bucket = 3;
        Vector2 facing = CharacterVisualDirectionResolver.GetCanonicalVector(Buckets[bucket]);

        SetBlend(1f);
        Vector2 withBlend = Pose(weapon, bucket, facing, true, Rotate(facing, 25f));
        SetBlend(0f);
        Vector2 withoutBlend = Pose(weapon, bucket, facing, true, Rotate(facing, 25f));

        Assert.That(Vector2.Distance(withBlend, withoutBlend), Is.LessThan(Tolerance));
    }

    private WeaponDefinition Setup(string weaponName)
    {
        // Copies, so a test can change a value without touching the shared assets.
        var weapon = Object.Instantiate(
            AssetDatabase.LoadAssetAtPath<WeaponDefinition>($"{Definitions}{weaponName}WeaponDefinition.asset"));
        var loot = Object.Instantiate(AssetDatabase.LoadAssetAtPath<LootDefinition>($"{Definitions}{weaponName}.asset"));
        typeof(LootDefinition).GetField("_weaponDefinition", Private).SetValue(loot, weapon);
        _weaponCopy = weapon;
        _lootCopy = loot;
        Assert.That(weapon, Is.Not.Null, weaponName);
        _player = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab));
        _player.hideFlags = HideFlags.HideAndDontSave;
        _presenter = _player.GetComponentInChildren<PlayerWeaponPresenter>(true);
        _view = _player.GetComponentInChildren<PlayerAnimatorView>(true);
        _animator = _view.GetComponent<Animator>();
        _visualRoot = _animator.transform;
        typeof(PlayerWeaponPresenter).GetMethod("CacheDependencies", Private).Invoke(_presenter, null);
        typeof(PlayerWeaponPresenter).GetMethod("CaptureBaseState", Private).Invoke(_presenter, null);
        typeof(PlayerWeaponPresenter).GetMethod("ApplyMainHandDefinition", Private)
            .Invoke(_presenter, new object[] { loot });
        _weaponPivot = (Transform)typeof(PlayerWeaponPresenter).GetField("_mainHandWeaponPivot", Private)
            .GetValue(_presenter);
        _leftHandPivot = _visualRoot.Find("LeftHandPivot");
        _rightHandPivot = _visualRoot.Find("RightHandPivot");
        _leftHand = _leftHandPivot.Find("LeftHand");
        _rightHand = _rightHandPivot.Find("RightHand");
        return weapon;
    }

    // Poses the weapon at its drawn frame in a bucket and returns the weapon pivot (the grip) in the visual root.
    private Vector2 Pose(WeaponDefinition weapon, int bucket, Vector2 facing, bool followsAim, Vector2 aim)
    {
        weapon.Presentation.GetAttackClip(bucket).SampleAnimation(_animator.gameObject, weapon.AimStanceDrawnClipSeconds);
        typeof(CharacterAnimatorView).GetProperty("VisualFacingDirection").SetValue(_view, facing);
        typeof(PlayerWeaponPresenter).GetMethod("PoseWeapon", Private)
            .Invoke(_presenter, new object[] { facing, followsAim, aim.normalized });
        return Local(_weaponPivot.position);
    }

    private Vector2 Local(Vector3 world)
    {
        Vector3 local = _visualRoot.InverseTransformPoint(world);
        return new Vector2(local.x, local.y);
    }

    private void ReEquip() =>
        typeof(PlayerWeaponPresenter).GetMethod("ApplyMainHandDefinition", Private)
            .Invoke(_presenter, new object[] { _lootCopy });

    private void SetBlend(float blend) =>
        typeof(PlayerAnimatorView).GetField("_aimStanceBlend", Private).SetValue(_view, blend);

    private static void SetOffset(WeaponDefinition weapon, float offset)
    {
        // The presenter caches the weapon values when it equips, so the test equips again after changing a value.
        var serialized = new SerializedObject(weapon);
        serialized.FindProperty("_aimStanceOutwardOffset").floatValue = offset;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Vector2 Rotate(Vector2 value, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float cosine = Mathf.Cos(radians);
        float sine = Mathf.Sin(radians);
        return new Vector2(value.x * cosine - value.y * sine, value.x * sine + value.y * cosine);
    }
}
