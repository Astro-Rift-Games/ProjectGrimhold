using System.Collections.Generic;
using NUnit.Framework;

namespace Tests.EditMode.Progression
{
    public sealed class RaidLootEligibilityResolverTests
    {
        [Test]
        public void Resolve_ClassifiesDungeonOwnTeammateAndEnemyQuantities()
        {
            CreateAffiliations(
                out RaidInitialAffiliationSnapshot affiliations,
                out RaidParticipantId alpha,
                out RaidParticipantId beta,
                out RaidParticipantId gamma);
            LootId coin = new("coin");
            LootEntry[] total = { new(coin, 10) };
            RaidLootOriginEntry[] origins =
            {
                new(coin, RaidLootOrigin.Dungeon, 2),
                PlayerOrigin(coin, alpha, 2),
                PlayerOrigin(coin, beta, 3),
                PlayerOrigin(coin, gamma, 3)
            };

            Assert.That(
                RaidLootEligibilityResolver.TryResolve(
                    alpha, total, origins, affiliations, out RaidLootEligibilitySnapshot forAlpha, out string error),
                Is.True,
                error);
            Assert.That(forAlpha.TotalAmount, Is.EqualTo(10));
            Assert.That(forAlpha.EligibleAmount, Is.EqualTo(5));
            Assert.That(forAlpha.IneligibleAmount, Is.EqualTo(5));
            Assert.That(forAlpha.Entries[0].EligibleAmount, Is.EqualTo(5));

            Assert.That(
                RaidLootEligibilityResolver.TryResolve(
                    gamma, total, origins, affiliations, out RaidLootEligibilitySnapshot forGamma, out error),
                Is.True,
                error);
            Assert.That(forGamma.EligibleAmount, Is.EqualTo(7));
            Assert.That(forGamma.IneligibleAmount, Is.EqualTo(3));
        }

        [Test]
        public void Resolve_OrdersLootIdsAndProducesExactMixedStackTotals()
        {
            CreateAffiliations(
                out RaidInitialAffiliationSnapshot affiliations,
                out RaidParticipantId alpha,
                out _,
                out RaidParticipantId gamma);
            LootId zeta = new("zeta");
            LootId alphaLoot = new("alpha-loot");
            LootEntry[] total = { new(zeta, 4), new(alphaLoot, 3) };
            RaidLootOriginEntry[] origins =
            {
                PlayerOrigin(zeta, gamma, 4),
                new(alphaLoot, RaidLootOrigin.Dungeon, 2),
                PlayerOrigin(alphaLoot, alpha, 1)
            };

            Assert.That(
                RaidLootEligibilityResolver.TryResolve(
                    alpha, total, origins, affiliations, out RaidLootEligibilitySnapshot result, out string error),
                Is.True,
                error);

            Assert.That(result.Entries[0].LootId, Is.EqualTo(alphaLoot));
            Assert.That(result.Entries[0].EligibleAmount, Is.EqualTo(2));
            Assert.That(result.Entries[0].IneligibleAmount, Is.EqualTo(1));
            Assert.That(result.Entries[1].LootId, Is.EqualTo(zeta));
            Assert.That(result.Entries[1].EligibleAmount, Is.EqualTo(4));
            Assert.That(result.TotalAmount, Is.EqualTo(7));
            Assert.That(result.EligibleAmount, Is.EqualTo(6));
            Assert.That(result.IneligibleAmount, Is.EqualTo(1));
        }

        [Test]
        public void Resolve_IsRepeatableAndDoesNotModifyOriginsOrExperience()
        {
            CreateAffiliations(
                out RaidInitialAffiliationSnapshot affiliations,
                out RaidParticipantId alpha,
                out _,
                out RaidParticipantId gamma);
            LootId lootId = new("loot");
            RaidLootOriginEntry[] origins =
            {
                new(lootId, RaidLootOrigin.Dungeon, 1),
                PlayerOrigin(lootId, gamma, 2)
            };
            RaidLootOriginEntry[] original = (RaidLootOriginEntry[])origins.Clone();
            var experience = new ExpeditionExperienceSnapshot(3, 4, 5, 0);

            Assert.That(RaidLootEligibilityResolver.TryResolve(
                alpha, new[] { new LootEntry(lootId, 3) }, origins, affiliations,
                out RaidLootEligibilitySnapshot first, out string firstError), Is.True, firstError);
            Assert.That(RaidLootEligibilityResolver.TryResolve(
                alpha, new[] { new LootEntry(lootId, 3) }, origins, affiliations,
                out RaidLootEligibilitySnapshot second, out string secondError), Is.True, secondError);

            Assert.That(second.EligibleAmount, Is.EqualTo(first.EligibleAmount));
            Assert.That(second.IneligibleAmount, Is.EqualTo(first.IneligibleAmount));
            Assert.That(origins, Is.EqualTo(original));
            Assert.That(experience.ExtractedLootExperience, Is.Zero);
        }

