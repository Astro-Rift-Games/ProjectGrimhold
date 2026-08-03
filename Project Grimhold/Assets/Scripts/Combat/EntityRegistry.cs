using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Associative entity registry for a NetworkRunner.
/// Maps efficiently from EntityId to gameplay capabilities, and from Collider2D to EntityId
/// without incurring GetComponent calls in simulation loops.
/// </summary>
[DisallowMultipleComponent]
public sealed class EntityRegistry : MonoBehaviour
{
    private readonly Dictionary<EntityId, IDamageable> _entities = new();
    private readonly Dictionary<EntityId, ICharacter> _characters = new();
    private readonly Dictionary<EntityId, IInteractable> _interactables = new();
    private readonly Dictionary<EntityId, ILootReceiver> _lootReceivers = new();
    private readonly Dictionary<EntityId, LootSourceRegistration> _lootSources = new();
    private readonly Dictionary<EntityId, IExtractionZone> _extractionZones = new();
    private readonly Dictionary<EntityId, IExtractionParticipant> _extractionParticipants = new();
    private readonly Dictionary<EntityId, IExtractionProgressReceiver> _extractionProgressReceivers = new();
    private readonly Dictionary<EntityId, IExtractionProgressReader> _extractionProgressReaders = new();
    private readonly Dictionary<EntityId, IExtractionProgressDefeatSource> _extractionProgressDefeatSources = new();
    private readonly Dictionary<EntityId, IExtractionSanctuary> _extractionSanctuaries = new();
    private readonly Dictionary<Collider2D, EntityId> _colliders = new();
    private readonly Dictionary<EntityId, DamageColliderRegistration> _damageColliderRegistrations = new();
    private readonly Dictionary<Collider2D, EntityId> _damageColliders = new();

    private readonly struct DamageColliderRegistration
    {
        public IDamageable Damageable { get; }
        public Collider2D[] Colliders { get; }

        public DamageColliderRegistration(IDamageable damageable, Collider2D[] colliders)
        {
            Damageable = damageable;
            Colliders = colliders;
        }
    }

    private readonly struct LootSourceRegistration
    {
        public ILootExtractor Extractor { get; }
        public ILootQuantityReader QuantityReader { get; }
        public Collider2D[] Colliders { get; }

        public LootSourceRegistration(
            ILootExtractor extractor,
            ILootQuantityReader quantityReader,
            Collider2D[] colliders)
        {
            Extractor = extractor;
            QuantityReader = quantityReader;
            Colliders = colliders;
        }
    }

    /// <summary>
    /// Attempts to register an entity and its associated colliders.
    /// Backward-compatible wrapper for damageable-only registrations.
    /// </summary>
    public bool TryRegister(EntityId id, IDamageable damageable, IReadOnlyList<Collider2D> colliders)
    {
        return TryRegisterEntity(id, damageable, colliders);
    }

    /// <summary>
    /// Registers a damageable entity while explicitly identifying which of its colliders
    /// participate in damage detection. Other colliders remain mapped to the same entity
    /// for movement, interaction, and identity resolution.
    /// </summary>
    public bool TryRegisterDamageable(
        EntityId id,
        IDamageable damageable,
        IReadOnlyList<Collider2D> colliders,
        IReadOnlyList<Collider2D> damageColliders)
    {
        if (damageable == null || damageColliders == null || damageColliders.Count == 0)
        {
            return false;
        }

        if (_damageColliderRegistrations.TryGetValue(id, out DamageColliderRegistration existing))
        {
            if (!ReferenceEquals(existing.Damageable, damageable) ||
                !ContainsSameColliders(existing.Colliders, damageColliders))
            {
                return false;
            }

            return TryRegisterEntity(id, damageable, colliders);
        }

        var copiedDamageColliders = new Collider2D[damageColliders.Count];
        for (int i = 0; i < damageColliders.Count; i++)
        {
            Collider2D damageCollider = damageColliders[i];
            if (damageCollider == null ||
                !ContainsCollider(colliders, damageCollider) ||
                _damageColliders.ContainsKey(damageCollider))
            {
                return false;
            }

            for (int existingIndex = 0; existingIndex < i; existingIndex++)
            {
                if (copiedDamageColliders[existingIndex] == damageCollider)
                {
                    return false;
                }
            }

            copiedDamageColliders[i] = damageCollider;
        }

        if (!TryRegisterEntity(id, damageable, colliders))
        {
            return false;
        }

        _damageColliderRegistrations.Add(
            id,
            new DamageColliderRegistration(damageable, copiedDamageColliders));

        for (int i = 0; i < copiedDamageColliders.Length; i++)
        {
            _damageColliders.Add(copiedDamageColliders[i], id);
        }

        return true;
    }

