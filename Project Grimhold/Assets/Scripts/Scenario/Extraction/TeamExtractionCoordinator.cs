using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// Authoritative network coordinator that maintains extraction countdowns and simultaneous completion for all teams.
/// Lives on <c>NetworkMatchController</c> at DefaultExecutionOrder(115).
/// Driven tick-by-tick under State Authority using <see cref="TeamExtractionRequirementRules"/>.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(115)]
public sealed class TeamExtractionCoordinator : NetworkBehaviour, ITeamExtractionCoordinator
{
    [SerializeField]
    private ExtractionConfig _config;

    [Networked, Capacity(RaidSessionRules.MaxTeams)]
    private NetworkArray<TeamExtractionSlot> _teamSlots { get; }

#if UNITY_EDITOR
    private TeamExtractionSlot[] _testSlots;
    internal bool _allowEditModeSimulation;
#endif

    private EntityRegistry _registry;
    private NetworkSpawnManager _spawnManager;
    private ExtractionSanctuaryAssignmentService _assignmentService;
    private RaidTeamRoster _roster;
    private EntityId _registeredId;
    private bool _isRegistered;
    private bool _isInitialized;

    private readonly List<TeamMemberExtractionSnapshot> _memberSnapshotsBuffer = new(4);
    private readonly List<PlayerExtractionController> _memberControllersBuffer = new(4);

    public new EntityId Id => Object != null && Object.IsValid
        ? new EntityId(unchecked((int)Object.Id.Raw))
        : default;

    public ExtractionConfig Config => _config;

    public override void Spawned()
    {
        _registry = Runner != null ? Runner.GetComponent<EntityRegistry>() : null;
        _spawnManager = Runner != null ? Runner.GetComponent<NetworkSpawnManager>() : null;
        _assignmentService = Runner != null ? Runner.GetComponent<ExtractionSanctuaryAssignmentService>() : null;
        RegisterCapability();

        if (HasStateAuthority && !HostMigrationRestoreUtility.IsRestoreSpawn(this))
        {
            if (_spawnManager != null && _spawnManager.TryGetRaidTeamRoster(out RaidTeamRoster roster))
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

    public void CopyStateFrom(TeamExtractionCoordinator source)
    {
        if (source == null)
        {
            return;
        }

        base.CopyStateFrom(source);
        _config = source._config;
    }

    public override void FixedUpdateNetwork()
    {
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

        if (!isSimulating)
        {
            return;
        }

        if (_config == null || !_config.TryValidate(out _))
        {
            return;
        }

        EnsureDependencies();

        if (_roster == null && _spawnManager != null && _spawnManager.TryGetRaidTeamRoster(out RaidTeamRoster roster))
        {
            TryInitializeFromRoster(roster);
        }

        int capacity = GetSlotCapacity();
        for (int i = 0; i < capacity; i++)
        {
            TeamExtractionSlot slot = GetSlot(i);
            if (slot.IsEmpty)
            {
                continue;
            }

            UpdateTeamExtraction(i, slot);
        }
    }

    /// <summary>
    /// Initializes extraction slots for all teams defined in the frozen launch roster.
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
            Debug.LogError($"[{nameof(TeamExtractionCoordinator)}] Cannot initialize from roster: missing or invalid ExtractionConfig.", this);
            return false;
        }

        int capacity = GetSlotCapacity();
        int teamCount = roster.TeamCount;
        for (int i = 0; i < capacity; i++)
        {
            if (i < teamCount)
            {
                RaidTeamId teamId = roster.Teams[i];
                SetSlot(i, new TeamExtractionSlot
                {
                    TeamIdRaw = teamId.Value,
                    ActiveZoneIdValue = 0,
                    CountdownTimer = TickTimer.None,
                    ActiveSignatureValue = 0
                });
            }
            else
            {
                SetSlot(i, default);
            }
        }

        _roster = roster;
        _isInitialized = true;
        return true;
    }

    /// <summary>
    /// Initializes a single team extraction slot directly (useful for tests or dynamic registration).
    /// </summary>
    public bool TryInitializeTeam(RaidTeamId teamId)
    {
        if (Runner != null && !HasStateAuthority)
        {
            return false;
        }

        if (!teamId.IsValid)
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

        SetSlot(emptyIndex, new TeamExtractionSlot
        {
            TeamIdRaw = teamId.Value,
            ActiveZoneIdValue = 0,
            CountdownTimer = TickTimer.None,
            ActiveSignatureValue = 0
        });

        return true;
    }

    public bool IsTeamExtracting(RaidTeamId teamId)
    {
        int idx = FindSlotIndex(teamId);
        return idx >= 0 && GetSlot(idx).IsActive;
    }

