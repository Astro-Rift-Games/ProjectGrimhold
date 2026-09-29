using System;
using System.Collections.Generic;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;

public class MerchantRequestValidatorTests
{
    private const string Potion = "healthpotion";
    private const string Bone = "bone";
    private const string Hat = "placeholder_helmet";
    private const string Coin = "ancient_coin";

    private static readonly ProfileId ProfileA = new ProfileId("profile-a");
    private static readonly ProfileId ProfileB = new ProfileId("profile-b");
    private static readonly MerchantTradeLine[] None = Array.Empty<MerchantTradeLine>();

    private FakeMerchantAuthorityState _state;
    private LootDefinitionCatalog _catalog;
    private List<MerchantStockItem> _stock;
    private MerchantRequestValidator _validator;
    private int _guidGenerationCount;
    private long _nextTimestamp;

    [SetUp]
    public void Setup()
    {
        LootDefinition potion = MerchantTestContent.CreateDefinition(Potion, buyValue: 30, sellValue: 20, extractionValue: 100);
        LootDefinition bone = MerchantTestContent.CreateDefinition(Bone, buyValue: 5, sellValue: 4);
        LootDefinition hat = MerchantTestContent.CreateDefinition(Hat, buyValue: 10, sellValue: 5);
        LootDefinition coin = MerchantTestContent.CreateDefinition(Coin, buyValue: 0, sellValue: 7);

        _catalog = MerchantTestContent.CreateCatalog(potion, bone, hat, coin);
        _stock = new List<MerchantStockItem>
        {
            MerchantTestContent.Offer(potion, 3),
            MerchantTestContent.Offer(bone, MerchantStockItem.UnlimitedQuantity),
            MerchantTestContent.Offer(hat, 1)
        };

        _state = new FakeMerchantAuthorityState();
        CreateValidator();
        Assert.That(_validator.TryInitializeStock(), Is.True);
    }

    private void CreateValidator()
    {
        _guidGenerationCount = 0;
        _nextTimestamp = 1000;
        _validator = new MerchantRequestValidator(
            _stock,
            _state,
            timestampProvider: () => _nextTimestamp++,
            guidProvider: () =>
            {
                _guidGenerationCount++;
                return Guid.NewGuid();
            });
    }

    // --- Authority ---

    [Test]
    public void WithoutStateAuthority_RequestsAreNotProcessedAndStockIsUntouched()
    {
        _state.CanMutate = false;

        Assert.That(Trade(ProfileA, NewId(), Lines(Line(Potion, 1)), None, out MerchantTradeResponse response), Is.False);

        Assert.That(response.IsApproved, Is.False);
        Assert.That(_state.CountRecords(MerchantTradeRecordStatus.Pending), Is.Zero);
        Assert.That(_guidGenerationCount, Is.Zero);
    }

    [Test]
    public void Proxy_CannotInitializeCompleteOrReleaseStock()
    {
        Guid requestId = NewId();
        Trade(ProfileA, requestId, Lines(Line(Potion, 2)), None, out MerchantTradeResponse response);
        int changesBefore = _state.StockChangeCount;
        _state.CanMutate = false;

        Assert.That(_validator.TryInitializeStock(), Is.False);
        Assert.That(Complete(ProfileA, requestId, response, persisted: true), Is.False);
        Assert.That(Complete(ProfileA, requestId, response, persisted: false), Is.False);

        Assert.That(_state.GetStockQuantity(0), Is.EqualTo(3));
        Assert.That(_state.CountRecords(MerchantTradeRecordStatus.Pending), Is.EqualTo(1));
        Assert.That(_state.StockChangeCount, Is.EqualTo(changesBefore));
    }

