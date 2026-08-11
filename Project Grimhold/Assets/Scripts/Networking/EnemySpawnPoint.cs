using UnityEngine;

namespace Spawning
{
    /// <summary>
    /// Component placed on scene objects used as enemy spawn points to define spawn-specific data,
    /// such as the route an enemy should patrol after spawning.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemySpawnPoint : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The patrol route to assign to an enemy spawned at this point. If null, the enemy will be stationary (Idle).")]
        private EnemyPatrolRoute _patrolRoute;

        /// <summary>
        /// Gets the patrol route assigned to this spawn point.
        /// Returns null if no route is assigned or if the assigned route has no valid waypoints.
        /// </summary>
        public EnemyPatrolRoute PatrolRoute
        {
            get
            {
                if (_patrolRoute != null && _patrolRoute.HasWaypoints)
                {
                    return _patrolRoute;
                }
                return null;
            }
        }
    }
}
