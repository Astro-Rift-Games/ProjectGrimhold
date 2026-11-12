using NUnit.Framework;

public sealed class RaidLossNoticeTests
{
    private static readonly LootId Bone = new("bone");
    private static readonly LootId Sword = new("sword");
    private static readonly LootId Helmet = new("helmet");

    private static PendingLoadoutReservation Reservation(
        StashItem[] items,
        PreparedEquipmentLoadout prepared = default) =>
        new("reservation-1", items, prepared);

    [Test]
    public void TryCreate_ListsReservedItemsAndPreparedEquipment()
    {
        PendingLoadoutReservation reservation = Reservation(
            new[] { new StashItem(Bone, 2, 0) },
            new PreparedEquipmentLoadout(Sword, default, helmet: Helmet));

        bool created = RaidLossNotice.TryCreate(
            RaidLossNoticeReason.RaidStartFailed, reservation, out RaidLossNotice notice, out string error);

        Assert.That(created, Is.True, error);
        Assert.That(notice.Reason, Is.EqualTo(RaidLossNoticeReason.RaidStartFailed));
        Assert.That(notice.Lost.Kind, Is.EqualTo(RaidResultsLootKind.Lost));
        Assert.That(notice.Lost.Entries, Is.EquivalentTo(new[]
        {
            new RaidResultsLootEntry(Bone, 2),
            new RaidResultsLootEntry(Sword, 1),
            new RaidResultsLootEntry(Helmet, 1)
        }));
    }

    [Test]
    public void TryCreate_FoldsEquipmentUnitsOfTheSameDefinitionIntoOneEntry()
    {
        // The lost builder rejects duplicate LootIds; two identical swords must show as one line.
        var plain = new EquipmentInstance(null, default(WeaponInstanceModifiers));
        PendingLoadoutReservation reservation = Reservation(
            new[] { new StashItem(Sword, 1, 0, plain), new StashItem(Sword, 1, 1, plain) },
            new PreparedEquipmentLoadout(Sword, default));

        bool created = RaidLossNotice.TryCreate(
            RaidLossNoticeReason.ConnectionLost, reservation, out RaidLossNotice notice, out string error);

        Assert.That(created, Is.True, error);
        Assert.That(notice.Lost.Entries, Is.EqualTo(new[] { new RaidResultsLootEntry(Sword, 3) }));
    }

    [Test]
    public void TryCreate_RejectsAnEmptyReservation_BecauseNothingWasLost()
    {
        bool created = RaidLossNotice.TryCreate(
            RaidLossNoticeReason.ConnectionLost, Reservation(new StashItem[0]), out RaidLossNotice notice, out string error);

        Assert.That(created, Is.False);
        Assert.That(notice, Is.Null);
        Assert.That(error, Is.Not.Empty);
    }

    [Test]
    public void TryCreate_RejectsAMissingReservation()
    {
        bool created = RaidLossNotice.TryCreate(
            RaidLossNoticeReason.ConnectionLost, null, out RaidLossNotice notice, out string error);

        Assert.That(created, Is.False);
        Assert.That(notice, Is.Null);
        Assert.That(error, Is.Not.Empty);
    }
}
