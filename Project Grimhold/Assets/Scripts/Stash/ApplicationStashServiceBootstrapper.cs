using UnityEngine;
using Grimhold.Backend;

/// <summary>
/// Creates one process-local stash context that survives scene and NetworkRunner transitions.
/// Durable profile state will be supplied and confirmed by the authenticated backend flow.
///
/// Initialization is split into two phases:
/// 1. BeforeSceneLoad creates the DontDestroyOnLoad GameObject.
/// 2. InitializeWithProfile is called after login supplies the backend CharacterId.
/// </summary>
public static class ApplicationStashServiceBootstrapper
{
    private const string ContextName = "ApplicationStashContext";

    private static ApplicationStashContext _context;
    private static LocalProfilePersistenceConfiguration _configuration;
    private static ProfileId _initializedProfileId;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        _context = null;
        _configuration = null;
        _initializedProfileId = default;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Initialize()
    {
        if (Object.FindAnyObjectByType<ApplicationStashContext>() != null)
        {
            return;
        }

        _configuration = Resources.Load<LocalProfilePersistenceConfiguration>("LocalProfilePersistenceConfiguration");
        if (_configuration == null ||
            _configuration.LootCatalog == null ||
            _configuration.AbilityCatalog == null)
        {
            Debug.LogError($"[{nameof(ApplicationStashServiceBootstrapper)}] Local profile configuration, loot catalog, or ability catalog is missing.");
            return;
        }

        var contextObject = new GameObject(ContextName);
        _context = contextObject.AddComponent<ApplicationStashContext>();
        Object.DontDestroyOnLoad(contextObject);

        // Attempt immediate initialization if a valid ProfileId is already available
        // (e.g. during an Editor domain reload mid-session).
        var profileId = LocalProfileProvider.GetOrCreateLocalProfile();
        if (profileId.IsValid)
        {
            InitializeStore(profileId);
        }
        else
        {
            Debug.Log($"[{nameof(ApplicationStashServiceBootstrapper)}] Context created. Waiting for login to provide a ProfileId.");
        }
    }

    /// <summary>
    /// Initializes the stash store for the given profile. Called by LoginFlowController
    /// after a successful login. Safe to call only once per profile per session.
    /// </summary>
    public static bool InitializeWithProfile(ProfileId profileId, Grimhold.Backend.InventoryData? inventoryData = null, Grimhold.Backend.ProgressionData? progressionData = null, bool allowCorruptCacheRecovery = false)
    {
        if (!profileId.IsValid)
        {
            Debug.LogError($"[{nameof(ApplicationStashServiceBootstrapper)}] InitializeWithProfile called with an invalid ProfileId.");
            return false;
        }

        if (_initializedProfileId == profileId)
        {
            Debug.Log($"[{nameof(ApplicationStashServiceBootstrapper)}] Store already initialized for ProfileId {profileId.Value}. Skipping.");
            return _context != null && _context.IsAvailable;
        }

        if (_context == null)
        {
            Debug.LogError($"[{nameof(ApplicationStashServiceBootstrapper)}] Context not created yet. Was BeforeSceneLoad suppressed?");
            return false;
        }

        if (_configuration == null ||
            _configuration.LootCatalog == null ||
            _configuration.AbilityCatalog == null)
        {
            Debug.LogError($"[{nameof(ApplicationStashServiceBootstrapper)}] Configuration unavailable during deferred initialization.");
            return false;
        }

        return InitializeStore(profileId, inventoryData, progressionData, allowCorruptCacheRecovery);
    }

