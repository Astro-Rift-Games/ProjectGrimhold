#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using NUnit.Framework;

[Category("TASK206")]
public sealed class AbilityVendorTextTests
{
    private const string NoGoldNotice = "Not enough gold for any ability. Sell loot to the Merchant to earn gold.";

    [Test]
    public void BuyLabel_Available_IsBuy()
    {
        Assert.That(AbilityVendorText.BuyLabel(AbilityVendorOfferState.Available, 150L), Is.EqualTo("Buy"));
    }

    [Test]
    public void BuyLabel_AlreadyUnlocked_IsOwned()
    {
        Assert.That(AbilityVendorText.BuyLabel(AbilityVendorOfferState.AlreadyUnlocked, 150L), Is.EqualTo("Owned"));
    }

    [Test]
    public void BuyLabel_InsufficientCurrency_ShowsPrice()
    {
        Assert.That(AbilityVendorText.BuyLabel(AbilityVendorOfferState.InsufficientCurrency, 1500L), Is.EqualTo("Need 1500"));
    }

    [Test]
    public void Notice_AllInsufficient_ExplainsHowToEarnGold()
    {
        var entries = new[] { Entry(AbilityVendorOfferState.InsufficientCurrency), Entry(AbilityVendorOfferState.InsufficientCurrency) };

        Assert.That(AbilityVendorText.Notice(entries), Is.EqualTo(NoGoldNotice));
    }

    [Test]
    public void Notice_AvailableAndInsufficient_IsEmpty()
    {
        var entries = new[] { Entry(AbilityVendorOfferState.Available), Entry(AbilityVendorOfferState.InsufficientCurrency) };

        Assert.That(AbilityVendorText.Notice(entries), Is.Empty);
    }

    [Test]
    public void Notice_AllOwned_IsEmpty()
    {
        var entries = new[] { Entry(AbilityVendorOfferState.AlreadyUnlocked), Entry(AbilityVendorOfferState.AlreadyUnlocked) };

        Assert.That(AbilityVendorText.Notice(entries), Is.Empty);
    }

    [Test]
    public void Notice_OwnedAndInsufficientWithoutAvailable_ExplainsHowToEarnGold()
    {
        var entries = new[] { Entry(AbilityVendorOfferState.AlreadyUnlocked), Entry(AbilityVendorOfferState.InsufficientCurrency) };

        Assert.That(AbilityVendorText.Notice(entries), Is.EqualTo(NoGoldNotice));
    }

    [Test]
    public void Notice_EmptyList_IsEmpty()
    {
        Assert.That(AbilityVendorText.Notice(new List<AbilityVendorEntry>()), Is.Empty);
    }

    [Test]
    public void Notice_Null_IsEmpty()
    {
        Assert.That(AbilityVendorText.Notice(null), Is.Empty);
    }

    private static AbilityVendorEntry Entry(AbilityVendorOfferState state) =>
        new(new AbilityId("trap"), null, 100L, state, true);
}
#endif
