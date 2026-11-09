using System.Threading.Tasks;
using UnityEngine;
using Grimhold.Backend;

/// <summary>
/// Handles backend synchronization for the stash/loadout inventory system.
/// Intended to be called by Presenters before applying local state changes.
/// </summary>
public class RemoteInventoryService : MonoBehaviour, IRemoteInventoryService, ISlotRemoteInventoryService, IRemoteGemInstallService, IRemoteBulkTransferService
{
    public async Task<(bool success, MoveItemResult data, BackendError error)> InstallGemAsync(InstallGemRequest request)
    {
        if (string.IsNullOrEmpty(AuthToken))
            return (false, default, new BackendError { error = "UNAUTHORIZED" });
        await _mutationLock.WaitAsync();
        try
        {
            if (_store == null || _store.InventorySchemaVersion != 3 || _store.RequiresEquipmentHydration)
                return (false, default, new BackendError { error = "INVENTORY_MIGRATION_REQUIRED" });
            var result = await InventoryClient.InstallGemAsync(_backendConfig, AuthToken, request);
            if (!result.success && result.error.error == BackendErrorUtility.Unauthorized)
                ApplicationAuthContext.Instance?.HandleUnauthorizedSession();
            // No revision-only commit: the coordinator applies both authoritative lists atomically.
            return result;
        }
        finally { _mutationLock.Release(); }
    }

    /// <summary>One revision-guarded request for Take All / Leave All; the coordinator applies both lists atomically.</summary>
    public async Task<(bool success, TransferAllResult data, BackendError error)> TransferAllAsync(bool fromStash)
    {
        if (string.IsNullOrEmpty(AuthToken))
            return (false, default, new BackendError { error = "UNAUTHORIZED", message = "Not authenticated" });
        await _mutationLock.WaitAsync();
        try
        {
            if (_store == null || _store.InventorySchemaVersion < 2 || _store.InventorySchemaVersion > 3)
                return (false, default, new BackendError { error = "INVENTORY_MIGRATION_REQUIRED", message = "Inventory migration required" });
            var result = await InventoryClient.TransferAllAsync(_backendConfig, AuthToken, new TransferAllRequest
            {
                direction = fromStash ? "toLoadout" : "toStash", expectedRevision = _store.RemoteRevision
            });
            if (!result.success && result.error.error == BackendErrorUtility.Unauthorized)
                ApplicationAuthContext.Instance?.HandleUnauthorizedSession();
            // No revision-only commit: the coordinator applies the lists and revision in one local commit.
            return result;
        }
        finally { _mutationLock.Release(); }
    }

    public Task<(bool success, BackendError error)> OrderAsync(
        InventoryList list, System.Collections.Generic.IReadOnlyList<LootId> lootIds)
    {
        var slots = new System.Collections.Generic.List<int>();
        var source = list == InventoryList.Stash ? _store?.GetStash() : _store?.GetLoadout();
        if (source == null || lootIds == null) return InvalidSlotRequest();
        foreach (LootId id in lootIds)
        {
            int index = ProfileInventoryRules.FindIndex(source, id);
            if (index < 0) return InvalidSlotRequest();
            slots.Add(source[index].Slot);
        }
        return OrderSlotsAsync(list, slots);
    }

    private static Task<(bool success, BackendError error)> InvalidSlotRequest() =>
        Task.FromResult((false, new BackendError { error = "VALIDATION_FAILED", message = "Invalid inventory slots" }));

    public Task<(bool success, BackendError error)> OrderSlotsAsync(
        InventoryList list, System.Collections.Generic.IReadOnlyList<int> slots)
    {
        if ((list != InventoryList.Stash && list != InventoryList.Loadout) || slots == null) return InvalidSlotRequest();
        var order = new int[slots.Count];
        for (int i = 0; i < order.Length; i++) order[i] = slots[i];
        return MutateSlotsAsync(() => InventoryClient.OrderSlotsAsync(_backendConfig, AuthToken,
            new InventorySlotOrderRequest { list = ListName(list), slotOrder = order, expectedRevision = _store.RemoteRevision }));
    }

    public Task<(bool success, BackendError error)> TransferAsync(int sourceSlot, LootId id, int amount, bool fromStash) =>
        MutateSlotsAsync(() => fromStash
            ? InventoryClient.MoveToLoadoutAsync(_backendConfig, AuthToken, id.Value, amount, _store.RemoteRevision, sourceSlot)
            : InventoryClient.MoveToStashAsync(_backendConfig, AuthToken, id.Value, amount, _store.RemoteRevision, sourceSlot));

