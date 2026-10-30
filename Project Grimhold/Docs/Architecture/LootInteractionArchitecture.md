# Loot Interaction and Transfer Architecture

## Context and decision

Loot movement and aggregate snapshot/persistence contracts use `LootEntry` and `LootId` as the domain identity. Player Inventory stores physical `LootInventorySlot` values, while containers retain aggregate stacks. `LootDefinitionCatalog` assigns deterministic indices by ordinal ID order. Fusion transports and replicates catalog indices and quantities; every peer resolves static names, icons, rarity and value locally.

The architecture composes a reusable synchronized source with an authoritative transfer adapter, a separate `IInteractable` adapter and local presentation for selective looting. It supports single-unit and full-stack intentions in both directions, plus ordered full-stack withdrawals performed locally without adding a bulk RPC, transaction or networked batch state.

## Components and responsibilities

- `NetworkLootContainer` owns replicated container contents, initialization, runtime availability, change sequence and the registry mapping for its loot capabilities and colliders. It implements `ILootExtractor`, `ILootQuantityReader`, `ILootContentReader` and `ILootSlotCapacityReader`, but not `ILootReceiver` or `IInteractable`.
- `NetworkLootContainerInteractable` shares the container root and `NetworkObject`, derives the same `EntityId`, and owns only its independently registered `IInteractable` capability. It never registers colliders or changes loot state.
- `PlayerLootReceiver` remains the only temporary player inventory. It owns quantity and origin together in exactly thirty replicated physical slots. Its validators own expected gameplay rejection; its joint commits apply a completely prevalidated request.
- `LootTransferTransaction` performs source validation, destination validation and synchronous commits. When both endpoints compose Raid provenance it also resolves the logical origin payload, prevalidates the destination and invokes joint quantity-plus-provenance commits. It performs no entity resolution, catalog lookup, range calculation or presentation.
- `PlayerLootTransferNetworkController` is the Fusion integration boundary on the player object. Input Authority sends a quantity mode and, for addressed Inventory actions, the source slot and observed Inventory revision; State Authority derives requester, destination, exact quantity, range and tick, then executes the transaction.
- `EntityRegistry` remains runner-scoped. A grouped loot-source registration atomically publishes extractor, quantity reader and associated colliders. Independent interactable registration composes with that source in either lifecycle order and removes only the expected owner.
- `LootContainerTransferDebugHarness` is separate development tooling. It is not attached to production player prefabs or scenes.

## Sources of truth and network authority

### Player physical Inventory

`PlayerLootReceiver` replicates `NetworkArray<LootInventorySlot>` with fixed capacity 30 and `LootChangeSequence`. Each slot stores one catalog index, positive quantity and exact `Dungeon` or `Player(RaidParticipantId)` origin. The default slot is empty; catalog index zero with positive quantity is a valid item. Slots hold at most the definition's `StackMax` (one for nonstackables), while `MaxTransportable` applies to the item's sum across every slot and origin. `OccupiedSlotCount` counts physical slots and `DistinctLootCount` counts item identities.

Reception fills compatible partial stacks first, then holes, in ascending address order. Different participant IDs never share a slot. Legacy unaddressed extraction resolves origin quantities using the requested selection order, then consumes matching physical slots in address order without compaction. Addressed extraction instead consumes only the selected physical slot and its exact origin, including when an earlier stack has the same item and origin. Ordinary quantity-only reception is a necessary optimistic capacity check because the request has no origin. The origin-aware validator plans the entire payload, including all buckets' competition for holes, before either endpoint commits. Equipment exchanges similarly preflight outgoing removal and all displaced equipment origins together; equipping directly from a container frees no Inventory space.

Validation, initialization, replacement sync and joint commits use bounded stack-local plans. Failed operations write no network quantity, origin or revision. Successful requests advance the receiver revision once, not once per bucket or physical stack. Inventory has no parallel replicated or cached provenance store. `Spawned()` does not initialize, compact or overwrite slots, so Fusion `CopyStateFrom` retains their exact addresses and holes.

