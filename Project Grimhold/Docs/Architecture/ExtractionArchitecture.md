# Extraction Architecture

## Context and status

The authoritative extraction core owns zones, the player extraction process and terminal gameplay restrictions. The local HUD and replicated player visuals project only its confirmed state. The core does not despawn the player, create rewards, persist loot, close the session, or depend on UI, animation, audio or VFX.

## Sources of truth

| Concern | Owner | Representation |
| --- | --- | --- |
| Stable balance | `ExtractionConfig` | Immutable `ScriptableObject` values |
| Zone identity | `ExtractionZone` | `EntityId` derived from its `NetworkObject` |
| Zone geometry | `ExtractionZone` | Serialized same-root `Collider2D` |
| Zone availability | `ExtractionZone` | Private `[Networked] NetworkBool`, exposed as `bool` |
| Player process | `PlayerExtractionController` | `[Networked] ExtractionState` |
| Active zone | `PlayerExtractionController` | Networked primitive exposed as `EntityId` |
| Countdown | `PlayerExtractionController` | `[Networked] TickTimer` |
| Death | `ICharacter` | `IsAlive`; extraction never changes it |
| Sanctuary reservation | `ExtractionSanctuary` | Primitive networked owner ID |
| Ritual lifecycle | `ExtractionSanctuary` | `[Networked] ExtractionRitualState` and `TickTimer` |
| Ritual timing | `ExtractionConfig` | Immutable `RitualDurationSeconds` |
| Pending extracted ownership | `NetworkPlayer` Raid loot states | Inventory, Equipment and Raid provenance retained until persistence ACK |

`EntityRegistry` is runner-scoped and stores zone and participant capabilities independently from character, damage, interaction, collider and loot capabilities. Every peer registers its local instances. Registration grants discovery only; it never grants authority to mutate gameplay. Conflicts are rejected and unregistration removes only the expected instance.

## Responsibilities and flow

`ExtractionZone` owns identity, exact geometry, candidate detection and availability. It does not read `ExtractionConfig`, own a timer, write player state, block controls, change inventory, cancel or complete a process, or despawn a player.

`PlayerExtractionController` owns the individual state machine, active zone, timer, cancellation and completion consequences. Its `ValidationPoint` is the authoritative point used by both zone entry and continuation checks.

```text
authoritative movement, damage, interaction, loot, projectiles and AI
    -> ExtractionZone (execution order 100)
    -> PlayerExtractionController (execution order 110)
```

Enemy AI keeps its existing execution orders. Movement and all ordinary gameplay for the tick therefore finish before extraction reads the final authoritative position and state. A zone may request a start before the participant controller runs in the same tick. The controller evaluates zone existence, availability, vitality and geometry before timer expiry, so leaving or dying on the terminal tick cancels when configured.

An operation legitimately processed while the player was still `InProgress` is not rolled back if extraction completes later in that tick. Once `State` is written as `Extracted`, later operations cannot mutate gameplay; from the next tick all owner validations observe the terminal state or the disabled control APIs.

## Geometry and occupancy

Entry always uses `ContainsExact(ValidationPoint)`. Continuation uses `ContainsWithTolerance(ValidationPoint, BoundaryTolerance)` only when `CancelWhenLeavingArea` is enabled. Geometry rejects non-finite points, invalid tolerances, and disabled or incorrectly composed colliders. Exact containment uses the concrete collider and tolerant continuation uses `ClosestPoint`.

The zone runs one reusable non-alloc broadphase buffer filtered to the `Character` layer, resolves collider identities through `EntityRegistry`, deduplicates by `EntityId`, and performs a narrowphase check against each participant's validation point. The local occupant set is auxiliary: it deduplicates candidates and identifies possible exact-volume exits, but it is not authoritative process state.

On a complete scan the zone may request start for every exactly present participant. `TryBeginExtraction` is idempotent through the participant state check. An exact-volume exit only calls `NotifyExtractionZoneExit`; the participant resolves the active zone again and applies the same tolerant continuation policy used by its tick revalidation.

If the broadphase fills its buffer, the scan is incomplete: it creates no starts, infers no exits and does not replace the confirmed occupant set. One diagnostic is emitted per saturation episode. The participant controller still resolves and revalidates its active zone directly every tick, so saturation cannot preserve an invalid process.

An unavailable zone requests no starts and does not modify participants. Active participants observe unavailability through their own tick revalidation and cancel. Re-enabling permits starts on the next complete scan.

