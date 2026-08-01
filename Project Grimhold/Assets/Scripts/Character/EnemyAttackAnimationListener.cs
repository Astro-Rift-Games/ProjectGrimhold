using UnityEngine;

/// <summary>
/// Presentation-layer bridge between the Animator Controller and the enemy combat simulation.
///
/// This component must be placed on the same GameObject as the Animator, or reachable from it,
/// so that Animation Events in the attack AnimationClip can call <see cref="OnAttackHit"/>.
///
/// Responsibility:
/// - Receives the hit-frame Animation Event from the attack AnimationClip.
/// - Delegates to <see cref="EnemyCombatAIController.ExecutePendingDamage"/> which enforces
///   State Authority before applying any damage.
///
/// This component does not own combat state and does not make authoritative decisions.
/// It is purely a bridge from the Animator Event system to the simulation layer.
///
/// Network authority:
/// - <see cref="OnAttackHit"/> runs on all peers (host and clients) because Animation Events
///   fire wherever the Animator runs.
/// - Damage is applied only on the State Authority peer; the authority guard is inside
///   <see cref="EnemyCombatAIController.ExecutePendingDamage"/>.
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyAttackAnimationListener : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField]
    private EnemyCombatAIController _combatController;

    private void Awake()
    {
        CacheDependencies();
    }

    /// <summary>
    /// Called by an Animation Event on the attack AnimationClip at the hit frame.
    /// Delegates damage resolution to the combat controller, which enforces State Authority.
    /// </summary>
    public void OnAttackHit()
    {
        if (_combatController == null)
        {
            return;
        }

        _combatController.ExecutePendingDamage();
    }

    private void CacheDependencies()
    {
        if (_combatController == null)
        {
            _combatController = GetComponentInParent<EnemyCombatAIController>();
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        CacheDependencies();
    }
#endif
}
