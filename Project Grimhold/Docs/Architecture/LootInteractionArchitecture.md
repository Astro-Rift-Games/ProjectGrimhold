# Loot Interaction and Transfer Architecture

## Context and decision

Loot movement uses `LootEntry` as its only runtime stack and `LootId` as its domain identity. `LootDefinitionCatalog` assigns deterministic indices by ordinal ID order. Fusion transports and replicates catalog indices and quantities; every peer resolves static names, icons, rarity and value locally.

TASK-33 adds a reusable synchronized source and an authoritative transfer adapter. TASK-34 composes that source with a separate `IInteractable` adapter and local presentation for selective looting. TASK-50 adds single-unit and full-stack intentions in both directions. TASK-51 composes ordered full-stack withdrawals locally without adding a bulk RPC, transaction or networked batch state.

## Components and responsibilities

- `NetworkLootContainer` owns replicated container contents, initialization, runtime availability, change sequence and the registry mapping for its loot capabilities and colliders. It implements `ILootExtractor`, `ILootQuantityReader`, `ILootContentReader` and `ILootSlotCapacityReader`, but not `ILootReceiver` or `IInteractable`.
- `NetworkLootContainerInteractable` shares the container root and `NetworkObject`, derives the same `EntityId`, and owns only its independently registered `IInteractable` capability. It never registers colliders or changes loot state.
- `PlayerLootReceiver` remains the only temporary player inventory. Its validators own expected gameplay rejection; its commits apply a previously validated request.
- `LootTransferTransaction` performs `ValidateExtraction -> ValidateReceive -> CommitExtraction -> CommitReceive` synchronously. It performs no entity resolution, catalog lookup, range calculation or presentation.
- `PlayerLootTransferNetworkController` is the Fusion integration boundary on the player object. Input Authority sends an intention containing a quantity mode; State Authority derives requester, destination, exact quantity, range and tick, then executes the transaction.
- `EntityRegistry` remains runner-scoped. A grouped loot-source registration atomically publishes extractor, quantity reader and associated colliders. Independent interactable registration composes with that source in either lifecycle order and removes only the expected owner.
- `LootContainerTransferDebugHarness` is separate development tooling. It is not attached to production player prefabs or scenes.

## Sources of truth and network authority

The container replicates only:

```text
NetworkDictionary<catalog index, quantity>
IsInitialized
IsAvailable
LootChangeSequence
```

`IsEmpty` and occupied slots are derived. Initial configuration is fully validated before any stack is written and is then loaded in catalog-index order. State Authority alone initializes content and writes availability. A container with no requested override uses its serialized `LootContainerInitialEntry` values; an authoritative pre-spawn override uses its materialized `LootEntry` values; a rejected override fails closed and never falls back to manual content. Every peer registers the source and colliders locally for runner-scoped discovery, including before its replicated snapshot observes initialization. Proxy registration grants neither extraction authority nor mutation access; authoritative validation and commits still require State Authority. If grouped registration fails on State Authority, no partial registry entries remain and availability stays false.

Generated chests use `LootContainerContentTable` as stable configuration. `NetworkSpawnManager` validates it into an immutable snapshot containing catalog indices, integer weights and amount ranges, then performs weighted selection without replacement through a local SplitMix64-based roller. Weight mapping uses integer rejection sampling rather than floating point or global `UnityEngine.Random`. The result is ordered by catalog index and applied through Fusion's `OnBeforeSpawned` callback before `NetworkLootContainer.Spawned` publishes the synchronized dictionary. Seeds and overrides remain local and non-networked; late joiners consume the existing snapshot.

`SetAvailability(bool)` requires State Authority. Enabling requires successful initialization and registration. Repeating a value is idempotent. Availability changes neither contents, registration, despawn state nor `LootChangeSequence`. `ValidateExtraction` returns `ContainerUnavailable` when the source cannot participate.

## Prevalidation and commit invariant

Expected failures, including missing authority, invalid loot or amount, insufficient quantity, capacity, overflow, unavailable containers, and extracted players (`PlayerUnavailable`), are returned by endpoint validators. After both validators return `None`, the two commits must apply the exact request.

