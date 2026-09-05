using System;
using Fusion;
using UnityEngine;

/// <summary>Binds only the Input Authority Town player to confirmed local character attributes.</summary>
[DisallowMultipleComponent]
public sealed class TownAttributeAssignmentPresenter : NetworkBehaviour
{
    private TownAttributeAssignmentBinding _binding;
    private TownAttributeAssignmentView _view;
    private ApplicationStashContext _profileContext;
    private LocalProfileStore _store;
    private PlayerInputReader _inputReader;
    private IDisposable _inputSuppression;

    public override void Spawned() => TryBind();

    private void OnEnable()
    {
        if (Object != null && Object.IsValid)
        {
            TryBind();
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState) => Cleanup();
    private void OnDisable() => Cleanup();
    private void OnDestroy() => Cleanup();

    private void TryBind()
    {
        if (_binding != null || Object == null || !Object.IsValid || !HasInputAuthority)
        {
            return;
        }

        LocalPlayerJoinContext joinContext = Runner != null
            ? Runner.GetComponent<LocalPlayerJoinContext>()
            : null;
        ProfileId localProfileId = joinContext != null
            ? joinContext.JoinData.ProfileId
            : default;
        LocalInputContext inputContext = Runner != null
            ? Runner.GetComponent<LocalInputContext>()
            : null;

        _profileContext = FindAnyObjectByType<ApplicationStashContext>();
        _store = _profileContext != null ? _profileContext.Store : null;
        _inputReader = inputContext != null ? inputContext.Reader : null;
        if (_profileContext == null || _store == null || _inputReader == null ||
            !localProfileId.IsValid || _store.ProfileId != localProfileId)
        {
            ClearReferences();
            return;
        }

        _view = TownAttributeAssignmentView.Create(transform);
        if (_view == null)
        {
            Debug.LogError(
                $"[{nameof(TownAttributeAssignmentPresenter)}] Missing required Resources prefab '{TownAttributeAssignmentView.ResourcesPrefabName}.prefab'.",
                this);
            ClearReferences();
            return;
        }

        _view.Close();
        _view.AssignmentRequested += AssignAttribute;
        _view.CloseRequested += ClosePanel;
        _inputReader.AttributesToggleRequested += TogglePanel;
        _inputReader.InventoryCloseRequested += TryClosePanelFromInput;

        ApplicationStashContext boundContext = _profileContext;
        LocalProfileStore boundStore = _store;
        _binding = new TownAttributeAssignmentBinding(
            localProfileId,
            ProgressionBalanceDefaults.InitialMaximumAttributeValue,
            boundStore.TryGetCharacterAttributeState,
            handler => boundContext.ProfileCommitted += handler,
            handler => boundContext.ProfileCommitted -= handler,
            Present,
            PresentUnavailable);
    }

    private void TogglePanel()
    {
        if (_view == null)
        {
            return;
        }

        if (_view.IsOpen)
        {
            ClosePanel();
            return;
        }

        if (_inputReader == null || _inputReader.IsGameplayInputSuppressed)
        {
            return;
        }

        _inputSuppression = _inputReader.AcquireGameplayInputSuppression();
        _view.Open();
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

    private async void AssignAttribute(CharacterAttribute attribute)
    {
        if (_store != null)
        {
            var commitResult = _store.TryAssignCharacterAttribute(attribute, out _);
            if (commitResult == CharacterAttributeAssignmentCommitResult.Success &&
                _store.TryGetCharacterAttributeState(out CharacterAttributeState state))
            {
                var remoteService = _profileContext != null ? _profileContext.GetComponent<RemoteInventoryService>() : null;
                if (remoteService != null)
                {
                    var dto = new Grimhold.Backend.CharacterAttributesData
                    {
                        vitality = state.Vitality,
                        resistance = state.Resistance,
                        strength = state.Strength,
                        dexterity = state.Dexterity,
                        intelligence = state.Intelligence,
                        luck = state.Luck,
                        availablePoints = state.AvailablePoints
                    };
                    var (success, error) = await remoteService.CommitProgressionAsync(dto);
                    if (success)
                    {
                        Debug.Log($"[TownAttributeAssignmentPresenter] Attributes committed to backend successfully.");
                    }
                    else
                    {
                        Debug.LogError($"[TownAttributeAssignmentPresenter] Failed to commit attributes: {error.error} - {error.message}");
                    }
                }
                else
                {
                    Debug.LogWarning("[TownAttributeAssignmentPresenter] RemoteInventoryService not found, attributes not committed to backend.");
                }
            }
        }
    }

    private void Present(TownAttributeAssignmentPresentation presentation) =>
        _view?.Present(presentation);

    private void PresentUnavailable() => _view?.PresentUnavailable();

    private void ClosePanel()
    {
        _view?.Close();
        _inputSuppression?.Dispose();
        _inputSuppression = null;
    }

    private void Cleanup()
    {
        _binding?.Dispose();
        _binding = null;

        if (_inputReader != null)
        {
            _inputReader.AttributesToggleRequested -= TogglePanel;
            _inputReader.InventoryCloseRequested -= TryClosePanelFromInput;
        }

        if (_view != null)
        {
            _view.AssignmentRequested -= AssignAttribute;
            _view.CloseRequested -= ClosePanel;
        }

        ClosePanel();
        if (_view != null)
        {
            Destroy(_view.gameObject);
            _view = null;
        }

        ClearReferences();
    }

    private void ClearReferences()
    {
        _profileContext = null;
        _store = null;
        _inputReader = null;
    }
}
