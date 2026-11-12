using NUnit.Framework;

public sealed class SoloAbandonFailureNoticeTests
{
    [Test]
    public void FirstObservation_OnlyBaselines_EvenWhenFailuresAlreadyHappened()
    {
        // Rebinding after a reconnect or Host Migration must not replay an old failure.
        var notice = new SoloAbandonFailureNotice();

        Assert.That(notice.Observe(3), Is.False);
    }

    [Test]
    public void NewFailure_IsReportedExactlyOnce()
    {
        var notice = new SoloAbandonFailureNotice();
        notice.Observe(0);

        Assert.That(notice.Observe(1), Is.True);
        Assert.That(notice.Observe(1), Is.False);
    }

    [Test]
    public void EachSubsequentFailure_IsReportedAgain()
    {
        var notice = new SoloAbandonFailureNotice();
        notice.Observe(0);
        notice.Observe(1);

        Assert.That(notice.Observe(2), Is.True);
    }

    [Test]
    public void UnchangedSequence_NeverReports()
    {
        var notice = new SoloAbandonFailureNotice();
        notice.Observe(5);

        Assert.That(notice.Observe(5), Is.False);
        Assert.That(notice.Observe(5), Is.False);
    }
}
