using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Loot
{
    public sealed class LootContextActionProviderTests
    {
        private LootDefinition _definition;

        [SetUp]
        public void SetUp()
        {
            _definition = ScriptableObject.CreateInstance<LootDefinition>();
            SetField("_id", "coin");
            SetField("_displayName", "Coin");
            SetField("_category", LootCategory.Valuable);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_definition);
        }

        [Test]
        public void DropProvider_ContributesStableOrderedActions()
        {
            var provider = new LootDropContextActionProvider();
            var actions = new List<LootContextActionDescriptor>();
            var context = new LootContextActionContext(
                new LootEntry(new LootId("coin"), 1),
                _definition);

            provider.CollectActions(context, actions);

            Assert.That(actions, Has.Count.EqualTo(2));
            Assert.That(actions[0].Id, Is.EqualTo(LootDropContextActionProvider.DropSingleId));
            Assert.That(actions[0].Label, Is.EqualTo("Soltar"));
            Assert.That(actions[1].Id, Is.EqualTo(LootDropContextActionProvider.DropAllId));
            Assert.That(actions[1].Label, Is.EqualTo("Soltar todo"));
            Assert.That(actions[0].IsEnabled, Is.False);
            Assert.That(actions[1].IsEnabled, Is.False);
        }

        [Test]
        public void InvalidDefinition_ContributesNoActions()
        {
            var provider = new LootDropContextActionProvider();
            var actions = new List<LootContextActionDescriptor>();
            var context = new LootContextActionContext(
                new LootEntry(new LootId("other"), 1),
                _definition);

            provider.CollectActions(context, actions);

            Assert.That(actions, Is.Empty);
        }

        private void SetField(string fieldName, object value)
        {
            typeof(LootDefinition)
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_definition, value);
        }
    }
}