    public bool TryGetCountdownProgress(RaidTeamId teamId, out ExtractionCountdownSnapshot snapshot)
    {
        snapshot = default;

        int index = FindSlotIndex(teamId);
        if (index < 0)
        {
            return false;
        }

        TeamExtractionSlot slot = GetSlot(index);
        if (!slot.IsActive)
        {
            snapshot = ExtractionCountdownSnapshot.None();
            return true;
        }

        if (_config == null || slot.ActiveZoneId.Value == 0)
        {
            return false;
        }

        float totalSeconds = _config.CountdownDurationSeconds;
        float remainingSeconds = 0f;

        if (Runner != null)
        {
            float? remainingTime = slot.CountdownTimer.RemainingTime(Runner);
            if (!remainingTime.HasValue || float.IsNaN(remainingTime.Value) || float.IsInfinity(remainingTime.Value))
            {
                return false;
            }
            remainingSeconds = remainingTime.Value;
        }

        float progress = totalSeconds > 0f ? Mathf.Clamp01((totalSeconds - remainingSeconds) / totalSeconds) : 1f;
        snapshot = new ExtractionCountdownSnapshot(
            ExtractionState.InProgress,
            slot.ActiveZoneId,
            remainingSeconds,
            totalSeconds,
            progress);
        return true;
    }

    /// <summary>
    /// Attempts to query the count of required team members and how many are currently inside the sanctuary extraction zone and connected.
    /// </summary>
    public bool TryGetTeamPresence(RaidTeamId teamId, out int presentCount, out int requiredCount)
    {
        presentCount = 0;
        requiredCount = 0;

#if UNITY_EDITOR
        if (TeamPresenceOverrideForTest != null)
        {
            var testPresence = TeamPresenceOverrideForTest(teamId);
            if (testPresence.HasValue)
            {
                presentCount = testPresence.Value.present;
                requiredCount = testPresence.Value.required;
                return true;
            }
            return false;
        }
#endif

        if (!teamId.IsValid || _assignmentService == null || _registry == null)
        {
            return false;
        }

        SanctuaryAssignmentResult assignment = _assignmentService.TryGetAssignment(teamId);
        if (!assignment.Success || assignment.SanctuaryId.Value == 0)
        {
            return false;
        }

        if (!_registry.TryGetExtractionSanctuary(assignment.SanctuaryId, out IExtractionSanctuary sanctuary) ||
            sanctuary == null ||
            !_registry.TryGetExtractionZone(assignment.SanctuaryId, out IExtractionZone zone) ||
            zone == null)
        {
            return false;
        }

        List<TeamMemberExtractionSnapshot> snapshots = new List<TeamMemberExtractionSnapshot>(4);
        List<PlayerExtractionController> controllers = new List<PlayerExtractionController>(4);
        if (!TryBuildMemberSnapshots(teamId, zone, snapshots, controllers))
        {
            return false;
        }

        for (int i = 0; i < snapshots.Count; i++)
        {
            TeamMemberExtractionSnapshot member = snapshots[i];
            if (member.IsRequiredMember)
            {
                requiredCount++;
                if (member.IsInsideExact && member.IsConnected)
                {
                    presentCount++;
                }
            }
        }

        return requiredCount > 0;
    }

