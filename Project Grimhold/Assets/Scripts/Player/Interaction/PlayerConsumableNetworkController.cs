using System;
using Fusion;
using UnityEngine;

/// <summary>
/// Adaptador de red que transporta la intención de consumo desde el cliente (Input Authority)
/// al servidor (State Authority). Se encarga de validar, ejecutar el efecto y consumir el ítem.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(PlayerLootReceiver))]
public sealed class PlayerConsumableNetworkController : NetworkBehaviour
{
    [Header("Dependencies")]
    [SerializeField]
    private LootDefinitionCatalog _lootCatalog;

    [SerializeField]
    private MonoBehaviour _characterSource;

    private ICharacter _character;
    private PlayerLootReceiver _lootReceiver;
    private NetworkMatchController _matchController;
    private bool _dependenciesValid;

    // Tracking para evitar duplicados en concurrencia
    private uint _clientNextSequence = 1;
    private uint _serverLastProcessedSequence;

    // Estado local para presentación
    private uint _clientInFlightSequence;

    /// <summary>
    /// Evento disparado localmente (Render) cuando se confirma el consumo con éxito.
    /// </summary>
    public event Action<LootId> ConsumeConfirmed;

    /// <summary>
    /// Evento disparado localmente (Render) cuando la solicitud de consumo es rechazada.
    /// </summary>
    public event Action<ConsumableFailureReason> ConsumeRejected;

    /// <summary>
    /// Indica si hay una solicitud de consumo pendiente de respuesta del servidor.
    /// </summary>
    public bool HasRequestInFlight => _clientInFlightSequence > 0;

    private void Awake()
    {
        CacheDependencies();
    }

    public override void Spawned()
    {
        CacheDependencies();
        _dependenciesValid = ValidateDependencies();
        _matchController = Runner.GetComponent<NetworkMatchController>();
        _clientNextSequence = 1;
        _serverLastProcessedSequence = 0;
        _clientInFlightSequence = 0;
    }

    /// <summary>
    /// Solicita el consumo de un objeto de loot específico desde el inventario del jugador.
    /// Solo disponible para Input Authority.
    /// </summary>
    public bool TryRequestConsume(LootId lootId)
    {
        if (!HasInputAuthority || !_dependenciesValid || !IsGameplayPhaseActive() || HasRequestInFlight)
        {
            return false;
        }

        if (_lootCatalog == null || !_lootCatalog.TryGetIndex(lootId, out int catalogIndex))
        {
            return false;
        }

        uint sequence = _clientNextSequence++;
        _clientInFlightSequence = sequence;
        
        RpcInvokeInfo invokeInfo = RPC_RequestConsume(catalogIndex, sequence);
        if (!WasAccepted(invokeInfo, HasStateAuthority))
        {
            _clientInFlightSequence = 0;
            return false;
        }

        return true;
    }

    private bool IsGameplayPhaseActive()
    {
        return _matchController == null ||
               _matchController.Phase == NetworkMatchController.MatchPhase.InProgress;
    }

    [Rpc(
        RpcSources.InputAuthority,
        RpcTargets.StateAuthority,
        InvokeLocal = true,
        HostMode = RpcHostMode.SourceIsHostPlayer)]
    private RpcInvokeInfo RPC_RequestConsume(int catalogIndex, uint requestSequence, RpcInfo info = default)
    {
        if (!HasStateAuthority || info.Source != Object.InputAuthority)
        {
            return default;
        }

        if (!IsGameplayPhaseActive())
        {
            RPC_ReceiveConsumeConfirmation(
                requestSequence,
                catalogIndex,
                false,
                (int)ConsumableFailureReason.EffectFailed);
            return default;
        }

        if (!_dependenciesValid)
        {
            RPC_ReceiveConsumeConfirmation(requestSequence, catalogIndex, false, (int)ConsumableFailureReason.EffectFailed);
            return default;
        }

        // Idempotency: ignorar secuencias antiguas o duplicadas
        if (requestSequence <= _serverLastProcessedSequence)
        {
            return default;
        }

        _serverLastProcessedSequence = requestSequence;

        ConsumableResult result = ProcessAuthoritativeConsume(catalogIndex);
        
        RPC_ReceiveConsumeConfirmation(requestSequence, catalogIndex, result.Success, (int)result.FailureReason);
        return default;
    }

