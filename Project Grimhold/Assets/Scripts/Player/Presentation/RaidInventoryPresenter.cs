using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// Orchestrates the local personal-inventory and confirmed container-looting screen.
/// It observes replicated snapshots and sends single-unit or full-stack intentions without mutating gameplay state.
/// </summary>
[DisallowMultipleComponent]
public sealed class RaidInventoryPresenter : MonoBehaviour
{
    private enum ScreenMode
    {
        Closed,
        Personal,
        ContainerLoot
    }

    [SerializeField]
    private RaidInventoryView _view;

    [SerializeField]
    private LootDefinitionCatalog _lootCatalog;

    [SerializeField]
    private PlayerInteractionConfig _interactionConfig;

    private readonly RaidLootPanelPresenter _playerPanelPresenter = new();
    private readonly RaidLootPanelPresenter _containerPanelPresenter = new();
    private readonly RaidLootSelectionState _playerSelection = new();
    private readonly RaidLootSelectionState _containerSelection = new();
    private readonly RaidLootTakeAllState _takeAllState = new();
    private readonly LootDropContextActionProvider _dropActionProvider = new();
    private readonly LootConsumeContextActionProvider _consumeActionProvider = new();
    private readonly LootEquipContextActionProvider _equipActionProvider = new();
    private readonly TownLootEquipContextActionProvider _townEquipActionProvider = new();
    private readonly EquipmentUnequipContextActionProvider _unequipActionProvider = new();
    private readonly List<ILootContextActionProvider> _contextActionProviders = new();
    private readonly List<LootContextActionDescriptor> _contextActions = new();

    private IInventoryReadSource _inventorySource;
    private IPreparedEquipmentReadSource _preparedEquipmentSource;
    private ITownEquipmentMutationEndpoint _townEquipmentEndpoint;
    private PlayerLootReceiver _lootReceiver;
    private PlayerInputReader _inputReader;
    private PlayerInteractionNetworkController _interactionController;
    private PlayerLootTransferNetworkController _transferController;
    private PlayerLootDropNetworkController _dropController;
    private PlayerConsumableNetworkController _consumableController;
    private PlayerWeaponEquipmentNetworkController _equipmentController;
    private NetworkRunner _runner;
    private Transform _localPlayerTransform;
    private EntityRegistry _registry;
    private IDisposable _inputSuppression;
    private ScreenMode _mode;
    private bool _isBound;
    private bool _isSubscribed;
    private int _lastObservedInteractionSequence;
    private int _observedPlayerLootSequence;
    private int _observedContainerLootSequence;
    private int _observedEquipmentRevision;
    private RaidInventorySlotData[] _equipmentSlotData;
    private bool _playerValueRefreshPending;
    private bool _playerValueFailureReported;
    private bool _takeAllHadFailure;
    private string _takeAllLastFailureMessage;
    private LootContextActionContext _contextActionContext;
    private bool _gameplayMutationsBlocked;
    private bool _isRaidBinding;
    private bool _observedTownEquipmentCanMutate;
    private bool _isEquipmentContext;
    private EquipmentSlot _equipmentContextSlot;

    private NetworkId _containerNetworkId;
    private NetworkObject _containerNetworkObject;
    private NetworkLootContainer _container;
    private NetworkLootContainerInteractable _containerInteractable;
    private Collider2D[] _containerColliders = Array.Empty<Collider2D>();

    public bool IsOpen => _mode != ScreenMode.Closed;
    public bool GameplayMutationsBlocked => _gameplayMutationsBlocked;

    /// <summary>
    /// Blocks every local inventory mutation entry point for a defeated participant.
    /// The block is presentation-local and does not replace authoritative validation.
    /// </summary>
    public void SetGameplayMutationsBlocked(bool blocked)
    {
        if (_gameplayMutationsBlocked == blocked)
        {
            return;
        }

        _gameplayMutationsBlocked = blocked;
        if (blocked)
        {
            CancelTakeAll();
            Close();
        }
    }

    public void Bind(
        PlayerLootReceiver lootReceiver,
        PlayerInputReader inputReader,
        PlayerInteractionNetworkController interactionController,
        PlayerLootTransferNetworkController transferController,
        PlayerLootDropNetworkController dropController,
        PlayerConsumableNetworkController consumableController,
        PlayerWeaponEquipmentNetworkController equipmentController,
        NetworkRunner runner,
        Transform localPlayerTransform)
    {
        Unbind();

        if (lootReceiver == null || inputReader == null || interactionController == null ||
            transferController == null || dropController == null || consumableController == null ||
            equipmentController == null || runner == null ||
            localPlayerTransform == null ||
            _view == null || _view.PlayerPanel == null || _view.ContainerPanel == null ||
            _view.ContextMenu == null || _lootCatalog == null || _interactionConfig == null)
        {
            Debug.LogError($"{nameof(RaidInventoryPresenter)} has missing binding or serialized dependencies.", this);
            return;
        }

        _inventorySource = lootReceiver;
        _lootReceiver = lootReceiver;
        _inputReader = inputReader;
        _interactionController = interactionController;
        _transferController = transferController;
        _dropController = dropController;
        _consumableController = consumableController;
        _equipmentController = equipmentController;
        _dropActionProvider.Bind(dropController);
        _consumeActionProvider.Bind(consumableController);
        _equipActionProvider.Bind(equipmentController);
        _contextActionProviders.Clear();
        _contextActionProviders.Add(_dropActionProvider);
        _contextActionProviders.Add(_consumeActionProvider);
        _contextActionProviders.Add(_equipActionProvider);
        _runner = runner;
        _localPlayerTransform = localPlayerTransform;
        _registry = runner.GetComponent<EntityRegistry>();
        if (_registry == null)
        {
            Debug.LogError($"{nameof(RaidInventoryPresenter)} could not resolve {nameof(EntityRegistry)} from the runner.", this);
            ClearBindingReferences();
            return;
        }

        _isRaidBinding = true;
        _view.SetEquipmentPanelVisible(true);
        _isBound = true;
        if (isActiveAndEnabled)
        {
            Subscribe();
            RefreshPlayerPanel();
            RefreshEquipmentSlots();
            Close();
        }
    }