    [Test]
    public void Initialize_SeedsSharedStockFromConfiguration()
    {
        Assert.That(_state.GetStockQuantity(0), Is.EqualTo(3));
        Assert.That(_state.GetStockQuantity(1), Is.EqualTo(MerchantStockItem.UnlimitedQuantity));
        Assert.That(_state.GetStockQuantity(2), Is.EqualTo(1));
        Assert.That(Available(Potion), Is.EqualTo(3));
        Assert.That(Available(Bone), Is.EqualTo(MerchantStockItem.UnlimitedQuantity));
        Assert.That(Available(Coin), Is.Zero, "Not offered.");
    }

    // --- Ticket ---

    [Test]
    public void MultiLineTrade_IsAnsweredWithOneTicketPricedByAuthority()
    {
        Trade(ProfileA, NewId(),
            Lines(Line(Potion, 2), Line(Bone, 3)),
            Lines(Line(Hat, 1), Line(Coin, 2)),
            out MerchantTradeResponse response);

        Assert.That(response.IsApproved, Is.True);
        MerchantTradeTicket ticket = response.Ticket;
        Assert.That(ticket.IsWellFormed, Is.True);
        Assert.That(ticket.TransactionId.IsValid, Is.True);
        Assert.That(_guidGenerationCount, Is.EqualTo(1), "One transaction id per accepted trade.");
        AssertLine(ticket.Purchases[0], Potion, 2, 30);
        AssertLine(ticket.Purchases[1], Bone, 3, 5);
        AssertLine(ticket.Sales[0], Hat, 1, 5);
        AssertLine(ticket.Sales[1], Coin, 2, 7);
    }

    [Test]
    public void Authority_ResolvesBuyAndSellValuesNeverExtractionValue()
    {
        Trade(ProfileA, NewId(), Lines(Line(Potion, 2)), Lines(Line(Potion, 3)), out MerchantTradeResponse response);

        AssertLine(response.Ticket.Purchases[0], Potion, 2, 30);
        AssertLine(response.Ticket.Sales[0], Potion, 3, 20);
    }

    [Test]
    public void SameLootBoughtAndSold_IsAcceptedAsIndependentLines()
    {
        Trade(ProfileA, NewId(), Lines(Line(Potion, 1)), Lines(Line(Potion, 1)), out MerchantTradeResponse response);

        Assert.That(response.IsApproved, Is.True);
        Assert.That(Available(Potion), Is.EqualTo(2), "The purchase reserves; the sale adds stock only on success.");
    }

    // --- Validation ---

    private static IEnumerable<TestCaseData> InvalidTrades()
    {
        yield return new TestCaseData(None, None).SetName("Empty");
        yield return new TestCaseData(Lines(Line(Potion, 0)), None).SetName("ZeroAmount");
        yield return new TestCaseData(None, Lines(Line(Potion, -1))).SetName("NegativeAmount");
        yield return new TestCaseData(Lines(Line("unknown", 1)), None).SetName("UnknownItem");
        yield return new TestCaseData(Lines(new MerchantTradeLine(default, 1)), None).SetName("InvalidLootId");
        yield return new TestCaseData(Lines(Line(Coin, 1)), None).SetName("NotOffered");
        yield return new TestCaseData(Lines(Line(Potion, 4)), None).SetName("InsufficientStock");
        yield return new TestCaseData(Lines(Line(Potion, 1), Line(Potion, 1)), None).SetName("DuplicatePurchaseLoot");
        yield return new TestCaseData(None, Lines(Line(Bone, 1), Line(Bone, 1))).SetName("DuplicateSaleLoot");
        yield return new TestCaseData(Lines(Line(Potion, 1)), Lines(Line("unknown", 1))).SetName("OneInvalidLineRejectsAll");
    }

    [TestCaseSource(nameof(InvalidTrades))]
    public void InvalidTrades_AreRejectedWithoutReservation(MerchantTradeLine[] purchases, MerchantTradeLine[] sales)
    {
        Assert.That(Trade(ProfileA, NewId(), purchases, sales, out MerchantTradeResponse response), Is.True);

        Assert.That(response.IsApproved, Is.False);
        Assert.That(_state.CountRecords(MerchantTradeRecordStatus.Pending), Is.Zero);
        Assert.That(Available(Potion), Is.EqualTo(3));
        Assert.That(_guidGenerationCount, Is.Zero);
    }

