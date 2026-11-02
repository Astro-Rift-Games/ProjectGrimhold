# Player Interaction Architecture

This document defines the architecture and data flow of the interaction system in **Project Grimhold**.

The system allows characters to perform contextual actions on world objects, such as collecting loot, activating mechanisms, and opening doors. Interaction is network-authoritative and safe under Fusion resimulation.

## 1. Structure and data flow

The flow keeps these responsibilities separate:

1. **Local input capture (Input Authority):** `PlayerInputReader` detects `PlayerInputButton.Interact` and writes it into `PlayerNetworkInput`.
2. **Input transport:** `FusionInputProvider` sends the input through Fusion.
3. **Network simulation (State Authority):** `PlayerInteractionNetworkController.FixedUpdateNetwork` validates and processes the interaction intent.
4. **Pure selection policy:** `InteractionResolver` selects a candidate from spatial and priority data.
5. **Interactable behavior:** each `IInteractable` owns the business rules for its interaction.
6. **Presentation:** confirmed results and local predictive candidates are published during `Render` for local presenters.

`PlayerMovementNetworkController` resolves the shared replicated `FacingDirection` before
interaction simulation. Locomotion supplies its default direction, while a same-tick
`Interact` intent may override it with a valid cursor direction resolved from the player's
post-movement position. This facing context does not select a target, alter
`InteractionResolver`, or add interaction network state.

```text
PlayerInputReader (local input)
       |
       v
FusionInputProvider (input transport)
       |
       v
PlayerInteractionNetworkController.FixedUpdateNetwork (State Authority)
       |-- candidate query (IInteractionTargetQuery / Physics2D)
       |-- selection policy (InteractionResolver)
       |     |-- distance and exclusion checks
       |     `-- authoritative IInteractable.Interact invocation
       v
[Networked] InteractionSequence increment
       |
       v
PlayerInteractionNetworkController.Render
       `-- local InteractionResolved notification
```

## 2. Core components

### 2.1 `PlayerInteractionNetworkController`

The controller coordinates interaction during network simulation ticks:

- It processes gameplay exclusively under State Authority in `FixedUpdateNetwork`.
- It detects button edges with Fusion's `WasPressed` API so holding the button does not repeat interactions.
- It validates the character's interaction eligibility.
- It obtains spatial candidates through `IInteractionTargetQuery`.
- It delegates candidate selection and interaction execution to `InteractionResolver`.
- It records target, result, tick, and `InteractionSequence` in replicated state.

### 2.2 `IInteractionTargetQuery`

This contract defines how candidates are found in the 2D world. `Physics2DInteractionTargetQuery` uses `Physics2D.OverlapCircleNonAlloc`, applies the configured layer mask and maximum distance, resolves entities through `EntityRegistry`, and avoids recurring allocations in the simulation loop.

### 2.3 `InteractionResolver`

`InteractionResolver` contains the shared deterministic selection policy:

1. Exclude the interacting character.
2. Reject invalid or out-of-range distance values.
3. Resolve the target's `IInteractable` capability.
4. Check `CanInteract`.
5. Invoke `Interact` on the first valid candidate and stop immediately, guaranteeing a single interaction attempt.

`TrySelect` uses the same policy without executing `Interact`, allowing local predictive presentation to agree with authoritative selection.

### 2.4 Contracts

- `IInteractable` exposes `CanInteract(in InteractionRequest)` and `Interact(in InteractionRequest)`.
- `InteractionRequest` is immutable and contains `InteractorId`, `TargetId`, and `SimulationTick`.
- `InteractionResult` is immutable and contains success, consumption state, and `InteractionFailureReason`.

## 3. Local presentation and confirmed results

`LocalInteractionCandidateSource` runs `InteractionResolver.TrySelect` during `Render` only for the player with Input Authority. It exposes a read-only local candidate for the predictive prompt. This prompt does not guarantee acceptance and does not synchronize text or visual resources.

For a changed candidate, the source resolves the exact runner-local `NetworkObject` and reads optional `InteractionPromptMetadata` once. The cached local text remains until the target or resolved instance changes, except for the social, Downed-revive and door branches, which refresh their dynamic prompt during `Render`; missing metadata falls back to `Interactuar`. `InteractionHudPresenter` renders the text as `[F] action` through `InteractionPromptText.Format`; the `F` label is a constant guarded by an EditMode test against the real `Gameplay/Interact` keyboard binding, and the prompt sits above the HUD action bar. Candidate loss, disable, despawn or a runner/session change clears the cache. Metadata never enters `EntityRegistry`, gameplay contracts or network state.

