#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Grimhold.Backend;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.PlayMode
{
    /// <summary>In-memory profile and controlled transport; commerce still crosses the real Fusion RPC boundary.</summary>
    internal sealed class PlayModeMerchantComposition : IDisposable, ITownSessionTicketVerifier
    {
        public readonly ProfileId ProfileId = new ProfileId("63636363636363636363636363636363");
        private readonly GameObject _owner;
        private readonly ApplicationStashContext[] _suspended;
        private readonly ApplicationAuthContext _auth;
        private readonly bool _ownsAuth;
        private readonly TownSessionTicket _previousTicket;
        private readonly ITownSessionTicketVerifier _previousTownVerifier;
        private readonly IRaidAdmissionVerifier _previousRaidVerifier;
        public LocalProfileStore Store { get; }
        public LocalLoadoutInventoryReadSource Inventory { get; }
        public RemoteShopTransactionService Shop { get; }
        public InMemoryPlayerCurrencyService Currency { get; }
        public int BackendCalls { get; private set; }
        public int Reconciliations { get; private set; }
        public string BackendErrorCode { get; set; }
        public string BackendTransactionId { get; private set; }
        public TaskCompletionSource<(bool success, ShopTransactionResult data, BackendError error)> Pending { get; set; }

        public PlayModeMerchantComposition(long gold, LootId owned = default, int amount = 0)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<LootDefinitionCatalog>(
                "Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset");
            Assert.That(catalog, Is.Not.Null);
            var repository = new InMemoryLocalProfileRepository();
            Assert.That(repository.Initialize(ProfileId, catalog), Is.True);
            var snapshot = repository.Snapshot.Clone();
            snapshot.Currency = gold;
            snapshot.Loadout.Clear();
            if (amount > 0) snapshot.Loadout.Add(new StashItem(owned, amount));
            Assert.That(repository.TrySave(snapshot, out string error), Is.True, error);
            Store = new LocalProfileStore(repository, ProfileId, catalog);
            _suspended = Object.FindObjectsByType<ApplicationStashContext>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var context in _suspended) context.gameObject.SetActive(false);
            _owner = new GameObject("Merchant in-memory test composition");
            var stash = _owner.AddComponent<InMemoryPlayerStashService>();
            stash.Initialize(Store);
            var loadout = _owner.AddComponent<InMemoryPlayerLoadoutService>();
            loadout.Initialize(Store);
            Currency = _owner.AddComponent<InMemoryPlayerCurrencyService>();
            Currency.Initialize(Store);
            Shop = _owner.AddComponent<RemoteShopTransactionService>();
            Shop.Initialize(Store, Backend, () => { Reconciliations++; return Task.FromResult(true); });
            var application = _owner.AddComponent<ApplicationStashContext>();
            application.Initialize(Store, stash, loadout, Currency, Shop);
            Inventory = new LocalLoadoutInventoryReadSource(application, ProfileId);

            _auth = ApplicationAuthContext.Instance;
            _ownsAuth = _auth == null;
            if (_ownsAuth) _auth = new GameObject("Merchant test auth").AddComponent<ApplicationAuthContext>();
            _previousTicket = _auth.ActiveTownTicket;
            _previousTownVerifier = _auth.TownTicketVerifier;
            _previousRaidVerifier = _auth.RaidAdmissionVerifier;
            // Ticket assignment and SocialPlayerIdentity prewarming must never reach a live backend.
            _auth.SetVerifiers(this, _previousRaidVerifier);
            string raw = TownSessionTicketCodec.CreateTestTicket(ProfileId, "Merchant test");
            Assert.That(TownSessionTicketCodec.TryDecode(raw, out var ticket), Is.True);
            _auth.SetTownSessionTicket(ticket);
        }

        private Task<(bool success, ShopTransactionResult data, BackendError error)> Backend(
            bool purchase, string transactionId, MerchantPricedTradeLine line, int revision)
        {
            BackendCalls++;
            BackendTransactionId = transactionId;
            return Pending?.Task ?? Task.FromResult((BackendErrorCode == null,
                new ShopTransactionResult { revision = revision + 1 }, new BackendError { error = BackendErrorCode }));
        }

        public Task<(bool valid, ProfileId profileId, string characterName, string error)> VerifyTownTicketAsync(string rawTicket) =>
            Task.FromResult((rawTicket == _auth.ActiveTownTicket.RawTicket, ProfileId, "Merchant test", (string)null));

        public void Dispose()
        {
            Inventory.Dispose();
            Object.DestroyImmediate(_owner);
            if (_ownsAuth) Object.DestroyImmediate(_auth.gameObject);
            else
            {
                _auth.SetTownSessionTicket(_previousTicket);
                _auth.SetVerifiers(_previousTownVerifier, _previousRaidVerifier);
            }
            foreach (var context in _suspended) if (context != null) context.gameObject.SetActive(true);
        }

        /// <summary>Observes requests/responses without bypassing the production endpoint or RPCs.</summary>
        internal sealed class EndpointObserver : IMerchantTradeEndpoint, IMerchantTradeClient
        {
            private readonly TownMerchantNetworkController _merchant;
            private IMerchantTradeClient _client;
            public int Submissions { get; private set; }
            public int Outcomes { get; private set; }
            public MerchantTradeTicket Ticket { get; private set; }
            public EndpointObserver(TownMerchantNetworkController merchant) { _merchant = merchant; }
            public LootDefinitionCatalog Catalog => _merchant.Catalog;
            public IReadOnlyList<MerchantStockItem> Stock => _merchant.Stock;
            public int GetAvailableStock(LootId id) => _merchant.GetAvailableStock(id);
            public event Action StockChanged { add => _merchant.StockChanged += value; remove => _merchant.StockChanged -= value; }
            public event Action AuthorityChanged { add => _merchant.AuthorityChanged += value; remove => _merchant.AuthorityChanged -= value; }
            public bool TrySubmitTrade(ProfileId profile, Guid request, IReadOnlyList<MerchantTradeLine> buys, IReadOnlyList<MerchantTradeLine> sales)
            { Submissions++; return _merchant.TrySubmitTrade(profile, request, buys, sales); }
            public void ReportTradeOutcome(Guid request, ShopTransactionId transaction, bool persisted)
            { Outcomes++; _merchant.ReportTradeOutcome(request, transaction, persisted); }
            public void RequestPendingTrades(ProfileId profile) => _merchant.RequestPendingTrades(profile);
            public void SetTradeClient(IMerchantTradeClient client) { _client = client; _merchant.SetTradeClient(this); }
            public void ClearTradeClient(IMerchantTradeClient client)
            { if (ReferenceEquals(_client, client)) { _client = null; _merchant.ClearTradeClient(this); } }
            public bool HandleTradeResponse(MerchantTradeResponse response)
            { if (response.IsApproved) Ticket = response.Ticket; return _client != null && _client.HandleTradeResponse(response); }
            public void HandlePendingTrade(Guid request, ShopTransactionId transaction) => _client?.HandlePendingTrade(request, transaction);
        }
    }
}
#endif