    [Test]
    public void InvalidIdentity_IsRejected()
    {
        Trade(default, NewId(), Lines(Line(Bone, 1)), None, out MerchantTradeResponse noProfile);
        Trade(ProfileA, Guid.Empty, Lines(Line(Bone, 1)), None, out MerchantTradeResponse noRequest);

        Assert.That(noProfile.IsApproved, Is.False);
        Assert.That(noRequest.IsApproved, Is.False);
    }

    // --- Shared stock ---

    [Test]
    public void FiniteStock_IsSharedBetweenProfiles()
    {
        Guid first = NewId();
        Trade(ProfileA, first, Lines(Line(Potion, 2)), None, out MerchantTradeResponse accepted);
        Complete(ProfileA, first, accepted, persisted: true);

        Trade(ProfileB, NewId(), Lines(Line(Potion, 2)), None, out MerchantTradeResponse tooMany);
        Trade(ProfileB, NewId(), Lines(Line(Potion, 1)), None, out MerchantTradeResponse last);

        Assert.That(tooMany.IsApproved, Is.False);
        Assert.That(last.IsApproved, Is.True);
    }

    [Test]
    public void UnlimitedStock_NeverRunsOutOrChanges()
    {
        for (int i = 0; i < 5; i++)
        {
            Guid requestId = NewId();
            Trade(ProfileA, requestId, Lines(Line(Bone, 1000)), None, out MerchantTradeResponse response);
            Assert.That(response.IsApproved, Is.True);
            Complete(ProfileA, requestId, response, persisted: true);
        }

        Assert.That(_state.GetStockQuantity(1), Is.EqualTo(MerchantStockItem.UnlimitedQuantity));
        Assert.That(Available(Bone), Is.EqualTo(MerchantStockItem.UnlimitedQuantity));
    }

    [Test]
    public void LastUnit_IsGrantedToOnlyOneOfTwoConcurrentTrades()
    {
        Trade(ProfileA, NewId(), Lines(Line(Hat, 1)), None, out MerchantTradeResponse first);
        Trade(ProfileB, NewId(), Lines(Line(Hat, 1), Line(Bone, 1)), None, out MerchantTradeResponse second);

        Assert.That(first.IsApproved, Is.True);
        Assert.That(second.IsApproved, Is.False, "One unavailable line rejects the whole trade.");
        Assert.That(Available(Hat), Is.Zero);
    }

    // --- Reservation lifecycle ---

    [Test]
    public void ApprovedTrade_ReservesEveryPurchaseLineWithoutConsumingStock()
    {
        Trade(ProfileA, NewId(), Lines(Line(Potion, 2), Line(Hat, 1)), None, out MerchantTradeResponse response);

        Assert.That(response.IsApproved, Is.True);
        Assert.That(_state.GetStockQuantity(0), Is.EqualTo(3), "Approval must not consume stock.");
        Assert.That(Available(Potion), Is.EqualTo(1));
        Assert.That(Available(Hat), Is.Zero);
    }

    [Test]
    public void PersistedTrade_ConsumesReservationsAndRestocksSoldOfferedItems()
    {
        Guid requestId = NewId();
        Trade(ProfileA, requestId, Lines(Line(Potion, 2)), Lines(Line(Hat, 4), Line(Coin, 1)), out MerchantTradeResponse response);

        Assert.That(Complete(ProfileA, requestId, response, persisted: true), Is.True);

        Assert.That(_state.GetStockQuantity(0), Is.EqualTo(1));
        Assert.That(_state.GetStockQuantity(2), Is.EqualTo(5), "Sold offered units return to stock.");
        Assert.That(Available(Coin), Is.Zero, "Items outside the offering never become purchasable.");
        Assert.That(_state.ActiveStockLineCount, Is.Zero);
        Assert.That(_state.CountRecords(MerchantTradeRecordStatus.Committed), Is.EqualTo(1));
    }

