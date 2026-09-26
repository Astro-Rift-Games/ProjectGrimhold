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
    private LocalProfileStore _store;
    private readonly System.Threading.SemaphoreSlim _mutationLock = new System.Threading.SemaphoreSlim(1, 1);

    // Use property getter for token to avoid caching it when not authenticated
    private string AuthToken => ApplicationAuthContext.Instance?.Token;

    public void Initialize(LocalProfilePersistenceConfiguration localConfig, LocalProfileStore store)
    {
        _store = store;
        // Fetch configuration from LoginFlowController if available, otherwise try Resources
        if (LoginFlowController.Instance != null && LoginFlowController.Instance.Config != null)
        {
            _backendConfig = LoginFlowController.Instance.Config;
            Debug.Log($"[{nameof(RemoteInventoryService)}] Loaded BackendConfiguration from LoginFlowController: {_backendConfig.BaseUrl}");
        }
        else
        {
            _backendConfig = Resources.Load<BackendConfiguration>("BackendConfiguration");
            if (_backendConfig == null)
            {
                _backendConfig = ScriptableObject.CreateInstance<BackendConfiguration>();
                Debug.LogWarning($"[{nameof(RemoteInventoryService)}] No BackendConfiguration found. Using defaults.");
            }
        }
    }

    /// <summary>
    /// Persists character progression and attribute allocations.
    /// </summary>
    public async Task<(bool success, CharacterAttributesData data, BackendError error)> CommitProgressionAsync(string attributeName)
    {
        if (string.IsNullOrEmpty(AuthToken))
        {
            Debug.LogError($"[{nameof(RemoteInventoryService)}] CommitProgressionAsync: Not authenticated.");
            return (false, default, new BackendError { error = "UNAUTHORIZED", message = "Not authenticated" });
        }

        await _mutationLock.WaitAsync();
        try
        {
            var request = new CommitProgressionRequest
            {
                attribute = attributeName,
                expectedRevision = _store?.RemoteRevision ?? 0
            };

            var (success, result, error) = await ProgressionClient.CommitProgressionAsync(_backendConfig, AuthToken, request);
            
            if (success && _store != null)
            {
                _store.SetRemoteRevision(result.revision);
            }
            
            return (success, result.characterAttributes, error);
        }
        finally
        {
            _mutationLock.Release();
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

        await _mutationLock.WaitAsync();
        try
        {
            int expectedRevision = _store?.RemoteRevision ?? 0;
            var (success, result, error) = await InventoryClient.MoveToLoadoutAsync(_backendConfig, AuthToken, lootId.Value, amount, expectedRevision);
            if (success && _store != null)
            {
                _store.SetRemoteRevision(result.revision);
            }
            return (success, error);
        }
        finally
        {
            _mutationLock.Release();
        }
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

        await _mutationLock.WaitAsync();
        try
        {
            int expectedRevision = _store?.RemoteRevision ?? 0;
            var (success, result, error) = await InventoryClient.MoveToStashAsync(_backendConfig, AuthToken, lootId.Value, amount, expectedRevision);
            if (success && _store != null)
            {
                _store.SetRemoteRevision(result.revision);
            }
            return (success, error);
        }
        finally
        {
            _mutationLock.Release();
        }
    }

    /// <summary>
    /// Persists the updated prepared equipment to the backend.
    /// </summary>
    public async Task<(bool success, BackendError error)> UpdatePreparedEquipmentAsync(PreparedEquipmentLoadout equipment)
    {
        if (string.IsNullOrEmpty(AuthToken))
        {
            Debug.LogError($"[{nameof(RemoteInventoryService)}] UpdatePreparedEquipmentAsync: Not authenticated.");
            return (false, new BackendError { error = "UNAUTHORIZED", message = "Not authenticated" });
        }

        await _mutationLock.WaitAsync();
        try
        {
            int expectedRevision = _store?.RemoteRevision ?? 0;
            var request = new UpdatePreparedEquipmentRequest
            {
                weaponSlot1 = equipment.WeaponSetAMainHand.IsValid ? equipment.WeaponSetAMainHand.Value : "",
                weaponSlot2 = equipment.WeaponSetBMainHand.IsValid ? equipment.WeaponSetBMainHand.Value : "",
                helmet      = equipment.Helmet.IsValid      ? equipment.Helmet.Value      : "",
                armor       = equipment.Armor.IsValid       ? equipment.Armor.Value       : "",
                gloves      = equipment.Gloves.IsValid      ? equipment.Gloves.Value      : "",
                boots       = equipment.Boots.IsValid       ? equipment.Boots.Value       : "",
                offHand1    = equipment.WeaponSetAOffHand.IsValid ? equipment.WeaponSetAOffHand.Value : "",
                offHand2    = equipment.WeaponSetBOffHand.IsValid ? equipment.WeaponSetBOffHand.Value : ""
            };

            var (success, result, error) = await InventoryClient.UpdatePreparedEquipmentAsync(_backendConfig, AuthToken, request, expectedRevision);
            if (success && _store != null)
            {
                _store.SetRemoteRevision(result.revision);
            }
            return (success, error);
        }
        finally
        {
            _mutationLock.Release();
        }
    }

    /// <summary>
    /// Persists the active raid reservation.
    /// </summary>
    public Task<(bool success, BackendError error)> SavePendingReservationAsync(PendingLoadoutReservation reservation)
    {
        if (string.IsNullOrEmpty(AuthToken))
        {
            Debug.LogError($"[{nameof(RemoteInventoryService)}] SavePendingReservationAsync: Not authenticated.");
            return Task.FromResult((false, new BackendError { error = "UNAUTHORIZED", message = "Not authenticated" }));
        }

        return RemoteIdempotentRetryPolicy.ExecuteWithRetryAsync(async () =>
        {
            await _mutationLock.WaitAsync();
            try
            {
                var request = new SaveReservationRequest
                {
                    reservationId = reservation.ReservationId
                };

                var (success, result, error) = await InventoryClient.SavePendingReservationAsync(_backendConfig, AuthToken, request);
                if (success && _store != null)
                {
                    _store.SetRemoteRevision(result.revision);
                }
                return (success, error);
            }
            finally
            {
                _mutationLock.Release();
            }
        });
    }

    /// <summary>
    /// Clears the active raid reservation.
    /// </summary>
    public Task<(bool success, BackendError error)> ClearPendingReservationAsync()
    {
        if (string.IsNullOrEmpty(AuthToken))
        {
            Debug.LogError($"[{nameof(RemoteInventoryService)}] ClearPendingReservationAsync: Not authenticated.");
            return Task.FromResult((false, new BackendError { error = "UNAUTHORIZED", message = "Not authenticated" }));
        }

        return RemoteIdempotentRetryPolicy.ExecuteWithRetryAsync(async () =>
        {
            await _mutationLock.WaitAsync();
            try
            {
                var (success, result, error) = await InventoryClient.ClearPendingReservationAsync(_backendConfig, AuthToken);
                if (success && _store != null)
                {
                    _store.SetRemoteRevision(result.revision);
                }
                return (success, error);
            }
            finally
            {
                _mutationLock.Release();
            }
        });
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

    public Task<(bool success, CommitExtractionUnifiedResult result, BackendError error)> CommitExtractionUnifiedAsync(
        ExtractionReceipt receipt,
        System.Collections.Generic.IReadOnlyList<StashItem> items,
        PreparedEquipmentLoadout preparedEquipment,
        long consolidatedExperience,
        int resultingLevel)
    {
        if (string.IsNullOrEmpty(AuthToken))
        {
            Debug.LogError($"[{nameof(RemoteInventoryService)}] CommitExtractionUnifiedAsync: Not authenticated.");
            return Task.FromResult<(bool, CommitExtractionUnifiedResult, BackendError)>((false, default, new BackendError { error = "UNAUTHORIZED", message = "Not authenticated" }));
        }

        return RemoteIdempotentRetryPolicy.ExecuteWithRetryAsync(async () =>
        {
            await _mutationLock.WaitAsync();
            try
            {
                var request = new CommitExtractionUnifiedRequest
                {
                    raidId         = receipt.RaidId,
                    resultSequence = receipt.ResultSequence,
                    items          = MapToDTO(items),
                    preparedEquipment = new PreparedEquipmentData
                    {
                        weaponSlot1 = preparedEquipment.WeaponSetAMainHand.IsValid ? preparedEquipment.WeaponSetAMainHand.Value : null,
                        weaponSlot2 = preparedEquipment.WeaponSetBMainHand.IsValid ? preparedEquipment.WeaponSetBMainHand.Value : null,
                        helmet = preparedEquipment.Helmet.IsValid ? preparedEquipment.Helmet.Value : null,
                        armor = preparedEquipment.Armor.IsValid ? preparedEquipment.Armor.Value : null,
                        gloves = preparedEquipment.Gloves.IsValid ? preparedEquipment.Gloves.Value : null,
                        boots = preparedEquipment.Boots.IsValid ? preparedEquipment.Boots.Value : null,
                        offHand1 = preparedEquipment.WeaponSetAOffHand.IsValid ? preparedEquipment.WeaponSetAOffHand.Value : null,
                        offHand2 = preparedEquipment.WeaponSetBOffHand.IsValid ? preparedEquipment.WeaponSetBOffHand.Value : null
                    },
                    progression    = new ExtractionProgressionData 
                    {
                        consolidatedExperience = consolidatedExperience,
                        resultingLevel = resultingLevel
                    }
                };

                var (success, result, error) = await InventoryClient.CommitExtractionUnifiedAsync(_backendConfig, AuthToken, request);

                if (success && _store != null)
                {
                    _store.SetRemoteRevision(result.revision);
                    if (result.alreadySecured)
                    {
                        Debug.Log($"[{nameof(RemoteInventoryService)}] Unified extraction already secured on backend " +
                                  $"(raidId={receipt.RaidId}, seq={receipt.ResultSequence}). No action needed.");
                    }
                }

                return (success, result, error);
            }
            finally
            {
                _mutationLock.Release();
            }
        });
    }

    public Task<(bool success, BackendError error)> PublishExtractionResultAsync(
        ExtractionReceipt receipt,
        System.Collections.Generic.IReadOnlyList<StashItem> items,
        PreparedEquipmentLoadout preparedEquipment,
        long experienceGranted)
    {
        if (string.IsNullOrEmpty(AuthToken))
        {
            Debug.LogError($"[{nameof(RemoteInventoryService)}] PublishExtractionResultAsync: Not authenticated.");
            return Task.FromResult((false, new BackendError { error = "UNAUTHORIZED", message = "Not authenticated" }));
        }

        string messageToSign = $"{receipt.RaidId}:{receipt.ResultSequence}";
        string hostSignature = "";

        using (var hmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(_backendConfig.WebhookSecret)))
        {
            byte[] hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(messageToSign));
            hostSignature = System.BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        var request = new PublishExtractionResultRequest
        {
            raidId         = receipt.RaidId,
            resultSequence = receipt.ResultSequence,
            items          = MapToDTO(items),
            preparedEquipment = new PreparedEquipmentData
            {
                weaponSlot1 = preparedEquipment.WeaponSetAMainHand.IsValid ? preparedEquipment.WeaponSetAMainHand.Value : null,
                weaponSlot2 = preparedEquipment.WeaponSetBMainHand.IsValid ? preparedEquipment.WeaponSetBMainHand.Value : null,
                helmet = preparedEquipment.Helmet.IsValid ? preparedEquipment.Helmet.Value : null,
                armor = preparedEquipment.Armor.IsValid ? preparedEquipment.Armor.Value : null,
                gloves = preparedEquipment.Gloves.IsValid ? preparedEquipment.Gloves.Value : null,
                boots = preparedEquipment.Boots.IsValid ? preparedEquipment.Boots.Value : null,
                offHand1 = preparedEquipment.WeaponSetAOffHand.IsValid ? preparedEquipment.WeaponSetAOffHand.Value : null,
                offHand2 = preparedEquipment.WeaponSetBOffHand.IsValid ? preparedEquipment.WeaponSetBOffHand.Value : null
            },
            experienceGranted = experienceGranted,
            hostSignature = hostSignature
        };

        return RemoteIdempotentRetryPolicy.ExecuteWithRetryAsync(async () =>
        {
            var (success, _, error) = await InventoryClient.PublishExtractionResultAsync(_backendConfig, AuthToken, request);
            return (success, error);
        });
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
