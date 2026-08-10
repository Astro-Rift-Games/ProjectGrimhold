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
        if (!interactionEvent.Success || interactionEvent.TargetId.Value == 0 || Runner == null ||
            _view == null || _view.IsOpen)
        {
            return;
        }

        var networkId = new NetworkId { Raw = unchecked((uint)interactionEvent.TargetId.Value) };
        if (!Runner.TryFindObject(networkId, out NetworkObject target) || target == null ||
            !target.TryGetBehaviour(out TownStashNpcInteractable _))
        {
            return;
        }

        ApplicationStashContext context = FindAnyObjectByType<ApplicationStashContext>();
        if (context == null || context.Store == null || !context.Store.IsAvailable ||
            context.StashService == null || context.LoadoutService == null)
        {
            Debug.LogWarning("Town stash is unavailable because local persistence is not ready.", this);
            return;
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
