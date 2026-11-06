using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using UnityEngine;

/// <summary>
/// Presents the Town Mission Guild UI after confirmed interaction with a <see cref="TownMissionNpcInteractable"/>.
/// Enforces rank filtering, max active mission limit (3 for Rank E MVP), safe abandonment,
/// and atomic claim with Stash capacity verification.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInteractionNetworkController))]
public sealed class TownMissionGuildPresenter : NetworkBehaviour
{
    public const int MaxActiveContracts = 3;

    [SerializeField] private TownMissionGuildView _viewPrefab;
    [SerializeField] private MissionDefinitionCatalog _missionCatalog;
    [SerializeField] private PlayerInteractionNetworkController _interactionController;
    [SerializeField] private DialoguePresenter _dialoguePresenter;

    private TownMissionGuildView _view;
    private NetworkObject _openNpc;
    private IDisposable _inputSuppression;
    private PlayerInputReader _inputReader;
    private LocalProfileStore _store;
    private bool _isListening;
    private MissionDefinition _selectedMission;

    public TownMissionGuildView View => _view;
    public bool IsOpen => _view != null && _view.IsOpen;

    public void BindProfile(LocalProfileStore store)
    {
        ClosePanel();
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public void UnbindProfile()
    {
        ClosePanel();
        _store = null;
    }

    public void SetView(TownMissionGuildView view)
    {
        if (_view != null && _view != view)
        {
            UnbindViewEvents();
        }

        _view = view;
        if (_view != null)
        {
            BindViewEvents();
        }
    }

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
        if (!HasInputAuthority || _interactionController == null)
        {
            return;
        }

        if (_view == null)
        {
            _view = FindAnyObjectByType<TownMissionGuildView>(FindObjectsInactive.Include);
        }

        if (_view == null && _viewPrefab != null)
        {
            _view = TownMissionGuildView.Create(transform, _viewPrefab);
        }

        if (_view != null)
        {
            BindViewEvents();
        }

        _interactionController.InteractionResolved += OnInteractionResolved;
        if (_dialoguePresenter != null)
        {
            _dialoguePresenter.DialogueCompletedNormally += OnDialogueCompletedNormally;
        }
    }

    private void BindViewEvents()
    {
        if (_view == null) return;
        _view.AcceptRequested += OnAcceptRequested;
        _view.AbandonRequested += OnAbandonRequested;
        _view.ClaimRequested += OnClaimRequested;
        _view.CloseRequested += ClosePanel;
        _view.MissionSelected += OnMissionSelected;
        _view.TabChanged += OnTabChanged;
    }

    private void UnbindViewEvents()
    {
        if (_view == null) return;
        _view.AcceptRequested -= OnAcceptRequested;
        _view.AbandonRequested -= OnAbandonRequested;
        _view.ClaimRequested -= OnClaimRequested;
        _view.CloseRequested -= ClosePanel;
        _view.MissionSelected -= OnMissionSelected;
        _view.TabChanged -= OnTabChanged;
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
        UnbindViewEvents();

        if (_view != null && _viewPrefab != null)
        {
            Destroy(_view.gameObject);
            _view = null;
        }
    }

    private void CacheDependencies()
    {
        if (_interactionController == null)
        {
            _interactionController = GetComponent<PlayerInteractionNetworkController>();
        }

        if (_dialoguePresenter == null)
        {
            _dialoguePresenter = GetComponent<DialoguePresenter>();
        }
    }

    private void OnInteractionResolved(InteractionPresentationEvent interactionEvent)
    {
        if (!interactionEvent.Success || interactionEvent.TargetId.Value == 0 || Runner == null || _view == null || _view.IsOpen)
        {
            return;
        }

        var networkId = new NetworkId { Raw = unchecked((uint)interactionEvent.TargetId.Value) };
        if (!Runner.TryFindObject(networkId, out NetworkObject target) || target == null)
        {
            return;
        }

        if (!target.TryGetBehaviour(out TownMissionNpcInteractable _))
        {
            return;
        }

        if (target.GetComponentInChildren<IDialogueTrigger>() != null)
        {
            return;
        }

        OpenForTarget(target);
    }

