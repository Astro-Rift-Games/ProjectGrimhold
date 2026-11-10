using System.IO;
using NUnit.Framework;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.SceneManagement;
using Assert = NUnit.Framework.Assert;

namespace Tests.EditMode.Loot
{
    public sealed class RaidLootTestSpawnerSecurityTests
    {
        private const string ScriptPath = "Assets/Scripts/Gameplay/Debugging/RaidLootTestSpawner.cs";
        private const string ScenePath = "Assets/Scenes/Gameplay.unity";

        [Test]
        public void RaidLootTestSpawner_IsGuardedByDevelopmentBuildDirectives()
        {
            string fullPath = Path.Combine(Application.dataPath, "..", ScriptPath);
            Assert.That(File.Exists(fullPath), Is.True, $"Script not found at {fullPath}");

            string content = File.ReadAllText(fullPath).Trim();
            Assert.That(content.StartsWith("#if UNITY_EDITOR || DEVELOPMENT_BUILD"), Is.True,
                "RaidLootTestSpawner.cs must start with #if UNITY_EDITOR || DEVELOPMENT_BUILD");
            Assert.That(content.EndsWith("#endif"), Is.True,
                "RaidLootTestSpawner.cs must end with #endif");
        }

        [Test]
        public void RaidLootTestSpawnerBuildProcessor_ImplementsSceneProcessingContract()
        {
            var processor = new RaidLootTestSpawnerBuildProcessor();
            Assert.That(processor, Is.InstanceOf<IProcessSceneWithReport>());
            Assert.That(processor.callbackOrder, Is.EqualTo(0));
        }

        [Test]
        public void StripTestSpawners_RemovesDebugSpawnerGameObjects()
        {
            // Additive creation fails while the runner's untitled scene is unsaved; use a single empty scene.
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
                UnityEditor.SceneManagement.NewSceneMode.Single);
            {
                var testGo = new GameObject("[LootTestSpawner]");
                SceneManager.MoveGameObjectToScene(testGo, scene);

                var normalGo = new GameObject("NormalObject");
                SceneManager.MoveGameObjectToScene(normalGo, scene);

                int stripped = RaidLootTestSpawnerBuildProcessor.StripTestSpawners(scene);
                Assert.That(stripped, Is.EqualTo(1));
                Assert.That(testGo == null || !testGo, Is.True, "[LootTestSpawner] must be destroyed");
                Assert.That(normalGo != null, Is.True, "Normal objects must remain untouched");

                Object.DestroyImmediate(normalGo);
            }
        }

        [Test]
        public void GameplayScene_ContainsLootTestSpawner_InSourceForEditorDev()
        {
            string fullPath = Path.Combine(Application.dataPath, "..", ScenePath);
            Assert.That(File.Exists(fullPath), Is.True, $"Scene not found at {fullPath}");

            string content = File.ReadAllText(fullPath);
            Assert.That(content.Contains("RaidLootTestSpawner"), Is.True,
                "Gameplay.unity must contain RaidLootTestSpawner in development/editor source.");
            Assert.That(content.Contains("[LootTestSpawner]"), Is.True,
                "Gameplay.unity must contain [LootTestSpawner] in development/editor source.");
        }
    }
}
