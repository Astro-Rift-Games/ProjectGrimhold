#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Combat
{
    public sealed class WeaponAttackTimingTests
    {
        [Test]
        public void HandsHaveIndependentIntervals_AndWindowEndsBeforeRelease()
        {
            WeaponAttackTiming main = default, off = default;
            Assert.That(main.TryStart(100, 0.02f, 1f, 0.2f, out int windowEnd), Is.True);
            Assert.That(windowEnd, Is.EqualTo(110));
            Assert.That(main.IsReady(110), Is.False);
            Assert.That(off.IsReady(110), Is.True);
            Assert.That(off.TryStart(110, 0.02f, 0.4f, 0.2f, out _), Is.True);
            Assert.That(main.IntervalEndTick, Is.EqualTo(150));
            Assert.That(off.IntervalEndTick, Is.EqualTo(130));
            var request = new AttackRequest(new EntityId(1), Vector2.zero, Vector2.up, 100);
            MeleeAttackRelease release = default;
            Assert.That(release.TryAccept(request, new AttackExecutionParameters(20, DamageType.Physical, 1, 2, 0, 0.5f), 3, 0.02f), Is.True);
            Assert.That(release.ReleaseTick, Is.GreaterThan(windowEnd));
            Assert.That((bool)release.Pending, Is.True, "Starting the other hand does not consume the first release.");
        }

        [Test]
        public void InactiveSetAndSnapshotKeepAbsoluteDeadline()
        {
            WeaponAttackTiming setA = default, setB = default;
            Assert.That(setA.TryStart(100, 0.02f, 1f, 0.2f, out _), Is.True);
            WeaponAttackTiming restored = setA;
            Assert.That(setB.TryStart(115, 0.02f, 0.2f, 0.1f, out _), Is.True);
            Assert.That(restored.IsReady(149), Is.False);
            Assert.That(restored.IsReady(150), Is.True);
            Assert.That(restored.IntervalSeconds, Is.EqualTo(1f));
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void RejectedDurationLeavesTimerUnchanged(float invalid)
        {
            WeaponAttackTiming timing = default;
            Assert.That(timing.TryStart(100, 0.02f, 1f, invalid, out _), Is.False);
            Assert.That(timing.IntervalEndTick, Is.Zero);
            Assert.That(timing.IntervalSeconds, Is.Zero);
        }

        [Test]
        public void RejectedEarlyRestartDoesNotResetCooldown()
        {
            WeaponAttackTiming timing = default;
            Assert.That(timing.TryStart(100, 0.02f, 1, 0.2f, out _), Is.True);
            Assert.That(timing.TryStart(110, 0.02f, 2, 0.2f, out _), Is.False);
            Assert.That(timing.IntervalEndTick, Is.EqualTo(150));
        }

        [Test]
        public void MeleeSnapshotRetainsAcceptedStatisticsAndHandIdentity()
        {
            var request = new AttackRequest(new EntityId(1), Vector2.zero, Vector2.up, 100);
            MeleeAttackRelease release = default;
            Assert.That(release.TryAccept(request, new AttackExecutionParameters(33, DamageType.Magical, 1, 4, 2, 0.5f), 3, 0.02f), Is.True);
            MeleeAttackRelease restored = release;
            Assert.That(restored.ExecutionParameters.Damage, Is.EqualTo(33));
            Assert.That(restored.ExecutionParameters.Range, Is.EqualTo(4));
            Assert.That(restored.ExecutionParameters.KnockbackForce, Is.EqualTo(2));
            var state = new WeaponHandAttackState { Melee = restored, Sequence = 7, StartTick = 100, ReleaseTick = 125 };
            var notification = state.ToEvent(new EntityId(1), EquipmentSlot.WeaponSetAOffHand, 0.02f);
            Assert.That(notification.IsOffHand, Is.True);
            Assert.That(notification.SourceSlot, Is.EqualTo(EquipmentSlot.WeaponSetAOffHand));
            Assert.That(restored.TryConsume(125, 3, out _), Is.True);
            Assert.That(restored.TryConsume(125, 3, out _), Is.False);
        }
    }
}
#endif
