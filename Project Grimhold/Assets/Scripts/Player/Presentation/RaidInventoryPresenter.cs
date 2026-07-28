using System;
using Fusion;
using UnityEngine;

/// <summary>
/// Orchestrates the local personal-inventory and confirmed container-looting screen.
/// It observes replicated snapshots and sends full-stack intentions without mutating gameplay state.
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
    private readonly RaidLootSelectionState _selection = new();

    private PlayerLootReceiver _lootReceiver;
    private PlayerInputReader _inputReader;
    private PlayerInteractionNetworkController _interactionController;
    private PlayerLootTransferNetworkController _transferController;
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
    private bool _playerValueRefreshPending;
    private bool _playerValueFailureReported;

    private NetworkId _containerNetworkId;
    private NetworkObject _containerNetworkObject;
    private NetworkLootContainer _container;
    private NetworkLootContainerInteractable _containerInteractable;
    private Collider2D[] _containerColliders = Array.Empty<Collider2D>();

    public bool IsOpen => _mode != ScreenMode.Closed;

    public void Bind(
        PlayerLootReceiver lootReceiver,
        PlayerInputReader inputReader,
        PlayerInteractionNetworkController interactionController,
        PlayerLootTransferNetworkController transferController,
        NetworkRunner runner,
        Transform localPlayerTransform)
    {
        Unbind();

        if (lootReceiver == null || inputReader == null || interactionController == null ||
            transferController == null || runner == null || localPlayerTransform == null ||
            _view == null || _view.PlayerPanel == null || _view.ContainerPanel == null ||
            _lootCatalog == null || _interactionConfig == null)
        {
            Debug.LogError($"{nameof(RaidInventoryPresenter)} has missing binding or serialized dependencies.", this);
            return;
        }

        _lootReceiver = lootReceiver;
        _inputReader = inputReader;
        _interactionController = interactionController;
        _transferController = transferController;
        _runner = runner;
        _localPlayerTransform = localPlayerTransform;
        _registry = runner.GetComponent<EntityRegistry>();
        if (_registry == null)
        {
            Debug.LogError($"{nameof(RaidInventoryPresenter)} could not resolve {nameof(EntityRegistry)} from the runner.", this);
            ClearBindingReferences();
            return;
        }

        _isBound = true;
        if (isActiveAndEnabled)
        {
            Subscribe();
            RefreshPlayerPanel();
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
        _isBound = false;
        ClearBindingReferences();
    }

    public void Close()
    {
        _mode = ScreenMode.Closed;
        ClearContainerBinding();
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
        if (!_isBound || _lootReceiver == null)
        {
            return;
        }

        if (_lootReceiver.LootChangeSequence != _observedPlayerLootSequence)
        {
            RefreshPlayerPanel();
        }
        else if (_playerValueRefreshPending)
        {
            RetryPlayerValue();
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

        _lastObservedInteractionSequence = _interactionController.CurrentInteractionSequence;
        _inputReader.InventoryToggleRequested += OnInventoryToggleRequested;
        _inputReader.InventoryCloseRequested += OnInventoryCloseRequested;
        _inputReader.InteractPressedLocally += OnInteractPressedLocally;
        _interactionController.InteractionResolved += OnInteractionResolved;
        _transferController.RequestInFlightChanged += OnRequestInFlightChanged;
        _transferController.TransferConfirmed += OnTransferConfirmed;
        _view.ContainerPanel.SelectionRequested += OnContainerSlotSelected;
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

        if (_interactionController != null)
        {
            _interactionController.InteractionResolved -= OnInteractionResolved;
        }

        if (_transferController != null)
        {
            _transferController.RequestInFlightChanged -= OnRequestInFlightChanged;
            _transferController.TransferConfirmed -= OnTransferConfirmed;
        }

        if (_view != null && _view.ContainerPanel != null)
        {
            _view.ContainerPanel.SelectionRequested -= OnContainerSlotSelected;
        }

        _isSubscribed = false;
    }

    private void OnInventoryToggleRequested()
    {
        if (_mode != ScreenMode.Closed)
        {
            Close();
            return;
        }

        OpenPersonalInventory();
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
        if (!_isBound)
        {
            return;
        }

        ClearContainerBinding();
        _mode = ScreenMode.Personal;
        RefreshPlayerPanel();
        EnsureInputSuppression();
        _view.SetContainerPanelVisible(false);
        _view.SetScreenVisible(true);
    }

    private void OnInteractionResolved(InteractionPresentationEvent interactionEvent)
    {
        if (!_isBound || interactionEvent.Sequence <= _lastObservedInteractionSequence)
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

    private void OnContainerSlotSelected(LootId lootId)
    {
        if (_mode != ScreenMode.ContainerLoot || _container == null ||
            _transferController.HasRequestInFlight ||
            !_selection.TrySelect(lootId, _containerPanelPresenter.OccupiedEntries))
        {
            return;
        }

        _transferController.TryRequestFullStack(_container.Id, lootId);
        RefreshContainerInteraction();
    }

    private void OnRequestInFlightChanged(bool _)
    {
        RefreshContainerInteraction();
    }

    private void OnTransferConfirmed(LootTransferConfirmation confirmation)
    {
        RefreshPlayerPanel();
        if (_mode == ScreenMode.ContainerLoot && _container != null && confirmation.SourceId == _container.Id)
        {
            RefreshContainerPanel();
        }
    }

    private void RefreshPlayerPanel()
    {
        if (!_isBound || _lootReceiver == null || _view == null || _view.PlayerPanel == null)
        {
            return;
        }

        int currentSequence = _lootReceiver.LootChangeSequence;
        if (currentSequence != _observedPlayerLootSequence)
        {
            _playerValueFailureReported = false;
        }

        _observedPlayerLootSequence = currentSequence;
        bool hasTotalValue = _lootReceiver.TryCalculateTotalValue(out long value);
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

        _playerPanelPresenter.Refresh(
            _lootReceiver,
            _lootReceiver,
            _lootCatalog,
            _view.PlayerPanel,
            totalValue,
            false,
            false,
            default,
            this);
    }

    private void RetryPlayerValue()
    {
        if (_lootReceiver == null || _view == null || _view.PlayerPanel == null)
        {
            return;
        }

        if (!_lootReceiver.TryCalculateTotalValue(out long value))
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
        bool interactive = !_transferController.HasRequestInFlight;
        bool refreshed = _containerPanelPresenter.Refresh(
            _container,
            _container,
            _lootCatalog,
            _view.ContainerPanel,
            null,
            true,
            interactive,
            _selection.SelectedLootId,
            this);

        if (!refreshed)
        {
            _selection.Clear();
            return;
        }

        _selection.Reconcile(_containerPanelPresenter.OccupiedEntries);
        RefreshContainerInteraction();
    }

    private void RefreshContainerInteraction()
    {
        if (_view == null || _view.ContainerPanel == null)
        {
            return;
        }

        bool interactive = _mode == ScreenMode.ContainerLoot && _container != null &&
            _container.IsInitialized && _container.IsAvailable &&
            _transferController != null && !_transferController.HasRequestInFlight;
        _view.ContainerPanel.RefreshInteraction(interactive, _selection.SelectedLootId);
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
        _selection.Clear();
        _containerPanelPresenter.Clear();
        _view?.ContainerPanel?.ClearContent();
        _containerNetworkId = default;
        _containerNetworkObject = null;
        _container = null;
        _containerInteractable = null;
        _containerColliders = Array.Empty<Collider2D>();
        _observedContainerLootSequence = 0;
    }

    private void ClearBindingReferences()
    {
        _lootReceiver = null;
        _inputReader = null;
        _interactionController = null;
        _transferController = null;
        _runner = null;
        _localPlayerTransform = null;
        _registry = null;
    }
}
