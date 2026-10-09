using Fusion;
using UnityEngine;

/// <summary>Owns the authoritative initialization of the productive avatar's ability slots.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-8)]
public sealed class PlayerAbilityRuntimeNetworkController : NetworkBehaviour
{
    [SerializeField] private RaidAvatarParticipantLink _participantLink;
    [SerializeField] private AbilityDefinitionCatalog _catalog;
    [SerializeField] private PlayerCharacter _playerCharacter;
    [SerializeField] private PlayerStaminaNetworkController _staminaController;
    [SerializeField] private PlayerManaNetworkController _manaController;
    [SerializeField] private PlayerMovementNetworkController _movementController;
    [SerializeField] private AbilityAreaTargetFinder _areaTargetFinder;
    [SerializeField] private AbilityExecutionBehaviour[] _executionBehaviours = System.Array.Empty<AbilityExecutionBehaviour>();

    [Networked] private NetworkBool InitializationConfirmed { get; set; }
    [Networked] private NetworkButtons PreviousAbilityButtons { get; set; }
    [Networked] private AbilityExecutionSnapshot Slot1Execution { get; set; }
    [Networked] private AbilityExecutionSnapshot Slot2Execution { get; set; }

    private AbilityRuntimeSlots _slots;
    private NetworkRaidParticipant _boundParticipant;
    private NetworkString<_32> _boundGeneration;
    private NetworkSpawnManager _spawnManager;
    private bool _spawned;
    private bool _restoreSpawn;
    private bool _ended;
    private bool _invalid;
    private bool _baselineAbilityInput;
    private bool _restoreInputBaselined;
    private NetworkButtons _activationRequests;
    private int _requestTick;
    private AbilityExecutionBehaviour _slot1Behaviour;
    private AbilityExecutionBehaviour _slot2Behaviour;
    private bool _rebindExecutions;
    private AbilityActivationFailure _slot1Failure;
    private AbilityActivationFailure _slot2Failure;

    /// <summary>True only for the confirmed, resolved runtime of the current productive avatar.</summary>
    public bool IsInitialized => isActiveAndEnabled && _spawned && Object != null && Object.IsValid &&
        InitializationConfirmed && !_ended && !_invalid && _slots != null &&
        TryResolveCurrentParticipant(out _);

    public override void Spawned()
    {
        _spawned = true;
        _restoreSpawn = HostMigrationRestoreUtility.IsRestoreSpawn(this);
        _baselineAbilityInput = _restoreSpawn;
        _restoreInputBaselined = false;
        _activationRequests = default;
        _spawnManager = Runner.GetComponent<NetworkSpawnManager>();
        _ended = false;
        _invalid = false;
        ClearLocalBinding();
        if (_participantLink == null || _catalog == null || _playerCharacter == null || _staminaController == null ||
            _manaController == null || _movementController == null || _areaTargetFinder == null ||
            !_areaTargetFinder.IsConfigured)
        {
            RejectConfiguration("Raid ability runtime requires participant, catalog, character, Stamina, Mana, movement and a configured area target finder.");
            return;
        }
        ValidateBehaviours();
    }

    public override void FixedUpdateNetwork()
    {
        _activationRequests = default;
        RefreshBinding();
        if (!isActiveAndEnabled || !HasStateAuthority || _ended || _invalid) return;
        if (IsInitialized && Runner.IsForward)
        {
            RebindExecutions();
            ProgressExecution(UniversalAbilitySlot.Slot1);
            ProgressExecution(UniversalAbilitySlot.Slot2);
        }
        if (!GetInput(out PlayerNetworkInput input)) return;

        NetworkButtons current = default;
        current.Set(PlayerInputButton.AbilitySlot1, input.Buttons.IsSet(PlayerInputButton.AbilitySlot1));
        current.Set(PlayerInputButton.AbilitySlot2, input.Buttons.IsSet(PlayerInputButton.AbilitySlot2));
        NetworkButtons previous = PreviousAbilityButtons;
        // Consume edges before availability so held input cannot queue for binding.
        PreviousAbilityButtons = current;
        if (_baselineAbilityInput || (_restoreSpawn && !_restoreInputBaselined))
        {
            _baselineAbilityInput = false;
            _restoreInputBaselined = true;
            return;
        }
        _requestTick = Runner.Tick.Raw;
        _activationRequests.Set(PlayerInputButton.AbilitySlot1,
            current.WasPressed(previous, PlayerInputButton.AbilitySlot1) && IsSlotAvailable(UniversalAbilitySlot.Slot1));
        _activationRequests.Set(PlayerInputButton.AbilitySlot2,
            current.WasPressed(previous, PlayerInputButton.AbilitySlot2) && IsSlotAvailable(UniversalAbilitySlot.Slot2));
        if (!Runner.IsForward) return;
        // Stable authoritative processing order, not cross-slot exclusivity.
        if (WasActivationRequested(UniversalAbilitySlot.Slot1)) _slot1Failure = TryStartExecution(UniversalAbilitySlot.Slot1);
        if (WasActivationRequested(UniversalAbilitySlot.Slot2)) _slot2Failure = TryStartExecution(UniversalAbilitySlot.Slot2);
    }

