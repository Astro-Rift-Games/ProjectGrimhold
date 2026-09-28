# Player Combat Architecture

## Shared aim direction

`PlayerMovementNetworkController` resolves locomotion-facing after the player's kinematic
displacement, then lets a valid cursor direction override it only for a same-tick
`PrimaryAttack` or `Interact` intent. It writes the single synchronized
`FacingDirection` and runs before `PlayerCombatNetworkController` in every Fusion
simulation tick. Melee and ranged both validate and consume that same finite, normalized
contextual facing; combat does not recompute aim from cursor input or `_attackOrigin`.

`_attackOrigin` remains the physical `AttackRequest.Origin`. `LastAttackDirection` is
not continuous aim state: it is replicated only after a strategy successfully executes,
together with the attack origin, type, tick and sequence for presentation.

This document describes the design, components, network authority, data contracts, and simulation mechanics of the Player Combat System in Project Grimhold.

## Architectural Overview

The combat system is built on a modular, strategy-based architecture designed to support deterministic and authoritative multiplayer combat using **Photon Fusion 2.1** in Host/Client mode. It separates:
1. Input capture and transport.
2. Network boundaries and state tracking.
3. Attack execution strategies (Melee and Ranged).
4. Projectile spawning and physical simulation.
5. Entity registration and collision resolution.

```text
PlayerInputReader (Local Input)
   │
   ▼
FusionInputProvider (Transport)
   │
   ▼
PlayerCombatNetworkController (Network Boundary)
   │
   ├── [AttackSequence, Cooldown Timer, HasActiveAttack]
   ├── PlayerShieldDefenseNetworkController
   │      └── [IsDefending, active Off Hand shield, frontal coverage]
   ▼
Optional Active Strategy (IAttack: MeleeAttack / RangedAttack)
   │
   ▼ [Ranged Strategy]
FusionProjectileSpawner (Network Spawner)
   │
   ▼
NetworkProjectile (Authoritative Simulation) ──► EntityRegistry & IDamageResolver
```

---

## Key Components

### 1. Data Contracts and Interface Definitions (`IAttack`)
The current basic weapon attack strategies implement this contract:
* **`IAttack`**: Interface defining the execution strategy for the active basic weapon attack.
  * `AttackType Type { get; }` (Melee, Ranged, etc.)
  * `float CooldownSeconds { get; }`
  * `AttackInputMode InputMode { get; }` (Press or Hold)
  * `AttackResult Execute(in AttackRequest request)`
* **`AttackRequest`**: Encapsulates attacker context:
  * `EntityId AttackerId` (resolved from `CharacterBase`)
  * `Vector2 Origin`: The world-space attack origin provided by the character combat controller. For ranged attacks, the final projectile origin may include the configured spawn offset.
  * `Vector2 Direction` (normalized shoot direction)
  * `int SimulationTick` (the exact Fusion tick of execution)
* **`AttackResult`**: Captures execution success or detailed failure reasons (Cooldown, MissingConfiguration, InvalidDirection).

### 2. Network Controller (`PlayerCombatNetworkController`)
Serves as the network boundary for the current basic weapon attack flow:
* Owns the current basic weapon attack flow; it is not the runtime contract or owner for character abilities.
* Extends `NetworkBehaviour` and processes combat input during Fusion simulation ticks.
* Only State Authority validates and executes attacks.
* Listens to player input commands (e.g., `PrimaryAttack` button and `AimWorldPosition`).
* Synchronizes `AttackSequence` using a `[Networked]` state variable to ensure clients replicate visual presentation smoothly.
* Handles combat cooldowns authoritatively via network tick timers (`TickTimer`).
* Represents strategy presence through the authoritative `[Networked]` `HasActiveAttack` state, independently from `IsAttackEnabled`.
* Delegates execution to the local `IAttack` only when both authoritative presence and a valid local implementation exist.
* Stores the duration associated with the attack that started the current cooldown so proxies can present it without resolving the concrete strategy.

An absent strategy is a valid neutral gameplay state. Primary-attack input still advances
the replicated button history but produces no execution, cooldown, sequence, rejection, or
feedback. `TrySetActiveAttack` and `TryClearActiveAttack` are State-Authority-only operations.
Neither operation changes a pending cooldown or its recorded duration, preventing strategy
changes from bypassing recovery time. Runtime dependency caching never discovers a replacement
strategy implicitly after an explicit clear.

On a fresh State Authority spawn, strategy presence is initialized from the serialized source
only after it resolves as `IAttack`; cooldown, cooldown duration, and attack enablement receive
their explicit fresh baselines. A spawn restored by `HostMigrationRestoreUtility.IsRestoreSpawn`
does not overwrite those networked snapshots.

`PlayerWeaponEquipmentNetworkController` is the single authoritative source of the Raid avatar's
Equipment. It owns two Weapon Sets (`Main Hand + Off Hand`) plus `Helmet`, `Armor`, `Gloves`
and `Boots`, and one active Set selection that may only reference a Set with an occupied Main Hand. Each
slot replicates only the deterministic `LootDefinitionCatalog` index plus one (`0` means empty).
Every Equip intention names its exact `EquipmentSlot`. One-handed weapons may occupy either hand;
two-handed weapons may occupy only Main Hand and derive the blocked state of the matching Off Hand.
Equipping a two-handed weapon displaces both hands in that Set, while the other Set is untouched.
Unequip intentions identify one slot and return exactly one unit to `PlayerLootReceiver`.
Input Authority expresses those discrete intentions and State Authority validates and commits them
during `FixedUpdateNetwork`. Before writing, State Authority simulates extraction of the incoming
unit and return of every displaced unit, validates capacity, ownership, provenance, catalog and
attribute requirements, then commits the exchange. A rejected operation mutates neither Inventory,
Equipment, provenance, revision, active Set nor attack strategy.

Slot compatibility lives in `EquipmentSlotRules`, not in Loot. `LootCategory` only classifies the
unit (`Weapon`, `Shield`, `Helmet`, `Armor`, `Gloves`, `Boots`); deciding which slot may receive it is an
Equipment rule. `PlayerLootReceiver` is never the source of truth for what is equipped.

Only the Main Hand of the active Weapon Set resolves `LootDefinition -> WeaponDefinition -> AttackConfig` together
with the participant's effective `CharacterAttributeState`. Equipment adapts the current legacy
`WeaponOffensiveScaling` to resolved runtime contributions, calculates effective damage through the
single canonical `WeaponDamageCalculator`, and builds a
local, non-replicated `AttackExecutionParameters` value from the active weapon's damage, type,
interval, effective range and knockback, configures the shared `MeleeAttack` or `RangedAttack`
executor, and assigns it
through `TrySetActiveAttack`. Inserting an inactive weapon never reconfigures either executor.
Armor changes do not rebuild the weapon strategy or disturb the authoritative cooldown; their
`EquipmentRevision` change invalidates the separate local armor-statistics projection. Slot-selection input travels in the normal
`PlayerNetworkInput` buttons; it uses no RPC and preserves the authoritative cooldown. Any mutation
of any of the eight slots advances `EquipmentRevision`, which is what presentation observes.

Equipment also observes the participant's effective-attribute revision. A testing override therefore
revalidates the active weapon requirement and rebuilds its local attack parameters under State Authority;
the same effective snapshot supplies both the requirement and the scaling calculation. If a decrease
makes the active weapon invalid, the existing authoritative invalid-active-weapon path clears the active
selection without removing the equipped unit.

On Host Migration restore, State Authority resolves the replicated slot identities and the active
slot again, rebuilding the strategy and recalculating effective damage from the restored effective
attributes without replaying equipment requests. Effective damage, equipment modifiers and final
player runtime statistics are derived local state and are not replicated or persisted. The armor
slots need no dedicated restore logic — they are ordinary `[Networked]` properties. Restored slots,
`EquipmentRevision`, effective attributes and active Weapon Set reconstruct the same projections.
ScriptableObjects and presentation state are never replicated.

`TryGetPrimaryAttackStatus` returns no presentable state while `HasActiveAttack` is false.
When presence is authoritative, the query does not require a local `IAttack`; it derives
availability from `IsAttackEnabled`, `ICharacter.IsAlive`, and the replicated cooldown, and
reports the replicated duration snapshot from the attack that started that timer. This allows
proxies to present the same cooldown without knowing strategy identity. `RaidHudPresenter`
clears its attack presentation whenever the query returns `false`, so a neutral player cannot
retain a stale weapon cooldown in the HUD.

### 3. Sustained shield defense (`PlayerShieldDefenseNetworkController`)

`SecondaryAction` is a continuous local intention bound to the right mouse button and transported
inside the normal `PlayerNetworkInput.Buttons` snapshot. State Authority derives the replicated
`IsDefending` value every simulation tick; clients never request or author defensive state through
an RPC. Missing input clears the state.

