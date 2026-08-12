#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class CurrencyPersistenceEditModeTests
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
    public void TC_C01_CreditValid_IncreasesCurrencyAndPersists()
    {
        var files = new MemoryFileStore();
        var profile = new ProfileId("11111111111111111111111111111111");
        var (repo, store) = CreateStore(profile, files);

        Assert.That(store.GetCurrency(), Is.EqualTo(0L));
        Assert.That(store.TryCreditCurrency(100), Is.EqualTo(StashOperationResult.Success));
        Assert.That(store.GetCurrency(), Is.EqualTo(100L));

        var (repo2, store2) = CreateStore(profile, files);
        Assert.That(store2.GetCurrency(), Is.EqualTo(100L));
    }

    [Test]
    public void TC_C02_CreditZero_ReturnsInvalidInventory()
    {
        var files = new MemoryFileStore();
        var profile = new ProfileId("11111111111111111111111111111111");
        var (repo, store) = CreateStore(profile, files);
        
        Assert.That(store.TryCreditCurrency(50), Is.EqualTo(StashOperationResult.Success));
        Assert.That(store.TryCreditCurrency(0), Is.EqualTo(StashOperationResult.InvalidInventory));
        Assert.That(store.GetCurrency(), Is.EqualTo(50L));
    }

    [Test]
    public void TC_C03_CreditNegative_ReturnsInvalidInventory()
    {
        var files = new MemoryFileStore();
        var profile = new ProfileId("11111111111111111111111111111111");
        var (repo, store) = CreateStore(profile, files);
        
        Assert.That(store.TryCreditCurrency(50), Is.EqualTo(StashOperationResult.Success));
        Assert.That(store.TryCreditCurrency(-1), Is.EqualTo(StashOperationResult.InvalidInventory));
        Assert.That(store.GetCurrency(), Is.EqualTo(50L));
    }

    [Test]
    public void TC_C04_CreditOverflow_ReturnsInvalidInventory()
    {
        var files = new MemoryFileStore();
        var profile = new ProfileId("11111111111111111111111111111111");
        var (repo, store) = CreateStore(profile, files);
        
        var snapshot = repo.Snapshot.Clone();
        snapshot.Currency = long.MaxValue;
        repo.TrySave(snapshot, out _);

        Assert.That(store.GetCurrency(), Is.EqualTo(long.MaxValue));
        Assert.That(store.TryCreditCurrency(1), Is.EqualTo(StashOperationResult.InvalidInventory));
        Assert.That(store.GetCurrency(), Is.EqualTo(long.MaxValue));
    }

    [Test]
    public void TC_C05_DebitValid_DecreasesCurrencyAndPersists()
    {
        var files = new MemoryFileStore();
        var profile = new ProfileId("11111111111111111111111111111111");
        var (repo, store) = CreateStore(profile, files);
        
        store.TryCreditCurrency(200);
        Assert.That(store.GetCurrency(), Is.EqualTo(200L));
        
        Assert.That(store.TryDebitCurrency(75), Is.EqualTo(StashOperationResult.Success));
        Assert.That(store.GetCurrency(), Is.EqualTo(125L));

        var (repo2, store2) = CreateStore(profile, files);
        Assert.That(store2.GetCurrency(), Is.EqualTo(125L));
    }

    [Test]
    public void TC_C06_DebitZero_ReturnsInvalidInventory()
    {
        var files = new MemoryFileStore();
        var profile = new ProfileId("11111111111111111111111111111111");
        var (repo, store) = CreateStore(profile, files);
        
        store.TryCreditCurrency(200);
        Assert.That(store.TryDebitCurrency(0), Is.EqualTo(StashOperationResult.InvalidInventory));
        Assert.That(store.GetCurrency(), Is.EqualTo(200L));
    }

    [Test]
    public void TC_C07_DebitInsufficientFunds_ReturnsInvalidInventory()
    {
        var files = new MemoryFileStore();
        var profile = new ProfileId("11111111111111111111111111111111");
        var (repo, store) = CreateStore(profile, files);
        
        store.TryCreditCurrency(50);
        Assert.That(store.TryDebitCurrency(51), Is.EqualTo(StashOperationResult.InvalidInventory));
        Assert.That(store.GetCurrency(), Is.EqualTo(50L));
    }

    [Test]
    public void TC_C08_DebitExactBalance_ReturnsSuccess()
    {
        var files = new MemoryFileStore();
        var profile = new ProfileId("11111111111111111111111111111111");
        var (repo, store) = CreateStore(profile, files);
        
        store.TryCreditCurrency(100);
        Assert.That(store.TryDebitCurrency(100), Is.EqualTo(StashOperationResult.Success));
        Assert.That(store.GetCurrency(), Is.EqualTo(0L));
    }

    [Test]
    public void TC_C09_CreditPersistenceFailure_LeavesCurrencyUnchanged()
    {
        var files = new MemoryFileStore();
        var profile = new ProfileId("11111111111111111111111111111111");
        var (repo, store) = CreateStore(profile, files);
        
        Assert.That(store.GetCurrency(), Is.EqualTo(0L));
        files.FailWrites = true;
        Assert.That(store.TryCreditCurrency(100), Is.EqualTo(StashOperationResult.PersistenceFailed));
        Assert.That(store.GetCurrency(), Is.EqualTo(0L));
    }

    [Test]
    public void TC_C10_DebitPersistenceFailure_LeavesCurrencyUnchanged()
    {
        var files = new MemoryFileStore();
        var profile = new ProfileId("11111111111111111111111111111111");
        var (repo, store) = CreateStore(profile, files);
        
        store.TryCreditCurrency(200);
        files.FailWrites = true;
        Assert.That(store.TryDebitCurrency(50), Is.EqualTo(StashOperationResult.PersistenceFailed));
        Assert.That(store.GetCurrency(), Is.EqualTo(200L));
    }

    [Test]
    public void TC_C11_RoundTripJson_MaintainsCurrency()
    {
        var profile = new ProfileId("11111111111111111111111111111111");
        var snapshot = new LocalProfileSnapshot { ProfileId = profile, Currency = 999_999_999_999L };
        
        string json = LocalProfileSaveCodec.Encode(snapshot);
        bool decoded = LocalProfileSaveCodec.TryDecode(json, profile, _catalog, out var restored, out _, out string error);
        
        Assert.That(decoded, Is.True, error);
        Assert.That(restored.Currency, Is.EqualTo(999_999_999_999L));
    }

    [Test]
    public void TC_C12_DecodeWithoutCurrencyField_MigratesToInitialCurrency()
    {
        var profile = new ProfileId("11111111111111111111111111111111");
        string json = $"{{\"schemaVersion\":1,\"profileId\":\"{profile.Value}\"}}";
        
        bool decoded = LocalProfileSaveCodec.TryDecode(json, profile, _catalog, out var restored, out _, out string error);
        
        Assert.That(decoded, Is.True, error);
        Assert.That(restored.Currency, Is.EqualTo(LocalProfileSnapshot.InitialCurrency));
    }

    [Test]
    public void TC_C13_DecodeWithNegativeCurrency_Fails()
    {
        var profile = new ProfileId("11111111111111111111111111111111");
        string json = $"{{\"schemaVersion\":1,\"profileId\":\"{profile.Value}\",\"currency\":-1}}";
        
        bool decoded = LocalProfileSaveCodec.TryDecode(json, profile, _catalog, out _, out _, out string error);
        
        Assert.That(decoded, Is.False);
        Assert.That(error, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void TC_C14_StashOperation_DoesNotModifyCurrency()
    {
        var files = new MemoryFileStore();
        var profile = new ProfileId("11111111111111111111111111111111");
        var (repo, store) = CreateStore(profile, files);
        
        store.TryCreditCurrency(500);
        Assert.That(store.GetCurrency(), Is.EqualTo(500L));

        var items = new[] { new StashItem(new LootId("healthpotion"), 2) };
        Assert.That(store.TrySecureLoot(items), Is.EqualTo(StashOperationResult.Success));
        
        Assert.That(store.GetCurrency(), Is.EqualTo(500L));
    }

    [Test]
    public void TC_C15_ProfileCommitted_FiresExactlyOnceOnCredit()
    {
        var files = new MemoryFileStore();
        var profile = new ProfileId("11111111111111111111111111111111");
        var (repo, store) = CreateStore(profile, files);
        
        int count = 0;
        store.ProfileCommitted += _ => count++;
        
        store.TryCreditCurrency(100);
        Assert.That(count, Is.EqualTo(1));
    }

    [Test]
    public void TC_C16_Clone_CopiesCurrencyExactly()
    {
        var snapshot = new LocalProfileSnapshot { ProfileId = new ProfileId("11111111111111111111111111111111"), Currency = 12345L };
        var clone = snapshot.Clone();
        
        Assert.That(clone.Currency, Is.EqualTo(12345L));
        clone.Currency = 999L;
        Assert.That(snapshot.Currency, Is.EqualTo(12345L));
    }

    [Test]
    public void TC_C17_CurrencyChanged_DoesNotFireOnStashOperationWithoutCurrencyChange()
    {
        var files = new MemoryFileStore();
        var profile = new ProfileId("11111111111111111111111111111111");
        var (repo, store) = CreateStore(profile, files);
        store.TryCreditCurrency(100);

        var serviceObj = new GameObject();
        var service = serviceObj.AddComponent<InMemoryPlayerCurrencyService>();
        service.Initialize(store);

        int eventCount = 0;
        service.CurrencyChanged += _ => eventCount++;

        var items = new[] { new StashItem(new LootId("healthpotion"), 2) };
        Assert.That(store.TrySecureLoot(items), Is.EqualTo(StashOperationResult.Success));

        Assert.That(eventCount, Is.EqualTo(0));
        Assert.That(service.GetCurrency(profile), Is.EqualTo(100L));
        
        Object.DestroyImmediate(serviceObj);
    }

    [Test]
    public void TC_C18_CurrencyChanged_FiresOnSuccessfulCredit()
    {
        var files = new MemoryFileStore();
        var profile = new ProfileId("11111111111111111111111111111111");
        var (repo, store) = CreateStore(profile, files);

        var serviceObj = new GameObject();
        var service = serviceObj.AddComponent<InMemoryPlayerCurrencyService>();
        service.Initialize(store);

        int eventCount = 0;
        ProfileId receivedId = default;
        service.CurrencyChanged += id => { eventCount++; receivedId = id; };

        Assert.That(store.TryCreditCurrency(100), Is.EqualTo(StashOperationResult.Success));

        Assert.That(eventCount, Is.EqualTo(1));
        Assert.That(receivedId, Is.EqualTo(profile));

        Object.DestroyImmediate(serviceObj);
    }

    [Test]
    public void TC_C19_LastKnownCurrency_InitializedCorrectlyFromPreexistingProfile()
    {
        var files = new MemoryFileStore();
        var profile = new ProfileId("11111111111111111111111111111111");
        var (repo, store) = CreateStore(profile, files);
        store.TryCreditCurrency(500);

        var serviceObj = new GameObject();
        var service = serviceObj.AddComponent<InMemoryPlayerCurrencyService>();
        service.Initialize(store);

        int eventCount = 0;
        service.CurrencyChanged += _ => eventCount++;

        var items = new[] { new StashItem(new LootId("healthpotion"), 2) };
        Assert.That(store.TrySecureLoot(items), Is.EqualTo(StashOperationResult.Success));

        Assert.That(eventCount, Is.EqualTo(0));
        
        Object.DestroyImmediate(serviceObj);
    }

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
