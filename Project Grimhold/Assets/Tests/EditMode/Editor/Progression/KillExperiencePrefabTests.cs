using Fusion;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Assert = NUnit.Framework.Assert;

namespace Tests.EditMode.Progression
{
    public sealed class KillExperiencePrefabTests
    {
        [TestCase("Assets/Prefabs/Enemies/NetworkEnemy.prefab", 0L)]
        [TestCase("Assets/Prefabs/Enemies/Slimes/BlueSlime.prefab", 10L)]
        [TestCase("Assets/Prefabs/Enemies/Slimes/GreenSlime.prefab", 10L)]
        [TestCase("Assets/Prefabs/Enemies/Slimes/RedSlime.prefab", 10L)]
        [TestCase("Assets/Prefabs/Enemies/NetworkEnemyRanged.prefab", 15L)]
        public void EnemyPrefab_HasOneNetworkedKillSourceWithIndependentValue(
            string path,
            long expectedExperience)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, path);

            KillExperienceSource[] sources = prefab.GetComponents<KillExperienceSource>();
            ExtractionProgressDefeatSource extractionSource =
                prefab.GetComponent<ExtractionProgressDefeatSource>();
            NetworkObject networkObject = prefab.GetComponent<NetworkObject>();

            Assert.That(sources, Has.Length.EqualTo(1), path);
            Assert.That(sources[0].KillExperience, Is.EqualTo(expectedExperience), path);
            Assert.That(networkObject.NetworkedBehaviours, Does.Contain(sources[0]), path);
            Assert.That(extractionSource, Is.Not.Null, path);
        }

        [Test]
        public void PlayerPrefab_HasNoKillExperienceSource()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/NetworkPlayer.prefab");

            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent<KillExperienceSource>(), Is.Null);
        }
    }
}
