using System;
using System.Collections.Generic;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;

public class MerchantTradeTicketTests
{
    private static readonly ShopTransactionId Id = new ShopTransactionId(1000, new Guid("22222222-2222-2222-2222-222222222222"));
    private static readonly LootId Potion = new LootId("healthpotion");
    private static readonly LootId Bone = new LootId("bone");
    private static readonly MerchantPricedTradeLine[] None = Array.Empty<MerchantPricedTradeLine>();

    [Test]
    public void Ticket_CopiesLinesAndExposesReadOnlyCollections()
    {
        var purchases = new List<MerchantPricedTradeLine> { new MerchantPricedTradeLine(Potion, 2, 30) };
        var ticket = new MerchantTradeTicket(Id, purchases, None);

        purchases.Add(new MerchantPricedTradeLine(Bone, 1, 5));

        Assert.That(ticket.Purchases.Count, Is.EqualTo(1));
        Assert.Throws<NotSupportedException>(() => ((IList<MerchantPricedTradeLine>)ticket.Purchases).Add(default));
        Assert.Throws<NotSupportedException>(() => ((IList<MerchantPricedTradeLine>)ticket.Sales).Add(default));
    }

    [Test]
    public void LineTotal_DoesNotOverflow()
    {
        Assert.That(new MerchantPricedTradeLine(Potion, int.MaxValue, int.MaxValue).Total, Is.EqualTo((long)int.MaxValue * int.MaxValue));
    }

    [Test]
    public void WellFormedTicket_AllowsSameLootOnceOnEachSide()
    {
        var ticket = new MerchantTradeTicket(
            Id,
            new[] { new MerchantPricedTradeLine(Potion, 2, 30), new MerchantPricedTradeLine(Bone, 1, 5) },
            new[] { new MerchantPricedTradeLine(Potion, 1, 0) });

        Assert.That(ticket.IsWellFormed, Is.True);
        Assert.That(ticket.LineCount, Is.EqualTo(3));
    }

    [Test]
    public void MalformedTickets_AreDetected()
    {
        MerchantPricedTradeLine valid = new MerchantPricedTradeLine(Potion, 1, 1);

        Assert.That(new MerchantTradeTicket(Id, None, None).IsWellFormed, Is.False, "Empty.");
        Assert.That(new MerchantTradeTicket(default, new[] { valid }, None).IsWellFormed, Is.False, "Invalid id.");
        Assert.That(new MerchantTradeTicket(Id, new[] { new MerchantPricedTradeLine(default, 1, 1) }, None).IsWellFormed, Is.False, "Invalid loot.");
        Assert.That(new MerchantTradeTicket(Id, None, new[] { new MerchantPricedTradeLine(Potion, 0, 1) }).IsWellFormed, Is.False, "Zero amount.");
        Assert.That(new MerchantTradeTicket(Id, new[] { new MerchantPricedTradeLine(Potion, 1, 0) }, None).IsWellFormed, Is.False, "Free purchase.");
        Assert.That(new MerchantTradeTicket(Id, None, new[] { new MerchantPricedTradeLine(Potion, 1, -1) }).IsWellFormed, Is.False, "Negative sale.");
        Assert.That(new MerchantTradeTicket(Id, new[] { valid, valid }, None).IsWellFormed, Is.False, "Duplicate purchase loot.");
    }

    [Test]
    public void NullLineCollections_AreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new MerchantTradeTicket(Id, null, None));
        Assert.Throws<ArgumentNullException>(() => new MerchantTradeTicket(Id, None, null));
    }
}
