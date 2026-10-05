#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Tests.EditMode.Presentation
{
    public sealed class TownCharacterStatisticsLinesTests
    {
        private static TownCharacterStatisticsPresentation Create(
            TownStatBreakdown? health = null,
            int physicalDefense = 0,
            float physicalMitigation = 0f,
            int magicalDefense = 0,
            float magicalMitigation = 0f,
            float loot = 0f,
            TownWeaponStatistics[] weapons = null) => new(
            health ?? new TownStatBreakdown(125, 0),
            new TownStatBreakdown(100, 0),
            new TownStatBreakdown(100, 0),
            physicalDefense,
            magicalDefense,
            physicalMitigation,
            magicalMitigation,
            loot,
            weapons ?? new TownWeaponStatistics[0],
            new TownEquipmentSlotEntry[0]);

        private static TownStatLine Find(IReadOnlyList<TownStatLine> lines, string label) =>
            lines.Single(line => line.Label == label);

        [Test]
        public void Sections_AppearInSheetOrder()
        {
            IReadOnlyList<TownStatLine> lines = TownCharacterStatisticsLines.Build(Create());

            Assert.That(
                lines.Where(line => line.Kind == TownStatLineKind.Header).Select(line => line.Label),
                Is.EqualTo(new[] { "Core", "Defense", "Weapons", "Utility" }));
        }

        [Test]
        public void CoreResources_ShowTotalsAndTheEquipmentBonusOnlyWhenItIsNotZero()
        {
            IReadOnlyList<TownStatLine> lines = TownCharacterStatisticsLines.Build(
                Create(health: new TownStatBreakdown(125, 20)));

            TownStatLine health = Find(lines, "Max Health");
            Assert.That(health.Value, Is.EqualTo("145"));
            Assert.That(health.Detail, Is.EqualTo("+20 from equipment"));
            TownStatLine stamina = Find(lines, "Max Stamina");
            Assert.That(stamina.Value, Is.EqualTo("100"));
            Assert.That(stamina.Detail, Is.Empty);
            Assert.That(Find(lines, "Max Mana").Value, Is.EqualTo("100"));
        }

        [Test]
        public void NegativeEquipmentContribution_IsShownWithItsSign()
        {
            IReadOnlyList<TownStatLine> lines = TownCharacterStatisticsLines.Build(
                Create(health: new TownStatBreakdown(125, -5)));

            Assert.That(Find(lines, "Max Health").Value, Is.EqualTo("120"));
            Assert.That(Find(lines, "Max Health").Detail, Is.EqualTo("-5 from equipment"));
        }

        [Test]
        public void Defense_ShowsValueAndMitigation()
        {
            IReadOnlyList<TownStatLine> lines = TownCharacterStatisticsLines.Build(
                Create(physicalDefense: 100, physicalMitigation: 50f, magicalDefense: 30, magicalMitigation: 23.0769f));

            Assert.That(Find(lines, "Physical Defense").Value, Is.EqualTo("100"));
            Assert.That(Find(lines, "Physical Defense").Detail, Is.EqualTo("50% reduction"));
            Assert.That(Find(lines, "Magical Defense").Value, Is.EqualTo("30"));
            Assert.That(Find(lines, "Magical Defense").Detail, Is.EqualTo("23.1% reduction"));
        }

        [Test]
        public void NoWeapons_ShowsANote()
        {
            IReadOnlyList<TownStatLine> lines = TownCharacterStatisticsLines.Build(Create());

            Assert.That(
                lines.Any(line => line.Kind == TownStatLineKind.Note && line.Label == "No weapons equipped"),
                Is.True);
        }

        [Test]
        public void Weapon_ShowsEffectiveDamageWithSlotTypeAndBaseOnlyWhenScaled()
        {
            var scaled = new TownWeaponStatistics(
                EquipmentSlot.WeaponSetAMainHand, new LootId("arming_sword"), "Arming Sword",
                DamageType.Physical, 30f, 34f);
            var unscaled = new TownWeaponStatistics(
                EquipmentSlot.WeaponSetBMainHand, new LootId("magic_wand"), "Magic Wand",
                DamageType.Magical, 22f, 22f);

            IReadOnlyList<TownStatLine> lines = TownCharacterStatisticsLines.Build(
                Create(weapons: new[] { scaled, unscaled }));

            TownStatLine sword = Find(lines, "Arming Sword");
            Assert.That(sword.Value, Is.EqualTo("34"));
            Assert.That(sword.Detail, Is.EqualTo("Set A Main Hand · Physical · base 30"));
            TownStatLine wand = Find(lines, "Magic Wand");
            Assert.That(wand.Value, Is.EqualTo("22"));
            Assert.That(wand.Detail, Is.EqualTo("Set B Main Hand · Magical"));
            Assert.That(
                lines.Any(line => line.Kind == TownStatLineKind.Note),
                Is.False,
                "The empty-weapons note must only appear without weapons.");
        }

        [Test]
        public void LootBonus_IsShownAsAPercentage()
        {
            IReadOnlyList<TownStatLine> lines = TownCharacterStatisticsLines.Build(Create(loot: 12.5f));

            Assert.That(Find(lines, "Loot Bonus").Value, Is.EqualTo("12.5%"));
        }

        [Test]
        public void Formatting_DoesNotDependOnTheCurrentCulture()
        {
            System.Globalization.CultureInfo previous = System.Globalization.CultureInfo.CurrentCulture;
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("es-AR");
            try
            {
                IReadOnlyList<TownStatLine> lines = TownCharacterStatisticsLines.Build(Create(loot: 12.5f));

                Assert.That(Find(lines, "Loot Bonus").Value, Is.EqualTo("12.5%"));
            }
            finally
            {
                System.Globalization.CultureInfo.CurrentCulture = previous;
            }
        }
    }
}
#endif
