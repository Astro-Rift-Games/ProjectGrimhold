#if UNITY_INCLUDE_TESTS
using NUnit.Framework;

namespace Tests.EditMode.Combat
{
    public sealed class FriendlyFireRulesTests
    {
        [Test]
        public void AllyDamage_FriendlyFireDisabled_BlocksHit() =>
            Assert.That(FriendlyFireRules.IsBlocked(true, false), Is.True);

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        public void PermittedDamage_IsNotBlocked(bool ally, bool enabled) =>
            Assert.That(FriendlyFireRules.IsBlocked(ally, enabled), Is.False);

        [TestCase(10f, 0.5f, 5f)]
        [TestCase(10f, 1f, 10f)]
        [TestCase(10f, -1f, 0f)]
        public void AllyDamage_MultiplierIsNonnegative(float amount, float multiplier, float expected) =>
            Assert.That(FriendlyFireRules.ScaleDamage(amount, true, multiplier), Is.EqualTo(expected));

        [Test]
        public void NonAllyDamage_MultiplierDoesNotApply() =>
            Assert.That(FriendlyFireRules.ScaleDamage(10f, false, 0f), Is.EqualTo(10f));

        [TestCase(10f, 50f, 9f)]
        [TestCase(1f, 50f, 0f)]
        [TestCase(0f, 50f, 0f)]
        [TestCase(10f, 2f, 2f)]
        public void AllyDamage_AfterMitigation_ClampsToOneHp(float health, float damage, float expected) =>
            Assert.That(FriendlyFireRules.ClampDamage(health, damage, true), Is.EqualTo(expected));

        [Test]
        public void EnvironmentalOrCreatureDamage_HasNoHealthFloor() =>
            Assert.That(FriendlyFireRules.ClampDamage(1f, 50f, false), Is.EqualTo(50f));
    }
}
#endif
