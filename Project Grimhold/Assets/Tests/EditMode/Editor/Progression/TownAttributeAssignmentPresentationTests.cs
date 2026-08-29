#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using NUnit.Framework;

namespace Tests.EditMode.Progression
{
    public sealed class TownAttributeAssignmentPresentationTests
    {
        [Test]
        public void Projection_ContainsEveryValueAndMatchesAssignmentRules()
        {
            Assert.That(CharacterAttributeState.TryCreate(
                5, 25, 26, 7, 8, 9, 3, out CharacterAttributeState state), Is.True);
            Assert.That(TownAttributeAssignmentPresentation.TryCreate(
                state,
                ProgressionBalanceDefaults.InitialMaximumAttributeValue,
                out TownAttributeAssignmentPresentation presentation), Is.True);

            Assert.That(presentation.AvailablePoints, Is.EqualTo(3));
            foreach (CharacterAttribute attribute in System.Enum.GetValues(typeof(CharacterAttribute)))
            {
                Assert.That(presentation.TryGet(attribute, out int value, out bool canAssign), Is.True);
                Assert.That(state.TryGetValue(attribute, out int expectedValue), Is.True);
                bool expectedCanAssign = CharacterAttributeAssignmentRules.TryAssign(
                    ProgressionBalanceDefaults.InitialMaximumAttributeValue,
                    state,
                    attribute,
                    out _,
                    out _);
                Assert.That(value, Is.EqualTo(expectedValue));
                Assert.That(canAssign, Is.EqualTo(expectedCanAssign));
            }
        }

        [Test]
        public void ZeroPoints_DisablesEveryAssignment()
        {
            Assert.That(CharacterAttributeState.TryCreate(
                5, 5, 5, 5, 5, 5, 0, out CharacterAttributeState state), Is.True);
            Assert.That(TownAttributeAssignmentPresentation.TryCreate(
                state, 25, out TownAttributeAssignmentPresentation presentation), Is.True);

            foreach (CharacterAttribute attribute in System.Enum.GetValues(typeof(CharacterAttribute)))
            {
                Assert.That(presentation.TryGet(attribute, out _, out bool canAssign), Is.True);
                Assert.That(canAssign, Is.False);
            }
        }

        [Test]
        public void InvalidMaximum_RejectsPresentation()
        {
            Assert.That(TownAttributeAssignmentPresentation.TryCreate(
                ProgressionBalanceDefaults.InitialCharacterAttributeState,
                -1,
                out _), Is.False);
        }
    }
}
#endif
