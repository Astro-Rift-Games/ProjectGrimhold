#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.AttackAnimationValidator
{
    public sealed class AttackPreviewHoldingContractTests
    {
        [Test]
        public void WeaponDrivenPreview_AllowsRuntimeReparentingAndUsesLeftHolder()
        {
            using var rig = new AttackPreviewRig();
            foreach (var cell in AttackPreviewCatalog.Enumerate().Where(c => c.OffHand == null &&
                c.Action == AttackPreviewAction.MainAttack && c.MainHand.WeaponDefinition.Presentation.Rig == WeaponRig.WeaponDriven))
            {
                rig.Configure(cell);
                Assert.That(PrefabUtility.IsPartOfPrefabInstance(rig.Root), Is.False,
                    "Preview objects must permit the same hierarchy changes as runtime clones.");
                rig.Evaluate(rig.Duration * 0.5f);
                Assert.That(rig.WeaponRenderers[0].transform.IsChildOf(rig.VisualRoot.Find("WeaponPose")), Is.True, cell.DisplayName);
                using var checks = new AttackPreviewChecks();
                var expected = checks.WeaponGripWorldPoint(rig.WeaponRenderers[0], cell.MainHand.WeaponDefinition.Presentation.GripPoint);
                Assert.That(Vector3.Distance(expected, checks.DrawnHandWorldPoint(rig.HandRenderers[1])) *
                    rig.HandRenderers[1].sprite.pixelsPerUnit, Is.LessThan(1.5f), cell.DisplayName);
            }
        }

        [Test]
        public void RuntimeStyleEquipmentChange_ReparentsAnOrdinaryCloneWithoutReinitializingPresenter()
        {
            using var rig = new AttackPreviewRig();
            var sword = AttackPreviewCatalog.Enumerate().First(c => c.MainHand != null && c.MainHand.name == "ArmingSword" &&
                c.OffHand == null && c.Action == AttackPreviewAction.MainAttack && c.FacingIndex == 0);
            var bow = AttackPreviewCatalog.Enumerate().First(c => c.MainHand != null && c.MainHand.name == "LongBow" &&
                c.OffHand == null && c.Action == AttackPreviewAction.MainAttack && c.FacingIndex == 0);
            rig.Configure(sword);
            rig.Evaluate(rig.Duration + .2f);
            var state = (PreviewPlayerPresentationState)typeof(AttackPreviewRig).GetField("_state",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(rig);
            var presenter = rig.Root.GetComponentInChildren<PlayerWeaponPresenter>(true);
            state.Configure(bow);
            presenter.BeginFrame();
            presenter.PresentFrame();
            Assert.That(rig.WeaponRenderers[0].transform.IsChildOf(rig.VisualRoot.Find("WeaponPose")), Is.True);
            state.Configure(sword);
            presenter.BeginFrame();
            presenter.PresentFrame();
            Assert.That(rig.WeaponRenderers[0].transform.IsChildOf(rig.MainHandGrip), Is.True);
        }

        [Test]
        public void SecondaryGrip_RemainsOnDrawnLeftHandAfterAttackInEveryFacing()
        {
            using var rig = new AttackPreviewRig();
            using var checks = new AttackPreviewChecks();
            foreach (var cell in AttackPreviewCatalog.Enumerate().Where(c => c.OffHand == null &&
                c.Action == AttackPreviewAction.MainAttack && c.MainHand.WeaponDefinition.Handedness == WeaponHandedness.TwoHanded &&
                c.MainHand.WeaponDefinition.Presentation.SecondHand == SecondHandPresentation.HoldsSecondaryGrip))
            {
                rig.Configure(cell);
                foreach (float seconds in new[] { rig.Duration * .5f, rig.Duration, rig.Duration + .2f })
                {
                    rig.Evaluate(seconds);
                    Vector3 expected = checks.WeaponGripWorldPoint(rig.WeaponRenderers[0], cell.MainHand.WeaponDefinition.Presentation.SecondaryGripPoint);
                    float pixels = rig.HandRenderers[1].transform.InverseTransformVector(expected -
                        checks.DrawnHandWorldPoint(rig.HandRenderers[1])).magnitude * rig.HandRenderers[1].sprite.pixelsPerUnit;
                    Assert.That(pixels, Is.LessThan(1.5f), cell.DisplayName + " at " + seconds);
                }
            }
        }

        [Test]
        public void SecondaryGrip_WalkUsesEachFramesAuthoredHandAnchorWithoutDrift()
        {
            using var rig = new AttackPreviewRig();
            using var checks = new AttackPreviewChecks();
            foreach (var cell in AttackPreviewCatalog.Enumerate().Where(c => c.OffHand == null &&
                c.Action == AttackPreviewAction.MainAttack && c.MainHand.WeaponDefinition.Handedness == WeaponHandedness.TwoHanded &&
                c.MainHand.WeaponDefinition.Presentation.SecondHand == SecondHandPresentation.HoldsSecondaryGrip))
            {
                rig.Configure(cell);
                rig.Evaluate(rig.Duration + .2f);
                var presenter = rig.Root.GetComponentInChildren<PlayerWeaponPresenter>(true);
                string direction = new[] { "N", "NE", "NW", "S", "SE", "SW" }[cell.FacingIndex];
                foreach (float time in new[] { 0f, .1f, .2f, .3f, .4f, .5f })
                {
                    presenter.BeginFrame();
                    foreach (string path in new[] { "Walk/Walk_", "Walk/RightHand/RightHand_Walk_", "Walk/LeftHand/LeftHand_Walk_" })
                        AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Player/" + path + direction + ".anim")
                            .SampleAnimation(rig.VisualRoot.gameObject, time);
                    presenter.PresentFrame();
                    Vector3 target = checks.WeaponGripWorldPoint(rig.WeaponRenderers[0], cell.MainHand.WeaponDefinition.Presentation.SecondaryGripPoint);
                    Assert.That(Vector3.Distance(target, checks.DrawnHandWorldPoint(rig.HandRenderers[1])) *
                        rig.HandRenderers[1].sprite.pixelsPerUnit, Is.LessThan(1.5f), cell.DisplayName + " walk " + time);
                    Assert.That(rig.HandRenderers[1].sortingOrder, Is.EqualTo(cell.FacingIndex < 3 ? -2 : 25));
                }
            }
        }

        [Test]
        public void WeaponDrivenDepth_ExemptsOnlyStringHandAndDetectsDisplacedHolder()
        {
            using var rig = new AttackPreviewRig();
            using var checks = new AttackPreviewChecks();
            var cell = AttackPreviewCatalog.Enumerate().First(c => c.MainHand != null && c.MainHand.name == "LongBow" &&
                c.OffHand == null && c.Action == AttackPreviewAction.MainAttack && c.FacingIndex == 0);
            rig.Configure(cell);
            using var source = new AttackPreviewFrameSource(rig, cell);
            var holder = source.Capture(1, 0, false);
            var strings = source.Capture(0, 0, false);
            Assert.That(holder.CheckHeldItemDepth, Is.True);
            Assert.That(holder.CheckWeaponGrip, Is.True);
            Assert.That(strings.CheckHeldItemDepth, Is.False);
            Assert.That(strings.CheckWeaponGrip, Is.False);
            Assert.That(checks.Evaluate(holder).Any(c => c.Id == AttackPreviewCheckId.DepthOrder), Is.False);
            rig.HandRenderers[1].sortingOrder = 50;
            Assert.That(checks.Evaluate(holder).Any(c => c.Id == AttackPreviewCheckId.DepthOrder), Is.True);
            Assert.That(checks.Evaluate(strings).Any(c => c.Id == AttackPreviewCheckId.DepthOrder), Is.False);
        }

        [Test]
        public void ReleaseOrigin_DetectsDisplacementFromAuthoredPose()
        {
            using var rig = new AttackPreviewRig();
            using var checks = new AttackPreviewChecks();
            var cell = AttackPreviewCatalog.Enumerate().First(c => c.MainHand != null && c.MainHand.name == "Spellbook" &&
                c.OffHand == null && c.Action == AttackPreviewAction.MainAttack && c.FacingIndex == 3);
            rig.Configure(cell);
            rig.Evaluate(.7f);
            using var source = new AttackPreviewFrameSource(rig, cell);
            var frame = source.Capture(0, .7f, false);
            Assert.That(frame.ExpectedVfxWorldOrigin.HasValue, Is.True);
            Assert.That(frame.VfxRenderer.enabled && frame.VfxRenderer.sprite != null, Is.True);
            Assert.That(checks.Evaluate(frame).Any(c => c.Id == AttackPreviewCheckId.VfxOrigin), Is.False);
            frame.VfxRenderer.transform.position += Vector3.right;
            Assert.That(checks.Evaluate(frame).Any(c => c.Id == AttackPreviewCheckId.VfxOrigin), Is.True);
        }

        [Test]
        public void ReleaseAndCastOrigins_AcceptTheirResolvedAuthoredPoses()
        {
            using var rig = new AttackPreviewRig();
            foreach (var cell in AttackPreviewCatalog.Enumerate().Where(c => c.OffHand == null &&
                c.Action == AttackPreviewAction.MainAttack && new[] { "LongBow", "CompoundBow", "LightCrossbow", "Spellbook" }.Contains(c.MainHand.name)))
            {
                var report = new AttackPreviewValidator().Validate(rig, cell);
                Assert.That(report.WorstChecks.Where(c => c.Id == AttackPreviewCheckId.VfxOrigin && c.IsViolation), Is.Empty, cell.DisplayName);
            }
        }
    }
}
#endif
