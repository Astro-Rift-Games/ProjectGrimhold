using System.Collections;
using UnityEngine;

/// <summary>
/// Observa eventos de combate y reproduce los efectos de sonido del arma actualmente equipada,
/// resolviendo la configuración de audio dinámicamente desde el WeaponDefinition o usando un fallback.
/// Soporta Swing, Shoot, Reload, y reacciones de impacto (Attack contra personajes, Block contra destructibles o escenario).
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

    [Header("Ranged Configuration")]
    [SerializeField, Min(0f)]
    [Tooltip("Retardo en segundos antes de disparar el sonido de Reload tras un disparo a distancia.")]
    private float _reloadDelaySeconds = 0.25f;

    [Header("Environment Detection")]
    [SerializeField]
    [Tooltip("Capas consideradas escenario u obstáculo para impactos de bloqueo.")]
    private LayerMask _obstacleLayers = 1 << 6 | 1 << 11; // Layer 6: WorldCollision, Layer 11: Obstacles

    [SerializeField, Min(0.1f)]
    private float _sceneryCheckRadius = 0.8f;

    private ICombatController _combatController;
    private PlayerCombatNetworkController _playerCombatController;
    private bool _isSubscribed;
    private Coroutine _reloadCoroutine;
    private EntityRegistry _entityRegistry;

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
        if (_reloadCoroutine != null)
        {
            StopCoroutine(_reloadCoroutine);
            _reloadCoroutine = null;
        }
    }

    private void CacheDependencies()
    {
        if (_combatControllerSource != null)
        {
            _combatController = _combatControllerSource as ICombatController;
            _playerCombatController = _combatControllerSource as PlayerCombatNetworkController;
        }

        if (_combatController == null)
        {
            _combatController = GetComponentInParent<ICombatController>();
            _playerCombatController = GetComponentInParent<PlayerCombatNetworkController>();
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
        }

        if (_playerCombatController != null)
        {
            _playerCombatController.CombatFeedbackResolved += OnCombatFeedbackResolved;
        }

        _isSubscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_isSubscribed) return;

        if (_combatController != null)
        {
            _combatController.AttackPerformed -= OnAttackPerformed;
        }

        if (_playerCombatController != null)
        {
            _playerCombatController.CombatFeedbackResolved -= OnCombatFeedbackResolved;
        }

        _isSubscribed = false;
    }

    private void OnAttackPerformed(AttackPerformedEvent attackEvent)
    {
        if (AudioManager.Instance == null) return;

        WeaponAudioConfig config = ResolveAudioConfig();
        if (config == null) return;

        if (attackEvent.AttackType == AttackType.Melee)
        {
            // 1. Sonido de Swing
            if (config.TryGetClip("Swing", out var swingClip))
            {
                AudioManager.Instance.PlaySfx(swingClip, transform.position);
            }

            // 2. Chequeo de colisión con escenario estático (WorldCollision / Obstacles)
            Vector2 origin = attackEvent.Origin != Vector2.zero ? attackEvent.Origin : (Vector2)transform.position;
            Vector2 checkPosition = origin + attackEvent.Direction * (_sceneryCheckRadius * 0.7f);
            Collider2D obstacleHit = Physics2D.OverlapCircle(checkPosition, _sceneryCheckRadius, _obstacleLayers);

            if (obstacleHit != null)
            {
                if (config.TryGetClip("Block", out var blockClip))
                {
                    AudioManager.Instance.PlaySfx(blockClip, obstacleHit.transform.position);
                }
            }
        }
        else if (attackEvent.AttackType == AttackType.Ranged)
        {
            // 1. Sonido de Shoot
            if (config.TryGetClip("Shoot", out var shootClip))
            {
                AudioManager.Instance.PlaySfx(shootClip, transform.position);
            }

            // 2. Sonido de Reload temporizado al inicio del cooldown
            if (config.TryGetClip("Reload", out var reloadClip))
            {
                if (_reloadCoroutine != null)
                {
                    StopCoroutine(_reloadCoroutine);
                }
                _reloadCoroutine = StartCoroutine(PlayDelayedReload(reloadClip, _reloadDelaySeconds));
            }
        }
    }

    private void OnCombatFeedbackResolved(CombatPresentationEvent feedbackEvent)
    {
        if (AudioManager.Instance == null) return;
        if (feedbackEvent.Kind != CombatFeedbackKind.ConfirmedImpact || feedbackEvent.AppliedDamage <= 0f) return;

        WeaponAudioConfig config = ResolveAudioConfig();
        if (config == null) return;

        Vector3 hitWorldPos = new Vector3(feedbackEvent.HitPoint.x, feedbackEvent.HitPoint.y, transform.position.z);

        if (IsCharacterEntity(feedbackEvent.TargetId))
        {
            // Impacto confirmado contra enemigo
            if (config.TryGetClip("Attack", out var attackClip))
            {
                AudioManager.Instance.PlaySfx(attackClip, hitWorldPos);
            }
        }
        else
        {
            // Impacto confirmado contra destructible (BreakableObject) u obstáculo interactivo
            if (config.TryGetClip("Block", out var blockClip))
            {
                AudioManager.Instance.PlaySfx(blockClip, hitWorldPos);
            }
        }
    }

    private IEnumerator PlayDelayedReload(CustomClip reloadClip, float delay)
    {
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySfx(reloadClip, transform.position);
        }

        _reloadCoroutine = null;
    }

    private bool IsCharacterEntity(EntityId targetId)
    {
        if (_entityRegistry == null)
        {
            if (Fusion.NetworkRunner.Instances != null)
            {
                foreach (var runner in Fusion.NetworkRunner.Instances)
                {
                    if (runner != null && runner.IsRunning)
                    {
                        _entityRegistry = runner.GetComponent<EntityRegistry>();
                        if (_entityRegistry != null) break;
                    }
                }
            }

            if (_entityRegistry == null)
            {
                _entityRegistry = FindAnyObjectByType<EntityRegistry>();
            }
        }

        if (_entityRegistry == null || targetId.Value == 0) return false;

        if (_entityRegistry.TryGetDamageable(targetId, out IDamageable damageable))
        {
            return damageable is CharacterBase;
        }

        return false;
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