    public override void Render()
    {
        if (!HasStateAuthority)
        {
            RefreshBinding();
        }
    }

    public bool TryGetSlot(UniversalAbilitySlot slot, out AbilityRuntimeSlot state)
    {
        state = default;
        return IsInitialized && _slots.TryGetSlot(slot, out state);
    }

    /// <summary>Reports prepared-slot availability, not activation eligibility or affordability.</summary>
    public bool IsSlotAvailable(UniversalAbilitySlot slot) =>
        TryGetSlot(slot, out var state) && state.IsPrepared;

    /// <summary>Reads copied execution even while local bindings are disabled or awaiting restore fixup.</summary>
    public bool HasActiveExecution
    {
        get
        {
            if (!_spawned || Object == null || !Object.IsValid || _ended || _invalid || !InitializationConfirmed)
                return false;
            if (_participantLink != null && _participantLink.TryResolveParticipant(out var participant) &&
                (participant.State != RaidParticipantState.Raiding ||
                 (_boundGeneration.Length > 0 && (!participant.RaidGenerationId.Equals(_boundGeneration) ||
                                                 participant.CurrentAvatarId != Object.Id)))) return false;
            return Slot1Execution.IsActive || Slot2Execution.IsActive;
        }
    }

    public bool TryGetExecutionSnapshot(UniversalAbilitySlot slot, out AbilityExecutionSnapshot snapshot)
    {
        snapshot = default;
        if (!TryGetSlot(slot, out _)) return false;
        snapshot = ReadExecution(slot);
        return true;
    }

    public bool IsOnCooldown(UniversalAbilitySlot slot) =>
        TryGetExecutionSnapshot(slot, out var snapshot) && !snapshot.Cooldown.ExpiredOrNotRunning(Runner);

    public float GetRemainingCooldownSeconds(UniversalAbilitySlot slot) =>
        TryGetExecutionSnapshot(slot, out var snapshot) ? Mathf.Max(0f, snapshot.Cooldown.RemainingTime(Runner) ?? 0f) : 0f;

    /// <summary>Last local authoritative attempt, not a replicated presentation notification.</summary>
    public AbilityActivationFailure GetLastActivationFailure(UniversalAbilitySlot slot) => slot switch
    {
        UniversalAbilitySlot.Slot1 => _slot1Failure,
        UniversalAbilitySlot.Slot2 => _slot2Failure,
        _ => AbilityActivationFailure.PlayerUnavailable
    };

    /// <summary>Explicit simulation operation scoped to one accepted execution identity.</summary>
    public bool TryInterrupt(UniversalAbilitySlot slot, uint sequence, AbilityExecutionStopReason reason)
    {
        if (!HasStateAuthority || Runner == null || !Runner.IsSimulationUpdating || !Runner.IsForward ||
            reason == AbilityExecutionStopReason.Completed || reason > AbilityExecutionStopReason.ConfigurationUnavailable ||
            !TryGetExecutionSnapshot(slot, out var snapshot) ||
            !snapshot.IsActive || snapshot.Sequence != sequence) return false;
        var behaviour = ReadBehaviour(slot);
        bool mandatory = reason == AbilityExecutionStopReason.ParticipationEnded ||
                         reason == AbilityExecutionStopReason.ConfigurationUnavailable ||
                         reason == AbilityExecutionStopReason.Downed || reason == AbilityExecutionStopReason.Stun;
        if (!mandatory && (behaviour == null || !behaviour.CanInterrupt(reason))) return false;
        StopExecution(slot, reason);
        return true;
    }