    /// <summary>
    /// Binds the existing personal inventory screen to confirmed persistent Town state.
    /// Containers and Raid-only mutation endpoints remain unavailable.
    /// </summary>
    public void BindTown(
        IInventoryReadSource inventorySource,
        IPreparedEquipmentReadSource preparedEquipmentSource,
        ITownEquipmentMutationEndpoint equipmentEndpoint,
        PlayerInputReader inputReader)
    {
        Unbind();

        if (inventorySource == null || preparedEquipmentSource == null || equipmentEndpoint == null ||
            inputReader == null || _view == null || _view.PlayerPanel == null ||
            _view.ContextMenu == null || _lootCatalog == null)
        {
            Debug.LogError($"{nameof(RaidInventoryPresenter)} has missing Town binding or serialized dependencies.", this);
            return;
        }

        _inventorySource = inventorySource;
        _preparedEquipmentSource = preparedEquipmentSource;
        _townEquipmentEndpoint = equipmentEndpoint;
        _inputReader = inputReader;
        _townEquipActionProvider.Bind(equipmentEndpoint);
        _contextActionProviders.Clear();
        _contextActionProviders.Add(_townEquipActionProvider);
        _isRaidBinding = false;
        _observedTownEquipmentCanMutate = equipmentEndpoint.CanMutate;
        _view.SetContainerPanelVisible(false);
        _view.SetEquipmentPanelVisible(true);
        _isBound = true;

        if (isActiveAndEnabled)
        {
            Subscribe();
            RefreshPlayerPanel();
            RefreshEquipmentSlots();
            Close();
        }
    }

    public void Unbind()
    {
        Unsubscribe();
        Close();
        _playerPanelPresenter.Clear();
        _containerPanelPresenter.Clear();
        _view?.ClearContent();
        _observedPlayerLootSequence = 0;
        _playerValueRefreshPending = false;
        _playerValueFailureReported = false;
        _lastObservedInteractionSequence = 0;
        _gameplayMutationsBlocked = false;
        _isRaidBinding = false;
        _observedTownEquipmentCanMutate = false;
        _isBound = false;
        ClearBindingReferences();
    }

    public void Close()
    {
        _mode = ScreenMode.Closed;
        HideContextMenu();
        ClearContainerBinding();
        _view?.HideTransferFeedback();
        _view?.SetContainerPanelVisible(false);
        _view?.SetScreenVisible(false);
        ReleaseInputSuppression();
    }

    private void OnEnable()
    {
        if (!_isBound)
        {
            return;
        }

        Subscribe();
        RefreshPlayerPanel();
        RefreshEquipmentSlots();
        Close();
    }

    private void OnDisable()
    {
        Unsubscribe();
        Close();
    }

    private void OnDestroy()
    {
        Unbind();
    }

    private void Update()
    {
        if (!_isBound || _inventorySource == null)
        {
            return;
        }

        if (_playerValueRefreshPending)
        {
            RetryPlayerValue();
        }

        if (!_isRaidBinding)
        {
            bool canMutate = _townEquipmentEndpoint != null && _townEquipmentEndpoint.CanMutate;
            if (canMutate != _observedTownEquipmentCanMutate)
            {
                _observedTownEquipmentCanMutate = canMutate;
                HideContextMenu();
                RefreshPlayerPanel();
                RefreshEquipmentSlots();
            }
            return;
        }

        if (_equipmentController != null &&
            _equipmentController.ObservedEquipmentRevision != _observedEquipmentRevision)
        {
            RefreshEquipmentSlots();
        }

        if (_mode != ScreenMode.ContainerLoot)
        {
            return;
        }

        if (!IsContainerBindingValidAndInRange())
        {
            Close();
            return;
        }

        if (_container.LootChangeSequence != _observedContainerLootSequence)
        {
            RefreshContainerPanel();
        }
    }

    private void Subscribe()
    {
        if (_isSubscribed)
        {
            return;
        }

        _lastObservedInteractionSequence = _isRaidBinding
            ? _interactionController.CurrentInteractionSequence
            : 0;
        _inventorySource.Changed += OnInventorySourceChanged;
        _inputReader.InventoryToggleRequested += OnInventoryToggleRequested;
        _inputReader.InventoryCloseRequested += OnInventoryCloseRequested;
        if (_contextActionProviders.Count > 0)
        {
            _view.PlayerPanel.ContextRequested += OnPlayerSlotContextRequested;
            _view.ContextMenu.ActionRequested += OnContextActionRequested;
            _view.ContextMenu.DismissRequested += OnContextMenuDismissRequested;
        }
        if (_equipmentController != null || _preparedEquipmentSource != null)
        {
            _view.EquipmentUnequipRequested += OnEquipmentUnequipRequested;
            _view.EquipmentContextRequested += OnEquipmentSlotContextRequested;
        }
        if (!_isRaidBinding)
        {
            _isSubscribed = true;
            return;
        }

        _inputReader.InteractPressedLocally += OnInteractPressedLocally;
        _interactionController.InteractionResolved += OnInteractionResolved;
        _transferController.RequestInFlightChanged += OnRequestInFlightChanged;
        _transferController.TransportRejected += OnTransportRejected;
        _transferController.TransferConfirmed += OnTransferConfirmed;
        _dropController.RequestInFlightChanged += OnDropRequestInFlightChanged;
        _dropController.TransportRejected += OnDropTransportRejected;
        _dropController.DropConfirmed += OnDropConfirmed;
        _consumableController.ConsumeConfirmed += OnConsumeConfirmed;
        _consumableController.ConsumeRejected += OnConsumeRejected;
        _equipmentController.EquipRequestResolved += OnEquipRequestResolved;
        _view.PlayerPanel.SelectionRequested += OnPlayerSlotSelected;
        _view.ContainerPanel.SelectionRequested += OnContainerSlotSelected;
        _view.TakeAllRequested += OnTakeAllRequested;
        _isSubscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_isSubscribed)
        {
            return;
        }

