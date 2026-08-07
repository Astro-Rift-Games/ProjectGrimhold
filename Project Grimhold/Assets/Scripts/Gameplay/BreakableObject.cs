using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// Authoritative world obstacle that receives regular combat damage and releases
/// pre-rolled network pickups when destroyed.
///
/// It owns no interaction or container capability. State Authority is the only
/// peer allowed to change health, confirm destruction, or spawn drops.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public sealed class BreakableObject : NetworkBehaviour, IDamageable
{
    [Header("Health")]
    [SerializeField, Min(0.1f)]
    private float _maximumHealth = 25f;

    [Header("Drop Configuration")]
    [SerializeField]
    private LootContainerContentTable _lootTable;

    [SerializeField]
    private LootDefinitionCatalog _lootCatalog;

    [SerializeField]
    private NetworkPrefabRef _pickupPrefab;

    [SerializeField]
    private Vector2[] _dropOffsets = Array.Empty<Vector2>();

    [Header("Presentation")]
    [SerializeField]
    private SpriteRenderer[] _intactRenderers = Array.Empty<SpriteRenderer>();

    private Collider2D[] _cachedColliders;
    private EntityRegistry _registry;
    private EntityId _registeredId;
    private bool _isRegistered;
    private LootEntry[] _initialDrops;
    private bool _hasInitialDrops;

    /// <summary>Authoritative health replicated through Fusion snapshots.</summary>
    [Networked]
    public float Health { get; private set; }

    /// <summary>Authoritative one-way lifecycle state; destroyed objects never respawn.</summary>
    [Networked]
    public NetworkBool IsDestroyed { get; private set; }

    /// <summary>Stable gameplay identity derived from the Fusion network object.</summary>
    public new EntityId Id => new(unchecked((int)Object.Id.Raw));

    /// <summary>Whether authoritative combat may currently damage this obstacle.</summary>
    public bool CanReceiveDamage => !IsDestroyed && Health > 0f;

    /// <summary>Weighted static configuration used during authoritative map spawning.</summary>
    public LootContainerContentTable LootTable => _lootTable;

    /// <summary>Catalog that defines every loot identity representable by this obstacle.</summary>
    public LootDefinitionCatalog LootCatalog => _lootCatalog;

    /// <summary>Generic network pickup spawned for each rolled stack.</summary>
    public NetworkPrefabRef PickupPrefab => _pickupPrefab;

    /// <summary>Maximum number of distinct stacks that can be placed around this obstacle.</summary>
    public int DropCapacity => _dropOffsets?.Length ?? 0;

    /// <summary>Whether State Authority received a validated pre-spawn drop result.</summary>
    public bool HasInitialDrops => _hasInitialDrops;

    private void Awake()
    {
        _cachedColliders = GetComponentsInChildren<Collider2D>(true);
        if (_intactRenderers == null || _intactRenderers.Length == 0)
        {
            _intactRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        }
    }

    public override void Spawned()
    {
        if (HasStateAuthority && !Object.IsResume)
        {
            Health = _maximumHealth;
            IsDestroyed = false;
        }

        _registry = Runner.GetComponent<EntityRegistry>();
        if (_registry == null)
        {
            Debug.LogError(
                $"{nameof(BreakableObject)} requires an {nameof(EntityRegistry)} on the runner.",
                this);
        }
        else
        {
            _registeredId = Id;
            _isRegistered = _registry.TryRegisterEntity(_registeredId, this, _cachedColliders);
            if (!_isRegistered)
            {
                Debug.LogError(
                    $"{nameof(BreakableObject)} could not register entity '{_registeredId}'.",
                    this);
            }
        }

        ApplyDestroyedState();
    }

    public override void FixedUpdateNetwork()
    {
        ApplyDestroyedState();
    }

    public override void Render()
    {
        ApplyDestroyedState();
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        UnregisterDamageable();
        _registry = null;
        _initialDrops = null;
        _hasInitialDrops = false;
    }

    /// <summary>
    /// Materializes the authoritative drop result during Fusion's pre-spawn callback.
    /// The result remains local to State Authority until destruction creates pickups.
    /// </summary>
    internal bool TrySetInitialDropsOverride(
        NetworkRunner runner,
        NetworkObject expectedObject,
        IReadOnlyList<LootEntry> drops)
    {
        if (_hasInitialDrops || runner == null || !runner.IsServer ||
            expectedObject == null || expectedObject.gameObject != gameObject ||
            expectedObject.GetComponent<BreakableObject>() != this || drops == null ||
            drops.Count > DropCapacity)
        {
            return false;
        }

        var materialized = new LootEntry[drops.Count];
        for (int i = 0; i < drops.Count; i++)
        {
            if (!drops[i].IsValid)
            {
                return false;
            }

            materialized[i] = drops[i];
        }

        _initialDrops = materialized;
        _hasInitialDrops = true;
        return true;
    }

    /// <summary>
    /// Applies one damage request under State Authority. The destruction flag is
    /// committed before spawning pickups so repeated requests cannot duplicate loot.
    /// </summary>
    public DamageResult ApplyDamage(in DamageRequest request)
    {
        if (!HasStateAuthority)
        {
            return Rejected(DamageFailureReason.MissingAuthority);
        }

        if (IsDestroyed || Health <= 0f)
        {
            return Rejected(DamageFailureReason.TargetDead);
        }

        if (!_hasInitialDrops || !_isRegistered)
        {
            return Rejected(DamageFailureReason.TargetUnavailable);
        }

        if (request.TargetId != Id)
        {
            return Rejected(DamageFailureReason.InvalidTarget);
        }

        if (request.Amount <= 0f)
        {
            return Rejected(DamageFailureReason.InvalidAmount);
        }

        float previousHealth = Health;
        Health = Mathf.Max(0f, Health - request.Amount);
        float appliedDamage = previousHealth - Health;
        bool isFatal = Health <= 0f;

        if (isFatal)
        {
            IsDestroyed = true;
            ApplyDestroyedState();
            SpawnDrops();
        }

        return new DamageResult(
            Id,
            true,
            appliedDamage,
            Health,
            isFatal,
            DamageFailureReason.None);
    }

    private void SpawnDrops()
    {
        if (!HasStateAuthority || Runner == null || _initialDrops == null)
        {
            return;
        }

        for (int i = 0; i < _initialDrops.Length; i++)
        {
            LootEntry entry = _initialDrops[i];
            Vector2 offset = _dropOffsets[i];
            Vector3 position = transform.TransformPoint(new Vector3(offset.x, offset.y, 0f));
            bool callbackApplied = false;
            NetworkLootPickup callbackPickup = null;

            NetworkObject pickupObject = Runner.Spawn(
                _pickupPrefab,
                position,
                Quaternion.identity,
                inputAuthority: null,
                onBeforeSpawned: (callbackRunner, instance) =>
                {
                    callbackPickup = instance != null
                        ? instance.GetComponent<NetworkLootPickup>()
                        : null;
                    callbackApplied = callbackPickup != null &&
                        callbackPickup.TrySetSpawnContentOverride(callbackRunner, instance, entry);
                });

            bool initialized = pickupObject != null &&
                pickupObject.Id.IsValid &&
                callbackPickup != null &&
                callbackPickup.Object == pickupObject &&
                callbackApplied &&
                callbackPickup.IsInitialized;
            if (initialized)
            {
                continue;
            }

            Debug.LogError(
                $"{nameof(BreakableObject)} failed to initialize drop {i} for '{name}'.",
                this);
            if (pickupObject != null && pickupObject.Id.IsValid)
            {
                Runner.Despawn(pickupObject);
            }
        }
    }

    private void ApplyDestroyedState()
    {
        bool destroyed = IsDestroyed;
        if (destroyed)
        {
            UnregisterDamageable();
        }

        if (_cachedColliders != null)
        {
            for (int i = 0; i < _cachedColliders.Length; i++)
            {
                if (_cachedColliders[i] != null)
                {
                    _cachedColliders[i].enabled = !destroyed;
                }
            }
        }

        if (_intactRenderers != null)
        {
            for (int i = 0; i < _intactRenderers.Length; i++)
            {
                if (_intactRenderers[i] != null)
                {
                    _intactRenderers[i].enabled = !destroyed;
                }
            }
        }
    }

    private void UnregisterDamageable()
    {
        if (!_isRegistered || _registry == null)
        {
            return;
        }

        _registry.TryUnregisterEntity(_registeredId, this);
        _isRegistered = false;
    }

    private DamageResult Rejected(DamageFailureReason reason)
    {
        return new DamageResult(Id, false, 0f, Health, false, reason);
    }
}
