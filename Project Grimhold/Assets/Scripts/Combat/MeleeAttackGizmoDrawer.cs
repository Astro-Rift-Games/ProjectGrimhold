using UnityEngine;

#if UNITY_EDITOR
/// <summary>
/// Draws the melee attack area and direction in the Editor.
/// Works for both Player and Enemy entities by resolving the movement controller
/// through interface-based queries (IMovementState via EnemyMovementAIController or PlayerMovementNetworkController).
/// </summary>
public sealed class MeleeAttackGizmoDrawer : MonoBehaviour
{
    [SerializeField]
    private Transform _attackOrigin;
    public Transform AttackOrigin => _attackOrigin;

    [SerializeField]
    private MeleeAttackConfig _config;

    [SerializeField]
    private MonoBehaviour _movementControllerSource;

    private IMovementState _movementState;

    private void OnDrawGizmosSelected()
    {
        if (_config == null)
        {
            return;
        }

        Vector2 origin = _attackOrigin != null ? (Vector2)_attackOrigin.position : (Vector2)transform.position;
        Vector2 direction = Vector2.down;

        // Try to resolve facing direction from any IMovementState component (Player or Enemy)
        TryResolveFacingDirection(ref direction);

        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = Vector2.down;
        }
        direction.Normalize();

        Vector2 attackCenter = origin + direction * _config.Range;

        // Draw origin point
        Gizmos.color = Color.blue;
        Gizmos.DrawSphere(origin, 0.05f);

        // Draw line from origin to center of area
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(origin, attackCenter);

        // Draw normalized direction
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(origin, origin + direction * 0.5f);

        // Draw circle at the actual attack area center
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackCenter, _config.Radius);

        // Draw line from origin to the farthest edge of the circle
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f);
        Gizmos.DrawLine(origin, attackCenter + direction * _config.Radius);
    }

    private void TryResolveFacingDirection(ref Vector2 direction)
    {
        // Try from the serialized source
        if (_movementControllerSource != null)
        {
            _movementState = _movementControllerSource as IMovementState;
            if (_movementState != null && _movementState.FacingDirection.sqrMagnitude > 0.0001f)
            {
                direction = _movementState.FacingDirection;
                return;
            }
        }

        if (!Application.isPlaying)
        {
            return;
        }

        // Try to find any IMovementState component in parent hierarchy
        _movementState = GetComponentInParent<IMovementState>();
        if (_movementState != null && _movementState.FacingDirection.sqrMagnitude > 0.0001f)
        {
            direction = _movementState.FacingDirection;
        }
    }
}
#endif