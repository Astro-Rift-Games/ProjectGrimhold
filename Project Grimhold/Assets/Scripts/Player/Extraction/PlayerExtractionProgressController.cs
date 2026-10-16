using Fusion;
using UnityEngine;

/// <summary>
/// Forwarder controller for player extraction progress.
/// Validates player vitality, simulation tick, and state authority, then forwards
/// contributions to the authoritative <see cref="ITeamExtractionProgress"/> pool of the player's team.
/// </summary>
/// <remarks>
/// Producers own one-shot semantics. This controller stores no individual progress state on the network.
/// Intermediate state (Etapa 3): Until Etapa 4 introduces team sanctuary assignment, completing the team quota
/// triggers individual sanctuary assignment for the player who completed it via <see cref="_assignmentService"/>.
/// See <c>Docs/Architecture/ExtractionArchitecture.md</c>.
/// </remarks>
[DisallowMultipleComponent]
public sealed class PlayerExtractionProgressController : NetworkBehaviour, IExtractionProgressReceiver, IExtractionProgressReader
{
    [SerializeField]
    private ExtractionConfig _config;

    [SerializeField]
    private MonoBehaviour _characterSource;

    [SerializeField]
    private PlayerExtractionController _extractionController;

    private ICharacter _character;
    private EntityRegistry _registry;
    private ITeamExtractionProgress _teamProgress;
    private RaidTeamId _overrideTeamId;
    private EntityId _registeredId;
    private bool _isReceiverRegistered;
    private bool _isReaderRegistered;
    private bool _dependenciesValid;

    public int CurrentProgress
    {
        get
        {
            if (TryGetSnapshot(out ExtractionProgressSnapshot snapshot))
            {
                return snapshot.CurrentProgress;
            }

            return 0;
        }
    }

    public NetworkBool AssignmentRequested
    {
        get
        {
            if (TryGetSnapshot(out ExtractionProgressSnapshot snapshot))
            {
                return snapshot.AssignmentRequested;
            }

            return false;
        }
    }

    public int Quota
    {
        get
        {
            if (TryResolveTeamId(out RaidTeamId teamId) &&
                TryResolveTeamProgress(out ITeamExtractionProgress teamProgress) &&
                teamProgress.TryGetProgress(teamId, out _, out int quota))
            {
                return quota;
            }

            return _config != null ? _config.SoloProgressQuota : 0;
        }
    }

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
        RegisterCapabilities();
        _dependenciesValid = ValidateDependencies();
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        UnregisterCapabilities();
        _dependenciesValid = false;
        _teamProgress = null;
    }

    /// <summary>
    /// Validates local player eligibility and forwards progress to the team's shared quota pool.
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

        if (!TryResolveTeamId(out RaidTeamId teamId))
        {
            return false;
        }

        if (!TryResolveTeamProgress(out ITeamExtractionProgress teamProgress))
        {
            Debug.LogError($"[{nameof(PlayerExtractionProgressController)}] Cannot apply contribution: ITeamExtractionProgress is not available.", this);
            return false;
        }

        if (!teamProgress.TryApplyContribution(teamId, contribution.Amount, out int nextProgress, out bool completedQuota))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Obtains the current team extraction progress snapshot projected from the team pool.
    /// </summary>
    public bool TryGetSnapshot(out ExtractionProgressSnapshot snapshot)
    {
        if (!_dependenciesValid || !TryResolveTeamId(out RaidTeamId teamId))
        {
            snapshot = default;
            return false;
        }

        if (TryResolveTeamProgress(out ITeamExtractionProgress teamProgress))
        {
            return teamProgress.TryGetSnapshot(teamId, out snapshot);
        }

        snapshot = default;
        return false;
    }

    private bool TryResolveTeamProgress(out ITeamExtractionProgress teamProgress)
    {
        if (_teamProgress != null)
        {
            teamProgress = _teamProgress;
            return true;
        }

        if (_registry != null && _registry.TryGetTeamExtractionProgress(out _teamProgress) && _teamProgress != null)
        {
            teamProgress = _teamProgress;
            return true;
        }

        if (Runner != null)
        {
            _teamProgress = Runner.GetComponent<ITeamExtractionProgress>();
            if (_teamProgress != null)
            {
                teamProgress = _teamProgress;
                return true;
            }
        }

        teamProgress = null;
        return false;
    }

    private bool TryResolveTeamId(out RaidTeamId teamId)
    {
        if (_overrideTeamId.IsValid)
        {
            teamId = _overrideTeamId;
            return true;
        }

        if (Runner != null)
        {
            NetworkSpawnManager spawnManager = Runner.GetComponent<NetworkSpawnManager>();
            if (spawnManager != null &&
                spawnManager.TryGetRaidInitialAffiliations(out RaidInitialAffiliationSnapshot affiliations) &&
                TryGetComponent(out RaidAvatarParticipantLink link) &&
                link.TryResolveParticipant(out NetworkRaidParticipant participant) &&
                participant != null &&
                participant.RaidParticipantId.IsValid &&
                affiliations.TryGetTeam(participant.RaidParticipantId, out teamId))
            {
                return true;
            }
        }

        // Fallback to default Team 1 for test environments or unassigned standalone avatars
        return RaidTeamId.TryCreate(1, out teamId);
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

        if (_character == null || _extractionController == null || _registry == null ||
            !_isReceiverRegistered || !_isReaderRegistered)
        {
            Debug.LogError(
                $"{nameof(PlayerExtractionProgressController)} requires character, extraction controller, registry, " +
                "and valid receiver/reader registrations.",
                this);
            return false;
        }

        return true;
    }

    private void RegisterCapabilities()
    {
        if (_isReceiverRegistered || _isReaderRegistered || _registry == null || Id.Value == 0)
        {
            return;
        }

        _registeredId = Id;
        _isReceiverRegistered = _registry.TryRegisterExtractionProgressReceiver(_registeredId, this);
        if (!_isReceiverRegistered)
        {
            _registeredId = default;
            return;
        }

        _isReaderRegistered = _registry.TryRegisterExtractionProgressReader(_registeredId, this);
        if (_isReaderRegistered)
        {
            return;
        }

        _registry.TryUnregisterExtractionProgressReceiver(_registeredId, this);
        _isReceiverRegistered = false;
        _registeredId = default;
    }

    private void UnregisterCapabilities()
    {
        if (_isReaderRegistered)
        {
            _registry?.TryUnregisterExtractionProgressReader(_registeredId, this);
        }

        if (_isReceiverRegistered)
        {
            _registry?.TryUnregisterExtractionProgressReceiver(_registeredId, this);
        }

        _registeredId = default;
        _isReaderRegistered = false;
        _isReceiverRegistered = false;
    }

    private void OnDestroy()
    {
        UnregisterCapabilities();
    }

    internal void SetTeamProgressForTesting(ITeamExtractionProgress teamProgress)
    {
        _teamProgress = teamProgress;
    }

    internal void SetTeamIdOverrideForTesting(RaidTeamId teamId)
    {
        _overrideTeamId = teamId;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        CacheDependencies();
    }
#endif
}
