using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stateless A* path solver over a <see cref="PathfindingGrid"/>.
///
/// This class owns preallocated working buffers (open heap, node data array)
/// and writes its output into a caller-provided <see cref="List{T}"/> so that
/// no heap allocations occur per search.
///
/// Each call to <see cref="FindPath"/> is fully self-contained. The solver
/// holds no state between calls and is safe to invoke multiple times within
/// the same simulation tick (e.g. during Fusion resimulation).
///
/// Path smoothing is applied post-A* via <c>Physics2D.CircleCast</c> with the
/// agent radius, ensuring that any direct segment in the smoothed path is
/// physically traversable by the agent volume — not just the path centre-line.
///
/// The diagonal corner-cutting rule is delegated to
/// <see cref="PathfindingGrid.IsValidDiagonalMove"/> so that both systems share
/// a single, consistent definition of what constitutes a legal diagonal step.
/// </summary>
/// <remarks>
/// See <c>Docs/Architecture/PathfindingArchitecture.md</c> for the complete
/// pathfinding data flow and authority model.
/// </remarks>
public sealed class AStarPathSolver
{
    // Cost multipliers expressed as integers (×10) to avoid floating-point
    // comparisons in G-cost accumulation.
    private const int CostStraight = 10;
    private const int CostDiagonal = 14; // approx √2 × 10

    private readonly PathfindingGridConfig _config;
    private readonly LayerMask _obstacleLayer;
    private readonly PathNode[] _nodeData;
    private readonly BinaryMinHeap _openHeap;

    // Tracks which nodes were dirtied during a search so they can be reset
    // without clearing the entire array every call.
    private readonly List<int> _visitedIndices = new List<int>(256);

    /// <summary>
    /// Allocates preallocated internal buffers sized for the given grid dimensions.
    /// </summary>
    /// <param name="gridWidth">Number of columns in the grid.</param>
    /// <param name="gridHeight">Number of rows in the grid.</param>
    /// <param name="config">Configuration shared with the grid.</param>
    public AStarPathSolver(int gridWidth, int gridHeight, PathfindingGridConfig config)
    {
        _config       = config;
        _obstacleLayer = config.ObstacleLayer;

        int nodeCount = gridWidth * gridHeight;
        _nodeData = new PathNode[nodeCount];
        // Heap capacity equals node count: in the absolute worst case every node
        // could enter the open set once.
        _openHeap = new BinaryMinHeap(nodeCount);
    }

