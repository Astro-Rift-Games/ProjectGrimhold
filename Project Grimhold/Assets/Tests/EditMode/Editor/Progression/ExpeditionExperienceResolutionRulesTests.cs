using System;
using NUnit.Framework;

namespace Tests.EditMode.Progression
{
    public sealed class ExpeditionExperienceResolutionRulesTests
    {
        [TestCase(ExpeditionExperienceResolutionOutcome.Extracted, 10_000, 100)]
        [TestCase(ExpeditionExperienceResolutionOutcome.Defeated, 2_000, 20)]
        [TestCase(ExpeditionExperienceResolutionOutcome.Abandoned, 0, 0)]
        [TestCase(ExpeditionExperienceResolutionOutcome.DefinitivelyDisconnected, 0, 0)]
        public void InitialPolicy_ResolvesEveryDefinitiveOutcome(
            ExpeditionExperienceResolutionOutcome outcome,
            int expectedBasisPoints,
            long expectedConsolidatedExperience)
        {
            var snapshot = new ExpeditionExperienceSnapshot(40, 20, 40, 0);

            ExpeditionExperienceResolution resolution = Resolve(
                snapshot,
                outcome,
                ProgressionBalanceDefaults.InitialExpeditionExperienceRetentionPolicy);

            AssertResolution(
                resolution,
                outcome,
                snapshot,
                expectedBasisPoints,
                expectedConsolidatedExperience);
        }

        [Test]
        public void Defeated_FloorsFractionalExperience()
        {
            var snapshot = new ExpeditionExperienceSnapshot(87, 0, 0, 0);

            ExpeditionExperienceResolution resolution = Resolve(
                snapshot,
                ExpeditionExperienceResolutionOutcome.Defeated,
                ProgressionBalanceDefaults.InitialExpeditionExperienceRetentionPolicy);

            Assert.That(resolution.ConsolidatedExperience, Is.EqualTo(17));
        }

        [Test]
        public void Policy_AcceptsInclusivePercentageLimits()
        {
            Assert.That(
                ExpeditionExperienceRetentionPolicy.TryCreate(
                    10_000,
                    0,
                    10_000,
                    0,
                    out ExpeditionExperienceRetentionPolicy policy),
                Is.True);
            Assert.That(policy, Is.Not.Null);
            Assert.That(policy.ExtractedBasisPoints, Is.EqualTo(10_000));
            Assert.That(policy.DefeatedBasisPoints, Is.Zero);
            Assert.That(policy.AbandonedBasisPoints, Is.EqualTo(10_000));
            Assert.That(policy.DefinitivelyDisconnectedBasisPoints, Is.Zero);
        }

        [TestCase(-1, 0, 0, 0)]
        [TestCase(10_001, 0, 0, 0)]
        [TestCase(0, -1, 0, 0)]
        [TestCase(0, 10_001, 0, 0)]
        [TestCase(0, 0, -1, 0)]
        [TestCase(0, 0, 10_001, 0)]
        [TestCase(0, 0, 0, -1)]
        [TestCase(0, 0, 0, 10_001)]
        public void Policy_RejectsEveryOutOfRangeField(
            int extracted,
            int defeated,
            int abandoned,
            int disconnected)
        {
            Assert.That(
                ExpeditionExperienceRetentionPolicy.TryCreate(
                    extracted,
                    defeated,
                    abandoned,
                    disconnected,
                    out ExpeditionExperienceRetentionPolicy policy),
                Is.False);
            Assert.That(policy, Is.Null);
        }

        [Test]
        public void CustomPolicy_ChangesSelectedOutcome()
        {
            Assert.That(
                ExpeditionExperienceRetentionPolicy.TryCreate(
                    5_000,
                    2_500,
                    1_000,
                    7_500,
                    out ExpeditionExperienceRetentionPolicy policy),
                Is.True);

            ExpeditionExperienceResolution resolution = Resolve(
                new ExpeditionExperienceSnapshot(9, 0, 0, 0),
                ExpeditionExperienceResolutionOutcome.Extracted,
                policy);

            Assert.That(resolution.RetentionBasisPoints, Is.EqualTo(5_000));
            Assert.That(resolution.ConsolidatedExperience, Is.EqualTo(4));
        }

