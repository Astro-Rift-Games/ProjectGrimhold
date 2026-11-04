#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Collections.Generic;

/// <summary>Existing test fixtures previously seeded aggregate entries; seed physical lists explicitly.</summary>
public static class PhysicalSlotTestSeed
{
    public static void AssignUnplaced(LocalProfileSnapshot snapshot)
    {
        Assign(snapshot.Stash);
        Assign(snapshot.Loadout);
        if (snapshot.PendingReservation != null)
        {
            var items = new List<StashItem>(snapshot.PendingReservation.Items);
            Assign(items);
            snapshot.PendingReservation = new PendingLoadoutReservation(snapshot.PendingReservation.ReservationId,
                items, snapshot.PendingReservation.PreparedEquipment);
        }
    }

    private static void Assign(List<StashItem> items)
    {
        for (int index = 0; index < items.Count; index++)
            if (items[index].Slot < 0) items[index] = items[index].AtSlot(index);
    }
}
#endif
