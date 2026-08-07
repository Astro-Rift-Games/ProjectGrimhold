using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// Networked Finite State Machine for enemy AI entities.
/// Coordinates high-level states (Patrol, Chase, Attack, Dead) and synchronizes state across the network.
/// Easily extensible by registering custom <see cref="IEnemyState"/> implementations.
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyFSM : NetworkBehaviour
{
    [Header("Dependencies")]
    [SerializeField]
    private CharacterBase _character;

    [SerializeField]
    private EnemyMovementAIController _movementController;

    [SerializeField]
    private EnemyCombatAIController _combatController;

    private readonly Dictionary<EnemyStateType, IEnemyState> _states = new();
    private IEnemyState _currentState;

    [Networked]
    public EnemyStateType CurrentStateType { get; private set; }

    public CharacterBase Character => _character;
    public EnemyMovementAIController MovementController => _movementController;
    public EnemyCombatAIController CombatController => _combatController;
    public IEnemyState CurrentState => _currentState;

    private void Awake()
    {
        CacheDependencies();
        RegisterDefaultStates();
    }

    public override void Spawned()
    {
        CacheDependencies();

        if (HasStateAuthority && !HostMigrationRestoreUtility.IsRestoreSpawn(this))
        {
            TransitionTo(EnemyStateType.Idle);
        }
        else
        {
            SyncLocalCurrentState();
        }
    }

    public override void Render()
    {
        if (!HasStateAuthority)
        {
            SyncLocalCurrentState();
        }
    }

    private void SyncLocalCurrentState()
    {
        if (_currentState == null || _currentState.Type != CurrentStateType)
        {
            if (_states.TryGetValue(CurrentStateType, out IEnemyState state))
            {
                _currentState = state;
            }
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority)
        {
            return;
        }

        if (_currentState != null)
        {
            _currentState.FixedUpdateNetwork(this);
        }
    }

    /// <summary>
    /// Registers a custom state implementation into the state machine.
    /// </summary>
    /// <param name="state">The state instance to register.</param>
    public void RegisterState(IEnemyState state)
    {
        if (state == null)
        {
            Debug.LogError($"{nameof(EnemyFSM)}: Cannot register null state.", this);
            return;
        }

        _states[state.Type] = state;
    }

    /// <summary>
    /// Authoritatively transitions to the requested state. Requires State Authority.
    /// </summary>
    /// <param name="nextStateType">The target state type.</param>
    /// <returns>True if transition was performed; otherwise false.</returns>
    public bool TransitionTo(EnemyStateType nextStateType)
    {
        if (!HasStateAuthority)
        {
            return false;
        }

        if (!_states.TryGetValue(nextStateType, out IEnemyState nextState))
        {
            Debug.LogError($"{nameof(EnemyFSM)}: State {nextStateType} is not registered.", this);
            return false;
        }

        if (_currentState != null && _currentState.Type == nextStateType)
        {
            return false;
        }

        _currentState?.Exit(this);
        _currentState = nextState;
        CurrentStateType = nextStateType;
        _currentState.Enter(this);

        return true;
    }

    private void RegisterDefaultStates()
    {
        RegisterState(new EnemyIdleState());
        RegisterState(new EnemyChaseState());
        RegisterState(new EnemyAttackState());
        RegisterState(new EnemyDeadState());
    }

    private void CacheDependencies()
    {
        if (_character == null)
        {
            _character = GetComponent<CharacterBase>();
        }

        if (_movementController == null)
        {
            _movementController = GetComponent<EnemyMovementAIController>();
        }

        if (_combatController == null)
        {
            _combatController = GetComponent<EnemyCombatAIController>();
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        CacheDependencies();
    }
#endif
}
