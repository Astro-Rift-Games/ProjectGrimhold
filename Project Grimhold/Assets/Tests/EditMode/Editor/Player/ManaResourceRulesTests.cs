using NUnit.Framework;

namespace Tests.EditMode.Player
{
    public sealed class ManaResourceRulesTests
    {
        [TestCase(40f, 130f, 40f)]
        [TestCase(100f, 60f, 60f)]
        [TestCase(-1f, 100f, 0f)]
        public void MaximumChanges_OnlyClampExcess(float current, float maximum, float expected)
        {
            Assert.That(ManaResourceRules.ClampCurrent(current, maximum), Is.EqualTo(expected));
        }

        [Test]
        public void MaximumDecreaseThenIncrease_DoesNotRestoreDiscardedExcess()
        {
            float clamped = ManaResourceRules.ClampCurrent(150f, 100f);
            Assert.That(ManaResourceRules.ClampCurrent(clamped, 150f), Is.EqualTo(100f));
        }

        [TestCase(10f, 10f, true, 0f)]
        [TestCase(10f, 4f, true, 6f)]
        [TestCase(10f, 11f, false, 10f)]
        [TestCase(10f, 0f, true, 10f)]
        public void Spend_IsCompleteOrPreservesBalance(float current, float amount, bool expected, float remaining)
        {
            Assert.That(ManaResourceRules.TrySpend(current, amount, out float resulting), Is.EqualTo(expected));
            Assert.That(resulting, Is.EqualTo(remaining));
            Assert.That(ManaResourceRules.CanSpend(current, amount), Is.EqualTo(expected));
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void InvalidCosts_AreRejectedWithoutMutation(float amount)
        {
            Assert.That(ManaResourceRules.TrySpend(10f, amount, out float resulting), Is.False);
            Assert.That(resulting, Is.EqualTo(10f));
            Assert.That(ManaResourceRules.CanSpend(10f, amount), Is.False);
        }

        [TestCase(40f, 100f, 10f, 50f)]
        [TestCase(90f, 100f, 20f, 100f)]
        [TestCase(40f, 100f, 0f, 40f)]
        public void Restore_IsInstantAndCapped(float current, float maximum, float amount, float expected)
        {
            Assert.That(ManaResourceRules.TryRestore(current, maximum, amount, out float resulting), Is.True);
            Assert.That(resulting, Is.EqualTo(expected));
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidRestoration_PreservesBalance(float amount)
        {
            Assert.That(ManaResourceRules.TryRestore(40f, 100f, amount, out float resulting), Is.False);
            Assert.That(resulting, Is.EqualTo(40f));
        }
    }
}
