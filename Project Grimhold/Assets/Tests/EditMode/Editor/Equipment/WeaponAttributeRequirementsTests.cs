#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using NUnit.Framework;

namespace Tests.EditMode.Equipment
{
    [TestFixture]
    public sealed class WeaponAttributeRequirementsTests
    {
        [TestCase(5, 0, 0, 5, 0, 0, true)]
        [TestCase(5, 0, 0, 4, 30, 30, false)]
        [TestCase(0, 10, 0, 0, 10, 0, true)]
        [TestCase(0, 10, 0, 30, 9, 30, false)]
        [TestCase(0, 0, 15, 0, 0, 15, true)]
        [TestCase(0, 0, 15, 30, 30, 14, false)]
        [TestCase(5, 10, 15, 5, 10, 15, true)]
        [TestCase(5, 10, 15, 5, 9, 15, false)]
        public void IsSatisfiedBy_EvaluatesEveryConfiguredMinimum(
            int requiredStrength,
            int requiredDexterity,
            int requiredIntelligence,
            int strength,
            int dexterity,
            int intelligence,
            bool expected)
        {
            var requirements = new WeaponAttributeRequirements(
                requiredStrength,
                requiredDexterity,
                requiredIntelligence);
            CharacterAttributeState attributes = CreateAttributes(strength, dexterity, intelligence);

            Assert.That(requirements.TryValidate(out string error), Is.True, error);
            Assert.That(requirements.IsSatisfiedBy(attributes), Is.EqualTo(expected));
        }

        [Test]
        public void EmptyRequirements_AcceptEveryStructurallyValidDistribution()
        {
            var requirements = new WeaponAttributeRequirements(0, 0, 0);
            CharacterAttributeState[] distributions =
            {
                CreateAttributes(0, 0, 0),
                CreateAttributes(30, 0, 0),
                CreateAttributes(0, 30, 0),
                CreateAttributes(0, 0, 30),
                CreateAttributes(7, 11, 13)
            };

            for (int index = 0; index < distributions.Length; index++)
            {
                Assert.That(requirements.IsSatisfiedBy(distributions[index]), Is.True, index.ToString());
            }
        }

        [TestCase(-1, 0, 0)]
        [TestCase(0, -1, 0)]
        [TestCase(0, 0, -1)]
        public void NegativeRequirement_IsInvalidAndNeverEligible(
            int strength,
            int dexterity,
            int intelligence)
        {
            var requirements = new WeaponAttributeRequirements(strength, dexterity, intelligence);

            Assert.That(requirements.TryValidate(out string error), Is.False);
            Assert.That(error, Does.Contain("negative"));
            Assert.That(
                requirements.IsSatisfiedBy(CreateAttributes(30, 30, 30)),
                Is.False);
        }

        private static CharacterAttributeState CreateAttributes(
            int strength,
            int dexterity,
            int intelligence)
        {
            Assert.That(
                CharacterAttributeState.TryCreate(
                    0,
                    0,
                    strength,
                    dexterity,
                    intelligence,
                    0,
                    0,
                    out CharacterAttributeState state),
                Is.True);
            return state;
        }
    }
}
#endif
