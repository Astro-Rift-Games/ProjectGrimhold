using System.Collections.Generic;
using NUnit.Framework;
using ProjectGrimhold.Gameplay.Visibility;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Visibility
{
    /// <summary>
    /// Every renderer of a vision-hidden entity must be hidden together, or parts
    /// (weapons, particles, armor) would leak through walls.
    /// </summary>
    public sealed class VisibilityPrefabCoverageTests
    {
        private static readonly string[] HiddenEntityPrefabs =
        {
            "Assets/Prefabs/NetworkPlayer.prefab",
            "Assets/Prefabs/Enemies/NetworkEnemy.prefab",
            "Assets/Prefabs/LootContainer.prefab",
            "Assets/Prefabs/LootPickup.prefab",
            "Assets/Prefabs/LootPickup 1.prefab",
            "Assets/Prefabs/BreakableObject.prefab",
            "Assets/Prefabs/Traps/SpikesTrap.prefab",
            "Assets/Prefabs/Traps/DartTrap.prefab",
            "Assets/Prefabs/PlaceholderProjectile.prefab",
            "Assets/Prefabs/Abilities/NetworkTrap.prefab",
            "Assets/Prefabs/Abilities/ProtectiveOrb.prefab",
            "Assets/Prefabs/ExtractionSanctuary.prefab"
        };

        private static readonly string[] OcclusionOnlyPrefabs =
        {
            "Assets/Prefabs/LootContainer.prefab",
            "Assets/Prefabs/LootPickup.prefab",
            "Assets/Prefabs/LootPickup 1.prefab",
            "Assets/Prefabs/BreakableObject.prefab",
            "Assets/Prefabs/Traps/SpikesTrap.prefab",
            "Assets/Prefabs/Traps/DartTrap.prefab",
            "Assets/Prefabs/Abilities/NetworkTrap.prefab",
            "Assets/Prefabs/ExtractionSanctuary.prefab"
        };

        [TestCaseSource(nameof(HiddenEntityPrefabs))]
        public void StaticObjectsIgnoreFacing_DynamicEntitiesRequireTheCone(string prefabPath)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            EntityVisibilityPresenter presenter = prefab.GetComponentInChildren<EntityVisibilityPresenter>(true);
            bool occlusionOnly = new SerializedObject(presenter).FindProperty("_occlusionOnly").boolValue;

            Assert.That(
                occlusionOnly,
                Is.EqualTo(System.Array.IndexOf(OcclusionOnlyPrefabs, prefabPath) >= 0),
                prefabPath);
        }

        [TestCaseSource(nameof(HiddenEntityPrefabs))]
        public void Presenter_ListsEveryRendererOfTheEntity(string prefabPath)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefab, Is.Not.Null, prefabPath);

            EntityVisibilityPresenter presenter = prefab.GetComponentInChildren<EntityVisibilityPresenter>(true);
            Assert.That(presenter, Is.Not.Null, prefabPath);

            SerializedProperty renderers = new SerializedObject(presenter).FindProperty("_renderers");
            var listed = new HashSet<Object>();
            for (int index = 0; index < renderers.arraySize; index++)
            {
                listed.Add(renderers.GetArrayElementAtIndex(index).objectReferenceValue);
            }

            foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
            {
                Assert.That(listed.Contains(renderer), Is.True, $"{prefabPath}: {renderer.name} is not hidden by vision.");
            }
        }
    }
}