    private AbilityActivationFailure TryStartExecution(UniversalAbilitySlot slot)
    {
        if (!TryBuildContext(slot, out var context) || !_playerCharacter.IsAlive ||
            PlayerDownedGate.IsDowned(_playerCharacter)) return AbilityActivationFailure.PlayerUnavailable;
        var match = _spawnManager != null ? _spawnManager.MatchController : null;
        if (match != null && match.Phase != NetworkMatchController.MatchPhase.InProgress)
            return AbilityActivationFailure.PlayerUnavailable;
        var behaviour = ReadBehaviour(slot);
        if (behaviour == null || !behaviour.isActiveAndEnabled) return AbilityActivationFailure.MissingBehaviour;
        if (!context.Definition.AreAttributeRequirementsSatisfiedBy(context.Attributes))
            return AbilityActivationFailure.RequirementsNotMet;
        var state = ReadExecution(slot);
        if (state.IsActive) return AbilityActivationFailure.AlreadyExecuting;
        if (state.Sequence == uint.MaxValue) return AbilityActivationFailure.PlayerUnavailable;
        if (!state.Cooldown.ExpiredOrNotRunning(Runner)) return AbilityActivationFailure.Cooldown;
        if (!behaviour.TryPlanStart(context, out var plan)) return AbilityActivationFailure.BehaviourRejected;
        if (!plan.IsValidStart) return AbilityActivationFailure.InvalidPlan;
        // Resolved before payment so an unusable direction rejects without cost, sequence or cooldown.
        if (!AbilityAimResolver.TryResolve(_movementController.FacingDirection, _movementController.AimDirection,
                out Vector2 aimDirection)) return AbilityActivationFailure.AimUnavailable;
        switch (context.Definition.Resource)
        {
            case AbilityResourceType.Stamina:
                if (!_staminaController.CanSpend(context.Definition.Cost) || !_staminaController.TrySpend(context.Definition.Cost))
                    return AbilityActivationFailure.InsufficientResource;
                break;
            case AbilityResourceType.Mana:
                if (!_manaController.IsInitialized) return AbilityActivationFailure.ResourceUnavailable;
                if (!_manaController.CanSpend(context.Definition.Cost) || !_manaController.TrySpend(context.Definition.Cost))
                    return AbilityActivationFailure.InsufficientResource;
                break;
            default:
                return AbilityActivationFailure.ResourceUnavailable;
        }

        state.Sequence++;
        state.Phase = plan.Phase;
        state.PhaseDeadline = CreateDeadline(plan.DurationSeconds);
        state.Cooldown = TickTimer.CreateFromSeconds(Runner, context.Definition.CooldownSeconds);
        state.AimDirection = aimDirection; // Captured once with the sequence; never re-read for this execution.
        WriteExecution(slot, state);
        PlayerReviveGate.InterruptIfReviving(_playerCharacter);
        behaviour.Begin(context, state);
        return AbilityActivationFailure.None;
    }

    private void ProgressExecution(UniversalAbilitySlot slot)
    {
        var state = ReadExecution(slot);
        if (!state.IsActive) return;
        if (!_playerCharacter.IsAlive || PlayerDownedGate.IsDowned(_playerCharacter))
        {
            StopExecution(slot, AbilityExecutionStopReason.Downed);
            return;
        }
        var behaviour = ReadBehaviour(slot);
        if (behaviour == null || !behaviour.isActiveAndEnabled)
        {
            StopExecution(slot, AbilityExecutionStopReason.ConfigurationUnavailable);
            return;
        }
        if (!TryBuildContext(slot, out var context)) return; // Temporary restore dependencies remain pending.
        var step = behaviour.Simulate(context, state);
        if (step.Phase == AbilityExecutionPhase.Idle)
        {
            StopExecution(slot, AbilityExecutionStopReason.Completed);
            return;
        }
        if (step.Phase == state.Phase) return; // Preserve original timer; never restart it each tick.
        if (!step.IsValidStart || state.Phase != AbilityExecutionPhase.Preparing ||
            step.Phase != AbilityExecutionPhase.Executing || !state.PhaseDeadline.Expired(Runner))
        {
            StopExecution(slot, AbilityExecutionStopReason.ConfigurationUnavailable);
            return;
        }
        state.Phase = step.Phase;
        state.PhaseDeadline = CreateDeadline(step.DurationSeconds);
        WriteExecution(slot, state);
    }

