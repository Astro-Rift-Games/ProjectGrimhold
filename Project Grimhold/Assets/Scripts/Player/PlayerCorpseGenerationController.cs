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
    private enum GenerationState
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
    private GenerationState State { get; set; }

#if UNITY_INCLUDE_TESTS
    // The production transaction has no extension point. This conditional seam lets
    // the focused integration test exercise compensation after publication.
    internal Func<bool> TestShouldClearPlayerInventory { get; set; }
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
        IReadOnlyList<LootEntry> snapshot = null;
        PlayerExpeditionLootSnapshot ownershipSnapshot = null;
        try
        {
            CacheDependencies();
            if (_lootReceiver == null || _weaponEquipment == null || _lootContainer == null || Runner == null ||
                !Runner.IsSimulationUpdating)
            {
                FailAndCompensate(contentLoaded, null, simulationTick, "Dependencies or runner are invalid.");
                return false;
            }

            string snapshotError = null;
            if (!PlayerExpeditionLootSnapshot.TryCapture(
                    _lootReceiver,
                    _weaponEquipment,
                    out ownershipSnapshot,
                    out snapshotError))
            {
                FailAndCompensate(contentLoaded, snapshot, simulationTick, $"Cannot capture expedition loot. {snapshotError}");
                return false;
            }

            snapshot = ownershipSnapshot.Combined;
            if (!TryValidateSnapshot(snapshot, out snapshotError))
            {
                FailAndCompensate(contentLoaded, snapshot, simulationTick, $"Cannot capture a valid inventory snapshot. {snapshotError}");
                return false;
            }

            if (!_lootContainer.IsInitialized || _lootContainer.IsAvailable || !_lootContainer.IsEmpty)
            {
                FailAndCompensate(contentLoaded, snapshot, simulationTick, "The co-located container is not empty and unavailable.");
                return false;
            }

            if (!_lootContainer.TryLoadExactContent(snapshot, out string loadError))
            {
                FailAndCompensate(contentLoaded, snapshot, simulationTick, $"Cannot load the container. {loadError}");
                return false;
            }
            contentLoaded = true;

            string inventoryError = null;
            if (!_lootContainer.HasExactContent(snapshot) ||
                !ownershipSnapshot.MatchesCurrent(_lootReceiver, _weaponEquipment, out inventoryError))
            {
                FailAndCompensate(contentLoaded, snapshot, simulationTick, $"Snapshot verification failed. {inventoryError}");
                return false;
            }

#if UNITY_INCLUDE_TESTS
            if (TestShouldClearPlayerInventory != null && !TestShouldClearPlayerInventory())
            {
                FailAndCompensate(contentLoaded, snapshot, simulationTick, "The focused test seam rejected player inventory clearing.");
                return false;
            }
#endif

            if (!ownershipSnapshot.TryClearExact(_lootReceiver, _weaponEquipment, out string clearError))
            {
                FailAndCompensate(contentLoaded, snapshot, simulationTick, $"Inventory changed before atomic clear. {clearError}");
                return false;
            }

            if (!_lootReceiver.TryGetLootContent(out IReadOnlyList<LootEntry> clearedInventory) ||
                clearedInventory.Count != 0 || _weaponEquipment.HasAnyEquipment)
            {
                FailAndCompensate(contentLoaded, snapshot, simulationTick, "Player inventory did not become empty after the exact clear.");
                return false;
            }

            _lootContainer.SetAvailability(true);
            State = GenerationState.Completed;
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            FailAndCompensate(contentLoaded, snapshot, simulationTick, "Unexpected inventory conversion exception.");
            return false;
        }
    }

    private void FailAndCompensate(bool contentLoaded, IReadOnlyList<LootEntry> snapshot, int simulationTick, string reason)
    {
        if (contentLoaded && _lootContainer != null && snapshot != null)
        {
            if (_lootContainer.IsAvailable)
            {
                _lootContainer.SetAvailability(false);
            }

            if (!_lootContainer.TryClearExactContent(snapshot, out string rollbackError))
            {
                Debug.LogError($"{nameof(PlayerCorpseGenerationController)} rollback failed. {rollbackError}", this);
            }
        }

        State = GenerationState.Failed;
        Debug.LogError(
            $"{nameof(PlayerCorpseGenerationController)} failed for player '{name}' at tick {simulationTick}. {reason}",
            this);
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
