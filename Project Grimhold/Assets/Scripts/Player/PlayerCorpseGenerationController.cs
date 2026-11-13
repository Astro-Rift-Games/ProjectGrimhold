using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// Converts the defeated player's temporary inventory into its co-located loot container.
///
/// This component is invoked by <see cref="PlayerCharacter.HandleDeath"/> from the
/// damage simulation path. It owns no loot storage and only coordinates the existing
/// player inventory with its existing <see cref="NetworkLootContainer"/>.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerLootReceiver))]
public sealed class PlayerCorpseGenerationController : NetworkBehaviour
{
    internal enum GenerationState
    {
        Waiting,
        Processing,
        Completed,
        Failed
    }

    [SerializeField]
    private PlayerLootReceiver _lootReceiver;

    [SerializeField]
    private PlayerWeaponEquipmentNetworkController _weaponEquipment;

    [SerializeField]
    private NetworkLootContainer _lootContainer;

    [Networked]
    internal GenerationState State { get; set; }

    internal bool IsCompleted => State == GenerationState.Completed;

#if UNITY_INCLUDE_TESTS
    // The production transaction has no extension point. This conditional seam lets
    // the focused integration test exercise compensation after publication.
    internal Func<bool> TestShouldClearPlayerInventory { get; set; }
    internal Func<bool> TestShouldFailPostClearVerification { get; set; }
    internal Action TestThrowDuringPostClearCooldownTransfer { get; set; }
#endif

    private void Awake()
    {
        CacheDependencies();
    }

    /// <summary>
    /// Attempts the one-shot authoritative inventory conversion for the fatal transition
    /// that has just been accepted by <see cref="CharacterBase.ApplyDamage"/>.
    /// </summary>
    internal bool TryConvertInventoryToCorpseLoot(int simulationTick)
    {
        if (!HasStateAuthority)
        {
            return false;
        }

        if (State == GenerationState.Completed)
        {
            return true;
        }

        if (State != GenerationState.Waiting)
        {
            return false;
        }

        State = GenerationState.Processing;
        bool contentLoaded = false;
        bool playerCleared = false;
        IReadOnlyList<LootEntry> snapshot = null;
        IReadOnlyList<RaidLootOriginEntry> originSnapshot = null;
        PlayerExpeditionLootSnapshot ownershipSnapshot = null;
        IReadOnlyList<ExpeditionWeaponUnit> modifiedUnits = null;
        try
        {
            CacheDependencies();
            if (_lootReceiver == null || _lootReceiver.LootCatalog == null || _weaponEquipment == null || _lootContainer == null || Runner == null ||
                (!Runner.IsSimulationUpdating && !Runner.IsServer))
            {
                FailAndCompensate(ownershipSnapshot, contentLoaded, playerCleared: false, null, null, null, simulationTick, "Dependencies or runner are invalid.");
                return false;
            }

            string snapshotError = null;
            if (!PlayerExpeditionLootSnapshot.TryCapture(
                    _lootReceiver,
                    _weaponEquipment,
                    out ownershipSnapshot,
                    out snapshotError))
            {
                FailAndCompensate(ownershipSnapshot, contentLoaded, playerCleared: false, snapshot, originSnapshot, modifiedUnits, simulationTick, $"Cannot capture expedition loot. {snapshotError}");
                return false;
            }

            if (!TryValidateSnapshot(ownershipSnapshot.Combined, out snapshotError))
            {
                FailAndCompensate(ownershipSnapshot, contentLoaded, playerCleared: false, snapshot, originSnapshot, modifiedUnits, simulationTick, $"Cannot capture a valid inventory snapshot. {snapshotError}");
                return false;
            }

            // The container receives the corpse projection; the player is still cleared from the full snapshot.
            CorpseLootProjection.ExcludeTrainingWeapons(
                ownershipSnapshot.Combined,
                ownershipSnapshot.CombinedOrigins,
                ownershipSnapshot.ModifiedWeaponUnits,
                _lootReceiver.LootCatalog,
                out snapshot,
                out originSnapshot,
                out modifiedUnits);

            if (!_lootContainer.IsInitialized || _lootContainer.IsAvailable || !_lootContainer.IsEmpty)
            {
                FailAndCompensate(ownershipSnapshot, contentLoaded, playerCleared: false, snapshot, originSnapshot, modifiedUnits, simulationTick, "The co-located container is not empty and unavailable.");
                return false;
            }

            if (!_lootContainer.TryLoadExactContent(snapshot, originSnapshot, modifiedUnits, out string loadError))
            {
                FailAndCompensate(ownershipSnapshot, contentLoaded, playerCleared: false, snapshot, originSnapshot, modifiedUnits, simulationTick, $"Cannot load the container. {loadError}");
                return false;
            }
            contentLoaded = true;

            string inventoryError = null;
            if (!_lootContainer.HasExactContent(snapshot, originSnapshot, modifiedUnits) ||
                !ownershipSnapshot.MatchesCurrent(_lootReceiver, _weaponEquipment, out inventoryError))
            {
                FailAndCompensate(ownershipSnapshot, contentLoaded, playerCleared: false, snapshot, originSnapshot, modifiedUnits, simulationTick, $"Snapshot verification failed. {inventoryError}");
                return false;
            }

#if UNITY_INCLUDE_TESTS
            if (TestShouldClearPlayerInventory != null && !TestShouldClearPlayerInventory())
            {
                FailAndCompensate(ownershipSnapshot, contentLoaded, playerCleared: false, snapshot, originSnapshot, modifiedUnits, simulationTick, "The focused test seam rejected player inventory clearing.");
                return false;
            }
#endif

            if (!ownershipSnapshot.TryClearExact(_lootReceiver, _weaponEquipment, out string clearError))
            {
                FailAndCompensate(ownershipSnapshot, contentLoaded, playerCleared: false, snapshot, originSnapshot, modifiedUnits, simulationTick, $"Inventory changed before atomic clear. {clearError}");
                return false;
            }
            playerCleared = true;

#if UNITY_INCLUDE_TESTS
            if (TestShouldFailPostClearVerification != null && TestShouldFailPostClearVerification())
            {
                FailAndCompensate(ownershipSnapshot, contentLoaded, playerCleared: true, snapshot, originSnapshot, modifiedUnits, simulationTick, "The focused test seam rejected post-clear verification.");
                return IsCompleted;
            }
#endif

            if (!_lootReceiver.TryGetLootContent(out IReadOnlyList<LootEntry> clearedInventory) ||
                clearedInventory.Count != 0 || _weaponEquipment.HasAnyEquipment)
            {
                FailAndCompensate(ownershipSnapshot, contentLoaded, playerCleared: true, snapshot, originSnapshot, modifiedUnits, simulationTick, "Player inventory did not become empty after the exact clear.");
                return IsCompleted;
            }

#if UNITY_INCLUDE_TESTS
            TestThrowDuringPostClearCooldownTransfer?.Invoke();
#endif

            try
            {
                TransferActiveUtilityCooldownsToCorpse(snapshot);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, this);
            }

            try
            {
                PublishLostResultsSummary(ownershipSnapshot);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, this);
            }

