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

            Assert.That(actions, Has.Count.EqualTo(3));
            Assert.That(actions[0].Id, Is.EqualTo(LootDropContextActionProvider.DropSingleId));
            Assert.That(actions[0].Label, Is.EqualTo("Soltar"));
            Assert.That(actions[1].Id, Is.EqualTo(new LootContextActionId("drop.amount")));
            Assert.That(actions[1].Label, Is.EqualTo("Soltar cantidad..."));
            Assert.That(actions[2].Id, Is.EqualTo(LootDropContextActionProvider.DropAllId));
            Assert.That(actions[2].Label, Is.EqualTo("Soltar todo"));
            Assert.That(actions[2].IsEnabled, Is.False);
            Assert.That(actions[0].IsEnabled, Is.False);
            Assert.That(actions[1].IsEnabled, Is.False);
        }

        [TestCase(1, false, false)]
        [TestCase(2, false, false)]
        [TestCase(1, true, false)]
        [TestCase(2, true, true)]
        public void DropAmount_RequiresAddressAndPartialStack(int amount, bool addressed, bool expected)
        {
            var holder = new GameObject("Drop provider test");
            try
            {
                var provider = new LootDropContextActionProvider();
                provider.Bind(holder.AddComponent<PlayerLootDropNetworkController>());
                var context = new LootContextActionContext(new LootEntry(new LootId("coin"), amount),
                    _definition, addressed ? 0 : -1, addressed ? 0 : -1);
                var actions = new List<LootContextActionDescriptor>();
                provider.CollectActions(context, actions);
                Assert.That(actions[1].IsEnabled, Is.EqualTo(expected));
            }
            finally { Object.DestroyImmediate(holder); }
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
