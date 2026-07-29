using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// Adapts local full-stack loot intentions to primitive Fusion RPCs and executes them on State Authority.
/// Request queueing and idempotency are bounded local adapter state; gameplay mutation remains tick-driven.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public sealed class PlayerLootTransferNetworkController : NetworkBehaviour
{
    [Header("Dependencies")]
    [SerializeField]
    private LootDefinitionCatalog _lootCatalog;

    [SerializeField]
    private PlayerInteractionConfig _interactionConfig;

    [SerializeField]
    private MonoBehaviour _characterSource;

    [SerializeField]
    private MonoBehaviour _querySource;

    [SerializeField]
    private Transform _interactionOrigin;

    private ICharacter _character;
    private IInteractionTargetQuery _query;
    private EntityRegistry _registry;
    private bool _dependenciesValid;

    private readonly LootTransferClientRequestState _clientRequest = new();
    private readonly LootTransferRequestState _authoritativeRequests = new();
    private readonly PresentationNotificationQueue _presentationNotifications = new();

    /// <summary>
    /// Local presentation notification emitted during Render after a transport confirmation is reconstructed.
    /// </summary>
    public event Action<LootTransferConfirmation> TransferConfirmed;

    /// <summary>
    /// Local presentation notification published during Render after the legitimate request state changes.
    /// </summary>
    public event Action<bool> RequestInFlightChanged;

    /// <summary>
    /// Local presentation notification emitted when transport finalizes a request without a domain confirmation.
    /// </summary>
    public event Action<LootTransferTransportRejectionReason> TransportRejected;

    public bool HasRequestInFlight => _clientRequest.HasInFlight;

    private void Awake()
    {
        CacheDependencies();
    }

    public override void Spawned()
    {
        CacheDependencies();
        _registry = Runner.GetComponent<EntityRegistry>();
        _dependenciesValid = ValidateDependencies();
        ResetLocalState();
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || !_dependenciesValid ||
            !_authoritativeRequests.TryConsume(out LootTransferRequestIdentity identity))
        {
            return;
        }

        LootTransferConfirmation confirmation = ProcessAuthoritativeRequest(identity);
        _authoritativeRequests.RecordProcessed(identity, confirmation);
        SendConfirmation(confirmation);
    }

    public override void Render()
    {
        while (_presentationNotifications.TryDequeue(out PresentationNotification notification))
        {
            if (notification.Kind == PresentationNotificationKind.RequestInFlightChanged)
            {
                RequestInFlightChanged?.Invoke(notification.HasRequestInFlight);
                continue;
            }

            if (notification.Kind == PresentationNotificationKind.TransportRejected)
            {
                TransportRejected?.Invoke(notification.TransportRejectionReason);
                continue;
            }

            TransferConfirmed?.Invoke(notification.Confirmation);
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        ResetLocalState();
        _registry = null;
    }

    /// <summary>
    /// Sends one full-stack transfer intention from Input Authority.
    /// A second legitimate request is rejected locally until the matching sequence is confirmed.
    /// </summary>
    public bool TryRequestFullStack(EntityId sourceId, LootId lootId)
    {
        if (!HasInputAuthority || !_dependenciesValid || sourceId.Value == 0 ||
            _lootCatalog == null || !_lootCatalog.TryGetIndex(lootId, out int catalogIndex))
        {
            return false;
        }

        if (!_clientRequest.TryCreateCandidate(sourceId, catalogIndex, out LootTransferRequestIdentity identity))
        {
            return false;
        }

        RpcInvokeInfo invokeInfo = RPC_RequestFullStack(sourceId.Value, catalogIndex, identity.RequestSequence);
        if (!WasAccepted(invokeInfo, HasStateAuthority))
        {
            return false;
        }

        _clientRequest.MarkSent(identity);
        EnqueueRequestStateChanged(true);
        return true;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    /// <summary>
    /// Sends a primitive request for development transport tests without changing legitimate in-flight state.
    /// </summary>
    public bool DebugSendRawRequest(EntityId sourceId, int catalogIndex, uint requestSequence)
    {
        if (!HasInputAuthority || sourceId.Value == 0)
        {
            return false;
        }

        return WasAccepted(
            RPC_RequestFullStack(sourceId.Value, catalogIndex, requestSequence),
            HasStateAuthority);
    }

    public bool DebugTryGetInFlightIdentity(out LootTransferRequestIdentity identity)
    {
        return _clientRequest.TryGetExpected(out identity);
    }

    /// <summary>
    /// Stages an accepted local request without transport so deferred presentation ordering can be tested.
    /// </summary>
    public bool DebugStageAcceptedRequestForPresentation(
        EntityId sourceId,
        int catalogIndex,
        out uint requestSequence)
    {
        requestSequence = 0;
        if (!_clientRequest.TryCreateCandidate(sourceId, catalogIndex, out LootTransferRequestIdentity identity))
        {
            return false;
        }

        _clientRequest.MarkSent(identity);
        EnqueueRequestStateChanged(true);
        requestSequence = identity.RequestSequence;
        return true;
    }

    /// <summary>
    /// Stages local request finalization and optionally its reconstructed confirmation for Render tests.
    /// </summary>
    public bool DebugStageRequestCompletionForPresentation(
        uint requestSequence,
        bool publishConfirmation,
        in LootTransferConfirmation confirmation)
    {
        if (!_clientRequest.TryRelease(requestSequence, out _))
        {
            return false;
        }

        EnqueueRequestStateChanged(false);
        if (publishConfirmation)
        {
            EnqueueConfirmation(confirmation);
        }

        return true;
    }

    /// <summary>
    /// Stages a transport rejection so tests can verify that it finalizes the local request.
    /// </summary>
    public bool DebugStageTransportRejectionForPresentation(
        uint requestSequence,
        LootTransferTransportRejectionReason reason)
    {
        if (!_clientRequest.TryRelease(requestSequence, out _))
        {
            return false;
        }

        EnqueueRequestStateChanged(false);
        EnqueueTransportRejection(reason);
        return true;
    }

    /// <summary>
    /// Clears request and deferred presentation state without invoking subscribers.
    /// </summary>
    public void DebugResetLocalPresentationState()
    {
        ResetLocalState();
    }
#endif

    [Rpc(
        RpcSources.InputAuthority,
        RpcTargets.StateAuthority,
        InvokeLocal = true,
        HostMode = RpcHostMode.SourceIsHostPlayer)]
    private RpcInvokeInfo RPC_RequestFullStack(
        int sourceIdValue,
        int catalogIndex,
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
            Debug.LogError($"{nameof(PlayerLootTransferNetworkController)}: Sequence zero is not a valid request envelope.", this);
            return default;
        }

        var identity = new LootTransferRequestIdentity(
            requestSequence,
            new EntityId(sourceIdValue),
            catalogIndex);

        LootTransferRequestState.Disposition disposition =
            _authoritativeRequests.TryEnqueue(identity, out LootTransferConfirmation cached);

        switch (disposition)
        {
            case LootTransferRequestState.Disposition.AcceptedPending:
            case LootTransferRequestState.Disposition.PendingDuplicate:
                break;
            case LootTransferRequestState.Disposition.ProcessedDuplicate:
                SendConfirmation(cached);
                break;
            case LootTransferRequestState.Disposition.BusyWithDifferentSequence:
                RPC_ReceiveTransportRejection(
                    requestSequence,
                    (int)LootTransferTransportRejectionReason.BusyWithDifferentSequence);
                break;
            case LootTransferRequestState.Disposition.StaleSequence:
                RPC_ReceiveTransportRejection(
                    requestSequence,
                    (int)LootTransferTransportRejectionReason.StaleSequence);
                break;
            case LootTransferRequestState.Disposition.PendingPayloadConflict:
            case LootTransferRequestState.Disposition.ProcessedPayloadConflict:
                Debug.LogError(
                    $"{nameof(PlayerLootTransferNetworkController)}: Conflicting payload received for request sequence {requestSequence}; original state was preserved.",
                    this);
                break;
        }

        return default;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    private void RPC_ReceiveTransferConfirmation(
        uint requestSequence,
        int sourceIdValue,
        int destinationIdValue,
        int catalogIndex,
        int transferredAmount,
        bool success,
        int failureReasonValue,
        int simulationTick)
    {
        if (!_clientRequest.TryRelease(requestSequence, out LootTransferRequestIdentity expected))
        {
            Debug.LogError($"{nameof(PlayerLootTransferNetworkController)}: Discarded confirmation for unknown sequence {requestSequence}.", this);
            return;
        }

        EnqueueRequestStateChanged(false);

        if (!LootTransferConfirmation.TryReconstruct(
                requestSequence,
                sourceIdValue,
                destinationIdValue,
                catalogIndex,
                transferredAmount,
                success,
                failureReasonValue,
                simulationTick,
                expected,
                _character?.Id ?? default,
                _lootCatalog,
                out LootTransferConfirmation confirmation,
                out string error))
        {
            Debug.LogError($"{nameof(PlayerLootTransferNetworkController)}: Invalid confirmation payload for sequence {requestSequence}. {error}", this);
            return;
        }

        EnqueueConfirmation(confirmation);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    private void RPC_ReceiveTransportRejection(uint requestSequence, int rejectionReasonValue)
    {
        if (!_clientRequest.TryRelease(requestSequence, out _))
        {
            Debug.LogError($"{nameof(PlayerLootTransferNetworkController)}: Discarded transport rejection for unknown sequence {requestSequence}.", this);
            return;
        }

        EnqueueRequestStateChanged(false);

        if (!Enum.IsDefined(typeof(LootTransferTransportRejectionReason), rejectionReasonValue) ||
            rejectionReasonValue == (int)LootTransferTransportRejectionReason.Uninitialized)
        {
            Debug.LogError($"{nameof(PlayerLootTransferNetworkController)}: Malformed transport rejection for sequence {requestSequence}.", this);
            return;
        }

        var rejectionReason = (LootTransferTransportRejectionReason)rejectionReasonValue;
        EnqueueTransportRejection(rejectionReason);

        Debug.LogWarning(
            $"{nameof(PlayerLootTransferNetworkController)}: Request {requestSequence} was rejected by transport: {rejectionReason}.",
            this);
    }

    private LootTransferConfirmation ProcessAuthoritativeRequest(in LootTransferRequestIdentity identity)
    {
        EntityId destinationId = _character?.Id ?? default;
        int tick = Runner.Tick;

        if (identity.SourceId.Value == 0 || _registry == null ||
            !_registry.TryGetLootSource(identity.SourceId, out ILootExtractor extractor, out ILootQuantityReader quantityReader))
        {
            return RejectedConfirmation(identity, destinationId, tick, LootTransferFailureReason.SourceNotFound);
        }

        if (destinationId.Value == 0 || !_registry.TryGetLootReceiver(destinationId, out ILootReceiver receiver))
        {
            return RejectedConfirmation(identity, destinationId, tick, LootTransferFailureReason.DestinationNotFound);
        }

        if (_lootCatalog == null || !_lootCatalog.TryGetByIndex(identity.CatalogIndex, out LootDefinition definition))
        {
            return RejectedConfirmation(identity, destinationId, tick, LootTransferFailureReason.InvalidLoot);
        }

        int fullAmount = quantityReader.GetLootAmount(definition.LootId);
        if (fullAmount <= 0)
        {
            return RejectedConfirmation(identity, destinationId, tick, LootTransferFailureReason.InsufficientAmount);
        }

        if (!IsSourceInRange(identity.SourceId, destinationId))
        {
            return RejectedConfirmation(identity, destinationId, tick, LootTransferFailureReason.OutOfRange);
        }

        var request = new LootTransferRequest(
            identity.SourceId,
            destinationId,
            definition.LootId,
            fullAmount,
            tick);

        LootTransferResult result = LootTransferTransaction.Execute(extractor, receiver, request);
        return new LootTransferConfirmation(
            identity.RequestSequence,
            identity.SourceId,
            destinationId,
            identity.CatalogIndex,
            tick,
            result,
            definition.LootId);
    }

    private bool IsSourceInRange(EntityId sourceId, EntityId destinationId)
    {
        Vector2 origin = _interactionOrigin != null ? (Vector2)_interactionOrigin.position : (Vector2)transform.position;
        var query = new InteractionTargetQuery(
            destinationId,
            origin,
            _interactionConfig.MaximumDistance,
            _interactionConfig.TargetLayerMask);

        IReadOnlyList<InteractionTarget> targets = _query.FindTargets(query);
        for (int i = 0; i < targets.Count; i++)
        {
            if (targets[i].TargetId == sourceId && targets[i].Distance <= _interactionConfig.MaximumDistance)
            {
                return true;
            }
        }

        return false;
    }

    private static LootTransferConfirmation RejectedConfirmation(
        in LootTransferRequestIdentity identity,
        EntityId destinationId,
        int tick,
        LootTransferFailureReason reason)
    {
        LootTransferResult result = LootTransferResult.Rejected(reason);
        return new LootTransferConfirmation(
            identity.RequestSequence,
            identity.SourceId,
            destinationId,
            identity.CatalogIndex,
            tick,
            result,
            null);
    }

    private void SendConfirmation(in LootTransferConfirmation confirmation)
    {
        RPC_ReceiveTransferConfirmation(
            confirmation.RequestSequence,
            confirmation.SourceId.Value,
            confirmation.DestinationId.Value,
            confirmation.CatalogIndex,
            confirmation.Result.TransferredAmount,
            confirmation.Result.Success,
            (int)confirmation.Result.FailureReason,
            confirmation.SimulationTick);
    }

    private void CacheDependencies()
    {
        _character = _characterSource != null ? _characterSource as ICharacter : GetComponent<ICharacter>();
        _query = _querySource != null ? _querySource as IInteractionTargetQuery : GetComponent<IInteractionTargetQuery>();
        if (_interactionOrigin == null)
        {
            _interactionOrigin = transform;
        }
    }

    private bool ValidateDependencies()
    {
        string catalogError = null;
        if (_lootCatalog == null || !_lootCatalog.TryValidate(out catalogError))
        {
            Debug.LogError($"{nameof(PlayerLootTransferNetworkController)}: Loot catalog is missing or invalid. {catalogError}", this);
            return false;
        }

        if (_interactionConfig == null || _character == null || _query == null || _registry == null)
        {
            Debug.LogError($"{nameof(PlayerLootTransferNetworkController)}: Required interaction, character, query or registry dependency is missing.", this);
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

    private void EnqueueRequestStateChanged(bool hasRequestInFlight)
    {
        if (!_presentationNotifications.TryEnqueue(PresentationNotification.RequestState(hasRequestInFlight)))
        {
            Debug.LogError($"{nameof(PlayerLootTransferNetworkController)}: Presentation notification queue capacity was exceeded.", this);
        }
    }

    private void EnqueueConfirmation(in LootTransferConfirmation confirmation)
    {
        if (!_presentationNotifications.TryEnqueue(PresentationNotification.Transfer(confirmation)))
        {
            Debug.LogError($"{nameof(PlayerLootTransferNetworkController)}: Presentation notification queue capacity was exceeded.", this);
        }
    }

    private void EnqueueTransportRejection(LootTransferTransportRejectionReason reason)
    {
        if (!_presentationNotifications.TryEnqueue(PresentationNotification.TransportRejection(reason)))
        {
            Debug.LogError($"{nameof(PlayerLootTransferNetworkController)}: Presentation notification queue capacity was exceeded.", this);
        }
    }

    private enum PresentationNotificationKind
    {
        RequestInFlightChanged,
        TransportRejected,
        TransferConfirmed
    }

    private readonly struct PresentationNotification
    {
        public PresentationNotificationKind Kind { get; }
        public bool HasRequestInFlight { get; }
        public LootTransferTransportRejectionReason TransportRejectionReason { get; }
        public LootTransferConfirmation Confirmation { get; }

        private PresentationNotification(
            PresentationNotificationKind kind,
            bool hasRequestInFlight,
            LootTransferTransportRejectionReason transportRejectionReason,
            in LootTransferConfirmation confirmation)
        {
            Kind = kind;
            HasRequestInFlight = hasRequestInFlight;
            TransportRejectionReason = transportRejectionReason;
            Confirmation = confirmation;
        }

        public static PresentationNotification RequestState(bool hasRequestInFlight)
        {
            return new PresentationNotification(
                PresentationNotificationKind.RequestInFlightChanged,
                hasRequestInFlight,
                LootTransferTransportRejectionReason.Uninitialized,
                default);
        }

        public static PresentationNotification TransportRejection(
            LootTransferTransportRejectionReason reason)
        {
            return new PresentationNotification(
                PresentationNotificationKind.TransportRejected,
                false,
                reason,
                default);
        }

        public static PresentationNotification Transfer(in LootTransferConfirmation confirmation)
        {
            return new PresentationNotification(
                PresentationNotificationKind.TransferConfirmed,
                false,
                LootTransferTransportRejectionReason.Uninitialized,
                confirmation);
        }
    }

    private sealed class PresentationNotificationQueue
    {
        private const int Capacity = 8;

        private readonly PresentationNotification[] _items = new PresentationNotification[Capacity];
        private int _head;
        private int _count;

        public bool TryEnqueue(in PresentationNotification notification)
        {
            if (_count >= Capacity)
            {
                return false;
            }

            int index = (_head + _count) % Capacity;
            _items[index] = notification;
            _count++;
            return true;
        }

        public bool TryDequeue(out PresentationNotification notification)
        {
            if (_count == 0)
            {
                notification = default;
                return false;
            }

            notification = _items[_head];
            _items[_head] = default;
            _head = (_head + 1) % Capacity;
            _count--;
            return true;
        }

        public void Clear()
        {
            Array.Clear(_items, 0, _items.Length);
            _head = 0;
            _count = 0;
        }
    }

    private static bool WasAccepted(in RpcInvokeInfo invokeInfo, bool hasStateAuthority)
    {
        return invokeInfo.SendMessageResult == RpcSendMessageResult.Sent ||
            hasStateAuthority && invokeInfo.LocalInvokeResult == RpcLocalInvokeResult.Invoked;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        CacheDependencies();
    }
#endif
}