        if (_inputReader != null)
        {
            _inputReader.InventoryToggleRequested -= OnInventoryToggleRequested;
            _inputReader.InventoryCloseRequested -= OnInventoryCloseRequested;
            _inputReader.InteractPressedLocally -= OnInteractPressedLocally;
        }

        if (_inventorySource != null)
        {
            _inventorySource.Changed -= OnInventorySourceChanged;
        }

        if (_view != null && _view.PlayerPanel != null)
        {
            _view.PlayerPanel.ContextRequested -= OnPlayerSlotContextRequested;
        }

        if (_view != null)
        {
            _view.EquipmentUnequipRequested -= OnEquipmentUnequipRequested;
            _view.EquipmentContextRequested -= OnEquipmentSlotContextRequested;
            if (_view.ContextMenu != null)
            {
                _view.ContextMenu.ActionRequested -= OnContextActionRequested;
                _view.ContextMenu.DismissRequested -= OnContextMenuDismissRequested;
            }
        }

        if (!_isRaidBinding)
        {
            _isSubscribed = false;
            return;
        }

        if (_interactionController != null)
        {
            _interactionController.InteractionResolved -= OnInteractionResolved;
        }

        if (_transferController != null)
        {
            _transferController.RequestInFlightChanged -= OnRequestInFlightChanged;
            _transferController.TransportRejected -= OnTransportRejected;
            _transferController.TransferConfirmed -= OnTransferConfirmed;
        }

        if (_dropController != null)
        {
            _dropController.RequestInFlightChanged -= OnDropRequestInFlightChanged;
            _dropController.TransportRejected -= OnDropTransportRejected;
            _dropController.DropConfirmed -= OnDropConfirmed;
        }

        if (_consumableController != null)
        {
            _consumableController.ConsumeConfirmed -= OnConsumeConfirmed;
            _consumableController.ConsumeRejected -= OnConsumeRejected;
        }

        if (_equipmentController != null)
        {
            _equipmentController.EquipRequestResolved -= OnEquipRequestResolved;
        }

        if (_view != null && _view.PlayerPanel != null)
        {
            _view.PlayerPanel.SelectionRequested -= OnPlayerSlotSelected;
        }

        if (_view != null && _view.ContainerPanel != null)
        {
            _view.ContainerPanel.SelectionRequested -= OnContainerSlotSelected;
        }

        if (_view != null)
        {
            _view.TakeAllRequested -= OnTakeAllRequested;
        }

