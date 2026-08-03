using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// Physical extraction zone component attached to scene trigger volumes.
/// Manages spatial geometry boundary checks, participant broadphase overlap queries,
/// and networked availability state under State Authority.
/// </summary>
/// <remarks>
/// See <c>Docs/Architecture/ExtractionArchitecture.md</c> for subsystem boundaries and network authority rules.
/// </remarks>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
[DefaultExecutionOrder(100)]
public sealed class ExtractionZone : NetworkBehaviour, IExtractionZone
{
    private const int MaxBroadphaseCapacity = 16;

    [SerializeField]
    private Collider2D _zoneCollider;

    [SerializeField]
    private SpriteRenderer _spriteRenderer;

    [SerializeField]
    private bool _startsAvailable = true;

    [SerializeField]
    private LayerMask _participantMask;

    private static Sprite _fallbackSquareSprite;

    private EntityRegistry _entityRegistry;
    private bool _isRegistered;
    private bool _configurationValid;
    private bool _reportedBroadphaseSaturation;
    private ContactFilter2D _participantFilter;

    private readonly Collider2D[] _overlapBuffer = new Collider2D[MaxBroadphaseCapacity];
    private readonly HashSet<EntityId> _currentOccupants = new();
    private readonly HashSet<EntityId> _detectedThisTick = new();
    private readonly List<EntityId> _occupantsToRemove = new();

    /// <summary>
    /// Canonical entity identifier of the extraction zone.
    /// Derived from the underlying Fusion <see cref="NetworkObject"/> identifier.
    /// </summary>
    public new EntityId Id => Object != null ? new EntityId(unchecked((int)Object.Id.Raw)) : default;

    [Networked]
    private NetworkBool NetworkedIsAvailable { get; set; }

    /// <summary>
    /// Gets whether the extraction zone is currently available for extraction.
    /// </summary>
    public bool IsAvailable => Object != null && Object.IsValid
        ? NetworkedIsAvailable
        : _startsAvailable;

    private void Awake()
    {
        CacheCollider();
        ConfigureParticipantFilter();
        CacheRenderer();
        UpdateVisualPresentation();
    }

    public override void Render()
    {
        UpdateVisualPresentation();
    }

    public override void Spawned()
    {
        CacheCollider();
        ConfigureParticipantFilter();
        _configurationValid = ValidateConfiguration();

        if (HasStateAuthority)
        {
            NetworkedIsAvailable = _startsAvailable;
        }

        RegisterInRunner();
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        UnregisterFromRunners();
        _currentOccupants.Clear();
        _detectedThisTick.Clear();
        _occupantsToRemove.Clear();
        _reportedBroadphaseSaturation = false;
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || !_configurationValid || !_isRegistered || !IsAvailable || !IsColliderValid())
        {
            return;
        }