## Player process

Start requires State Authority, complete prefab composition, a valid configuration, a valid participant identity and registration, `State == None`, a registered available zone, exact containment, and `IsAlive` when `RequireAliveToStart` is enabled. A second or overlapping zone cannot replace an active process.

While `InProgress`, evaluation order is:

1. Resolve the active zone.
2. Validate availability.
3. Validate `ICharacter.IsAlive` when `CancelWhenNotAlive` is enabled.
4. Validate tolerant continuation when `CancelWhenLeavingArea` is enabled.
5. Evaluate timer expiration.

Cancellation writes `None`, invalidates `ActiveZoneId` and clears the timer. An exit notification from a non-active zone is ignored. Disabling leaving cancellation also makes exact-volume exit notifications harmless, while zone loss and unavailability remain tick-authoritative cancellation causes.

Completion writes `Extracted` first, preserves the completing zone identity, clears the timer and applies movement and attack restrictions through `TrySetControlEnabled(false)` and `TrySetAttackEnabled(false)`. These calls are reapplied idempotently while terminal. Player movement and combat retain only their existing enabled, vitality, cooldown and input rules; they do not depend on extraction types.

`TryGetProgress(out ExtractionCountdownSnapshot)` is side-effect free. The contract was renamed from
`ExtractionProgressSnapshot` when individual quota progress was introduced, to distinguish the zone countdown from the individual quota snapshot:

- `None`: valid, zero time and zero progress.
- `InProgress`: valid only when runner, configuration, running timer and remaining time are available; no value is invented otherwise.
- `Extracted`: valid, zero remaining time, progress one, and the completing zone identity preserved.

Duration, remaining time and percentage are derived and are not networked. Presentation must consume this query and must not run a parallel local clock.

## Terminal gameplay restrictions

`Extracted` is terminal and does not mean dead. `PlayerCharacter.CanReceiveDamage` returns false while extracted, while `ICharacter.IsAlive`, health and inventory remain unchanged.

Interaction, loot transfer and world drop revalidate extraction inside their authoritative owner protocols. Interaction records `InteractorUnavailable`, advances `InteractionSequence`, sends the normal directed confirmation and publishes it during `Render` without querying or invoking an interactable. Transfer and drop return their `PlayerUnavailable` reasons through normal confirmations so pending state and `HasRequestInFlight` are released; Take All follows its existing success/rejection continuation policy.

Enemy AI has no extraction dependency. `EnemyMovementAIController` owns one atomic target reference whose canonical identity is `EntityId` and whose `Transform` is only a cache. Acquisition and maintenance resolve `ICharacter` and `IDamageable` for that identity and require `IsAlive && CanReceiveDamage`. Invalidating an expected identity clears identity, transform, pursuit and attack flags together, allowing the existing FSM to leave `Chase` or `Attack`. `EnemyCombatAIController` repeats the same registry-backed validation before committing an attack and immediately before executing pending damage. Existing projectiles continue simulation, but impact resolution rejects an unavailable damage target.

## Extraction presentation boundary

`RaidHudPresenter` is bound only to the Input Authority player's `PlayerExtractionController`. It reads `TryGetProgress` during presentation updates and renders the confirmed local countdown, a one-shot cancellation message, or the terminal `Extracted` label. It does not start, cancel or complete extraction and does not maintain a gameplay timer. The cancellation message uses an unscaled presentation-only duration.

`PlayerExtractionPresenter` observes the replicated `ExtractionState` on every peer. When the state is `Extracted`, it hides only the serialized `Body` and `CombatVisuals` roots. The NetworkObject, colliders, camera, HUD, interaction and authoritative gameplay components remain active. Disabling the presenter restores its serialized visual state; re-enabling it reapplies the terminal visual from the current confirmed state.

The extraction zone keeps its existing pozo visual and availability tint. This presentation boundary does not add world-space UI, audio, particles, a second extraction clock or another networked state source.

## Lifecycle and validation

Zones clear overlap buffers' logical sets, occupant state and saturation diagnostics during despawn and destroy. Participants and zones unregister only their expected capabilities. Destroying the runner destroys its registry, preventing state from leaking into a later session.

Automated coverage should include configuration and geometry boundaries, progress semantics, registry composition/conflicts, atomic enemy target invalidation, owner-protocol rejection, exact entry versus tolerant continuation, saturation behavior, authority, terminality and lifecycle. Fusion availability, timing, overlapping zones, independent players, despawn and clean-session behavior require runner tests plus the documented Host/Client manual checklist when no automated multi-runner harness proves them.

