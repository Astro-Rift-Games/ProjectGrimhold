using Fusion;
using UnityEngine;

/// <summary>
/// Projects the current local player's confirmed world state into the local minimap.
/// This component owns presentation references only; assignment, ritual and extraction
/// state remain owned by their existing network contracts.
/// </summary>
[DisallowMultipleComponent]
public sealed class RaidMinimapPresenter : MonoBehaviour
{
    [SerializeField]
    private RaidMinimapView _view;

    [SerializeField]
    [Min(0f)]
    [Tooltip("Local RectTransform units per world unit at the Canvas reference resolution; not physical monitor pixels.")]
    private float _uiUnitsPerWorldUnit = 4f;

    [SerializeField]
    [Tooltip("World-space position of the static Dungeon_Graybox prefab instance. This converts the generated layout's prefab-local pivot to the active Gameplay scene.")]
    private Vector2 _layoutWorldOriginOffset;

    [SerializeField]
    [Min(0f)]
    private float _zoom = 1f;

    [SerializeField]
    [Min(0f)]
    private float _markerInnerMargin = 8f;

    [SerializeField]
    private float _arrowBaseAngleCorrection;

    [SerializeField]
    [Min(0f)]
    private float _pulseFrequency = 1.5f;

    [SerializeField]
    private Color _assignedColor = new Color(0.2f, 0.65f, 1f, 1f);

    [SerializeField]
    private Color _ritualInProgressColor = new Color(0.2f, 0.9f, 1f, 1f);

    [SerializeField]
    private Color _ritualCancelledColor = new Color(0.65f, 0.25f, 0.25f, 1f);

    [SerializeField]
    private Color _sanctuaryEnabledColor = new Color(0.2f, 0.9f, 0.4f, 1f);

    private NetworkRunner _runner;
    private NetworkObject _playerObject;
    private Transform _playerTransform;
    private PlayerExtractionController _extractionController;
    private ExtractionSanctuaryAssignmentService _assignmentService;
    private EntityRegistry _entityRegistry;
    private IExtractionSanctuary _cachedSanctuary;
    private Transform _cachedSanctuaryTransform;
    private EntityId _cachedSanctuaryId;
    private bool _isBound;
    private bool _configurationValid;
    private bool _configurationDiagnosticReported;

    /// <summary>
    /// Binds the minimap to one Input Authority player and one runner session.
    /// Missing assignment infrastructure affects only Sanctuary presentation.
    /// </summary>
    public void Bind(
        NetworkRunner runner,
        NetworkObject playerObject,
        Transform playerTransform,
        PlayerExtractionController extractionController,
        ExtractionSanctuaryAssignmentService assignmentService,
        EntityRegistry entityRegistry)
    {
        Unbind();
        _runner = runner;
        _playerObject = playerObject;
        _playerTransform = playerTransform;
        _extractionController = extractionController;
        _assignmentService = assignmentService;
        _entityRegistry = entityRegistry;
        _isBound = true;
        string error = null;
        _configurationValid = _view != null;
        if (!_configurationValid)
        {
            error = "RaidMinimapPresenter has no RaidMinimapView reference.";
        }

        if (_configurationValid && !_view.TryValidateConfiguration(out error))
        {
            _configurationValid = false;
        }

        if (_configurationValid && !TryValidatePresenterConfiguration(out error))
        {
            _configurationValid = false;
        }

        if (_configurationValid && !_view.TryConfigureMap(_uiUnitsPerWorldUnit, _zoom, out error))
        {
            _configurationValid = false;
        }
        if (!_configurationValid && !_configurationDiagnosticReported)
        {
            Debug.LogError(
                $"{nameof(RaidMinimapPresenter)} configuration is invalid: {error}.",
                this);
            _configurationDiagnosticReported = true;
        }

        ClearPresentation();
    }

    /// <summary>Clears the current runner binding and every cached Sanctuary reference.</summary>
    public void Unbind()
    {
        ClearSanctuaryCache();
        _runner = null;
        _playerObject = null;
        _playerTransform = null;
        _extractionController = null;
        _assignmentService = null;
        _entityRegistry = null;
        _isBound = false;
        _configurationValid = false;
        ClearPresentation();
    }

