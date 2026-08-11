using Fusion;
using ProjectGrimhold.Gameplay.Visibility;
using UnityEngine;

/// <summary>
/// Owns the local-only line-of-sight presentation stack for a raid avatar.
///
/// This Fusion presentation adapter enables the visibility mesh and mask camera
/// only for the current avatar with Input Authority. It owns no networked state
/// and does not participate in simulation or prediction.
/// </summary>
[DisallowMultipleComponent]
public sealed class LocalPlayerVisibilityBinder : NetworkBehaviour
{
    [SerializeField] private GameObject _visibilityRoot;
    [SerializeField] private VisibilityMeshBuilder _meshBuilder;

    private RaidAvatarParticipantLink _participantLink;
    private EntityVisibilitySystem _visibilitySystem;
    private bool _isRegistered;

    private void Awake()
    {
        _participantLink = GetComponent<RaidAvatarParticipantLink>();
        SetVisibilityActive(false);
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
        SetVisibilityActive(false);
    }

    private void OnDestroy()
    {
        SetVisibilityActive(false);
    }

    private void RefreshLocalOwnership()
    {
        SetVisibilityActive(HasInputAuthority && IsCurrentRaidAvatar());
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

    private void SetVisibilityActive(bool active)
    {
        if (_visibilityRoot != null && _visibilityRoot.activeSelf != active)
        {
            _visibilityRoot.SetActive(active);
        }

        if (active)
        {
            TryRegisterLocalMeshBuilder();
            return;
        }

        UnregisterLocalMeshBuilder();
    }

    private void TryRegisterLocalMeshBuilder()
    {
        if (_isRegistered || _meshBuilder == null)
        {
            return;
        }

        _visibilitySystem = FindAnyObjectByType<EntityVisibilitySystem>(FindObjectsInactive.Exclude);
        if (_visibilitySystem == null)
        {
            return;
        }

        _visibilitySystem.RegisterLocalMeshBuilder(_meshBuilder);
        _isRegistered = true;
    }

    private void UnregisterLocalMeshBuilder()
    {
        if (!_isRegistered)
        {
            return;
        }

        if (_visibilitySystem != null)
        {
            _visibilitySystem.UnregisterLocalMeshBuilder(_meshBuilder);
        }

        _visibilitySystem = null;
        _isRegistered = false;
    }


#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_visibilityRoot == null)
        {
            Transform visibilityTransform = transform.Find("VisibilityMesh");
            _visibilityRoot = visibilityTransform != null ? visibilityTransform.gameObject : null;
        }

        if (_meshBuilder == null && _visibilityRoot != null)
        {
            _meshBuilder = _visibilityRoot.GetComponent<VisibilityMeshBuilder>();
        }
    }
#endif
}
