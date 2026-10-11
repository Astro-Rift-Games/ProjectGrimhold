using Fusion;
using UnityEngine;

/// <summary>
/// Avatar-owned PvE orb effect, independent of the instant casting execution.
/// Fusion copies all effect progress on the same avatar; no caster id or separate NetworkObject needs remapping.
/// Local slot geometry is pooled once, retains original indices, and is never registered in EntityRegistry.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-6)]
public sealed class ProtectiveOrbsNetworkEffect : NetworkBehaviour
{
    private const int OverlapCapacity = 64;
    [SerializeField] private PlayerCharacter _playerCharacter;
    [SerializeField] private MonoBehaviour _damageResolverSource;
    [SerializeField] private ProtectiveOrbCollider _orbPrefab;
    [SerializeField] private LayerMask _targetLayerMask;

    [Networked] public uint ActiveMask { get; private set; }
    [Networked] public int InitialCount { get; private set; }
    [Networked] public int StartTick { get; private set; }
    [Networked] public TickTimer LifetimeTimer { get; private set; }
    [Networked] public float OrbitRadius { get; private set; }
    [Networked] public float AngularSpeedDegreesPerSecond { get; private set; }
    [Networked] public float ContactDamage { get; private set; }
    [Networked] public DamageType ContactDamageType { get; private set; }
    [Networked] private uint ActivationSequence { get; set; }

    private readonly ProtectiveOrbCollider[] _slots = new ProtectiveOrbCollider[ProtectiveOrbsRules.MaxOrbCount];
    private readonly Collider2D[] _overlaps = new Collider2D[OverlapCapacity];
    private EntityRegistry _registry;
    private IDamageResolver _damageResolver;
    private NetworkSpawnManager _spawnManager;
    private PlayerExtractionController _extraction;
    private RaidAvatarParticipantLink _participantLink;
    private ContactFilter2D _contactFilter;
    private bool _spawned;
    private bool _poolReady;

    public EntityId OwnerId => CanReadState() && _playerCharacter != null ? _playerCharacter.Id : default;
    public bool IsActive => CanReadState() && ActiveMask != 0u;

    public override void Spawned()
    {
        _spawned = true;
        _registry = Runner.GetComponent<EntityRegistry>();
        _damageResolver = _damageResolverSource as IDamageResolver;
        _spawnManager = Runner.GetComponent<NetworkSpawnManager>();
        _extraction = _playerCharacter != null ? _playerCharacter.GetComponent<PlayerExtractionController>() : null;
        _participantLink = _playerCharacter != null ? _playerCharacter.GetComponent<RaidAvatarParticipantLink>() : null;
        _contactFilter = new ContactFilter2D { useLayerMask = true, useTriggers = true };
        _contactFilter.SetLayerMask(_targetLayerMask);

        // There is deliberately NO fresh-state initialization here. A fresh Fusion allocation is inactive
        // (zero mask). CopyStateFrom supplies restored state before Spawned; neither that mask nor its timers
        // may be overwritten, including an empty/consumed snapshot. Re-entering Spawned is also idempotent.
        if (!TryValidateDependencies(out string error))
        {
            Debug.LogError($"{nameof(ProtectiveOrbsNetworkEffect)} configuration is invalid: {error}", this);
            DisableSlots();
            return;
        }
        EnsurePool();
        UpdateSlotGeometry();
    }

    /// <summary>Side-effect-free composition preflight, callable before resource payment.</summary>
    public bool TryValidateDependencies(out string error)
    {
        if (_playerCharacter == null || _playerCharacter.gameObject != gameObject)
            error = "assign the same-root PlayerCharacter.";
        else if (_damageResolverSource == null || !_damageResolverSource.enabled ||
                 _damageResolverSource.gameObject != gameObject || !(_damageResolverSource is IDamageResolver))
            error = "assign an enabled same-root damage resolver.";
        else if (_targetLayerMask.value == 0)
            error = "assign creature damage-hitbox layers.";
        else if (_orbPrefab == null || _orbPrefab.GetComponent<Collider2D>() == null ||
                 !_orbPrefab.GetComponent<Collider2D>().isTrigger ||
                 _orbPrefab.gameObject.layer != LayerMask.NameToLayer("Character") ||
                 _orbPrefab.GetComponentInChildren<NetworkObject>(true) != null ||
                 _orbPrefab.GetComponentsInChildren<NetworkBehaviour>(true).Length != 0 ||
                 _orbPrefab.GetComponentInChildren<SpriteRenderer>(true) == null ||
                 _orbPrefab.GetComponentInChildren<SpriteRenderer>(true).sprite == null ||
                 !_orbPrefab.gameObject.activeSelf)
            error = "assign an active local Character-layer trigger orb prefab with a sprite and no NetworkObject.";
        else if (!isActiveAndEnabled)
            error = "the effect must be enabled.";
        else
            error = string.Empty;
        return error.Length == 0;
    }

