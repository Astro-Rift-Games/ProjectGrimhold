using UnityEngine;

/// <summary>
/// Resolves desired 2D displacement against configured world colliders and
/// applies the accepted movement to a kinematic <see cref="Rigidbody2D"/>.
///
/// This component is independent from Fusion, input and visual presentation.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public sealed class Kinematic2DMovementMotor : MonoBehaviour
{
    private const int CastHitCapacity = 8;
    private const float MinimumMoveSqrMagnitude = 0.0000001f;

    [SerializeField]
    private Rigidbody2D _rigidbody;

    [SerializeField]
    private Collider2D _collider;

    [SerializeField]
    private LayerMask _collisionMask;

    [SerializeField, Min(0f)]
    private float _skinWidth = 0.02f;

    [SerializeField, Range(1, CastHitCapacity)]
    private int _maxSlideIterations = 3;

    private readonly RaycastHit2D[] _castHits =
        new RaycastHit2D[CastHitCapacity];

    private readonly Collider2D[] _overlapColliders =
        new Collider2D[CastHitCapacity];

    private ContactFilter2D _contactFilter;

    private void Awake()
    {
        CacheDependencies();
        ConfigureContactFilter();
        ValidateRigidbody();
    }

    /// <summary>
    /// Resolves and applies the requested displacement immediately for the
    /// current simulation tick.
    /// </summary>
    /// <returns>
    /// The actual displacement applied to the Rigidbody after resolving collisions.
    /// </returns>
    public Vector2 Move(Vector2 displacement)
    {
        // Sincronizar la posición del Rigidbody con el Transform restaurado por Fusion al inicio del tick
        _rigidbody.position = transform.position;

        ConfigureContactFilter();
        ResolveInitialOverlaps();

        Vector2 initialPosition = _rigidbody.position;

        if (displacement.sqrMagnitude <= MinimumMoveSqrMagnitude)
        {
            return Vector2.zero;
        }

        Vector2 remainingDisplacement = displacement;

        for (int iteration = 0;
             iteration < _maxSlideIterations &&
             remainingDisplacement.sqrMagnitude > MinimumMoveSqrMagnitude;
             iteration++)
        {
            float distance = remainingDisplacement.magnitude;
            Vector2 direction = remainingDisplacement / distance;
            float castDistance = distance + _skinWidth;

            int hitCount = _collider.Cast(
                direction,
                _contactFilter,
                _castHits,
                castDistance);

            if (!TryGetClosestHit(hitCount, out RaycastHit2D closestHit))
            {
                ApplyPosition(_rigidbody.position + remainingDisplacement);
                break;
            }

            float allowedDistance = Mathf.Min(
                distance,
                Mathf.Max(0f, closestHit.distance - _skinWidth));

            Vector2 appliedStep = direction * allowedDistance;

            if (appliedStep.sqrMagnitude > MinimumMoveSqrMagnitude)
            {
                ApplyPosition(_rigidbody.position + appliedStep);
            }

            Vector2 blockedRemainder =
                remainingDisplacement - appliedStep;

            float intoSurface =
                Vector2.Dot(blockedRemainder, closestHit.normal);

            if (intoSurface < 0f)
            {
                blockedRemainder -= closestHit.normal * intoSurface;
            }

            remainingDisplacement = blockedRemainder;
        }

        return _rigidbody.position - initialPosition;
    }

    /// <summary>
    /// Pushes the body out of any configured collider it already overlaps
    /// (for example after knockback), because casts started inside geometry
    /// report zero distance in every direction. The correction is not part of
    /// the displacement returned by <see cref="Move"/>.
    /// </summary>
    private void ResolveInitialOverlaps()
    {
        for (int pass = 0; pass < _maxSlideIterations; pass++)
        {
            int overlapCount = _collider.Overlap(_contactFilter, _overlapColliders);
            bool resolvedAny = false;

            for (int index = 0; index < overlapCount; index++)
            {
                Collider2D other = _overlapColliders[index];

                if (other == null || other == _collider)
                {
                    continue;
                }

                ColliderDistance2D separation = _collider.Distance(other);

                if (!separation.isValid || !separation.isOverlapped)
                {
                    continue;
                }

                ApplyPosition(_rigidbody.position + GetSeparationStep(separation));
                resolvedAny = true;
            }

            if (!resolvedAny)
            {
                break;
            }
        }
    }

    private Vector2 GetSeparationStep(ColliderDistance2D separation)
    {
        // pointA lies on this collider and pointB on the other one; moving by
        // (pointB - pointA) brings the deepest penetrating point back to the surface.
        Vector2 correction = separation.pointB - separation.pointA;

        if (correction.sqrMagnitude <= MinimumMoveSqrMagnitude)
        {
            return correction;
        }

        return correction + correction.normalized * _skinWidth;
    }

    private void ApplyPosition(Vector2 position)
    {
        _rigidbody.position = position;
        transform.position = position;
    }

    private void ConfigureContactFilter()
    {
        _contactFilter.useLayerMask = true;
        _contactFilter.useTriggers = false;
        _contactFilter.SetLayerMask(_collisionMask);
    }

    private bool TryGetClosestHit(
        int hitCount,
        out RaycastHit2D closestHit)
    {
        closestHit = default;
        float closestDistance = float.PositiveInfinity;

        for (int index = 0; index < hitCount; index++)
        {
            RaycastHit2D hit = _castHits[index];

            if (hit.collider == null ||
                hit.distance >= closestDistance)
            {
                continue;
            }

            closestHit = hit;
            closestDistance = hit.distance;
        }

        return closestHit.collider != null;
    }

    private void CacheDependencies()
    {
        if (_rigidbody == null)
        {
            _rigidbody = GetComponent<Rigidbody2D>();
        }

        if (_collider == null)
        {
            _collider = GetComponent<Collider2D>();
        }
    }

    private void ValidateRigidbody()
    {
        if (_rigidbody == null)
        {
            Debug.LogError(
                $"{nameof(Kinematic2DMovementMotor)} requires " +
                $"{nameof(Rigidbody2D)}.",
                this);

            return;
        }

        if (_rigidbody.bodyType == RigidbodyType2D.Kinematic &&
            Mathf.Approximately(_rigidbody.gravityScale, 0f))
        {
            return;
        }

        Debug.LogWarning(
            $"{nameof(Kinematic2DMovementMotor)} expects a kinematic " +
            $"{nameof(Rigidbody2D)} with zero gravity.",
            this);
    }

#if UNITY_EDITOR
    private void Reset()
    {
        CacheDependencies();

        if (_rigidbody == null)
        {
            return;
        }

        _rigidbody.bodyType = RigidbodyType2D.Kinematic;
        _rigidbody.gravityScale = 0f;
        _rigidbody.interpolation = RigidbodyInterpolation2D.Interpolate;
        _rigidbody.constraints = RigidbodyConstraints2D.FreezeRotation;
    }

    private void OnValidate()
    {
        CacheDependencies();
        _skinWidth = Mathf.Max(0f, _skinWidth);
        _maxSlideIterations = Mathf.Clamp(
            _maxSlideIterations,
            1,
            CastHitCapacity);
    }
#endif
}
