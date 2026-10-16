using System;
using Fusion;
using UnityEngine;

/// <summary>
/// Authoritative network coordinator that maintains extraction progress pools for all teams in the raid.
/// Lives on <c>NetworkMatchController</c> as a runner-level capability.
/// Only the State Authority writes to team progress slots.
/// </summary>
[DisallowMultipleComponent]
public sealed class TeamExtractionProgress : NetworkBehaviour, ITeamExtractionProgress
{
    [SerializeField]
    private ExtractionConfig _config;

    [Networked, Capacity(RaidSessionRules.MaxTeams)]
    private NetworkArray<TeamProgressSlot> _teamSlots { get; }

#if UNITY_EDITOR
    private TeamProgressSlot[] _testSlots;
    internal bool _allowEditModeSimulation;
#endif

    private EntityRegistry _registry;
    private ExtractionSanctuaryAssignmentService _assignmentService;
    private EntityId _registeredId;
    private bool _isRegistered;
    private bool _isInitialized;

    internal void SetAssignmentServiceForTest(ExtractionSanctuaryAssignmentService assignmentService)
    {
        _assignmentService = assignmentService;
    }

    public new EntityId Id => Object != null && Object.IsValid
        ? new EntityId(unchecked((int)Object.Id.Raw))
        : default;

    public ExtractionConfig Config => _config;

