using Fusion;
using UnityEngine;

/// <summary>
/// Owns the local Town lifecycle that connects the shared inventory screen to the
/// confirmed application-level Loadout. It never owns or replicates inventory state.
/// </summary>
[DisallowMultipleComponent]
public sealed class TownInventoryBinder : NetworkBehaviour
{
    [SerializeField]
    private RaidInventoryPresenter _inventoryPresenter;

    private ApplicationStashContext _profileContext;
    private LocalInputContext _inputContext;
    private LocalLoadoutInventoryReadSource _inventorySource;
    private bool _isLifecycleBound;
    private bool _missingDependenciesReported;

    public override void Spawned()
    {
        TryBindLifecycle();
    }

    private void OnEnable()
    {
        if (Object != null && Object.IsValid)
        {
            TryBindLifecycle();
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

    private void TryBindLifecycle()
    {
        if (_isLifecycleBound || Object == null || !Object.IsValid || !HasInputAuthority)
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
        _inputContext = Runner != null ? Runner.GetComponent<LocalInputContext>() : null;
        if (_inventoryPresenter == null || _profileContext == null || !_profileContext.IsAvailable ||
            _profileContext.LoadoutService == null || _inputContext == null ||
            !localProfileId.IsValid || _profileContext.ProfileId != localProfileId)
        {
            ReportMissingDependencies();
            ClearReferences();
            return;
        }

        _inventorySource = new LocalLoadoutInventoryReadSource(_profileContext, localProfileId);
        _inputContext.ReaderChanged += OnInputReaderChanged;
        _isLifecycleBound = true;
        OnInputReaderChanged(_inputContext.Reader);
    }

    private void OnInputReaderChanged(PlayerInputReader inputReader)
    {
        if (!_isLifecycleBound || _inventoryPresenter == null)
        {
            return;
        }

        _inventoryPresenter.Unbind();
        if (inputReader != null)
        {
            _inventoryPresenter.BindTown(_inventorySource, inputReader);
        }
    }

    private void Cleanup()
    {
        if (_inputContext != null)
        {
            _inputContext.ReaderChanged -= OnInputReaderChanged;
        }

        _inventoryPresenter?.Unbind();
        _inventorySource?.Dispose();
        _inventorySource = null;
        _isLifecycleBound = false;
        ClearReferences();
    }

    private void ClearReferences()
    {
        _profileContext = null;
        _inputContext = null;
    }

    private void ReportMissingDependencies()
    {
        if (_missingDependenciesReported)
        {
            return;
        }

        _missingDependenciesReported = true;
        Debug.LogError(
            $"{nameof(TownInventoryBinder)} could not bind the local persistent Loadout or input context.",
            this);
    }
}
