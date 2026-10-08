#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using Fusion;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Assert = NUnit.Framework.Assert;

namespace Tests.EditMode.Combat
{
    public sealed class RangedAttackReleaseTests
    {
        private RangedAttackConfig Config => AssetDatabase.LoadAssetAtPath<RangedAttackConfig>(
            "Assets/Scriptable Objects/RangePlayerAttackConfig.asset");
        private static AttackExecutionParameters Parameters => new(28f, DamageType.Physical, 0.1f, 6f, 3f, 0.45f);
        private static AttackRequest Request => new(new EntityId(7), Vector2.zero, new Vector2(3f, 4f), 100);

        [Test]
        public void Deadline_ConsumesOnceWithCommittedPayloadAndCurrentOrigin()
        {
            RangedAttackRelease release = default;
            Assert.That(release.TryAccept(Request, Config, Parameters, 0.02f), Is.True);
            Assert.That(release.ReleaseTick, Is.EqualTo(123));
            Assert.That(release.TryConsume(122, new EntityId(7), Vector2.one, out _), Is.False);
            Assert.That(release.Pending, Is.EqualTo((NetworkBool)true));
            Vector2 currentOrigin = new(8f, 9f);
            Assert.That(release.TryConsume(123, new EntityId(7), currentOrigin, out var shot), Is.True);
            Assert.That(shot.Origin, Is.EqualTo(currentOrigin + new Vector2(0.6f, 0.8f) * Config.ProjectileSpawnOffset));
            Assert.That(shot.Direction.x, Is.EqualTo(0.6f).Within(0.00001f));
            Assert.That(shot.Direction.y, Is.EqualTo(0.8f).Within(0.00001f));
            Assert.That(shot.Damage, Is.EqualTo(28f));
            Assert.That(shot.DamageType, Is.EqualTo(DamageType.Physical));
            Assert.That(shot.MaximumRange, Is.EqualTo(6f));
            Assert.That(shot.KnockbackForce, Is.EqualTo(3f));
            Assert.That(shot.Speed, Is.EqualTo(Config.ProjectileSpeed));
            Assert.That(shot.LifetimeSeconds, Is.EqualTo(Config.LifetimeSeconds));
            Assert.That(shot.ProjectilePrefab, Is.EqualTo(Config.ProjectilePrefab));
            Assert.That(shot.ImpactLayerMask, Is.EqualTo(Config.ImpactLayerMask.value));
            Assert.That(shot.SimulationTick, Is.EqualTo(123));
            Assert.That(release.TryConsume(123, new EntityId(7), currentOrigin, out _), Is.False);
            Assert.That(release.TryConsume(124, new EntityId(7), currentOrigin, out _), Is.False);
        }

        [Test]
        public void PendingShot_PreventsOverwriteEvenWhenCooldownWouldHaveExpired()
        {
            RangedAttackRelease release = default;
            Assert.That(release.TryAccept(Request, Config, Parameters, 0.02f), Is.True);
            var replacement = new AttackRequest(new EntityId(7), Vector2.one, Vector2.left, 120);
            Assert.That(release.TryAccept(replacement, Config, Parameters, 0.02f), Is.False);
            Assert.That(release.ReleaseTick, Is.EqualTo(123));
            Assert.That(release.Direction, Is.EqualTo(Request.Direction.normalized));
        }

        [Test]
        public void SnapshotCopy_PreservesDeadlinePayloadAndUsesRestoredAvatarIdentity()
        {
            RangedAttackRelease original = default;
            Assert.That(original.TryAccept(Request, Config, Parameters, 0.02f), Is.True);
            RangedAttackRelease restored = original;
            Assert.That(restored.TryConsume(122, new EntityId(70), Vector2.one, out _), Is.False);
            Assert.That(restored.TryConsume(123, new EntityId(70), Vector2.one, out var shot), Is.True);
            Assert.That(shot.OwnerId, Is.EqualTo(new EntityId(70)));
            Assert.That(shot.Damage, Is.EqualTo(original.Damage));
            RangedAttackRelease consumedSnapshot = restored;
            Assert.That(consumedSnapshot.TryConsume(124, new EntityId(70), Vector2.one, out _), Is.False);
        }

        [Test]
        public void Cancellation_DiscardsReleaseButAllowsLaterAcceptance()
        {
            RangedAttackRelease release = default;
            Assert.That(release.TryAccept(Request, Config, Parameters, 0.02f), Is.True);
            release.Cancel();
            Assert.That(release.TryConsume(123, new EntityId(7), Vector2.zero, out _), Is.False);
            Assert.That(release.TryAccept(Request, Config, Parameters, 0.02f), Is.True);
        }

        [Test]
        public void InvalidReleaseOrigin_ConsumesAttemptRatherThanLeavingPerpetualPending()
        {
            RangedAttackRelease release = default;
            Assert.That(release.TryAccept(Request, Config, Parameters, 0.02f), Is.True);
            Assert.That(release.TryConsume(123, new EntityId(7), new Vector2(float.NaN, 0f), out _), Is.False);
            Assert.That((bool)release.Pending, Is.False);
            Assert.That(release.TryConsume(124, new EntityId(7), Vector2.zero, out _), Is.False);
        }

        [TestCase(0f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidDirection_IsNotAccepted(float x)
        {
            RangedAttackRelease release = default;
            var invalid = new AttackRequest(new EntityId(7), Vector2.zero, new Vector2(x, 0f), 100);
            Assert.That(release.TryAccept(invalid, Config, Parameters, 0.02f), Is.False);
            Assert.That((bool)release.Pending, Is.False);
        }

        [TestCase("LongBow", 0.45f, 0f)]
        [TestCase("CompoundBow", 0.4f, 0f)]
        [TestCase("LightCrossbow", 0.3f, 0.025f)]
        [TestCase("MagicWand", 0.35f, 0.025f)]
        [TestCase("MagicStaff", 0.9f, 0.025f)]
        public void WeaponCalibration_ReleaseMatchesAuthoredKeyAndArtLead(string name, float releaseSeconds, float lead)
        {
            WeaponDefinition weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(
                $"Assets/Scriptable Objects/Loot/Definitions/{name}WeaponDefinition.asset");
            Assert.That(weapon.TryValidate(out string error), Is.True, error);
            Assert.That(weapon.RangedReleaseSeconds, Is.EqualTo(releaseSeconds));
            Assert.That(weapon.Presentation.AttackVfx.ReleaseLeadSeconds, Is.EqualTo(lead));
            Assert.That(weapon.Presentation.AttackVfx.StartSeconds,
                Is.EqualTo(releaseSeconds - lead).Within(0.00001f));
            for (int direction = 0; direction < 6; direction++)
            {
                AnimationClip clip = weapon.Presentation.GetAttackClip(direction);
                var binding = EditorCurveBinding.FloatCurve("RightHandPivot/RightHand", typeof(Transform), "m_LocalPosition.x");
                AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                Assert.That(curve, Is.Not.Null, clip.name);
                Assert.That(System.Array.Exists(curve.keys, key => Mathf.Abs(key.time - releaseSeconds) < 0.00001f),
                    Is.True, $"{clip.name} must retain the calibrated release key.");
            }
        }
    }
}
#endif
