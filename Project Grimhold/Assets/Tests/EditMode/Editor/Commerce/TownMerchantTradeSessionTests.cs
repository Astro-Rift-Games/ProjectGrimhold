using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;

public class TownMerchantTradeSessionTests
{
    private const double Timeout = 35d;
    private static readonly ProfileId Profile = new ProfileId("profile-a");
    private static readonly LootId Potion = new LootId("healthpotion");
    private static readonly LootId Bone = new LootId("bone");
    private static readonly LootId Hat = new LootId("placeholder_helmet");

    private FakeAuthorityEndpoint _endpoint;
    private FakeProfile _profile;
    private TownMerchantTradeSession _session;
    private List<MerchantShopViewModel> _presented;
    private List<MerchantTransactionResult> _results;
    private double _now;
    private int _requestIdCount;

    [SetUp]
    public void Setup()
    {
        LootDefinition potion = MerchantTestContent.CreateDefinition(Potion.Value, buyValue: 30, sellValue: 20);
        LootDefinition bone = MerchantTestContent.CreateDefinition(Bone.Value, buyValue: 5, sellValue: 4);
        LootDefinition hat = MerchantTestContent.CreateDefinition(Hat.Value, buyValue: 10, sellValue: 5);
        LootDefinitionCatalog catalog = MerchantTestContent.CreateCatalog(potion, bone, hat);
        var stock = new List<MerchantStockItem>
        {
            MerchantTestContent.Offer(potion, 3),
            MerchantTestContent.Offer(bone, MerchantStockItem.UnlimitedQuantity)
        };

        _endpoint = new FakeAuthorityEndpoint(catalog, stock);
        _profile = new FakeProfile(currency: 100, new LootEntry(Hat, 2), new LootEntry(Bone, 1));
        _presented = new List<MerchantShopViewModel>();
        _results = new List<MerchantTransactionResult>();
        _now = 0;
        _requestIdCount = 0;
        OpenSession();
    }

    private void OpenSession()
    {
        _session = new TownMerchantTradeSession(
            Profile,
            _endpoint,
            _profile,
            _profile,
            () => _profile.Currency,
            _presented.Add,
            () => _now,
            Timeout,
            () => new Guid(++_requestIdCount, 0, 0, new byte[8]));
        _session.TradeCompleted += _results.Add;
    }

    [TearDown]
    public void TearDown()
    {
        _session.Dispose();
    }

    private MerchantShopViewModel View => _session.Current;

    // --- Draft and preview ---

    [Test]
    public void Opening_PresentsConfirmedProfileAndStockWithoutDraft()
    {
        Assert.That(_presented, Is.Not.Empty);
        Assert.That(View.IsAvailable, Is.True);
        Assert.That(View.ConfirmedCurrency, Is.EqualTo(100));
        Assert.That(View.HasDraft, Is.False);
        Assert.That(View.CanConfirm, Is.False, "An empty draft cannot be confirmed.");
        Assert.That(View.MerchantRows.Select(r => r.LootId), Is.EqualTo(new[] { Potion, Bone }));
        Assert.That(View.MerchantRows[0].Available, Is.EqualTo(3));
        Assert.That(View.MerchantRows[1].IsUnlimited, Is.True);
        Assert.That(View.InventoryRows.Select(r => r.Available), Is.EqualTo(new[] { 2, 1 }));
    }

    [Test]
    public void Edits_RecomputePreviewFromConfirmedStateAndAuthorityPrices()
    {
        _session.AddPurchase(Potion, 2);
        _session.AddSale(Hat, 2);

        Assert.That(View.PurchaseTotal, Is.EqualTo(60));
        Assert.That(View.SaleTotal, Is.EqualTo(10));
        Assert.That(View.Balance, Is.EqualTo(-50));
        Assert.That(View.ProjectedCurrency, Is.EqualTo(50));
        Assert.That(View.ConfirmedCurrency, Is.EqualTo(100), "Editing never changes confirmed Gold.");
        Assert.That(View.CanConfirm, Is.True);
        Assert.That(View.MerchantRows[0].DraftAmount, Is.EqualTo(2));
        Assert.That(View.MerchantRows[0].DraftTotal, Is.EqualTo(60));
        Assert.That(View.InventoryRows[0].DraftAmount, Is.EqualTo(2));
        Assert.That(View.InventoryRows[0].CanAdd, Is.False, "Every owned unit is already in the draft.");
        Assert.That(_profile.Currency, Is.EqualTo(100));
        Assert.That(_endpoint.Submissions, Is.Empty);
    }