    private void StopExecution(UniversalAbilitySlot slot, AbilityExecutionStopReason reason)
    {
        var state = ReadExecution(slot);
        if (!state.IsActive) return;
        var stopped = state;
        state.Phase = AbilityExecutionPhase.Idle;
        state.PhaseDeadline = TickTimer.None;
        state.AimDirection = default; // The callback still receives the captured direction through `stopped`.
        WriteExecution(slot, state); // Commit before callback: no repeated completion/reentrant stop.
        var behaviour = ReadBehaviour(slot);
        // Stop must not depend on live slot binding: the cached behaviour survives a transient unresolve.
        if (behaviour != null && TryBuildStopContext(slot, behaviour, out var context)) behaviour.Stop(context, stopped, reason);
    }

    private bool TryBuildStopContext(UniversalAbilitySlot slot, AbilityExecutionBehaviour behaviour,
        out AbilityExecutionContext context)
    {
        context = default;
        if (behaviour.Definition == null || !_participantLink.TryGetCharacterAttributeState(out var attributes)) return false;
        context = new AbilityExecutionContext(Runner, _playerCharacter, slot, behaviour.Definition, attributes, _areaTargetFinder);
        return true;
    }

    private void RebindExecutions()
    {
        if (!_rebindExecutions) return;
        if (!_participantLink.TryGetCharacterAttributeState(out _)) return;
        _rebindExecutions = false;
        RebindExecution(UniversalAbilitySlot.Slot1);
        RebindExecution(UniversalAbilitySlot.Slot2);
    }

    private void RebindExecution(UniversalAbilitySlot slot)
    {
        var state = ReadExecution(slot);
        var behaviour = ReadBehaviour(slot);
        if (state.IsActive && behaviour != null && TryBuildContext(slot, out var context)) behaviour.Rebind(context, state);
    }

    private bool TryBuildContext(UniversalAbilitySlot slot, out AbilityExecutionContext context)
    {
        context = default;
        if (_slots == null || !_slots.TryGetSlot(slot, out var prepared) || !prepared.IsPrepared ||
            !_participantLink.TryGetCharacterAttributeState(out var attributes)) return false;
        context = new AbilityExecutionContext(Runner, _playerCharacter, slot, prepared.Definition, attributes, _areaTargetFinder);
        return true;
    }

    private TickTimer CreateDeadline(float seconds) => seconds > 0f ? TickTimer.CreateFromSeconds(Runner, seconds) : TickTimer.None;
    private AbilityExecutionSnapshot ReadExecution(UniversalAbilitySlot slot) =>
        slot == UniversalAbilitySlot.Slot1 ? Slot1Execution : Slot2Execution;
    private AbilityExecutionBehaviour ReadBehaviour(UniversalAbilitySlot slot) =>
        slot == UniversalAbilitySlot.Slot1 ? _slot1Behaviour : _slot2Behaviour;
    private void WriteExecution(UniversalAbilitySlot slot, in AbilityExecutionSnapshot state)
    {
        if (slot == UniversalAbilitySlot.Slot1) Slot1Execution = state;
        else Slot2Execution = state;
    }

    private void ValidateBehaviours()
    {
        if (_executionBehaviours == null)
        {
            RejectConfiguration("Raid ability execution bindings must be an explicit array; empty is supported.");
            return;
        }
        for (int index = 0; index < _executionBehaviours.Length; index++)
        {
            var behaviour = _executionBehaviours[index];
            if (behaviour == null || behaviour.gameObject != gameObject || !_catalog.TryGetId(behaviour.Definition, out _))
            {
                RejectConfiguration("Raid ability execution binding must reference an avatar behavior and canonical catalog definition.");
                return;
            }
            for (int previous = 0; previous < index; previous++)
            {
                if (_executionBehaviours[previous].Definition != behaviour.Definition) continue;
                RejectConfiguration("Raid ability execution bindings contain a duplicate definition.");
                return;
            }
        }
    }

    private AbilityExecutionBehaviour ResolveBehaviour(UniversalAbilitySlot slot)
    {
        if (!_slots.TryGetSlot(slot, out var prepared) || !prepared.IsPrepared) return null;
        foreach (var behaviour in _executionBehaviours)
            if (behaviour.Definition == prepared.Definition) return behaviour;
        return null;
    }