Production player configuration remains 30. The retained lower serialized capacity used by tests/hub configuration limits the number of occupied slots without resizing the NetworkArray or restricting addresses. Lowering it never discards existing slots; compatible partials can still be filled, extraction remains possible, and additional holes cannot be occupied above the configured limit.

`TryGetInventorySlot` exposes a read-only physical address query on every peer. Existing `IInventoryReadSource`, Quick Slot quantity queries, expedition/extraction/corpse snapshots and backend persistence remain detached item-aggregate projections. Aggregate snapshots preserve canonical catalog/origin order and never persist slot positions. Direct physical-slot UI remains outside this work unit.

### Addressed Inventory actions

`LootTransferRequest`, transfer/drop request identities and additive public transfer, drop and equip overloads carry `SourceInventorySlotIndex` and `ExpectedInventoryRevision`. Existing constructors, candidates and methods remain unaddressed with the pair `-1/-1`. Mixed sentinels and addresses outside `0..29` are invalid. Revisions are opaque comparison tokens; `-1` is reserved for the unaddressed sentinel. Neither field is durable persistence or an origin payload.

`ResolveInventorySlotExtraction` validates the authoritative source and observed revision before building a request. `SingleUnit` means one unit; addressed `FullStack` means the selected slot's quantity, never the item's aggregate. Source validation, provenance resolution, scratch planning and commit honor the same address, catalog identity and revision. Stale revisions, empty/invalid addresses, catalog mismatches and insufficient selected quantities reject without canonical fallback, retargeting or automatic retry. Incoming receivers validate metadata shape but never compare a source revision against their own revision.

The existing primitive request RPCs and bounded queues retain both fields. Transfer/drop equality, hashing and same-sequence replay conflicts include them. An addressed transfer source must be the authenticated actor's Inventory; container withdrawals cannot carry Inventory source metadata. Existing actor authentication, availability, range, quantity and deduplication guards remain in force. Equipment queues also retain the observed revision until tick processing, so a change after local submission still rejects. Equipment exchange preflight removes the selected physical unit on scratch storage before testing all displaced items together; it cannot free a different same-origin stack to manufacture capacity.

Destination reception remains authoritative fill-compatible-partials-then-holes insertion. Container withdrawal, Take All, container-equipping, unequip and legacy item-identity actions remain unaddressed. Quick Slot/consumption remains item-identity PlayerFirst. The later Raid UI must bind physical addresses and capture their revision for addressed actions; disabling Raid sorting is approved for that UI integration only. Town sorting is unchanged. This implementation does not edit UI or serialized assets.

Addressed automated coverage includes exact-stack deposit/drop, selected equipment origin, stale queued equipment requests, source/destination atomicity, malformed metadata and exact-slot exchange capacity. After observed missing-API compile-RED, Unity compiled the extension; the four request/transaction EditMode fixtures passed 46/46 cases and five new equipment cases passed with strict log checking. The receiver PlayMode job completed all 24 cases with terminal status `succeeded` and no recorded failures; its result payload was unavailable after reconnection, so no skipped count is inferred. These focused results do not establish that all broader regressions are green. Single-runner authority-processor tests do not prove multi-peer RPC delivery. Existing revive interruption on incompatible intentions remains unchanged; rejection atomicity here concerns Inventory, Equipment and world-loot state.

### Container state

The container replicates only:

```text
NetworkDictionary<catalog index, quantity>
IsInitialized
IsAvailable
LootChangeSequence
LootSourceGenerationState
RandomGenerationSeedBits
AdditionalLootChanceBasisPoints
```

`IsEmpty` and occupied slots are derived. Initial configuration is fully validated before any stack is written and is then loaded in catalog-index order. State Authority alone initializes content and writes availability. A container with no requested override uses its serialized `LootContainerInitialEntry` values; an authoritative pre-spawn override uses its materialized `LootEntry` values; a rejected override fails closed and never falls back to manual content. Every peer registers the source and colliders locally for runner-scoped discovery, including before its replicated snapshot observes initialization. Proxy registration grants neither extraction authority nor mutation access; authoritative validation and commits still require State Authority. If grouped registration fails on State Authority, no partial registry entries remain and availability stays false.