        _isSubscribed = false;
    }

    private void OnInventoryToggleRequested()
    {
        if (_gameplayMutationsBlocked)
        {
            Close();
            return;
        }

        if (_mode != ScreenMode.Closed)
        {
            Close();
            return;
        }

        OpenPersonalInventory();
    }

    private void OnInventorySourceChanged()
    {
        if (!_isBound || _inventorySource == null)
        {
            return;
        }

        RefreshPlayerPanel();
        RefreshEquipmentSlots();
    }

    private bool OnInventoryCloseRequested()
    {
        if (_mode == ScreenMode.Closed)
        {
            return false;
        }

        Close();
        return true;
    }

    private void OnInteractPressedLocally()
    {
        if (_mode != ScreenMode.ContainerLoot)
        {
            return;
        }

        Close();
    }

    private void OpenPersonalInventory()
    {
        if (!_isBound || _gameplayMutationsBlocked)
        {
            return;
        }

        HideContextMenu();
        ClearContainerBinding();
        _mode = ScreenMode.Personal;
        RefreshPlayerPanel();
        RefreshEquipmentSlots();
        EnsureInputSuppression();
        _view.SetContainerPanelVisible(false);
        _view.SetScreenVisible(true);
    }

    private void OnInteractionResolved(InteractionPresentationEvent interactionEvent)
    {
        if (!_isBound || _gameplayMutationsBlocked ||
            interactionEvent.Sequence <= _lastObservedInteractionSequence)
        {
            return;
        }

        _lastObservedInteractionSequence = interactionEvent.Sequence;
        if (_lootReceiver == null || interactionEvent.InteractorId != _lootReceiver.Id ||
            !interactionEvent.Success || interactionEvent.TargetId.Value == 0)
        {
            return;
        }

        TryOpenConfirmedContainer(interactionEvent.TargetId);
    }

    private void TryOpenConfirmedContainer(EntityId targetId)
    {
        if (_gameplayMutationsBlocked)
        {
            return;
        }

        HideContextMenu();
        var networkId = new NetworkId { Raw = unchecked((uint)targetId.Value) };
        if (!_runner.TryFindObject(networkId, out NetworkObject networkObject) || networkObject == null ||
            networkObject.Id.Raw != networkId.Raw)
        {
            return;
        }

        NetworkLootContainer container = networkObject.GetComponent<NetworkLootContainer>();
        NetworkLootContainerInteractable interactable = networkObject.GetComponent<NetworkLootContainerInteractable>();
        if (container == null || interactable == null ||
            !ReferenceEquals(container.Object, networkObject) ||
            !ReferenceEquals(interactable.Object, networkObject) ||
            container.Id != targetId || interactable.Id != targetId ||
            !_registry.TryGetInteractable(targetId, out IInteractable registered) ||
            !ReferenceEquals(registered, interactable) ||
            !container.IsInitialized || !container.IsAvailable)
        {
            return;
        }

        Collider2D[] colliders = networkObject.GetComponentsInChildren<Collider2D>(true);
        if (colliders == null || colliders.Length == 0)
        {
            return;
        }

        ClearContainerBinding();
        _containerNetworkId = networkId;
        _containerNetworkObject = networkObject;
        _container = container;
        _containerInteractable = interactable;
        _containerColliders = colliders;
        _mode = ScreenMode.ContainerLoot;
        _observedContainerLootSequence = container.LootChangeSequence;

        RefreshPlayerPanel();
        RefreshContainerPanel();
        EnsureInputSuppression();
        _view.SetContainerPanelVisible(true);
        _view.SetScreenVisible(true);
    }

    private void OnContainerSlotSelected(LootId lootId, LootTransferQuantityMode quantityMode)
    {
        if (_gameplayMutationsBlocked || _mode != ScreenMode.ContainerLoot || _container == null ||
            _takeAllState.IsActive || _transferController.HasRequestInFlight ||
            !_containerSelection.TrySelect(lootId, _containerPanelPresenter.OccupiedEntries))
        {
            return;
        }

        _playerSelection.Clear();
        _view.HideTransferFeedback();
        if (!_transferController.TryRequestTransfer(
                _container.Id,
                _lootReceiver.Id,
                lootId,
                quantityMode))
        {
            _containerSelection.Reconcile(_containerPanelPresenter.OccupiedEntries);
            _view.ShowTransferFeedback("No se pudo solicitar la transferencia");
        }

        RefreshTransferInteraction();
    }

    private void OnPlayerSlotSelected(LootId lootId, LootTransferQuantityMode quantityMode)
    {
        if (_gameplayMutationsBlocked || _mode != ScreenMode.ContainerLoot || _container == null ||
            _takeAllState.IsActive || _transferController.HasRequestInFlight ||
            !_playerSelection.TrySelect(lootId, _playerPanelPresenter.OccupiedEntries))
        {
            return;
        }

        _containerSelection.Clear();
        _view.HideTransferFeedback();
        if (!_transferController.TryRequestTransfer(
                _lootReceiver.Id,
                _container.Id,
                lootId,
                quantityMode))
        {
            _playerSelection.Reconcile(_playerPanelPresenter.OccupiedEntries);
            _view.ShowTransferFeedback("No se pudo solicitar la transferencia");
        }

        RefreshTransferInteraction();
    }

    private void OnPlayerSlotContextRequested(LootId lootId, RectTransform anchor)
    {
        bool contextActionsAvailable = _isRaidBinding
            ? _dropController != null && !_dropController.HasRequestInFlight &&
                _equipmentController != null && !_equipmentController.HasRequestInFlight
            : _townEquipmentEndpoint != null && _townEquipmentEndpoint.CanMutate;
        if (_gameplayMutationsBlocked || _mode != ScreenMode.Personal || !contextActionsAvailable ||
            !_playerSelection.TrySelect(lootId, _playerPanelPresenter.OccupiedEntries) ||
            !TryGetEntry(_playerPanelPresenter.OccupiedEntries, lootId, out LootEntry entry) ||
            _lootCatalog == null || !_lootCatalog.TryGet(lootId.Value, out LootDefinition definition))
        {
            HideContextMenu();
            RefreshTransferInteraction();
            return;
        }

        _contextActionContext = new LootContextActionContext(entry, definition);
        _contextActions.Clear();
        for (int i = 0; i < _contextActionProviders.Count; i++)
        {
            _contextActionProviders[i].CollectActions(_contextActionContext, _contextActions);
        }

        _view.HideTransferFeedback();
        if (!_view.ContextMenu.Show(_contextActions, anchor))
        {
            HideContextMenu();
        }

        RefreshTransferInteraction();
    }

    private void OnContextActionRequested(LootContextActionId actionId)
    {
        if (_gameplayMutationsBlocked || _mode != ScreenMode.Personal || !_contextActionContext.IsValid ||
            !TryResolveCurrentContextEntry(out LootEntry currentEntry))
        {
            HideContextMenu();
            RefreshTransferInteraction();
            return;
        }

        var currentContext = new LootContextActionContext(
            currentEntry,
            _contextActionContext.Definition);
        bool executed = false;
        for (int i = 0; i < _contextActions.Count; i++)
        {
            LootContextActionDescriptor descriptor = _contextActions[i];
            if (descriptor.Id == actionId && descriptor.IsEnabled && descriptor.Provider != null)
            {
                executed = descriptor.Provider.TryExecute(actionId, currentContext);
                break;
            }
        }

        HideContextMenu();
        if (!executed)
        {
            _view.ShowTransferFeedback("No se pudo solicitar la acción");
        }

        RefreshTransferInteraction();
    }

    private void OnContextMenuDismissRequested()
    {
        HideContextMenu();
        RefreshTransferInteraction();
    }

    private void OnDropRequestInFlightChanged(bool hasRequestInFlight)
    {
        if (hasRequestInFlight)
        {
            HideContextMenu();
        }

        RefreshTransferInteraction();
    }

    private void OnEquipRequestResolved(EquipmentOperationResult result)
    {
        RefreshPlayerPanel();
        RefreshEquipmentSlots();
        if (_mode == ScreenMode.Personal)
        {
            if (result == EquipmentOperationResult.Succeeded)
            {
                _view.HideTransferFeedback();
            }
            else
            {
                _view.ShowTransferFeedback(GetEquipFailureMessage(result));
            }
        }

        RefreshTransferInteraction();
    }

    private static string GetEquipFailureMessage(EquipmentOperationResult result)
    {
        return result switch
        {
            EquipmentOperationResult.NoFreeWeaponSlot => "Los dos slots de arma están ocupados",
            EquipmentOperationResult.SlotOccupied => "El slot ya está ocupado",
            EquipmentOperationResult.EmptySlot => "El slot está vacío",
            EquipmentOperationResult.InventoryFull => "El inventario está lleno",
            EquipmentOperationResult.ItemNotOwned => "El objeto ya no está disponible",
            EquipmentOperationResult.PlayerUnavailable => "No se puede equipar en este estado",
            EquipmentOperationResult.InvalidEquipment => "La configuración del objeto no es válida",
            EquipmentOperationResult.AttributeRequirementsNotMet =>
                "Tus atributos no cumplen los requisitos del arma",
            _ => "No se pudo completar la operación"
        };
    }

    private void OnEquipmentUnequipRequested(EquipmentSlot slot)
    {
        if (_gameplayMutationsBlocked || _mode != ScreenMode.Personal)
        {
            _view.ShowTransferFeedback("No se pudo solicitar desequipar");
            return;
        }

        bool accepted = TryRequestEquipmentUnequip(slot);
        if (!accepted)
        {
            _view.ShowTransferFeedback(
                !_isRaidBinding && _townEquipmentEndpoint != null && !_townEquipmentEndpoint.CanMutate
                    ? "No se puede modificar Equipment mientras estás Listo"
                    : "No se pudo solicitar desequipar");
            return;
        }

        _view.HideTransferFeedback();
        if (_isRaidBinding)
        {
            RefreshEquipmentSlots();
        }
    }

    private void OnEquipmentSlotContextRequested(EquipmentSlot slot, RectTransform anchor)
    {
        if (_gameplayMutationsBlocked || _mode != ScreenMode.Personal || anchor == null ||
            !TryGetEquipmentEntry(slot, out LootEntry entry) ||
            !_lootCatalog.TryGet(entry.LootId.Value, out LootDefinition definition))
        {
            HideContextMenu();
            return;
        }

        _isEquipmentContext = true;
        _equipmentContextSlot = slot;
        _contextActionContext = new LootContextActionContext(entry, definition);
        _unequipActionProvider.Bind(slot, CanRequestEquipmentUnequip, TryRequestEquipmentUnequip);
        _contextActions.Clear();
        _unequipActionProvider.CollectActions(_contextActionContext, _contextActions);
        if (!_view.ContextMenu.Show(_contextActions, anchor))
        {
            HideContextMenu();
        }
    }

    private bool CanRequestEquipmentUnequip(EquipmentSlot slot)
    {
        return _mode == ScreenMode.Personal && TryGetEquipmentEntry(slot, out _) &&
            (_isRaidBinding
                ? _equipmentController != null && !_equipmentController.HasRequestInFlight
                : _townEquipmentEndpoint != null && _townEquipmentEndpoint.CanMutate);
    }

    private bool TryRequestEquipmentUnequip(EquipmentSlot slot)
    {
        if (!CanRequestEquipmentUnequip(slot))
        {
            return false;
        }

        return _isRaidBinding
            ? _equipmentController.TryRequestUnequip(slot)
            : _townEquipmentEndpoint.TryUnequip(slot) == StashOperationResult.Success;
    }

    private bool TryGetEquipmentEntry(EquipmentSlot slot, out LootEntry entry)
    {
        entry = default;
        if (_isRaidBinding)
        {
            return _equipmentController != null && _equipmentController.TryGetSlotLoot(slot, out entry);
        }

        if (_preparedEquipmentSource == null ||
            !_preparedEquipmentSource.TryGetPreparedEquipment(out PreparedEquipmentLoadout prepared))
        {
            return false;
        }

        LootId lootId = prepared.Get(slot);
        if (!lootId.IsValid)
        {
            return false;
        }

        entry = new LootEntry(lootId, 1);
        return true;
    }

    private bool TryResolveCurrentContextEntry(out LootEntry entry)
    {
        if (_isEquipmentContext)
        {
            return TryGetEquipmentEntry(_equipmentContextSlot, out entry) &&
                entry.LootId == _contextActionContext.Entry.LootId;
        }

        return TryGetEntry(
            _playerPanelPresenter.OccupiedEntries,
            _contextActionContext.Entry.LootId,
            out entry);
    }

    private void RefreshEquipmentSlots()
    {
        if (_view == null)
        {
            return;
        }

        EquipmentSlot[] slots = EquipmentSlotRules.AllSlots;
        if (_equipmentSlotData == null || _equipmentSlotData.Length != slots.Length)
        {
            _equipmentSlotData = new RaidInventorySlotData[slots.Length];
        }

        PreparedEquipmentLoadout prepared = default;
        bool hasPreparedSnapshot = _isRaidBinding ||
            (_preparedEquipmentSource != null &&
                _preparedEquipmentSource.TryGetPreparedEquipment(out prepared));
        for (int index = 0; index < slots.Length; index++)
        {
            _equipmentSlotData[index] = hasPreparedSnapshot
                ? CreateEquipmentSlotData(slots[index], in prepared)
                : RaidInventorySlotData.Empty;
        }

        bool canUnequip = _mode == ScreenMode.Personal && !_gameplayMutationsBlocked &&
            (_isRaidBinding
                ? _equipmentController != null && !_equipmentController.HasRequestInFlight
                : _townEquipmentEndpoint != null && _townEquipmentEndpoint.CanMutate);
        WeaponSlot activeSlot = WeaponSlot.None;
        if (_isRaidBinding && _equipmentController != null)
        {
            _observedEquipmentRevision = _equipmentController.ObservedEquipmentRevision;
            activeSlot = _equipmentController.ActiveWeaponSlot;
        }
        _view.PresentEquipmentSlots(
            _equipmentSlotData,
            activeSlot,
            canUnequip);
    }

    private RaidInventorySlotData CreateEquipmentSlotData(
        EquipmentSlot slot,
        in PreparedEquipmentLoadout prepared)
    {
        LootEntry entry;
        if (_isRaidBinding)
        {
            if (_equipmentController == null || !_equipmentController.TryGetSlotLoot(slot, out entry))
            {
                return RaidInventorySlotData.Empty;
            }
        }
        else
        {
            LootId lootId = prepared.Get(slot);
            if (!lootId.IsValid)
            {
                return RaidInventorySlotData.Empty;
            }

            entry = new LootEntry(lootId, 1);
        }

        if (!entry.IsValid)
        {
            return RaidInventorySlotData.Empty;
        }

        _lootCatalog.TryGet(entry.LootId.Value, out LootDefinition definition);
        return RaidInventorySlotData.Create(entry, definition, null);
    }

    private void OnDropTransportRejected(LootTransferTransportRejectionReason reason)
    {
        if (_mode == ScreenMode.Personal)
        {
            _view.ShowTransferFeedback(GetDropTransportRejectionMessage(reason));
        }

        RefreshTransferInteraction();
    }

    private void OnDropConfirmed(LootDropConfirmation confirmation)
    {
        RefreshPlayerPanel();
        if (_mode == ScreenMode.Personal)
        {
            if (confirmation.Result.Success)
            {
                _view.HideTransferFeedback();
            }
            else
            {
                _view.ShowTransferFeedback(GetDropFailureMessage(confirmation.Result.FailureReason));
            }
        }

        RefreshTransferInteraction();
    }

    private void OnConsumeConfirmed(LootId _)
    {
        RefreshPlayerPanel();
        if (_mode == ScreenMode.Personal)
        {
            _view.HideTransferFeedback();
        }

        RefreshTransferInteraction();
    }

    private void OnConsumeRejected(ConsumableFailureReason reason)
    {
        if (_mode == ScreenMode.Personal)
        {
            string message = reason switch
            {
                ConsumableFailureReason.TargetDead => "Estás muerto.",
                ConsumableFailureReason.HealthFull => "Tu salud ya está al máximo.",
                ConsumableFailureReason.InsufficientAmount => "No tienes suficientes consumibles.",
                _ => "No se pudo usar el objeto."
            };
            _view.ShowTransferFeedback(message);
        }

        RefreshTransferInteraction();
    }

    private void OnRequestInFlightChanged(bool _)
    {
        RefreshTransferInteraction();
    }

    private void OnTransportRejected(LootTransferTransportRejectionReason reason)
    {
        if (_mode != ScreenMode.ContainerLoot)
        {
            return;
        }

        if (_takeAllState.IsAwaitingCompletion)
        {
            LootId completedLootId = _takeAllState.CurrentLootId;
            RecordTakeAllFailure(GetTransportRejectionMessage(reason));
            RefreshPlayerPanel();
            RefreshContainerPanel();
            AdvanceTakeAll(completedLootId);
            TryStartNextTakeAllTransfer();
        }
        else
        {
            _view.ShowTransferFeedback(GetTransportRejectionMessage(reason));
        }

        RefreshTransferInteraction();
    }

    private void OnTransferConfirmed(LootTransferConfirmation confirmation)
    {
        RefreshPlayerPanel();
        if (_mode == ScreenMode.ContainerLoot && _container != null &&
            (confirmation.SourceId == _container.Id || confirmation.DestinationId == _container.Id))
        {
            bool completesTakeAll = IsCurrentTakeAllConfirmation(confirmation);
            if (confirmation.Result.Success)
            {
                if (!completesTakeAll || !_takeAllHadFailure)
                {
                    _view.HideTransferFeedback();
                }
            }
            else
            {
                bool isDeposit = confirmation.DestinationId == _container.Id;
                bool isCapacityRejection =
                    confirmation.Result.FailureReason == LootTransferFailureReason.InventoryFull;
                string failureMessage = GetDirectionalTransferFailureMessage(
                    confirmation.Result.FailureReason,
                    isDeposit);
                if (isCapacityRejection)
                {
                    RaidLootPanelView destinationPanel = isDeposit
                        ? _view.ContainerPanel
                        : _view.PlayerPanel;
                    destinationPanel?.ShowCapacityRejection();
                    _view.HideTransferFeedback();
                }

                if (completesTakeAll)
                {
                    RecordTakeAllFailure(
                        isCapacityRejection ? null : failureMessage,
                        !isCapacityRejection);
                }
                else if (!isCapacityRejection)
                {
                    _view.ShowTransferFeedback(failureMessage);
                }
            }

            RefreshContainerPanel();
            if (completesTakeAll)
            {
                LootId completedLootId = _takeAllState.CurrentLootId;
                AdvanceTakeAll(completedLootId);
                TryStartNextTakeAllTransfer();
            }
        }
    }

    private void OnTakeAllRequested()
    {
        if (_gameplayMutationsBlocked || _mode != ScreenMode.ContainerLoot || _container == null ||
            _transferController == null || _transferController.HasRequestInFlight ||
            _takeAllState.IsActive ||
            !_takeAllState.TryBegin(_containerPanelPresenter.OccupiedEntries))
        {
            RefreshTransferInteraction();
            return;
        }

        _playerSelection.Clear();
        _containerSelection.Clear();
        _takeAllHadFailure = false;
        _takeAllLastFailureMessage = null;
        _view.HideTransferFeedback();
        RefreshTransferInteraction();
        TryStartNextTakeAllTransfer();
    }

    private void TryStartNextTakeAllTransfer()
    {
        if (_gameplayMutationsBlocked)
        {
            CancelTakeAll();
            return;
        }

        while (_takeAllState.IsActive && !_takeAllState.IsAwaitingCompletion)
        {
            if (_mode != ScreenMode.ContainerLoot || _container == null ||
                _lootReceiver == null || _transferController == null)
            {
                CancelTakeAll();
                break;
            }

            LootId lootId = _takeAllState.CurrentLootId;
            if (_transferController.TryRequestTransfer(
                    _container.Id,
                    _lootReceiver.Id,
                    lootId,
                    LootTransferQuantityMode.FullStack))
            {
                if (!_takeAllState.TryMarkRequestSent(lootId))
                {
                    Debug.LogError(
                        $"{nameof(RaidInventoryPresenter)} could not track an accepted take-all request.",
                        this);
                    CancelTakeAll();
                }

                break;
            }

            RecordTakeAllFailure("No se pudo solicitar la transferencia");
            AdvanceTakeAll(lootId);
        }

        RefreshTransferInteraction();
    }

    private bool IsCurrentTakeAllConfirmation(in LootTransferConfirmation confirmation)
    {
        return _takeAllState.IsAwaitingCompletion && _container != null && _lootReceiver != null &&
            confirmation.SourceId == _container.Id && confirmation.DestinationId == _lootReceiver.Id &&
            confirmation.ResolvedLootId.HasValue &&
            confirmation.ResolvedLootId.Value == _takeAllState.CurrentLootId;
    }

    private void RecordTakeAllFailure(string message, bool showText = true)
    {
        _takeAllHadFailure = true;
        _takeAllLastFailureMessage = message;
        if (showText)
        {
            _view.ShowPersistentTransferFeedback(message);
        }
    }

    private void AdvanceTakeAll(LootId completedLootId)
    {
        _takeAllState.TryAdvance(completedLootId);
        if (!_takeAllState.IsActive && _takeAllHadFailure)
        {
            if (string.IsNullOrWhiteSpace(_takeAllLastFailureMessage))
            {
                _view.HideTransferFeedback();
            }
            else
            {
                _view.ShowTransferFeedback(_takeAllLastFailureMessage);
            }
        }
    }

    private static string GetTransferFailureMessage(LootTransferFailureReason reason)
    {
        return GetDirectionalTransferFailureMessage(reason, false);
    }

    private static string GetDirectionalTransferFailureMessage(
        LootTransferFailureReason reason,
        bool isDeposit)
    {
        return reason switch
        {
            LootTransferFailureReason.InvalidLoot => "Loot no válido",
            LootTransferFailureReason.InvalidAmount => "Cantidad no válida",
            LootTransferFailureReason.SourceNotFound => isDeposit
                ? "Inventario no disponible"
                : "Contenedor no encontrado",
            LootTransferFailureReason.DestinationNotFound => isDeposit
                ? "Contenedor no encontrado"
                : "Inventario no disponible",
            LootTransferFailureReason.InsufficientAmount => "El stack ya no está disponible",
            LootTransferFailureReason.InventoryFull => isDeposit ? "Contenedor lleno" : "Inventario lleno",
            LootTransferFailureReason.OutOfRange => "Fuera de alcance",
            LootTransferFailureReason.MissingAuthority => "Transferencia sin autoridad",
            LootTransferFailureReason.ContainerUnavailable => "Contenedor no disponible",
            LootTransferFailureReason.PlayerUnavailable => "Jugador no disponible",
            LootTransferFailureReason.Overflow => "La cantidad excede el límite",
            _ => isDeposit ? "No se pudo depositar el loot" : "No se pudo retirar el loot"
        };
    }

    private static string GetTransportRejectionMessage(LootTransferTransportRejectionReason reason)
    {
        return reason switch
        {
            LootTransferTransportRejectionReason.BusyWithDifferentSequence => "Hay otra transferencia en curso",
            LootTransferTransportRejectionReason.StaleSequence => "La solicitud de transferencia venció",
            LootTransferTransportRejectionReason.DependenciesUnavailable => "Transferencia no disponible",
            _ => "No se pudo completar la transferencia"
        };
    }

    private static string GetDropFailureMessage(LootDropFailureReason reason)
    {
        return reason switch
        {
            LootDropFailureReason.PlayerUnavailable => "Jugador no disponible",
            LootDropFailureReason.InvalidLoot => "Loot no válido",
            LootDropFailureReason.InvalidAmount => "Cantidad no válida",
            LootDropFailureReason.InsufficientAmount => "El stack ya no está disponible",
            LootDropFailureReason.NoValidPosition => "No hay espacio para soltar el objeto",
            LootDropFailureReason.SpawnFailed => "No se pudo crear el pickup",
            LootDropFailureReason.MissingAuthority => "Drop sin autoridad",
            _ => "No se pudo soltar el loot"
        };
    }

    private static string GetDropTransportRejectionMessage(
        LootTransferTransportRejectionReason reason)
    {
        return reason switch
        {
            LootTransferTransportRejectionReason.BusyWithDifferentSequence =>
                "Hay otra solicitud de drop en curso",
            LootTransferTransportRejectionReason.StaleSequence =>
                "La solicitud de drop venció",
            LootTransferTransportRejectionReason.DependenciesUnavailable =>
                "El drop no está disponible",
            _ => "No se pudo solicitar el drop"
        };
    }

    private void RefreshPlayerPanel()
    {
        if (!_isBound || _inventorySource == null || _view == null || _view.PlayerPanel == null)
        {
            return;
        }

        int currentSequence = _inventorySource.Revision;
        if (currentSequence != _observedPlayerLootSequence)
        {
            _playerValueFailureReported = false;
        }

        _observedPlayerLootSequence = currentSequence;
        long value = 0;
        bool hasTotalValue = _inventorySource.TryGetLootContent(out IReadOnlyList<LootEntry> content) &&
            LootInventoryValueCalculator.TryCalculate(content, _lootCatalog, out value);
        long? totalValue = hasTotalValue ? value : null;
        _playerValueRefreshPending = !hasTotalValue;
        if (hasTotalValue)
        {
            _playerValueFailureReported = false;
        }
        else
        {
            ReportPlayerValueFailureOnce();
        }

        bool transferInteractive = _mode == ScreenMode.ContainerLoot && _container != null &&
            _container.IsInitialized && _container.IsAvailable &&
            _transferController != null && !_transferController.HasRequestInFlight &&
            !_takeAllState.IsActive;
        RaidLootSlotInteractionMode interactionMode = _mode == ScreenMode.Personal &&
            (_isRaidBinding
                ? _dropController != null && !_dropController.HasRequestInFlight &&
                    _equipmentController != null && !_equipmentController.HasRequestInFlight
                : _townEquipmentEndpoint != null && _townEquipmentEndpoint.CanMutate)
                ? RaidLootSlotInteractionMode.ContextMenu
                : transferInteractive
                    ? RaidLootSlotInteractionMode.Transfer
                    : RaidLootSlotInteractionMode.ReadOnly;
        bool refreshed = _playerPanelPresenter.Refresh(
            _inventorySource,
            _lootCatalog,
            _view.PlayerPanel,
            totalValue,
            false,
            interactionMode,
            _playerSelection.SelectedLootId,
            this);

        if (!refreshed)
        {
            _playerSelection.Clear();
            return;
        }

        _playerSelection.Reconcile(_playerPanelPresenter.OccupiedEntries);
        if (_view.ContextMenu != null && _view.ContextMenu.IsOpen && !_playerSelection.HasSelection)
        {
            HideContextMenu();
        }
    }

    private void RetryPlayerValue()
    {
        if (_inventorySource == null || _view == null || _view.PlayerPanel == null)
        {
            return;
        }

        if (!_inventorySource.TryGetLootContent(out IReadOnlyList<LootEntry> content) ||
            !LootInventoryValueCalculator.TryCalculate(content, _lootCatalog, out long value))
        {
            ReportPlayerValueFailureOnce();
            return;
        }

        _view.PlayerPanel.PresentTotalValue(value);
        _playerValueRefreshPending = false;
        _playerValueFailureReported = false;
    }

    private void ReportPlayerValueFailureOnce()
    {
        if (_playerValueFailureReported)
        {
            return;
        }

        Debug.LogError(
            $"{nameof(RaidInventoryPresenter)} could not read a complete loot value. The inventory will retry without showing a subtotal.",
            this);
        _playerValueFailureReported = true;
    }

    private void RefreshContainerPanel()
    {
        if (_container == null || _view == null || _view.ContainerPanel == null)
        {
            return;
        }

        _observedContainerLootSequence = _container.LootChangeSequence;
        bool interactive = !_transferController.HasRequestInFlight && !_takeAllState.IsActive;
        bool refreshed = _containerPanelPresenter.Refresh(
            _container,
            _container,
            _lootCatalog,
            _view.ContainerPanel,
            null,
            true,
            interactive,
            _containerSelection.SelectedLootId,
            this);

        if (!refreshed)
        {
            _containerSelection.Clear();
            return;
        }

        _containerSelection.Reconcile(_containerPanelPresenter.OccupiedEntries);
        RefreshTransferInteraction();
    }

    private void RefreshTransferInteraction()
    {
        if (_view == null || _view.PlayerPanel == null || _view.ContainerPanel == null)
        {
            return;
        }

        bool interactive = _mode == ScreenMode.ContainerLoot && _container != null &&
            _container.IsInitialized && _container.IsAvailable &&
            _transferController != null && !_transferController.HasRequestInFlight &&
            !_takeAllState.IsActive;
        RaidLootSlotInteractionMode playerMode = _mode == ScreenMode.Personal &&
            (_isRaidBinding
                ? _dropController != null && !_dropController.HasRequestInFlight &&
                    _equipmentController != null && !_equipmentController.HasRequestInFlight
                : _townEquipmentEndpoint != null && _townEquipmentEndpoint.CanMutate)
                ? RaidLootSlotInteractionMode.ContextMenu
                : interactive
                    ? RaidLootSlotInteractionMode.Transfer
                    : RaidLootSlotInteractionMode.ReadOnly;
        _view.PlayerPanel.RefreshInteraction(playerMode, _playerSelection.SelectedLootId);
        _view.ContainerPanel.RefreshInteraction(
            interactive
                ? RaidLootSlotInteractionMode.Transfer
                : RaidLootSlotInteractionMode.ReadOnly,
            _containerSelection.SelectedLootId);
        _view.SetTakeAllInteractable(
            interactive && HasTransferableEntries(_containerPanelPresenter.OccupiedEntries));
    }

    private static bool HasTransferableEntries(System.Collections.Generic.IReadOnlyList<LootEntry> entries)
    {
        if (entries == null)
        {
            return false;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].IsValid)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsContainerBindingValidAndInRange()
    {
        if (_runner == null || _containerNetworkObject == null || _container == null ||
            _containerInteractable == null || !_container.IsInitialized || !_container.IsAvailable ||
            !_runner.TryFindObject(_containerNetworkId, out NetworkObject resolved) ||
            !ReferenceEquals(resolved, _containerNetworkObject))
        {
            return false;
        }

        Vector2 playerPosition = _localPlayerTransform.position;
        float minimumDistance = float.PositiveInfinity;
        bool hasUsableCollider = false;
        for (int i = 0; i < _containerColliders.Length; i++)
        {
            Collider2D collider = _containerColliders[i];
            if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy)
            {
                continue;
            }

            hasUsableCollider = true;
            float distance = Vector2.Distance(playerPosition, collider.ClosestPoint(playerPosition));
            if (distance < minimumDistance)
            {
                minimumDistance = distance;
            }
        }

        return hasUsableCollider && minimumDistance <= _interactionConfig.MaximumDistance;
    }

    private void EnsureInputSuppression()
    {
        if (_inputSuppression == null)
        {
            _inputSuppression = _inputReader.AcquireGameplayInputSuppression();
        }
    }

    private void ReleaseInputSuppression()
    {
        _inputSuppression?.Dispose();
        _inputSuppression = null;
    }

    private void ClearContainerBinding()
    {
        CancelTakeAll();
        _playerSelection.Clear();
        _containerSelection.Clear();
        _containerPanelPresenter.Clear();
        _view?.ContainerPanel?.ClearContent();
        _containerNetworkId = default;
        _containerNetworkObject = null;
        _container = null;
        _containerInteractable = null;
        _containerColliders = Array.Empty<Collider2D>();
        _observedContainerLootSequence = 0;
    }

    private void HideContextMenu()
    {
        _view?.ContextMenu?.Hide();
        _contextActions.Clear();
        _contextActionContext = default;
        _isEquipmentContext = false;
        _equipmentContextSlot = EquipmentSlot.None;
        _unequipActionProvider.Clear();
        _playerSelection.Clear();
    }

    private static bool TryGetEntry(
        IReadOnlyList<LootEntry> entries,
        LootId lootId,
        out LootEntry entry)
    {
        if (entries != null)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].IsValid && entries[i].LootId == lootId)
                {
                    entry = entries[i];
                    return true;
                }
            }
        }

        entry = default;
        return false;
    }

    private void CancelTakeAll()
    {
        _takeAllState.Cancel();
        _takeAllHadFailure = false;
        _takeAllLastFailureMessage = null;
        _view?.SetTakeAllInteractable(false);
    }

    private void ClearBindingReferences()
    {
        _inventorySource = null;
        _preparedEquipmentSource = null;
        _townEquipmentEndpoint = null;
        _townEquipActionProvider.Bind(null);
        _lootReceiver = null;
        _inputReader = null;
        _interactionController = null;
        _transferController = null;
        _dropController = null;
        _dropActionProvider.Bind(null);
        _consumableController = null;
        _consumeActionProvider.Bind(null);
        _equipmentController = null;
        _equipActionProvider.Bind(null);
        _contextActionProviders.Clear();
        _runner = null;
        _localPlayerTransform = null;
        _registry = null;
    }
}
