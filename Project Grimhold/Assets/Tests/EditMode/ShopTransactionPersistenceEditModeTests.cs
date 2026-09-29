#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

public sealed class ShopTransactionPersistenceEditModeTests
{
    private static readonly ProfileId Profile = new ProfileId("11111111111111111111111111111111");
    private static readonly LootId Potion = new LootId("healthpotion");
    private static readonly LootId Bone = new LootId("bone");
    private static readonly LootId Coins = new LootId("coins");

    private LootDefinitionCatalog _catalog;
    private MemoryFileStore _files;
    private LocalProfileRepository _repository;
    private LocalProfileStore _store;
    private int _commits;
    private long _nextTimestamp;

    [SetUp]
    public void SetUp()
    {
        _catalog = AssetDatabase.LoadAssetAtPath<LootDefinitionCatalog>(
            "Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset");
        Assert.That(_catalog, Is.Not.Null);

        _files = new MemoryFileStore();
        _repository = new LocalProfileRepository(_files, ".");
        Assert.That(_repository.Initialize(Profile, _catalog), Is.True);
        _store = new LocalProfileStore(_repository, Profile);
        _store.ProfileCommitted += _ => _commits++;
        _nextTimestamp = 1000;
    }

    // --- Multi-line trades ---

    [Test]
    public void MultiLinePurchase_DebitsTotalAndAddsEveryLine()
    {
        Seed(currency: 500);

        Assert.That(Trade(Buy(Potion, 2, 50), Buy(Bone, 3, 10)), Is.EqualTo(StashOperationResult.Success));

        Assert.That(_store.GetCurrency(), Is.EqualTo(370L));
        AssertLoadout((Potion, 2), (Bone, 3));
    }

    [Test]
    public void MultiLineSale_CreditsTotalAndRemovesEveryLine()
    {
        Seed(currency: 0, (Potion, 5), (Bone, 2));

        Assert.That(Trade(Sell(Potion, 3, 20), Sell(Bone, 2, 5)), Is.EqualTo(StashOperationResult.Success));

        Assert.That(_store.GetCurrency(), Is.EqualTo(70L));
        AssertLoadout((Potion, 2));
    }

    [Test]
    public void PurchaseAndSale_AreAppliedTogether()
    {
        Seed(currency: 100, (Coins, 4));

        Assert.That(Trade(Buy(Potion, 1, 30), Sell(Coins, 4, 10)), Is.EqualTo(StashOperationResult.Success));

        Assert.That(_store.GetCurrency(), Is.EqualTo(110L));
        AssertLoadout((Potion, 1));
    }

    [Test]
    public void SaleFundsPurchase_OnTheFinalBalance()
    {
        Seed(currency: 0, (Coins, 3));

        Assert.That(Trade(Buy(Potion, 1, 30), Sell(Coins, 3, 10)), Is.EqualTo(StashOperationResult.Success));

        Assert.That(_store.GetCurrency(), Is.Zero);
        AssertLoadout((Potion, 1));
    }

    [Test]
    public void SaleOfWholeStack_FreesSlotForPurchase()
    {
        LootId[] ids = FillLoadoutToCapacity(out LootId newLoot);

        Assert.That(Trade(Buy(newLoot, 1, 1), Sell(ids[0], 2, 1)), Is.EqualTo(StashOperationResult.Success));

        Assert.That(_store.GetLoadout().Count, Is.EqualTo(LocalProfileSnapshot.MaxLoadoutSlots));
        Assert.That(_store.GetLoadout().Any(item => item.LootId == newLoot), Is.True);
    }

    [Test]
    public void PurchaseIntoExistingStack_FitsAFullInventory()
    {
        LootId[] ids = FillLoadoutToCapacity(out _);

        Assert.That(Trade(Buy(ids[0], 5, 1)), Is.EqualTo(StashOperationResult.Success));
    }

    // --- Rejections leave the profile unchanged ---