## Extraction ownership snapshot

`PlayerExpeditionLootSnapshot` captures Inventory, each of the six Equipment slots, their persistent aggregate by `LootId`, Inventory origin buckets, each Equipment origin and the aggregate origin buckets. It validates duplicate buckets, positive quantities, overflow and exact per-LootId equality before exposing a snapshot. The persistent RPC and Loadout commit continue to carry only catalog indices and quantities; Raid provenance never enters stash or backend storage.

When a participant becomes `Extracted`, State Authority's `PlayerExtractionLootSaver` retains the authoritative ownership snapshot while the commit remains unconfirmed. Inventory, Equipment, logical Raid participant origins and every bucket stay in Fusion state until Input Authority ACKs the idempotent persistent commit. The process-local snapshot is only a verified projection. A valid aggregate may contain up to 22 distinct entries: sixteen Inventory types plus six Equipment slots, while every catalog index remains valid in the independent `0..63` range.

On Host Migration restore, Fusion `CopyStateFrom` restores `RaidParticipantId` values, bucket identities and quantities together. `Spawned()` recaptures the pending projection from that restored authoritative state when the participant is still `Extracted` and unconfirmed; it does not rebuild or reassign identities.

Before sending the persistent payload, State Authority also projects initial affiliation, eligibility,
eligible Value and candidate extracted-Loot Experience from the retained snapshot. That process-local
candidate carries the same `ResultSequence` as the pending transaction and is never part of the
Loadout/stash payload. A calculation failure is logged but does not invalidate the Loot transaction.

For a valid ACK, exact-clear first validates and removes Inventory, Equipment and provenance. The
participant extraction commit is then confirmed. Only after confirmation may the matching candidate
be offered once to the provisional Experience ledger. The pending transaction and candidate are
cleared whether that ledger operation succeeds or fails; Progression cannot revert extraction and
callbacks after confirmation cannot retry the reward.

## Individual quota progress

The MVP has individual progress only: the game supports solo expeditions and has no team progress, shared quota, team assignment or team contribution. `PlayerExtractionProgressController` on each player owns replicated `CurrentProgress` and `AssignmentRequested`; the positive quota remains immutable local configuration in `ExtractionConfig` and is not replicated. State Authority is the only writer. Contributions are accepted only during the matching authoritative simulation tick while the player is alive and not `Extracted`, use `long` arithmetic, and saturate at the configured quota. Reaching the quota changes `AssignmentRequested` once and it remains true for that player for the rest of the expedition. A new network player instance starts at zero with no pending assignment.

`ExtractionProgressSnapshot` is the immutable quota projection: current progress, quota, percentage, completion and pending assignment. The earlier zone countdown contract was renamed to `ExtractionCountdownSnapshot`; this public rename prevents two unrelated meanings from sharing the same type name.

`EntityRegistry` stores `IExtractionProgressReceiver` and `IExtractionProgressDefeatSource` in independent runner-scoped maps keyed by `EntityId`. These capabilities do not participate in collider registration or `TryRegisterEntity`. Producers call the receiver directly under State Authority; there is no global coordinator, gameplay event, RPC or event bus. `SimulationTick` is authoritative metadata and context validation, never a deduplication key. Multiple legitimate contributions may share one tick. One-shot behavior belongs to each producer: fatal `DamageResult`, first-open network state, provenance consumption during extraction, or pickup reservation/consumption.

## Individual sanctuary assignment

The pre-ritual flow is `quota -> request -> assignment -> reservation`. When State Authority first completes a player's quota, `PlayerExtractionProgressController` writes the final progress, sets the historical `AssignmentRequested` flag, and invokes the runner-local `ExtractionSanctuaryAssignmentService` during the same simulation tick. Failure to assign never rolls back progress or the request and is not retried automatically on later ticks.

Each `ExtractionSanctuary` owns the only authoritative reservation value: one primitive networked owner ID, where zero means unreserved. `OwnerId`, `IsReserved`, and ownership checks are derived from that value. There is no replicated assignment on the player and no local player-to-sanctuary cache. Both Host and Client derive the inverse relation by scanning stable ordered sanctuary IDs and resolving current capabilities through the runner-scoped `EntityRegistry`. More than one sanctuary owned by the same player is reported as invalid state instead of returning the first match.

