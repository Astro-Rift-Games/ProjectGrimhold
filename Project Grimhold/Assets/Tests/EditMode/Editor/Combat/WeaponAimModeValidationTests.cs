using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Combat
{
    public sealed class WeaponAimModeValidationTests
    {
        private const string DefinitionsFolder = "Assets/Scriptable Objects/Loot/Definitions/";
        private const string AimModeProperty = "_presentation._aimMode";
        private const string SecondaryGripProperty = "_presentation._secondaryGripPoint";

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
        public void AimMode_DefaultsToBakedFacing()
        {
            Assert.That(WeaponAimMode.BakedFacing, Is.EqualTo((WeaponAimMode)0));
            Assert.That(
                AssetDatabase.LoadAssetAtPath<WeaponDefinition>(
                    $"{DefinitionsFolder}ArmingSwordWeaponDefinition.asset").Presentation.AimMode,
                Is.EqualTo(WeaponAimMode.BakedFacing));
        }

        [TestCase("LongBow")]
        [TestCase("MagicWand")]
        public void FreeAim_OnRangedWeapon_IsValid(string weaponName)
        {
            _weapon = Load(weaponName);
            Assert.That(_weapon.PrimaryAttack, Is.InstanceOf<RangedAttackConfig>(), weaponName);

            SetAimMode(_weapon, WeaponAimMode.FreeAim);
            EnsureDistinctSecondaryGrip(_weapon);

            Assert.That(_weapon.TryValidate(out string error), Is.True, error);
        }

        [Test]
        public void FreeAim_OnMeleeWeapon_IsRejected()
        {
            _weapon = Load("ArmingSword");
            Assert.That(_weapon.PrimaryAttack, Is.InstanceOf<MeleeAttackConfig>());

            SetAimMode(_weapon, WeaponAimMode.FreeAim);

            Assert.That(_weapon.TryValidate(out string error), Is.False);
            Assert.That(error, Does.Contain("free aim"));
        }

        [Test]
        public void FreeAim_OnHandHeldTwoHandedWithAuthoredSecondHand_IsValid()
        {
            _weapon = Load("MagicStaff");
            Assert.That(_weapon.Presentation.Rig, Is.EqualTo(WeaponRig.HandHeld));
            Assert.That(_weapon.Presentation.SecondHand, Is.EqualTo(SecondHandPresentation.FollowsAuthoredMotion));

            SetAimMode(_weapon, WeaponAimMode.FreeAim);
            EnsureDistinctSecondaryGrip(_weapon);

            Assert.That(_weapon.TryValidate(out string error), Is.True, error);
        }

        [TestCase("MagicStaff")]
        [TestCase("LongBow")]
        [TestCase("LightCrossbow")]
        public void FreeAim_OnTwoHandedWeaponWithoutADistinctSecondaryGrip_IsRejected(string weaponName)
        {
            _weapon = Load(weaponName);
            Assert.That(_weapon.Handedness, Is.EqualTo(WeaponHandedness.TwoHanded), weaponName);

            SetAimMode(_weapon, WeaponAimMode.FreeAim);
            SetSecondaryGrip(_weapon, _weapon.Presentation.GripPoint);

            Assert.That(_weapon.TryValidate(out string error), Is.False, weaponName);
            Assert.That(error, Does.Contain("secondary grip"));
        }

        [Test]
        public void FreeAim_OnOneHandedWeapon_DoesNotNeedASecondaryGrip()
        {
            _weapon = Load("MagicWand");
            Assert.That(_weapon.Handedness, Is.EqualTo(WeaponHandedness.OneHanded));

            SetAimMode(_weapon, WeaponAimMode.FreeAim);
            SetSecondaryGrip(_weapon, _weapon.Presentation.GripPoint);

            Assert.That(_weapon.TryValidate(out string error), Is.True, error);
        }

        [Test]
        public void UndefinedAimMode_IsRejected()
        {
            _weapon = Load("LongBow");
            var serialized = new SerializedObject(_weapon);
            serialized.FindProperty(AimModeProperty).intValue = 99;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(_weapon.TryValidate(out string error), Is.False);
            Assert.That(error, Does.Contain("aim mode"));
        }

        private static WeaponDefinition Load(string name)
        {
            WeaponDefinition asset = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(
                $"{DefinitionsFolder}{name}WeaponDefinition.asset");
            Assert.That(asset, Is.Not.Null, name);
            return Object.Instantiate(asset);
        }

        private static void EnsureDistinctSecondaryGrip(WeaponDefinition weapon) =>
            SetSecondaryGrip(weapon, weapon.Presentation.GripPoint + new Vector2(0f, -0.3f));

        private static void SetSecondaryGrip(WeaponDefinition weapon, Vector2 point)
        {
            var serialized = new SerializedObject(weapon);
            serialized.FindProperty(SecondaryGripProperty).vector2Value = point;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetAimMode(WeaponDefinition weapon, WeaponAimMode mode)
        {
            var serialized = new SerializedObject(weapon);
            serialized.FindProperty(AimModeProperty).intValue = (int)mode;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
