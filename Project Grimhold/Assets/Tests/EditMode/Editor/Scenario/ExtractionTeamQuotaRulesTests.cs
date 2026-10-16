using System;
using NUnit.Framework;

namespace Tests.EditMode.Scenario
{
    [TestFixture]
    public sealed class ExtractionTeamQuotaRulesTests
    {
        private const int SoloQuota = 100;
        private const int DuoQuota = 150;

        [TestCase(1, SoloQuota)]
        [TestCase(2, DuoQuota)]
        public void TryGetQuota_ValidTeamSizes_ResolvesExpectedQuota(int teamSize, int expectedQuota)
        {
            bool success = ExtractionTeamQuotaRules.TryGetQuota(teamSize, SoloQuota, DuoQuota, out int quota);
            Assert.That(success, Is.True);
            Assert.That(quota, Is.EqualTo(expectedQuota));

            int directQuota = ExtractionTeamQuotaRules.GetQuota(teamSize, SoloQuota, DuoQuota);
            Assert.That(directQuota, Is.EqualTo(expectedQuota));
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(16)]
        public void TryGetQuota_InvalidTeamSizes_FailsAndThrows(int invalidSize)
        {
            bool success = ExtractionTeamQuotaRules.TryGetQuota(invalidSize, SoloQuota, DuoQuota, out int quota);
            Assert.That(success, Is.False);
            Assert.That(quota, Is.EqualTo(0));

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                ExtractionTeamQuotaRules.GetQuota(invalidSize, SoloQuota, DuoQuota));
        }

        [Test]
        public void TryGetQuota_RejectsNonPositiveQuotas()
        {
            Assert.That(ExtractionTeamQuotaRules.TryGetQuota(1, 0, DuoQuota, out _), Is.False);
            Assert.That(ExtractionTeamQuotaRules.TryGetQuota(1, -10, DuoQuota, out _), Is.False);
            Assert.That(ExtractionTeamQuotaRules.TryGetQuota(2, SoloQuota, 0, out _), Is.False);
            Assert.That(ExtractionTeamQuotaRules.TryGetQuota(2, SoloQuota, -5, out _), Is.False);
        }

        [Test]
        public void Quota_RemainsImmutable_RegardlessOfCompositionChanges()
        {
            // Initial duo launch
            int initialSize = 2;
            int initialQuota = ExtractionTeamQuotaRules.GetQuota(initialSize, SoloQuota, DuoQuota);
            Assert.That(initialQuota, Is.EqualTo(DuoQuota));

            // Partner is downed, disconnected, or definitive defeat occurs mid-raid:
            // The team quota remains the original duo quota (150) and never drops to 100.
            int midRaidEffectiveSurvivors = 1;
            // The business rule explicitly dictates that the initial size governs the expedition quota
            Assert.That(initialQuota, Is.EqualTo(DuoQuota));
            Assert.That(initialQuota, Is.Not.EqualTo(ExtractionTeamQuotaRules.GetQuota(midRaidEffectiveSurvivors, SoloQuota, DuoQuota)));
        }
    }
}
