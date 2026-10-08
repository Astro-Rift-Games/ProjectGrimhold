using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Presentation
{
    public sealed class RangedWeaponAimPoseMathTests
    {
        private const float Tolerance = 0.0001f;

        private static readonly Vector2 Anchor = new Vector2(0.3f, 0.45f);
        private static readonly Vector2 Grip = new Vector2(0.1f, -0.4f);
        private static readonly Vector2 SecondaryGrip = new Vector2(-0.2f, 0.7f);
        private static readonly Vector2 Scale = new Vector2(1.5f, 0.75f);
        private const float AngleCorrection = -50f;

        [TestCase(1f, 0f, 0f)]
        [TestCase(0f, 1f, 90f)]
        [TestCase(-1f, 0f, 180f)]
        [TestCase(0f, -1f, -90f)]
        [TestCase(0.6f, 0.8f, 53.1301f)]
        public void Resolve_PivotAngleIsTheAimAngle(float x, float y, float expected)
        {
            RangedWeaponAimPose pose = Resolve(new Vector2(x, y));

            Assert.That(pose.PivotAngleDegrees, Is.EqualTo(expected).Within(0.001f));
        }

        [TestCase(1f, 0f, false)]
        [TestCase(0f, 1f, false)]
        [TestCase(0f, -1f, false)]
        [TestCase(-0.01f, 1f, true)]
        [TestCase(-1f, 0f, true)]
        public void Resolve_MirrorsOnlyWhenAimingLeft(float x, float y, bool expected)
        {
            Assert.That(Resolve(new Vector2(x, y).normalized).Mirrored, Is.EqualTo(expected));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Resolve_AngleCorrectionFollowsTheMirrorRule(bool mirroredAim)
        {
            Vector2 aim = mirroredAim ? Vector2.left : Vector2.right;

            RangedWeaponAimPose pose = Resolve(aim);

            Assert.That(
                pose.WeaponAngleCorrection,
                Is.EqualTo(PlayerWeaponPresentationMath.ResolveAngleCorrection(AngleCorrection, mirroredAim))
                    .Within(Tolerance));
        }

        [Test]
        public void Resolve_HandsAtAxisAlignedAim_MatchHandComputedPositions()
        {
            RangedWeaponAimPose east = RangedWeaponAimPoseMath.Resolve(
                Vector2.right, Vector2.zero, Vector2.zero, new Vector2(0f, 1f), 0f, Vector2.one);
            RangedWeaponAimPose north = RangedWeaponAimPoseMath.Resolve(
                Vector2.up, Vector2.zero, Vector2.zero, new Vector2(0f, 1f), 0f, Vector2.one);

            AssertVector(east.SecondaryHandPosition, new Vector2(0f, 1f));
            AssertVector(north.SecondaryHandPosition, new Vector2(-1f, 0f));
        }

        [Test]
        public void Resolve_HandsMatchTheEquivalentTransformHierarchy([NUnit.Framework.Range(0, 355, 5)] int degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            Vector2 aim = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
            RangedWeaponAimPose pose = Resolve(aim);

            var pivot = new GameObject("Pivot").transform;
            var visual = new GameObject("Visual").transform;
            try
            {
                pivot.localPosition = Anchor;
                pivot.localRotation = Quaternion.Euler(0f, 0f, pose.PivotAngleDegrees);
                pivot.localScale = new Vector3(1f, pose.Mirrored ? -1f : 1f, 1f);
                visual.SetParent(pivot, false);
                visual.localPosition = pose.WeaponLocalPosition;
                visual.localRotation = Quaternion.Euler(0f, 0f, pose.WeaponAngleCorrection);
                visual.localScale = new Vector3(Scale.x, Scale.y, 1f);

                AssertVector(pose.MainHandPosition, visual.TransformPoint(Grip));
                AssertVector(pose.SecondaryHandPosition, visual.TransformPoint(SecondaryGrip));
            }
            finally
            {
                Object.DestroyImmediate(pivot.gameObject);
            }
        }

        [Test]
        public void Resolve_MainHandStaysOnTheAnchor([NUnit.Framework.Range(0, 355, 15)] int degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;

            RangedWeaponAimPose pose = Resolve(new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)));

            AssertVector(pose.MainHandPosition, Anchor);
        }

        [Test]
        public void Resolve_HandSeparationIsRigidForEveryAim([NUnit.Framework.Range(0, 355, 15)] int degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float expected = Vector2.Distance(
                Vector2.Scale(Grip, Scale),
                Vector2.Scale(SecondaryGrip, Scale));

            RangedWeaponAimPose pose = Resolve(new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)));

            Assert.That(
                Vector2.Distance(pose.MainHandPosition, pose.SecondaryHandPosition),
                Is.EqualTo(expected).Within(Tolerance));
        }

        [TestCase(0f, 1f, false)]
        [TestCase(1f, 0f, true)]
        [TestCase(-1f, 0f, true)]
        [TestCase(0f, -1f, true)]
        [TestCase(0.7f, 0.0001f, false)]
        [TestCase(-0.7f, -0.0001f, true)]
        public void Resolve_FrontFacingFollowsTheVerticalAimWithFrontalTie(float x, float y, bool expected)
        {
            Assert.That(Resolve(new Vector2(x, y).normalized).FrontFacing, Is.EqualTo(expected));
        }

        [Test]
        public void Resolve_FrontFacingAgreesWithTheBucketResolver([NUnit.Framework.Range(0, 355, 5)] int degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            Vector2 aim = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));

            bool bucketFront = CharacterVisualDirectionResolver.IsFrontFacing(
                CharacterVisualDirectionResolver.Resolve(aim));

            Assert.That(Resolve(aim).FrontFacing, Is.EqualTo(bucketFront), $"{degrees} degrees");
        }

        private static RangedWeaponAimPose Resolve(Vector2 aim) =>
            RangedWeaponAimPoseMath.Resolve(aim, Anchor, Grip, SecondaryGrip, AngleCorrection, Scale);

        private static void AssertVector(Vector2 actual, Vector2 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(Tolerance));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(Tolerance));
        }
    }
}
