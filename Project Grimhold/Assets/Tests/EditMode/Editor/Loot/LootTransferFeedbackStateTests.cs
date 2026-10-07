using NUnit.Framework;

public sealed class LootTransferFeedbackStateTests
{
    private static readonly LootId Bone = new("bone");
    private static readonly LootId Coins = new("coins");

    [Test]
    public void ConfirmationBeforeSnapshot_WaitsForIncomingQuantity()
    {
        var state = new LootTransferFeedbackState();
        state.Observe(new[] { new LootEntry(Bone, 2) });
        state.CaptureRequest(Bone);
        state.CompleteRequest(Bone, true);
        Assert.That(state.Observe(new[] { new LootEntry(Bone, 2) }), Is.Empty);
        Assert.That(state.Observe(new[] { new LootEntry(Bone, 3) }), Is.EqualTo(new[] { Bone }));
        Assert.That(state.Observe(new[] { new LootEntry(Bone, 3) }), Is.Empty);
    }

    [Test]
    public void SnapshotBeforeConfirmation_DoesNotLoseRequestBaseline()
    {
        var state = new LootTransferFeedbackState();
        state.Observe(System.Array.Empty<LootEntry>());
        state.CaptureRequest(Bone);
        Assert.That(state.Observe(new[] { new LootEntry(Bone, 1) }), Is.Empty);
        state.CompleteRequest(Bone, true);
        Assert.That(state.Observe(new[] { new LootEntry(Bone, 1) }), Is.EqualTo(new[] { Bone }));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void WithdrawalAndDeposit_OnlyDestinationProducesFeedback(bool deposit)
    {
        var player = new LootTransferFeedbackState();
        var container = new LootTransferFeedbackState();
        var destination = deposit ? container : player;
        var source = deposit ? player : container;
        destination.Observe(System.Array.Empty<LootEntry>());
        source.Observe(new[] { new LootEntry(Bone, 2) });
        destination.CaptureRequest(Bone);
        destination.CompleteRequest(Bone, true);
        Assert.That(source.Observe(new[] { new LootEntry(Bone, 1) }), Is.Empty);
        Assert.That(destination.Observe(new[] { new LootEntry(Bone, 1) }), Is.EqualTo(new[] { Bone }));
    }

    [Test]
    public void TakeAll_PartialFailurePreservesEarlierSuccessAndCoalescesRepeatedItem()
    {
        var state = new LootTransferFeedbackState();
        state.Observe(System.Array.Empty<LootEntry>());
        state.CaptureRequest(Bone);
        state.CompleteRequest(Bone, true);
        state.CaptureRequest(Coins);
        state.CompleteRequest(Coins, false);
        state.CaptureRequest(Bone);
        state.CompleteRequest(Bone, true);
        Assert.That(state.Observe(new[] { new LootEntry(Bone, 2), new LootEntry(Coins, 1) }),
            Is.EqualTo(new[] { Bone }));
    }

    [Test]
    public void TransportRejectionOrUnrelatedConfirmation_NeverBecomesSuccess()
    {
        var state = new LootTransferFeedbackState();
        state.CaptureRequest(Bone);
        state.CompleteRequest(null, false);
        Assert.That(state.Observe(new[] { new LootEntry(Bone, 1) }), Is.Empty);
        state.CaptureRequest(Bone);
        state.CompleteRequest(Coins, true);
        Assert.That(state.Observe(new[] { new LootEntry(Coins, 1), new LootEntry(Bone, 2) }), Is.Empty);
    }

    [Test]
    public void ClosingBinding_DiscardsPendingAndRequestHistory()
    {
        var state = new LootTransferFeedbackState();
        state.CaptureRequest(Bone);
        state.CompleteRequest(Bone, true);
        state.Clear();
        Assert.That(state.Observe(new[] { new LootEntry(Bone, 1) }), Is.Empty);
        state.CompleteRequest(Bone, true);
        Assert.That(state.Observe(new[] { new LootEntry(Bone, 2) }), Is.Empty);
    }
}