    /// <summary>
    /// Searches for a path from <paramref name="startWorld"/> to
    /// <paramref name="endWorld"/> and writes world-space waypoints into
    /// <paramref name="outputWaypoints"/>.
    ///
    /// <para>The list is cleared before use. The caller retains ownership.</para>
    /// </summary>
    /// <param name="grid">The walkability grid to search.</param>
    /// <param name="startWorld">Start position in world space.</param>
    /// <param name="endWorld">Goal position in world space.</param>
    /// <param name="enableSmoothing">If true, applies CircleCast smoothing to cut corners.</param>
    /// <param name="pathSmoothingRadius">Radius used for CircleCast if smoothing is enabled.</param>
    /// <param name="outputWaypoints">
    /// Caller-owned list that receives world-space waypoint positions.
    /// </param>
    /// <returns>
    /// Number of waypoints written (>0 on success, 0 on failure).
    /// </returns>
    public int FindPath(
        PathfindingGrid grid,
        Vector2 startWorld,
        Vector2 endWorld,
        bool enableSmoothing,
        float pathSmoothingRadius,
        List<Vector2> outputWaypoints)
    {
        outputWaypoints.Clear();

        if (!grid.IsBuilt)
        {
            return 0;
        }

        // Resolve start and end to the nearest walkable nodes.
        if (!grid.TryGetNearestWalkableNode(startWorld, out int sx, out int sy) ||
            !grid.TryGetNearestWalkableNode(endWorld,   out int ex, out int ey))
        {
            return 0;
        }

        // Trivial case: already at the destination node.
        if (sx == ex && sy == ey)
        {
            outputWaypoints.Add(endWorld);
            return 1;
        }

        ResetVisitedNodes(grid);
        _openHeap.Clear();

        // Initialise the start node.
        int startIndex = FlatIndex(sx, sy, grid.Width);
        PathNode startNode = grid.GetNode(sx, sy);
        startNode.GCost = 0;
        startNode.HCost = Heuristic(sx, sy, ex, ey);
        startNode.InOpenSet = true;
        _nodeData[startIndex] = startNode;
        Track(startIndex);

        _openHeap.Push(startNode.FCost, sx, sy);

        int iterations = 0;
        int maxIterations = _config.MaxPathIterations;

        while (_openHeap.Count > 0 && iterations < maxIterations)
        {
            iterations++;

            (_, int cx, int cy) = _openHeap.Pop();
            int currentIndex = FlatIndex(cx, cy, grid.Width);

            ref PathNode current = ref _nodeData[currentIndex];
            if (current.InClosedSet) continue;
            current.InClosedSet = true;
            current.InOpenSet   = false;

            // Reached the goal.
            if (cx == ex && cy == ey)
            {
                RetracePath(grid, sx, sy, ex, ey, outputWaypoints);
                
                if (enableSmoothing)
                {
                    SmoothPath(outputWaypoints, pathSmoothingRadius, endWorld);
                }
                else
                {
                    // Replace the exact final position with the requested endWorld for sub-node precision
                    if (outputWaypoints.Count > 0)
                    {
                        outputWaypoints[outputWaypoints.Count - 1] = endWorld;
                    }
                }
                
                return outputWaypoints.Count;
            }

            // Expand neighbours.
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = cx + dx;
                    int ny = cy + dy;
                    if (!grid.IsWalkable(nx, ny)) continue;

                    bool isDiagonal = dx != 0 && dy != 0;
                    if (isDiagonal && !grid.IsValidDiagonalMove(cx, cy, dx, dy)) continue;

                    int neighbourIndex = FlatIndex(nx, ny, grid.Width);
                    ref PathNode neighbour = ref _nodeData[neighbourIndex];

                    if (neighbour.InClosedSet) continue;

                    // Copy walkability into working node if it has not been visited yet.
                    if (!neighbour.InOpenSet && !neighbour.InClosedSet)
                    {
                        PathNode original = grid.GetNode(nx, ny);
                        neighbour.X           = original.X;
                        neighbour.Y           = original.Y;
                        neighbour.WorldPosition = original.WorldPosition;
                        neighbour.IsWalkable  = original.IsWalkable;
                        Track(neighbourIndex);
                    }

                    int tentativeG = current.GCost + (isDiagonal ? CostDiagonal : CostStraight);
                    if (tentativeG < neighbour.GCost || !neighbour.InOpenSet)
                    {
                        neighbour.GCost   = tentativeG;
                        neighbour.HCost   = Heuristic(nx, ny, ex, ey);
                        neighbour.ParentX = cx;
                        neighbour.ParentY = cy;

                        if (!neighbour.InOpenSet)
                        {
                            neighbour.InOpenSet = true;
                            _openHeap.Push(neighbour.FCost, nx, ny);
                        }
                    }
                }
            }
        }

        if (iterations >= maxIterations)
        {
            Debug.LogWarning(
                $"[{nameof(AStarPathSolver)}] Path search exceeded {maxIterations} iterations " +
                $"(start={startWorld}, end={endWorld}). Returning no path.",
                null);
        }

        return 0;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Octile distance heuristic: admissible for 8-directional movement.
    /// Uses integer costs (×10) to match G-cost accumulation.
    /// </summary>
    private static int Heuristic(int ax, int ay, int bx, int by)
    {
        int dx = Mathf.Abs(ax - bx);
        int dy = Mathf.Abs(ay - by);
        // Octile: D * (dx + dy) + (D2 - 2*D) * min(dx, dy)  where D=10, D2=14
        return CostStraight * (dx + dy) + (CostDiagonal - 2 * CostStraight) * Mathf.Min(dx, dy);
    }

    private static int FlatIndex(int x, int y, int width) => y * width + x;

    private void Track(int index)
    {
        _visitedIndices.Add(index);
    }

    /// <summary>
    /// Resets only the A* working fields of nodes that were touched in the last
    /// search, avoiding a full array clear on every call.
    /// </summary>
    private void ResetVisitedNodes(PathfindingGrid grid)
    {
        foreach (int index in _visitedIndices)
        {
            _nodeData[index].GCost      = 0;
            _nodeData[index].HCost      = 0;
            _nodeData[index].ParentX    = 0;
            _nodeData[index].ParentY    = 0;
            _nodeData[index].InOpenSet  = false;
            _nodeData[index].InClosedSet = false;
        }
        _visitedIndices.Clear();
    }

    /// <summary>
    /// Reconstructs the path by following parent pointers from the goal back to
    /// the start, then reverses the result into world-space waypoints.
    /// </summary>
    private void RetracePath(
        PathfindingGrid grid,
        int sx, int sy,
        int ex, int ey,
        List<Vector2> output)
    {
        int cx = ex;
        int cy = ey;

        while (cx != sx || cy != sy)
        {
            output.Add(grid.GetWorldPosition(cx, cy));
            int idx = FlatIndex(cx, cy, grid.Width);
            int px = _nodeData[idx].ParentX;
            int py = _nodeData[idx].ParentY;
            cx = px;
            cy = py;
        }

        // Add the start position and reverse so the list runs start → goal.
        output.Add(grid.GetWorldPosition(sx, sy));
        output.Reverse();
    }

    /// <summary>
    /// Applies line-of-sight smoothing using <c>Physics2D.CircleCast</c> to
    /// remove intermediate waypoints that are directly reachable by the agent.
    ///
    /// CircleCast with the agent radius correctly accounts for the agent volume;
    /// a plain Linecast would allow paths through gaps smaller than the agent.
    ///
    /// The final waypoint is replaced with the raw <paramref name="endWorld"/>
    /// position for sub-node precision.
    /// </summary>
    private void SmoothPath(List<Vector2> waypoints, float pathSmoothingRadius, Vector2 endWorld)
    {
        if (waypoints.Count <= 2)
        {
            if (waypoints.Count > 0)
            {
                waypoints[waypoints.Count - 1] = endWorld;
            }
            return;
        }

        int writeIndex = 0;
        int current = 0;

        while (current < waypoints.Count - 1)
        {
            // Find the furthest waypoint reachable from current via CircleCast.
            int furthest = current + 1;
            for (int lookahead = current + 2; lookahead < waypoints.Count; lookahead++)
            {
                if (HasDirectPath(waypoints[current], waypoints[lookahead], pathSmoothingRadius))
                {
                    furthest = lookahead;
                }
            }

            waypoints[writeIndex++] = waypoints[current];
            current = furthest;
        }

        // Always include the last waypoint.
        waypoints[writeIndex++] = endWorld;

        // Trim excess entries without allocating a new list.
        int excess = waypoints.Count - writeIndex;
        for (int i = 0; i < excess; i++)
        {
            waypoints.RemoveAt(waypoints.Count - 1);
        }
    }

    /// <summary>
    /// Returns <see langword="true"/> when the agent volume can move from
    /// <paramref name="from"/> to <paramref name="to"/> without intersecting any
    /// obstacle collider.
    /// </summary>
    private bool HasDirectPath(Vector2 from, Vector2 to, float pathSmoothingRadius)
    {
        Vector2 delta = to - from;
        float distance = delta.magnitude;
        if (distance < 0.001f) return true;

        // CircleCast projects the agent circle along the segment to detect any
        // collision that would block the agent body (not just the centre-line).
        RaycastHit2D hit = Physics2D.CircleCast(
            from,
            pathSmoothingRadius,
            delta / distance,
            distance,
            _obstacleLayer);

        return !hit.collider;
    }
}
