using System.Linq;
using Fusion;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Assert = NUnit.Framework.Assert;

namespace Tests.EditMode.Scenario
{
    public sealed class ExtractionSanctuaryPrefabTests
    {
        private const string PrefabPath = "Assets/Prefabs/ExtractionSanctuary.prefab";
        private const string ScenePath = "Assets/Scenes/Gameplay.unity";

        [Test]
        public void Prefab_HasNetworkObjectSanctuaryAndProvisionalVisual()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent<NetworkObject>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<ExtractionSanctuary>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<SpriteRenderer>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<IInteractable>(), Is.Null);
        }

        [Test]
        public void Gameplay_ContainsExactlyFourDistinctSanctuaryInstances()
        {
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                ExtractionSanctuary[] sanctuaries = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<ExtractionSanctuary>(true))
                    .ToArray();

                Assert.That(sanctuaries, Has.Length.EqualTo(4));
                Assert.That(sanctuaries.Select(item => item.gameObject.name).Distinct().Count(), Is.EqualTo(4));
                Assert.That(sanctuaries.All(item => item.GetComponent<NetworkObject>() != null), Is.True);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded)
                {
                    SceneManager.SetActiveScene(previous);
                }
            }
        }
    }
}
