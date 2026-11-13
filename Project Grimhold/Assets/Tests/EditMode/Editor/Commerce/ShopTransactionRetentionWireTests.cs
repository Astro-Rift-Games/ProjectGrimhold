using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Grimhold.Backend;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// FIX-31 wire contract: shop and ability requests carry the timestamped transaction id
/// (<c>{unix ms}_{32 hex}</c>) the backend needs to detect an id whose receipt it already evicted,
/// and the typed rejection TRANSACTION_RETENTION_EXCEEDED is never reported as success.
/// </summary>
public sealed class ShopTransactionRetentionWireTests
{
    private const string RetentionExceeded = "TRANSACTION_RETENTION_EXCEEDED";
    private static readonly Regex TimestampedId = new Regex("^[0-9]{10,15}_[0-9a-f]{32}$");
    private static readonly ProfileId Profile = new ProfileId("11111111111111111111111111111111");
    private static readonly LootId Bone = new LootId("bone");
    private static readonly AbilityId Charge = new AbilityId("charge");

    [Test]
    public void AbilityVendorFeedback_TransactionExpired_TellsThePlayerToRetry()
    {
        Assert.That(
            AbilityVendorFeedback.Describe(AbilityPurchaseResult.TransactionExpired),
            Is.EqualTo("This purchase expired. Please try again"));
    }

    [Test]
    public void ShopTransactionId_ToString_IsTheTimestampedWireFormat()
    {
        var guid = new Guid("0123456789abcdef0123456789abcdef");
        var id = new ShopTransactionId(1700000000123L, guid);

        Assert.That(id.ToString(), Is.EqualTo("1700000000123_0123456789abcdef0123456789abcdef"));
        Assert.That(TimestampedId.IsMatch(id.ToString()), Is.True);
    }

    public sealed class Shop
    {
        private LocalProfileRepository _repository;
        private LocalProfileStore _store;
        private GameObject _owner;
        private RemoteShopTransactionService _service;
        private string _sentTransactionId;
        private string _errorCode;
        private int _calls;
        private int _reconciliations;

        [SetUp]
        public void SetUp()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<LootDefinitionCatalog>(
                "Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset");
            _repository = new LocalProfileRepository(new LocalProfilePersistenceEditModeTests.MemoryFileStore(), ".");
            Assert.That(_repository.Initialize(Profile, catalog), Is.True);
            _store = new LocalProfileStore(_repository, Profile, catalog);
            var snapshot = _repository.Snapshot.Clone();
            snapshot.Currency = 100;
            snapshot.InventorySchemaVersion = 2;
            snapshot.Loadout.Clear();
            snapshot.Loadout.Add(new StashItem(Bone, 2, 0));
            Assert.That(_repository.TrySave(snapshot, out string error), Is.True, error);
            _store.SetRemoteRevision(7);

            _owner = new GameObject("RetentionWireShopTest");
            _service = _owner.AddComponent<RemoteShopTransactionService>();
            _service.Initialize(_store, request =>
            {
                _calls++;
                _sentTransactionId = request.transactionId;
                return Task.FromResult((false, default(ShopTransactionResult), new BackendError { error = _errorCode }));
            }, () => { _reconciliations++; return Task.FromResult(true); });
            _service.InitializeBuyback(new BuybackLedger(), (transactionId, entryId, amount, revision) =>
            {
                _calls++;
                _sentTransactionId = transactionId;
                return Task.FromResult((false, default(ShopTransactionResult), new BackendError { error = _errorCode }));
            });
            _errorCode = RetentionExceeded;
            _calls = _reconciliations = 0;
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(_owner);

        private void AssertNothingApplied()
        {
            Assert.That(_store.GetCurrency(), Is.EqualTo(100));
            Assert.That(_store.GetLoadout()[0].Amount, Is.EqualTo(2));
            Assert.That(_store.RemoteRevision, Is.EqualTo(7));
            Assert.That(_repository.Snapshot.AppliedShopTransactionReceipts, Is.Empty);
            Assert.That(_reconciliations, Is.EqualTo(1), "A rejection reconciles with the backend state.");
        }

