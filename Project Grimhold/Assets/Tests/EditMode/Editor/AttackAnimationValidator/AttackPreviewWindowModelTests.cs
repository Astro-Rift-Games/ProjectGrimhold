#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Linq;
using NUnit.Framework;

namespace Tests.EditMode.AttackAnimationValidator
{
    public sealed class AttackPreviewWindowModelTests
    {
        private static System.Collections.Generic.IReadOnlyList<AttackPreviewCombination> Catalog => AttackPreviewCatalog.Enumerate();
        private static bool Matches(AttackPreviewCombination c, AttackPreviewEquipmentKind kind)
        {
            bool main = c.MainHand != null, off = c.OffHand != null;
            switch (kind)
            {
                case AttackPreviewEquipmentKind.OneHandMain: return main && !off && c.MainHand.WeaponDefinition.Handedness == WeaponHandedness.OneHanded;
                case AttackPreviewEquipmentKind.OneHandOff: return !main && off;
                case AttackPreviewEquipmentKind.TwoHand: return main && c.MainHand.WeaponDefinition?.Handedness == WeaponHandedness.TwoHanded;
                case AttackPreviewEquipmentKind.DualWield: return main && off && c.MainHand.Category == LootCategory.Weapon && c.OffHand.Category == LootCategory.Weapon;
                default: return main && off && (c.MainHand.Category == LootCategory.Shield || c.OffHand.Category == LootCategory.Shield);
            }
        }
        [TestCase(AttackPreviewEquipmentKind.OneHandMain)]
        [TestCase(AttackPreviewEquipmentKind.OneHandOff)]
        [TestCase(AttackPreviewEquipmentKind.TwoHand)]
        [TestCase(AttackPreviewEquipmentKind.DualWield)]
        [TestCase(AttackPreviewEquipmentKind.ShieldAndWeapon)]
        public void EquipmentFilter_ClassifiesEntireCatalog(AttackPreviewEquipmentKind kind)
        {
            var catalog = Catalog;
            var expected = catalog.Where(c => Matches(c, kind)).ToArray();
            Assert.That(expected, Is.Not.Empty);
            foreach (var cell in expected) Assert.That(AttackPreviewWindowModel.Classify(cell), Is.EqualTo(kind));
            var model = new AttackPreviewWindowModel(catalog) { EquipmentKind = kind };
            Assert.That(model.VisibleCells, Is.EqualTo(expected));
        }
        [Test]
        public void CombinedFilters_IntersectWithCaseInsensitiveWeaponSearch()
        {
            var catalog = Catalog;
            var seed = catalog.First(c => Matches(c, AttackPreviewEquipmentKind.OneHandOff));
            var model = new AttackPreviewWindowModel(catalog) { Search = seed.OffHand.name.ToUpperInvariant(),
                EquipmentKind = AttackPreviewEquipmentKind.OneHandOff, Action = AttackPreviewAction.OffAttack, Facing = 4,
                Outcome = AttackPreviewCellOutcome.Unvalidated };
            Assert.That(model.VisibleCells, Is.EqualTo(catalog.Where(c => Matches(c, AttackPreviewEquipmentKind.OneHandOff) &&
                c.Action == AttackPreviewAction.OffAttack && c.FacingIndex == 4 &&
                c.OffHand.name.IndexOf(seed.OffHand.name, StringComparison.OrdinalIgnoreCase) >= 0)));
            model.Search = "no-such-weapon-xyz";
            Assert.That(model.VisibleCells, Is.Empty);
        }
        [Test]
        public void Rows_GroupEquipmentAndActionInCatalogOrderWithSixFacingColumns()
        {
            var catalog = Catalog;
            var model = new AttackPreviewWindowModel(catalog);
            var expected = catalog.GroupBy(c => (c.MainHand, c.OffHand, c.Action)).ToArray();
            Assert.That(model.Rows.Count, Is.EqualTo(expected.Length));
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.That(model.Rows[i], Is.EqualTo(expected[i].OrderBy(c => c.FacingIndex)));
                Assert.That(model.Rows[i].Select(c => c.FacingIndex), Is.EqualTo(Enumerable.Range(0, 6)));
            }
        }
        [Test]
        public void FilteredCollections_AreReusedAndInvalidatedByEveryFilterAndReport()
        {
            var model = new AttackPreviewWindowModel(Catalog);
            System.Action<System.Action> assertInvalidates = change =>
            {
                var cells = model.VisibleCells;
                var rows = model.Rows;
                Assert.That(model.VisibleCells, Is.SameAs(cells));
                Assert.That(model.Rows, Is.SameAs(rows));
                change();
                Assert.That(model.VisibleCells, Is.Not.SameAs(cells));
                Assert.That(model.Rows, Is.Not.SameAs(rows));
            };
            assertInvalidates(() => model.Search = "sword");
            assertInvalidates(() => model.EquipmentKind = AttackPreviewEquipmentKind.OneHandMain);
            assertInvalidates(() => model.Action = AttackPreviewAction.MainAttack);
            assertInvalidates(() => model.Facing = 3);
            assertInvalidates(() => model.Outcome = AttackPreviewCellOutcome.Pass);
            assertInvalidates(() => model.SetReport(new AttackPreviewReport(Catalog[0])));
            var cached = model.VisibleCells;
            model.Search = model.Search;
            Assert.That(model.VisibleCells, Is.SameAs(cached), "Assigning an unchanged filter should not rebuild.");
            var replacement = new AttackPreviewWindowModel(new[] { Catalog[0] });
            Assert.That(replacement.VisibleCells.Count, Is.EqualTo(1), "A new catalog must not reuse another model's cache.");
        }

