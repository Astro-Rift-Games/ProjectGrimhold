# Project Grimhold Shared Contract Reference

> [!NOTE]
> **Status:** Current
>
> **Last updated:** 2026-07-21
>
> **Scope:** Shared identity, combat, damage, interaction, loot, and presentation contracts.

## 1. Purpose

This document is the reference for public gameplay contracts shared across Project Grimhold systems. It records the meaning and ownership of those contracts without duplicating implementation details from concrete components.

The current C# declarations are the source of truth for exact signatures. System-specific behavior belongs in the corresponding architecture document:

- [`PlayerCombatArchitecture.md`](PlayerCombatArchitecture.md)
- [`PlayerInteractionArchitecture.md`](PlayerInteractionArchitecture.md)
- [`LootInteractionArchitecture.md`](LootInteractionArchitecture.md)

## 2. Contract principles

- Requests, results, identifiers, targets, and presentation payloads are immutable value types.
- Core contracts depend on domain identifiers and values, not concrete player, enemy, UI, or Fusion types.
- Input Authority expresses intent. State Authority validates and commits authoritative gameplay changes.
- Presentation observes confirmed results or replicated state and never decides gameplay outcomes.
- Network representation errors are translated or diagnosed at integration boundaries; they are not exposed as domain-specific Fusion failures.
- Public results use typed failure reasons. Exceptions indicate programmer or integration contract violations, not normal gameplay rejection.
- Capabilities are segregated so an entity implements only the operations it actually supports.

## 3. Identity and entity capabilities

### `EntityId`

`EntityId` is an immutable, comparable wrapper around an integer. It identifies an entity independently from a concrete `GameObject`, `NetworkObject`, player, or enemy type.

`default(EntityId)`, whose value is zero, represents an invalid or absent identity where request validity requires a real endpoint.

### `IEntity`

```csharp
public interface IEntity
{
    EntityId Id { get; }
}
```

All entity capabilities derive from `IEntity` when their operation is associated with a registered gameplay entity.

### `ICharacter` and `IPickup`

- `ICharacter : IEntity` exposes whether an entity is alive.
- `IPickup : IInteractable` marks an interactable as a pickup without adding unrelated inventory behavior.

### Entity resolution

`EntityRegistry` is runner-scoped infrastructure that maps an `EntityId` to the independently registered character, damage, interaction, loot and extraction capabilities for that entity. It prevents gameplay code from depending on concrete prefab classes. Capability conflicts are rejected, and expected-instance unregistration cannot remove a replacement or an unrelated co-located capability.

Registration does not grant network authority. The authoritative caller must still validate State Authority before changing gameplay state.

### Individual extraction progress contracts

- `ExtractionProgressContribution` is immutable and contains source type, valid source `EntityId`, `long` amount and authoritative `SimulationTick`. Tick is metadata, not deduplication.
- `ExtractionProgressSnapshot` contains current individual progress, configured quota, percentage, quota completion and pending assignment. It is distinct from `ExtractionCountdownSnapshot`, the renamed zone-timer projection.
- `IExtractionProgressReceiver : IEntity` accepts direct State Authority contributions. It owns saturation and player lifecycle validation, not producer deduplication.
- `IExtractionProgressDefeatSource : IEntity` exposes the non-negative configured reward associated with defeating that entity.
- `ILootFirstAcquisitionSource` exposes one side-effect-free query for a source-validated `LootTransferRequest`. `LootFirstAcquisitionResult` returns an eligible amount in the closed range from zero to requested amount. There is no provenance commit contract.

These capabilities are independently registered per runner and per `EntityId`. The MVP has individual progress only and no team progress, shared quota or global progress service.

## 4. Combat contracts

### Attack execution

`IAttack` represents an attack strategy:

```csharp
public interface IAttack
{
    AttackType Type { get; }
    float CooldownSeconds { get; }
    AttackInputMode InputMode { get; }
    AttackResult Execute(in AttackRequest request);
}
```

`AttackRequest` contains runtime intent only:

- `AttackerId`
- `Origin`
- normalized `Direction`, or zero when no direction was supplied
- `SimulationTick`

Range, damage, cooldown, projectile speed, and other balance rules belong to attack configuration or the selected strategy rather than the request.

