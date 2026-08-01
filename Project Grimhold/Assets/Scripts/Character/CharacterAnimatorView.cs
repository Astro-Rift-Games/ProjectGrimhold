using UnityEngine;

/// <summary>
/// Updates character animation parameters based on any component implementing <see cref="IMovementState"/>.
///
/// This component belongs to the presentation layer. It reads values from the movement simulation
/// and updates the Unity Animator without participating in network simulation or predictions.
/// </summary>
[DisallowMultipleComponent]
public class CharacterAnimatorView : MonoBehaviour, IAnimatorController
{
    [SerializeField]
    private Animator _animator;

    protected Animator AnimatorInstance => _animator;

    [SerializeField]
    private MonoBehaviour _movementControllerSource;

    private IMovementState _movementState;

    private int _moveXHash;
    private int _moveYHash;
    private int _isMovingHash;
    private int _onDefeatedHash;
    private int _onAttackHash;

    private bool _hashesInitialized;
    private Vector2? _temporalFacingDirection;
    private bool _isDefeated;

    protected virtual void Awake()
    {
        InitializeHashes();
        CacheDependencies();
    }

    protected virtual void OnDisable()
    {
        _temporalFacingDirection = null;
        _isDefeated = false;
    }

    protected virtual void LateUpdate()
    {
        if (_movementState == null || _animator == null)
        {
            return;
        }

        if (!_hashesInitialized)
        {
            InitializeHashes();
        }

        Vector2 facing;
        bool isMoving;

        if (_isDefeated)
        {
            facing = _movementState.FacingDirection;
            isMoving = false;
        }
        else if (_temporalFacingDirection.HasValue)
        {
            facing = _temporalFacingDirection.Value;
            isMoving = false;
        }
        else
        {
            facing = _movementState.FacingDirection;
            isMoving = _movementState.IsMoving;
        }

        if (facing.sqrMagnitude < 0.001f)
        {
            facing = Vector2.down;
        }

        _animator.SetFloat(_moveXHash, facing.x);
        _animator.SetFloat(_moveYHash, facing.y);
        _animator.SetBool(_isMovingHash, isMoving);
    }

    /// <summary>
    /// Sets the defeated visual state of the animator, halting locomotion animation.
    /// </summary>
    public void SetDefeated(bool defeated)
    {
        _isDefeated = defeated;
        if (defeated)
        {
            _temporalFacingDirection = null;
            if (_animator != null)
            {
                _animator.SetTrigger(_onDefeatedHash);
            }
        }
    }

    /// <summary>
    /// Applies a temporal facing direction that overrides locomotion facing direction.
    /// </summary>
    public void ApplyTemporalFacingDirection(Vector2 direction)
    {
        if (_isDefeated)
        {
            return;
        }
        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }
        _temporalFacingDirection = direction.normalized;
    }

    /// <summary>
    /// Clears any temporal facing direction, returning the animator to standard locomotion state.
    /// </summary>
    public void ClearTemporalFacingDirection()
    {
        _temporalFacingDirection = null;
    }

    /// <summary>
    /// Fires the OnAttack trigger on the Animator to start a controller-driven attack animation.
    /// </summary>
    public void TriggerAttack()
    {
        if (_animator != null)
        {
            _animator.SetTrigger(_onAttackHash);
        }
    }

    private void InitializeHashes()
    {
        _moveXHash = Animator.StringToHash("MoveX");
        _moveYHash = Animator.StringToHash("MoveY");
        _isMovingHash = Animator.StringToHash("IsMoving");
        _onDefeatedHash = Animator.StringToHash("OnDefeated");
        _onAttackHash = Animator.StringToHash("OnAttack");
        _hashesInitialized = true;
    }

    protected virtual void CacheDependencies()
    {
        if (_animator == null)
        {
            _animator = GetComponentInChildren<Animator>();
        }

        if (_movementControllerSource != null)
        {
            _movementState = _movementControllerSource as IMovementState;
        }

        if (_movementState == null)
        {
            _movementState = GetComponentInParent<IMovementState>();
        }
    }

#if UNITY_EDITOR
    protected virtual void OnValidate()
    {
        CacheDependencies();
    }

    protected virtual void Reset()
    {
        CacheDependencies();
    }
#endif
}
