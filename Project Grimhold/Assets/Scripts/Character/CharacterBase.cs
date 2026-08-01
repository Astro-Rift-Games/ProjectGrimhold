using Fusion;
using UnityEngine;

/// <summary>
/// Abstract base class for all game characters (Players, Enemies, and NPCs).
/// Implements the identity and damage contracts under an authoritative network model.
///
/// This class is part of the network and gameplay integration layer.
/// Manages the health, lifecycle, and death of the networked entity.
/// </summary>
[DisallowMultipleComponent]
public abstract class CharacterBase : NetworkBehaviour, ICharacter, IDamageable
{
    [Header("Health Configuration")]
    [SerializeField, Min(0.1f)]
    private float _maxHealth = 100f;

    [Header("Damage Collision")]
    [SerializeField]
    private Collider2D[] _damageHitboxes;

    private Collider2D[] _cachedColliders;
    private EntityRegistry _registry;
    private EntityId _registeredId;
    private bool _isRegistered;

    [Networked]
    public float Health { get; private set; }

    /// <summary>
    /// Gets the configured maximum health for this character.
    /// This is immutable local prefab configuration, not duplicated network state.
    /// </summary>
    public float MaxHealth => _maxHealth;

    /// <summary>
    /// Stable entity identifier in the gameplay core.
    /// Mapped from the network identifier assigned by Photon Fusion.
    /// </summary>
    public new EntityId Id => new EntityId(unchecked((int)Object.Id.Raw));

    /// <summary>
    /// Indicates whether the character is currently alive.
    /// </summary>
    public bool IsAlive => Object != null && Object.IsValid ? Health > 0f : true;

    /// <summary>
    /// Indicates whether the character can receive damage at this moment.
    /// Can be overridden to apply temporary invulnerability states.
    /// </summary>
    public virtual bool CanReceiveDamage => IsAlive;

    protected virtual void Awake()
    {
        _cachedColliders = GetComponentsInChildren<Collider2D>(true);
    }

    /// <summary>
    /// Initializes character state on the network and registers it with the EntityRegistry.
    /// </summary>
    public override void Spawned()
    {
        if (HasStateAuthority)
        {
            Health = _maxHealth;
        }

        // Keep identity mapped through every collider, but expose only the configured
        // body hitboxes to damage queries so movement collision cannot receive hits.
        _registry = Runner.GetComponent<EntityRegistry>();
        if (_registry != null)
        {
            _registeredId = Id;
            _isRegistered = _registry.TryRegisterDamageable(
                _registeredId,
                this,
                _cachedColliders,
                _damageHitboxes);

            if (!_isRegistered)
            {
                Debug.LogError(
                    $"{GetType().Name} could not register its damage hitboxes for entity '{_registeredId}'.",
                    this);
            }
        }
    }

    /// <summary>
    /// Removes the entity and its colliders from the EntityRegistry when despawned.
    /// </summary>
    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (_isRegistered && _registry != null)
        {
            _registry.Unregister(_registeredId, _cachedColliders);
            _isRegistered = false;
        }
    }

    /// <summary>
    /// Processes and applies a damage request to the character.
    /// This method requires State Authority to execute authoritatively.
    /// </summary>
    /// <param name="request">The detailed damage request.</param>
    /// <returns>The result of the processed damage.</returns>
    public DamageResult ApplyDamage(in DamageRequest request)
    {
        // Attackers or local clients cannot authoritatively confirm damage.
        if (!HasStateAuthority)
        {
            return new DamageResult(Id, false, 0f, Health, false, DamageFailureReason.MissingAuthority);
        }

        if (!IsAlive)
        {
            return new DamageResult(Id, false, 0f, Health, false, DamageFailureReason.TargetDead);
        }

        if (!CanReceiveDamage)
        {
            return new DamageResult(Id, false, 0f, Health, false, DamageFailureReason.TargetUnavailable);
        }

        if (request.Amount <= 0f)
        {
            return new DamageResult(Id, false, 0f, Health, false, DamageFailureReason.InvalidAmount);
        }

        float finalDamage = CalculateMitigatedDamage(request.Amount, request.DamageType);

        float previousHealth = Health;
        Health = Mathf.Max(0f, Health - finalDamage);
        float actualDamageApplied = previousHealth - Health;

        bool isFatal = Health <= 0f;

        if (isFatal)
        {
            HandleDeath();
        }

        return new DamageResult(
            Id,
            true,
            actualDamageApplied,
            Health,
            isFatal,
            DamageFailureReason.None
        );
    }

    /// <summary>
    /// Allows calculation of specific damage mitigations.
    /// Can be overridden by derived classes to incorporate armor, resistances, etc.
    /// </summary>
    /// <param name="amount">Original damage amount.</param>
    /// <param name="damageType">Damage type of the request.</param>
    /// <returns>The final damage amount after mitigations have been applied.</returns>
    protected virtual float CalculateMitigatedDamage(float amount, DamageType damageType)
    {
        return amount;
    }

    /// <summary>
    /// Method called when character health reaches 0 on the State Authority.
    /// </summary>
    protected virtual void HandleDeath()
    {
        // Base behavior for character death
    }
}