        PerformBroadphaseOverlap();
    }

    /// <summary>
    /// Evaluates whether a given point is strictly inside the physical zone collider.
    /// </summary>
    public bool ContainsExact(Vector2 point)
    {
        if (!IsPointValid(point) || !IsColliderValid())
        {
            return false;
        }

        return _zoneCollider.OverlapPoint(point);
    }

    /// <summary>
    /// Evaluates whether a given point is inside the zone collider or within an expanded boundary tolerance.
    /// </summary>
    public bool ContainsWithTolerance(Vector2 point, float tolerance)
    {
        if (!IsPointValid(point) || !IsColliderValid())
        {
            return false;
        }

        if (float.IsNaN(tolerance) || float.IsInfinity(tolerance) || tolerance < 0f)
        {
            return false;
        }

        if (tolerance == 0f)
        {
            return _zoneCollider.OverlapPoint(point);
        }

        if (_zoneCollider.OverlapPoint(point))
        {
            return true;
        }

        Vector2 closestPoint = _zoneCollider.ClosestPoint(point);
        float distanceSqr = (point - closestPoint).sqrMagnitude;
        return distanceSqr <= tolerance * tolerance;
    }

    /// <summary>
    /// Authoritatively sets the availability state of this extraction zone.
    /// Requires State Authority and is idempotent.
    /// </summary>
    public bool TrySetAvailability(bool available)
    {
        if (!HasStateAuthority)
        {
            return false;
        }

        if (!available && _entityRegistry != null &&
            _entityRegistry.TryGetExtractionSanctuary(Id, out IExtractionSanctuary sanctuary) &&
            sanctuary != null && sanctuary.RitualState == ExtractionRitualState.Completed)
        {
            return false;
        }

        if (NetworkedIsAvailable != available)
        {
            NetworkedIsAvailable = available;
            Debug.Log($"[ExtractionZone] {name} (ID: {Id.Value}) availability changed to: {available}.", this);
        }

        return true;
    }

    private void PerformBroadphaseOverlap()
    {
        _detectedThisTick.Clear();

        int count = Physics2D.OverlapCollider(_zoneCollider, _participantFilter, _overlapBuffer);
        if (count >= _overlapBuffer.Length)
        {
            if (!_reportedBroadphaseSaturation)
            {
                Debug.LogWarning(
                    $"{nameof(ExtractionZone)} on '{name}' filled its {_overlapBuffer.Length}-collider broadphase buffer. " +
                    "The scan is incomplete, so no entries or exits will be inferred.",
                    this);
                _reportedBroadphaseSaturation = true;
            }

            return;
        }

        _reportedBroadphaseSaturation = false;
        for (int i = 0; i < count; i++)
        {
            Collider2D hit = _overlapBuffer[i];
            if (hit == null ||
                !_entityRegistry.TryGetEntityId(hit, out EntityId participantId) ||
                !_entityRegistry.TryGetExtractionParticipant(participantId, out IExtractionParticipant participant))
            {
                continue;
            }

            if (!_detectedThisTick.Add(participantId))
            {
                continue;
            }

            if (ContainsExact(participant.ValidationPoint))
            {
                participant.TryBeginExtraction(Id);
            }
        }

        _occupantsToRemove.Clear();
        foreach (EntityId occupantId in _currentOccupants)
        {
            if (!_detectedThisTick.Contains(occupantId))
            {
                if (_entityRegistry.TryGetExtractionParticipant(occupantId, out IExtractionParticipant occupant))
                {
                    occupant.NotifyExtractionZoneExit(Id);
                }

                _occupantsToRemove.Add(occupantId);
            }
        }

        for (int i = 0; i < _occupantsToRemove.Count; i++)
        {
            _currentOccupants.Remove(_occupantsToRemove[i]);
        }

        foreach (EntityId participantId in _detectedThisTick)
        {
            _currentOccupants.Add(participantId);
        }
    }

    private void CacheCollider()
    {
        if (_zoneCollider == null)
        {
            _zoneCollider = GetComponent<Collider2D>();
        }
    }

    private void CacheRenderer()
    {
        if (_spriteRenderer == null)
        {
            _spriteRenderer = GetComponent<SpriteRenderer>() ?? GetComponentInChildren<SpriteRenderer>();
        }

        if (_spriteRenderer != null && _spriteRenderer.sprite == null)
        {
            if (_fallbackSquareSprite == null)
            {
                Texture2D texture = Texture2D.whiteTexture;
                _fallbackSquareSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 1f);
            }
            _spriteRenderer.sprite = _fallbackSquareSprite;
            _spriteRenderer.drawMode = SpriteDrawMode.Sliced;
        }
    }

    private void UpdateVisualPresentation()
    {
        if (_spriteRenderer == null)
        {
            return;
        }

        bool isAvailable = Application.isPlaying ? IsAvailable : _startsAvailable;
        Color targetColor = isAvailable ? new Color(0.2f, 0.9f, 0.4f, 0.35f) : new Color(0.9f, 0.2f, 0.2f, 0.35f);
        _spriteRenderer.color = targetColor;

        if (_zoneCollider is BoxCollider2D box)
        {
            _spriteRenderer.size = box.size;
        }
    }

    private bool IsColliderValid()
    {
        return _zoneCollider != null &&
            _zoneCollider.gameObject == gameObject &&
            _zoneCollider.enabled &&
            _zoneCollider.gameObject.activeInHierarchy;
    }

    private static bool IsPointValid(Vector2 point)
    {
        return !float.IsNaN(point.x) && !float.IsNaN(point.y) &&
               !float.IsInfinity(point.x) && !float.IsInfinity(point.y);
    }

    private void ConfigureParticipantFilter()
    {
        _participantFilter = new ContactFilter2D
        {
            useTriggers = true,
            useLayerMask = true,
            layerMask = _participantMask
        };
    }

    private bool ValidateConfiguration()
    {
        if (!IsColliderValid())
        {
            Debug.LogError($"{nameof(ExtractionZone)} requires an enabled collider on the same GameObject.", this);
            return false;
        }

        if (_participantMask.value == 0)
        {
            Debug.LogError($"{nameof(ExtractionZone)} requires a non-empty participant layer mask.", this);
            return false;
        }

        return true;
    }

    private void RegisterInRunner()
    {
        if (_isRegistered || Runner == null)
        {
            return;
        }

        _entityRegistry = Runner.GetComponent<EntityRegistry>();
        if (_entityRegistry == null)
        {
            Debug.LogError($"{nameof(ExtractionZone)} requires a runner-scoped {nameof(EntityRegistry)}.", this);
            return;
        }

        _isRegistered = _configurationValid && _entityRegistry.TryRegisterExtractionZone(Id, this);
        if (!_isRegistered)
        {
            Debug.LogError($"{nameof(ExtractionZone)} could not register zone ID {Id.Value}.", this);
        }
    }

    private void UnregisterFromRunners()
    {
        if (!_isRegistered)
        {
            return;
        }

        if (_entityRegistry != null)
        {
            _entityRegistry.TryUnregisterExtractionZone(Id, this);
        }

        _isRegistered = false;
    }

    private void OnDestroy()
    {
        UnregisterFromRunners();
        _currentOccupants.Clear();
        _detectedThisTick.Clear();
        _occupantsToRemove.Clear();
        _reportedBroadphaseSaturation = false;
    }

    [ContextMenu("Debug: Toggle Availability")]
    private void ToggleAvailabilityContextMenu()
    {
        if (Application.isPlaying && HasStateAuthority)
        {
            TrySetAvailability(!IsAvailable);
        }
        else if (Application.isPlaying)
        {
            Debug.LogWarning($"[ExtractionZone] {name}: Cannot toggle availability without State Authority.", this);
        }
    }

    private void OnDrawGizmos()
    {
        CacheCollider();
        if (_zoneCollider == null)
        {
            return;
        }

        Bounds bounds = _zoneCollider.bounds;
        bool isAvailable = Application.isPlaying ? IsAvailable : true;
        Color fill = isAvailable ? new Color(0.2f, 0.9f, 0.4f, 0.25f) : new Color(0.9f, 0.2f, 0.2f, 0.25f);
        Color wire = isAvailable ? new Color(0.2f, 0.9f, 0.4f, 0.9f) : new Color(0.9f, 0.2f, 0.2f, 0.9f);

        Gizmos.color = fill;
        Gizmos.DrawCube(bounds.center, bounds.size);
        Gizmos.color = wire;
        Gizmos.DrawWireCube(bounds.center, bounds.size);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        CacheCollider();
        ConfigureParticipantFilter();
        if (_spriteRenderer == null)
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
        }
    }
#endif
}