Commits do not return rejections and do not silently skip mutation. Defensive structural checks diagnose an impossible integration state with a contextual error and exception. Because each commit verifies its structural contract before changing its own collection and the transaction runs synchronously without yielding or callbacks, a violated contract stops execution instead of allowing the destination commit to continue after an omitted extraction.

## Transport and local request lifecycle

Domain structs are never sent as RPC parameters. The request RPC contains only:

```text
source EntityId value
destination EntityId value
catalog index
quantity mode
request sequence
```

Input Authority permits one legitimate request in flight. `HasRequestInFlight` is the only local pending source of truth. A locally rejected second request neither sends an RPC nor advances sequence. Matching confirmation or transport rejection releases it immediately; despawn/session restart clears it.

State Authority revalidates player availability when it consumes the pending request. An extracted player produces `LootTransferFailureReason.PlayerUnavailable` through the ordinary confirmation path; it never exits silently. The matching confirmation releases pending and `HasRequestInFlight`. A Take All sequence then continues or finishes according to its existing per-request rejection policy.

“Take all” snapshots only the valid `LootId` values visible in the container panel, preserving presentation order and never copying client quantities. `RaidInventoryPresenter` submits one existing `FullStack` intention, waits for its confirmation or transport rejection, refreshes both endpoint projections and then advances. A rejection does not undo earlier commits or stop later identities. Newly appearing stacks are outside the snapshot; removed or changed stacks are revalidated normally by State Authority. Closing or losing the container cancels only future local intentions, while an already accepted request completes through the normal lifecycle.

State Authority stores one local, non-networked pending identity and never overwrites it. It distinguishes an exact pending duplicate, conflicting payload with the same sequence, a different sequence while busy, an exact duplicate of the last processed request, and stale input. Pending is consumed only by `FixedUpdateNetwork`. Only the last processed identity and confirmation are cached; an exact processed duplicate resends that confirmation without executing gameplay.

The confirmation RPC contains only sequence, source/destination integer IDs, catalog index, transferred amount, success, failure reason integer and simulation tick. Input Authority first verifies sequence, then releases matching in-flight state before validating the rest of the envelope. A malformed matching payload is diagnosed without blocking later requests. An unknown sequence releases nothing and publishes no gameplay.

`LootTransferConfirmation` belongs to the adapter layer. It always preserves primitive identity, index, tick and `LootTransferResult`; resolved `LootId` metadata is optional. Success requires positive amount, `None` and a resolvable catalog entry. A valid rejection such as `InvalidLoot` can be published without local metadata.

Presentation notification is deferred through one bounded local queue. RPC and simulation callbacks may update `HasRequestInFlight`, but they do not invoke presenters. `Render` publishes a changed pending value once and then the corresponding confirmation, preserving `RequestInFlightChanged(false) -> TransferConfirmed`. Transport rejection publishes only finalization. Reset and despawn discard queued presentation without callbacks or history growth.

## Range and competition

For every consumed pending request, State Authority reruns `Physics2DInteractionTargetQuery` against registry colliders using the player's authoritative origin and interaction configuration. It never trusts client position, amount, destination or an earlier target selection.

Fusion simulation processes authoritative requests serially. A single-unit intention resolves to one, while a full-stack intention resolves from the current authoritative source amount. Each exact resolved request commits atomically before a later competing player validates, so the later request observes the remaining amount or an insufficient source rather than duplicating loot.

## Pickup presentation boundary

`CommitReceive` is generic and emits no pickup toast. `ILootPickupFeedbackSink` is an optional integration boundary implemented by `PlayerLootReceiver`. `NetworkLootPickup` invokes it only after a successful pickup commit. Its presentation RPC uses primitive values and reconstructs `LootGrantPresentationEvent` locally. Container transfers never consult this sink.

