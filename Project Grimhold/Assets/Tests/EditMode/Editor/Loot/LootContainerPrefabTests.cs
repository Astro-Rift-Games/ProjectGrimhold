using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using NetworkBehaviour = Fusion.NetworkBehaviour;
using NetworkObject = Fusion.NetworkObject;
using NetworkTransform = Fusion.NetworkTransform;

namespace Tests.EditMode.Loot
{
    public sealed class LootContainerPrefabTests
    {
        [TestCase("Assets/Prefabs/LootContainer.prefab")]
        [TestCase("Assets/Prefabs/Enemies/NetworkEnemy.prefab")]
        [TestCase("Assets/Prefabs/Enemies/NetworkEnemyRanged.prefab")]
        [TestCase("Assets/Prefabs/Enemies/Slimes/BlueSlime.prefab")]
        [TestCase("Assets/Prefabs/Enemies/Slimes/GreenSlime.prefab")]
        [TestCase("Assets/Prefabs/Enemies/Slimes/RedSlime.prefab")]
        public void ProductiveRaidContainerCapacity_IsAtMostSixteen(string prefabPath)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefab, Is.Not.Null, prefabPath);
            NetworkLootContainer container = prefab.GetComponent<NetworkLootContainer>();
            ContainerRaidLootOriginState origins = prefab.GetComponent<ContainerRaidLootOriginState>();
            NetworkObject networkObject = prefab.GetComponent<NetworkObject>();
            Assert.That(container, Is.Not.Null, prefabPath);
            Assert.That(origins, Is.Not.Null, prefabPath);
            Assert.That(networkObject, Is.Not.Null, prefabPath);
            Assert.That(container.SlotCapacity, Is.InRange(1, NetworkLootContainer.MaxDistinctLootTypes), prefabPath);
            AssertNetworkBehaviourIsBaked(networkObject, origins);
        }

        [TestCase("Assets/Prefabs/NetworkPlayer.prefab")]
        [TestCase("Assets/Prefabs/NetworkPlayerMelee.prefab")]
        [TestCase("Assets/Prefabs/NetworkPlayerRanged.prefab")]
        [TestCase("Assets/Prefabs/SocialPlayer.prefab")]
        public void ProductivePlayerReceiverCapacity_IsAtMostSixteen(string prefabPath)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefab, Is.Not.Null, prefabPath);
            PlayerLootReceiver receiver = prefab.GetComponent<PlayerLootReceiver>();
            Assert.That(receiver, Is.Not.Null, prefabPath);
            Assert.That(receiver.SlotCapacity, Is.InRange(1, PlayerLootReceiver.MaxDistinctLootTypes), prefabPath);
        }

        [Test]
        public void ContainerPrefab_HasRequiredProductionComponentsAndLayer()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/LootContainer.prefab");

            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.layer, Is.EqualTo(8));
            Assert.That(prefab.GetComponent<NetworkObject>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<NetworkTransform>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<NetworkLootContainer>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<ContainerRaidLootOriginState>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<NetworkLootContainerInteractable>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<LootContainerRandomContentConfig>(), Is.Not.Null);
        }

        [Test]
        public void ProductiveContainerTable_ContainsCataloguedSingleUnitWeapon()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/LootContainer.prefab");

            Assert.That(prefab, Is.Not.Null);
            NetworkLootContainer container = prefab.GetComponent<NetworkLootContainer>();
            LootContainerContentTable table = prefab.GetComponent<LootContainerRandomContentConfig>().Table;
            Assert.That(container, Is.Not.Null);
            Assert.That(table, Is.Not.Null);

            LootContainerContentTableEntry[] weaponEntries = table.Entries
                .Where(entry => entry.Definition != null && entry.Definition.Category == LootCategory.Weapon)
                .ToArray();

            Assert.That(weaponEntries, Has.Length.EqualTo(1));
            LootContainerContentTableEntry weaponEntry = weaponEntries[0];
            LootDefinition weapon = weaponEntry.Definition;
            Assert.That(weapon.TryValidate(out string validationError), Is.True, validationError);
            Assert.That(weapon.Icon, Is.Not.Null);
            Assert.That(weapon.DefaultPickupQuantity, Is.EqualTo(1));
            Assert.That(weaponEntry.Weight, Is.GreaterThan(0));
            Assert.That(weaponEntry.MinimumAmount, Is.EqualTo(1));
            Assert.That(weaponEntry.MaximumAmount, Is.EqualTo(1));
            Assert.That(container.LootCatalog.TryGet(weapon.Id, out LootDefinition cataloguedWeapon), Is.True);
            Assert.That(cataloguedWeapon, Is.SameAs(weapon));
            Assert.That(
                LootContainerContentTableValidation.TryCreateSnapshot(
                    table,
                    container.LootCatalog,
                    container.SlotCapacity,
                    NetworkLootContainer.MaxDistinctLootTypes,
                    out ValidatedLootContainerContentSnapshot snapshot,
                    out string snapshotError),
                Is.True,
                snapshotError);
            Assert.That(
                Enumerable.Range(0, snapshot.EntryCount).Select(snapshot.GetEntry),
                Has.Some.Matches<ValidatedLootContainerContentSnapshot.Entry>(entry =>
                    entry.LootId == weapon.LootId && entry.MinimumAmount == 1 && entry.MaximumAmount == 1));

            RaidInventorySlotData slotData = RaidInventorySlotData.Create(
                new LootEntry(weapon.LootId, 1),
                weapon,
                null);
            Assert.That(slotData.DisplayName, Is.EqualTo(weapon.DisplayName));
            Assert.That(slotData.Icon, Is.SameAs(weapon.Icon));
            Assert.That(slotData.Amount, Is.EqualTo(1));
        }

        [Test]
        public void EnemyPrefab_PersistsAsItsOwnInitiallyUnavailableLootContainer()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/NetworkEnemy.prefab");

            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponentsInChildren<NetworkObject>(true), Has.Length.EqualTo(1));
            Assert.That(prefab.GetComponent<EnemyCharacter>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<NetworkLootContainer>().StartsAvailable, Is.False);
            Assert.That(prefab.GetComponent<NetworkLootContainerInteractable>(), Is.Not.Null);
        }

        [TestCase("Assets/Prefabs/NetworkPlayer.prefab")]
        [TestCase("Assets/Prefabs/NetworkPlayerMelee.prefab")]
        [TestCase("Assets/Prefabs/NetworkPlayerRanged.prefab")]
        public void PlayerPrefab_ComposesOneUnavailableLootEndpointOnItsNetworkRoot(string prefabPath)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponentsInChildren<NetworkObject>(true), Has.Length.EqualTo(1));
            NetworkObject networkObject = prefab.GetComponent<NetworkObject>();
            PlayerCharacter character = prefab.GetComponent<PlayerCharacter>();
            PlayerLootReceiver receiver = prefab.GetComponent<PlayerLootReceiver>();
            PlayerCorpseGenerationController generationController =
                prefab.GetComponent<PlayerCorpseGenerationController>();
            NetworkLootContainer container = prefab.GetComponent<NetworkLootContainer>();
            NetworkLootContainerInteractable interactable = prefab.GetComponent<NetworkLootContainerInteractable>();
            PlayerWeaponEquipmentNetworkController equipment =
                prefab.GetComponent<PlayerWeaponEquipmentNetworkController>();
            PlayerRaidLootOriginState playerOrigins = prefab.GetComponent<PlayerRaidLootOriginState>();
            ContainerRaidLootOriginState corpseOrigins = prefab.GetComponent<ContainerRaidLootOriginState>();

            Assert.That(networkObject, Is.Not.Null);
            Assert.That(character, Is.Not.Null);
            Assert.That(receiver, Is.Not.Null);
            Assert.That(generationController, Is.Not.Null);
            Assert.That(container, Is.Not.Null);
            Assert.That(interactable, Is.Not.Null);
            Assert.That(equipment, Is.Not.Null);
            Assert.That(playerOrigins, Is.Not.Null);
            Assert.That(corpseOrigins, Is.Not.Null);
            Assert.That(character.gameObject, Is.SameAs(networkObject.gameObject));
            Assert.That(receiver.gameObject, Is.SameAs(networkObject.gameObject));
            Assert.That(generationController.gameObject, Is.SameAs(networkObject.gameObject));
            Assert.That(container.gameObject, Is.SameAs(networkObject.gameObject));
            Assert.That(interactable.gameObject, Is.SameAs(networkObject.gameObject));

            Assert.That(container.StartsAvailable, Is.False);
            var serializedContainer = new SerializedObject(container);
            Assert.That(serializedContainer.FindProperty("_initialContent").arraySize, Is.Zero);

            InteractionPromptMetadata metadata = prefab.GetComponent<InteractionPromptMetadata>();
            Assert.That(metadata, Is.Not.Null);
            Assert.That(string.IsNullOrWhiteSpace(metadata.PromptText), Is.False);
            Assert.That(prefab.GetComponent<LootContainerRandomContentConfig>(), Is.Null);
            Assert.That(typeof(ILootReceiver).IsAssignableFrom(typeof(NetworkLootContainer)), Is.True);

            SerializedProperty interactionColliders = serializedContainer.FindProperty("_interactionColliders");
            Assert.That(interactionColliders.arraySize, Is.GreaterThan(0));
            for (int index = 0; index < interactionColliders.arraySize; index++)
            {
                Collider2D collider =
                    interactionColliders.GetArrayElementAtIndex(index).objectReferenceValue as Collider2D;
                Assert.That(collider, Is.Not.Null);
                Assert.That(collider.isTrigger, Is.True);
                Assert.That(collider.gameObject.layer, Is.EqualTo(8));
            }

            AssertNetworkBehaviourIsBaked(networkObject, character);
            AssertNetworkBehaviourIsBaked(networkObject, receiver);
            AssertNetworkBehaviourIsBaked(networkObject, generationController);
            AssertNetworkBehaviourIsBaked(networkObject, container);
            AssertNetworkBehaviourIsBaked(networkObject, interactable);
            AssertNetworkBehaviourIsBaked(networkObject, equipment);
            AssertNetworkBehaviourIsBaked(networkObject, playerOrigins);
            AssertNetworkBehaviourIsBaked(networkObject, corpseOrigins);
        }

        [Test]
        public void PlayerPrefabs_DoNotReferenceASeparateCorpsePrefab()
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PlayerCorpse.prefab"), Is.Null);
        }

        [Test]
        public void DebugHarnessPrefab_IsSeparateAndHasNoNetworkGameplayEndpoint()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Debug/LootContainerTransferDebugHarness.prefab");

            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent<NetworkObject>(), Is.Null);
            Assert.That(prefab.GetComponent<PlayerLootReceiver>(), Is.Null);
        }

        private static void AssertNetworkBehaviourIsBaked(
            NetworkObject networkObject,
            NetworkBehaviour expected)
        {
            Assert.That(networkObject.NetworkedBehaviours, Does.Contain(expected));
        }
    }
}
