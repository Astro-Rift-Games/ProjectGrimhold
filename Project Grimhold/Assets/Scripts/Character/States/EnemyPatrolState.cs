using UnityEngine;

/// <summary>
/// Active state while the enemy is patrolling its configured waypoints.
/// </summary>
public sealed class EnemyPatrolState : IEnemyState
{
    public EnemyStateType Type => EnemyStateType.Patrol;

    public void Enter(EnemyFSM fsm)
    {
        if (fsm.MovementController != null)
        {
            fsm.MovementController.TrySetControlEnabled(true);
            fsm.MovementController.IsPatrolActive = true;
        }
        
        if (fsm.CombatController != null)
        {
            fsm.CombatController.TrySetAttackEnabled(false);
        }
    }

    public void FixedUpdateNetwork(EnemyFSM fsm)
    {
        if (!fsm.Character.IsAlive)
        {
            fsm.TransitionTo(EnemyStateType.Dead);
            return;
        }

        if (fsm.MovementController.IsAttacking)
        {
            fsm.TransitionTo(EnemyStateType.Attack);
            return;
        }

        if (fsm.MovementController.IsOnPursuit)
        {
            fsm.TransitionTo(EnemyStateType.Chase);
            return;
        }
    }

    public void Exit(EnemyFSM fsm)
    {
        if (fsm.MovementController != null)
        {
            fsm.MovementController.IsPatrolActive = false;
        }
        
        // PatrolWaypointIndex is deliberately NOT reset here, so the enemy 
        // resumes the patrol from where it left off.
    }
}
