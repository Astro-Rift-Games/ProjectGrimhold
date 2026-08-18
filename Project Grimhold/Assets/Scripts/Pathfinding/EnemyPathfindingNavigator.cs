using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// Manages the lifecycle of a single enemy's active path: requesting a new
/// path when needed, caching the result, advancing waypoints and reporting
/// the direction to the next target point.
///
/// This component is owned by the State Authority (Host). It is never invoked
/// on proxies because <see cref="EnemyMovementAIController.FixedUpdateNetwork"/>
/// is gated on HasStateAuthority.
///
/// Resimulation safety: Fusion may re-execute FixedUpdateNetwork multiple times
/// for the same simulation tick (e.g. when correcting a client's predicted input).
/// To remain idempotent, the repath timer is based on the simulation tick number
/// (Runner.Tick), not on accumulated delta time. Calling
/// <see cref="GetDirectionToTarget"/> twice with the same tick value produces the
/// same outcome and does not duplicate waypoint advancement.
///
/// Host Migration: this component holds no [Networked] state. Its local state
/// (active path, waypoint index, timer) is reset when Spawned() is re-invoked
/// on the replacement host. The first call to GetDirectionToTarget on the new
/// host will trigger a fresh path request.
/// </summary>
/// <remarks>
/// See <c>Docs/Architecture/PathfindingArchitecture.md</c> for the complete
/// pathfinding flow and authority model.
/// </remarks>
[DisallowMultipleComponent]
public sealed class EnemyPathfindingNavigator : MonoBehaviour
{
    // ── Configuration ─────────────────────────────────────────────────────────

    [SerializeField]
    private PathfindingGridConfig _config;

    [Tooltip("How often to recalculate the path, in seconds.")]
    [SerializeField, Min(0.05f)]
    private float _repathIntervalSeconds = 0.5f;

    [Tooltip("Distance from the current waypoint at which the agent advances to the next.")]
    [SerializeField, Min(0.05f)]
    private float _waypointReachRadius = 0.3f;

    [Tooltip("How far the target must move before a repath is triggered immediately.")]
    [SerializeField, Min(0.1f)]
    private float _targetMovedThreshold = 1.5f;

    // ── Runtime state (not networked; lives on State Authority only) ──────────

    private AStarPathSolver _solver;
    private PathfindingGrid _grid;

    // Caller-owned waypoint buffer reused across path requests to avoid allocations.
    private readonly List<Vector2> _waypointBuffer = new List<Vector2>(32);
    private int _waypointCount;
    private int _waypointIndex;

    private bool _hasValidPath;

    // Tick-based repath timer (see Resimulation safety note in the class summary).
    private int _lastRepathTick = int.MinValue;

    private Vector2 _lastKnownTargetPosition;

    // Track whether the waypoint has already been advanced in the current tick
    // to guard against double-advancement during Fusion resimulation.
    private int _lastWaypointAdvanceTick = int.MinValue;

    // ── Fusion runner reference (obtained in Spawned) ─────────────────────────

    private NetworkRunner _runner;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// <see langword="true"/> when a valid path is cached and has remaining waypoints.
    /// </summary>
    public bool HasValidPath => _hasValidPath && _waypointCount > 0;

    /// <summary>
    /// Initializes the navigator after the network object has been spawned.
    ///
    /// Must be called from <see cref="EnemyMovementAIController.Spawned"/> so that
    /// the runner reference and grid are available.
    /// </summary>
    /// <param name="runner">The active NetworkRunner (used to read simulation tick).</param>
    public void Initialize(NetworkRunner runner)
    {
        _runner = runner;
        _grid   = UnityEngine.Object.FindFirstObjectByType<PathfindingGrid>();

        if (_config == null)
        {
            Debug.LogError(
                $"{nameof(EnemyPathfindingNavigator)} on '{name}' has no " +
                $"{nameof(PathfindingGridConfig)} assigned.",
                this);
            return;
        }

        if (_grid == null)
        {
            // Clients will hit this path because they do not build the grid.
            // Only log a warning on the server where pathfinding is expected.
            if (runner != null && runner.IsServer)
            {
                Debug.LogWarning(
                    $"{nameof(EnemyPathfindingNavigator)} on '{name}': no " +
                    $"{nameof(PathfindingGrid)} found on the NetworkRunner. " +
                    "Pathfinding will not be available.",
                    this);
            }
            return;
        }

        _solver = new AStarPathSolver(_grid.Width, _grid.Height, _config);
        ResetState();

        // Stagger the initial repath timer deterministically by the object's
        // network ID to distribute A* calls across different simulation ticks.
        // This avoids a spike where many enemies all repath on the first tick.
        if (runner != null && runner.IsServer)
        {
            NetworkObject networkObject = GetComponent<NetworkObject>();
            if (networkObject != null)
            {
                int repathTicks = RepathIntervalTicks;
                int offset = (int)(networkObject.Id.Raw % (uint)repathTicks);
                _lastRepathTick = runner.Tick - repathTicks + offset;
            }
        }
    }

