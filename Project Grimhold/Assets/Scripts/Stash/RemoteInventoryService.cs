using System.Threading.Tasks;
using UnityEngine;
using Grimhold.Backend;

/// <summary>
/// Handles backend synchronization for the stash/loadout inventory system.
/// Intended to be called by Presenters before applying local state changes.
/// </summary>
public class RemoteInventoryService : MonoBehaviour
{
    private BackendConfiguration _backendConfig;

    // Use property getter for token to avoid caching it when not authenticated
    private string AuthToken => ApplicationAuthContext.Instance?.Token;

    public void Initialize(LocalProfilePersistenceConfiguration localConfig, LocalProfileStore store)
    {
        // Try to load backend config. In a real app this might be injected.
        _backendConfig = Resources.Load<BackendConfiguration>("BackendConfiguration");
        if (_backendConfig == null)
        {
            _backendConfig = ScriptableObject.CreateInstance<BackendConfiguration>();
            Debug.LogWarning($"[{nameof(RemoteInventoryService)}] No BackendConfiguration found. Using defaults.");
        }
    }

    /// <summary>
    /// Persists a move from stash to loadout.
    /// </summary>
    public async Task<(bool success, BackendError error)> MoveToLoadoutAsync(LootId lootId, int amount)
    {
        if (string.IsNullOrEmpty(AuthToken))
        {
            Debug.LogError($"[{nameof(RemoteInventoryService)}] MoveToLoadoutAsync: Not authenticated.");
            return (false, new BackendError { error = "UNAUTHORIZED", message = "Not authenticated" });
        }

        var (success, _, error) = await InventoryClient.MoveToLoadoutAsync(_backendConfig, AuthToken, lootId.Value, amount);
        return (success, error);
    }

    /// <summary>
    /// Persists a move from loadout to stash.
    /// </summary>
    public async Task<(bool success, BackendError error)> MoveToStashAsync(LootId lootId, int amount)
    {
        if (string.IsNullOrEmpty(AuthToken))
        {
            Debug.LogError($"[{nameof(RemoteInventoryService)}] MoveToStashAsync: Not authenticated.");
            return (false, new BackendError { error = "UNAUTHORIZED", message = "Not authenticated" });
        }

        var (success, _, error) = await InventoryClient.MoveToStashAsync(_backendConfig, AuthToken, lootId.Value, amount);
        return (success, error);
    }

    /// <summary>
    /// Persists the full prepared equipment loadout.
    /// </summary>
    public async Task<(bool success, BackendError error)> UpdatePreparedEquipmentAsync(PreparedEquipmentLoadout equipment)
    {
        if (string.IsNullOrEmpty(AuthToken))
        {
            Debug.LogError($"[{nameof(RemoteInventoryService)}] UpdatePreparedEquipmentAsync: Not authenticated.");
            return (false, new BackendError { error = "UNAUTHORIZED", message = "Not authenticated" });
        }

        var request = new UpdatePreparedEquipmentRequest
        {
            weaponSlot1 = equipment.WeaponSlot1.IsValid ? equipment.WeaponSlot1.Value : "",
            weaponSlot2 = equipment.WeaponSlot2.IsValid ? equipment.WeaponSlot2.Value : "",
            helmet      = equipment.Helmet.IsValid      ? equipment.Helmet.Value      : "",
            armor       = equipment.Armor.IsValid       ? equipment.Armor.Value       : "",
            gloves      = equipment.Gloves.IsValid      ? equipment.Gloves.Value      : "",
            boots       = equipment.Boots.IsValid       ? equipment.Boots.Value       : ""
        };

        var (success, _, error) = await InventoryClient.UpdatePreparedEquipmentAsync(_backendConfig, AuthToken, request);
        return (success, error);
    }

