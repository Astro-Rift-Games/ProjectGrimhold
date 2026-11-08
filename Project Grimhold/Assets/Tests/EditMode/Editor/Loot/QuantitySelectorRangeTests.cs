#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using NUnit.Framework;

namespace Tests.EditMode.Loot
{
    public sealed class QuantitySelectorRangeTests
    {
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(-4)]
        public void TryCreate_StacksBelowTwoHaveNoSelectableRange(int maxAmount)
        {
            Assert.That(QuantitySelectorRange.TryCreate(maxAmount, out QuantitySelectorRange range), Is.False);
            Assert.That(range.IsValid, Is.False);
        }

        [Test]
        public void TryCreate_RangeIsOneToMaxAmountMinusOne()
        {
            Assert.That(QuantitySelectorRange.TryCreate(13, out QuantitySelectorRange range), Is.True);
            Assert.That(QuantitySelectorRange.Min, Is.EqualTo(1));
            Assert.That(range.Max, Is.EqualTo(12));
            Assert.That(range.Contains(0), Is.False);
            Assert.That(range.Contains(1), Is.True);
            Assert.That(range.Contains(12), Is.True);
            Assert.That(range.Contains(13), Is.False);
        }

        [TestCase("1", 1)]
        [TestCase("5", 5)]
        [TestCase("12", 12)]
        [TestCase("007", 7)]
        public void TryParse_AcceptsDigitsWithinRange(string text, int expected)
        {
            QuantitySelectorRange.TryCreate(13, out QuantitySelectorRange range);

            Assert.That(range.TryParse(text, out int amount), Is.True);
            Assert.That(amount, Is.EqualTo(expected));
        }

        [TestCase("0")]
        [TestCase("13")]
        [TestCase("99999999999")]
        [TestCase("-1")]
        [TestCase("+3")]
        [TestCase("5a")]
        [TestCase("a")]
        [TestCase("1.5")]
        [TestCase(" 5")]
        [TestCase("")]
        [TestCase(null)]
        public void TryParse_RejectsOutOfRangeAndNonDigitText(string text)
        {
            QuantitySelectorRange.TryCreate(13, out QuantitySelectorRange range);

            Assert.That(range.TryParse(text, out int amount), Is.False);
            Assert.That(amount, Is.EqualTo(0));
        }

        [TestCase('0', true)]
        [TestCase('9', true)]
        [TestCase('a', false)]
        [TestCase('-', false)]
        [TestCase('.', false)]
        [TestCase(' ', false)]
        public void IsAcceptedCharacter_OnlyAsciiDigits(char character, bool expected)
        {
            Assert.That(QuantitySelectorRange.IsAcceptedCharacter(character), Is.EqualTo(expected));
        }

        [TestCase(-3, 1)]
        [TestCase(0, 1)]
        [TestCase(7, 7)]
        [TestCase(50, 12)]
        public void Clamp_KeepsValuesInsideRange(int value, int expected)
        {
            QuantitySelectorRange.TryCreate(13, out QuantitySelectorRange range);

            Assert.That(range.Clamp(value), Is.EqualTo(expected));
        }
    }
}
#endif
