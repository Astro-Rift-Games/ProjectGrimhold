using NUnit.Framework;

namespace Tests.EditMode.Progression
{
    public sealed class CharacterAttributeStateTests
    {
        [Test]
        public void InitialState_UsesDocumentedAttributeAndAvailablePointValues()
        {
            CharacterAttributeState state = ProgressionBalanceDefaults.InitialCharacterAttributeState;

            Assert.That(state.Vitality, Is.EqualTo(5));
            Assert.That(state.Resistance, Is.EqualTo(5));
            Assert.That(state.Strength, Is.EqualTo(5));
            Assert.That(state.Dexterity, Is.EqualTo(5));
            Assert.That(state.Intelligence, Is.EqualTo(5));
            Assert.That(state.Luck, Is.EqualTo(5));
            Assert.That(state.AvailablePoints, Is.EqualTo(10));
        }

        [Test]
        public void Create_AllZeroValuesProducesDefaultValidState()
        {
            Assert.That(TryCreate(0, 0, 0, 0, 0, 0, 0, out CharacterAttributeState state), Is.True);
            Assert.That(state, Is.EqualTo(default(CharacterAttributeState)));
        }

        [TestCase(-1, 0, 0, 0, 0, 0, 0)]
        [TestCase(0, -1, 0, 0, 0, 0, 0)]
        [TestCase(0, 0, -1, 0, 0, 0, 0)]
        [TestCase(0, 0, 0, -1, 0, 0, 0)]
        [TestCase(0, 0, 0, 0, -1, 0, 0)]
        [TestCase(0, 0, 0, 0, 0, -1, 0)]
        [TestCase(0, 0, 0, 0, 0, 0, -1)]
        public void Create_NegativeValueIsRejectedWithoutPartialState(
            int vitality,
            int resistance,
            int strength,
            int dexterity,
            int intelligence,
            int luck,
            int availablePoints)
        {
            Assert.That(TryCreate(
                vitality,
                resistance,
                strength,
                dexterity,
                intelligence,
                luck,
                availablePoints,
                out CharacterAttributeState state), Is.False);
            Assert.That(state, Is.EqualTo(default(CharacterAttributeState)));
        }

        [Test]
        public void Create_ValueAboveProvisionalBalanceMaximumRemainsStructurallyValid()
        {
            Assert.That(TryCreate(26, 0, 0, 0, 0, 0, 0, out CharacterAttributeState state), Is.True);
            Assert.That(state.Vitality, Is.EqualTo(26));
        }

        [TestCase(CharacterAttribute.Vitality, 1)]
        [TestCase(CharacterAttribute.Resistance, 2)]
        [TestCase(CharacterAttribute.Strength, 3)]
        [TestCase(CharacterAttribute.Dexterity, 4)]
        [TestCase(CharacterAttribute.Intelligence, 5)]
        [TestCase(CharacterAttribute.Luck, 6)]
        public void GetValue_KnownAttributeReturnsItsValue(CharacterAttribute attribute, int expected)
        {
            TryCreate(1, 2, 3, 4, 5, 6, 7, out CharacterAttributeState state);

            Assert.That(state.TryGetValue(attribute, out int value), Is.True);
            Assert.That(value, Is.EqualTo(expected));
        }

        [Test]
        public void GetValue_UnknownAttributeIsRejectedWithoutUsableValue()
        {
            TryCreate(1, 2, 3, 4, 5, 6, 7, out CharacterAttributeState state);

            Assert.That(state.TryGetValue((CharacterAttribute)int.MaxValue, out int value), Is.False);
            Assert.That(value, Is.Zero);
        }

        [Test]
        public void Equality_UsesEveryStoredValue()
        {
            TryCreate(1, 2, 3, 4, 5, 6, 7, out CharacterAttributeState expected);
            TryCreate(1, 2, 3, 4, 5, 6, 7, out CharacterAttributeState equal);

            Assert.That(equal.Equals(expected), Is.True);
            Assert.That(equal.Equals((object)expected), Is.True);
            Assert.That(equal.GetHashCode(), Is.EqualTo(expected.GetHashCode()));

            AssertDifferent(expected, 0, 2, 3, 4, 5, 6, 7);
            AssertDifferent(expected, 1, 0, 3, 4, 5, 6, 7);
            AssertDifferent(expected, 1, 2, 0, 4, 5, 6, 7);
            AssertDifferent(expected, 1, 2, 3, 0, 5, 6, 7);
            AssertDifferent(expected, 1, 2, 3, 4, 0, 6, 7);
            AssertDifferent(expected, 1, 2, 3, 4, 5, 0, 7);
            AssertDifferent(expected, 1, 2, 3, 4, 5, 6, 0);
        }

        private static bool TryCreate(
            int vitality,
            int resistance,
            int strength,
            int dexterity,
            int intelligence,
            int luck,
            int availablePoints,
            out CharacterAttributeState state) =>
            CharacterAttributeState.TryCreate(
                vitality,
                resistance,
                strength,
                dexterity,
                intelligence,
                luck,
                availablePoints,
                out state);

        private static void AssertDifferent(
            CharacterAttributeState expected,
            int vitality,
            int resistance,
            int strength,
            int dexterity,
            int intelligence,
            int luck,
            int availablePoints)
        {
            Assert.That(TryCreate(
                vitality,
                resistance,
                strength,
                dexterity,
                intelligence,
                luck,
                availablePoints,
                out CharacterAttributeState state), Is.True);
            Assert.That(state.Equals(expected), Is.False);
        }
    }
}