Generated chests use `LootContainerContentTable` as stable configuration. During authoritative spawning, `NetworkSpawnManager` validates the table and reserves capacity for one additional distinct stack, derives the deterministic seed, and calculates the effective additional-Loot probability from the frozen initial Raid cohort. Solo uses its only participant; a group averages each participant's derived probability in basis points. This descriptor is applied in Fusion's `OnBeforeSpawned` callback and replicated as seed, effective probability and `Pending` state. It is never recalculated after defeat, abandonment, disconnection or Host Migration.

The first valid chest interaction consumes `Pending` before attempting any generation. The base selection uses the existing SplitMix64 seed and therefore remains identical for the same seed with or without Luck. An independent deterministic sequence evaluates Luck and, on success, selects at most one additional stack from the same table without replacing a base identity; its amount uses the table's normal range. The resulting content, full first-acquisition eligibility and Dungeon origin are committed as one initialization operation before the state becomes `Resolved`. Any validation, roll or commit failure leaves the container empty and sets `Failed`. Both `Resolved` and `Failed` are terminal: reopening, deposits, transfers and Host Migration never evaluate the descriptor again. A failed chest remains available and interactable as an empty container.

`SetAvailability(bool)` requires State Authority. Enabling requires successful initialization and registration. Repeating a value is idempotent. Availability changes neither contents, registration, despawn state nor `LootChangeSequence`. `ValidateExtraction` returns `ContainerUnavailable` when the source cannot participate.

## Prevalidation and commit invariant

Expected failures, including missing authority, invalid loot or amount, insufficient quantity, capacity, overflow, unavailable containers, and extracted players (`PlayerUnavailable`), are returned by endpoint validators. A Raid-aware endpoint also validates its origin payload before mutation. After every applicable validator returns `None`, the two endpoints must apply the exact request; each Raid-aware endpoint exposes one joint commit so callers cannot commit quantity while omitting provenance.

Commits do not return rejections and do not silently skip mutation. Defensive structural checks diagnose an impossible integration state with a contextual error and exception. Because each commit verifies its structural contract before changing its own collection and the transaction runs synchronously without yielding or callbacks, a violated contract stops execution instead of allowing the destination commit to continue after an omitted extraction.

## Transport and local request lifecycle

Domain structs are never sent as RPC parameters. The request RPC contains only:

