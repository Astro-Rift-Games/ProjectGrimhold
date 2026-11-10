using NUnit.Framework;

namespace Tests.EditMode.Progression
{
    public sealed class CharacterAttributePointGrantRulesTests
    {
        [Test]
        public void Apply_LevelOneWithoutLevelGainIsConsumedAsNoOp()
        {
            CharacterAttributeState state = ProgressionBalanceDefaults.InitialCharacterAttributeState;
            ExperienceApplicationResult progressionResult = CreateProgressionResult(1, 0, 1, 50, 0);

            Assert.That(TryApply(
                ProgressionBalanceDefaults.InitialAttributePointsPerLevel,
                default,
                state,
                progressionResult,
                out CharacterAttributePointGrant grant,
                out CharacterAttributePointGrantFailure failure), Is.True);

            Assert.That(failure, Is.EqualTo(CharacterAttributePointGrantFailure.None));
            AssertGrant(grant, 0, state);
        }

        [Test]
        public void Apply_OneLevelAddsOneInitialBalancePoint()
        {
            CharacterAttributeState state = ProgressionBalanceDefaults.InitialCharacterAttributeState;
            ExperienceApplicationResult progressionResult = CreateProgressionResult(1, 90, 2, 25, 1);

            Assert.That(TryApply(
                ProgressionBalanceDefaults.InitialAttributePointsPerLevel,
                default,
                state,
                progressionResult,
                out CharacterAttributePointGrant grant,
                out _), Is.True);

            Assert.That(grant.GrantedPoints, Is.EqualTo(1));
            Assert.That(grant.Result.AvailablePoints, Is.EqualTo(11));
        }

        [Test]
        public void Apply_MultipleLevelsAddOneInitialBalancePointPerLevel()
        {
            CharacterAttributeState state = ProgressionBalanceDefaults.InitialCharacterAttributeState;
            ExperienceApplicationResult progressionResult = CreateProgressionResult(1, 90, 4, 40, 3);

            Assert.That(TryApply(
                ProgressionBalanceDefaults.InitialAttributePointsPerLevel,
                default,
                state,
                progressionResult,
                out CharacterAttributePointGrant grant,
                out _), Is.True);

            Assert.That(grant.GrantedPoints, Is.EqualTo(3));
            Assert.That(grant.Result.AvailablePoints, Is.EqualTo(13));
        }

        [Test]
        public void Apply_UsesPositiveConfiguredPointsPerLevel()
        {
            CharacterAttributeState state = ProgressionBalanceDefaults.InitialCharacterAttributeState;
            ExperienceApplicationResult progressionResult = CreateProgressionResult(2, 10, 4, 20, 2);

            Assert.That(TryApply(
                3,
                default,
                state,
                progressionResult,
                out CharacterAttributePointGrant grant,
                out _), Is.True);

            Assert.That(grant.GrantedPoints, Is.EqualTo(6));
            Assert.That(grant.Result.AvailablePoints, Is.EqualTo(16));
        }

        [Test]
        public void Apply_PreservesEveryAttributeValue()
        {
            Assert.That(CharacterAttributeState.TryCreate(1, 2, 3, 4, 5, 6, 7, out CharacterAttributeState state), Is.True);
            ExperienceApplicationResult progressionResult = CreateProgressionResult(3, 10, 4, 0, 1);

            Assert.That(TryApply(1, default, state, progressionResult, out CharacterAttributePointGrant grant, out _), Is.True);

            Assert.That(grant.Result.Vitality, Is.EqualTo(1));
            Assert.That(grant.Result.Resistance, Is.EqualTo(2));
            Assert.That(grant.Result.Strength, Is.EqualTo(3));
            Assert.That(grant.Result.Dexterity, Is.EqualTo(4));
            Assert.That(grant.Result.Intelligence, Is.EqualTo(5));
            Assert.That(grant.Result.Luck, Is.EqualTo(6));
            Assert.That(grant.Result.AvailablePoints, Is.EqualTo(8));
        }

        [Test]
        public void Apply_ConsumedGrantRejectsBeforeOtherValidationAndPreservesResult()
        {
            CharacterAttributeState state = ProgressionBalanceDefaults.InitialCharacterAttributeState;
            ExperienceApplicationResult progressionResult = CreateProgressionResult(1, 90, 2, 25, 1);
            Assert.That(TryApply(1, default, state, progressionResult, out CharacterAttributePointGrant first, out _), Is.True);

            Assert.That(TryApply(
                0,
                first,
                default,
                default,
                out CharacterAttributePointGrant candidate,
                out CharacterAttributePointGrantFailure failure), Is.False);

            Assert.That(failure, Is.EqualTo(CharacterAttributePointGrantFailure.AlreadyApplied));
            AssertGrant(candidate, first.GrantedPoints, first.Result);
        }

