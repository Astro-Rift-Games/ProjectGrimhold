using NUnit.Framework;

public sealed class RaidLossNoticeSlotTests
{
    private static RaidLossNotice Notice(RaidLossNoticeReason reason, string lootId = "bone")
    {
        var reservation = new PendingLoadoutReservation(
            "reservation-1", new[] { new StashItem(new LootId(lootId), 1, 0) });
        Assert.That(RaidLossNotice.TryCreate(reason, reservation, out RaidLossNotice notice, out string error), Is.True, error);
        return notice;
    }

    [Test]
    public void Captured_IsConsumedExactlyOnce()
    {
        var slot = new RaidLossNoticeSlot();
        RaidLossNotice notice = Notice(RaidLossNoticeReason.RaidStartFailed);

        Assert.That(slot.Capture(notice), Is.True);
        Assert.That(slot.HasPending, Is.True);
        Assert.That(slot.TryConsume(out RaidLossNotice consumed), Is.True);
        Assert.That(consumed, Is.SameAs(notice));
        Assert.That(slot.HasPending, Is.False);
        Assert.That(slot.TryConsume(out _), Is.False);
    }

    [Test]
    public void EmptySlot_HasNothingToConsume()
    {
        var slot = new RaidLossNoticeSlot();

        Assert.That(slot.TryConsume(out RaidLossNotice notice), Is.False);
        Assert.That(notice, Is.Null);
    }

    [Test]
    public void SecondCapture_KeepsTheFirstNotice()
    {
        // One departure is one loss: a later shutdown callback must not replace or duplicate it.
        var slot = new RaidLossNoticeSlot();
        RaidLossNotice first = Notice(RaidLossNoticeReason.RaidStartFailed);
        RaidLossNotice second = Notice(RaidLossNoticeReason.ConnectionLost, "sword");

        Assert.That(slot.Capture(first), Is.True);
        Assert.That(slot.Capture(second), Is.False);
        Assert.That(slot.TryConsume(out RaidLossNotice consumed), Is.True);
        Assert.That(consumed, Is.SameAs(first));
    }

    [Test]
    public void NullCapture_IsIgnored()
    {
        var slot = new RaidLossNoticeSlot();

        Assert.That(slot.Capture(null), Is.False);
        Assert.That(slot.HasPending, Is.False);
    }
}
