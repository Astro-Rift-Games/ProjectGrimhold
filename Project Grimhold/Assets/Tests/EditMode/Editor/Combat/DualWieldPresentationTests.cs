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
            AnimationClip clip = loot.WeaponDefinition.Presentation.GetAttackClip(3);
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
                    sampler.Sample(clip, loot.WeaponDefinition.AttackReleaseSeconds, off.transform, grip.transform, renderer);
                Assert.That(renderer.sprite, Is.Not.Null);
                Assert.That(main.transform.localPosition, Is.EqualTo(new Vector3(7, 8, 0)));
                Assert.That(AssetDatabase.GetAssetDependencyHash(path), Is.EqualTo(before));
            }
            finally
            {
                Object.DestroyImmediate(main);
                Object.DestroyImmediate(off);
            }
        }

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