    [Test]
    public void ViewModel_ProjectsCapacityAndCurrencyShortfall()
    {
        _session.AddPurchase(Potion, 3);
        _session.AddPurchase(Bone, 1);
        _session.AddSale(Hat, 2);

        Assert.That(View.OccupiedSlots, Is.EqualTo(2));
        Assert.That(View.ProjectedOccupiedSlots, Is.EqualTo(2), "The sold Hat slot is reused by the Potion.");
        Assert.That(View.SlotCapacity, Is.EqualTo(_profile.SlotCapacity));
        Assert.That(View.ProjectedCurrency, Is.EqualTo(100 - 95 + 10));
        Assert.That(View.CurrencyShortfall, Is.Zero);

        _session.RemoveSale(Hat);

        Assert.That(View.ProjectedCurrency, Is.EqualTo(5));
        _session.SetPurchaseAmount(Bone, 3);
        Assert.That(View.CurrencyShortfall, Is.EqualTo(5));
        Assert.That(View.Blocks.Select(b => b.Reason), Does.Contain(MerchantTradeBlockReason.InsufficientCurrency));
    }

    [Test]
    public void ClearTrade_DiscardsEveryLine()
    {
        _session.AddPurchase(Potion, 1);
        _session.AddSale(Hat, 1);

        Assert.That(_session.ClearTrade(), Is.True);

        Assert.That(View.HasDraft, Is.False);
        Assert.That(View.ProjectedCurrency, Is.EqualTo(100));
    }

    [Test]
    public void Confirm_IsOnlySentWhenThePreviewAllowsIt()
    {
        Assert.That(_session.ConfirmTrade(), Is.False, "Empty draft.");

        _session.AddPurchase(Potion, 3);
        _session.AddPurchase(Bone, 3);
        Assert.That(View.Blocks.Any(block => block.Reason == MerchantTradeBlockReason.InsufficientCurrency), Is.True);
        Assert.That(_session.ConfirmTrade(), Is.False, "Insufficient Gold.");

        _session.SetPurchaseAmount(Potion, 4);
        Assert.That(_session.ConfirmTrade(), Is.False, "Insufficient stock.");

        Assert.That(_endpoint.Submissions, Is.Empty);
    }

    // --- Confirmation ---

    [Test]
    public void OneConfirmation_SendsOneMultiLineRequestWithProfileAndRequestId()
    {
        _session.AddPurchase(Potion, 1);
        _session.AddPurchase(Bone, 2);
        _session.AddSale(Hat, 1);

        Assert.That(_session.ConfirmTrade(), Is.True);

        Assert.That(_endpoint.Submissions.Count, Is.EqualTo(1));
        Submission submission = _endpoint.Submissions[0];
        Assert.That(submission.ProfileId, Is.EqualTo(Profile));
        Assert.That(submission.RequestId, Is.EqualTo(RequestId(1)));
        Assert.That(submission.Purchases.Select(l => (l.LootId, l.Amount)), Is.EqualTo(new[] { (Potion, 1), (Bone, 2) }));
        Assert.That(submission.Sales.Select(l => (l.LootId, l.Amount)), Is.EqualTo(new[] { (Hat, 1) }));
    }

    [Test]
    public void WhileInFlight_TheDraftIsLockedAndCannotBeConfirmedAgain()
    {
        _session.AddPurchase(Potion, 1);
        _session.ConfirmTrade();

        Assert.That(View.IsInFlight, Is.True);
        Assert.That(View.CanConfirm, Is.False);
        Assert.That(View.CanEdit, Is.False);
        Assert.That(_session.AddPurchase(Bone, 1), Is.False);
        Assert.That(_session.ClearTrade(), Is.False);
        Assert.That(_session.ConfirmTrade(), Is.False);
        Assert.That(_endpoint.Submissions.Count, Is.EqualTo(1));
    }

