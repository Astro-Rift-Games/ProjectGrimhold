using UnityEngine;

/// <summary>
/// Observa eventos del combate de un arma y reproduce efectos de sonido de ataque,
/// delegando la reproducción real al AudioManager global.
/// </summary>
[DisallowMultipleComponent]
public class WeaponAudioPresenter : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField]
    private MonoBehaviour _combatControllerSource;

    [Header("Configuration")]
    [SerializeField]
    private WeaponAudioConfig _audioConfig;

    private ICombatController _combatController;
    private bool _isSubscribed;

    private void Awake()
    {
        CacheDependencies();
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void CacheDependencies()
    {
        if (_combatControllerSource != null)
        {
            _combatController = _combatControllerSource as ICombatController;
        }

        if (_combatController == null)
        {
            _combatController = GetComponentInParent<ICombatController>();
        }
    }

    private void Subscribe()
    {
        if (_isSubscribed) return;

        if (_combatController != null)
        {
            _combatController.AttackPerformed += OnAttackPerformed;
            _isSubscribed = true;
        }
    }

    private void Unsubscribe()
    {
        if (!_isSubscribed) return;

        if (_combatController != null)
        {
            _combatController.AttackPerformed -= OnAttackPerformed;
            _isSubscribed = false;
        }
    }

    private void OnAttackPerformed(AttackPerformedEvent attackEvent)
    {
        if (_audioConfig == null || AudioManager.Instance == null) return;

        if (_audioConfig.TryGetClip("Swing", out var clip))
        {
            AudioManager.Instance.PlaySfx(clip, transform.position);
        }
    }
}
