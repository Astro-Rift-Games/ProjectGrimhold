using UnityEngine;

/// <summary>
/// Represents a single node in the pathfinding grid.
///
/// Stores the node's grid coordinates, world position and walkability flag.
/// Also contains working data used internally by <see cref="AStarPathSolver"/>
/// during each path search. These A* fields are reset at the start of every
/// search and must not be read outside of an active <c>FindPath</c> call.
///
/// This type is a struct to allow allocation as a flat array inside
/// <see cref="PathfindingGrid"/> without heap overhead per node.
/// </summary>
public struct PathNode
{
    /// <summary>Grid column index (X axis).</summary>
    public int X;

    /// <summary>Grid row index (Y axis).</summary>
    public int Y;

    /// <summary>Centre position of this node in world space.</summary>
    public Vector2 WorldPosition;

    /// <summary>
    /// Whether this node is navigable by the pathfinding agent.
    /// Set once during <see cref="PathfindingGrid.Build"/> and never mutated at runtime.
    /// </summary>
    public bool IsWalkable;

    // ── A* working data ──────────────────────────────────────────────────────
    // Reset at the start of every FindPath call. Not valid outside an active search.

    /// <summary>Movement cost from the start node to this node (actual cost so far).</summary>
    public int GCost;

    /// <summary>Estimated cost from this node to the goal (heuristic).</summary>
    public int HCost;

    /// <summary>Total estimated cost of a path through this node (GCost + HCost).</summary>
    public int FCost => GCost + HCost;

    /// <summary>Grid X of the node that preceded this one on the cheapest known path.</summary>
    public int ParentX;

    /// <summary>Grid Y of the node that preceded this one on the cheapest known path.</summary>
    public int ParentY;

    /// <summary>Whether this node is currently in the open set of the A* search.</summary>
    public bool InOpenSet;

    /// <summary>Whether this node has been fully evaluated (closed set).</summary>
    public bool InClosedSet;
}