Equipment remains the only source of truth for shield availability. `Shield` is an equippable Loot
category compatible exclusively with the Off Hand slots. A valid shield is not a weapon, has no
`WeaponDefinition`, never resolves an `IAttack`, and has no attribute-requirement check. Only the
active Weapon Set's Off Hand can sustain defense. The state also requires a living character and an
active gameplay phase, so releasing input, changing Set, removing or displacing the shield, defeat,
phase exit, or loss of input cancels defense on the next authoritative tick. Host Migration keeps a
restored state only when those reconstructed conditions remain compatible.

Defense has priority over attack when both intentions are present in the same tick. Movement and
the normal locomotion-facing flow continue while defending; `SecondaryAction` alone does not
enable a cursor-facing override. `PlayerCombatNetworkController`
does not execute either Press or Hold attacks while the current conditions accept the
secondary-action intention.

`ShieldDefenseMath` owns the deterministic coverage calculation. It safely normalizes the player's
logical `IMovementState.FacingDirection` and the direction from the player toward the impact origin,
which is the inverse of `DamageRequest.Direction`. The impact is covered when their dot product is
at least the cosine of half the configured cone. The Training Shield configures a `120` degree total
cone, therefore `+60` and `-60` degrees are included. A zero, non-finite, or otherwise unsafe input
direction fails open and receives no shield mitigation.

`PlayerCharacter` applies mitigation in this order:

```text
DamageRequest.Amount
   -> passive Equipment armor mitigation
   -> active directional shield reduction
   -> final Health subtraction
```

Physical and Magical damage covered by the Training Shield are reduced by `50%` after armor. True
Damage returns before both passive armor and shield evaluation. Shield defense does not modify the
`DamageRequest`, knockback, damage feedback, or presentation contracts. `IsDefending` is the stable
replicated read model reserved for the separate six-direction shield presentation work.

### 4. Melee Attack Strategy (`MeleeAttack` & `MeleeAttackConfig`)
Executes instant damage detection in a localized area:
* Reads radius, maximum targets and target mask from `MeleeAttackConfig`.
* Receives damage, type, interval, effective range and knockback through `AttackExecutionParameters`.
* Converts effective weapon range into the query's circle-center offset as `Range - Radius` and rejects `Range < Radius`.
* Passes damage requests directly to the centralized `IDamageResolver`.

### 5. Ranged Attack Strategy (`RangedAttack` & `RangedAttackConfig`)
Generates physical projectiles that traverse the world:
* Reads projectile prefab, speed, lifetime, spawn offset and impact mask from `RangedAttackConfig`.
* Receives damage, type, interval, maximum range and knockback through `AttackExecutionParameters`.
* Integrates a configurable **`ProjectileSpawnOffset`**, configured according to the combined collision bounds of the shooter and projectile, which offsets the initial projectile spawn coordinate in the direction of the aim vector to clear the shooter's own collider bounds.
* Delegates spawning requests to an `IProjectileSpawner` instance.

### 6. Projectile Simulation (`NetworkProjectile`)
Represents a networked projectile whose gameplay simulation is executed exclusively by State Authority.
* **Authority-only simulation**: Movement, collision queries, damage resolution, range validation, lifetime expiration, and despawn decisions occur only on State Authority. Proxy instances receive replicated state for presentation.
* **Kinematic movement**: Uses a kinematic `Rigidbody2D` and advances using `Runner.DeltaTime`, avoiding non-authoritative collision responses or forces.
* **Continuous collision detection**: Casts the projectile's collider across the requested tick displacement so targets cannot be skipped between ticks.
* **Physical initialization**: Aligns the Rigidbody2D, Transform, and networked spawn position before the first authoritative simulation step.
* **Physics synchronization**: When required by manually updated transforms, synchronizes the Unity 2D physics state before performing the collider cast.
* **Impact selection and owner filtering**: Chooses the nearest cast hit. Registered owner colliders and registered non-damage colliders are ignored; an unregistered collider in the impact mask is treated as blocking world geometry.
* **Single-impact guarantee**: Consumes an accepted impact before applying damage or requesting despawn, preventing duplicated damage across subsequent ticks or multiple overlapping cast results.
* **Range and lifetime**: Tracks the exact traveled distance and a network `TickTimer`. The final movement step is clamped so the projectile never exceeds its configured maximum range.
* **Obstacle behavior**: A collider without a registered damageable entity still blocks and despawns the projectile but does not produce a damage request. A wall blocks/despawns the projectile without damage.
* **Collision volume**: The projectile prefab defines a gameplay collider whose effective world-space size is independent from unintended visual scaling. The current implementation validates or adjusts the CircleCollider2D radius during initialization to prevent prefab scale from producing an oversized world-space collision volume.

### 7. Entity Identity (`EntityRegistry`)
A fast-lookup database mapping physical colliders (`Collider2D`) to gameplay entity identities (`EntityId`) and damageable contracts (`IDamageable`):
* Allows the projectile simulation to instantly identify targets without expensive `GetComponent` searches.
* Enables precise owner filtering by checking `BelongsToOwner(Collider2D)`, ensuring a projectile never collides with its shooter or any of its child-objects, while allowing impacts against other players/enemies.
* Keeps explicit damage-collider registration separate from general collider identity. Damage queries use the explicit set when present and retain all-collider fallback behavior for legacy or non-character damageables.

### 8. Fatal PvE Kill Experience

An authoritative `DamageResult` with both `IsApplied` and `IsFatal` identifies the unique Last
Hit used by the PvE Kill Experience producer. `DamageResolver` resolves the target's independent
`IKillExperienceSource` and the attacker's current stable Raid participation, then invokes the
source synchronously. The source requests ledger application before setting its replicated
one-shot flag, so rejected rewards remain available and accepted deaths cannot reward twice.
This direct relationship introduces no gameplay event, RPC, transaction service or coordinator.
PvP remains blocked until networking provides an authoritative ally/enemy affiliation contract.

---

## Implementation Status

| Component | Status | Responsibility | Notes |
| :--- | :--- | :--- | :--- |
| **`PlayerInputReader`** | Fully Implemented | Captures local buttons/aim and packs into `PlayerNetworkInput`. | Relies on local Unity input wrappers. |
| **`PlayerCombatNetworkController`** | Fully Implemented | Handles network input, authoritative optional strategy presence, TickTimer cooldowns, and local strategies. | Structural dependencies remain required; an attack strategy is optional. |
| **`PlayerShieldDefenseNetworkController`** | Implemented | Derives replicated sustained defense and evaluates the active shield's frontal coverage. | Six-direction shield presentation remains in TASK-329. |
| **`MeleeAttack`** | Fully Implemented | Melee execution strategy, queries targets, resolves damage. | Behavior comes from `MeleeAttackConfig`; resolved statistics come from `AttackExecutionParameters`. |
| **`Physics2DAttackTargetQuery`** | Fully Implemented | Circular target query with `Physics2D.OverlapCircle`. | Uses `_colliderBuffer` to avoid heap allocations. |
| **`RangedAttack`** | Fully Implemented | Ranged execution strategy, spawns projectile via `IProjectileSpawner`. | Translates input to `ProjectileSpawnRequest`. |
| **`FusionProjectileSpawner`** | Fully Implemented | Replicated network spawning via `Runner.TrySpawn`. | State Authority validated. |
| **`NetworkProjectile`** | Fully Implemented | Replicated kinematic projectile movement and casting queries. | Uses `ImpactConsumed` state to guarantee single damage. |
| **`DamageResolver`** | Fully Implemented | Route `DamageRequest` to target and returns `DamageResult`. | Resolves target from `EntityRegistry`. |
| **`EntityRegistry`** | Fully Implemented | Shared registry mapping `Collider2D` to `EntityId` and `IDamageable`. | Must be present on the same GameObject as the NetworkRunner. |
| **`CharacterBase`** | Fully Implemented | Base abstract character class handling health and damage resolution. | Inherited by players and enemies. |

---

## Combat Configuration

Gameplay properties are separated into stable configurations and dynamic network state:

### 1. `AttackConfig` and `MeleeAttackConfig` (ScriptableObjects)
`AttackConfig` owns only `_inputMode`. `MeleeAttackConfig` adds reusable execution behavior:
* **`_radius`** (float, Min: 0.1): Detection circle radius.
* **`_maximumTargets`** (int, Min: 1): Maximum number of targets hit in one execute.
* **`_targetLayerMask`** (LayerMask): Layer mask defining which objects are queried.

