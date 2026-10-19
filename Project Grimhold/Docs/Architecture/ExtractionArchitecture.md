# Extraction Architecture

## Context and status

The authoritative extraction core owns zones, the player extraction process and terminal gameplay restrictions. The local HUD and replicated player visuals project only its confirmed state. The core does not despawn the player, create rewards, persist loot, close the session, or depend on UI, animation, audio or VFX.

## Sources of truth

| Concern | Owner | Representation |
| --- | --- | --- |
| Stable balance & capacity | `ExtractionConfig` | Immutable `ScriptableObject` (`SoloProgressQuota`, `DuoProgressQuota`, `MaxTeamsPerRaid`) |
| Team launch identity & roster | `RaidTeamRoster` | Pure C# immutable projection of frozen cohort (`RaidTeamId`, initial team sizes 1-2) |
| Team progress & quota | `TeamExtractionProgress` | `[Networked, Capacity(RaidSessionRules.MaxTeams)]` on `NetworkMatchController` |
| Producer forwarder | `PlayerExtractionProgressController` | Implements `IExtractionProgressReceiver/Reader`, forwards to team progress |
| Zone identity & geometry | `ExtractionZone` | `EntityId` derived from `NetworkObject`, serialized `Collider2D` |
| Zone availability | `ExtractionZone` | Private `[Networked] NetworkBool`, exposed as `bool` |
| Team sanctuary assignment | `ExtractionSanctuaryAssignmentService` | Runner-local deterministic assignment by `RaidTeamId` |
| Team sanctuary ritual | `ExtractionSanctuary` | `[Networked, Capacity(RaidSessionRules.MaxTeams)]` slots `{TeamId, RitualState, RitualTimer}` |
| Team countdown coordination | `TeamExtractionCoordinator` | `NetworkBehaviour` (order 115) on `NetworkMatchController`, slots by `RaidTeamId` |
| Individual player outcome | `PlayerExtractionController` | Individual terminal `ExtractionState.Extracted`, restrictions, loot saver hooks |
| Death & Downed state | `ICharacter` / `PlayerCharacter` | `IsAlive`; Downed counts as alive for extraction |
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

Additionally, a participant must be `Connected` to initiate or complete extraction (the connectivity gate). A disconnected participant cannot start or continue extraction, and any active extraction process is cancelled upon disconnection.

Team composition considers any `Raiding` member (Active or Downed), regardless of connectivity, as an active member (see `RaidConnectivityContinuityArchitecture.md`). Team extraction mechanics, shared quota, team sanctuary assignment, and team countdown coordination are coordinated authoritatively per team via `TeamExtractionProgress`, `TeamExtractionCoordinator`, `RaidTeamRoster`, and `ExtractionSanctuary` team slots.

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

`RaidHudPresenter` is bound only to the Input Authority player's `PlayerExtractionController`. It reads `TryGetProgress` during presentation updates and renders the confirmed local countdown, a one-shot cancellation message, or the terminal `Extracted` label in the ritual/extraction status section of the HUD (`RaidHudView.ExtractionText`, inside `RitualBlock` of `RaidRightPanel`). It does not start, cancel or complete extraction and does not maintain a gameplay timer. The cancellation message uses an unscaled presentation-only duration.

`PlayerExtractionPresenter` observes the replicated `ExtractionState` on every peer. When the state is `Extracted`, it hides only the serialized `Body` and `CombatVisuals` roots. The NetworkObject, colliders, camera, HUD, interaction and authoritative gameplay components remain active. Disabling the presenter restores its serialized visual state; re-enabling it reapplies the terminal visual from the current confirmed state.

The extraction zone keeps its existing pozo visual and availability tint. This presentation boundary does not add world-space UI, audio, particles, a second extraction clock or another networked state source.

## Lifecycle and validation

Zones clear overlap buffers' logical sets, occupant state and saturation diagnostics during despawn and destroy. Participants and zones unregister only their expected capabilities. Destroying the runner destroys its registry, preventing state from leaking into a later session.

Automated coverage should include configuration and geometry boundaries, progress semantics, registry composition/conflicts, atomic enemy target invalidation, owner-protocol rejection, exact entry versus tolerant continuation, saturation behavior, authority, terminality and lifecycle. Fusion availability, timing, overlapping zones, independent players, despawn and clean-session behavior require runner tests plus the documented Host/Client manual checklist when no automated multi-runner harness proves them.

## Extraction ownership snapshot

`PlayerExpeditionLootSnapshot` captures Inventory, each of the eight Equipment slots, their persistent aggregate by `LootId`, Inventory origin buckets, each Equipment origin and the aggregate origin buckets. It validates duplicate buckets, positive quantities, overflow and exact per-LootId equality before exposing a snapshot. The persistent RPC and Loadout commit continue to carry only catalog indices and quantities; Raid provenance never enters stash or backend storage.

