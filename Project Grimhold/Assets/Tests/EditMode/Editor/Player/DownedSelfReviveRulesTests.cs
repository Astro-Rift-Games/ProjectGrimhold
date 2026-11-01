using NUnit.Framework;

namespace Tests.EditMode.Player
{
    /// <summary>Self-revive and Self-reviver handoff rules (GD 13 sections 7, 7.1, 8 and 18).</summary>
    public sealed class DownedSelfReviveRulesTests
    {
        private const float Range = 1.5f;
        private const float TapWindow = 0.3f;

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
        public void CanStartSelf_DownedCarrierWithQuota_IsAllowed()
        {
            Assert.That(DownedRecoveryRules.CanStartSelf(true, false, true, false), Is.True);
        }

        [TestCase(false, false, true, false, TestName = "CanStartSelf_NotDowned_IsRejected")]
        [TestCase(true, true, true, false, TestName = "CanStartSelf_SessionRunning_IsRejected")]
        [TestCase(true, false, false, false, TestName = "CanStartSelf_WithoutSelfReviver_IsRejected")]
        [TestCase(true, false, true, true, TestName = "CanStartSelf_ExpeditionQuotaUsed_IsRejected")]
        public void CanStartSelf_Rejections(bool isDowned, bool hasSession, bool carries, bool used)
        {
            Assert.That(DownedRecoveryRules.CanStartSelf(isDowned, hasSession, carries, used), Is.False);
        }

        [Test]
        public void EvaluateSelfInterruption_ValidSession_ReturnsNone()
        {
            Assert.That(DownedRecoveryRules.EvaluateSelfInterruption(true, 1, 1, false),
                Is.EqualTo(DownedRecoveryInterruptReason.None));
        }

        [Test]
        public void EvaluateSelfInterruption_Displacement_Interrupts()
        {
            Assert.That(DownedRecoveryRules.EvaluateSelfInterruption(true, 1, 1, true),
                Is.EqualTo(DownedRecoveryInterruptReason.DownedMoved));
        }

        [TestCase(false, 1, 1)]
        [TestCase(true, 1, 2)]
        public void EvaluateSelfInterruption_DefeatOrNewCycle_IsDownedDefeated(bool isDowned, int session, int cycle)
        {
            Assert.That(DownedRecoveryRules.EvaluateSelfInterruption(isDowned, session, cycle, false),
                Is.EqualTo(DownedRecoveryInterruptReason.DownedDefeated));
        }

        [TestCase(RecoveryKind.Assisted, true)]
        [TestCase(RecoveryKind.Self, true)]
        [TestCase(RecoveryKind.Handoff, false)]
        [TestCase(RecoveryKind.None, false)]
        public void PausesDrain_OnlyRevivesPause(RecoveryKind kind, bool expected)
        {
            Assert.That(DownedRecoveryRules.PausesDrain(kind), Is.EqualTo(expected));
        }

        [Test]
        public void CanStartHandoff_ValidGiver_IsAllowed()
        {
            Assert.That(Handoff(), Is.True);
        }

        [Test]
        public void CanStartHandoff_Rejections()
        {
            Assert.That(Handoff(giverAlive: false), Is.False, "Dead giver.");
            Assert.That(Handoff(giverDowned: true), Is.False, "Downed giver.");
            Assert.That(Handoff(sameTeam: false), Is.False, "Enemy.");
            Assert.That(Handoff(distance: 2f), Is.False, "Out of range.");
            Assert.That(Handoff(targetDowned: false), Is.False, "Target Active.");
            Assert.That(Handoff(targetHasSession: true), Is.False, "Target busy.");
            Assert.That(Handoff(giverBusy: true), Is.False, "Giver busy.");
            Assert.That(Handoff(giverCarries: false), Is.False, "Nothing to give.");
            Assert.That(Handoff(targetCanReceive: false), Is.False, "Target carries one or has no space.");
            Assert.That(Handoff(targetUsed: true), Is.False, "Target quota used.");
        }

        [Test]
        public void ShouldConvertToHandoff_QuickReleaseWithHandoff_Converts()
        {
            Assert.That(DownedRecoveryRules.ShouldConvertToHandoff(
                DownedRecoveryInterruptReason.InteractReleased, 0.1f, TapWindow, true), Is.True);
        }

        [Test]
        public void ShouldConvertToHandoff_Rejections()
        {
            Assert.That(DownedRecoveryRules.ShouldConvertToHandoff(
                DownedRecoveryInterruptReason.InteractReleased, 0.5f, TapWindow, true), Is.False, "Held: assisted.");
            Assert.That(DownedRecoveryRules.ShouldConvertToHandoff(
                DownedRecoveryInterruptReason.InteractReleased, 0.1f, TapWindow, false), Is.False, "No handoff.");
            Assert.That(DownedRecoveryRules.ShouldConvertToHandoff(
                DownedRecoveryInterruptReason.ReviverMoved, 0.1f, TapWindow, true), Is.False, "Other cause.");
        }

        [Test]
        public void EvaluateHandoffInterruption_ReleaseAndDownedMovement_DoNotInterrupt()
        {
            Assert.That(DownedRecoveryRules.EvaluateHandoffInterruption(Valid().With(reviverInteractHeld: false)),
                Is.EqualTo(DownedRecoveryInterruptReason.None));
            Assert.That(DownedRecoveryRules.EvaluateHandoffInterruption(Valid().With(downedMoving: true)),
                Is.EqualTo(DownedRecoveryInterruptReason.None));
        }

        [Test]
        public void EvaluateHandoffInterruption_GiverConditions_Interrupt()
        {
            Assert.That(DownedRecoveryRules.EvaluateHandoffInterruption(Valid().With(reviverMoving: true)),
                Is.EqualTo(DownedRecoveryInterruptReason.ReviverMoved));
            Assert.That(DownedRecoveryRules.EvaluateHandoffInterruption(Valid().With(distance: 2f)),
                Is.EqualTo(DownedRecoveryInterruptReason.OutOfRange));
            Assert.That(DownedRecoveryRules.EvaluateHandoffInterruption(Valid().With(currentReviverHealth: 90f)),
                Is.EqualTo(DownedRecoveryInterruptReason.ReviverDamaged));
            Assert.That(DownedRecoveryRules.EvaluateHandoffInterruption(Valid().With(reviverConnected: false)),
                Is.EqualTo(DownedRecoveryInterruptReason.ReviverDisconnected));
        }

        private static bool Handoff(
            bool giverAlive = true,
            bool giverDowned = false,
            bool sameTeam = true,
            float distance = 1f,
            bool targetDowned = true,
            bool targetHasSession = false,
            bool giverBusy = false,
            bool giverCarries = true,
            bool targetCanReceive = true,
            bool targetUsed = false)
        {
            return DownedRecoveryRules.CanStartHandoff(
                giverAlive, giverDowned, sameTeam, distance, Range, targetDowned, targetHasSession,
                giverBusy, giverCarries, targetCanReceive, targetUsed);
        }
    }
}
