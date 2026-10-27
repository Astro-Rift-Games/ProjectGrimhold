#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.AttackAnimationValidator
{
    public sealed class AttackPreviewCatalogTests
    {
        internal static LootDefinition Sword => AssetDatabase.LoadAssetAtPath<LootDefinition>("Assets/Scriptable Objects/Loot/Definitions/ArmingSword.asset");
        private static LootDefinition[] Definitions() => AssetDatabase.FindAssets("t:LootDefinition")
            .Select(g => AssetDatabase.LoadAssetAtPath<LootDefinition>(AssetDatabase.GUIDToAssetPath(g))).ToArray();

        [Test]
        public void TwoHandedWeapons_NeverCoexistWithOffHand()
        {
            var cells = AttackPreviewCatalog.Enumerate();
            Assert.That(cells, Is.Not.Empty);
            Assert.That(cells.Where(c => c.MainHand?.WeaponDefinition?.Handedness == WeaponHandedness.TwoHanded)
                .All(c => c.OffHand == null), Is.True);
        }

        [Test]
        public void Spellbook_IsMainOnlyAndNeverPairedWithAnyOffHandItem()
        {
            var book = AssetDatabase.LoadAssetAtPath<LootDefinition>("Assets/Scriptable Objects/Loot/Definitions/Spellbook.asset");
            Assert.That(book, Is.Not.Null);
            var cells = AttackPreviewCatalog.Enumerate();
            Assert.That(cells.Any(c => c.OffHand == book), Is.False);
            var bookCells = cells.Where(c => c.MainHand == book).ToArray();
            Assert.That(bookCells, Has.Length.EqualTo(12));
            Assert.That(bookCells.All(c => c.OffHand == null &&
                (c.Action == AttackPreviewAction.MainAttack || c.Action == AttackPreviewAction.Knockback)), Is.True);
            Assert.That(bookCells.Where(c => c.Action == AttackPreviewAction.MainAttack).Select(c => c.FacingIndex).OrderBy(i => i),
                Is.EqualTo(Enumerable.Range(0, 6)));
            Assert.That(bookCells.Where(c => c.Action == AttackPreviewAction.Knockback).Select(c => c.FacingIndex).OrderBy(i => i),
                Is.EqualTo(Enumerable.Range(0, 6)));
        }

        [Test]
        public void EveryValidOneHandedWeapon_HasBothAttacksInAllSixFacings()
        {
            var weapons = Definitions().Where(d => d.Category == LootCategory.Weapon && d.WeaponDefinition != null &&
                d.WeaponDefinition.Handedness == WeaponHandedness.OneHanded && d.WeaponDefinition.TryValidate(out _)).ToArray();
            Assert.That(weapons, Is.Not.Empty);
            var cells = AttackPreviewCatalog.Enumerate();
            foreach (var weapon in weapons)
            for (int facing = 0; facing < 6; facing++)
            {
                Assert.That(cells.Any(c => c.MainHand == weapon && c.Action == AttackPreviewAction.MainAttack && c.FacingIndex == facing), Is.True, weapon.name);
                Assert.That(cells.Any(c => c.OffHand == weapon && c.Action == AttackPreviewAction.OffAttack && c.FacingIndex == facing), Is.True, weapon.name);
                Assert.That(cells.Any(c => c.MainHand == weapon && c.Action == AttackPreviewAction.Knockback && c.FacingIndex == facing), Is.True, weapon.name);
            }
        }

        [Test]
        public void Shields_NeverAttack()
        {
            var cells = AttackPreviewCatalog.Enumerate();
            Assert.That(cells, Is.Not.Empty);
            foreach (var c in cells)
            {
                if (c.Action == AttackPreviewAction.MainAttack) Assert.That(c.MainHand.Category, Is.EqualTo(LootCategory.Weapon));
                if (c.Action == AttackPreviewAction.OffAttack) Assert.That(c.OffHand.Category, Is.EqualTo(LootCategory.Weapon));
            }
        }

        [Test]
        public void Enumeration_IsDeterministicWithUniqueStableIds()
        {
            var first = AttackPreviewCatalog.Enumerate();
            Assert.That(first, Is.Not.Empty);
            Assert.That(first.Select(c => c.Id), Is.EqualTo(AttackPreviewCatalog.Enumerate().Select(c => c.Id)));
            Assert.That(first.Select(c => c.Id).Distinct().Count(), Is.EqualTo(first.Count));
            Assert.That(first.All(c => !string.IsNullOrWhiteSpace(c.DisplayName)), Is.True);
        }

        [Test]
        public void DualEquipment_DoesNotEnableSimultaneousStarts()
        {
            Assert.That(AttackPreviewCatalog.SupportsSimultaneousAttacks, Is.False,
                "Combat accepts at most one attack per tick with Main-first arbitration");
            var cells = AttackPreviewCatalog.Enumerate(new[] { Sword });
            Assert.That(cells.Any(c => c.MainHand == Sword && c.OffHand == Sword && c.Action == AttackPreviewAction.MainAttack), Is.True);
            Assert.That(cells.Any(c => c.MainHand == Sword && c.OffHand == Sword && c.Action == AttackPreviewAction.OffAttack), Is.True);
            Assert.That(cells.Any(c => c.MainHand == Sword && c.OffHand == Sword && c.Action == AttackPreviewAction.Knockback), Is.True);
            Assert.That(cells.All(c => c.Action == AttackPreviewAction.MainAttack || c.Action == AttackPreviewAction.OffAttack ||
                c.Action == AttackPreviewAction.Defend || c.Action == AttackPreviewAction.Knockback), Is.True);
        }

        [Test]
        public void PairEligibility_MirrorsRuntimeEitherHandWeaponAndTwoHandBlock()
        {
            Assert.That(AttackPreviewCatalog.IsLegalPair(Sword, null), Is.True);
            Assert.That(AttackPreviewCatalog.IsLegalPair(null, Sword), Is.True);
            Assert.That(AttackPreviewCatalog.IsLegalPair(Sword, Sword), Is.True);
            Assert.That(AttackPreviewCatalog.IsLegalPair(null, null), Is.False);
            var twoHanded = Definitions().First(d => d.WeaponDefinition != null && d.WeaponDefinition.Handedness == WeaponHandedness.TwoHanded && d.WeaponDefinition.TryValidate(out _));
            Assert.That(AttackPreviewCatalog.IsLegalPair(twoHanded, null), Is.True);
            Assert.That(AttackPreviewCatalog.IsLegalPair(twoHanded, Sword), Is.False);
            Assert.That(AttackPreviewCatalog.IsLegalPair(null, twoHanded), Is.False);
        }

        [Test]
        public void MissingOffHandSet_IsListedAndFlaggedRatherThanFiltered()
        {
            var loot = Object.Instantiate(Sword);
            var weapon = Object.Instantiate(Sword.WeaponDefinition);
            try
            {
                var lootData = new SerializedObject(loot);
                lootData.FindProperty("_weaponDefinition").objectReferenceValue = weapon;
                lootData.ApplyModifiedPropertiesWithoutUndo();
                var data = new SerializedObject(weapon);
                data.FindProperty("_presentation").FindPropertyRelative("_offHandAttackAnimationSet").objectReferenceValue = null;
                data.ApplyModifiedPropertiesWithoutUndo();
                var cells = AttackPreviewCatalog.Enumerate(new[] { loot }).Where(c => c.Action == AttackPreviewAction.OffAttack).ToArray();
                Assert.That(cells.Select(c => c.FacingIndex).Distinct().OrderBy(i => i), Is.EqualTo(Enumerable.Range(0, 6)));
                Assert.That(cells.All(c => c.MissingPresentation), Is.True);
            }
            finally { Object.DestroyImmediate(loot); Object.DestroyImmediate(weapon); }
        }
    }
}
#endif