### 2. `RangedAttackConfig` (ScriptableObject)
Inherits the input mode and owns only reusable projectile behavior:
* **`_projectileSpeed`** (float, Min: 0.1): Travel speed of the spawned projectile.
* **`_lifetimeSeconds`** (float, Min: 0.1): Duration before projectile expires.
* **`_projectileSpawnOffset`** (float, Min: 0.0): Distance in front of the attacker origin where the projectile spawns.
* **`_projectilePrefab`** (NetworkPrefabRef): Fusion registered prefab reference.
* **`_impactLayerMask`** (LayerMask): Collision mask including both target characters and blocking obstacle walls.

### 3. Equipment template configuration, runtime parameters and scaling

`LootDefinition` is the static template and catalog identity of an item. A weapon template
references `WeaponDefinition`; an armor template references `ArmorDefinition`. These referenced
objects are functional configurations, not competing template identities. Presentation remains a
separate `EquipmentVisualDefinition` concern.

`WeaponDefinition` owns player weapon `BaseDamage`, `AttackIntervalSeconds`, effective `Range`,
`StaminaCost`, `DamageType`, `KnockbackForce`, handedness, requirements and natural scaling
attribute. `ArmorDefinition` owns integer Physical Defense, Magical Defense and one flat maximum
Health, Stamina or Mana modifier. `EquipmentStatisticsCalculator` rebuilds a complete immutable
`EquipmentStatisticsModifiers` snapshot from the four equipped armor definitions whenever
`EquipmentRevision` changes; it never accumulates deltas. `StaminaCost` is validated weapon
configuration but is not consumed yet. Bow, wand and staff templates reuse the shared ranged
behavior; weapon-specific projectile manifestation remains outside this contract.

The canonical future instance contract is `WeaponInstanceModifiers`: optional primary and secondary
`WeaponScalingModifier` slots using grades E through S. `WeaponScalingGrade.None` is only the default
serialization sentinel and normalizes to an absent slot. A present primary must match the weapon's
natural Strength, Dexterity or Intelligence attribute. A present secondary requires a primary and may
use any other character attribute. Coefficients are fixed by grade: E=0.25, D=0.40, C=0.55, B=0.70,
A=0.85 and S=1.00. These values are not stored in `LootEntry`, Equipment or persistence until unique
item identity is integrated end to end.

`WeaponOffensiveScaling` remains only the legacy runtime representation for the current
`LootId`-based model. It is not the canonical instance contract and must not evolve as a parallel
source of truth. A zero coefficient means no legacy scaling. A positive coefficient accepts only
Strength, Dexterity or Intelligence. `WeaponScalingContributionsResolver` adapts it to zero or one
resolved `(CharacterAttribute, Coefficient)` contribution without converting arbitrary legacy
coefficients into grades. A separate pure resolver can translate `WeaponInstanceModifiers` into up
to two contributions for tests and future integration, but instances are not an active runtime
source until unique Item Instances exist end to end. Both forms resolve values from the confirmed
`CharacterAttributeState` already owned by the Raid participant. The canonical runtime rule is:

```text
EffectiveDamage = floor(BaseDamage * (1 + sum((AttributeValue / 100) * Coefficient)))
```

`WeaponDamageCalculator` is the only implementation of this pure arithmetic. Equipment performs the calculation when it
configures or rebuilds the active player weapon. `MeleeAttack` and `RangedAttack` retain the resolved
runtime value without modifying their shared `AttackConfig`. Non-player executors serialize their
own `AttackExecutionParameters`, keeping their existing behavior independent from Equipment.
The common runtime representation is the removal boundary for the legacy source; the complete
Template/Instance migration remains separate from US-36.

`PlayerRuntimeStatistics` combines character-derived maximums with the current equipment defenses.
Its resource formulas add the aggregated equipment modifiers to maximum Health, Stamina and Mana.
The final projection invalidates on either effective-attribute revision or equipment projection
invalidation. Increasing maximum Health or Stamina does not recover the current resource, while
decreasing it authoritatively applies `NewCurrent = min(PreviousCurrent, NewMaximum)`. Maximum Mana
is projected only; there is no current-Mana runtime state yet.

`PlayerCharacter` applies compatible armor defense in the existing State-Authority damage pipeline.
Physical and Magical damage use their matching defense and the configured positive mitigation
constant `K` (initially 100): `floor(IncomingDamage * K / (Defense + K))`. Zero defense returns the
incoming amount exactly, and True Damage bypasses mitigation. No calculation mutates shared assets.

---

## Melee Attack Flow

1. **Input Collection**: `PlayerInputReader` latches primary attack input.
2. **Transport**: `PlayerNetworkInput` transports buttons to `FixedUpdateNetwork` via Fusion.
3. **Trigger**: `PlayerCombatNetworkController` processes input. Neutral players update button history and stop. An active player requires authoritative strategy presence, a local implementation, enabled combat, a living character, and an expired `AttackCooldown`.
4. **Execution**: If authorized and ready, calls `MeleeAttack.Execute(in AttackRequest)`.
5. **Direction**: Movement resolves the finite, normalized `PlayerMovementNetworkController.FacingDirection` before combat runs in the same tick; a valid primary-attack cursor direction overrides simultaneous locomotion-facing from the final simulated player position.
6. **Query Targets**: `MeleeAttack` delegates queries to `Physics2DAttackTargetQuery.FindTargets()`.
   * Center is computed as: `Origin + FacingDirection * (WeaponDefinition.Range - MeleeAttackConfig.Radius)`.
   * `WeaponDefinition.Range` is the effective distance from origin to the farthest edge of the circle; `Range < Radius` is invalid.
   * Targets are queried within `Radius` using `Physics2D.OverlapCircle` with a non-allocating buffer.
7. **Deduplication and Exclusion**:
   * Attacker's own `EntityId` is excluded.
   * Targets not registered in the `EntityRegistry`, dead, or invulnerable are ignored.
   * A character collider is eligible only when it belongs to that character's explicit damage-hitbox set; movement and interaction colliders remain identity mappings but cannot produce damage candidates.
   * Multiple eligible damage hitboxes belonging to the same entity are deduplicated (preserving only the closest hit point).
8. **Damage Request**: For each candidate target up to `MaximumTargets` (sorted by distance), a `DamageRequest` is built and passed to `IDamageResolver.Resolve()`.

## Confirmed local combat feedback

`DamageResolver` reports its final `DamageResolvedEvent` to an optional direct `IResolvedDamageFeedbackSink` on the attacker object. `PlayerCombatNetworkController` accepts only applied results belonging to that attacker, advances one networked feedback sequence under State Authority, and sends a reliable result only to Input Authority. The result carries target, confirmed hit point, actual applied damage, simulation tick and sequence.

`CombatFeedbackPresenter` consumes this result during presentation and displays only the damage number from a fixed local TMP pool. Executed attacks without a target, wall impacts and rejected damage never produce a number. Existing target flash/scale pulse remains the hit reaction and is not reimplemented. Sequence baselining and monotonic consumption prevent duplicates during proxy observation, resimulation and session rebinding.

A fresh attack press rejected specifically by `CooldownActive` uses the same sequenced local channel to pulse the bottom cooldown icon. For `Hold` configurations the rejection is still emitted only on the physical press edge. Feedback never calls an attack, applies damage or changes the cooldown.

## Fatal defeat contribution

`ExtractionProgressDefeatSource` is a separate co-located network component that owns only configured defeat reward, entity identity and runner-scoped registration. Player, base enemy and enemy variants serialize their own rewards. Characters, attacks, projectiles and traps contain no quota logic.

After `IDamageable.ApplyDamage` returns, `DamageResolver` contributes only for an applied fatal `DamageResult` under State Authority. It resolves reward by `TargetId` and the individual receiver by `AttackerId`, then performs a direct call carrying source type, target identity, amount and simulation tick. Invalid/environmental attackers, zero rewards, non-fatal or rejected damage and already defeated targets contribute nothing. The target's fatal health transition supplies the producer one-shot guarantee; the receiver stores neither ticks nor defeated identities, so two distinct fatal contributions in the same simulation tick remain valid.

---

## Ranged Attack Flow

1. **Input Collection**: `PlayerInputReader` reads primary attack button and mouse world position `AimWorldPosition`.
2. **Facing**: `PlayerMovementNetworkController` has already resolved `FacingDirection`. A valid primary-attack cursor direction from the final simulated player position overrides locomotion-facing; invalid cursor direction falls back to valid movement and otherwise preserves the prior facing.
3. **Execution**: If ready, calls `RangedAttack.Execute(in AttackRequest)` with the same facing used by melee.
4. **Build Request**: `RangedAttack` calculates origin using `SpawnOffset` along the normalized direction and builds `ProjectileSpawnRequest`.
5. **Spawn**: `RangedAttack` calls `IProjectileSpawner.Spawn()`.
6. **Spawner Validation**: `FusionProjectileSpawner` runs only under State Authority. It validates its configs and executes `Runner.TrySpawn()`.
7. **Pre-initialization**: In the `onBeforeSpawned` callback of `TrySpawn`, `NetworkProjectile.InitializeNetworkState()` is invoked to setup the networked variables before replication.
8. **Kinematic Simulation**: `NetworkProjectile` updates in `FixedUpdateNetwork`:
   * Checks `LifetimeTimer` expiration.
   * Moves transform and Rigidbody2D based on `Direction * Speed * DeltaTime`.
   * Clamps final step if remaining range is exceeded.