    [Test]
    public void FinalCapacityExceeded_RejectsWholeTrade()
    {
        LootId[] ids = FillLoadoutToCapacity(out LootId newLoot);

        AssertRejectedWithoutChange(
            StashOperationResult.InvalidInventory,
            Buy(newLoot, 1, 1),
            Sell(ids[0], 1, 1),
            Buy(ids[1], 1, 1));
    }

    [Test]
    public void PartialSale_DoesNotFreeItsSlot()
    {
        LootId[] ids = FillLoadoutToCapacity(out LootId newLoot);

        AssertRejectedWithoutChange(StashOperationResult.InvalidInventory, Buy(newLoot, 1, 1), Sell(ids[0], 1, 1));
    }

    [Test]
    public void SaleAboveOwnedUnits_RejectsWholeTrade()
    {
        Seed(currency: 100, (Potion, 5), (Bone, 1));

        AssertRejectedWithoutChange(StashOperationResult.InvalidInventory, Sell(Potion, 2, 20), Sell(Bone, 2, 5));
    }

    [Test]
    public void InsufficientCurrency_RejectsWholeTrade()
    {
        Seed(currency: 50, (Coins, 1));

        AssertRejectedWithoutChange(StashOperationResult.InvalidInventory, Buy(Potion, 2, 30), Sell(Coins, 1, 9));
    }

    [Test]
    public void CurrencyOverflow_RejectsWholeTrade()
    {
        Seed(currency: long.MaxValue - 5, (Coins, 1));

        AssertRejectedWithoutChange(StashOperationResult.InvalidInventory, Sell(Coins, 1, 10));
    }

    [Test]
    public void PurchaseTotalOverflow_RejectsWholeTrade()
    {
        Seed(currency: long.MaxValue);

        AssertRejectedWithoutChange(
            StashOperationResult.InvalidInventory,
            Buy(Potion, int.MaxValue, int.MaxValue),
            Buy(Bone, int.MaxValue, int.MaxValue),
            Buy(Coins, int.MaxValue, int.MaxValue));
    }

    [Test]
    public void StackOverflow_RejectsWholeTrade()
    {
        Seed(currency: 100, (Potion, int.MaxValue));

        AssertRejectedWithoutChange(StashOperationResult.InvalidInventory, Buy(Bone, 1, 1), Buy(Potion, 1, 1));
    }

    [TestCaseSource(nameof(MalformedTickets))]
    public void MalformedTicket_IsRejectedWithoutChange(MerchantTradeTicket ticket)
    {
        Seed(currency: 100, (Potion, 5));

        AssertRejectedWithoutChange(StashOperationResult.InvalidInventory, () => _store.TryCommitTrade(Profile, ticket));
    }

    [Test]
    public void TicketForAnotherProfile_IsRejectedWithoutChange()
    {
        Seed(currency: 100);

        AssertRejectedWithoutChange(
            StashOperationResult.InvalidInventory,
            () => _store.TryCommitTrade(new ProfileId("22222222222222222222222222222222"), Ticket(NextId(), Buy(Potion, 1, 1))));
    }

    [Test]
    public void PersistenceFailure_LeavesProfileUnchanged()
    {
        Seed(currency: 100, (Coins, 2));
        _files.FailWrites = true;

        AssertRejectedWithoutChange(StashOperationResult.PersistenceFailed, Buy(Potion, 1, 30), Sell(Coins, 2, 10));
    }

    // --- Same loot on both sides: independent lines, never netted ---

    [Test]
    public void SameLootBoughtAndSold_KeepsLinesIndependent()
    {
        Seed(currency: 200, (Potion, 2));

        Assert.That(Trade(Buy(Potion, 5, 30), Sell(Potion, 2, 10)), Is.EqualTo(StashOperationResult.Success));

        Assert.That(_store.GetCurrency(), Is.EqualTo(200L + 20L - 150L));
        AssertLoadout((Potion, 5));
    }

