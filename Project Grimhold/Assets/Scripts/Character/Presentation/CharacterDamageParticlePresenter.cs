using UnityEngine;

/// <summary>
/// Provee feedback de partículas cuando el character recibe daño.
/// Observa la salud replicada en el presentation layer y emite las partículas configuradas.
/// </summary>
[DisallowMultipleComponent]
public sealed class CharacterDamageParticlePresenter : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField]
    private CharacterBase _characterBase;

    [Header("Configuration")]
    [SerializeField]
    [Tooltip("Sistema de partículas a reproducir cuando el character recibe daño. Debe estar hijo de este GameObject y no destruir en stop.")]
    private ParticleSystem _damageParticles;

    [SerializeField, Min(0f)]
    private float _healthEpsilon = 0.001f;

    [SerializeField, Min(0f), Tooltip("One-shot coalescing window for attacker-local confirmed melee particles, in seconds.")]
    private float _meleeMergeWindowSeconds = .15f;

    [SerializeField, Tooltip("Use this target's body color for confirmed melee impact particles only.")]
    private bool _overrideMeleeImpactColor;

    [SerializeField]
    private Color _meleeImpactColor = Color.white;

    public bool TryGetMeleeImpactColor(out Color color)
    {
        color = _meleeImpactColor;
        return _overrideMeleeImpactColor;
    }

    // Runtime state
    private float _lastObservedHealth;
    private bool _isInitialized;
    private float _genericStartedAt = float.NegativeInfinity;
    private float _genericDrop;
    private float _absorbUntil = float.NegativeInfinity;
    private float _absorbBudget;

    private void Awake()
    {
        if (_characterBase == null)
        {
            _characterBase = GetComponentInParent<CharacterBase>();
        }
    }

    private void OnDisable()
    {
        _isInitialized = false;
        _genericStartedAt = float.NegativeInfinity;
        _genericDrop = 0f;
        _absorbUntil = float.NegativeInfinity;
        _absorbBudget = 0f;

        if (_damageParticles != null)
        {
            _damageParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void LateUpdate()
    {
        if (_characterBase == null || _damageParticles == null)
        {
            return;
        }

        // Defer tracking initialization until the character's network object is spawned and valid
        if (!_isInitialized)
        {
            if (_characterBase.Object != null && _characterBase.Object.IsValid)
            {
                _lastObservedHealth = _characterBase.Health;
                _isInitialized = true;
            }
            return;
        }

        float currentHealth = _characterBase.Health;

        // Feedback should not trigger if the character is already dead
        if (!_characterBase.IsAlive)
        {
            _lastObservedHealth = currentHealth;
            return;
        }

        if (currentHealth < _lastObservedHealth - _healthEpsilon)
        {
            TriggerDamageParticles(_lastObservedHealth - currentHealth);
        }

        _lastObservedHealth = currentHealth;
    }

    /// <summary>
    /// Only the attacker's confirmation path calls this, with the confirmed applied damage. Health
    /// observations carry no source, so correlation is by amount inside a short window: a generic burst
    /// is replaced, or upcoming drops are absorbed, only when the confirmed melee damage explains them.
    /// A larger drop includes another source and keeps its generic particles. Other peers never call this.
    /// </summary>
    public void AbsorbConfirmedMeleeImpact(float appliedDamage)
    {
        if (!isActiveAndEnabled || _damageParticles == null || !(appliedDamage > 0f) ||
            float.IsInfinity(appliedDamage)) return;
        float now = Time.time;
        float window = Mathf.Max(0f, _meleeMergeWindowSeconds);
        if (now - _genericStartedAt <= window)
        {
            // The Health drop arrived first. Clear its burst only if this melee damage accounts for it.
            if (_genericDrop <= appliedDamage + Tolerance)
            {
                _damageParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                _genericStartedAt = float.NegativeInfinity;
                _genericDrop = 0f;
            }
            return;
        }
        // Simultaneous melee hits on one target can land in a single observed drop: accumulate their damage.
        _absorbBudget = now <= _absorbUntil ? _absorbBudget + appliedDamage : appliedDamage;
        _absorbUntil = now + window;
    }

    private float Tolerance => Mathf.Max(_healthEpsilon, .01f);

    private void TriggerDamageParticles(float drop)
    {
        float now = Time.time;
        if (now <= _absorbUntil && drop <= _absorbBudget + Tolerance)
        {
            _absorbBudget -= drop;
            if (_absorbBudget <= Tolerance) ClearAbsorb();
            return;
        }
        // Expired, or the drop includes damage the confirmed melee cannot explain: keep generic feedback.
        ClearAbsorb();
        _genericStartedAt = now;
        _genericDrop = drop;
        ParticleEffectPlayer.PlayInPlace(_damageParticles, transform.position);
    }

    private void ClearAbsorb()
    {
        _absorbUntil = float.NegativeInfinity;
        _absorbBudget = 0f;
    }
}
