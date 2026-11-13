using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Grimhold.Backend;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class RemoteShopTransactionServiceTests
{
    private static readonly ProfileId Profile = new ProfileId("11111111111111111111111111111111");
    private static readonly LootId Bone = new LootId("bone");
    private LocalProfileRepository _repository;
    private MemoryFileStore _files;
    private bool _failLocalWrites;
    private LocalProfileStore _store;
    private GameObject _owner;
    private RemoteShopTransactionService _service;
    private BuybackLedger _ledger;
    private ShopTransactionResult _buybackResponse;
    private ShopTransactionResult? _tradeResponse;
    private int _calls;
    private int _reconciliations;
    private int _commits;
    private bool _success;
    private string _error;
    private bool _throw;
    private bool _purchase;
    private ShopTradeRequest _request;
    private int _revision;
    private string _transactionId;
    private TaskCompletionSource<(bool success, ShopTransactionResult data, BackendError error)> _pending;

    [SetUp]
    public void SetUp()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<LootDefinitionCatalog>(
            "Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset");
        _files = new MemoryFileStore();
        _repository = new LocalProfileRepository(_files, ".");
        _failLocalWrites = false;
        Assert.That(_repository.Initialize(Profile, catalog), Is.True);
        _store = new LocalProfileStore(_repository, Profile, catalog);
        Seed(100);
        _store.SetRemoteRevision(7);
        _store.ProfileCommitted += _ => _commits++;
        _owner = new GameObject("RemoteShopTest");
        _service = _owner.AddComponent<RemoteShopTransactionService>();
        _service.Initialize(_store, Backend, () => { _reconciliations++; return Task.FromResult(true); });
        _ledger = new BuybackLedger();
        _buybackResponse = new ShopTransactionResult
        {
            currency = 91, revision = 8,
            loadout = new[] { new InventoryItemData { lootId = "bone", amount = 1 } },
            buyback = Rows("remaining")
        };
        _service.InitializeBuyback(_ledger, (transactionId, entryId, amount, revision) =>
        {
            _calls++;
            _transactionId = transactionId;
            _revision = revision;
            if (_throw) throw new InvalidOperationException("Transport lost");
            return Task.FromResult((_success, _buybackResponse, new BackendError { error = _error }));
        });
        _success = true;
        _error = null;
        _calls = _commits = _reconciliations = 0;
        _throw = false;
        _pending = null;
        _tradeResponse = null;
    }

    [TearDown]
    public void TearDown() => UnityEngine.Object.DestroyImmediate(_owner);

    private Task<(bool success, ShopTransactionResult data, BackendError error)> Backend(
        ShopTradeRequest request)
    {
        _calls++;
        _request = request;
        _purchase = request.purchases.Length > 0;
        _revision = request.expectedRevision;
        _transactionId = request.transactionId;
        if (_throw) throw new InvalidOperationException("Transport lost");
        if (_failLocalWrites) _files.FailWrites = true;
        return _pending?.Task ?? Task.FromResult((_success, _tradeResponse ?? new ShopTransactionResult { revision = 8, buyback = Rows("sale") }, new BackendError { error = _error }));
    }

    [Test]
    public async Task BackendSuccess_CommitsThenSetsRevision()
    {
        var ticket = Ticket();
        Assert.That(await _service.ExecuteTradeAsync(Profile, ticket), Is.EqualTo(StashOperationResult.Success));
        Assert.That(_store.GetCurrency(), Is.EqualTo(95));
        Assert.That(_store.RemoteRevision, Is.EqualTo(8));
        Assert.That(_store.IsTradeApplied(Profile, ticket.TransactionId), Is.True);
        Assert.That(_purchase, Is.True);
        Assert.That(_calls, Is.EqualTo(1));
        Assert.That(_request.sales, Is.Empty);
        Assert.That((_request.purchases[0].lootId, _request.purchases[0].amount), Is.EqualTo(("bone", 1)));
        Assert.That(_revision, Is.EqualTo(7));
        Assert.That(_transactionId, Is.EqualTo(ticket.TransactionId.ToString()));
        Assert.That(_commits, Is.EqualTo(2), "Trade and remote revision each publish the aggregate.");
    }

    [Test]
    public async Task BackendAcknowledgedButLocalDiskFails_StillReportsDurableSuccessAndReconciles()
    {
        _failLocalWrites = true;
        LogAssert.Expect(LogType.Error, new Regex("LocalProfileStore.*Commit failed"));
        LogAssert.Expect(LogType.Error, new Regex("LocalProfileStore.*Commit failed"));
        LogAssert.Expect(LogType.Error, new Regex("Backend persisted trade .*local commit/revision save failed"));
        Assert.That(await _service.ExecuteTradeAsync(Profile, Ticket()), Is.EqualTo(StashOperationResult.Success));
        AssertUnchanged(100, 0);
        Assert.That(_reconciliations, Is.EqualTo(1));
    }

    [Test]
    public async Task BackendRejection_DoesNotCommitAndRequestsReconciliation()
    {
        _success = false;
        _error = "ITEM_NOT_SELLABLE";
        Seed(100, 2);
        Assert.That(await _service.ExecuteTradeAsync(Profile, Ticket(sale: true)), Is.EqualTo(StashOperationResult.InvalidInventory));
        AssertUnchanged(100, 2);
        Assert.That(_purchase, Is.False);
        Assert.That(_reconciliations, Is.EqualTo(1));
    }

    [Test]
    public async Task DryRunFailure_DoesNotCallBackend()
    {
        Seed(0);
        Assert.That(await _service.ExecuteTradeAsync(Profile, Ticket()), Is.EqualTo(StashOperationResult.InvalidInventory));
        Assert.That(_calls, Is.Zero);
        AssertUnchanged(0, 0);
    }

    [Test]
    public async Task ReservationPending_IsTypedAndDoesNotCommit()
    {
        _success = false;
        _error = "RESERVATION_PENDING";
        Assert.That(await _service.ExecuteTradeAsync(Profile, Ticket()), Is.EqualTo(StashOperationResult.ReservationPending));
        AssertUnchanged(100, 0);
        Assert.That(_reconciliations, Is.EqualTo(1));
    }

    [TestCase("NETWORK_ERROR")]
    [TestCase("UNKNOWN")]
    [TestCase("REVISION_CONFLICT")]
    public async Task Failure_ReconcilesWithoutLocalMutationOrRetry(string error)
    {
        _success = false;
        _error = error;
        var expected = error == "REVISION_CONFLICT" ? StashOperationResult.InvalidInventory : StashOperationResult.PersistenceFailed;
        Assert.That(await _service.ExecuteTradeAsync(Profile, Ticket()), Is.EqualTo(expected));
        AssertUnchanged(100, 0);
        Assert.That(_calls, Is.EqualTo(1));
        Assert.That(_reconciliations, Is.EqualTo(1));
    }

    [Test]
    public async Task TransportException_IsFailureAndReconciles()
    {
        _throw = true;
        Assert.That(await _service.ExecuteTradeAsync(Profile, Ticket()), Is.EqualTo(StashOperationResult.PersistenceFailed));
        AssertUnchanged(100, 0);
        Assert.That(_reconciliations, Is.EqualTo(1));
    }

    private MerchantTradeTicket MixedTicket()
    {
        Seed(100, 2);
        var snapshot = _repository.Snapshot.Clone();
        snapshot.Loadout.Add(new StashItem(new LootId("healthpotion"), 3, 4));
        Assert.That(_repository.TrySave(snapshot, out var error), Is.True, error);
        return new MerchantTradeTicket(new ShopTransactionId(1000, Guid.NewGuid()),
            new[] { new MerchantPricedTradeLine(Bone, 1, 5) },
            new[] { new MerchantPricedTradeLine(Bone, 2, 2),
                new MerchantPricedTradeLine(new LootId("healthpotion"), 3, 3) });
    }

    [Test]
    public async Task MultiLine_MixedTicket_CommitsOnceThroughOneBackendCall()
    {
        var ticket = MixedTicket();
        Assert.That(await _service.ExecuteTradeAsync(Profile, ticket), Is.EqualTo(StashOperationResult.Success));
        Assert.That(_calls, Is.EqualTo(1));
        Assert.That(_revision, Is.EqualTo(7));
        Assert.That(_transactionId, Is.EqualTo(ticket.TransactionId.ToString()));
        Assert.That(_request.purchases, Has.Length.EqualTo(1));
        Assert.That((_request.purchases[0].lootId, _request.purchases[0].amount), Is.EqualTo(("bone", 1)));
        Assert.That(_request.sales, Has.Length.EqualTo(2));
        Assert.That((_request.sales[0].lootId, _request.sales[0].amount, _request.sales[0].sourceSlot), Is.EqualTo(("bone", 2, 0)));
        Assert.That((_request.sales[1].lootId, _request.sales[1].amount, _request.sales[1].sourceSlot), Is.EqualTo(("healthpotion", 3, 4)));
        Assert.That(_store.GetCurrency(), Is.EqualTo(108));
        Assert.That(_store.GetLoadout(), Has.Count.EqualTo(1));
        Assert.That(_store.GetLoadout()[0].LootId, Is.EqualTo(Bone));
        Assert.That(_store.GetLoadout()[0].Amount, Is.EqualTo(1));
        Assert.That(_store.IsTradeApplied(Profile, ticket.TransactionId), Is.True);
        Assert.That(_store.RemoteRevision, Is.EqualTo(8));
        Assert.That(_commits, Is.EqualTo(2), "One trade commit and one revision save.");
        Assert.That(_reconciliations, Is.Zero);
        Assert.That(_ledger.Entries[0].EntryId, Is.EqualTo("sale"));
    }

    [Test]
    public async Task MultiLine_BackendFailure_PreservesProfile()
    {
        var ticket = MixedTicket();
        _success = false;
        _error = "INSUFFICIENT_ITEMS";
        Assert.That(await _service.ExecuteTradeAsync(Profile, ticket), Is.EqualTo(StashOperationResult.InvalidInventory));
        Assert.That(_calls, Is.EqualTo(1));
        Assert.That(_store.GetCurrency(), Is.EqualTo(100));
        Assert.That(_store.GetLoadout(), Has.Count.EqualTo(2));
        Assert.That(_store.GetLoadout()[0].Amount, Is.EqualTo(2));
        Assert.That(_store.GetLoadout()[1].Amount, Is.EqualTo(3));
        Assert.That(_store.GetLoadout()[1].Slot, Is.EqualTo(4));
        Assert.That(_store.IsTradeApplied(Profile, ticket.TransactionId), Is.False);
        Assert.That(_store.RemoteRevision, Is.EqualTo(7));
        Assert.That(_commits, Is.Zero);
        Assert.That(_ledger.Entries, Is.Empty);
        Assert.That(_reconciliations, Is.EqualTo(1));
    }

    [Test]
    public async Task ReceiptReplay_DoesNotCallBackendAgain()
    {
        var ticket = Ticket();
        Assert.That(await _service.ExecuteTradeAsync(Profile, ticket), Is.EqualTo(StashOperationResult.Success));
        Assert.That(await _service.ExecuteTradeAsync(Profile, ticket), Is.EqualTo(StashOperationResult.AlreadyApplied));
        Assert.That(_calls, Is.EqualTo(1));
        Assert.That(_commits, Is.EqualTo(2), "Receipt replay publishes nothing beyond trade and revision.");
    }

    [UnityTest]
    public IEnumerator PendingBackend_DoesNotMutateOrCompleteEarly()
    {
        _pending = new TaskCompletionSource<(bool, ShopTransactionResult, BackendError)>();
        Task<StashOperationResult> task = _service.ExecuteTradeAsync(Profile, Ticket());
        Assert.That(task.IsCompleted, Is.False);
        AssertUnchanged(100, 0);
        _pending.SetResult((true, new ShopTransactionResult { revision = 8 }, default));
        yield return null;
        Assert.That(task.Result, Is.EqualTo(StashOperationResult.Success));
        Assert.That(_store.GetCurrency(), Is.EqualTo(95));
    }

    [Test]
    public void LocalBuyback_TotalPriceAndReceiptAreAtomic()
    {
        var request = Buyback();
        Assert.That(_store.EvaluateBuyback(Profile, request.TransactionId, Bone, 2, 13), Is.EqualTo(StashOperationResult.Success));
        AssertUnchanged(100, 0);
        Assert.That(_store.TryApplyBuyback(Profile, request.TransactionId, Bone, 2, 13), Is.EqualTo(StashOperationResult.Success));
        Assert.That(_store.GetCurrency(), Is.EqualTo(87));
        Assert.That(_store.GetLoadout()[0].Amount, Is.EqualTo(2));
        Assert.That(_store.TryApplyBuyback(Profile, request.TransactionId, Bone, 2, 13), Is.EqualTo(StashOperationResult.AlreadyApplied));
        Assert.That(_commits, Is.EqualTo(1));
    }

    private static BuybackData Rows(string id) => new BuybackData
    {
        entries = new[] { new BuybackEntryData { entryId = id, lootId = "bone", amount = 2, unitPrice = 9 } }
    };

    private static MerchantBuybackRequest Buyback() => new MerchantBuybackRequest(
        new ShopTransactionId(1000, Guid.NewGuid()), "sold", Bone, 1, 5);

    [Test]
    public async Task BuybackSuccess_UsesAuthoritativeTotalsAndReplacesLedger()
    {
        _ledger.Replace(Rows("old"));
        var request = Buyback();
        Assert.That(await _service.ExecuteBuybackAsync(Profile, request), Is.EqualTo(StashOperationResult.Success));
        Assert.That(_store.GetCurrency(), Is.EqualTo(91), "Backend reprices the advisory cost of 5 to 9.");
        Assert.That(_store.GetLoadout()[0].Amount, Is.EqualTo(1));
        Assert.That(_store.RemoteRevision, Is.EqualTo(8));
        Assert.That(_store.IsTradeApplied(Profile, request.TransactionId), Is.True);
        Assert.That(_ledger.Entries[0].EntryId, Is.EqualTo("remaining"));
        Assert.That(_revision, Is.EqualTo(7));
        Assert.That(_transactionId, Is.EqualTo(request.TransactionId.ToString()));
    }

    [TestCase("BUYBACK_ENTRY_NOT_FOUND", StashOperationResult.InvalidInventory)]
    [TestCase("INSUFFICIENT_BUYBACK_UNITS", StashOperationResult.InvalidInventory)]
    [TestCase("INSUFFICIENT_FUNDS", StashOperationResult.InvalidInventory)]
    [TestCase("REVISION_CONFLICT", StashOperationResult.InvalidInventory)]
    [TestCase("VALIDATION_FAILED", StashOperationResult.InvalidInventory)]
    [TestCase("RESERVATION_PENDING", StashOperationResult.ReservationPending)]
    [TestCase("TRANSACTION_ID_REUSED", StashOperationResult.PersistenceFailed)]
    [TestCase("NETWORK_ERROR", StashOperationResult.PersistenceFailed)]
    public async Task BuybackRejection_DoesNotMutateAndReconciles(string error, StashOperationResult expected)
    {
        _success = false;
        _error = error;
        Assert.That(await _service.ExecuteBuybackAsync(Profile, Buyback()), Is.EqualTo(expected));
        AssertUnchanged(100, 0);
        Assert.That(_calls, Is.EqualTo(1));
        Assert.That(_reconciliations, Is.EqualTo(1));
    }

    [Test]
    public async Task BuybackDryRun_InsufficientGold_DoesNotCallBackend()
    {
        Seed(0);
        Assert.That(await _service.ExecuteBuybackAsync(Profile, Buyback()), Is.EqualTo(StashOperationResult.InvalidInventory));
        Assert.That(_calls, Is.Zero);
        AssertUnchanged(0, 0);
    }

    [Test]
    public async Task BuybackDryRun_PendingReservation_DoesNotCallBackend()
    {
        var snapshot = _repository.Snapshot.Clone();
        snapshot.PendingReservation = new PendingLoadoutReservation("pending",
            new[] { new StashItem(new LootId("arming_sword"), 1, 0) },
            new PreparedEquipmentLoadout(new LootId("arming_sword"), default));
        Assert.That(_repository.TrySave(snapshot, out var error), Is.True, error);
        Assert.That(await _service.ExecuteBuybackAsync(Profile, Buyback()), Is.EqualTo(StashOperationResult.ReservationPending));
        Assert.That(_calls, Is.Zero);
        AssertUnchanged(100, 0);
    }

    [Test]
    public async Task BuybackDryRun_Capacity_DoesNotCallBackend()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<LootDefinitionCatalog>(
            "Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset");
        var snapshot = _repository.Snapshot.Clone();
        for (int i = 0; i < catalog.DefinitionCount && snapshot.Loadout.Count < LocalProfileSnapshot.MaxLoadoutSlots; i++)
            if (catalog.TryGetByIndex(i, out var definition) && definition.LootId != Bone)
                snapshot.Loadout.Add(new StashItem(definition.LootId, 1, snapshot.Loadout.Count));
        Assert.That(snapshot.Loadout.Count, Is.EqualTo(LocalProfileSnapshot.MaxLoadoutSlots));
        Assert.That(_repository.TrySave(snapshot, out var error), Is.True, error);
        Assert.That(await _service.ExecuteBuybackAsync(Profile, Buyback()), Is.EqualTo(StashOperationResult.InvalidInventory));
        Assert.That(_calls, Is.Zero);
        Assert.That(_store.GetCurrency(), Is.EqualTo(100));
        Assert.That(_store.RemoteRevision, Is.EqualTo(7));
        Assert.That(_commits, Is.Zero);
    }

    [Test]
    public async Task BuybackTransportException_ReconcilesWithoutMutation()
    {
        _throw = true;
        Assert.That(await _service.ExecuteBuybackAsync(Profile, Buyback()), Is.EqualTo(StashOperationResult.PersistenceFailed));
        AssertUnchanged(100, 0);
        Assert.That(_reconciliations, Is.EqualTo(1));
    }

    [Test]
    public async Task BuybackAlreadySecured_RecordsReceiptAndReplayPublishesNothing()
    {
        _buybackResponse.alreadySecured = true;
        var request = Buyback();
        Assert.That(await _service.ExecuteBuybackAsync(Profile, request), Is.EqualTo(StashOperationResult.Success));
        int commits = _commits;
        Assert.That(await _service.ExecuteBuybackAsync(Profile, request), Is.EqualTo(StashOperationResult.AlreadyApplied));
        Assert.That(_calls, Is.EqualTo(1));
        Assert.That(_commits, Is.EqualTo(commits));
        Assert.That(_store.GetCurrency(), Is.EqualTo(91));
        Assert.That(_store.GetLoadout()[0].Amount, Is.EqualTo(1));
    }

    [Test]
    public async Task SaleSuccess_ReplacesBuybackLedgerWithoutReconcile()
    {
        Seed(100, 2);
        Assert.That(await _service.ExecuteTradeAsync(Profile, Ticket(sale: true)), Is.EqualTo(StashOperationResult.Success));
        Assert.That(_calls, Is.EqualTo(1));
        Assert.That(_request.purchases, Is.Empty);
        Assert.That((_request.sales[0].lootId, _request.sales[0].amount, _request.sales[0].sourceSlot), Is.EqualTo(("bone", 1, 0)));
        Assert.That(_store.GetCurrency(), Is.EqualTo(105));
        Assert.That(_store.GetLoadout()[0].Amount, Is.EqualTo(1));
        Assert.That(_store.RemoteRevision, Is.EqualTo(8));
        Assert.That(_commits, Is.EqualTo(2));
        Assert.That(_ledger.Entries, Has.Count.EqualTo(1));
        Assert.That(_ledger.Entries[0].EntryId, Is.EqualTo("sale"));
        Assert.That(_reconciliations, Is.Zero);
    }

    [Test]
    public void ShopTradeRequest_SerializesExactS6Body()
    {
        var body = new ShopTradeRequest
        {
            transactionId = "ticket", expectedRevision = 7,
            purchases = new[] { new ShopTradePurchaseLine { lootId = "bone", amount = 1 } },
            sales = new[] { new ShopTradeSaleLine { lootId = "healthpotion", amount = 3, sourceSlot = 4 } }
        };
        Assert.That(InventoryRequestJson.Encode(body), Is.EqualTo(
            "{\"transactionId\":\"ticket\",\"expectedRevision\":7,\"purchases\":[{\"lootId\":\"bone\",\"amount\":1}],\"sales\":[{\"lootId\":\"healthpotion\",\"amount\":3,\"sourceSlot\":4}]}"));
    }

    private void UseSchema(int schema)
    {
        var snapshot = _repository.Snapshot.Clone();
        snapshot.InventorySchemaVersion = schema;
        snapshot.RequiresEquipmentHydration = false;
        Assert.That(_repository.TrySave(snapshot, out var error), Is.True, error);
    }

    private static ShopTransactionResult V3Response() => Newtonsoft.Json.JsonConvert.DeserializeObject<ShopTransactionResult>(
        "{\"revision\":8,\"currency\":123,\"stash\":[{\"slot\":6,\"lootId\":\"bone\",\"amount\":4}]," +
        "\"loadout\":[{\"slot\":7,\"lootId\":\"rapier\",\"amount\":1,\"instance\":{\"instanceId\":\"0123456789abcdef01234567\",\"modifiers\":{\"gemSocketCount\":0,\"gems\":[]}}}]}");

    [TestCase(false)]
    [TestCase(true)]
    public async Task V3_AcknowledgementHydratesSnapshotAndReceiptAtomically(bool buyback)
    {
        var mixed = MixedTicket();
        var ticket = new MerchantTradeTicket(mixed.TransactionId, Array.Empty<MerchantPricedTradeLine>(), mixed.Sales);
        UseSchema(3);
        _tradeResponse = _buybackResponse = V3Response();
        var request = Buyback();
        var id = buyback ? request.TransactionId : ticket.TransactionId;
        var result = buyback ? await _service.ExecuteBuybackAsync(Profile, request) :
            await _service.ExecuteTradeAsync(Profile, ticket);
        Assert.That(_calls, Is.EqualTo(1), "v3 must reach the backend rather than fail the schema guard");
        Assert.That(result, Is.EqualTo(StashOperationResult.Success));
        Assert.That(_store.GetCurrency(), Is.EqualTo(123));
        Assert.That(_store.GetLoadout(), Has.Count.EqualTo(1));
        Assert.That(_store.GetLoadout()[0].Instance?.InstanceId, Is.EqualTo("0123456789abcdef01234567"));
        Assert.That(_store.GetLoadout()[0].Slot, Is.EqualTo(7));
        Assert.That(_store.GetStash(), Has.Count.EqualTo(1));
        Assert.That(_store.GetStash()[0], Is.EqualTo(new StashItem(Bone, 4, 6)));
        Assert.That(_store.RemoteRevision, Is.EqualTo(8));
        Assert.That(_store.IsTradeApplied(Profile, id), Is.True);
        Assert.That(_commits, Is.EqualTo(1));
        Assert.That(_reconciliations, Is.Zero);
        var replay = buyback ? await _service.ExecuteBuybackAsync(Profile, request) :
            await _service.ExecuteTradeAsync(Profile, ticket);
        Assert.That(replay, Is.EqualTo(StashOperationResult.AlreadyApplied));
        Assert.That(_calls, Is.EqualTo(1));
        Assert.That(_commits, Is.EqualTo(1));
    }

    [Test]
    public async Task V3_EquipmentSaleAndPurchase_UsesBackendInstanceAndTotals()
    {
        UseSchema(3);
        var snapshot = _repository.Snapshot.Clone();
        snapshot.Loadout.Add(new StashItem(new LootId("rapier"), 1, 9,
            new EquipmentInstance("1123456789abcdef01234567", default(WeaponInstanceModifiers))));
        Assert.That(_repository.TrySave(snapshot, out var error), Is.True, error);
        var ticket = new MerchantTradeTicket(new ShopTransactionId(1000, Guid.NewGuid()),
            new[] { new MerchantPricedTradeLine(new LootId("rapier"), 1, 20) },
            new[] { new MerchantPricedTradeLine(new LootId("rapier"), 1, 1) });
        _tradeResponse = V3Response();
        Assert.That(await _service.ExecuteTradeAsync(Profile, ticket), Is.EqualTo(StashOperationResult.Success));
        Assert.That(_calls, Is.EqualTo(1));
        Assert.That(_request.sales[0].sourceSlot, Is.EqualTo(9));
        Assert.That(_store.GetLoadout()[0].Instance?.InstanceId, Is.EqualTo("0123456789abcdef01234567"));
        Assert.That(_store.GetCurrency(), Is.EqualTo(123), "Never commit the advisory 81 gold projection.");
        Assert.That(_store.IsTradeApplied(Profile, ticket.TransactionId), Is.True);
        Assert.That(_commits, Is.EqualTo(1));
        Assert.That(_reconciliations, Is.Zero);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task UnsupportedSchema_LogsVersionWithoutCallingBackend(bool buyback)
    {
        UseSchema(1);
        LogAssert.Expect(LogType.Warning, "[RemoteShopTransactionService] Unsupported inventory schema version 1; commerce requires schema 2 or 3.");
        var result = buyback ? await _service.ExecuteBuybackAsync(Profile, Buyback()) :
            await _service.ExecuteTradeAsync(Profile, Ticket());
        Assert.That(result, Is.EqualTo(StashOperationResult.PersistenceFailed));
        Assert.That(_calls, Is.Zero);
        AssertUnchanged(100, 0);
        Assert.That(_reconciliations, Is.Zero);
    }

    private void Seed(long currency, int bone = 0)
    {
        var snapshot = _repository.Snapshot.Clone();
        snapshot.Currency = currency;
        snapshot.InventorySchemaVersion = 2;
        snapshot.Loadout.Clear();
        if (bone > 0) snapshot.Loadout.Add(new StashItem(Bone, bone, 0));
        Assert.That(_repository.TrySave(snapshot, out string error), Is.True, error);
    }

    private void AssertUnchanged(long currency, int bone)
    {
        Assert.That(_store.GetCurrency(), Is.EqualTo(currency));
        Assert.That(_store.GetLoadout().Count, Is.EqualTo(bone == 0 ? 0 : 1));
        if (bone > 0) Assert.That(_store.GetLoadout()[0].Amount, Is.EqualTo(bone));
        Assert.That(_repository.Snapshot.AppliedShopTransactionReceipts, Is.Empty);
        Assert.That(_store.RemoteRevision, Is.EqualTo(7));
        Assert.That(_commits, Is.Zero);
    }

    private static MerchantTradeTicket Ticket(bool sale = false, bool multi = false)
    {
        var lines = new List<MerchantPricedTradeLine> { new MerchantPricedTradeLine(Bone, 1, 5) };
        if (multi) lines.Add(new MerchantPricedTradeLine(new LootId("healthpotion"), 1, 5));
        return new MerchantTradeTicket(new ShopTransactionId(1000, Guid.NewGuid()),
            sale ? Array.Empty<MerchantPricedTradeLine>() : lines,
            sale ? lines : Array.Empty<MerchantPricedTradeLine>());
    }

    private sealed class MemoryFileStore : ILocalProfileFileStore
    {
        private readonly Dictionary<string, string> _files = new Dictionary<string, string>();
        public bool FailWrites;
        public bool Exists(string path) => _files.ContainsKey(path);
        public bool TryRead(string path, out string contents, out string error)
        {
            error = null;
            return _files.TryGetValue(path, out contents);
        }
        public bool TryWriteAtomically(string mainPath, string temporaryPath, string backupPath, string contents, out string error)
        {
            if (FailWrites) { error = "Simulated disk failure"; return false; }
            if (_files.TryGetValue(mainPath, out string previous)) _files[backupPath] = previous;
            _files[mainPath] = contents;
            error = null;
            return true;
        }
        public bool TryRestoreMainFromBackup(string mainPath, string backupPath, out string error)
        {
            error = null;
            if (!_files.TryGetValue(backupPath, out string contents)) return false;
            _files[mainPath] = contents;
            return true;
        }
    }
}
