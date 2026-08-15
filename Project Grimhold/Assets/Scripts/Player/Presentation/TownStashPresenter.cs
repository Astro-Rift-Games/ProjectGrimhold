using System;
using Fusion;
using UnityEngine;

/// <summary>
/// Presents the local stash after a confirmed interaction with a Town stash NPC.
/// Only Input Authority creates the Canvas and reads the local persistence context.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInteractionNetworkController))]
public sealed class TownStashPresenter : NetworkBehaviour
{
    [SerializeField]
    private GameObject _stashInventoryPrefab;

    [SerializeField]
    private PlayerInteractionNetworkController _interactionController;

    private TownStashView _view;
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
        if (!HasInputAuthority || _interactionController == null || _stashInventoryPrefab == null || _view != null)
        {
            return;
        }

        _view = TownStashView.Create(transform, _stashInventoryPrefab);
        if (_view == null)
        {
            return;
        }

        _interactionController.InteractionResolved += OnInteractionResolved;
    }

    private void OnInteractionResolved(InteractionPresentationEvent interactionEvent)
    {
        Debug.Log($"[StashUI] OnInteractionResolved triggered. Success: {interactionEvent.Success}, TargetId: {interactionEvent.TargetId.Value}");

        if (!interactionEvent.Success || interactionEvent.TargetId.Value == 0 || Runner == null ||
            _view == null || _view.IsOpen)
        {
            Debug.Log($"[StashUI] Exiting early. Success={interactionEvent.Success}, TargetId.Value={interactionEvent.TargetId.Value}, RunnerIsNull={Runner == null}, ViewIsNull={_view == null}, ViewIsOpen={_view != null && _view.IsOpen}");
            return;
        }

        var networkId = new NetworkId { Raw = unchecked((uint)interactionEvent.TargetId.Value) };
        if (!Runner.TryFindObject(networkId, out NetworkObject target) || target == null)
        {
            Debug.Log($"[StashUI] NetworkObject with ID {networkId.Raw} not found.");
            return;
        }

        if (!target.TryGetBehaviour(out TownStashNpcInteractable _))
        {
            Debug.Log($"[StashUI] Target {target.name} does not have a TownStashNpcInteractable.");
            return;
        }
        
        Debug.Log($"[StashUI] Target is valid and has TownStashNpcInteractable. Checking StashContext...");

        ApplicationStashContext context = FindAnyObjectByType<ApplicationStashContext>();
        if (context == null)
        {
            Debug.LogWarning("[StashUI] Town stash is unavailable: ApplicationStashContext is null.", this);
            return;
        }
        
        if (context.Store == null)
        {
            Debug.LogWarning("[StashUI] Town stash is unavailable: context.Store is null.", this);
            return;
        }
        
        if (!context.Store.IsAvailable)
        {
            Debug.LogWarning("[StashUI] Town stash is unavailable: context.Store.IsAvailable is false.", this);
            return;
        }
        
        if (context.StashService == null)
        {
            Debug.LogWarning("[StashUI] Town stash is unavailable: context.StashService is null.", this);
            return;
        }
        
        if (context.LoadoutService == null)
        {
            Debug.LogWarning("[StashUI] Town stash is unavailable: context.LoadoutService is null.", this);
            return;
        }

        Debug.Log($"[StashUI] All checks passed. Opening view and acquiring input suppression.");
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

    private void ClosePanel()
    {
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
