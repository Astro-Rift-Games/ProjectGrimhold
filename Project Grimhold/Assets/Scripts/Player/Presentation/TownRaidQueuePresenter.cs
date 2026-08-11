using System;
using Fusion;
using UnityEngine;

/// <summary>
/// Binds the local Shared Mode player to the Town raid-code view.
/// It forwards explicit UI requests to the application session coordinator and owns no session state.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInteractionNetworkController))]
[RequireComponent(typeof(LocalInteractionCandidateSource))]
[RequireComponent(typeof(SocialPlayerIdentity))]
public sealed class TownRaidQueuePresenter : NetworkBehaviour
{
    [SerializeField]
    private LocalInteractionCandidateSource _candidateSource;

    [SerializeField]
    private PlayerInteractionNetworkController _interactionController;

    private TownRaidQueueView _view;
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

    private void Bind()
    {
        if (!HasInputAuthority || _candidateSource == null || _interactionController == null || _view != null)
        {
            return;
        }

        _view = TownRaidQueueView.Create(transform);
        _view.CreateRequested += CreateRaid;
        _view.JoinRequested += JoinRaid;
        _view.CloseRequested += ClosePanel;
        _interactionController.InteractionResolved += OnInteractionResolved;
    }

    public override void Render()
    {
        if (!HasInputAuthority || _view == null)
        {
            return;
        }

        _view.SetPrompt(
            _candidateSource != null && _candidateSource.HasCandidate,
            _candidateSource?.CurrentPromptText);
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

    private void OnInteractionResolved(InteractionPresentationEvent interactionEvent)
    {
        if (!interactionEvent.Success || interactionEvent.TargetId.Value == 0 || Runner == null)
        {
            return;
        }

        var networkId = new NetworkId { Raw = unchecked((uint)interactionEvent.TargetId.Value) };
        if (!Runner.TryFindObject(networkId, out NetworkObject target) || target == null ||
            !target.TryGetBehaviour(out TownRaidNpcInteractable _))
        {
            return;
        }

        _view.Open();
        AcquireInputSuppression();
    }

    private async void CreateRaid(string code)
    {
        await StartRaidTransition(code, true);
    }

    private async void JoinRaid(string code)
    {
        await StartRaidTransition(code, false);
    }

    private async System.Threading.Tasks.Task StartRaidTransition(string code, bool create)
    {
        SessionConnectionCoordinator coordinator = SessionConnectionCoordinator.Instance;
        if (coordinator == null)
        {
            _view?.ShowTransitionFailure(SessionTransitionResult.InvalidState);
            return;
        }

        _view?.SetBusy(true, create ? $"Creando raid {code}…" : $"Uniéndose a raid {code}…");
        SessionTransitionResult result = create
            ? await coordinator.CreateCodeRaidAsync(code)
            : await coordinator.JoinCodeRaidAsync(code);

        if (result != SessionTransitionResult.Succeeded && this != null)
        {
            _view?.ShowTransitionFailure(result);
        }
    }

    private void ClosePanel()
    {
        _view?.Close();
        ReleaseInputSuppression();
    }

    private void AcquireInputSuppression()
    {
        if (_inputSuppression != null || Runner == null)
        {
            return;
        }

        LocalInputContext context = Runner.GetComponent<LocalInputContext>();
        if (context != null && context.Reader != null)
        {
            _inputReader = context.Reader;
            _inputReader.InventoryCloseRequested += TryClosePanelFromInput;
            _inputSuppression = _inputReader.AcquireGameplayInputSuppression();
        }
    }

    private void ReleaseInputSuppression()
    {
        if (_inputReader != null)
        {
            _inputReader.InventoryCloseRequested -= TryClosePanelFromInput;
            _inputReader = null;
        }

        _inputSuppression?.Dispose();
        _inputSuppression = null;
    }

    private bool TryClosePanelFromInput()
    {
        if (_view == null || !_view.IsPanelOpen)
        {
            return false;
        }

        ClosePanel();
        return true;
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
            _view.CreateRequested -= CreateRaid;
            _view.JoinRequested -= JoinRaid;
            _view.CloseRequested -= ClosePanel;
            Destroy(_view.gameObject);
            _view = null;
        }
    }

    private void CacheDependencies()
    {
        if (_candidateSource == null)
        {
            _candidateSource = GetComponent<LocalInteractionCandidateSource>();
        }

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