    /// <summary>Reports a slot intention only within the current authoritative simulation tick.</summary>
    public bool WasActivationRequested(UniversalAbilitySlot slot)
    {
        if (!isActiveAndEnabled || !_spawned || Object == null || !Object.IsValid ||
            !HasStateAuthority || !Runner.IsSimulationUpdating || _requestTick != Runner.Tick.Raw ||
            !IsSlotAvailable(slot)) return false;
        return slot switch
        {
            UniversalAbilitySlot.Slot1 => _activationRequests.IsSet(PlayerInputButton.AbilitySlot1),
            UniversalAbilitySlot.Slot2 => _activationRequests.IsSet(PlayerInputButton.AbilitySlot2),
            _ => false
        };
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        _spawned = false;
        ClearLocalBinding();
    }

    private void OnDisable()
    {
        _activationRequests = default;
        _baselineAbilityInput = true;
        _slots = null;
        _boundParticipant = null;
        _rebindExecutions = true;
    }
    private void OnDestroy() => ClearLocalBinding();

    private void RefreshBinding()
    {
        if (!isActiveAndEnabled || !_spawned || Object == null || !Object.IsValid || _ended || _invalid)
        {
            return;
        }

        NetworkMatchController match = _spawnManager != null ? _spawnManager.MatchController : null;
        if (match != null && (match.Phase == NetworkMatchController.MatchPhase.Closing ||
                              match.Phase == NetworkMatchController.MatchPhase.Finished))
        {
            EndParticipation();
            return;
        }

        if (_participantLink == null || !_participantLink.TryResolveParticipant(out var participant))
        {
            // Missing references may be temporary during restore fixup; never reset copied state.
            _slots = null;
            _rebindExecutions = true;
            return;
        }

        NetworkString<_32> generation = participant.RaidGenerationId;
        if (participant.State != RaidParticipantState.Raiding ||
            (_boundGeneration.Length > 0 && !_boundGeneration.Equals(generation)) ||
            (_boundGeneration.Length > 0 && participant.CurrentAvatarId != Object.Id))
        {
            EndParticipation();
            return;
        }

        if (generation.Length == 0 || participant.CurrentAvatarId != Object.Id)
        {
            _slots = null;
            return;
        }

        if (_slots != null && _boundParticipant == participant)
        {
            return;
        }

        if (!InitializationConfirmed && (!HasStateAuthority || _restoreSpawn))
        {
            return;
        }

        if (!participant.TryGetPreparedAbilityLoadout(out var prepared))
        {
            return;
        }

        if (!AbilityRuntimeSlots.TryCreate(prepared, _catalog, out var resolved, out string error))
        {
            RejectConfiguration(error);
            return;
        }

        _slots = resolved;
        _boundParticipant = participant;
        _boundGeneration = generation;
        _slot1Behaviour = ResolveBehaviour(UniversalAbilitySlot.Slot1);
        _slot2Behaviour = ResolveBehaviour(UniversalAbilitySlot.Slot2);
        _rebindExecutions = true;
        if (HasStateAuthority && !_restoreSpawn && !InitializationConfirmed)
        {
            InitializationConfirmed = true;
            Slot1Execution = default;
            Slot2Execution = default;
        }
    }

    private bool TryResolveCurrentParticipant(out NetworkRaidParticipant participant)
    {
        participant = null;
        return _participantLink != null && _participantLink.TryResolveParticipant(out participant) &&
            participant == _boundParticipant && participant.State == RaidParticipantState.Raiding &&
            participant.CurrentAvatarId == Object.Id &&
            participant.RaidGenerationId.Equals(_boundGeneration);
    }

    private void EndParticipation()
    {
        if (HasStateAuthority)
        {
            StopExecution(UniversalAbilitySlot.Slot1, AbilityExecutionStopReason.ParticipationEnded);
            StopExecution(UniversalAbilitySlot.Slot2, AbilityExecutionStopReason.ParticipationEnded);
            Slot1Execution = default;
            Slot2Execution = default;
            InitializationConfirmed = false;
            PreviousAbilityButtons = default;
        }
        _ended = true;
        ClearLocalBinding();
    }

    private void RejectConfiguration(string error)
    {
        _invalid = true;
        ClearLocalBinding();
        Debug.LogError(error, this);
    }

    private void ClearLocalBinding()
    {
        _slots = null;
        _boundParticipant = null;
        _boundGeneration = default;
    }
}