9. **Collision Casting**: Casts the projectile collider shape along its displacement vector (`Collider2D.Cast`) using `ImpactLayerMask`.
10. **Target/Obstacle Resolution**:
    * Hits are queried against `EntityRegistry`.
    * Projectile owner colliders are ignored.
    * If a valid damageable hit is found under State Authority:
      * Projectile is aligned to the hit contact point.
      * `ImpactConsumed` is set to `true` (guaranteeing one-time damage).
      * `DamageRequest` is dispatched to `IDamageResolver`.
      * Spawner despawns the projectile via `Runner.Despawn()`.
    * If a blocking obstacle (wall) is hit, the projectile despawns without damage.

---

## Damage Pipeline

```text
DamageRequest ──► IDamageResolver ──► IDamageable (ApplyDamage) ──► DamageResult
```

### 1. `DamageRequest` & `DamageResult`
* **`DamageRequest`**: A plain C# struct transporting attacker ID, target ID, effective damage amount, damage type, direction, hit point, and execution tick. For player weapons, Equipment resolves that amount before configuring the attack executor; `DamageResolver` does not calculate weapon scaling.
* **`DamageResult`**: Communicates target ID, execution success, damage amount applied, remaining health, fatal flag, and detailed failure reason.

### 2. `DamageResolver`
A network component that validates damage rules:
* Prevents self-damage: returns `SelfDamageRejected` if target ID matches attacker ID.
* Queries target `IDamageable` from the `EntityRegistry`.
* Excludes targets that cannot receive damage or are dead.
* Calls `IDamageable.ApplyDamage()` on the target.

### 3. Entity Registration (`EntityRegistry` & `CharacterBase`)
* Any damageable character must inherit from `CharacterBase` (which implements `IDamageable` and `ICharacter`).
* On `Spawned()`, characters retrieve the runner's `EntityRegistry` and invoke `TryRegisterDamageable()`, mapping their unique `EntityId` to the `IDamageable` instance, mapping all child `Collider2D` components to the `EntityId`, and registering the serialized `_damageHitboxes` subset for damage detection.
* On `Despawned()`, they call `Unregister()` to remove these references.
* This ensures that multiple colliders representing a single character map to the exact same `EntityId`, while only semantically configured body hitboxes can be selected by melee attacks, projectiles, or area hazards.
* Player and enemy prefabs keep a solid root collider dedicated to foot-level movement and world collision. `Kinematic2DMovementMotor` references only this collider.
* Each character also owns a root-level `DamageHitbox` child on the `Character` physics layer. Its prefab-configured trigger collider covers the animated body without participating in movement collision.
* `DamageHitbox` is independent from the visual `Body` hierarchy so presentation scaling, animation and defeat rotation cannot alter authoritative target detection. It is explicitly referenced by `CharacterBase`; the foot collider remains registered for identity but is excluded from damage detection.

Non-character world targets may register the same contracts without inheriting
`CharacterBase`. `BreakableObject` registers its Character-layer damage hitbox
and WorldCollision blocker under one `EntityId`, accepts the ordinary
`DamageRequest` pipeline, and removes both mappings after its authoritative
destruction. See `Docs/Architecture/BreakableLootArchitecture.md`.

---

## Combat Presentation & Character Defeat Cycle

The combat system coordinates gameplay state with the visual presentation layer through decoupled events and synchronized networked variables. This ensures visual changes have zero impact on the simulation's determinism.

### Modular Character Composition and Animator-Owned Weapon Presentation

`NetworkPlayer.prefab` owns one `PlayerWeaponPresenter` and one `PlayerAnimatorView` over the
modular hierarchy under `VisualRoot`. `NetworkPlayer.prefab` is the productive Raid avatar;
the legacy `NetworkPlayerMelee.prefab` and `NetworkPlayerRanged.prefab` remain only as
historical references. Weapon-specific grip point, angular correction and animation category
belong to `WeaponDefinition.Presentation`, while the sprite remains sourced from the linked
`LootDefinition`. Player prefabs do not select or override those values.

The character visual structure is modularized under `VisualRoot`:
* **`VisualRoot`**: Houses the single common `Animator` and `PlayerAnimatorView` for the character.
* **Modular Slots**: Contains independent `SpriteRenderer` components for `Legs`, `Body`, `Head`, `LeftHand`, and `RightHand`, sharing a uniform 96x96 canvas and local position origin `(0, 0, 0)`. `LeftHand` and `RightHand` are the sole visual hands of the character and are driven exclusively by the modular Animator clips.
* **Single Animator**: A single common `Animator` on `VisualRoot` acts as the ancestor for all modular slots, driving coordinated animation clips across the six visual directions.
* **Held visual hierarchy**: `RightHand/MainHandGrip/MainHandWeaponVisual/WeaponSprite` and `LeftHand/OffHandGrip/OffHandVisual` make each visual inherit the corresponding animated hand transform. A two-handed weapon remains a single visual; authored clips may move both hands without giving the weapon two parents. That visual is owned by `MainHandGrip` unless its definition gives the weapon its own pose through `WeaponPose` (see Weapon rig).

`PlayerWeaponPresenter` owns only Equipment presentation. It resolves the replicated active
Set through `PlayerWeaponEquipmentNetworkController`, assigns or clears Main Hand and shield
sprites, applies the weapon's static grip alignment and angular correction, and derives
front/back sorting from the six-direction facing bucket. It does not subscribe to attacks,
track swing time, rotate a combat pivot, capture input, add networked state, or write gameplay.

Attack presentation follows one path:

```text
PlayerCombatNetworkController.AttackSequence
-> AttackPerformed during Render
-> PlayerAnimatorView.OnAttack trigger + equipment presentation configuration
-> Main Hand Combat Animator layer
-> RightHand transform
-> MainHandGrip
-> MainHandWeaponVisual
```

