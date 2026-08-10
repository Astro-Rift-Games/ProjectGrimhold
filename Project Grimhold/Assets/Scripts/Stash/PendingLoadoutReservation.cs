using System.Collections.Generic;

/// <summary>
/// Durable loadout snapshot reserved for a future raid admission.
/// </summary>
public sealed class PendingLoadoutReservation
{
    public string ReservationId { get; }
    public List<StashItem> Items { get; }

    public PendingLoadoutReservation(string reservationId, IReadOnlyList<StashItem> items)
    {
        ReservationId = reservationId;
        Items = new List<StashItem>();
        if (items != null)
        {
            Items.AddRange(items);
        }
    }

    public PendingLoadoutReservation Clone() => new(ReservationId, Items);
}
