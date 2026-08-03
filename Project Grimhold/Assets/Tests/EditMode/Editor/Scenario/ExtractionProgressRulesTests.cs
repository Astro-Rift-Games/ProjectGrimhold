using NUnit.Framework;

namespace Tests.EditMode.Scenario
{
    public sealed class ExtractionProgressRulesTests
    {
        [Test]
        public void TryCalculateNext_SaturatesWithoutOverflow()
        {
            Assert.That(ExtractionProgressRules.TryCalculateNext(90, 100, long.MaxValue, out int next, out bool completed), Is.True);
            Assert.That(next, Is.EqualTo(100));
            Assert.That(completed, Is.True);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void TryCalculateNext_RejectsNonPositiveContribution(long amount)
        {
            Assert.That(ExtractionProgressRules.TryCalculateNext(10, 100, amount, out int next, out _), Is.False);
            Assert.That(next, Is.EqualTo(10));
        }

        [Test]
        public void Snapshot_ClampsValuesAndExposesPercentage()
        {
            var snapshot = new ExtractionProgressSnapshot(150, 100, true);

            Assert.That(snapshot.CurrentProgress, Is.EqualTo(100));
            Assert.That(snapshot.Quota, Is.EqualTo(100));
            Assert.That(snapshot.Percentage, Is.EqualTo(100f));
            Assert.That(snapshot.IsQuotaComplete, Is.True);
            Assert.That(snapshot.AssignmentRequested, Is.True);
        }

        [Test]
        public void Rules_AcceptDistinctContributionsThatShareATickMetadata()
        {
            var first = new ExtractionProgressContribution(
                ExtractionProgressSourceType.Defeat, new EntityId(2), 10, 44);
            var second = new ExtractionProgressContribution(
                ExtractionProgressSourceType.ContainerFirstOpen, new EntityId(3), 5, 44);

            Assert.That(ExtractionProgressRules.TryCalculateNext(0, 100, first.Amount, out int afterFirst, out _), Is.True);
            Assert.That(ExtractionProgressRules.TryCalculateNext(afterFirst, 100, second.Amount, out int afterSecond, out _), Is.True);
            Assert.That(afterSecond, Is.EqualTo(15));
        }
    }
}
