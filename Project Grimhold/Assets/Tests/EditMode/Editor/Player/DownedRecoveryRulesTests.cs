using NUnit.Framework;

namespace Tests.EditMode.Player
{
    public sealed class DownedRecoveryRulesTests
    {
        private const float Range = 1.5f;

        private static DownedRecoveryInterruptionSnapshot Valid()
        {
            return new DownedRecoveryInterruptionSnapshot(
                reviverInteractHeld: true,
                reviverMoving: false,
                downedMoving: false,
                distance: 1f,
                range: Range,
                previousReviverHealth: 100f,
                currentReviverHealth: 100f,
                reviverDowned: false,
                reviverConnected: true,
                downedDamaged: false,
                incompatibleReviverAction: false,
                downedIsDowned: true,
                sessionCycle: 1,
                downedCycle: 1);
        }

        [Test]
        public void EvaluateInterruption_ValidSession_ReturnsNone()
        {
            Assert.That(
                DownedRecoveryRules.EvaluateInterruption(Valid()),
                Is.EqualTo(DownedRecoveryInterruptReason.None));
        }

        [Test]
        public void EvaluateInterruption_InteractReleased()
        {
            DownedRecoveryInterruptionSnapshot s = Valid().With(reviverInteractHeld: false);
            Assert.That(DownedRecoveryRules.EvaluateInterruption(s),
                Is.EqualTo(DownedRecoveryInterruptReason.InteractReleased));
        }

        [Test]
        public void EvaluateInterruption_ReviverMoving()
        {
            DownedRecoveryInterruptionSnapshot s = Valid().With(reviverMoving: true);
            Assert.That(DownedRecoveryRules.EvaluateInterruption(s),
                Is.EqualTo(DownedRecoveryInterruptReason.ReviverMoved));
        }

        [Test]
        public void EvaluateInterruption_DownedMoving()
        {
            DownedRecoveryInterruptionSnapshot s = Valid().With(downedMoving: true);
            Assert.That(DownedRecoveryRules.EvaluateInterruption(s),
                Is.EqualTo(DownedRecoveryInterruptReason.DownedMoved));
        }

        [Test]
        public void EvaluateInterruption_ExactlyAtRange_IsStillValid()
        {
            DownedRecoveryInterruptionSnapshot s = Valid().With(distance: Range);
            Assert.That(DownedRecoveryRules.EvaluateInterruption(s),
                Is.EqualTo(DownedRecoveryInterruptReason.None));
        }

