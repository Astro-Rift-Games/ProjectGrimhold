using Fusion;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Assert = NUnit.Framework.Assert;

namespace Tests.EditMode.Loot
{
    public sealed class RaidLootOriginWordCountTests
    {
        // Player's old exact 1884-word layout included the removed packed Inventory.
        // Report the actual newly woven layout; retain the approved upper bound, not a guessed total.
        // TASK-283 S18: container, enemy and pickup ceilings are the measured sizes with weapon instance
        // modifiers (receiver array, hand words, 34 container weapon units, pickup word).
        [TestCase("Assets/Prefabs/NetworkPlayer.prefab", 1289, null, 1884)]
        [TestCase("Assets/Prefabs/LootContainer.prefab", 820, 1230, 1230)]
        [TestCase("Assets/Prefabs/Enemies/NetworkEnemy.prefab", 844, 1292, 1292)]
        [TestCase("Assets/Prefabs/LootPickup.prefab", 6, 24, 192)]
        public void ProductivePrefab_UsesMeasuredCompactBudget(
            string prefabPath,
            int baselineWords,
            int? expectedFinalWords,
            int maximumWords)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefab, Is.Not.Null, prefabPath);
            NetworkObject networkObject = prefab.GetComponent<NetworkObject>();
            Assert.That(networkObject, Is.Not.Null, prefabPath);

            int actualWords = 0;
            for (int index = 0; index < networkObject.NetworkedBehaviours.Length; index++)
            {
                NetworkBehaviour behaviour = networkObject.NetworkedBehaviours[index];
                Assert.That(behaviour, Is.Not.Null, $"{prefabPath} behaviour {index}");
                // The approved TASK-133.1 baselines measure baked gameplay behaviours and
                // exclude Fusion's invariant 21-word NetworkTransform/TRSP contribution.
                if (behaviour is NetworkTransform)
                {
                    continue;
                }
                int words = NetworkBehaviourUtils.GetWordCount(behaviour);
                actualWords += words;
                if (behaviour is PlayerLootReceiver || behaviour is PlayerRaidLootOriginState)
                    TestContext.Out.WriteLine($"TASK-227 {behaviour.GetType().Name}: {words} woven words");
            }
            TestContext.Out.WriteLine(
                $"TASK-133.1 {prefabPath}: baseline={baselineWords}, final={actualWords}, delta={actualWords - baselineWords}");
            if (expectedFinalWords.HasValue)
                Assert.That(actualWords, Is.EqualTo(expectedFinalWords.Value), prefabPath);
            else
                Assert.That(actualWords, Is.LessThan(1884), "Physical Inventory must remove the old parallel packed-state cost.");
            Assert.That(actualWords, Is.GreaterThan(0), prefabPath);
            Assert.That(actualWords, Is.LessThanOrEqualTo(maximumWords), prefabPath);
        }

        [Test]
        public void IsolatedCandidates_ReportExactWovenWordCounts()
        {
            int directEndpoint = MeasureCandidateWords<RaidLootOriginDirectEndpointWeaverCandidate>();
            int directPlayer = MeasureCandidateWords<RaidLootOriginDirectPlayerWeaverCandidate>();
            int directPickup = MeasureCandidateWords<RaidLootOriginDirectPickupWeaverCandidate>();
            int packedEndpoint = MeasureCandidateWords<ContainerRaidLootOriginState>();
            int packedPlayer = MeasureCandidateWords<PlayerRaidLootOriginState>();
            int tableEndpoint = MeasureCandidateWords<RaidParticipantIdTableEndpointWeaverCandidate>();
            int tablePlayer = MeasureCandidateWords<RaidParticipantIdTablePlayerWeaverCandidate>();
            int tablePickup = MeasureCandidateWords<RaidParticipantIdTablePickupWeaverCandidate>();

            Assert.That(directEndpoint, Is.GreaterThan(packedEndpoint));
            Assert.That(directPlayer, Is.GreaterThan(packedPlayer));
            Assert.That(directPickup, Is.GreaterThan(16));
            Assert.That(tableEndpoint, Is.GreaterThan(packedEndpoint));
            Assert.That(tablePlayer, Is.GreaterThan(packedPlayer));
            Assert.That(tablePickup, Is.GreaterThan(16));
        }

        [Test]
        public void WeaponInstanceTransport_ReportsPackedStrideAndCurrentPrefabSizes()
        {
            // Current measurements only: productive ceilings change after component integration.
            int modifiers = NetworkStructUtils.GetWordCount<WeaponInstanceModifiersNetworkData>();
            int inventorySlot = NetworkStructUtils.GetWordCount<LootInventorySlot>();
            Assert.That(modifiers, Is.EqualTo(1));
            Assert.That(inventorySlot, Is.EqualTo(2));
            TestContext.Out.WriteLine($"F5 modifiers per unit: {modifiers} words");
            TestContext.Out.WriteLine($"F5 receiver parallel array (30): +{30 * modifiers} words");
            TestContext.Out.WriteLine($"F5 four equipment hands: +{4 * modifiers} words");
            TestContext.Out.WriteLine($"F5 pickup: +{modifiers} words");

            // Candidate separate weapon storage: catalog/origin key, eligibility flag, modifiers.
            // Amount is implicitly one. Existing non-weapon aggregates/provenance are retained.
            int weaponUnit = 2 + modifiers;
            foreach (int capacity in new[] { 6, 34 })
                TestContext.Out.WriteLine(
                    $"F5 separate weapon array ({capacity}): +{capacity * weaponUnit} words " +
                    $"({weaponUnit} per unit), before any additional lifecycle fields");

            foreach (string prefabPath in new[]
            {
                "Assets/Prefabs/NetworkPlayer.prefab",
                "Assets/Prefabs/LootContainer.prefab",
                "Assets/Prefabs/Enemies/NetworkEnemy.prefab",
                "Assets/Prefabs/LootPickup.prefab"
            })
            {
                NetworkObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath)
                    .GetComponent<NetworkObject>();
                int total = 0;
                foreach (NetworkBehaviour behaviour in prefab.NetworkedBehaviours)
                {
                    if (behaviour is NetworkTransform)
                        continue;
                    int words = NetworkBehaviourUtils.GetWordCount(behaviour);
                    total += words;
                    if (behaviour is PlayerLootReceiver ||
                        behaviour is PlayerWeaponEquipmentNetworkController ||
                        behaviour is NetworkLootContainer ||
                        behaviour is ContainerRaidLootOriginState)
                        TestContext.Out.WriteLine($"F5 {prefabPath} {behaviour.GetType().Name}: {words} words");
                }
                TestContext.Out.WriteLine($"F5 {prefabPath} current total: {total} words (excluding TRSP)");
            }
        }

        private static void AssertCandidateWords<T>(int expectedWords) where T : NetworkBehaviour
        {
            var gameObject = new GameObject(typeof(T).Name);
            try
            {
                T behaviour = gameObject.AddComponent<T>();
                int actualWords = NetworkBehaviourUtils.GetWordCount(behaviour);
                TestContext.Out.WriteLine($"TASK-133.1 {typeof(T).Name}: {actualWords} words");
                Assert.That(actualWords, Is.EqualTo(expectedWords), typeof(T).Name);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        private static int MeasureCandidateWords<T>() where T : NetworkBehaviour
        {
            var gameObject = new GameObject(typeof(T).Name);
            try
            {
                T behaviour = gameObject.AddComponent<T>();
                int words = NetworkBehaviourUtils.GetWordCount(behaviour);
                TestContext.Out.WriteLine($"TASK-133.1 {typeof(T).Name}: {words} words");
                return words;
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }
    }
}
