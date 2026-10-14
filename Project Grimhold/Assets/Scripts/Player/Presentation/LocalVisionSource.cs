using ProjectGrimhold.Gameplay.Visibility;
using UnityEngine;

/// <summary>
/// Local-only vision producer for a raid avatar: an unlimited-range facing cone plus a near
/// radius, both occluded by obstacle colliders. It renders nothing in game and owns no networked
/// state. <see cref="LocalPlayerVisibilityBinder"/> registers it as the viewer only for the local
/// current avatar; the local spectator registers the spectated avatar's source instead.
/// </summary>
[DisallowMultipleComponent]
public sealed class LocalVisionSource : MonoBehaviour, IVisionViewer
{
    private const int HitBufferSize = 8;

    [SerializeField] private VisibilitySettings _settings;
    [SerializeField] private PlayerMovementNetworkController _movement;

    [Tooltip("Vision origin. Defaults to this transform when empty.")]
    [SerializeField] private Transform _origin;

    [Tooltip("Facing used while the avatar has no usable facing direction.")]
    [SerializeField] private Vector2 _fallbackFacing = Vector2.down;

    [Header("Debug Gizmos")]
    [Tooltip("Draws the near radius and the occluded cone in the Scene view.")]
    [SerializeField] private bool _drawGizmos = true;

    [Tooltip("Number of rays used to draw the cone and where obstacles cut it.")]
    [SerializeField, Range(4, 128)] private int _gizmoRayCount = 32;

    [Tooltip("Drawing length for cone rays that hit nothing. Visual only; vision itself has no range limit.")]
    [SerializeField, Min(1f)] private float _gizmoRayLength = 30f;

    private readonly RaycastHit2D[] _hitBuffer = new RaycastHit2D[HitBufferSize];
    private bool _hasLoggedMissingDependencies;

    /// <summary>Root of the avatar that owns this viewer; its own colliders and targets are never hidden.</summary>
    public Transform ViewerRoot => _movement != null ? _movement.transform : transform;

    /// <summary>
    /// Builds this frame's vision query. Fails while dependencies are missing or the avatar
    /// is not spawned, which the visibility system treats as "no viewer".
    /// </summary>
    public bool TryBuildQuery(out VisionQuery query)
    {
        query = default;
        if (!gameObject.activeInHierarchy || !HasValidDependencies() || !IsSpawned())
        {
            return false;
        }

        query = new VisionQuery(
            ResolveOrigin(),
            ResolveFacing(),
            _settings.NearRadius,
            VisionConeRules.CosineOfHalfAngle(_settings.ConeAngleDegrees),
            _settings.ObstacleLayer,
            _hitBuffer,
            ViewerRoot,
            _settings.FadeTimings);
        return true;
    }

    private bool IsSpawned()
    {
        return _movement != null && _movement.Object != null && _movement.Object.IsValid;
    }

    private Vector2 ResolveOrigin()
    {
        Transform origin = _origin != null ? _origin : transform;
        return origin.position;
    }

    private Vector2 ResolveFacing()
    {
        Vector2 facing = IsSpawned() ? _movement.FacingDirection : _fallbackFacing;
        return VisionConeRules.ResolveFacing(facing, _fallbackFacing);
    }

    private bool HasValidDependencies()
    {
        if (_settings != null && _movement != null)
        {
            return true;
        }

        if (!_hasLoggedMissingDependencies)
        {
            _hasLoggedMissingDependencies = true;
            Debug.LogError(
                $"{nameof(LocalVisionSource)} requires {nameof(VisibilitySettings)} and {nameof(PlayerMovementNetworkController)} references.",
                this);
        }

        return false;
    }

#if UNITY_EDITOR
    private static readonly Color NearRadiusColor = new Color(1f, 0.85f, 0.2f, 0.9f);
    private static readonly Color ConeEdgeColor = new Color(0.2f, 1f, 0.4f, 1f);
    private static readonly Color ConeRayColor = new Color(0.2f, 1f, 0.4f, 0.35f);
    private static readonly Color BlockedColor = new Color(1f, 0.25f, 0.2f, 1f);
    private const float BlockedMarkerSize = 0.12f;
    private const int NearRadiusSegments = 48;

    private void OnValidate()
    {
        if (_movement == null)
        {
            _movement = GetComponentInParent<PlayerMovementNetworkController>();
        }
    }

    private void OnDrawGizmos()
    {
        if (!_drawGizmos || _settings == null)
        {
            return;
        }

        Vector2 origin = ResolveOrigin();
        Vector2 facing = ResolveFacing();
        DrawNearRadius(origin, _settings.NearRadius);
        DrawCone(origin, facing);
    }

    private static void DrawNearRadius(Vector2 origin, float radius)
    {
        if (radius <= 0f)
        {
            return;
        }

        Gizmos.color = NearRadiusColor;
        Vector3 previous = origin + Vector2.right * radius;
        for (int i = 1; i <= NearRadiusSegments; i++)
        {
            float angle = i * Mathf.PI * 2f / NearRadiusSegments;
            Vector3 next = origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            Gizmos.DrawLine(previous, next);
            previous = next;
        }
    }

    private void DrawCone(Vector2 origin, Vector2 facing)
    {
        float coneAngle = Mathf.Clamp(_settings.ConeAngleDegrees, 0f, 360f);
        float halfAngle = coneAngle * 0.5f;
        float facingAngle = Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg;
        int rayCount = Mathf.Max(2, _gizmoRayCount);

        for (int i = 0; i <= rayCount; i++)
        {
            float angle = (facingAngle - halfAngle + coneAngle * i / rayCount) * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            bool isEdge = i == 0 || i == rayCount;
            DrawConeRay(origin, direction, isEdge);
        }
    }

    private void DrawConeRay(Vector2 origin, Vector2 direction, bool isEdge)
    {
        RaycastHit2D hit = Physics2D.Raycast(origin, direction, _gizmoRayLength, _settings.ObstacleLayer);
        bool blocked = hit.collider != null && !hit.collider.transform.IsChildOf(ViewerRoot);
        Vector2 end = blocked ? hit.point : origin + direction * _gizmoRayLength;

        Gizmos.color = isEdge ? ConeEdgeColor : ConeRayColor;
        Gizmos.DrawLine(origin, end);

        if (blocked)
        {
            Gizmos.color = BlockedColor;
            Gizmos.DrawWireCube(end, Vector3.one * BlockedMarkerSize);
        }
    }
#endif
}
