using UnityEngine;

/// <summary>
/// Provides procedural visual feedback (sprite flash and scale pulse) when a character receives damage.
/// Operates on the presentation layer, observing the networked health of a CharacterBase.
/// </summary>
[DisallowMultipleComponent]
public sealed class DamageFeedbackPresenter : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField]
    private CharacterBase _characterBase;

    [SerializeField]
    private Transform _visualTransform;

    [SerializeField]
    private SpriteRenderer[] _spriteRenderers;

    [Header("Feedback Configuration")]
    [SerializeField, Min(0.001f)]
    private float _duration = 0.2f;

    [SerializeField]
    private Color _flashColor = Color.red;

    [SerializeField, Min(1f)]
    private float _maxScaleMultiplier = 1.08f;

    [SerializeField]
    private AnimationCurve _flashCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);

    [SerializeField]
    private AnimationCurve _scaleCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.5f, 1f),
        new Keyframe(1f, 0f)
    );

    [SerializeField, Min(0f)]
    private float _healthEpsilon = 0.001f;

    [SerializeField, Min(0f), Tooltip("Coalesces nearby generic Health feedback with confirmed attacker-local melee feedback.")]
    private float _meleeMergeWindowSeconds = .15f;

    [SerializeField, Min(0f), Tooltip("Consecutive melee confirmations inside this window keep the current reaction.")]
    private float _meleeRetriggerGuardSeconds = .05f;

    private HitReactionPlayback _meleePlayback;
    private bool IsMeleeReactionActive => _meleePlayback != null && _meleePlayback.IsActive;
    // Evaluated only on a melee hit or while its reaction is active, never on every idle frame.
    private bool IsDowned => PlayerDownedGate.IsDowned(_characterBase);
    private float _genericStartedAt;

    // Runtime state
    private float _lastObservedHealth;
    private bool _isInitialized;
    private float _elapsedTime;
    private bool _isFeedbackActive;

    // Cached base states
    private Vector3 _baseLocalScale;
    private Color[] _originalColors;
    private bool _hasCapturedBaseState;

    private void OnEnable()
    {
        CacheDependencies();
        CaptureBaseState();
        InitializeHealthTracking();
    }

    private void OnDisable()
    {
        CancelAndRestore();
        _isInitialized = false;
    }

    private void LateUpdate()
    {
        if (_characterBase == null)
        {
            return;
        }

        if (_isInitialized && (_characterBase.Object == null || !_characterBase.Object.IsValid))
        {
            CancelAndRestore();
            _isInitialized = false;
            return;
        }

        // Defer tracking initialization until the character's network object is spawned and valid
        if (!_isInitialized)
        {
            if (_characterBase.Object != null && _characterBase.Object.IsValid)
            {
                _lastObservedHealth = _characterBase.Health;
                _isInitialized = true;
                // Re-capture state if not done successfully during OnEnable
                CaptureBaseState();
            }
            return;
        }

        float currentHealth = _characterBase.Health;

        // Damage feedback should not run or restart if the character is dead/defeated
        if (!_characterBase.IsAlive)
        {
            if (_isFeedbackActive || IsMeleeReactionActive)
            {
                CancelAndRestore();
            }
            _lastObservedHealth = currentHealth;
            return;
        }

        // Downed only ends the melee reaction; the generic Health feedback keeps its previous behavior.
        if (IsMeleeReactionActive && IsDowned)
        {
            _meleePlayback.Restore();
        }

        _meleePlayback?.Tick(Time.deltaTime);

        if (currentHealth < _lastObservedHealth - _healthEpsilon)
        {
            TriggerFeedback();
        }
        else if (currentHealth > _lastObservedHealth)
        {
            // Health increased (e.g. healing), update reference silently without feedback
            _lastObservedHealth = currentHealth;
        }

        _lastObservedHealth = currentHealth;

        if (_isFeedbackActive)
        {
            UpdateFeedback();
        }
    }

    private void CacheDependencies()
    {
        if (_characterBase == null)
        {
            _characterBase = GetComponentInParent<CharacterBase>();
        }
    }

    private void CaptureBaseState()
    {
        if (_hasCapturedBaseState)
        {
            return;
        }

        if (_visualTransform != null)
        {
            _baseLocalScale = _visualTransform.localScale;
        }

        if (_spriteRenderers != null && _spriteRenderers.Length > 0)
        {
            _originalColors = new Color[_spriteRenderers.Length];
            for (int i = 0; i < _spriteRenderers.Length; i++)
            {
                if (_spriteRenderers[i] != null)
                {
                    _originalColors[i] = _spriteRenderers[i].color;
                }
            }
        }

        _meleePlayback = new HitReactionPlayback(_visualTransform, _spriteRenderers, _meleeRetriggerGuardSeconds);
        _hasCapturedBaseState = true;
    }

    private void InitializeHealthTracking()
    {
        if (_characterBase != null && _characterBase.Object != null && _characterBase.Object.IsValid)
        {
            _lastObservedHealth = _characterBase.Health;
            _isInitialized = true;
        }
        else
        {
            _isInitialized = false;
        }
    }

    /// <summary>Plays only on the peer that receives the attacker's confirmed melee result.</summary>
    public bool TryPlayMeleeReaction(MeleeHitReactionProfile profile, Vector2 direction, float delaySeconds, float holdSeconds)
    {
        if (!isActiveAndEnabled || !_isInitialized || _characterBase == null || !_characterBase.IsAlive ||
            IsDowned || _meleePlayback == null ||
            profile == null || !profile.TryValidate(out _) ||
            float.IsNaN(delaySeconds) || float.IsInfinity(delaySeconds) ||
            float.IsNaN(holdSeconds) || float.IsInfinity(holdSeconds)) return false;

        // A recent Health pulse is the same contact observed before the RPC. Do not replace an older,
        // unrelated generic pulse. Other peers never enter this path and keep their existing feedback.
        if (_isFeedbackActive)
        {
            if (Time.time - _genericStartedAt > _meleeMergeWindowSeconds) return false;
            CancelAndRestore();
        }
        return _meleePlayback.Play(profile, direction, delaySeconds, holdSeconds);
    }

    private void TriggerFeedback()
    {
        if (_meleePlayback != null && _meleePlayback.AbsorbsHealthFeedback(_meleeMergeWindowSeconds)) return;
        CancelAndRestore();
        _genericStartedAt = Time.time;
        _isFeedbackActive = true;
        _elapsedTime = 0f;

        // Apply initial frame of feedback immediately
        UpdateFeedback();
    }

    private void UpdateFeedback()
    {
        _elapsedTime += Time.deltaTime;
        float normalizedTime = Mathf.Clamp01(_elapsedTime / _duration);

        float flashFactor = _flashCurve != null ? _flashCurve.Evaluate(normalizedTime) : 0f;
        float scaleFactor = _scaleCurve != null ? _scaleCurve.Evaluate(normalizedTime) : 0f;

        // Apply flash color
        if (_spriteRenderers != null && _originalColors != null)
        {
            for (int i = 0; i < _spriteRenderers.Length; i++)
            {
                if (_spriteRenderers[i] != null && i < _originalColors.Length)
                {
                    _spriteRenderers[i].color = Color.Lerp(_originalColors[i], _flashColor, flashFactor);
                }
            }
        }

        // Apply scale pulse
        if (_visualTransform != null)
        {
            _visualTransform.localScale = _baseLocalScale * Mathf.Lerp(1f, _maxScaleMultiplier, scaleFactor);
        }

        if (_elapsedTime >= _duration)
        {
            CancelAndRestore();
        }
    }

    /// <summary>
    /// Forcefully stops the damage feedback presentation and restores base scale and colors.
    /// Safe to call multiple times or when visual references are unassigned.
    /// </summary>
    public void CancelAndRestore()
    {
        _isFeedbackActive = false;
        _elapsedTime = 0f;
        _meleePlayback?.Restore();

        if (!_hasCapturedBaseState)
        {
            return;
        }

        if (_visualTransform != null)
        {
            _visualTransform.localScale = _baseLocalScale;
        }

        if (_spriteRenderers != null && _originalColors != null)
        {
            for (int i = 0; i < _spriteRenderers.Length; i++)
            {
                if (_spriteRenderers[i] != null && i < _originalColors.Length)
                {
                    _spriteRenderers[i].color = _originalColors[i];
                }
            }
        }
    }
}