`AttackResult` reports whether execution occurred and, when rejected, an `AttackFailureReason`. A successful attack execution does not imply that damage was applied. In particular, a ranged attack may succeed by spawning a projectile whose impact is resolved later.

### Target queries

`IAttackTargetQuery.FindTargets(in AttackTargetQuery)` isolates spatial detection from attack strategies. The query carries attacker identity, origin, normalized direction, range, radius, maximum targets, and layer mask. Each `AttackTarget` contains only the resolved `TargetId` and hit point.

Implementations must exclude invalid targets, deduplicate entities that own multiple colliders, and honor the requested maximum count. Target queries detect candidates; they do not apply damage.

### Projectile spawning

`IProjectileSpawner.Spawn(in ProjectileSpawnRequest)` isolates core attack logic from Fusion spawning infrastructure.

The immutable request contains owner identity, origin, normalized direction, damage data, speed, lifetime, maximum range, and simulation tick. `ProjectileSpawnResult` reports whether the projectile was spawned.

Only State Authority may perform the authoritative network spawn. A spawned projectile owns later movement and impact timing; the attack does not apply ranged damage immediately.

## 5. Damage contracts

### `DamageRequest`

`DamageRequest` describes one immutable damage attempt:

- attacker and target identities
- requested amount and `DamageType`
- direction and hit point
- simulation tick

### `IDamageResolver`

```csharp
public interface IDamageResolver
{
    DamageResult Resolve(in DamageRequest request);
}
```

The resolver validates authority and request data, resolves the target capability, applies shared rules such as self-damage policy, and delegates final health ownership to `IDamageable`.

### `IDamageable`

```csharp
public interface IDamageable : IEntity
{
    bool CanReceiveDamage { get; }
    DamageResult ApplyDamage(in DamageRequest request);
}
```

The damageable entity owns health, mitigation, death, and the final applied amount. Attacks and projectiles must not modify health directly.

`DamageResult` contains target identity, whether damage was applied, applied damage, remaining health, fatal state, and a typed `DamageFailureReason`.

## 6. Interaction contracts

### Request and target discovery

`InteractionRequest` contains `InteractorId`, `TargetId`, and `SimulationTick`.

`IInteractionTargetQuery.FindTargets(in InteractionTargetQuery)` finds candidates using the interactor identity, origin, maximum distance, and layer mask. Each `InteractionTarget` contains a target identity, closest point, and measured distance.

Target queries discover and order candidates. They do not execute interactions.

### `IInteractable`

```csharp
public interface IInteractable : IEntity
{
    bool CanInteract(in InteractionRequest request);
    InteractionResult Interact(in InteractionRequest request);
}
```

`CanInteract` is a lightweight eligibility query used during selection. `Interact` remains the authoritative operation and returns the final interaction outcome. Callers must not infer authority or guaranteed success from `CanInteract` alone.

### `InteractionResult`

`InteractionResult` is the general result of interacting with a world entity. It records:

- `Success`
- whether the target was consumed
- `InteractionFailureReason`

The result intentionally does not expose every subsystem-specific reason. An interactable translates its detailed domain outcome to the stable interaction vocabulary.

For loot pickups:

- `MissingAuthority` maps to `MissingStateAuthority`.
- `DestinationNotFound` maps to `ReceiverNotFound`.
- `OutOfRange` maps to `OutOfRange`.
- Other loot rejections map to `LootRejected`.

## 7. Loot value objects

### `LootId`

`LootId` is a small immutable identifier compared with ordinal string semantics. `IsValid` is derived from a non-empty, non-whitespace value.

The public constructor rejects an invalid textual ID because constructing a configured loot identity is a programming or configuration boundary. `default(LootId)` remains representable so requests and entries can expose derived validity and be tested safely.

### `LootEntry`

`LootEntry` is the only canonical aggregated loot stack. It contains exclusively:

- `LootId`
- `Amount`
- derived `IsValid`
- value equality and a consistent hash code

The constructor preserves supplied values, including invalid quantities, so validation remains explicit at system boundaries. An entry is valid only when its loot ID is valid and its amount is positive.

There is no parallel `LootStack` type.

## 8. Unified loot transfer model

