using Fusion;
using UnityEngine;

namespace Spawning
{
    /// <summary>
    /// Resolves metadata and spawn candidate representation for enemy prefabs,
    /// inspecting EnemySpawnMetadata if authored, or automatically inferring
    /// archetypes (such as Catacombs Specter) from attached components.
    /// </summary>
    public static class EnemySpawnCandidateResolver
    {
        public static EnemySpawnCandidate<NetworkPrefabRef> ResolveCandidate(
            NetworkRunner runner,
            NetworkPrefabRef prefabRef)
        {
            if (!prefabRef.IsValid)
            {
                return new EnemySpawnCandidate<NetworkPrefabRef>(prefabRef, "unknown", 0, 1);
            }

            NetworkObject prefabObject = null;
            if (runner != null && runner.Config != null && runner.Config.PrefabTable != null)
            {
                try
                {
                    var id = runner.Config.PrefabTable.GetId((NetworkObjectGuid)prefabRef);
                    prefabObject = runner.Config.PrefabTable.Load(id, true);
                }
                catch
                {
                    // Fallback if runner table load fails
                }
            }

            if (prefabObject != null)
            {
                return ResolveCandidateFromGameObject(prefabRef, prefabObject.gameObject);
            }

            // Default fallback candidate
            return new EnemySpawnCandidate<NetworkPrefabRef>(
                prefabRef,
                "enemy",
                weight: 45,
                threatCost: EnemyThreatCost.DefaultCost);
        }

        public static EnemySpawnCandidate<T> ResolveCandidateFromGameObject<T>(
            T item,
            GameObject gameObject)
        {
            if (gameObject == null)
            {
                return new EnemySpawnCandidate<T>(item, "unknown", 0, 1);
            }

            if (gameObject.TryGetComponent<EnemySpawnMetadata>(out var metadata))
            {
                return new EnemySpawnCandidate<T>(
                    item,
                    metadata.EnemyId,
                    metadata.SpawnWeight,
                    metadata.ThreatCost,
                    metadata.RequiredEnvironment);
            }

            if (gameObject.TryGetComponent<ProjectGrimhold.Gameplay.Enemies.Specter.SpecterAIController>(out _) ||
                (!string.IsNullOrEmpty(gameObject.name) && gameObject.name.ToLowerInvariant().Contains("specter")))
            {
                return new EnemySpawnCandidate<T>(
                    item,
                    "specter",
                    weight: 10,
                    threatCost: EnemyThreatCost.SpecterCost,
                    requiredEnvironment: DungeonEnvironmentResolver.Catacombs);
            }

            if (gameObject.TryGetComponent<EnemyMissionProgressDefeatSource>(out var defeatSource) &&
                !string.IsNullOrEmpty(defeatSource.TargetId))
            {
                return new EnemySpawnCandidate<T>(
                    item,
                    defeatSource.TargetId,
                    weight: 45,
                    threatCost: EnemyThreatCost.DefaultCost,
                    requiredEnvironment: defeatSource.ZoneId);
            }

            return new EnemySpawnCandidate<T>(
                item,
                "enemy",
                weight: 45,
                threatCost: EnemyThreatCost.DefaultCost);
        }
    }
}
