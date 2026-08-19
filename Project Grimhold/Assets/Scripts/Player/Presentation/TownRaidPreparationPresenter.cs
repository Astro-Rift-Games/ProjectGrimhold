using System;
using Fusion;
using UnityEngine;

/// <summary>
/// Binds the local Shared Mode player to only their own Town Raid preparation.
/// It reads the directory cache and forwards explicit UI intentions without owning replicated state.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInteractionNetworkController))]
[RequireComponent(typeof(LocalInteractionCandidateSource))]
[RequireComponent(typeof(SocialPlayerIdentity))]
public sealed class TownRaidPreparationPresenter : NetworkBehaviour
{
    [SerializeField]
    private LocalInteractionCandidateSource _candidateSource;

    [SerializeField]
    private PlayerInteractionNetworkController _interactionController;

    private TownRaidPreparationView _view;
    private TownRaidPreparationDirectory _directory;
    private TownRaidPreparationNetworkController _presentedPreparation;
    private IDisposable _inputSuppression;
    private PlayerInputReader _inputReader;
    private int _presentedRevision = -1;
    private bool _showingNoPreparation;

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
        if (!HasInputAuthority || _view == null)
        {
            return;
        }

        _view.SetPrompt(
            _candidateSource != null && _candidateSource.HasCandidate,
            _candidateSource?.CurrentPromptText);
        RefreshPreparationPresentation(false);

        // Consumed once, so a rejected launch revision reports to the player exactly one time.
        SessionConnectionCoordinator coordinator = SessionConnectionCoordinator.Instance;
        if (coordinator != null &&
            coordinator.TryConsumeLastLaunchRejection(out ExpeditionPreparationResult rejection))
        {
            _view.ShowPreparationRejected(rejection);
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
        if (!HasInputAuthority || _candidateSource == null || _interactionController == null || _view != null)
        {
            return;
        }

        _view = TownRaidPreparationView.Create(transform);
        if (_view == null)
        {
            return;
        }

        _view.CreateRequested += CreateRaid;
        _view.JoinRequested += JoinRaid;
        _view.LeaveRequested += LeaveRaid;
        _view.ReadyRequested += SetReady;
        _view.StartRequested += StartRaid;
        _view.CloseRequested += ClosePanel;
        _interactionController.InteractionResolved += OnInteractionResolved;

        SessionConnectionCoordinator coordinator = SessionConnectionCoordinator.Instance;
        if (coordinator != null && coordinator.TryConsumeLastTransitionFailure(out SessionTransitionResult failure))
        {
            _view.ShowTransitionFailure(failure);
        }
    }

    private void OnInteractionResolved(InteractionPresentationEvent interactionEvent)
    {
        if (_view == null || !interactionEvent.Success || interactionEvent.TargetId.Value == 0 || Runner == null)
        {
            return;
        }

        var networkId = new NetworkId { Raw = unchecked((uint)interactionEvent.TargetId.Value) };
        if (!Runner.TryFindObject(networkId, out NetworkObject target) || target == null ||
            !target.TryGetBehaviour(out TownRaidNpcInteractable npc))
        {
            return;
        }

        _directory = npc.PreparationDirectory;
        _view.Open();
        RefreshPreparationPresentation(true);
        AcquireInputSuppression();
    }

    private void RefreshPreparationPresentation(bool force)
    {
        ProfileId localProfile = GetLocalProfile();
        if (_directory == null || !_directory.TryGetPreparation(localProfile, out TownRaidPreparationNetworkController preparation) ||
            !TownRaidPreparationPresentation.TryCreate(preparation.Snapshot, localProfile, out TownRaidPreparationPresentation presentation))
        {
            if (force || !_showingNoPreparation)
            {
                _view.PresentNoPreparation();
                _showingNoPreparation = true;
                _presentedPreparation = null;
                _presentedRevision = -1;
            }

            return;
        }

        if (!force && !_showingNoPreparation && _presentedPreparation == preparation &&
            _presentedRevision == preparation.SnapshotRevision)
        {
            return;
        }

        _showingNoPreparation = false;
        _presentedPreparation = preparation;
        _presentedRevision = preparation.SnapshotRevision;
        _view.PresentPreparation(presentation);
    }

    private void CreateRaid(string _) => _directory?.RequestCreate();
    private void JoinRaid(string code) => _directory?.RequestJoin(code);
    private void LeaveRaid() => _directory?.RequestLeave();
    private void SetReady(bool ready) => _directory?.RequestSetReady(ready);
    private void StartRaid() => _directory?.RequestStart();

    private ProfileId GetLocalProfile()
    {
        LocalPlayerJoinContext context = Runner != null ? Runner.GetComponent<LocalPlayerJoinContext>() : null;
        return context != null ? context.JoinData.ProfileId : default;
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
            _view.LeaveRequested -= LeaveRaid;
            _view.ReadyRequested -= SetReady;
            _view.StartRequested -= StartRaid;
            _view.CloseRequested -= ClosePanel;
            Destroy(_view.gameObject);
            _view = null;
        }

        _directory = null;
        _presentedPreparation = null;
        _presentedRevision = -1;
        _showingNoPreparation = false;
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