        [Test]
        public void Selection_RetainsCellWhileFiltersChange()
        {
            var model = new AttackPreviewWindowModel(Catalog);
            var selected = Catalog[0];
            model.Selection = selected;
            model.Search = "no-such-weapon-xyz";
            Assert.That(model.Selection, Is.SameAs(selected));
        }
        [Test]
        public void Counterpart_MapsSingleWeaponOffAndMainBothWaysAtSameFacing()
        {
            var catalog = Catalog;
            var model = new AttackPreviewWindowModel(catalog);
            foreach (var off in catalog.Where(c => c.MainHand == null && c.Action == AttackPreviewAction.OffAttack))
            {
                var main = model.Counterpart(off);
                Assert.That(main, Is.Not.Null);
                Assert.That(main.MainHand, Is.SameAs(off.OffHand));
                Assert.That(main.FacingIndex, Is.EqualTo(off.FacingIndex));
                Assert.That(main.Action, Is.EqualTo(AttackPreviewAction.MainAttack));
                Assert.That(model.Counterpart(main).Id, Is.EqualTo(off.Id));
            }
        }
        [Test]
        public void Counterpart_DefendKnockbackAndTwoHandHaveNone()
        {
            var catalog = Catalog;
            var model = new AttackPreviewWindowModel(catalog);
            var excluded = catalog.Where(c => c.Action == AttackPreviewAction.Defend ||
                c.Action == AttackPreviewAction.Knockback || Matches(c, AttackPreviewEquipmentKind.TwoHand)).ToArray();
            Assert.That(excluded, Is.Not.Empty);
            foreach (var c in excluded) Assert.That(model.Counterpart(c), Is.Null);
        }
        [TestCase(0.25f)]
        [TestCase(0.5f)]
        [TestCase(1f)]
        public void Timeline_AdvancesAtSelectedSpeedAndPauses(float speed)
        {
            var model = new AttackPreviewWindowModel(Catalog) { Playing = true, Speed = speed };
            model.Advance(0.5f, 2f);
            Assert.That(model.NormalizedTime, Is.EqualTo(0.25f * speed).Within(0.00001f));
            model.Playing = false;
            model.Advance(1f, 2f);
            Assert.That(model.NormalizedTime, Is.EqualTo(0.25f * speed).Within(0.00001f));
        }
        [TestCase(true)]
        [TestCase(false)]
        public void Timeline_EndLoopsOrStops(bool loop)
        {
            var model = new AttackPreviewWindowModel(Catalog) { Playing = true, Loop = loop, NormalizedTime = 0.9f };
            model.Advance(0.3f, 1f);
            Assert.That(model.NormalizedTime, Is.EqualTo(loop ? 0.2f : 1f).Within(0.00001f));
            Assert.That(model.Playing, Is.EqualTo(loop));
        }
        [Test]
        public void Reports_StoreReplaceFilterAndSummarizeWithoutFilterDependence()
        {
            var catalog = Catalog;
            var model = new AttackPreviewWindowModel(catalog);
            var outcomes = new[] { AttackPreviewCellOutcome.Pass, AttackPreviewCellOutcome.Warning, AttackPreviewCellOutcome.Fail };
            for (int i = 0; i < 3; i++)
            {
                var report = new AttackPreviewReport(catalog[i]);
                report.AddSample(0, i == 0 ? Array.Empty<AttackPreviewCheck>() : new[] {
                    new AttackPreviewCheck(AttackPreviewCheckId.DepthOrder, i == 1 ? AttackPreviewSeverity.Warning : AttackPreviewSeverity.Error,
                        AttackPreviewHand.Main, 0, 1, 0, "fixture") });
                model.SetReport(report);
                Assert.That(model.GetReport(catalog[i].Id), Is.SameAs(report));
                model.Outcome = outcomes[i];
                Assert.That(model.VisibleCells, Is.EqualTo(new[] { catalog[i] }));
                Assert.That(model.Count(outcomes[i]), Is.EqualTo(1));
            }
            Assert.That(model.Count(AttackPreviewCellOutcome.Unvalidated), Is.EqualTo(catalog.Count - 3));
            model.SetReport(new AttackPreviewReport(catalog[2]));
            Assert.That(model.Count(AttackPreviewCellOutcome.Fail), Is.Zero);
            Assert.That(model.Count(AttackPreviewCellOutcome.Pass), Is.EqualTo(2));
        }
    }
}
#endif
