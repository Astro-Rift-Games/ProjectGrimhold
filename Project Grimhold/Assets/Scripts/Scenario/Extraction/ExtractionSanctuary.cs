using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// Authoritative reservation and ritual endpoint for an extraction point.
/// Supports concurrent team reservations and rituals per decision D-3 and rules A-4 / D-5.
/// The co-located ExtractionZone shares this NetworkObject identity while retaining its
/// independent registry capability and geometry responsibility.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(ExtractionZone))]
[RequireComponent(typeof(Collider2D))]
[DefaultExecutionOrder(90)]
public sealed class ExtractionSanctuary : NetworkBehaviour, IExtractionSanctuary, IInteractable
{
    [SerializeField]
    private ExtractionConfig _config;

    [SerializeField]
    private ExtractionZone _extractionZone;

    [SerializeField]
    private Collider2D _interactionCollider;

    private EntityRegistry _registry;
    private ExtractionSanctuaryAssignmentService _assignmentService;
    private Collider2D[] _registeredColliders;
    private EntityId _registeredId;
    private bool _isRegistryRegistered;
    private bool _isServiceRegistered;
    private bool _isEntityRegistered;
    private bool _compositionValid;

    [Networked, Capacity(RaidSessionRules.MaxTeams)]
    private NetworkArray<TeamSanctuarySlot> _teamSlots { get; }

    private readonly TeamSanctuarySlot[] _editModeSlots = new TeamSanctuarySlot[RaidSessionRules.MaxTeams];
    private bool _allowEditModeSimulation;

    internal Func<RaidTeamId, int> RequiredMemberCountProviderForTest { get; set; }
    internal RaidTeamId OverrideTeamIdForTest { get; set; }

    public new EntityId Id => Object != null && Object.IsValid
        ? new EntityId(unchecked((int)Object.Id.Raw))
        : default;