        [TestCase(1.51f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void EvaluateInterruption_OutOfRange(float distance)
        {
            DownedRecoveryInterruptionSnapshot s = Valid().With(distance: distance);
            Assert.That(DownedRecoveryRules.EvaluateInterruption(s),
                Is.EqualTo(DownedRecoveryInterruptReason.OutOfRange));
        }

        [Test]
        public void EvaluateInterruption_ReviverHealthDropped()
        {
            DownedRecoveryInterruptionSnapshot s = Valid().With(currentReviverHealth: 90f);
            Assert.That(DownedRecoveryRules.EvaluateInterruption(s),
                Is.EqualTo(DownedRecoveryInterruptReason.ReviverDamaged));
        }

        [Test]
        public void EvaluateInterruption_ReviverHealthIncrease_DoesNotInterrupt()
        {
            DownedRecoveryInterruptionSnapshot s = Valid().With(currentReviverHealth: 110f);
            Assert.That(DownedRecoveryRules.EvaluateInterruption(s),
                Is.EqualTo(DownedRecoveryInterruptReason.None));
        }

        [Test]
        public void EvaluateInterruption_ReviverDowned_TakesPrecedenceOverDamage()
        {
            DownedRecoveryInterruptionSnapshot s = Valid().With(reviverDowned: true, currentReviverHealth: 0f);
            Assert.That(DownedRecoveryRules.EvaluateInterruption(s),
                Is.EqualTo(DownedRecoveryInterruptReason.ReviverDowned));
        }

        [Test]
        public void EvaluateInterruption_ReviverDisconnected()
        {
            DownedRecoveryInterruptionSnapshot s = Valid().With(reviverConnected: false);
            Assert.That(DownedRecoveryRules.EvaluateInterruption(s),
                Is.EqualTo(DownedRecoveryInterruptReason.ReviverDisconnected));
        }

        [Test]
        public void EvaluateInterruption_DownedDamaged()
        {
            DownedRecoveryInterruptionSnapshot s = Valid().With(downedDamaged: true);
            Assert.That(DownedRecoveryRules.EvaluateInterruption(s),
                Is.EqualTo(DownedRecoveryInterruptReason.DownedDamaged));
        }

        [Test]
        public void EvaluateInterruption_IncompatibleReviverAction()
        {
            DownedRecoveryInterruptionSnapshot s = Valid().With(incompatibleReviverAction: true);
            Assert.That(DownedRecoveryRules.EvaluateInterruption(s),
                Is.EqualTo(DownedRecoveryInterruptReason.IncompatibleReviverAction));
        }

        [Test]
        public void EvaluateInterruption_DownedDefeated_WhenNoLongerDowned()
        {
            DownedRecoveryInterruptionSnapshot s = Valid().With(downedIsDowned: false);
            Assert.That(DownedRecoveryRules.EvaluateInterruption(s),
                Is.EqualTo(DownedRecoveryInterruptReason.DownedDefeated));
        }

        [Test]
        public void EvaluateInterruption_StaleCycle_IsDownedDefeated()
        {
            DownedRecoveryInterruptionSnapshot s = Valid().With(downedCycle: 2);
            Assert.That(DownedRecoveryRules.EvaluateInterruption(s),
                Is.EqualTo(DownedRecoveryInterruptReason.DownedDefeated));
        }

        [Test]
        public void IsSessionValid_RequiresKindDownedAndMatchingCycle()
        {
            Assert.That(DownedRecoveryRules.IsSessionValid(RecoveryKind.Assisted, 2, 2, true), Is.True);
            Assert.That(DownedRecoveryRules.IsSessionValid(RecoveryKind.None, 2, 2, true), Is.False);
            Assert.That(DownedRecoveryRules.IsSessionValid(RecoveryKind.Assisted, 1, 2, true), Is.False);
            Assert.That(DownedRecoveryRules.IsSessionValid(RecoveryKind.Assisted, 2, 2, false), Is.False);
            Assert.That(DownedRecoveryRules.IsSessionValid(RecoveryKind.Assisted, 0, 0, true), Is.False);
        }

        [Test]
        public void CanStartAssisted_AcceptsActiveInRangeTeammate()
        {
            Assert.That(StartWith(), Is.True);
        }

        [Test]
        public void CanStartAssisted_RejectsEachInvalidCondition()
        {
            Assert.That(StartWith(reviverAlive: false), Is.False);
            Assert.That(StartWith(reviverDowned: true), Is.False);
            Assert.That(StartWith(sameTeam: false), Is.False);
            Assert.That(StartWith(distance: 2f), Is.False);
            Assert.That(StartWith(distance: float.NaN), Is.False);
            Assert.That(StartWith(targetDowned: false), Is.False);
            Assert.That(StartWith(targetHasValidSession: true), Is.False);
            Assert.That(StartWith(reviverAlreadyReviving: true), Is.False);
            Assert.That(StartWith(range: 0f), Is.False);
            Assert.That(StartWith(range: float.NaN), Is.False);
        }

        private static bool StartWith(
            bool reviverAlive = true,
            bool reviverDowned = false,
            bool sameTeam = true,
            float distance = 1f,
            float range = Range,
            bool targetDowned = true,
            bool targetHasValidSession = false,
            bool reviverAlreadyReviving = false)
        {
            return DownedRecoveryRules.CanStartAssisted(
                reviverAlive,
                reviverDowned,
                sameTeam,
                distance,
                range,
                targetDowned,
                targetHasValidSession,
                reviverAlreadyReviving);
        }
    }

    internal static class DownedRecoverySnapshotTestExtensions
    {
        internal static DownedRecoveryInterruptionSnapshot With(
            this DownedRecoveryInterruptionSnapshot s,
            bool? reviverInteractHeld = null,
            bool? reviverMoving = null,
            bool? downedMoving = null,
            float? distance = null,
            float? currentReviverHealth = null,
            bool? reviverDowned = null,
            bool? reviverConnected = null,
            bool? downedDamaged = null,
            bool? incompatibleReviverAction = null,
            bool? downedIsDowned = null,
            int? downedCycle = null)
        {
            return new DownedRecoveryInterruptionSnapshot(
                reviverInteractHeld ?? s.ReviverInteractHeld,
                reviverMoving ?? s.ReviverMoving,
                downedMoving ?? s.DownedMoving,
                distance ?? s.Distance,
                s.Range,
                s.PreviousReviverHealth,
                currentReviverHealth ?? s.CurrentReviverHealth,
                reviverDowned ?? s.ReviverDowned,
                reviverConnected ?? s.ReviverConnected,
                downedDamaged ?? s.DownedDamaged,
                incompatibleReviverAction ?? s.IncompatibleReviverAction,
                downedIsDowned ?? s.DownedIsDowned,
                s.SessionCycle,
                downedCycle ?? s.DownedCycle);
        }
    }
}
