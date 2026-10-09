/// <summary>
/// Explicit marker for entities that abilities treat as valid enemy targets.
/// It carries no members: liveness and damageability are still decided by
/// <see cref="AbilityTargetPredicate"/>.
/// </summary>
public interface IAbilityEnemyTarget
{
}
