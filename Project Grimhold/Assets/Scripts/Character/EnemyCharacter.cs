using UnityEngine;
using Fusion;
using Spawning;

/// <summary>
/// Owns the authoritative enemy death transition.
/// A defeated enemy remains the same network entity and exposes its co-located
/// loot container without spawning a replacement corpse object.
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyCharacter : CharacterBase
{
    [Networked]
    public EnemyPopulationOrigin PopulationOrigin { get; set; }
    [Header("Death Dependencies")]
    [SerializeField]
    private EnemyMovementAIController _movementController;

    [SerializeField]
    private EnemyCombatAIController _combatController;

    [SerializeField]
    private NetworkLootContainer _lootContainer;

    [SerializeField]
    private EnemyFSM _fsm;

    private bool _deathDependenciesValid;

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

        if (HasStateAuthority && IsAlive)
        {
            var spawnManager = Runner.GetComponent<NetworkSpawnManager>();
            if (spawnManager != null)
            {
                spawnManager.PopulationTracker.Register(this);
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
                spawnManager.PopulationTracker.Unregister(this);
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
                spawnManager.PopulationTracker.Unregister(this);
            }
        }
    }

    private void CacheDeathDependencies()
    {
        if (_movementController == null)
        {
            _movementController = GetComponent<EnemyMovementAIController>();
        }

        if (_combatController == null)
        {
            _combatController = GetComponent<EnemyCombatAIController>();
        }

        if (_lootContainer == null)
        {
            _lootContainer = GetComponent<NetworkLootContainer>();
        }

        if (_fsm == null)
        {
            _fsm = GetComponent<EnemyFSM>();
        }
    }

    private bool ValidateDeathDependencies()
    {
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