`NetworkLootPickup` also supports authoritative pre-spawn initialization for
world drops. It replicates catalog index and quantity, resolves the shared
`LootDefinition` and world sprite locally, and otherwise uses the same registry,
interaction, transfer, feedback and despawn flow. Breakables release these
pickups directly and never expose a container interaction; see
`Docs/Architecture/BreakableLootArchitecture.md`.
World rendering remains prefab-owned rather than definition-owned: the generic
pickup exposes a sorting layer and order, then applies both whenever it resolves
a `LootDefinition.WorldSprite`. Dynamic loot therefore shares one configurable
map-rendering policy without requiring a prefab per loot definition.

## Prefabs and development validation

`NetworkPlayer.prefab` contains `PlayerLootTransferNetworkController` and no debug component. `LootContainer.prefab` contains one `NetworkObject`, `NetworkLootContainer`, `NetworkLootContainerInteractable`, enabled `LootContainerRandomContentConfig`, local prompt metadata, a layer-8 collider and its provisional visual. Its serialized manual stacks remain an empty development fallback; the production spawn path requires a valid random table.

Gameplay's authoritative `NetworkSpawnManager` consumes only the `SpawnGroupType.Loot` scene group and spawns `LootContainer.prefab` at ordered, unique points without Input Authority. It owns one local session seed and derives each chest seed from session, scene-load generation and point index. It validates and rolls content before calling Fusion, applies the materialized override in `OnBeforeSpawned`, and records the point only after callback application, initialization and availability are confirmed. A failed returned object is despawned immediately and does not consume the point, so a retry uses the same deterministic seed without leaving an orphan. The spawn generation remains runner-scoped and idempotent; requests beyond available points are clamped rather than overlapped. Unsupported NPC, boss and miscellaneous groups are skipped and never receive an enemy fallback.

Defeated enemies remain the same network entity instead of spawning a replacement corpse. `NetworkEnemy.prefab` composes the shared `NetworkLootContainer` and `NetworkLootContainerInteractable` on its root `NetworkObject`, with an Interactable-layer trigger used only for loot queries. The container initializes with the enemy but starts unavailable. When authoritative damage reaches zero, `EnemyCharacter` disables movement and combat and enables that existing container during simulation. The enemy therefore preserves its `NetworkId`, position, colliders and replicated contents across the alive-to-defeated transition. Its defeat presentation keeps the body visible and may later settle into a death-animation pose; presentation state never owns or changes loot. No separate corpse prefab, automatic replacement spawn or dead-entity inventory copy exists.

Players keep the same `NetworkPlayer` identity after defeat. The prefab composes `NetworkLootContainer`, `NetworkLootContainerInteractable`, prompt metadata and a dedicated layer-8 trigger alongside `PlayerLootReceiver`; the container begins empty and unavailable, and its receiver capability cannot accept transfers until the authoritative defeat handoff makes it available. `CharacterBase.ApplyDamage` reaches `PlayerCharacter.HandleDeath`, which invokes `PlayerCorpseGenerationController` in the same authoritative simulation flow. Its networked one-shot state (`Waiting`, `Processing`, `Completed`, `Failed`) captures the complete expedition ownership snapshot: normal inventory plus Weapon Slot 1 and Weapon Slot 2, aggregated by `LootId`. It validates and loads the co-located unavailable container, verifies Inventory and both Equipment slots exactly, clears all three ownership locations exactly once, and only then enables the container. Extraction uses the same aggregation before committing to the local stash and clears those sources only after its existing ACK. A load or clear failure keeps the container unavailable, rolls it back to empty when necessary, preserves player ownership, enters `Failed`, and never retries. No player-corpse prefab, secondary `NetworkObject`, generated Fusion prefab registration, appearance copy, or alternate transfer route exists. `PlayerDefeatPresenter` retains the defeated player's own final pose independently of this gameplay transition; presentation has no authority over content, availability, or interaction.

The defeated player then uses the same interaction and transfer route as every
other container. There is no corpse-specific RPC, transaction, controller,
registry capability, or presentation mode. Removing the last stack increments
the existing change sequence but does not make the container unavailable or
despawn the player.