    [Test]
    public void FailedTrade_ReleasesReservationsWithoutRestock()
    {
        Guid requestId = NewId();
        Trade(ProfileA, requestId, Lines(Line(Potion, 2)), Lines(Line(Hat, 4)), out MerchantTradeResponse response);

        Assert.That(Complete(ProfileA, requestId, response, persisted: false), Is.True);

        Assert.That(_state.GetStockQuantity(0), Is.EqualTo(3));
        Assert.That(_state.GetStockQuantity(2), Is.EqualTo(1));
        Assert.That(Available(Potion), Is.EqualTo(3));
        Assert.That(_state.CountRecords(MerchantTradeRecordStatus.Released), Is.EqualTo(1));
    }

    [Test]
    public void OutcomeWithDifferentTransactionId_IsIgnored()
    {
        Guid requestId = NewId();
        Trade(ProfileA, requestId, Lines(Line(Potion, 2)), None, out _);
        var stale = new ShopTransactionId(1, Guid.NewGuid());

        Assert.That(_validator.TryCompleteTrade(ProfileA, requestId, stale, persisted: true), Is.False);

        Assert.That(_state.GetStockQuantity(0), Is.EqualTo(3));
        Assert.That(Available(Potion), Is.EqualTo(1));
    }

    [Test]
    public void OutcomeFromAnotherProfile_IsIgnored()
    {
        Guid requestId = NewId();
        Trade(ProfileA, requestId, Lines(Line(Potion, 2)), None, out MerchantTradeResponse response);

        Assert.That(Complete(ProfileB, requestId, response, persisted: true), Is.False);
        Assert.That(_state.GetStockQuantity(0), Is.EqualTo(3));
    }

    // --- Request identity and deduplication ---

    [Test]
    public void IdenticalRetry_ReturnsSameTicketWithoutSecondReservation()
    {
        Guid requestId = NewId();
        Trade(ProfileA, requestId, Lines(Line(Potion, 2)), Lines(Line(Hat, 1)), out MerchantTradeResponse first);
        Trade(ProfileA, requestId, Lines(Line(Potion, 2)), Lines(Line(Hat, 1)), out MerchantTradeResponse retry);

        Assert.That(retry.IsApproved, Is.True);
        AssertSameTicket(retry.Ticket, first.Ticket);
        Assert.That(_guidGenerationCount, Is.EqualTo(1));
        Assert.That(_state.CountRecords(MerchantTradeRecordStatus.Pending), Is.EqualTo(1));
        Assert.That(Available(Potion), Is.EqualTo(1));
    }

    private static IEnumerable<TestCaseData> ConflictingPayloads()
    {
        yield return new TestCaseData(Lines(Line(Potion, 1)), None).SetName("DifferentAmount");
        yield return new TestCaseData(Lines(Line(Bone, 2)), None).SetName("DifferentItem");
        yield return new TestCaseData(None, Lines(Line(Potion, 2))).SetName("DifferentSide");
        yield return new TestCaseData(Lines(Line(Potion, 2), Line(Bone, 1)), None).SetName("ExtraLine");
    }

    [TestCaseSource(nameof(ConflictingPayloads))]
    public void SameRequestIdWithDifferentPayload_IsRejected(MerchantTradeLine[] purchases, MerchantTradeLine[] sales)
    {
        Guid requestId = NewId();
        Trade(ProfileA, requestId, Lines(Line(Potion, 2)), None, out _);

        Trade(ProfileA, requestId, purchases, sales, out MerchantTradeResponse conflict);

        Assert.That(conflict.IsApproved, Is.False);
        Assert.That(_guidGenerationCount, Is.EqualTo(1));
        Assert.That(_state.CountRecords(MerchantTradeRecordStatus.Pending), Is.EqualTo(1));
        Assert.That(Available(Potion), Is.EqualTo(1));
    }