    [Test]
    public void SameLootBoughtAndSold_SaleIsCheckedAgainstConfirmedUnitsOnly()
    {
        Seed(currency: 200, (Potion, 2));

        AssertRejectedWithoutChange(StashOperationResult.InvalidInventory, Buy(Potion, 5, 30), Sell(Potion, 3, 10));
    }

    // --- Commit, idempotency and preview parity ---

    [Test]
    public void SuccessfulMultiLineTrade_PublishesExactlyOneCommit()
    {
        Seed(currency: 500, (Coins, 2));
        _commits = 0;

        Trade(Buy(Potion, 1, 30), Buy(Bone, 2, 5), Sell(Coins, 2, 10));

        Assert.That(_commits, Is.EqualTo(1));
    }

    [Test]
    public void SameTicket_IsAppliedOnceAndRetryReturnsAlreadyApplied()
    {
        Seed(currency: 100, (Coins, 2));
        MerchantTradeTicket ticket = Ticket(NextId(), Buy(Potion, 1, 30), Sell(Coins, 2, 10));
        Assert.That(_store.TryCommitTrade(Profile, ticket), Is.EqualTo(StashOperationResult.Success));
        _commits = 0;

        Assert.That(_store.TryCommitTrade(Profile, ticket), Is.EqualTo(StashOperationResult.AlreadyApplied));

        Assert.That(_store.GetCurrency(), Is.EqualTo(90L));
        AssertLoadout((Potion, 1));
        Assert.That(_commits, Is.Zero);
    }

    [Test]
    public void IsTradeApplied_ReflectsOnlyPersistedTickets()
    {
        Seed(currency: 100);
        MerchantTradeTicket persisted = Ticket(NextId(), Buy(Potion, 1, 30));
        MerchantTradeTicket rejected = Ticket(NextId(), Buy(Potion, 10, 30));

        _store.TryCommitTrade(Profile, persisted);
        _store.TryCommitTrade(Profile, rejected);

        Assert.That(_store.IsTradeApplied(Profile, persisted.TransactionId), Is.True);
        Assert.That(_store.IsTradeApplied(Profile, rejected.TransactionId), Is.False);
        Assert.That(_store.IsTradeApplied(new ProfileId("other"), persisted.TransactionId), Is.False);
    }

    [Test]
    public void RetryOfAppliedTicket_IsAlreadyAppliedEvenWhenItsSalesAreNoLongerOwned()
    {
        Seed(currency: 0, (Coins, 2));
        MerchantTradeTicket ticket = Ticket(NextId(), Sell(Coins, 2, 10));
        _store.TryCommitTrade(Profile, ticket);

        Assert.That(_store.TryCommitTrade(Profile, ticket), Is.EqualTo(StashOperationResult.AlreadyApplied));
        Assert.That(_store.GetCurrency(), Is.EqualTo(20L));
    }

    [Test]
    public void RejectedTicket_CanBeRetriedAfterTheProfileChanges()
    {
        Seed(currency: 10);
        MerchantTradeTicket ticket = Ticket(NextId(), Buy(Potion, 1, 30));
        Assert.That(_store.TryCommitTrade(Profile, ticket), Is.EqualTo(StashOperationResult.InvalidInventory));

        _store.TryCreditCurrency(20);

        Assert.That(_store.TryCommitTrade(Profile, ticket), Is.EqualTo(StashOperationResult.Success));
    }

    [Test]
    public void Trade_NeverTouchesStash()
    {
        Seed(currency: 100, (Coins, 1));
        _store.TrySecureLoot(new[] { new StashItem(Coins, 7) });

        Trade(Buy(Potion, 1, 30), Sell(Coins, 1, 10));

        Assert.That(_store.GetStash(), Is.EqualTo(new[] { new StashItem(Coins, 7) }));
    }

