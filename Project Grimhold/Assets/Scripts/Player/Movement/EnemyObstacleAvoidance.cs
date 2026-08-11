using UnityEngine;
using Fusion;

/// <summary>
/// Settings for deterministic enemy obstacle avoidance.
/// Defines how the AI detects and steers around physical obstacles.
/// </summary>
[System.Serializable]
public struct EnemyObstacleAvoidanceSettings
{
    [Tooltip("Radius of the circle cast used to detect obstacles. Should approximate the enemy's physical radius.")]
    [Min(0.01f)]
    public float CastRadius;

    [Tooltip("How far ahead the enemy projects its detection cast.")]
    [Min(0.1f)]
    public float CastDistance;

    [Tooltip("How strongly the enemy steers parallel to the wall when an obstacle is detected. 1.0 means it moves completely parallel, 0 means no avoidance.")]
    [Range(0f, 1f)]
    public float AvoidanceStrength;
}

/// <summary>
/// Pure C# class responsible for computing obstacle avoidance steering.
///
/// Implements deterministic wall-steering (sliding) using CircleCastNonAlloc.
/// Uses the cross product of the intended direction and the obstacle normal to pick a sticky side.
/// Resolves dead-center collisions deterministically using the EntityId as a tiebreaker.
///
/// Does NOT maintain [Networked] state, as the direction to the target and the obstacle geometry
/// provide enough stable context to derive the avoidance tangent mathematically.
/// </summary>
public sealed class EnemyObstacleAvoidance
{
    private readonly RaycastHit2D[] _hitBuffer = new RaycastHit2D[1];
    
    private readonly LayerMask _obstacleLayer;

    public EnemyObstacleAvoidance(LayerMask obstacleLayer)
    {
        _obstacleLayer = obstacleLayer;
    }

    /// <summary>
    /// Evaluates the intended direction against physical obstacles and returns a steered direction.
    /// </summary>
    /// <param name="currentPos">The enemy's current world position.</param>
    /// <param name="targetPos">The world position of the target (player or waypoint).</param>
    /// <param name="intendedDirection">The original normalized movement direction computed by the AI.</param>
    /// <param name="settings">The configuration for the avoidance cast.</param>
    /// <param name="entityIdValue">The Raw EntityId value used as a deterministic tiebreaker for dead-on collisions.</param>
    /// <returns>A new normalized direction vector adjusted to steer around detected obstacles.</returns>
    public Vector2 Steer(
        Vector2 currentPos,
        Vector2 targetPos,
        Vector2 intendedDirection,
        in EnemyObstacleAvoidanceSettings settings,
        int entityIdValue)
    {
        if (intendedDirection.sqrMagnitude < 0.001f)
        {
            return intendedDirection;
        }

        int hitCount = Physics2D.CircleCastNonAlloc(
            currentPos,
            settings.CastRadius,
            intendedDirection,
            _hitBuffer,
            settings.CastDistance,
            _obstacleLayer);

        if (hitCount == 0)
        {
            return intendedDirection;
        }

        RaycastHit2D hit = _hitBuffer[0];
        
        if (hit.collider == null)
        {
            return intendedDirection;
        }

        // Determine the tangent along the wall.
        // We use the direction to the target to provide a stable "sticky" context, avoiding jitter.
        Vector2 dirToTarget = (targetPos - currentPos).normalized;
        if (dirToTarget.sqrMagnitude < 0.001f)
        {
            dirToTarget = intendedDirection;
        }

        // Cross product Z = X1*Y2 - Y1*X2
        float crossZ = dirToTarget.x * hit.normal.y - dirToTarget.y * hit.normal.x;

        Vector2 tangent;
        if (Mathf.Abs(crossZ) > 0.01f)
        {
            // If crossZ > 0, normal is to the left, steer right
            // Tangent: (normal.y, -normal.x)
            // If crossZ < 0, normal is to the right, steer left
            // Tangent: (-normal.y, normal.x)
            tangent = crossZ > 0
                ? new Vector2(hit.normal.y, -hit.normal.x)
                : new Vector2(-hit.normal.y, hit.normal.x);
        }
        else
        {
            // Dead-on collision (e.g. perfectly perpendicular to the wall and target is directly behind it).
            // Use EntityId as a strict deterministic tiebreaker.
            tangent = (entityIdValue % 2 == 0)
                ? new Vector2(hit.normal.y, -hit.normal.x)
                : new Vector2(-hit.normal.y, hit.normal.x);
        }

        // Blend the intended direction with the tangent to steer smoothly.
        // If AvoidanceStrength is 1, it returns the pure tangent (sliding exactly parallel).
        Vector2 steeredDirection = Vector2.Lerp(intendedDirection, tangent, settings.AvoidanceStrength).normalized;

        return steeredDirection;
    }
}