    [Test]
    public void ApprovedTicket_IsPersistedWithAuthorityPricesAndClearsTheDraft()
    {
        _session.AddPurchase(Potion, 2);
        _session.AddSale(Hat, 1);
        _session.ConfirmTrade();

        _endpoint.DeliverResponses();

        MerchantTradeTicket ticket = _profile.ExecutedTickets.Single();
        Assert.That(ticket.Purchases.Single().UnitPrice, Is.EqualTo(30));
        Assert.That(ticket.Sales.Single().UnitPrice, Is.EqualTo(5));
        Assert.That(_results, Is.EqualTo(new[] { MerchantTransactionResult.Success }));
        Assert.That(View.HasDraft, Is.False, "Success clears the draft.");
        Assert.That(View.IsInFlight, Is.False);
    }

    [Test]
    public void PersistedOutcome_ConsumesTheReservation()
    {
        _session.AddPurchase(Potion, 2);
        _session.ConfirmTrade();
        Assert.That(_endpoint.GetAvailableStock(Potion), Is.EqualTo(1), "Reserved while pending.");

        _endpoint.DeliverResponses();

        Assert.That(_endpoint.Outcomes.Single().Persisted, Is.True);
        Assert.That(_endpoint.State.GetStockQuantity(0), Is.EqualTo(1));
        Assert.That(_endpoint.GetAvailableStock(Potion), Is.EqualTo(1));
    }

    [Test]
    public void ProfileCommitted_RefreshesConfirmedData()
    {
        _session.AddPurchase(Potion, 2);
        _session.ConfirmTrade();
        _endpoint.DeliverResponses();

        Assert.That(View.ConfirmedCurrency, Is.EqualTo(40));
        Assert.That(View.InventoryRows.Single(r => r.LootId == Potion).Available, Is.EqualTo(2));

        _profile.CommitExternally(currency: 500);

        Assert.That(View.ConfirmedCurrency, Is.EqualTo(500));
    }

    [Test]
    public void StockChanges_RefreshTheRows()
    {
        _endpoint.State.SetStockQuantity(0, 1);
        _endpoint.RaiseStockChanged();

        Assert.That(View.MerchantRows[0].Available, Is.EqualTo(1));
    }

    [Test]
    public void AlreadyApplied_CountsAsPersisted()
    {
        _profile.NextResult = StashOperationResult.AlreadyApplied;
        _session.AddPurchase(Potion, 1);
        _session.ConfirmTrade();

        _endpoint.DeliverResponses();

        Assert.That(_endpoint.Outcomes.Single().Persisted, Is.True);
        Assert.That(_results, Is.EqualTo(new[] { MerchantTransactionResult.AlreadyApplied }));
        Assert.That(View.HasDraft, Is.False);
    }

    // --- Rejection and failure ---

    [Test]
    public void AuthorityRejection_KeepsTheDraftEditableAndRetryReusesTheRequestId()
    {
        _session.AddPurchase(Potion, 2);
        _endpoint.RejectNext = true; // e.g. another player took the stock after the preview.
        _session.ConfirmTrade();
        _endpoint.DeliverResponses();

        Assert.That(_results, Is.EqualTo(new[] { MerchantTransactionResult.RejectedByMerchant }));
        Assert.That(View.HasDraft, Is.True);
        Assert.That(View.CanEdit, Is.True);
        Assert.That(_profile.ExecutedTickets, Is.Empty);
        Assert.That(_endpoint.Outcomes, Is.Empty, "Nothing was reserved.");

        _session.ConfirmTrade();

        Assert.That(_endpoint.Submissions[1].RequestId, Is.EqualTo(_endpoint.Submissions[0].RequestId));
    }

