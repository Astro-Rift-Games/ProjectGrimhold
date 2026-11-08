using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Loot
{
    public sealed class LootSplitContextActionProviderTests
    {
        [TestCase(false, true, 5, false, true, false)]
        [TestCase(true, false, 5, false, true, false)]
        [TestCase(true, true, 1, false, true, false)]
        [TestCase(true, true, 5, true, true, false)]
        [TestCase(true, true, 5, false, false, false)]
        [TestCase(true, true, 5, false, true, true)]
        public void Split_RequiresControllerAddressPartialNonEquipmentStackAndCapacity(
            bool bound, bool addressed, int quantity, bool equipment, bool free, bool enabled)
        {
            Type providerType = typeof(LootDropContextActionProvider).Assembly.GetType("LootSplitContextActionProvider");
            Assert.That(providerType, Is.Not.Null, "Raid split provider must exist.");
            var holder = new GameObject("Split provider test");
            var definition = ScriptableObject.CreateInstance<LootDefinition>();
            try
            {
                typeof(LootDefinition).GetField("_id", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(definition, "coin");
                typeof(LootDefinition).GetField("_category", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(definition, equipment ? LootCategory.Helmet : LootCategory.Valuable);
                var provider = (ILootContextActionProvider)Activator.CreateInstance(providerType);
                Type controllerType = providerType.Assembly.GetType("PlayerLootSplitNetworkController");
                providerType.GetMethod("Bind").Invoke(provider, new object[]
                {
                    bound ? holder.AddComponent(controllerType) : null, (Func<bool>)(() => free)
                });
                var context = new LootContextActionContext(new LootEntry(new LootId("coin"), quantity),
                    definition, addressed ? 0 : -1, addressed ? 0 : -1);
                var actions = new List<LootContextActionDescriptor>();
                provider.CollectActions(context, actions);
                Assert.That(actions, Has.Count.EqualTo(1));
                Assert.That(actions[0].Id, Is.EqualTo(new LootContextActionId("inventory.split")));
                Assert.That(actions[0].Label, Is.EqualTo("Dividir"));
                Assert.That(actions[0].IsEnabled, Is.EqualTo(enabled));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(holder);
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void Split_IsDisabledWhileTheBoundControllerHasARequestInFlight()
        {
            var holder = new GameObject("Split provider in-flight test");
            var definition = ScriptableObject.CreateInstance<LootDefinition>();
            try
            {
                typeof(LootDefinition).GetField("_id", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(definition, "coin");
                typeof(LootDefinition).GetField("_category", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(definition, LootCategory.Valuable);
                var controller = holder.AddComponent<PlayerLootSplitNetworkController>();
                FieldInfo inFlight = typeof(PlayerLootSplitNetworkController)
                    .GetField("_inFlight", BindingFlags.Instance | BindingFlags.NonPublic);
                inFlight.SetValue(controller, Convert.ChangeType(1, inFlight.FieldType));
                Assert.That(controller.HasRequestInFlight, Is.True);
                var provider = new LootSplitContextActionProvider();
                provider.Bind(controller, () => true);
                var context = new LootContextActionContext(new LootEntry(new LootId("coin"), 5), definition, 0, 0);
                var actions = new List<LootContextActionDescriptor>();

                provider.CollectActions(context, actions);

                Assert.That(actions, Has.Count.EqualTo(1));
                Assert.That(actions[0].IsEnabled, Is.False);
                Assert.That(provider.TryExecute(actions[0].Id, context), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(holder);
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }
    }
}
