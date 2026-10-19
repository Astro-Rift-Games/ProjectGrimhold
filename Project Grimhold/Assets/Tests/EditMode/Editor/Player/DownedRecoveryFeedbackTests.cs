using NUnit.Framework;

namespace Tests.EditMode.Player
{
    public sealed class DownedRecoveryFeedbackTests
    {
        [TestCase(4f, 4f, 0f)]
        [TestCase(2f, 4f, 0.5f)]
        [TestCase(0f, 4f, 1f)]
        [TestCase(-1f, 4f, 1f)]
        [TestCase(9f, 4f, 0f)]
        [TestCase(1f, 0f, 0f)]
        [TestCase(float.NaN, 4f, 0f)]
        [TestCase(1f, float.PositiveInfinity, 0f)]
        public void ProgressFraction_IsClampedAndSanitized(float remaining, float duration, float expected)
        {
            Assert.That(DownedRecoveryFeedbackRules.ProgressFraction(remaining, duration), Is.EqualTo(expected).Within(1e-5f));
        }

        [TestCase(75f, 75f, 1f)]
        [TestCase(30f, 60f, 0.5f)]
        [TestCase(-5f, 75f, 0f)]
        [TestCase(10f, 0f, 0f)]
        [TestCase(float.NaN, 75f, 0f)]
        public void ReserveFraction_IsClampedAndSanitized(float health, float initial, float expected)
        {
            Assert.That(DownedRecoveryFeedbackRules.ReserveFraction(health, initial), Is.EqualTo(expected).Within(1e-5f));
        }

        [TestCase(DownedRecoveryInterruptReason.InteractReleased)]
        [TestCase(DownedRecoveryInterruptReason.ReviverMoved)]
        [TestCase(DownedRecoveryInterruptReason.DownedMoved)]
        [TestCase(DownedRecoveryInterruptReason.OutOfRange)]
        [TestCase(DownedRecoveryInterruptReason.ReviverDamaged)]
        [TestCase(DownedRecoveryInterruptReason.DownedDamaged)]
        [TestCase(DownedRecoveryInterruptReason.IncompatibleReviverAction)]
        [TestCase(DownedRecoveryInterruptReason.ReviverDowned)]
        [TestCase(DownedRecoveryInterruptReason.ReviverDisconnected)]
        public void InterruptionMessage_ExistsForEveryShownCause(DownedRecoveryInterruptReason reason)
        {
            Assert.That(DownedRecoveryFeedbackRules.InterruptionMessage(reason), Is.Not.Null.And.Not.Empty);
        }

        [TestCase(DownedRecoveryInterruptReason.None)]
        [TestCase(DownedRecoveryInterruptReason.DownedDefeated)]
        public void InterruptionMessage_IsNullWhenNothingIsShown(DownedRecoveryInterruptReason reason)
        {
            Assert.That(DownedRecoveryFeedbackRules.InterruptionMessage(reason), Is.Null);
        }

        [Test]
        public void MessageFor_MapsOutcomeKinds()
        {
            Assert.That(
                DownedRecoveryFeedbackRules.MessageFor(RecoveryOutcomeKind.Completed, DownedRecoveryInterruptReason.None),
                Is.EqualTo(DownedRecoveryFeedbackRules.CompletedMessage));
            Assert.That(
                DownedRecoveryFeedbackRules.MessageFor(RecoveryOutcomeKind.Interrupted, DownedRecoveryInterruptReason.OutOfRange),
                Is.Not.Null);
            Assert.That(
                DownedRecoveryFeedbackRules.MessageFor(RecoveryOutcomeKind.None, DownedRecoveryInterruptReason.None),
                Is.Null);
        }

        [Test]
        public void Tracker_FirstSampleIsSilent()
        {
            var tracker = new DownedRecoveryOutcomeTracker();
            Assert.That(tracker.Observe(5), Is.False);
            Assert.That(tracker.Observe(5), Is.False);
        }

        [Test]
        public void Tracker_ReportsEachAdvanceOnce()
        {
            var tracker = new DownedRecoveryOutcomeTracker();
            tracker.Baseline(2);
            Assert.That(tracker.Observe(3), Is.True);
            Assert.That(tracker.Observe(3), Is.False);
            Assert.That(tracker.Observe(4), Is.True);
        }

        [Test]
        public void Tracker_BackwardsSequenceIsAdoptedSilently()
        {
            var tracker = new DownedRecoveryOutcomeTracker();
            tracker.Baseline(7);
            Assert.That(tracker.Observe(1), Is.False);
            Assert.That(tracker.Observe(2), Is.True);
        }

        [Test]
        public void Tracker_ResetAdoptsNextSampleSilently()
        {
            var tracker = new DownedRecoveryOutcomeTracker();
            tracker.Baseline(1);
            tracker.Reset();
            Assert.That(tracker.Observe(9), Is.False);
        }
    }
}