    private ConsumableResult ProcessAuthoritativeConsume(int catalogIndex)
    {
        if (_lootCatalog == null || !_lootCatalog.TryGetByIndex(catalogIndex, out LootDefinition definition))
        {
            return ConsumableResult.Rejected(ConsumableFailureReason.InvalidLoot);
        }

        if (definition.ConsumableDefinition == null || definition.ConsumableDefinition.Effect == null)
        {
            return ConsumableResult.Rejected(ConsumableFailureReason.InvalidLoot);
        }

        int availableAmount = _lootReceiver.GetLootAmount(definition.LootId);
        if (availableAmount <= 0)
        {
            return ConsumableResult.Rejected(ConsumableFailureReason.InsufficientAmount);
        }

        // Intentar aplicar el efecto al Character
        bool effectSuccess = definition.ConsumableDefinition.Effect.TryApplyEffect(_character, out string failureReason);
        if (!effectSuccess)
        {
            // Mapeamos el string reason (provisorio) o fallos comunes
            if (failureReason == "El objetivo está muerto.") return ConsumableResult.Rejected(ConsumableFailureReason.TargetDead);
            if (failureReason == "La salud ya está al máximo.") return ConsumableResult.Rejected(ConsumableFailureReason.HealthFull);
            if (failureReason == "No hay autoridad para curar.") return ConsumableResult.Rejected(ConsumableFailureReason.MissingAuthority);
            
            return ConsumableResult.Rejected(ConsumableFailureReason.EffectFailed);
        }

        // El efecto fue un éxito, consumimos exactamente 1 unidad del inventario
        var extractionRequest = new LootTransferRequest(
            _character.Id,
            _character.Id, // Fake destination para satisfacer validación de Extraction
            definition.LootId,
            1,
            Runner.Tick);

        LootTransferFailureReason extractReason = _lootReceiver.ValidateExtraction(extractionRequest);
        if (extractReason != LootTransferFailureReason.None)
        {
            return ConsumableResult.Rejected(ConsumableFailureReason.InsufficientAmount);
        }

        if (!_lootReceiver.TryResolveRaidLootOriginTransfer(
                extractionRequest,
                out RaidLootOriginTransfer originTransfer))
        {
            throw new InvalidOperationException("Validated consumable provenance could not be resolved.");
        }

        _lootReceiver.CommitRaidLootExtraction(extractionRequest, originTransfer);

        return ConsumableResult.Ok();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    private void RPC_ReceiveConsumeConfirmation(uint requestSequence, int catalogIndex, bool success, int failureReasonValue)
    {
        if (requestSequence != _clientInFlightSequence)
        {
            // Ignoramos confirmaciones de secuencias que ya no estamos esperando
            return;
        }

        _clientInFlightSequence = 0;
        var reason = (ConsumableFailureReason)failureReasonValue;

        if (success)
        {
            LootId lootId = default;
            if (_lootCatalog != null && _lootCatalog.TryGetByIndex(catalogIndex, out LootDefinition definition))
            {
                lootId = definition.LootId;
            }
            ConsumeConfirmed?.Invoke(lootId);
        }
        else
        {
            ConsumeRejected?.Invoke(reason);
        }
    }

    private void CacheDependencies()
    {
        _character = _characterSource != null ? _characterSource as ICharacter : GetComponent<ICharacter>();
        _lootReceiver = GetComponent<PlayerLootReceiver>();
    }

    private bool ValidateDependencies()
    {
        if (_character == null)
        {
            Debug.LogError($"{nameof(PlayerConsumableNetworkController)}: No ICharacter dependency found.", this);
            return false;
        }

        if (_lootReceiver == null)
        {
            Debug.LogError($"{nameof(PlayerConsumableNetworkController)}: PlayerLootReceiver is missing.", this);
            return false;
        }

        if (_lootCatalog == null)
        {
            Debug.LogError($"{nameof(PlayerConsumableNetworkController)}: LootCatalog is missing.", this);
            return false;
        }

        return true;
    }

    private static bool WasAccepted(in RpcInvokeInfo invokeInfo, bool hasStateAuthority) =>
        invokeInfo.SendMessageResult == RpcSendMessageResult.Sent ||
        hasStateAuthority && invokeInfo.LocalInvokeResult == RpcLocalInvokeResult.Invoked;

#if UNITY_EDITOR
    private void OnValidate()
    {
        CacheDependencies();
    }
#endif
}
