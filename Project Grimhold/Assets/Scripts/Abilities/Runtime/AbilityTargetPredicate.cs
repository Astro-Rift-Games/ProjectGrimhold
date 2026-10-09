/// <summary>
/// The single valid-enemy rule shared by ability start validation and ability resolution.
/// It owns no state and caches nothing.
/// </summary>
public static class AbilityTargetPredicate
{
    /// <summary>True when <paramref name="candidate"/> is a valid enemy target for an ability cast by <paramref name="casterId"/>.</summary>
    public static bool IsValidEnemy(EntityId casterId, IDamageable candidate) =>
        IsValidCombatant(casterId, candidate) && IsEnemyAffiliation(candidate);

    /// <summary>Common state conditions: not the caster, can receive damage and, for characters, alive.</summary>
    internal static bool IsValidCombatant(EntityId casterId, IDamageable candidate)
    {
        if (candidate == null || candidate.Id == casterId) return false;
        if (!candidate.CanReceiveDamage) return false;
        return !(candidate is ICharacter character) || character.IsAlive;
    }

    // EXTENSION POINT (affiliation): the only place that decides who counts as an enemy.
    // Today only creatures are valid. Players of any team, Downed or not, are deliberately excluded until
    // Game Design closes PvP and Downed targeting and an authoritative runtime affiliation contract exists.
    // Do not invent affiliation here; replace this check when that contract is available.
    private static bool IsEnemyAffiliation(IDamageable candidate) => candidate is EnemyCharacter;
}
