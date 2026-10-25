#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Combat
{
    public sealed class DualWieldPresentationTests
    {
        [Test]
        public void SampledOffHandReusesClipWithoutChangingLiveMainOrSourceAsset()
        {
            var loot = AssetDatabase.LoadAssetAtPath<LootDefinition>("Assets/Scriptable Objects/Loot/Definitions/ArmingSword.asset");
            AnimationClip clip = loot.WeaponDefinition.Presentation.GetOffHandAttackClip(3);
            string path = AssetDatabase.GetAssetPath(clip);
            Hash128 before = AssetDatabase.GetAssetDependencyHash(path);
            var main = new GameObject("LiveMain");
            var off = new GameObject("OffHand");
            var grip = new GameObject("OffHandGrip");
            grip.transform.SetParent(off.transform, false);
            main.transform.localPosition = new Vector3(7, 8, 0);
            var renderer = off.AddComponent<SpriteRenderer>();
            try
            {
                using (var sampler = new OneHandPoseSampler())
                {
                    sampler.Sample(clip, loot.WeaponDefinition.AttackReleaseSeconds, off.transform, grip.transform, renderer);
                    Assert.That(renderer.sprite, Is.Not.Null);
                }
                Assert.That(renderer.sprite, Is.Null, "Disposal restores the base pose.");
                Assert.That(main.transform.localPosition, Is.EqualTo(new Vector3(7, 8, 0)));
                Assert.That(AssetDatabase.GetAssetDependencyHash(path), Is.EqualTo(before));
            }
            finally
            {
                Object.DestroyImmediate(main);
                Object.DestroyImmediate(off);
            }
        }

        [TestCase("ArmingSword", 90f)]
        [TestCase("ArmingSword", 45f)]
        [TestCase("ArmingSword", 135f)]
        [TestCase("ArmingSword", -90f)]
        [TestCase("ArmingSword", -45f)]
        [TestCase("ArmingSword", -135f)]
        [TestCase("MagicCinquedea", 90f)]
        [TestCase("MagicCinquedea", 45f)]
        [TestCase("MagicCinquedea", 135f)]
        [TestCase("MagicCinquedea", -90f)]
        [TestCase("MagicCinquedea", -45f)]
        [TestCase("MagicCinquedea", -135f)]
        [TestCase("MagicSword", 90f)]
        [TestCase("MagicSword", 45f)]
        [TestCase("MagicSword", 135f)]
        [TestCase("MagicSword", -90f)]
        [TestCase("MagicSword", -45f)]
        [TestCase("MagicSword", -135f)]
        [TestCase("MagicWand", 90f)]
        [TestCase("MagicWand", 45f)]
        [TestCase("MagicWand", 135f)]
        [TestCase("MagicWand", -90f)]
        [TestCase("MagicWand", -45f)]
        [TestCase("MagicWand", -135f)]
        [TestCase("Rapier", 90f)]
        [TestCase("Rapier", 45f)]
        [TestCase("Rapier", 135f)]
        [TestCase("Rapier", -90f)]
        [TestCase("Rapier", -45f)]
        [TestCase("Rapier", -135f)]
        [TestCase("RondelDagger", 90f)]
        [TestCase("RondelDagger", 45f)]
        [TestCase("RondelDagger", 135f)]
        [TestCase("RondelDagger", -90f)]
        [TestCase("RondelDagger", -45f)]
        [TestCase("RondelDagger", -135f)]
        [TestCase("MagicWand", 135f, 17f)]
        [TestCase("MagicWand", -135f, -17f)]
        [TestCase("MagicWand", 45f, 17f)]
        [TestCase("MagicWand", -45f, -17f)]
        public void OffHandOrientationMatchesMainHandRigAndKeepsGripAnchored(string asset, float degrees, float residual = 0f)
        {
            var loot = AssetDatabase.LoadAssetAtPath<LootDefinition>(
                $"Assets/Scriptable Objects/Loot/Definitions/{asset}.asset");
            Assert.That(loot, Is.Not.Null);
            var root = new GameObject("OffHandOrientationTest");
            root.SetActive(false);
            var pivot = new GameObject("ReferenceFacingPivot").transform;
            pivot.SetParent(root.transform, false);
            var reference = new GameObject("ReferenceWeapon").transform;
            reference.SetParent(pivot, false);
            var visual = new GameObject("OffHandVisual").transform;
            visual.SetParent(root.transform, false);
            try
            {
                var view = root.AddComponent<PlayerAnimatorView>();
                var presenter = root.AddComponent<PlayerWeaponPresenter>();
                SetField(presenter, "_animatorView", view);
                SetField(presenter, "_offHandDefinition", loot);
                SetField(presenter, "_offHandVisual", visual);
                SetField(presenter, "_offHandBaseScale", Vector3.one);
                Vector2 facing = Quaternion.Euler(0f, 0f, degrees) * Vector2.right;
                if (residual != 0f)
                {
                    SetField(view, "_hasPresentedAim", true);
                    SetField(view, "_presentedAim", (Vector2)(Quaternion.Euler(0f, 0f, degrees + residual) * Vector2.right));
                }
                bool mirrored = facing.x < -0.001f;
                // Public rig contract: a mirrored facing pivot contains the corrected weapon visual.
                pivot.localRotation = Quaternion.Euler(0f, 0f, degrees + residual);
                pivot.localScale = new Vector3(1f, mirrored ? -1f : 1f, 1f);
                float correction = loot.WeaponDefinition.Presentation.AngleCorrection;
                reference.localRotation = Quaternion.Euler(0f, 0f, mirrored ? -180f - correction : correction);
                Vector2 grip = loot.WeaponDefinition.Presentation.GripPoint;
                reference.localPosition = -(reference.localRotation * (Vector3)grip);
                typeof(PlayerWeaponPresenter).GetMethod("PoseOffHandWeapon", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(presenter, new object[] { facing, mirrored });
                Assert.That(Vector3.Distance(visual.TransformVector(Vector3.up), reference.TransformVector(Vector3.up)),
                    Is.LessThan(0.0001f), "The blade axis differs from the existing Main Hand rig.");
                Assert.That(Vector3.Distance(visual.TransformVector(Vector3.right), reference.TransformVector(Vector3.right)),
                    Is.LessThan(0.0001f), "The reflected art axis differs from Main Hand.");
                Assert.That(visual.TransformPoint(grip).sqrMagnitude, Is.LessThan(0.0001f), "Grip must stay anchored.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void SetField(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        [TestCase(-0.01f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void WeaponRejectsInvalidExecutionWindow(float value)
        {
            var loot = AssetDatabase.LoadAssetAtPath<LootDefinition>("Assets/Scriptable Objects/Loot/Definitions/ArmingSword.asset");
            WeaponDefinition weapon = Object.Instantiate(loot.WeaponDefinition);
            try
            {
                typeof(WeaponDefinition).GetField("_executionWindowSeconds", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(weapon, value);
                Assert.That(weapon.TryValidate(out string error), Is.False);
                Assert.That(error, Does.Contain("execution window"));
            }
            finally { Object.DestroyImmediate(weapon); }
        }
    }
}
#endif
