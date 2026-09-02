using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// The Presenter component of the MVP pattern for the Lobby Stash and Loadout.
/// Bridges the UI view and the Stash/Loadout Services, isolating logic from rendering.
/// Handles user requests to transfer items between Stash and Loadout.
/// </summary>
public class LobbyStashPresenter : MonoBehaviour
{
    [SerializeField] private LobbyStashUI _stashUI;
    [SerializeField] private LootDefinitionCatalog _lootCatalog;
    private IPlayerStashService _stashService;
    private IPlayerLoadoutService _loadoutService;
    private RemoteInventoryService _remoteInventoryService;
    private ApplicationStashContext _context;
    private ProfileId _localProfileId;
    private readonly List<RaidInventorySlotData> _preparedProjection = new();

    private void OnEnable()
    {
        _localProfileId = LocalProfileProvider.GetOrCreateLocalProfile();

        if (_stashUI != null)
        {
            _stashUI.TransferRequested += OnTransferRequested;
            _stashUI.TakeAllRequested += OnTakeAllRequested;
            _stashUI.LeaveAllRequested += OnLeaveAllRequested;
            _stashUI.PreparedEquipmentAssignmentRequested += OnPreparedEquipmentAssignmentRequested;
            _stashUI.PreparedEquipmentClearRequested += OnPreparedEquipmentClearRequested;
        }

        _context = FindAnyObjectByType<ApplicationStashContext>();
        if (_context != null)
        {
            _stashService = _context.StashService;
            _loadoutService = _context.LoadoutService;
            _remoteInventoryService = _context.GetComponent<RemoteInventoryService>();
            _context.ProfileCommitted += OnProfileCommitted;
            RefreshUI();
        }
        else
        {
            Debug.LogWarning("[LobbyStashPresenter] ApplicationStashContext not found. Stash UI will be empty.");
        }
    }

    private void OnDisable()
    {
        if (_stashUI != null)
        {
            _stashUI.TransferRequested -= OnTransferRequested;
            _stashUI.TakeAllRequested -= OnTakeAllRequested;
            _stashUI.LeaveAllRequested -= OnLeaveAllRequested;
            _stashUI.PreparedEquipmentAssignmentRequested -= OnPreparedEquipmentAssignmentRequested;
            _stashUI.PreparedEquipmentClearRequested -= OnPreparedEquipmentClearRequested;
        }

        if (_context != null) _context.ProfileCommitted -= OnProfileCommitted;
        _context = null;
    }

    private async void OnTakeAllRequested()
    {
        if (_loadoutService == null || _remoteInventoryService == null) return;
        
        var stashItems = _stashService.GetStash(_localProfileId).ToList();
        foreach (var item in stashItems)
        {
            var (success, error) = await _remoteInventoryService.MoveToLoadoutAsync(item.LootId, item.Amount);
            if (!success)
            {
                Debug.LogWarning($"[LobbyStashPresenter] Remote Take All failed for {item.LootId.Value}: {error.message}");
                return;
            }
        }

        var result = _loadoutService.TryTransferAllToLoadout(_localProfileId);
        if (result != StashOperationResult.Success)
        {
            Debug.LogWarning($"[LobbyStashPresenter] Take All failed locally: {result}");
        }
    }

    private async void OnLeaveAllRequested()
    {
        if (_loadoutService == null || _remoteInventoryService == null) return;

        var loadoutItems = _loadoutService.GetLoadout(_localProfileId).ToList();
        foreach (var item in loadoutItems)
        {
            var (success, error) = await _remoteInventoryService.MoveToStashAsync(item.LootId, item.Amount);
            if (!success)
            {
                Debug.LogWarning($"[LobbyStashPresenter] Remote Leave All failed for {item.LootId.Value}: {error.message}");
                return;
            }
        }

        var result = _loadoutService.TryTransferAllToStash(_localProfileId);
        if (result != StashOperationResult.Success)
        {
            Debug.LogWarning($"[LobbyStashPresenter] Leave All failed locally: {result}");
        }
    }

    private async void OnTransferRequested(LootId lootId, bool isFromStash, LootTransferQuantityMode mode)
    {
        if (_loadoutService == null || _remoteInventoryService == null) return;

        int amountToTransfer = mode == LootTransferQuantityMode.FullStack ? int.MaxValue : 1;

        if (amountToTransfer == int.MaxValue)
        {
            if (isFromStash)
            {
                var item = _stashService.GetStash(_localProfileId).FirstOrDefault(i => i.LootId == lootId);
                if (item != null) amountToTransfer = item.Amount;
            }
            else
            {
                var item = _loadoutService.GetLoadout(_localProfileId).FirstOrDefault(i => i.LootId == lootId);
                if (item != null) amountToTransfer = item.Amount;
            }
        }

        if (amountToTransfer == int.MaxValue || amountToTransfer <= 0) return;

        var (success, error) = isFromStash 
            ? await _remoteInventoryService.MoveToLoadoutAsync(lootId, amountToTransfer)
            : await _remoteInventoryService.MoveToStashAsync(lootId, amountToTransfer);

        if (!success)
        {
            Debug.LogWarning($"[LobbyStashPresenter] Remote transfer failed: {error.message}");
            return;
        }

        StashOperationResult result;
        if (isFromStash)
        {
            result = _loadoutService.TryTransferToLoadout(_localProfileId, lootId, amountToTransfer);
        }
        else
        {
            result = _loadoutService.TryTransferToStash(_localProfileId, lootId, amountToTransfer);
        }

        if (result != StashOperationResult.Success)
        {
            Debug.LogWarning($"[LobbyStashPresenter] Transfer failed locally: {result}");
        }
    }