    [Test]
    public void PersistenceRejection_ReleasesTheReservationAndKeepsTheDraft()
    {
        _profile.NextResult = StashOperationResult.InvalidInventory;
        _session.AddPurchase(Potion, 2);
        _session.ConfirmTrade();

        _endpoint.DeliverResponses();

        Assert.That(_endpoint.Outcomes.Single().Persisted, Is.False);
        Assert.That(_endpoint.GetAvailableStock(Potion), Is.EqualTo(3), "Reservation released.");
        Assert.That(_results, Is.EqualTo(new[] { MerchantTransactionResult.RejectedByProfile }));
        Assert.That(View.HasDraft, Is.True);
        Assert.That(View.CanConfirm, Is.True);
    }

    [Test]
    public void RemoteMultiLinePersistenceFailure_IsPropagatedWithoutSplittingTheTrade()
    {
        _profile.NextResult = StashOperationResult.PersistenceFailed;
        _session.AddPurchase(Potion, 1);
        _session.AddSale(Hat, 1);
        _session.ConfirmTrade();

        _endpoint.DeliverResponses();

        Assert.That(_profile.ExecutedTickets.Single().LineCount, Is.EqualTo(2), "One ticket, never split.");
        Assert.That(_endpoint.Submissions.Count, Is.EqualTo(1));
        Assert.That(_endpoint.Outcomes.Single().Persisted, Is.False);
        Assert.That(_endpoint.GetAvailableStock(Potion), Is.EqualTo(3));
        Assert.That(_results, Is.EqualTo(new[] { MerchantTransactionResult.PersistenceFailed }));
        Assert.That(View.HasDraft, Is.True);
    }

    [Test]
    public void RetryAfterFailure_ReusesTheTicketTransactionId()
    {
        _profile.NextResult = StashOperationResult.PersistenceFailed;
        _session.AddPurchase(Potion, 1);
        _session.ConfirmTrade();
        _endpoint.DeliverResponses();

        _profile.NextResult = StashOperationResult.Success;
        _session.ConfirmTrade();
        _endpoint.DeliverResponses();

        Assert.That(_profile.ExecutedTickets.Select(t => t.TransactionId).Distinct().Count(), Is.EqualTo(1));
        Assert.That(View.HasDraft, Is.False);
    }

    [Test]
    public void EditAfterRejection_GeneratesANewRequestId()
    {
        _profile.NextResult = StashOperationResult.InvalidInventory;
        _session.AddPurchase(Potion, 1);
        _session.ConfirmTrade();
        _endpoint.DeliverResponses();

        _session.SetPurchaseAmount(Potion, 2);
        _profile.NextResult = StashOperationResult.Success;
        _session.ConfirmTrade();

        Assert.That(_endpoint.Submissions[1].RequestId, Is.Not.EqualTo(_endpoint.Submissions[0].RequestId));
        _endpoint.DeliverResponses();
        Assert.That(_profile.ExecutedTickets[1].TransactionId, Is.Not.EqualTo(_profile.ExecutedTickets[0].TransactionId));
    }

    [Test]
    public void SubmissionThatCannotBeSent_KeepsTheDraft()
    {
        _endpoint.AcceptSubmissions = false;
        _session.AddPurchase(Potion, 1);

        Assert.That(_session.ConfirmTrade(), Is.False);

        Assert.That(_results, Is.EqualTo(new[] { MerchantTransactionResult.SubmissionFailed }));
        Assert.That(View.HasDraft, Is.True);
        Assert.That(View.IsInFlight, Is.False);
    }

    // --- Lost answers ---

    [Test]
    public void NoAnswerInTime_UnlocksTheDraftKeepingTheRequestId()
    {
        _session.AddPurchase(Potion, 1);
        _session.ConfirmTrade();

        _now = Timeout - 1;
        _session.Tick();
        Assert.That(View.IsInFlight, Is.True);

        _now = Timeout;
        _session.Tick();

        Assert.That(View.IsInFlight, Is.False);
        Assert.That(_results, Is.EqualTo(new[] { MerchantTransactionResult.NoResponse }));
        _session.ConfirmTrade();
        Assert.That(_endpoint.Submissions[1].RequestId, Is.EqualTo(_endpoint.Submissions[0].RequestId));
    }