    public bool CanActivate => CanMutateState() && _poolReady && _registry != null &&
        _damageResolver != null && _playerCharacter != null && _playerCharacter.IsAlive &&
        AttackTiming.IsFinite((Vector2)_playerCharacter.transform.position) && !HasLifecycleEnded();

    // The runtime refuses uint.MaxValue rather than wrapping its accepted sequence. Reject old as well as
    // duplicate starts so an earlier accepted snapshot cannot replace a newer cast after restore/reentry.
    public bool WasActivatedBy(uint sequence) => CanReadState() && sequence != 0 && sequence <= ActivationSequence;

    public bool TryActivate(uint sequence, int count, float orbitRadius, float angularSpeedDegreesPerSecond,
        float lifetimeSeconds, float contactDamage, DamageType damageType)
    {
        if (!CanActivate || sequence == 0 || WasActivatedBy(sequence) ||
            !ProtectiveOrbsRules.TryValidateConfiguration(count, orbitRadius,
                angularSpeedDegreesPerSecond, lifetimeSeconds, out _) ||
            !AttackTiming.IsFinite(contactDamage) || contactDamage < 0f ||
            !System.Enum.IsDefined(typeof(DamageType), damageType) ||
            !ProtectiveOrbsRules.TryCreateFullMask(count, out uint mask)) return false;

        ActivationSequence = sequence;
        InitialCount = count;
        OrbitRadius = orbitRadius;
        AngularSpeedDegreesPerSecond = angularSpeedDegreesPerSecond;
        ContactDamage = contactDamage;
        ContactDamageType = damageType;
        StartTick = Runner.Tick.Raw;
        LifetimeTimer = lifetimeSeconds == 0f ? TickTimer.None : TickTimer.CreateFromSeconds(Runner, lifetimeSeconds);
        ActiveMask = mask;
        UpdateSlotGeometry();
        return true;
    }

    public bool IsSlotActive(int slotIndex) => IsActive && slotIndex >= 0 && slotIndex < InitialCount &&
        slotIndex < ProtectiveOrbsRules.MaxOrbCount && (ActiveMask & (1u << slotIndex)) != 0u;

    private bool CanAcceptImpact(int slotIndex) =>
        IsSlotActive(slotIndex) && _poolReady && Runner.IsRunning && _playerCharacter != null &&
        _playerCharacter.IsAlive && OwnerId.Value != 0 && !HasLifecycleEnded() && !LifetimeTimer.Expired(Runner);

    /// <summary>Melee/contact require a currently valid live enemy; only TryConsume commits.</summary>
    public bool CanConsumeEnemyImpact(int slotIndex, EntityId attackerId) =>
        CanAcceptImpact(slotIndex) && _registry != null &&
        _registry.TryGetDamageable(attackerId, out IDamageable attacker) &&
        AbilityTargetPredicate.IsValidEnemy(OwnerId, attacker);

    /// <summary>Independent flight uses its committed PvE affiliation, never a live shooter lookup.</summary>
    public bool CanConsumeEnemyProjectileImpact(int slotIndex, EntityId attackerId, bool sourceWasPvEEnemy) =>
        sourceWasPvEEnemy && attackerId.Value != 0 && attackerId != OwnerId && CanAcceptImpact(slotIndex);

    /// <summary>Blocks one live enemy impact. No damage, Resolve, feedback or Assist contribution.</summary>
    public bool TryConsumeEnemyImpact(int slotIndex, EntityId attackerId) =>
        CanConsumeEnemyImpact(slotIndex, attackerId) && TryCommitSlotConsumption(slotIndex);

    /// <summary>Blocks one committed enemy projectile even after its shooter dies or despawns.</summary>
    public bool TryConsumeEnemyProjectileImpact(int slotIndex, EntityId attackerId, bool sourceWasPvEEnemy) =>
        CanConsumeEnemyProjectileImpact(slotIndex, attackerId, sourceWasPvEEnemy) && TryCommitSlotConsumption(slotIndex);

    private bool TryCommitSlotConsumption(int slotIndex)
    {
        if (!CanMutateState() ||
            !ProtectiveOrbsRules.TryConsumeSlot(ActiveMask, InitialCount, slotIndex, out uint remaining)) return false;
        ActiveMask = remaining; // Commit before any external operation or possible reentry.
        _slots[slotIndex].SetSlotEnabled(false);
        return true;
    }