    public override void Spawned()
    {
        _registry = Runner != null ? Runner.GetComponent<EntityRegistry>() : null;
        _assignmentService = Runner != null ? Runner.GetComponent<ExtractionSanctuaryAssignmentService>() : null;
        RegisterCapability();

        if (HasStateAuthority && !HostMigrationRestoreUtility.IsRestoreSpawn(this))
        {
            NetworkSpawnManager spawnManager = Runner != null ? Runner.GetComponent<NetworkSpawnManager>() : null;
            if (spawnManager != null && spawnManager.TryGetRaidTeamRoster(out RaidTeamRoster roster))
            {
                TryInitializeFromRoster(roster);
            }
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        UnregisterCapability();
    }

    private void OnDestroy()
    {
        UnregisterCapability();
    }

    public void CopyStateFrom(TeamExtractionProgress source)
    {
        if (source == null)
        {
            return;
        }

        base.CopyStateFrom(source);
        _config = source._config;
    }

    /// <summary>
    /// Initializes progress slots for all teams defined in the frozen cohort roster.
    /// </summary>
    public bool TryInitializeFromRoster(RaidTeamRoster roster)
    {
        if (Runner != null && !HasStateAuthority)
        {
            return false;
        }

        if (_isInitialized)
        {
            return true;
        }

        if (roster == null)
        {
            return false;
        }

        if (_config == null || !_config.TryValidate(out _))
        {
            Debug.LogError($"[{nameof(TeamExtractionProgress)}] Cannot initialize from roster: missing or invalid ExtractionConfig.", this);
            return false;
        }

        int capacity = GetSlotCapacity();
        int teamCount = roster.TeamCount;
        for (int i = 0; i < capacity; i++)
        {
            if (i < teamCount)
            {
                RaidTeamId teamId = roster.Teams[i];
                int initialTeamSize = roster.GetInitialTeamSize(teamId);
                SetSlot(i, new TeamProgressSlot
                {
                    TeamIdRaw = teamId.Value,
                    CurrentProgress = 0,
                    AssignmentRequested = false,
                    InitialTeamSize = initialTeamSize
                });
            }
            else
            {
                SetSlot(i, default);
            }
        }

        _isInitialized = true;
        return true;
    }

    /// <summary>
    /// Initializes a single team slot directly (useful for tests or dynamic registration).
    /// </summary>
    public bool TryInitializeTeam(RaidTeamId teamId, int initialTeamSize)
    {
        if (Runner != null && !HasStateAuthority)
        {
            return false;
        }

        if (!teamId.IsValid || initialTeamSize < 1 || initialTeamSize > 2)
        {
            return false;
        }

        if (_config == null || !_config.TryValidate(out _))
        {
            return false;
        }

        if (FindSlotIndex(teamId) >= 0)
        {
            return true; // Already initialized
        }

        int emptyIndex = FindEmptySlotIndex();
        if (emptyIndex < 0)
        {
            return false;
        }

        SetSlot(emptyIndex, new TeamProgressSlot
        {
            TeamIdRaw = teamId.Value,
            CurrentProgress = 0,
            AssignmentRequested = false,
            InitialTeamSize = initialTeamSize
        });

        return true;
    }

    public bool IsAssignedToTeam(RaidTeamId teamId) => FindSlotIndex(teamId) >= 0;

    /// <summary>
    /// Queries the team's current progress and derived quota.
    /// </summary>
    public bool TryGetProgress(RaidTeamId teamId, out int currentProgress, out int quota)
    {
        currentProgress = 0;
        quota = 0;

        int index = FindSlotIndex(teamId);
        if (index < 0)
        {
            return false;
        }

        TeamProgressSlot slot = GetSlot(index);
        if (!ExtractionTeamQuotaRules.TryGetQuota(slot.InitialTeamSize, _config, out quota))
        {
            return false;
        }

        currentProgress = slot.CurrentProgress;
        return true;
    }

    /// <summary>
    /// Builds a read-only snapshot of the team's extraction progress.
    /// </summary>
    public bool TryGetSnapshot(RaidTeamId teamId, out ExtractionProgressSnapshot snapshot)
    {
        snapshot = default;

        int index = FindSlotIndex(teamId);
        if (index < 0)
        {
            return false;
        }

        TeamProgressSlot slot = GetSlot(index);
        if (!ExtractionTeamQuotaRules.TryGetQuota(slot.InitialTeamSize, _config, out int quota))
        {
            return false;
        }

        snapshot = new ExtractionProgressSnapshot(
            slot.CurrentProgress,
            quota,
            (bool)slot.AssignmentRequested);
        return true;
    }

    /// <summary>
    /// Applies a contribution toward the team's shared quota pool.
    /// </summary>
    public bool TryApplyContribution(RaidTeamId teamId, long amount, out int nextProgress, out bool completedQuota)
    {
        nextProgress = 0;
        completedQuota = false;

        bool isSimulating;
        if (Runner != null)
        {
            isSimulating = HasStateAuthority && Runner.IsSimulationUpdating;
        }
        else
        {
#if UNITY_EDITOR
            isSimulating = _allowEditModeSimulation;
#else
            isSimulating = false;
#endif
        }

        if (!isSimulating || amount <= 0)
        {
            return false;
        }

        int index = FindSlotIndex(teamId);
        if (index < 0)
        {
            return false;
        }

        TeamProgressSlot slot = GetSlot(index);
        if (!ExtractionTeamQuotaRules.TryGetQuota(slot.InitialTeamSize, _config, out int quota))
        {
            return false;
        }

        if (slot.CurrentProgress >= quota)
        {
            nextProgress = slot.CurrentProgress;
            return false;
        }

        if (!ExtractionProgressRules.TryCalculateNext(
                slot.CurrentProgress,
                quota,
                amount,
                out nextProgress,
                out completedQuota))
        {
            return false;
        }

        slot.CurrentProgress = nextProgress;
        if (completedQuota)
        {
            slot.AssignmentRequested = true;
        }

        SetSlot(index, slot);

        if (completedQuota)
        {
            if (_assignmentService == null && Runner != null)
            {
                _assignmentService = Runner.GetComponent<ExtractionSanctuaryAssignmentService>();
            }

            _assignmentService?.TryAssign(teamId);
        }

        return true;
    }

    private int FindSlotIndex(RaidTeamId teamId)
    {
        if (!teamId.IsValid)
        {
            return -1;
        }

        int targetRaw = teamId.Value;
        int capacity = GetSlotCapacity();
        for (int i = 0; i < capacity; i++)
        {
            if (GetSlot(i).TeamIdRaw == targetRaw)
            {
                return i;
            }
        }

        return -1;
    }

    private int FindEmptySlotIndex()
    {
        int capacity = GetSlotCapacity();
        for (int i = 0; i < capacity; i++)
        {
            if (GetSlot(i).IsEmpty)
            {
                return i;
            }
        }

        return -1;
    }

    private int GetSlotCapacity()
    {
#if UNITY_EDITOR
        if (Object == null || !Object.IsValid)
        {
            return RaidSessionRules.MaxTeams;
        }
#endif
        return _teamSlots.Length;
    }

    private TeamProgressSlot GetSlot(int index)
    {
#if UNITY_EDITOR
        if (Object == null || !Object.IsValid)
        {
            if (_testSlots == null)
            {
                _testSlots = new TeamProgressSlot[RaidSessionRules.MaxTeams];
            }
            return _testSlots[index];
        }
#endif
        return _teamSlots[index];
    }

    private void SetSlot(int index, TeamProgressSlot slot)
    {
#if UNITY_EDITOR
        if (Object == null || !Object.IsValid)
        {
            if (_testSlots == null)
            {
                _testSlots = new TeamProgressSlot[RaidSessionRules.MaxTeams];
            }
            _testSlots[index] = slot;
            return;
        }
#endif
        _teamSlots.Set(index, slot);
    }

    private void RegisterCapability()
    {
        if (_isRegistered || _registry == null || Id.Value == 0)
        {
            return;
        }

        _registeredId = Id;
        _isRegistered = _registry.TryRegisterTeamExtractionProgress(_registeredId, this);
        if (!_isRegistered)
        {
            _registeredId = default;
        }
    }

    private void UnregisterCapability()
    {
        if (_isRegistered && _registry != null)
        {
            _registry.TryUnregisterTeamExtractionProgress(_registeredId, this);
        }

        _registeredId = default;
        _isRegistered = false;
    }

    internal void SetConfigForTesting(ExtractionConfig config)
    {
        _config = config;
    }
}