    [Test]
    public void LocalTimeout_UnlocksTheDraftButKeepsTheAcceptedReservation()
    {
        _session.AddPurchase(Potion, 2);
        _session.ConfirmTrade();

        _now = Timeout;
        _session.Tick();

        Assert.That(View.IsInFlight, Is.False);
        Assert.That(_endpoint.GetAvailableStock(Potion), Is.EqualTo(1), "A persisted outcome may still arrive.");
        Assert.That(_endpoint.Outcomes, Is.Empty);
    }

    [Test]
    public void EditDiscardingASentRequest_ResolvesItsReservation()
    {
        _session.AddPurchase(Potion, 2);
        _session.ConfirmTrade();
        _now = Timeout;
        _session.Tick();

        _session.SetPurchaseAmount(Potion, 1);

        Assert.That(_endpoint.Outcomes.Single().Persisted, Is.False, "The discarded request can never be persisted.");
        Assert.That(_endpoint.GetAvailableStock(Potion), Is.EqualTo(3));
    }

    [Test]
    public void AuthorityChange_UnlocksAnInFlightDraft()
    {
        _session.AddPurchase(Potion, 1);
        _session.ConfirmTrade();

        _endpoint.RaiseAuthorityChanged();

        Assert.That(View.IsInFlight, Is.False);
        Assert.That(_results, Is.EqualTo(new[] { MerchantTransactionResult.NoResponse }));
        Assert.That(_endpoint.Outcomes, Is.Empty, "The current request may still be retried and persisted.");
        Assert.That(_endpoint.GetAvailableStock(Potion), Is.EqualTo(2));
    }

    [Test]
    public void OpeningASession_ResolvesUnreportedTradesFromTheProfileReceipts()
    {
        _session.Dispose();
        var persistedRequest = new Guid("aaaaaaaa-0000-0000-0000-000000000001");
        var abandonedRequest = new Guid("aaaaaaaa-0000-0000-0000-000000000002");
        _endpoint.Submit(new Submission(Profile, persistedRequest, new[] { new MerchantTradeLine(Potion, 1) }, Array.Empty<MerchantTradeLine>()));
        _endpoint.Submit(new Submission(Profile, abandonedRequest, new[] { new MerchantTradeLine(Potion, 1) }, Array.Empty<MerchantTradeLine>()));
        // The first ticket was persisted but its report never reached State Authority.
        _profile.MarkApplied(_endpoint.PendingResponses[0].Ticket.TransactionId);
        _endpoint.PendingResponses.Clear();

        OpenSession();

        Assert.That(_endpoint.Outcomes.Select(o => (o.RequestId, o.Persisted)),
            Is.EquivalentTo(new[] { (persistedRequest, true), (abandonedRequest, false) }));
        Assert.That(_endpoint.State.GetStockQuantity(0), Is.EqualTo(2), "The persisted unit is consumed.");
        Assert.That(_endpoint.GetAvailableStock(Potion), Is.EqualTo(2));
    }

    [Test]
    public void LateAnswerAfterUnlock_IsNotPersistedAndItsReservationIsReleased()
    {
        _session.AddPurchase(Potion, 2);
        _session.ConfirmTrade();
        _endpoint.RaiseAuthorityChanged();

        _endpoint.DeliverResponses();

        Assert.That(_profile.ExecutedTickets, Is.Empty);
        Assert.That(_endpoint.Outcomes.Single().Persisted, Is.False);
        Assert.That(_endpoint.GetAvailableStock(Potion), Is.EqualTo(3));
    }

    [Test]
    public void ClosingTheSession_DiscardsTheDraftAndReleasesAnUnhandledTicket()
    {
        _session.AddPurchase(Potion, 2);
        _session.ConfirmTrade();

        _session.Dispose();
        _endpoint.DeliverResponses();

        Assert.That(_endpoint.HasHandler, Is.False);
        Assert.That(_profile.ExecutedTickets, Is.Empty);
        Assert.That(_endpoint.GetAvailableStock(Potion), Is.EqualTo(3));
    }