        [Test]
        public async Task Trade_SendsTheFullTimestampedId_AndRetentionRejectionIsNotSuccess()
        {
            var ticket = new MerchantTradeTicket(new ShopTransactionId(1700000000123L, Guid.NewGuid()),
                new[] { new MerchantPricedTradeLine(Bone, 1, 5) }, Array.Empty<MerchantPricedTradeLine>());

            var result = await _service.ExecuteTradeAsync(Profile, ticket);

            Assert.That(_sentTransactionId, Is.EqualTo(ticket.TransactionId.ToString()));
            Assert.That(TimestampedId.IsMatch(_sentTransactionId), Is.True, _sentTransactionId);
            Assert.That(result, Is.EqualTo(StashOperationResult.InvalidInventory));
            Assert.That(_calls, Is.EqualTo(1));
            AssertNothingApplied();
        }

        [Test]
        public async Task Buyback_SendsTheFullTimestampedId_AndRetentionRejectionIsNotSuccess()
        {
            var request = new MerchantBuybackRequest(new ShopTransactionId(1700000000456L, Guid.NewGuid()),
                "sold", Bone, 1, 5);

            var result = await _service.ExecuteBuybackAsync(Profile, request);

            Assert.That(_sentTransactionId, Is.EqualTo(request.TransactionId.ToString()));
            Assert.That(TimestampedId.IsMatch(_sentTransactionId), Is.True, _sentTransactionId);
            Assert.That(result, Is.EqualTo(StashOperationResult.InvalidInventory));
            Assert.That(_calls, Is.EqualTo(1));
            AssertNothingApplied();
        }
    }

    public sealed class Ability
    {
        private const long Price = 100L;
        private GameObject _owner;
        private RemoteAbilityPurchaseService _service;
        private LocalProfileStore _store;
        private AbilityDefinitionCatalog _abilityCatalog;
        private AbilityDefinition _chargeDefinition;
        private string _sentTransactionId;
        private string _errorCode;
        private int _reconciliations;

        [SetUp]
        public void SetUp()
        {
            var lootCatalog = AssetDatabase.LoadAssetAtPath<LootDefinitionCatalog>(
                "Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset");
            _chargeDefinition = AbilityTestFactory.CreateDefinition("charge");
            _abilityCatalog = AbilityTestFactory.CreateCatalog(_chargeDefinition);
            var repository = new LocalProfileRepository(new LocalProfilePersistenceEditModeTests.MemoryFileStore(), ".");
            Assert.That(repository.Initialize(Profile, lootCatalog), Is.True);
            _store = new LocalProfileStore(repository, Profile, abilityCatalog: _abilityCatalog);
            Assert.That(_store.TryCreditCurrency(500L), Is.EqualTo(StashOperationResult.Success));
            _store.SetRemoteRevision(3);

            _owner = new GameObject("RetentionWireAbilityTest");
            _service = _owner.AddComponent<RemoteAbilityPurchaseService>();
            _errorCode = RetentionExceeded;
            _reconciliations = 0;
            _service.Initialize(_store, (transactionId, abilityId, revision) =>
            {
                _sentTransactionId = transactionId;
                return Task.FromResult((false, default(PurchaseAbilityResult), new BackendError { error = _errorCode }));
            }, () => { _reconciliations++; return Task.FromResult(true); });
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_owner);
            UnityEngine.Object.DestroyImmediate(_abilityCatalog);
            UnityEngine.Object.DestroyImmediate(_chargeDefinition);
        }

        [Test]
        public async Task Purchase_SendsATimestampedId_AndRetentionRejectionIsATypedNonSuccess()
        {
            long before = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            var result = await _service.PurchaseAbilityAsync(Profile, Charge, Price);

            long after = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Assert.That(TimestampedId.IsMatch(_sentTransactionId), Is.True, _sentTransactionId);
            long timestamp = long.Parse(_sentTransactionId.Substring(0, _sentTransactionId.IndexOf('_')));
            Assert.That(timestamp, Is.InRange(before, after));
            Assert.That(result, Is.EqualTo(AbilityPurchaseResult.TransactionExpired));
            Assert.That(_store.GetCurrency(), Is.EqualTo(500L));
            Assert.That(_store.IsAbilityUnlocked(Charge), Is.False);
            Assert.That(_store.RemoteRevision, Is.EqualTo(3));
            Assert.That(_reconciliations, Is.EqualTo(1));
        }

        [Test]
        public async Task Purchase_CreatesAFreshIdPerAttempt()
        {
            await _service.PurchaseAbilityAsync(Profile, Charge, Price);
            string first = _sentTransactionId;
            await _service.PurchaseAbilityAsync(Profile, Charge, Price);

            Assert.That(_sentTransactionId, Is.Not.EqualTo(first));
        }
    }
}