    [Test]
    public void SameRequestIdFromDifferentProfiles_AreIndependent()
    {
        Guid requestId = NewId();
        Trade(ProfileA, requestId, Lines(Line(Bone, 1)), None, out MerchantTradeResponse first);
        Trade(ProfileB, requestId, Lines(Line(Bone, 1)), None, out MerchantTradeResponse second);

        Assert.That(first.IsApproved, Is.True);
        Assert.That(second.IsApproved, Is.True);
        Assert.That(first.Ticket.TransactionId, Is.Not.EqualTo(second.Ticket.TransactionId));
    }

    [Test]
    public void LateRetryAfterCommit_ReturnsSameTicketWithoutNewTransactionOrReservation()
    {
        Guid requestId = NewId();
        Trade(ProfileA, requestId, Lines(Line(Potion, 2)), None, out MerchantTradeResponse first);
        Complete(ProfileA, requestId, first, persisted: true);

        Trade(ProfileA, requestId, Lines(Line(Potion, 2)), None, out MerchantTradeResponse late);

        AssertSameTicket(late.Ticket, first.Ticket);
        Assert.That(_guidGenerationCount, Is.EqualTo(1));
        Assert.That(_state.GetStockQuantity(0), Is.EqualTo(1));
        Assert.That(Available(Potion), Is.EqualTo(1), "No second reservation.");
        Assert.That(Complete(ProfileA, requestId, late, persisted: true), Is.False, "A second persisted outcome consumes nothing.");
        Assert.That(_state.GetStockQuantity(0), Is.EqualTo(1));
    }

    [Test]
    public void RetryAfterFailedOutcome_ReservesAgainUnderTheSameTransactionId()
    {
        Guid requestId = NewId();
        Trade(ProfileA, requestId, Lines(Line(Potion, 2)), None, out MerchantTradeResponse first);
        Complete(ProfileA, requestId, first, persisted: false);

        Trade(ProfileA, requestId, Lines(Line(Potion, 2)), None, out MerchantTradeResponse retry);

        AssertSameTicket(retry.Ticket, first.Ticket);
        Assert.That(_guidGenerationCount, Is.EqualTo(1));
        Assert.That(Available(Potion), Is.EqualTo(1));
        Assert.That(Complete(ProfileA, requestId, retry, persisted: true), Is.True);
        Assert.That(_state.GetStockQuantity(0), Is.EqualTo(1));
    }

    [Test]
    public void PersistedOutcomeForAReleasedTrade_IsIgnored()
    {
        Guid requestId = NewId();
        Trade(ProfileA, requestId, Lines(Line(Potion, 2)), None, out MerchantTradeResponse first);
        Complete(ProfileA, requestId, first, persisted: false);

        Assert.That(Complete(ProfileA, requestId, first, persisted: true), Is.False,
            "A released ticket is only persisted after reserving again.");
        Assert.That(_state.CountRecords(MerchantTradeRecordStatus.Released), Is.EqualTo(1));
        Assert.That(_state.GetStockQuantity(0), Is.EqualTo(3));
    }

    [Test]
    public void DeduplicationHistory_IsBoundedAndEvictsTheOldestCompletedTrade()
    {
        _state = new FakeMerchantAuthorityState(tradeRecordCapacity: 2);
        CreateValidator();
        _validator.TryInitializeStock();

        Guid oldest = NewId();
        Trade(ProfileA, oldest, Lines(Line(Bone, 1)), None, out MerchantTradeResponse oldestResponse);
        Complete(ProfileA, oldest, oldestResponse, persisted: true);
        Guid newer = NewId();
        Trade(ProfileA, newer, Lines(Line(Bone, 1)), None, out MerchantTradeResponse newerResponse);
        Complete(ProfileA, newer, newerResponse, persisted: true);

        Trade(ProfileA, NewId(), Lines(Line(Bone, 1)), None, out MerchantTradeResponse third);
        Trade(ProfileA, newer, Lines(Line(Bone, 1)), None, out MerchantTradeResponse newerRetry);

        Assert.That(third.IsApproved, Is.True);
        AssertSameTicket(newerRetry.Ticket, newerResponse.Ticket);
        Assert.That(_state.CountRecords(MerchantTradeRecordStatus.Committed) + _state.CountRecords(MerchantTradeRecordStatus.Pending), Is.EqualTo(2));
    }

