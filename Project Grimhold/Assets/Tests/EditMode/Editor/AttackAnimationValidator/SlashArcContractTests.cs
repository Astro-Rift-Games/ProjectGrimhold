#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.AttackAnimationValidator
{
    public sealed class SlashArcContractTests
    {
        [TestCase(false)] [TestCase(true)]
        public void SyntheticCanonicalArc_Passes(bool off)
        {
            var method = typeof(AttackPreviewChecks).GetMethod("CheckSlashArc");
            Assert.That(method, Is.Not.Null, "Add the temporal SlashArcContract check.");
            var points = Enumerable.Range(0, 25).Select(i =>
            {
                float a = Mathf.Lerp(off ? -135 : 135, off ? 45 : -45, i / 24f) * Mathf.Deg2Rad;
                return new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            }).ToArray();
            var check = (AttackPreviewCheck)method.Invoke(null, new object[] { points, points, points,
                off ? AttackPreviewHand.Off : AttackPreviewHand.Main, 2f / 32f, 0f });
            Assert.That(check.IsViolation, Is.False, check.Message);
        }

        [TestCase(false)] [TestCase(true)]
        public void ReversedArc_Fails(bool off)
        {
            var method = typeof(AttackPreviewChecks).GetMethod("CheckSlashArc");
            Assert.That(method, Is.Not.Null);
            var points = Enumerable.Range(0, 25).Select(i =>
            {
                float a = Mathf.Lerp(off ? 45 : -45, off ? -135 : 135, i / 24f) * Mathf.Deg2Rad;
                return new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            }).ToArray();
            var check = (AttackPreviewCheck)method.Invoke(null, new object[] { points, points, points,
                off ? AttackPreviewHand.Off : AttackPreviewHand.Main, 2f / 32f, 0f });
            Assert.That(check.IsViolation, Is.True);
        }

        [Test]
        public void VfxOrigin_IsIndependentOfAuthoredSlashPose()
        {
            var cell = AttackPreviewCatalog.Enumerate().First(c => c.OffHand == null && c.MainHand.name == "ArmingSword");
            var loot = UnityEngine.Object.Instantiate(cell.MainHand);
            var weapon = UnityEngine.Object.Instantiate(loot.WeaponDefinition);
            var vfx = UnityEngine.Object.Instantiate(weapon.Presentation.AttackVfx);
            try
            {
                using (var data = new UnityEditor.SerializedObject(loot))
                { data.FindProperty("_weaponDefinition").objectReferenceValue = weapon; data.ApplyModifiedPropertiesWithoutUndo(); }
                using (var data = new UnityEditor.SerializedObject(weapon))
                { data.FindProperty("_presentation").FindPropertyRelative("_attackVfx").objectReferenceValue = vfx; data.ApplyModifiedPropertiesWithoutUndo(); }
                using (var data = new UnityEditor.SerializedObject(vfx))
                { data.FindProperty("_poses").GetArrayElementAtIndex(0).FindPropertyRelative("_position").vector3Value = new Vector3(.5f,0,0); data.ApplyModifiedPropertiesWithoutUndo(); }
                using var rig = new AttackPreviewRig();
                var report = new AttackPreviewValidator().Validate(rig,
                    new AttackPreviewCombination(loot, null, AttackPreviewAction.MainAttack, 0));
                Assert.That(report.WorstChecks.Any(c => c.Id == AttackPreviewCheckId.VfxOrigin && c.IsViolation), Is.True);
            }
            finally { UnityEngine.Object.DestroyImmediate(vfx); UnityEngine.Object.DestroyImmediate(weapon); UnityEngine.Object.DestroyImmediate(loot); }
        }

        [TestCase("ArmingSword")] [TestCase("MagicSword")]
        public void RealCatalog_BothHandsAllFacingsSatisfyCanonicalArc(string name)
        {
            var cells = AttackPreviewCatalog.Enumerate().Where(c =>
                (c.MainHand == null || c.OffHand == null) && (c.MainHand ?? c.OffHand).name == name).ToArray();
            Assert.That(cells.Length, Is.EqualTo(12));
            Assert.That(Enum.GetNames(typeof(AttackPreviewCheckId)), Does.Contain("SlashArcContract"));
            foreach (var cell in cells)
            {
                using var rig = new AttackPreviewRig();
                var report = new AttackPreviewValidator().Validate(rig, cell);
                Assert.That(report.WorstChecks.Any(m => m.Id.ToString() == "SlashArcContract"), Is.True,
                    "No arc measurement: " + cell.DisplayName);
                Assert.That(report.WorstChecks.Where(m => (m.Id.ToString() == "SlashArcContract" || m.Id == AttackPreviewCheckId.VfxOrigin) && m.IsViolation),
                    Is.Empty, cell.DisplayName);
            }
        }
    }
}
#endif
