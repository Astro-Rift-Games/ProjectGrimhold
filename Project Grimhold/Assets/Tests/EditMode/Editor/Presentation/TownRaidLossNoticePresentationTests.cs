using NUnit.Framework;

public sealed class TownRaidLossNoticePresentationTests
{
    private static RaidLossNotice Notice(RaidLossNoticeReason reason)
    {
        var reservation = new PendingLoadoutReservation(
            "reservation-1",
            new[] { new StashItem(new LootId("bone"), 3, 0) },
            new PreparedEquipmentLoadout(new LootId("sword"), default));
        Assert.That(RaidLossNotice.TryCreate(reason, reservation, out RaidLossNotice notice, out string error), Is.True, error);
        return notice;
    }

    private static string Name(LootId id) => id.Value == "sword" ? "Espada" : id.Value;

    [Test]
    public void Build_StartFailure_UsesTheSpecifiedSpanishCopy()
    {
        TownRaidLossNoticePresentation view =
            TownRaidLossNoticePresentation.Build(Notice(RaidLossNoticeReason.RaidStartFailed), Name);

        Assert.That(view.Title, Is.EqualTo("Incursión perdida"));
        Assert.That(
            view.Body,
            Is.EqualTo("La incursión no pudo iniciarse. Perdiste el equipamiento que llevabas:"));
        Assert.That(view.ContinueLabel, Is.EqualTo("Continuar"));
    }

    [Test]
    public void Build_ConnectionLost_UsesTheSpecifiedSpanishCopy()
    {
        TownRaidLossNoticePresentation view =
            TownRaidLossNoticePresentation.Build(Notice(RaidLossNoticeReason.ConnectionLost), Name);

        Assert.That(view.Title, Is.EqualTo("Incursión perdida"));
        Assert.That(
            view.Body,
            Is.EqualTo("Se perdió la conexión con la incursión. Perdiste el equipamiento que llevabas:"));
    }

    [Test]
    public void Build_ListsEveryLostItemWithItsDisplayNameAndAmount()
    {
        TownRaidLossNoticePresentation view =
            TownRaidLossNoticePresentation.Build(Notice(RaidLossNoticeReason.ConnectionLost), Name);

        Assert.That(view.Lines, Is.EqualTo(new[] { "bone x3", "Espada" }));
    }

    [Test]
    public void Build_FallsBackToTheLootId_WhenTheNameIsUnknown()
    {
        TownRaidLossNoticePresentation view =
            TownRaidLossNoticePresentation.Build(Notice(RaidLossNoticeReason.ConnectionLost), _ => null);

        Assert.That(view.Lines, Is.EqualTo(new[] { "bone x3", "sword" }));
    }
}
