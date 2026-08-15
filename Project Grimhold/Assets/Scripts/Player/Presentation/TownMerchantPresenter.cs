using System;
using Fusion;
using UnityEngine;

/// <summary>
/// Presents the local merchant shop after a confirmed interaction with a Town Merchant NPC.
/// Only Input Authority creates the Canvas, initializes the network controller, and manages input.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInteractionNetworkController))]
public sealed class TownMerchantPresenter : NetworkBehaviour
{
    [SerializeField]
    private GameObject _merchantShopPrefab;

    [SerializeField]
    private PlayerInteractionNetworkController _interactionController;

    private TownMerchantView _view;
    private NetworkObject _openNpc;
    private IDisposable _inputSuppression;
    private PlayerInputReader _inputReader;

    private void Awake()
    {
        CacheDependencies();
    }

    public override void Spawned()
    {
        CacheDependencies();
        Bind();
    }

    private void OnEnable()
    {
        if (Object != null && Object.IsValid)
        {
            Bind();
        }
    }

    public override void Render()
    {
        if (!HasInputAuthority || _view == null || !_view.IsOpen)
        {
            return;
        }

        if (Runner == null || !Runner.IsRunning || _openNpc == null || !_openNpc.IsValid)
        {
            ClosePanel();
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        Unbind();
    }

    private void OnDisable()
    {
        Unbind();
    }

    private void OnDestroy()
    {
        Unbind();
    }

    private void Bind()
    {
        if (!HasInputAuthority || _interactionController == null || _view != null)
        {
            return;
        }
        
        if (_merchantShopPrefab == null)
        {
            Debug.LogError("TownMerchantPresenter failed to bind because MerchantShopPrefab is not assigned in the Inspector.", this);
            return;
        }

        _view = TownMerchantView.Create(transform, _merchantShopPrefab);
        if (_view == null)
        {
            return;
        }

        _interactionController.InteractionResolved += OnInteractionResolved;
    }

    private void OnInteractionResolved(InteractionPresentationEvent interactionEvent)
    {
        if (!interactionEvent.Success || interactionEvent.TargetId.Value == 0 || Runner == null)
        {
            Debug.LogWarning($"[TownMerchantPresenter] Event ignored. Success: {interactionEvent.Success}, TargetId: {interactionEvent.TargetId.Value}, Runner: {Runner != null}", this);
            return;
        }
        
        if (_view == null)
        {
            Debug.LogError("TownMerchantPresenter cannot open UI because _view is null. Did you assign the MerchantShopPrefab in the inspector?");
            return;
        }
        
        if (_view.IsOpen)
        {
            Debug.LogWarning("[TownMerchantPresenter] Event ignored because the view is already open.", this);
            return;
        }

        var networkId = new NetworkId { Raw = unchecked((uint)interactionEvent.TargetId.Value) };
        if (!Runner.TryFindObject(networkId, out NetworkObject target) || target == null)
        {
            // Do not log an error. Other interactions (like looting) destroy the object,
            // which legitimately causes it to not be found here.
            return;
        }
        
        if (!target.TryGetBehaviour(out TownMerchantNpcInteractable _))
        {
            return;
        }
        
        if (!target.TryGetBehaviour(out TownMerchantNetworkController merchantController))
        {
            Debug.LogError("Merchant NPC is missing TownMerchantNetworkController.", target);
            return;
        }

        ApplicationStashContext context = FindAnyObjectByType<ApplicationStashContext>();
        if (context == null || context.ShopTransactionService == null || context.Store == null)
        {
            Debug.LogWarning("Merchant is unavailable because local stash context is not ready.", this);
            return;
        }

        // Initialize the network controller with the local execution dependencies
        merchantController.InitializeLocalClient(context.ShopTransactionService, context.Store.ProfileId);

        // Only force sync the loadout in the social hub (Shared Mode).
        // In a raid (Host/Client), the inventory is authoritative and must not be overridden.
        if (Runner.Topology == Topologies.Shared)
        {
            var playerLootReceiver = GetComponent<PlayerLootReceiver>();
            if (playerLootReceiver != null && context.Store != null)
            {
                var loadout = context.Store.GetLoadout();
                var entries = new System.Collections.Generic.List<LootEntry>(loadout.Count);
                foreach (var item in loadout)
                {
                    entries.Add(new LootEntry(item.LootId, item.Amount));
                }
                playerLootReceiver.TryForceSyncLoadout(entries, out _);
            }
        }

        // Pass dependencies to the UI
        if (_view.ShopUI != null)
        {
            _view.ShopUI.Initialize(merchantController, context, GetComponent<PlayerLootReceiver>());
            _view.ShopUI.OnCloseRequested.AddListener(ClosePanelFromUI);
        }

        _openNpc = target;
        _view.Open();
        AcquireInputSuppression();
    }

    private void AcquireInputSuppression()
    {
        if (_inputSuppression != null || Runner == null)
        {
            return;
        }

        LocalInputContext context = Runner.GetComponent<LocalInputContext>();
        if (context == null || context.Reader == null)
        {
            return;
        }

        _inputReader = context.Reader;
        _inputReader.InventoryCloseRequested += TryClosePanelFromInput;
        _inputReader.InteractPressedLocally += ClosePanelFromInteraction;
        _inputSuppression = _inputReader.AcquireGameplayInputSuppression();
    }

    private void ReleaseInputSuppression()
    {
        if (_inputReader != null)
        {
            _inputReader.InventoryCloseRequested -= TryClosePanelFromInput;
            _inputReader.InteractPressedLocally -= ClosePanelFromInteraction;
            _inputReader = null;
        }

        _inputSuppression?.Dispose();
        _inputSuppression = null;
    }

    private bool TryClosePanelFromInput()
    {
        if (_view == null || !_view.IsOpen)
        {
            return false;
        }

        ClosePanel();
        return true;
    }

    private void ClosePanelFromInteraction()
    {
        if (_view != null && _view.IsOpen)
        {
            ClosePanel();
        }
    }
    
    private void ClosePanelFromUI()
    {
        ClosePanel();
    }

    private void ClosePanel()
    {
        if (_view != null && _view.ShopUI != null)
        {
            _view.ShopUI.OnCloseRequested.RemoveListener(ClosePanelFromUI);
        }

        _view?.Close();
        _openNpc = null;
        ReleaseInputSuppression();
    }

    private void Unbind()
    {
        if (_interactionController != null)
        {
            _interactionController.InteractionResolved -= OnInteractionResolved;
        }

        ReleaseInputSuppression();
        if (_view != null)
        {
            Destroy(_view.gameObject);
            _view = null;
        }

        _openNpc = null;
    }

    private void CacheDependencies()
    {
        if (_interactionController == null)
        {
            _interactionController = GetComponent<PlayerInteractionNetworkController>();
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        CacheDependencies();
    }
#endif
}