        [TestCase(0, 0, 0, 0, 0)]
        [TestCase(2, 0, 1, 0, -1)]
        [TestCase(1, 0, 1, 0, -1)]
        [TestCase(1, 0, 3, 0, 1)]
        [TestCase(1, -1, 1, 0, 0)]
        [TestCase(1, 0, 1, -1, 0)]
        public void Apply_StructurallyInvalidProgressionResultIsRejected(
            int previousLevel,
            long previousExperience,
            int resultingLevel,
            long resultingExperience,
            int levelsGained)
        {
            ExperienceApplicationResult progressionResult = CreateProgressionResult(
                previousLevel,
                previousExperience,
                resultingLevel,
                resultingExperience,
                levelsGained);

            Assert.That(TryApply(
                1,
                default,
                default,
                progressionResult,
                out CharacterAttributePointGrant candidate,
                out CharacterAttributePointGrantFailure failure), Is.False);

            Assert.That(failure, Is.EqualTo(CharacterAttributePointGrantFailure.InvalidProgressionResult));
            AssertPending(candidate);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void Apply_NonPositivePointsPerLevelIsRejected(int pointsPerLevel)
        {
            ExperienceApplicationResult progressionResult = CreateProgressionResult(1, 0, 2, 0, 1);

            Assert.That(TryApply(
                pointsPerLevel,
                default,
                default,
                progressionResult,
                out CharacterAttributePointGrant candidate,
                out CharacterAttributePointGrantFailure failure), Is.False);

            Assert.That(failure, Is.EqualTo(CharacterAttributePointGrantFailure.InvalidPointsPerLevel));
            AssertPending(candidate);
        }

        [Test]
        public void Apply_GrantedPointMultiplicationOverflowIsRejected()
        {
            ExperienceApplicationResult progressionResult = CreateProgressionResult(
                1,
                0,
                int.MaxValue,
                0,
                int.MaxValue - 1);

            AssertOverflow(2, default, progressionResult);
        }

        [Test]
        public void Apply_AvailablePointAdditionOverflowIsRejected()
        {
            Assert.That(CharacterAttributeState.TryCreate(0, 0, 0, 0, 0, 0, int.MaxValue,
                out CharacterAttributeState state), Is.True);
            ExperienceApplicationResult progressionResult = CreateProgressionResult(1, 0, 2, 0, 1);

            AssertOverflow(1, state, progressionResult);
        }

        [Test]
        public void Apply_IndependentPendingGrantsWithSameResultBothApplyDeterministically()
        {
            CharacterAttributeState state = ProgressionBalanceDefaults.InitialCharacterAttributeState;
            ExperienceApplicationResult progressionResult = CreateProgressionResult(2, 50, 5, 10, 3);

            Assert.That(TryApply(2, default, state, progressionResult, out CharacterAttributePointGrant first,
                out CharacterAttributePointGrantFailure firstFailure), Is.True);
            Assert.That(TryApply(2, default, state, progressionResult, out CharacterAttributePointGrant second,
                out CharacterAttributePointGrantFailure secondFailure), Is.True);

            Assert.That(secondFailure, Is.EqualTo(firstFailure));
            AssertGrant(second, first.GrantedPoints, first.Result);
        }

        private static ExperienceApplicationResult CreateProgressionResult(
            int previousLevel,
            long previousExperience,
            int resultingLevel,
            long resultingExperience,
            int levelsGained) =>
            new ExperienceApplicationResult(
                previousLevel,
                previousExperience,
                resultingLevel,
                resultingExperience,
                levelsGained);

        private static bool TryApply(
            int pointsPerLevel,
            in CharacterAttributePointGrant previous,
            in CharacterAttributeState currentState,
            in ExperienceApplicationResult progressionResult,
            out CharacterAttributePointGrant candidate,
            out CharacterAttributePointGrantFailure failure) =>
            CharacterAttributePointGrantRules.TryApply(
                pointsPerLevel,
                previous,
                currentState,
                progressionResult,
                out candidate,
                out failure);

        private static void AssertOverflow(
            int pointsPerLevel,
            in CharacterAttributeState state,
            in ExperienceApplicationResult progressionResult)
        {
            Assert.That(TryApply(
                pointsPerLevel,
                default,
                state,
                progressionResult,
                out CharacterAttributePointGrant candidate,
                out CharacterAttributePointGrantFailure failure), Is.False);
            Assert.That(failure, Is.EqualTo(CharacterAttributePointGrantFailure.AvailablePointsOverflow));
            AssertPending(candidate);
        }

        private static void AssertGrant(
            in CharacterAttributePointGrant grant,
            int grantedPoints,
            in CharacterAttributeState result)
        {
            Assert.That(grant.IsApplied, Is.True);
            Assert.That(grant.GrantedPoints, Is.EqualTo(grantedPoints));
            Assert.That(grant.Result, Is.EqualTo(result));
        }

        private static void AssertPending(in CharacterAttributePointGrant grant)
        {
            Assert.That(grant.IsApplied, Is.False);
            Assert.That(grant.GrantedPoints, Is.Zero);
            Assert.That(grant.Result, Is.EqualTo(default(CharacterAttributeState)));
        }
    }
}