    private void OnDialogueCompletedNormally(EntityId targetId)
    {
        var networkId = new NetworkId { Raw = unchecked((uint)targetId.Value) };
        if (Runner == null || !Runner.IsRunning ||
            !Runner.TryFindObject(networkId, out NetworkObject target) || target == null ||
            !target.TryGetBehaviour(out TownMissionNpcInteractable _) ||
            target.GetComponentInChildren<IDialogueTrigger>() == null)
        {
            return;
        }

        if (_view != null && _view.IsOpen)
        {
            return;
        }

        if (_view != null)
        {
            OpenForTarget(target);
            if (_view.IsOpen)
            {
                return;
            }
        }

        target.GetComponentInChildren<TownNpcDirectionalView>()?.RestoreInitialFacing();
    }

    public void OpenForTarget(NetworkObject target)
    {
        EnsureStore();

        if (_store == null || !_store.IsAvailable)
        {
            Debug.LogWarning("Mission Guild is unavailable because the local profile is not bound.", this);
            return;
        }

        _openNpc = target;
        _selectedMission = null;

        if (_view == null)
        {
            _view = FindAnyObjectByType<TownMissionGuildView>(FindObjectsInactive.Include);
            if (_view == null && _viewPrefab != null)
            {
                _view = TownMissionGuildView.Create(transform, _viewPrefab);
            }
            if (_view != null) BindViewEvents();
        }

        if (_view != null)
        {
            _view.ShowFeedback(string.Empty);
            _view.Open();
            Refresh();
        }

        if (_openNpc != null)
        {
            _openNpc.GetComponentInChildren<TownNpcDirectionalView>()?.FaceTarget(transform.position);
        }

        StartListening();
        AcquireInputSuppression();
    }

    public void ClosePanel()
    {
        StopListening();
        _view?.Close();

        if (_openNpc != null)
        {
            _openNpc.GetComponentInChildren<TownNpcDirectionalView>()?.RestoreInitialFacing();
        }

        _openNpc = null;
        ReleaseInputSuppression();
    }

    private void EnsureStore()
    {
        if (_store == null)
        {
            var stashContext = FindAnyObjectByType<ApplicationStashContext>();
            if (stashContext != null && stashContext.IsAvailable)
            {
                _store = stashContext.Store;
            }
        }
    }

    private void StartListening()
    {
        if (_isListening || _store == null) return;
        _store.ProfileCommitted += OnProfileCommitted;
        _isListening = true;
    }

    private void StopListening()
    {
        if (!_isListening) return;
        if (_store != null)
        {
            _store.ProfileCommitted -= OnProfileCommitted;
        }
        _isListening = false;
    }

    private void OnProfileCommitted(ProfileId profileId)
    {
        if (_view != null && _view.IsOpen)
        {
            Refresh();
        }
    }

    public void Refresh()
    {
        EnsureStore();
        if (_store == null || _view == null) return;

        var catalog = _missionCatalog != null ? _missionCatalog : _store.MissionCatalog;
        var activeMissions = _store.GetActiveMissions();
        int activeCount = activeMissions.Count(m => m.State == MissionState.Activa || m.State == MissionState.PendienteDeReclamar);

        _view.PresentHeader(_store.GuildRank, _store.GuildReputation, activeCount, MaxActiveContracts);

        // Filter missions according to active tab
        List<MissionDefinition> displayList = new List<MissionDefinition>();

        if (catalog != null)
        {
            for (int i = 0; i < catalog.DefinitionCount; i++)
            {
                if (!catalog.TryGetByIndex(i, out var def) || def == null) continue;

                var instance = activeMissions.FirstOrDefault(m => m.MissionId == def.MissionId);
                bool isActive = instance != null && (instance.State == MissionState.Activa || instance.State == MissionState.PendienteDeReclamar);
                bool isReclaimed = instance != null && instance.State == MissionState.Reclamada;

                if (_view.ActiveTab == GuildViewTab.Available)
                {
                    // Available: Rank matches or lower, not active, not already reclaimed
                    if (!isActive && !isReclaimed && def.RankRequired <= _store.GuildRank)
                    {
                        displayList.Add(def);
                    }
                }
                else // Active tab
                {
                    if (isActive)
                    {
                        displayList.Add(def);
                    }
                }
            }
        }

        // Keep current selected if in list, otherwise select first or null
        if (_selectedMission == null || !displayList.Contains(_selectedMission))
        {
            _selectedMission = displayList.FirstOrDefault();
        }

        _view.PresentMissionList(displayList, activeMissions, _selectedMission);
        PresentSelectedDetails(activeMissions, activeCount);
    }