In Town, `SocialPlayerInteractable` shares the avatar `EntityId` and registers only its
dedicated layer-8 trigger. Its replicated `SocialPlayerIdentity.DisplayName` supplies the
small dynamic prompt `Invitar a Grupo a <Nombre>`; the existing nearest-valid-candidate
policy is unchanged. `TownPartyInvitationPresenter` binds the single preauthored Town
`InteractionHudPresenter` and projects invitation state replicated by `TownPartyDirectory` into
its serialized modal. Invitations mutate only the authoritative Solo/Duo Party roster and never
create or join a Raid preparation. Only an incoming modal suppresses gameplay input.

`TownPartyHudPresenter` is a separate local projection on `SocialPlayer.prefab`. Its preauthored
view is enabled only for `HasInputAuthority`, resolves member names through replicated
`SocialPlayerIdentity` values with `ProfileId` fallback, and exposes `Abandonar Party` only for a
Duo. The HUD forwards that explicit intent to `TownPartyDirectory`; it does not own roster state.

Every interaction press processed by State Authority increments `InteractionSequence`, including disabled control, unavailable interactor (dead or `Extracted`), and missing-target failures. The result retains target, tick, success, consumption, and its typed failure reason.

An intent consumed after the player is already `Extracted` follows this same confirmed path with `InteractorUnavailable`: it records the authoritative tick, advances exactly one sequence, sends the directed result to Input Authority and publishes it during `Render`. It does not run the target query, selection policy or `IInteractable.Interact`.

State Authority sends each result through a reliable RPC directed to Input Authority. The RPC handler only queues the payload; `PlayerInteractionNetworkController.Render` publishes `InteractionResolved`. The presenter deduplicates by sequence, ignores initial replicated state, and resets when bound to a player object from a new session.

`LocalPlayerHudBinder` enables the prefab HUD only when `HasInputAuthority`. Proxies, animations, and views do not execute interactions or modify authoritative state.

The three functional Town NPCs (Stash, Raid preparation, and Merchant) retain their existing single `IInteractable` endpoints. On a confirmed interaction, local presentation temporarily faces the NPC toward the player and starts its data-only dialogue. Their existing Town presenters defer panel opening until `DialoguePresenter` reports normal completion after hiding the dialogue and releasing its input suppression; each presenter re-resolves and validates the target before opening its existing panel. Cancellation, invalid targets, distance loss, and session teardown do not open a panel, and temporary facing is restored when the flow ends. This handoff adds no authoritative interaction path or cosmetic network state.

## 4. Loot-container interaction adapter

`NetworkLootContainerInteractable` is a same-root adapter over `NetworkLootContainer`. Both must share exactly one `NetworkObject` and therefore one `EntityId`. The container owns loot-source and collider registration; the adapter independently registers only `IInteractable`, accepting either spawn order. Expected-instance unregistration means either despawn order preserves the other capability and an obsolete owner cannot remove a later instance.

The adapter accepts initialized and available containers even when empty. `Interact` runs only under State Authority, returns a successful non-consumed result, and never changes contents, availability, `LootChangeSequence` or object lifecycle. Invalid prefab composition is diagnosed once and disabled.

The defeated `NetworkPlayer` is one composition of this generic endpoint, not a separate interaction type. While the player is alive its co-located container is initialized but unavailable, so the adapter rejects it. After `PlayerCorpseGenerationController` completes the authoritative inventory handoff, the same adapter and `EntityId` become eligible. Emptying the container does not disable interaction, close presentation, or despawn the player.

The confirmed event is only an opening signal. `RaidInventoryPresenter` requires the exact adapter/container composition and registry mapping before entering loot mode; merely finding a container on another successful interactable is insufficient. It deduplicates sequences per bound player object and advances its baseline even for failures, while a failed result leaves the current mode unchanged.

When in loot mode, a subsequent press of the interaction input raises `PlayerInputReader.InteractPressedLocally`. `RaidInventoryPresenter` catches this local event to close the UI immediately without executing gameplay, sending RPCs, calling `Interact()`, or modifying container state. The closing press is consumed locally and suppressed from network transport.

## 5. Loot pickup integration

### TASK-235: contextual lockpicking

