#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using NUnit.Framework;

namespace Tests.EditMode.Combat
{
    public sealed class AttackTimingTests
    {
        [TestCase(0f, 0.02f, 100)]
        [TestCase(0.4f, 0.02f, 120)]
        [TestCase(0.401f, 0.02f, 121)]
        [TestCase(0.45f, 0.02f, 123)]
        public void ReleaseTick_RoundsUpWithoutEarlyRelease(float seconds, float delta, int expected)
        {
            Assert.That(AttackTiming.TryGetReleaseTick(100, seconds, delta, out int tick), Is.True);
            Assert.That(tick, Is.EqualTo(expected));
        }

        [TestCase(float.NaN, 0.02f)]
        [TestCase(float.PositiveInfinity, 0.02f)]
        [TestCase(-1f, 0.02f)]
        [TestCase(0.4f, 0f)]
        [TestCase(0.4f, float.NaN)]
        public void ReleaseTick_RejectsInvalidTiming(float seconds, float delta) =>
            Assert.That(AttackTiming.TryGetReleaseTick(100, seconds, delta, out _), Is.False);

        [Test]
        public void ReleaseTick_RejectsOverflow() =>
            Assert.That(AttackTiming.TryGetReleaseTick(int.MaxValue, 1f, 0.02f, out _), Is.False);

        [Test]
        public void DelayedObservation_UsesConfirmedElapsedPhaseNotReceiptTime()
        {
            float elapsed = AttackTiming.ElapsedSeconds(2.3, 100, 0.02f);
            Assert.That(elapsed, Is.EqualTo(0.3f).Within(0.00001f));
            Assert.That(AttackTiming.ClipSeconds(elapsed, 0.46f, 0.45f),
                Is.EqualTo(0.3f * 0.45f / 0.46f).Within(0.00001f));
        }

        [Test]
        public void RoundedDeadline_AlignsAuthoredReleaseAndPreservesRecoverySpeed()
        {
            Assert.That(AttackTiming.ClipSeconds(0.46f, 0.46f, 0.45f), Is.EqualTo(0.45f));
            Assert.That(AttackTiming.ClipSeconds(0.66f, 0.46f, 0.45f), Is.EqualTo(0.65f).Within(0.00001f));
            Assert.That(AttackTiming.ClipSeconds(0.2f, 0f, 0f), Is.EqualTo(0.2f));
        }

        [Test]
        public void AimedClipSeconds_WindsUpFromTheDrawnFrameToTheAuthoredRelease()
        {
            // 0.1 s aimed delay, drawn frame at 0.44 s, authored release at 0.45 s.
            Assert.That(AttackTiming.AimedClipSeconds(0f, 0.1f, 0.45f, 0.44f), Is.EqualTo(0.44f).Within(0.00001f));
            Assert.That(AttackTiming.AimedClipSeconds(0.05f, 0.1f, 0.45f, 0.44f), Is.EqualTo(0.445f).Within(0.00001f));
            Assert.That(AttackTiming.AimedClipSeconds(0.1f, 0.1f, 0.45f, 0.44f), Is.EqualTo(0.45f).Within(0.00001f));
        }

        [Test]
        public void AimedClipSeconds_KeepsTheRecoverySpeedAfterTheRelease()
        {
            Assert.That(AttackTiming.AimedClipSeconds(0.3f, 0.1f, 0.45f, 0.44f), Is.EqualTo(0.65f).Within(0.00001f));
        }

        [Test]
        public void AimedClipSeconds_NeverPlaysBeforeTheDrawnFrame()
        {
            for (float elapsed = -0.05f; elapsed < 0.6f; elapsed += 0.01f)
            {
                Assert.That(AttackTiming.AimedClipSeconds(elapsed, 0.1f, 0.45f, 0.44f),
                    Is.GreaterThanOrEqualTo(0.44f - 0.00001f), elapsed.ToString());
            }
        }

        [Test]
        public void AimedClipSeconds_WithNoWindupIsTheRecoveryMapping()
        {
            Assert.That(AttackTiming.AimedClipSeconds(0.2f, 0f, 0.45f, 0.44f),
                Is.EqualTo(AttackTiming.ClipSeconds(0.2f, 0f, 0.45f)));
        }

        [TestCase(0f)]
        [TestCase(0.025f)]
        public void RangedVfxPhase_IsRelativeToRoundedReleaseWithExplicitArtLead(float lead)
        {
            Assert.That(AttackTiming.ReleaseVfxSeconds(0.46f, 0.46f, lead), Is.EqualTo(lead));
            Assert.That(AttackTiming.ReleaseVfxSeconds(0.46f - lead, 0.46f, lead), Is.EqualTo(0f).Within(0.00001f));
            Assert.That(AttackTiming.ReleaseVfxSeconds(0.6f, 0.46f, lead), Is.EqualTo(0.14f + lead).Within(0.00001f));
        }

        [Test]
        public void RemoteTimelineAheadOfReceipt_DoesNotStartBeforeAcceptance() =>
            Assert.That(AttackTiming.ElapsedSeconds(1.9, 100, 0.02f), Is.LessThan(0f));
    }
}
#endif