The registry stores `IExtractionProgressReader` separately from the contribution-only `IExtractionProgressReceiver`, and stores `IExtractionSanctuary` independently from zones, participants, colliders, and other capabilities. Player reader/receiver registration and sanctuary registry/service registration are compensated workflows. Sanctuary cleanup runs in reverse order so the service can verify the expected registry instance before removing the identity.

The assignment service is created on the `NetworkRunner` before `StartGame` and initialized for one runner and requested mode. Host initialization generates one local `ulong` seed through `System.Security.Cryptography.RandomNumberGenerator`; zero is valid and the seed is never replicated. Client instances are query-only and hold no seed. The service retains only sorted sanctuary identities and reusable candidate buffers. Host simulation validates the progress reader, character vitality, participant state, and current registrations before selecting. The pure policy mixes the seed, authoritative tick, player identity, and candidate count, then indexes the already sorted free set.

During resimulation, the same inputs and free set select the same candidate. An existing reservation is returned idempotently before mutable eligibility is reconsidered, and `ExtractionSanctuary.TryReserve` performs the only final mutation while repeating the State Authority check. Destroying the runner clears the service seed, identities, buffers, and diagnostics. Sanctuary despawn removes both registrations, so a new session starts from newly spawned owner values of zero.

## Authoritative individual ritual

Each `ExtractionSanctuary` composes one `ExtractionZone` on the same root `NetworkObject` and therefore shares one canonical `EntityId`. The Sanctuary is the single extraction interactable: it owns reservation, ritual state, ritual timer and the interaction that starts the ritual. The co-located Zone is only the Sanctuary's physical interaction/extraction area; after the ritual completes it validates geometry, availability and occupancy for the final countdown, but it is never a second interactable or destination. The registry keeps `IExtractionSanctuary`, `IInteractable`, collider identity and `IExtractionZone` as distinct capabilities under that shared identity; no player-side assignment or second zone identity is synchronized.

`Gameplay` contains four scene NetworkObjects rather than runtime-randomized Sanctuary spawns. Their positions are authored from valid cell centers of the scene's `Floor` tilemap, and the serialized-scene test requires every tile covered by each zone collider to exist on that tilemap. This keeps the endpoints reachable when the dungeon layout or floor transform offsets differ from raw world-space quadrants.

Registration is compensated in the order Sanctuary capability, assignment service, then entity/interactable/collider. Sanctuary cleanup reverses that order. Zone cleanup remains independent. Both Sanctuary and Zone capability removal use the registry's expected-instance independent cleanup, so collider mappings remain while any co-located capability is registered and disappear when the last capability leaves, regardless of callback order.

`CanInteract` is a side-effect-free query used by authoritative and predictive selection. It consumes only replicated reservation/ritual state plus the runner-local character capability, so only the living owner sees the ritual candidate. `Interact` repeats those validations under State Authority, starts one `TickTimer`, and never emits presentation effects from simulation.

Simulation order is authoritative gameplay and damage, Sanctuary ritual at order 90, Zone scan at 100, then player extraction continuation at 110. An in-progress ritual validates composition and owner identity, resolves the owner, checks vitality, and only then checks expiry. Death on the expiry tick therefore cancels. `Cancelled` and `Completed` are terminal. Completion writes `Completed` before enabling the co-located Zone and subsequent authoritative ticks idempotently reaffirm availability; while the owner remains inside, that area then starts the final countdown through the existing authoritative zone flow. The only interaction target is the Sanctuary.

The Zone rejects attempts to disable itself after the co-located registered Sanctuary reaches `Completed`. Availability alone is not extraction authorization: `PlayerExtractionController` resolves the Sanctuary with the same zone ID and requires the participant to be its owner with a completed ritual both when starting and continuing.

`ExtractionRitualSnapshot` derives total duration, remaining time and percentage without a parallel clock. `NotStarted` and `Cancelled` report the configured duration remaining with zero progress; `InProgress` derives values from `TickTimer`; `Completed` reports zero remaining and full progress. The HUD and world presentation consume this projection, as does the local private minimap marker.

## Ritual presentation boundary

The ritual presentation is intentionally partial. The local HUD consumes the nullable
`PlayerExtractionProgressController`, runner-scoped `ExtractionSanctuaryAssignmentService`,
runner-scoped `EntityRegistry`, `IExtractionSanctuary`, `ExtractionProgressSnapshot`,
`ExtractionRitualSnapshot` and the existing `ExtractionCountdownSnapshot`. Each source is
evaluated independently: a missing assignment service or sanctuary does not hide valid
progress, and a missing progress source does not disable the rest of the HUD.