    /// <summary>
    /// Persists the active raid reservation.
    /// </summary>
    public async Task<(bool success, BackendError error)> SavePendingReservationAsync(PendingLoadoutReservation reservation)
    {
        if (string.IsNullOrEmpty(AuthToken))
        {
            Debug.LogError($"[{nameof(RemoteInventoryService)}] SavePendingReservationAsync: Not authenticated.");
            return (false, new BackendError { error = "UNAUTHORIZED", message = "Not authenticated" });
        }

        var request = new SaveReservationRequest
        {
            reservationId = reservation.ReservationId,
            items = MapToDTO(reservation.Items),
            preparedEquipment = new PreparedEquipmentData
            {
                weaponSlot1 = reservation.PreparedEquipment.WeaponSlot1.IsValid ? reservation.PreparedEquipment.WeaponSlot1.Value : "",
                weaponSlot2 = reservation.PreparedEquipment.WeaponSlot2.IsValid ? reservation.PreparedEquipment.WeaponSlot2.Value : "",
                helmet      = reservation.PreparedEquipment.Helmet.IsValid      ? reservation.PreparedEquipment.Helmet.Value      : "",
                armor       = reservation.PreparedEquipment.Armor.IsValid       ? reservation.PreparedEquipment.Armor.Value       : "",
                gloves      = reservation.PreparedEquipment.Gloves.IsValid      ? reservation.PreparedEquipment.Gloves.Value      : "",
                boots       = reservation.PreparedEquipment.Boots.IsValid       ? reservation.PreparedEquipment.Boots.Value       : ""
            }
        };

        var (success, _, error) = await InventoryClient.SavePendingReservationAsync(_backendConfig, AuthToken, request);
        return (success, error);
    }

    /// <summary>
    /// Clears the active raid reservation.
    /// </summary>
    public async Task<(bool success, BackendError error)> ClearPendingReservationAsync()
    {
        if (string.IsNullOrEmpty(AuthToken))
        {
            Debug.LogError($"[{nameof(RemoteInventoryService)}] ClearPendingReservationAsync: Not authenticated.");
            return (false, new BackendError { error = "UNAUTHORIZED", message = "Not authenticated" });
        }

        return await InventoryClient.ClearPendingReservationAsync(_backendConfig, AuthToken);
    }

    /// <summary>
    /// Persists the loot items from a successful raid extraction to the backend loadout.
    /// Idempotent: safe to call multiple times with the same receipt.
    /// </summary>
    public async Task<(bool success, BackendError error)> CommitExtractionAsync(
        ExtractionReceipt receipt,
        System.Collections.Generic.IReadOnlyList<StashItem> items)
    {
        if (string.IsNullOrEmpty(AuthToken))
        {
            Debug.LogError($"[{nameof(RemoteInventoryService)}] CommitExtractionAsync: Not authenticated.");
            return (false, new BackendError { error = "UNAUTHORIZED", message = "Not authenticated" });
        }

        var request = new CommitExtractionRequest
        {
            raidId         = receipt.RaidId,
            resultSequence = receipt.ResultSequence,
            items          = MapToDTO(items)
        };

        var (success, result, error) = await InventoryClient.CommitExtractionAsync(_backendConfig, AuthToken, request);

        if (success && result.alreadySecured)
        {
            Debug.Log($"[{nameof(RemoteInventoryService)}] Extraction already secured on backend " +
                      $"(raidId={receipt.RaidId}, seq={receipt.ResultSequence}). No action needed.");
        }

        return (success, error);
    }

    public async Task<(bool success, BackendError error)> CommitExtractionUnifiedAsync(
        ExtractionReceipt receipt,
        System.Collections.Generic.IReadOnlyList<StashItem> items,
        long consolidatedExperience,
        int resultingLevel)
    {
        if (string.IsNullOrEmpty(AuthToken))
        {
            Debug.LogError($"[{nameof(RemoteInventoryService)}] CommitExtractionUnifiedAsync: Not authenticated.");
            return (false, new BackendError { error = "UNAUTHORIZED", message = "Not authenticated" });
        }

        var request = new CommitExtractionUnifiedRequest
        {
            raidId         = receipt.RaidId,
            resultSequence = receipt.ResultSequence,
            items          = MapToDTO(items),
            progression    = new ExtractionProgressionData
            {
                consolidatedExperience = consolidatedExperience,
                resultingLevel = resultingLevel
            }
        };

        var (success, result, error) = await InventoryClient.CommitExtractionUnifiedAsync(_backendConfig, AuthToken, request);

        if (success && result.alreadySecured)
        {
            Debug.Log($"[{nameof(RemoteInventoryService)}] Unified extraction already secured on backend " +
                      $"(raidId={receipt.RaidId}, seq={receipt.ResultSequence}). No action needed.");
        }

        return (success, error);
    }

    private InventoryItemData[] MapToDTO(System.Collections.Generic.IReadOnlyList<StashItem> items)
    {
        var result = new InventoryItemData[items.Count];
        for (int i = 0; i < items.Count; i++)
        {
            result[i] = new InventoryItemData
            {
                lootId = items[i].LootId.Value,
                amount = items[i].Amount
            };
        }
        return result;
    }
}
