#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using Fusion;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Assert = NUnit.Framework.Assert;

namespace Tests.PlayMode
{
    /// <summary>Composes the runner-scoped capabilities required by gameplay fixtures.</summary>
    internal static class PlayModeRunnerComposition
    {
        private const string RaidMinimapLayoutPath = "Assets/Data/Minimap/DungeonGrayboxMinimapLayout.asset";

        internal static LocalInputContext AddLocalInputContext(NetworkRunner runner)
        {
            Assert.That(runner, Is.Not.Null, "Compose input only after creating the fixture runner.");
            Assert.That(runner.GetComponent<LocalInputContext>(), Is.Null,
                "The fixture must have exactly one owner of its runner-local input context.");

            return runner.gameObject.AddComponent<LocalInputContext>();
        }

        /// <summary>
        /// Declares the Raid map layout the local HUD resolves when it binds. It is parented to the runner
        /// object, so destroying the fixture runner releases it.
        /// </summary>
        internal static RaidSceneMinimap AddRaidSceneMinimap(NetworkRunner runner)
        {
            Assert.That(runner, Is.Not.Null, "Compose the Raid minimap only after creating the fixture runner.");
            var layout = AssetDatabase.LoadAssetAtPath<MinimapLayout>(RaidMinimapLayoutPath);
            Assert.That(layout, Is.Not.Null, RaidMinimapLayoutPath);

            var minimapObject = new GameObject("RaidSceneMinimap", typeof(RaidSceneMinimap));
            minimapObject.transform.SetParent(runner.transform, false);
            var minimap = minimapObject.GetComponent<RaidSceneMinimap>();
            minimap.EditorConfigure(layout, null);
            return minimap;
        }

        /// <summary>
        /// Registers the team pool that avatar contributions forward to. It lives in the runner's
        /// <see cref="EntityRegistry"/>, so destroying the fixture runner releases it.
        /// </summary>
        internal static PlayModeTeamExtractionProgress AddTeamExtractionProgress(NetworkRunner runner)
        {
            Assert.That(runner, Is.Not.Null, "Compose team progress only after creating the fixture runner.");
            EntityRegistry registry = runner.GetComponent<EntityRegistry>();
            Assert.That(registry, Is.Not.Null, "Compose team progress after the fixture's EntityRegistry.");
            Assert.That(registry.TryGetTeamExtractionProgress(out _), Is.False,
                "The fixture runner must have exactly one team extraction progress capability.");

            var progress = new PlayModeTeamExtractionProgress();
            Assert.That(registry.TryRegisterTeamExtractionProgress(progress.Id, progress), Is.True);
            return progress;
        }
    }
}
#endif
