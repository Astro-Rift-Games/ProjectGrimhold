# Pathfinding Architecture

## Context
Enemies in Project Grimhold previously relied on direct-line pursuit and a basic obstacle avoidance steering layer. This led to enemies becoming stuck behind large walls or complex room geometry in the dungeon. A pathfinding system was required to navigate around these obstacles during `Chase` and `Patrol` states.

## Decision
Implement a custom A* pathfinding system tailored for the existing 2D Grid/Tilemap architecture and Photon Fusion 2.1 network simulation.

We rejected importing a third-party solution (like NavMeshPlus or generic A* assets) because:
1. They often assume they own the movement and transform updates (violating our separation of concerns).
2. They are difficult to make deterministic and idempotent for Fusion's resimulation.
3. They introduce heavy MonoBehaviour dependencies that conflict with our existing pure C# simulation data flow.

## Responsibilities

* **`PathfindingGrid` (MonoBehaviour)**: Scans active Tilemaps once during raid bootstrap and constructs a flat, 1D array of `PathNode` structs representing walkable space. It applies a conservative Minkowski erosion using the agent's radius to ensure walls are avoided by the agent's full volume, not just its centre.
* **`PathfindingGridConfig` (ScriptableObject)**: Stores static configuration (node size, agent radius, iteration limits, layer masks) shared across the grid and all solvers.
* **`AStarPathSolver` (Pure C#)**: A stateless, allocation-free A* solver. It holds preallocated working buffers and writes world-space waypoints into a caller-owned list. It includes post-process smoothing using `Physics2D.CircleCast` to remove redundant waypoints.
* **`EnemyPathfindingNavigator` (MonoBehaviour)**: The lifecycle manager per enemy. It caches the active path, advances the waypoint index, and contains the tick-based logic to decide when a repath is necessary.
* **`EnemyMovementAIController` (MonoBehaviour / NetworkBehaviour)**: The consumer. It queries the navigator for a normalized direction and passes that direction to the existing `EnemyObstacleAvoidance` and `Kinematic2DMovementMotor`.

## Network Authority & Sources of Truth

Pathfinding is **strictly a State Authority (Host) concern**.

* **Grid Construction**: The grid is built only on the Host by `NetworkSpawnManager` during `TryExecuteInitialRaidBootstrap` and rebuilt locally during `SealHostMigrationRoster`. Proxies (Clients) never build the grid or run A*.
* **State Ownership**: Paths and waypoints are **not** `[Networked]` state. They are local, transient data computed on the Host. The network boundary is the *resulting locomotion state* (`FacingDirection`, `IsMoving`), which `EnemyMovementAIController` synchronizes to clients.
* **Resimulation Tolerance**: `EnemyPathfindingNavigator` uses Fusion's `Runner.Tick` instead of `Time.time` to throttle path recalculations and prevent double-advancement of waypoints during `FixedUpdateNetwork` resimulation.
* **Host Migration**: Because paths are transient local state, a migrating Host simply drops the old path. Its `Spawned()` callback re-initializes the navigator, which immediately detects it has no valid path and requests a new one on the first tick.

## Data Flow

```text
EnemyMovementAIController.FixedUpdateNetwork (Host Only)
  -> ComputePursuitDirection (or Patrol)
    -> EnemyPathfindingNavigator.GetDirectionToTarget(currentPos, targetPos, Runner.Tick)
      -> Check timers / validity
      -> RequestPath (if needed)
        -> AStarPathSolver.FindPath(Grid, start, end, agentRadius, waypointBuffer)
          -> (A* search on PathfindingGrid)
          -> SmoothPath (CircleCast)
      -> Advance waypoint if reached (guarded by Runner.Tick)
    <- Returns normalized Vector2 direction to next waypoint
  -> Apply direction to EnemyObstacleAvoidance (steers away from dynamic entities)
  -> Apply final direction to Kinematic2DMovementMotor
-> Synchronize Transform, FacingDirection, IsMoving to Clients
```

## Risks and Limitations
* The `AStarPathSolver` operates entirely synchronously. A massive grid or a highly complex, unreachable path request could block the simulation tick. This is mitigated by `PathfindingGridConfig.MaxPathIterations`, which early-exits and returns a failed path if the limit is exceeded.
* All enemies share the same grid walkability map. If different enemy types have drastically different collider sizes, they would either need separate grids or a unified worst-case grid. Currently, we use a single grid configured for the standard enemy radius (0.35).