    private static bool InitializeStore(ProfileId profileId, Grimhold.Backend.InventoryData? inventoryData = null, Grimhold.Backend.ProgressionData? progressionData = null, bool allowCorruptCacheRecovery = false)
    {
        var contextObject = _context.gameObject;

        var fileStore = new LocalProfileFileStore();
        var repository = new LocalProfileRepository(
            fileStore,
            Application.persistentDataPath,
            _configuration.AbilityCatalog);
        if (!PrepareRepositoryForLogin(repository, profileId, _configuration.LootCatalog,
                _configuration.AbilityCatalog, inventoryData, progressionData, allowCorruptCacheRecovery))
        {
            return false;
        }

        var store = new LocalProfileStore(
            repository,
            profileId,
            _configuration.LootCatalog,
            _configuration.RecoveryWeaponLootId,
            _configuration.MissionCatalog,
            _configuration.AbilityCatalog,
            _configuration.StashCapacity);
        bool? orphanRollback = null;
        if (store.PendingExtractionCommit != null)
        {
            Debug.LogWarning($"[{nameof(ApplicationStashServiceBootstrapper)}] Retrying pending extraction commit from a previous session crash.");
            
            // Since we know the extraction succeeded locally, the reservation should not be rolled back.
            // We clear it locally and retry the extraction commit to the backend.
            if (store.PendingReservation != null)
            {
                store.TryConfirmLoadoutReservation(store.PendingReservation.ReservationId);
            }
            
            var authToken = ApplicationAuthContext.Instance?.Token;
            if (!string.IsNullOrEmpty(authToken) && _configuration != null)
            {
                var backendConfig = ApplicationAuthContext.Instance?.BackendConfig ?? Resources.Load<BackendConfiguration>("BackendConfiguration");
                if (backendConfig != null)
                {
                    _ = RetryPendingExtractionAsync(store, backendConfig, authToken);
                }
            }
        }
        else if (store.PendingReservation != null)
        {
            bool admitted = inventoryData.HasValue &&
                !string.IsNullOrEmpty(inventoryData.Value.pendingReservation.admittedAt);
            Debug.LogWarning(admitted
                ? $"[{nameof(ApplicationStashServiceBootstrapper)}] Discarding admitted orphaned loadout reservation as a loss."
                : $"[{nameof(ApplicationStashServiceBootstrapper)}] Rolling back orphaned loadout reservation from a previous session crash.");
            StashOperationResult recovery = TryResolveOrphanedReservation(store, admitted);
            // Login stays available, as before: the backend is left untouched and the next
            // hydration retries the orphan.
            if (recovery != StashOperationResult.Success)
                Debug.LogError($"[{nameof(ApplicationStashServiceBootstrapper)}] Orphan reservation recovery failed: {recovery}. Backend cleanup skipped.");
            else
                orphanRollback = !admitted;
        }

        var stashService = contextObject.AddComponent<InMemoryPlayerStashService>();
        var loadoutService = contextObject.AddComponent<InMemoryPlayerLoadoutService>();
        var currencyService = contextObject.AddComponent<InMemoryPlayerCurrencyService>();
        var shopTransactionService = contextObject.AddComponent<RemoteShopTransactionService>();
        
        // Add RemoteInventoryService to handle backend operations
        var remoteInventoryService = contextObject.AddComponent<RemoteInventoryService>();
        remoteInventoryService.Initialize(_configuration, store);
        if (orphanRollback.HasValue)
        {
            _ = ClearOrphanedReservationAsync(remoteInventoryService, orphanRollback.Value);
        }

        // Add ProfileReconciliationService to handle automatic retry and hydration on conflicts
        var reconciliationService = contextObject.AddComponent<ProfileReconciliationService>();
        reconciliationService.Initialize(_configuration, store, _context.BuybackLedger);

        stashService.Initialize(store);
        loadoutService.Initialize(store);
        currencyService.Initialize(store);
        var abilityPurchaseService = contextObject.AddComponent<RemoteAbilityPurchaseService>();
        abilityPurchaseService.Initialize(store, reconciliationService);
        shopTransactionService.Initialize(store, _configuration, reconciliationService, _context.BuybackLedger);
        _context.Initialize(store, stashService, loadoutService, currencyService, shopTransactionService, abilityPurchaseService);
        // HydrateSnapshot deliberately excludes this memory-only, backend-owned ledger.
        if (inventoryData.HasValue) _context.HydrateBuyback(inventoryData.Value);

        _initializedProfileId = profileId;
        Debug.Log($"[{nameof(ApplicationStashServiceBootstrapper)}] Store initialized for ProfileId {profileId.Value}.");
        return true;
    }