    [Test]
    public void PersistedTrade_MatchesPreviewProjection()
    {
        Seed(currency: 1000, (Coins, 3), (Bone, 4));
        Assert.That(_catalog.TryGet(Potion.Value, out LootDefinition potion), Is.True);
        Assert.That(_catalog.TryGet(Bone.Value, out LootDefinition bone), Is.True);
        Assert.That(_catalog.TryGet(Coins.Value, out LootDefinition coins), Is.True);
        Assume.That(potion.BuyValuePerUnit, Is.GreaterThan(0), "Preview parity needs a purchasable loot.");

        var draft = new MerchantTradeDraft();
        draft.TryAddPurchase(Potion, 2);
        draft.TryAddSale(Coins, 3);
        draft.TryAddSale(Bone, 1);
        MerchantTradePreview preview = MerchantTradePreview.Calculate(
            _store.GetLoadout().Select(item => new LootEntry(item.LootId, item.Amount)).ToList(),
            LocalProfileSnapshot.MaxLoadoutSlots,
            _store.GetCurrency(),
            draft,
            _ => MerchantStockItem.UnlimitedQuantity,
            _catalog);
        Assume.That(preview.CanConfirm, Is.True);

        Assert.That(Trade(
            Buy(Potion, 2, potion.BuyValuePerUnit),
            Sell(Coins, 3, coins.SellValuePerUnit),
            Sell(Bone, 1, bone.SellValuePerUnit)), Is.EqualTo(StashOperationResult.Success));

        Assert.That(_store.GetCurrency(), Is.EqualTo(preview.ProjectedCurrency));
        Assert.That(
            _store.GetLoadout().Select(item => new LootEntry(item.LootId, item.Amount)),
            Is.EquivalentTo(preview.ProjectedInventory));
    }

    // --- Receipt retention ---

    [Test]
    public void ReceiptPruning_UpdatesWatermarkOnEviction()
    {
        Seed(currency: 100000);

        for (int i = 0; i < LocalProfileSnapshot.MaxAppliedShopTransactionReceipts; i++)
        {
            _store.TryCommitTrade(Profile, Ticket(new ShopTransactionId(1000 + i, Guid.NewGuid()), Buy(Potion, 1, 1)));
        }

        Assert.That(_repository.Snapshot.ShopIdempotencyWatermark, Is.EqualTo(0L));
        Assert.That(_repository.Snapshot.AppliedShopTransactionReceipts.Count, Is.EqualTo(LocalProfileSnapshot.MaxAppliedShopTransactionReceipts));

        _store.TryCommitTrade(Profile, Ticket(new ShopTransactionId(2000, Guid.NewGuid()), Buy(Potion, 1, 1)));

        Assert.That(_repository.Snapshot.AppliedShopTransactionReceipts.Count, Is.EqualTo(LocalProfileSnapshot.MaxAppliedShopTransactionReceipts));
        Assert.That(_repository.Snapshot.ShopIdempotencyWatermark, Is.EqualTo(1000L));
    }

    [Test]
    public void TicketAtOrBelowWatermark_ReturnsAlreadyApplied()
    {
        LocalProfileSnapshot snapshot = _repository.Snapshot.Clone();
        snapshot.Currency = 100;
        snapshot.ShopIdempotencyWatermark = 5000L;
        _repository.TrySave(snapshot, out _);

        Assert.That(_store.TryCommitTrade(Profile, Ticket(new ShopTransactionId(4500, Guid.NewGuid()), Buy(Potion, 1, 1))), Is.EqualTo(StashOperationResult.AlreadyApplied));
        Assert.That(_store.TryCommitTrade(Profile, Ticket(new ShopTransactionId(5000, Guid.NewGuid()), Buy(Potion, 1, 1))), Is.EqualTo(StashOperationResult.AlreadyApplied));
        Assert.That(_store.TryCommitTrade(Profile, Ticket(new ShopTransactionId(5001, Guid.NewGuid()), Buy(Potion, 1, 1))), Is.EqualTo(StashOperationResult.Success));
    }

