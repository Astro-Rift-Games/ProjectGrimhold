#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using Fusion;
using NUnit.Framework;
using UnityEngine;
using Assert = NUnit.Framework.Assert;

namespace Tests.EditMode.Combat
{
    public sealed class MeleeAttackReleaseTests
    {
        private const float DeltaTime = 0.02f;
        private const int WeaponIndexPlusOne = 4;

        private static AttackExecutionParameters ParametersWithRelease(float releaseSeconds) =>
            new(20f, DamageType.Physical, 0.5f, 2f, 1f, releaseSeconds);

        private static AttackRequest Request => new(new EntityId(7), Vector2.zero, new Vector2(3f, 4f), 100);

        [Test]
        public void Accept_SetsDeadlineFromCeilOfReleaseOverDeltaTime()
        {
            MeleeAttackRelease release = default;

            bool accepted = release.TryAccept(Request, ParametersWithRelease(0.45f), WeaponIndexPlusOne, DeltaTime);

            Assert.That(accepted, Is.True);
            Assert.That((bool)release.Pending, Is.True);
            Assert.That(release.ReleaseTick, Is.EqualTo(123));
            Assert.That(release.WeaponCatalogIndexPlusOne, Is.EqualTo(WeaponIndexPlusOne));
            Assert.That(release.Direction.x, Is.EqualTo(0.6f).Within(0.00001f));
            Assert.That(release.Direction.y, Is.EqualTo(0.8f).Within(0.00001f));
        }

        [Test]
        public void Accept_RoundsPartialTicksUp()
        {
            MeleeAttackRelease release = default;

            Assert.That(release.TryAccept(Request, ParametersWithRelease(0.41f), WeaponIndexPlusOne, DeltaTime), Is.True);

            Assert.That(release.ReleaseTick, Is.EqualTo(121));
        }

        [Test]
        public void ZeroDelay_ReleasesOnTheAcceptanceTick()
        {
            MeleeAttackRelease release = default;
            Assert.That(release.TryAccept(Request, ParametersWithRelease(0f), WeaponIndexPlusOne, DeltaTime), Is.True);

            Assert.That(release.ReleaseTick, Is.EqualTo(100));
            Assert.That(release.TryConsume(100, WeaponIndexPlusOne, out Vector2 direction), Is.True);
            Assert.That(direction.x, Is.EqualTo(0.6f).Within(0.00001f));
        }

        [Test]
        public void Consume_IsRejectedBeforeDeadlineAndSucceedsExactlyOnceAtOrAfterIt()
        {
            MeleeAttackRelease release = default;
            Assert.That(release.TryAccept(Request, ParametersWithRelease(0.45f), WeaponIndexPlusOne, DeltaTime), Is.True);

            Assert.That(release.TryConsume(122, WeaponIndexPlusOne, out _), Is.False);
            Assert.That((bool)release.Pending, Is.True);

            Assert.That(release.TryConsume(124, WeaponIndexPlusOne, out Vector2 direction), Is.True);
            Assert.That(direction.y, Is.EqualTo(0.8f).Within(0.00001f));
            Assert.That((bool)release.Pending, Is.False);
            Assert.That(release.TryConsume(124, WeaponIndexPlusOne, out _), Is.False);
            Assert.That(release.TryConsume(125, WeaponIndexPlusOne, out _), Is.False);
        }

        [Test]
        public void Consume_WithChangedWeapon_IsRejectedAndClearsPending()
        {
            MeleeAttackRelease release = default;
            Assert.That(release.TryAccept(Request, ParametersWithRelease(0.45f), WeaponIndexPlusOne, DeltaTime), Is.True);

            Assert.That(release.TryConsume(123, WeaponIndexPlusOne + 1, out _), Is.False);

            Assert.That((bool)release.Pending, Is.False);
            Assert.That(release.TryConsume(123, WeaponIndexPlusOne, out _), Is.False);
        }

        [Test]
        public void CancelIfWeaponChanged_ClearsPendingOnlyWhenTheWeaponDiffers()
        {
            MeleeAttackRelease release = default;
            Assert.That(release.TryAccept(Request, ParametersWithRelease(0.45f), WeaponIndexPlusOne, DeltaTime), Is.True);

            Assert.That(release.CancelIfWeaponChanged(WeaponIndexPlusOne), Is.False);
            Assert.That((bool)release.Pending, Is.True);

            Assert.That(release.CancelIfWeaponChanged(WeaponIndexPlusOne + 1), Is.True);
            Assert.That((bool)release.Pending, Is.False);
        }

        [Test]
        public void Cancel_DiscardsReleaseAndAllowsLaterAcceptance()
        {
            MeleeAttackRelease release = default;
            Assert.That(release.TryAccept(Request, ParametersWithRelease(0.45f), WeaponIndexPlusOne, DeltaTime), Is.True);

            release.Cancel();

            Assert.That((bool)release.Pending, Is.False);
            Assert.That(release.TryConsume(123, WeaponIndexPlusOne, out _), Is.False);
            Assert.That(release.TryAccept(Request, ParametersWithRelease(0.45f), WeaponIndexPlusOne, DeltaTime), Is.True);
        }

        [Test]
        public void PendingSwing_PreventsOverwrite()
        {
            MeleeAttackRelease release = default;
            Assert.That(release.TryAccept(Request, ParametersWithRelease(0.45f), WeaponIndexPlusOne, DeltaTime), Is.True);
            var replacement = new AttackRequest(new EntityId(7), Vector2.one, Vector2.left, 120);

            Assert.That(release.TryAccept(replacement, ParametersWithRelease(0.45f), WeaponIndexPlusOne, DeltaTime), Is.False);

            Assert.That(release.ReleaseTick, Is.EqualTo(123));
            Assert.That(release.Direction, Is.EqualTo(Request.Direction.normalized));
        }

        [Test]
        public void SnapshotCopy_PreservesDeadlineAndConsumesIndependently()
        {
            MeleeAttackRelease original = default;
            Assert.That(original.TryAccept(Request, ParametersWithRelease(0.45f), WeaponIndexPlusOne, DeltaTime), Is.True);

            MeleeAttackRelease restored = original;

            Assert.That(restored.TryConsume(122, WeaponIndexPlusOne, out _), Is.False);
            Assert.That(restored.TryConsume(123, WeaponIndexPlusOne, out _), Is.True);
            Assert.That((bool)original.Pending, Is.True);
        }

        [TestCase(0f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidDirection_IsNotAccepted(float x)
        {
            MeleeAttackRelease release = default;
            var invalid = new AttackRequest(new EntityId(7), Vector2.zero, new Vector2(x, 0f), 100);

            Assert.That(release.TryAccept(invalid, ParametersWithRelease(0.45f), WeaponIndexPlusOne, DeltaTime), Is.False);
            Assert.That((bool)release.Pending, Is.False);
        }

        [Test]
        public void InvalidParametersOrDeltaTime_AreNotAccepted()
        {
            MeleeAttackRelease release = default;
            var invalidParameters = new AttackExecutionParameters(0f, DamageType.Physical, 0.5f, 2f, 1f, 0.3f);

            Assert.That(release.TryAccept(Request, invalidParameters, WeaponIndexPlusOne, DeltaTime), Is.False);
            Assert.That(release.TryAccept(Request, ParametersWithRelease(0.45f), WeaponIndexPlusOne, 0f), Is.False);
            Assert.That((bool)release.Pending, Is.False);
        }
    }
}
#endif