### `LootTransferRequest`

`LootTransferRequest` is an immutable request to move one complete quantity between two entities:

```csharp
public readonly struct LootTransferRequest
{
    public EntityId SourceId { get; }
    public EntityId DestinationId { get; }
    public LootId LootId { get; }
    public int RequestedAmount { get; }
    public int SimulationTick { get; }
    public int SourceInventorySlotIndex { get; }
    public int ExpectedInventoryRevision { get; }
    public bool HasInventorySlotAddress { get; }
    public bool IsValid { get; }
}
```

Validity requires non-default source and destination identities, a valid loot ID, a positive requested amount, and valid source metadata. The legacy constructor supplies the unaddressed pair `-1/-1`; an addressed request requires a slot index in `0..29` and an opaque integer revision other than the reserved `-1`. Mixed sentinel pairs are invalid. The simulation tick is preserved without imposing an additional validity rule.

The constructor preserves invalid gameplay data instead of throwing, allowing endpoints to return typed rejection reasons. Source metadata is ephemeral intent, not provenance or persisted position. At runtime an addressed source must be the authenticated actor's Inventory: validation binds the current source revision, address, item and complete selected quantity, without fallback, retargeting or automatic retry. A destination validates metadata shape but never compares the source revision with its own. See [Addressed Inventory actions](LootInteractionArchitecture.md#addressed-inventory-actions) for the owning integration contract.

### `LootTransferResult`

`LootTransferResult` represents either complete success or complete rejection:

- `Succeeded(request)` accepts only a valid request and copies its complete `RequestedAmount`.
- `Rejected(reason)` accepts only a defined failure reason and records zero transferred quantity.
- `default(LootTransferResult)` is invalid and represents an uninitialized result.
- No public factory can create partial or contradictory results.

Factory exceptions identify incorrect API usage. They are not normal gameplay control flow.

### `LootTransferFailureReason`

The stable domain reasons are:

| Reason | Meaning |
| --- | --- |
| `Uninitialized` | Sentinel for a result that has not been produced. It is not a valid rejection. |
| `None` | No failure. Used only by successful validation or results. |
| `InvalidLoot` | The loot identity is invalid or has no usable domain definition. |
| `InvalidAmount` | The requested quantity is not positive. |
| `SourceNotFound` | The source entity cannot be resolved. |
| `DestinationNotFound` | The destination entity cannot be resolved. |
| `InsufficientAmount` | The source cannot provide the complete requested quantity. |
| `InventoryFull` | The endpoint's gameplay slot capacity prevents receiving the complete quantity; an existing player item ID may also require another physical slot. |
| `OutOfRange` | The endpoints do not satisfy the gameplay distance rule. |
| `MissingAuthority` | The operation is not running under the required State Authority. |
| `ContainerUnavailable` | A resolved endpoint cannot currently participate in the operation. |
| `Overflow` | Applying the complete quantity would exceed the supported integer range. |
| `PlayerUnavailable` | The player endpoint is unavailable or in Extracted process state. |

Catalog index failures and Fusion representation details are not public failure reasons. Integrations diagnose them locally and translate them to an appropriate stable reason, such as `InvalidLoot` or `ContainerUnavailable`.

## 9. Loot capabilities

Capabilities are intentionally independent:

| Capability | Contract |
| --- | --- |
| `ILootContentReader` | Produces a complete `IReadOnlyList<LootEntry>` snapshot. |
| `ILootQuantityReader` | Returns the aggregated amount for one `LootId`; invalid or absent IDs return zero. |
| `ILootSlotCapacityReader` | Exposes gameplay `SlotCapacity` and `OccupiedSlotCount`. |
| `ILootReceiver` | Prevalidates and commits reception. |
| `ILootExtractor` | Prevalidates and commits extraction. |

Reading must not expose a mutable backing collection. A caller may retain or enumerate a snapshot without gaining write access to authoritative storage.

Slot semantics belong to each endpoint; aggregate read capabilities do not require aggregate authoritative storage. Content snapshots remain detached `LootEntry` projections, and quantity queries remain item aggregates.

### Player Inventory