`NetworkLootContainer` optionally starts locked (`_startsLocked`, default false). Its replicated
`IsLocked` is initialized only on fresh spawn, blocks loot transfers and random-content resolution,
and can only transition to unlocked through forward State Authority simulation. Snapshot restoration
does not reset the lock. Existing chest, enemy and corpse configurations remain unlocked by default.

`PlayerInteractionNetworkController` owns the contextual `CON_LOCKPICK` channel and its replicated
target, start position and deadline. Its serialized duration defaults to 3 seconds. Starting does
not open the container, generate contents, award first-open progress or spend inventory. Missing
inventory produces `LockpickRequired`, displayed as "Requiere Ganzúa". Pending execution uses
`LockpickingStarted`, not a successful loot-panel opening signal.

Movement intent or displacement, positive committed damage, invalid actor/target state, range loss
or missing inventory cancels without spending. Completion runs in `IAfterTick`, after movement and
committed damage on the deadline tick. It revalidates inventory/provenance, unlocks once and spends
exactly one unit through the existing PlayerFirst extraction without yielding. Only then does the
normal interaction adapter resolve first opening and publish the confirmed opening result. A second
participant observing an already unlocked chest cancels their channel without a second spend.
After Host Migration, a copied in-flight channel is cancelled without spending on its first forward
tick: its target EntityId has not been remapped. The restored container lock itself is preserved.

`LockedChest.prefab` uses the static `LockedChest` sprite as its sole presentation frame and starts
locked. Gameplay's scene spawn configuration assigns this variant a 10% chance per existing chest
point; the total count and points remain unchanged. Selection is deterministic from the container
seed, independent of content generation, so retries keep the same variant. Both prefab references
and the chance are copied to the runner-owned manager. Scenes without variant configuration retain
the normal-only path. Enabled variants must pass the same Fusion/content preflight plus a locked
initial-state check before any chest spawning begins.
For testing, Gameplay enables `Guarantee First Locked Chest For Testing`: the first existing chest
point is forced locked in the Editor and development builds, while the remaining points retain
their configured 10% rolls. The flag defaults to false elsewhere and is ignored by release builds.
It does not add a chest when the configured spawn count is zero, bypass spawning failures or
change the total count, content seed or idempotent retry policy.

For manual tests, the existing `RaidLootTestSpawner` Custom Item slot can spawn `CON_LOCKPICK` on
the Host, then normal pickup supplies inventory for contextual interaction. The item already exists
in the shared catalog; this integration does not add natural loot-table entries or merchant stock.

`NetworkLootPickup` implements `IInteractable` while preserving a strict reservation transaction:

1. `CanInteract` verifies that the pickup is not consumed.
2. `Interact` validates State Authority, the request, and availability.
3. It resolves `ILootReceiver` and builds the unified `LootTransferRequest`.
4. It reserves the pickup with `IsConsumed = true`.
5. It calls `ValidateReceive` without mutating the destination.
6. If prevalidation rejects, it restores `IsConsumed = false` and maps the precise loot reason to a general interaction result.
7. If prevalidation succeeds, it calls `CommitReceive` immediately without yielding State Authority or allowing an intervening mutation.
8. Only after the commit does it return `InteractionResult.Succeeded(isConsumed: true)` and despawn through `Runner.Despawn(Object)`.

The pickup is a consumable source with its own reservation. It does not implement extraction and does not coordinate transfers between two storage endpoints.

`LootTransferResult` preserves precise loot semantics. `InteractionResult` and `InteractionPresentationEvent` retain the nested `LootTransferFailureReason` when the general result is `LootRejected`, without expanding `InteractionFailureReason` with every transfer-specific case. This lets Input Authority distinguish `InventoryFull` from technical failures after authoritative confirmation.

For a full pickup, local presentation animates only the pickup's `WorldVisual` child through a short jump and complete rotation, then restores its exact local pose. The root `NetworkObject`, collider, identity and inventory remain untouched. The interaction sequence deduplicates the signal across proxies and resimulation. Contextual invalid/out-of-range rejections pulse the existing prompt; technical failures retain their diagnostic text.

## TASK-276: Dungeon doors

`NetworkDoor` implements `IInteractable` on a `NetworkObject`. State Authority owns the
replicated `DungeonDoorState` (`Closed`, `Opening`, `Open`, `Closing`) and `TransitionTimer`.
The existing interaction controller invokes it during `FixedUpdateNetwork`; only stable
states accept a toggle. Repeated presses during transitions are ignored without changing
door state or the timer. Closing is rejected with `TargetUnavailable` while a non-trigger
body on the `Character` or `Enemies` layer overlaps the doorway blocker bounds.
`NetworkDoor.FixedUpdateNetwork` completes transitions when the timer expires.

