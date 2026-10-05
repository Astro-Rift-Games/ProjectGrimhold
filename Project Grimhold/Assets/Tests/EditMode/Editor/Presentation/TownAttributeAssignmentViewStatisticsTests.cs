#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.EditMode.Presentation
{
    public sealed class TownAttributeAssignmentViewStatisticsTests
    {
        private const string ViewPath = "Assets/Prefabs/TownAttributeAssignmentView.prefab";

        private GameObject _instance;
        private TownAttributeAssignmentView _view;

        [SetUp]
        public void SetUp()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ViewPath);
            Assert.That(prefab, Is.Not.Null);
            _instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            _view = _instance.GetComponent<TownAttributeAssignmentView>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_instance);

        private static TownCharacterStatisticsPresentation Create(
            int healthFromAttributes = 125,
            int healthFromEquipment = 20,
            TownWeaponStatistics[] weapons = null,
            TownEquipmentSlotEntry[] slots = null) => new(
            new TownStatBreakdown(healthFromAttributes, healthFromEquipment),
            new TownStatBreakdown(100, 0),
            new TownStatBreakdown(100, 0),
            30, 12, 23.1f, 10.7f, 10f,
            weapons ?? new TownWeaponStatistics[0],
            slots ?? new TownEquipmentSlotEntry[0]);

        private TownStatLineView[] ActiveLines() =>
            _instance.GetComponentsInChildren<TownStatLineView>(false);

        [Test]
        public void PresentStatistics_ShowsEverySheetRowWithItsText()
        {
            _view.PresentStatistics(Create());

            TownStatLineView[] lines = ActiveLines();
            TownStatLineView health = lines.Single(line => line.Label == "Max Health");
            Assert.That(health.Value, Is.EqualTo("145"));
            Assert.That(health.Detail, Is.EqualTo("+20 from equipment"));
            Assert.That(lines.Any(line => line.Label == "Core"), Is.True);
            Assert.That(lines.Any(line => line.Label == "Loot Bonus" && line.Value == "10%"), Is.True);
            Assert.That(lines.Any(line => line.Label == "No weapons equipped"), Is.True);
        }

        [Test]
        public void PresentStatistics_Twice_ReplacesTheRowsInsteadOfAccumulatingThem()
        {
            _view.PresentStatistics(Create());
            int firstCount = ActiveLines().Length;

            _view.PresentStatistics(Create(healthFromEquipment: 0));

            Assert.That(ActiveLines().Length, Is.EqualTo(firstCount));
            Assert.That(ActiveLines().Single(line => line.Label == "Max Health").Detail, Is.Empty);
        }

        [Test]
        public void PresentStatistics_ListsEquippedWeapons()
        {
            var sword = new TownWeaponStatistics(
                EquipmentSlot.WeaponSetAMainHand, new LootId("arming_sword"), "Arming Sword",
                DamageType.Physical, 30f, 34f);

            _view.PresentStatistics(Create(weapons: new[] { sword }));

            TownStatLineView line = ActiveLines().Single(l => l.Label == "Arming Sword");
            Assert.That(line.Value, Is.EqualTo("34"));
            Assert.That(ActiveLines().Any(l => l.Label == "No weapons equipped"), Is.False);
        }

        [Test]
        public void PresentStatisticsUnavailable_ReplacesTheSheetWithASingleNote()
        {
            _view.PresentStatistics(Create());

            _view.PresentStatisticsUnavailable();

            TownStatLineView[] lines = ActiveLines();
            Assert.That(lines, Has.Length.EqualTo(1));
            Assert.That(lines[0].Label, Is.EqualTo("Statistics unavailable"));
        }

        [Test]
        public void PresentStatistics_FillsTheEquipmentSlotsByTheirSlotId()
        {
            Sprite icon = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.zero);
            try
            {
                var slots = new[]
                {
                    new TownEquipmentSlotEntry(EquipmentSlot.Helmet, new LootId("helm"), "Steel Helm", icon),
                    new TownEquipmentSlotEntry(EquipmentSlot.Boots, default, string.Empty, null)
                };

                _view.PresentStatistics(Create(slots: slots));

                TownEquipmentSlotView[] views = _instance.GetComponentsInChildren<TownEquipmentSlotView>(true);
                Assert.That(views.Select(v => v.Slot).Distinct().Count(), Is.EqualTo(8), "All eight slots are authored.");
                TownEquipmentSlotView helmet = views.Single(v => v.Slot == EquipmentSlot.Helmet);
                Assert.That(helmet.HasItem, Is.True);
                Assert.That(helmet.ItemName, Is.EqualTo("Steel Helm"));
                Assert.That(views.Single(v => v.Slot == EquipmentSlot.Boots).HasItem, Is.False);
                Assert.That(views.Single(v => v.Slot == EquipmentSlot.Armor).HasItem, Is.False,
                    "A slot missing from the presentation is shown empty.");
            }
            finally
            {
                Object.DestroyImmediate(icon);
            }
        }

        [Test]
        public void Present_UsesEnglishAvailablePointsText()
        {
            Assert.That(CharacterAttributeState.TryCreate(10, 5, 5, 5, 5, 5, 3, out var state), Is.True);
            Assert.That(
                TownAttributeAssignmentPresentation.TryCreate(state, ProgressionBalanceDefaults.InitialMaximumAttributeValue, out var presentation),
                Is.True);

            _view.Present(presentation);

            Assert.That(_view.AvailablePointsText.text, Is.EqualTo("Available Points: 3"));
        }
    }
}
#endif
