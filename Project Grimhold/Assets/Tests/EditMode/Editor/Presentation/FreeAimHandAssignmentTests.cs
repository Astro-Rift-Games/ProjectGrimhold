using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Presentation
{
    public sealed class FreeAimHandAssignmentTests
    {
        private static readonly Vector2 Anchor = new Vector2(0.2f, 0.3f);
        private static readonly Vector2 Grip = new Vector2(0f, 0.13f);
        private static readonly Vector2 Secondary = new Vector2(0f, -0.4f);

        [Test]
        public void WeaponDriven_LeftHandHoldsTheHandleAndRightHandTheStringOrStock()
        {
            RangedWeaponAimPose pose = Pose(Vector2.right);

            FreeAimHandTargets targets = FreeAimHandAssignment.Resolve(weaponDriven: true, twoHanded: true, pose);

            Assert.That(targets.DrivesLeftHand, Is.True);
            Assert.That(targets.DrivesRightHand, Is.True);
            Assert.That(targets.LeftHand, Is.EqualTo(pose.MainHandPosition));
            Assert.That(targets.RightHand, Is.EqualTo(pose.SecondaryHandPosition));
        }

        [Test]
        public void WeaponDriven_DrivesBothHandsEvenWhenNotFlaggedTwoHanded()
        {
            FreeAimHandTargets targets = FreeAimHandAssignment.Resolve(true, twoHanded: false, Pose(Vector2.up));

            Assert.That(targets.DrivesLeftHand, Is.True);
            Assert.That(targets.DrivesRightHand, Is.True);
        }

        [Test]
        public void HandHeldTwoHanded_RightHandOnGripAndLeftHandOnSecondaryGrip()
        {
            RangedWeaponAimPose pose = Pose(Vector2.left);

            FreeAimHandTargets targets = FreeAimHandAssignment.Resolve(false, true, pose);

            Assert.That(targets.RightHand, Is.EqualTo(pose.MainHandPosition));
            Assert.That(targets.LeftHand, Is.EqualTo(pose.SecondaryHandPosition));
            Assert.That(targets.DrivesRightHand && targets.DrivesLeftHand, Is.True);
        }

        [Test]
        public void HandHeldOneHanded_LeavesTheLeftHandToTheAnimator()
        {
            RangedWeaponAimPose pose = Pose(Vector2.down);

            FreeAimHandTargets targets = FreeAimHandAssignment.Resolve(false, false, pose);

            Assert.That(targets.DrivesRightHand, Is.True);
            Assert.That(targets.RightHand, Is.EqualTo(pose.MainHandPosition));
            Assert.That(targets.DrivesLeftHand, Is.False);
        }

        private static RangedWeaponAimPose Pose(Vector2 aim) =>
            RangedWeaponAimPoseMath.Resolve(aim, Anchor, Grip, Secondary, -90f, Vector2.one);
    }
}
