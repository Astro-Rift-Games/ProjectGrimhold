#if UNITY_INCLUDE_TESTS
using Fusion;
using UnityEngine;

/// <summary>
/// Applies one enemy hit from Fusion simulation so PlayMode tests exercise
/// the same authoritative timing contract as gameplay damage.
/// </summary>
public sealed class EnemyFatalDamageSimulationDriver : SimulationBehaviour
{
    public EnemyCharacter Target { get; set; }
    public DamageResolver Resolver { get; set; }
    public EntityId AttackerId { get; set; } = new EntityId(int.MaxValue);
    public float DamageAmount { get; set; } = 1000f;
    public bool IsRequested { get; set; }
    public DamageResult LastResult { get; private set; }

    public override void FixedUpdateNetwork()
    {
        if (!IsRequested || Target == null)
        {
            return;
        }

        IsRequested = false;
        var request = new DamageRequest(
            AttackerId,
            Target.Id,
            DamageAmount,
            DamageType.TrueDamage,
            Vector2.down,
            Target.transform.position,
            Runner.Tick);
        LastResult = Resolver != null ? Resolver.Resolve(request) : Target.ApplyDamage(request);
    }
}
#endif