On `Closed` → `Opening`, State Authority resolves the interactor through the runner's
`EntityRegistry.TryGetTransform` and sets replicated `DungeonDoorOpenDirection`: interactor Y
strictly above the door pivot opens `South`; below or equal opens `North`. An unresolved
interactor rejects with `InteractorUnavailable` without changing state, direction or timer.
Direction persists through `Open` and `Closing` and is chosen again only on the next opening.

The `LocalInteractionCandidateSource` door branch reads `NetworkDoor.PromptText` during
`Render`, producing `[F] Abrir puerta` when closed and `[F] Cerrar puerta` when open.
Transitioning doors are not eligible candidates and have no prompt. Only the dedicated
`Interactable` trigger is registered with the door in `EntityRegistry`, never the blocker;
expected-instance unregistration releases it on despawn or destruction.

The non-trigger `WorldCollision` blocker stops characters, enemies, projectiles and vision.
Its closed-door width is 2.125 units and its Y footprint spans the door base through
base +1.0 unit, matching adjacent horizontal wall colliders. A player approaching from
the north stops at that north face rather than walking into the drawn door. Occupancy,
pathfinding refresh and vision all use this same blocker, not a separate footprint.
It stays enabled until opening completes and becomes enabled as soon as closing begins;
only `Open` disables it. Every peer applies the blocker from replicated state. On State
Authority, applying a changed blocker state calls `PathfindingGrid.RefreshRegion` over the
doorway bounds when the grid is built, updating local walkability without rebuilding the
whole grid. Vision already queries live colliders (see [Local vision](VisibilityArchitecture.md)).
For Host Migration, `Spawned` initializes `Closed` and clears the timer only under State
Authority when `HostMigrationRestoreUtility.IsRestoreSpawn(this)` is false; restore spawns
must preserve copied state, timer and opening direction. Fresh spawns alone initialize the
direction to `North` under the same guard.

`DungeonDoorPresenter` drives `Assets/Animations/Doors/DungeonDoor.controller` only in
`Render`. Its seven explicit states each own an editable sprite-only AnimationClip:
`Door_Closed` (frame 3), `Door_Open_North`, `Door_Opening_North`, `Door_Closing_North`,
`Door_Open_South`, `Door_Opening_South`, `Door_Closing_South` (frame 0 fully open).
Opening plays 3→2→1→0 and closing 0→1→2→3, at 10 fps for exactly 0.4 seconds;
transition clips do not loop and the single-pose idle clips loop. The controller has no
automatic transitions or gameplay events. On replicated state/direction changes or first
Render after spawn/restore, `Animator.Play` selects the matching state at the replicated
timer's normalized progress (idle at 0). Transition drift beyond half a sprite-frame is
corrected against the same timer. The View Animator uses Normal update, AlwaysAnimate
culling and no root motion. `Spawned` validates its controller and all seven states once.

`Door-Open-Sheet.png` opens north. `Door-Open-South-Sheet.png` remains provisional Art:
frames 1–2 use integer column shifts, frame 3 is unchanged, and frame 0 shifts the north
edge-on strip 7 pixels down inside its cell so the fully open leaf hangs south of the
threshold. Art may retouch or replace it. Both sheets use PPU 16, Point, uncompressed,
four full 128 x 128 cells and custom pivot (0.5, 0.3671875).
The presenter retains placeholder opening/closing audio through `AudioSource.PlayOneShot`
on observed transition entry in `Render`, deduplicated by state; spawn/restore and drift
corrections do not replay audio. Neither Animator nor clip completion advances door state,
toggles collision, or executes any gameplay.

`Assets/Prefabs/Doors/DungeonDoor.prefab` has `NetworkObject`, `NetworkDoor`,
`DungeonDoorPresenter` and `AudioSource` on its root. Children include `SortAnchor/View` (`SpriteRenderer` + `Animator`,
`Characters` sorting layer, order 0, Pivot sort point), `Blocker` (2.125 x 1.0 non-trigger box on `WorldCollision`,
center Y +0.5) and `InteractionTrigger` (2.5 x 2.5 trigger on `Interactable`, center Y +0.5,
covering base -0.75 through base +1.75). This keeps the trigger reachable within
`PlayerInteractionConfig.MaximumDistance` from either blocked side with the player's
root..root +0.5 body collider. Layers, registration and references are unchanged. The transition duration is
0.4 seconds; Play On Awake is disabled. Only the front animated door is implemented;
the import-configured side sprite is reserved for a follow-up.
Door/character Y-sort uses Renderer2D Custom Axis `(0,1,0)` and `Characters/0` groups: door `SortAnchor` at Y +0.6875 matches the player's root-to-feet offset, with `View` at Y -0.6875 preserving art/collider positions; walls remain `Default/1` (see [Rendering](VisibilityArchitecture.md#vision-fog)).