    /// <summary>
    /// Removes an entity and its associated colliders from the registry.
    /// Backward-compatible wrapper for damageable-only unregistrations.
    /// </summary>
    public void Unregister(EntityId id, IReadOnlyList<Collider2D> colliders)
    {
        if (_entities.TryGetValue(id, out var damageable))
        {
            TryUnregisterEntity(id, damageable);
        }
    }

    /// <summary>
    /// Registers any IEntity (which might implement IDamageable, IInteractable, or both) and its colliders.
    /// </summary>
    public bool TryRegisterEntity(EntityId id, IEntity entity, IReadOnlyList<Collider2D> colliders)
    {
        if (entity == null)
        {
            return false;
        }

        if (id.Value == 0)
        {
            return false;
        }

        if (entity.Id != id)
        {
            return false;
        }

        if (entity is IDamageable damageableCandidate &&
            _entities.TryGetValue(id, out IDamageable existingDamageable) &&
            !ReferenceEquals(existingDamageable, damageableCandidate))
        {
            return false;
        }

        if (entity is IInteractable interactableCandidate &&
            _interactables.TryGetValue(id, out IInteractable existingInteractable) &&
            !ReferenceEquals(existingInteractable, interactableCandidate))
        {
            return false;
        }

        if (entity is ICharacter characterCandidate &&
            _characters.TryGetValue(id, out ICharacter existingCharacter) &&
            !ReferenceEquals(existingCharacter, characterCandidate))
        {
            return false;
        }

        if (entity is ILootReceiver receiverCandidate &&
            _lootReceivers.TryGetValue(id, out ILootReceiver existingReceiver) &&
            !ReferenceEquals(existingReceiver, receiverCandidate))
        {
            return false;
        }

        // Validate colliders are not registered to someone else
        if (colliders != null)
        {
            for (int i = 0; i < colliders.Count; i++)
            {
                Collider2D col = colliders[i];
                if (col != null && _colliders.TryGetValue(col, out var existingId) && existingId != id)
                {
                    return false;
                }
            }
        }

        // Register contracts
        if (entity is IDamageable damageable)
        {
            _entities[id] = damageable;
        }

        if (entity is IInteractable interactable)
        {
            _interactables[id] = interactable;
        }

        if (entity is ICharacter character)
        {
            _characters[id] = character;
        }

        if (entity is ILootReceiver lootReceiver)
        {
            _lootReceivers[id] = lootReceiver;
        }

        // Register colliders
        if (colliders != null)
        {
            for (int i = 0; i < colliders.Count; i++)
            {
                Collider2D col = colliders[i];
                if (col != null)
                {
                    _colliders[col] = id;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Unregisters only the capabilities owned by the expected entity instance.
    /// Collider mappings remain while another co-located capability uses the same ID.
    /// </summary>
    public bool TryUnregisterEntity(EntityId id, IEntity expectedEntity)
    {
        if (expectedEntity == null)
        {
            return false;
        }

        bool removedCapability = false;
        if (_entities.TryGetValue(id, out IDamageable damageable) && ReferenceEquals(damageable, expectedEntity))
        {
            _entities.Remove(id);
            RemoveDamageColliderRegistration(id, damageable);
            removedCapability = true;
        }

        if (_interactables.TryGetValue(id, out IInteractable interactable) && ReferenceEquals(interactable, expectedEntity))
        {
            _interactables.Remove(id);
            removedCapability = true;
        }

        if (_characters.TryGetValue(id, out ICharacter character) && ReferenceEquals(character, expectedEntity))
        {
            _characters.Remove(id);
            removedCapability = true;
        }

        if (_lootReceivers.TryGetValue(id, out ILootReceiver lootReceiver) && ReferenceEquals(lootReceiver, expectedEntity))
        {
            _lootReceivers.Remove(id);
            removedCapability = true;
        }

        if (!removedCapability)
        {
            return false;
        }

        if (HasRegisteredCapability(id))
        {
            return true;
        }

        RemoveColliderMappings(id);
        return true;
    }

    /// <summary>
    /// Attempts to retrieve a damageable entity by its EntityId.
    /// </summary>
    public bool TryGetDamageable(EntityId id, out IDamageable damageable)
    {
        return _entities.TryGetValue(id, out damageable);
    }

    /// <summary>
    /// Attempts to retrieve a character capability by its canonical entity identity.
    /// </summary>
    public bool TryGetCharacter(EntityId id, out ICharacter character)
    {
        return _characters.TryGetValue(id, out character);
    }

    /// <summary>
    /// Registers an extraction zone without changing collider or unrelated capability mappings.
    /// </summary>
    public bool TryRegisterExtractionZone(EntityId id, IExtractionZone zone)
    {
        return TryRegisterIndependentCapability(id, zone, _extractionZones);
    }

    /// <summary>
    /// Removes an extraction zone only when the expected instance still owns the registration.
    /// </summary>
    public bool TryUnregisterExtractionZone(EntityId id, IExtractionZone expectedZone)
    {
        return TryUnregisterIndependentCapability(id, expectedZone, _extractionZones);
    }

    /// <summary>
    /// Attempts to resolve an extraction zone by its canonical identity.
    /// </summary>
    public bool TryGetExtractionZone(EntityId id, out IExtractionZone zone)
    {
        return _extractionZones.TryGetValue(id, out zone);
    }

    /// <summary>
    /// Registers an extraction participant without changing collider or unrelated capability mappings.
    /// </summary>
    public bool TryRegisterExtractionParticipant(EntityId id, IExtractionParticipant participant)
    {
        return TryRegisterIndependentCapability(id, participant, _extractionParticipants);
    }

    /// <summary>
    /// Removes an extraction participant only when the expected instance still owns the registration.
    /// </summary>
    public bool TryUnregisterExtractionParticipant(EntityId id, IExtractionParticipant expectedParticipant)
    {
        return TryUnregisterIndependentCapability(id, expectedParticipant, _extractionParticipants);
    }

    /// <summary>
    /// Attempts to resolve an extraction participant by its canonical identity.
    /// </summary>
    public bool TryGetExtractionParticipant(EntityId id, out IExtractionParticipant participant)
    {
        return _extractionParticipants.TryGetValue(id, out participant);
    }

    public bool TryRegisterExtractionProgressReceiver(EntityId id, IExtractionProgressReceiver receiver)
    {
        return TryRegisterIndependentCapability(id, receiver, _extractionProgressReceivers);
    }

    public bool TryUnregisterExtractionProgressReceiver(EntityId id, IExtractionProgressReceiver expectedReceiver)
    {
        return TryUnregisterIsolatedCapability(id, expectedReceiver, _extractionProgressReceivers);
    }

    public bool TryGetExtractionProgressReceiver(EntityId id, out IExtractionProgressReceiver receiver)
    {
        return _extractionProgressReceivers.TryGetValue(id, out receiver);
    }

    /// <summary>Registers a read-only extraction progress capability independently.</summary>
    public bool TryRegisterExtractionProgressReader(EntityId id, IExtractionProgressReader reader)
    {
        return TryRegisterIndependentCapability(id, reader, _extractionProgressReaders);
    }

    /// <summary>Removes only the expected extraction progress reader instance.</summary>
    public bool TryUnregisterExtractionProgressReader(EntityId id, IExtractionProgressReader expectedReader)
    {
        return TryUnregisterIsolatedCapability(id, expectedReader, _extractionProgressReaders);
    }

    /// <summary>Attempts to resolve a read-only extraction progress capability.</summary>
    public bool TryGetExtractionProgressReader(EntityId id, out IExtractionProgressReader reader)
    {
        return _extractionProgressReaders.TryGetValue(id, out reader);
    }

    public bool TryRegisterExtractionProgressDefeatSource(EntityId id, IExtractionProgressDefeatSource source)
    {
        return TryRegisterIndependentCapability(id, source, _extractionProgressDefeatSources);
    }

    public bool TryUnregisterExtractionProgressDefeatSource(EntityId id, IExtractionProgressDefeatSource expectedSource)
    {
        return TryUnregisterIsolatedCapability(id, expectedSource, _extractionProgressDefeatSources);
    }

    public bool TryGetExtractionProgressDefeatSource(EntityId id, out IExtractionProgressDefeatSource source)
    {
        return _extractionProgressDefeatSources.TryGetValue(id, out source);
    }

    /// <summary>Registers a sanctuary capability independently from other capabilities.</summary>
    public bool TryRegisterExtractionSanctuary(EntityId id, IExtractionSanctuary sanctuary)
    {
        return TryRegisterIndependentCapability(id, sanctuary, _extractionSanctuaries);
    }

    /// <summary>
    /// Removes only the expected sanctuary capability and releases collider mappings when
    /// no other capability remains under the same identity.
    /// </summary>
    public bool TryUnregisterExtractionSanctuary(EntityId id, IExtractionSanctuary expectedSanctuary)
    {
        return TryUnregisterIndependentCapability(id, expectedSanctuary, _extractionSanctuaries);
    }

    /// <summary>Attempts to resolve a sanctuary capability by canonical identity.</summary>
    public bool TryGetExtractionSanctuary(EntityId id, out IExtractionSanctuary sanctuary)
    {
        return _extractionSanctuaries.TryGetValue(id, out sanctuary);
    }

    /// <summary>
    /// Returns whether a collider is eligible for damage detection for its entity.
    /// Entities without an explicit damage-collider registration retain legacy behavior
    /// in which every registered collider is eligible.
    /// </summary>
    public bool IsDamageCollider(EntityId id, Collider2D collider)
    {
        if (collider == null)
        {
            return false;
        }

        if (!_damageColliderRegistrations.ContainsKey(id))
        {
            return true;
        }

        return _damageColliders.TryGetValue(collider, out EntityId registeredId) && registeredId == id;
    }

    /// <summary>
    /// Attempts to retrieve an interactable entity by its EntityId.
    /// </summary>
    public bool TryGetInteractable(EntityId id, out IInteractable interactable)
    {
        return _interactables.TryGetValue(id, out interactable);
    }

    /// <summary>
    /// Registers only an interactable capability for an existing or future entity ID.
    /// Collider ownership and every other capability remain unchanged.
    /// </summary>
    public bool TryRegisterInteractable(EntityId id, IInteractable interactable)
    {
        if (interactable == null || id.Value == 0 || interactable.Id != id)
        {
            return false;
        }

        if (_interactables.TryGetValue(id, out IInteractable existing))
        {
            return ReferenceEquals(existing, interactable);
        }

        _interactables.Add(id, interactable);
        return true;
    }

    /// <summary>
    /// Removes only the interactable capability owned by the expected instance.
    /// Loot-source registration and collider mappings are not modified.
    /// </summary>
    public bool TryUnregisterInteractable(EntityId id, IInteractable expectedInteractable)
    {
        if (expectedInteractable == null || id.Value == 0 ||
            !_interactables.TryGetValue(id, out IInteractable existing) ||
            !ReferenceEquals(existing, expectedInteractable))
        {
            return false;
        }

        _interactables.Remove(id);
        if (!HasRegisteredCapability(id))
        {
            RemoveColliderMappings(id);
        }

        return true;
    }

    /// <summary>
    /// Attempts to retrieve a loot receiver entity by its EntityId.
    /// </summary>
    public bool TryGetLootReceiver(EntityId id, out ILootReceiver lootReceiver)
    {
        return _lootReceivers.TryGetValue(id, out lootReceiver);
    }

    /// <summary>
    /// Registers a loot receiver mapping separate from other entities' contracts.
    /// </summary>
    public bool TryRegisterLootReceiver(EntityId id, ILootReceiver receiver)
    {
        if (receiver == null || id.Value == 0 || receiver.Id != id)
        {
            return false;
        }

        if (_lootReceivers.TryGetValue(id, out var existing))
        {
            if (existing == receiver)
            {
                return true; // Idempotent on same instance
            }
            return false; // Rejects conflicts
        }

        _lootReceivers[id] = receiver;
        return true;
    }

    /// <summary>
    /// Unregisters a loot receiver mapping safely without removing other capacities.
    /// </summary>
    public bool TryUnregisterLootReceiver(EntityId id, ILootReceiver expectedReceiver)
    {
        if (expectedReceiver == null || id.Value == 0)
        {
            return false;
        }

        if (_lootReceivers.TryGetValue(id, out var existing))
        {
            if (existing == expectedReceiver)
            {
                _lootReceivers.Remove(id);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Atomically registers a loot source's extraction, quantity and collider capabilities.
    /// All conflicts are checked before any registry map is changed.
    /// </summary>
    public bool TryRegisterLootSource(
        EntityId id,
        ILootExtractor extractor,
        ILootQuantityReader quantityReader,
        IReadOnlyList<Collider2D> colliders)
    {
        if (id.Value == 0 || extractor == null || quantityReader == null ||
            extractor.Id != id || quantityReader.Id != id)
        {
            return false;
        }

        if (_lootSources.TryGetValue(id, out LootSourceRegistration existing))
        {
            return ReferenceEquals(existing.Extractor, extractor) &&
                ReferenceEquals(existing.QuantityReader, quantityReader);
        }

        int colliderCount = colliders?.Count ?? 0;
        var copiedColliders = new Collider2D[colliderCount];
        for (int i = 0; i < colliderCount; i++)
        {
            Collider2D collider = colliders[i];
            copiedColliders[i] = collider;
            if (collider != null && _colliders.TryGetValue(collider, out EntityId existingId) && existingId != id)
            {
                return false;
            }
        }

        // Mutation starts only after every capability and collider has passed validation.
        _lootSources.Add(id, new LootSourceRegistration(extractor, quantityReader, copiedColliders));
        for (int i = 0; i < copiedColliders.Length; i++)
        {
            if (copiedColliders[i] != null)
            {
                _colliders[copiedColliders[i]] = id;
            }
        }

        return true;
    }

    /// <summary>
    /// Removes a grouped loot source only when both expected capability instances match.
    /// </summary>
    public bool TryUnregisterLootSource(
        EntityId id,
        ILootExtractor expectedExtractor,
        ILootQuantityReader expectedQuantityReader)
    {
        if (!_lootSources.TryGetValue(id, out LootSourceRegistration existing) ||
            !ReferenceEquals(existing.Extractor, expectedExtractor) ||
            !ReferenceEquals(existing.QuantityReader, expectedQuantityReader))
        {
            return false;
        }

        _lootSources.Remove(id);
        for (int i = 0; i < existing.Colliders.Length; i++)
        {
            Collider2D collider = existing.Colliders[i];
            if (collider != null && _colliders.TryGetValue(collider, out EntityId mappedId) && mappedId == id)
            {
                _colliders.Remove(collider);
            }
        }

        return true;
    }

    private bool HasRegisteredCapability(EntityId id)
    {
        return _entities.ContainsKey(id) ||
            _characters.ContainsKey(id) ||
            _interactables.ContainsKey(id) ||
            _lootReceivers.ContainsKey(id) ||
            _lootSources.ContainsKey(id) ||
            _extractionZones.ContainsKey(id) ||
            _extractionParticipants.ContainsKey(id) ||
            _extractionProgressReceivers.ContainsKey(id) ||
            _extractionProgressReaders.ContainsKey(id) ||
            _extractionProgressDefeatSources.ContainsKey(id) ||
            _extractionSanctuaries.ContainsKey(id);
    }

    private static bool TryRegisterIndependentCapability<TCapability>(
        EntityId id,
        TCapability capability,
        Dictionary<EntityId, TCapability> registrations)
        where TCapability : class, IEntity
    {
        if (capability == null || id.Value == 0 || capability.Id != id)
        {
            return false;
        }

        if (registrations.TryGetValue(id, out TCapability existing))
        {
            return ReferenceEquals(existing, capability);
        }

        registrations.Add(id, capability);
        return true;
    }

    private bool TryUnregisterIndependentCapability<TCapability>(
        EntityId id,
        TCapability expectedCapability,
        Dictionary<EntityId, TCapability> registrations)
        where TCapability : class, IEntity
    {
        if (expectedCapability == null || id.Value == 0 ||
            !registrations.TryGetValue(id, out TCapability existing) ||
            !ReferenceEquals(existing, expectedCapability))
        {
            return false;
        }

        registrations.Remove(id);
        if (!HasRegisteredCapability(id))
        {
            RemoveColliderMappings(id);
        }

        return true;
    }

    private static bool TryUnregisterIsolatedCapability<TCapability>(
        EntityId id,
        TCapability expectedCapability,
        Dictionary<EntityId, TCapability> registrations)
        where TCapability : class, IEntity
    {
        if (expectedCapability == null || id.Value == 0 ||
            !registrations.TryGetValue(id, out TCapability existing) ||
            !ReferenceEquals(existing, expectedCapability))
        {
            return false;
        }

        registrations.Remove(id);
        return true;
    }

    private void RemoveColliderMappings(EntityId id)
    {
        var keysToRemove = new List<Collider2D>();
        foreach (KeyValuePair<Collider2D, EntityId> pair in _colliders)
        {
            if (pair.Value == id)
            {
                keysToRemove.Add(pair.Key);
            }
        }

        for (int i = 0; i < keysToRemove.Count; i++)
        {
            _colliders.Remove(keysToRemove[i]);
        }
    }

    private void RemoveDamageColliderRegistration(EntityId id, IDamageable expectedDamageable)
    {
        if (!_damageColliderRegistrations.TryGetValue(id, out DamageColliderRegistration registration) ||
            !ReferenceEquals(registration.Damageable, expectedDamageable))
        {
            return;
        }

        _damageColliderRegistrations.Remove(id);
        for (int i = 0; i < registration.Colliders.Length; i++)
        {
            Collider2D collider = registration.Colliders[i];
            if (collider != null &&
                _damageColliders.TryGetValue(collider, out EntityId registeredId) &&
                registeredId == id)
            {
                _damageColliders.Remove(collider);
            }
        }
    }

    private static bool ContainsCollider(IReadOnlyList<Collider2D> colliders, Collider2D expected)
    {
        if (colliders == null)
        {
            return false;
        }

        for (int i = 0; i < colliders.Count; i++)
        {
            if (colliders[i] == expected)
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsSameColliders(
        IReadOnlyList<Collider2D> existing,
        IReadOnlyList<Collider2D> requested)
    {
        if (existing == null || requested == null || existing.Count != requested.Count)
        {
            return false;
        }

        for (int i = 0; i < existing.Count; i++)
        {
            if (!ContainsCollider(requested, existing[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Resolves both capabilities that comprise a registered loot source.
    /// </summary>
    public bool TryGetLootSource(
        EntityId id,
        out ILootExtractor extractor,
        out ILootQuantityReader quantityReader)
    {
        if (_lootSources.TryGetValue(id, out LootSourceRegistration registration))
        {
            extractor = registration.Extractor;
            quantityReader = registration.QuantityReader;
            return true;
        }

        extractor = null;
        quantityReader = null;
        return false;
    }

    /// <summary>
    /// Attempts to retrieve the EntityId that owns a given Collider2D.
    /// </summary>
    public bool TryGetEntityId(Collider2D collider, out EntityId id)
    {
        id = default;
        if (collider == null)
        {
            return false;
        }
        return _colliders.TryGetValue(collider, out id);
    }
}
