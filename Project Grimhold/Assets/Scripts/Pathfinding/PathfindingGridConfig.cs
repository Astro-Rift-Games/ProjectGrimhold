using UnityEngine;

/// <summary>
/// Shared configuration asset for <see cref="PathfindingGrid"/> and
/// <see cref="AStarPathSolver"/>.
///
/// Contains only static values that are safe to share across all enemies.
/// Mutable per-enemy runtime state must not be stored here.
///
/// Recommended values for the current dungeon layout (verified 2026-08-15):
///   NodeSize   = 0.5  (half of the Tilemap cell size of 1 unit)
///   AgentRadius = 0.35 (slightly less than the enemy CircleCollider2D radius of 0.4)
/// </summary>
[CreateAssetMenu(fileName = "PathfindingGridConfig", menuName = "Grimhold/Pathfinding/Grid Config")]
public sealed class PathfindingGridConfig : ScriptableObject
{
    [Header("Grid Resolution")]
    [Tooltip("Distance between adjacent node centres, in world units. " +
             "Recommended: half the Tilemap cell size (e.g. 0.5 for 1-unit tiles).")]
    [SerializeField, Min(0.05f)]
    private float _nodeSize = 0.5f;

    [Header("Agent Footprint")]
    [Tooltip("Agent radius used to inflate obstacle detection during grid build. " +
             "Set slightly below the enemy CircleCollider2D radius so the centre of " +
             "the agent never reaches within this distance of a wall. " +
             "Recommended: 0.35 for the current enemy prefab (collider radius 0.4).")]
    [SerializeField, Min(0f)]
    private float _agentRadius = 0.35f;

    [Header("Path Smoothing")]
    [Tooltip("El radio del CircleCollider2D físico del enemigo actual. Utilizado para validar atajos rectos.")]
    [SerializeField, Min(0f)]
    private float _physicalColliderRadius = 0.4f;

    [Tooltip("Margen de seguridad extra añadido al radio del collider para evitar fricción por errores de precisión en esquinas (Recomendado: 0.02).")]
    [SerializeField, Min(0f)]
    private float _pathSmoothingSafetyMargin = 0.02f;

    [Header("Obstacle Detection")]
    [Tooltip("Layer mask used by OverlapBox during grid construction to classify nodes as obstacles. " +
             "Must match the obstacle layer used by EnemyMovementAIController.")]
    [SerializeField]
    private LayerMask _obstacleLayer;

    [Header("A* Limits")]
    [Tooltip("Maximum number of A* iterations per path request. " +
             "Prevents blocking the simulation tick on very large or unsolvable searches. " +
             "Increase only if valid paths in large dungeons are not found.")]
    [SerializeField, Min(100)]
    private int _maxPathIterations = 8000;

    /// <summary>Distance between adjacent node centres, in world units.</summary>
    public float NodeSize => _nodeSize;

    /// <summary>
    /// Agent radius used to inflate the obstacle detection box during grid build.
    /// The walkable area is effectively shrunk by this value on every side, ensuring
    /// that the agent centre never comes closer than this distance to an obstacle.
    /// </summary>
    public float AgentRadius => _agentRadius;

    /// <summary>Layer mask for obstacle detection during grid construction.</summary>
    public LayerMask ObstacleLayer => _obstacleLayer;

    /// <summary>Maximum A* iterations before aborting a path search.</summary>
    public int MaxPathIterations => _maxPathIterations;

    /// <summary>Radio final utilizado por el CircleCast de SmoothPath (cuando está activado).</summary>
    public float PathSmoothingRadius => _physicalColliderRadius + _pathSmoothingSafetyMargin;
}