    private void PresentSelectedDetails(IReadOnlyList<MissionInstanceState> activeMissions, int activeCount)
    {
        if (_selectedMission == null)
        {
            _view.PresentMissionDetails(null, null, false, false, false);
            return;
        }

        var instance = activeMissions.FirstOrDefault(m => m.MissionId == _selectedMission.MissionId);
        bool isAlreadyActiveOrClaimable = instance != null && (instance.State == MissionState.Activa || instance.State == MissionState.PendienteDeReclamar);
        bool isReclaimed = instance != null && instance.State == MissionState.Reclamada;

        bool canAccept = !isAlreadyActiveOrClaimable && !isReclaimed && activeCount < MaxActiveContracts && _selectedMission.RankRequired <= _store.GuildRank;
        bool canAbandon = instance != null && instance.State == MissionState.Activa;
        bool canClaim = instance != null && instance.State == MissionState.PendienteDeReclamar;

        string claimWarning = null;
        if (canClaim)
        {
            if (!_store.CanClaimMission(_selectedMission, out string failureReason))
            {
                claimWarning = failureReason;
                canClaim = false;
            }
        }

        _view.PresentMissionDetails(_selectedMission, instance, canAccept, canAbandon, canClaim, claimWarning);
    }

    private void OnTabChanged(GuildViewTab tab)
    {
        _view?.ClearFeedback();
        _selectedMission = null;
        Refresh();
    }

    private void OnMissionSelected(MissionDefinition def)
    {
        if (_selectedMission != def)
        {
            _view?.ClearFeedback();
        }
        _selectedMission = def;
        var activeMissions = _store.GetActiveMissions();
        int activeCount = activeMissions.Count(m => m.State == MissionState.Activa || m.State == MissionState.PendienteDeReclamar);
        PresentSelectedDetails(activeMissions, activeCount);
    }

    public void OnAcceptRequested(MissionDefinition def)
    {
        if (def == null || _store == null) return;

        var activeMissions = _store.GetActiveMissions();
        int activeCount = activeMissions.Count(m => m.State == MissionState.Activa || m.State == MissionState.PendienteDeReclamar);

        if (activeCount >= MaxActiveContracts)
        {
            _view.ShowFeedback($"Límite alcanzado: no puedes tener más de {MaxActiveContracts} contratos activos simultáneos.", isError: true);
            return;
        }

        var result = _store.TryAcceptMission(def);
        if (result == StashOperationResult.Success)
        {
            _view.ShowFeedback($"Contrato '{def.Title}' aceptado.");
            Refresh();
        }
        else
        {
            _view.ShowFeedback($"Error al aceptar contrato: {result}", isError: true);
        }
    }

    public void OnAbandonRequested(MissionDefinition def)
    {
        if (def == null || _store == null) return;

        var result = _store.TryAbandonMission(def.MissionId);
        if (result == StashOperationResult.Success)
        {
            _view.ShowFeedback($"Contrato '{def.Title}' abandonado.");
            Refresh();
        }
        else
        {
            _view.ShowFeedback($"Error al abandonar contrato: {result}", isError: true);
        }
    }

    public void OnClaimRequested(MissionDefinition def)
    {
        if (def == null || _store == null) return;

        if (!_store.CanClaimMission(def, out string reason))
        {
            _view.ShowFeedback(reason, isError: true);
            return;
        }

        var result = _store.TryClaimMission(def);
        if (result == StashOperationResult.Success)
        {
            _view.ShowFeedback($"¡Recompensa del contrato '{def.Title}' reclamada con éxito!");
            Refresh();
        }
        else
        {
            _view.ShowFeedback($"Error al reclamar recompensa: {result}", isError: true);
        }
    }

    private void AcquireInputSuppression()
    {
        if (_inputSuppression != null || Runner == null) return;

        LocalInputContext context = Runner.GetComponent<LocalInputContext>();
        if (context == null || context.Reader == null) return;

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
        if (_view == null || !_view.IsOpen) return false;
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
}
