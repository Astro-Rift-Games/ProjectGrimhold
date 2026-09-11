#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Combat
{
    public sealed class ShieldDefenseMathTests
    {
        [TestCase(0f, true)]
        [TestCase(59.9f, true)]
        [TestCase(60f, true)]
        [TestCase(60.1f, false)]
        [TestCase(90f, false)]
        [TestCase(180f, false)]
        public void Mitigate_UsesInclusiveSixtyDegreeHalfAngle(float angle, bool expectedBlocked)
        {
            Vector2 directionToOrigin = Quaternion.Euler(0f, 0f, angle) * Vector2.up;
            Vector2 attackDirection = -directionToOrigin;

            bool blocked = ShieldDefenseMath.TryMitigate(
                40f,
                0.5f,
                120f,
                Vector2.up,
                attackDirection,
                out float damage);

            Assert.That(blocked, Is.EqualTo(expectedBlocked));
            Assert.That(damage, Is.EqualTo(expectedBlocked ? 20f : 40f).Within(0.0001f));
        }

        [TestCase(0f, 0f)]
        [TestCase(float.NaN, 1f)]
        [TestCase(float.PositiveInfinity, 1f)]
        public void Mitigate_InvalidAttackDirection_FailsOpen(float x, float y)
        {
            Assert.That(ShieldDefenseMath.TryMitigate(
                40f,
                0.5f,
                120f,
                Vector2.up,
                new Vector2(x, y),
                out float damage), Is.False);
            Assert.That(damage, Is.EqualTo(40f));
        }

        [TestCase(0f, 0f)]
        [TestCase(float.NaN, 1f)]
        [TestCase(float.NegativeInfinity, 1f)]
        public void Mitigate_InvalidFacingDirection_FailsOpen(float x, float y)
        {
            Assert.That(ShieldDefenseMath.TryMitigate(
                40f,
                0.5f,
                120f,
                new Vector2(x, y),
                Vector2.down,
                out float damage), Is.False);
            Assert.That(damage, Is.EqualTo(40f));
        }

        [Test]
        public void Mitigate_AppliesExactConfiguredPercentage()
        {
            Assert.That(ShieldDefenseMath.TryMitigate(
                37.5f,
                0.5f,
                120f,
                Vector2.up,
                Vector2.down,
                out float damage), Is.True);
            Assert.That(damage, Is.EqualTo(18.75f));
        }

        [Test]
        public void Mitigate_ComposesAfterArmorMitigation()
        {
            Assert.That(EquipmentDamageMitigationCalculator.TryCalculate(
                100f,
                100,
                100f,
                out float armorMitigatedDamage), Is.True);
            Assert.That(ShieldDefenseMath.TryMitigate(
                armorMitigatedDamage,
                0.5f,
                120f,
                Vector2.up,
                Vector2.down,
                out float finalDamage), Is.True);

            Assert.That(armorMitigatedDamage, Is.EqualTo(50f));
            Assert.That(finalDamage, Is.EqualTo(25f));
        }
    }
}
#endif
