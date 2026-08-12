#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class ShopTransactionPersistenceEditModeTests
{
    private LootDefinitionCatalog _catalog;

    [SetUp]
    public void SetUp()
    {
        _catalog = AssetDatabase.LoadAssetAtPath<LootDefinitionCatalog>(
            "Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset");
        Assert.That(_catalog, Is.Not.Null);
    }

    private (LocalProfileRepository, LocalProfileStore) CreateStore(ProfileId profile, MemoryFileStore files)
    {
        var repository = new LocalProfileRepository(files, ".");
        Assert.That(repository.Initialize(profile, _catalog), Is.True);
        var store = new LocalProfileStore(repository, profile);
        return (repository, store);
    }

    [Test]
    public void TC_S01_PurchaseSuccess_DeductsCurrencyAndAddsItem()
    {
        var files = new MemoryFileStore();
        var profile = new ProfileId("11111111111111111111111111111111");
        var (repo, store) = CreateStore(profile, files);

        store.TryCreditCurrency(500);
        
        var txId = new ShopTransactionId(1000, Guid.NewGuid());
        var receipt = new ShopTransactionReceipt(txId, profile);
        var lootId = new LootId("healthpotion");

        Assert.That(store.TryCommitPurchase(receipt, lootId, 2, 150), Is.EqualTo(StashOperationResult.Success));
        
        Assert.That(store.GetCurrency(), Is.EqualTo(350L));
        var stash = store.GetStash();
        Assert.That(stash.Count, Is.EqualTo(1));
        Assert.That(stash[0].LootId, Is.EqualTo(lootId));
        Assert.That(stash[0].Amount, Is.EqualTo(2));
    }

    [Test]
    public void TC_S02_PurchaseInsufficientFunds_LeavesMemoryIntact()
    {
        var files = new MemoryFileStore();
        var profile = new ProfileId("11111111111111111111111111111111");
        var (repo, store) = CreateStore(profile, files);

        store.TryCreditCurrency(100);
        
        var txId = new ShopTransactionId(1000, Guid.NewGuid());
        var receipt = new ShopTransactionReceipt(txId, profile);
        var lootId = new LootId("healthpotion");

        Assert.That(store.TryCommitPurchase(receipt, lootId, 2, 150), Is.EqualTo(StashOperationResult.InvalidInventory));
        
        Assert.That(store.GetCurrency(), Is.EqualTo(100L));
        Assert.That(store.GetStash().Count, Is.EqualTo(0));
    }

    [Test]
    public void TC_S03_PurchaseIdempotency_SameReceiptReturnsAlreadyApplied()
    {
        var files = new MemoryFileStore();
        var profile = new ProfileId("11111111111111111111111111111111");
        var (repo, store) = CreateStore(profile, files);

        store.TryCreditCurrency(500);
        
        var txId = new ShopTransactionId(1000, Guid.NewGuid());
        var receipt = new ShopTransactionReceipt(txId, profile);
        var lootId = new LootId("healthpotion");

        Assert.That(store.TryCommitPurchase(receipt, lootId, 1, 50), Is.EqualTo(StashOperationResult.Success));
        Assert.That(store.GetCurrency(), Is.EqualTo(450L));

        Assert.That(store.TryCommitPurchase(receipt, lootId, 1, 50), Is.EqualTo(StashOperationResult.AlreadyApplied));
        Assert.That(store.GetCurrency(), Is.EqualTo(450L));
        Assert.That(store.GetStash()[0].Amount, Is.EqualTo(1));
    }

    [Test]
    public void TC_S04_PurchaseNegativePrice_ReturnsInvalidInventory()
    {
        var files = new MemoryFileStore();
        var profile = new ProfileId("11111111111111111111111111111111");
        var (repo, store) = CreateStore(profile, files);

        var txId = new ShopTransactionId(1000, Guid.NewGuid());
        var receipt = new ShopTransactionReceipt(txId, profile);
        var lootId = new LootId("healthpotion");

        Assert.That(store.TryCommitPurchase(receipt, lootId, 1, -10), Is.EqualTo(StashOperationResult.InvalidInventory));
    }

    [Test]
    public void TC_S05_PurchasePersistenceFailure_LeavesMemoryIntact()
    {
        var files = new MemoryFileStore();
        var profile = new ProfileId("11111111111111111111111111111111");
        var (repo, store) = CreateStore(profile, files);

        store.TryCreditCurrency(500);
        
        var txId = new ShopTransactionId(1000, Guid.NewGuid());
        var receipt = new ShopTransactionReceipt(txId, profile);
        var lootId = new LootId("healthpotion");

        files.FailWrites = true;
        Assert.That(store.TryCommitPurchase(receipt, lootId, 1, 50), Is.EqualTo(StashOperationResult.PersistenceFailed));
        
        Assert.That(store.GetCurrency(), Is.EqualTo(500L));
        Assert.That(store.GetStash().Count, Is.EqualTo(0));
    }

    [Test]
    public void TC_S06_SaleSuccess_RemovesItemAndCreditsCurrency()
    {
        var files = new MemoryFileStore();
        var profile = new ProfileId("11111111111111111111111111111111");
        var (repo, store) = CreateStore(profile, files);

        var lootId = new LootId("healthpotion");
        store.TrySecureLoot(new[] { new StashItem(lootId, 5) });
        Assert.That(store.GetCurrency(), Is.EqualTo(0L));

        var txId = new ShopTransactionId(1000, Guid.NewGuid());
        var receipt = new ShopTransactionReceipt(txId, profile);

        Assert.That(store.TryCommitSale(receipt, lootId, 2, 80), Is.EqualTo(StashOperationResult.Success));
        
        Assert.That(store.GetCurrency(), Is.EqualTo(80L));
        var stash = store.GetStash();
        Assert.That(stash.Count, Is.EqualTo(1));
        Assert.That(stash[0].Amount, Is.EqualTo(3));
    }

    [Test]
    public void TC_S07_SaleMissingItems_ReturnsInvalidInventoryAndMemoryIntact()
    {
        var files = new MemoryFileStore();
        var profile = new ProfileId("11111111111111111111111111111111");
        var (repo, store) = CreateStore(profile, files);

        var lootId = new LootId("healthpotion");
        store.TrySecureLoot(new[] { new StashItem(lootId, 2) });
        
        var txId = new ShopTransactionId(1000, Guid.NewGuid());
        var receipt = new ShopTransactionReceipt(txId, profile);

        Assert.That(store.TryCommitSale(receipt, lootId, 3, 120), Is.EqualTo(StashOperationResult.InvalidInventory));
        
        Assert.That(store.GetCurrency(), Is.EqualTo(0L));
        Assert.That(store.GetStash()[0].Amount, Is.EqualTo(2));
    }

    [Test]
    public void TC_S08_SaleIdempotency_SameReceiptReturnsAlreadyApplied()
    {
        var files = new MemoryFileStore();
        var profile = new ProfileId("11111111111111111111111111111111");
        var (repo, store) = CreateStore(profile, files);

        var lootId = new LootId("healthpotion");
        store.TrySecureLoot(new[] { new StashItem(lootId, 5) });
        
        var txId = new ShopTransactionId(1000, Guid.NewGuid());
        var receipt = new ShopTransactionReceipt(txId, profile);

        Assert.That(store.TryCommitSale(receipt, lootId, 1, 40), Is.EqualTo(StashOperationResult.Success));
        Assert.That(store.GetCurrency(), Is.EqualTo(40L));

        Assert.That(store.TryCommitSale(receipt, lootId, 1, 40), Is.EqualTo(StashOperationResult.AlreadyApplied));
        Assert.That(store.GetCurrency(), Is.EqualTo(40L));
        Assert.That(store.GetStash()[0].Amount, Is.EqualTo(4));
    }

    [Test]
    public void TC_S09_SaleOverflow_ReturnsInvalidInventory()
    {
        var files = new MemoryFileStore();
        var profile = new ProfileId("11111111111111111111111111111111");
        var (repo, store) = CreateStore(profile, files);

        var snapshot = repo.Snapshot.Clone();
        snapshot.Currency = long.MaxValue;
        repo.TrySave(snapshot, out _);

        var lootId = new LootId("healthpotion");
        store.TrySecureLoot(new[] { new StashItem(lootId, 1) });
        
        var txId = new ShopTransactionId(1000, Guid.NewGuid());
        var receipt = new ShopTransactionReceipt(txId, profile);

        Assert.That(store.TryCommitSale(receipt, lootId, 1, 10), Is.EqualTo(StashOperationResult.InvalidInventory));
        
        Assert.That(store.GetCurrency(), Is.EqualTo(long.MaxValue));
        Assert.That(store.GetStash()[0].Amount, Is.EqualTo(1));
    }

    [Test]
    public void TC_S10_SalePersistenceFailure_LeavesMemoryIntact()
    {
        var files = new MemoryFileStore();
        var profile = new ProfileId("11111111111111111111111111111111");
        var (repo, store) = CreateStore(profile, files);

        var lootId = new LootId("healthpotion");
        store.TrySecureLoot(new[] { new StashItem(lootId, 5) });
        
        var txId = new ShopTransactionId(1000, Guid.NewGuid());
        var receipt = new ShopTransactionReceipt(txId, profile);

        files.FailWrites = true;
        Assert.That(store.TryCommitSale(receipt, lootId, 1, 40), Is.EqualTo(StashOperationResult.PersistenceFailed));
        
        Assert.That(store.GetCurrency(), Is.EqualTo(0L));
        Assert.That(store.GetStash()[0].Amount, Is.EqualTo(5));
    }

    [Test]
    public void TC_S11_PurchasePruningWatermark_UpdatesWatermarkOnEviction()
    {
        var files = new MemoryFileStore();
        var profile = new ProfileId("11111111111111111111111111111111");
        var (repo, store) = CreateStore(profile, files);
        
        store.TryCreditCurrency(100000);
        var lootId = new LootId("healthpotion");

        // Fill up to exactly max (256)
        for (int i = 0; i < LocalProfileSnapshot.MaxAppliedShopTransactionReceipts; i++)
        {
            var receipt = new ShopTransactionReceipt(new ShopTransactionId(1000 + i, Guid.NewGuid()), profile);
            store.TryCommitPurchase(receipt, lootId, 1, 0);
        }

        Assert.That(repo.Snapshot.ShopIdempotencyWatermark, Is.EqualTo(0L));
        Assert.That(repo.Snapshot.AppliedShopTransactionReceipts.Count, Is.EqualTo(LocalProfileSnapshot.MaxAppliedShopTransactionReceipts));

        // Adding 257th receipt (Timestamp 2000)
        var triggerReceipt = new ShopTransactionReceipt(new ShopTransactionId(2000, Guid.NewGuid()), profile);
        store.TryCommitPurchase(triggerReceipt, lootId, 1, 0);

        Assert.That(repo.Snapshot.AppliedShopTransactionReceipts.Count, Is.EqualTo(LocalProfileSnapshot.MaxAppliedShopTransactionReceipts));
        
        // The oldest receipt was Timestamp 1000, so watermark should now be 1000.
        Assert.That(repo.Snapshot.ShopIdempotencyWatermark, Is.EqualTo(1000L));
    }

    [Test]
    public void TC_S12_PurchaseReplayRejectedByWatermark_ReturnsAlreadyApplied()
    {
        var files = new MemoryFileStore();
        var profile = new ProfileId("11111111111111111111111111111111");
        var (repo, store) = CreateStore(profile, files);

        var snapshot = repo.Snapshot.Clone();
        snapshot.ShopIdempotencyWatermark = 5000L;
        repo.TrySave(snapshot, out _);

        var lootId = new LootId("healthpotion");

        // Try to commit a transaction with timestamp 4500 (older than watermark)
        var oldReceipt = new ShopTransactionReceipt(new ShopTransactionId(4500, Guid.NewGuid()), profile);
        Assert.That(store.TryCommitPurchase(oldReceipt, lootId, 1, 0), Is.EqualTo(StashOperationResult.AlreadyApplied));
        
        // Exact watermark match is also rejected
        var exactReceipt = new ShopTransactionReceipt(new ShopTransactionId(5000, Guid.NewGuid()), profile);
        Assert.That(store.TryCommitPurchase(exactReceipt, lootId, 1, 0), Is.EqualTo(StashOperationResult.AlreadyApplied));
        
        // Newer timestamp is accepted
        var newReceipt = new ShopTransactionReceipt(new ShopTransactionId(5001, Guid.NewGuid()), profile);
        Assert.That(store.TryCommitPurchase(newReceipt, lootId, 1, 0), Is.EqualTo(StashOperationResult.Success));
    }

    [Test]
    public void TC_S13_PurchaseReplayRejectedByList_ReturnsAlreadyApplied()
    {
        var files = new MemoryFileStore();
        var profile = new ProfileId("11111111111111111111111111111111");
        var (repo, store) = CreateStore(profile, files);
        
        store.TryCreditCurrency(500);
        var lootId = new LootId("healthpotion");
        var txId = new ShopTransactionId(1000, Guid.NewGuid());
        var receipt = new ShopTransactionReceipt(txId, profile);

        store.TryCommitPurchase(receipt, lootId, 1, 50);
        
        // Even if watermark is 0 (lower than 1000), it's rejected by list membership
        Assert.That(repo.Snapshot.ShopIdempotencyWatermark, Is.EqualTo(0L));
        Assert.That(store.TryCommitPurchase(receipt, lootId, 1, 50), Is.EqualTo(StashOperationResult.AlreadyApplied));
    }

    [Test]
    public void TC_S14_CodecRoundtrip_MaintainsShopIdempotencyFields()
    {
        var profile = new ProfileId("11111111111111111111111111111111");
        var snapshot = new LocalProfileSnapshot { ProfileId = profile, ShopIdempotencyWatermark = 12345L };
        var txId = new ShopTransactionId(67890L, Guid.NewGuid());
        snapshot.AppliedShopTransactionReceipts.Add(new ShopTransactionReceipt(txId, profile));
        
        string json = LocalProfileSaveCodec.Encode(snapshot);
        bool decoded = LocalProfileSaveCodec.TryDecode(json, profile, _catalog, out var restored, out _, out string error);
        
        Assert.That(decoded, Is.True, error);
        Assert.That(restored.ShopIdempotencyWatermark, Is.EqualTo(12345L));
        Assert.That(restored.AppliedShopTransactionReceipts.Count, Is.EqualTo(1));
        Assert.That(restored.AppliedShopTransactionReceipts[0].TransactionId.Timestamp, Is.EqualTo(67890L));
        Assert.That(restored.AppliedShopTransactionReceipts[0].TransactionId.Value, Is.EqualTo(txId.Value));
    }

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
