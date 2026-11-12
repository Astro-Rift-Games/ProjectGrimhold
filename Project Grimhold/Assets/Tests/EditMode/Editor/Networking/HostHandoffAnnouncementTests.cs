#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using NUnit.Framework;

public sealed class HostHandoffAnnouncementTests
{
    [Test]
    public void Format_UsesDecidedTextWithBothNames()
    {
        Assert.AreEqual(
            "Ana salió. Beto es el nuevo Host.",
            HostHandoffAnnouncement.Format("Ana", "Beto"));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Format_UnavailableDepartedName_UsesNeutralHostLabel(string departed)
    {
        Assert.AreEqual(
            "El Host salió. Beto es el nuevo Host.",
            HostHandoffAnnouncement.Format(departed, "Beto"));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Format_UnavailableNewHostName_UsesNeutralPlayerLabel(string newHost)
    {
        Assert.AreEqual(
            "Ana salió. Otro jugador es el nuevo Host.",
            HostHandoffAnnouncement.Format("Ana", newHost));
    }

    [Test]
    public void Format_TrimsNames()
    {
        Assert.AreEqual(
            "Ana salió. Beto es el nuevo Host.",
            HostHandoffAnnouncement.Format("  Ana ", " Beto  "));
    }

    [Test]
    public void Slot_DeliversPublishedNoticeExactlyOnce()
    {
        var slot = new HostHandoffNoticeSlot();
        slot.Publish("Ana salió. Beto es el nuevo Host.");

        Assert.IsTrue(slot.TryConsume(out string first));
        Assert.AreEqual("Ana salió. Beto es el nuevo Host.", first);
        Assert.IsFalse(slot.TryConsume(out string second));
        Assert.IsNull(second);
    }

    [Test]
    public void Slot_EmptyBeforePublishAndIgnoresBlankMessages()
    {
        var slot = new HostHandoffNoticeSlot();
        Assert.IsFalse(slot.TryConsume(out _));

        slot.Publish("  ");
        slot.Publish(null);
        Assert.IsFalse(slot.TryConsume(out _));
    }
}
#endif