When a participant becomes `Extracted`, State Authority's `PlayerExtractionLootSaver` retains the authoritative ownership snapshot while the commit remains unconfirmed. Inventory, Equipment, logical Raid participant origins and every bucket stay in Fusion state until Input Authority ACKs the idempotent persistent commit. The local durable snapshot is only a verified projection. A valid aggregate may contain up to 38 distinct entries: thirty Inventory types plus eight Equipment slots, while every catalog index remains valid in the independent `0..63` range.

On Host Migration restore, Fusion `CopyStateFrom` restores `RaidParticipantId` values, bucket identities and quantities together. `Spawned()` recaptures the pending projection from that restored authoritative state when the participant is still `Extracted` and unconfirmed; it does not rebuild or reassign identities.

Before sending the persistent payload, State Authority projects initial affiliation, eligibility,
eligible Value and candidate extracted-Loot Experience from the retained snapshot. The participant
stores that candidate durably for the matching `ResultSequence`; it is not provisional Experience
until confirmed extraction lets the ledger resolve it. Preparation failure retains ownership and
retries without starting persistence.

In the same preparation step State Authority publishes the Results loot summary on the participant
(`ResultsLootKind.Extracted`). Its grid contains only the quantities classified as eligible by the
same Raid origin resolution that feeds the Experience candidate: Dungeon origin plus players of another
initial team. Loot that left Town with the player, or came from a teammate, is omitted, and the
displayed total Value matches the eligible Value of the candidate. Publication is write-once and
presentation-only: a failure is reported but never blocks or retries the extraction transaction.

The authoritative phases are `AwaitingExperiencePreparation -> AwaitingPersistenceAck ->
ExtractedLootPending -> ProgressionPending -> Complete`. A valid ACK exact-clears ownership and
enters `ExtractedLootPending`. Ledger success, the same sequence already resolved, or a valid zero
award advances; a real failure remains retryable and cannot freeze Progression. After
`ProgressionPending`, retries execute only TASK-110. Resolver `Success` or `AlreadyCommitted`
completes the authoritative Raid calculation; Input Authority must then durably commit and ACK
the matching progression receipt before Return is permitted. Fusion restoration resumes the
stored phase, candidate, resolver result and ACK without recalculation.

## Team quota progress (supersedes Individual quota progress)

The expedition supports team progress and shared quotas across participants. Initial team roster and team sizes are defined immutably at raid launch via `RaidTeamRoster` (MVP supports teams of size 1 and 2 per rule A-1).

Quota balances are configured in `ExtractionConfig`:
- `SoloProgressQuota`: 100 points.
- `DuoProgressQuota`: 150 points.
- `MaxTeamsPerRaid`: 4 teams (capped at `RaidSessionRules.MaxTeams` = 16).
Quota resolution is pure and immutable per team size via `ExtractionTeamQuotaRules`.

Team progress is networked authoritatively on `NetworkMatchController` via `TeamExtractionProgress` (`[Networked, Capacity(RaidSessionRules.MaxTeams)]`), tracking `CurrentProgress` and `AssignmentRequested` indexed by `RaidTeamId`. State Authority is the only writer.

`PlayerExtractionProgressController` on each player implements `IExtractionProgressReceiver` and `IExtractionProgressReader`. Rather than storing individual progress, it forwards contributions directly to `TeamExtractionProgress` using the participant's assigned `RaidTeamId`. Contributions are accepted only during the matching authoritative simulation tick while the player is alive and not `Extracted`, and accumulate into the team's shared pool saturating at the team's quota. Reaching quota triggers assignment request once for the team.

`ExtractionProgressSnapshot` provides the team quota projection: current progress, quota, percentage, completion, and pending assignment.

## Team sanctuary assignment (supersedes Individual sanctuary assignment)

The pre-ritual flow is `team quota -> team request -> team assignment -> sanctuary reservation`. When a team completes its quota under State Authority, `TeamExtractionProgress` triggers `ExtractionSanctuaryAssignmentService` for the team's `RaidTeamId`.

**Divergence D-3 with Game Design Doc 06 §10/§12:**
In Game Design doc 06 §10 and §12, each sanctuary was originally envisioned as exclusive to one team. In the technical implementation (US-53), multiple teams may be assigned to and share the same `ExtractionSanctuary` in the scene (divergence D-3, tracked as a GD follow-up).

To support D-3, each `ExtractionSanctuary` maintains networked team slots (`[Networked, Capacity(RaidSessionRules.MaxTeams)]`) storing `{TeamId, RitualState, RitualTimer}` rather than a single owner ID. Multiple teams can have reservations on the same Sanctuary simultaneously and progress through rituals independently.

Host simulation deterministic assignment matches pure policy: pseudorandom mixing of seed, tick, team identity, and sanctuary candidate count.

## Authoritative team ritual and shared extraction coordination (supersedes Authoritative individual ritual)

Each `ExtractionSanctuary` composes one `ExtractionZone` on the same root `NetworkObject`. The Sanctuary is the team interaction target for initiating the extraction ritual.

