using UnityEngine;

/// <summary>
/// Abstract base presenter responsible for procedural attack presentation (melee swings and ranged arcs).
/// Subscribes to combat controller network events to drive local visual state across players and enemies.
/// </summary>
[DisallowMultipleComponent]
public abstract class CombatPresenterBase : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField]
    private MonoBehaviour _combatControllerSource;

    [SerializeField]
    private MonoBehaviour _animatorViewSource;

    [SerializeField]
    private Transform _weaponPivot;

    [SerializeField]
    private SpriteRenderer _weaponSpriteRenderer;

    [Header("Attack Presentation")]
    [SerializeField, Min(0.001f)]
    private float _attackDuration = 0.2f;

    [SerializeField]
    private float _attackStartAngle = -45f;

    [SerializeField]
    private float _attackEndAngle = 45f;

    [SerializeField]
    private AnimationCurve _attackAngleCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [SerializeField, Min(0f)]
    private float _weaponHandDistance = 0.2f;

    [SerializeField]
    private int _sortingOrderFront = 10;

    [SerializeField]
    private int _sortingOrderBack = -10;

    [SerializeField]
    private float _verticalSortingThreshold = 0f;

    [SerializeField]
    private bool _hideWeaponAtEnd = true;

    private ICombatController _combatController;
    private IAnimatorController _animatorView;

    // Base state capture
    private Vector3 _baseLocalPosition;
    private Quaternion _baseLocalRotation;
    private Vector3 _baseLocalScale;
    private int _baseSortingOrder;
    private bool _baseSpriteRendererEnabled;
    private bool _baseGameObjectActive;

    private bool _hasCapturedBaseState;
    private bool _isSubscribed;

    // Animation runtime state
    private bool _animationActive;
    private Vector2 _attackDirection;
    private Vector2 _snappedDirection;
    private float _animationElapsedTime;

    protected virtual void Awake()
    {
        CacheDependencies();
    }

    protected virtual void OnEnable()
    {
        CaptureBaseState();
        Subscribe();
    }

    protected virtual void OnDisable()
    {
        Unsubscribe();
        CancelAndRestore();
    }

    protected virtual void CacheDependencies()
    {
        if (_combatControllerSource != null)
        {
            _combatController = _combatControllerSource as ICombatController;
        }

        if (_combatController == null)
        {
            _combatController = GetComponentInParent<ICombatController>();
        }

        if (_animatorViewSource != null)
        {
            _animatorView = _animatorViewSource as IAnimatorController;
        }

        if (_animatorView == null)
        {
            _animatorView = GetComponentInParent<IAnimatorController>();
        }
    }

    private void CaptureBaseState()
    {
        if (_hasCapturedBaseState)
        {
            return;
        }

        if (_weaponPivot != null)
        {
            _baseLocalPosition = _weaponPivot.localPosition;
            _baseLocalRotation = _weaponPivot.localRotation;
            _baseLocalScale = _weaponPivot.localScale;
        }

        if (_weaponSpriteRenderer != null)
        {
            _baseSortingOrder = _weaponSpriteRenderer.sortingOrder;
            _baseSpriteRendererEnabled = _weaponSpriteRenderer.enabled;
            _baseGameObjectActive = _weaponSpriteRenderer.gameObject.activeSelf;
        }

        _hasCapturedBaseState = true;
    }

    private void Subscribe()
    {
        if (_isSubscribed)
        {
            return;
        }

        if (_combatController != null)
        {
            _combatController.AttackPerformed += OnAttackPerformed;
            _isSubscribed = true;
        }
    }

    private void Unsubscribe()
    {
        if (!_isSubscribed)
        {
            return;
        }

        if (_combatController != null)
        {
            _combatController.AttackPerformed -= OnAttackPerformed;
            _isSubscribed = false;
        }
    }

    private Vector2 SnapDirectionToEightWays(Vector2 direction)
    {
        if (direction.sqrMagnitude < 0.0001f)
        {
            return Vector2.right;
        }

        Vector2 norm = direction.normalized;
        float angleRad = Mathf.Atan2(norm.y, norm.x);
        float angleDeg = angleRad * Mathf.Rad2Deg;

        float snappedAngleDeg = Mathf.Round(angleDeg / 45f) * 45f;
        float snappedAngleRad = snappedAngleDeg * Mathf.Deg2Rad;

        return new Vector2(Mathf.Cos(snappedAngleRad), Mathf.Sin(snappedAngleRad));
    }

    /// <summary>
    /// Called when the combat controller raises an attack event during Render.
    /// Override in subclasses to replace the default procedural weapon animation.
    /// </summary>
    protected virtual void OnAttackPerformed(AttackPerformedEvent attackEvent)
    {
        if (_combatController != null)
        {
            MonoBehaviour controllerMb = _combatController as MonoBehaviour;
            if (controllerMb != null)
            {
                CharacterBase character = controllerMb.GetComponentInParent<CharacterBase>();
                if (character != null && !character.IsAlive)
                {
                    return;
                }
            }
        }

        CancelAndRestore();

        _animationActive = true;
        _attackDirection = attackEvent.Direction.sqrMagnitude > 0.0001f ? attackEvent.Direction.normalized : Vector2.right;
        _snappedDirection = SnapDirectionToEightWays(_attackDirection);
        _animationElapsedTime = 0f;

        if (_animatorView != null)
        {
            _animatorView.ApplyTemporalFacingDirection(_snappedDirection);
        }

        if (_weaponSpriteRenderer != null)
        {
            _weaponSpriteRenderer.gameObject.SetActive(true);
            _weaponSpriteRenderer.enabled = true;
        }

        EvaluatePresentation(0f);
    }

    protected virtual void LateUpdate()
    {
        if (_combatController != null)
        {
            MonoBehaviour controllerMb = _combatController as MonoBehaviour;
            if (controllerMb != null)
            {
                CharacterBase character = controllerMb.GetComponentInParent<CharacterBase>();
                if (character != null && !character.IsAlive)
                {
                    if (_animationActive)
                    {
                        CancelAndRestore();
                    }
                    return;
                }
            }
        }

        if (!_animationActive)
        {
            return;
        }

        _animationElapsedTime += Time.deltaTime;
        float duration = Mathf.Max(0.001f, _attackDuration);
        float progress = Mathf.Clamp01(_animationElapsedTime / duration);

        EvaluatePresentation(progress);

        if (progress >= 1f)
        {
            CompletePresentation();
        }
    }

    private void EvaluatePresentation(float progress)
    {
        if (_weaponPivot == null || _weaponSpriteRenderer == null)
        {
            return;
        }

        float snappedAngleRad = Mathf.Atan2(_snappedDirection.y, _snappedDirection.x);
        float snappedAngleDeg = snappedAngleRad * Mathf.Rad2Deg;

        Vector3 basePos = _baseLocalPosition;
        if (_weaponHandDistance > 0f)
        {
            Vector3 localOffset = new Vector3(_snappedDirection.x, _snappedDirection.y, 0f) * _weaponHandDistance;
            basePos = _baseLocalPosition + localOffset;
        }

        float t = _attackAngleCurve.Evaluate(progress);
        float currentSwingAngle = Mathf.Lerp(_attackStartAngle, _attackEndAngle, t);
        float finalAngle = snappedAngleDeg + currentSwingAngle;

        _weaponPivot.localRotation = Quaternion.Euler(0f, 0f, finalAngle);
        _weaponPivot.localPosition = basePos;
        _weaponPivot.localScale = _baseLocalScale;

        if (_snappedDirection.y > _verticalSortingThreshold)
        {
            _weaponSpriteRenderer.sortingOrder = _sortingOrderBack;
        }
        else
        {
            _weaponSpriteRenderer.sortingOrder = _sortingOrderFront;
        }
    }

    private void CompletePresentation()
    {
        _animationActive = false;

        if (_weaponPivot != null)
        {
            _weaponPivot.localPosition = _baseLocalPosition;
            _weaponPivot.localRotation = _baseLocalRotation;
            _weaponPivot.localScale = _baseLocalScale;
        }

        if (_weaponSpriteRenderer != null)
        {
            _weaponSpriteRenderer.sortingOrder = _baseSortingOrder;
        }

        if (_animatorView != null)
        {
            _animatorView.ClearTemporalFacingDirection();
        }

        if (_weaponSpriteRenderer != null && _hideWeaponAtEnd)
        {
            _weaponSpriteRenderer.enabled = false;
        }
    }

    /// <summary>
    /// Cancels any active attack presentation and completely restores the weapon pivot and renderer to base states.
    /// </summary>
    public void CancelAndRestore()
    {
        _animationActive = false;

        if (_animatorView != null)
        {
            _animatorView.ClearTemporalFacingDirection();
        }

        if (!_hasCapturedBaseState)
        {
            return;
        }

        if (_weaponPivot != null)
        {
            _weaponPivot.localPosition = _baseLocalPosition;
            _weaponPivot.localRotation = _baseLocalRotation;
            _weaponPivot.localScale = _baseLocalScale;
        }

        if (_weaponSpriteRenderer != null)
        {
            _weaponSpriteRenderer.sortingOrder = _baseSortingOrder;
            _weaponSpriteRenderer.enabled = _baseSpriteRendererEnabled;
            if (gameObject.activeInHierarchy)
            {
                _weaponSpriteRenderer.gameObject.SetActive(_baseGameObjectActive);
            }
        }
    }

#if UNITY_EDITOR
    protected virtual void OnValidate()
    {
        CacheDependencies();
    }
#endif
}
