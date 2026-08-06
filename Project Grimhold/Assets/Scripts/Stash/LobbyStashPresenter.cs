using UnityEngine;

/// <summary>
/// The Presenter component of the MVP pattern for the Lobby Stash.
/// Bridges the UI view and the Stash Service, isolating logic from rendering.
/// </summary>
public class LobbyStashPresenter : MonoBehaviour
{
    [SerializeField] private LobbyStashUI _stashUI;
    private IPlayerStashService _stashService;
    private ProfileId _localProfileId;

    private void OnEnable()
    {
        _localProfileId = LocalProfileProvider.GetOrCreateLocalProfile();

        var context = FindAnyObjectByType<ApplicationStashContext>();
        if (context != null)
        {
            _stashService = context.StashService;
            if (_stashService != null)
            {
                _stashService.StashChanged += OnStashChanged;
                RefreshUI();
            }
        }
        else
        {
            Debug.LogWarning("[LobbyStashPresenter] ApplicationStashContext not found. Stash UI will be empty.");
        }
    }

    private void OnDisable()
    {
        if (_stashService != null)
        {
            _stashService.StashChanged -= OnStashChanged;
        }
    }

    private void OnStashChanged(ProfileId updatedProfileId)
    {
        if (updatedProfileId.Value == _localProfileId.Value)
        {
            RefreshUI();
        }
    }

    private void RefreshUI()
    {
        if (_stashUI == null || _stashService == null)
            return;

        var items = _stashService.GetStash(_localProfileId);
        _stashUI.DisplayStash(items);
    }
}
