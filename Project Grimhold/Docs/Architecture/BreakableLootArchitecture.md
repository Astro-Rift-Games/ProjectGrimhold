# Breakable Loot Architecture

## Context and decision

Breakable world obstacles are authoritative network entities spawned from scene
points. They receive the same `DamageRequest` used by character combat and release
ordinary `NetworkLootPickup` objects directly into the world. A destroyed
breakable is never inspectable and never becomes a loot container.

Random content reuses `LootContainerContentTable`,
`LootContainerContentTableValidation`, `LootContainerContentRoller` and
`LootDefinitionCatalog`. This keeps one catalog identity and one weighted-roll
implementation for containers and world drops.

## Responsibilities and data flow

```text
NetworkSpawnSceneConfiguration (Breakables points and amount)
  -> NetworkSpawnManager (State Authority)
  -> validate table/catalog/prefabs and additional-stack capacity
  -> derive group-scoped seed and frozen-cohort Luck probability
  -> Runner.Spawn(BreakableObject, OnBeforeSpawned)
  -> BreakableObject replicates the Pending generation descriptor

DamageRequest
  -> DamageResolver
  -> BreakableObject.ApplyDamage
  -> partial damage leaves generation Pending
  -> fatal damage consumes Pending as Resolved or Failed
  -> confirm fatal damage and IsDestroyed independently of generation outcome
  -> disable/unregister damage and blocking colliders
  -> on Resolved only, Runner.Spawn(NetworkLootPickup, OnBeforeSpawned) per stack
  -> pickup replicates catalog index and quantity
  -> existing interaction and inventory transfer flow
```

## Sources of truth and ownership

- `BreakableObject.Health` and `IsDestroyed` are networked simulation state.
- Seed, effective additional-Loot probability and `LootSourceGenerationState` are networked descriptor state. `Pending` may transition only once to terminal `Resolved` or `Failed`.
- State Authority alone applies damage, confirms destruction and spawns pickups.
- The content table, catalog, pickup prefab and local offsets are immutable prefab configuration.
- The materialized `LootEntry[]` exists only on State Authority after a successful fatal resolution. Proxies receive the descriptor and terminal state through snapshots but never roll.
- Each spawned pickup owns the replicated catalog index, quantity and consumed state. Static display data is resolved locally from the shared catalog.
- `NetworkLootPickup` is spawned at an authoritative world position and therefore its root requires `Fusion.NetworkTransform`, registration in `NetworkedBehaviours`, and `HasMainNetworkTRSP`. This is transform replication, not an AOI workaround.
- `IsDestroyed` is committed before pickup spawning and is the one-shot guard against simultaneous or repeated damage. Descriptor state survives Host Migration, so neither `Resolved` nor `Failed` can roll again.

## Boundaries and failure policy

`BreakableObject` implements only `IDamageable`; it does not implement
`IInteractable`, `ILootExtractor`, `ILootQuantityReader`, or contain
`NetworkLootContainer`. Presentation observes replicated destruction and never
changes gameplay state.

Invalid prefab, catalog, table, capacity without one additional-stack slot or
pre-spawn descriptor initialization skips the affected breakable spawn. An invalid
returned network object is immediately despawned. At fatal damage, any generation
failure first commits terminal `Failed` and produces zero pickups; it never rejects
or rolls back the fatal damage, destruction, collider removal or registry removal.
Runtime pickup initialization failure is logged and the invalid pickup is despawned;
destruction and generation state are not rolled back or retried.

## Scene authoring

Add a `SpawnGroupType.Breakables` entry to the scene's
`NetworkSpawnSceneConfiguration`, assign unique points and set `Amount` no higher
than the point count. Assign `BreakableObject.prefab` to the scene-owned
`NetworkSpawnManager`. The current `Gameplay` scene intentionally has no
Breakables group or points.

The prefab uses a Character-layer trigger for combat targeting and a separate
WorldCollision-layer solid collider for movement blocking. Both are disabled
after destruction.

## Validation

- EditMode validates group dispatch, seed-domain separation, prefab composition, table/catalog compatibility and point idempotence.
- PlayMode validates that partial damage leaves generation pending, fatal success creates one deterministic batch, fatal failure still destroys with zero pickups, repeated hits cannot resolve again, and colliders/renderers are removed.
- Manual Host/Client validation must confirm replicated descriptor and terminal state, simultaneous fatal hits, identical pickups, collection, late joining and Host Migration before and after both `Resolved` and `Failed`.

## Pickup first-acquisition provenance

Every `NetworkLootPickup` replicates both total quantity and first-acquisition eligible quantity. Existing manually configured pickups and pickups spawned from natural or breakable content initialize eligibility equal to their quantity. A pickup provisionally spawned by `PlayerLootDropNetworkController` receives explicit eligibility zero and cannot be published otherwise. No separate pickup type is introduced.

The pickup reserves its one-shot state before validating reception. A rejection releases the reservation without consuming eligibility. After `CommitReceive`, it computes `ExtractionValuePerUnit * EligibleAmount` with `long`, contributes directly to the receiving player's individual progress, clears eligibility, publishes the existing feedback and despawns. This ordering prevents rejected pickups, inventory drops and reacquisition from duplicating progress. Breakable destruction remains authoritative and non-rollback; only a failed provisional pickup spawn is compensated by despawning the invalid publication.
