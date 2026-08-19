using Fusion;
using UnityEngine;

/// <summary>
/// Binds the local player's combat events to the scene-level <see cref="CameraShakeController"/>.
///
/// Belongs to the local presentation layer. Not replicated.
/// Placed on the player prefab alongside <see cref="LocalPlayerCameraBinder"/>.
///
/// Triggers:
/// - Damage received (any source): detected by observing <see cref="CharacterBase.Health"/>
///   decreasing across Render frames. Works on both host and client because Fusion
///   replicates the [Networked] Health property.
/// - Damage dealt (to characters only): driven by the <see cref="PlayerCombatNetworkController.CombatFeedbackResolved"/>
///   presentation event. Breakable objects never register as <see cref="CharacterBase"/> in
///   the EntityRegistry, so they are naturally excluded from the dealt-damage shake.
/// </summary>
[DisallowMultipleComponent]
public sealed class LocalPlayerCameraShakeBinder : NetworkBehaviour
{
    [SerializeField]
    private PlayerCharacter _playerCharacter;

    [SerializeField]
    private PlayerCombatNetworkController _combatController;

    private CameraShakeController _shakeController;
    private CameraShakeConfig _config;
    private EntityRegistry _entityRegistry;
    private float _lastObservedHealth;
    private int _lastConsumedFeedbackSequence;
    private bool _isBound;

    /// <summary>
    /// Binds this binder to the local player's combat data.
    /// Must be called by <see cref="LocalPlayerHudBinder"/> when the local HUD is activated.
    /// </summary>
    /// <param name="config">Shake config asset to use for this session.</param>
    /// <param name="entityRegistry">Runner-scoped entity registry for character lookup.</param>
    public void Bind(CameraShakeConfig config, EntityRegistry entityRegistry)
    {
        Unbind();

        if (_playerCharacter == null || _combatController == null)
        {
            Debug.LogError(
                $"{nameof(LocalPlayerCameraShakeBinder)}: Missing player or combat controller dependencies.",
                this);
            return;
        }

        _config                      = config;
        _entityRegistry              = entityRegistry;
        _lastObservedHealth          = _playerCharacter.Health;
        _lastConsumedFeedbackSequence = _combatController.CurrentCombatFeedbackSequence;

        _shakeController = CameraShakeController.Instance;

        if (_shakeController != null && config != null)
        {
            _shakeController.Configure(config);
        }

        _combatController.CombatFeedbackResolved += OnCombatFeedbackResolved;
        _isBound = true;
    }

    /// <summary>
    /// Clears all subscriptions and resets internal state.
    /// Must be called when the local HUD is deactivated or the session ends.
    /// </summary>
    public void Unbind()
    {
        if (_isBound && _combatController != null)
        {
            _combatController.CombatFeedbackResolved -= OnCombatFeedbackResolved;
        }

        _config          = null;
        _entityRegistry  = null;
        _shakeController = null;
        _isBound         = false;
    }

    public override void Render()
    {
        if (!_isBound || _playerCharacter == null || _shakeController == null || _config == null)
        {
            return;
        }

        // Detect health loss by comparing against the last observed replicated value.
        // This approach works on both State Authority (host) and Input Authority (client)
        // because [Networked] Health is replicated to all peers before Render executes.
        float currentHealth = _playerCharacter.Health;
        if (currentHealth < _lastObservedHealth && _playerCharacter.IsAlive)
        {
            _shakeController.RequestShake(_config.ReceiveDamageIntensity, _config.ReceiveDamageDuration);
        }

        _lastObservedHealth = currentHealth;
    }

    private void OnDisable()
    {
        Unbind();
    }

    private void OnDestroy()
    {
        Unbind();
    }

    private void OnCombatFeedbackResolved(CombatPresentationEvent feedbackEvent)
    {
        if (!_isBound ||
            _shakeController == null ||
            _config == null ||
            feedbackEvent.Sequence <= _lastConsumedFeedbackSequence)
        {
            return;
        }

        _lastConsumedFeedbackSequence = feedbackEvent.Sequence;

        // Only shake on confirmed hits that dealt actual damage to a character.
        // Breakables do not register as CharacterBase in the EntityRegistry, so
        // IsCharacterEntity returns false for them.
        if (feedbackEvent.Kind != CombatFeedbackKind.ConfirmedImpact ||
            feedbackEvent.AppliedDamage <= 0f)
        {
            return;
        }

        if (!IsCharacterEntity(feedbackEvent.TargetId))
        {
            return;
        }

        _shakeController.RequestShake(_config.DealDamageIntensity, _config.DealDamageDuration);
    }

    /// <summary>
    /// Returns true when the given EntityId corresponds to a registered <see cref="CharacterBase"/>.
    /// Breakable objects and other non-character damageables return false.
    /// </summary>
    private bool IsCharacterEntity(EntityId targetId)
    {
        if (_entityRegistry == null || targetId.Value == 0)
        {
            return false;
        }

        if (!_entityRegistry.TryGetDamageable(targetId, out IDamageable damageable))
        {
            return false;
        }

        return damageable is CharacterBase;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_playerCharacter == null)
        {
            _playerCharacter = GetComponent<PlayerCharacter>();
        }

        if (_combatController == null)
        {
            _combatController = GetComponent<PlayerCombatNetworkController>();
        }
    }
#endif
}
