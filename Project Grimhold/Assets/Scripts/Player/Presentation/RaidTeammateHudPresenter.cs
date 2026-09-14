using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// Resolves the frozen Raid teammate and projects replicated Health into the local HUD.
/// </summary>
[DisallowMultipleComponent]
public sealed class RaidTeammateHudPresenter : MonoBehaviour
{
    internal enum ProjectionMode
    {
        Unavailable,
        LiveHealth,
        ExtractedCache,
        Defeated
    }

    private enum PresentationMode
    {
        None,
        Unavailable,
        Health,
        Defeated
    }

    [SerializeField]
    private RaidTeammateHudView _view;

    private readonly List<NetworkObject> _networkObjects = new();

    private NetworkRunner _runner;
    private NetworkRaidParticipant _localParticipant;
    private NetworkRaidParticipant _teammateParticipant;
    private PlayerCharacter _teammateCharacter;
    private ProfileId _teammateProfileId;
    private string _boundGenerationId;
    private NetworkId _observedAvatarId;
    private bool _isBound;
    private bool _hasTeammate;

    private bool _hasLastHealth;
    private float _lastHealth;
    private float _lastMaximumHealth;

    private PresentationMode _presentedMode;
    private bool _hasPresented;
    private float _presentedHealth;
    private float _presentedMaximumHealth;
    private bool _presentedHasMaximumHealth;

    public void Bind(
        NetworkRunner runner,
        NetworkRaidParticipant localParticipant,
        RaidInitialAffiliationSnapshot affiliations)
    {
        Unbind();

        _runner = runner;
        _localParticipant = localParticipant;
        _boundGenerationId = localParticipant != null
            ? localParticipant.RaidGenerationId.ToString()
            : string.Empty;
        ProfileId localProfileId = default;
        _isBound = runner != null && localParticipant != null && affiliations != null &&
            TryReadProfileId(localParticipant, out localProfileId);
        _hasTeammate = _isBound &&
            affiliations.TryGetTeammateProfileId(localProfileId, out _teammateProfileId);

        _view?.SetVisible(_hasTeammate);
        if (!_hasTeammate)
        {
            return;
        }

        PresentUnavailable();
        Refresh();
    }

    public void Unbind()
    {
        _runner = null;
        _localParticipant = null;
        _teammateParticipant = null;
        _teammateCharacter = null;
        _teammateProfileId = default;
        _boundGenerationId = string.Empty;
        _observedAvatarId = default;
        _isBound = false;
        _hasTeammate = false;
        _hasLastHealth = false;
        _lastHealth = 0f;
        _lastMaximumHealth = 0f;
        _networkObjects.Clear();
        ResetPresentedState();
        _view?.PresentUnavailable();
        _view?.SetVisible(false);
    }

    private void OnEnable()
    {
        if (!_isBound || !_hasTeammate)
        {
            return;
        }

        ResetPresentedState();
        _view?.SetVisible(true);
        Refresh();
    }

    private void OnDisable()
    {
        ResetPresentedState();
        _view?.SetVisible(false);
    }

    private void OnDestroy()
    {
        Unbind();
    }

    private void Update()
    {
        if (_isBound && _hasTeammate)
        {
            Refresh();
        }
    }

    private void Refresh()
    {
        if (!HasValidBinding())
        {
            Unbind();
            return;
        }

        if (!IsValidTeammate(_teammateParticipant) && !TryResolveTeammateParticipant())
        {
            InvalidateAvatar();
            PresentUnavailable();
            return;
        }

        NetworkId currentAvatarId = _teammateParticipant.CurrentAvatarId;
        if (currentAvatarId != _observedAvatarId || !IsValidCharacter(_teammateCharacter))
        {
            _observedAvatarId = currentAvatarId;
            _teammateCharacter = null;
            TryResolveCurrentCharacter();
        }

        bool hasValidCharacter = IsValidCharacter(_teammateCharacter);
        switch (ResolveProjectionMode(
                    _teammateParticipant.State,
                    hasValidCharacter,
                    _hasLastHealth))
        {
            case ProjectionMode.LiveHealth:
                float health = _teammateCharacter.Health;
                float maximumHealth = _teammateCharacter.MaxHealth;
                _hasLastHealth = true;
                _lastHealth = health;
                _lastMaximumHealth = maximumHealth;
                PresentHealth(health, maximumHealth);
                return;
            case ProjectionMode.ExtractedCache:
                PresentHealth(_lastHealth, _lastMaximumHealth);
                return;
            case ProjectionMode.Defeated:
                PresentDefeated();
                return;
            default:
                PresentUnavailable();
                return;
        }
    }

    private bool HasValidBinding()
    {
        if (_runner == null || !_runner.IsRunning || _localParticipant == null ||
            _localParticipant.Object == null || !_localParticipant.Object.IsValid ||
            _localParticipant.Runner != _runner)
        {
            return false;
        }

        return string.Equals(
            _localParticipant.RaidGenerationId.ToString(),
            _boundGenerationId,
            StringComparison.Ordinal);
    }

