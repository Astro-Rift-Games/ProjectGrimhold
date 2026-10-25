using System.Threading.Tasks;
using Grimhold.Backend;

/// <summary>
/// The backend inventory operations the Stash screen drives. It exists so Stash action logic can
/// be tested without HTTP; <see cref="RemoteInventoryService"/> is the production implementation.
/// </summary>
public interface IRemoteInventoryService
{
    Task<(bool success, BackendError error)> OrderAsync(InventoryList list, System.Collections.Generic.IReadOnlyList<LootId> lootIds);

    /// <summary>Persists a move of <paramref name="amount"/> units from the Stash to the Loadout.</summary>
    Task<(bool success, BackendError error)> MoveToLoadoutAsync(LootId lootId, int amount);

    /// <summary>Persists a move of <paramref name="amount"/> units from the Loadout to the Stash.</summary>
    Task<(bool success, BackendError error)> MoveToStashAsync(LootId lootId, int amount);

    /// <summary>Persists the prepared Equipment assignments.</summary>
    Task<(bool success, BackendError error)> UpdatePreparedEquipmentAsync(PreparedEquipmentLoadout equipment);
}
