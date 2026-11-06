using UnityEngine;
using Fusion;
using Spawning;

/// <summary>
/// Owns the authoritative enemy death transition.
/// A defeated enemy remains the same network entity and exposes its co-located
/// loot container without spawning a replacement corpse object.
/// </summary>
[DisallowMultipleComponent]
public class EnemyCharacter : CharacterBase, IAbilityEnemyTarget
{
    [Networked]
    public EnemyPopulationOrigin PopulationOrigin { get; set; }

    [Header("Creature Classification")]
    [SerializeField]
    private CreatureTier _creatureTier = CreatureTier.Ordinary;

    public CreatureTier CreatureTier
    {
        get => _creatureTier;
        set => _creatureTier = value;
    }

    [Header("Specter Integration")]
    [SerializeField]
    private ProjectGrimhold.Gameplay.Enemies.Specter.SpecterAIController _specterController;

    public ProjectGrimhold.Gameplay.Enemies.Specter.SpecterAIController SpecterController => _specterController;

    [Header("Death Dependencies")]
    [SerializeField]
    private EnemyMovementAIController _movementController;

    [SerializeField]
    private EnemyCombatAIController _combatController;

    [SerializeField]
    private NetworkLootContainer _lootContainer;

    [SerializeField]
    private EnemyFSM _fsm;

    [Tooltip("Knockback Resistance (RE)")]
    [SerializeField, Min(0f)] private float _knockbackResistance;

    private bool _deathDependenciesValid;

    protected NetworkLootContainer LootContainer => _lootContainer;

    public override bool CanReceiveDamage =>
        IsAlive &&
        (_specterController == null || ProjectGrimhold.Gameplay.Enemies.Specter.SpecterRules.CanReceiveDamage(_specterController.CurrentState));

    /// <summary>Resolves this enemy's configured knockback resistance (RE).</summary>
    protected override float ResolveKnockbackResistance() => _knockbackResistance;

    protected override float CalculateMitigatedDamage(in DamageRequest request)
    {
        if (_specterController != null)
        {
            return ProjectGrimhold.Gameplay.Enemies.Specter.SpecterRules.CalculateMitigatedDamage(
                _specterController.CurrentState,
                request.Amount,
                request.DamageType);
        }

        return base.CalculateMitigatedDamage(request);
    }

    protected override void OnDamageApplied(in DamageRequest request, float amount)
    {
        base.OnDamageApplied(request, amount);

        if (_specterController != null)
        {
            _specterController.OnDamageReceived(
                request.AttackerId,
                amount,
                request.DamageType,
                Health <= 0f);
        }
    }

    protected override void Awake()
    {
        base.Awake();
        CacheDeathDependencies();
    }

    public override void Spawned()
    {
        base.Spawned();
        CacheDeathDependencies();
        _deathDependenciesValid = ValidateDeathDependencies();

        if (_specterController != null)
        {
            if (_movementController != null) _movementController.enabled = false;
            if (_combatController != null) _combatController.enabled = false;
            if (_fsm != null) _fsm.enabled = false;
            var pathfinding = GetComponent<EnemyPathfindingNavigator>();
            if (pathfinding != null) pathfinding.enabled = false;
        }

        if (HasStateAuthority && IsAlive)
        {
            var spawnManager = Runner.GetComponent<NetworkSpawnManager>();
            if (spawnManager != null)
            {
                spawnManager.PopulationTracker.Register(Object.Id.Raw, PopulationOrigin, EnemyThreatCost.GetCost());
            }
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (hasState)
        {
            var spawnManager = runner.GetComponent<NetworkSpawnManager>();
            if (spawnManager != null)
            {
                spawnManager.PopulationTracker.Unregister(Object.Id.Raw);
            }
        }
        base.Despawned(runner, hasState);
    }

    /// <summary>
    /// Stops authoritative enemy simulation and makes the existing network object
    /// inspectable through its shared loot container.
    /// </summary>
    protected override void HandleDeath()
    {
        if (!HasStateAuthority || !_deathDependenciesValid)
        {
            return;
        }

        if (_specterController != null)
        {
            _specterController.TransitionTo(ProjectGrimhold.Gameplay.Enemies.Specter.SpecterStateType.Muerte);

            if (_lootContainer != null && _lootContainer.IsInitialized)
            {
                _lootContainer.SetAvailability(true);
            }

            if (HasStateAuthority)
            {
                var spawnManager = Runner.GetComponent<NetworkSpawnManager>();
                if (spawnManager != null)
                {
                    spawnManager.PopulationTracker.Unregister(Object.Id.Raw);
                }
            }
            return;
        }

        // Authoritatively transition the FSM to Dead.
        // The FSM states (specifically EnemyDeadState) will manage disabling
        // movement and combat control authoritatively.
        _fsm.TransitionTo(EnemyStateType.Dead);

        if (!_lootContainer.IsInitialized)
        {
            Debug.LogError(
                $"{nameof(EnemyCharacter)} cannot expose loot because its container is not initialized.",
                this);
            return;
        }

        // Death is resolved inside authoritative simulation, which is the only safe
        // place to change replicated container availability.
        _lootContainer.SetAvailability(true);

        if (HasStateAuthority)
        {
            var spawnManager = Runner.GetComponent<NetworkSpawnManager>();
            if (spawnManager != null)
            {
                spawnManager.PopulationTracker.Unregister(Object.Id.Raw);
            }
        }
    }

    protected virtual void CacheDeathDependencies()
    {
        if (_specterController == null)
        {
            _specterController = GetComponent<ProjectGrimhold.Gameplay.Enemies.Specter.SpecterAIController>();
        }

        if (_lootContainer == null)
        {
            _lootContainer = GetComponent<NetworkLootContainer>();
        }

        if (_specterController != null)
        {
            return;
        }

        if (_movementController == null)
        {
            _movementController = GetComponent<EnemyMovementAIController>();
        }

        if (_combatController == null)
        {
            _combatController = GetComponent<EnemyCombatAIController>();
        }

        if (_fsm == null)
        {
            _fsm = GetComponent<EnemyFSM>();
        }
    }

    protected virtual bool ValidateDeathDependencies()
    {
        if (_specterController != null)
        {
            if (_lootContainer != null)
            {
                return true;
            }

            Debug.LogError(
                $"{nameof(EnemyCharacter)} requires loot-container dependency on the same enemy.",
                this);
            return false;
        }

        if (_movementController != null && _combatController != null && _lootContainer != null && _fsm != null)
        {
            return true;
        }

        Debug.LogError(
            $"{nameof(EnemyCharacter)} requires movement, combat, FSM and loot-container dependencies on the same enemy.",
            this);
        return false;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        CacheDeathDependencies();
    }
#endif
}