        [Test]
        public void Resolve_RejectsUnknownParticipantsInvalidTotalsDuplicatesAndOverflowWithoutResult()
        {
            CreateAffiliations(
                out RaidInitialAffiliationSnapshot affiliations,
                out RaidParticipantId alpha,
                out _,
                out _);
            LootId lootId = new("loot");
            RaidParticipantId.TryCreate(16, out RaidParticipantId missing);

            AssertRejected(
                missing,
                new[] { new LootEntry(lootId, 1) },
                new[] { new RaidLootOriginEntry(lootId, RaidLootOrigin.Dungeon, 1) },
                affiliations);
            AssertRejected(
                alpha,
                new[] { new LootEntry(lootId, 1) },
                new[] { PlayerOrigin(lootId, missing, 1) },
                affiliations);
            AssertRejected(
                alpha,
                new[] { new LootEntry(lootId, 1) },
                new[] { new RaidLootOriginEntry(lootId, default, 1) },
                affiliations);
            AssertRejected(
                alpha,
                new[] { new LootEntry(lootId, 2) },
                new[] { new RaidLootOriginEntry(lootId, RaidLootOrigin.Dungeon, 1) },
                affiliations);
            AssertRejected(
                alpha,
                new[] { new LootEntry(lootId, 1) },
                new[] { new RaidLootOriginEntry(lootId, RaidLootOrigin.Dungeon, 0) },
                affiliations);
            AssertRejected(
                alpha,
                new[] { new LootEntry(lootId, 2) },
                new[]
                {
                    new RaidLootOriginEntry(lootId, RaidLootOrigin.Dungeon, 1),
                    new RaidLootOriginEntry(lootId, RaidLootOrigin.Dungeon, 1)
                },
                affiliations);
            AssertRejected(
                alpha,
                new[] { new LootEntry(lootId, int.MaxValue) },
                new[]
                {
                    new RaidLootOriginEntry(lootId, RaidLootOrigin.Dungeon, int.MaxValue),
                    PlayerOrigin(lootId, alpha, 1)
                },
                affiliations);
        }

        private static void CreateAffiliations(
            out RaidInitialAffiliationSnapshot affiliations,
            out RaidParticipantId alphaId,
            out RaidParticipantId betaId,
            out RaidParticipantId gammaId)
        {
            ProfileId alpha = new("alpha");
            ProfileId beta = new("beta");
            ProfileId gamma = new("gamma");
            ProfileId[] profiles = { gamma, alpha, beta };
            RaidTeamId.TryCreate(1, out RaidTeamId allies);
            RaidTeamId.TryCreate(2, out RaidTeamId enemies);
            RaidLaunchParticipant[] participants =
            {
                new(gamma, enemies),
                new(alpha, allies),
                new(beta, allies)
            };

            Assert.That(RaidInitialAffiliationSnapshot.TryCreate(participants, out affiliations), Is.True);
            Assert.That(RaidParticipantIdAssignment.TryResolve(profiles, alpha, out alphaId), Is.True);
            Assert.That(RaidParticipantIdAssignment.TryResolve(profiles, beta, out betaId), Is.True);
            Assert.That(RaidParticipantIdAssignment.TryResolve(profiles, gamma, out gammaId), Is.True);
        }

        private static RaidLootOriginEntry PlayerOrigin(
            LootId lootId,
            RaidParticipantId participantId,
            int amount)
        {
            Assert.That(RaidLootOrigin.TryCreatePlayer(participantId, out RaidLootOrigin origin), Is.True);
            return new RaidLootOriginEntry(lootId, origin, amount);
        }

        private static void AssertRejected(
            RaidParticipantId extractorId,
            IReadOnlyList<LootEntry> total,
            IReadOnlyList<RaidLootOriginEntry> origins,
            RaidInitialAffiliationSnapshot affiliations)
        {
            Assert.That(
                RaidLootEligibilityResolver.TryResolve(
                    extractorId,
                    total,
                    origins,
                    affiliations,
                    out RaidLootEligibilitySnapshot result,
                    out string error),
                Is.False);
            Assert.That(result, Is.Null);
            Assert.That(error, Is.Not.Null.And.Not.Empty);
        }
    }
}
