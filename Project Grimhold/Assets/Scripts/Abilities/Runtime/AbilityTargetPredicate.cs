using System;

/// <summary>
/// The single valid-enemy rule and the single valid-ally rule shared by ability start validation and ability
/// resolution. It owns no state and caches nothing.
/// </summary>
public static class AbilityTargetPredicate
{
    private static readonly Func<PlayerCharacter, PlayerCharacter, bool> InitialTeammates =
        AbilityAllyAffiliation.AreInitialTeammates;

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

    /// <summary>
    /// True when <paramref name="candidate"/> is a valid ally of <paramref name="caster"/>: the caster itself or an
    /// initial teammate, and alive. Downed allies stay valid here; particular conditions (for example "not Downed")
    /// belong to the concrete ability.
    /// </summary>
    public static bool IsValidAlly(PlayerCharacter caster, PlayerCharacter candidate) =>
        IsValidAlly(caster, candidate, InitialTeammates);

    /// <summary>Same rule with the teammate decision injected, so tests do not need Fusion.</summary>
    internal static bool IsValidAlly(PlayerCharacter caster, PlayerCharacter candidate,
        Func<PlayerCharacter, PlayerCharacter, bool> areInitialTeammates)
    {
        if (caster == null || candidate == null) return false;
        bool isCaster = ReferenceEquals(caster, candidate);
        bool isAlive = candidate.IsAlive;
        return IsAllyEligible(isCaster, isAlive, isAlive && !isCaster && areInitialTeammates(caster, candidate));
    }

    /// <summary>The ally rule on already-resolved facts: alive, and either the caster or an initial teammate.</summary>
    internal static bool IsAllyEligible(bool isCaster, bool isAlive, bool isInitialTeammate) =>
        isAlive && (isCaster || isInitialTeammate);

    // The ally variant (IsValidAlly) now exists above; the two rules are independent.
    // EXTENSION POINT (affiliation): the only place that decides who counts as an enemy.
    // Today only creatures are valid. Players of any team, Downed or not, are deliberately excluded until
    // Game Design closes PvP and Downed targeting and an authoritative runtime affiliation contract exists.
    // Do not invent affiliation here; replace this check when that contract is available.
    private static bool IsEnemyAffiliation(IDamageable candidate) => candidate is IAbilityEnemyTarget;
}
