#if UNITY_INCLUDE_TESTS
using NUnit.Framework;

namespace Tests.EditMode.Combat
{
    public sealed class WeaponDamageCalculatorTests
    {
        [TestCase(10f, 5, 0.7f, 13.5f)]
        [TestCase(22f, 0, 0.85f, 22f)]
        [TestCase(4.25f, 12, 0.55f, 10.85f)]
        public void Calculate_AppliesTheLinearFormula(
            float baseDamage,
            int attributeValue,
            float scalingCoefficient,
            float expected)
        {
            float result = WeaponDamageCalculator.Calculate(
                baseDamage,
                attributeValue,
                scalingCoefficient);

            Assert.That(result, Is.EqualTo(expected).Within(0.0001f));
        }

        [Test]
        public void Calculate_WithZeroCoefficient_ReturnsExactBaseDamage()
        {
            const float baseDamage = 17.375f;

            float result = WeaponDamageCalculator.Calculate(baseDamage, 999, 0f);

            Assert.That(result, Is.EqualTo(baseDamage));
        }

        [Test]
        public void Calculate_PreservesFractionalDamageWithoutRounding()
        {
            float result = WeaponDamageCalculator.Calculate(10.125f, 3, 0.25f);

            Assert.That(result, Is.EqualTo(10.875f));
        }
    }
}
#endif
