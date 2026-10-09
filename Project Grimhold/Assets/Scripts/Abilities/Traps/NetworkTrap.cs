using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// Caster-owned, runtime-spawned trap entity. Under State Authority it waits for the first valid enemy of its caster
/// inside its trigger radius, applies its immobilize effect to that enemy through <see cref="ImmobilizeEffect"/>,
/// raises <see cref="Triggered"/> once and despawns; it also despawns when its lifetime ends. The effect parameters
/// are [Networked] state, so a Host Migration restore keeps a fully working trap (a C# subscription would be lost).
/// It owns no ability or placement rules: those belong to the behaviour that spawns it.
/// </summary>
[DisallowMultipleComponent]
public sealed class NetworkTrap : NetworkBehaviour
{
    private const int OverlapBufferSize = 16;

    // Eviction scratch shared by the main thread only; grown on demand and never exposed.
    private static int[] s_evictionRemainingTicks = new int[8];

    [Networked] private int CasterEntityIdValue { get; set; }
    [Networked] private TickTimer LifetimeTimer { get; set; }
    [Networked] private int TargetLayerMaskValue { get; set; }
    [Networked] private float TriggerRadius { get; set; }
    [Networked] private NetworkBool HasTriggered { get; set; }
    [Networked] private int TriggeredTargetIdValue { get; set; }
    [Networked] private float ImmobilizeSeconds { get; set; }
    [Networked] private float DamagePerTick { get; set; }
    [Networked] private float TickIntervalSeconds { get; set; }
    [Networked] private int DamageTypeValue { get; set; }
    [Networked] private NetworkBool Purifiable { get; set; }

    private readonly Collider2D[] _overlapBuffer = new Collider2D[OverlapBufferSize];
    private ContactFilter2D _contactFilter;
    private EntityRegistry _registry;

    /// <summary>Raised once on State Authority with the triggering enemy, before the trap despawns.</summary>
    public event Action<EntityId> Triggered;

    public EntityId CasterId => new EntityId(CasterEntityIdValue);
    public bool IsTriggered => Object != null && Object.IsValid && HasTriggered;
    public EntityId TriggeredTargetId => new EntityId(TriggeredTargetIdValue);

    /// <summary>Remaining lifetime in simulation ticks; zero once the lifetime is over or not running.</summary>
    public int RemainingLifetimeTicks(NetworkRunner runner) => LifetimeTimer.RemainingTicks(runner) ?? 0;

    public override void Spawned()
    {
        // Initial state is written by InitializeNetworkState before spawning; a Host Migration restore spawn must
        // keep the restored values (including the running lifetime timer), so nothing is written here.
        _registry = Runner.GetComponent<EntityRegistry>();
        if (_registry == null)
            Debug.LogError($"{nameof(NetworkTrap)}: EntityRegistry was not found on the NetworkRunner GameObject.", this);

        _contactFilter = new ContactFilter2D { useLayerMask = true, useTriggers = true };
        _contactFilter.SetLayerMask(new LayerMask { value = TargetLayerMaskValue });
    }

    /// <summary>
    /// Initializes the networked state before spawning completes. Only valid during the spawner's
    /// <c>onBeforeSpawned</c> callback on the State Authority.
    /// </summary>
    public void InitializeNetworkState(EntityId casterId, float lifetimeSeconds, float triggerRadius, int targetLayerMask,
        float immobilizeSeconds, float damagePerTick, float tickIntervalSeconds, DamageType damageType, bool purifiable)
    {
        CasterEntityIdValue = casterId.Value;
        LifetimeTimer = TickTimer.CreateFromSeconds(Runner, lifetimeSeconds);
        TriggerRadius = triggerRadius;
        TargetLayerMaskValue = targetLayerMask;
        HasTriggered = false;
        TriggeredTargetIdValue = 0;
        ImmobilizeSeconds = immobilizeSeconds;
        DamagePerTick = damagePerTick;
        TickIntervalSeconds = tickIntervalSeconds;
        DamageTypeValue = (int)damageType;
        Purifiable = purifiable;
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || !Runner.IsForward || HasTriggered) return;

        if (LifetimeTimer.ExpiredOrNotRunning(Runner))
        {
            Runner.Despawn(Object);
            return;
        }

        if (!TryFindTriggeringEnemy(out EntityId enemyId)) return;