    [Test]
    public void PendingCapacityExhausted_RejectsNewTrades()
    {
        for (int i = 0; i < _state.TradeRecordCapacity; i++)
        {
            Trade(ProfileA, NewId(), Lines(Line(Bone, 1)), None, out MerchantTradeResponse accepted);
            Assert.That(accepted.IsApproved, Is.True);
        }

        Trade(ProfileA, NewId(), Lines(Line(Bone, 1)), None, out MerchantTradeResponse overflow);

        Assert.That(overflow.IsApproved, Is.False, "Pending trades are never evicted.");
    }

    [Test]
    public void StockLineCapacityExhausted_RejectsTradeWithoutPartialReservation()
    {
        _state = new FakeMerchantAuthorityState(tradeStockLineCapacity: 1);
        CreateValidator();
        _validator.TryInitializeStock();

        Trade(ProfileA, NewId(), Lines(Line(Potion, 1), Line(Hat, 1)), None, out MerchantTradeResponse response);

        Assert.That(response.IsApproved, Is.False);
        Assert.That(_state.ActiveStockLineCount, Is.Zero);
        Assert.That(Available(Potion), Is.EqualTo(3));
    }

    // --- Reservations held until the outcome ---

    [Test]
    public void LastUnitReservation_IsHeldUntilItsOutcomeSoItIsNeverSoldTwice()
    {
        Guid requestA = NewId();
        Trade(ProfileA, requestA, Lines(Line(Hat, 1)), None, out MerchantTradeResponse accepted);

        // However late the outcome is, nothing but the outcome releases the reservation.
        Trade(ProfileB, NewId(), Lines(Line(Hat, 1)), None, out MerchantTradeResponse competitor);
        Assert.That(competitor.IsApproved, Is.False);
        Assert.That(Available(Hat), Is.Zero);

        Assert.That(Complete(ProfileA, requestA, accepted, persisted: true), Is.True);
        Trade(ProfileB, NewId(), Lines(Line(Hat, 1)), None, out MerchantTradeResponse afterOutcome);

        Assert.That(_state.GetStockQuantity(2), Is.Zero);
        Assert.That(afterOutcome.IsApproved, Is.False, "The unit was sold once.");
    }

    [Test]
    public void PendingTrades_AreListedPerProfileForResolution()
    {
        Guid pendingA = NewId();
        Trade(ProfileA, pendingA, Lines(Line(Potion, 1)), None, out MerchantTradeResponse pending);
        Guid committedA = NewId();
        Trade(ProfileA, committedA, Lines(Line(Bone, 1)), None, out MerchantTradeResponse committed);
        Complete(ProfileA, committedA, committed, persisted: true);
        Trade(ProfileB, NewId(), Lines(Line(Potion, 1)), None, out _);

        var records = new List<MerchantTradeRecord>();
        Assert.That(_validator.GetPendingTrades(ProfileA, records), Is.EqualTo(1));

        Assert.That(records[0].RequestId, Is.EqualTo(pendingA));
        Assert.That(records[0].TransactionId, Is.EqualTo(pending.Ticket.TransactionId));
    }

    [Test]
    public void ResolvingAPendingTrade_ConsumesOrReleasesItOnce()
    {
        Guid persisted = NewId();
        Trade(ProfileA, persisted, Lines(Line(Potion, 1)), None, out MerchantTradeResponse persistedTicket);
        Guid abandoned = NewId();
        Trade(ProfileA, abandoned, Lines(Line(Potion, 1)), None, out MerchantTradeResponse abandonedTicket);

        Assert.That(Complete(ProfileA, persisted, persistedTicket, persisted: true), Is.True);
        Assert.That(Complete(ProfileA, abandoned, abandonedTicket, persisted: false), Is.True);
        Assert.That(Complete(ProfileA, persisted, persistedTicket, persisted: true), Is.False, "Repeated report.");

        Assert.That(_state.GetStockQuantity(0), Is.EqualTo(2));
        Assert.That(Available(Potion), Is.EqualTo(2));
        Assert.That(_validator.GetPendingTrades(ProfileA, new List<MerchantTradeRecord>()), Is.Zero);
    }