    /// <summary>
    /// Returns the normalized direction toward the next waypoint for the given target.
    /// Requests a new path when the active path is invalid, the target has moved
    /// beyond the movement threshold, or the repath interval has elapsed.
    ///
    /// Returns <see cref="Vector2.zero"/> when no valid path exists. The caller
    /// should stall (apply no movement) rather than fall back to direct-line
    /// navigation, because direct-line movement is the pre-pathfinding behaviour
    /// that caused enemies to become stuck.
    /// </summary>
    /// <param name="currentPos">Current world-space position of the enemy.</param>
    /// <param name="targetPos">Current world-space position of the navigation target.</param>
    /// <param name="currentTick">
    /// The current Fusion simulation tick (<c>Runner.Tick</c>).
    /// Must be the same value for all calls within the same simulation tick to
    /// ensure idempotent behaviour during resimulation.
    /// </param>
    /// <returns>
    /// Normalized direction toward the next waypoint, or <see cref="Vector2.zero"/>
    /// if pathfinding is unavailable or has found no valid path.
    /// </returns>
    public Vector2 GetDirectionToTarget(Vector2 currentPos, Vector2 targetPos, int currentTick)
    {
        if (_grid == null || !_grid.IsBuilt || _solver == null)
        {
            return Vector2.zero;
        }

        // Request a new path if necessary.
        if (!_hasValidPath ||
            ShouldRepath(currentTick) ||
            TargetHasMoved(targetPos))
        {
            RequestPath(currentPos, targetPos, currentTick);
        }

        if (!_hasValidPath || _waypointCount == 0)
        {
            return Vector2.zero;
        }

        // Advance the waypoint index only once per simulation tick, even if
        // GetDirectionToTarget is called multiple times during resimulation.
        if (_waypointIndex < _waypointCount - 1 && currentTick != _lastWaypointAdvanceTick)
        {
            Vector2 toWaypoint = _waypointBuffer[_waypointIndex] - currentPos;
            if (toWaypoint.sqrMagnitude < _waypointReachRadius * _waypointReachRadius)
            {
                _waypointIndex++;
                _lastWaypointAdvanceTick = currentTick;
            }
        }

        // Mark the path as exhausted when the final waypoint is reached.
        if (_waypointIndex >= _waypointCount)
        {
            _hasValidPath = false;
            return Vector2.zero;
        }

        Vector2 direction = _waypointBuffer[_waypointIndex] - currentPos;
        if (direction.sqrMagnitude < 0.0001f)
        {
            return Vector2.zero;
        }

        return direction.normalized;
    }

    /// <summary>
    /// Immediately invalidates the current path, forcing a repath on the
    /// next call to <see cref="GetDirectionToTarget"/>.
    ///
    /// Call this when the navigation goal changes (e.g. patrol waypoint index
    /// advances, or pursuit ends and then resumes toward a different target).
    /// </summary>
    public void InvalidatePath()
    {
        _hasValidPath  = false;
        _waypointCount = 0;
        _waypointIndex = 0;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private int RepathIntervalTicks
    {
        get
        {
            // Guard against zero DeltaTime (should never happen in Fusion but be safe).
            float dt = _runner != null ? _runner.DeltaTime : 0.02f;
            return Mathf.Max(1, Mathf.CeilToInt(_repathIntervalSeconds / dt));
        }
    }

    private bool ShouldRepath(int currentTick)
    {
        return (currentTick - _lastRepathTick) >= RepathIntervalTicks;
    }

    private bool TargetHasMoved(Vector2 targetPos)
    {
        return Vector2.SqrMagnitude(targetPos - _lastKnownTargetPosition) >
               _targetMovedThreshold * _targetMovedThreshold;
    }

    /// <summary>
    /// Invokes A* and updates the cached path. Resets the repath timer regardless
    /// of whether a path was found, to avoid thrashing on unsolvable requests.
    /// </summary>
    private void RequestPath(Vector2 currentPos, Vector2 targetPos, int currentTick)
    {
        _lastRepathTick          = currentTick;
        _lastKnownTargetPosition = targetPos;

        int count = _solver.FindPath(_grid, currentPos, targetPos, _waypointBuffer);

        if (count > 0)
        {
            _waypointCount = count;
            _waypointIndex = 0;
            _hasValidPath  = true;
        }
        else
        {
            _hasValidPath  = false;
            _waypointCount = 0;
            _waypointIndex = 0;
        }
    }

    private void ResetState()
    {
        _waypointBuffer.Clear();
        _waypointCount           = 0;
        _waypointIndex           = 0;
        _hasValidPath            = false;
        _lastRepathTick          = int.MinValue;
        _lastKnownTargetPosition = Vector2.zero;
        _lastWaypointAdvanceTick = int.MinValue;
    }

    // ── Editor Gizmos ─────────────────────────────────────────────────────────
#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (_config != null)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, _config.AgentRadius);
        }

        if (!_hasValidPath || _waypointCount == 0) return;

        // Draw the full path in cyan.
        Gizmos.color = Color.cyan;
        for (int i = 0; i < _waypointCount - 1; i++)
        {
            Gizmos.DrawLine(
                new Vector3(_waypointBuffer[i].x, _waypointBuffer[i].y, 0f),
                new Vector3(_waypointBuffer[i + 1].x, _waypointBuffer[i + 1].y, 0f));
        }

        // Highlight the current waypoint in yellow.
        if (_waypointIndex < _waypointCount)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(
                new Vector3(_waypointBuffer[_waypointIndex].x,
                    _waypointBuffer[_waypointIndex].y, 0f),
                0.15f);
        }

        // Highlight the destination in red.
        Vector2 last = _waypointBuffer[_waypointCount - 1];
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(new Vector3(last.x, last.y, 0f), 0.2f);
    }
#endif
}
