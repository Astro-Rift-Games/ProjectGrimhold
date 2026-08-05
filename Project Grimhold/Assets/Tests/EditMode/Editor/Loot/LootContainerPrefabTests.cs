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
        [Test]
        public void ContainerPrefab_HasRequiredProductionComponentsAndLayer()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/LootContainer.prefab");

            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.layer, Is.EqualTo(8));
            Assert.That(prefab.GetComponent<NetworkObject>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<NetworkTransform>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<NetworkLootContainer>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<NetworkLootContainerInteractable>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<LootContainerRandomContentConfig>(), Is.Not.Null);
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

            Assert.That(networkObject, Is.Not.Null);
            Assert.That(character, Is.Not.Null);
            Assert.That(receiver, Is.Not.Null);
            Assert.That(generationController, Is.Not.Null);
            Assert.That(container, Is.Not.Null);
            Assert.That(interactable, Is.Not.Null);
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
