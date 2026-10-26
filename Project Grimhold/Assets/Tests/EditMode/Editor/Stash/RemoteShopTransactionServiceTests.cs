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
    private int _calls;
    private int _reconciliations;
    private int _commits;
    private bool _success;
    private string _error;
    private bool _throw;
    private bool _purchase;
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
        _store = new LocalProfileStore(_repository, Profile);
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
    }

    [TearDown]
    public void TearDown() => UnityEngine.Object.DestroyImmediate(_owner);

    private Task<(bool success, ShopTransactionResult data, BackendError error)> Backend(
        bool purchase, string transactionId, MerchantPricedTradeLine line, int revision)
    {
        _calls++;
        _purchase = purchase;
        _revision = revision;
        _transactionId = transactionId;
        if (_throw) throw new InvalidOperationException("Transport lost");
        if (_failLocalWrites) _files.FailWrites = true;
        return _pending?.Task ?? Task.FromResult((_success, new ShopTransactionResult { revision = 8, buyback = Rows("sale") }, new BackendError { error = _error }));
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
        Assert.That(_revision, Is.EqualTo(7));
        Assert.That(_transactionId, Is.EqualTo(ticket.TransactionId.Value.ToString("N")));
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

    [Test]
    public async Task MultiLine_IsRefusedBeforeCallingBackend()
    {
        LogAssert.Expect(LogType.Error, new Regex("has 2 lines; the backend has no atomic multi-line trade endpoint"));
        Assert.That(await _service.ExecuteTradeAsync(Profile, Ticket(multi: true)), Is.EqualTo(StashOperationResult.PersistenceFailed));
        Assert.That(_calls, Is.Zero);
        AssertUnchanged(100, 0);
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
        Assert.That(_transactionId, Is.EqualTo(request.TransactionId.Value.ToString("N")));
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
            new[] { new StashItem(new LootId("arming_sword"), 1) },
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
                snapshot.Loadout.Add(new StashItem(definition.LootId, 1));
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
        Assert.That(_ledger.Entries, Has.Count.EqualTo(1));
        Assert.That(_ledger.Entries[0].EntryId, Is.EqualTo("sale"));
        Assert.That(_reconciliations, Is.Zero);
    }

    private void Seed(long currency, int bone = 0)
    {
        var snapshot = _repository.Snapshot.Clone();
        snapshot.Currency = currency;
        snapshot.Loadout.Clear();
        if (bone > 0) snapshot.Loadout.Add(new StashItem(Bone, bone));
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