            _lootContainer.SetAvailability(true);
            State = GenerationState.Completed;
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            FailAndCompensate(ownershipSnapshot, contentLoaded, playerCleared, snapshot, originSnapshot, modifiedUnits, simulationTick, "Unexpected inventory conversion exception.");
            return IsCompleted;
        }
    }

    private void TransferActiveUtilityCooldownsToCorpse(IReadOnlyList<LootEntry> snapshot)
    {
        if (snapshot == null || _lootContainer == null ||
            !TryGetComponent(out PlayerConsumableNetworkController cooldownProvider))
        {
            return;
        }

        for (int i = 0; i < snapshot.Count; i++)
        {
            LootId lootId = snapshot[i].LootId;
            TickTimer timer = cooldownProvider.GetUtilityCooldown(lootId);
            if (Runner != null ? !timer.ExpiredOrNotRunning(Runner) : timer.IsRunning)
            {
                _lootContainer.SetUtilityCooldown(lootId, timer);
            }

            cooldownProvider.ClearUtilityCooldown(lootId);
        }
    }

    /// <summary>
    /// Records what the player lost for the Results screen while the exact snapshot is still
    /// known. Presentation only: a failure is reported but never undoes the conversion.
    /// </summary>
    private void PublishLostResultsSummary(PlayerExpeditionLootSnapshot ownershipSnapshot)
    {
        // Without a Raid participant there is no Results screen to describe.
        if (!TryGetComponent(out RaidAvatarParticipantLink participantLink) ||
            !participantLink.TryResolveParticipant(out NetworkRaidParticipant participant))
        {
            return;
        }

        string error = $"Missing {nameof(ExtractedLootExperienceProducer)}.";
        if (TryGetComponent(out ExtractedLootExperienceProducer producer) &&
            producer.TryPrepareLostResultsSummary(
                ownershipSnapshot,
                out RaidResultsLootSummary summary,
                out error) &&
            participant.TryStoreResultsLoot(summary, _lootReceiver.LootCatalog))
        {
            return;
        }

        Debug.LogError(
            $"{nameof(PlayerCorpseGenerationController)}: The Results lost-loot summary could not be published. {error}",
            this);
    }

    /// <summary>
    /// When a forced defeat occurs and corpse generation did not complete successfully,
    /// ensures unowned items are liquidated from the player avatar and the lost loot
    /// summary is published to the participant so the Results screen reflects the loss.
    /// </summary>
    internal void EnsureUnownedItemsLiquidatedAndLossSummaryPublished(NetworkRaidParticipant participant)
    {
        if (State == GenerationState.Completed)
        {
            return;
        }

        if (participant == null)
        {
            TryGetComponent(out RaidAvatarParticipantLink participantLink);
            participantLink?.TryResolveParticipant(out participant);
        }

        if (PlayerExpeditionLootSnapshot.TryCapture(_lootReceiver, _weaponEquipment, out PlayerExpeditionLootSnapshot snapshot, out string _))
        {
            if (participant != null)
            {
                PublishLostResultsSummary(snapshot);
            }

            if (_lootReceiver != null && _weaponEquipment != null)
            {
                snapshot.TryClearExact(_lootReceiver, _weaponEquipment, out _);
            }
        }
    }

    private void FailAndCompensate(
        PlayerExpeditionLootSnapshot ownershipSnapshot,
        bool contentLoaded,
        bool playerCleared,
        IReadOnlyList<LootEntry> snapshot,
        IReadOnlyList<RaidLootOriginEntry> originSnapshot,
        IReadOnlyList<ExpeditionWeaponUnit> modifiedUnits,
        int simulationTick,
        string reason)
    {
        PlayerCorpseCompensationAction action = PlayerCorpseCompensationRule.DetermineCompensationAction(playerCleared);
        if (action == PlayerCorpseCompensationAction.PreserveAndEnableCorpse)
        {
            if (_lootContainer != null && !_lootContainer.IsAvailable)
            {
                _lootContainer.SetAvailability(true);
            }

            try
            {
                PublishLostResultsSummary(ownershipSnapshot);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, this);
            }

            State = GenerationState.Completed;
            Debug.LogWarning(
                $"{nameof(PlayerCorpseGenerationController)}: Post-clear failure occurred ({reason}), but player inventory was already cleared. Preserved and enabled corpse container to prevent loot destruction.",
                this);
            return;
        }

        if (contentLoaded && _lootContainer != null && snapshot != null)
        {
            if (_lootContainer.IsAvailable)
            {
                _lootContainer.SetAvailability(false);
            }

            if (!_lootContainer.TryClearExactContent(snapshot, originSnapshot, modifiedUnits, out string rollbackError))
            {
                Debug.LogError($"{nameof(PlayerCorpseGenerationController)} rollback failed. {rollbackError}", this);
            }
        }

        // Recoverable only when compensation left nothing behind (FIX-53 S7): a later Abandon may then
        // retry. Otherwise the state stays terminal so a retry cannot lose or duplicate items.
        bool canRetry = PlayerCorpseGenerationRetryRule.CanRetryAfterFailure(
            IsPlayerInventoryIntact(ownershipSnapshot),
            IsCorpseContainerClean());
        State = canRetry ? GenerationState.Waiting : GenerationState.Failed;
        Debug.LogError(
            $"{nameof(PlayerCorpseGenerationController)} failed for player '{name}' at tick {simulationTick}. " +
            $"{reason} Retry {(canRetry ? "allowed" : "not allowed")}.",
            this);
    }

    // Nothing is mutated before the snapshot exists, so a missing snapshot means an untouched player.
    private bool IsPlayerInventoryIntact(PlayerExpeditionLootSnapshot ownershipSnapshot)
    {
        if (ownershipSnapshot == null)
        {
            return true;
        }

        try
        {
            return _lootReceiver != null && _weaponEquipment != null &&
                ownershipSnapshot.MatchesCurrent(_lootReceiver, _weaponEquipment, out _);
        }
        catch (Exception exception)
        {
            // Compensation must always settle the state; an unverifiable inventory stays terminal.
            Debug.LogException(exception, this);
            return false;
        }
    }

    private bool IsCorpseContainerClean()
    {
        return _lootContainer != null && !_lootContainer.IsAvailable && _lootContainer.IsEmpty;
    }

    private static bool TryValidateSnapshot(IReadOnlyList<LootEntry> snapshot, out string error)
    {
        error = null;
        if (snapshot == null)
        {
            error = "Snapshot is null.";
            return false;
        }

        var lootIds = new HashSet<LootId>();
        for (int index = 0; index < snapshot.Count; index++)
        {
            LootEntry entry = snapshot[index];
            if (!entry.IsValid || !lootIds.Add(entry.LootId))
            {
                error = $"Snapshot entry {index} is invalid or duplicated.";
                return false;
            }
        }

        return true;
    }

    private void CacheDependencies()
    {
        if (_lootReceiver == null)
        {
            _lootReceiver = GetComponent<PlayerLootReceiver>();
        }

        if (_lootContainer == null)
        {
            _lootContainer = GetComponent<NetworkLootContainer>();
        }

        if (_weaponEquipment == null)
        {
            _weaponEquipment = GetComponent<PlayerWeaponEquipmentNetworkController>();
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        CacheDependencies();
    }
#endif
}
