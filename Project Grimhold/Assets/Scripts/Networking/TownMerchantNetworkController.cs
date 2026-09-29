using Fusion;
using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Fusion boundary of a Town merchant. State Authority alone evaluates trade requests against the
/// shared, replicated stock and trade records, keyed by the sender's <see cref="ProfileId"/> and
/// the client request id. Responses go only to the requester, whose persistence outcome is reported
/// back to confirm or release the reservation; a reservation is never released by time or
/// departure while a persisted outcome can still arrive.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public sealed class TownMerchantNetworkController : NetworkBehaviour, IMerchantTradeEndpoint, IMerchantAuthorityState, IStateAuthorityChanged
{
    public const int MaxStockSlots = 16;
    public const int MaxTradeRecords = 32;
    public const int MaxTradeStockLines = 64;

    [SerializeField] private LootDefinitionCatalog _catalog;
    [SerializeField, Tooltip("Initial offering. Seeds the shared stock once; it is not runtime state.")]
    private List<MerchantStockItem> _stock;

    [Networked] private NetworkBool IsStockInitialized { get; set; }
    [Networked] private int StockRevision { get; set; }
    [Networked, Capacity(MaxStockSlots)] private NetworkArray<int> StockQuantities => default;
    [Networked, Capacity(MaxTradeRecords)] private NetworkArray<MerchantTradeRecord> TradeRecords => default;
    [Networked, Capacity(MaxTradeStockLines)] private NetworkArray<MerchantTradeStockLine> TradeStockLines => default;

    public IReadOnlyList<MerchantStockItem> Stock => _stock;
    public LootDefinitionCatalog Catalog => _catalog;

    private readonly List<MerchantTradeRecord> _pendingTradesBuffer = new List<MerchantTradeRecord>();
    private MerchantRequestValidator _requestValidator;
    private IMerchantTradeClient _tradeClient;
    private int _observedStockRevision = -1;

    /// <summary>Raised on every peer when the replicated available stock changes.</summary>
    public event Action StockChanged;

    public event Action AuthorityChanged;

    public int GetAvailableStock(LootId lootId)
    {
        if (_requestValidator == null || !IsStockInitialized)
        {
            return 0;
        }

        return _requestValidator.GetAvailableStock(lootId);
    }

    public bool TrySubmitTrade(
        ProfileId profileId,
        Guid requestId,
        IReadOnlyList<MerchantTradeLine> purchases,
        IReadOnlyList<MerchantTradeLine> sales)
    {
        if (Object == null || !Object.IsValid || !profileId.IsValid || requestId == Guid.Empty ||
            !MerchantTradeWireCodec.TryEncodeRequest(_catalog, purchases, out MerchantTradeRequestLineMessage[] purchaseMessages) ||
            !MerchantTradeWireCodec.TryEncodeRequest(_catalog, sales, out MerchantTradeRequestLineMessage[] saleMessages))
        {
            return false;
        }

        Rpc_RequestTrade(profileId.Value, requestId, purchaseMessages, saleMessages);
        return true;
    }

    public void ReportTradeOutcome(Guid requestId, ShopTransactionId transactionId, bool persisted)
    {
        if (Object == null || !Object.IsValid)
        {
            return;
        }

        Rpc_ReportTradeOutcome(requestId, transactionId.Timestamp, transactionId.Value, persisted);
    }

    public void RequestPendingTrades(ProfileId profileId)
    {
        if (Object == null || !Object.IsValid || !profileId.IsValid)
        {
            return;
        }

        Rpc_RequestPendingTrades(profileId.Value);
    }

    public void SetTradeClient(IMerchantTradeClient client)
    {
        _tradeClient = client;
    }

    public void ClearTradeClient(IMerchantTradeClient client)
    {
        if (ReferenceEquals(_tradeClient, client))
        {
            _tradeClient = null;
        }
    }

    public override void Spawned()
    {
        if (!MerchantRequestValidator.TryValidateStockConfiguration(_stock, MaxStockSlots, out string error))
        {
            Debug.LogError($"[TownMerchantNetworkController] Invalid merchant stock configuration: {error}", this);
            return;
        }

        _requestValidator = new MerchantRequestValidator(_stock, this);
        EnsureStockInitialized();
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        _requestValidator = null;
        _tradeClient = null;
    }

    public override void Render()
    {
        if (_observedStockRevision == StockRevision)
        {
            return;
        }

        _observedStockRevision = StockRevision;
        StockChanged?.Invoke();
    }

    public void StateAuthorityChanged()
    {
        if (HasStateAuthority && _requestValidator != null)
        {
            EnsureStockInitialized();
        }

        AuthorityChanged?.Invoke();
    }

    private void EnsureStockInitialized()
    {
        if (!HasStateAuthority || IsStockInitialized)
        {
            return;
        }

        IsStockInitialized = _requestValidator.TryInitializeStock();
    }

    /// <summary>
    /// Resolves the RPC sender and its persistent profile from the sender's replicated
    /// <see cref="SocialPlayerIdentity"/>; a PlayerRef is never used as profile identity.
    /// </summary>
    private bool TryResolveRequester(RpcInfo info, out PlayerRef requester, out ProfileId profileId)
    {
        profileId = default;
        requester = info.Source.IsNone ? Runner.LocalPlayer : info.Source;
        NetworkObject playerObject = _requestValidator != null && !requester.IsNone ? Runner.GetPlayerObject(requester) : null;
        if (playerObject == null || !playerObject.TryGetBehaviour(out SocialPlayerIdentity identity) ||
            identity.Object.InputAuthority != requester || string.IsNullOrWhiteSpace(identity.ProfileId.ToString()))
        {
            return false;
        }

        profileId = new ProfileId(identity.ProfileId.ToString());
        return true;
    }

    // --- IMerchantAuthorityState ---
    bool IMerchantAuthorityState.CanMutate => HasStateAuthority && Object != null && Object.IsValid;
    int IMerchantAuthorityState.StockSlotCapacity => MaxStockSlots;
    int IMerchantAuthorityState.GetStockQuantity(int slot) => StockQuantities[slot];
    void IMerchantAuthorityState.SetStockQuantity(int slot, int quantity) => StockQuantities.Set(slot, quantity);
    int IMerchantAuthorityState.TradeRecordCapacity => MaxTradeRecords;
    MerchantTradeRecord IMerchantAuthorityState.GetTradeRecord(int index) => TradeRecords[index];
    void IMerchantAuthorityState.SetTradeRecord(int index, MerchantTradeRecord record) => TradeRecords.Set(index, record);
    int IMerchantAuthorityState.TradeStockLineCapacity => MaxTradeStockLines;
    MerchantTradeStockLine IMerchantAuthorityState.GetTradeStockLine(int index) => TradeStockLines[index];
    void IMerchantAuthorityState.SetTradeStockLine(int index, MerchantTradeStockLine line) => TradeStockLines.Set(index, line);
    void IMerchantAuthorityState.MarkStockChanged() => StockRevision++;

    // --- RPCs ---
    [Rpc(RpcSources.All, RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
    private void Rpc_RequestTrade(
        string profileId,
        Guid requestId,
        MerchantTradeRequestLineMessage[] purchases,
        MerchantTradeRequestLineMessage[] sales,
        RpcInfo info = default)
    {
        if (!TryResolveRequester(info, out PlayerRef requester, out ProfileId senderProfile))
        {
            return;
        }

        MerchantTradeResponse response = MerchantTradeResponse.Rejected(requestId);
        MerchantTradeLine[] purchaseLines = null;
        MerchantTradeLine[] saleLines = null;
        bool isWellFormed = new ProfileId(profileId) == senderProfile &&
            MerchantTradeWireCodec.TryDecodeRequest(_catalog, purchases, _requestValidator.MaxPurchaseLines, out purchaseLines) &&
            MerchantTradeWireCodec.TryDecodeRequest(_catalog, sales, MerchantRequestValidator.MaxSaleLines, out saleLines);

        if (isWellFormed && !_requestValidator.TryProcessTradeRequest(
                senderProfile, requestId, purchaseLines, saleLines, _catalog, out response))
        {
            return;
        }

        SendTradeResponse(requester, response);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
    private void Rpc_ReportTradeOutcome(Guid requestId, long timestamp, Guid txGuid, NetworkBool persisted, RpcInfo info = default)
    {
        if (!TryResolveRequester(info, out _, out ProfileId senderProfile))
        {
            return;
        }

        _requestValidator.TryCompleteTrade(senderProfile, requestId, new ShopTransactionId(timestamp, txGuid), persisted);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
    private void Rpc_RequestPendingTrades(string profileId, RpcInfo info = default)
    {
        if (!TryResolveRequester(info, out PlayerRef requester, out ProfileId senderProfile) ||
            new ProfileId(profileId) != senderProfile || !HasStateAuthority)
        {
            return;
        }

        _requestValidator.GetPendingTrades(senderProfile, _pendingTradesBuffer);
        foreach (MerchantTradeRecord record in _pendingTradesBuffer)
        {
            ShopTransactionId transactionId = record.TransactionId;
            Rpc_PendingTrade(requester, record.RequestId, transactionId.Timestamp, transactionId.Value);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All, Channel = RpcChannel.Reliable)]
    private void Rpc_PendingTrade([RpcTarget] PlayerRef target, Guid requestId, long timestamp, Guid txGuid)
    {
        _tradeClient?.HandlePendingTrade(requestId, new ShopTransactionId(timestamp, txGuid));
    }

    private void SendTradeResponse(PlayerRef requester, MerchantTradeResponse response)
    {
        var none = Array.Empty<MerchantPricedTradeLineMessage>();
        if (!response.IsApproved)
        {
            Rpc_TradeResponse(requester, response.RequestId, false, 0, Guid.Empty, none, none);
            return;
        }

        MerchantTradeTicket ticket = response.Ticket;
        if (!MerchantTradeWireCodec.TryEncodeTicketLines(_catalog, ticket.Purchases, out MerchantPricedTradeLineMessage[] purchases) ||
            !MerchantTradeWireCodec.TryEncodeTicketLines(_catalog, ticket.Sales, out MerchantPricedTradeLineMessage[] sales))
        {
            Debug.LogError($"[TownMerchantNetworkController] Ticket {ticket.TransactionId} could not be encoded.", this);
            return;
        }

        Rpc_TradeResponse(requester, response.RequestId, true, ticket.TransactionId.Timestamp, ticket.TransactionId.Value, purchases, sales);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All, Channel = RpcChannel.Reliable)]
    private void Rpc_TradeResponse(
        [RpcTarget] PlayerRef target,
        Guid requestId,
        NetworkBool isApproved,
        long timestamp,
        Guid txGuid,
        MerchantPricedTradeLineMessage[] purchases,
        MerchantPricedTradeLineMessage[] sales)
    {
        var transactionId = new ShopTransactionId(timestamp, txGuid);
        MerchantTradeResponse response = MerchantTradeResponse.Rejected(requestId);
        if (isApproved && MerchantTradeWireCodec.TryDecodeTicket(
                _catalog, transactionId, purchases, sales, MaxStockSlots, MerchantRequestValidator.MaxSaleLines, out MerchantTradeTicket ticket))
        {
            response = MerchantTradeResponse.Approved(requestId, ticket);
        }

        bool handled = _tradeClient != null && _tradeClient.HandleTradeResponse(response);
        if (isApproved && (!handled || !response.IsApproved))
        {
            // Nobody will persist this ticket: release its reservation now instead of on expiry.
            ReportTradeOutcome(requestId, transactionId, persisted: false);
        }
    }
}
