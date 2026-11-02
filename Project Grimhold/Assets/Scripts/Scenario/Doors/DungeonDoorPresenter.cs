using Fusion;
using UnityEngine;

/// <summary>Maps replicated state/time to editable sprite clips and local placeholder audio.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NetworkDoor))]
public sealed class DungeonDoorPresenter : NetworkBehaviour
{
    // Full paths disambiguate Animator states without per-frame string allocation.
    private static readonly int Closed = Animator.StringToHash("Base Layer.Door_Closed");
    private static readonly int OpenNorth = Animator.StringToHash("Base Layer.Door_Open_North");
    private static readonly int OpeningNorth = Animator.StringToHash("Base Layer.Door_Opening_North");
    private static readonly int ClosingNorth = Animator.StringToHash("Base Layer.Door_Closing_North");
    private static readonly int OpenSouth = Animator.StringToHash("Base Layer.Door_Open_South");
    private static readonly int OpeningSouth = Animator.StringToHash("Base Layer.Door_Opening_South");
    private static readonly int ClosingSouth = Animator.StringToHash("Base Layer.Door_Closing_South");
    // Half a sprite-frame (0.05s of the 0.4s clip) tolerates normal render/tick ordering.
    private const float MaximumNormalizedDrift = 0.125f;

    [SerializeField] private NetworkDoor _door;
    [SerializeField] private Animator _animator;
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _openingClip;
    [SerializeField] private AudioClip _closingClip;

    private bool _dependenciesValid;
    private bool _hasAppliedPose;
    private DungeonDoorState _lastObservedState;
    private DungeonDoorOpenDirection _lastObservedDirection;

    public override void Spawned()
    {
        _hasAppliedPose = false;
        _dependenciesValid = _door != null && _door.Object == Object && _animator != null &&
            _animator.GetComponentInParent<NetworkObject>() == Object &&
            _animator.runtimeAnimatorController != null &&
            _animator.HasState(0, Closed) && _animator.HasState(0, OpenNorth) &&
            _animator.HasState(0, OpeningNorth) && _animator.HasState(0, ClosingNorth) &&
            _animator.HasState(0, OpenSouth) && _animator.HasState(0, OpeningSouth) &&
            _animator.HasState(0, ClosingSouth) && _audioSource != null &&
            _openingClip != null && _closingClip != null;

        if (!_dependenciesValid)
        {
            Debug.LogError($"{nameof(DungeonDoorPresenter)} requires its co-located NetworkDoor, an Animator " +
                "under its NetworkObject with a controller containing Door_Closed and Door_Open/Opening/Closing_North/South, " +
                "an AudioSource and opening/closing placeholder clips.", this);
            return;
        }

        _animator.applyRootMotion = false;
        _animator.updateMode = AnimatorUpdateMode.Normal;
        _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        // Late joins/restores adopt the pose on first Render, without replaying audio.
        _lastObservedState = _door.State;
        _lastObservedDirection = _door.OpenDirection;
    }

    public override void Render()
    {
        if (!_dependenciesValid || !_door.IsReady)
        {
            return;
        }

        DungeonDoorState state = _door.State;
        DungeonDoorOpenDirection direction = _door.OpenDirection;
        int stateHash = ResolveAnimationState(state, direction);
        bool transition = !DungeonDoorStateRules.IsStable(state);
        float progress = transition ? _door.TransitionProgress : 0f;
        AnimatorStateInfo current = _animator.GetCurrentAnimatorStateInfo(0);
        if (!_hasAppliedPose || state != _lastObservedState || direction != _lastObservedDirection ||
            current.fullPathHash != stateHash ||
            (transition && Mathf.Abs(Mathf.Clamp01(current.normalizedTime) - progress) > MaximumNormalizedDrift))
        {
            // Presentation only: the replicated deadline, never clip completion, owns gameplay.
            _animator.Play(stateHash, 0, progress);
            _hasAppliedPose = true;
        }

        bool stateChanged = state != _lastObservedState;
        _lastObservedState = state;
        _lastObservedDirection = direction;
        if (stateChanged && state == DungeonDoorState.Opening)
        {
            _audioSource.PlayOneShot(_openingClip);
        }
        else if (stateChanged && state == DungeonDoorState.Closing)
        {
            _audioSource.PlayOneShot(_closingClip);
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        _dependenciesValid = false;
        _hasAppliedPose = false;
    }

    private static int ResolveAnimationState(DungeonDoorState state, DungeonDoorOpenDirection direction)
    {
        bool south = direction == DungeonDoorOpenDirection.South;
        return state switch
        {
            DungeonDoorState.Open => south ? OpenSouth : OpenNorth,
            DungeonDoorState.Opening => south ? OpeningSouth : OpeningNorth,
            DungeonDoorState.Closing => south ? ClosingSouth : ClosingNorth,
            _ => Closed
        };
    }
}
