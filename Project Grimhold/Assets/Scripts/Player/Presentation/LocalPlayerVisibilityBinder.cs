using Fusion;
using ProjectGrimhold.Gameplay.Visibility;
using UnityEngine;

/// <summary>
/// Owns the local-only vision for a raid avatar.
///
/// This Fusion presentation adapter registers the avatar's <see cref="LocalVisionSource"/> as the
/// scene viewer only for the current avatar with Input Authority. It owns
/// no networked state and does not participate in simulation or prediction.
/// </summary>
[DisallowMultipleComponent]
public sealed class LocalPlayerVisibilityBinder : NetworkBehaviour
{
    [SerializeField] private LocalVisionSource _visionSource;

    private RaidAvatarParticipantLink _participantLink;
    private EntityVisibilitySystem _visibilitySystem;
    private bool _isRegistered;

    private void Awake()
    {
        _participantLink = GetComponent<RaidAvatarParticipantLink>();
        SetVisionActive(false);
    }

    public override void Spawned()
    {
        RefreshLocalOwnership();
    }

    public override void Render()
    {
        RefreshLocalOwnership();
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        SetVisionActive(false);
    }

    private void OnDestroy()
    {
        SetVisionActive(false);
    }

    private void RefreshLocalOwnership()
    {
        SetVisionActive(HasInputAuthority && IsCurrentRaidAvatar());
    }

    private bool IsCurrentRaidAvatar()
    {
        if (_participantLink == null)
        {
            return true;
        }

        return _participantLink.TryResolveParticipant(out NetworkRaidParticipant participant) &&
            participant.TryResolveCurrentAvatar(out NetworkObject avatar) && avatar == Object;
    }

    private void SetVisionActive(bool active)
    {
        if (active)
        {
            TryRegisterLocalViewer();
            return;
        }

        UnregisterLocalViewer();
    }

    private void TryRegisterLocalViewer()
    {
        if (_isRegistered || _visionSource == null)
        {
            return;
        }

        _visibilitySystem = FindAnyObjectByType<EntityVisibilitySystem>(FindObjectsInactive.Exclude);
        if (_visibilitySystem == null)
        {
            return;
        }

        _visibilitySystem.RegisterLocalViewer(_visionSource);
        _isRegistered = true;
    }

    private void UnregisterLocalViewer()
    {
        if (!_isRegistered)
        {
            return;
        }

        if (_visibilitySystem != null)
        {
            _visibilitySystem.UnregisterLocalViewer(_visionSource);
        }

        _visibilitySystem = null;
        _isRegistered = false;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_visionSource == null)
        {
            _visionSource = GetComponent<LocalVisionSource>();
        }
    }
#endif
}
