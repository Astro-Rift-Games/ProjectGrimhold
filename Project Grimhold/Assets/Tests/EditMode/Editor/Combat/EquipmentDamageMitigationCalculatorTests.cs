#if UNITY_INCLUDE_TESTS
using NUnit.Framework;

namespace Tests.EditMode.Combat
{
    public sealed class EquipmentDamageMitigationCalculatorTests
    {
        [Test]
        public void Calculate_ZeroDefense_PreservesIncomingDamageExactly()
        {
            Assert.That(EquipmentDamageMitigationCalculator.TryCalculate(
                10.75f, 0, 100f, out float result), Is.True);
            Assert.That(result, Is.EqualTo(10.75f));
        }

        [TestCase(100f, 100, 100f, 50f)]
        [TestCase(99f, 50, 100f, 66f)]
        [TestCase(1f, 1000, 100f, 0f)]
        public void Calculate_PositiveDefense_AppliesConfiguredCurveAndFloor(
            float incomingDamage,
            int defense,
            float mitigationConstant,
            float expected)
        {
            Assert.That(EquipmentDamageMitigationCalculator.TryCalculate(
                incomingDamage, defense, mitigationConstant, out float result), Is.True);
            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void Calculate_ConfigurableConstant_ChangesMitigation()
        {
            Assert.That(EquipmentDamageMitigationCalculator.TryCalculate(
                100f, 100, 300f, out float result), Is.True);
            Assert.That(result, Is.EqualTo(75f));
        }

        [TestCase(float.NaN, 0, 100f)]
        [TestCase(float.PositiveInfinity, 0, 100f)]
        [TestCase(-1f, 0, 100f)]
        [TestCase(1f, -1, 100f)]
        [TestCase(1f, 0, 0f)]
        public void Calculate_InvalidInput_IsRejected(float damage, int defense, float mitigationConstant)
        {
            Assert.That(EquipmentDamageMitigationCalculator.TryCalculate(
                damage, defense, mitigationConstant, out float result), Is.False);
            Assert.That(result, Is.Zero);
        }
    }
}
#endif