    private static bool PrepareRepositoryForLogin(
        LocalProfileRepository repository,
        ProfileId profileId,
        LootDefinitionCatalog catalog,
        AbilityDefinitionCatalog abilityCatalog,
        InventoryData? inventoryData,
        ProgressionData? progressionData,
        bool recoveryAllowed)
    {
        if (!repository.Initialize(profileId, catalog))
        {
            if (recoveryAllowed && repository.CanRecoverCorruptCache &&
                inventoryData.HasValue && progressionData.HasValue &&
                inventoryData.Value.revision == progressionData.Value.revision)
            {
                var candidate = new LocalProfileSnapshot { ProfileId = profileId };
                if (!HydrateSnapshot(profileId, candidate, inventoryData, progressionData, catalog, abilityCatalog))
                    return false;

                if (!repository.TryRecoverCorruptCache(profileId, candidate, out string recoveryError))
                {
                    Debug.LogError($"[{nameof(ApplicationStashServiceBootstrapper)}] Corrupt profile recovery could not be saved: {recoveryError}");
                    return false;
                }

                Debug.LogWarning($"[{nameof(ApplicationStashServiceBootstrapper)}] Corrupt local cache replaced from authenticated backend data. Unsent local state cannot be recovered.");
                return true;
            }

            Debug.LogError($"[{nameof(ApplicationStashServiceBootstrapper)}] Local profile unavailable: {repository.LastError}");
            return false;
        }

        if (inventoryData.HasValue || progressionData.HasValue)
        {
            // Hydrate a clone and persist it, so the repository only ever holds a snapshot
            // that passed the same save path every later Commit uses.
            LocalProfileSnapshot hydrated = repository.Snapshot.Clone();
            if (!HydrateSnapshot(profileId, hydrated, inventoryData, progressionData, catalog, abilityCatalog))
            {
                return false;
            }

            if (!repository.TrySave(hydrated, out string saveError))
            {
                Debug.LogError($"[{nameof(ApplicationStashServiceBootstrapper)}] Hydrated profile could not be saved: {saveError}");
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Updates the local snapshot with authoritative data from the backend.
    /// Note: The following local-only or unsynced fields are NOT overwritten by the backend:
    /// - ActiveMissions
    /// - PreparedAbilities (unless revalidation fails due to attribute changes)
    /// - QuickSlots
    /// - Shop receipts
    /// - PendingExtractionCommit
    /// - Off Hand weapon assignments
    ///
    /// Every input is validated before anything is written, so a rejected payload leaves the
    /// snapshot untouched. Item lists are normalized to what the save codec accepts: unknown
    /// LootIds and non-positive amounts are skipped and duplicates are merged. A Loadout over
    /// its slot capacity is accepted as is; the Store only allows operations that reduce it.
    /// </summary>
    public static bool HydrateSnapshot(
        ProfileId profileId,
        LocalProfileSnapshot snapshot,
        Grimhold.Backend.InventoryData? inventoryData,
        Grimhold.Backend.ProgressionData? progressionData,
        LootDefinitionCatalog catalog,
        AbilityDefinitionCatalog abilityCatalog)
    {
        CharacterAttributeState? validatedAttributes = null;
        PreparedAbilityLoadout? validatedAbilities = null;
        System.Collections.Generic.List<AbilityId> backendUnlockedAbilities = null;

        if (progressionData.HasValue)
        {
            var prog = progressionData.Value;
            var attr = prog.characterAttributes;
            if (!CharacterAttributeState.TryCreate(
                attr.vitality, attr.resistance, attr.strength,
                attr.dexterity, attr.intelligence, attr.luck, attr.availablePoints,
                out var state))
            {
                Debug.LogError($"[{nameof(ApplicationStashServiceBootstrapper)}] Hydration failed: invalid character attributes.");
                return false;
            }

            if (prog.unlockedAbilities != null)
            {
                backendUnlockedAbilities = new System.Collections.Generic.List<AbilityId>();
                foreach (var rawId in prog.unlockedAbilities)
                {
                    if (string.IsNullOrWhiteSpace(rawId)) continue;
                    var abilityId = new AbilityId(rawId);
                    if (abilityCatalog != null && !abilityCatalog.TryGet(abilityId, out _))
                    {
                        Debug.LogWarning($"[{nameof(ApplicationStashServiceBootstrapper)}] Hydration skipped unknown ability '{rawId}'.");
                        continue;
                    }
                    if (!backendUnlockedAbilities.Contains(abilityId))
                    {
                        backendUnlockedAbilities.Add(abilityId);
                    }
                }
            }

            var abilityContext = backendUnlockedAbilities ?? snapshot.UnlockedAbilities;
            if (!PreparedAbilityLoadout.TryRevalidateAfterAttributeChange(
                    snapshot.PreparedAbilities,
                    abilityContext,
                    state,
                    abilityCatalog,
                    out PreparedAbilityLoadout revalidatedAbilities,
                    out string abilityError))
            {
                Debug.LogWarning($"[{nameof(ApplicationStashServiceBootstrapper)}] Hydration found invalid prepared abilities ({abilityError}). Clearing local abilities.");
                revalidatedAbilities = default;
            }

            validatedAttributes = state;
            validatedAbilities = revalidatedAbilities;
        }

        System.Collections.Generic.List<StashItem> stash = null;
        System.Collections.Generic.List<StashItem> loadout = null;
        System.Collections.Generic.List<StashItem> reservationItems = null;
        if (inventoryData.HasValue)
        {
            var data = inventoryData.Value;
            if (data.currency < 0)
            {
                Debug.LogError($"[{nameof(ApplicationStashServiceBootstrapper)}] Hydration failed: negative currency.");
                return false;
            }

            if (!TryNormalizeItems(data.stash, catalog, "stash", out stash) ||
                !TryNormalizeItems(data.loadout, catalog, "loadout", out loadout) ||
                (!string.IsNullOrEmpty(data.pendingReservation.reservationId) &&
                 !TryNormalizeItems(data.pendingReservation.items, catalog, "reservation", out reservationItems)))
            {
                return false;
            }
        }

        if (inventoryData.HasValue)
        {
            var data = inventoryData.Value;
            snapshot.Currency = data.currency;
            snapshot.Stash.Clear();
            snapshot.Stash.AddRange(stash);

            if (data.loadout != null && snapshot.PendingExtractionCommit == null)
            {
                snapshot.Loadout.Clear();
                snapshot.Loadout.AddRange(loadout);
            }

            if (snapshot.PendingExtractionCommit == null)
            {
                var eq = data.preparedEquipment;
                snapshot.PreparedEquipment = new PreparedEquipmentLoadout(
                    string.IsNullOrEmpty(eq.weaponSlot1) ? default : new LootId(eq.weaponSlot1),
                    string.IsNullOrEmpty(eq.weaponSlot2) ? default : new LootId(eq.weaponSlot2),
                    string.IsNullOrEmpty(eq.helmet) ? default : new LootId(eq.helmet),
                    string.IsNullOrEmpty(eq.armor) ? default : new LootId(eq.armor),
                    string.IsNullOrEmpty(eq.gloves) ? default : new LootId(eq.gloves),
                    string.IsNullOrEmpty(eq.boots) ? default : new LootId(eq.boots),
                    string.IsNullOrEmpty(eq.offHand1) ? default : new LootId(eq.offHand1),
                    string.IsNullOrEmpty(eq.offHand2) ? default : new LootId(eq.offHand2)
                );
            }

            if (!string.IsNullOrEmpty(data.pendingReservation.reservationId))
            {
                var res = data.pendingReservation;
                var resEq = res.preparedEquipment;
                var preparedResEq = new PreparedEquipmentLoadout(
                    string.IsNullOrEmpty(resEq.weaponSlot1) ? default : new LootId(resEq.weaponSlot1),
                    string.IsNullOrEmpty(resEq.weaponSlot2) ? default : new LootId(resEq.weaponSlot2),
                    string.IsNullOrEmpty(resEq.helmet) ? default : new LootId(resEq.helmet),
                    string.IsNullOrEmpty(resEq.armor) ? default : new LootId(resEq.armor),
                    string.IsNullOrEmpty(resEq.gloves) ? default : new LootId(resEq.gloves),
                    string.IsNullOrEmpty(resEq.boots) ? default : new LootId(resEq.boots),
                    string.IsNullOrEmpty(resEq.offHand1) ? default : new LootId(resEq.offHand1),
                    string.IsNullOrEmpty(resEq.offHand2) ? default : new LootId(resEq.offHand2)
                );

                snapshot.PendingReservation = new PendingLoadoutReservation(res.reservationId, reservationItems, preparedResEq);
            }
            else if (snapshot.PendingExtractionCommit == null)
            {
                snapshot.PendingReservation = null;
            }

            if (data.lastAppliedExtractionReceipt.resultSequence > 0)
            {
                snapshot.AppliedExtractionReceipts.Clear();
                snapshot.AppliedExtractionReceipts.Add(new ExtractionReceipt(
                    data.lastAppliedExtractionReceipt.raidId,
                    profileId,
                    data.lastAppliedExtractionReceipt.resultSequence
                ));
            }
            
            snapshot.RemoteRevision = data.revision;
        }

        if (progressionData.HasValue)
        {
            var prog = progressionData.Value;
            snapshot.Level = prog.level > 0 ? prog.level : 1;
            snapshot.CurrentExperience = prog.experience > 0 ? prog.experience : 0;
            if (prog.lastAppliedProgressionResultSequence > 0)
            {
                snapshot.LastAppliedProgressionResultSequence = prog.lastAppliedProgressionResultSequence;
                
                var receipt = new ProgressionReceipt(
                    "backend-sync",
                    profileId,
                    prog.lastAppliedProgressionResultSequence,
                    snapshot.CurrentExperience,
                    snapshot.Level);
                    
                snapshot.LastProgressionReceipt = receipt;
                
                // Limpiar el historial local e inyectar el recibo para pasar la validación estricta del Codec V5
                snapshot.AppliedProgressionReceipts.Clear();
                snapshot.AppliedProgressionReceipts.Add(receipt);
            }
            else
            {
                snapshot.LastAppliedProgressionResultSequence = 0;
                snapshot.LastProgressionReceipt = null;
            }
            
            if (backendUnlockedAbilities != null)
            {
                snapshot.UnlockedAbilities.Clear();
                snapshot.UnlockedAbilities.AddRange(backendUnlockedAbilities);
            }

            snapshot.CharacterAttributes = validatedAttributes.Value;
            snapshot.PreparedAbilities = validatedAbilities.Value;
            snapshot.RemoteRevision = prog.revision;
        }

        return true;
    }

    private static bool TryNormalizeItems(
        InventoryItemData[] items,
        LootDefinitionCatalog catalog,
        string label,
        out System.Collections.Generic.List<StashItem> normalized)
    {
        normalized = new System.Collections.Generic.List<StashItem>();
        if (items == null) return true;

        foreach (var item in items)
        {
            if (item.amount <= 0 || string.IsNullOrWhiteSpace(item.lootId) || !catalog.TryGet(item.lootId, out _))
            {
                continue;
            }

            var lootId = new LootId(item.lootId);
            int index = normalized.FindIndex(existing => existing.LootId == lootId);
            if (index < 0)
            {
                normalized.Add(new StashItem(lootId, item.amount));
                continue;
            }

            long merged = (long)normalized[index].Amount + item.amount;
            if (merged > int.MaxValue)
            {
                Debug.LogError($"[{nameof(ApplicationStashServiceBootstrapper)}] Hydration failed: {label} amount overflow for '{item.lootId}'.");
                return false;
            }

            normalized[index] = new StashItem(lootId, (int)merged);
        }

        return true;
    }

    private static async System.Threading.Tasks.Task RetryPendingExtractionAsync(
        LocalProfileStore store,
        BackendConfiguration backendConfig,
        string authToken)
    {
        var commit = store.PendingExtractionCommit;
        if (commit == null) return;

        var requestItems = new InventoryItemData[commit.Items.Count];
        for (int i = 0; i < commit.Items.Count; i++)
        {
            requestItems[i] = new InventoryItemData
            {
                lootId = commit.Items[i].LootId.Value,
                amount = commit.Items[i].Amount
            };
        }

        var request = new CommitExtractionUnifiedRequest
        {
            raidId = commit.Receipt.RaidId,
            resultSequence = commit.Receipt.ResultSequence,
            items = requestItems,
            preparedEquipment = new PreparedEquipmentData
            {
                weaponSlot1 = commit.PreparedEquipment.WeaponSetAMainHand.IsValid ? commit.PreparedEquipment.WeaponSetAMainHand.Value : null,
                weaponSlot2 = commit.PreparedEquipment.WeaponSetBMainHand.IsValid ? commit.PreparedEquipment.WeaponSetBMainHand.Value : null,
                helmet = commit.PreparedEquipment.Helmet.IsValid ? commit.PreparedEquipment.Helmet.Value : null,
                armor = commit.PreparedEquipment.Armor.IsValid ? commit.PreparedEquipment.Armor.Value : null,
                gloves = commit.PreparedEquipment.Gloves.IsValid ? commit.PreparedEquipment.Gloves.Value : null,
                boots = commit.PreparedEquipment.Boots.IsValid ? commit.PreparedEquipment.Boots.Value : null,
                offHand1 = commit.PreparedEquipment.WeaponSetAOffHand.IsValid ? commit.PreparedEquipment.WeaponSetAOffHand.Value : null,
                offHand2 = commit.PreparedEquipment.WeaponSetBOffHand.IsValid ? commit.PreparedEquipment.WeaponSetBOffHand.Value : null
            },
            progression = new ExtractionProgressionData
            {
                consolidatedExperience = commit.ConsolidatedExperience,
                resultingLevel = commit.ResultingLevel
            }
        };

        var (success, result, error) = await InventoryClient.CommitExtractionUnifiedAsync(backendConfig, authToken, request);

        if (success)
        {
            Debug.Log($"[{nameof(ApplicationStashServiceBootstrapper)}] Successfully recovered pending extraction commit.");
            store.SetRemoteRevision(result.revision);
            store.ClearPendingExtractionCommit();
        }
        else
        {
            Debug.LogError($"[{nameof(ApplicationStashServiceBootstrapper)}] Failed to recover pending extraction commit: {error.error} - {error.message}");
        }
    }

    /// <summary>
    /// Resets the application stash context so that a new authenticated profile can be loaded.
    /// This destroys the in-memory services but preserves the DDOL GameObject.
    /// </summary>
    public static void ResetForLogout()
    {
        if (_context != null)
        {
            var contextObject = _context.gameObject;
            
            // Destroy all dynamically added service components
            foreach (var comp in contextObject.GetComponents<MonoBehaviour>())
            {
                // We don't want to destroy the context itself, just the services
                if (comp != _context)
                {
                    Object.Destroy(comp);
                }
            }

            _context.Initialize(null, null, null, null, null);
        }

        _initializedProfileId = default;
        Debug.Log($"[{nameof(ApplicationStashServiceBootstrapper)}] Context reset for logout.");
    }

    /// <summary>
    /// Resolves only an orphan, never a pending extraction. Confirmation already implements
    /// the transactional discard we need: it removes the reservation without returning ownership.
    /// Admission metadata comes from the authenticated inventory payload, not the local cache.
    /// </summary>
    public static StashOperationResult TryResolveOrphanedReservation(LocalProfileStore store, bool admitted)
    {
        if (store == null || !store.IsAvailable) return StashOperationResult.InvalidInventory;
        if (store.PendingExtractionCommit != null) return StashOperationResult.ReservationPending;
        if (store.PendingReservation == null) return StashOperationResult.InvalidInventory;
        string reservationId = store.PendingReservation.ReservationId;
        return admitted
            ? store.TryConfirmLoadoutReservation(reservationId)
            : store.TryRollbackLoadoutReservation(reservationId);
    }

    private static async System.Threading.Tasks.Task ClearOrphanedReservationAsync(
        RemoteInventoryService service, bool rollback)
    {
        var (success, error) = rollback
            ? await service.RollbackPendingReservationAsync()
            : await service.ClearPendingReservationAsync();
        // The loss service already warns for this protected authoritative result.
        // Do not retry as rollback, reconcile, or discard a pending extraction.
        if (!success && error.error != "EXTRACTION_RESULT_PENDING")
        {
            Debug.LogWarning($"[{nameof(ApplicationStashServiceBootstrapper)}] Orphan reservation cleanup failed: {error.error} - {error.message}");
        }
    }
}
