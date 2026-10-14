using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// The Presenter component of the MVP pattern for the Lobby Stash and Loadout.
/// Bridges the UI view and the Stash/Loadout Services, isolating logic from rendering.
/// It turns user requests into <see cref="StashTransferCoordinator"/> actions: the coordinator owns
/// the rules, the remote calls and the serialized queue, so this class only binds events and
/// refreshes the view when the confirmed profile changes.
/// </summary>
public class LobbyStashPresenter : MonoBehaviour
{
    [SerializeField] private LobbyStashUI _stashUI;
    [SerializeField] private LootDefinitionCatalog _lootCatalog;
    private IPlayerStashService _stashService;
    private IPlayerLoadoutService _loadoutService;
    private ApplicationStashContext _context;
    private StashTransferCoordinator _coordinator;
    private ProfileId _coordinatorProfileId;
    private IPlayerStashService _coordinatorStashService;
    private IPlayerLoadoutService _coordinatorLoadoutService;
    private IRemoteInventoryService _coordinatorRemote;
    private IProfileReconciliation _coordinatorReconciliation;
    private ProfileId _localProfileId;
    private readonly List<RaidInventorySlotData> _preparedProjection = new();
    private DragPayload _activeDrag = DragPayload.Empty;
    private Func<bool> _canMutateEquipment = AllowEquipmentMutation;

    /// <summary>
    /// Supplies the Town Equipment gate (closed while the player is Ready). Without one, Equipment
    /// changes are always allowed. The owner of the screen calls this before it opens.
    /// </summary>
    public void SetEquipmentGate(Func<bool> canMutateEquipment)
    {
        _canMutateEquipment = canMutateEquipment ?? AllowEquipmentMutation;
    }

    private static bool AllowEquipmentMutation() => true;

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
            ResolveCoordinator();
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

    /// <summary>
    /// Returns the coordinator for the current profile and services. It is kept across closing and
    /// reopening the screen and only rebuilt when a dependency instance changes (a logout replaces
    /// the services), because a new instance would start a second queue while a transfer from the
    /// previous opening is still waiting for the backend.
    /// </summary>
    private StashTransferCoordinator ResolveCoordinator()
    {
        if (!_localProfileId.IsValid || _stashService == null || _loadoutService == null || _lootCatalog == null)
        {
            Debug.LogWarning(
                "[LobbyStashPresenter] The profile, its services or the loot catalog are unavailable. " +
                "Stash actions are disabled.");
            _coordinator = null;
            return null;
        }

        IRemoteInventoryService remote = _context.GetComponent<IRemoteInventoryService>();
        IProfileReconciliation reconciliation = _context.GetComponent<IProfileReconciliation>();
        if (_coordinator != null &&
            _coordinatorProfileId == _localProfileId &&
            ReferenceEquals(_coordinatorStashService, _stashService) &&
            ReferenceEquals(_coordinatorLoadoutService, _loadoutService) &&
            ReferenceEquals(_coordinatorRemote, remote) &&
            ReferenceEquals(_coordinatorReconciliation, reconciliation))
        {
            return _coordinator;
        }

        var equipment = new TownEquipmentMutationEndpoint(
            _loadoutService,
            _localProfileId,
            _lootCatalog,
            () => _canMutateEquipment());
        _coordinator = new StashTransferCoordinator(
            _localProfileId,
            _stashService,
            _loadoutService,
            remote,
            reconciliation,
            equipment);
        _coordinatorProfileId = _localProfileId;
        _coordinatorStashService = _stashService;
        _coordinatorLoadoutService = _loadoutService;
        _coordinatorRemote = remote;
        _coordinatorReconciliation = reconciliation;
        return _coordinator;
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
            Run(coordinator => coordinator.EquipAsync(payload.LootId, targetEquipmentSlot, fromStash: true));
        }
    }

    private void OnTakeAllRequested() => Run(coordinator => coordinator.TakeAllAsync());

    private void OnLeaveAllRequested() => Run(coordinator => coordinator.LeaveAllAsync());

    private void OnTransferRequested(LootId lootId, bool isFromStash, LootTransferQuantityMode mode) =>
        Run(coordinator => coordinator.TransferAsync(lootId, isFromStash, mode));

    private void OnPreparedEquipmentAssignmentRequested(LootId lootId, EquipmentSlot slot) =>
        Run(coordinator => coordinator.EquipAsync(lootId, slot, fromStash: false));

    private void OnPreparedEquipmentClearRequested(EquipmentSlot slot) =>
        Run(coordinator => coordinator.UnequipAsync(slot));

    /// <summary>Queues one action on the coordinator and reports, without throwing, how it ended.</summary>
    private void Run(Func<StashTransferCoordinator, Task<StashTransferOutcome>> action)
    {
        if (_coordinator == null) return;
        _ = ObserveAsync(action(_coordinator));
    }

    private static async Task ObserveAsync(Task<StashTransferOutcome> action)
    {
        try
        {
            StashTransferOutcome outcome = await action;
            if (!outcome.IsSuccess)
            {
                Debug.LogWarning($"[{nameof(LobbyStashPresenter)}] Stash action did not complete: {outcome}.");
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
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