    private void OnEnable()
    {
        if (!_isBound)
        {
            return;
        }

        ClearSanctuaryCache();
        ClearPresentation();
    }

    private void OnDisable()
    {
        ClearSanctuaryCache();
        ClearPresentation();
    }

    private void OnDestroy()
    {
        Unbind();
    }

    private void LateUpdate()
    {
        if (!_isBound || !_configurationValid || _view == null)
        {
            return;
        }

        if (!TryValidateLocalBinding(out EntityId localPlayerId))
        {
            ClearSanctuaryCache();
            ClearPresentation();
            return;
        }

        Vector2 mapPivotWorldPosition = _view.MapPivotWorldPosition + _layoutWorldOriginOffset;
        if (!MinimapProjection.TryProjectMapOffset(
                mapPivotWorldPosition,
                _playerTransform.position,
                _uiUnitsPerWorldUnit,
                _zoom,
                out Vector2 mapPosition))
        {
            ClearSanctuaryCache();
            ClearPresentation();
            return;
        }

        _view.ShowMinimap();
        _view.PresentMapPosition(mapPosition);
        _view.PresentLocalMarker();

        if (!TryGetCurrentExtraction(out ExtractionCountdownSnapshot extraction) ||
            extraction.State == ExtractionState.Extracted)
        {
            _view.HideSanctuary();
            return;
        }

        if (!TryResolveOrValidateSanctuary(localPlayerId) ||
            !_cachedSanctuary.TryGetRitualProgress(out ExtractionRitualSnapshot ritual))
        {
            _view.HideSanctuary();
            return;
        }

        MinimapProjectionResult projection = MinimapProjection.ProjectMarker(
            _playerTransform.position,
            _cachedSanctuaryTransform.position,
            _uiUnitsPerWorldUnit,
            _zoom,
            _view.ViewportSize,
            _view.SanctuaryMarkerSize,
            _markerInnerMargin);
        if (!projection.IsValid)
        {
            _view.HideSanctuary();
            return;
        }

        GetRitualVisual(ritual.State, out Color color, out float pulseScale);
        if (projection.IsClampedToEdge)
        {
            _view.PresentSanctuaryArrow(
                projection.Position,
                projection.AngleDegrees,
                _arrowBaseAngleCorrection,
                color,
                pulseScale);
        }
        else
        {
            _view.PresentSanctuaryIcon(projection.Position, color, pulseScale);
        }
    }

    private bool TryValidateLocalBinding(out EntityId localPlayerId)
    {
        localPlayerId = default;
        if (_runner == null || !_runner.IsRunning || _playerObject == null || !_playerObject.IsValid ||
            _playerTransform == null || _runner.LocalPlayer.IsNone ||
            _playerObject.InputAuthority != _runner.LocalPlayer)
        {
            return false;
        }

        if (_playerObject.TryGetBehaviour(out RaidAvatarParticipantLink participantLink))
        {
            if (!participantLink.TryResolveParticipant(out NetworkRaidParticipant participant) ||
                !participant.TryResolveCurrentAvatar(out NetworkObject currentAvatar) ||
                !ReferenceEquals(currentAvatar, _playerObject))
            {
                return false;
            }
        }
        else if (!_runner.TryGetPlayerObject(_runner.LocalPlayer, out NetworkObject legacyPlayer) ||
                 !ReferenceEquals(legacyPlayer, _playerObject))
        {
            return false;
        }

        localPlayerId = new EntityId(unchecked((int)_playerObject.Id.Raw));
        return localPlayerId.Value != 0;
    }

    private bool TryGetCurrentExtraction(out ExtractionCountdownSnapshot snapshot)
    {
        snapshot = default;
        return _extractionController != null &&
            _extractionController.Object != null &&
            _extractionController.Object.IsValid &&
            _extractionController.TryGetProgress(out snapshot);
    }

