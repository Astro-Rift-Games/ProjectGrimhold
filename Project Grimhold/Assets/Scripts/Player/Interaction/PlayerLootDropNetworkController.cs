using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// Adapts local inventory-drop intentions to one bounded Fusion request and materializes
/// the selected loot under State Authority during network simulation.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(PlayerLootReceiver))]
public sealed class PlayerLootDropNetworkController : NetworkBehaviour
{
    [Header("Dependencies")]
    [SerializeField]
    private LootDefinitionCatalog _lootCatalog;

    [SerializeField]
    private NetworkPrefabRef _pickupPrefab;

    [SerializeField]
    private MonoBehaviour _characterSource;

    [SerializeField]
    private MonoBehaviour _movementStateSource;

    [SerializeField]
    private Transform _dropOrigin;

    [Header("Placement")]
    [SerializeField, Min(0f)]
    private float _dropDistance = 0.75f;

    [SerializeField, Min(0.01f)]
    private float _clearanceRadius = 0.27f;

    [SerializeField]
    private LayerMask _worldCollisionMask;

    private readonly LootDropClientRequestState _clientRequest = new();
    private readonly LootDropRequestState _authoritativeRequests = new();
    private readonly Queue<PresentationNotification> _presentationNotifications = new(8);
    private readonly Collider2D[] _placementHits = new Collider2D[1];

    private ICharacter _character;
    private IMovementState _movementState;
    private PlayerLootReceiver _lootReceiver;
    private PlayerExtractionController _extractionController;
    private bool _dependenciesValid;

    public event Action<LootDropConfirmation> DropConfirmed;
    public event Action<bool> RequestInFlightChanged;
    public event Action<LootTransferTransportRejectionReason> TransportRejected;

    public bool HasRequestInFlight => _clientRequest.HasInFlight;

    private void Awake()
    {
        CacheDependencies();
    }

    public override void Spawned()
    {
        CacheDependencies();
        _dependenciesValid = ValidateDependencies();
        ResetLocalState();
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || !_dependenciesValid ||
            !_authoritativeRequests.TryConsume(out LootDropRequestIdentity identity))
        {
            return;
        }

