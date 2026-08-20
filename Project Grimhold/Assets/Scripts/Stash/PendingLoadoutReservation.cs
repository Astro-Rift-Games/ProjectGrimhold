using System.Collections.Generic;

/// <summary>
/// Durable loadout snapshot reserved for a future raid admission.
/// </summary>
public sealed class PendingLoadoutReservation
{
    public string ReservationId { get; }
    private readonly List<StashItem> _items;
    public IReadOnlyList<StashItem> Items => _items;
    public PreparedEquipmentLoadout PreparedEquipment { get; }

    public PendingLoadoutReservation(
        string reservationId,
        IReadOnlyList<StashItem> items,
        PreparedEquipmentLoadout preparedEquipment = default)
    {
        ReservationId = reservationId;
        PreparedEquipment = preparedEquipment;
        _items = new List<StashItem>();
        if (items != null)
        {
            _items.AddRange(items);
        }
    }

    public PendingLoadoutReservation Clone() => new(ReservationId, Items, PreparedEquipment);
}