`Assets/Prefabs/Debug/LootContainerTransferDebugHarness.prefab` can be placed manually in a graybox. In Editor or Development Build it resolves the local player through `TryGetPlayerObject`, detects nearby containers directly from colliders, reads their snapshot and invokes the public or raw debug transport methods. F8 sends a public full-stack request; F9 repeats its exact envelope; F10 reuses its sequence with a conflicting catalog index; F11 sends a different sequence while the legitimate request is in flight; and F12 queues an availability toggle that only succeeds on the peer holding State Authority and is applied by the container in `FixedUpdateNetwork`. Press F8 together with F9, F10 or F11 to guarantee both envelopes arrive before the next simulation tick. In a non-development release the class remains loadable but disables itself and performs no input or searches.

## Risks and validation

Catalogs must be identical across peers because transport indices are catalog-local. Invalid success envelopes are rejected at the transport boundary. Missing/disabled random configuration, invalid tables, weight overflow and impossible capacities skip only the Loot group. A callback or initialization failure triggers authoritative compensating despawn. Registry conflicts leave containers unavailable and therefore invalidate the production spawn instead of leaving an orphan.

Edit Mode prefab tests cover the shared root, single `NetworkObject`,
initially unavailable empty container, dedicated interaction trigger and
baked network behaviours for the base, melee and ranged player prefabs. Shared
runtime `EntityId` is verified only in Play Mode. Single Runner Play Mode tests
cover the alive-to-defeated availability transition,
generic interaction resolution, local confirmed opening, single-unit and full-stack transfer,
change-sequence refresh, persistent empty state, distance close, despawn
unregistration and shutdown cleanup. These tests do not establish multi-peer
replication or visual correctness.

Host/Client validation remains required for replicated contents and
availability, real client interaction, two clients competing for one stack,
out-of-range rejection across peers, disconnect cleanup, a second session in
the same process, and the final visual transition and pose.

Automated coverage targets initialization rules, registry atomicity, transaction order, queue/idempotency semantics and prefab composition. Host/Client placement, range, capacity, competition, availability, feedback and session cleanup still require the manual development harness flow.

## US-13 first acquisition and container opening

`NetworkLootContainerInteractable` owns the networked one-shot first-open state because it is the authoritative interaction boundary. On the first valid interaction it captures whether the container currently has loot and then marks the open as resolved before contributing. A non-empty chest may contribute its configured reward once globally; an empty first open permanently consumes the opportunity. Later interactions still open the UI. Enemy and player corpse interactables have a zero first-open reward.

`NetworkLootContainer` additionally owns a replicated eligible quantity per catalog index. Natural initial chest and enemy content begins fully eligible; exact content loaded from a defeated player and player deposits add no eligibility. A stack may mix both classes. Eligibility is always positive when stored, never exceeds total quantity and never exists without the total stack. `CommitExtraction` is the single origin commit and atomically consumes eligible units first, changes total and eligible quantities, removes empty entries and advances `LootChangeSequence`.

The unchanged `LootTransferRequest` carries no provenance. After source validation, `LootTransferTransaction` performs the side-effect-free `ILootFirstAcquisitionSource` query, validates `0 <= EligibleAmount <= RequestedAmount`, validates reception, then executes the existing mandatory extraction and reception commits. A successful result exposes a separate immutable `LootFirstAcquisitionResult`; every rejection exposes zero. Out-of-range provenance is an integration violation. There is no second provenance commit, yield, callback or general transaction rollback. Existing provisional initialization, corpse-loading, pickup-spawn and drop-publication flows compensate total and eligibility together when they already support compensation.

`PlayerLootTransferNetworkController` contributes only after both commits when the destination is its own `PlayerLootReceiver` and the eligible amount is positive. Deposits, other destinations, rejected transfers and credited-only units contribute zero. The definition's authoritative `ExtractionValuePerUnit` is multiplied by eligible units using `long`; the individual receiver performs final quota saturation. `PlayerLootReceiver` stores no provenance.

Economy is independent: `ExtractionValuePerUnit` measures quota contribution and `SellValuePerUnit` measures money. Inventory total-value calculations and economic presentation use only the sell value.