        [Test]
        public void EmptySnapshot_ResolvesToZero()
        {
            ExpeditionExperienceResolution resolution = Resolve(
                ExpeditionExperienceSnapshot.Empty,
                ExpeditionExperienceResolutionOutcome.Extracted,
                ProgressionBalanceDefaults.InitialExpeditionExperienceRetentionPolicy);

            Assert.That(resolution.ProvisionalExperienceTotal, Is.Zero);
            Assert.That(resolution.ConsolidatedExperience, Is.Zero);
        }

        [Test]
        public void MaximumTotal_AtFullRetention_DoesNotOverflow()
        {
            var snapshot = new ExpeditionExperienceSnapshot(long.MaxValue, 0, 0, 0);

            ExpeditionExperienceResolution resolution = Resolve(
                snapshot,
                ExpeditionExperienceResolutionOutcome.Extracted,
                ProgressionBalanceDefaults.InitialExpeditionExperienceRetentionPolicy);

            Assert.That(resolution.ProvisionalExperienceTotal, Is.EqualTo(long.MaxValue));
            Assert.That(resolution.ConsolidatedExperience, Is.EqualTo(long.MaxValue));
        }

        [Test]
        public void LargeTotal_AtPartialRetention_DoesNotOverflow()
        {
            var snapshot = new ExpeditionExperienceSnapshot(long.MaxValue, 0, 0, 0);

            ExpeditionExperienceResolution resolution = Resolve(
                snapshot,
                ExpeditionExperienceResolutionOutcome.Defeated,
                ProgressionBalanceDefaults.InitialExpeditionExperienceRetentionPolicy);

            Assert.That(resolution.ConsolidatedExperience, Is.EqualTo(1_844_674_407_370_955_161L));
        }

        [Test]
        public void CompletedResolution_RejectsReplacementAndPreservesFirstValue()
        {
            ExpeditionExperienceResolution first = Resolve(
                new ExpeditionExperienceSnapshot(87, 0, 0, 0),
                ExpeditionExperienceResolutionOutcome.Defeated,
                ProgressionBalanceDefaults.InitialExpeditionExperienceRetentionPolicy);

            Assert.That(
                ExpeditionExperienceResolutionRules.TryResolve(
                    first,
                    new ExpeditionExperienceSnapshot(500, 0, 0, 0),
                    ExpeditionExperienceResolutionOutcome.Extracted,
                    ProgressionBalanceDefaults.InitialExpeditionExperienceRetentionPolicy,
                    out ExpeditionExperienceResolution candidate,
                    out ExpeditionExperienceResolutionFailure failure),
                Is.False);
            Assert.That(failure, Is.EqualTo(ExpeditionExperienceResolutionFailure.AlreadyResolved));
            Assert.That(candidate, Is.EqualTo(first));
        }

        [Test]
        public void ExtractedLootExperience_RemainsInBreakdownAndParticipatesInTotal()
        {
            var snapshot = new ExpeditionExperienceSnapshot(10, 5, 7, 78);

            ExpeditionExperienceResolution resolution = Resolve(
                snapshot,
                ExpeditionExperienceResolutionOutcome.Extracted,
                ProgressionBalanceDefaults.InitialExpeditionExperienceRetentionPolicy);

            Assert.That(resolution.ProvisionalExperience, Is.EqualTo(snapshot));
            Assert.That(resolution.ProvisionalExperience.ExtractedLootExperience, Is.EqualTo(78));
            Assert.That(resolution.ProvisionalExperienceTotal, Is.EqualTo(100));
            Assert.That(resolution.ConsolidatedExperience, Is.EqualTo(100));
        }

        [Test]
        public void OutcomeEnum_ContainsOnlyFourDefinitiveResults()
        {
            Array values = Enum.GetValues(typeof(ExpeditionExperienceResolutionOutcome));

            Assert.That(values.Length, Is.EqualTo(4));
            CollectionAssert.AreEquivalent(
                new[]
                {
                    ExpeditionExperienceResolutionOutcome.Extracted,
                    ExpeditionExperienceResolutionOutcome.Defeated,
                    ExpeditionExperienceResolutionOutcome.Abandoned,
                    ExpeditionExperienceResolutionOutcome.DefinitivelyDisconnected
                },
                values);
        }

