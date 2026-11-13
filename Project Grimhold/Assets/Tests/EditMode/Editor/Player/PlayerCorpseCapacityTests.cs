using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Player
{
    public sealed class PlayerCorpseCapacityTests
    {
        private const string PlayerPrefabPath = "Assets/Prefabs/NetworkPlayer.prefab";
        private const int RequiredCorpseCapacity = 38;

        [Test]
        public void NetworkLootContainer_DistinctLootCapacity_IsAtLeastThirtyEight()
        {
            Assert.That(NetworkLootContainer.MaxDistinctLootTypes, Is.GreaterThanOrEqualTo(RequiredCorpseCapacity));
            Assert.That(RaidLootOriginPackedBuffer.MaximumStacks, Is.GreaterThanOrEqualTo(RequiredCorpseCapacity));
        }

        [Test]
        public void NetworkPlayerPrefab_CorpseContainer_HasThirtyEightSlotCapacity()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            Assert.That(prefab, Is.Not.Null, PlayerPrefabPath);

            NetworkLootContainer corpseContainer = prefab.GetComponent<NetworkLootContainer>();
            Assert.That(corpseContainer, Is.Not.Null, "NetworkPlayer must have NetworkLootContainer.");
            Assert.That(corpseContainer.SlotCapacity, Is.EqualTo(RequiredCorpseCapacity));
            Assert.That(corpseContainer.SupportsFullPlayerInventory, Is.True);

            PlayerLootReceiver playerReceiver = prefab.GetComponent<PlayerLootReceiver>();
            Assert.That(playerReceiver, Is.Not.Null, "NetworkPlayer must have PlayerLootReceiver.");
            Assert.That(playerReceiver.SlotCapacity, Is.EqualTo(LootInventoryRules.PhysicalSlotCount));
        }

        [Test]
        public void LootContainerInitializationRules_AcceptsThirtyEightDistinctEntries()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<LootDefinitionCatalog>(
                "Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset");
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.DefinitionCount, Is.GreaterThanOrEqualTo(RequiredCorpseCapacity));

            var entries = new List<LootEntry>(RequiredCorpseCapacity);
            for (int i = 0; i < RequiredCorpseCapacity; i++)
            {
                Assert.That(catalog.TryGetByIndex(i, out LootDefinition def), Is.True);
                entries.Add(new LootEntry(def.LootId, 1));
            }

            bool built = LootContainerInitializationRules.TryBuild(
                entries,
                catalog,
                RequiredCorpseCapacity,
                NetworkLootContainer.MaxDistinctLootTypes,
                out IReadOnlyList<KeyValuePair<int, int>> resolvedEntries,
                out string error);

            Assert.That(built, Is.True, error);
            Assert.That(resolvedEntries.Count, Is.EqualTo(RequiredCorpseCapacity));
        }

        [Test]
        public void ContainerLootPanelPrefab_HasAtLeastThirtyEightAuthoredSlots()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/UI/PlayerUI/ContainerLootPanel.prefab");
            Assert.That(prefab, Is.Not.Null);

            RaidLootPanelView panelView = prefab.GetComponent<RaidLootPanelView>();
            Assert.That(panelView, Is.Not.Null);
            Assert.That(panelView.AuthoredSlotCount, Is.GreaterThanOrEqualTo(RequiredCorpseCapacity));
        }
    }
}
