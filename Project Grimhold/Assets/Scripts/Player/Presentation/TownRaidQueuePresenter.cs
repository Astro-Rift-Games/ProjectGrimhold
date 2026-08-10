using System;
using Fusion;
using UnityEngine;

/// <summary>
/// Binds the local Shared Mode player to the Town interaction prompt and raid queue view.
/// It observes confirmed interaction results and never mutates authoritative state directly.
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
    private TownRaidQueueNetworkController _queue;
    private SocialPlayerIdentity _identity;
    private IDisposable _inputSuppression;
    private PlayerInputReader _inputReader;
    private int _lastSnapshotRevision = -1;
    private ProfileId _localProfile;

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
        if (!HasInputAuthority || _candidateSource == null || _interactionController == null ||
            _identity == null || string.IsNullOrWhiteSpace(_identity.ProfileId.ToString()))
        {
            return;
        }

        if (_view != null)
        {
            return;
        }

        _localProfile = new ProfileId(_identity.ProfileId.ToString());
        _view = TownRaidQueueView.Create(transform);
        _view.CreateRequested += RequestCreate;
        _view.JoinRequested += RequestJoin;
        _view.LeaveRequested += RequestLeave;
        _view.ReadyRequested += RequestReady;
        _view.LaunchRequested += RequestLaunch;
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

        if (!_view.IsPanelOpen)
        {
            return;
        }

        if (_queue == null || _queue.Object == null || !_queue.Object.IsValid)
        {
            ClosePanel();
            return;
        }

        if (_lastSnapshotRevision != _queue.SnapshotRevision)
        {
            _lastSnapshotRevision = _queue.SnapshotRevision;
            TownRaidQueueSnapshot snapshot = _queue.Snapshot;
            _view.Refresh(in snapshot, _localProfile);
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

    private void OnInteractionResolved(InteractionPresentationEvent interactionEvent)
    {
        if (!interactionEvent.Success || interactionEvent.TargetId.Value == 0 || Runner == null)
        {
            return;
        }

        var networkId = new NetworkId { Raw = unchecked((uint)interactionEvent.TargetId.Value) };
        if (!Runner.TryFindObject(networkId, out NetworkObject target) || target == null ||
            !target.TryGetBehaviour(out TownRaidNpcInteractable npc) || npc.QueueController == null)
        {
            return;
        }

        _queue = npc.QueueController;
        _lastSnapshotRevision = -1;
        _view.Open();
        AcquireInputSuppression();
    }

    private void RequestCreate() => ReportSend(_queue != null && _queue.RequestCreate());
    private void RequestJoin() => ReportSend(_queue != null && _queue.RequestJoin());
    private void RequestLeave() => ReportSend(_queue != null && _queue.RequestLeave());
    private void RequestReady(bool ready) => ReportSend(_queue != null && _queue.RequestSetReady(ready));
    private void RequestLaunch() => ReportSend(_queue != null && _queue.RequestLaunch());

    private void ReportSend(bool sent)
    {
        if (!sent)
        {
            _view?.ShowTransportFailure();
        }
    }

    private void ClosePanel()
    {
        _view?.Close();
        _queue = null;
        _lastSnapshotRevision = -1;
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
            Destroy(_view.gameObject);
            _view = null;
        }

        _queue = null;
        _lastSnapshotRevision = -1;
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

        if (_identity == null)
        {
            _identity = GetComponent<SocialPlayerIdentity>();
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        CacheDependencies();
    }
#endif
}
