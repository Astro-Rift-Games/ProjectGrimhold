#if UNITY_EDITOR || DEVELOPMENT_BUILD
using NUnit.Framework;

namespace Tests.EditMode.Abilities
{
    public sealed class DummyDamageLogTests
    {
        [Test]
        public void NewLog_IsEmpty()
        {
            var log = new DummyDamageLog();
            Assert.That(log.LastDamage, Is.EqualTo(0f));
            Assert.That(log.TotalDamage, Is.EqualTo(0f));
            Assert.That(log.HitCount, Is.EqualTo(0));
        }

        [Test]
        public void Record_TracksLastTotalAndCount()
        {
            var log = new DummyDamageLog();
            log.Record(10f);
            log.Record(5.5f);

            Assert.That(log.LastDamage, Is.EqualTo(5.5f));
            Assert.That(log.TotalDamage, Is.EqualTo(15.5f).Within(0.0001f));
            Assert.That(log.HitCount, Is.EqualTo(2));
        }

        [TestCase(0f)]
        [TestCase(-3f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void Record_IgnoresNonPositiveOrNonFiniteAmounts(float amount)
        {
            var log = new DummyDamageLog();
            log.Record(4f);
            log.Record(amount);

            Assert.That(log.LastDamage, Is.EqualTo(4f));
            Assert.That(log.TotalDamage, Is.EqualTo(4f));
            Assert.That(log.HitCount, Is.EqualTo(1));
        }

        [Test]
        public void Reset_ClearsEverything()
        {
            var log = new DummyDamageLog();
            log.Record(7f);
            log.Reset();

            Assert.That(log.LastDamage, Is.EqualTo(0f));
            Assert.That(log.TotalDamage, Is.EqualTo(0f));
            Assert.That(log.HitCount, Is.EqualTo(0));
        }
    }
}
#endif