    private void UpdateTeamExtraction(int slotIndex, TeamExtractionSlot slot)
    {
        RaidTeamId teamId = slot.TeamId;
        if (!teamId.IsValid)
        {
            return;
        }

        // 1. Resolve team's assigned sanctuary
        if (_assignmentService == null)
        {
            if (slot.IsActive)
            {
                CancelTeamCountdown(slotIndex, slot, teamId);
            }
            return;
        }

        SanctuaryAssignmentResult assignment = _assignmentService.TryGetAssignment(teamId);
        if (!assignment.Success || assignment.SanctuaryId.Value == 0)
        {
            if (slot.IsActive)
            {
                CancelTeamCountdown(slotIndex, slot, teamId);
            }
            return;
        }

        EntityId sanctuaryId = assignment.SanctuaryId;

        // 2. Resolve sanctuary & zone
        if (_registry == null ||
            !_registry.TryGetExtractionSanctuary(sanctuaryId, out IExtractionSanctuary sanctuary) ||
            sanctuary == null ||
            !_registry.TryGetExtractionZone(sanctuaryId, out IExtractionZone zone) ||
            zone == null ||
            !zone.IsAvailable)
        {
            if (slot.IsActive)
            {
                CancelTeamCountdown(slotIndex, slot, teamId);
            }
            return;
        }

        // 3. Ritual completion check
        bool isRitualCompleted = sanctuary.CanUseExtraction(teamId);

        // 4. Gather member snapshots
        _memberSnapshotsBuffer.Clear();
        _memberControllersBuffer.Clear();

        if (!TryBuildMemberSnapshots(teamId, zone, _memberSnapshotsBuffer, _memberControllersBuffer))
        {
            if (slot.IsActive)
            {
                CancelTeamCountdown(slotIndex, slot, teamId);
            }
            return;
        }

        // 5. Evaluate start vs continuation
        if (!slot.IsActive)
        {
            TeamExtractionEvaluationResult startResult = TeamExtractionRequirementRules.EvaluateStart(isRitualCompleted, _memberSnapshotsBuffer);
            if (startResult.CanStart)
            {
                StartTeamCountdown(slotIndex, slot, teamId, sanctuaryId, startResult.Signature, _memberControllersBuffer);
            }
        }
        else
        {
            TeamExtractionEvaluationResult contResult = TeamExtractionRequirementRules.EvaluateContinuation(
                isRitualCompleted,
                slot.ActiveSignature,
                _memberSnapshotsBuffer);

            if (!contResult.ShouldContinue)
            {
                if (contResult.CancelReason == TeamExtractionCancelReason.CompositionChanged)
                {
                    // Immediate restart check with new composition
                    TeamExtractionEvaluationResult restartResult = TeamExtractionRequirementRules.EvaluateStart(isRitualCompleted, _memberSnapshotsBuffer);
                    if (restartResult.CanStart)
                    {
                        StartTeamCountdown(slotIndex, slot, teamId, sanctuaryId, restartResult.Signature, _memberControllersBuffer);
                        return;
                    }
                }

                CancelTeamCountdown(slotIndex, slot, teamId);
                return;
            }

            // Check countdown expiration
            bool expired;
            if (Runner != null)
            {
                expired = slot.CountdownTimer.Expired(Runner);
            }
            else
            {
#if UNITY_EDITOR
                expired = TimerExpiredOverrideForTest != null ? TimerExpiredOverrideForTest(teamId) : (slot.CountdownTimer.TargetTick.HasValue && slot.CountdownTimer.TargetTick.Value <= 0);
#else
                expired = false;
#endif
            }

            if (expired)
            {
                CompleteTeamExtraction(slotIndex, slot, teamId, _memberControllersBuffer);
            }
        }
    }

    private bool TryBuildMemberSnapshots(
        RaidTeamId teamId,
        IExtractionZone zone,
        List<TeamMemberExtractionSnapshot> snapshots,
        List<PlayerExtractionController> controllers)
    {
#if UNITY_EDITOR
        if (MemberSnapshotsProviderForTest != null)
        {
            return MemberSnapshotsProviderForTest(teamId, zone, snapshots, controllers);
        }
#endif
        IReadOnlyList<RaidParticipantId> members = null;
        if (_roster != null)
        {
            _roster.TryGetTeamMembers(teamId, out members);
        }

        if (members == null || members.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < members.Count; i++)
        {
            RaidParticipantId participantId = members[i];
            NetworkRaidParticipant participant = null;
            if (_spawnManager != null)
            {
                _spawnManager.TryGetRaidParticipant(participantId, out participant);
            }

            if (participant == null)
            {
                // Fail-closed: unresolvable participant is not connected
                snapshots.Add(new TeamMemberExtractionSnapshot(
                    participantId,
                    RaidParticipantState.Aborted,
                    isConnected: false,
                    isInsideExact: false,
                    isInsideTolerant: false));
                continue;
            }

            RaidParticipantState state = participant.State;

            // Fail-closed connectivity evaluation
            bool isConnected = participant.TryGetComponent(out RaidParticipantConnectivity connectivity) &&
                               !connectivity.IsDisconnected;

            bool isInsideExact = false;
            bool isInsideTolerant = false;
            PlayerExtractionController controller = null;

            if (participant.TryResolveCurrentAvatar(out NetworkObject avatar) && avatar != null)
            {
                Vector2 pos = avatar.transform.position;
                isInsideExact = zone.ContainsExact(pos);
                isInsideTolerant = zone.ContainsWithTolerance(pos, _config.BoundaryTolerance);
                controller = avatar.GetComponent<PlayerExtractionController>();
            }

            if (state == RaidParticipantState.Raiding && controller != null)
            {
                controllers.Add(controller);
            }

            snapshots.Add(new TeamMemberExtractionSnapshot(
                participantId,
                state,
                isConnected,
                isInsideExact,
                isInsideTolerant));
        }

        return true;
    }