    [Test]
    public void CodecRoundtrip_MaintainsShopIdempotencyFields()
    {
        var snapshot = new LocalProfileSnapshot { ProfileId = Profile, ShopIdempotencyWatermark = 12345L };
        var txId = new ShopTransactionId(67890L, Guid.NewGuid());
        snapshot.AppliedShopTransactionReceipts.Add(new ShopTransactionReceipt(txId, Profile));

        string json = LocalProfileSaveCodec.Encode(snapshot);
        bool decoded = LocalProfileSaveCodec.TryDecode(json, Profile, _catalog, out var restored, out _, out string error);

        Assert.That(decoded, Is.True, error);
        Assert.That(restored.ShopIdempotencyWatermark, Is.EqualTo(12345L));
        Assert.That(restored.AppliedShopTransactionReceipts.Count, Is.EqualTo(1));
        Assert.That(restored.AppliedShopTransactionReceipts[0].TransactionId.Timestamp, Is.EqualTo(67890L));
        Assert.That(restored.AppliedShopTransactionReceipts[0].TransactionId.Value, Is.EqualTo(txId.Value));
    }

    // --- Helpers ---

    private static IEnumerable<TestCaseData> MalformedTickets()
    {
        var id = new ShopTransactionId(1000, Guid.NewGuid());
        MerchantPricedTradeLine[] none = Array.Empty<MerchantPricedTradeLine>();
        yield return new TestCaseData(new MerchantTradeTicket(id, none, none)).SetName("EmptyTicket");
        yield return new TestCaseData(new MerchantTradeTicket(default, new[] { Line(Potion, 1, 1) }, none)).SetName("InvalidTransactionId");
        yield return new TestCaseData(new MerchantTradeTicket(id, new[] { Line(default, 1, 1) }, none)).SetName("InvalidLootId");
        yield return new TestCaseData(new MerchantTradeTicket(id, new[] { Line(Potion, 0, 1) }, none)).SetName("ZeroAmount");
        yield return new TestCaseData(new MerchantTradeTicket(id, none, new[] { Line(Potion, -1, 1) })).SetName("NegativeAmount");
        yield return new TestCaseData(new MerchantTradeTicket(id, new[] { Line(Potion, 1, 0) }, none)).SetName("NonPositiveBuyPrice");
        yield return new TestCaseData(new MerchantTradeTicket(id, none, new[] { Line(Potion, 1, -1) })).SetName("NegativeSellPrice");
        yield return new TestCaseData(new MerchantTradeTicket(id, new[] { Line(Bone, 1, 1), Line(Bone, 1, 1) }, none)).SetName("DuplicatePurchaseLoot");
        yield return new TestCaseData(new MerchantTradeTicket(id, none, new[] { Line(Potion, 1, 1), Line(Potion, 1, 1) })).SetName("DuplicateSaleLoot");
        yield return new TestCaseData(new object[] { null }).SetName("NullTicket");
    }

    private void Seed(long currency, params (LootId lootId, int amount)[] loadout)
    {
        LocalProfileSnapshot snapshot = _repository.Snapshot.Clone();
        snapshot.Currency = currency;
        snapshot.Loadout.Clear();
        foreach ((LootId lootId, int amount) in loadout) snapshot.Loadout.Add(new StashItem(lootId, amount));
        Assert.That(_repository.TrySave(snapshot, out string error), Is.True, error);
    }

    private LootId[] FillLoadoutToCapacity(out LootId unusedLoot)
    {
        var ids = new List<LootId>();
        for (int i = 0; i < _catalog.DefinitionCount; i++)
        {
            Assert.That(_catalog.TryGetByIndex(i, out LootDefinition definition), Is.True);
            ids.Add(new LootId(definition.Id));
        }

        Assume.That(ids.Count, Is.GreaterThan(LocalProfileSnapshot.MaxLoadoutSlots), "Capacity tests need one loot beyond the slot capacity.");
        LootId[] loadout = ids.Take(LocalProfileSnapshot.MaxLoadoutSlots).ToArray();
        unusedLoot = ids[LocalProfileSnapshot.MaxLoadoutSlots];
        Seed(currency: 100, loadout.Select(id => (id, 2)).ToArray());
        return loadout;
    }

