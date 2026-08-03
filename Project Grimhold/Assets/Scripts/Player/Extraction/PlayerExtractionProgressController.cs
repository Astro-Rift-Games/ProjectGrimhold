using Fusion;
using UnityEngine;

/// <summary>
/// Owns one player's authoritative, individual progress toward the extraction quota.
/// It accepts direct contributions during Fusion simulation and exposes replicated state
/// for future sanctuary assignment and presentation systems.
/// </summary>
/// <remarks>
/// Producers own one-shot semantics. This receiver stores no source IDs, ticks, or contribution history.
/// See <c>Docs/Architecture/ExtractionArchitecture.md</c>.
/// </remarks>
[DisallowMultipleComponent]
public sealed class PlayerExtractionProgressController : NetworkBehaviour, IExtractionProgressReceiver
{
    [SerializeField]
    private ExtractionConfig _config;

    [SerializeField]
    private MonoBehaviour _characterSource;

    [SerializeField]
    private PlayerExtractionController _extractionController;

    private ICharacter _character;
    private EntityRegistry _registry;
    private EntityId _registeredId;
    private bool _isRegistered;
    private bool _dependenciesValid;

    [Networked]
    public int CurrentProgress { get; private set; }

    [Networked]
    public NetworkBool AssignmentRequested { get; private set; }

    public int Quota => _config != null ? _config.ProgressQuota : 0;

    public new EntityId Id => Object != null && Object.IsValid
        ? new EntityId(unchecked((int)Object.Id.Raw))
        : default;

    private void Awake()
    {
        CacheDependencies();
    }

    public override void Spawned()
    {
        CacheDependencies();
        _registry = Runner != null ? Runner.GetComponent<EntityRegistry>() : null;
        RegisterReceiver();
        _dependenciesValid = ValidateDependencies();

        if (HasStateAuthority)
        {
            CurrentProgress = 0;
            AssignmentRequested = false;
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        UnregisterReceiver();
        _dependenciesValid = false;
    }

    /// <summary>
    /// Applies progress only during the matching State Authority simulation tick.
    /// Multiple distinct contributions in the same tick are intentionally valid.
    /// </summary>
    public bool TryApplyContribution(in ExtractionProgressContribution contribution)
    {
        if (!HasStateAuthority || !_dependenciesValid || Runner == null || !Runner.IsSimulationUpdating)
        {
            return false;
        }

        if (contribution.SourceType == ExtractionProgressSourceType.None ||
            contribution.SourceId.Value == 0 || contribution.Amount <= 0 ||
            contribution.SimulationTick != Runner.Tick)
        {
            return false;
        }

        if (_character == null || !_character.IsAlive ||
            _extractionController == null || _extractionController.State == ExtractionState.Extracted)
        {
            return false;
        }

        int quota = Quota;
        if (quota <= 0 || CurrentProgress >= quota)
        {
            return false;
        }

        if (!ExtractionProgressRules.TryCalculateNext(
                CurrentProgress,
                quota,
                contribution.Amount,
                out int nextProgress,
                out bool completedQuota))
        {
            return false;
        }

        CurrentProgress = nextProgress;
        if (completedQuota)
        {
            AssignmentRequested = true;
        }

        return true;
    }

    /// <summary>
    /// Returns the current replicated progress and the local immutable quota configuration.
    /// </summary>
    public bool TryGetSnapshot(out ExtractionProgressSnapshot snapshot)
    {
        int quota = Quota;
        if (!_dependenciesValid || quota <= 0 || CurrentProgress < 0 || CurrentProgress > quota)
        {
            snapshot = default;
            return false;
        }

        snapshot = new ExtractionProgressSnapshot(CurrentProgress, quota, AssignmentRequested);
        return true;
    }

    private void CacheDependencies()
    {
        _character = _characterSource != null ? _characterSource as ICharacter : GetComponent<ICharacter>();
        if (_extractionController == null)
        {
            _extractionController = GetComponent<PlayerExtractionController>();
        }
    }

    private bool ValidateDependencies()
    {
        string configError = null;
        if (_config == null || !_config.TryValidate(out configError))
        {
            Debug.LogError($"{nameof(PlayerExtractionProgressController)} requires valid configuration. {configError}", this);
            return false;
        }

        if (_character == null || _extractionController == null || _registry == null || !_isRegistered)
        {
            Debug.LogError(
                $"{nameof(PlayerExtractionProgressController)} requires character, extraction controller, registry, and a valid progress registration.",
                this);
            return false;
        }

        return true;
    }

    private void RegisterReceiver()
    {
        if (_isRegistered || _registry == null || Id.Value == 0)
        {
            return;
        }

        _registeredId = Id;
        _isRegistered = _registry.TryRegisterExtractionProgressReceiver(_registeredId, this);
    }

    private void UnregisterReceiver()
    {
        if (!_isRegistered)
        {
            return;
        }

        _registry?.TryUnregisterExtractionProgressReceiver(_registeredId, this);
        _registeredId = default;
        _isRegistered = false;
    }

    private void OnDestroy()
    {
        UnregisterReceiver();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        CacheDependencies();
    }
#endif
}