        [Test]
        public void UnknownOutcome_IsRejectedAtomically()
        {
            AssertRejected(
                new ExpeditionExperienceSnapshot(3, 0, 0, 0),
                (ExpeditionExperienceResolutionOutcome)byte.MaxValue,
                ProgressionBalanceDefaults.InitialExpeditionExperienceRetentionPolicy,
                ExpeditionExperienceResolutionFailure.InvalidOutcome);
        }

        [Test]
        public void NegativeSnapshot_IsRejectedWithoutThrowing()
        {
            AssertRejected(
                new ExpeditionExperienceSnapshot(-1, 0, 0, 0),
                ExpeditionExperienceResolutionOutcome.Extracted,
                ProgressionBalanceDefaults.InitialExpeditionExperienceRetentionPolicy,
                ExpeditionExperienceResolutionFailure.InvalidSnapshot);
        }

        [Test]
        public void OverflowingSnapshot_IsRejectedWithoutThrowing()
        {
            AssertRejected(
                new ExpeditionExperienceSnapshot(long.MaxValue, 1, 0, 0),
                ExpeditionExperienceResolutionOutcome.Extracted,
                ProgressionBalanceDefaults.InitialExpeditionExperienceRetentionPolicy,
                ExpeditionExperienceResolutionFailure.InvalidSnapshot);
        }

        [Test]
        public void MissingPolicy_IsRejectedAtomically()
        {
            AssertRejected(
                new ExpeditionExperienceSnapshot(3, 0, 0, 0),
                ExpeditionExperienceResolutionOutcome.Extracted,
                null,
                ExpeditionExperienceResolutionFailure.MissingPolicy);
        }

        private static ExpeditionExperienceResolution Resolve(
            in ExpeditionExperienceSnapshot snapshot,
            ExpeditionExperienceResolutionOutcome outcome,
            ExpeditionExperienceRetentionPolicy policy)
        {
            Assert.That(
                ExpeditionExperienceResolutionRules.TryResolve(
                    default,
                    snapshot,
                    outcome,
                    policy,
                    out ExpeditionExperienceResolution resolution,
                    out ExpeditionExperienceResolutionFailure failure),
                Is.True,
                failure.ToString());
            Assert.That(failure, Is.EqualTo(ExpeditionExperienceResolutionFailure.None));
            return resolution;
        }

        private static void AssertRejected(
            ExpeditionExperienceSnapshot snapshot,
            ExpeditionExperienceResolutionOutcome requestedOutcome,
            ExpeditionExperienceRetentionPolicy policy,
            ExpeditionExperienceResolutionFailure expectedFailure)
        {
            ExpeditionExperienceResolution previous = default;

            Assert.That(
                ExpeditionExperienceResolutionRules.TryResolve(
                    previous,
                    snapshot,
                    requestedOutcome,
                    policy,
                    out ExpeditionExperienceResolution candidate,
                    out ExpeditionExperienceResolutionFailure failure),
                Is.False);
            Assert.That(failure, Is.EqualTo(expectedFailure));
            Assert.That(candidate, Is.EqualTo(previous));
        }

        private static void AssertResolution(
            in ExpeditionExperienceResolution resolution,
            ExpeditionExperienceResolutionOutcome expectedOutcome,
            in ExpeditionExperienceSnapshot expectedSnapshot,
            int expectedBasisPoints,
            long expectedConsolidatedExperience)
        {
            Assert.That(resolution.IsResolved, Is.True);
            Assert.That(resolution.Outcome, Is.EqualTo(expectedOutcome));
            Assert.That(resolution.ProvisionalExperience, Is.EqualTo(expectedSnapshot));
            Assert.That(resolution.ProvisionalExperienceTotal, Is.EqualTo(expectedSnapshot.TotalExperience));
            Assert.That(resolution.RetentionBasisPoints, Is.EqualTo(expectedBasisPoints));
            Assert.That(resolution.ConsolidatedExperience, Is.EqualTo(expectedConsolidatedExperience));
        }
    }
}