    [Test]
    public void StockChanges_AreSignaledForReservationConsumptionAndRestock()
    {
        int initial = _state.StockChangeCount;

        Guid purchase = NewId();
        Trade(ProfileA, purchase, Lines(Line(Potion, 1)), None, out MerchantTradeResponse bought);
        Complete(ProfileA, purchase, bought, persisted: true);
        Guid sale = NewId();
        Trade(ProfileA, sale, None, Lines(Line(Potion, 1)), out MerchantTradeResponse sold);
        Complete(ProfileA, sale, sold, persisted: true);

        Assert.That(_state.StockChangeCount, Is.EqualTo(initial + 3));
    }

    // --- Configuration ---

    [Test]
    public void StockConfiguration_AcceptsValidOffering()
    {
        Assert.That(MerchantRequestValidator.TryValidateStockConfiguration(_stock, 16, out string error), Is.True, error);
    }

    [Test]
    public void StockConfiguration_RejectsInvalidOfferings()
    {
        LootDefinition potion = _stock[0].Item;

        AssertInvalid(new List<MerchantStockItem> { _stock[0], _stock[1] }, capacity: 1, "at most");
        AssertInvalid(new List<MerchantStockItem> { new MerchantStockItem { Item = null, InitialQuantity = 1 } }, 16, "no item");
        AssertInvalid(new List<MerchantStockItem> { MerchantTestContent.Offer(potion, -2) }, 16, "initial quantity");
        AssertInvalid(new List<MerchantStockItem> { MerchantTestContent.Offer(potion, 1), MerchantTestContent.Offer(potion, 2) }, 16, "more than once");
    }

    private static void AssertInvalid(List<MerchantStockItem> stock, int capacity, string expectedError)
    {
        Assert.That(MerchantRequestValidator.TryValidateStockConfiguration(stock, capacity, out string error), Is.False);
        Assert.That(error, Does.Contain(expectedError));
    }

    private static void AssertLine(MerchantPricedTradeLine line, string lootId, int amount, int unitPrice)
    {
        Assert.That(line.LootId, Is.EqualTo(new LootId(lootId)));
        Assert.That(line.Amount, Is.EqualTo(amount));
        Assert.That(line.UnitPrice, Is.EqualTo(unitPrice));
    }

    private static void AssertSameTicket(MerchantTradeTicket actual, MerchantTradeTicket expected)
    {
        Assert.That(actual, Is.Not.Null);
        Assert.That(actual.TransactionId, Is.EqualTo(expected.TransactionId));
        Assert.That(actual.Purchases, Is.EqualTo(expected.Purchases));
        Assert.That(actual.Sales, Is.EqualTo(expected.Sales));
    }

    private static MerchantTradeLine Line(string lootId, int amount) => new MerchantTradeLine(new LootId(lootId), amount);
    private static MerchantTradeLine[] Lines(params MerchantTradeLine[] lines) => lines;
    private static Guid NewId() => Guid.NewGuid();

    private int Available(string lootId) => _validator.GetAvailableStock(new LootId(lootId));

    private bool Trade(
        ProfileId profile,
        Guid requestId,
        MerchantTradeLine[] purchases,
        MerchantTradeLine[] sales,
        out MerchantTradeResponse response)
    {
        return _validator.TryProcessTradeRequest(profile, requestId, purchases, sales, _catalog, out response);
    }

    private bool Complete(ProfileId profile, Guid requestId, MerchantTradeResponse response, bool persisted)
    {
        return _validator.TryCompleteTrade(profile, requestId, response.Ticket.TransactionId, persisted);
    }
}
