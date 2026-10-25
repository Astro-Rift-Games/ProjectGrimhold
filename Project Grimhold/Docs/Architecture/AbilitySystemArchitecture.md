# Ability System Architecture

## Status and scope

This document defines the Ability System foundation and the authoritative Raid runtime contract (TASK 417 and TASK 444). The foundation establishes identity, Town preparation and frozen Raid admission. TASK 445 implements slot binding, TASK 446 implements activation intentions and TASK 447 implements the common authoritative activation/execution/cooldown cycle. TASK 199 implements Current Mana and initial Mana payment. TASK 210 defines the targeting contract (see [Targeting](#targeting)) and TASK 211 implements its aim capture and TASK 212 implements the caster-centered area query with the creature-only valid-enemy predicate; PvP and Downed enemy targets remain pending Game Design or later tasks. TASK-204 adds ally areas using Self plus INITIAL teammates and implements Potenciar, Restauración and preventive Purificación with a minimal future-state seam. These join Embestida, Golpe Sísmico, Trampa, the PvE-only Proyectil Arcano and Orbes Protectores (TASK203); TASK-205 adds the PvE-only Drenaje Vital, completing nine bindings on `NetworkPlayer.prefab`, one per catalog definition (see their sections); Trampa is the first persistent spawned effect, while Orbes is an avatar-owned effect with local pooled geometry. A generic Status Effect system, Assist, balance, toggles and a generic summon system remain deferred.

The names used for future roles in this document describe responsibilities, not existing runtime types. Later tasks may choose concrete type names while preserving these boundaries.

TASK 445 implements the slot-binding foundation as `PlayerAbilityRuntimeNetworkController` on `Assets/Prefabs/NetworkPlayer.prefab`. It exposes `IsInitialized`, `TryGetSlot` and `IsSlotAvailable`, backed by independent read-only `AbilityRuntimeSlot` descriptors resolved atomically through `AbilityRuntimeSlots`. Availability here means a confirmed, current avatar with an occupied prepared slot, not activation eligibility, affordability or cooldown readiness. TASK 447 extends this same owner, rather than replacing prepared-slot availability with activation readiness.

The component replicates its initialization marker and previous ability-button history. The prepared identities still belong to `NetworkRaidParticipant`; resolved definitions are derived local references. Fresh State Authority initializes once after bidirectional avatar/participant binding. Restore preserves the copied marker, including an uninitialized snapshot, and waits for reference fixup. Terminal participation/generation cleanup clears availability; disable/re-enable releases and reconstructs local references without resetting confirmed state.

TASK 446 adds `Gameplay/AbilitySlot1` on Q and `Gameplay/AbilitySlot2` on E (Interact is on F), through the owning InputActions asset and Unity-generated wrapper. `PlayerInputReader` transports independent button intentions through the existing `FusionInputProvider` and `PlayerNetworkInput.Buttons`, preserving short taps, held state and gameplay suppression. A held key after suppression or reader re-enable requires physical release before another intention.

State Authority consumes rising edges in the avatar runtime's `FixedUpdateNetwork`. `WasActivationRequested` is a read-only current-simulation-tick query, false outside simulation and on proxies; it is not an accepted execution or queued request. History is consumed before slot availability, so empty/unbound slots cannot defer a press until later binding. Missing input produces no request and retains history. Restore and runtime re-enable baseline the first valid sample without emitting intentions; terminal cleanup clears transient intentions. Execution validation, resource payment and cooldowns belong to TASK 447.

## Design constraints

The technical architecture preserves the current MVP rules from Game Design:

- Abilities belong to the character's persistent repertoire, not to an equipped weapon.
- The character has exactly two universal ability slots. The slots are equivalent, independent of Weapon Sets A/B, and each may be empty.
- A prepared build may contain zero, one, or two abilities; the same ability cannot occupy both slots.
- Attribute requirements gate preparation and use. They do not gate acquisition.
- Ability definitions own their configured requirements. The character's confirmed attributes supply the values used to evaluate them.
- Preparation changes occur in Town while the player is eligible to edit the build. The prepared snapshot is frozen for the Raid.

Concrete ability configuration determines targeting, execution-specific behavior and feedback. The common execution, resource and cooldown boundaries are defined below without introducing new gameplay rules.

## Architectural boundary

```text
Static content                       Persistent character aggregate
Ability identity -> definition       unlocked repertoire
                 -> single catalog   prepared slot 1 / prepared slot 2
          |                                      |
          +-------------- Town validation -------+
                                                 |
                                      Raid admission snapshot
                                      prepared identities + attributes
                                                 |
                                      NetworkRaidParticipant
                                      frozen Raid entitlement
                                                 |
                                      Raid ability runtime boundary
                                      authoritative session state
```

The Ability System is a separate domain boundary from the current basic weapon attack flow. It may consume compatible shared gameplay services, but it does not route through `IAttack` or `PlayerCombatNetworkController`.

## Sources of truth

| State kind | Source of truth | Contents | Must not own |
| --- | --- | --- | --- |
| Static | One ability definition catalog | Stable identity and immutable authored configuration, including attribute requirements | Per-character unlocks, preparation, cooldowns, or active effects |
| Persistent | The character aggregate exposed transactionally through `LocalProfileStore` | Unlocked ability identities and the two prepared optional slot values | Raid execution state or network authority |
| Prepared | Projection of the confirmed persistent profile snapshot; not an independent source of truth | The ordered pair of universal slot identities selected for the next Raid | A second repertoire or mutable Raid state |
| Admission | The versioned Town-to-Raid admission payload | The prepared slot identities and the already-confirmed character attributes required to validate them | The full unlocked repertoire or mutable runtime state |
| Raid entitlement | `NetworkRaidParticipant` | The frozen prepared identities and frozen admitted attributes for that participation | Static definitions or Town persistence |
| Raid runtime | One future ability runtime component on the productive Raid avatar | Execution state and independent cooldowns for both universal slots | Persistent unlocks, Town preparation, character resource balances, or basic weapon attack state |

Presentation may read confirmed state later, but it is never a source of truth and cannot mutate gameplay.

## Static content

### Stable identity

Every ability requires one stable, serializable identity that remains valid across persistence, admission, replication, and content lookup. Identity comparison is ordinal and independent of display name, asset path, localization, or list position.

At the static-definition reference level, stable ability identity is the only value transported across persistence and admission boundaries. Runtime replicated state is a separate concern owned by the future Raid ability runtime. Unity object references and complete ScriptableObject definitions do not cross those boundaries.

### Immutable definition

Each catalog entry resolves one identity to one authored definition. A definition is immutable shared configuration at runtime. The implemented `AbilityDefinition` contains identity, attribute requirements, resource, initial cost and cooldown. Later tasks may add execution-specific configuration only when their owning designs and contracts are implemented.

A definition never stores whether a character unlocked or prepared it, and it never holds mutable cooldown, resource, targeting, or effect state.

### Single catalog

One catalog is the authoritative runtime lookup for all ability definitions. It must reject invalid identities, null definitions, and duplicate identities during validation. Consumers resolve an identity through this catalog rather than maintaining subsystem-specific definition lists.

If later network transport uses compact catalog indices, those indices are session transport details derived deterministically from the same catalog. Stable identities remain the persistence and admission contract.

## Persistent repertoire and preparation

### Unlocked repertoire

The character aggregate owns a set of unlocked ability identities. Unlocking is a persistent transaction. A duplicate unlock is not a second owned copy, and failing an attribute requirement does not remove an unlock.

`LocalProfileStore` remains the transactional application boundary. Ability mutations must build and validate a complete candidate snapshot, persist it through the active repository boundary, and publish the confirmed replacement only after acceptance. UI and Town network presentation do not mutate the repertoire or prepared slots directly.

The current process-local persistence limitations still apply until backend integration exists; this architecture does not invent a second ability save path.

### Acquisition sources

Every source ends in the same persistent unlock and validates through one private store rule: valid identity, known to the catalog, not already unlocked. Attribute requirements are never checked on acquisition.

| Source | Boundary | Transaction | Already unlocked |
| --- | --- | --- | --- |
| Town Ability Vendor NPC | `TownAbilityVendorPresenter` (Input Authority, local panel) | `LocalProfileStore.TryPurchaseAbility(id, price)` debits Currency and unlocks in one commit | Not offered as buyable; the store rejects with `AlreadyUnlocked` and charges nothing |
| Mission reward (`RewardDefinition.RewardType.Ability`, `ReferenceId` = ability identity) | `LocalProfileStore.TryClaimMission` | The unlock joins the claim's single candidate snapshot | No-op; the rest of the reward package is still claimed |
| Dungeon discovery | Not in the MVP | — | — |

The vendor has no shared stock, so a purchase is not networked: like a merchant trade commit, the requesting client's store is the only writer. The price comes from `AbilityVendorCatalog`, never from the view, and the presenter rebuilds the offer from the confirmed profile before buying. Buying is not preparation and is not gated by Ready. An invalid or unknown ability reward rejects the whole mission claim without changing the confirmed snapshot. The vendor catalog content (nine abilities at a flat 100 Gold) is provisional until Game Design defines vendor offers and prices.

### Two universal prepared slots

The persistent snapshot contains exactly two equivalent optional slot values:

```text
Prepared Ability Slot 1: Ability identity or empty
Prepared Ability Slot 2: Ability identity or empty
```

Slot position identifies which prepared value is transported; neither slot has different gameplay capabilities. A valid prepared snapshot satisfies all of these invariants:

1. Each non-empty identity resolves through the single catalog.
2. Each prepared identity belongs to the unlocked repertoire.
3. The two non-empty identities are different.
4. The confirmed character attributes satisfy every requirement of each prepared definition.
5. Zero, one, and two occupied slots are all valid.

Town owns preparation changes. A respec or other confirmed attribute mutation must revalidate both slots in the same persistent aggregate transaction and clear any now-invalid prepared ability without removing it from the unlocked repertoire. The existing Town Ready/Not Ready policy decides whether the mutation is currently permitted; the Ability System does not create a parallel readiness rule.

## Town-to-Raid flow

```text
Confirmed LocalProfileStore snapshot
  -> validate both prepared slots against repertoire, catalog, and confirmed attributes
  -> encode the two optional stable identities in versioned Raid admission data
  -> validate the admission snapshot against the same catalog and admitted attributes
  -> initialize the NetworkRaidParticipant frozen ability snapshot
  -> future avatar ability runtime binds to that frozen participant state
```

Only the two prepared identities cross admission. The complete unlocked repertoire stays persistent and is not copied into Raid runtime state. The existing admission payload already transports `CharacterAttributeState`; ability validation consumes that same frozen snapshot instead of creating an ability-specific attribute copy.

The implemented admission path represents both optional slots and preserves slot order. Unknown identities, duplicate occupied slots, malformed values, or unmet admitted attribute requirements fail admission; they are not defaulted, substituted, or silently removed. `NetworkRaidParticipant.TryGetPreparedAbilityLoadout` exposes the admitted pair only after its replicated initialization marker is available.

The Raid Host never queries another player's `LocalProfileStore`. It accepts only the validated admission snapshot for the frozen cohort.

## Raid ownership and authority

### Frozen participant state

`NetworkRaidParticipant` is the stable Raid `PlayerObject` and owns the admitted ability entitlement for the participation: the two frozen prepared identities plus the existing frozen attributes. This state survives avatar lifecycle changes and cannot be changed by Town profile commits after admission.

The participant does not execute abilities. It exposes the frozen snapshot to the future runtime boundary in the same way that other Raid consumers read admitted character state.

### Future runtime owner

Exactly one ability runtime component on the productive Raid avatar owns the slot runtime, execution and cooldown state for both slots. `PlayerAbilityRuntimeNetworkController` implements the binding foundation; the common execution/cooldown transitions described below are implemented. It is separate from `PlayerCombatNetworkController`; the participant remains the stable entitlement/result owner and does not execute abilities.

The runtime resolves its participant through the existing `RaidAvatarParticipantLink`, verifies that it is the participant's current avatar, reads `TryGetPreparedAbilityLoadout`, and resolves each occupied `AbilityId` through the single `AbilityDefinitionCatalog`. Empty slots remain empty and unavailable; binding never substitutes an ability. Static definition references are local derived configuration, not a second replicated loadout.

Fresh State Authority initializes runtime state once after the participant relationship and admitted pair are available. An unresolved relationship leaves the runtime unbound, unable to activate, and does not initialize empty slots as a fallback. Restore binding reconstructs references without fresh initialization.

The boundary preserves these rules:

- State Authority is the only writer of authoritative ability state and transitions.
- Input Authority may express intentions but does not commit outcomes.
- Ability selection and static configuration are initialized from the frozen participant snapshot and the single static catalog. Runtime execution may consume explicitly owned Raid services and resources without duplicating their state or ownership.
- Runtime state is never written back into `LocalProfileStore` as an unlock or preparation mutation.
- Configuration that can be resolved from an ability identity is derived locally and is not replicated as duplicated mutable state.
- Presentation observes confirmed/networked outcomes and does not drive simulation.

Attribute checks at use consume the participant's existing effective `TryGetCharacterAttributeState` read, including its session-only testing overrides, as established by [Raid Participant Architecture](RaidParticipantArchitecture.md). Ability identities remain frozen; no second attribute snapshot or Town-profile lookup is introduced.

### Per-slot state

Slot 1 and Slot 2 use the same representation and rules. Neither follows Weapon Set selection, shares the weapon cooldown, nor has different capabilities. Cooldown is independent of execution phase: a completed or interrupted execution may leave its slot cooling down.

| State | Ownership and representation |
| --- | --- |
| Slot position and prepared identity | Read from the frozen participant pair; no mutable runtime selection |
| Resolved definition and configured cost/cooldown | Derived locally through the catalog |
| Runtime initialization marker | Authoritative replicated state; distinguishes fresh initialization from restore/rebind |
| Execution phase | Authoritative replicated state: idle, preparing when required, or executing; completion/interruption returns the execution to idle |
| Execution sequence | Authoritative per-slot sequence identifying each accepted execution, independent of weapon `AttackSequence`; rejected requests do not advance it |
| Execution deadline/progress | Authoritative Fusion tick-based state only when required by the execution; no local coroutine or wall-clock source of truth |
| Cooldown deadline | Authoritative per-slot Fusion tick deadline, started on accepted activation; remaining time and availability are derived |
| Specific execution context | Only the committed context/progress needed to resume that concrete behavior without resolving it twice; later ability tasks define their necessary snapshot fields |
| Presentation state | Local observation baselines, interpolated visuals and feedback; never controls payment, phases, deadlines or outcomes |

An instantaneous ability may start and finish in the same simulation tick. The execution sequence still identifies its confirmed occurrence. The base contract does not require a generic target payload, effect list or global action tracker. Ability-specific state is added only when its implemented behavior requires it.

## Activation and execution

### Intent and authoritative start

```text
PlayerInputReader
  -> FusionInputProvider -> PlayerNetworkInput
  -> independent Slot 1 / Slot 2 activation intentions
  -> avatar ability runtime in FixedUpdateNetwork
  -> common and ability-specific validation
  -> complete resource payment + accepted execution + slot cooldown
  -> concrete behavior through existing gameplay services
```

Input Authority expresses slot intentions, not an arbitrary ability identity, resource balance or accepted outcome. TASK 446 implements independent button bindings and transport, preserving the existing input flow and gameplay suppression without routing through Primary Attack or per-frame RPCs.

State Authority validates the current participation/avatar, gameplay phase, Active state, occupied slot, resolved configuration, effective attribute requirements, execution compatibility, cooldown and full resource cost. It also requires the concrete behavior's start validation, including its targeting rules when implemented. Downed uses the existing `PlayerDownedGate`; recovery restrictions follow [Downed and Revive Architecture](DownedAndReviveArchitecture.md), not a new ability-owned character state.

All non-mutating start checks precede payment. In one simulation boundary, State Authority commits the complete payment and then the accepted execution sequence, phase/context and cooldown deadline. Failure before acceptance leaves resource, execution sequence and cooldown unchanged. The concrete behavior must not discover a start rejection only after payment. A valid execution that later misses, finds no targets or is interrupted follows its configured resolution rules; it does not refund the accepted cost or cooldown.

The runtime processes input edges without queuing stale presses for later cooldown expiry or re-enable. Missing input prevents new intentions, not progression of an already accepted execution or expiry of its cooldown. This document does not invent a global exclusivity rule, cross-slot priority or new action compatibility policy: those rules must come from the owning ability configuration and Game Design.

### Common runtime versus concrete behavior

The common owner controls binding, validation, payment coordination, phases, execution identity, cooldowns and lifecycle transitions. Concrete behavior controls authored preparation/duration, valid targets, resolution and configured interruption/compatibility rules. It uses the accepted execution context and existing damage, healing, movement or knockback services without becoming another owner of the slot state.

Ordinary C# notifications are not authoritative transition sources. Simulation reads authoritative character/lifecycle state or receives explicit authoritative operations at the owning boundary. Outcomes and irreversible effects must be tied to the accepted execution identity/progress and not repeated by resimulation, presentation, rebind or Host Migration.

### TASK 447 common-cycle implementation

Each slot copies an `AbilityExecutionSnapshot`: phase (`Idle`, `Preparing`, `Executing`), accepted sequence, cooldown `TickTimer`, optional phase-deadline `TickTimer` and the captured `AimDirection` (TASK 211, see [Targeting](#targeting)). Cooldown is independent of execution phase; normal completion or interruption clears the phase deadline and the captured aim but retains the paid cost, sequence and cooldown. No replicated remaining-time, configuration reference or derived readiness flag is added. `TryGetExecutionSnapshot`, `IsOnCooldown`, `GetRemainingCooldownSeconds` and `HasActiveExecution` are read-only queries. `GetLastActivationFailure` reports the local authoritative attempt only, not replicated feedback.

The runtime runs after the existing Stamina (-11), movement (-10) and equipment (-9) boundaries, and before primary combat (-7), using order -8. It processes Slot 1 then Slot 2 deterministically, rechecking available resources for each request. This is not universal cross-slot exclusivity. Existing executions progress without input; held/rejected presses never queue for later availability.

`AbilityExecutionBehaviour` is an abstract, avatar-local composition seam, bound through a serialized array and a canonical `AbilityDefinition` reference. Duplicate definitions, foreign-avatar behaviors and non-catalog references are configuration errors. The production array now binds Charge, Seismic Strike, Trap, Arcane Projectile, Protective Orbs, Empower, Restoration and Purification, in that order; an occupied slot without a concrete behavior still rejects before payment. Mana payment uses the separate character resource owner; no ability-local fallback pool or instant placeholder execution is supplied.

`TryPlanStart` is side-effect-free and returns an authored phase/duration plan before the all-or-nothing payment through the selected Stamina or Mana owner. `Begin` runs only after acceptance. `Simulate` requests continued execution, a preparation-to-execution transition after its original deadline, or completion; the owner alone commits phase changes. An unchanged phase never restarts its timer. `Stop` receives the accepted snapshot after the owner has cleared its active phase. `TryInterrupt(slot, sequence, reason)` accepts only a matching active execution at the authoritative forward-simulation boundary. Downed/Stun and participation closure cannot be vetoed; concrete behaviors own configured exceptions for other interruption categories. Effects already created by a future behavior remain independently owned.

On restore or runtime re-enable, `Rebind` reconstructs local references once for each bound active execution without `Begin`, payment or timer restart. Temporary participant fixup gaps preserve the copied state. Installed Fusion 2.1.1 restores the server simulation tick from the host snapshot's resume tick, so copied `TickTimer` targets remain in the same epoch: no custom timer rebase or wall-clock downtime subtraction is performed. Local state-copy fixtures verify payload/rebind behavior, not actual multi-runner Host Migration.

Authoritative consumable, object-interaction, Weapon Set-change and assisted-revive start owners consult the active-execution query for existing global restrictions. This does not block unrelated armor operations or invent primary-attack/movement compatibility. A rejected ability leaves assisted revival untouched; a valid accepted ability uses `PlayerReviveGate.InterruptIfReviving` before `Begin`, preserving the existing incompatible-action recovery contract.

Execution and already-created effects are different lifecycles. Completing/interruption of an execution does not universally delete projectiles, traps or applied buffs/debuffs. Future effect owners retain Source/Caster attribution and follow their own authored persistence rules; the base runtime does not add a general effect manager.

### Character resources

Abilities consume character resources; they do not own resource pools. Stamina remains owned by `PlayerStaminaNetworkController`, using its all-or-nothing discrete spend for initial costs. Its exhaustion, regeneration and delay semantics remain unchanged. Authoritative ability acceptance does not give Input Authority permission to commit an ability outcome, even where the existing resource controller supports predicted movement spending.

`PlayerManaNetworkController` owns expedition-local `CurrentMana` and a copied initialization marker on the existing Raid avatar. It derives Maximum Mana through `PlayerCharacter.TryGetRuntimeStatistics`: the existing base maximum plus external equipment modifiers, never another Intelligence formula or replicated maximum. It initializes once to the effective maximum only on a fresh, bidirectionally bound Raiding avatar. `CanSpend`, `TrySpend` and `TryRestore` expose full payment and explicit capped instant restoration; only State Authority can mutate them in forward Fusion simulation. Zero amounts do not bypass initialization or authority. No passive regeneration, exhaustion, persistence or ability-owned resource pool exists.

Maximum increases do not refill Current Mana; decreases clamp only an excess. Maximum is re-read at every payment/restoration so equipment changes cannot permit spending a stale excess. In an authorized operation, excess is clamped even when the requested cost or restoration is rejected; this maintenance is not partial payment. The maintenance tick runs at order -12 before the ability runtime; it performs no regeneration. Downed and revive do not write Mana. A temporarily unresolved participant freezes copied state, while terminal participation, generation/avatar replacement or Raid closure clears temporary Mana and makes the owner unavailable without allowing fresh reinitialization. Disable/re-enable preserves the balance. Despawn disposes the avatar-owned resource.

The ability runtime selects the existing Stamina owner or the Mana owner after all start preflight checks, then commits sequence, phase and cooldown only after complete payment. Unavailable Mana rejects as `ResourceUnavailable`; insufficient Mana rejects as `InsufficientResource`. The production avatar composes the Mana component, serialized references and the nine concrete behaviors listed below. `TryRestore` provides a seam only; no restoration source or consumable is implemented here.

For future periodic costs, each due payment must be complete; insufficient resource terminates the ability without partial payment or a negative balance. Periodic schedule/progress belongs to the ability execution snapshot, while the current balance belongs to the resource owner. Mana restored by another valid source may extend execution without changing those owners.

The Mana resource contract's deactivation responsibility in [Downed and Revive Architecture](DownedAndReviveArchitecture.md) is fulfilled through the ability runtime's authoritative stop boundary: it may request cessation of periodic Mana drain, but does not write ability phases or own the execution. No competing execution state is introduced.

### Embestida (charge)

`ChargeAbilityBehaviour` (`Assets/Scripts/Abilities/Behaviours/`) is the first concrete behavior. It is a Directional ability: it never selects an entity and every direction is valid to start, so a blocked trajectory does not reject the attempt.

- **Plan:** `TryPlanStart` is always valid and returns an `Executing` plan lasting `distance / speed`. An unusable configuration (non-positive or non-finite distance or speed, negative damage or knockback, empty target layers) is a configuration error, not a normal rejection: it is logged and yields an invalid plan, so the runtime rejects it as `InvalidPlan` before payment.
- **Direction:** `Begin` uses `AbilityExecutionSnapshot.AimDirection`, captured with the sequence. Later aiming never bends the path.
- **Displacement ownership:** `PlayerMovementNetworkController` owns the movement (see [Player Movement Architecture](PlayerMovementArchitecture.md), "Forced displacement"). The behavior calls `TryBeginForcedDisplacement(direction, speed)` once per accepted sequence, watches `IsForcedDisplacementActive` / `WasForcedDisplacementBlocked`, and `Stop` calls the idempotent `EndForcedDisplacement()` for every stop reason. If the begin fails, the error is logged and the next `Simulate` completes the execution.
- **Completion** (decided by the pure `ChargeRules.Decide`): the displacement is no longer active (Downed, death), an enemy was hit, the Environment blocked the motor, or the phase deadline expired. An inactive displacement wins, so a stopped charge never resolves a hit.
- **First-hit stop:** the motor does not collide with entities, so each tick `Simulate` sweeps the path the motor just applied (`LastAppliedDisplacement`) with a `Physics2D.BoxCast` sized from the caster's collider and a serialized target layer mask, using preallocated buffers. Colliders resolve to entities through `EntityRegistry` (damage colliders only) and the shared `AbilityTargetPredicate.IsValidEnemy`. The first enemy along the path wins (smallest distance, then `EntityId`); the charge never passes through it. The hit and the completion happen in the same step, once per sequence.
- **Damage and knockback:** applied through the existing pipeline with a `DamageRequest` along the charge direction and the configured knockback force. `DamageResolver` only pushes a target when the damage was applied, and `CharacterBase.ApplyDamage` rejects a non-positive amount, so a charge configured with zero damage calls `IKnockbackReceiver.ReceiveKnockback` directly instead. Damage, distance, speed, knockback force and target layers are serialized parameters owned by Balance; the defaults are not final.
- **Interruption:** `CanInterrupt` accepts `Knockback` and `Stun`; Downed, participation closure and configuration loss remain mandatory in the runtime.
- **Rebind:** re-resolves local references only. No displacement start or hit is replayed.
- **Stun is deferred:** Game Design stuns the enemy it hits, but no stun system exists. Nothing is implemented or simulated; the hit application in `ChargeAbilityBehaviour.ApplyImpact` is the extension seam.
- **Overshoot:** the phase deadline is quantized to simulation ticks, so a charge that runs its full distance may cover up to about one motor step more than configured.
- **Composition and validation boundary:** Charge is bound in `NetworkPlayer.prefab`'s execution array. Actual multi-runner Host Migration of a charge in flight remains unvalidated; the existing PlayMode coverage is rules, the single-runner cycle and local components.

### Golpe Sísmico (seismic_strike)

`SeismicStrikeAbilityBehaviour` (`Assets/Scripts/Abilities/Behaviours/`) is the first Area-from-caster behavior. It owns the radius and the effect; the target layers live on the caster's `AbilityAreaTargetFinder`, reached only through `AbilityExecutionContext.TryFindEnemiesInArea`.

- **Start validation:** `TryPlanStart` queries the area with a preallocated list. No valid enemy is a normal rejection (`BehaviourRejected`: no payment, no sequence, no cooldown). Unavailable targeting or an unusable configuration (non-positive radius or preparation, negative damage or knockback) is a configuration error: it is logged and yields an invalid plan (`InvalidPlan`). A valid start returns a `Preparing` plan of the configured seconds; `Begin` applies nothing.
- **Resolution re-query:** the start targets are never reused. When the preparation deadline expires, `Simulate` queries again around the caster's position at that moment, affects every returned enemy in the finder's deterministic order (distance, then `EntityId`) within the same tick, and returns `Idle`, which the runtime completes as `Completed`. A local last-resolved-sequence guard keeps the resolution to once per sequence. An enemy that entered the area is affected; one that left is not; with no target left the ability finishes normally and cost and cooldown are not refunded.
- **Valid enemy:** `AbilityTargetPredicate.IsValidEnemy`, so only creatures. Players are excluded until Game Design closes PvP.
- **Interruption:** `CanInterrupt` accepts `Knockback` and `Stun`; Downed, participation closure and configuration loss remain mandatory in the runtime. An interrupted preparation applies no effect and refunds neither cost nor cooldown. `Stop` has nothing to release.
- **Radial direction:** `SeismicStrikeRules.ResolveKnockbackDirection` returns the unit vector from the caster position to the target transform position. A coincident (or non-finite) position has no radial direction and uses the fixed `FallbackDirection` (up), so every simulation computes the same push.
- **Damage and knockback:** through the shared `AbilityImpact.TryApply`, the same path as Embestida: a `DamageRequest` through `IDamageResolver` when damage is positive, otherwise a direct `IKnockbackReceiver.ReceiveKnockback` (the resolver rejects non-positive amounts). Radius, damage, knockback force and preparation are serialized parameters owned by Balance (baseline preparation 0.75 s; cost and cooldown live on the definition).
- **Deferred seams:** Knockback immunity does not exist; the comment in `SeismicStrikeAbilityBehaviour.Resolve` marks where an immune target must still take damage without the push. Stun is not applied either, as no stun system exists.
- **Rebind:** re-resolves local references only. No resolution is replayed.
- **Composition and validation boundary:** Seismic Strike is bound in `NetworkPlayer.prefab`'s execution array. Actual multi-runner Host Migration of a preparation in flight remains unvalidated; the existing PlayMode coverage is rules, the single-runner cycle and local components.

### Trampa (trap)

`TrapAbilityBehaviour` (`Assets/Scripts/Abilities/Behaviours/`) is the first Automatic-position behavior and the first ability that leaves a persistent entity behind. It validates and places; the spawned `NetworkTrap` (`Assets/Scripts/Abilities/Traps/`) owns the trigger and the effect. Pure rules live in `TrapRules`.

- **Placement:** the position is `caster position + captured aim * distance` (`TrapRules.TryResolvePlacement`). `TryPlanStart` reads the aim from `AbilityExecutionContext.AimDirection`; `Begin` recomputes the position from `AbilityExecutionSnapshot.AimDirection` with the same rule, so later aiming never moves the trap. The distance is a serialized parameter.
- **Ground check:** a position is valid when a `Physics2D.OverlapCircle` of the serialized clearance radius finds no non-trigger collider on the serialized blocking mask (default `WorldCollision` and `Obstacles`, the same layers pathfinding and the weapon audio treat as obstacles; breakable props carry a `WorldCollision` blocker), and a `Physics2D.Linecast` from the caster to the position finds none either. The Floor tilemap has no collider, so floor existence itself cannot be queried. Limits: a hole or void inside the map that has no blocking collider is accepted, and a prop between the caster and the position rejects the cast even when the position itself is free. The linecast exists so a trap is never placed behind a wall, outside the map boundary. A stricter walkable-floor check would need a runtime tilemap or pathfinding grid reference on the avatar and is a follow-up.
- **Start rules:** an invalid position is the normal rejection (`BehaviourRejected`: no trap, no Stamina, no sequence, no cooldown), decided inside the side-effect-free `TryPlanStart`. An unusable configuration (`TrapRules.TryValidateConfiguration`, `TrapRules.TryValidatePlacementProbe`, missing trap prefab) is logged and yields an invalid plan (`InvalidPlan`) before payment.
- **Instant cast contract:** the plan is `Executing` with duration zero (no deadline); `Simulate` returns `Idle` and the runtime completes it. `CanInterrupt` is false for every optional reason; `Stop` and `Rebind` are no-ops that never spawn or delete a trap.
- **Spawn:** `Begin` runs on State Authority forward simulation only, once per sequence (local last-resolved-sequence guard). It first calls `NetworkTrap.TryEvictForNewTrap`, then `Runner.TrySpawn` with `InitializeNetworkState` in `onBeforeSpawned`. The caster is `PlayerCharacter.Id`. The trap is an independent entity: completing or interrupting the cast, Downed included, never deletes it.
- **Limit and lifetime:** at most `maxTrapsPerCaster` untriggered traps per caster (baseline 3). Exceeding the limit despawns the caster's trap with the least remaining lifetime (`TrapRules.SelectEvictionIndex`, first listed on a tie); other casters are untouched. A trap despawns when its networked lifetime timer ends (baseline 30 s on the ground, serialized on the behavior).
- **Trigger and effect ownership:** `NetworkTrap` waits for a valid creature through the unchanged `AbilityTargetPredicate.IsValidEnemy`, or a confirmed rival player through trap-only `TrapPlayerTargetRules`. Both paths require registry damage colliders. The caster, initial teammates, unresolved affiliations, Downed/defeated players and players protected by Purification never trigger or consume it. Rivalry requires a successful `RaidInitialAffiliationSnapshot.TryAreInitialTeammates` result of false, not merely failure to prove alliance. On trigger, State Authority forward simulation applies the concrete effect, raises `Triggered` for observers and despawns. Creature effect failure retains the existing logged-and-consumed behavior; protected players are filtered before trigger.
- **Creature effect:** unchanged `ImmobilizeEffect` holds voluntary movement and deals periodic damage through `IDamageResolver`, attributed to the caster, for its configured duration. Creature attacks, abilities and knockback remain available. Its existing purifiable flag and parameters remain independent of player immobilization.
- **Player effect (TASK-220):** avatar-owned `PlayerImmobilizationNetworkEffect` holds only a copied `ExpiryTimer`; reapplication replaces its deadline without stacking, and no creature damage ticks are applied. Duration is separately configured by `TrapAbilityBehaviour._playerImmobilizeSeconds` (default 3 s) and copied into the trap. Only voluntary walking/sprinting is blocked; attacks, abilities, consumables and forced displacement remain available. Downed or definitive defeat immediately removes the observable restriction and clears the timer on the next authoritative forward tick. The effect is always purifiable: Purification removes it and prevents reapplication while protection runs. Both mutation APIs require State Authority in forward simulation. `Spawned` rebuilds local readiness without overwriting the copied deadline, and no source EntityId remap is required. This does not broaden shared ability targeting or introduce a generic Stun/status system.
- **Failure and feedback:** `BehaviourRejected` reaches the owner through the existing channel: `TryStartExecution` sends `RPC_NotifyActivationRejected` for every failure other than `None` (`AbilityFeedbackPolicy.ShouldNotifyRejection`), the controller raises `ActivationRejected` on the owning peer and the Raid ability HUD shows its message. No new UI was added.
- **Visibility:** like the environment traps, `NetworkTrap.prefab` carries an `EntityVisibilityPresenter` bound to its sprite, so a trap outside the local vision is not rendered. Visibility is presentation only and never affects the trigger.
- **Host Migration:** `NetworkTrap` and `ImmobilizeEffect` hold only [Networked] state and are restored as dynamic objects; see [Host Migration Recovery Architecture](HostMigrationRecoveryArchitecture.md).
- **Residual risks:** if the spawn fails after payment (for example while the raid is closing), cost and cooldown stay spent and the error is logged; there is no retry. Multi-runner Host Migration of a live trap was not exercised, only the single-runner cycle. Eviction and trigger are decided on State Authority only.

### Proyectil Arcano (arcane_projectile, TASK202 #491)

`ArcaneProjectileAbilityBehaviour` prepares, then spawns the existing `NetworkProjectile` through the avatar's `FusionProjectileSpawner`. It is independent of the equipped weapon and does not alter its ranged configuration.

| Boundary | Contract |
| --- | --- |
| Acceptance | Definition owns INT 10, Mana 15 and cooldown 5 s. The side-effect-free plan validates explicit prefab/mask/spawner, finite tuning, runner registry and projectile-local resolver composition before payment. No enemy or clear trajectory is required. |
| Preparation | Baseline 0.5 s, configurable. Runtime captures the character facing at Preparing start and owns the copied phase/deadline/sequence. Movement reads a derived multiplier on every simulating peer; only voluntary movement is scaled, never forced displacement or knockback; Sprint is suppressed during execution. Other abilities default to 1. |
| Resolution | State Authority forward simulation consumes the sequence before the spawn attempt and completes in that tick. Missing input does not stall it; Begin/Rebind never spawn or restart the deadline. Spawn failure retains cost/cooldown and is not retried. |
| Interruption | Knockback/Stun and runtime-mandatory Downed/participation closure stop preparation before spawn without refund. Stop never deletes an already-spawned projectile. |
| Flight | Request explicitly opts into copied `IsUnbounded` and `EnemyOnly` modes. No lifetime timer, range clamp, huge sentinel range or caster-lifecycle cleanup. Caster/player colliders are ignored; the shared valid-enemy predicate filters creature damage hitboxes. First valid enemy or environment consumes the projectile once, without piercing; non-character world objects consume it as geometry. Existing requests default to bounded/all-damageable behavior. |
| Damage ownership | The ability projectile must compose its own existing `DamageResolver`. Its Spawned callback derives EntityRegistry and optional NetworkSpawnManager from the projectile's runner; no serialized resolver dependencies or global locator are added. The spawner's avatar resolver is not retained for unbounded flight. The original caster EntityId remains on every DamageRequest even after avatar removal. Target damage does not require a registered attacker; attacker-dependent feedback/rewards keep their existing eligibility checks and are not guaranteed after removal. |

**Composition:** Arcane is the fourth binding in NetworkPlayer's execution array, alongside Charge/Seismic Strike/Trap and now Protective Orbs. It references the canonical `arcane_projectile` definition, the existing avatar spawner, the ability-owned `Assets/Prefabs/Abilities/ArcaneProjectile.prefab` and Character/WorldCollision/Obstacles impact layers. That prefab (TASK-208) is a `PlaceholderProjectile` variant with `NetworkProjectile` and its own `DamageResolver` in the Fusion `NetworkBehaviours`, the `FusionPrefab` label, and an `EntityVisibilityPresenter` bound to its sprite, so it hides outside the local vision. It is separate from the basic ranged weapon's `MagicProjectile`, so weapon configuration cannot change the ability. Its sprite and animator are still placeholder art. No new projectile family is introduced.

**Balance:** damage 5, projectile speed 6 and preparation movement multiplier 0.5 are explicit provisional serialized defaults, not final Game Design values. Preparation/cost/cooldown retain their authored baselines and remain configurable.

**Scope and limits:** PvP tasks #441/#442 remain Planned follow-up, not a PvE blocker. Unbounded dungeon flight assumes the user-confirmed collider walls enclose the dungeon; missing/incorrect boundary colliders can leave a projectile flying until runner teardown. Full multiplayer and Host Migration evidence belong to wider US55 validation. Migration *after caster removal* remains an explicit follow-up: the unchanged restorer rejects an absent projectile-owner mapping. This implementation neither clears attribution nor claims that recovery case works. Unity composition and automated/runtime validation must be observed separately; code alone is not their proof.

### Orbes Protectores (protective_orbs, TASK203)

Instant **Self** cast with an independently simulated, avatar-owned effect. This section records the current PvE implementation, not new Game Design or final Balance. PvP is future work, explicitly outside TASK203.

#### Configuration and composition

`ProtectiveOrbsAbilityBehaviour` is the fifth entry in `NetworkPlayer.prefab`'s execution array. It references the canonical catalog definition and the same-root `ProtectiveOrbsNetworkEffect`; the effect references the same-root PlayerCharacter/DamageResolver, Character target layers and `Assets/Prefabs/Abilities/ProtectiveOrb.prefab`.

| Configuration | Current value / constraint |
| --- | --- |
| Definition | `protective_orbs`: Resistance 10, Mana 30, cooldown 25 s; unchanged |
| Initial count | 8 by default; 1..32 supported. 32 is the technical `uint` mask capacity, not a Balance limit |
| Lifetime | 15 s by default; 0 means permanent (`TickTimer.None`), still subject to consumption and lifecycle cleanup |
| Orbit radius / angular speed | Prefab: 1 unit / 30° per second; provisional. The C# speed initializer also matches 30°/s |
| Contact damage | 3 Magical per consumed orb; provisional, configurable, no knockback |
| Local orb prefab | Character-layer trigger `CircleCollider2D` (radius 0.14), `ProtectiveOrbCollider` and sprite; no NetworkObject/NetworkBehaviour |

Planning checks configuration and authoritative effect readiness before payment, without enemy search. Acceptance uses the common Mana/cooldown/sequence boundary; `Begin` activates once and `Simulate` completes the instant execution. `Stop`/`Rebind` never remove, replenish or reactivate the effect. Missing dependencies produce `InvalidPlan` before payment; a post-acceptance activation failure is logged without refund or retry.

#### State, geometry and impacts

- **Copied state:** the avatar effect owns `[Networked]` `ActiveMask`, `InitialCount`, `StartTick`, `LifetimeTimer`, orbit/contact parameters and `ActivationSequence`. Old or duplicate accepted sequences cannot refill consumed slots or restart time; a genuinely newer accepted cast replaces the effect.
- **Local pool:** `Spawned` builds 32 local child slots once. These are neither spawned NetworkObjects nor EntityRegistry entities. Inactive slots disable collider and sprite synchronously; consumed holes retain their original index. Survivors never redistribute around a smaller circle.
- **Simulation:** orbit phase is `(Runner.Tick - StartTick) * Runner.DeltaTime`, using the original count. State Authority alone commits activation, consumption, damage and cleanup in forward simulation. Authoritative geometry advances on ticks; proxies reconstruct geometry/visuals from copied state, including in `Render`, without committing gameplay.
- **Enemy contact:** after updating geometry and `Physics2D.SyncTransforms`, the effect uses a reusable 64-collider non-alloc overlap buffer per active slot. Registry damage-hitbox checks and the ordinary `AbilityTargetPredicate.IsValidEnemy` select the nearest valid creature (EntityId tie-break). Players and environment never consume contact slots; dense overlaps can exceed the buffer.
- **Consume before damage:** clear only the selected bit and disable its collider before `DamageResolver.Resolve`. Positive contact damage retains the caster's current EntityId in `DamageRequest`; different slots may each damage the same enemy. Zero damage still consumes the contacting slot.
- **Physical interception, not a full-player shield:** projectile sweeps and melee overlaps must select an actual active orb collider. `AttackTarget.ProtectiveOrb` carries the selected melee collider, deduplicated by owner; a body-only hit still damages normally. One blocked impact consumes one selected slot, bypasses damage resolution and grants no Block/Assist contribution. A consumed slot cannot block a later same-tick hit.
- **Independent hostile flight:** `NetworkProjectile.InitializeNetworkState` captures `[Networked] SourceWasPvEEnemy` from the registered source's `IAbilityEnemyTarget` affiliation. Interception uses that committed affiliation and retained source EntityId, not a live shooter lookup, so already-fired enemy projectiles remain blockable after shooter death/despawn. `Spawned` never recalculates it. Own/other-player and `EnemyOnly` player projectiles pass through orb geometry; melee/contact retain the ordinary live-enemy predicate.

#### Lifecycle and recovery boundary

Walls/environment do not consume orbs, and existing effects persist through knockback, Stun interruption requests and Downed. This is effect persistence, not a new CC implementation or permission to cast while Downed. Exhaustion of the mask, lifetime expiry, definitive defeat, successful extraction, terminal participation/avatar replacement or Raid Closing/Finished ends the effect; despawn disables local slots. A temporary unresolved participant fixup is not terminal cleanup.

Fusion `CopyStateFrom` restores the effect with its containing avatar. `Spawned` performs **no network initialization**, even for an empty/fully consumed snapshot; it rebuilds only local dependencies/pool/geometry. Original slots, start tick, deadline and activation identity remain copied, with no new EntityId fixup or separate spawned-orb restoration path. Existing projectile-owner migration limitations remain unchanged.

**Verification boundary:** `ProtectiveOrbsRulesTests` covers configuration, mask capacity/consumption and original-slot orbit math; `ProtectiveOrbsIntegrationTests` covers definition, prefab composition and Fusion fields. `ProtectiveOrbsAbilityPlayModeTests` covers instant payment/cooldown, actual projectile/melee/contact paths, dead/despawned shooters, body-only damage, no blocking Assist, Downed/defeat, expiry/permanence and real behavior-state copy without timer/refill replay. Explicit Host/Client and targeted cloud-migration fixtures are separate from ordinary tests; see [Sandbox verification](AbilitySandbox.md#protective-orbs-verification). State-copy coverage is not migration proof, and the targeted migration fixture does not validate full Raid recovery-roster closure.

### TASK-204 support abilities: Potenciar, Restauración and Purificación

This section records the current `New-Testing` implementation and the user-approved ally source and preventive-protection scope, not a generic status system or final Balance.

| Behavior / canonical definition | Current configuration |
| --- | --- |
| `EmpowerAbilityBehaviour` / `empower` | Instant area cast; radius 3, +30% basic weapon attack damage for 10 s |
| `RestorationAbilityBehaviour` / `restoration` | Radius 3; preparation 1 s; voluntary movement multiplier 0.5; flat heal 30 |
| `PurificationAbilityBehaviour` / `purification` | Radius 3; interruptible cast 0.5 s; protection 5 s |

Radii, Empower bonus/duration, Restoration heal/movement multiplier and Purification cast/protection defaults are provisional. Restoration's 1 s preparation and the existing definition requirements, Mana costs and cooldowns retain their authored baselines: Empower Mana 20 / cooldown 20 s, Restoration Mana 25 / cooldown 15 s, Purification Mana 30 / cooldown 20 s. Definitions remain immutable catalog configuration; no attribute-based heal formula is added.

**Common execution:** planning uses `TryFindAlliesInArea` before payment; no eligible target returns `BehaviourRejected`, while invalid configuration/unavailable targeting yields `InvalidPlan`. Each resolution rebuilds the area and uses an accepted-sequence guard. Missing targets at resolution do not refund payment/cooldown. Empower resolves in `Begin`, then completes; Restoration and Purification resolve when the copied preparation deadline expires. Their `Begin`/`Rebind` do not apply effects or restart preparation. Both accept Knockback/Stun interruption requests; mandatory Downed/participation teardown still belongs to the common runtime. No concrete Stun state or automatic Stunned-caster gate exists today.

**Empower:** every valid ally, including Self, receives its own `EmpowerNetworkEffect`. The recipient owns networked `ExpiryTimer` and `BonusFraction`, independent of caster and cast execution. Reapplication sets the bonus and refreshes the deadline, never adds bonuses or consumes charges; attacking/missing or changing weapons does not consume it. `Spawned` performs no fresh network initialization, preserving copied fields. Application requires a valid same-avatar owner, State Authority and forward simulation; the common ally predicate allows Downed recipients because they remain alive. Planning checks every intended recipient's effect readiness before payment, so malformed composition returns `InvalidPlan` rather than charging for a missing effect.

The multiplier is read ONLY by `PlayerCombatNetworkController` for player basic weapon attacks, not by abilities, traps, orb contact, consumables or enemy attacks. Melee reads it at actual swing release. Ranged captures scaled damage at wind-up acceptance in the copied `RangedAttackRelease`; subsequent expiry, refresh, weapon reconfiguration or flight does not change that committed damage. Ordinary strategy calls retain multiplier 1. A combat avatar without Empower capability may omit the effect; a composed Empower-capable avatar must reference its own valid effect, and a missing/foreign reference fails dependency validation. No global `DamageResolver` scaling or weapon-stat mutation is introduced.

**Restoration:** requires at least one living, non-Downed ally missing Health at start and rechecks those particular conditions at resolution. Each eligible ally receives `ApplyHealing(new HealRequest(_healAmount))` (currently 30), using the existing authoritative, maximum-capped healing path. It neither heals Downed reserve nor revives. During preparation only voluntary walking is scaled by 0.5; Sprint is suppressed during execution and forced displacement/knockback are not scaled. An interrupted preparation applies no heal and retains the spent Mana/cooldown.

**Purification:** the particular target rule is `HasPurifiableState || CanReceiveProtection`, allowing preventive use without an existing debuff. `PurificationNetworkEffect.CanReceiveProtection` reads `PlayerCharacter.CanReceiveStatusEffects` (`IsAlive && !IsDowned`), excluding Downed and defeated recipients, including Self. Accepted resolution starts/refreshes each recipient's networked `ProtectionTimer`; `Spawned` does not overwrite it. The effect is independent of the caster/execution and does not undo previously resolved damage or displacement.

`HasPurifiableState` reports the same-avatar trap-only `PlayerImmobilizationNetworkEffect`, and `PurgePurifiableStates` ends it before protection starts. The trap's creature-only `ImmobilizeEffect` is still not an allied-player cleanse path. Future purifiable player states must consult `IsProtectedFromPurifiableStates` before application, report through `HasPurifiableState` and end through `PurgePurifiableStates`; non-purifiable states ignore protection. This is a concrete immobilization cleanse, not a generic status framework or implemented Stun system. The intended Stunned-caster restriction cannot be enforced until a future authoritative Stun state/gate exists.

**Serialized composition:** `NetworkPlayer.prefab` binds the three canonical support definitions after Protective Orbs. Its root contains `EmpowerNetworkEffect` and `PurificationNetworkEffect`, both referencing the same-root `PlayerCharacter` and listed in the Fusion `NetworkBehaviours` composition. The combat controller references that same Empower effect. Support behaviors reuse the runtime's existing area finder; recipient effects are resolved from each ally. Empower checks required recipient composition at planning; failures appearing after acceptance are logged/skipped without refund or fallback. Both recipient mutation APIs reject out-of-simulation calls and foreign owners. Composition was inspected after Editor import, including all eight bindings and both effects in Fusion's baked list; this is not multiplayer validation.

**Verification:** the final focused Unity runs passed 313/313 EditMode tests and 149/149 ordinary ability PlayMode tests, including all 11 `SupportAbilitiesPlayModeTests`, with no skips. Support coverage includes rejected full-health starts without payment/cooldown, delayed capped healing, no refund after targets become invalid, refresh/expiry, malformed recipient refusal, same-root/forward-boundary guards, effect-state copying without deadline reset, real controller-driven melee damage versus unscaled `AbilityImpact`, retained ranged-acceptance damage after buff expiry, and real initial teammates entering/leaving the area during preparation. Melee uses deterministic injected target selection; ranged coverage is accepted payload retention, not controller-driven flight. Local `CopyStateFrom` coverage is not actual Host Migration proof.

**Remaining limits:** no Assist attribution guarantee is added. Host/Client, real Host Migration, support interruption/Downed and preparation-state recovery remain manual or follow-up checks. Earlier broader runs were not all green: EditMode 3621/3664 (43 failures) and a 938-test selection (12 legacy missing-prefab failures); PlayMode 181/184 (2 legacy prefab failures and a direct `DamageResolver` fatal-reward test that also fails with the HEAD prefab). The final green scoped runs do not resolve these unrelated suite failures.

### Drenaje Vital (life_drain, TASK-205)

`VitalDrainAbilityBehaviour` (`Assets/Scripts/Abilities/Behaviours/`) is a PvE-only Area-from-caster behavior with the same start/resolution shape as Golpe Sísmico. Pure rules live in `VitalDrainRules`.

- **Start validation:** no valid enemy in the area is `BehaviourRejected` (no Mana, no sequence, no cooldown). An unusable configuration, unavailable targeting or a missing `IDamageResolver` on the caster yields `InvalidPlan` before payment. A valid start returns a `Preparing` plan of the configured seconds.
- **Resolution:** when the preparation deadline expires, the area is queried again around the caster. Each valid enemy takes the flat damage through `IDamageResolver.Resolve`; only the confirmed damage of each result is summed, and the configured proportion of that total heals the caster through `ApplyHealing`, capped at missing Health (no overheal). With no target left the ability finishes normally, keeping cost and cooldown. A last-resolved-sequence guard resolves once per sequence.
- **Interruption and recovery:** `CanInterrupt` accepts `Knockback` and `Stun`; an interrupted preparation applies nothing and refunds nothing. `Begin`, `Rebind` and `Stop` never resolve, pay, heal or restart the copied deadline.
- **Configuration:** radius, preparation, damage, `DamageType` and drain proportion are independent serialized parameters. Preparation follows the Game Design baseline (1 s); radius 3, damage 5 Magical and drain 0.5 are provisional. Cost (Mana 35) and cooldown (20 s) live on the definition.
- **Composition:** bound as the ninth entry of `NetworkPlayer.prefab`'s execution array with `LifeDrainDefinition`. Players are not enemy targets until PvP targeting is closed.

## Targeting

This section is the technical mapping of Game Design "15 - Targeting de Habilidades" (TASK 210). It defines ownership and timing only; it adds no code, no gameplay rule and no numeric value. Radii and placement distances remain per-ability configuration owned by Balance and must not change a category or a validation rule.

### Categories and technical owners

| Category | Used by | Target source | Technical owner |
| --- | --- | --- | --- |
| Directional | Embestida, Proyectil Arcano | The captured facing direction; no entity is selected | Common runtime captures the direction; the concrete behavior consumes it |
| Area from caster | Golpe Sísmico, Potenciar, Restauración, Purificación, Drenaje Vital | Entities inside a radius centered on the caster | Common runtime owns the start gate and the valid-target predicate; the concrete behavior owns the radius and the effect |
| Automatic position | Trampa | A position derived from the caster and the captured facing | Common runtime captures the direction; the concrete behavior owns the distance and the ground validity check |
| Self | Orbes Protectores | The caster, implicitly; no search | Concrete behavior; the common runtime performs no target search |

The common runtime never chooses a category: the concrete `AbilityExecutionBehaviour` declares it through its authored configuration. No ability selects an entity manually, so no target-selection input, replicated target field or target lock exists.

### Aim capture

The ability runtime is the only reader of direction for abilities. Abilities follow the character facing, like melee attacks: it reads the already-networked `FacingDirection` owned by `PlayerMovementNetworkController` (see [Player Movement Architecture](PlayerMovementArchitecture.md)) and does not redefine it. The continuous cursor `AimDirection` is ignored by abilities; only bows and crossbows follow it (TASK-208 decision: the aiming system moved to facing so a cast never leaves away from where the character looks; Game Design documents 11 and 15 still say "dirección de aim"). Because the runtime runs at order -8, after the movement boundary at -10, it sees the facing of the same simulation tick. The snapshot field keeps its historical name `AimDirection`.

- **When:** once, in the simulation boundary that accepts the activation. For Proyectil Arcano this is the start of Preparing; there is no later re-adjustment before instantiation. Embestida and Trampa capture at the same acceptance point.
- **Who:** State Authority only. Input Authority transports movement and cursor through the existing input flow and never supplies an ability direction.
- **Fixed:** the captured direction is part of the accepted execution. Later aim changes, stick or cursor movement, and facing changes do not alter it.
- **Source rules:** `FacingDirection` keeps its last valid value when there is no movement, so standing still casts toward the last facing. It turns to the cursor only while an attack, interaction, accepted defense or aim stance is held, as the movement rules define. The runtime adds no fallback to the cursor. Every direction is valid to start: a blocked trajectory never rejects a directional ability, it is resolved later by the ability's collision rules.
- **Behaviors:** concrete behaviors receive the captured direction through the accepted execution snapshot (`AbilityExecutionSnapshot.AimDirection`, passed to `Begin`, `Simulate`, `Rebind` and `Stop`). They never read input, the cursor, `FacingDirection` or `AimDirection` themselves.

TASK 211 implements this capture in `PlayerAbilityRuntimeNetworkController`, which reads the avatar's `PlayerMovementNetworkController` through a serialized reference. The pure `AbilityAimResolver` normalizes `FacingDirection` with the existing `PlayerAimMath` rule, the same one melee uses. It runs after the non-mutating start checks and before `TryPlanStart` and payment: if the facing is unusable the attempt is rejected as `AimUnavailable` without payment, sequence or cooldown. The resolved direction is exposed to the behavior as a read-only copy in `AbilityExecutionContext.AimDirection` (planning, `Begin`, and, from the snapshot, `Simulate`, `Rebind` and `Stop`), so a behavior can validate against the exact direction its execution will capture. This resolver is ability-only: basic attacks keep their own direction rule (melee facing, bows and crossbows cursor) and are unaffected. The direction is written in the same boundary as the sequence, phase and cooldown, is zeroed when the execution stops, and every accepted ability receives it even if its category does not use it.

### Start validation and resolution

```text
Accepted-intent tick
  -> common preflight (phase, slot, attributes, cooldown, resource availability)
  -> resolve aim (AimUnavailable rejects here), exposed via AbilityExecutionContext.AimDirection
  -> TryPlanStart: targeting start rules, side-effect-free
  -> complete payment + sequence + cooldown + captured aim (one boundary)
  -> Begin / Simulate ... resolve: revalidate targets, apply the effect once
```

- **Start rules** run inside the side-effect-free `TryPlanStart`, before payment, using the same predicate as resolution. Area abilities require at least one valid target in the area, where Self may satisfy an ally requirement. Trampa requires a valid ground position. Directional and Self abilities have no target requirement.
- **Invalid attempt:** a failed targeting start rule is an activation failure before acceptance. It leaves resource, execution sequence and cooldown unchanged and produces the local feedback channel already used for rejections. No concrete behavior may discover a targeting failure after payment.
- **Resolution:** an area is centered on the caster's position at the resolving tick, and its targets are rebuilt then. Membership comes from the radius; line of sight is not required. An entity that enters before resolving may be affected and one that leaves is not.
- **Valid start, no targets at resolution:** the execution finishes normally without effect. Cost and cooldown are not refunded and the execution is not cancelled because its initial targets became invalid.
- **Resolve once:** the effect application is tied to the accepted execution sequence, so resimulation, rebind or Host Migration do not apply it twice.

TASK 212 implements the area query for enemies as one code path. `AbilityExecutionContext.TryFindEnemiesInArea(radius, results)` is the enemy-area entry: a concrete behavior calls it inside `TryPlanStart` (reject when the list is empty, so the attempt is a `BehaviourRejected` activation failure before payment) and again in `Simulate` at resolution, which rebuilds the targets around the caster's position at that tick. The radius is a parameter owned by the behavior; changing it never changes a validation rule. The behavior supplies its own reusable `List<AttackTarget>`; the call replaces its content and returns false (empty list) when targeting is unavailable or the radius is not a finite positive number. Area target sets are never stored or replicated.

The implementation is `AbilityAreaTargetFinder`, a plain component on `NetworkPlayer.prefab` referenced by `PlayerAbilityRuntimeNetworkController` (a missing or unconfigured finder is a configuration error in `Spawned`, like the other required references). It builds a normal `AttackTargetQuery` (zero direction, zero range, the radius, no target limit) and runs it through the avatar's existing `Physics2DAttackTargetQuery`, so identity resolution, damage-collider filtering, deduplication by `EntityId` (an entity with several hitboxes counts once) and the deterministic order (distance, then `EntityId`) are inherited unchanged. The query's reusable result list is copied into the caller's list immediately, so a melee query in the same tick cannot overwrite it. The origin is the `AttackOrigin` transform the equipped weapon uses (`PlayerCombatNetworkController`), and the serialized layer mask is the `Character` layer, where creature damage hitboxes live (the same layer as the melee target mask). Like melee, no physics synchronization is performed before the query. No raycast is made, so line of sight is never required.

### Single valid-target predicate

One predicate answers whether an entity is a valid target for a given ability at a given tick. The start rules and the resolution step call the same predicate, so they cannot diverge. It combines:

- the relationship to the caster (enemy, ally, with the caster counting as its own ally for ally abilities);
- the common state conditions of the entity (alive, Downed, Stun and similar) read from their existing authoritative owners;
- the ability's own particular conditions, supplied by the concrete behavior.

The predicate evaluates conditions; it owns no state and caches no target set. Self never bypasses a particular condition: a Downed caster is not a valid Restauración target. The intended Stunned-caster restriction for Purificación remains unimplemented until a Stun state exists.

The definition of a valid enemy, including PvP and Downed characters, belongs to Game Design and is an open dependency of US-56. PvP also remains blocked until networking provides an authoritative ally/enemy affiliation contract (see [Player Combat Architecture](PlayerCombatArchitecture.md)). This section fixes where the decision is applied, not what it decides.

TASK 212 implements the enemy case as `AbilityTargetPredicate.IsValidEnemy(casterId, damageable)`, the single rule used by the area query at start and at resolution. For now a valid enemy implements `IAbilityEnemyTarget` (`EnemyCharacter` and the sandbox dummy), is alive, can receive damage and is not the caster. Players of any team, Downed or not, are deliberately not valid targets until Game Design closes PvP and Downed targeting and an authoritative runtime affiliation contract exists; no affiliation is invented. The private enemy affiliation check remains the enemy extension point. TASK-204 adds the independent `IsValidAlly` rule without broadening enemy/PvP eligibility.

### TASK-204 ally area query

`AbilityExecutionContext.TryFindAlliesInArea(radius, List<PlayerCharacter> results)` is the support behavior entry at start and resolution. It delegates to `AbilityAreaTargetFinder.TryFindAllies`, reusing the existing physics query, origin, Character mask, damage-hitbox registry resolution and distance/EntityId order. Lists are caller-owned/reused, not replicated; no line-of-sight or room filter is added.

`AbilityTargetPredicate.IsValidAlly` requires a living candidate and either reference-equal Self or an INITIAL teammate. Because the physics query excludes the caster ID, the finder explicitly inserts valid Self first, then deduplicated teammates. Self does not require affiliation lookup but still passes alive and behavior-specific conditions. Downed is alive in the shared predicate; Restoration and Purification separately exclude it, while Empower does not.

`AbilityAllyAffiliation` uses the runner's `NetworkSpawnManager.TryGetRaidInitialAffiliations` and `RaidInitialAffiliationSnapshot.TryAreInitialTeammates`, the same source as assisted revive. Different/missing runners, missing manager/snapshot, unresolved avatar-participant links, invalid participant IDs or unknown affiliation fail closed for teammates. No current-party, proximity or inferred team substitute is used. The context returns false when its character/finder is unavailable; the finder clears results and returns false for unavailable runner/configuration, registry/query or invalid radius. Behaviors stop on false rather than reuse targets.

### State, Host Migration and rebind

The captured aim direction (implemented; Trampa derives its position from it without storing it) is committed execution context in the per-slot snapshot together with the sequence and phase (see "Per-slot state" and "Host Migration"). It is written once at acceptance, is restored by Fusion state copy and is never recomputed from the current aim after rebind or migration. Area target sets are never stored: they are always rebuilt from the predicate at resolution. `Rebind` reuses the committed direction without `Begin`, payment or recapture. Presentation observes the confirmed direction and never drives it.

### Planned extensions (described only)

The implicit Self cast is implemented by Protective Orbs without target search. TASK-204 implements allied targets and Self inclusion in caster-centered areas through the entry above. Trampa, the Automatic-position behavior, is implemented (see its section): it validates ground inside its `TryPlanStart` and recomputes the position from the committed aim at `Begin`, so no derived position is stored. Ally and Self areas feed the same predicate. Concrete behaviors consume the contract and make no structural targeting decision.

### Known gaps

- No right-stick aim action exists, so `AimDirection` comes only from the cursor; gamepad aim and neutral-stick direction retention are not implemented (see [Player Combat Architecture](PlayerCombatArchitecture.md), known limitations).
- Restricting area effects by Dungeon room or logical space is a non-blocking pending of Game Design. Until it is decided, areas use radius membership only.
- Valid enemies are creatures only. Players (any team, Downed or not) are never enemy-area targets until Game Design closes PvP and Downed targeting and networking provides an authoritative affiliation contract.
- `Physics2DAttackTargetQuery` reads at most 64 colliders per query (`_colliderBufferSize`) and silently drops the rest. Area queries share that limit, and the `Character` layer also holds player hitboxes, so a very dense area could omit valid enemies. The buffer is unchanged by TASK 212.
- As with melee, the query does not synchronize Physics2D transforms first; it observes the physics state of the last step.

## Presentation feedback signals

Presentation observes confirmed state and never advances simulation. Start, phase change and end are derived on every peer from the copied `(Sequence, Phase)` of each slot by `AbilityFeedbackTracker`, which adopts its first sample silently so restore, Host Migration, rebind and presenter reactivation never replay an old activation.

The two outcomes that are not visible in copied state reach only the owning client, as one-shot RPCs from State Authority to Input Authority: `ActivationRejected(slot, AbilityActivationFailure)` and `ExecutionInterrupted(slot, AbilityExecutionStopReason)` on `PlayerAbilityRuntimeNetworkController`. They are notifications, not state: they add no `[Networked]` field, are never read by simulation, are sent only in forward simulation, and are not restored or replayed. `AbilityFeedbackPolicy` decides which outcomes notify (every rejection; every stop except completion and participation teardown).

## Interruptions and Raid lifecycle

Live Ability Design distinguishes voluntary movement, forced movement and configured incapacitating categories. Stun blocks/interrupts character actions; already-active effects may have explicitly declared persistence exceptions. Consumables are incompatible with execution; loot/object interaction requires execution to finish; Weapon Set changes require no other action except movement. This contract does not invent concrete CC implementations or movement constraints.

| Event | Execution and effects | Cooldown and resources |
| --- | --- | --- |
| Rejected activation | No execution begins | No payment or cooldown |
| Accepted activation | Starts its configured phase, or resolves immediately | Complete initial payment; conventional cooldown begins immediately |
| Configured interruption | Stops the affected execution; independently owned effects follow their rules | Paid cost is retained; cooldown continues |
| Downed | New abilities blocked; interrupted actions do not resume. Persistent periodic-Mana abilities stop; other effects follow their declared persistence rules | Current Mana preserved; entering Downed does not reset cooldown |
| Revive / self-revive | Returns control under the existing recovery contract, not automatic execution resumption | Mana preserved; no ability cooldown reset or resource grant |
| Disconnect while retained in Dungeon | No new owner input. Existing valid execution progresses as AFK; periodic-Mana abilities stop. Independent effects are not cancelled solely by departure | Cooldowns continue in simulation; Current Mana preserved while participation remains |
| Reconnect to retained participation | Rebind to the existing runtime; do not reactivate a stopped ability or replay stale input | Preserve cooldowns and resources, not fresh initialization |
| Definitive Defeat | Stop caster-dependent execution. Independent effects may finish; corpse is not a productive ability avatar | No new activation; remaining temporary state is not persisted to Town |
| Extraction | Stop caster-dependent execution. Independent effects on entities still in Dungeon may persist | Cooldown/resource remainder does not carry to another expedition |
| Abandon / abort / Raid closure | Stop dependent execution and release runtime binding under the existing lifecycle owner; independent effects follow world cleanup | End-of-expedition reset/disposal of temporary execution and cooldown state |
| New expedition | Fresh runtime binds the newly admitted pair, not the previous Raid's execution | No prior cooldowns; new Current Mana starts at Max Mana |

**Knockback.** `PlayerMovementNetworkController.ApplyKnockbackImpulse` is the only caller. When an impulse actually applies to the player (State Authority, positive force), it calls `PlayerAbilityRuntimeNetworkController.InterruptActiveExecutions(Knockback)` on the avatar's own runtime, cached with the other sibling components. That method walks Slot1 then Slot2 and uses `TryInterrupt` for each active execution, so the forward-simulation and State Authority guards apply and each behavior's `CanInterrupt(Knockback)` decides: Embestida and Golpe Sísmico accept it, while a behavior that declares an exception keeps running. Enemies use a different motor and are never affected, and the caster is not pushed by its own Embestida or Golpe Sísmico because they only push enemies. Stun does not exist yet, so nothing calls the interruption with it.

### Existing disconnect limitation

These disconnect rules apply while the character remains in the expedition. General Active-player retention/reconnection is an external session dependency, not implemented by this architecture task. Current `NetworkSpawnManager.OnPlayerLeft` retains a Downed raider, but ordinarily despawns an Active avatar and finalizes definitive disconnect. [Raid Defeat and Spectator Architecture](RaidDefeatAndSpectatorArchitecture.md) documents the existing departure behavior and deferred reconnect policy.

Live States of the Game requires general disconnected-character retention and continued gameplay. That gap must be resolved by its owning session work before the full AFK/reconnect path can be claimed. The ability runtime must not add its own disconnect roster, reconnection budget, avatar replacement or session policy to compensate.

## Host Migration

For a fresh Raid, State Authority initializes the participant's frozen ability snapshot once after admission succeeds. For a Host Migration restore, copied Fusion state is authoritative: fresh initialization must not overwrite restored prepared identities or future ability runtime snapshots.

The replacement Host resolves definitions again through the same catalog, rebinds the restored participant/avatar relationship, and reconstructs only derived local configuration. Any future mutable runtime state that affects gameplay continuity must be part of the appropriate Fusion snapshot before that feature can claim Host Migration support.

For the future runtime, that snapshot includes the initialization marker, per-slot execution phases/sequences, execution and cooldown deadlines, and any committed specific context/progress required to avoid duplicate resolution. A future periodic behavior also snapshots its due-payment progress. The resource owners separately preserve their balances and required state; the runtime does not copy their pools into its own snapshot.

Use the existing state-copy, restore-spawn guards and reference-fixup lifecycle described in [Host Migration Recovery Architecture](HostMigrationRecoveryArchitecture.md). After participant/avatar reference fixup, the replacement Host resolves the frozen identities and resumes the copied state. Until binding is valid, it does not activate, spend, clamp/reset resources or advance unresolved executions. Restoring state must not repeat initial payment, restart cooldown/preparation, replay completed effects or run a fresh Raid bootstrap. Tick deadlines remain authoritative; local wall-clock recovery time is not a new cooldown policy.

Presentation baselines the restored confirmed sequence and state instead of replaying an old activation as new feedback. Independent future effect owners must snapshot their own lifetime/progress and remap source references where needed. The existing limitation for disconnected Downed players during migration remains a session recovery limitation, not an ability-owned recovery path.

Host Migration never reloads Town persistence, re-runs unlock acquisition, re-prepares slots, or changes ability identity from `PlayerRef`, `NetworkId`, arrival order, display data, or asset position.

## Relationship to basic weapon combat

The implemented `IAttack` strategies and `PlayerCombatNetworkController` form the current basic weapon attack flow. Their single active strategy, primary-attack input, weapon cooldown, and attack presentation sequence are not the runtime contract for character abilities.

While `HasActiveExecution` is true, basic primary/hand attacks are unavailable and rejected as
`AttackFailureReason.AbilityInProgress`, including the legacy path. Rejection neither cancels the
ability nor queues an attack, and produces no cooldown pulse. The reverse gate (ability start
during a basic attack) remains a TASK-223 follow-up to Game Design 17 §7.3.
Sprint is suppressed during ability or weapon-attack execution, reevaluated each movement tick,
without sprint speed, sprint Stamina spend or sprint-rejection telemetry. Held Sprint resumes after
execution; the gate does not change `canMove` or base movement. Movement runs before combat, so
Sprint still applies on the tick an attack starts (see `PlayerMovementArchitecture.md` §20.1).

Future abilities must not be adapted into `IAttack`, installed as the controller's active weapon strategy, or multiplexed through its cooldown and `AttackSequence`. Doing so would merge two independently prepared and independently owned domains into one state slot.

Abilities may reuse existing concrete gameplay services when their semantics match, for example:

- `IDamageResolver` for authoritative damage application;
- `IHealable` for compatible healing outcomes;
- existing movement or knockback contracts when the later ability's rules match those contracts.

Sharing a concrete service does not transfer ownership. The Ability System owns ability validation and runtime state; the damage, healing, movement, or knockback service owns its established operation.

## Failure policy

- Invalid or duplicate catalog content fails validation visibly.
- Persistent snapshots retain stable identities; missing definitions are configuration errors, not permission to invent fallback abilities.
- Rejected Town preparation is atomic and preserves the last confirmed snapshot.
- Invalid admission fails before Raid ability state is initialized.
- Missing runtime composition disables the affected ability boundary explicitly; it must not fall back to the weapon attack controller.
- No source normalizes an invalid prepared ability into a different identity.

## Deferred implementation

The following implementation belongs to later tasks; this document defines its common boundaries, not executable behavior:

- additional execution-specific replicated fields and concrete behavior dispatch beyond the nine implemented bindings;
- periodic payment schedule/progress and concrete execution-specific behavior;
- the remaining targeting contract: a walkable-floor check stricter than blocking colliders for Trampa, Downed/PvP enemy definition, and Dungeon-room restriction (aim capture and the caster-centered enemy area query with its creature-only predicate are implemented by TASK 211 and TASK 212);
- additional Mana consumers, periodic drain and Mana restoration sources (instant Mana consumers are implemented; Restoration heals Health, not Mana);
- a generic Status Effect system (Trampa owns separate creature and player immobilization effects; Purification cleanses the concrete player effect only), Assist, toggles, and a generic summon system;
- the Raid ability HUD, audio, VFX, and animation (the Town preparation UI is the Abilities tab of the Town player menu, see [Town Player Menu Architecture](TownPlayerMenuArchitecture.md): it reads the profile through `LocalProfileStore` and mutates prepared slots only through the Ready-gated `TownAbilityMutationEndpoint`);
- balance values and concrete ability content;
- execution-specific prefab composition for later abilities beyond the nine current bindings;
- general disconnected-character retention/reconnection under the session owner's contract.

Those tasks must extend this boundary rather than add a parallel catalog, persistent repertoire, prepared loadout, admission path, participant snapshot, or weapon-controller integration.

## Acceptance traceability

| TASK 417 criterion | Architecture section |
| --- | --- |
| Stable identity, immutable definition, single catalog | Static content |
| Unlocked repertoire | Persistent repertoire and preparation |
| Exactly two optional, equivalent universal slots | Two universal prepared slots |
| Attribute requirements | Design constraints; Two universal prepared slots |
| Town-to-Raid transport | Town-to-Raid flow |
| Raid ownership | Raid ownership and authority |
| Static/persistent/prepared/runtime separation | Sources of truth |
| State Authority and Host Migration rules | Raid ownership and authority |
| `LocalProfileStore`, admission, and `NetworkRaidParticipant` relationship | Sources of truth; Town-to-Raid flow; Frozen participant state |
| Basic weapon combat boundary | Relationship to basic weapon combat |
| Foundation independent of targeting or Mana implementation | Status and scope; Deferred implementation |
| No abstractions outside the MVP | Architectural boundary; Deferred implementation |

| TASK 444 criterion | Architecture section |
| --- | --- |
| One runtime owner | Future runtime owner |
| Abilities resolved exclusively from the frozen admitted pair | Town-to-Raid flow; Future runtime owner |
| Basic weapon controller remains separate; no `IAttack` adaptation | Architectural boundary; Relationship to basic weapon combat |
| Equivalent slots independent of Weapon Sets | Per-slot state |
| Authoritative, derived and presentation state identified | Sources of truth; Per-slot state |
| Cooldowns and execution during disconnect defined | Interruptions and Raid lifecycle; Existing disconnect limitation |
| State surviving Host Migration identified | Host Migration |
| Concrete abilities can extend behavior without replacing the base contract | Common runtime versus concrete behavior; Deferred implementation |

| TASK 210 criterion | Architecture section |
| --- | --- |
| Each targeting category has an explicit technical owner | Targeting: Categories and technical owners |
| Who captures aim and when is defined | Targeting: Aim capture |
| Start and resolution use the same valid-target rule | Targeting: Start validation and resolution; Single valid-target predicate |
| An invalid attempt produces no payment or cooldown | Targeting: Start validation and resolution; Activation and execution |
| Existing aim contract referenced, not duplicated | Targeting: Aim capture |
| Host Migration and rebind behavior | Targeting: State, Host Migration and rebind |
| Extension and known gaps | Targeting: Planned extensions; Known gaps |

| TASK 212 criterion | Architecture section |
| --- | --- |
| One area query built on the existing attack target detection | Targeting: Start validation and resolution |
| Single valid-enemy predicate (creature only, PvP/Downed pending) | Targeting: Single valid-target predicate; Known gaps |
| Start with no valid target is rejected without payment, sequence or cooldown | Targeting: Start validation and resolution |
| Resolution rebuilds targets around the caster's current position | Targeting: Start validation and resolution |
| No targets at resolution finishes normally without refund | Targeting: Start validation and resolution |
| No line of sight; deterministic, deduplicated result | Targeting: Start validation and resolution |
| Radius configurable without changing validation | Targeting: Start validation and resolution |

## References and validation boundary

Technical integration follows [Player Combat Architecture](PlayerCombatArchitecture.md), [Raid Participant Architecture](RaidParticipantArchitecture.md), [Downed and Revive Architecture](DownedAndReviveArchitecture.md) and [Host Migration Recovery Architecture](HostMigrationRecoveryArchitecture.md). This document does not supersede their state owners or session policies.

Gameplay rules were checked against the live owning documents: [Ability Design](https://docs.google.com/document/d/14pw5-NsV_lw4_YGj5AuPg5wJUXqg1TWm6YEozcr1sMY/edit) sections 7-11, [Ability Targeting](https://docs.google.com/document/d/1sLRrRTRbIBl0vTDwweju3hjIr-DGvcphCqBPusdirZo/edit) sections 4-9, [Mana](https://docs.google.com/document/d/1HPk4UFBF3gpVPlrpYpYveaj3GNepg_nSR_4Qy61izZI/edit) sections 4-12, [Downed and Revive](https://docs.google.com/document/d/1DQGj9THvTb0QtBdg0CT-WiMIbTI3j580ePjf-gznXGI/edit) section 5, and [States of the Game](https://docs.google.com/document/d/1j-1yZ6eiJacTdVl4EOya570zt1pvVNYtWvXHDo2orDk/edit) section 13. Those documents own gameplay intent; architecture owns the technical mapping.

TASK 444 is verified by structural readback, cross-contract consistency, acceptance traceability and a focused documentation diff. It does not prove Unity composition or runtime behavior. Later implementation must validate zero/one/two prepared slots, rejected/accepted/interrupted execution, authoritative payment, independent cooldowns, retained disconnect/reconnect, Downed/revive, terminal cleanup and migration without duplicate payment/resolution. Actual Host Migration proof requires the multi-process scenario specified by its owning architecture.

See also: [Ability Sandbox](AbilitySandbox.md), the dev-only test environment for abilities.
