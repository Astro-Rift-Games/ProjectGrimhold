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
    private ProfileId _localProfileId;

    private void OnEnable()
    {
        _localProfileId = LocalProfileProvider.GetOrCreateLocalProfile();

        if (_stashUI != null)
        {
            _stashUI.TransferRequested += OnTransferRequested;
            _stashUI.TakeAllRequested += OnTakeAllRequested;
            _stashUI.LeaveAllRequested += OnLeaveAllRequested;
        }

        var context = FindAnyObjectByType<ApplicationStashContext>();
        if (context != null)
        {
            _stashService = context.StashService;
            _loadoutService = context.LoadoutService;

            if (_stashService != null)
            {
                _stashService.StashChanged += OnStashChanged;
            }
            if (_loadoutService != null)
            {
                _loadoutService.LoadoutChanged += OnLoadoutChanged;
            }
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
        }

        if (_stashService != null)
        {
            _stashService.StashChanged -= OnStashChanged;
        }
        if (_loadoutService != null)
        {
            _loadoutService.LoadoutChanged -= OnLoadoutChanged;
        }
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

    private void OnStashChanged(ProfileId updatedProfileId)
    {
        if (updatedProfileId.Value == _localProfileId.Value)
        {
            RefreshUI();
        }
    }

    private void OnLoadoutChanged(ProfileId updatedProfileId)
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
                var slotData = RaidInventorySlotData.Create(entry, definition, null);
                presentationData.Add(slotData);
            }
        }
        return presentationData;
    }
}
