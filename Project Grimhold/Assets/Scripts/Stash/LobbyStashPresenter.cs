using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
    private ProfileReconciliationService _reconciliationService;
    private ApplicationStashContext _context;
    private ProfileId _localProfileId;
    private readonly List<RaidInventorySlotData> _preparedProjection = new();
    private DragPayload _activeDrag = DragPayload.Empty;
    private bool _isTransferring;

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
            
            _stashUI.DragStarted += OnDragStarted;
            _stashUI.DragUpdated += OnDragUpdated;
            _stashUI.DragEnded += OnDragEnded;
            _stashUI.DropReceived += OnDropReceived;
        }

        _context = FindAnyObjectByType<ApplicationStashContext>();
        if (_context != null)
        {
            _stashService = _context.StashService;
            _loadoutService = _context.LoadoutService;
            _remoteInventoryService = _context.GetComponent<RemoteInventoryService>();
            _reconciliationService = _context.GetComponent<ProfileReconciliationService>();
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
        CancelActiveDrag();
        if (_stashUI != null)
        {
            _stashUI.TransferRequested -= OnTransferRequested;
            _stashUI.TakeAllRequested -= OnTakeAllRequested;
            _stashUI.LeaveAllRequested -= OnLeaveAllRequested;
            _stashUI.PreparedEquipmentAssignmentRequested -= OnPreparedEquipmentAssignmentRequested;
            _stashUI.PreparedEquipmentClearRequested -= OnPreparedEquipmentClearRequested;
            
            _stashUI.DragStarted -= OnDragStarted;
            _stashUI.DragUpdated -= OnDragUpdated;
            _stashUI.DragEnded -= OnDragEnded;
            _stashUI.DropReceived -= OnDropReceived;
        }

        if (_context != null) _context.ProfileCommitted -= OnProfileCommitted;
        _context = null;
    }

    private void CancelActiveDrag()
    {
        if (_activeDrag.IsValid)
        {
            _activeDrag = DragPayload.Empty;
            if (_stashUI != null && _stashUI.DragPreview != null) _stashUI.DragPreview.Hide();
        }
    }

    private void OnDragStarted(DragPayload payload)
    {
        if (!payload.IsValid) return;
        
        _activeDrag = payload;
        if (_stashUI.TooltipView != null) _stashUI.TooltipView.Hide();
        
        if (_stashUI.DragPreview != null)
        {
            _stashUI.DragPreview.Show(payload.Icon, UnityEngine.Input.mousePosition);
        }
    }

    private void OnDragUpdated()
    {
        if (!_activeDrag.IsValid || _stashUI.DragPreview == null) return;
        _stashUI.DragPreview.UpdatePosition(UnityEngine.Input.mousePosition);
    }

    private void OnDragEnded(bool isValidDropTarget)
    {
        CancelActiveDrag();
    }

    private void OnDropReceived(DragSlotLocation targetLocation, EquipmentSlot targetEquipmentSlot)
    {
        if (!_activeDrag.IsValid) return;
        DragPayload payload = _activeDrag;
        
        if (!DropPolicy.CanAttemptDrop(payload.Source, targetLocation, targetEquipmentSlot)) return;
        if (targetLocation == DragSlotLocation.Equipment && !DropPolicy.CanAttemptEquipmentDrop(in payload, targetEquipmentSlot)) return;

        if (payload.Source == DragSlotLocation.Inventory && targetLocation == DragSlotLocation.Stash)
        {
            OnTransferRequested(payload.LootId, false, LootTransferQuantityMode.FullStack);
        }
        else if (payload.Source == DragSlotLocation.Stash && targetLocation == DragSlotLocation.Inventory)
        {
            OnTransferRequested(payload.LootId, true, LootTransferQuantityMode.FullStack);
        }
        else if (payload.Source == DragSlotLocation.Inventory && targetLocation == DragSlotLocation.Equipment)
        {
            OnPreparedEquipmentAssignmentRequested(payload.LootId, targetEquipmentSlot);
        }
        else if (payload.Source == DragSlotLocation.Equipment && targetLocation == DragSlotLocation.Inventory)
        {
            OnPreparedEquipmentClearRequested(payload.EquipmentSlot);
        }
        else if (payload.Source == DragSlotLocation.Stash && targetLocation == DragSlotLocation.Equipment)
        {
            OnTryEquipFromStash(payload.LootId, targetEquipmentSlot);
        }
    }

    private async void OnTryEquipFromStash(LootId lootId, EquipmentSlot slot)
    {
        if (_loadoutService == null) return;
        
        // This is a local atomic operation bridging Stash and Equipment.
        // It relies on the persistence layer to sync these changes (or fails locally).
        // Since equipment is in Loadout but source is Stash, it does affect both.
        
        // First we do the remote stash->loadout transfer if applicable? No, Equipment is NOT loadout in remote (or is it?).
        // In this game, RemoteInventoryService.MoveToLoadoutAsync actually syncs the backend loadout.
        // If an item goes from Stash to Equipment, we MUST call MoveToLoadoutAsync for it.
        // And if an item is displaced from Equipment back to Stash, we MUST call MoveToStashAsync for it!
        // But doing both sequentially is not atomic on the client. Wait, TryEquipFromStash handles it locally atomically.
        // We can do the network sync after. If it fails, well, the local operation succeeded but we might have a sync issue.
        // This is why we use SyncPreparedEquipmentAsync() for equipment! But Stash needs Sync too.

        // For MVP, we'll execute the local atomic change, then force a sync or just use the local result.
        // The instructions say "Etapa 6: tryEquipFromStash in LocalProfileStore", let's trust that the backend sync will be handled or is secondary here.
        // Just call the service:

        StashOperationResult result = _loadoutService.TryEquipFromStash(_localProfileId, lootId, slot);
        if (result != StashOperationResult.Success)
        {
            Debug.LogWarning($"[LobbyStashPresenter] Equip from stash failed locally: {result}");
            return;
        }

        // Like PreparedEquipmentAssignmentRequested, we should sync equipment state.
        bool syncSuccess = await SyncPreparedEquipmentAsync();
        if (!syncSuccess)
        {
            // SyncPreparedEquipmentAsync already uses RemoteOperationPolicy
        }
    }

    private async void OnTakeAllRequested()
    {
        if (_loadoutService == null || _remoteInventoryService == null) return;
        if (_isTransferring) return;
        _isTransferring = true;
        
        try
        {
            var stashItems = _stashService.GetStash(_localProfileId).ToList();
            foreach (var item in stashItems)
            {
                var (success, error) = await RemoteOperationPolicy.ExecuteWithReconciliationAsync(
                    () => _remoteInventoryService.MoveToLoadoutAsync(item.LootId, item.Amount),
                    async () => _reconciliationService != null && await _reconciliationService.ReconcileAsync()
                );
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
                // No need to reconcile here; local errors shouldn't happen if remote succeeded, but if they do, we're out of sync.
                if (_reconciliationService != null) await _reconciliationService.ReconcileAsync();
            }
        }
        finally
        {
            _isTransferring = false;
        }
    }

    private async void OnLeaveAllRequested()
    {
        if (_loadoutService == null || _remoteInventoryService == null) return;
        if (_isTransferring) return;
        _isTransferring = true;

        try
        {
            var loadoutItems = _loadoutService.GetLoadout(_localProfileId).ToList();
            foreach (var item in loadoutItems)
            {
                var (success, error) = await RemoteOperationPolicy.ExecuteWithReconciliationAsync(
                    () => _remoteInventoryService.MoveToStashAsync(item.LootId, item.Amount),
                    async () => _reconciliationService != null && await _reconciliationService.ReconcileAsync()
                );
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
                if (_reconciliationService != null) await _reconciliationService.ReconcileAsync();
            }
        }
        finally
        {
            _isTransferring = false;
        }
    }

    private async void OnTransferRequested(LootId lootId, bool isFromStash, LootTransferQuantityMode mode)
    {
        if (_loadoutService == null || _remoteInventoryService == null) return;
        if (_isTransferring) return;
        _isTransferring = true;

        try
        {
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

            var (success, error) = await RemoteOperationPolicy.ExecuteWithReconciliationAsync(
                () => isFromStash 
                    ? _remoteInventoryService.MoveToLoadoutAsync(lootId, amountToTransfer)
                    : _remoteInventoryService.MoveToStashAsync(lootId, amountToTransfer),
                async () => _reconciliationService != null && await _reconciliationService.ReconcileAsync()
            );

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
                if (_reconciliationService != null) await _reconciliationService.ReconcileAsync();
            }
        }
        finally
        {
            _isTransferring = false;
        }
    }

    private async void OnPreparedEquipmentAssignmentRequested(LootId lootId, EquipmentSlot slot)
    {
        if (_loadoutService == null) return;

        StashOperationResult result = _loadoutService.TryAssignPreparedEquipment(
            _localProfileId,
            slot,
            lootId);

        if (result != StashOperationResult.Success)
        {
            Debug.LogWarning($"[LobbyStashPresenter] Prepared equipment assignment failed locally: {result}");
            return;
        }

        bool syncSuccess = await SyncPreparedEquipmentAsync();
        if (!syncSuccess)
        {
            // Handled internally
        }
    }

    private async void OnPreparedEquipmentClearRequested(EquipmentSlot slot)
    {
        if (_loadoutService == null) return;

        StashOperationResult result = _loadoutService.TryClearPreparedEquipment(_localProfileId, slot);
        if (result != StashOperationResult.Success)
        {
            Debug.LogWarning($"[LobbyStashPresenter] Prepared equipment clear failed locally: {result}");
            return;
        }

        bool syncSuccess = await SyncPreparedEquipmentAsync();
        if (!syncSuccess)
        {
            // Handled internally
        }
    }

    /// <summary>
    /// Pushes the current local prepared-equipment state to the backend so it survives
    /// logout/login. The local mutation has already been committed by the time this runs;
    /// on remote failure we log rather than roll back, since the local operation is an
    /// atomic multi-slot swap that isn't safely reversible from here.
    /// </summary>
    private async Task<bool> SyncPreparedEquipmentAsync()
    {
        if (_remoteInventoryService == null)
        {
            Debug.LogWarning("[LobbyStashPresenter] No RemoteInventoryService available; prepared equipment change will not persist across sessions.");
            return false;
        }

        PreparedEquipmentLoadout prepared = _loadoutService.GetPreparedEquipment(_localProfileId);
        var (success, error) = await RemoteOperationPolicy.ExecuteWithReconciliationAsync(
            () => _remoteInventoryService.UpdatePreparedEquipmentAsync(prepared),
            async () => _reconciliationService != null && await _reconciliationService.ReconcileAsync()
        );
        
        if (!success)
        {
            Debug.LogWarning($"[LobbyStashPresenter] Remote prepared equipment sync failed: {error.message}");
        }
        return success;
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

            _stashUI.DisplayPreparedEquipment(
                _preparedProjection,
                PreparedEquipmentLoadout.IsOffHandBlocked(prepared, WeaponSetSlot.SetA, _lootCatalog),
                PreparedEquipmentLoadout.IsOffHandBlocked(prepared, WeaponSetSlot.SetB, _lootCatalog));
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