    public bool IsReserved
    {
        get
        {
            int capacity = GetSlotCapacity();
            for (int i = 0; i < capacity; i++)
            {
                if (GetSlot(i).IsAssigned)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public bool IsAnyRitualCompleted
    {
        get
        {
            int capacity = GetSlotCapacity();
            for (int i = 0; i < capacity; i++)
            {
                TeamSanctuarySlot slot = GetSlot(i);
                if (slot.IsAssigned && slot.RitualState == ExtractionRitualState.Completed)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public EntityId OwnerId
    {
        get
        {
            int capacity = GetSlotCapacity();
            for (int i = 0; i < capacity; i++)
            {
                TeamSanctuarySlot slot = GetSlot(i);
                if (slot.IsAssigned)
                {
                    return new EntityId(slot.TeamIdRaw);
                }
            }

            return default;
        }
    }

    public ExtractionRitualState RitualState
    {
        get
        {
            int capacity = GetSlotCapacity();
            bool hasInProgress = false;
            bool hasCancelled = false;
            for (int i = 0; i < capacity; i++)
            {
                TeamSanctuarySlot slot = GetSlot(i);
                if (!slot.IsAssigned)
                {
                    continue;
                }

                if (slot.RitualState == ExtractionRitualState.Completed)
                {
                    return ExtractionRitualState.Completed;
                }

                if (slot.RitualState == ExtractionRitualState.InProgress)
                {
                    hasInProgress = true;
                }
                else if (slot.RitualState == ExtractionRitualState.Cancelled)
                {
                    hasCancelled = true;
                }
            }

            if (hasInProgress)
            {
                return ExtractionRitualState.InProgress;
            }

            if (hasCancelled)
            {
                return ExtractionRitualState.Cancelled;
            }

            return ExtractionRitualState.NotStarted;
        }
    }

    private void Awake()
    {
        CacheComposition();
    }

    public override void Spawned()
    {
        CacheComposition();
        _registry = Runner != null ? Runner.GetComponent<EntityRegistry>() : null;
        _assignmentService = Runner != null
            ? Runner.GetComponent<ExtractionSanctuaryAssignmentService>()
            : null;
        _registeredId = Id;

        if (HasStateAuthority && !HostMigrationRestoreUtility.IsRestoreSpawn(this))
        {
            int capacity = GetSlotCapacity();
            for (int i = 0; i < capacity; i++)
            {
                SetSlot(i, default);
            }

            _extractionZone?.TrySetAvailability(false);
        }

        if (!ValidateComposition())
        {
            Debug.LogError(
                $"{nameof(ExtractionSanctuary)} requires a valid shared identity, configuration, zone, collider, registry, and assignment service.",
                this);
            return;
        }

        _isRegistryRegistered = _registry.TryRegisterExtractionSanctuary(_registeredId, this);
        if (!_isRegistryRegistered)
        {
            Debug.LogError(
                $"{nameof(ExtractionSanctuary)} could not register Sanctuary {_registeredId.Value} in the runner registry.",
                this);
            return;
        }

        _isServiceRegistered = _assignmentService.TryRegisterSanctuary(_registeredId, this);
        if (!_isServiceRegistered)
        {
            Unregister();
            Debug.LogError(
                $"{nameof(ExtractionSanctuary)} could not register Sanctuary {_registeredId.Value} in the assignment service.",
                this);
            return;
        }

        _isEntityRegistered = _registry.TryRegisterEntity(_registeredId, this, _registeredColliders);
        if (!_isEntityRegistered)
        {
            Unregister();
            Debug.LogError(
                $"{nameof(ExtractionSanctuary)} could not register its interactable collider for {_registeredId.Value}.",
                this);
            return;
        }

        _compositionValid = true;
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority && !_allowEditModeSimulation)
        {
            return;
        }

        bool anyCompleted = false;
        int capacity = GetSlotCapacity();
        for (int i = 0; i < capacity; i++)
        {
            TeamSanctuarySlot slot = GetSlot(i);
            if (!slot.IsAssigned)
            {
                continue;
            }

            if (slot.RitualState == ExtractionRitualState.Completed)
            {
                anyCompleted = true;
                continue;
            }

            if (slot.RitualState != ExtractionRitualState.InProgress)
            {
                continue;
            }

            bool timerInvalid = !_allowEditModeSimulation && !slot.RitualTimer.IsRunning;
            if (!_compositionValid || _extractionZone == null || (!_allowEditModeSimulation && _extractionZone.Id != Id) || timerInvalid)
            {
                CancelRitual(i, slot);
                continue;
            }

            int requiredRemaining = CountRemainingRequiredMembers(slot.TeamId);
            if (TeamRitualRules.ShouldCancelRitual(requiredRemaining))
            {
                CancelRitual(i, slot);
                continue;
            }

            if (Runner != null && slot.RitualTimer.Expired(Runner))
            {
                CompleteRitual(i, slot);
                anyCompleted = true;
            }
        }

        if (anyCompleted && _compositionValid && _extractionZone != null)
        {
            _extractionZone.TrySetAvailability(true);
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        Unregister();
    }

    public bool IsAssignedToTeam(RaidTeamId teamId)
    {
        return FindSlotIndex(teamId) >= 0;
    }

    public bool CanUseExtraction(RaidTeamId teamId)
    {
        if (!_compositionValid || !teamId.IsValid)
        {
            return false;
        }

        int index = FindSlotIndex(teamId);
        if (index < 0)
        {
            return false;
        }

        return GetSlot(index).RitualState == ExtractionRitualState.Completed;
    }

    public bool TryAssignTeam(RaidTeamId teamId)
    {
        bool hasAuth = Runner == null || HasStateAuthority;
        if (!_compositionValid || !teamId.IsValid || !hasAuth)
        {
            Debug.LogWarning($"[{nameof(ExtractionSanctuary)} {name}] TryAssignTeam for team {teamId.Value} rejected: CompositionValid={_compositionValid}, IsValid={teamId.IsValid}, HasAuth={hasAuth}.");
            return false;
        }

        int existingIndex = FindSlotIndex(teamId);
        if (existingIndex >= 0)
        {
            return true;
        }

        int emptyIndex = FindEmptySlotIndex();
        if (emptyIndex < 0)
        {
            Debug.LogWarning($"[{nameof(ExtractionSanctuary)} {name}] TryAssignTeam for team {teamId.Value} rejected: No empty slot.");
            return false;
        }

        SetSlot(emptyIndex, new TeamSanctuarySlot
        {
            TeamIdRaw = teamId.Value,
            RitualState = ExtractionRitualState.NotStarted,
            RitualTimer = TickTimer.None
        });

        Debug.Log($"[{nameof(ExtractionSanctuary)} {name}] Team {teamId.Value} successfully assigned to slot {emptyIndex}!");
        return true;
    }

    public bool TryGetRitualProgress(RaidTeamId teamId, out ExtractionRitualSnapshot snapshot)
    {
        snapshot = default;
        if (_config == null || !_config.TryValidate(out _) || !teamId.IsValid)
        {
            return false;
        }

        int index = FindSlotIndex(teamId);
        if (index < 0)
        {
            return false;
        }

        TeamSanctuarySlot slot = GetSlot(index);
        float totalSeconds = _config.RitualDurationSeconds;
        switch (slot.RitualState)
        {
            case ExtractionRitualState.NotStarted:
                snapshot = new ExtractionRitualSnapshot(slot.RitualState, totalSeconds, totalSeconds, 0f);
                return true;
            case ExtractionRitualState.Completed:
                snapshot = new ExtractionRitualSnapshot(slot.RitualState, totalSeconds, 0f, 1f);
                return true;
            case ExtractionRitualState.Cancelled:
                snapshot = new ExtractionRitualSnapshot(slot.RitualState, totalSeconds, totalSeconds, 0f);
                return true;
            case ExtractionRitualState.InProgress:
                if (Runner == null || !slot.RitualTimer.IsRunning)
                {
#if UNITY_EDITOR
                    if (_allowEditModeSimulation)
                    {
                        snapshot = new ExtractionRitualSnapshot(slot.RitualState, totalSeconds, totalSeconds * 0.5f, 0.5f);
                        return true;
                    }
#endif
                    snapshot = default;
                    return false;
                }

                float? remaining = slot.RitualTimer.RemainingTime(Runner);
                if (!remaining.HasValue || float.IsNaN(remaining.Value) || float.IsInfinity(remaining.Value))
                {
                    snapshot = default;
                    return false;
                }

                float remainingSeconds = Mathf.Clamp(remaining.Value, 0f, totalSeconds);
                float progress = Mathf.Clamp01((totalSeconds - remainingSeconds) / totalSeconds);
                snapshot = new ExtractionRitualSnapshot(
                    slot.RitualState,
                    totalSeconds,
                    remainingSeconds,
                    progress);
                return true;
            default:
                snapshot = default;
                return false;
        }
    }

    public bool CanInteract(in InteractionRequest request)
    {
        if (!_compositionValid || request.TargetId != Id || request.InteractorId.Value == 0)
        {
            return false;
        }

        if (!TryResolveTeamId(request.InteractorId, out RaidTeamId teamId))
        {
            return false;
        }

        int slotIndex = FindSlotIndex(teamId);
        if (slotIndex < 0)
        {
            return false;
        }

        TeamSanctuarySlot slot = GetSlot(slotIndex);
        if (slot.RitualState != ExtractionRitualState.NotStarted)
        {
            return false;
        }

        if (_registry == null ||
            !_registry.TryGetCharacter(request.InteractorId, out ICharacter character) ||
            character == null)
        {
            return false;
        }

        bool isAlive = character.IsAlive;
        bool isDowned = PlayerDownedGate.IsDowned(character);
        bool isConnected = IsMemberConnected(request.InteractorId);

        return TeamRitualRules.CanInitiateRitual(true, isAlive, isDowned, isConnected);
    }

    public InteractionResult Interact(in InteractionRequest request)
    {
        bool hasAuth = Runner == null || HasStateAuthority;
        if (!hasAuth)
        {
            Debug.LogWarning($"[{nameof(ExtractionSanctuary)} {name}] Interact rejected: MissingStateAuthority.");
            return InteractionResult.Rejected(InteractionFailureReason.MissingStateAuthority);
        }

        if (!CanInteract(request))
        {
            Debug.LogWarning($"[{nameof(ExtractionSanctuary)} {name}] Interact rejected: CanInteract returned false for Interactor {request.InteractorId.Value}.");
            return InteractionResult.Rejected(InteractionFailureReason.TargetUnavailable);
        }

        if (!TryResolveTeamId(request.InteractorId, out RaidTeamId teamId))
        {
            Debug.LogWarning($"[{nameof(ExtractionSanctuary)} {name}] Interact rejected: Unable to resolve team for Interactor {request.InteractorId.Value}.");
            return InteractionResult.Rejected(InteractionFailureReason.TargetUnavailable);
        }

        int slotIndex = FindSlotIndex(teamId);
        if (slotIndex < 0)
        {
            Debug.LogWarning($"[{nameof(ExtractionSanctuary)} {name}] Interact rejected: Sanctuary is not assigned to team {teamId.Value}.");
            return InteractionResult.Rejected(InteractionFailureReason.TargetUnavailable);
        }

        TeamSanctuarySlot slot = GetSlot(slotIndex);
        TickTimer timer = Runner != null
            ? TickTimer.CreateFromSeconds(Runner, _config.RitualDurationSeconds)
            : TickTimer.None;

        slot.RitualTimer = timer;
        slot.RitualState = ExtractionRitualState.InProgress;
        SetSlot(slotIndex, slot);
        Debug.Log($"[{nameof(ExtractionSanctuary)} {name}] Ritual initiated for Team {teamId.Value}! Timer set for {_config.RitualDurationSeconds}s.");

        return InteractionResult.Succeeded(isConsumed: false);
    }

    // --- Legacy / compatibility helpers ---

    public bool IsOwnedBy(EntityId playerId)
    {
        if (!_compositionValid || playerId.Value == 0)
        {
            return false;
        }

        if (TryResolveTeamId(playerId, out RaidTeamId teamId))
        {
            return IsAssignedToTeam(teamId);
        }

        return false;
    }

    public bool CanUseExtraction(EntityId playerId)
    {
        if (!_compositionValid || playerId.Value == 0)
        {
            return false;
        }

        if (TryResolveTeamId(playerId, out RaidTeamId teamId))
        {
            return CanUseExtraction(teamId);
        }

        return false;
    }

    public bool TryGetRitualProgress(out ExtractionRitualSnapshot snapshot)
    {
        int capacity = GetSlotCapacity();
        for (int i = 0; i < capacity; i++)
        {
            TeamSanctuarySlot slot = GetSlot(i);
            if (slot.IsAssigned)
            {
                return TryGetRitualProgress(slot.TeamId, out snapshot);
            }
        }

        snapshot = default;
        return false;
    }

    public bool TryReserve(EntityId playerId)
    {
        if (playerId.Value == 0)
        {
            return false;
        }

        if (TryResolveTeamId(playerId, out RaidTeamId teamId))
        {
            return TryAssignTeam(teamId);
        }

        if (RaidTeamId.TryCreate(playerId.Value, out RaidTeamId fallbackTeam))
        {
            return TryAssignTeam(fallbackTeam);
        }

        return false;
    }

    // --- State & Slot management ---

    public void CopyStateFrom(ExtractionSanctuary source)
    {
        base.CopyStateFrom(source);
        if (source == null)
        {
            return;
        }

        int capacity = GetSlotCapacity();
        for (int i = 0; i < capacity; i++)
        {
            SetSlot(i, source.GetSlot(i));
        }
    }

    private void CompleteRitual(int slotIndex, TeamSanctuarySlot slot)
    {
        if (slot.RitualState != ExtractionRitualState.InProgress)
        {
            return;
        }

        slot.RitualState = ExtractionRitualState.Completed;
        slot.RitualTimer = TickTimer.None;
        SetSlot(slotIndex, slot);
        _extractionZone.TrySetAvailability(true);
    }

    private void CancelRitual(int slotIndex, TeamSanctuarySlot slot)
    {
        if (slot.RitualState != ExtractionRitualState.InProgress)
        {
            return;
        }

        slot.RitualState = ExtractionRitualState.Cancelled;
        slot.RitualTimer = TickTimer.None;
        SetSlot(slotIndex, slot);
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

    private int CountRemainingRequiredMembers(RaidTeamId teamId)
    {
        if (RequiredMemberCountProviderForTest != null)
        {
            return RequiredMemberCountProviderForTest(teamId);
        }

        if (Runner != null)
        {
            NetworkSpawnManager spawnManager = Runner.GetComponent<NetworkSpawnManager>();
            if (spawnManager != null && spawnManager.TryGetRaidTeamRoster(out RaidTeamRoster roster))
            {
                if (roster.TryGetTeamMembers(teamId, out IReadOnlyList<RaidParticipantId> members))
                {
                    int count = 0;
                    for (int i = 0; i < members.Count; i++)
                    {
                        if (spawnManager.TryGetRaidParticipant(members[i], out NetworkRaidParticipant participant) &&
                            participant != null &&
                            participant.State == RaidParticipantState.Raiding)
                        {
                            count++;
                        }
                    }

                    return count;
                }
            }
        }

        return 1;
    }

    private bool IsMemberConnected(EntityId interactorId)
    {
        if (_registry != null &&
            _registry.TryGetCharacter(interactorId, out ICharacter character) &&
            character is Component c)
        {
            if (c.TryGetComponent(out RaidAvatarParticipantLink link) &&
                link.TryResolveParticipant(out NetworkRaidParticipant participant) &&
                participant != null)
            {
                if (participant.TryGetComponent(out RaidParticipantConnectivity connectivity))
                {
                    return !connectivity.IsDisconnected;
                }
            }
        }

        return true;
    }

    private bool TryResolveTeamId(EntityId interactorId, out RaidTeamId teamId)
    {
        if (OverrideTeamIdForTest.IsValid)
        {
            teamId = OverrideTeamIdForTest;
            return true;
        }

        if (_registry != null &&
            _registry.TryGetCharacter(interactorId, out ICharacter character) &&
            character is Component c)
        {
            if (c.TryGetComponent(out RaidAvatarParticipantLink link) &&
                link.TryResolveParticipant(out NetworkRaidParticipant participant) &&
                participant != null &&
                participant.RaidParticipantId.IsValid)
            {
                if (Runner != null)
                {
                    NetworkSpawnManager spawnManager = Runner.GetComponent<NetworkSpawnManager>();
                    if (spawnManager != null &&
                        spawnManager.TryGetRaidInitialAffiliations(out RaidInitialAffiliationSnapshot affiliations) &&
                        affiliations.TryGetTeam(participant.RaidParticipantId, out teamId))
                    {
                        return true;
                    }
                }
            }
        }

        return RaidTeamId.TryCreate(1, out teamId);
    }

    private int GetSlotCapacity()
    {
        return Runner != null ? _teamSlots.Length : _editModeSlots.Length;
    }

    private TeamSanctuarySlot GetSlot(int index)
    {
        return Runner != null ? _teamSlots[index] : _editModeSlots[index];
    }

    private void SetSlot(int index, TeamSanctuarySlot slot)
    {
        if (Runner != null)
        {
            _teamSlots.Set(index, slot);
        }
        else
        {
            _editModeSlots[index] = slot;
        }
    }

    internal void SetAllowEditModeSimulation(bool allow)
    {
        _allowEditModeSimulation = allow;
    }

    private void CacheComposition()
    {
        if (_extractionZone == null)
        {
            _extractionZone = GetComponent<ExtractionZone>();
        }

        if (_interactionCollider == null)
        {
            _interactionCollider = GetComponent<Collider2D>();
        }

        if (_registeredColliders == null || _registeredColliders.Length != 1 ||
            _registeredColliders[0] != _interactionCollider)
        {
            _registeredColliders = _interactionCollider != null
                ? new[] { _interactionCollider }
                : null;
        }
    }

    private bool ValidateComposition()
    {
        return _config != null && _config.TryValidate(out _) &&
            _registry != null && _assignmentService != null && _registeredId.Value != 0 &&
            _extractionZone != null && _extractionZone.gameObject == gameObject && _extractionZone.Id == _registeredId &&
            _interactionCollider != null && _interactionCollider.gameObject == gameObject &&
            _registeredColliders != null && _registeredColliders.Length == 1;
    }

    private void Unregister()
    {
        if (_isEntityRegistered && _registry != null)
        {
            _registry.TryUnregisterEntity(_registeredId, this);
        }

        if (_isServiceRegistered && _assignmentService != null)
        {
            _assignmentService.TryUnregisterSanctuary(_registeredId, this);
        }

        if (_isRegistryRegistered && _registry != null)
        {
            _registry.TryUnregisterExtractionSanctuary(_registeredId, this);
        }

        _isEntityRegistered = false;
        _isServiceRegistered = false;
        _isRegistryRegistered = false;
        _compositionValid = false;
        _registeredId = default;
        _registeredColliders = null;
        _assignmentService = null;
        _registry = null;
    }

    private void OnDestroy()
    {
        Unregister();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        CacheComposition();
    }

    [ContextMenu("Debug: Log Sanctuary Status")]
    private void DebugLogSanctuaryStatus()
    {
        Debug.Log($"[{nameof(ExtractionSanctuary)} {name}] Id={Id.Value}, CompositionValid={_compositionValid}, HasStateAuth={HasStateAuthority}, SlotCapacity={GetSlotCapacity()}");
        int capacity = GetSlotCapacity();
        for (int i = 0; i < capacity; i++)
        {
            TeamSanctuarySlot slot = GetSlot(i);
            Debug.Log($"   Slot {i}: TeamIdRaw={slot.TeamIdRaw}, RitualState={slot.RitualState}, IsAssigned={slot.IsAssigned}");
        }
    }

    [ContextMenu("Debug: Check CanInteract With All Characters")]
    private void DebugCheckCanInteract()
    {
        EntityRegistry reg = Runner != null ? Runner.GetComponent<EntityRegistry>() : FindFirstObjectByType<EntityRegistry>();
        Debug.Log($"[{nameof(ExtractionSanctuary)} {name}] CanInteract Diagnostics: Id={Id.Value}, CompositionValid={_compositionValid}");
        if (reg == null)
        {
            Debug.LogWarning($"   EntityRegistry not found.");
            return;
        }

        for (int i = 0; i < GetSlotCapacity(); i++)
        {
            TeamSanctuarySlot s = GetSlot(i);
            Debug.Log($"   Slot [{i}]: TeamIdRaw={s.TeamIdRaw}, RitualState={s.RitualState}, IsAssigned={s.IsAssigned}");
        }

        CharacterBase[] characters = FindObjectsByType<CharacterBase>(FindObjectsSortMode.None);
        for (int i = 0; i < characters.Length; i++)
        {
            CharacterBase ch = characters[i];
            if (ch != null)
            {
                InteractionRequest req = new InteractionRequest(ch.Id, Id, 0);
                bool can = CanInteract(req);
                TryResolveTeamId(ch.Id, out RaidTeamId tId);
                int sIndex = FindSlotIndex(tId);
                Debug.Log($"   Character {ch.Id.Value} ({ch.name}): CanInteract={can}, ResolvedTeam={tId.Value}, SlotIndex={sIndex}, Alive={ch.IsAlive}, Downed={PlayerDownedGate.IsDowned(ch)}, Connected={IsMemberConnected(ch.Id)}");
            }
        }
    }
#endif
}
