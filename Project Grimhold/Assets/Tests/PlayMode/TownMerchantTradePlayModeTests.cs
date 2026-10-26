#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using Fusion;
using Grimhold.Backend;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;
using Tests.PlayMode;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class TownMerchantTradePlayModeTests
{
    private NetworkRunner _runner;
    private PlayModeMerchantComposition _profile;
    private TownMerchantNetworkController _merchant;
    private PlayModeMerchantComposition.EndpointObserver _endpoint;
    private TownMerchantTradeSession _session;
    private GameObject _uiObject;
    private MerchantShopUI _ui;
    private TMP_Text _feedback;
    private int _commits;
    private int _completed;
    private MerchantTransactionResult _result;

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        _session?.Dispose();
        if (_profile?.Pending != null && !_profile.Pending.Task.IsCompleted)
            _profile.Pending.SetResult((false, default, new BackendError { error = "TEST_TEARDOWN" }));
        if (_runner != null && _runner.IsRunning)
        {
            var shutdown = _runner.Shutdown();
            yield return WaitFor(() => shutdown.IsCompleted, "Runner shutdown");
        }
        if (_runner != null) UnityEngine.Object.DestroyImmediate(_runner.gameObject);
        if (_uiObject != null) UnityEngine.Object.DestroyImmediate(_uiObject);
        _profile?.Dispose();
    }

    private IEnumerator Start(long gold = 10000, bool seedSale = false, LootId ownedOverride = default)
    {
        _completed = _commits = 0;
        var merchantPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/TownMerchantNPC.prefab");
        var owned = ownedOverride.IsValid ? ownedOverride : merchantPrefab.GetComponent<TownMerchantNetworkController>().Stock[0].Item.LootId;
        _profile = new PlayModeMerchantComposition(gold, owned, seedSale ? 2 : 0);
        _profile.Store.ProfileCommitted += _ => _commits++;
        var owner = new GameObject("Town merchant E2E runner");
        _runner = owner.AddComponent<NetworkRunner>();
        owner.AddComponent<EntityRegistry>();
        owner.AddComponent<LocalInputContext>();
        owner.AddComponent<LocalPlayerJoinContext>().Initialize(new PlayerJoinData(_profile.ProfileId));
        var start = _runner.StartGame(new StartGameArgs
        {
            GameMode = GameMode.Single,
            SessionName = "merchant-test-" + Guid.NewGuid().ToString("N"),
            SceneManager = owner.AddComponent<NetworkSceneManagerDefault>(),
            ObjectProvider = owner.AddComponent<NetworkObjectProviderDefault>()
        });
        yield return WaitFor(() => start.IsCompleted, "Runner startup");
        Assert.That(start.Result.Ok, Is.True, start.Result.ShutdownReason.ToString());
        var player = Spawn("b58bec13d63beb74ca61349f7d983c36");
        _runner.SetPlayerObject(_runner.LocalPlayer, player);
        Assert.That(player.GetComponent<SocialPlayerIdentity>().SessionTicket.ToString(), Is.Not.Empty);
        _merchant = Spawn("2b48446013b1f154b878ae1793dab811").GetComponent<TownMerchantNetworkController>();
        Assert.That(_merchant, Is.Not.Null);
        Assert.That(_merchant.HasStateAuthority, Is.True);
        _merchant.TicketVerifier = _profile;
        _endpoint = new PlayModeMerchantComposition.EndpointObserver(_merchant);
        _uiObject = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/UI/MerchantShopUI.prefab"));
        _ui = _uiObject.GetComponent<MerchantShopUI>();
        _feedback = (TMP_Text)new SerializedObject(_ui).FindProperty("_feedbackText").objectReferenceValue;
        Assert.That(_feedback, Is.Not.Null, "Real merchant UI must wire its feedback label");
        _session = new TownMerchantTradeSession(_profile.ProfileId, _endpoint, _profile.Shop,
            _profile.Inventory, () => _profile.Currency.GetCurrency(_profile.ProfileId), _ui.Present, () => 0, 35,
            buybackLedger: _profile.BuybackLedger, buybackService: _profile.Shop);
        _ui.Bind(_session);
        _session.TradeCompleted += _ui.PresentResult;
        _session.TradeCompleted += result => { _completed++; _result = result; };
        Assert.That(_session.Current.IsAvailable, Is.True);
    }

    private NetworkObject Spawn(string guid)
    {
        var id = _runner.Config.PrefabTable.GetId(NetworkObjectGuid.Parse(guid));
        var prefab = _runner.Config.PrefabTable.Load(id, true);
        Assert.That(prefab, Is.Not.Null, "Required prefab is absent from the Fusion PrefabTable: " + guid);
        return _runner.Spawn(prefab, Vector3.zero, Quaternion.identity, _runner.LocalPlayer);
    }

    private LootId Item => _merchant.Stock[0].Item.LootId;
    private int Stock => _merchant.GetAvailableStock(Item);
    private static IEnumerator WaitFor(Func<bool> ready, string operation)
    {
        for (int frame = 0; frame < 120 && !ready(); frame++) yield return null;
        Assert.That(ready(), Is.True, operation + " did not complete within 120 frames");
    }

    private bool HasAuthorityOutcome(MerchantTradeRecordStatus expected)
    {
        if (_endpoint.Ticket == null) return false;
        var state = (IMerchantAuthorityState)_merchant;
        for (int index = 0; index < state.TradeRecordCapacity; index++)
        {
            var record = state.GetTradeRecord(index);
            if (record.IsOccupied && record.TransactionId.Equals(_endpoint.Ticket.TransactionId))
                return record.RecordStatus == expected;
        }
        return false;
    }

    private void AssertOutcome(MerchantTransactionResult expected)
    {
        Assert.That(_completed, Is.EqualTo(1));
        Assert.That(_result, Is.EqualTo(expected));
        Assert.That(_feedback.text, Is.EqualTo(MerchantTradeFeedback.DescribeResult(expected)));
        Assert.That(_session.IsSubmissionInFlight, Is.False);
    }

    private void AssertProfile(long gold, int units)
    {
        Assert.That(_profile.Store.GetCurrency(), Is.EqualTo(gold));
        var loadout = _profile.Store.GetLoadout();
        Assert.That(loadout.Count, Is.EqualTo(units == 0 ? 0 : 1));
        if (units > 0)
        {
            Assert.That(loadout[0].LootId, Is.EqualTo(Item));
            Assert.That(loadout[0].Amount, Is.EqualTo(units));
        }
    }

    [UnityTest]
    public IEnumerator SaleOfEquipment_ThenBuyback_RestoresItemAtBuybackPrice()
    {
        var equipment = new LootId("placeholder_helmet");
        yield return Start(seedSale: true, ownedOverride: equipment);
        _profile.CaptureEquipmentSales = true;
        Assert.That(_merchant.Catalog.TryGet(equipment.Value, out LootDefinition definition), Is.True);
        long sellValue = definition.SellValuePerUnit;
        long buybackPrice = (long)Math.Ceiling(sellValue * 1.5);
        Assert.That(_session.AddSale(equipment, 2), Is.True);
        Assert.That(_session.ConfirmTrade(), Is.True);
        yield return WaitFor(() => _completed == 1, "Equipment sale");
        Assert.That(_result, Is.EqualTo(MerchantTransactionResult.Success));
        Assert.That(_profile.Store.GetLoadout(), Is.Empty);
        Assert.That(_profile.BuybackLedger.Entries.Count, Is.EqualTo(1));
        int submissions = _endpoint.Submissions;
        int outcomes = _endpoint.Outcomes;
        var rows = _session.Current.MerchantRows;
        Assert.That(rows[rows.Count - 1].IsBuyback, Is.True);
        Assert.That(rows[rows.Count - 1].UnitPrice, Is.EqualTo(buybackPrice));
        Assert.That(_session.SetBuyback("equipment-sale", 2), Is.True);
        Assert.That(_session.Current.PurchaseTotal, Is.EqualTo(2 * buybackPrice));
        Assert.That(_session.ConfirmTrade(), Is.True);
        yield return WaitFor(() => _completed == 2, "Equipment buy-back");
        Assert.That(_result, Is.EqualTo(MerchantTransactionResult.Success));
        Assert.That(_feedback.text, Is.EqualTo(MerchantTradeFeedback.DescribeResult(_result)));
        Assert.That(_profile.Store.GetCurrency(), Is.EqualTo(10000 + 2 * sellValue - 2 * buybackPrice));
        Assert.That(_profile.Store.GetLoadout().Count, Is.EqualTo(1));
        Assert.That(_profile.Store.GetLoadout()[0].LootId, Is.EqualTo(equipment));
        Assert.That(_profile.Store.GetLoadout()[0].Amount, Is.EqualTo(2));
        Assert.That(_profile.BuybackLedger.Entries, Is.Empty);
        Assert.That(_profile.BuybackCalls, Is.EqualTo(1));
        Assert.That(_endpoint.Submissions, Is.EqualTo(submissions));
        Assert.That(_endpoint.Outcomes, Is.EqualTo(outcomes));
        Assert.That(_session.Current.HasDraft, Is.False);
    }

    [UnityTest]
    public IEnumerator Purchase_BackendAcknowledges_UpdatesProfileStockAndReportsSuccess()
    {
        yield return Start();
        int stock = Stock;
        int price = _merchant.Stock[0].Item.BuyValuePerUnit;
        Assert.That(_session.AddPurchase(Item, 1), Is.True);
        Assert.That(_session.Current.CanConfirm, Is.True);
        Assert.That(_session.ConfirmTrade(), Is.True);
        yield return WaitFor(() => _completed > 0, "Acknowledged purchase");
        yield return WaitFor(() => Stock == stock - 1, "Purchase stock commit");
        AssertProfile(10000 - price, 1);
        AssertOutcome(MerchantTransactionResult.Success);
        Assert.That(_profile.Shop.IsTradeApplied(_profile.ProfileId, _endpoint.Ticket.TransactionId), Is.True);
        Assert.That(_profile.BackendCalls, Is.EqualTo(1));
        Assert.That(_profile.Store.RemoteRevision, Is.EqualTo(1));
        Assert.That(HasAuthorityOutcome(MerchantTradeRecordStatus.Committed), Is.True);
        Assert.That(_endpoint.Outcomes, Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator Sale_BackendRejects_KeepsProfileAndStockAndReportsFailure()
    {
        yield return Start(seedSale: true);
        int stock = Stock;
        _profile.BackendErrorCode = "ITEM_NOT_SELLABLE";
        Assert.That(_session.AddSale(Item, 1), Is.True);
        Assert.That(_session.ConfirmTrade(), Is.True);
        yield return WaitFor(() => _completed > 0, "Rejected sale");
        yield return WaitFor(() => HasAuthorityOutcome(MerchantTradeRecordStatus.Released), "Sale reservation release");
        AssertProfile(10000, 2);
        Assert.That(Stock, Is.EqualTo(stock));
        AssertOutcome(MerchantTransactionResult.RejectedByProfile);
        Assert.That(_result, Is.Not.EqualTo(MerchantTransactionResult.Success));
        Assert.That(_profile.BackendCalls, Is.EqualTo(1));
        Assert.That(_profile.Reconciliations, Is.EqualTo(1));
        Assert.That(_commits, Is.Zero);
        Assert.That(_profile.Store.RemoteRevision, Is.Zero);
        Assert.That(_profile.Shop.IsTradeApplied(_profile.ProfileId, _endpoint.Ticket.TransactionId), Is.False);
        Assert.That(_endpoint.Outcomes, Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator Purchase_InsufficientCurrency_DisablesConfirmWithoutRequest()
    {
        yield return Start(0);
        int stock = Stock;
        Assert.That(_session.AddPurchase(Item, 1), Is.True);
        Assert.That(_session.Current.CanConfirm, Is.False);
        Assert.That(_session.ConfirmTrade(), Is.False);
        for (int frame = 0; frame < 10; frame++) yield return null;
        Assert.That(_endpoint.Submissions, Is.Zero);
        Assert.That(_profile.BackendCalls, Is.Zero);
        Assert.That(_endpoint.Ticket, Is.Null);
        Assert.That(_completed, Is.Zero);
        Assert.That(_commits, Is.Zero);
        AssertProfile(0, 0);
        Assert.That(Stock, Is.EqualTo(stock));
        Assert.That(MerchantTradeFeedback.TryGetPrimaryBlock(_session.Current.Blocks, out var block), Is.True);
        Assert.That(block.Reason, Is.EqualTo(MerchantTradeBlockReason.InsufficientCurrency));
        Assert.That(_feedback.text, Is.EqualTo(MerchantTradeFeedback.DescribePrimaryBlock(_session.Current)));
    }

    [UnityTest]
    public IEnumerator SecondConfirm_WhileBackendPending_IsRefused()
    {
        yield return Start();
        int stock = Stock;
        _profile.Pending = new TaskCompletionSource<(bool, ShopTransactionResult, BackendError)>();
        Assert.That(_session.AddPurchase(Item, 1), Is.True);
        Assert.That(_session.ConfirmTrade(), Is.True);
        yield return WaitFor(() => _profile.BackendCalls == 1, "Pending backend call");
        Assert.That(_session.Current.CanConfirm, Is.False);
        Assert.That(_session.ConfirmTrade(), Is.False);
        for (int frame = 0; frame < 10; frame++) { _session.Tick(); yield return null; }
        Assert.That(_endpoint.Submissions, Is.EqualTo(1));
        Assert.That(_profile.BackendCalls, Is.EqualTo(1));
        Assert.That(_completed, Is.Zero);
        Assert.That(_endpoint.Outcomes, Is.Zero);
        Assert.That(Stock, Is.EqualTo(stock - 1));
        AssertProfile(10000, 0);
        Assert.That(_commits, Is.Zero);
        Assert.That(_feedback.text, Is.EqualTo(MerchantTradeFeedback.DescribePrimaryBlock(_session.Current)));
        _profile.Pending.SetResult((true, new ShopTransactionResult { revision = 1 }, default));
        yield return WaitFor(() => _completed > 0, "Pending purchase release");
        for (int frame = 0; frame < 10; frame++) yield return null;
        AssertOutcome(MerchantTransactionResult.Success);
        AssertProfile(10000 - _merchant.Stock[0].Item.BuyValuePerUnit, 1);
        Assert.That(Stock, Is.EqualTo(stock - 1));
        Assert.That(_profile.BackendCalls, Is.EqualTo(1));
        Assert.That(_endpoint.Outcomes, Is.EqualTo(1));
        Assert.That(_profile.Shop.IsTradeApplied(_profile.ProfileId, _endpoint.Ticket.TransactionId), Is.True);
    }

    [UnityTest]
    public IEnumerator MultiLineDraft_IsRejectedBeforeSubmission_AndSingleLineStillWorks()
    {
        yield return Start();
        var second = _merchant.Stock[1].Item.LootId;
        int stock = Stock;
        int secondStock = _merchant.GetAvailableStock(second);
        Assert.That(_session.AddPurchase(Item, 1), Is.True);
        Assert.That(_session.AddPurchase(second, 1), Is.True);
        Assert.That(_session.ConfirmTrade(), Is.False);
        Assert.That(_session.IsSubmissionInFlight, Is.False);
        Assert.That(Stock, Is.EqualTo(stock));
        Assert.That(_merchant.GetAvailableStock(second), Is.EqualTo(secondStock));
        Assert.That(_endpoint.Submissions, Is.Zero);
        AssertProfile(10000, 0);
        AssertOutcome(MerchantTransactionResult.RejectedByMerchant);
        Assert.That(_profile.BackendCalls, Is.Zero);
        Assert.That(_commits, Is.Zero);
        Assert.That(_profile.Store.RemoteRevision, Is.Zero);
        Assert.That(_endpoint.Outcomes, Is.Zero);

        Assert.That(_session.RemovePurchase(second), Is.True);
        Assert.That(_session.ConfirmTrade(), Is.True);
        yield return WaitFor(() => _endpoint.Submissions == 1 && _completed == 2, "Single-line trade after rejection");
        Assert.That(_result, Is.EqualTo(MerchantTransactionResult.Success));
        Assert.That(_profile.BackendCalls, Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator TradeRequest_SingleRunner_ReceivesAuthorityTicketThroughRpc()
    {
        yield return Start();
        _profile.Pending = new TaskCompletionSource<(bool, ShopTransactionResult, BackendError)>();
        int stock = Stock;
        Assert.That(_session.AddPurchase(Item, 1), Is.True);
        Assert.That(_session.ConfirmTrade(), Is.True);
        yield return WaitFor(() => _endpoint.Ticket != null, "RPC authority ticket response");
        Assert.That(_endpoint.Ticket.Purchases[0].LootId, Is.EqualTo(Item));
        Assert.That(_endpoint.Ticket.Purchases[0].UnitPrice, Is.EqualTo(_merchant.Stock[0].Item.BuyValuePerUnit));
        Assert.That(_profile.BackendCalls, Is.EqualTo(1));
        Assert.That(Stock, Is.EqualTo(stock - 1), "Authority reserved the ticket's purchase");
        Assert.That(_profile.Store.GetCurrency(), Is.EqualTo(10000));
        Assert.That(_completed, Is.Zero);
        _profile.Pending.SetResult((true, new ShopTransactionResult { revision = 1 }, default));
        yield return WaitFor(() => _completed == 1, "Smoke trade completion");
        Assert.That(_result, Is.EqualTo(MerchantTransactionResult.Success));
    }
}
#endif
