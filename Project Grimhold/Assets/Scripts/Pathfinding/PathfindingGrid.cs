using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Builds and exposes a 2D grid of <see cref="PathNode"/> values that represent
/// the navigable world for enemy pathfinding.
///
/// This component belongs to the Scene layer. It is not a singleton; references
/// are obtained explicitly via <c>Runner.GetComponent&lt;PathfindingGrid&gt;()</c>
/// in <see cref="EnemyPathfindingNavigator.Spawned"/>.
///
/// The grid is constructed once during the raid bootstrap sequence by
/// <see cref="NetworkSpawnManager"/> on the Host only. Clients never build the
/// grid because proxies do not execute pathfinding.
///
/// Walkability is determined by <c>Physics2D.OverlapBox</c> with an area
/// inflated by <see cref="PathfindingGridConfig.AgentRadius"/>, which acts as a
/// conservative Minkowski erosion of the free space. This ensures the agent
/// centre never comes closer than AgentRadius to an obstacle surface.
///
/// The diagonal corner-cutting rule is enforced here so that
/// <see cref="AStarPathSolver"/> does not generate paths that require the agent
/// to squeeze through two touching wall corners.
/// </summary>
[DisallowMultipleComponent]
public sealed class PathfindingGrid : MonoBehaviour
{
    [SerializeField]
    private PathfindingGridConfig _config;

    // Flat row-major array: index = y * _width + x.
    private PathNode[] _nodes;
    private int _width;
    private int _height;
    private Vector2 _worldBottomLeft;
    private bool _isBuilt;

    // ── Gizmo toggle (Editor only) ────────────────────────────────────────────
#if UNITY_EDITOR
    [Header("Debug")]
    [SerializeField]
    private bool _showGizmos;
#endif

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Whether the grid has been built and is ready for queries.</summary>
    public bool IsBuilt => _isBuilt;

    /// <summary>Number of columns in the grid.</summary>
    public int Width => _width;

    /// <summary>Number of rows in the grid.</summary>
    public int Height => _height;

    /// <summary>
    /// Constructs the walkability grid by scanning all active Tilemaps in the
    /// same scene and running <c>Physics2D.OverlapBox</c> for each node.
    ///
    /// <para>Call <c>Physics2D.SyncTransforms()</c> before invoking this method
    /// to ensure all Tilemap colliders (including CompositeCollider2D) have been
    /// fully generated.</para>
    ///
    /// <para>This method performs a large number of physics queries and is
    /// intended to run once during the raid bootstrap sequence, not during
    /// gameplay simulation ticks.</para>
    /// </summary>
    public void Build()
    {
        if (_config == null)
        {
            Debug.LogError(
                $"{nameof(PathfindingGrid)} cannot build: {nameof(PathfindingGridConfig)} is not assigned.",
                this);
            return;
        }

        Bounds bounds = CalculateSceneTilemapBounds();
        if (bounds.size.sqrMagnitude < 0.001f)
        {
            Debug.LogError(
                $"{nameof(PathfindingGrid)} cannot build: no active Tilemaps found in the scene " +
                "or their bounds are empty.",
                this);
            return;
        }

        BuildFromBounds(bounds);
    }

    /// <summary>
    /// Returns the <see cref="PathNode"/> at the given grid coordinates.
    /// The caller must verify bounds before calling this method.
    /// </summary>
    public PathNode GetNode(int x, int y)
    {
        return _nodes[y * _width + x];
    }

    /// <summary>
    /// Writes updated A* working data back into the flat node array.
    /// Only <see cref="AStarPathSolver"/> should call this during a search.
    /// </summary>
    internal void SetNode(int x, int y, in PathNode node)
    {
        _nodes[y * _width + x] = node;
    }

    /// <summary>Returns the world-space centre position of the node at (x, y).</summary>
    public Vector2 GetWorldPosition(int x, int y)
    {
        return _worldBottomLeft + new Vector2(x * _config.NodeSize, y * _config.NodeSize);
    }

    /// <summary>
    /// Converts a world-space position to the closest grid node.
    /// Returns <see langword="false"/> when the position is outside the grid bounds.
    /// </summary>
    public bool TryGetNodeFromWorldPoint(Vector2 worldPos, out int x, out int y)
    {
        float relX = worldPos.x - _worldBottomLeft.x;
        float relY = worldPos.y - _worldBottomLeft.y;
        x = Mathf.RoundToInt(relX / _config.NodeSize);
        y = Mathf.RoundToInt(relY / _config.NodeSize);
        return IsInBounds(x, y);
    }

