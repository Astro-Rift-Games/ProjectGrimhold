using NUnit.Framework;

namespace Tests.EditMode.Progression
{
    public sealed class CharacterAttributeAssignmentRulesTests
    {
        [TestCase(CharacterAttribute.Vitality, 2, 2, 3, 4, 5, 6)]
        [TestCase(CharacterAttribute.Resistance, 1, 3, 3, 4, 5, 6)]
        [TestCase(CharacterAttribute.Strength, 1, 2, 4, 4, 5, 6)]
        [TestCase(CharacterAttribute.Dexterity, 1, 2, 3, 5, 5, 6)]
        [TestCase(CharacterAttribute.Intelligence, 1, 2, 3, 4, 6, 6)]
        [TestCase(CharacterAttribute.Luck, 1, 2, 3, 4, 5, 7)]
        public void Assign_KnownAttributeIncrementsOnlySelectionAndConsumesOnePoint(
            CharacterAttribute attribute,
            int vitality,
            int resistance,
            int strength,
            int dexterity,
            int intelligence,
            int luck)
        {
            CharacterAttributeState state = CreateState(1, 2, 3, 4, 5, 6, 7);
            CharacterAttributeState expected = CreateState(
                vitality,
                resistance,
                strength,
                dexterity,
                intelligence,
                luck,
                6);

            Assert.That(TryAssign(25, state, attribute, out CharacterAttributeState candidate,
                out CharacterAttributeAssignmentFailure failure), Is.True);
            Assert.That(failure, Is.EqualTo(CharacterAttributeAssignmentFailure.None));
            Assert.That(candidate, Is.EqualTo(expected));
        }

        [Test]
        public void Assign_LastAvailablePointLeavesZeroPoints()
        {
            CharacterAttributeState state = CreateState(5, 5, 5, 5, 5, 5, 1);

            Assert.That(TryAssign(25, state, CharacterAttribute.Vitality, out CharacterAttributeState candidate,
                out _), Is.True);
            Assert.That(candidate.Vitality, Is.EqualTo(6));
            Assert.That(candidate.AvailablePoints, Is.Zero);
        }

        [Test]
        public void Assign_NoAvailablePointsIsRejectedWithoutChangingState()
        {
            CharacterAttributeState state = CreateState(5, 5, 5, 5, 5, 5, 0);

            AssertFailure(
                25,
                state,
                CharacterAttribute.Vitality,
                CharacterAttributeAssignmentFailure.NoAvailablePoints);
        }

        [Test]
        public void Assign_UnknownAttributeIsRejectedBeforeAvailablePointValidation()
        {
            CharacterAttributeState state = CreateState(5, 5, 5, 5, 5, 5, 0);

            AssertFailure(
                25,
                state,
                (CharacterAttribute)int.MaxValue,
                CharacterAttributeAssignmentFailure.UnknownAttribute);
        }

        [Test]
        public void Assign_ValueBelowInitialMaximumCanReachMaximum()
        {
            CharacterAttributeState state = CreateState(24, 5, 5, 5, 5, 5, 1);

            Assert.That(TryAssign(
                ProgressionBalanceDefaults.InitialMaximumAttributeValue,
                state,
                CharacterAttribute.Vitality,
                out CharacterAttributeState candidate,
                out _), Is.True);
            Assert.That(candidate.Vitality, Is.EqualTo(25));
            Assert.That(candidate.AvailablePoints, Is.Zero);
        }

        [TestCase(25)]
        [TestCase(26)]
        public void Assign_ValueAtOrAboveInitialMaximumIsRejected(int value)
        {
            CharacterAttributeState state = CreateState(value, 5, 5, 5, 5, 5, 1);

            AssertFailure(
                ProgressionBalanceDefaults.InitialMaximumAttributeValue,
                state,
                CharacterAttribute.Vitality,
                CharacterAttributeAssignmentFailure.AttributeAtMaximum);
        }

