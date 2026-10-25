#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Linq;
using NUnit.Framework;

namespace Tests.EditMode.AttackAnimationValidator
{
    public sealed class AttackPreviewAnimatedIdleTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void SequentialTail_AnimatedIdleOnEmptyNonAttackingHand_HasNoIdleReturnViolation(bool offAttack)
        {
            var sword = AttackPreviewCatalogTests.Sword;
            var cell = new AttackPreviewCombination(offAttack ? null : sword, offAttack ? sword : null,
                offAttack ? AttackPreviewAction.OffAttack : AttackPreviewAction.MainAttack, 3);
            using var rig = new AttackPreviewRig();
            rig.Configure(cell, sequential: true);
            int idleHand = offAttack ? 0 : 1;
            var initialSprite = rig.HandRenderers[idleHand].sprite;
            Assert.That(initialSprite, Is.Not.Null);
            int layer = rig.Animator.GetLayerIndex(offAttack ? "RightHand" : "LeftHand");
            Assert.That(layer, Is.GreaterThanOrEqualTo(0));

            // Establish that this real fixture reaches a different idle frame AFTER the attack.
            // Pick a grid-aligned endpoint dynamically instead of hardcoding an art-specific time.
            float observedTime = -1;
            for (int sample = 1; sample <= UnityEngine.Mathf.CeilToInt((rig.Duration + 1.1f) * 60); sample++)
            {
                float seconds = sample / 60f;
                rig.Evaluate(seconds);
                if (seconds <= rig.Duration || rig.HandRenderers[idleHand].sprite == initialSprite) continue;
                var clips = rig.Animator.GetCurrentAnimatorClipInfo(layer);
                Assert.That(clips.Any(c => c.clip != null && c.clip.name.Contains("Idle")), Is.True,
                    "The differing sprite must come from a playing idle clip, not a leftover attack.");
                Assert.That(rig.Animator.GetCurrentAnimatorStateInfo(layer).normalizedTime, Is.GreaterThan(0));
                observedTime = seconds;
                break;
            }
            Assert.That(observedTime, Is.GreaterThan(rig.Duration), "Fixture must exercise a later animated idle frame in the cleanup tail.");

            var report = new AttackPreviewValidator(new AttackPreviewCheckSettings
            {
                SampleRate = 60,
                TailSeconds = observedTime - rig.Duration + 1f / 60
            }).Validate(rig, cell);
            var hand = offAttack ? AttackPreviewHand.Main : AttackPreviewHand.Off;
            Assert.That(report.EndSeconds, Is.GreaterThanOrEqualTo(observedTime));
            Assert.That(report.WorstChecks.Where(c => c.Id == AttackPreviewCheckId.ReturnsToIdle && c.Hand == hand && c.IsViolation),
                Is.Empty, "An empty non-attacking hand following its animated idle must not be compared against idle frame zero.");
            Assert.That(report.FailureRanges.Where(r => r.Id == AttackPreviewCheckId.ReturnsToIdle && r.Hand == hand), Is.Empty);
        }
    }
}
#endif
