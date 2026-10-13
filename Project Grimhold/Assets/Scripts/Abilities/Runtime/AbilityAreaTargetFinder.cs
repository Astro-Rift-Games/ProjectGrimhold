using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// Detects valid enemies inside a caster-centered circle. It is the one code path used by an ability's
/// start validation (<c>TryPlanStart</c>) and its resolution; the radius is supplied by the concrete behavior.
/// Built on the existing attack target detection: no line of sight is required and results are
/// deduplicated by entity and ordered deterministically (distance, then EntityId).
/// </summary>
[DisallowMultipleComponent]
public sealed class AbilityAreaTargetFinder : MonoBehaviour
{
    // The existing query reads at most its collider buffer size (64) and silently drops the rest.
    private const int UnlimitedTargets = int.MaxValue;
    private static readonly Func<EntityId, IDamageable, bool> ValidEnemy = AbilityTargetPredicate.IsValidEnemy;
    private static readonly Func<PlayerCharacter, PlayerCharacter, bool> ValidAlly = AbilityTargetPredicate.IsValidAlly;

    [SerializeField] private Physics2DAttackTargetQuery _targetQuery;
    // Same point the equipped weapon uses (PlayerCombatNetworkController attack origin). Falls back to the root.
    [SerializeField] private Transform _origin;
    [SerializeField] private LayerMask _targetLayerMask;

    private EntityRegistry _registry;
    private NetworkRunner _registryRunner;

    public bool IsConfigured => _targetQuery != null && _targetLayerMask.value != 0;

    /// <summary>
    /// Replaces <paramref name="results"/> with the valid enemies around the caster's current origin.
    /// Returns false (and leaves it empty) when the finder is unconfigured or the radius is invalid.
    /// The caller owns the list; it is a copy that later queries cannot overwrite.
    /// </summary>
    public bool TryFindEnemies(NetworkRunner runner, EntityId casterId, float radius, List<AttackTarget> results)
    {
        results?.Clear();
        if (runner == null || !IsConfigured) return false;
        return TryCollect(_targetQuery, ResolveRegistry(runner), casterId, ResolveOrigin(), radius,
            _targetLayerMask.value, results);
    }

    /// <summary>
    /// Replaces <paramref name="results"/> with the valid allies around the caster's current origin: the caster
    /// first (it is the center), then initial teammates by distance and EntityId. Same query path as
    /// <see cref="TryFindEnemies"/>. Returns false (and leaves it empty) when unconfigured or the radius is invalid.
    /// </summary>
    public bool TryFindAllies(NetworkRunner runner, PlayerCharacter caster, float radius, List<PlayerCharacter> results)
    {
        results?.Clear();
        if (runner == null || caster == null || !IsConfigured) return false;
        return TryCollectAllies(_targetQuery, ResolveRegistry(runner), caster.Id, caster, ResolveOrigin(), radius,
            _targetLayerMask.value, results, ValidAlly);
    }

    private EntityRegistry ResolveRegistry(NetworkRunner runner)
    {
        if (_registry == null || _registryRunner != runner)
        {
            _registry = runner.GetComponent<EntityRegistry>();
            _registryRunner = runner;
        }
        return _registry;
    }

    private Vector2 ResolveOrigin() => _origin != null ? (Vector2)_origin.position : (Vector2)transform.position;

    /// <summary>Query plus predicate. The optional predicate exists for tests; production always uses the shared one.</summary>
    internal static bool TryCollect(IAttackTargetQuery query, EntityRegistry registry, EntityId casterId,
        Vector2 origin, float radius, int layerMask, List<AttackTarget> results,
        Func<EntityId, IDamageable, bool> isValid = null)
    {
        results?.Clear();
        if (query == null || registry == null || results == null || layerMask == 0 ||
            !(radius > 0f) || float.IsInfinity(radius) || float.IsNaN(origin.x) || float.IsNaN(origin.y)) return false;
        isValid ??= ValidEnemy;
        var found = query.FindTargets(new AttackTargetQuery(
            casterId, origin, Vector2.zero, 0f, radius, UnlimitedTargets, layerMask));
        if (found == null) return true;
        // Copy immediately: the query returns its own reused list.
        for (int i = 0; i < found.Count; i++)
        {
            AttackTarget target = found[i];
            if (registry.TryGetDamageable(target.TargetId, out IDamageable damageable) &&
                isValid(casterId, damageable)) results.Add(target);
        }
        return true;
    }

    /// <summary>
    /// Ally variant of <see cref="TryCollect"/>: same query, candidates resolved to <typeparamref name="T"/>.
    /// The caster is added first when it is a valid ally of itself, since the query excludes its own id.
    /// </summary>
    internal static bool TryCollectAllies<T>(IAttackTargetQuery query, EntityRegistry registry, EntityId casterId,
        T caster, Vector2 origin, float radius, int layerMask, List<T> results, Func<T, T, bool> isValidAlly)
        where T : class, IDamageable
    {
        results?.Clear();
        if (query == null || registry == null || caster == null || results == null || isValidAlly == null ||
            layerMask == 0 || !(radius > 0f) || float.IsInfinity(radius) ||
            float.IsNaN(origin.x) || float.IsInfinity(origin.x) ||
            float.IsNaN(origin.y) || float.IsInfinity(origin.y)) return false;
        var found = query.FindTargets(new AttackTargetQuery(
            casterId, origin, Vector2.zero, 0f, radius, UnlimitedTargets, layerMask));
        if (isValidAlly(caster, caster)) results.Add(caster);
        if (found == null) return true;
        for (int i = 0; i < found.Count; i++)
        {
            if (!registry.TryGetDamageable(found[i].TargetId, out IDamageable damageable) ||
                !(damageable is T candidate) || ContainsReference(results, candidate) ||
                !isValidAlly(caster, candidate)) continue;
            results.Add(candidate);
        }
        return true;
    }

    private static bool ContainsReference<T>(List<T> list, T item) where T : class
    {
        for (int i = 0; i < list.Count; i++)
            if (ReferenceEquals(list[i], item)) return true;
        return false;
    }
}
