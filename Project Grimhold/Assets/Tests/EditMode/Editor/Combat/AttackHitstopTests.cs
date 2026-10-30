#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEngine;
using Assert = NUnit.Framework.Assert;

namespace Tests.EditMode.Combat
{
    public sealed class AttackHitstopTests
    {
        [Test]
        public void OnTime_FreezesAndRecoversContinuously()
        {
            AttackHitstop clock = default;
            Assert.That(clock.TrySchedule(1, .3f, .2f, .1f), Is.True);
            Assert.That(clock.Map(1, .29f), Is.EqualTo(.29f));
            Assert.That(clock.Map(1, .35f), Is.EqualTo(.3f));
            Assert.That(clock.Map(1, .4f), Is.EqualTo(.3f).Within(.00001f));
            Assert.That(clock.Map(1, .5f), Is.EqualTo(.4f).Within(.00001f));
            clock.GetRemaining(.2f, out float delay, out float hold);
            Assert.That(delay, Is.EqualTo(.1f).Within(.00001f));
            Assert.That(hold, Is.EqualTo(.1f).Within(.00001f));
        }

        [Test]
        public void LateConfirmation_ShortensWithoutRewinding()
        {
            AttackHitstop clock = default;
            Assert.That(clock.TrySchedule(7, .3f, .34f, .1f), Is.True);
            Assert.That(clock.Map(7, .34f), Is.EqualTo(.34f));
            Assert.That(clock.Map(7, .39f), Is.EqualTo(.34f));
            Assert.That(clock.Map(7, .45f), Is.EqualTo(.39f).Within(.00001f));
            clock.GetRemaining(.34f, out float delay, out float hold);
            Assert.That(delay, Is.Zero);
            Assert.That(hold, Is.EqualTo(.06f).Within(.00001f));
        }

        [TestCase(.395f)]
        [TestCase(.4f)]
        [TestCase(.8f)]
        public void TooLate_IsDropped(float raw)
        {
            AttackHitstop clock = default;
            Assert.That(clock.TrySchedule(1, .3f, raw, .1f), Is.False);
            Assert.That(clock.Map(1, raw), Is.EqualTo(raw));
        }

        [Test]
        public void SameSequence_NeverStacksOrRestarts_EvenAfterExpiry()
        {
            AttackHitstop clock = default;
            Assert.That(clock.TrySchedule(1, .3f, .3f, .1f), Is.True);
            Assert.That(clock.TrySchedule(1, .3f, .32f, .2f), Is.False);
            Assert.That(clock.TrySchedule(1, 1f, 1f, .2f), Is.False);
            Assert.That(clock.Map(1, .5f), Is.EqualTo(.4f).Within(.00001f));
            Assert.That(clock.Map(2, .35f), Is.EqualTo(.35f));
            Assert.That(clock.TrySchedule(2, .2f, .2f, .04f), Is.True);
            Assert.That(clock.Map(1, .5f), Is.EqualTo(.5f));
        }

        [TestCase(.1f)]
        [TestCase(.33f)]
        public void DenseSamples_AreContinuousMonotoneAndNeverRewindAtReceipt(float receipt)
        {
            AttackHitstop clock = default;
            Assert.That(clock.TrySchedule(1, .3f, receipt, .1f), Is.True);
            float previous = receipt;
            for (int i = 0; i <= 1000; i++)
            {
                float raw = receipt + i * .001f;
                float mapped = clock.Map(1, raw);
                Assert.That(mapped, Is.GreaterThanOrEqualTo(previous));
                Assert.That(mapped, Is.LessThanOrEqualTo(raw));
                Assert.That(mapped - previous, Is.LessThanOrEqualTo(.00101f));
                previous = mapped;
            }
        }

        [TestCase(0, .1f)]
        [TestCase(-1, .1f)]
        [TestCase(1, 0f)]
        [TestCase(1, -.1f)]
        [TestCase(1, float.NaN)]
        [TestCase(1, float.PositiveInfinity)]
        public void InvalidInput_DoesNotChangeExistingSchedule(int sequence, float duration)
        {
            AttackHitstop clock = default;
            Assert.That(clock.TrySchedule(3, .3f, .3f, .1f), Is.True);
            Assert.That(clock.TrySchedule(sequence, .3f, .3f, duration), Is.False);
            Assert.That(clock.Map(3, .35f), Is.EqualTo(.3f));
        }

        [Test]
        public void Cancellation_RestoresRawClockWithoutAllowingSameAttackToRestart()
        {
            AttackHitstop clock = default;
            Assert.That(clock.TrySchedule(1, .3f, .3f, .1f), Is.True);
            clock.Cancel();
            Assert.That(clock.Map(1, .35f), Is.EqualTo(.35f));
            Assert.That(clock.TrySchedule(1, .3f, .35f, .1f), Is.False);
            Assert.That(clock.TrySchedule(2, .3f, .35f, .1f), Is.True);
        }

        [Test]
        public void IndependentHands_DoNotPauseEachOther()
        {
            AttackHitstop main = default, off = default;
            Assert.That(main.TrySchedule(1, .3f, .3f, .1f), Is.True);
            Assert.That(off.Map(2, .35f), Is.EqualTo(.35f));
            Assert.That(off.TrySchedule(2, .35f, .35f, .04f), Is.True);
            Assert.That(main.Map(1, .38f), Is.EqualTo(.3f));
            Assert.That(off.Map(2, .4f), Is.EqualTo(.36f).Within(.00001f));
        }

        [Test]
        public void Feedback_DefaultCallersRemainUntagged_OnlyConfirmedMeleeIsTagged()
        {
            var ordinary = new CombatPresentationEvent(1, CombatFeedbackKind.ConfirmedImpact,
                new EntityId(2), Vector2.one, 10f, 5, AttackFailureReason.None);
            Assert.That(default(CombatPresentationEvent).IsMeleeImpact, Is.False);
            Assert.That(ordinary.IsMeleeImpact, Is.False);
            Assert.That(ordinary.AppliedDamage, Is.EqualTo(10f));
            Assert.That(ordinary.TargetId, Is.EqualTo(new EntityId(2)));
            var melee = new CombatPresentationEvent(2, CombatFeedbackKind.ConfirmedImpact,
                new EntityId(2), Vector2.one, 10f, 5, AttackFailureReason.None,
                EquipmentSlot.WeaponSetAOffHand, 7, 9);
            Assert.That(melee.IsMeleeImpact, Is.True);
            Assert.That(melee.MeleeSourceSlot, Is.EqualTo(EquipmentSlot.WeaponSetAOffHand));
            Assert.That(melee.MeleeAttackSequence, Is.EqualTo(7));
            Assert.That(melee.MeleeWeaponCatalogIndexPlusOne, Is.EqualTo(9));
            var rejected = new CombatPresentationEvent(3, CombatFeedbackKind.AttackRejected,
                default, default, 0f, 5, AttackFailureReason.CooldownActive,
                EquipmentSlot.None, 7, 9);
            Assert.That(rejected.IsMeleeImpact, Is.False);
        }
    }
}
#endif
