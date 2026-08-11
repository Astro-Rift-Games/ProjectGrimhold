using UnityEngine;

/// <summary>
/// Enemy state active during attack execution against a target.
/// Halts movement and enables combat execution.
/// </summary>
public sealed class EnemyAttackState : IEnemyState
{
    public EnemyStateType Type => EnemyStateType.Attack;

    public void Enter(EnemyFSM fsm)
    {
        if (fsm.MovementController != null)
        {
            fsm.MovementController.TrySetControlEnabled(false);
        }

        if (fsm.CombatController != null)
        {
            fsm.CombatController.TrySetAttackEnabled(true);
        }
    }

    public void FixedUpdateNetwork(EnemyFSM fsm)
    {
        if (!fsm.Character.IsAlive)
        {
            fsm.TransitionTo(EnemyStateType.Dead);
            return;
        }

        if (!fsm.MovementController.IsAttacking)
        {
            if (fsm.MovementController.IsOnPursuit)
            {
                fsm.TransitionTo(EnemyStateType.Chase);
            }
            else if (fsm.MovementController.HasPatrolRoute)
            {
                fsm.TransitionTo(EnemyStateType.Patrol);
            }
            else
            {
                fsm.TransitionTo(EnemyStateType.Idle);
            }
            return;
        }
    }

    public void Exit(EnemyFSM fsm)
    {
    }
}
