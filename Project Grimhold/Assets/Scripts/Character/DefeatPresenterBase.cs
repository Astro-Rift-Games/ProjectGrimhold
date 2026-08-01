using UnityEngine;

/// <summary>
/// Presenter component responsible for coordinating enemy defeat presentation.
/// Operates on the presentation layer, observing the networked health state of a CharacterBase.
///
/// On death, this component:
/// - Notifies the animator view to trigger the defeat animation.
/// - Cancels any active combat or damage feedback presentation.
/// - Hides the weapon sprite.
/// - Disables colliders so the corpse no longer participates in hit detection.
///
/// Visual defeat animation is handled entirely by the Animator Controller.
/// </summary>
[DisallowMultipleComponent]
public abstract class DefeatPresenterBase : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField]
    private CharacterBase _characterBase;

    [SerializeField]
    private MonoBehaviour _animatorViewSource;

    [SerializeField]
    private CombatPresenterBase _combatPresenter;

    [SerializeField]
    private DamageFeedbackPresenter _damageFeedbackPresenter;

    [SerializeField]
    private SpriteRenderer _weaponSpriteRenderer;

    [SerializeField]
    private Collider2D[] _colliders;

    private IAnimatorController _animatorView;

    // Runtime state
    private bool _isDefeated;
    private bool _isInitialized;

    protected virtual void OnEnable()
    {
        CacheDependencies();
        InitializeDeathTracking();
    }

    protected virtual void OnDisable()
    {
        CancelAndRestore();
        _isInitialized = false;
    }

    protected virtual void LateUpdate()
    {
        if (_characterBase == null)
        {
            return;
        }

        if (!_isInitialized)
        {
            if (_characterBase.Object != null && _characterBase.Object.IsValid)
            {
                _isInitialized = true;

                if (!_characterBase.IsAlive)
                {
                    TriggerDefeat();
                }
            }
            return;
        }

        if (!_characterBase.IsAlive && !_isDefeated)
        {
            TriggerDefeat();
        }
    }

    protected virtual void CacheDependencies()
    {
        if (_characterBase == null)
        {
            _characterBase = GetComponentInParent<CharacterBase>();
        }

        if (_animatorViewSource != null)
        {
            _animatorView = _animatorViewSource as IAnimatorController;
        }

        if (_animatorView == null)
        {
            _animatorView = GetComponentInParent<IAnimatorController>();
        }

        if (_combatPresenter == null)
        {
            _combatPresenter = GetComponentInChildren<CombatPresenterBase>();
        }

        if (_damageFeedbackPresenter == null)
        {
            _damageFeedbackPresenter = GetComponentInChildren<DamageFeedbackPresenter>();
        }

        if (_colliders == null || _colliders.Length == 0)
        {
            _colliders = GetComponentsInParent<Collider2D>();
        }
    }

    private void InitializeDeathTracking()
    {
        if (_characterBase != null && _characterBase.Object != null && _characterBase.Object.IsValid)
        {
            _isInitialized = true;
            if (!_characterBase.IsAlive)
            {
                TriggerDefeat();
            }
        }
        else
        {
            _isInitialized = false;
        }
    }

    private void TriggerDefeat()
    {
        _isDefeated = true;

        if (_combatPresenter != null)
        {
            _combatPresenter.CancelAndRestore();
        }

        if (_damageFeedbackPresenter != null)
        {
            _damageFeedbackPresenter.CancelAndRestore();
        }

        if (_weaponSpriteRenderer != null)
        {
            _weaponSpriteRenderer.enabled = false;
        }

        if (_animatorView != null)
        {
            _animatorView.SetDefeated(true);
        }

        SetCollidersEnabled(false);
    }

    /// <summary>
    /// Cancels defeat presentation and restores animation and collider states.
    /// </summary>
    public void CancelAndRestore()
    {
        _isDefeated = false;

        if (_animatorView != null)
        {
            _animatorView.SetDefeated(false);
        }

        SetCollidersEnabled(true);
    }

    private void SetCollidersEnabled(bool enabled)
    {
        if (_colliders == null)
        {
            return;
        }

        for (int i = 0; i < _colliders.Length; i++)
        {
            if (_colliders[i] != null)
            {
                _colliders[i].enabled = enabled;
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