    private async void OnPreparedEquipmentAssignmentRequested(LootId lootId, EquipmentSlot slot)
    {
        if (_loadoutService == null || _remoteInventoryService == null) return;

        // Optimistic local first so we can read the full prepared equipment layout to send it
        StashOperationResult result = _loadoutService.TryAssignPreparedEquipment(
            _localProfileId,
            slot,
            lootId);

        if (result != StashOperationResult.Success)
        {
            Debug.LogWarning($"[LobbyStashPresenter] Prepared equipment assignment failed locally: {result}");
            return;
        }

        var equipment = _loadoutService.GetPreparedEquipment(_localProfileId);
        var (success, error) = await _remoteInventoryService.UpdatePreparedEquipmentAsync(equipment);
        
        if (!success)
        {
            Debug.LogWarning($"[LobbyStashPresenter] Remote prepared equipment assignment failed: {error.message}");
            // Rollback local change
            _loadoutService.TryClearPreparedEquipment(_localProfileId, slot);
        }
    }

    private async void OnPreparedEquipmentClearRequested(EquipmentSlot slot)
    {
        if (_loadoutService == null || _remoteInventoryService == null) return;

        var previousEquipment = _loadoutService.GetPreparedEquipment(_localProfileId);
        var previousLootId = previousEquipment.Get(slot);

        StashOperationResult result = _loadoutService.TryClearPreparedEquipment(_localProfileId, slot);
        if (result != StashOperationResult.Success)
        {
            Debug.LogWarning($"[LobbyStashPresenter] Prepared equipment clear failed locally: {result}");
            return;
        }

        var equipment = _loadoutService.GetPreparedEquipment(_localProfileId);
        var (success, error) = await _remoteInventoryService.UpdatePreparedEquipmentAsync(equipment);

        if (!success)
        {
            Debug.LogWarning($"[LobbyStashPresenter] Remote prepared equipment clear failed: {error.message}");
            // Rollback
            _loadoutService.TryAssignPreparedEquipment(_localProfileId, slot, previousLootId);
        }
    }

    private void OnProfileCommitted(ProfileId updatedProfileId)
    {
        if (updatedProfileId.Value == _localProfileId.Value)
        {
            RefreshUI();
        }
    }

    private void RefreshUI()
    {
        if (_stashUI == null)
            return;

        // Refresh Stash
        if (_stashService != null)
        {
            var stashItems = _stashService.GetStash(_localProfileId);
            _stashUI.DisplayStash(MapToPresentation(stashItems));
        }

        // Refresh Loadout
        if (_loadoutService != null)
        {
            var loadoutItems = _loadoutService.GetLoadout(_localProfileId);
            _stashUI.DisplayLoadout(MapToPresentation(loadoutItems));
            PreparedEquipmentLoadout prepared = _loadoutService.GetPreparedEquipment(_localProfileId);
            EquipmentSlot[] slots = EquipmentSlotRules.AllSlots;
            _preparedProjection.Clear();
            for (int index = 0; index < slots.Length; index++)
            {
                _preparedProjection.Add(MapPreparedUnit(prepared.Get(slots[index])));
            }

            _stashUI.DisplayPreparedEquipment(_preparedProjection);
        }
    }

    private IReadOnlyList<RaidInventorySlotData> MapToPresentation(IReadOnlyList<StashItem> items)
    {
        var presentationData = new List<RaidInventorySlotData>();
        if (items != null)
        {
            foreach (var item in items)
            {
                LootDefinition definition = null;
                if (_lootCatalog != null)
                {
                    _lootCatalog.TryGet(item.LootId.Value, out definition);
                }
                
                LootEntry entry = new LootEntry(item.LootId, item.Amount);
                var slotData = RaidInventorySlotData.Create(entry, definition, ResolvePlaceholderIcon());
                presentationData.Add(slotData);
            }
        }
        return presentationData;
    }

    private RaidInventorySlotData MapPreparedUnit(LootId lootId)
    {
        if (!lootId.IsValid)
        {
            return RaidInventorySlotData.Empty;
        }

        LootDefinition definition = null;
        if (_lootCatalog != null)
        {
            _lootCatalog.TryGet(lootId.Value, out definition);
        }
        return RaidInventorySlotData.Create(new LootEntry(lootId, 1), definition, ResolvePlaceholderIcon());
    }

    /// <summary>The fallback icon authored on the panels, used when a definition has no icon.</summary>
    private Sprite ResolvePlaceholderIcon() => _stashUI != null ? _stashUI.PlaceholderIcon : null;
}
