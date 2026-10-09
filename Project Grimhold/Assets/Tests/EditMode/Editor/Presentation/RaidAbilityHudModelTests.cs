#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using NUnit.Framework;

namespace Tests.EditMode.Presentation
{
    public sealed class RaidAbilityHudModelTests
    {
        private static RaidAbilityHudSlotFacts Ready() => new RaidAbilityHudSlotFacts
        {
            IsPrepared = true,
            Phase = AbilityExecutionPhase.Idle,
            TotalCooldownSeconds = 6f,
            Resource = AbilityResourceType.Stamina,
            Cost = 15f,
            HasResourceReading = true,
            AvailableResource = 100f
        };

        [Test]
        public void UnpreparedSlotIsEmptyEvenWithStaleFacts()
        {
            RaidAbilityHudSlotFacts facts = Ready();
            facts.IsPrepared = false;
            facts.IsOnCooldown = true;
            facts.RemainingCooldownSeconds = 3f;
            facts.Phase = AbilityExecutionPhase.Executing;

            RaidAbilityHudSlotModel model = RaidAbilityHudModelBuilder.Build(facts);

            Assert.That(model.State, Is.EqualTo(RaidAbilityHudSlotState.Empty));
            Assert.That(model.CooldownFill, Is.Zero);
            Assert.That(model.CooldownSeconds, Is.Zero);
            Assert.That(model.InsufficientResource, Is.False);
        }

        [Test]
        public void IdleAffordableSlotIsReady()
        {
            RaidAbilityHudSlotModel model = RaidAbilityHudModelBuilder.Build(Ready());

            Assert.That(model.State, Is.EqualTo(RaidAbilityHudSlotState.Ready));
            Assert.That(model.CooldownFill, Is.Zero);
            Assert.That(model.InsufficientResource, Is.False);
        }

        [TestCase(6f, 6f, 1f, 6f)]
        [TestCase(3f, 6f, 0.5f, 3f)]
        [TestCase(2.01f, 6f, 2.01f / 6f, 2.1f)]
        [TestCase(0.01f, 6f, 0.01f / 6f, 0.1f)]
        [TestCase(9f, 6f, 1f, 9f)]
        public void CooldownIsNormalisedAndRoundedUpToTenths(
            float remaining, float total, float expectedFill, float expectedSeconds)
        {
            RaidAbilityHudSlotFacts facts = Ready();
            facts.IsOnCooldown = true;
            facts.RemainingCooldownSeconds = remaining;
            facts.TotalCooldownSeconds = total;

            RaidAbilityHudSlotModel model = RaidAbilityHudModelBuilder.Build(facts);

            Assert.That(model.State, Is.EqualTo(RaidAbilityHudSlotState.OnCooldown));
            Assert.That(model.CooldownFill, Is.EqualTo(expectedFill).Within(0.0001f));
            Assert.That(model.CooldownSeconds, Is.EqualTo(expectedSeconds).Within(0.0001f));
        }

        [TestCase(0f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidTotalCooldownProducesZeroFillWithoutThrowing(float total)
        {
            RaidAbilityHudSlotFacts facts = Ready();
            facts.IsOnCooldown = true;
            facts.RemainingCooldownSeconds = 2f;
            facts.TotalCooldownSeconds = total;

            RaidAbilityHudSlotModel model = RaidAbilityHudModelBuilder.Build(facts);

            Assert.That(model.CooldownFill, Is.Zero);
        }

        [Test]
        public void CooldownFlagWithoutRemainingTimeIsReady()
        {
            RaidAbilityHudSlotFacts facts = Ready();
            facts.IsOnCooldown = true;
            facts.RemainingCooldownSeconds = 0f;

            Assert.That(
                RaidAbilityHudModelBuilder.Build(facts).State,
                Is.EqualTo(RaidAbilityHudSlotState.Ready));
        }

        [Test]
        public void ExecutionPhasesTakePrecedenceOverCooldown()
        {
            RaidAbilityHudSlotFacts facts = Ready();
            facts.IsOnCooldown = true;
            facts.RemainingCooldownSeconds = 5f;

            facts.Phase = AbilityExecutionPhase.Preparing;
            Assert.That(
                RaidAbilityHudModelBuilder.Build(facts).State,
                Is.EqualTo(RaidAbilityHudSlotState.Preparing));

            facts.Phase = AbilityExecutionPhase.Executing;
            RaidAbilityHudSlotModel executing = RaidAbilityHudModelBuilder.Build(facts);
            Assert.That(executing.State, Is.EqualTo(RaidAbilityHudSlotState.Executing));
            Assert.That(executing.CooldownFill, Is.GreaterThan(0f));
        }

        [Test]
        public void InsufficientResourceIsFlaggedOnlyWhenReadingIsKnownAndBelowCost()
        {
            RaidAbilityHudSlotFacts facts = Ready();
            facts.AvailableResource = 14.9f;
            Assert.That(RaidAbilityHudModelBuilder.Build(facts).InsufficientResource, Is.True);

            facts.AvailableResource = 15f;
            Assert.That(RaidAbilityHudModelBuilder.Build(facts).InsufficientResource, Is.False);

            facts.AvailableResource = 0f;
            facts.HasResourceReading = false;
            Assert.That(
                RaidAbilityHudModelBuilder.Build(facts).InsufficientResource,
                Is.False,
                "An unknown balance must not be presented as insufficient.");
        }

        [Test]
        public void RejectionAndInterruptionMessagesAreSpanishAndFallBackToGeneric()
        {
            Assert.That(
                RaidAbilityHudMessages.ForRejection(AbilityActivationFailure.Cooldown),
                Is.EqualTo("En enfriamiento"));
            Assert.That(
                RaidAbilityHudMessages.ForRejection(AbilityActivationFailure.InsufficientResource),
                Is.EqualTo("Recurso insuficiente"));
            Assert.That(
                RaidAbilityHudMessages.ForRejection(AbilityActivationFailure.BehaviourRejected),
                Is.EqualTo(RaidAbilityHudMessages.ForRejection(AbilityActivationFailure.InvalidPlan)));
            Assert.That(
                RaidAbilityHudMessages.ForRejection(AbilityActivationFailure.MissingBehaviour),
                Is.EqualTo("Habilidad no disponible"));
            Assert.That(
                RaidAbilityHudMessages.ForInterruption(AbilityExecutionStopReason.Knockback),
                Is.EqualTo("Interrumpida"));
        }
    }
}
#endif
