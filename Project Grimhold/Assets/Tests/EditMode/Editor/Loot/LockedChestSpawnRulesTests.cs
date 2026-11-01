using NUnit.Framework;

namespace Tests.EditMode.Loot
{
    public sealed class LockedChestSpawnRulesTests
    {
        [Test]
        public void TestingGuarantee_ForcesOnlyFirstPointAndLeavesOtherRollsUnchanged()
        {
            for (ulong seed = 0; seed < 100; seed++)
            {
                Assert.That(LockedChestSpawnRules.ShouldSpawnLocked(seed, 10, 0, true), Is.True);
                Assert.That(LockedChestSpawnRules.ShouldSpawnLocked(seed, 10, 1, true),
                    Is.EqualTo(LockedChestSpawnRules.ShouldSpawnLocked(seed, 10)));
                Assert.That(LockedChestSpawnRules.ShouldSpawnLocked(seed, 10, 0, false),
                    Is.EqualTo(LockedChestSpawnRules.ShouldSpawnLocked(seed, 10)));
                Assert.That(LockedChestSpawnRules.ShouldSpawnLocked(seed, 0, 0, true), Is.False,
                    "Disabled variants must not require a locked prefab in legacy scenes.");
            }
        }

        [TestCase(0UL)]
        [TestCase(123456789UL)]
        [TestCase(ulong.MaxValue)]
        public void DisabledAndGuaranteedChances_KeepTheirBoundaryBehavior(ulong seed)
        {
            Assert.That(LockedChestSpawnRules.ShouldSpawnLocked(seed, 0), Is.False);
            Assert.That(LockedChestSpawnRules.ShouldSpawnLocked(seed, 100), Is.True);
        }

        [Test]
        public void TenPercent_ProducesBothVariantsAtTheConfiguredRate()
        {
            int lockedCount = 0;
            const int sampleCount = 100_000;
            for (ulong seed = 0; seed < sampleCount; seed++)
            {
                if (LockedChestSpawnRules.ShouldSpawnLocked(seed, 10))
                {
                    lockedCount++;
                }
            }

            Assert.That(lockedCount, Is.InRange(9500, 10500));
        }

        [Test]
        public void RetryingTheSamePoint_RemainsStableAfterOtherSelections()
        {
            const ulong seed = 145_976UL;
            bool firstSelection = LockedChestSpawnRules.ShouldSpawnLocked(seed, 10);
            for (ulong otherSeed = 0; otherSeed < 1000; otherSeed++)
            {
                LockedChestSpawnRules.ShouldSpawnLocked(otherSeed, 10);
            }

            Assert.That(LockedChestSpawnRules.ShouldSpawnLocked(seed, 10), Is.EqualTo(firstSelection));
        }
    }
}
