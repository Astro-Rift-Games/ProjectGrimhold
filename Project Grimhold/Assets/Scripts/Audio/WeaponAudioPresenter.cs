using UnityEngine;

/// <summary>
/// Observa eventos de combate y reproduce los efectos de sonido del arma actualmente equipada,
/// resolviendo la configuración de audio dinámicamente desde el WeaponDefinition o usando un fallback.
/// </summary>
[DisallowMultipleComponent]
public class WeaponAudioPresenter : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField]
    private MonoBehaviour _combatControllerSource;

    [SerializeField]
    private PlayerWeaponEquipmentNetworkController _equipmentSource;

    [Header("Fallback Configuration")]
    [SerializeField]
    [Tooltip("Configuración opcional de fallback en caso de no contar con un controlador de equipamiento dinámico.")]
    private WeaponAudioConfig _fallbackAudioConfig;

    private ICombatController _combatController;
    private bool _isSubscribed;

    private void Awake()
    {
        CacheDependencies();
    }

    private void OnEnable()
    {
        CacheDependencies();
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

        if (_equipmentSource == null)
        {
            _equipmentSource = GetComponentInParent<PlayerWeaponEquipmentNetworkController>();
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
        if (AudioManager.Instance == null) return;

        WeaponAudioConfig config = ResolveAudioConfig();
        if (config == null) return;

        if (config.TryGetClip("Swing", out var clip))
        {
            AudioManager.Instance.PlaySfx(clip, transform.position);
        }
    }

    private WeaponAudioConfig ResolveAudioConfig()
    {
        if (_equipmentSource == null)
        {
            _equipmentSource = GetComponentInParent<PlayerWeaponEquipmentNetworkController>();
        }

        if (_equipmentSource != null && _equipmentSource.TryGetEquippedDefinition(out LootDefinition lootDefinition))
        {
            if (lootDefinition != null && lootDefinition.WeaponDefinition != null && lootDefinition.WeaponDefinition.AudioConfig != null)
            {
                return lootDefinition.WeaponDefinition.AudioConfig;
            }
        }

        return _fallbackAudioConfig;
    }
}