    /// <summary>
    /// Finds the nearest walkable node to the given world position.
    /// Returns <see langword="false"/> if the grid is not built or contains no
    /// walkable nodes (degenerate case).
    /// </summary>
    public bool TryGetNearestWalkableNode(Vector2 worldPos, out int x, out int y)
    {
        x = 0;
        y = 0;
        if (!_isBuilt)
        {
            return false;
        }

        // Snap to grid first; expand search radius if the clamped node is an obstacle.
        TryGetNodeFromWorldPoint(worldPos, out int cx, out int cy);
        cx = Mathf.Clamp(cx, 0, _width - 1);
        cy = Mathf.Clamp(cy, 0, _height - 1);

        if (_nodes[cy * _width + cx].IsWalkable)
        {
            x = cx;
            y = cy;
            return true;
        }

        // BFS outward to find the nearest walkable node.
        int maxRadius = Mathf.Max(_width, _height);
        for (int r = 1; r <= maxRadius; r++)
        {
            for (int dy = -r; dy <= r; dy++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Mathf.Abs(dx) != r && Mathf.Abs(dy) != r) continue;
                    int nx = cx + dx;
                    int ny = cy + dy;
                    if (!IsInBounds(nx, ny)) continue;
                    if (_nodes[ny * _width + nx].IsWalkable)
                    {
                        x = nx;
                        y = ny;
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>Returns whether the node at (x, y) is within bounds and walkable.</summary>
    public bool IsWalkable(int x, int y)
    {
        if (!IsInBounds(x, y)) return false;
        return _nodes[y * _width + x].IsWalkable;
    }

    /// <summary>Returns whether the grid coordinates (x, y) are within bounds.</summary>
    public bool IsInBounds(int x, int y)
    {
        return x >= 0 && x < _width && y >= 0 && y < _height;
    }

    /// <summary>
    /// Validates whether a diagonal step from (fromX, fromY) by (dx, dy) is
    /// physically possible given the agent footprint.
    ///
    /// A diagonal move is permitted only when both orthogonal neighbours in the
    /// direction of travel are walkable. Without this rule, the agent could pass
    /// between two touching wall corners, which the kinematic movement motor
    /// would block with sliding, causing the agent to become stuck despite the
    /// path appearing valid.
    /// </summary>
    /// <param name="fromX">Column of the origin node.</param>
    /// <param name="fromY">Row of the origin node.</param>
    /// <param name="dx">Horizontal step direction (-1 or +1).</param>
    /// <param name="dy">Vertical step direction (-1 or +1).</param>
    /// <returns>
    /// <see langword="true"/> when both (fromX+dx, fromY) and (fromX, fromY+dy)
    /// are walkable; otherwise <see langword="false"/>.
    /// </returns>
    public bool IsValidDiagonalMove(int fromX, int fromY, int dx, int dy)
    {
        return IsWalkable(fromX + dx, fromY) && IsWalkable(fromX, fromY + dy);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void BuildFromBounds(Bounds bounds)
    {
        float nodeSize = _config.NodeSize;
        float agentRadius = _config.AgentRadius;
        LayerMask obstacleLayer = _config.ObstacleLayer;

        // Add a small border so the outermost nodes are fully inside the tilemap.
        float border = nodeSize * 0.5f;
        Vector2 min = new Vector2(bounds.min.x + border, bounds.min.y + border);
        Vector2 max = new Vector2(bounds.max.x - border, bounds.max.y - border);

        _width  = Mathf.Max(1, Mathf.RoundToInt((max.x - min.x) / nodeSize) + 1);
        _height = Mathf.Max(1, Mathf.RoundToInt((max.y - min.y) / nodeSize) + 1);
        _worldBottomLeft = min;

        _nodes = new PathNode[_width * _height];

        // Inflated box half-extent: the agent centre must stay at least AgentRadius
        // away from any obstacle surface, so we enlarge each detection box by that amount.
        float halfExtent = nodeSize * 0.5f + agentRadius;
        Vector2 boxSize = new Vector2(halfExtent * 2f, halfExtent * 2f);

        for (int y = 0; y < _height; y++)
        {
            for (int x = 0; x < _width; x++)
            {
                Vector2 worldPos = _worldBottomLeft +
                    new Vector2(x * nodeSize, y * nodeSize);

                bool isObstacle = Physics2D.OverlapBox(
                    worldPos, boxSize, angle: 0f, obstacleLayer) != null;

                _nodes[y * _width + x] = new PathNode
                {
                    X             = x,
                    Y             = y,
                    WorldPosition = worldPos,
                    IsWalkable    = !isObstacle,
                };
            }
        }

        _isBuilt = true;
        Debug.Log(
            $"[{nameof(PathfindingGrid)}] Built {_width}x{_height} grid " +
            $"({_width * _height} nodes) from bounds {bounds}.",
            this);
    }

    private static Bounds CalculateSceneTilemapBounds()
    {
        // Collect all active Tilemaps in all loaded scenes.
        Tilemap[] tilemaps = FindObjectsByType<Tilemap>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        if (tilemaps == null || tilemaps.Length == 0)
        {
            return default;
        }

        bool initialised = false;
        Bounds combined = default;

        foreach (Tilemap tilemap in tilemaps)
        {
            if (tilemap == null) continue;

            tilemap.CompressBounds();
            Bounds local = tilemap.localBounds;
            if (local.size.sqrMagnitude < 0.001f) continue;

            // Transform bounds to world space.
            Bounds world = new Bounds(
                tilemap.transform.TransformPoint(local.center),
                Vector3.Scale(local.size, tilemap.transform.lossyScale));

            if (!initialised)
            {
                combined = world;
                initialised = true;
            }
            else
            {
                combined.Encapsulate(world);
            }
        }

        return combined;
    }

    // ── Editor Gizmos ─────────────────────────────────────────────────────────
#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!_showGizmos || !_isBuilt || _nodes == null) return;

        float nodeSize = _config != null ? _config.NodeSize : 0.5f;
        Vector3 cubeSize = new Vector3(nodeSize * 0.9f, nodeSize * 0.9f, 0.1f);

        Color walkableColor  = new Color(0f, 1f, 0f, 0.15f);
        Color obstacleColor  = new Color(1f, 0f, 0f, 0.3f);

        foreach (PathNode node in _nodes)
        {
            Gizmos.color = node.IsWalkable ? walkableColor : obstacleColor;
            Gizmos.DrawCube(new Vector3(node.WorldPosition.x, node.WorldPosition.y, 0f), cubeSize);
        }
    }
#endif
}
