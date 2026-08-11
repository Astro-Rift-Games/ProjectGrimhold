using UnityEngine;

/// <summary>
/// Defines a circular patrol route of waypoints for an enemy.
/// This is a data-only MonoBehaviour. The actual navigation logic is executed
/// by EnemyMovementAIController during the Patrol state.
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyPatrolRoute : MonoBehaviour
{
    [Tooltip("The ordered list of waypoints forming the patrol route.")]
    [SerializeField] private Transform[] _waypoints;

    /// <summary>
    /// Returns true if the route has at least one valid waypoint.
    /// </summary>
    public bool HasWaypoints => _waypoints != null && _waypoints.Length > 0;

    /// <summary>
    /// Gets the total number of waypoints in the route.
    /// </summary>
    public int Count => _waypoints?.Length ?? 0;

    /// <summary>
    /// Attempts to get a waypoint by index. Supports circular wrapping using modulo.
    /// </summary>
    /// <param name="index">The current waypoint index (will be wrapped).</param>
    /// <param name="waypoint">The resolved transform.</param>
    /// <returns>True if a valid waypoint is found.</returns>
    public bool TryGetWaypoint(int index, out Transform waypoint)
    {
        if (!HasWaypoints)
        {
            waypoint = null;
            return false;
        }

        // Circular wrapping
        int wrappedIndex = index % _waypoints.Length;
        waypoint = _waypoints[wrappedIndex];

        return waypoint != null;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!HasWaypoints)
            return;

        Gizmos.color = Color.cyan;
        for (int i = 0; i < _waypoints.Length; i++)
        {
            Transform current = _waypoints[i];
            if (current == null) continue;

            Gizmos.DrawSphere(current.position, 0.2f);

            Transform next = _waypoints[(i + 1) % _waypoints.Length];
            if (next != null)
            {
                Gizmos.DrawLine(current.position, next.position);
            }
        }
    }
#endif
}
