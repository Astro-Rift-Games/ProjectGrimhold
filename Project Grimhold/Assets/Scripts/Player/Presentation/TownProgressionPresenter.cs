using Fusion;
using UnityEngine;

/// <summary>
/// Binds only the local Town player to the persistent progression stored for that profile.
/// </summary>
[DisallowMultipleComponent]
public sealed class TownProgressionPresenter : NetworkBehaviour
{
    private TownProgressionBinding _binding;
    private TownProgressionView _view;
    private ApplicationStashContext _profileContext;
    private LocalProfileStore _store;
    private bool _reportedMissingContext;
    private bool _reportedProfileMismatch;
    private bool _reportedInvalidState;
    private bool _reportedMissingView;

    public override void Spawned()
    {
        TryBind();
    }

    private void OnEnable()
    {
        if (Object != null && Object.IsValid)
        {
            TryBind();
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        Cleanup();
    }

    private void OnDisable()
    {
        Cleanup();
    }

    private void OnDestroy()
    {
        Cleanup();
    }

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

        _profileContext = FindAnyObjectByType<ApplicationStashContext>();
        _store = _profileContext != null ? _profileContext.Store : null;
        if (_profileContext == null || _store == null || !_store.IsAvailable)
        {
            ReportMissingContext();
            ClearLocalReferences();
            return;
        }

        if (!localProfileId.IsValid || _store.ProfileId != localProfileId)
        {
            ReportProfileMismatch();
            ClearLocalReferences();
            return;
        }

        ApplicationStashContext boundContext = _profileContext;
        LocalProfileStore boundStore = _store;
        _binding = new TownProgressionBinding(
            localProfileId,
            ProgressionBalanceDefaults.InitialExperienceCurve,
            () => (boundStore.GetLevel(), boundStore.GetCurrentExperience()),
            handler => boundContext.ProfileCommitted += handler,
            handler => boundContext.ProfileCommitted -= handler,
            Present,
            PresentUnavailable);
    }

    private void Present(TownProgressionPresentation presentation)
    {
        if (_view == null)
        {
            _view = TownProgressionView.Create(transform);
            if (_view == null)
            {
                ReportMissingView();
                return;
            }
        }

        _view.Present(presentation);
    }

    private void PresentUnavailable()
    {
        DestroyView();
        if (!_reportedInvalidState)
        {
            _reportedInvalidState = true;
            Debug.LogError(
                $"[{nameof(TownProgressionPresenter)}] The local persistent progression state is incompatible with the configured curve.",
                this);
        }
    }

    private void Cleanup()
    {
        _binding?.Dispose();
        _binding = null;

        DestroyView();

        ClearLocalReferences();
    }

    private void DestroyView()
    {
        if (_view == null)
        {
            return;
        }

        _view.Hide();
        Destroy(_view.gameObject);
        _view = null;
    }

    private void ClearLocalReferences()
    {
        _profileContext = null;
        _store = null;
    }

    private void ReportMissingContext()
    {
        if (_reportedMissingContext)
        {
            return;
        }

        _reportedMissingContext = true;
        Debug.LogError(
            $"[{nameof(TownProgressionPresenter)}] The local persistent profile context is unavailable.",
            this);
    }

    private void ReportProfileMismatch()
    {
        if (_reportedProfileMismatch)
        {
            return;
        }

        _reportedProfileMismatch = true;
        Debug.LogError(
            $"[{nameof(TownProgressionPresenter)}] The Town player profile does not match the local persistent profile.",
            this);
    }

    private void ReportMissingView()
    {
        if (_reportedMissingView)
        {
            return;
        }

        _reportedMissingView = true;
        Debug.LogError(
            $"[{nameof(TownProgressionPresenter)}] Missing required Resources prefab '{TownProgressionView.ResourcesPrefabName}.prefab'.",
            this);
    }
}
