using NUnit.Framework;

namespace Tests.EditMode.Player
{
    /// <summary>Self-revive and handoff wording (GD 13 section 14): availability, progress and outcomes.</summary>
    public sealed class DownedSelfReviveFeedbackTests
    {
        [Test]
        public void MessageFor_Completed_NamesTheRecoveryForm()
        {
            Assert.That(Completed(RecoveryKind.Assisted, false), Is.EqualTo("Reanimación completada"));
            Assert.That(Completed(RecoveryKind.Self, true), Is.EqualTo("Auto-reanimación completada"));
            Assert.That(Completed(RecoveryKind.Handoff, true), Is.EqualTo("Auto-reanimador recibido"));
            Assert.That(Completed(RecoveryKind.Handoff, false), Is.EqualTo("Auto-reanimador entregado"));
        }

        [Test]
        public void MessageFor_Interrupted_NamesTheRecoveryFormAndCause()
        {
            Assert.That(Interrupted(RecoveryKind.Assisted, DownedRecoveryInterruptReason.DownedDamaged),
                Is.EqualTo("Reanimación interrumpida: daño recibido"));
            Assert.That(Interrupted(RecoveryKind.Self, DownedRecoveryInterruptReason.DownedDamaged),
                Is.EqualTo("Auto-reanimación interrumpida: daño recibido"));
            Assert.That(Interrupted(RecoveryKind.Self, DownedRecoveryInterruptReason.DownedMoved),
                Is.EqualTo("Auto-reanimación interrumpida: movimiento"));
            Assert.That(Interrupted(RecoveryKind.Handoff, DownedRecoveryInterruptReason.OutOfRange),
                Is.EqualTo("Entrega interrumpida: fuera de rango"));
            Assert.That(Interrupted(RecoveryKind.Handoff, DownedRecoveryInterruptReason.HandoffRejected),
                Is.EqualTo("Entrega fallida: el aliado no puede recibirlo"));
            Assert.That(Interrupted(RecoveryKind.Self, DownedRecoveryInterruptReason.DownedDefeated), Is.Null);
        }

        [Test]
        public void MessageFor_LegacyOverload_KeepsAssistedWording()
        {
            Assert.That(DownedRecoveryFeedbackRules.MessageFor(
                    RecoveryOutcomeKind.Interrupted, DownedRecoveryInterruptReason.InteractReleased),
                Is.EqualTo("Reanimación interrumpida: acción soltada"));
        }

        [Test]
        public void DownedStateText_ShowsSelfReviverAvailability()
        {
            Assert.That(DownedRecoveryFeedbackRules.DownedStateText(RecoveryKind.None, true, false),
                Is.EqualTo("ABATIDO · pulsa [F] para auto-reanimarte"));
            Assert.That(DownedRecoveryFeedbackRules.DownedStateText(RecoveryKind.None, true, true),
                Is.EqualTo("ABATIDO · Auto-reanimador ya usado"));
            Assert.That(DownedRecoveryFeedbackRules.DownedStateText(RecoveryKind.None, false, false),
                Is.EqualTo("ABATIDO"));
            Assert.That(DownedRecoveryFeedbackRules.DownedStateText(RecoveryKind.Assisted, true, false),
                Is.EqualTo("ABATIDO · Reanimando"));
            Assert.That(DownedRecoveryFeedbackRules.DownedStateText(RecoveryKind.Self, false, false),
                Is.EqualTo("ABATIDO · Auto-reanimándote"));
            Assert.That(DownedRecoveryFeedbackRules.DownedStateText(RecoveryKind.Handoff, false, false),
                Is.EqualTo("ABATIDO · Recibiendo Auto-reanimador"));
        }

        [Test]
        public void ProgressLabel_DependsOnFormAndRole()
        {
            Assert.That(DownedRecoveryFeedbackRules.ProgressLabel(RecoveryKind.Assisted, true), Is.EqualTo("Reanimando…"));
            Assert.That(DownedRecoveryFeedbackRules.ProgressLabel(RecoveryKind.Assisted, false), Is.EqualTo("Te están reanimando…"));
            Assert.That(DownedRecoveryFeedbackRules.ProgressLabel(RecoveryKind.Self, false), Is.EqualTo("Auto-reanimándote…"));
            Assert.That(DownedRecoveryFeedbackRules.ProgressLabel(RecoveryKind.Handoff, true), Is.EqualTo("Entregando Auto-reanimador…"));
            Assert.That(DownedRecoveryFeedbackRules.ProgressLabel(RecoveryKind.Handoff, false), Is.EqualTo("Recibiendo Auto-reanimador…"));
        }

        [Test]
        public void AllyDownedText_OffersTapAndHoldOnlyWhenHandoffIsPossible()
        {
            Assert.That(DownedRecoveryFeedbackRules.AllyDownedText(false, true), Is.EqualTo("Aliado abatido"));
            Assert.That(DownedRecoveryFeedbackRules.AllyDownedText(true, false),
                Is.EqualTo("Aliado abatido · mantén [F] para reanimar"));
            Assert.That(DownedRecoveryFeedbackRules.AllyDownedText(true, true),
                Is.EqualTo("Aliado abatido · toca [F]: entregar Auto-reanimador · mantén [F]: reanimar"));
        }

        private static string Completed(RecoveryKind kind, bool asRecipient) =>
            DownedRecoveryFeedbackRules.MessageFor(
                RecoveryOutcomeKind.Completed, DownedRecoveryInterruptReason.None, kind, asRecipient);

        private static string Interrupted(RecoveryKind kind, DownedRecoveryInterruptReason reason) =>
            DownedRecoveryFeedbackRules.MessageFor(RecoveryOutcomeKind.Interrupted, reason, kind, true);
    }
}