`DirectionalAttackAnimationSet` owns exactly six static clips in N, NE, NW, S, SE, SW
order; completeness requires every clip. `WeaponDefinition.Presentation` holds one
optional set reference instead of six clips. A complete set enables `HasGenericAttack`
and replaces only the six neutral `GenericAttack_*` slots in the local per-Animator
override controller. The generic `Attack` route does not inspect
`WeaponAnimationCategory`; an unarmed weapon or missing/incomplete set disables it
and restores placeholder slots. Arming Sword, Rapier, Magic Wand, Magic Sword, Long Sword, Zweihander,
Magic Staff and Long Bow reference their respective Sword1H, Rapier, Wand, MagicSword, LongSword, Zweihander,
MagicStaff and LongBow sets. Rondel Dagger and Magic Cinquedea
reference the same Dagger asset containing the generated Rondel clips. Reassigning a set changes
presentation without editing `Character.controller` or branching on weapon identity.
`DirectionalAnimationGenerator` bakes each set from one south-authored `<Weapon>_Attack.anim`:
it rotates the RightHand position trajectory (values and tangents) per facing, keeps the
RightHand rotation art, derives MainHandGrip, hand sprite and north sorting from the facing's
idle, keeps the source clip length, and always emits a one-shot clip. The generator takes the
weapon's `WeaponHandedness` as an explicit input and never branches on weapon identity.
For one-handed weapons only the `RightHandPivot/RightHand` hierarchy is part of the output; other
source curves, such as the LeftHand motion authored in `MagicSword_Attack.anim`, are dropped because
LeftHand carries `OffHandGrip` and belongs to separately authored off-hand clips.
A two-handed weapon blocks the Off Hand, so `OffHandGrip` stays empty and the authored
`LeftHandPivot/LeftHand` transform is its second hand, which the two-handed output therefore keeps.
Being two-handed equipment does not by itself mean the second hand holds the weapon:
`WeaponDefinition.Presentation.SecondHand` (`SecondHandPresentation`, static presentation data read only
by the generator) selects how that transform is baked, and only two-handed weapons may set it.
`HoldsSecondaryGrip` (the default) constrains the second hand to the handle as described below.
`FollowsAuthoredMotion` keeps a second hand that does not hold the weapon on its own authored path under
the main hand's rule: its position trajectory (values and tangents) turns with the facing at its authored
key times, its rotation art and depth stay authored, no `SecondaryGripPoint` is required, and no sorting is
emitted, so the LeftHand layer keeps owning its sorting and sprite. Magic Staff uses it:
`MagicStaff_Attack.anim` authors the left hand as a spell gesture on its own side of the body, rotating
against the main hand and never reaching the staff, whereas Long Sword and Zweihander author it turning
rigidly with the main hand on the handle. The staff's main hand grips `(0, -0.5)`, 8 px below the center
of the 24 px `MagicStaff.png`.
Long Bow is baked by the weapon-driven rig described in Weapon rig below: the bow owns its pose and both
hands are placed on it, so neither the main-hand trajectory rule above nor the second-hand modes position it.
With `HoldsSecondaryGrip` the second hand's rotation art, depth and every authored key time are preserved,
while its position is derived from the weapon's handle so the drawn second hand holds it. Such a two-handed
generic weapon serializes `SecondaryGripPoint` in `WeaponDefinition.Presentation`, in the same sprite
units as `GripPoint`: the main hand holds `GripPoint` and the second hand `SecondaryGripPoint`. The
generator receives the weapon definition, carries that point along the path the presenter gives the held
weapon (MainHandGrip turned by the main-hand rotation, then the facing with its left mirror and the
weapon's angle correction) and subtracts the facing's LeftHand idle sprite anchor, the opaque-pixel
centroid of that sprite at time zero measured from its pivot, so the drawn hand rather than its transform
origin lands on the handle. The held weapon visual must keep unit scale for those sprite units to hold.
Because the second hand follows the rotating handle, its position is keyed at the authored key times plus
the clip's frame grid. `LongSword_Attack.anim` was authored with a different blade rest pose than the
presenter's facing-aligned one, so its authored second-hand offset is not reused; Long Sword grips
`(0, -0.46875)` (the handle row under the guard) and `(0, -0.65625)` (the last handle row).
Zweihander follows the same rule on its own art: `Zweihander.png` has a six-row handle between guard
and pommel, so it grips `(0, -0.4375)` and `(0, -0.75)`. `Zweihander_Attack.anim` remains its single
south source and bakes through the same two-handed contract without weapon-specific code. The
second-hand sprite is not part of the output: the LeftHand layer keeps owning it, so a walk cycle played
during an attack can still move the drawn second hand away from the handle.
The second hand draws over the handle and under the main hand: sorting order 25 in front facings (weapon 20,
attack VFX 21, main hand 30) and -5 in north facings (weapon -10, main hand -2), leaving the next slot for
its glove.
Category 1 and its `LegacySword-Attack` state are retired: every melee weapon, including
`zweihander`, uses the generic `Attack` route.
The category-4 `LegacyRanged-Attack` route retains the original Magic Wand directional
motions and is gated by `!HasGenericAttack` for `compound_bow`. Generic Arming Sword,
Rapier, Magic Sword, Long Sword, Zweihander, Rondel Dagger, Magic Cinquedea, Magic Wand, Magic Staff and
Long Bow all serialize category `None` (0). Numeric categories 1, 2 and 3 are retired.
This is local presentation state,
not a replicated or authoritative combat decision.

The trigger represents an already accepted gameplay execution; local mouse input never starts
the animation. Proxies observe the same replicated sequence and therefore reproduce it. Attack
clips are one-shot presentation only. They do not apply damage or emit gameplay decisions, and
Animation Events are not part of hit timing. The confirmed attack snapshot carries the deterministic
Main Hand catalog index captured only after successful execution. Animation, held Main Hand
presentation, and attack-start audio resolve that snapshot identity rather than current Equipment,
so a Weapon Set change cannot rewrite an attack already in progress; once the tagged attack state
ends, presentation resumes reading current Equipment. The attack direction from the confirmed event
is held as the temporary visual facing until the Animator leaves its tagged attack state, while the
Base Layer preserves the replicated locomotion state so movement animation can continue.

Optional attack VFX is local presentation, independent of the directional attack animation set.
The effect is split by responsibility. An abstract `AttackVfxVisualDefinition` holds the shared art
(a sprite-only clip) and delegates geometry-specific placement and sizing to specialized subclasses:
`SlashVfxVisualDefinition` for arc-shaped swings (scaled by `TipRadius`), `ThrustVfxVisualDefinition`
for straight-line thrusts (scaled by path `Length` and shifted along the local +X axis from `BackX`)
`CastFlashVfxVisualDefinition` for point flashes emitted at the casting tip (never scaled, rotated
or mirrored; its art `Center` is placed on the cast point) and `BowShotVfxVisualDefinition` for a bow's
release impulse (never scaled; its art `Origin` is placed on the shot origin, pointed along the shot and
mirrored like the bow). Every archetype receives one length along the pose's +X axis and decides what it
measures: the span of its art for Slash and Thrust, the offset from the anchor to the cast point for Cast
Flash, the offset from the anchor to the shot origin for Bow Shot. The visual declares whether that length
uses the weapon: `UsesWeaponReach` is true by default and the length is the pose's reach offset plus the
blade reach; Bow Shot overrides it to false and the length is the reach offset alone. Only a visual that
uses the weapon reach requires a blade tip distinct from the grip point.
`WeaponDefinition.Presentation` references an `AttackVfxDefinition` that aligns one visual with
one attack: the start offset in the attack clip and six attack-relative directional poses (geometric anchor,
rotation, reach offset, mirror and sorting). Weapons with different attacks reuse the same visual
through their own alignments, so changing one attack's timing or geometry never moves another weapon's
effect, and the art is never duplicated.
The weapon owns its visual geometry: `BladeTip` sits in the weapon sprite's local units beside
`GripPoint`, and their distance is the blade reach. `AttackVfxDefinition` adds it only when its visual
uses the weapon reach. The presenter resolves each pose through the
visual's `TryResolvePose`, so weapons sharing an animation can share one alignment without
per-weapon sizes, and it never reads weapon-specific measurements.
`PlayerAttackVfxPresenter` snapshots confirmed weapon identity and direction from `AttackPerformed`,
not the currently equipped Set, and waits for the matching RightHand attack clip. It samples the
VFX clip at that clip's phase minus the configured start offset on the *existing* `VisualRoot`
Animator root. The VFX clip binds only `AttackVfx/SpriteRenderer.m_Sprite`, never hand transforms,
and finishes after its own clip duration. The renderer is a direct child of `VisualRoot`, not of
the animated hand or weapon pivot, so the effect never inherits the swing twice. The Sword Slash
visual plays four 100 ms sprite frames. Arming Sword aligns it from attack phase 0.1s, mirrored to
its counterclockwise swing; Magic Sword aligns it from 0.25s, so the first frame straddles its
windup apex at 0.3s and the last one its strike end at 0.55s, unmirrored to its clockwise strike
around the body center. The two-handed Long Sword aligns it from 0.08s, so frame 0 anticipates
its windup apex at 0.2s, frames 1-2 cover its counterclockwise strike to 0.4s and frame 3 the
recoil, mirrored like Arming Sword; its size follows the main-hand grip to blade tip reach only,
never `SecondaryGripPoint`. The two-handed Zweihander has its own alignment from 0.25s: frame 0
anticipates its windup apex at 0.3s, frames 1-2 cover its slower, roughly 190-degree
counterclockwise strike to 0.6s and frame 3 the strike end, mirrored like Long Sword; its arc
centers sit farther from the body because its hands travel farther, and its size also follows only
the grip to blade tip reach. Rapier aligns the four 50 ms frames of the Thrust visual from 0.19s,
spanning the path to its extended blade tip across all six facings. Rondel Dagger reuses the same
Thrust visual through its own alignment from 0.14s: its stroke runs 50 ms earlier than Rapier's and its
hand tilts the blade about 14 degrees off the facing, so each axis is the facing plus 14 degrees and each
anchor is its own stroke-start grip. Magic Cinquedea plays the same Dagger clips, so it shares that
`DaggerThrustAttackVfx` alignment and differs only by its own blade reach.
Magic Wand is the first Cast Flash consumer. Its clips wind up until 0.2s, flick forward until the
tip stops at 0.35s and recover until 0.6s in every facing. `MagicWandCastFlashAttackVfx` anchors
each facing at the grip of that stop with the grip to tip axis as rotation and no reach offset, so
the flash lands on the tip of any wand length; `BladeTip` is `(0.03125, 0.46875)`, the single top
pixel of the 4x16 px sprite. Its four 50 ms frames start at 0.325s: frame 0 ignites as the tip
arrives, frame 1 bursts on the hold and frames 2-3 fade where the cast happened while the wand
recovers, because a flash is emitted in place rather than carried. The visual holds no wand data,
so another caster reuses it through its own alignment.
Magic Staff is the second Cast Flash consumer: `MagicStaffCastFlashAttackVfx` aligns the same
`CastFlashVfxVisual` with its own cast. Its clips charge raised until 0.65s, pull back until 0.8s and
strike forward until the gem peaks at 0.9s in every facing, then recover until 1.1s. Each facing anchors
the main-hand grip at that peak with the grip to gem axis as rotation and no reach offset; `BladeTip` is
`(-0.0625, 0.53125)`, the center of the 5x5 px gem of the 13x24 px sprite. Its four frames start at
0.875s, so frame 0 ignites as the gem arrives and frames 2-3 fade in place while the staff recovers.
Sorting sits just above the held staff (21 in front facings, -9 in north facings). The staff's left hand
is an independent authored gesture outside the main-hand chain, so it never moves the cast point.
Long Bow is the first Bow Shot consumer. `VFX-BowShot.png` is four 96x96 px cells at 16 PPU with centered
pivots: a release flash, then a streak, rings and remnants that travel along +X from the flash center. That
center, `(-0.875, -0.03125)` in sprite local units, is the `BowShotVfxVisual` origin in every frame.
`LongBowBowShotAttackVfx` plays the four 50 ms frames from 0.45s, the end of the Long Bow stringing sequence
where the string hand releases, so the stringing stays the single source of that timing. Each facing
anchors the bow's pivot at that release: the grip on `WeaponPose`, with the facing as rotation, because the
presenter turns the bow along its shooting axis. The reach offset is 0.09375, from the grip to the limb's
front edge on the center column of `LongBow.png`, so the flash leaves the bow's front in every facing. NW
and SW mirror like the bow, and sorting sits just above the held bow (21 in front facings, -9 in north
facings). The shot is a release impulse, not a trajectory, so neither the blade reach nor the projectile's
range sizes it; the bow configures no blade tip for it.
Each alignment uses poses fitted to its own directional attacks; other weapons have no VFX reference. The effect clears on interruption,
defeat, disable or completion. Proxies observe the same confirmed attack snapshot; no VFX-only
network state, Animator layer/state, second Animator, animation events or gameplay timing authority
is introduced.

Visual authoring keeps those responsibilities explicit. The presentation grip point is
serialized in `WeaponDefinition` in the weapon sprite's local units. It identifies the
point inside the visible handle that must coincide with `MainHandGrip`, so grip tuning
remains per-weapon static configuration instead of a player-prefab override or code
constant. The animated hand moves the complete weapon pose; the internal grip
point must not be used to compensate for an incorrect hand animation.

the six discrete visual directions (N, NE, NW, S, SE, SW) resolved by `CharacterVisualDirectionResolver`
serve as the common facing buckets for body, hands and held visuals. The shield remains under
`OffHandGrip`, ready for separately authored defense clips; this integration does not fabricate
missing shield animation content. The Animator never owns mitigation or coverage rules. Both held
renderers stay on the existing `Characters` Sorting Layer and derive front/back order from the
resolved visual bucket.

### 1. Damage Feedback Visuals
When a character takes damage (authoritatively confirmed by `Health` changes on State Authority):
* Presentation components (`PlayerDamagePresenter`) trigger procedural feedback.
* **Sprite Flash**: Temporarily overrides the character's material colors to a bright flash color to signify a hit.
* **Scale Pulse**: Briefly scales the character's transform down/up to provide physical impact feedback.
* These reactions run completely client-side in the presentation loop (`Render` or via network property changed callbacks).

### 2. Player Defeat and Persistent Body
When player health drops to or below zero, a strict death/defeat pipeline is executed:
* **Gameplay Simulation Disabling**: 
  * The character's alive status (`IsAlive = false`) immediately disables movement input and combat actions in `FixedUpdateNetwork`.
  * Ongoing attack timers and active projectile spawns are halted.
  * The shared authoritative damage path calls `PlayerCharacter.HandleDeath`, which delegates co-located corpse-loot conversion to `PlayerCorpseGenerationController`. This is not driven by presenters, Animator events, polling, or `Update`. The controller's networked terminal state prevents duplicate conversion during repeated calls or resimulation.
  * The defeated avatar/body retains its existing `NetworkObject`. Its initially unavailable `NetworkLootContainer` receives and verifies the exact temporary-inventory snapshot before that inventory is cleared, then becomes available. A load or clear failure leaves the container empty and unavailable while preserving the inventory; no replacement corpse is spawned. The separate `NetworkRaidParticipant` remains the stable PlayerObject and participation identity.
* **Presentation Transition**:
  * Visual presentation components (`PlayerDefeatPresenter`, `PlayerAnimatorView`) detect the transition to the dead state and retain the player's own final defeat pose.
  * **Immediate Action Hiding**: Combat visual effects, attack animations, and movement indicators are stopped immediately (visual priority: Defeat > Damage Feedback > Attack > Locomotion).
  * **Persistent Body**: The shared transition rotates the visual and moves its sprite alpha toward the configured defeated value. `PlayerDefeatPresenter` overrides `HideBodyVisualAfterTransition` to `false`, so the body root and its renderers remain enabled after the transition. The delayed cleanup hides only combat-specific presentation such as the weapon or combat visual root.
  * Gameplay components (such as `NetworkObject`, health variables, colliders, and network controllers) remain active to support the multiplayer session lifecycle.
* **Remote Proxy Synchronization**:
  * Proxies observe replicated health and reproduce the same local defeat transition without creating or replacing a network entity. Exact Host/Client pose and visual consistency remain manual validation.

---

## Prefab and Asset Dependencies

### 1. Player Prefab
* Must contain:
  * **`PlayerCombatNetworkController`**:
    * `_characterSource` -> Reference to `PlayerCharacter` or character component.
    * `_attackOrigin` -> Transform indicating weapon output position.
    * `_activeAttackSource` -> Optional initial local strategy source. Empty on the productive neutral prefab; Equipment assigns it at runtime.
    * `_movementController` -> Reference to `PlayerMovementNetworkController`.
  * **Shared defeat and loot composition**: `PlayerCharacter`, `PlayerLootReceiver`, `PlayerCorpseGenerationController`, `NetworkLootContainer`, `NetworkLootContainerInteractable`, `InteractionPromptMetadata`, and the dedicated interaction trigger share the root `NetworkObject`.
  * The base `NetworkPlayer.prefab` contains inactive/configuration-free `MeleeAttack` and `RangedAttack` strategies, their shared query/projectile dependencies, and `PlayerWeaponEquipmentNetworkController`. It intentionally has no active serialized strategy.
  * `NetworkPlayerMelee.prefab` and `NetworkPlayerRanged.prefab` are legacy variants kept for historical tests and reference only. Productive runtime composition does not select them from equipped weapon identity.

### 2. Projectile Prefab (e.g. `Arrow.prefab`)
* Must contain:
  * **`NetworkObject`** & **`NetworkTransform`**.
  * **`Rigidbody2D`** (Kinematic, Simulated).
  * **`Collider2D`** (Trigger recommended, configured on Projectile layer).
  * **`NetworkProjectile`** script with assigned references.
* Must be registered in the **Network Project Settings** under Fusion's prefab catalog.

### 3. Enemy Melee Prefab
* Must contain:
  * A component deriving from `CharacterBase` (e.g. implementing health and `IDamageable`).
  * Collider components (on a layer included in combat masks).

### 4. Configuration Assets
* **`MeleeAttackConfig`** asset: Saved as a scriptable object, referenced in the character's `MeleeAttack` component.
* **`RangedAttackConfig`** asset: Saved as a scriptable object, referenced in `RangedAttack` and `FusionProjectileSpawner` components.

### 5. Weapon Content Set

`Assets/Scriptable Objects/Loot/Definitions` contains one `LootDefinition` and one
`WeaponDefinition` for each weapon currently represented in `Assets/Art/Weapons`. The Shield
uses its own `ShieldDefinition`. Every identity is registered in `LootDefinitionCatalog` and is
reachable through `DefaultLootContainerContentTable`; `arming_sword` is additionally the Town
recovery weapon configured by `LocalProfilePersistenceConfiguration.RecoveryWeaponLootId`.

The base templates have no offensive scaling coefficient. Their configured requirements, damage,
interval, range and Stamina cost are static playtest baselines. Weapon instances remain the owner
of future scaling variation.

| Loot id | Hands | Attack config | Attack animation |
| :--- | :---: | :--- | :--- |
| `arming_sword` | 1 | `PlayerMeleeAttackConfig` | Sword1H set (`None` category) |
| `rapier` | 1 | `PlayerMeleeAttackConfig` | Rapier set (`None` category) |
| `magic_sword` | 1 | `PlayerMeleeAttackConfig` | MagicSword set (`None` category) |
| `long_sword` | 2 | `PlayerMeleeAttackConfig` | LongSword set (two-handed; `None` category) |
| `zweihander` | 2 | `PlayerMeleeAttackConfig` | Zweihander set (two-handed; `None` category) |
| `rondel_dagger` | 1 | `PlayerMeleeAttackConfig` | shared Dagger set (Rondel clips; `None` category) |
| `magic_cinquedea` | 1 | `PlayerMeleeAttackConfig` | same Dagger set (`None` category) |
| `long_bow` | 2 | `RangePlayerAttackConfig` | LongBow set (two-handed, held by the left hand; `None` category) |
| `compound_bow` | 2 | `RangePlayerAttackConfig` | `LegacyRanged` fallback |
| `magic_wand` | 1 | `RangePlayerAttackConfig` | Wand set (`None` category) |
| `magic_staff` | 2 | `RangePlayerAttackConfig` | MagicStaff set (two-handed, authored second hand; `None` category) |

Grip points are expressed in sprite-local units from the centered pivot to the point that must
coincide with the owner of the weapon pose (`MainHandGrip` by default). Vertical weapon art uses a `-90` degree correction to align its
forward axis with the presenter's `+X`. Bow art spans its limbs horizontally and shoots along sprite
`+Y`, so a migrated bow uses the same `-90` correction; unmigrated `compound_bow` keeps its fallback values.
These values are static per-weapon presentation data and do not introduce LootId branches in the presenter.

### Weapon rig

The equipped Main Hand weapon has exactly one held visual hierarchy (`MainHandWeaponVisual` →
`WeaponSprite`). `WeaponDefinition.Presentation.Rig` (`WeaponRig`, static presentation data, never
replicated) selects what owns its pose:

- `HandHeld` (default): the main hand. The visual follows `RightHandPivot/RightHand/MainHandGrip`. Every
  weapon except Long Bow uses it.
- `WeaponDriven`: the weapon. The visual follows `WeaponPose`, a transform on the Animator root beside the
  hand pivots and inside no hand. Long Bow uses it.

`PlayerWeaponPresenter` reparents the same visual under the selected owner when the presented weapon
changes, and back to `MainHandGrip` when unarmed. The facing rotation, the left-facing Y mirror, `GripPoint`
and `AngleCorrection` apply unchanged under either owner. The rig is presentation only: equipment still
treats the weapon as Main Hand, a two-handed weapon still blocks the Off Hand, and `OffHandGrip` still
belongs only to the Off Hand item. Validation allows `WeaponDriven` only for a two-handed weapon whose second
hand is `FollowsAuthoredMotion`.

A weapon-driven attack is derived from its south source instead of rotating hand transforms:

- The source is read as drawn positions: each hand transform plus its south idle sprite anchor, turned by
  the hand's rotation.
- The authored bow-arm (left) hand is the weapon grip, so its drawn path becomes the `WeaponPose`
  trajectory. `WeaponPose` also takes that hand's rotation art.
- The authored drawing (right) hand, relative to the weapon, is the string-hand target.
- Both turn with the facing about the aim center, a point on the body axis at the south string hand's
  height during its authored draw hold (the first two equal consecutive main-hand keys).

Rotating drawn points about that aim center, rather than rotating hand transform offsets about the root and
adding unrotated sprite anchors, keeps the aim height in place. A south draw toward the target therefore
turns into an upward draw in N instead of a pull toward the feet. The runtime hands are then keyed so each
drawn centroid lands exactly on its point in every frame: the left hand on `WeaponPose` (the bow grip),
the right hand on its string target. The hands never carry the bow, and moving the string hand never
moves it. South reconstructs the source exactly, because its turn is the identity.

A weapon-driven source starts and ends in its authored ready pose, away from the locomotion rest, while the
generic Attack transitions are instant. So the bake delays the whole authored motion by
`DirectionalAnimationGenerator.WeaponDrivenBlendSeconds` (0.1 s) and keys the facing's idle pose on both
sides: `WeaponPose` at the idle drawn left hand, both hand transforms at their origin, nothing rotated.
While a weapon-driven weapon is presented, `PlayerWeaponPresenter` owns the sorting of the holding left hand
in idle, locomotion and attacks alike: order 25 in front facings, the second-hand slot `HoldsSecondaryGrip`
uses (over the front weapon at 20 and its VFX at 21, under the main hand at 30, with the glove one slot
above), and the hand's authored order in back facings. Weapon-driven clips never key that sorting, and
switching to another rig restores the authored order once so hand-held clips keep animating it. The
clip therefore eases out of and back into locomotion instead of snapping. For Long Bow the clip grows from
0.7 s to 0.9 s, its attack interval, and the authored release plays 0.1 s later. The Animator controller
and every hand-held weapon are unchanged.

Outside attacks, `WeaponPose` rests in the drawn left hand. The Idle and Walk body clips key its position,
stepped, at every LeftHand sprite frame of the matching `LeftHand_<Motion>_<Facing>` clip, on the
opaque-pixel centroid of that frame, and key its rotation to zero. The
`Tools/Animations/Generate Weapon Pose Locomotion` menu writes those keys; a weapon-driven attack bake
requires none of them.

Long Bow grips `(0, 0.125)` of the 27x7 px `LongBow.png`: the center of the three-row limb in the sprite's
center column (outline, wood, outline), 2 px above the centered pivot. The string lies on the bottom row.
After the `-90` correction the bow shoots along the facing in every direction. The left-facing mirror
flips only its symmetric limbs, and the grip lies on the shooting axis, so NW and SW keep the
string → bow → target order.

### Weapon attack sprite animation

A weapon whose own art changes during its attack, such as a bow drawing its string, references an optional
`WeaponAttackSpriteAnimation` from `WeaponDefinition.Presentation.AttackSpriteAnimation`. It is static
presentation data: a `StartSeconds` in the weapon's attack clip time and ordered frames, each a sprite and a
positive duration. Validation requires a complete attack animation set and a sequence that ends inside all six
attack clips. Weapons without it are unchanged.

The sequence only swaps the held visual's sprite. `WeaponPose`, both hands, `GripPoint`, `AngleCorrection`, the
facing rotation and the left-facing mirror keep owning the spatial pose, so every frame is authored in the frame
of reference of the weapon's world sprite: the same orientation and pixels per unit, with a pivot that keeps
`GripPoint` on the same pixel of the art. One sequence therefore serves all six facings.

`PlayerAnimatorView.TryGetPresentedAttackSeconds` reports the clip time while the `RightHand` layer plays an
`Attack`-tagged state whose current clip belongs to the pinned confirmed attack weapon's own set.
`PlayerWeaponPresenter` shows the frame for that time, and the world sprite before the start, after the last
frame, outside the attack and on proxies without a confirmed attack. It adds no Animator, layer, controller,
networked state or weapon identity branch, and it is independent from the attack VFX pipeline.

Long Bow uses `LongBowStringingAttackSpriteAnimation` over the four `Weapon-LongBow-Stringing.png` frames (rest,
two partial draws, full draw). Its frames lie in the world sprite's orientation (limb up, string on the bottom
row), at 16 PPU with point filtering, and frame 0 is pixel-identical to `LongBow.png`. Each pivot sits 3.5 px
below the top of the limb, on the center column, so the `(0, 0.125)` grip stays on the limb's center. The frames
follow the baked attack: the draw starts at 0.1 s after the ease-in, draws through 0.2 s and 0.25 s, reaches the
full draw at 0.35 s while the hands peak at 0.4 s, holds, and releases at 0.45 s, when the string hand leaves the
hold. The world sprite then shows the string at rest.

