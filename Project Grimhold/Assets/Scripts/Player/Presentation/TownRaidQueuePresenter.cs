using System;
using Fusion;
using UnityEngine;

/// <summary>
/// Binds the local Shared Mode player to the Town raid-code view.
/// It forwards explicit UI requests to the Town cohort controller and owns no session state.
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
    private TownRaidQueueNetworkController _queueController;
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
        if (_view == null)
        {
            return;
        }

        _view.CreateRequested += CreateRaid;
        _view.JoinRequested += JoinRaid;
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

    public override void Render()
    {
        if (!HasInputAuthority || _view == null)
        {
            return;
        }

        _view.SetPrompt(
            _candidateSource != null && _candidateSource.HasCandidate,
            _candidateSource?.CurrentPromptText);

        if (_queueController != null)
        {
            TownRaidQueueSnapshot snapshot = _queueController.Snapshot;
            ProfileId localProfile = GetLocalProfile();
            bool isMember = false;
            foreach (TownRaidQueueMember member in snapshot.Members)
            {
                if (member.ProfileId == localProfile)
                {
                    isMember = true;
                    break;
                }
            }

            if (snapshot.RaidCode.IsValid && isMember)
            {
                bool localReady = false;
                foreach (TownRaidQueueMember member in snapshot.Members)
                {
                    if (member.ProfileId == localProfile)
                    {
                        localReady = member.IsReady;
                        break;
                    }
                }

                bool allReady = snapshot.Members.Count > 0;
                foreach (TownRaidQueueMember member in snapshot.Members)
                {
                    if (!member.IsReady)
                    {
                        allReady = false;
                        break;
                    }
                }

                _view.PresentPreparation(
                    snapshot,
                    snapshot.HostProfileId == localProfile,
                    localReady,
                    allReady);
            }
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

        _queueController = npc.QueueController;
        _view.Open();
        AcquireInputSuppression();
    }

    private void CreateRaid(string _)
    {
        _queueController?.RequestCreate();
    }

    private void JoinRaid(string code)
    {
        _queueController?.RequestJoin(code);
    }

    private void SetReady(bool ready) => _queueController?.RequestSetReady(ready);

    private void StartRaid() => _queueController?.RequestLaunch();

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
            _view.ReadyRequested -= SetReady;
            _view.StartRequested -= StartRaid;
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
