# Ability System Architecture

## Status and scope

This document defines the Ability System foundation and the authoritative Raid runtime contract (TASK 417 and TASK 444). The foundation establishes identity, Town preparation and frozen Raid admission. TASK 445 implements slot binding, TASK 446 implements activation intentions and TASK 447 implements the common authoritative activation/execution/cooldown cycle. TASK 199 implements Current Mana and initial Mana payment. TASK 210 defines the targeting contract (see [Targeting](#targeting)) and TASK 211 implements its aim capture; the target predicate and concrete targeting remain deferred. Concrete abilities, Status Effects, Assist, UI, balance, toggles and summons remain deferred.

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

Exactly one ability runtime component on the productive Raid avatar owns the slot runtime and will own execution and cooldown state for both slots. `PlayerAbilityRuntimeNetworkController` implements the binding foundation; execution/cooldown transitions described below are still planned. It is separate from `PlayerCombatNetworkController`; the participant remains the stable entitlement/result owner and does not execute abilities.

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

`AbilityExecutionBehaviour` is an abstract, avatar-local composition seam, bound through a serialized array and a canonical `AbilityDefinition` reference. Duplicate definitions, foreign-avatar behaviors and non-catalog references are configuration errors. The production array is deliberately empty: an occupied slot without a concrete behavior rejects before payment. Mana payment uses the separate character resource owner; no ability-local fallback pool or instant placeholder execution is supplied.

`TryPlanStart` is side-effect-free and returns an authored phase/duration plan before the all-or-nothing payment through the selected Stamina or Mana owner. `Begin` runs only after acceptance. `Simulate` requests continued execution, a preparation-to-execution transition after its original deadline, or completion; the owner alone commits phase changes. An unchanged phase never restarts its timer. `Stop` receives the accepted snapshot after the owner has cleared its active phase. `TryInterrupt(slot, sequence, reason)` accepts only a matching active execution at the authoritative forward-simulation boundary. Downed/Stun and participation closure cannot be vetoed; concrete behaviors own configured exceptions for other interruption categories. Effects already created by a future behavior remain independently owned.

On restore or runtime re-enable, `Rebind` reconstructs local references once for each bound active execution without `Begin`, payment or timer restart. Temporary participant fixup gaps preserve the copied state. Installed Fusion 2.1.1 restores the server simulation tick from the host snapshot's resume tick, so copied `TickTimer` targets remain in the same epoch: no custom timer rebase or wall-clock downtime subtraction is performed. Local state-copy fixtures verify payload/rebind behavior, not actual multi-runner Host Migration.

Authoritative consumable, object-interaction, Weapon Set-change and assisted-revive start owners consult the active-execution query for existing global restrictions. This does not block unrelated armor operations or invent primary-attack/movement compatibility. A rejected ability leaves assisted revival untouched; a valid accepted ability uses `PlayerReviveGate.InterruptIfReviving` before `Begin`, preserving the existing incompatible-action recovery contract.

Execution and already-created effects are different lifecycles. Completing/interruption of an execution does not universally delete projectiles, traps or applied buffs/debuffs. Future effect owners retain Source/Caster attribution and follow their own authored persistence rules; the base runtime does not add a general effect manager.

### Character resources

Abilities consume character resources; they do not own resource pools. Stamina remains owned by `PlayerStaminaNetworkController`, using its all-or-nothing discrete spend for initial costs. Its exhaustion, regeneration and delay semantics remain unchanged. Authoritative ability acceptance does not give Input Authority permission to commit an ability outcome, even where the existing resource controller supports predicted movement spending.

`PlayerManaNetworkController` owns expedition-local `CurrentMana` and a copied initialization marker on the existing Raid avatar. It derives Maximum Mana through `PlayerCharacter.TryGetRuntimeStatistics`: the existing base maximum plus external equipment modifiers, never another Intelligence formula or replicated maximum. It initializes once to the effective maximum only on a fresh, bidirectionally bound Raiding avatar. `CanSpend`, `TrySpend` and `TryRestore` expose full payment and explicit capped instant restoration; only State Authority can mutate them in forward Fusion simulation. Zero amounts do not bypass initialization or authority. No passive regeneration, exhaustion, persistence or ability-owned resource pool exists.

Maximum increases do not refill Current Mana; decreases clamp only an excess. Maximum is re-read at every payment/restoration so equipment changes cannot permit spending a stale excess. In an authorized operation, excess is clamped even when the requested cost or restoration is rejected; this maintenance is not partial payment. The maintenance tick runs at order -12 before the ability runtime; it performs no regeneration. Downed and revive do not write Mana. A temporarily unresolved participant freezes copied state, while terminal participation, generation/avatar replacement or Raid closure clears temporary Mana and makes the owner unavailable without allowing fresh reinitialization. Disable/re-enable preserves the balance. Despawn disposes the avatar-owned resource.

The ability runtime selects the existing Stamina owner or the Mana owner after all start preflight checks, then commits sequence, phase and cooldown only after complete payment. Unavailable Mana rejects as `ResourceUnavailable`; insufficient Mana rejects as `InsufficientResource`. The production avatar composes the Mana component and serialized references, but concrete behavior composition remains a later task. `TryRestore` provides a seam only; no restoration source or consumable is implemented here.

For future periodic costs, each due payment must be complete; insufficient resource terminates the ability without partial payment or a negative balance. Periodic schedule/progress belongs to the ability execution snapshot, while the current balance belongs to the resource owner. Mana restored by another valid source may extend execution without changing those owners.

The Mana resource contract's deactivation responsibility in [Downed and Revive Architecture](DownedAndReviveArchitecture.md) is fulfilled through the ability runtime's authoritative stop boundary: it may request cessation of periodic Mana drain, but does not write ability phases or own the execution. No competing execution state is introduced.

## Targeting

This section is the technical mapping of Game Design "15 - Targeting de Habilidades" (TASK 210). It defines ownership and timing only; it adds no code, no gameplay rule and no numeric value. Radii and placement distances remain per-ability configuration owned by Balance and must not change a category or a validation rule.

### Categories and technical owners

| Category | Used by | Target source | Technical owner |
| --- | --- | --- | --- |
| Directional | Embestida, Proyectil Arcano | The captured aim direction; no entity is selected | Common runtime captures the direction; the concrete behavior consumes it |
| Area from caster | Golpe Sísmico, Potenciar, Restauración, Purificación, Drenaje Vital | Entities inside a radius centered on the caster | Common runtime owns the start gate and the valid-target predicate; the concrete behavior owns the radius and the effect |
| Automatic position | Trampa | A position derived from the caster and the captured aim | Common runtime captures the direction; the concrete behavior owns the distance and the ground validity check |
| Self | Orbes Protectores | The caster, implicitly; no search | Concrete behavior; the common runtime performs no target search |

The common runtime never chooses a category: the concrete `AbilityExecutionBehaviour` declares it through its authored configuration. No ability selects an entity manually, so no target-selection input, replicated target field or target lock exists.

### Aim capture

The ability runtime is the only reader of aim for abilities. It reads the already-networked `AimDirection` owned by `PlayerMovementNetworkController` (see [Player Movement Architecture](PlayerMovementArchitecture.md), "Aim direction", and [Player Combat Architecture](PlayerCombatArchitecture.md), "Shared aim direction") and does not redefine it. Because the runtime runs at order -8, after the movement boundary at -10, it sees the aim of the same simulation tick.

- **When:** once, in the simulation boundary that accepts the activation. For Proyectil Arcano this is the start of Preparing; there is no later re-adjustment before instantiation. Embestida and Trampa capture at the same acceptance point.
- **Who:** State Authority only. Input Authority transports the cursor through the existing input flow and never supplies an ability aim.
- **Fixed:** the captured direction is part of the accepted execution. Later aim changes, stick or cursor movement, and facing changes do not alter it.
- **Source rules:** an unusable aim (no aim sentinel, non-finite or near-zero) follows the existing `AimDirection` semantics, which keep the previous valid aim. The runtime adds no second fallback. Every direction is valid to start: a blocked trajectory never rejects a directional ability, it is resolved later by the ability's collision rules.
- **Behaviors:** concrete behaviors receive the captured direction through the accepted execution snapshot (`AbilityExecutionSnapshot.AimDirection`, passed to `Begin`, `Simulate`, `Rebind` and `Stop`). They never read input, the cursor, `FacingDirection` or `AimDirection` themselves.

TASK 211 implements this capture in `PlayerAbilityRuntimeNetworkController`, which reads the avatar's `PlayerMovementNetworkController` through a serialized reference. The pure `AbilityAimResolver` reuses the existing `PlayerAimMath` rule (continuous aim, falling back to `FacingDirection`). It runs after the non-mutating start checks and before payment: if neither direction is usable the attempt is rejected as `AimUnavailable` without payment, sequence or cooldown. The direction is written in the same boundary as the sequence, phase and cooldown, is zeroed when the execution stops, and every accepted ability receives it even if its category does not use it.

### Start validation and resolution

```text
Accepted-intent tick
  -> common preflight (phase, slot, attributes, cooldown, resource availability)
  -> TryPlanStart: targeting start rules, side-effect-free
  -> complete payment + sequence + cooldown + captured aim (one boundary)
  -> Begin / Simulate ... resolve: revalidate targets, apply the effect once
```

- **Start rules** run inside the side-effect-free `TryPlanStart`, before payment, using the same predicate as resolution. Area abilities require at least one valid target in the area, where Self may satisfy an ally requirement. Trampa requires a valid ground position. Directional and Self abilities have no target requirement.
- **Invalid attempt:** a failed targeting start rule is an activation failure before acceptance. It leaves resource, execution sequence and cooldown unchanged and produces the local feedback channel already used for rejections. No concrete behavior may discover a targeting failure after payment.
- **Resolution:** an area is centered on the caster's position at the resolving tick, and its targets are rebuilt then. Membership comes from the radius; line of sight is not required. An entity that enters before resolving may be affected and one that leaves is not.
- **Valid start, no targets at resolution:** the execution finishes normally without effect. Cost and cooldown are not refunded and the execution is not cancelled because its initial targets became invalid.
- **Resolve once:** the effect application is tied to the accepted execution sequence, so resimulation, rebind or Host Migration do not apply it twice.

### Single valid-target predicate

One predicate answers whether an entity is a valid target for a given ability at a given tick. The start rules and the resolution step call the same predicate, so they cannot diverge. It combines:

- the relationship to the caster (enemy, ally, with the caster counting as its own ally for ally abilities);
- the common state conditions of the entity (alive, Downed, Stun and similar) read from their existing authoritative owners;
- the ability's own particular conditions, supplied by the concrete behavior.

The predicate evaluates conditions; it owns no state and caches no target set. Self never bypasses a particular condition: a Downed caster is not a valid Restauración target and a Stunned caster cannot self-execute Purificación.

The definition of a valid enemy, including PvP and Downed characters, belongs to Game Design and is an open dependency of US-56. PvP also remains blocked until networking provides an authoritative ally/enemy affiliation contract (see [Player Combat Architecture](PlayerCombatArchitecture.md)). This section fixes where the decision is applied, not what it decides.

### State, Host Migration and rebind

The captured aim direction (implemented) and, when it applies, a derived Trampa position (planned) are committed execution context in the per-slot snapshot together with the sequence and phase (see "Per-slot state" and "Host Migration"). They are written once at acceptance, are restored by Fusion state copy and are never recomputed from the current aim after rebind or migration. Area target sets are never stored: they are always rebuilt from the predicate at resolution. `Rebind` reuses the committed direction without `Begin`, payment or recapture. Presentation observes the confirmed direction and never drives it.

### Planned extensions (described only)

Trampa placement, Self and allied targets extend this contract without changing it. Trampa adds a ground validity check inside its `TryPlanStart` and a computed position in its committed context. Ally and Self areas feed the same predicate. Concrete behaviors consume the contract and make no structural targeting decision.

### Known gaps

- No right-stick aim action exists, so `AimDirection` comes only from the cursor; gamepad aim and neutral-stick direction retention are not implemented (see [Player Combat Architecture](PlayerCombatArchitecture.md), known limitations).
- Restricting area effects by Dungeon room or logical space is a non-blocking pending of Game Design. Until it is decided, areas use radius membership only.

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

- execution-specific replicated fields and concrete behavior dispatch beyond the implemented slot-binding component;
- periodic payment schedule/progress and concrete execution-specific behavior;
- implementation of the targeting contract (aim capture, start validation, the shared valid-target predicate and resolution-time revalidation);
- concrete Mana consumers and restoration sources;
- Status Effects, Assist, toggles, summons, and persistent spawned effects;
- the Raid ability HUD, audio, VFX, and animation (the Town preparation UI is the Abilities tab of the Town player menu, see [Town Player Menu Architecture](TownPlayerMenuArchitecture.md): it reads the profile through `LocalProfileStore` and mutates prepared slots only through the Ready-gated `TownAbilityMutationEndpoint`);
- balance values and concrete ability content;
- execution-specific prefab composition beyond the implemented slot-binding references;
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

## References and validation boundary

Technical integration follows [Player Combat Architecture](PlayerCombatArchitecture.md), [Raid Participant Architecture](RaidParticipantArchitecture.md), [Downed and Revive Architecture](DownedAndReviveArchitecture.md) and [Host Migration Recovery Architecture](HostMigrationRecoveryArchitecture.md). This document does not supersede their state owners or session policies.

Gameplay rules were checked against the live owning documents: [Ability Design](https://docs.google.com/document/d/14pw5-NsV_lw4_YGj5AuPg5wJUXqg1TWm6YEozcr1sMY/edit) sections 7-11, [Ability Targeting](https://docs.google.com/document/d/1sLRrRTRbIBl0vTDwweju3hjIr-DGvcphCqBPusdirZo/edit) sections 4-9, [Mana](https://docs.google.com/document/d/1HPk4UFBF3gpVPlrpYpYveaj3GNepg_nSR_4Qy61izZI/edit) sections 4-12, [Downed and Revive](https://docs.google.com/document/d/1DQGj9THvTb0QtBdg0CT-WiMIbTI3j580ePjf-gznXGI/edit) section 5, and [States of the Game](https://docs.google.com/document/d/1j-1yZ6eiJacTdVl4EOya570zt1pvVNYtWvXHDo2orDk/edit) section 13. Those documents own gameplay intent; architecture owns the technical mapping.

TASK 444 is verified by structural readback, cross-contract consistency, acceptance traceability and a focused documentation diff. It does not prove Unity composition or runtime behavior. Later implementation must validate zero/one/two prepared slots, rejected/accepted/interrupted execution, authoritative payment, independent cooldowns, retained disconnect/reconnect, Downed/revive, terminal cleanup and migration without duplicate payment/resolution. Actual Host Migration proof requires the multi-process scenario specified by its owning architecture.