    private void StartTeamCountdown(
        int slotIndex,
        TeamExtractionSlot slot,
        RaidTeamId teamId,
        EntityId zoneId,
        TeamCompositionSignature signature,
        List<PlayerExtractionController> controllers)
    {
        TickTimer timer;
        if (Runner != null)
        {
            timer = TickTimer.CreateFromSeconds(Runner, _config.CountdownDurationSeconds);
        }
        else
        {
#if UNITY_EDITOR
            timer = TickTimer.None; // Will be set explicitly by test or left zero
#else
            timer = TickTimer.None;
#endif
        }

        slot.ActiveZoneIdValue = zoneId.Value;
        slot.CountdownTimer = timer;
        slot.ActiveSignatureValue = signature.Value;
        SetSlot(slotIndex, slot);

        for (int i = 0; i < controllers.Count; i++)
        {
            controllers[i]?.StartExtractionCountdown(zoneId, timer);
        }
    }

    private void CancelTeamCountdown(int slotIndex, TeamExtractionSlot slot, RaidTeamId teamId)
    {
        slot.ActiveZoneIdValue = 0;
        slot.CountdownTimer = TickTimer.None;
        slot.ActiveSignatureValue = 0;
        SetSlot(slotIndex, slot);

        if (_roster != null && _roster.TryGetTeamMembers(teamId, out IReadOnlyList<RaidParticipantId> members))
        {
            for (int i = 0; i < members.Count; i++)
            {
                if (_spawnManager != null &&
                    _spawnManager.TryGetRaidParticipant(members[i], out NetworkRaidParticipant participant) &&
                    participant != null &&
                    participant.TryResolveCurrentAvatar(out NetworkObject avatar) &&
                    avatar != null)
                {
                    avatar.GetComponent<PlayerExtractionController>()?.CancelExtraction();
                }
            }
        }
    }

    private void CompleteTeamExtraction(
        int slotIndex,
        TeamExtractionSlot slot,
        RaidTeamId teamId,
        List<PlayerExtractionController> controllers)
    {
        // Clear active countdown slot
        slot.ActiveZoneIdValue = 0;
        slot.CountdownTimer = TickTimer.None;
        slot.ActiveSignatureValue = 0;
        SetSlot(slotIndex, slot);

        // Simultaneous completion for all required members in this exact tick
        for (int i = 0; i < controllers.Count; i++)
        {
            PlayerExtractionController controller = controllers[i];
            if (controller != null && controller.State == ExtractionState.InProgress)
            {
                controller.CompleteExtraction();
            }
        }
    }

    private void EnsureDependencies()
    {
        if (_registry == null && Runner != null)
        {
            _registry = Runner.GetComponent<EntityRegistry>();
        }

        if (_spawnManager == null && Runner != null)
        {
            _spawnManager = Runner.GetComponent<NetworkSpawnManager>();
        }

        if (_assignmentService == null && Runner != null)
        {
            _assignmentService = Runner.GetComponent<ExtractionSanctuaryAssignmentService>();
        }
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

    private TeamExtractionSlot GetSlot(int index)
    {
#if UNITY_EDITOR
        if (Object == null || !Object.IsValid)
        {
            if (_testSlots == null)
            {
                _testSlots = new TeamExtractionSlot[RaidSessionRules.MaxTeams];
            }
            return _testSlots[index];
        }
#endif
        return _teamSlots[index];
    }

    private void SetSlot(int index, TeamExtractionSlot slot)
    {
#if UNITY_EDITOR
        if (Object == null || !Object.IsValid)
        {
            if (_testSlots == null)
            {
                _testSlots = new TeamExtractionSlot[RaidSessionRules.MaxTeams];
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
        _isRegistered = _registry.TryRegisterTeamExtractionCoordinator(_registeredId, this);
        if (!_isRegistered)
        {
            _registeredId = default;
        }
    }

    private void UnregisterCapability()
    {
        if (_isRegistered && _registry != null)
        {
            _registry.TryUnregisterTeamExtractionCoordinator(_registeredId, this);
        }

        _registeredId = default;
        _isRegistered = false;
    }

#if UNITY_EDITOR
    internal void SetDependenciesForTest(
        EntityRegistry registry,
        NetworkSpawnManager spawnManager,
        ExtractionSanctuaryAssignmentService assignmentService,
        RaidTeamRoster roster,
        ExtractionConfig config)
    {
        _registry = registry;
        _spawnManager = spawnManager;
        _assignmentService = assignmentService;
        _roster = roster;
        _config = config;
        _allowEditModeSimulation = true;
    }

    internal void SetSlotForTest(int index, TeamExtractionSlot slot) => SetSlot(index, slot);
    internal TeamExtractionSlot GetSlotForTest(int index) => GetSlot(index);
    internal Func<RaidTeamId, IExtractionZone, List<TeamMemberExtractionSnapshot>, List<PlayerExtractionController>, bool> MemberSnapshotsProviderForTest { get; set; }
    internal Func<RaidTeamId, bool> TimerExpiredOverrideForTest { get; set; }
    internal Func<RaidTeamId, (int present, int required)?> TeamPresenceOverrideForTest { get; set; }
#endif
}