    private StashOperationResult Trade(params (bool isPurchase, MerchantPricedTradeLine line)[] lines) =>
        _store.TryCommitTrade(Profile, Ticket(NextId(), lines));

    private void AssertRejectedWithoutChange(StashOperationResult expected, params (bool isPurchase, MerchantPricedTradeLine line)[] lines)
    {
        AssertRejectedWithoutChange(expected, () => _store.TryCommitTrade(Profile, Ticket(NextId(), lines)));
    }

    private void AssertRejectedWithoutChange(StashOperationResult expected, Func<StashOperationResult> trade)
    {
        long currency = _store.GetCurrency();
        StashItem[] loadout = _store.GetLoadout().ToArray();
        int receipts = _repository.Snapshot.AppliedShopTransactionReceipts.Count;
        _commits = 0;

        Assert.That(trade(), Is.EqualTo(expected));

        Assert.That(_store.GetCurrency(), Is.EqualTo(currency));
        Assert.That(_store.GetLoadout(), Is.EqualTo(loadout));
        Assert.That(_repository.Snapshot.AppliedShopTransactionReceipts.Count, Is.EqualTo(receipts));
        Assert.That(_commits, Is.Zero);
    }

    private void AssertLoadout(params (LootId lootId, int amount)[] expected)
    {
        Assert.That(_store.GetLoadout(), Is.EquivalentTo(expected.Select(item => new StashItem(item.lootId, item.amount))));
    }

    private ShopTransactionId NextId() => new ShopTransactionId(_nextTimestamp++, Guid.NewGuid());

    private static MerchantTradeTicket Ticket(ShopTransactionId id, params (bool isPurchase, MerchantPricedTradeLine line)[] lines) =>
        new MerchantTradeTicket(
            id,
            lines.Where(entry => entry.isPurchase).Select(entry => entry.line),
            lines.Where(entry => !entry.isPurchase).Select(entry => entry.line));

    private static (bool, MerchantPricedTradeLine) Buy(LootId lootId, int amount, int unitPrice) => (true, Line(lootId, amount, unitPrice));
    private static (bool, MerchantPricedTradeLine) Sell(LootId lootId, int amount, int unitPrice) => (false, Line(lootId, amount, unitPrice));
    private static MerchantPricedTradeLine Line(LootId lootId, int amount, int unitPrice) => new MerchantPricedTradeLine(lootId, amount, unitPrice);

    // Helper file store for edit mode tests without touching real disk
    private sealed class MemoryFileStore : ILocalProfileFileStore
    {
        public readonly Dictionary<string, string> Files = new(StringComparer.Ordinal);
        public bool FailWrites;

        public bool Exists(string path) => Files.ContainsKey(path);
        public bool TryRead(string path, out string contents, out string error)
        {
            if (Files.TryGetValue(path, out contents)) { error = null; return true; }
            error = "Missing file";
            return false;
        }

        public bool TryWriteAtomically(string mainPath, string temporaryPath, string backupPath, string contents, out string error)
        {
            if (FailWrites)
            {
                error = "Simulated disk failure";
                return false;
            }

            if (Files.TryGetValue(mainPath, out string previous)) Files[backupPath] = previous;
            Files[temporaryPath] = contents;
            Files[mainPath] = Files[temporaryPath];
            Files.Remove(temporaryPath);
            error = null;
            return true;
        }

        public bool TryRestoreMainFromBackup(string mainPath, string backupPath, out string error)
        {
            if (!Files.TryGetValue(backupPath, out string backup)) { error = "Missing backup"; return false; }
            Files[mainPath] = backup;
            error = null;
            return true;
        }
    }
}
#endif
