using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Stash
{
    public sealed class StashDisplayOrderTests
    {
        private readonly List<Object> _owned = new();
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [TearDown]
        public void TearDown()
        {
            foreach (Object value in _owned) Object.DestroyImmediate(value);
            _owned.Clear();
        }

        [Test]
        public void PresenterProjection_SortsEveryCategoryThenOrdinalIgnoreCaseNameWithoutMutatingSource()
        {
            LootCategory[] categories = { LootCategory.Valuable, LootCategory.Material, LootCategory.Quest,
                LootCategory.Miscellaneous, LootCategory.Weapon, LootCategory.Helmet, LootCategory.Armor,
                LootCategory.Gloves, LootCategory.Boots, LootCategory.Shield, LootCategory.None };
            var definitions = new List<LootDefinition>();
            for (int i = 0; i < categories.Length; i++)
                definitions.Add(Definition($"item{i:D2}", "same", categories[i]));
            definitions.Add(Definition("alpha", "Alpha", LootCategory.Valuable));
            definitions.Add(Definition("beta", "beta", LootCategory.Valuable));
            var source = definitions.Select(d => new StashItem(d.LootId, 3)).Reverse().ToArray();
            StashItem[] original = source.ToArray();
            var projected = Project(source, definitions);
            Assert.That(projected.Select(s => s.LootId.Value), Is.EqualTo(
                new[] { "alpha", "beta" }.Concat(Enumerable.Range(0, categories.Length).Select(i => $"item{i:D2}"))));
            Assert.That(source, Is.EqualTo(original));
            Assert.That(projected.All(s => s.Amount == 3), Is.True);
        }

        [Test]
        public void PresenterProjection_UsesOrdinalIdTiesRawBlankNameAndKnownCategoryDespiteMissingIcon()
        {
            var definitions = new List<LootDefinition> {
                Definition("tie_b", "SAME", LootCategory.Material),
                Definition("tie_a", "same", LootCategory.Material),
                Definition("aaa_blank", "   ", LootCategory.Material),
                Definition("bbb_null", null, LootCategory.Material),
                Definition("none", "zzz", LootCategory.None),
                Definition("invalid", "yyy", (LootCategory)999) };
            string[] ids = { "missing", "none", "tie_b", "invalid", "tie_a", "bbb_null", "aaa_blank" };
            var projected = Project(ids.Select(id => new StashItem(new LootId(id), 1)).ToArray(), definitions);
            Assert.That(projected.Select(s => s.LootId.Value), Is.EqualTo(new[] {
                "aaa_blank", "bbb_null", "tie_a", "tie_b", "missing", "invalid", "none" }));
            Assert.That(projected[0].DisplayName, Is.EqualTo("aaa_blank"));
            Assert.That(projected[1].DisplayName, Is.EqualTo("bbb_null"));
            Assert.That(projected[4].DisplayName, Is.EqualTo("missing"));
        }

        [Test]
        public void SortedCopy_UnknownAndNoneShareRank_UsesOrdinalTiesAndPreservesAmountsAcrossCultures()
        {
            var catalog = ScriptableObject.CreateInstance<LootDefinitionCatalog>();
            _owned.Add(catalog);
            var definitions = new List<LootDefinition> {
                Definition("z", "I", LootCategory.None),
                Definition("a", "i", (LootCategory)(-1)),
                Definition("known", "zzz", LootCategory.Shield) };
            typeof(LootDefinitionCatalog).GetField("_definitions", Private).SetValue(catalog, definitions);
            var source = new List<StashItem> { new(new LootId("z"), 7),
                new(new LootId("a"), 2), new(new LootId("known"), 9) };
            var original = source.ToArray();
            var culture = System.Globalization.CultureInfo.CurrentCulture;
            try
            {
                System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("tr-TR");
                var sorted = StashDisplayOrder.SortedCopy(source, catalog);
                Assert.That(sorted.Select(s => s.LootId.Value), Is.EqualTo(new[] { "known", "a", "z" }));
                Assert.That(sorted.Select(s => s.Amount), Is.EqualTo(new[] { 9, 2, 7 }));
                Assert.That(source, Is.EqualTo(original));
                Assert.That(sorted, Is.Not.SameAs(source));
            }
            finally { System.Globalization.CultureInfo.CurrentCulture = culture; }
        }

        [Test]
        public void Project_BlankNameCopyPreservesDefinitionMetadataAndOriginalFactoryBehavior()
        {
            var definition = Definition("blank", " ", LootCategory.Material);
            var catalog = ScriptableObject.CreateInstance<LootDefinitionCatalog>();
            _owned.Add(catalog);
            typeof(LootDefinitionCatalog).GetField("_definitions", Private)
                .SetValue(catalog, new List<LootDefinition> { definition });
            var item = new StashItem(definition.LootId, 6);
            var original = RaidInventorySlotData.Create(new LootEntry(item.LootId, item.Amount), definition, null);
            var projected = StashDisplayOrder.Project(new[] { item }, catalog, null)[0];
            Assert.That(projected.DisplayName, Is.EqualTo("blank"));
            Assert.That(original.DisplayName, Is.EqualTo(" "));
            Assert.That(projected.Category, Is.EqualTo(original.Category));
            Assert.That(projected.Tooltip, Is.EqualTo(original.Tooltip));
            Assert.That(projected.Icon, Is.EqualTo(original.Icon));
            Assert.That(projected.UsesFallback, Is.EqualTo(original.UsesFallback));
            Assert.That(projected.WeaponHandedness, Is.EqualTo(original.WeaponHandedness));
            Assert.That(projected.HasWeaponDefinition, Is.EqualTo(original.HasWeaponDefinition));
            Assert.That(projected.Amount, Is.EqualTo(6));
            Assert.That(projected.LootId, Is.EqualTo(item.LootId));
        }

        private IReadOnlyList<RaidInventorySlotData> Project(StashItem[] source, List<LootDefinition> definitions)
        {
            var catalog = ScriptableObject.CreateInstance<LootDefinitionCatalog>();
            _owned.Add(catalog);
            typeof(LootDefinitionCatalog).GetField("_definitions", Private).SetValue(catalog, definitions);
            var host = new GameObject("Inactive projection test");
            host.SetActive(false);
            _owned.Add(host);
            var presenter = host.AddComponent<LobbyStashPresenter>();
            typeof(LobbyStashPresenter).GetField("_lootCatalog", Private).SetValue(presenter, catalog);
            return (IReadOnlyList<RaidInventorySlotData>)typeof(LobbyStashPresenter)
                .GetMethod("MapToPresentation", Private).Invoke(presenter, new object[] { source });
        }

        private LootDefinition Definition(string id, string name, LootCategory category)
        {
            var definition = ScriptableObject.CreateInstance<LootDefinition>();
            _owned.Add(definition);
            typeof(LootDefinition).GetField("_id", Private).SetValue(definition, id);
            typeof(LootDefinition).GetField("_displayName", Private).SetValue(definition, name);
            typeof(LootDefinition).GetField("_category", Private).SetValue(definition, category);
            return definition;
        }
    }
}