    [Test]
    public void LateDuplicateRetryAfterSuccess_CreatesNoSecondTransaction()
    {
        _session.AddPurchase(Potion, 2);
        _session.ConfirmTrade();
        Submission original = _endpoint.Submissions[0];
        _endpoint.DeliverResponses();

        // The same request arrives again after its outcome (e.g. a duplicated send).
        _endpoint.Submit(original);

        MerchantTradeResponse late = _endpoint.PendingResponses.Single();
        Assert.That(late.Ticket.TransactionId, Is.EqualTo(_profile.ExecutedTickets.Single().TransactionId));
        Assert.That(_endpoint.State.GetStockQuantity(0), Is.EqualTo(1));
        Assert.That(_endpoint.GetAvailableStock(Potion), Is.EqualTo(1), "No new reservation.");
    }

    private static Guid RequestId(int value) => new Guid(value, 0, 0, new byte[8]);

    // --- Fakes ---

    private readonly struct Submission
    {
        public readonly ProfileId ProfileId;
        public readonly Guid RequestId;
        public readonly MerchantTradeLine[] Purchases;
        public readonly MerchantTradeLine[] Sales;

        public Submission(ProfileId profileId, Guid requestId, MerchantTradeLine[] purchases, MerchantTradeLine[] sales)
        {
            ProfileId = profileId;
            RequestId = requestId;
            Purchases = purchases;
            Sales = sales;
        }
    }

    private readonly struct Outcome
    {
        public readonly Guid RequestId;
        public readonly bool Persisted;

        public Outcome(Guid requestId, bool persisted)
        {
            RequestId = requestId;
            Persisted = persisted;
        }
    }

    /// <summary>
    /// Endpoint backed by the real State Authority rules. Responses are queued until delivered,
    /// and an approved response no handler accepts is reported as not persisted, like the controller.
    /// </summary>
    private sealed class FakeAuthorityEndpoint : IMerchantTradeEndpoint
    {
        private readonly MerchantRequestValidator _validator;
        private readonly Dictionary<Guid, ProfileId> _profiles = new Dictionary<Guid, ProfileId>();
        private IMerchantTradeClient _client;
        private long _timestamp = 1000;

        public FakeAuthorityEndpoint(LootDefinitionCatalog catalog, List<MerchantStockItem> stock)
        {
            Catalog = catalog;
            Stock = stock;
            State = new FakeMerchantAuthorityState();
            _validator = new MerchantRequestValidator(stock, State, () => _timestamp++);
            _validator.TryInitializeStock();
        }

        public FakeMerchantAuthorityState State { get; }
        public LootDefinitionCatalog Catalog { get; }
        public IReadOnlyList<MerchantStockItem> Stock { get; }
        public bool AcceptSubmissions { get; set; } = true;
        public bool RejectNext { get; set; }
        public bool HasHandler => _client != null;
        public List<Submission> Submissions { get; } = new List<Submission>();
        public List<Outcome> Outcomes { get; } = new List<Outcome>();
        public List<MerchantTradeResponse> PendingResponses { get; } = new List<MerchantTradeResponse>();

        public event Action StockChanged;
        public event Action AuthorityChanged;

        public int GetAvailableStock(LootId lootId) => _validator.GetAvailableStock(lootId);

        public bool TrySubmitTrade(ProfileId profileId, Guid requestId, IReadOnlyList<MerchantTradeLine> purchases, IReadOnlyList<MerchantTradeLine> sales)
        {
            if (!AcceptSubmissions)
            {
                return false;
            }

            var submission = new Submission(profileId, requestId, purchases.ToArray(), sales.ToArray());
            Submissions.Add(submission);
            Submit(submission);
            return true;
        }

        public void Submit(Submission submission)
        {
            _profiles[submission.RequestId] = submission.ProfileId;
            if (RejectNext)
            {
                RejectNext = false;
                PendingResponses.Add(MerchantTradeResponse.Rejected(submission.RequestId));
                return;
            }

            _validator.TryProcessTradeRequest(submission.ProfileId, submission.RequestId,
                submission.Purchases, submission.Sales, Catalog, out MerchantTradeResponse response);
            PendingResponses.Add(response);
        }

