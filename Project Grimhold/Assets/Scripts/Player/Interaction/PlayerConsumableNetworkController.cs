using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>Owns timed, authoritative consumable execution. Presentation observes replicated state.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(PlayerLootReceiver))]
public sealed class PlayerConsumableNetworkController : NetworkBehaviour
{
    [SerializeField] private LootDefinitionCatalog _lootCatalog;
    [SerializeField] private MonoBehaviour _characterSource;

    private ICharacter _character;
    private PlayerLootReceiver _lootReceiver;
    private NetworkMatchController _matchController;
    private PlayerAbilityRuntimeNetworkController _abilityRuntime;
    private PlayerCombatNetworkController _combat;
    private PlayerExtractionController _extraction;
    private bool _dependenciesValid;
    private bool _spawned;
    private uint _clientNextSequence = 1;
    private uint _clientInFlightSequence;
    private uint _pendingSequence;
    private int _pendingCatalogIndex;
    private readonly Queue<Confirmation> _confirmations = new();

    [Networked] private TickTimer ConsumptionTimer { get; set; }
    [Networked] private int ConsumptionCatalogIndex { get; set; }
    [Networked] private uint ConsumptionRequestSequence { get; set; }
    [Networked] private uint ServerLastProcessedSequence { get; set; }

    public event Action<LootId> ConsumeConfirmed;
    public event Action<ConsumableFailureReason> ConsumeRejected;
    public bool HasRequestInFlight => _clientInFlightSequence > 0;
    // Keep the gate closed through the completion tick, until the authoritative transaction clears it.
    public bool IsConsuming => _spawned && Object != null && Object.IsValid && ConsumptionTimer.IsRunning;

    private void Awake() => CacheDependencies();

    public override void Spawned()
    {
        CacheDependencies();
        _dependenciesValid = ValidateDependencies();
        _matchController = Runner.GetComponent<NetworkMatchController>();
        _spawned = true;
        _clientNextSequence = ServerLastProcessedSequence + 1;
        _clientInFlightSequence = 0;
        _pendingSequence = 0;
        _confirmations.Clear();
        // Copied execution and deadlines are deliberately not reset during restoration.
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        _spawned = false;
        _clientInFlightSequence = 0;
        _pendingSequence = 0;
        _confirmations.Clear();
    }

    public bool TryGetConsumingDefinition(out LootDefinition definition)
    {
        definition = null;
        return IsConsuming && _lootCatalog != null &&
            _lootCatalog.TryGetByIndex(ConsumptionCatalogIndex, out definition);
    }

    public bool TryRequestConsume(LootId lootId)
    {
        if (!_spawned || !HasInputAuthority || !_dependenciesValid || !IsGameplayPhaseActive()) return false;
        if (PlayerDownedGate.IsDowned(_character))
        {
            _confirmations.Enqueue(new Confirmation(-1, false, ConsumableFailureReason.Downed));
            return false;
        }
        if (HasRequestInFlight || IsConsuming)
        {
            _confirmations.Enqueue(new Confirmation(-1, false, ConsumableFailureReason.ConsumptionInProgress));
            return false;
        }
        if (_lootCatalog == null || !_lootCatalog.TryGetIndex(lootId, out int catalogIndex)) return false;

        uint sequence = _clientNextSequence++;
        _clientInFlightSequence = sequence;
        RpcInvokeInfo info = RPC_RequestConsume(catalogIndex, sequence);
        if (info.SendMessageResult == RpcSendMessageResult.Sent ||
            HasStateAuthority && info.LocalInvokeResult == RpcLocalInvokeResult.Invoked) return true;
        _clientInFlightSequence = 0;
        return false;
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || !Runner.IsForward || !_dependenciesValid) return;

        if (IsConsuming)
        {
            if (!IsGameplayPhaseActive() || _character == null || !_character.IsAlive ||
                PlayerDownedGate.IsDowned(_character) ||
                _extraction != null && _extraction.State == ExtractionState.Extracted)
            {
                CancelConsumption();
            }
            else if (ConsumptionTimer.Expired(Runner))
            {
                uint sequence = ConsumptionRequestSequence;
                int index = ConsumptionCatalogIndex;
                // Mark terminal before resolving so a confirmation cannot execute the transaction twice.
                ConsumptionTimer = TickTimer.None;
                ConsumableResult result = ProcessAuthoritativeConsume(index);
                RPC_ReceiveConsumeConfirmation(sequence, index, result.Success, (int)result.FailureReason);
            }
        }

        if (_pendingSequence == 0) return;
        uint pending = _pendingSequence;
        int catalogIndex = _pendingCatalogIndex;
        _pendingSequence = 0;
        ConsumableFailureReason failure = ValidateStart(catalogIndex, out LootDefinition definition);
        if (failure != ConsumableFailureReason.None)
        {
            RPC_ReceiveConsumeConfirmation(pending, catalogIndex, false, (int)failure);
            return;
        }

