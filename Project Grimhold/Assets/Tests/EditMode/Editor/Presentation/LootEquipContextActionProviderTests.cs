#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;

namespace Tests.EditMode.Presentation
{
    public sealed class LootEquipContextActionProviderTests
    {
        [Test]
        public void ValidWeapon_AddsEquipActionThroughExistingProviderContract()
        {
            LootDefinition weapon = AssetDatabase.LoadAssetAtPath<LootDefinition>(
                "Assets/Scriptable Objects/Loot/Definitions/TrainingSword.asset");
            Assert.That(weapon, Is.Not.Null);
            Assert.That(weapon.TryValidate(out string error), Is.True, error);

            var provider = new LootEquipContextActionProvider();
            var actions = new List<LootContextActionDescriptor>();
            LootEntry entry = new LootEntry(weapon.LootId, 2);

            provider.CollectActions(new LootContextActionContext(entry, weapon), actions);

            Assert.That(actions, Has.Count.EqualTo(1));
            Assert.That(actions[0].Id, Is.EqualTo(LootEquipContextActionProvider.EquipId));
            Assert.That(actions[0].Label, Is.EqualTo("Equipar"));
        }

        [Test]
        public void NonWeapon_DoesNotAddEquipAction()
        {
            LootDefinition nonWeapon = AssetDatabase.LoadAssetAtPath<LootDefinition>(
                "Assets/Scriptable Objects/Loot/Definitions/Bone.asset");
            Assert.That(nonWeapon, Is.Not.Null);

            var provider = new LootEquipContextActionProvider();
            var actions = new List<LootContextActionDescriptor>();
            LootEntry entry = new LootEntry(nonWeapon.LootId, 1);

            provider.CollectActions(new LootContextActionContext(entry, nonWeapon), actions);

            Assert.That(actions, Is.Empty);
        }
    }
}
#endif
