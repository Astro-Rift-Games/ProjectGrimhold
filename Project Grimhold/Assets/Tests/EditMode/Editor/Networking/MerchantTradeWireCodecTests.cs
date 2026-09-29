using System;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;

public class MerchantTradeWireCodecTests
{
    private static readonly ShopTransactionId TransactionId = new ShopTransactionId(1000, new Guid("33333333-3333-3333-3333-333333333333"));

    private LootDefinitionCatalog _catalog;

    [SetUp]
    public void Setup()
    {
        _catalog = MerchantTestContent.CreateCatalog(
            MerchantTestContent.CreateDefinition("healthpotion", 30, 20),
            MerchantTestContent.CreateDefinition("bone", 5, 4));
    }

    [Test]
    public void Request_RoundTripsThroughCatalogIndices()
    {
        var lines = new[] { Line("bone", 3), Line("healthpotion", 1) };

        Assert.That(MerchantTradeWireCodec.TryEncodeRequest(_catalog, lines, out MerchantTradeRequestLineMessage[] messages), Is.True);
        Assert.That(MerchantTradeWireCodec.TryDecodeRequest(_catalog, messages, 16, out MerchantTradeLine[] decoded), Is.True);

        Assert.That(decoded, Is.EqualTo(lines));
    }

    [Test]
    public void Ticket_RoundTripsWithAuthorityPrices()
    {
        var purchases = new[] { new MerchantPricedTradeLine(new LootId("healthpotion"), 2, 30) };
        var sales = new[] { new MerchantPricedTradeLine(new LootId("bone"), 4, 4) };

        MerchantTradeWireCodec.TryEncodeTicketLines(_catalog, purchases, out MerchantPricedTradeLineMessage[] purchaseMessages);
        MerchantTradeWireCodec.TryEncodeTicketLines(_catalog, sales, out MerchantPricedTradeLineMessage[] saleMessages);

        Assert.That(MerchantTradeWireCodec.TryDecodeTicket(_catalog, TransactionId, purchaseMessages, saleMessages, 16, 30, out MerchantTradeTicket ticket), Is.True);
        Assert.That(ticket.TransactionId, Is.EqualTo(TransactionId));
        Assert.That(ticket.Purchases, Is.EqualTo(purchases));
        Assert.That(ticket.Sales, Is.EqualTo(sales));
    }

    [Test]
    public void Decoding_RejectsUnknownIndicesTooManyLinesAndMalformedTickets()
    {
        var unknown = new[] { new MerchantTradeRequestLineMessage { LootIndex = 99, Amount = 1 } };
        var twoLines = new[] { new MerchantTradeRequestLineMessage { LootIndex = 0, Amount = 1 }, new MerchantTradeRequestLineMessage { LootIndex = 1, Amount = 1 } };
        var freePurchase = new[] { new MerchantPricedTradeLineMessage { LootIndex = 0, Amount = 1, UnitPrice = 0 } };
        var none = Array.Empty<MerchantPricedTradeLineMessage>();

        Assert.That(MerchantTradeWireCodec.TryDecodeRequest(_catalog, unknown, 16, out _), Is.False);
        Assert.That(MerchantTradeWireCodec.TryDecodeRequest(_catalog, twoLines, 1, out _), Is.False);
        Assert.That(MerchantTradeWireCodec.TryDecodeRequest(_catalog, null, 16, out _), Is.False);
        Assert.That(MerchantTradeWireCodec.TryDecodeTicket(_catalog, TransactionId, freePurchase, none, 16, 30, out _), Is.False);
        Assert.That(MerchantTradeWireCodec.TryDecodeTicket(_catalog, TransactionId, none, none, 16, 30, out _), Is.False, "Empty ticket.");
    }

    [Test]
    public void Encoding_RejectsItemsOutsideTheCatalog()
    {
        Assert.That(MerchantTradeWireCodec.TryEncodeRequest(_catalog, new[] { Line("unknown", 1) }, out _), Is.False);
    }

    private static MerchantTradeLine Line(string lootId, int amount) => new MerchantTradeLine(new LootId(lootId), amount);
}