        LootDropConfirmation confirmation = ProcessAuthoritativeRequest(identity);
        _authoritativeRequests.RecordProcessed(identity, confirmation);
        SendConfirmation(confirmation);
    }

    public override void Render()
    {
        while (_presentationNotifications.Count > 0)
        {
            PresentationNotification notification = _presentationNotifications.Dequeue();
            switch (notification.Kind)
            {
                case PresentationNotificationKind.RequestInFlightChanged:
                    RequestInFlightChanged?.Invoke(notification.HasRequestInFlight);
                    break;
                case PresentationNotificationKind.TransportRejected:
                    TransportRejected?.Invoke(notification.TransportRejectionReason);
                    break;
                case PresentationNotificationKind.DropConfirmed:
                    DropConfirmed?.Invoke(notification.Confirmation);
                    break;
            }
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        ResetLocalState();
    }

    /// <summary>
    /// Sends one single-unit or full-stack inventory drop intention from Input Authority.
    /// State Authority resolves the final quantity from its current inventory snapshot.
    /// </summary>
    public bool TryRequestDrop(LootId lootId, LootTransferQuantityMode quantityMode)
    {
        if (!HasInputAuthority || !_dependenciesValid ||
            (_extractionController != null && _extractionController.State == ExtractionState.Extracted) ||
            _lootCatalog == null || !_lootCatalog.TryGetIndex(lootId, out int catalogIndex) ||
            !_clientRequest.TryCreateCandidate(catalogIndex, quantityMode, out LootDropRequestIdentity identity))
        {
            return false;
        }

        RpcInvokeInfo invokeInfo = RPC_RequestDrop(
            catalogIndex,
            (int)quantityMode,
            identity.RequestSequence);
        if (!WasAccepted(invokeInfo, HasStateAuthority))
        {
            return false;
        }

        _clientRequest.MarkSent(identity);
        Enqueue(PresentationNotification.RequestState(true));
        return true;
    }

    [Rpc(
        RpcSources.InputAuthority,
        RpcTargets.StateAuthority,
        InvokeLocal = true,
        HostMode = RpcHostMode.SourceIsHostPlayer)]
    private RpcInvokeInfo RPC_RequestDrop(
        int catalogIndex,
        int quantityModeValue,
        uint requestSequence,
        RpcInfo info = default)
    {
        if (!HasStateAuthority || info.Source != Object.InputAuthority)
        {
            return default;
        }

        if (!_dependenciesValid)
        {
            RPC_ReceiveTransportRejection(
                requestSequence,
                (int)LootTransferTransportRejectionReason.DependenciesUnavailable);
            return default;
        }

        if (requestSequence == 0)
        {
            Debug.LogError($"{nameof(PlayerLootDropNetworkController)} received sequence zero.", this);
            return default;
        }

        var identity = new LootDropRequestIdentity(
            requestSequence,
            catalogIndex,
            (LootTransferQuantityMode)quantityModeValue);
        LootDropRequestState.Disposition disposition =
            _authoritativeRequests.TryEnqueue(identity, out LootDropConfirmation cached);

        switch (disposition)
        {
            case LootDropRequestState.Disposition.AcceptedPending:
            case LootDropRequestState.Disposition.PendingDuplicate:
                break;
            case LootDropRequestState.Disposition.ProcessedDuplicate:
                SendConfirmation(cached);
                break;
            case LootDropRequestState.Disposition.BusyWithDifferentSequence:
                RPC_ReceiveTransportRejection(
                    requestSequence,
                    (int)LootTransferTransportRejectionReason.BusyWithDifferentSequence);
                break;
            case LootDropRequestState.Disposition.StaleSequence:
                RPC_ReceiveTransportRejection(
                    requestSequence,
                    (int)LootTransferTransportRejectionReason.StaleSequence);
                break;
            case LootDropRequestState.Disposition.PendingPayloadConflict:
            case LootDropRequestState.Disposition.ProcessedPayloadConflict:
                Debug.LogError(
                    $"{nameof(PlayerLootDropNetworkController)} preserved the original payload for conflicting sequence {requestSequence}.",
                    this);
                break;
        }

        return default;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    private void RPC_ReceiveDropConfirmation(
        uint requestSequence,
        int catalogIndex,
        int droppedAmount,
        bool success,
        int failureReasonValue,
        int simulationTick)
    {
        if (!_clientRequest.TryRelease(requestSequence, out LootDropRequestIdentity expected))
        {
            Debug.LogError(
                $"{nameof(PlayerLootDropNetworkController)} discarded confirmation for unknown sequence {requestSequence}.",
                this);
            return;
        }

        Enqueue(PresentationNotification.RequestState(false));
        if (!LootDropConfirmation.TryReconstruct(
                requestSequence,
                catalogIndex,
                droppedAmount,
                success,
                failureReasonValue,
                simulationTick,
                expected,
                _lootCatalog,
                out LootDropConfirmation confirmation,
                out string error))
        {
            Debug.LogError(
                $"{nameof(PlayerLootDropNetworkController)} received an invalid confirmation. {error}",
                this);
            return;
        }

        Enqueue(PresentationNotification.Drop(confirmation));
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    private void RPC_ReceiveTransportRejection(uint requestSequence, int rejectionReasonValue)
    {
        if (!_clientRequest.TryRelease(requestSequence, out _))
        {
            return;
        }

        Enqueue(PresentationNotification.RequestState(false));
        if (!Enum.IsDefined(typeof(LootTransferTransportRejectionReason), rejectionReasonValue) ||
            rejectionReasonValue == (int)LootTransferTransportRejectionReason.Uninitialized)
        {
            Debug.LogError($"{nameof(PlayerLootDropNetworkController)} received a malformed transport rejection.", this);
            return;
        }

        Enqueue(PresentationNotification.Transport(
            (LootTransferTransportRejectionReason)rejectionReasonValue));
    }

    private LootDropConfirmation ProcessAuthoritativeRequest(in LootDropRequestIdentity identity)
    {
        int tick = Runner.Tick;
        if (!HasStateAuthority)
        {
            return Rejected(identity, tick, LootDropFailureReason.MissingAuthority);
        }

        if (_character == null || !_character.IsAlive || _lootReceiver == null ||
            _lootReceiver.Id.Value == 0 ||
            (_extractionController != null && _extractionController.State == ExtractionState.Extracted))
        {
            return Rejected(identity, tick, LootDropFailureReason.PlayerUnavailable);
        }

        if (_lootCatalog == null ||
            !_lootCatalog.TryGetByIndex(identity.CatalogIndex, out LootDefinition definition))
        {
            return Rejected(identity, tick, LootDropFailureReason.InvalidLoot);
        }

        int availableAmount = _lootReceiver.GetLootAmount(definition.LootId);
        LootTransferFailureReason quantityFailure = LootTransferQuantityResolver.Resolve(
            identity.QuantityMode,
            availableAmount,
            out int requestedAmount);
        if (quantityFailure != LootTransferFailureReason.None)
        {
            return Rejected(identity, tick, ToDropFailure(quantityFailure));
        }

        if (!TryResolveDropPosition(out Vector2 dropPosition))
        {
            return Rejected(identity, tick, LootDropFailureReason.NoValidPosition);
        }

        var entry = new LootEntry(definition.LootId, requestedAmount);
        bool overrideApplied = false;
        NetworkLootPickup callbackPickup = null;
        NetworkSpawnStatus spawnStatus = Runner.TrySpawn(
            _pickupPrefab,
            out NetworkObject pickupObject,
            dropPosition,
            rotation: Quaternion.identity,
            inputAuthority: null,
            onBeforeSpawned: (callbackRunner, instance) =>
            {
                callbackPickup = instance != null
                    ? instance.GetComponent<NetworkLootPickup>()
                    : null;
                overrideApplied = callbackPickup != null &&
                    callbackPickup.TrySetSpawnContentOverride(
                        callbackRunner,
                        instance,
                        entry,
                        false,
                        0);
            });

        bool validSpawn = spawnStatus == NetworkSpawnStatus.Spawned &&
            pickupObject != null && pickupObject.Id.IsValid && callbackPickup != null &&
            callbackPickup.Object == pickupObject && overrideApplied &&
            callbackPickup.IsInitialized && callbackPickup.Amount == requestedAmount &&
            callbackPickup.LootDefinition == definition &&
            callbackPickup.ValidateDropPublication(Runner, pickupObject);
        if (!validSpawn)
        {
            CompensateFailedSpawn(pickupObject);
            return Rejected(identity, tick, LootDropFailureReason.SpawnFailed);
        }

        var extraction = new LootTransferRequest(
            _lootReceiver.Id,
            callbackPickup.Id,
            definition.LootId,
            requestedAmount,
            tick);
        LootTransferFailureReason extractionFailure = _lootReceiver.ValidateExtraction(extraction);
        if (extractionFailure != LootTransferFailureReason.None)
        {
            CompensateFailedSpawn(pickupObject);
            return Rejected(identity, tick, ToDropFailure(extractionFailure));
        }

        _lootReceiver.CommitExtraction(extraction);
        callbackPickup.CommitDropPublication(Runner, pickupObject);
        return new LootDropConfirmation(
            identity.RequestSequence,
            identity.CatalogIndex,
            tick,
            LootDropResult.Succeeded(requestedAmount),
            definition.LootId);
    }

    private bool TryResolveDropPosition(out Vector2 position)
    {
        position = default;
        Vector2 origin = _dropOrigin != null
            ? (Vector2)_dropOrigin.position
            : (Vector2)transform.position;
        Vector2 facing = _movementState != null
            ? _movementState.FacingDirection
            : Vector2.down;

        var filter = new ContactFilter2D
        {
            useLayerMask = true,
            useTriggers = false
        };
        filter.SetLayerMask(_worldCollisionMask);

        for (int i = 0; i < LootDropPlacementMath.CandidateCount; i++)
        {
            Vector2 candidate = LootDropPlacementMath.GetCandidate(
                origin,
                facing,
                _dropDistance,
                i);
            int hitCount = Physics2D.OverlapCircle(
                candidate,
                _clearanceRadius,
                filter,
                _placementHits);
            if (hitCount == 0)
            {
                position = candidate;
                return true;
            }
        }

        return false;
    }

    private void CompensateFailedSpawn(NetworkObject pickupObject)
    {
        if (pickupObject != null && pickupObject.Id.IsValid)
        {
            Runner.Despawn(pickupObject);
        }
    }

    private static LootDropFailureReason ToDropFailure(LootTransferFailureReason reason)
    {
        return reason switch
        {
            LootTransferFailureReason.InvalidLoot => LootDropFailureReason.InvalidLoot,
            LootTransferFailureReason.InvalidAmount => LootDropFailureReason.InvalidAmount,
            LootTransferFailureReason.InsufficientAmount => LootDropFailureReason.InsufficientAmount,
            LootTransferFailureReason.MissingAuthority => LootDropFailureReason.MissingAuthority,
            _ => LootDropFailureReason.PlayerUnavailable
        };
    }

    private static LootDropConfirmation Rejected(
        in LootDropRequestIdentity identity,
        int tick,
        LootDropFailureReason reason)
    {
        return new LootDropConfirmation(
            identity.RequestSequence,
            identity.CatalogIndex,
            tick,
            LootDropResult.Rejected(reason),
            null);
    }

    private void SendConfirmation(in LootDropConfirmation confirmation)
    {
        RPC_ReceiveDropConfirmation(
            confirmation.RequestSequence,
            confirmation.CatalogIndex,
            confirmation.Result.DroppedAmount,
            confirmation.Result.Success,
            (int)confirmation.Result.FailureReason,
            confirmation.SimulationTick);
    }

    private void CacheDependencies()
    {
        _character = _characterSource != null
            ? _characterSource as ICharacter
            : GetComponent<ICharacter>();
        _movementState = _movementStateSource != null
            ? _movementStateSource as IMovementState
            : GetComponent<IMovementState>();
        _lootReceiver = GetComponent<PlayerLootReceiver>();
        if (_dropOrigin == null)
        {
            _dropOrigin = transform;
        }
        if (_extractionController == null)
        {
            _extractionController = GetComponent<PlayerExtractionController>();
        }
    }

    private bool ValidateDependencies()
    {
        string catalogError = null;
        if (_lootCatalog == null || !_lootCatalog.TryValidate(out catalogError) ||
            !_pickupPrefab.IsValid || _character == null || _movementState == null ||
            _lootReceiver == null || _worldCollisionMask.value == 0 ||
            _dropDistance <= 0f || _clearanceRadius <= 0f)
        {
            Debug.LogError(
                $"{nameof(PlayerLootDropNetworkController)} has invalid drop dependencies. {catalogError}",
                this);
            return false;
        }

        return true;
    }

    private void ResetLocalState()
    {
        _clientRequest.Reset();
        _authoritativeRequests.Reset();
        _presentationNotifications.Clear();
    }

    private void Enqueue(in PresentationNotification notification)
    {
        if (_presentationNotifications.Count >= 8)
        {
            Debug.LogError($"{nameof(PlayerLootDropNetworkController)} notification queue capacity was exceeded.", this);
            return;
        }

        _presentationNotifications.Enqueue(notification);
    }

    private static bool WasAccepted(in RpcInvokeInfo invokeInfo, bool hasStateAuthority) =>
        invokeInfo.SendMessageResult == RpcSendMessageResult.Sent ||
        hasStateAuthority && invokeInfo.LocalInvokeResult == RpcLocalInvokeResult.Invoked;

    private enum PresentationNotificationKind
    {
        RequestInFlightChanged,
        TransportRejected,
        DropConfirmed
    }

    private readonly struct PresentationNotification
    {
        public PresentationNotificationKind Kind { get; }
        public bool HasRequestInFlight { get; }
        public LootTransferTransportRejectionReason TransportRejectionReason { get; }
        public LootDropConfirmation Confirmation { get; }

        private PresentationNotification(
            PresentationNotificationKind kind,
            bool hasRequestInFlight,
            LootTransferTransportRejectionReason transportRejectionReason,
            in LootDropConfirmation confirmation)
        {
            Kind = kind;
            HasRequestInFlight = hasRequestInFlight;
            TransportRejectionReason = transportRejectionReason;
            Confirmation = confirmation;
        }

        public static PresentationNotification RequestState(bool inFlight) =>
            new(PresentationNotificationKind.RequestInFlightChanged, inFlight, default, default);

        public static PresentationNotification Transport(LootTransferTransportRejectionReason reason) =>
            new(PresentationNotificationKind.TransportRejected, false, reason, default);

        public static PresentationNotification Drop(in LootDropConfirmation confirmation) =>
            new(PresentationNotificationKind.DropConfirmed, false, default, confirmation);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        _dropDistance = Mathf.Max(0f, _dropDistance);
        _clearanceRadius = Mathf.Max(0.01f, _clearanceRadius);
        CacheDependencies();
    }
#endif
}
