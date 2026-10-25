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
    [SerializeField] private DialoguePresenter _dialoguePresenter;
    [SerializeField] private TownRaidPreparationView _viewPrefab;

    private TownRaidPreparationView _view;
    private TownRaidPreparationDirectory _directory;
    private TownPartyDirectory _partyDirectory;
    private TownRaidPreparationNetworkController _presentedPreparation;
    private TownRaidPreparationPresentation _presentedPresentation;
    private IDisposable _inputSuppression;
    private PlayerInputReader _inputReader;
    private int _presentedRevision = -1;
    private bool _showingNoPreparation;
    private NetworkObject _openNpc;
    private bool _reportedMissingViewPrefab;

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

        if (_view.IsPanelOpen && (Runner == null || !Runner.IsRunning || _openNpc == null || !_openNpc.IsValid))
        {
            ClosePanel();
        }

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

        if (_viewPrefab == null)
        {
            ReportMissingViewPrefab();
            return;
        }

        _view = Instantiate(_viewPrefab, transform, false);
        _view.name = _viewPrefab.name;

        _view.CreateRequested += CreateRaid;
        _view.JoinRequested += JoinRaid;
        _view.LeaveRequested += LeaveRaid;
        _view.ReadyRequested += SetReady;
        _view.StartRequested += StartRaid;
        _view.CloseRequested += ClosePanel;
        _view.ReconnectRequested += OnReconnectRequested;
        _view.DiscardReconnectRequested += OnDiscardReconnectRequested;
        _interactionController.InteractionResolved += OnInteractionResolved;
        if (_dialoguePresenter != null)
        {
            _dialoguePresenter.DialogueCompletedNormally += OnDialogueCompletedNormally;
        }

        SessionConnectionCoordinator coordinator = SessionConnectionCoordinator.Instance;
        if (coordinator != null && coordinator.TryConsumeLastTransitionFailure(out SessionTransitionResult failure))
        {
            _view.ShowTransitionFailure(failure);
        }
    }

    private void OnInteractionResolved(InteractionPresentationEvent interactionEvent)
    {
        if (_view == null || _view.IsPanelOpen || !interactionEvent.Success ||
            interactionEvent.TargetId.Value == 0 || Runner == null)
        {
            return;
        }

        var networkId = new NetworkId { Raw = unchecked((uint)interactionEvent.TargetId.Value) };
        if (!Runner.TryFindObject(networkId, out NetworkObject target) || target == null ||
            !target.TryGetBehaviour(out TownRaidNpcInteractable npc))
        {
            return;
        }

        if (target.GetComponentInChildren<IDialogueTrigger>() != null)
        {
            return;
        }

        OpenForTarget(target, npc);
    }

    private void OnDialogueCompletedNormally(EntityId targetId)
    {
        var networkId = new NetworkId { Raw = unchecked((uint)targetId.Value) };
        if (Runner == null || !Runner.IsRunning ||
            !Runner.TryFindObject(networkId, out NetworkObject target) || target == null ||
            !target.TryGetBehaviour(out TownRaidNpcInteractable npc) ||
            target.GetComponentInChildren<IDialogueTrigger>() == null)
        {
            return;
        }

        if (_view != null && _view.IsPanelOpen)
        {
            return;
        }

        if (_view != null)
        {
            OpenForTarget(target, npc);
            if (_view.IsPanelOpen)
            {
                return;
            }
        }

        target.GetComponentInChildren<TownNpcDirectionalView>()?.RestoreInitialFacing();
    }

    private void OpenForTarget(NetworkObject target, TownRaidNpcInteractable npc)
    {
        _directory = npc.PreparationDirectory;
        _partyDirectory = Runner.GetComponent<TownPartyDirectoryContext>()?.Directory;
        _openNpc = target;
        _view.Open();
        if (!_view.IsPanelOpen)
        {
            ClosePanel();
            return;
        }

        target.GetComponentInChildren<TownNpcDirectionalView>()?.FaceTarget(transform.position);
        RefreshPreparationPresentation(true);
        AcquireInputSuppression();
    }

    private void RefreshPreparationPresentation(bool force)
    {
        SessionConnectionCoordinator coordinator = SessionConnectionCoordinator.Instance;
        if (coordinator != null && coordinator.HasActiveReconnectTicket)
        {
            _view.PresentReconnectOpportunity(coordinator.ActiveReconnectTicket.Value);
            return;
        }

        ProfileId localProfile = GetLocalProfile();
        TownRaidPreparationNetworkController preparation = null;
        bool hasPreparation = _directory != null && _directory.TryGetPreparation(localProfile, out preparation);
        TownRaidPreparationSnapshot snapshot = hasPreparation ? preparation.Snapshot : default;
        if (!hasPreparation)
        {
            if (force || !_showingNoPreparation)
            {
                _view.PresentNoPreparation();
                _showingNoPreparation = true;
                _presentedPreparation = null;
                _presentedPresentation = default;
                _presentedRevision = -1;
            }

            return;
        }

        if (_partyDirectory == null)
        {
            _partyDirectory = Runner?.GetComponent<TownPartyDirectoryContext>()?.Directory;
        }

        if (!force && !_showingNoPreparation && _presentedPreparation == preparation &&
            _presentedRevision == preparation.SnapshotRevision && MemberPresentationMatches(snapshot))
        {
            return;
        }

        TownRaidPreparationPresentation.DisplayNameResolver resolver = _partyDirectory != null
            ? _partyDirectory.TryGetDisplayName
            : null;
        TownRaidPreparationPresentation.PreparedEquipmentResolver equipmentResolver = _partyDirectory != null
            ? _partyDirectory.TryGetPreparedEquipment
            : null;
        if (!TownRaidPreparationPresentation.TryCreate(
                snapshot,
                localProfile,
                resolver,
                equipmentResolver,
                out TownRaidPreparationPresentation presentation))
        {
            if (force || !_showingNoPreparation)
            {
                _view.PresentNoPreparation();
                _showingNoPreparation = true;
                _presentedPreparation = null;
                _presentedPresentation = default;
                _presentedRevision = -1;
            }

            return;
        }

        _showingNoPreparation = false;
        _presentedPreparation = preparation;
        _presentedRevision = preparation.SnapshotRevision;
        _presentedPresentation = presentation;
        _view.PresentPreparation(presentation);
    }

    private bool MemberPresentationMatches(in TownRaidPreparationSnapshot snapshot)
    {
        if (_presentedPresentation.Members == null ||
            _presentedPresentation.Members.Count != snapshot.Members.Count)
        {
            return false;
        }

        for (int index = 0; index < snapshot.Members.Count; index++)
        {
            ProfileId profileId = snapshot.Members[index].ProfileId;
            string displayName = profileId.Value;
            if (_partyDirectory != null &&
                _partyDirectory.TryGetDisplayName(profileId, out string resolvedName) &&
                !string.IsNullOrWhiteSpace(resolvedName))
            {
                displayName = resolvedName;
            }

            TownRaidPreparationPresentation.Member member = _presentedPresentation.Members[index];
            IPreparedEquipmentReader reader = null;
            bool hasEquipment = _partyDirectory != null &&
                _partyDirectory.TryGetPreparedEquipment(profileId, out reader) && reader != null;
            if (member.ProfileId != profileId || member.HasEquipment != hasEquipment ||
                member.EquipmentRevision != (hasEquipment ? reader.ObservedEquipmentRevision : 0))
            {
                return false;
            }

            if (!string.Equals(
                    member.DisplayName,
                    displayName,
                    StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private void CreateRaid(string _)
    {
        if (SessionConnectionCoordinator.Instance != null && SessionConnectionCoordinator.Instance.HasActiveReconnectTicket)
        {
            return;
        }

        _directory?.RequestCreate();
    }

    private void JoinRaid(string code)
    {
        if (SessionConnectionCoordinator.Instance != null && SessionConnectionCoordinator.Instance.HasActiveReconnectTicket)
        {
            return;
        }

        _directory?.RequestJoin(code);
    }

    private void LeaveRaid()
    {
        _directory?.RequestLeave();
    }
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
        if (_openNpc != null)
        {
            _openNpc.GetComponentInChildren<TownNpcDirectionalView>()?.RestoreInitialFacing();
        }
        _openNpc = null;
        ReleaseInputSuppression();
    }

    private async void OnReconnectRequested()
    {
        SessionConnectionCoordinator coordinator = SessionConnectionCoordinator.Instance;
        if (coordinator == null || !coordinator.HasActiveReconnectTicket)
        {
            return;
        }

        _view.SetBusy(true, "Reconectando a la raid...");
        SessionTransitionResult result = await coordinator.ReconnectToRaidAsync();
        if (this == null || _view == null)
        {
            return;
        }

        if (result != SessionTransitionResult.Succeeded)
        {
            _view.SetBusy(false);
            _view.ShowTransitionFailure(result);
            if (!coordinator.HasActiveReconnectTicket)
            {
                _view.HideReconnectOpportunity();
                RefreshPreparationPresentation(true);
            }
        }
    }

    private void OnDiscardReconnectRequested()
    {
        SessionConnectionCoordinator coordinator = SessionConnectionCoordinator.Instance;
        if (coordinator != null)
        {
            coordinator.DiscardActiveReconnectTicket();
        }

        _view.HideReconnectOpportunity();
        RefreshPreparationPresentation(true);
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

        if (_dialoguePresenter != null)
        {
            _dialoguePresenter.DialogueCompletedNormally -= OnDialogueCompletedNormally;
        }

        ClosePanel();

        if (_view != null)
        {
            _view.CreateRequested -= CreateRaid;
            _view.JoinRequested -= JoinRaid;
            _view.LeaveRequested -= LeaveRaid;
            _view.ReadyRequested -= SetReady;
            _view.StartRequested -= StartRaid;
            _view.CloseRequested -= ClosePanel;
            _view.ReconnectRequested -= OnReconnectRequested;
            _view.DiscardReconnectRequested -= OnDiscardReconnectRequested;
            Destroy(_view.gameObject);
            _view = null;
        }

        _directory = null;
        _partyDirectory = null;
        _presentedPreparation = null;
        _presentedPresentation = default;
        _presentedRevision = -1;
        _showingNoPreparation = false;
    }

    private void ReportMissingViewPrefab()
    {
        if (_reportedMissingViewPrefab)
        {
            return;
        }

        _reportedMissingViewPrefab = true;
        Debug.LogError(
            $"[{nameof(TownRaidPreparationPresenter)}] Missing required serialized {nameof(TownRaidPreparationView)} prefab.",
            this);
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

        if (_dialoguePresenter == null)
        {
            _dialoguePresenter = GetComponent<DialoguePresenter>();
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        CacheDependencies();
    }
#endif
}
