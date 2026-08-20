using System.Collections.Generic;
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

    private void OnTakeAllRequested()
    {
        if (_loadoutService == null) return;
        var result = _loadoutService.TryTransferAllToLoadout(_localProfileId);
        if (result != StashOperationResult.Success)
        {
            Debug.LogWarning($"[LobbyStashPresenter] Take All failed: {result}");
        }
    }

    private void OnLeaveAllRequested()
    {
        if (_loadoutService == null) return;
        var result = _loadoutService.TryTransferAllToStash(_localProfileId);
        if (result != StashOperationResult.Success)
        {
            Debug.LogWarning($"[LobbyStashPresenter] Leave All failed: {result}");
        }
    }

    private void OnTransferRequested(LootId lootId, bool isFromStash, LootTransferQuantityMode mode)
    {
        if (_loadoutService == null) return;

        int amountToTransfer = mode == LootTransferQuantityMode.FullStack ? int.MaxValue : 1;

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
            Debug.LogWarning($"[LobbyStashPresenter] Transfer failed: {result}");
        }
    }

    private void OnPreparedEquipmentAssignmentRequested(LootId lootId, EquipmentSlot slot)
    {
        if (_loadoutService == null) return;
        StashOperationResult result = _loadoutService.TryAssignPreparedEquipment(
            _localProfileId,
            slot,
            lootId);
        if (result != StashOperationResult.Success)
        {
            Debug.LogWarning($"[LobbyStashPresenter] Prepared equipment assignment failed: {result}");
        }
    }

    private void OnPreparedEquipmentClearRequested(EquipmentSlot slot)
    {
        if (_loadoutService == null) return;
        StashOperationResult result = _loadoutService.TryClearPreparedEquipment(_localProfileId, slot);
        if (result != StashOperationResult.Success)
        {
            Debug.LogWarning($"[LobbyStashPresenter] Prepared equipment clear failed: {result}");
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