### Team Ritual Rules (`TeamRitualRules`)
1. **Initiation**: Any living, active (not Downed), connected member of the assigned team can initiate the ritual via interaction with the assigned sanctuary (`CanInitiateRitual`).
2. **Continuation & Cancellation**: Once started, the ritual continues counting down. It cancels only if no living, connected members of that team remain in the raid (`ShouldCancelRitual`, rule A-4 / D-5).
3. **Completion**: Upon ritual timer expiry, the ritual transitions to `Completed`, activating the extraction zone countdown for that team.

### Team Extraction Coordination (`TeamExtractionCoordinator` & `TeamExtractionRequirementRules`)
Countdown and extraction completion are coordinated per team by `TeamExtractionCoordinator` (`NetworkBehaviour` on `NetworkMatchController`, execution order 115):

- **Required Members**: Evaluated via pure rules `TeamExtractionRequirementRules`. A team member is "required" for extraction if they are alive (Active or Downed) AND connected. Disconnected members create a fail-closed gate: if a living teammate is disconnected, extraction cannot proceed until they reconnect or their absence is confirmed via the absence protocol.
- **Zone Occupancy**: All required members must be inside the sanctuary's zone (`ContainsExact` to start, `ContainsWithTolerance` to continue).
- **Downed Extraction (Rule D-6)**: A Downed teammate inside the zone extracts together with their active teammate(s) upon countdown completion.
- **Team Composition Signature**: Evaluated dynamically. If alive/connected membership changes during countdown (e.g. death or confirmed absence), the composition signature changes and the countdown restarts safely with the updated required roster.
- **Completion**: When the team countdown completes, `TeamExtractionCoordinator` authoritatively transitions all present eligible team members to `ExtractionState.Extracted` via their `PlayerExtractionController`.

## Ritual presentation boundary

The ritual presentation is intentionally partial. The local HUD consumes the nullable
`PlayerExtractionProgressController`, runner-scoped `ExtractionSanctuaryAssignmentService`,
runner-scoped `EntityRegistry`, `IExtractionSanctuary`, `ExtractionProgressSnapshot`,
`ExtractionRitualSnapshot` and the existing `ExtractionCountdownSnapshot`. Each source is
evaluated independently: a missing assignment service or sanctuary does not hide valid
progress, and a missing progress source does not disable the rest of the HUD.

The former single extraction label is split into three independent HUD sections, each with
its own dirty check and clear operation: quota (`QuotaText`, `Progreso: n / m` or the persistent
`Cuota completada`), assigned Sanctuary (`SanctuaryText`) and ritual/extraction status
(`ExtractionText`). Individual progress also drives a bar and whole percentage in the objectives
block, derived by `ExpeditionProgressMath`; it is individual (MVP) progress, not team progress.
A missing source clears only its own section.

The ritual/extraction status has one explicit priority: terminal `Extracted`, active extraction
countdown, existing cancellation feedback, completed ritual, in-progress ritual, cancelled
ritual, and finally the unavailable placeholder. Quota completion is no longer transient
feedback: `Cuota completada` stays in the quota section while the quota is complete. Progress
and countdown maintain separate observation baselines; the first valid snapshot and the first
snapshot after an invalid interval establish a baseline, so only a countdown
`InProgress -> None` transition produces the local, unscaled cancellation feedback.

`ExtractionZone` is simulation-only and is the interaction area of the Sanctuary.
`ExtractionSanctuaryPresenter` is the only renderer owner for the Sanctuary visual. It resolves the current local PlayerObject on every presentation
update from `Runner.LocalPlayer` and never retains a private player identity. Invalid or
replaced runner/player context immediately falls back to the public visual state.

The Sanctuary presenter consumes `IExtractionSanctuary.TryGetRitualProgress` and rebuilds its
visual from the confirmed snapshot. Rivals see only public ritual progress: an unreserved/base
state, a global `InProgress` pulse, or a permanent `Completed` state. Reserved and cancelled
tints and their one-shot cues are private to the currently resolved local owner. Ritual start
and completion cues are public world feedback. A first valid snapshot (including a late join or
re-enable) establishes a baseline without replaying cues; only subsequently observed state or
reservation transitions emit them. Missing or invalid snapshots reset that baseline. Progress
intensifies the in-progress pulse as an aesthetic response, but timing remains derived only
from the confirmed `TickTimer` snapshot. Audio uses the existing `AudioManager` with authored
spatial `CustomClip` cues at the Sanctuary; one-shot completion particles use
`ParticleEffectPlayer` with an authored cue reference. Missing optional cue references or
presentation services never affect gameplay. All presenters preserve the alpha
authored on each renderer; state changes modify RGB only and never alter zone transparency.
Presentation components write no simulation state and add no replicated properties, RPCs or
gameplay timers.

The local minimap presentation is described below.

## Local minimap presentation

`RaidMinimapPresenter` is an independent presentation section bound by `LocalPlayerHudBinder`
to the current Input Authority `NetworkObject`, `Transform`, `PlayerExtractionController`,
runner-local assignment service and `EntityRegistry`. Missing assignment infrastructure affects
only the private Sanctuary marker and never disables the remaining HUD.

`RaidMinimapView` is composed on the existing `LocalGameplayHud` Canvas, as the first child of the `RaidRightPanel` column. Its north-up
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
