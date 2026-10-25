#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.AttackAnimationValidator
{
    public sealed class CanonicalSlashGeneratorTests
    {
        [Test]
        public void CanonicalRegeneration_PreservesGuidsAndSecondRunBytes()
        {
            void Generate()
            {
                DirectionalAnimationGenerator.GenerateAssets("ArmingSword");
                DirectionalAnimationGenerator.GenerateAssets("MagicSword");
            }
            Generate();
            var paths = new[] { "ArmingSword", "MagicSword" }.SelectMany(name =>
                System.IO.Directory.GetFiles("Assets/Animations/Weapons/Directional/" + name, "*.anim"))
                .Concat(new[] { "Sword1H", "MagicSword", "Sword1H_OffHand", "MagicSword_OffHand" }
                    .Select(name => "Assets/Scriptable Objects/Loot/AttackAnimationSets/" + name + ".asset"))
                .Concat(new[] { "ArmingSword", "MagicSword" }.Select(name =>
                    "Assets/Scriptable Objects/Loot/Definitions/" + name + "SlashAttackVfx.asset")).ToArray();
            var bytes = paths.Select(System.IO.File.ReadAllBytes).ToArray();
            var guids = paths.Select(AssetDatabase.AssetPathToGUID).ToArray();
            Generate();
            for (int i = 0; i < paths.Length; i++)
            {
                Assert.That(AssetDatabase.AssetPathToGUID(paths[i]), Is.EqualTo(guids[i]), paths[i]);
                Assert.That(System.IO.File.ReadAllBytes(paths[i]), Is.EqualTo(bytes[i]), paths[i]);
            }
        }

        [TestCase("ArmingSword", .6f, .1f, .15f, .45f, .6f)]
        [TestCase("MagicSword", .85f, .25f, .3f, .55f, .8f)]
        public void TimingMetadata_PreservesSourcePhasesAndGameplayRelease(string name, float duration,
            float release, float start, float end, float recovery)
        {
            var cell = AttackPreviewCatalog.Enumerate().First(c => c.OffHand == null && c.MainHand.name == name);
            var weapon = cell.MainHand.WeaponDefinition;
            Assert.That(weapon.AttackReleaseSeconds, Is.EqualTo(release).Within(.00001f));
            foreach (var set in new[] { weapon.Presentation.AttackAnimationSet, weapon.Presentation.OffHandAttackAnimationSet })
            {
                Assert.That(set.SlashStrokeWindow, Is.EqualTo(new Vector2(start,end)));
                Assert.That(set.SlashRecoveryEndSeconds, Is.EqualTo(recovery).Within(.00001f));
                for (int d = 0; d < 6; d++) Assert.That(set.GetAttackClip(d).length, Is.EqualTo(duration).Within(.00001f));
            }
        }

        [Test]
        public void MagicSword_RecoveryReachesNeutralAtAuthoredPointEightNotClipEnd()
        {
            foreach (var c in AttackPreviewCatalog.Enumerate().Where(c =>
                (c.MainHand == null || c.OffHand == null) && (c.MainHand ?? c.OffHand).name == "MagicSword"))
            {
                using var rig = new AttackPreviewRig();
                var visual = rig.VisualRoot;
                string direction = new[] { "N", "NE", "NW", "S", "SE", "SW" }[c.FacingIndex];
                foreach (string path in new[] { $"Idle/Idle_{direction}",
                    $"Idle/RightHand/RightHand_Idle_{direction}", $"Idle/LeftHand/LeftHand_Idle_{direction}" })
                    AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Player/" + path + ".anim").SampleAnimation(visual.gameObject, 0);
                var grip = c.Action == AttackPreviewAction.MainAttack ? rig.MainHandGrip : rig.OffHandGrip;
                Vector3 neutral = visual.InverseTransformPoint(grip.position);
                rig.Configure(c);
                rig.Evaluate(.8f);
                Assert.That(Vector3.Distance(visual.InverseTransformPoint(grip.position), neutral), Is.LessThan(.0005f), c.DisplayName);
            }
        }

        [TestCase("ArmingSword", .15f, .45f)]
        [TestCase("MagicSword", .30f, .55f)]
        public void GeneratedGeometry_IsBodyRelativeRadialAndMirrored(string name, float start, float end)
        {
            var cells = AttackPreviewCatalog.Enumerate().Where(c => c.OffHand == null && c.MainHand.name == name);
            foreach (var c in cells)
            {
                using var main = new AttackPreviewRig();
                using var off = new AttackPreviewRig();
                main.Configure(c);
                off.Configure(new AttackPreviewCombination(null, c.MainHand, AttackPreviewAction.OffAttack, c.FacingIndex));
                Vector2 f = new[] { Vector2.up, new Vector2(1,1).normalized, new Vector2(-1,1).normalized,
                    Vector2.down, new Vector2(1,-1).normalized, new Vector2(-1,-1).normalized }[c.FacingIndex];
                Vector2 r = new Vector2(f.y, -f.x);
                for (int i = 0; i <= 12; i++)
                {
                    float t = Mathf.Lerp(start, end, i / 12f);
                    main.Evaluate(t); off.Evaluate(t);
                    Vector2 m = main.VisualRoot.InverseTransformPoint(main.MainHandGrip.position);
                    Vector2 o = off.VisualRoot.InverseTransformPoint(off.OffHandGrip.position);
                    Assert.That(Vector2.Distance(o, 2 * Vector2.Dot(m, f) * f - m), Is.LessThan(.003f), $"{name}/{c.FacingIndex}/{t}");
                    float angle = Mathf.Atan2(Vector2.Dot(m,r), Vector2.Dot(m,f)) * Mathf.Rad2Deg;
                    if (i == 0) Assert.That(angle, Is.EqualTo(135).Within(1));
                    if (i == 12) Assert.That(angle, Is.EqualTo(-45).Within(1));
                }
            }
        }
    }
}
#endif
