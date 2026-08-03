using NUnit.Framework;

namespace Tests.EditMode.Loot
{
    public sealed class LootFirstAcquisitionRulesTests
    {
        [Test]
        public void MixedStack_ConsumesNaturalUnitsFirst()
        {
            Assert.That(LootFirstAcquisitionRules.TryResolveExtraction(
                10, 4, 3, out int eligible, out int remainingTotal, out int remainingEligible), Is.True);

            Assert.That(eligible, Is.EqualTo(3));
            Assert.That(remainingTotal, Is.EqualTo(7));
            Assert.That(remainingEligible, Is.EqualTo(1));
        }

        [Test]
        public void SuccessiveWithdrawals_CreditOnlyNaturalRemainder()
        {
            LootFirstAcquisitionRules.TryResolveExtraction(
                10, 4, 3, out _, out int total, out int eligible);

            Assert.That(LootFirstAcquisitionRules.TryResolveExtraction(
                total, eligible, 3, out int secondCredit, out total, out eligible), Is.True);
            Assert.That(secondCredit, Is.EqualTo(1));
            Assert.That(total, Is.EqualTo(4));
            Assert.That(eligible, Is.Zero);
        }

        [TestCase(5, -1, 1)]
        [TestCase(5, 6, 1)]
        [TestCase(5, 5, 6)]
        [TestCase(0, 0, 1)]
        public void InvalidInvariant_IsRejected(int total, int eligible, int requested)
        {
            Assert.That(LootFirstAcquisitionRules.TryResolveExtraction(
                total, eligible, requested, out _, out _, out _), Is.False);
        }
    }
}