`Gameplay.unity` contains one native-size test door. Only its test passage is narrowed
from 3 to approximately 2 units with wall tiles and collision; final placement belongs to
Level Design. Required validation is EditMode `DungeonDoorStateRulesTests`,
`DungeonDoorOpenDirectionRulesTests`, `DungeonDoorAnimationTests`, `DungeonDoorSortingTests`,
`PathfindingGridRefreshRegionTests` and `VisibilityPrefabCoverageTests`, plus PlayMode
`DungeonDoorPlayModeTests` and `VisionOcclusionPlayModeTests` for passage, north-side body
stopping depth, projectile/vision occlusion, opposite-side Animator states and sprites,
direction retention, presentation re-adoption/drift correction and rejection without
mutation for an unresolved opening interactor. Manual Host/Client validation of state, collision,
frames and prompts is still required; automated checks do not prove multiplayer behavior.

## TASK-220: timed consumable execution

Consumables now execute through `PlayerConsumableNetworkController` with a per-definition finite,
positive duration (initially 2 seconds). Input Authority sends intent; State Authority starts and
resolves execution during forward Fusion ticks. Catalog index, deadline and request sequence are
replicated and are not reinitialized by `Spawned` after state restoration.

The character's consumption gate blocks voluntary movement/sprint, primary attacks, shield use,
ability activation, interactions, loot transfers/drops and equipment changes. The main-hand
presenter temporarily shows the consumable WorldSprite without modifying real equipment; the
off hand remains visible. An already-pending attack release prevents starting consumption.

Completion revalidates target, inventory and provenance before applying the effect and spending
one unit through the existing player-first provenance extraction. Positive committed damage
cancels directly through the character damage hook (not a presentation event). Downed, defeat,
extraction and match closure also cancel. Cancellation applies no effect and spends nothing.
Rejected/zero damage does not cancel. Confirmations and specific rejection notices are dispatched
locally during Render, outside simulation callbacks.

Serialized integration must be applied and checked in Unity before acceptance. Source compilation
alone does not validate Fusion weaving, prefab baking, Host/Client or Host Migration.


## TASK-233: offensive and tactical throwable consumables

Offensive and tactical consumables (`CON_EXPLOSIVE_BOMB`, `CON_THROWING_DAGGER`, `CON_SMOKE_BOMB`) execute through `PlayerConsumableNetworkController` using `ItemUseMode.AimedQuickSlot` triggered from Quick Slots targeting the player's aim/cursor position.

### Immediate Consumption Rule
Unlike timed consumables (potions) which deduct items only upon successful timer completion, throwables enforce immediate consumption: 1 unit is deducted from the player's inventory immediately when the State Authority confirms and begins the throw, regardless of whether the projectile hits, misses, or is blocked by geometry.

### Throwable Mechanics
- **`CON_EXPLOSIVE_BOMB`**: Thrown in a parabolic arc towards the target world position (clamped to 7.0m maximum throw distance and raycast against obstacle colliders). Detonates after a 1.5-second fuse delay from launch, dealing 50 neutral damage via `IDamageResolver` to all `IDamageable` entities within a 2.5m radius.
- **`CON_THROWING_DAGGER`**: Fast linear directional projectile launched towards the player's aim direction. Deals 20 neutral damage to the first valid target or geometry collision, stopping and despawning without dropping to the ground.
- **`CON_SMOKE_BOMB`**: Aimed throw with a short 0.5s execution channel that deploys a neutral smoke zone (`NetworkSmokeZone`) at the target location. Deals no damage; creates an area of radius 3.0m lasting 8.0s.
- **Smoke Line-of-Sight Occlusion**: Active smoke zones register in `SmokeZoneRegistry`. Both player field-of-view (`VisionQuery`) and enemy perception sensors (`EnemyMovementAIController`) query `SmokeZoneRegistry.IsLineOccluded` to block vision through or into the smoke cloud. Point-blank mutual visibility within 1.2m is preserved when both viewer and target are inside the same smoke zone.
