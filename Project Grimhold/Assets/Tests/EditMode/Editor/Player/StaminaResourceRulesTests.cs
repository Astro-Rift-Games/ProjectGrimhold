using NUnit.Framework;

namespace Tests.EditMode.Player
{
    public sealed class StaminaResourceRulesTests
    {
        [Test]
        public void CanSpend_ExhaustionBlocksPositiveCostButNotZero()
        {
            Assert.That(StaminaResourceRules.CanSpend(100f, true, 1f), Is.False);
            Assert.That(StaminaResourceRules.CanSpend(0f, true, 0f), Is.True);
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void CanSpend_InvalidCostIsRejected(float amount)
        {
            Assert.That(StaminaResourceRules.CanSpend(100f, false, amount), Is.False);
        }

        [Test]
        public void DiscreteFailure_PreservesRemainderWithoutExhaustion()
        {
            bool spent = StaminaResourceRules.TrySpend(
                0.05f,
                false,
                0.16f,
                exhaustOnFailure: false,
                exhaustWhenDepleted: false,
                out float current,
                out bool exhausted);

            Assert.That(spent, Is.False);
            Assert.That(current, Is.EqualTo(0.05f));
            Assert.That(exhausted, Is.False);
        }

        [Test]
        public void ContinuousFailure_PreservesRemainderAndExhausts()
        {
            bool spent = StaminaResourceRules.TrySpend(
                0.05f,
                false,
                0.16f,
                exhaustOnFailure: true,
                exhaustWhenDepleted: true,
                out float current,
                out bool exhausted);

            Assert.That(spent, Is.False);
            Assert.That(current, Is.EqualTo(0.05f));
            Assert.That(exhausted, Is.True);
        }

        [Test]
        public void ContinuousCompletePayment_ClampsResultAndExhaustsWithoutFloatEqualityContract()
        {
            float cost = 0.1f + 0.06f;
            bool spent = StaminaResourceRules.TrySpend(
                cost,
                false,
                cost,
                exhaustOnFailure: true,
                exhaustWhenDepleted: true,
                out float current,
                out bool exhausted);

            Assert.That(spent, Is.True);
            Assert.That(current, Is.Zero);
            Assert.That(exhausted, Is.True);
        }

        [Test]
        public void ContinuousPayment_WithPositiveRemainderDoesNotExhaust()
        {
            bool spent = StaminaResourceRules.TrySpend(
                0.17f,
                false,
                0.16f,
                exhaustOnFailure: true,
                exhaustWhenDepleted: true,
                out float current,
                out bool exhausted);

            Assert.That(spent, Is.True);
            Assert.That(current, Is.EqualTo(0.01f).Within(0.000001f));
            Assert.That(exhausted, Is.False);
        }

        [Test]
        public void MaximumChanges_OnlyClampWhenCurrentExceedsNewMaximum()
        {
            Assert.That(StaminaResourceRules.ClampCurrent(40f, 130f), Is.EqualTo(40f));
            Assert.That(StaminaResourceRules.ClampCurrent(100f, 60f), Is.EqualTo(60f));
            Assert.That(StaminaResourceRules.ClampCurrent(-1f, 100f), Is.Zero);
        }

        [Test]
        public void Regeneration_UsesRateAndDeltaTimeAndCapsAtMaximum()
        {
            Assert.That(StaminaResourceRules.Regenerate(40f, 100f, 15f, 0.2f), Is.EqualTo(43f));
            Assert.That(StaminaResourceRules.Regenerate(99f, 100f, 15f, 0.2f), Is.EqualTo(100f));
        }

        [Test]
        public void ExhaustionRecovery_UsesConfiguredMaximumPercentage()
        {
            Assert.That(StaminaResourceRules.HasRecoveredFromExhaustion(24.99f, 100f, 0.25f), Is.False);
            Assert.That(StaminaResourceRules.HasRecoveredFromExhaustion(25f, 100f, 0.25f), Is.True);
        }
    }
}