    public Task<(bool success, BackendError error)> MoveSlotsAsync(
        InventoryList from, int fromSlot, InventoryList to, int toSlot, LootId id)
    {
        if ((from != InventoryList.Stash && from != InventoryList.Loadout) ||
            (to != InventoryList.Stash && to != InventoryList.Loadout)) return InvalidSlotRequest();
        return MutateSlotsAsync(() => InventoryClient.MoveSlotsAsync(_backendConfig, AuthToken,
            new InventorySlotMoveRequest
            {
                fromList = ListName(from), fromSlot = fromSlot, toList = ListName(to), toSlot = toSlot,
                lootId = id.Value, expectedRevision = _store.RemoteRevision
            }));
    }

    /// <summary>Submits one revision-guarded split without retry; the coordinator applies ownership.</summary>
    public Task<(bool success, BackendError error)> SplitSlotAsync(
        InventoryList list, int sourceSlot, int amount, int destinationSlot, LootId lootId)
    {
        if (list != InventoryList.Stash && list != InventoryList.Loadout) return InvalidSlotRequest();
        return MutateSlotsAsync(() => InventoryClient.SplitSlotAsync(_backendConfig, AuthToken,
            new InventorySlotSplitRequest
            {
                list = ListName(list), sourceSlot = sourceSlot, amount = amount, destinationSlot = destinationSlot,
                lootId = lootId.Value, expectedRevision = _store.RemoteRevision
            }));
    }

    private static string ListName(InventoryList list) => list == InventoryList.Stash ? "stash" : "loadout";

    private async Task<(bool success, BackendError error)> MutateSlotsAsync(
        System.Func<Task<(bool success, MoveItemResult data, BackendError error)>> operation)
    {
        if (string.IsNullOrEmpty(AuthToken))
            return (false, new BackendError { error = "UNAUTHORIZED", message = "Not authenticated" });
        await _mutationLock.WaitAsync();
        try
        {
            if (_store == null || _store.InventorySchemaVersion < 2 || _store.InventorySchemaVersion > 3)
                return (false, new BackendError { error = "INVENTORY_MIGRATION_REQUIRED", message = "Inventory migration required" });
            var (success, data, error) = await operation();
            if (!success && error.error == BackendErrorUtility.Unauthorized)
                ApplicationAuthContext.Instance?.HandleUnauthorizedSession();
            if (success) _store.SetRemoteRevision(data.revision);
            return (success, error);
        }
        finally { _mutationLock.Release(); }
    }

    private int ResolveSourceSlot(LootId id, bool fromStash)
    {
        var source = fromStash ? _store?.GetStash() : _store?.GetLoadout();
        if (source == null) return -1;
        int index = ProfileInventoryRules.FindIndex(source, id);
        return index < 0 ? -1 : source[index].Slot;
    }

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
            if (!success && error.error == BackendErrorUtility.Unauthorized)
            {
                ApplicationAuthContext.Instance?.HandleUnauthorizedSession();
            }

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
    public Task<(bool success, BackendError error)> MoveToLoadoutAsync(LootId lootId, int amount) =>
        TransferAsync(ResolveSourceSlot(lootId, true), lootId, amount, true);

    /// <summary>
    /// Persists a move from loadout to stash.
    /// </summary>
    public Task<(bool success, BackendError error)> MoveToStashAsync(LootId lootId, int amount) =>
        TransferAsync(ResolveSourceSlot(lootId, false), lootId, amount, false);

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
            if (_store == null || _store.RequiresEquipmentHydration)
                return (false, new BackendError { error = "INVENTORY_MIGRATION_REQUIRED", message = "Equipment hydration required" });
            int expectedRevision = _store.RemoteRevision;
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