        public void ReportTradeOutcome(Guid requestId, ShopTransactionId transactionId, bool persisted)
        {
            Outcomes.Add(new Outcome(requestId, persisted));
            _validator.TryCompleteTrade(_profiles[requestId], requestId, transactionId, persisted);
        }

        public void RequestPendingTrades(ProfileId profileId)
        {
            var pending = new List<MerchantTradeRecord>();
            _validator.GetPendingTrades(profileId, pending);
            foreach (MerchantTradeRecord record in pending)
            {
                _profiles[record.RequestId] = profileId;
                _client?.HandlePendingTrade(record.RequestId, record.TransactionId);
            }
        }

        public void SetTradeClient(IMerchantTradeClient client) => _client = client;

        public void ClearTradeClient(IMerchantTradeClient client)
        {
            if (ReferenceEquals(_client, client))
            {
                _client = null;
            }
        }

        public void DeliverResponses()
        {
            MerchantTradeResponse[] responses = PendingResponses.ToArray();
            PendingResponses.Clear();
            foreach (MerchantTradeResponse response in responses)
            {
                bool handled = _client != null && _client.HandleTradeResponse(response);
                if (response.IsApproved && !handled)
                {
                    ReportTradeOutcome(response.RequestId, response.Ticket.TransactionId, persisted: false);
                }
            }
        }

        public void RaiseStockChanged() => StockChanged?.Invoke();
        public void RaiseAuthorityChanged() => AuthorityChanged?.Invoke();
    }

    /// <summary>
    /// Confirmed profile stand-in: applies a persisted ticket to Inventory and Gold and signals the
    /// commit through <see cref="IInventoryReadSource.Changed"/>, like ProfileCommitted does.
    /// </summary>
    private sealed class FakeProfile : IInventoryReadSource, IShopTransactionService
    {
        private readonly List<LootEntry> _inventory;
        private readonly HashSet<ShopTransactionId> _applied = new HashSet<ShopTransactionId>();

        public FakeProfile(long currency, params LootEntry[] inventory)
        {
            Currency = currency;
            _inventory = inventory.ToList();
        }

        public long Currency { get; private set; }
        public StashOperationResult NextResult { get; set; } = StashOperationResult.Success;
        public List<MerchantTradeTicket> ExecutedTickets { get; } = new List<MerchantTradeTicket>();
        public int SlotCapacity => LocalProfileSnapshot.MaxLoadoutSlots;
        public int Revision { get; private set; }

        public event Action Changed;

        public bool TryGetLootContent(out IReadOnlyList<LootEntry> content)
        {
            content = _inventory.ToArray();
            return true;
        }

        public StashOperationResult TryExecuteTrade(ProfileId profileId, MerchantTradeTicket ticket)
        {
            ExecutedTickets.Add(ticket);
            if (NextResult != StashOperationResult.Success)
            {
                return NextResult;
            }

            _applied.Add(ticket.TransactionId);

            foreach (MerchantPricedTradeLine sale in ticket.Sales)
            {
                Apply(sale.LootId, -sale.Amount);
                Currency += sale.Total;
            }

            foreach (MerchantPricedTradeLine purchase in ticket.Purchases)
            {
                Apply(purchase.LootId, purchase.Amount);
                Currency -= purchase.Total;
            }

            Commit();
            return StashOperationResult.Success;
        }

        public bool IsTradeApplied(ProfileId profileId, ShopTransactionId transactionId) => _applied.Contains(transactionId);

        public void MarkApplied(ShopTransactionId transactionId) => _applied.Add(transactionId);

        public void CommitExternally(long currency)
        {
            Currency = currency;
            Commit();
        }

        private void Commit()
        {
            Revision++;
            Changed?.Invoke();
        }

        private void Apply(LootId lootId, int delta)
        {
            int index = _inventory.FindIndex(entry => entry.LootId == lootId);
            int amount = (index >= 0 ? _inventory[index].Amount : 0) + delta;
            if (index >= 0)
            {
                _inventory.RemoveAt(index);
            }

            if (amount > 0)
            {
                _inventory.Insert(index >= 0 ? index : _inventory.Count, new LootEntry(lootId, amount));
            }
        }
    }
}
