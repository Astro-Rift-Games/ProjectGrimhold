using System;
using System.Collections.Generic;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;

public class MerchantTradeDraftTests
{
    private static readonly LootId Potion = new LootId("healthpotion");
    private static readonly LootId Bone = new LootId("bone");
    private static readonly LootId Helmet = new LootId("placeholder_helmet");

    private Queue<Guid> _ids;
    private MerchantTradeDraft _draft;

    [SetUp]
    public void Setup()
    {
        _ids = new Queue<Guid>(new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() });
        _draft = new MerchantTradeDraft(() => _ids.Dequeue());
    }

    [Test]
    public void NewDraft_IsEmptyIdleAndWithoutIdentity()
    {
        Assert.That(_draft.IsEmpty, Is.True);
        Assert.That(_draft.IsInFlight, Is.False);
        Assert.That(_draft.RequestId, Is.Null);
        Assert.That(_draft.Purchases, Is.Empty);
        Assert.That(_draft.Sales, Is.Empty);
    }

    [Test]
    public void AddSetRemove_UpdatesLines()
    {
        Assert.That(_draft.TryAddPurchase(Potion, 2), Is.True);
        Assert.That(_draft.IsEmpty, Is.False);
        AssertLines(_draft.Purchases, (Potion, 2));

        Assert.That(_draft.TrySetPurchaseAmount(Potion, 5), Is.True);
        AssertLines(_draft.Purchases, (Potion, 5));

        Assert.That(_draft.TryRemovePurchase(Potion), Is.True);
        Assert.That(_draft.Purchases, Is.Empty);
        Assert.That(_draft.IsEmpty, Is.True);
    }

    [Test]
    public void SetOrRemoveMissingLine_Fails()
    {
        Assert.That(_draft.TrySetSaleAmount(Bone, 1), Is.False);
        Assert.That(_draft.TryRemoveSale(Bone), Is.False);
        Assert.That(_draft.IsEmpty, Is.True);
    }

    [Test]
    public void AddSameLootId_ConsolidatesAndPreservesOrder()
    {
        _draft.TryAddPurchase(Potion, 1);
        _draft.TryAddPurchase(Bone, 1);
        _draft.TryAddPurchase(Helmet, 1);

        Assert.That(_draft.TryAddPurchase(Potion, 3), Is.True);
        Assert.That(_draft.TrySetPurchaseAmount(Bone, 7), Is.True);

        AssertLines(_draft.Purchases, (Potion, 4), (Bone, 7), (Helmet, 1));
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void InvalidAmounts_AreRejectedWithoutChanges(int amount)
    {
        _draft.TryAddSale(Bone, 2);

        Assert.That(_draft.TryAddSale(Bone, amount), Is.False);
        Assert.That(_draft.TrySetSaleAmount(Bone, amount), Is.False);
        Assert.That(_draft.TryAddSale(Potion, amount), Is.False);

        AssertLines(_draft.Sales, (Bone, 2));
    }

    [Test]
    public void InvalidLootId_IsRejected()
    {
        Assert.That(_draft.TryAddPurchase(default, 1), Is.False);
        Assert.That(_draft.TryAddSale(default, 1), Is.False);
        Assert.That(_draft.IsEmpty, Is.True);
    }

    [Test]
    public void AddOverflow_IsRejectedWithoutChanges()
    {
        _draft.TryAddPurchase(Potion, int.MaxValue);

        Assert.That(_draft.TryAddPurchase(Potion, 1), Is.False);
        AssertLines(_draft.Purchases, (Potion, int.MaxValue));
    }

    [Test]
    public void PurchasesAndSales_AreIndependentForSameLootId()
    {
        _draft.TryAddPurchase(Potion, 2);
        _draft.TryAddSale(Potion, 5);

        _draft.TrySetSaleAmount(Potion, 1);
        _draft.TryRemovePurchase(Potion);

        Assert.That(_draft.Purchases, Is.Empty);
        AssertLines(_draft.Sales, (Potion, 1));
    }

    [Test]
    public void Clear_RemovesLinesAndIdentity()
    {
        _draft.TryAddPurchase(Potion, 1);
        _draft.TryAddSale(Bone, 1);
        _draft.TryBeginSubmission(out _);
        _draft.MarkSubmissionRejectedOrFailed();

        Assert.That(_draft.TryClear(), Is.True);

        Assert.That(_draft.IsEmpty, Is.True);
        Assert.That(_draft.RequestId, Is.Null);
    }

    [Test]
    public void InFlight_BlocksEveryMutation()
    {
        _draft.TryAddPurchase(Potion, 1);
        _draft.TryAddSale(Bone, 1);
        _draft.TryBeginSubmission(out Guid requestId);

        Assert.That(_draft.TryAddPurchase(Potion, 1), Is.False);
        Assert.That(_draft.TryAddPurchase(Helmet, 1), Is.False);
        Assert.That(_draft.TrySetPurchaseAmount(Potion, 3), Is.False);
        Assert.That(_draft.TryRemovePurchase(Potion), Is.False);
        Assert.That(_draft.TryAddSale(Bone, 1), Is.False);
        Assert.That(_draft.TrySetSaleAmount(Bone, 3), Is.False);
        Assert.That(_draft.TryRemoveSale(Bone), Is.False);
        Assert.That(_draft.TryClear(), Is.False);
        Assert.That(_draft.TryBeginSubmission(out _), Is.False);

        Assert.That(_draft.IsInFlight, Is.True);
        Assert.That(_draft.RequestId, Is.EqualTo(requestId));
        AssertLines(_draft.Purchases, (Potion, 1));
        AssertLines(_draft.Sales, (Bone, 1));
    }

    [Test]
    public void BeginSubmission_OnEmptyDraft_Fails()
    {
        Assert.That(_draft.TryBeginSubmission(out Guid requestId), Is.False);
        Assert.That(requestId, Is.EqualTo(Guid.Empty));
        Assert.That(_draft.IsInFlight, Is.False);
        Assert.That(_draft.RequestId, Is.Null);
    }

    [Test]
    public void BeginSubmission_CreatesIdentityAndEntersInFlight()
    {
        Guid expected = _ids.Peek();
        _draft.TryAddPurchase(Potion, 1);

        Assert.That(_draft.TryBeginSubmission(out Guid requestId), Is.True);

        Assert.That(requestId, Is.EqualTo(expected));
        Assert.That(_draft.RequestId, Is.EqualTo(expected));
        Assert.That(_draft.IsInFlight, Is.True);
    }

    [Test]
    public void RejectedOrFailed_KeepsContentAndIdentity()
    {
        _draft.TryAddPurchase(Potion, 2);
        _draft.TryAddSale(Bone, 3);
        _draft.TryBeginSubmission(out Guid requestId);

        _draft.MarkSubmissionRejectedOrFailed();

        Assert.That(_draft.IsInFlight, Is.False);
        Assert.That(_draft.RequestId, Is.EqualTo(requestId));
        AssertLines(_draft.Purchases, (Potion, 2));
        AssertLines(_draft.Sales, (Bone, 3));
    }

    [Test]
    public void UnchangedRetry_ReusesIdentity()
    {
        _draft.TryAddPurchase(Potion, 2);
        _draft.TryBeginSubmission(out Guid first);
        _draft.MarkSubmissionRejectedOrFailed();

        Assert.That(_draft.TryBeginSubmission(out Guid retry), Is.True);

        Assert.That(retry, Is.EqualTo(first));
    }

    [Test]
    public void SetToSameAmountAfterFailure_KeepsIdentity()
    {
        _draft.TryAddPurchase(Potion, 2);
        _draft.TryBeginSubmission(out Guid first);
        _draft.MarkSubmissionRejectedOrFailed();

        Assert.That(_draft.TrySetPurchaseAmount(Potion, 2), Is.True);

        Assert.That(_draft.RequestId, Is.EqualTo(first));
    }

    [TestCase("addPurchase")]
    [TestCase("setPurchase")]
    [TestCase("removePurchase")]
    [TestCase("addSale")]
    [TestCase("setSale")]
    [TestCase("removeSale")]
    public void EditAfterFailure_InvalidatesIdentityAndNextSubmissionCreatesNewOne(string edit)
    {
        _draft.TryAddPurchase(Potion, 2);
        _draft.TryAddSale(Bone, 3);
        _draft.TryBeginSubmission(out Guid first);
        _draft.MarkSubmissionRejectedOrFailed();

        bool edited = edit switch
        {
            "addPurchase" => _draft.TryAddPurchase(Helmet, 1),
            "setPurchase" => _draft.TrySetPurchaseAmount(Potion, 4),
            "removePurchase" => _draft.TryRemovePurchase(Potion),
            "addSale" => _draft.TryAddSale(Bone, 1),
            "setSale" => _draft.TrySetSaleAmount(Bone, 1),
            "removeSale" => _draft.TryRemoveSale(Bone),
            _ => false
        };

        Assert.That(edited, Is.True);
        Assert.That(_draft.RequestId, Is.Null);

        Assert.That(_draft.TryBeginSubmission(out Guid next), Is.True);
        Assert.That(next, Is.Not.EqualTo(first));
        Assert.That(_draft.RequestId, Is.EqualTo(next));
    }

    [Test]
    public void InvalidEditAfterFailure_KeepsIdentity()
    {
        _draft.TryAddPurchase(Potion, 2);
        _draft.TryBeginSubmission(out Guid first);
        _draft.MarkSubmissionRejectedOrFailed();

        Assert.That(_draft.TryAddPurchase(Potion, 0), Is.False);
        Assert.That(_draft.TryRemoveSale(Bone), Is.False);

        Assert.That(_draft.RequestId, Is.EqualTo(first));
    }

    [Test]
    public void Succeeded_ResetsDraftCompletely()
    {
        _draft.TryAddPurchase(Potion, 2);
        _draft.TryAddSale(Bone, 3);
        _draft.TryBeginSubmission(out _);

        _draft.MarkSubmissionSucceeded();

        Assert.That(_draft.IsEmpty, Is.True);
        Assert.That(_draft.IsInFlight, Is.False);
        Assert.That(_draft.RequestId, Is.Null);
        Assert.That(_draft.Purchases, Is.Empty);
        Assert.That(_draft.Sales, Is.Empty);
    }

    [Test]
    public void Succeeded_WithoutSubmissionInFlight_IsIgnored()
    {
        _draft.TryAddPurchase(Potion, 2);

        _draft.MarkSubmissionSucceeded();

        AssertLines(_draft.Purchases, (Potion, 2));
    }

    [Test]
    public void ExposedLines_CannotBeMutatedByCallers()
    {
        _draft.TryAddPurchase(Potion, 1);

        Assert.That(_draft.Purchases, Is.Not.InstanceOf<List<MerchantTradeLine>>());
        Assert.That(_draft.Sales, Is.Not.InstanceOf<List<MerchantTradeLine>>());
    }

    private static void AssertLines(IReadOnlyList<MerchantTradeLine> lines, params (LootId lootId, int amount)[] expected)
    {
        Assert.That(lines.Count, Is.EqualTo(expected.Length));
        for (int i = 0; i < expected.Length; i++)
        {
            Assert.That(lines[i].LootId, Is.EqualTo(expected[i].lootId), $"LootId at {i}");
            Assert.That(lines[i].Amount, Is.EqualTo(expected[i].amount), $"Amount at {i}");
        }
    }
}