            if (_store.InventorySchemaVersion == 3)
                request.instanceEquipment = PreparedEquipmentData.FromLoadout(equipment);
            var (success, result, error) = await InventoryClient.UpdatePreparedEquipmentAsync(_backendConfig, AuthToken, request, expectedRevision);
            if (!success && error.error == BackendErrorUtility.Unauthorized)
            {
                ApplicationAuthContext.Instance?.HandleUnauthorizedSession();
            }
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
                if (!success && error.error == BackendErrorUtility.Unauthorized)
                {
                    ApplicationAuthContext.Instance?.HandleUnauthorizedSession();
                }
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
    /// Marks the reservation admitted while preserving it for the participation outcome.
    /// </summary>
    public Task<(bool success, BackendError error)> MarkPendingReservationAdmittedAsync(string reservationId)
    {
        if (string.IsNullOrEmpty(AuthToken))
        {
            Debug.LogError($"[{nameof(RemoteInventoryService)}] MarkPendingReservationAdmittedAsync: Not authenticated.");
            return Task.FromResult((false, new BackendError { error = "UNAUTHORIZED", message = "Not authenticated" }));
        }

        return RemoteIdempotentRetryPolicy.ExecuteWithRetryAsync(async () =>
        {
            await _mutationLock.WaitAsync();
            try
            {
                var (success, result, error) = await InventoryClient.MarkPendingReservationAdmittedAsync(
                    _backendConfig, AuthToken, reservationId);
                if (!success && error.error == BackendErrorUtility.Unauthorized)
                {
                    ApplicationAuthContext.Instance?.HandleUnauthorizedSession();
                }
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
    /// Consumes the active raid reservation as a loss, without rollback.
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
                if (!success && error.error == "EXTRACTION_RESULT_PENDING")
                {
                    Debug.LogWarning($"[{nameof(RemoteInventoryService)}] Loss clear skipped: EXTRACTION_RESULT_PENDING - {error.message}");
                }
                if (!success && error.error == BackendErrorUtility.Unauthorized)
                {
                    ApplicationAuthContext.Instance?.HandleUnauthorizedSession();
                }
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
    /// Rolls back the active raid reservation on the backend, restoring reserved items and equipment.
    /// </summary>
    public Task<(bool success, BackendError error)> RollbackPendingReservationAsync()
    {
        if (string.IsNullOrEmpty(AuthToken))
        {
            Debug.LogError($"[{nameof(RemoteInventoryService)}] RollbackPendingReservationAsync: Not authenticated.");
            return Task.FromResult((false, new BackendError { error = "UNAUTHORIZED", message = "Not authenticated" }));
        }

        return RemoteIdempotentRetryPolicy.ExecuteWithRetryAsync(async () =>
        {
            await _mutationLock.WaitAsync();
            try
            {
                var (success, result, error) = await InventoryClient.RollbackPendingReservationAsync(_backendConfig, AuthToken);
                if (!success && error.error == BackendErrorUtility.Unauthorized)
                {
                    ApplicationAuthContext.Instance?.HandleUnauthorizedSession();
                }
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
        if (!success && error.error == BackendErrorUtility.Unauthorized)
        {
            ApplicationAuthContext.Instance?.HandleUnauthorizedSession();
        }

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
                if (!success && error.error == BackendErrorUtility.Unauthorized)
                {
                    ApplicationAuthContext.Instance?.HandleUnauthorizedSession();
                }

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

        string targetId = receipt.ProfileId.IsValid ? receipt.ProfileId.Value : "";
        string resultId = $"{receipt.RaidId}:{targetId}:{receipt.ResultSequence}";
        string issuedAt = System.DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        var itemsDTO = MapToDTO(items);
        // The published result is ID-free: units carry their modifiers, never an instance ID.
        PreparedEquipmentData eqDTO = PreparedEquipmentData.FromLoadout(preparedEquipment);

        string canonicalMessage = RaidResultCanonicalizer.BuildCanonicalResultString(
            receipt.RaidId,
            resultId,
            targetId,
            itemsDTO,
            eqDTO,
            experienceGranted,
            issuedAt);

        // Under Bounded Player-Host Authority (TASK-223 / SEC-223), the client no longer holds
        // or uses a shared HMAC secret. The backend validates the host's JWT, raid membership,
        // and per-raid loot/experience bounds.
        string hostSignature = string.Empty;

        var request = new PublishExtractionResultRequest
        {
            raidId            = receipt.RaidId,
            resultId          = resultId,
            resultSequence    = receipt.ResultSequence,
            targetCharacterId = targetId,
            items             = itemsDTO,
            preparedEquipment = eqDTO,
            experienceGranted = experienceGranted,
            issuedAt          = issuedAt,
            hostSignature     = hostSignature
        };

        return RemoteIdempotentRetryPolicy.ExecuteWithRetryAsync(async () =>
        {
            var (success, _, error) = await InventoryClient.PublishExtractionResultAsync(_backendConfig, AuthToken, request);
            if (!success && error.error == BackendErrorUtility.Unauthorized)
            {
                ApplicationAuthContext.Instance?.HandleUnauthorizedSession();
            }
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
                amount = items[i].Amount,
                instance = EquipmentInstanceData.FromInstance(items[i].Instance)
            };
        }
        return result;
    }
}