        PlayerReviveGate.InterruptIfReviving(_character);
        ConsumptionCatalogIndex = catalogIndex;
        ConsumptionRequestSequence = pending;
        ConsumptionTimer = TickTimer.CreateFromSeconds(Runner, definition.ConsumableDefinition.ConsumptionDurationSeconds);
    }

    /// <summary>Called directly by authoritative damage resolution, never by a presentation event.</summary>
    public void CancelConsumption()
    {
        if (!_spawned || !HasStateAuthority || Runner == null || !Runner.IsForward || !IsConsuming) return;
        uint sequence = ConsumptionRequestSequence;
        int index = ConsumptionCatalogIndex;
        ConsumptionTimer = TickTimer.None;
        RPC_ReceiveConsumeConfirmation(sequence, index, false, (int)ConsumableFailureReason.Interrupted);
    }

    private bool IsGameplayPhaseActive() => _matchController == null ||
        _matchController.Phase == NetworkMatchController.MatchPhase.InProgress;

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority, InvokeLocal = true,
        HostMode = RpcHostMode.SourceIsHostPlayer)]
    private RpcInvokeInfo RPC_RequestConsume(int catalogIndex, uint requestSequence, RpcInfo info = default)
    {
        if (!HasStateAuthority || info.Source != Object.InputAuthority || requestSequence == 0 ||
            requestSequence <= ServerLastProcessedSequence) return default;
        ServerLastProcessedSequence = requestSequence;
        if (_pendingSequence != 0 || IsConsuming)
        {
            RPC_ReceiveConsumeConfirmation(requestSequence, catalogIndex, false,
                (int)ConsumableFailureReason.ConsumptionInProgress);
            return default;
        }
        _pendingSequence = requestSequence;
        _pendingCatalogIndex = catalogIndex;
        return default;
    }

    private ConsumableFailureReason ValidateStart(int catalogIndex, out LootDefinition definition)
    {
        definition = null;
        if (!_dependenciesValid || !IsGameplayPhaseActive()) return ConsumableFailureReason.TargetUnavailable;
        if (_character == null || !_character.IsAlive) return ConsumableFailureReason.TargetDead;
        if (PlayerDownedGate.IsDowned(_character)) return ConsumableFailureReason.Downed;
        if (_abilityRuntime != null && _abilityRuntime.HasActiveExecution) return ConsumableFailureReason.AbilityInProgress;
        if (IsConsuming) return ConsumableFailureReason.ConsumptionInProgress;
        if (_extraction != null && _extraction.State == ExtractionState.Extracted) return ConsumableFailureReason.TargetUnavailable;
        if (_combat != null && _combat.HasPendingAttackRelease) return ConsumableFailureReason.TargetUnavailable;
        if (_lootCatalog == null || !_lootCatalog.TryGetByIndex(catalogIndex, out definition) ||
            definition.ConsumableDefinition == null || !definition.ConsumableDefinition.TryValidate(out _))
            return ConsumableFailureReason.InvalidLoot;
        return _lootReceiver.GetLootAmount(definition.LootId) > 0
            ? ConsumableFailureReason.None : ConsumableFailureReason.InsufficientAmount;
    }

    // Retains the existing completion seam for deterministic integration tests.
    private ConsumableResult ProcessAuthoritativeConsume(int catalogIndex)
    {
        ConsumableFailureReason failure = ValidateStart(catalogIndex, out LootDefinition definition);
        if (failure != ConsumableFailureReason.None) return ConsumableResult.Rejected(failure);
        var request = new LootTransferRequest(_character.Id, _character.Id, definition.LootId, 1, Runner.Tick);
        // Resolve every fallible inventory/provenance condition BEFORE applying the irreversible effect.
        if (_lootReceiver.ValidateExtraction(request) != LootTransferFailureReason.None ||
            !_lootReceiver.TryResolveRaidLootOriginTransfer(request, RaidLootOriginSelectionOrder.PlayerFirst,
                out RaidLootOriginTransfer origin))
            return ConsumableResult.Rejected(ConsumableFailureReason.InsufficientAmount);
        if (!definition.ConsumableDefinition.Effect.TryApplyEffect(_character, out string reason))
        {
            failure = reason switch
            {
                "El objetivo está muerto." => ConsumableFailureReason.TargetDead,
                "La salud ya está al máximo." => ConsumableFailureReason.HealthFull,
                "No hay autoridad para curar." => ConsumableFailureReason.MissingAuthority,
                _ => ConsumableFailureReason.EffectFailed
            };
            return ConsumableResult.Rejected(failure);
        }
        _lootReceiver.CommitRaidLootExtraction(request, origin);
        return ConsumableResult.Ok();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    private void RPC_ReceiveConsumeConfirmation(uint sequence, int index, bool success, int failure)
    {
        if (sequence != _clientInFlightSequence) return;
        _clientInFlightSequence = 0;
        _confirmations.Enqueue(new Confirmation(index, success, (ConsumableFailureReason)failure));
    }

    public override void Render()
    {
        while (_confirmations.Count > 0)
        {
            Confirmation confirmation = _confirmations.Dequeue();
            if (confirmation.Success && _lootCatalog.TryGetByIndex(confirmation.Index, out LootDefinition definition))
                ConsumeConfirmed?.Invoke(definition.LootId);
            else if (!confirmation.Success) ConsumeRejected?.Invoke(confirmation.Failure);
        }
    }

    private void CacheDependencies()
    {
        _character = _characterSource != null ? _characterSource as ICharacter : GetComponent<ICharacter>();
        _lootReceiver = GetComponent<PlayerLootReceiver>();
        _abilityRuntime = GetComponent<PlayerAbilityRuntimeNetworkController>();
        _combat = GetComponent<PlayerCombatNetworkController>();
        _extraction = GetComponent<PlayerExtractionController>();
    }

    private bool ValidateDependencies()
    {
        if (_character != null && _lootReceiver != null && _lootCatalog != null) return true;
        Debug.LogError($"{nameof(PlayerConsumableNetworkController)} requires character, loot receiver and catalog.", this);
        return false;
    }

    private readonly struct Confirmation
    {
        public readonly int Index;
        public readonly bool Success;
        public readonly ConsumableFailureReason Failure;
        public Confirmation(int index, bool success, ConsumableFailureReason failure)
        { Index = index; Success = success; Failure = failure; }
    }

#if UNITY_EDITOR
    private void OnValidate() => CacheDependencies();
#endif
}