```text
source EntityId value
destination EntityId value
catalog index
quantity mode
request sequence
source Inventory slot index (-1 for legacy)
expected source Inventory revision (-1 for legacy)
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

Fusion simulation processes authoritative requests serially. A single-unit intention resolves to one. An unaddressed full-stack intention resolves from the current authoritative aggregate source amount; an addressed one resolves from the selected slot only after its revision and identity match. Each exact resolved request commits atomically before a later competing player validates, so the later request observes the remaining amount or an insufficient source rather than duplicating loot.

## Pickup presentation boundary

`CommitReceive` is generic and emits no pickup toast. `ILootPickupFeedbackSink` is an optional integration boundary implemented by `PlayerLootReceiver`. `NetworkLootPickup` invokes it only after a successful pickup commit. Its presentation RPC uses primitive values and reconstructs `LootGrantPresentationEvent` locally. Container transfers never consult this sink.

`NetworkLootPickup` also supports authoritative pre-spawn initialization for
world drops. It replicates catalog index and quantity, resolves the shared
`LootDefinition` and world sprite locally, and otherwise uses the same registry,
interaction, transfer, feedback and despawn flow. Breakables release these
pickups directly and never expose a container interaction; see
`Docs/Architecture/BreakableLootArchitecture.md`.

Pickup reception is automatically partial (Doc 07 sections 11 and 14). State
Authority first validates the whole replicated stack. Only a capacity failure
(`InventoryFull`, `MaxTransportableExceeded` or `Overflow`) triggers the partial
path; every other rejection keeps the pickup unchanged. The admissible amount is
the largest canonical prefix of the pickup's origins, taken Dungeon first and
then by participant value through `RaidLootOriginTransfer.TrySplit`, that both
side-effect-free receiver validators accept. Prefixes are nested, so the bounded
binary search over that amount is exact. Zero admissible units reject with the
one-unit failure. A partial success commits only the taken buckets, consumes
first-acquisition eligibility eligible-first through
`LootFirstAcquisitionRules`, and contributes Progress and treasure instability
only for consumed eligible units. Pickup feedback reports the transferred
amount. The pickup then writes the remaining quantity, origin state and
eligibility, and stays published with the same `NetworkObject`. The result is
`Succeeded(isConsumed: false)`. The remainder's origin state is encoded before
the receiver commit, so a provenance failure cannot duplicate units. A complete
transfer still despawns the pickup. Authoritative interactions run serially, so
a later competitor revalidates against the remaining amount.

Single-runner PlayMode coverage
(`NetworkLootPickupPartialPickupPlayModeTests`) covers the complete, partial,
zero-capacity, mixed-origin remainder and sequential-competitor cases. Replication
of the reduced quantity to observing clients still requires Host/Client
validation.
World rendering remains prefab-owned rather than definition-owned: the generic
pickup exposes a sorting layer and order, then applies both whenever it resolves
a `LootDefinition.WorldSprite`. Dynamic loot therefore shares one configurable
map-rendering policy without requiring a prefab per loot definition.

## Prefabs and development validation

`NetworkPlayer.prefab` contains `PlayerLootTransferNetworkController` and no debug component. `LootContainer.prefab` contains one `NetworkObject`, `NetworkLootContainer`, `NetworkLootContainerInteractable`, enabled `LootContainerRandomContentConfig`, local prompt metadata, a layer-8 collider and its provisional visual. Its serialized manual stacks remain an empty development fallback; the production spawn path requires a valid random table.

Gameplay's authoritative `NetworkSpawnManager` consumes only the `SpawnGroupType.Loot` scene group and spawns `LootContainer.prefab` at ordered, unique points without Input Authority. It owns one local session seed and derives each chest seed from session, scene-load generation and point index. It validates the compatible table and descriptor before calling Fusion, applies that descriptor in `OnBeforeSpawned`, and records the point only after callback application, initialization, availability and `Pending` are confirmed. A failed returned object is despawned immediately and does not consume the point, so a retry uses the same deterministic descriptor without leaving an orphan. Once spawned, resolution belongs to the container and is consumed exactly once. The spawn generation remains runner-scoped and idempotent; requests beyond available points are clamped rather than overlapped. Unsupported NPC, boss and miscellaneous groups are skipped and never receive an enemy fallback.

Defeated enemies remain the same network entity instead of spawning a replacement corpse. `NetworkEnemy.prefab` composes the shared `NetworkLootContainer` and `NetworkLootContainerInteractable` on its root `NetworkObject`, with an Interactable-layer trigger used only for loot queries. The container initializes with the enemy but starts unavailable. When authoritative damage reaches zero, `EnemyCharacter` disables movement and combat and enables that existing container during simulation. The enemy therefore preserves its `NetworkId`, position, colliders and replicated contents across the alive-to-defeated transition. Its defeat presentation keeps the body visible and may later settle into a death-animation pose; presentation state never owns or changes loot. No separate corpse prefab, automatic replacement spawn or dead-entity inventory copy exists.

Players keep the same `NetworkPlayer` identity after defeat. The prefab composes `NetworkLootContainer`, `NetworkLootContainerInteractable`, prompt metadata and a dedicated layer-8 trigger alongside `PlayerLootReceiver`; the container begins empty and unavailable, and its receiver capability cannot accept transfers until the authoritative defeat handoff makes it available. `CharacterBase.ApplyDamage` reaches `PlayerCharacter.HandleDeath`, which invokes `PlayerCorpseGenerationController` in the same authoritative simulation flow. Its networked one-shot state (`Waiting`, `Processing`, `Completed`, `Failed`) captures the complete expedition ownership snapshot: normal inventory plus all eight Equipment slots — both hands of Set A and Set B, Helmet, Armor, Gloves and Boots — aggregated by `LootId`. An equipped piece is still expedition property, so armor is lost on defeat and kept on extraction exactly like a weapon. `PlayerExpeditionLootSnapshot` exposes each slot as its own named property rather than a positional array, so no caller can mismatch them. It validates and loads the co-located unavailable container, verifies Inventory and every Equipment slot exactly, clears all ownership locations exactly once, and only then enables the container. Extraction uses the same aggregation before committing to the local stash and clears those sources only after its existing ACK. A load or clear failure keeps the container unavailable, rolls it back to empty when necessary, preserves player ownership, enters `Failed`, and never retries. No player-corpse prefab, secondary `NetworkObject`, generated Fusion prefab registration, appearance copy, or alternate transfer route exists. `PlayerDefeatPresenter` retains the defeated player's own final pose independently of this gameplay transition; presentation has no authority over content, availability, or interaction.

The defeated player then uses the same interaction and transfer route as every
other container. There is no corpse-specific RPC, transaction, controller,
registry capability, or presentation mode. Removing the last stack increments
the existing change sequence but does not make the container unavailable or
despawn the player.

`Assets/Prefabs/Debug/LootContainerTransferDebugHarness.prefab` can be placed manually in a graybox. In Editor or Development Build it resolves the local player through `TryGetPlayerObject`, detects nearby containers directly from colliders, reads their snapshot and invokes the public or raw debug transport methods. F8 sends a public full-stack request; F9 repeats its exact envelope; F10 reuses its sequence with a conflicting catalog index; F11 sends a different sequence while the legitimate request is in flight; and F12 queues an availability toggle that only succeeds on the peer holding State Authority and is applied by the container in `FixedUpdateNetwork`. Press F8 together with F9, F10 or F11 to guarantee both envelopes arrive before the next simulation tick. In a non-development release the class remains loadable but disables itself and performs no input or searches.

## Risks and validation

Catalogs must be identical across peers because transport indices are catalog-local. Invalid success envelopes are rejected at the transport boundary. Missing/disabled random configuration, invalid tables, weight overflow and capacity without headroom for the additional stack skip only the Loot group. A descriptor callback or initial container initialization failure triggers authoritative compensating despawn. A failure after the first valid interaction is instead a terminal `Failed` resolution and preserves an empty interactable container. Registry conflicts leave containers unavailable and therefore invalidate the production spawn instead of leaving an orphan.

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

## First acquisition and container opening

`NetworkLootContainerInteractable` owns the networked one-shot first-open Progress state because it is the authoritative interaction boundary. On the first valid interaction it captures whether the container currently has loot and then marks the Progress open as resolved before contributing. A non-empty chest may contribute its configured Progress reward once globally; an empty first open permanently consumes that opportunity. Later interactions still open the UI. Enemy and player corpse interactables have a zero first-open Progress reward.

An independent configurable first-open Exploration Experience reward has its
own replicated resolved flag and stable owner `ProfileId`. Every base-valid interaction may
evaluate this XP branch even after the Progress one-shot has resolved. The first current Raid
participant with a valid avatar claims ownership before its co-located ledger is resolved.
Missing ledger or rejection leaves that owner and reward pending exclusively for later retry
by the same participant; acceptance marks the XP one-shot resolved immediately. XP does not
depend on current contents, so an empty chest may grant it. XP resolution and rejection never
change the interaction result or existing Progress branch, and content transfer never changes
the owner or either one-shot. Player and enemy corpse interactables configure zero XP.

Fusion snapshots and `CopyStateFrom` preserve the XP owner and both resolved flags. Fresh
State Authority spawns clear the XP owner and flag; Host Migration restore spawns retain the
copied state without a parallel persistence or remapping path because ownership uses the
participant's stable `NetworkString<_32>` `ProfileId` rather than a `NetworkId`.

`NetworkLootContainer` additionally owns a replicated first-acquisition-eligible quantity per catalog index. Natural initial chest and enemy content begins fully eligible; exact content loaded from a defeated player and player deposits add no eligibility. A stack may mix both classes. Eligibility is always positive when stored, never exceeds total quantity and never exists without the total stack.

`LootTransferRequest` carries optional source-address metadata but no provenance payload. After source validation, `LootTransferTransaction` performs the side-effect-free `ILootFirstAcquisitionSource` query, validates `0 <= EligibleAmount <= RequestedAmount`, validates reception, then executes the existing mandatory extraction and reception commits. A successful result exposes a separate immutable `LootFirstAcquisitionResult`; every rejection exposes zero. Out-of-range provenance is an integration violation. There is no second provenance commit, yield, callback or general transaction rollback. Existing provisional initialization, corpse-loading, pickup-spawn and drop-publication flows compensate total and eligibility together when they already support compensation.

`PlayerLootTransferNetworkController` contributes only after both commits when the destination is its own `PlayerLootReceiver` and the eligible amount is positive. Deposits, other destinations, rejected transfers and credited-only units contribute zero. The definition's authoritative `ExtractionValuePerUnit` is multiplied by eligible units using `long`; the individual receiver performs final quota saturation. `PlayerLootReceiver` stores no first-acquisition eligibility. Raid ownership provenance is a separate contract described below.

Economy and progression Value are independent: `ExtractionValuePerUnit` measures quota contribution,
`SellValuePerUnit` measures money, and `IRaidLootValueSource` provides the provisional positive Value
used only to calculate extracted-Loot Experience. Inventory total-value calculations and economic
presentation use only the sell value. The current local progression catalog assigns replaceable
placeholder values to every productive `LootId`; it does not define final balance or economy.

## Raid loot origin

Raid ownership provenance is quantified per `LootId` as `Dungeon` or `Player(RaidParticipantId)` and exists only for the current Raid. `LootEntry`, first-acquisition eligibility, economic value and persistent stash/loadout contracts remain unchanged. `LootTransferRequest` adds only ephemeral source-address metadata; logical origin buckets remain separate. Natural chests, enemies, breakables and pickups initialize Dungeon provenance; admitted loadout units initialize Player provenance from the participant's authority-assigned Raid identity.

`RaidParticipantId` is a Raid-scoped value in `1..16`; zero is invalid and reserved as the physical Dungeon origin. State Authority derives it deterministically from ordinal `ProfileId` order in the immutable frozen admission cohort, writes it once to `NetworkRaidParticipant`, and thereafter restores that value through Fusion state. Host Migration reconnect resolves the participant by its separately replicated `ProfileId`, then reuses the restored `RaidParticipantId`; it never reassigns it from `PlayerRef`, `NetworkId` or arrival order. Ordinary mid-Raid reconnect remains outside the current session contract, but deterministic re-resolution against the same frozen cohort returns the same ID. This adds no roster or process-local identity source and does not narrow the repository-wide `ProfileId` domain.

Buckets store catalog index, the logical `RaidParticipantId` itself in five bits, and positive quantity. There is no endpoint-local identity table: Dungeon is `0` and Player origins are their stable values `1..16`. Payloads therefore carry logical Raid identities directly, sort Dungeon first and then by participant value (the bucket order of every `RaidLootOriginTransfer`), and remain independent of holder, endpoint layout, `ProfileId`, `PlayerRef`, `NetworkId` and process-local caches. Fusion snapshots and `CopyStateFrom` copy bucket identities and quantities without reconstruction or fixup.

`PlayerRaidLootOriginState` belongs only to Raid `NetworkPlayer` and replicates eight Equipment origin references, not Inventory provenance. Its retained Inventory read methods delegate to `PlayerLootReceiver` slots. Inventory-only origin mutation APIs were removed; receiver joint commits cannot accidentally apply an Inventory change twice through a provenance facade. `ContainerRaidLootOriginState` remains packed and belongs to `LootContainer`, `NetworkEnemy`, and the co-located defeated-player container. `NetworkLootPickup` retains its full seventeen-origin representation for its single stack. World drops resolve and copy exactly the extracted buckets while retaining first-acquisition eligibility zero. Equipment moves one resolved unit between Inventory and its target slot. Corpse conversion and exact clear validate, load, compensate and clear quantity and provenance together.

`RaidInitialAffiliationSnapshot` projects the immutable launch participants into the same `RaidParticipantId` domain by calling the canonical `RaidParticipantIdAssignment` contract. It does not reorder profiles or derive a parallel identity mapping. `RaidLootEligibilityResolver` combines that affiliation with a complete `PlayerExpeditionLootSnapshot`: Dungeon quantities and quantities introduced by a different initial team are eligible, while the extractor's own quantities and those introduced by its initial teammates are not. The result is an immutable per-`LootId` quantity projection; no Loot bucket stores mutable eligibility.

The catalog index is seven bits and supports up to 128 catalog definitions. Player Inventory accepts thirty physical stacks, not thirty aggregate item types with unlimited origin subdivisions. World containers and enemy loot containers retain their six-distinct-item gameplay capacity and packed provenance. The unavailable container co-located on `NetworkPlayer` remains the deliberate exception: it accepts thirty aggregate item types to preserve complete expedition ownership on defeat, and is the only productive prefab enabling `_supportsFullPlayerInventory`. Container dictionaries retain the technical ceiling of thirty, and their packed provenance still reserves `30 LootIds * (Dungeon + 16 Players) = 510` quantities. Inventory instead represents only the origins and quantities actually occupying its thirty slots; pickups retain seventeen origins and Equipment retains eight origin references.

### Woven sizing and validation status

Unity/Fusion measurements after the storage migration reported **61 words for `PlayerLootReceiver`, 2 for `PlayerRaidLootOriginState`, and 1557 for the complete player**, excluding the invariant 21-word `NetworkTransform`/TRSP contribution. That is 327 fewer words than the historical all-packed 1884-word player layout. The co-located corpse container remains unchanged. `RaidLootOriginWordCountTests.ProductivePrefab_UsesMeasuredCompactBudget` retains the approved 1884-word ceiling and requires reduction without guessing a new exact total. These measurements precede the later addressed-action extension and must be rechecked when replicated layout changes.

The same regression run failed the existing endpoint budgets: `LootContainer` measured 1159 words against expected 983 (ceiling 1025), `NetworkEnemy` 1219 against 1007 (ceiling 1055), and `NetworkLootPickup` 23 against 22 (ceiling 192). These expectations and ceilings were not raised to make the tests pass. Their discrepancy remains unresolved; no baseline Unity execution is claimed. Measurements use baked gameplay behaviours and exclude Fusion's invariant 21-word `NetworkTransform`/TRSP contribution. This migration does not resize packed container or pickup state.

Focused receiver PlayMode coverage includes stack partitioning, exact-origin isolation, physical capacity, competing multi-origin transfers, aggregate transport limits, atomic admission, detached snapshots, exact clear and PlayerFirst consumption selection. Behavior-state copy coverage compares all thirty slot values, including a leading hole and partial stack, before and after copy and receiver `Spawned()` replay. This is not proof of real multi-process Host Migration.

### Consumption selection order

Quantity resolution from a mixed-origin stack has a selection order separate from the canonical bucket order. Legacy unaddressed drop, transfer and equip select Dungeon first by default. Addressed actions use only their selected slot's origin and do not apply an aggregate selection order. Authoritative consumption (`PlayerConsumableNetworkController.ProcessAuthoritativeConsume`, whether requested from the Inventory or a Quick Slot) uses `RaidLootOriginSelectionOrder.PlayerFirst`: Player origins by participant value are spent before Dungeon units, as the Game Design requires. The selected buckets are re-sorted canonically before the transfer is built, so `RaidLootOriginTransfer` validation and `TryRemove` are unchanged. The order is evaluated only on State Authority.
