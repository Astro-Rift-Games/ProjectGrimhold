#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.EditMode.AttackAnimationValidator
{
    public sealed class AttackPreviewValidationContractTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static AttackPreviewCombination MainCell() => new AttackPreviewCombination(
            AttackPreviewCatalogTests.Sword, null, AttackPreviewAction.MainAttack, 3);

        [Test]
        public void SequentialValidation_StartsAttackOnceAndPreservesPresenterStateThroughTail()
        {
            using var rig = new AttackPreviewRig();
            var state = (PreviewPlayerPresentationState)typeof(AttackPreviewRig)
                .GetField("_state", PrivateInstance).GetValue(rig);
            var presenter = rig.Root.GetComponentInChildren<PlayerWeaponPresenter>(true);
            var markerField = typeof(PlayerWeaponPresenter).GetField("_offHandSampler", PrivateInstance);
            using var overlay = new OneHandPoseSampler();
            var controllers = new HashSet<RuntimeAnimatorController>();
            int attackStarts = 0;
            bool initialized = false, markerWasErased = false;
            // A synthetic presenter-owned overlay resource, disposed by ReleasePresentation but
            // retained by ordinary frames. Observe identity before reinjection so resets cannot hide.
            Action<AttackPerformedEvent> observe = attack =>
            {
                if (initialized && markerField.GetValue(presenter) == null) markerWasErased = true;
                markerField.SetValue(presenter, overlay);
                initialized = true;
                attackStarts++;
                controllers.Add(rig.Animator.runtimeAnimatorController);
            };
            state.AttackPerformed += observe;
            try
            {
                var settings = new AttackPreviewCheckSettings { SampleRate = 20, TailSeconds = 0.3f };
                var report = new AttackPreviewValidator(settings).Validate(rig, MainCell());
                Assert.That(report.EndSeconds, Is.GreaterThan(rig.Duration), "The observation must include the cleanup tail.");
                Assert.That(markerWasErased, Is.False, "Validation must not call ReleasePresentation between forward samples.");
                Assert.That(markerField.GetValue(presenter), Is.SameAs(overlay), "Presenter-owned overlay resources must survive through the tail.");
                Assert.That(controllers.Count, Is.EqualTo(1), "Validation must not create a fresh override controller for every sample.");
                Assert.That(attackStarts, Is.EqualTo(1), "One configured attack is delivered once, not restarted at every sample.");
            }
            finally { state.AttackPerformed -= observe; }
        }

        [Test]
        public void MissingOffHandSet_ProducesExplicitPresentationConfigurationError()
        {
            var loot = Object.Instantiate(AttackPreviewCatalogTests.Sword);
            var weapon = Object.Instantiate(AttackPreviewCatalogTests.Sword.WeaponDefinition);
            try
            {
                using (var data = new SerializedObject(loot))
                {
                    data.FindProperty("_weaponDefinition").objectReferenceValue = weapon;
                    data.ApplyModifiedPropertiesWithoutUndo();
                }
                using (var data = new SerializedObject(weapon))
                {
                    data.FindProperty("_presentation").FindPropertyRelative("_offHandAttackAnimationSet").objectReferenceValue = null;
                    data.ApplyModifiedPropertiesWithoutUndo();
                }
                var cell = new AttackPreviewCombination(null, loot, AttackPreviewAction.OffAttack, 3);
                Assert.That(cell.MissingPresentation, Is.True, "Fixture must retain catalog eligibility but lack Off Hand presentation.");
                using var rig = new AttackPreviewRig();
                var report = new AttackPreviewValidator(new AttackPreviewCheckSettings { SampleRate = 5 }).Validate(rig, cell);
                // String comparison lets this behavior test compile before the diagnostic enum is added.
                var diagnostic = report.WorstChecks.SingleOrDefault(c => c.Id.ToString() == "PresentationConfiguration");
                Assert.That(diagnostic, Is.Not.Null, "Missing presentation needs an explicit configuration diagnostic, not incidental grip/depth failures.");
                Assert.That(diagnostic.IsViolation, Is.True);
                Assert.That(diagnostic.Severity, Is.EqualTo(AttackPreviewSeverity.Error));
                Assert.That(diagnostic.Hand, Is.EqualTo(AttackPreviewHand.Off));
                Assert.That(diagnostic.Message, Is.Not.Empty);
                Assert.That(report.Outcome, Is.EqualTo(AttackPreviewOutcome.Fail));
                LogAssert.NoUnexpectedReceived();
            }
            finally { Object.DestroyImmediate(loot); Object.DestroyImmediate(weapon); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void WorstChecks_SmallerViolationOutranksLargerPassInEitherOrder(bool violationFirst)
        {
            var report = new AttackPreviewReport(MainCell());
            var pass = new AttackPreviewCheck(AttackPreviewCheckId.ReturnsToIdle, AttackPreviewSeverity.Warning,
                AttackPreviewHand.Main, violationFirst ? 0.1f : 0, 1.4f, 1.5f, "Position is within tolerance (pixels).", false);
            var violation = new AttackPreviewCheck(AttackPreviewCheckId.ReturnsToIdle, AttackPreviewSeverity.Error,
                AttackPreviewHand.Main, violationFirst ? 0 : 0.1f, 0.2f, 0.1f, "Residual rotation (degrees).");
            report.AddSample(0, new[] { violationFirst ? violation : pass });
            report.AddSample(0.1f, new[] { violationFirst ? pass : violation });
            Assert.That(report.WorstChecks.Single(), Is.SameAs(violation));
            Assert.That(report.Outcome, Is.EqualTo(AttackPreviewOutcome.Fail));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void WorstChecks_ErrorOutranksLargerWarningInEitherOrder(bool errorFirst)
        {
            var report = new AttackPreviewReport(MainCell());
            var warning = new AttackPreviewCheck(AttackPreviewCheckId.ReturnsToIdle, AttackPreviewSeverity.Warning,
                AttackPreviewHand.Main, errorFirst ? 0.1f : 0, 5, 3, "Position warning (pixels).");
            var error = new AttackPreviewCheck(AttackPreviewCheckId.ReturnsToIdle, AttackPreviewSeverity.Error,
                AttackPreviewHand.Main, errorFirst ? 0 : 0.1f, 1, 0, "Residual sprite or overlay (boolean).");
            report.AddSample(0, new[] { errorFirst ? error : warning });
            report.AddSample(0.1f, new[] { errorFirst ? warning : error });
            Assert.That(report.WorstChecks.Single(), Is.SameAs(error));
        }
    }
}
#endif
