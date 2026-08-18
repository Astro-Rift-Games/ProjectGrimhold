#if UNITY_INCLUDE_TESTS
using UnityEngine;

/// <summary>
/// Controllable attack strategy used only by player-combat PlayMode tests.
/// </summary>
public sealed class PlayerCombatTestAttack : MonoBehaviour, IAttack
{
    public AttackType Type { get; private set; }
    public float CooldownSeconds { get; private set; }
    public AttackInputMode InputMode => AttackInputMode.Press;
    public int ExecutionCount { get; private set; }

    public void Initialize(AttackType type, float cooldownSeconds)
    {
        Type = type;
        CooldownSeconds = cooldownSeconds;
    }

    public AttackResult Execute(in AttackRequest request)
    {
        ExecutionCount++;
        return AttackResult.Executed();
    }
}
#endif
