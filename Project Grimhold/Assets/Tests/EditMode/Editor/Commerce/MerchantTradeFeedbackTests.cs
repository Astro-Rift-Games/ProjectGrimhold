using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Assert = NUnit.Framework.Assert;
using Object = UnityEngine.Object;

public class MerchantTradeFeedbackTests
{
    private static readonly LootId Potion = new LootId("healthpotion");
    private static readonly LootId Hat = new LootId("placeholder_helmet");

    private LootDefinition _potion;
    private LootDefinition _hat;

    [SetUp]
    public void SetUp()
    {
        _potion = MerchantTestContent.CreateDefinition(Potion.Value, buyValue: 30, sellValue: 20);
        _hat = MerchantTestContent.CreateDefinition(Hat.Value, buyValue: 10, sellValue: 5);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_potion);
        Object.DestroyImmediate(_hat);
    }

    [Test]
    public void InsufficientCurrency_NamesTheShortfall()
    {
        string message = Describe(projectedCurrency: -40, new MerchantTradeBlock(MerchantTradeBlockReason.InsufficientCurrency));

        Assert.That(message, Is.EqualTo("Oro insuficiente: faltan 40"));
    }

    [TestCase(MerchantTradeBlockReason.CapacityExceeded, "Inventario lleno")]
    [TestCase(MerchantTradeBlockReason.SubmissionInFlight, "Transacción en curso...")]
    [TestCase(MerchantTradeBlockReason.Overflow, "Valores fuera de rango")]
    [TestCase(MerchantTradeBlockReason.EmptyDraft, "Agrega objetos a la transacción")]
    public void TradeWideBlocks_HaveTheirMessage(MerchantTradeBlockReason reason, string expected)
    {
        Assert.That(Describe(0, new MerchantTradeBlock(reason)), Is.EqualTo(expected));
    }

    [Test]
    public void InsufficientStock_NamesThePurchasedItem()
    {
        string message = Describe(0, new MerchantTradeBlock(MerchantTradeBlockReason.InsufficientStock, Potion));

        Assert.That(message, Is.EqualTo("Stock insuficiente: healthpotion"));
    }

    [Test]
    public void InsufficientOwnedUnits_NamesTheSoldItem()
    {
        string message = Describe(0, new MerchantTradeBlock(MerchantTradeBlockReason.InsufficientOwnedUnits, Hat));

        Assert.That(message, Is.EqualTo("Unidades no disponibles: placeholder_helmet"));
    }

    [TestCase(MerchantTradeBlockReason.UnknownItem, "Objeto inválido: missing")]
    [TestCase(MerchantTradeBlockReason.NotPurchasable, "Objeto no disponible para compra: missing")]
    public void InvalidItemOrConfiguration_NamesTheLoot(MerchantTradeBlockReason reason, string expected)
    {
        Assert.That(Describe(0, new MerchantTradeBlock(reason, new LootId("missing"))), Is.EqualTo(expected));
    }

    [Test]
    public void EveryBlockReason_HasADedicatedMessage()
    {
        foreach (MerchantTradeBlockReason reason in Enum.GetValues(typeof(MerchantTradeBlockReason)))
        {
            string message = Describe(0, new MerchantTradeBlock(reason));
            Assert.That(message, Is.Not.Empty.And.Not.EqualTo("Transacción no disponible"), reason.ToString());
        }
    }

    [Test]
    public void InFlight_IsThePrimaryBlockOverAnyOther()
    {
        MerchantShopViewModel view = View(0,
            new MerchantTradeBlock(MerchantTradeBlockReason.CapacityExceeded),
            new MerchantTradeBlock(MerchantTradeBlockReason.SubmissionInFlight));

        Assert.That(MerchantTradeFeedback.DescribePrimaryBlock(view), Is.EqualTo("Transacción en curso..."));
    }

    [Test]
    public void CurrencyOutranksCapacityAndEmptyDraft()
    {
        Assert.That(MerchantTradeFeedback.TryGetPrimaryBlock(new[]
        {
            new MerchantTradeBlock(MerchantTradeBlockReason.EmptyDraft),
            new MerchantTradeBlock(MerchantTradeBlockReason.CapacityExceeded),
            new MerchantTradeBlock(MerchantTradeBlockReason.InsufficientCurrency)
        }, out MerchantTradeBlock primary), Is.True);

        Assert.That(primary.Reason, Is.EqualTo(MerchantTradeBlockReason.InsufficientCurrency));
    }

    [Test]
    public void NoBlock_HasNoMessage()
    {
        Assert.That(MerchantTradeFeedback.TryGetPrimaryBlock(Array.Empty<MerchantTradeBlock>(), out _), Is.False);
        Assert.That(MerchantTradeFeedback.DescribePrimaryBlock(View(0)), Is.Empty);
    }

    [Test]
    public void UnavailableTrade_SaysSo()
    {
        Assert.That(MerchantTradeFeedback.DescribePrimaryBlock(MerchantShopViewModel.Unavailable), Is.EqualTo("Comercio no disponible"));
    }

    [Test]
    public void EveryTransactionResult_HasAMessage()
    {
        foreach (MerchantTransactionResult result in Enum.GetValues(typeof(MerchantTransactionResult)))
        {
            Assert.That(MerchantTradeFeedback.DescribeResult(result), Is.Not.Empty, result.ToString());
        }
    }

    private string Describe(long projectedCurrency, MerchantTradeBlock block) =>
        MerchantTradeFeedback.Describe(block, View(projectedCurrency, block));

    private MerchantShopViewModel View(long projectedCurrency, params MerchantTradeBlock[] blocks)
    {
        var merchant = new List<MerchantShopRowViewModel>
        {
            new MerchantShopRowViewModel(_potion, Potion, true, 30, 3, 0, 0, true, 3)
        };
        var inventory = new List<MerchantShopRowViewModel>
        {
            new MerchantShopRowViewModel(_hat, Hat, false, 5, 1, 0, 0, true, 1)
        };

        return new MerchantShopViewModel(
            true, 100, projectedCurrency, 0, 0, 0, 1, 1, 10, true, false, blocks.Length == 0, blocks, merchant, inventory);
    }
}
