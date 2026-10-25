#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.AttackAnimationValidator
{
    public sealed class AttackPreviewValidatorTests
    {
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void RealCatalog_BackAttacksHaveNoDepthOrderViolations(int facing)
        {
            var cells = AttackPreviewCatalog.Enumerate().Where(c => c.FacingIndex == facing &&
                c.Action != AttackPreviewAction.Defend &&
                (c.MainHand == null || c.MainHand.WeaponDefinition == null ||
                 c.MainHand.WeaponDefinition.Handedness == WeaponHandedness.OneHanded)).ToArray();
            Assert.That(cells.Any(c => c.MainHand != null && c.OffHand == null), Is.True, "Main-only coverage");
            Assert.That(cells.Any(c => c.MainHand == null && c.OffHand != null), Is.True, "Off-only coverage");
            Assert.That(cells.Any(c => c.MainHand?.WeaponDefinition != null && c.OffHand?.WeaponDefinition != null), Is.True, "Dual-wield coverage");
            Assert.That(cells.Any(c => c.MainHand?.Category == LootCategory.Shield || c.OffHand?.Category == LootCategory.Shield), Is.True, "Shield + weapon coverage");
            AssertCatalogHasNoViolations(cells, AttackPreviewCheckId.DepthOrder);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void RealCatalog_DefendNeverOccludesWeaponHoldingHand(int facing)
        {
            var cells = AttackPreviewCatalog.Enumerate().Where(c => c.FacingIndex == facing &&
                c.Action == AttackPreviewAction.Defend).ToArray();
            Assert.That(cells.Any(c => c.MainHand?.Category == LootCategory.Shield && c.OffHand?.WeaponDefinition != null), Is.True);
            Assert.That(cells.Any(c => c.OffHand?.Category == LootCategory.Shield && c.MainHand?.WeaponDefinition != null), Is.True);
            AssertCatalogHasNoViolations(cells, AttackPreviewCheckId.HandOccludedByWeapon);
        }

        [TestCase(0, false)]
        [TestCase(1, false)]
        [TestCase(2, false)]
        [TestCase(3, false)]
        [TestCase(4, false)]
        [TestCase(5, false)]
        [TestCase(0, true)]
        [TestCase(1, true)]
        [TestCase(2, true)]
        [TestCase(3, true)]
        [TestCase(4, true)]
        [TestCase(5, true)]
        public void ShieldDefend_RealCatalogChecksHoldingGrip(int facing, bool off)
        {
            var cells = AttackPreviewCatalog.Enumerate().Where(c => c.FacingIndex == facing &&
                c.Action == AttackPreviewAction.Defend &&
                (off ? c.OffHand : c.MainHand)?.Category == LootCategory.Shield).ToArray();
            Assert.That(cells, Is.Not.Empty);
            using var rig = new AttackPreviewRig();
            var validator = new AttackPreviewValidator();
            foreach (var cell in cells)
            {
                var report = validator.Validate(rig, cell);
                var hand = off ? AttackPreviewHand.Off : AttackPreviewHand.Main;
                Assert.That(report.WorstChecks.Any(c => c.Id == AttackPreviewCheckId.WeaponGripOnHand && c.Hand == hand),
                    Is.True, "Shield grip measurement must be applicable: " + cell.DisplayName);
                Assert.That(report.FailureRanges.Where(c => c.Hand == hand &&
                    (c.Id == AttackPreviewCheckId.WeaponGripOnHand || c.Id == AttackPreviewCheckId.GripTransformOnHand)),
                    Is.Empty, cell.DisplayName);
            }
        }

        private static void AssertCatalogHasNoViolations(AttackPreviewCombination[] cells, AttackPreviewCheckId id)
        {
            Assert.That(cells, Is.Not.Empty);
            using var rig = new AttackPreviewRig();
            var validator = new AttackPreviewValidator();
            foreach (var cell in cells)
            {
                Assert.That(cell.MissingPresentation, Is.False, cell.DisplayName);
                var report = validator.Validate(rig, cell);
                Assert.That(report.FailureRanges.Where(range => range.Id == id), Is.Empty, cell.DisplayName);
            }
        }

        private static AttackPreviewCombination Cell() => new AttackPreviewCombination(
            AttackPreviewCatalogTests.Sword, null, AttackPreviewAction.MainAttack, 3);
        private static AttackPreviewCheck Failure(float time, float value = 4) => new AttackPreviewCheck(
            AttackPreviewCheckId.WeaponGripOnHand, AttackPreviewSeverity.Error, AttackPreviewHand.Main,
            time, value, 3, "Weapon grip is displaced from the drawn hand.");

        [Test]
        public void Report_MergesConsecutiveFailuresButNotAcrossCleanFrame()
        {
            var report = new AttackPreviewReport(Cell());
            report.AddSample(0, new[] { Failure(0) });
            report.AddSample(0.1f, new[] { Failure(0.1f, 6) });
            report.AddSample(0.2f, Array.Empty<AttackPreviewCheck>());
            report.AddSample(0.3f, new[] { Failure(0.3f) });
            Assert.That(report.SampleCount, Is.EqualTo(4));
            Assert.That(report.EndSeconds, Is.EqualTo(0.3f));
            Assert.That(report.Outcome, Is.EqualTo(AttackPreviewOutcome.Fail));
            Assert.That(report.FailureRanges.Count, Is.EqualTo(2));
            Assert.That(report.FailureRanges[0].StartSeconds, Is.Zero);
            Assert.That(report.FailureRanges[0].EndSeconds, Is.EqualTo(0.1f));
            Assert.That(report.FailureRanges[1].StartSeconds, Is.EqualTo(0.3f));
            Assert.That(report.FailureRanges[1].EndSeconds, Is.EqualTo(0.3f));
            Assert.That(report.WorstChecks.Single().MeasuredValue, Is.EqualTo(6));
        }
        [Test]
        public void Report_KeepsHandsAndSeveritiesSeparate()
        {
            var report = new AttackPreviewReport(Cell());
            report.AddSample(0, new[] { Failure(0) });
            report.AddSample(0.1f, new[] { new AttackPreviewCheck(AttackPreviewCheckId.WeaponGripOnHand,
                AttackPreviewSeverity.Warning, AttackPreviewHand.Off, 0.1f, 2, 1.5f, "Off hand offset.") });
            Assert.That(report.FailureRanges.Count, Is.EqualTo(2));
            Assert.That(report.WorstChecks.Count, Is.EqualTo(2));
        }
        [TestCase(false)]
        [TestCase(true)]
        public void Report_DistinguishesPassAndWarning(bool warning)
        {
            var report = new AttackPreviewReport(Cell());
            report.AddSample(0, warning ? new[] { new AttackPreviewCheck(AttackPreviewCheckId.VfxOrigin,
                AttackPreviewSeverity.Warning, AttackPreviewHand.Main, 0, 2, 1, "VFX absent.") }
                : Array.Empty<AttackPreviewCheck>());
            Assert.That(report.Outcome, Is.EqualTo(warning ? AttackPreviewOutcome.Warning : AttackPreviewOutcome.Pass));
        }
        [Test]
        public void Report_RetainsPassingMeasurementsWithoutCreatingFailureRanges()
        {
            var report = new AttackPreviewReport(Cell());
            report.AddSample(0, new[] { new AttackPreviewCheck(AttackPreviewCheckId.WeaponGripOnHand,
                AttackPreviewSeverity.Warning, AttackPreviewHand.Main, 0, 0.5f, 1.5f, "Within tolerance.", false) });
            Assert.That(report.WorstChecks.Single().MeasuredValue, Is.EqualTo(0.5f));
            Assert.That(report.WorstChecks.Single().IsViolation, Is.False);
            Assert.That(report.Outcome, Is.EqualTo(AttackPreviewOutcome.Pass));
            Assert.That(report.FailureRanges, Is.Empty);
        }
        [Test]
        public void MainArmingSwordSouth_CoversEntireDurationAtDefaultRateWithoutExceptions()
        {
            using var rig = new AttackPreviewRig();
            var settings = new AttackPreviewCheckSettings();
            AttackPreviewReport report = null;
            Assert.DoesNotThrow(() => report = new AttackPreviewValidator(settings).Validate(rig, Cell()));
            float end = rig.Duration + settings.TailSeconds;
            Assert.That(report.Combination.Id, Is.EqualTo(Cell().Id));
            Assert.That(report.EndSeconds, Is.EqualTo(end).Within(0.00001f));
            Assert.That(report.SampleCount, Is.EqualTo(Mathf.CeilToInt(end * settings.SampleRate) + 1));
            // Deliberately no assertion about actual art passing or failing.
        }
        [Test]
        public void Batch_CancellationBeforeFirstCellProducesNoReports()
        {
            int calls = 0;
            // Invalid sentinel cells are intentional: cancellation must happen BEFORE any cell is
            // inspected/configured. Ignoring cancellation either throws or produces unwanted reports.
            System.Collections.Generic.IReadOnlyList<AttackPreviewReport> reports = null;
            Assert.DoesNotThrow(() => reports = new AttackPreviewValidator().ValidateAll(
                new AttackPreviewCombination[] { null, null }, (index, total) =>
                {
                    calls++;
                    Assert.That(index, Is.Zero);
                    Assert.That(total, Is.EqualTo(2));
                    return false;
                }));
            Assert.That(calls, Is.EqualTo(1), "The cancellation delegate must actually be consulted.");
            Assert.That(reports, Is.Not.Null.And.Empty);
        }
        [Test]
        public void Batch_CancellationRetainsOnlyCompletedCells()
        {
            int calls = 0;
            var reports = new AttackPreviewValidator().ValidateAll(new[] { Cell(), Cell() }, (index, total) =>
            { calls++; return index < 1; });
            Assert.That(calls, Is.EqualTo(2));
            Assert.That(reports.Count, Is.EqualTo(1));
        }
    }
}
#endif