        [Test]
        public void Assign_UsesConfiguredMaximumWithoutRewritingRule()
        {
            CharacterAttributeState state = CreateState(30, 5, 5, 5, 5, 5, 1);

            Assert.That(TryAssign(31, state, CharacterAttribute.Vitality, out CharacterAttributeState candidate,
                out _), Is.True);
            Assert.That(candidate.Vitality, Is.EqualTo(31));
            Assert.That(candidate.AvailablePoints, Is.Zero);
        }

        [Test]
        public void Assign_NegativeMaximumIsRejectedBeforeOtherValidation()
        {
            CharacterAttributeState state = CreateState(5, 5, 5, 5, 5, 5, 0);

            AssertFailure(
                -1,
                state,
                (CharacterAttribute)int.MaxValue,
                CharacterAttributeAssignmentFailure.InvalidMaximumAttributeValue);
        }

        [TestCase(0)]
        [TestCase(5)]
        public void Assign_ZeroMaximumRejectsEveryStructurallyNonNegativeSelectedValue(int value)
        {
            CharacterAttributeState state = CreateState(value, 5, 5, 5, 5, 5, 1);

            AssertFailure(
                0,
                state,
                CharacterAttribute.Vitality,
                CharacterAttributeAssignmentFailure.AttributeAtMaximum);
        }

        [Test]
        public void Assign_ValueBelowIntMaximumCanReachIntMaximumWithoutOverflow()
        {
            CharacterAttributeState state = CreateState(int.MaxValue - 1, 0, 0, 0, 0, 0, 1);

            Assert.That(TryAssign(int.MaxValue, state, CharacterAttribute.Vitality,
                out CharacterAttributeState candidate, out _), Is.True);
            Assert.That(candidate.Vitality, Is.EqualTo(int.MaxValue));
            Assert.That(candidate.AvailablePoints, Is.Zero);
        }

        [Test]
        public void Assign_IntMaximumValueIsRejectedWithoutOverflow()
        {
            CharacterAttributeState state = CreateState(int.MaxValue, 0, 0, 0, 0, 0, 1);

            AssertFailure(
                int.MaxValue,
                state,
                CharacterAttribute.Vitality,
                CharacterAttributeAssignmentFailure.AttributeAtMaximum);
        }

        [Test]
        public void Assign_IdenticalInputsProduceIdenticalResults()
        {
            CharacterAttributeState state = CreateState(10, 11, 12, 13, 14, 15, 4);

            Assert.That(TryAssign(25, state, CharacterAttribute.Dexterity,
                out CharacterAttributeState first, out CharacterAttributeAssignmentFailure firstFailure), Is.True);
            Assert.That(TryAssign(25, state, CharacterAttribute.Dexterity,
                out CharacterAttributeState second, out CharacterAttributeAssignmentFailure secondFailure), Is.True);

            Assert.That(second, Is.EqualTo(first));
            Assert.That(secondFailure, Is.EqualTo(firstFailure));
        }

        private static bool TryAssign(
            int maximumAttributeValue,
            in CharacterAttributeState state,
            CharacterAttribute attribute,
            out CharacterAttributeState candidate,
            out CharacterAttributeAssignmentFailure failure) =>
            CharacterAttributeAssignmentRules.TryAssign(
                maximumAttributeValue,
                state,
                attribute,
                out candidate,
                out failure);

        private static CharacterAttributeState CreateState(
            int vitality,
            int resistance,
            int strength,
            int dexterity,
            int intelligence,
            int luck,
            int availablePoints)
        {
            Assert.That(CharacterAttributeState.TryCreate(
                vitality,
                resistance,
                strength,
                dexterity,
                intelligence,
                luck,
                availablePoints,
                out CharacterAttributeState state), Is.True);
            return state;
        }

        private static void AssertFailure(
            int maximumAttributeValue,
            in CharacterAttributeState state,
            CharacterAttribute attribute,
            CharacterAttributeAssignmentFailure expectedFailure)
        {
            Assert.That(TryAssign(
                maximumAttributeValue,
                state,
                attribute,
                out CharacterAttributeState candidate,
                out CharacterAttributeAssignmentFailure failure), Is.False);
            Assert.That(failure, Is.EqualTo(expectedFailure));
            Assert.That(candidate, Is.EqualTo(state));
        }
    }
}