The extraction text has one explicit priority: terminal `Extracted`, active extraction
countdown, existing cancellation feedback, completed ritual, in-progress ritual, cancelled
ritual, quota confirmation, assigned Sanctuary, completed quota without a resolvable
assignment, individual progress, and finally the unavailable placeholder. Progress and
countdown maintain separate observation baselines. The first valid snapshot and the first
snapshot after an invalid interval establish a baseline; only an observed incomplete-to-
complete quota transition produces the local, unscaled confirmation feedback.

`ExtractionZone` is simulation-only and is the interaction area of the Sanctuary.
`ExtractionSanctuaryPresenter` is the only renderer owner for the Sanctuary visual. It resolves the current local PlayerObject on every presentation
update from `Runner.LocalPlayer` and never retains a private player identity. Invalid or
replaced runner/player context immediately falls back to the public visual state.

The Sanctuary presenter exposes only public ritual progress to rivals: an unreserved/base
state, a global `InProgress` pulse, or a permanent `Completed` state. Reserved and cancelled
tints are private to the currently resolved local owner. The pulse uses unscaled time only as
an aesthetic phase and never represents ritual duration or progress. All presenters preserve
the alpha authored on each renderer; state changes modify RGB only and never alter zone
transparency. Presentation components
write no simulation state and add no replicated properties, RPCs or gameplay timers.

The local minimap presentation is described below. Spatial audio remains outside
the extraction presentation boundary.

## Local minimap presentation

`RaidMinimapPresenter` is an independent presentation section bound by `LocalPlayerHudBinder`
to the current Input Authority `NetworkObject`, `Transform`, `PlayerExtractionController`,
runner-local assignment service and `EntityRegistry`. Missing assignment infrastructure affects
only the private Sanctuary marker and never disables the remaining HUD.

`RaidMinimapView` is composed on the existing `LocalGameplayHud` Canvas. Its north-up
`RaidMinimapGraphic` renders the immutable `MinimapLayout` generated from the serialized `Floor`,
`Walls` and `Obstacles` Tilemaps in `Dungeon_Graybox.prefab`. The editor generator stores the
combined bounds, cell size, world pivot, occupancy and a hash of those permitted sources, so a
graybox edit regenerates a faithful layout without a hand-drawn PNG. The layout contains no runtime
entities, and no minimap camera or RenderTexture exists. The viewport is masked with `RectMask2D`;
the local marker remains centered while the generated geometry translates beneath it. Its encoded
world pivot is the sole map-pivot source used by the presenter.
The layout pivot is local to `Dungeon_Graybox.prefab`; `RaidMinimapPresenter` adds the serialized
world origin of its static Gameplay instance before projection. This is presentation configuration,
not replicated state, and avoids a scene lookup during tracking.

`MinimapProjection` is pure math. `_uiUnitsPerWorldUnit` means local RectTransform units per
world unit at the Canvas reference resolution, not physical monitor pixels. The map offset is
`(mapPivotWorldPosition - playerWorldPosition) * uiUnitsPerWorldUnit * zoom`. Sanctuary markers
use the same scale, intersect the direction vector with the useful rectangular bounds when
outside, and treat exact boundary points as interior. `MinimapProjectionResult.AngleDegrees`
uses `atan2(y, x)`: east `0`, north `90`, west `180`, south `-90`. Arrow orientation correction
is applied only by `RaidMinimapView`; zero displacement is a valid centered marker.

The presenter samples state only during `LateUpdate`. Extraction comes from
`PlayerExtractionController.TryGetProgress`; `Extracted` hides only the private Sanctuary
marker. Assignment comes from `ExtractionSanctuaryAssignmentService.TryGetAssignment`, and the
capability is resolved and revalidated through `EntityRegistry.TryGetExtractionSanctuary`.
The cached Sanctuary instance and Transform are presentation references only, never a second
assignment source. Ritual state comes from `IExtractionSanctuary.TryGetRitualProgress` and maps
to assigned, in-progress, cancelled and enabled visual states. Interior markers use a fixed,
unrotated icon; exterior markers use a separate edge arrow. All visual state is rebuilt from
current confirmed state after enable, rebind, replacement or a new session.
