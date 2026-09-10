using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Presentation
{
    public sealed class RaidInventorySlotDataTests
    {
        private Texture2D _texture;
        private Sprite _sprite;
        private LootDefinition _definition;

        [SetUp]
        public void SetUp()
        {
            _texture = new Texture2D(8, 8);
            _sprite = Sprite.Create(_texture, new Rect(0, 0, 8, 8), Vector2.zero);
            _definition = ScriptableObject.CreateInstance<LootDefinition>();
            SetField("_id", "coin");
            SetField("_displayName", "Coin");
            SetField("_icon", _sprite);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_definition);
            Object.DestroyImmediate(_sprite);
            Object.DestroyImmediate(_texture);
        }

        [Test]
        public void Create_ResolvedDefinitionUsesConfiguredMetadata()
        {
            RaidInventorySlotData data = RaidInventorySlotData.Create(
                new LootEntry(new LootId("coin"), 3),
                _definition,
                null);

            Assert.That(data.IsOccupied, Is.True);
            Assert.That(data.DisplayName, Is.EqualTo("Coin"));
            Assert.That(data.Icon, Is.SameAs(_sprite));
            Assert.That(data.Amount, Is.EqualTo(3));
            Assert.That(data.UsesFallback, Is.False);
            Assert.That(data.Tooltip.Title, Is.EqualTo("Coin"));
            Assert.That(
                data.Tooltip.Status,
                Is.EqualTo(EquipmentTooltipPresentationStatus.NoEquipmentStatistics));
        }

        [Test]
        public void Create_NullIconUsesPlaceholder()
        {
            SetField("_icon", null);

            RaidInventorySlotData data = RaidInventorySlotData.Create(
                new LootEntry(new LootId("coin"), 2),
                _definition,
                _sprite);

            Assert.That(data.Icon, Is.SameAs(_sprite));
            Assert.That(data.DisplayName, Is.EqualTo("Coin"));
            Assert.That(data.UsesFallback, Is.True);
        }

        [Test]
        public void Create_UnresolvedDefinitionDegradesOnlyThatSlot()
        {
            RaidInventorySlotData data = RaidInventorySlotData.Create(
                new LootEntry(new LootId("unknown_loot"), 7),
                null,
                _sprite);

            Assert.That(data.IsOccupied, Is.True);
            Assert.That(data.Icon, Is.SameAs(_sprite));
            Assert.That(data.DisplayName, Is.EqualTo("unknown_loot"));
            Assert.That(data.Amount, Is.EqualTo(7));
            Assert.That(data.UsesFallback, Is.True);
            Assert.That(data.Tooltip.CanShow, Is.False);
            Assert.That(RaidInventorySlotData.Empty.IsOccupied, Is.False);
        }

        private void SetField(string fieldName, object value)
        {
            FieldInfo field = typeof(LootDefinition).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(_definition, value);
        }
    }
}