    private bool TryResolveTeammateParticipant()
    {
        _teammateParticipant = null;
        NetworkRaidParticipant resolved = null;

        foreach (PlayerRef player in _runner.ActivePlayers)
        {
            NetworkObject playerObject = _runner.GetPlayerObject(player);
            if (playerObject != null &&
                playerObject.TryGetBehaviour(out NetworkRaidParticipant candidate) &&
                IsMatchingTeammate(candidate))
            {
                resolved = candidate;
                break;
            }
        }

        _networkObjects.Clear();
        _runner.GetAllNetworkObjects(_networkObjects);
        for (int index = 0; index < _networkObjects.Count; index++)
        {
            NetworkObject networkObject = _networkObjects[index];
            if (networkObject == null ||
                !networkObject.TryGetBehaviour(out NetworkRaidParticipant candidate) ||
                !IsMatchingTeammate(candidate))
            {
                continue;
            }

            if (resolved != null && !ReferenceEquals(resolved, candidate))
            {
                _teammateParticipant = null;
                return false;
            }

            resolved = candidate;
        }

        _teammateParticipant = resolved;
        InvalidateAvatar();
        return _teammateParticipant != null;
    }

    private bool IsValidTeammate(NetworkRaidParticipant participant) =>
        participant != null && participant.Object != null && participant.Object.IsValid &&
        participant.Runner == _runner && IsMatchingTeammate(participant);

    private bool IsMatchingTeammate(NetworkRaidParticipant participant)
    {
        return participant != null && participant != _localParticipant &&
            string.Equals(
                participant.ProfileId.ToString(),
                _teammateProfileId.Value,
                StringComparison.Ordinal) &&
            string.Equals(
                participant.RaidGenerationId.ToString(),
                _boundGenerationId,
                StringComparison.Ordinal);
    }

    private void TryResolveCurrentCharacter()
    {
        if (!_observedAvatarId.IsValid || _teammateParticipant == null ||
            !_teammateParticipant.TryResolveCurrentAvatar(out NetworkObject avatar) ||
            avatar == null || !avatar.IsValid || avatar.Id != _observedAvatarId ||
            avatar.Runner != _runner || !avatar.TryGetBehaviour(out _teammateCharacter))
        {
            _teammateCharacter = null;
        }
    }

    private bool IsValidCharacter(PlayerCharacter character) =>
        character != null && character.Object != null && character.Object.IsValid &&
        character.Runner == _runner && character.Object.Id == _observedAvatarId;

    private void InvalidateAvatar()
    {
        _observedAvatarId = default;
        _teammateCharacter = null;
    }

    private void PresentUnavailable()
    {
        if (_hasPresented && _presentedMode == PresentationMode.Unavailable)
        {
            return;
        }

        RecordPresentation(PresentationMode.Unavailable, 0f, 0f, false);
        _view?.PresentUnavailable();
    }

    private void PresentHealth(float health, float maximumHealth)
    {
        if (_hasPresented && _presentedMode == PresentationMode.Health &&
            Mathf.Approximately(_presentedHealth, health) &&
            Mathf.Approximately(_presentedMaximumHealth, maximumHealth))
        {
            return;
        }

        RecordPresentation(PresentationMode.Health, health, maximumHealth, true);
        _view?.PresentHealth(health, maximumHealth);
    }

    private void PresentDefeated()
    {
        bool hasMaximumHealth = _hasLastHealth;
        float maximumHealth = hasMaximumHealth ? _lastMaximumHealth : 0f;
        if (_hasPresented && _presentedMode == PresentationMode.Defeated &&
            _presentedHasMaximumHealth == hasMaximumHealth &&
            Mathf.Approximately(_presentedMaximumHealth, maximumHealth))
        {
            return;
        }

        RecordPresentation(PresentationMode.Defeated, 0f, maximumHealth, hasMaximumHealth);
        _view?.PresentDefeated(maximumHealth, hasMaximumHealth);
    }

    private void RecordPresentation(
        PresentationMode mode,
        float health,
        float maximumHealth,
        bool hasMaximumHealth)
    {
        _hasPresented = true;
        _presentedMode = mode;
        _presentedHealth = health;
        _presentedMaximumHealth = maximumHealth;
        _presentedHasMaximumHealth = hasMaximumHealth;
    }

    private void ResetPresentedState()
    {
        _hasPresented = false;
        _presentedMode = PresentationMode.None;
        _presentedHealth = 0f;
        _presentedMaximumHealth = 0f;
        _presentedHasMaximumHealth = false;
    }

    private static bool TryReadProfileId(
        NetworkRaidParticipant participant,
        out ProfileId profileId)
    {
        profileId = participant != null
            ? new ProfileId(participant.ProfileId.ToString())
            : default;
        return profileId.IsValid;
    }

    internal static ProjectionMode ResolveProjectionMode(
        RaidParticipantState state,
        bool hasValidCharacter,
        bool hasCachedHealth)
    {
        if (state == RaidParticipantState.Defeated)
        {
            return ProjectionMode.Defeated;
        }

        if (state == RaidParticipantState.Aborted)
        {
            return ProjectionMode.Unavailable;
        }

        if (hasValidCharacter)
        {
            return ProjectionMode.LiveHealth;
        }

        return state == RaidParticipantState.Extracted && hasCachedHealth
            ? ProjectionMode.ExtractedCache
            : ProjectionMode.Unavailable;
    }
}