        HasTriggered = true;
        TriggeredTargetIdValue = enemyId.Value;
        ApplyEffect(enemyId);
        Triggered?.Invoke(enemyId);
        Runner.Despawn(Object);
    }

    /// <summary>
    /// Applies the effect to the triggering enemy, attributed to the caster. A failure is logged and never keeps
    /// the trap alive: the trap is consumed by its trigger either way.
    /// </summary>
    private void ApplyEffect(EntityId enemyId)
    {
        ImmobilizeEffect effect = null;
        if (_registry != null && _registry.TryGetTransform(enemyId, out Transform enemyTransform) && enemyTransform != null)
            effect = enemyTransform.GetComponent<ImmobilizeEffect>() ?? enemyTransform.GetComponentInParent<ImmobilizeEffect>();

        if (effect == null || !effect.TryApply(CasterId, ImmobilizeSeconds, DamagePerTick, TickIntervalSeconds,
                (DamageType)DamageTypeValue, Purifiable))
        {
            Debug.LogError($"{nameof(NetworkTrap)} could not apply its effect to the triggering enemy.", this);
        }
    }

    private bool TryFindTriggeringEnemy(out EntityId enemyId)
    {
        enemyId = default;
        if (_registry == null) return false;

        Physics2D.SyncTransforms();
        int count = Physics2D.OverlapCircle(transform.position, TriggerRadius, _contactFilter, _overlapBuffer);
        EntityId caster = CasterId;
        bool found = false;
        for (int i = 0; i < count && !found; i++)
        {
            Collider2D collider = _overlapBuffer[i];
            if (collider == null || !_registry.TryGetEntityId(collider, out EntityId candidateId)) continue;
            if (candidateId.Value == 0 || !_registry.IsDamageCollider(candidateId, collider)) continue;
            if (!_registry.TryGetDamageable(candidateId, out IDamageable damageable)) continue;
            if (!AbilityTargetPredicate.IsValidEnemy(caster, damageable)) continue;

            enemyId = candidateId;
            found = true;
        }

        Array.Clear(_overlapBuffer, 0, count);
        return found;
    }

    /// <summary>
    /// Makes room for one new trap of <paramref name="casterId"/>: when the caster already owns
    /// <paramref name="maxPerCaster"/> untriggered traps, despawns the one with the least remaining lifetime.
    /// Returns true when a trap was despawned. State Authority only. <paramref name="scratch"/> is overwritten.
    /// </summary>
    public static bool TryEvictForNewTrap(NetworkRunner runner, EntityId casterId, int maxPerCaster, List<NetworkTrap> scratch)
    {
        if (runner == null || scratch == null) return false;

        scratch.Clear();
        runner.GetAllBehaviours(scratch);

        int owned = 0;
        for (int i = 0; i < scratch.Count; i++)
        {
            NetworkTrap trap = scratch[i];
            if (trap == null || trap.Object == null || !trap.Object.IsValid) continue;
            if (trap.CasterId != casterId || trap.HasTriggered) continue;
            scratch[owned++] = trap;
        }
        scratch.RemoveRange(owned, scratch.Count - owned);

        if (s_evictionRemainingTicks.Length < owned)
            s_evictionRemainingTicks = new int[Math.Max(owned, s_evictionRemainingTicks.Length * 2)];
        for (int i = 0; i < owned; i++)
            s_evictionRemainingTicks[i] = scratch[i].RemainingLifetimeTicks(runner);

        int index = TrapRules.SelectEvictionIndex(s_evictionRemainingTicks, owned, maxPerCaster);
        if (index == TrapRules.NoEviction) return false;

        NetworkObject evicted = scratch[index].Object;
        if (!evicted.HasStateAuthority) return false;

        runner.Despawn(evicted);
        return true;
    }

    internal int GetRestoredCasterEntityIdValue() => CasterEntityIdValue;
    internal void SetRestoredCasterEntityId(EntityId newCasterId) => CasterEntityIdValue = newCasterId.Value;

    /// <summary>
    /// Ends the lifetime of a restored trap whose caster could not be remapped, so it despawns on its next
    /// simulation tick without ever triggering, instead of failing the whole Host Migration restore.
    /// </summary>
    internal void ExpireRestoredTrap()
    {
        if (!HasStateAuthority) return;

        LifetimeTimer = TickTimer.None;
    }
}
