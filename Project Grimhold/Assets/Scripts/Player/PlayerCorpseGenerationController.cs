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
    internal void TryConvertInventoryToCorpseLoot(int simulationTick)
    {
        if (!HasStateAuthority || State != GenerationState.Waiting)
        {
            return;
        }

        State = GenerationState.Processing;
        bool contentLoaded = false;
        IReadOnlyList<LootEntry> snapshot = null;
        try
        {
            CacheDependencies();
            if (_lootReceiver == null || _lootContainer == null || Runner == null ||
                !Runner.IsSimulationUpdating)
            {
                FailAndCompensate(contentLoaded, null, simulationTick, "Dependencies or runner are invalid.");
                return;
            }

            string snapshotError = null;
            if (!_lootReceiver.TryGetLootContent(out snapshot) ||
                !TryValidateSnapshot(snapshot, out snapshotError))
            {
                FailAndCompensate(contentLoaded, snapshot, simulationTick, $"Cannot capture a valid inventory snapshot. {snapshotError}");
                return;
            }

            if (!_lootContainer.IsInitialized || _lootContainer.IsAvailable || !_lootContainer.IsEmpty)
            {
                FailAndCompensate(contentLoaded, snapshot, simulationTick, "The co-located container is not empty and unavailable.");
                return;
            }

            if (!_lootContainer.TryLoadExactContent(snapshot, out string loadError))
            {
                FailAndCompensate(contentLoaded, snapshot, simulationTick, $"Cannot load the container. {loadError}");
                return;
            }
            contentLoaded = true;

            string inventoryError = null;
            if (!_lootContainer.HasExactContent(snapshot) ||
                !_lootReceiver.TryMatchesExactContent(snapshot, out inventoryError))
            {
                FailAndCompensate(contentLoaded, snapshot, simulationTick, $"Snapshot verification failed. {inventoryError}");
                return;
            }

#if UNITY_INCLUDE_TESTS
            if (TestShouldClearPlayerInventory != null && !TestShouldClearPlayerInventory())
            {
                FailAndCompensate(contentLoaded, snapshot, simulationTick, "The focused test seam rejected player inventory clearing.");
                return;
            }
#endif

            if (!_lootReceiver.TryClearExactContent(snapshot, out string clearError))
            {
                FailAndCompensate(contentLoaded, snapshot, simulationTick, $"Inventory changed before atomic clear. {clearError}");
                return;
            }

            if (!_lootReceiver.TryGetLootContent(out IReadOnlyList<LootEntry> clearedInventory) || clearedInventory.Count != 0)
            {
                FailAndCompensate(contentLoaded, snapshot, simulationTick, "Player inventory did not become empty after the exact clear.");
                return;
            }

            _lootContainer.SetAvailability(true);
            State = GenerationState.Completed;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            FailAndCompensate(contentLoaded, snapshot, simulationTick, "Unexpected inventory conversion exception.");
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
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        CacheDependencies();
    }
#endif
}