    private bool TryResolveOrValidateSanctuary(EntityId localPlayerId)
    {
        if (_assignmentService == null || _entityRegistry == null)
        {
            ClearSanctuaryCache();
            return false;
        }

        if (_cachedSanctuary != null)
        {
            if (_entityRegistry.TryGetExtractionSanctuary(
                    _cachedSanctuaryId,
                    out IExtractionSanctuary registered) &&
                ReferenceEquals(registered, _cachedSanctuary) &&
                registered.Id == _cachedSanctuaryId &&
                registered.IsOwnedBy(localPlayerId) &&
                _cachedSanctuaryTransform != null &&
                _cachedSanctuaryTransform.gameObject != null)
            {
                return true;
            }

            ClearSanctuaryCache();
            return false;
        }

        SanctuaryAssignmentResult assignment = _assignmentService.TryGetAssignment(localPlayerId);
        if (!assignment.Success || assignment.PlayerId != localPlayerId ||
            assignment.SanctuaryId.Value == 0 ||
            !_entityRegistry.TryGetExtractionSanctuary(
                assignment.SanctuaryId,
                out IExtractionSanctuary sanctuary) ||
            sanctuary == null || sanctuary.Id != assignment.SanctuaryId ||
            !sanctuary.IsOwnedBy(localPlayerId) ||
            !(sanctuary is Component sanctuaryComponent) ||
            sanctuaryComponent.transform == null)
        {
            return false;
        }

        _cachedSanctuary = sanctuary;
        _cachedSanctuaryId = assignment.SanctuaryId;
        _cachedSanctuaryTransform = sanctuaryComponent.transform;
        return true;
    }

    private bool TryValidatePresenterConfiguration(out string error)
    {
        if (!IsPositiveFinite(_uiUnitsPerWorldUnit) || !IsFinite(_layoutWorldOriginOffset) ||
            !IsPositiveFinite(_zoom) ||
            !IsFinite(_markerInnerMargin) ||
            _markerInnerMargin < 0f || !IsFinite(_arrowBaseAngleCorrection) ||
            !IsFinite(_pulseFrequency) || _pulseFrequency < 0f)
        {
            error = "RaidMinimapPresenter has invalid projection or visual configuration.";
            return false;
        }

        error = null;
        return true;
    }

    private void ClearSanctuaryCache()
    {
        _cachedSanctuary = null;
        _cachedSanctuaryTransform = null;
        _cachedSanctuaryId = default;
    }

    private void ClearPresentation()
    {
        _view?.Clear();
    }

    private void GetRitualVisual(ExtractionRitualState state, out Color color, out float scale)
    {
        color = _assignedColor;
        scale = 1f;
        switch (state)
        {
            case ExtractionRitualState.InProgress:
                color = _ritualInProgressColor;
                float phase = _pulseFrequency > 0f
                    ? (Mathf.Sin(Time.unscaledTime * _pulseFrequency * Mathf.PI * 2f) + 1f) * 0.5f
                    : 0f;
                scale = Mathf.Lerp(0.9f, 1.1f, phase);
                break;
            case ExtractionRitualState.Cancelled:
                color = _ritualCancelledColor;
                break;
            case ExtractionRitualState.Completed:
                color = _sanctuaryEnabledColor;
                break;
        }
    }

    private static bool IsPositiveFinite(float value)
    {
        return IsFinite(value) && value > 0f;
    }

    private static bool IsFinite(Vector2 value)
    {
        return IsFinite(value.x) && IsFinite(value.y);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        _uiUnitsPerWorldUnit = Mathf.Max(0f, _uiUnitsPerWorldUnit);
        _zoom = Mathf.Max(0f, _zoom);
        _markerInnerMargin = Mathf.Max(0f, _markerInnerMargin);
        _pulseFrequency = Mathf.Max(0f, _pulseFrequency);
        if (!TryValidatePresenterConfiguration(out string error))
        {
            Debug.LogError($"{nameof(RaidMinimapPresenter)}: {error}.", this);
        }
    }
#endif
}