    public override void FixedUpdateNetwork()
    {
        if (!CanReadState() || !Runner.IsRunning || !Runner.IsSimulationUpdating || !Runner.IsForward || !_poolReady) return;
        if (HasStateAuthority && IsActive &&
            (_playerCharacter == null || !_playerCharacter.IsAlive || HasLifecycleEnded() || LifetimeTimer.Expired(Runner)))
            EndEffect();

        // Authoritative geometry advances only at the simulation boundary.
        UpdateSlotGeometry();
        if (!HasStateAuthority || !IsActive || _registry == null || _damageResolver == null) return;

        // Local transforms moved above; explicit sync is required before these nonalloc overlap queries.
        Physics2D.SyncTransforms();
        for (int slotIndex = 0; slotIndex < InitialCount; slotIndex++)
        {
            if (!IsSlotActive(slotIndex)) continue;
            Collider2D orbCollider = _slots[slotIndex].PhysicsCollider;
            int count = orbCollider.Overlap(_contactFilter, _overlaps);
            EntityId selectedId = default;
            Vector2 selectedPoint = default;
            float selectedDistance = float.PositiveInfinity;
            Vector2 origin = _slots[slotIndex].transform.position;
            for (int i = 0; i < count; i++)
            {
                Collider2D collider = _overlaps[i];
                if (collider == null || !_registry.TryGetEntityId(collider, out EntityId id) ||
                    !_registry.IsDamageCollider(id, collider) ||
                    !_registry.TryGetDamageable(id, out IDamageable target) ||
                    !AbilityTargetPredicate.IsValidEnemy(OwnerId, target)) continue;
                Vector2 point = collider.ClosestPoint(origin);
                float distance = (point - origin).sqrMagnitude;
                if (selectedId.Value == 0 || distance < selectedDistance ||
                    (distance == selectedDistance && id.Value < selectedId.Value))
                {
                    selectedId = id;
                    selectedPoint = point;
                    selectedDistance = distance;
                }
            }
            System.Array.Clear(_overlaps, 0, count);
            if (selectedId.Value == 0 || !TryConsumeEnemyImpact(slotIndex, selectedId)) continue;
            if (ContactDamage <= 0f) continue;
            var request = new DamageRequest(OwnerId, selectedId, ContactDamage, ContactDamageType,
                (selectedPoint - (Vector2)_playerCharacter.transform.position).normalized, selectedPoint, Runner.Tick.Raw);
            _damageResolver.Resolve(in request);
            // A reentrant resolver may end the effect or consume other slots; the next iteration rechecks the mask.
        }
    }

    public override void Render()
    {
        // Non-simulated proxies may not receive FixedUpdateNetwork. Reconstruct their local geometry/visuals
        // from copied mask and tick phase only; this never touches authoritative physics or network state.
        if (CanReadState() && !HasStateAuthority) UpdateSlotGeometry();
    }

    private bool CanReadState() => _spawned && isActiveAndEnabled && Object != null && Object.IsValid && Runner != null;
    private bool CanMutateState() => CanReadState() && HasStateAuthority && Runner.IsRunning &&
        Runner.IsSimulationUpdating && Runner.IsForward;

    private bool HasLifecycleEnded()
    {
        if (_extraction != null && _extraction.State == ExtractionState.Extracted) return true;
        var match = _spawnManager != null ? _spawnManager.MatchController : null;
        if (match != null && (match.Phase == NetworkMatchController.MatchPhase.Closing ||
                              match.Phase == NetworkMatchController.MatchPhase.Finished)) return true;
        // An unresolved migration fixup is temporary, not a terminal participation.
        return _participantLink != null && _participantLink.TryResolveParticipant(out var participant) &&
            (participant.State != RaidParticipantState.Raiding || participant.CurrentAvatarId != Object.Id);
    }

    private void EnsurePool()
    {
        if (_poolReady) return;
        for (int i = 0; i < _slots.Length; i++)
        {
            _slots[i] = Instantiate(_orbPrefab, transform);
            _slots[i].Initialize(this, i);
        }
        _poolReady = true;
    }

    private void UpdateSlotGeometry()
    {
        if (!_poolReady) return;
        float elapsed = Mathf.Max(0, Runner.Tick.Raw - StartTick) * Runner.DeltaTime;
        for (int i = 0; i < _slots.Length; i++)
        {
            Vector2 offset = default;
            bool active = IsSlotActive(i) && ProtectiveOrbsRules.TryComputeOrbitOffset(i, InitialCount,
                OrbitRadius, AngularSpeedDegreesPerSecond, elapsed, out offset);
            if (active) _slots[i].transform.position = _playerCharacter.transform.position + (Vector3)offset;
            _slots[i].SetSlotEnabled(active);
        }
    }

    private void EndEffect()
    {
        ActiveMask = 0u;
        DisableSlots();
    }

    private void DisableSlots()
    {
        if (!_poolReady) return;
        for (int i = 0; i < _slots.Length; i++) _slots[i].SetSlotEnabled(false);
    }

    private void OnDisable() => DisableSlots();

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        _spawned = false;
        DisableSlots();
        _registry = null;
        _damageResolver = null;
        _spawnManager = null;
    }
}