`PlayerLootReceiver` is the sole owner of Inventory quantity and exact Raid origin in a `NetworkArray<LootInventorySlot>` of exactly 30 physical slots. Its positive serialized `SlotCapacity` is an occupied-slot limit no greater than 30, configured to 30 on the base network-player prefab. A lower limit neither restricts the address range nor repacks or discards existing slots. `OccupiedSlotCount` counts physical stacks; `DistinctLootCount` counts item identities.

Stacks merge only when both item identity and exact origin match: `Dungeon` or `Player(RaidParticipantId)` with participant values `1..16`. Different participants' units never merge. Reception fills compatible partial stacks in ascending address order, then empty slots in ascending order, respecting per-stack `StackMax` and the item's aggregate `MaxTransportable` across all slots and origins. An existing item ID can require another physical slot and fail with `InventoryFull`. Extraction clears only depleted slots and never compacts.

State Authority alone writes quantity and origin together. Complete preflight includes all incoming origin buckets competing for holes and, for equipment exchanges, all displaced equipment origins; rejection changes neither quantity, origin nor revision. Each successful transfer commit advances the joint `LootChangeSequence` once, not once per stack or bucket. The detailed storage and planning contract belongs to [Player physical Inventory](LootInteractionArchitecture.md#player-physical-inventory).

The temporary inventory has the same lifecycle as the player's `NetworkObject`. On authoritative defeat, `PlayerCorpseGenerationController` copies and verifies its snapshot in the unavailable `NetworkLootContainer` co-located on that same object, clears the receiver exactly once, and only then makes the source available. Player despawn or runner shutdown destroys both replicated collections with that object, local presentation queues are cleared during despawn, and a player spawned in a later session starts from empty network collections. No loot state is persisted in static fields, services, or `ScriptableObject` assets.

### Containers

`NetworkLootContainer` remains a reusable bidirectional endpoint implementing reception, extraction, quantity, content and slot-capacity reads. It is not itself interactable. Its aggregate `NetworkDictionary<catalog index, quantity>` retains a technical capacity of 30, distinct from its configured gameplay capacity: six distinct item IDs for world/enemy containers and 30 for the co-located defeated-player container. These capacities remain local static configuration. Increasing an existing ID needs no new gameplay slot; a new ID requires one. Containers do not migrate to physical player slots or inherit player per-stack origin/capacity rules. A technical representation failure is not `InventoryFull`.

State Authority owns container quantities, initialization, availability and change sequence. Availability is independent of emptiness and changing it does not mutate content or the sequence. Reception retains aggregate distinct-ID and overflow rules; extraction removes an entry only when its complete requested quantity depletes it. Successful commits advance `LootChangeSequence`. Container provenance remains in the separate packed `ContainerRaidLootOriginState`, as specified by [Raid loot origin](LootInteractionArchitecture.md#raid-loot-origin); player slot ownership does not replace it.

Containers are deliberately not registered in the single-value `EntityRegistry` receiver map. A defeated player's `PlayerLootReceiver` and `NetworkLootContainer` share one `NetworkId`; the authoritative transfer controller therefore resolves the exact container component from the destination `NetworkObject` for deposits. This keeps the active player inventory registered as the entity receiver while preventing deposits from being routed into a defeated player's co-located inventory.

`NetworkLootContainerInteractable` is the same-root interaction adapter and registers only the `IInteractable` capability. A defeated player, defeated enemy, or chest enters the same interaction and transfer contracts through composition; none receives a specialized corpse identity, RPC, transaction, controller, registry route, or screen mode. An available empty container remains available and its owning object is not despawned by the loot contract.

## 10. Loot prevalidation and commit protocol

Reception and extraction each have two explicit phases:

```csharp
LootTransferFailureReason ValidateReceive(in LootTransferRequest request);
void CommitReceive(in LootTransferRequest request);

LootTransferFailureReason ValidateExtraction(in LootTransferRequest request);
void CommitExtraction(in LootTransferRequest request);
```

### Prevalidation

- Performs no mutation.
- Returns `None` only when the endpoint can immediately apply the complete requested amount.
- Returns a typed gameplay rejection for expected invalid conditions.
- Checks the rules owned by that endpoint, including identity, authority, loot, quantity, availability, capacity where supported, and overflow.

### Commit

- Runs only after successful prevalidation.
- Runs synchronously without yielding authoritative control, re-entry, or intervening mutation.
- Does not repeat gameplay validation.
- Does not return a rejection result.
- Applies exactly `RequestedAmount`.

If a commit cannot apply the prevalidated amount, the caller or implementation violated the protocol. That condition must be diagnosed as an integration/programming error rather than converted into a late gameplay rejection.

`LootTransferTransaction.Execute` implements the atomic runtime protocol for already resolved endpoints. It validates extraction, validates reception, commits extraction and then commits reception. It deliberately does not resolve entities, catalogs or distance and is not a runner-scoped coordinator.

## 11. Authoritative pickup integration

`NetworkLootPickup` is a consumable source with its own reservation, not an `ILootExtractor` and not a storage-to-storage coordinator.

Its authoritative sequence is:

1. Validate State Authority, interaction request, range, and pickup availability.
2. Resolve the destination's `ILootReceiver` capability.
3. Build the unified `LootTransferRequest`.
4. Reserve the pickup by setting its consumed state.
5. Call `ValidateReceive`.
6. Restore the reservation on every rejection before commit.
7. Call `CommitReceive` immediately after successful validation.
8. Publish pickup-only feedback through `ILootPickupFeedbackSink` after commit.
9. Produce complete success and despawn only after the commit.

This ordering prevents duplicate reward delivery while preserving the pickup's existing single-consumption transaction. It is an integration between a consumable source and a receiver, not a general transfer between two inventories.

## 12. Runtime container transfer integration

`PlayerLootTransferNetworkController` accepts one legitimate Input Authority request at a time. Its Fusion RPC transports source and destination integer IDs, catalog index, quantity mode and sequence. State Authority validates the direction, resolves `SingleUnit` to one or `FullStack` to the complete current authoritative amount, derives loot identity, distance and simulation tick, and then invokes `LootTransferTransaction`.

The authoritative adapter owns one local non-networked pending request and one last-processed cache. Pending cannot be overwritten. Exact pending duplicates are ignored, conflicting payloads do not affect gameplay, busy/stale sequences receive an internal transport rejection where required, and an exact processed duplicate replays only the cached primitive confirmation.

`LootTransferConfirmation` is an integration payload rather than a core gameplay contract. It preserves sequence, endpoint IDs, catalog index, tick and domain result while resolved `LootId` metadata remains optional. Rejections may be usable without a local definition; success requires one.

## 13. Presentation contracts

Presentation payloads describe confirmed gameplay without owning or mutating gameplay state:

- `AttackPerformedEvent` describes a successfully executed attack for animation, audio, or VFX.
- `DamageResolvedEvent` pairs a request with its resolved damage result.
- `InteractionPresentationEvent` carries the authoritative interaction sequence and outcome to local presentation.
- `LootGrantPresentationEvent` describes only an authoritative world-pickup delivery and resolves visual metadata locally through the loot catalog.
- `LootTransferConfirmation` describes a player adapter transfer result and may omit unresolved loot metadata for rejections.

Presentation events must not apply damage, commit inventory changes, spawn or despawn authoritative objects, or decide whether an interaction succeeded. Networked gameplay remains the source of truth.

`PlayerLootReceiver.CommitReceive` emits no feedback. `NetworkLootPickup` publishes the pickup event through the optional sink after a successful commit. Container confirmations are delivered separately during `Render` and do not generate the pickup toast.

## 14. Validation expectations

Contract tests should focus on observable semantics:

- construction and derived validity
- value equality and hash consistency
- request and result invariants
- contradictory factory inputs
- independent capability implementations through stubs
- validators that do not mutate state
- commit signatures that cannot return gameplay rejection
- read-only content snapshots
- receiver and pickup integration

Architecture independence is verified through actual dependencies, imports, public types, and diff review. Tests must not infer architecture by searching for concrete class names such as `Player`, `Chest`, or `EnemyCorpse`.

EditMode tests cover the pure transaction, bounded request state, grouped registry behavior and prefab composition. Host/Client range, capacity, competition and presentation remain manual checks through the separate development harness. The harness also queues availability changes from the State Authority peer so `SetAvailability` still executes inside authoritative simulation.
