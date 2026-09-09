using UnityEngine;

/// <summary>
/// Observa el estado de salud de un enemigo para reproducir audios de daño y muerte,
/// sin interferir en la simulación.
/// </summary>
[DisallowMultipleComponent]
public class EnemyAudioPresenter : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField]
    private CharacterBase _characterBase;

    [Header("Configuration")]
    [SerializeField]
    private EnemyAudioConfig _audioConfig;

    private float _lastObservedHealth;
    private bool _isInitialized;
    private bool _isDead;

    private void OnEnable()
    {
        if (_characterBase == null)
        {
            _characterBase = GetComponentInParent<CharacterBase>();
        }

        InitializeHealthTracking();
    }

    private void OnDisable()
    {
        _isInitialized = false;
        _isDead = false;
    }

    private void LateUpdate()
    {
        if (_characterBase == null || _audioConfig == null || AudioManager.Instance == null) return;

        if (!_isInitialized)
        {
            if (_characterBase.Object != null && _characterBase.Object.IsValid)
            {
                _lastObservedHealth = _characterBase.Health;
                _isInitialized = true;
                _isDead = !_characterBase.IsAlive;
            }
            return;
        }

        // Check for death
        if (!_characterBase.IsAlive)
        {
            if (!_isDead)
            {
                _isDead = true;
                if (_audioConfig.TryGetClip("Death", out var clip))
                {
                    AudioManager.Instance.PlaySfx(clip, transform.position);
                }
            }
            return;
        }

        // Check for damage
        float currentHealth = _characterBase.Health;
        if (currentHealth < _lastObservedHealth - 0.001f) // Health epsilon
        {
            if (_audioConfig.TryGetClip("TakeDamage", out var clip))
            {
                AudioManager.Instance.PlaySfx(clip, transform.position);
            }
        }

        _lastObservedHealth = currentHealth;
    }

    private void InitializeHealthTracking()
    {
        if (_characterBase != null && _characterBase.Object != null && _characterBase.Object.IsValid)
        {
            _lastObservedHealth = _characterBase.Health;
            _isInitialized = true;
            _isDead = !_characterBase.IsAlive;
        }
        else
        {
            _isInitialized = false;
        }
    }
}
