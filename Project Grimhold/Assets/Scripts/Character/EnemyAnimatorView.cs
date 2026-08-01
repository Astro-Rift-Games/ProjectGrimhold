using UnityEngine;

/// <summary>
/// Concrete animator view component for enemy entities, inheriting core animation logic from <see cref="CharacterAnimatorView"/>.
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyAnimatorView : CharacterAnimatorView
{
    private EnemyMovementAIController _enemyMovement;
    private int _isAttackingHash;

    protected override void Awake()
    {
        base.Awake();
        _isAttackingHash = Animator.StringToHash("IsAttacking");
        CacheEnemyDependencies();
    }

    private void CacheEnemyDependencies()
    {
        if (_enemyMovement == null)
        {
            _enemyMovement = GetComponentInParent<EnemyMovementAIController>();
        }
    }

    protected override void LateUpdate()
    {
        base.LateUpdate();

        if (AnimatorInstance == null)
        {
            return;
        }

        if (_enemyMovement == null)
        {
            CacheEnemyDependencies();
        }

        if (_enemyMovement != null)
        {
            AnimatorInstance.SetBool(_isAttackingHash, _enemyMovement.IsAttacking);
        }
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        CacheEnemyDependencies();
    }

    protected override void Reset()
    {
        base.Reset();
        CacheEnemyDependencies();
    }
#endif
}