The fallback assignments make every weapon use an authored Animator transition. They do not claim
to be final bow animation content. `WeaponAnimationCategory` therefore exposes
`None` (0) for all generic weapons and only `LegacyRanged` (4) for the unmigrated ranged
fallback route. Numeric values 1, 2 and 3 are retired; there is no sword, Rapier or dagger
category or Animator route.

`shield` preserves `0.5` damage reduction and a `120` degree defensive cone. Shield defense remains
independent from attack animation categories.

`LootDefinitionCatalog` derives network indices by ordinal-sorting loot ids, not by serialized
list order, so appending content shifts the indices of existing entries by design. This is safe
because indices are recomputed identically on every peer from the same catalog and are only used
for in-flight replication; local persistence stores `LootId` strings.

---

## Acceptance Criteria Matrix

| Criterion | Status | Code Evidence | Manual Validation Required |
| :--- | :--- | :--- | :--- |
| **Authorized attack execution** | Implemented | [PlayerCombatNetworkController.cs:L116-124](file:///c:/Users/Dani/OneDrive/Documentos/GitHub/ProjectGrimhold/Project%20Grimhold/Assets/Scripts/Player/Combat/PlayerCombatNetworkController.cs#L116-L124) | Yes (validate host-only decisions) |
| **Configurable cooldown** | Implemented | [PlayerCombatNetworkController.cs:L200-208](file:///c:/Users/Dani/OneDrive/Documentos/GitHub/ProjectGrimhold/Project%20Grimhold/Assets/Scripts/Player/Combat/PlayerCombatNetworkController.cs#L200-L208) | Yes (validate with modified configs) |
| **Correct attack direction** | Implemented | [PlayerCombatNetworkController.cs:L165-188](file:///c:/Users/Dani/OneDrive/Documentos/GitHub/ProjectGrimhold/Project%20Grimhold/Assets/Scripts/Player/Combat/PlayerCombatNetworkController.cs#L165-L188) | Yes (verify mouse aim vs facing fallback) |
| **Valid target filtering** | Implemented | [Physics2DAttackTargetQuery.cs:L97-113](file:///c:/Users/Dani/OneDrive/Documentos/GitHub/ProjectGrimhold/Project%20Grimhold/Assets/Scripts/Combat/Physics2DAttackTargetQuery.cs#L97-L113) | Yes (verify against non-damageable layers) |
| **Attacker and owner exclusion** | Implemented | [Physics2DAttackTargetQuery.cs:L91-95](file:///c:/Users/Dani/OneDrive/Documentos/GitHub/ProjectGrimhold/Project%20Grimhold/Assets/Scripts/Combat/Physics2DAttackTargetQuery.cs#L91-L95) / [NetworkProjectile.cs:L121](file:///c:/Users/Dani/OneDrive/Documentos/GitHub/ProjectGrimhold/Project%20Grimhold/Assets/Scripts/Combat/NetworkProjectile.cs#L121) | Yes (verify projectile ignores owner) |
| **One damage application per target/projectile** | Implemented | [MeleeAttack.cs:L163-167](file:///c:/Users/Dani/OneDrive/Documentos/GitHub/ProjectGrimhold/Project%20Grimhold/Assets/Scripts/Combat/MeleeAttack.cs#L163-L167) / [NetworkProjectile.cs:L224-226](file:///c:/Users/Dani/OneDrive/Documentos/GitHub/ProjectGrimhold/Project%20Grimhold/Assets/Scripts/Combat/NetworkProjectile.cs#L224-L226) | Yes (confirm no double-damage on walls/enemies) |
| **Authoritative projectile spawn** | Implemented | [FusionProjectileSpawner.cs:L45-50](file:///c:/Users/Dani/OneDrive/Documentos/GitHub/ProjectGrimhold/Project%20Grimhold/Assets/Scripts/Combat/FusionProjectileSpawner.cs#L45-L50) | Yes (client-side execution check) |
| **Synchronized projectile observation** | Implemented | Spawns via network-replicated Fusion object. | Yes (visible on remote proxies) |
| **Configurable speed and lifetime** | Implemented | [RangedAttackConfig.cs:L10-14](file:///c:/Users/Dani/OneDrive/Documentos/GitHub/ProjectGrimhold/Project%20Grimhold/Assets/Scripts/Combat/RangedAttackConfig.cs#L10-L14) | Yes (verify values change projectile behavior) |
| **Lifetime and range despawn** | Implemented | [NetworkProjectile.cs:L161-179](file:///c:/Users/Dani/OneDrive/Documentos/GitHub/ProjectGrimhold/Project%20Grimhold/Assets/Scripts/Combat/NetworkProjectile.cs#L161-L179) | Yes (verify ranges/durations) |
| **Shared damage pipeline** | Implemented | [DamageResolver.cs](file:///c:/Users/Dani/OneDrive/Documentos/GitHub/ProjectGrimhold/Project%20Grimhold/Assets/Scripts/Combat/DamageResolver.cs) | Yes (verify damage application logs) |
| **Enemy melee damage integration** | Implemented | Evaluates `IDamageable` registered from `CharacterBase`. | Yes (verify enemy health reduction) |
| **Independence from animation playback** | Implemented | Simulation executes entirely in `FixedUpdateNetwork` tick loops. | Yes (test with empty animation parameters) |
| **No player/enemy-specific logic in core** | Implemented | Systems interact strictly via interface models. | Yes |
| **Stable participant plus persistent defeated avatar** | Implemented | `NetworkRaidParticipant` remains the PlayerObject; `PlayerCharacter.HandleDeath` delegates to the avatar's co-located `PlayerCorpseGenerationController`, and no replacement corpse is spawned. | Yes (Host/Client participant/body observation) |
| **Persistent inspectable player body** | Implemented | `PlayerDefeatPresenter.HideBodyVisualAfterTransition` is `false`; the co-located generic loot endpoint becomes available only after authoritative conversion. | Yes (final pose and interaction on both peers) |

---

## Known Limitations and Technical Debt

* **Layer Configuration Dependency**: The system requires strict layer separation. If targets or obstacles are not on the correct layers specified in `MeleeAttackConfig` and `RangedAttackConfig`, collision queries will fail to report hits.
* **Component Casting**: Configured strategies rely on a serialized `MonoBehaviour` cast to `IAttack`. An empty source is a valid neutral state; a non-empty source must implement the contract.
* **Equipment Instances Remain Template-Based**: armor statistics and maximum-resource modifiers are applied through the equipped `LootId` definitions, and inventory tooltips expose those same definition-owned values. Unique per-instance armor modifiers remain unavailable until instance identity is transported end to end.
* **No Unique Equipment Instances Yet**: current inventory, Equipment, world and persistence paths identify items by `LootId` plus quantity. `WeaponInstanceModifiers` defines the canonical scaling payload but is not transported or stored yet, so multiple runtime variants of one template do not exist.
* **Town preparation covers all eight slots**: `PreparedEquipmentLoadout` and `TryInitializePreparedEquipment` carry both hands of Set A and Set B plus Helmet, Armor, Gloves and Boots. Only a valid Main Hand weapon is required to launch (`04 - Character Build Design` §15.1); armor and Off Hand are optional and are never granted by the recovery guarantee.
* **Off Hand attacks are deferred**: primary attack still resolves only the active Set's Main Hand. Sustained shield defense is implemented, while Dual Wield attacks remain a separate feature.
* **Armor Presentation**: `PlayerArmorPresenter` handles the visualization of equipped armor (`Helmet`, `Armor`, `Gloves`, `Boots`) by reading the slot presence from `PlayerWeaponEquipmentNetworkController`. It dynamically overlays and tints copies of the base modular sprites to provide visual feedback during testing. Proxy players synchronize this presentation entirely through the replicated `EquipmentRevision` and slot definitions, without additional networked state.

---

## Manual Validation Guide

For thorough multi-peer validation, configure two instances (Host and Client) and follow these steps:

### 1. Melee Combat Test
* **Setup**: Place an Enemy Melee prefab within the scene. Spawn a Player character.
* **Action**: Execute a melee attack while facing the Enemy.
* **Expected Result**: The combat controller triggers target search. Enemy takes damage as shown in host simulation logs. Local gizmo outline correctly overlaps target.

### 2. Ranged Projectile Combat Test
* **Setup**: Deploy Player and Enemy.
* **Action**: Perform ranged attack targeting the Enemy.
* **Expected Result**: Projectile spawns at configured offset. It travels at defined speed, detects the Enemy collider, inflicts damage, and despawns immediately on impact. Projectile is visible on both Host and Client viewports.

### 3. Obstacle Collision Test
* **Setup**: Place a wall obstacle (with static collider) on the impact layer.
* **Action**: Fire a projectile directly at the wall.
* **Expected Result**: Projectile travels and despawns instantly on wall contact. No damage request is generated.

### 4. Range & Lifetime Expiration Test
* **Setup**: Fire a projectile into open space.
* **Action**: Observe projectile travel.
* **Expected Result**: Projectile despawns automatically when either travel distance exceeds `MaxRange` or duration exceeds `LifetimeSeconds`.

### 5. Client Authority Verification
* **Setup**: Launch Client instance.
* **Action**: Force Client to trigger `IProjectileSpawner.Spawn` directly.
* **Expected Result**: Spawner rejects command immediately due to missing `HasStateAuthority` validation check.

### 6. Player Defeat, Persistent Body and Loot Handoff
* **Setup**: Launch Host and Client, give one player temporary inventory, and defeat that player.
* **Action**: Observe the defeated entity from both peers and inspect it from the surviving player.
* **Expected Result**: The original `NetworkPlayer` remains spawned and visible in its final pose with the same network identity. Its generic container becomes interactable only after the authoritative inventory handoff, opens `ScreenMode.ContainerLoot`, remains available when emptied, and disappears only when that original player object is despawned or the session ends.
* **Boundary**: The loot transaction, UI, registry and lifecycle details are defined in `LootInteractionArchitecture.md`, `PlayerInteractionArchitecture.md`, and `RaidInventoryUIArchitecture.md`; combat presentation does not own those states.
