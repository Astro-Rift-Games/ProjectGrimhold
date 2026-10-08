# Player Combat Architecture

## Shared aim direction

`PlayerMovementNetworkController` resolves locomotion-facing after the player's kinematic
displacement, then lets a valid cursor direction override it only for a same-tick
`PrimaryAttack` or `Interact` intent. It writes the single synchronized
`FacingDirection` and runs before `PlayerCombatNetworkController` in every Fusion
simulation tick. Melee and ranged both validate and consume that same finite, normalized
contextual facing; combat does not recompute aim from cursor input or `_attackOrigin`.

`_attackOrigin` remains the physical `AttackRequest.Origin`. `LastAttackDirection` is
not continuous aim state: it commits at an accepted melee swing or ranged wind-up,
together with the original weapon identity, origin, type, acceptance tick and sequence for presentation.

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
* Only State Authority validates and executes attacks. Irreversible player execution/spawning runs on forward ticks, not resimulation.
* Owns one networked `RangedAttackRelease` value (deadline and committed projectile payload) and one networked `MeleeAttackRelease` value (deadline, committed direction and committed weapon identity). Both advance before reading input, independently of the active strategy.
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
player runtime statistics are derived local state and are not generally replicated or persisted.
An already accepted ranged shot is the narrow exception: the combat snapshot copies its resolved
projectile payload, so rebuilding Equipment cannot recalculate that shot. The armor
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

Defense has priority over attack when both intentions are present in the same tick. Movement
continues while defending, and an accepted defense aims like an attack: while `CanDefend` accepts
the held `SecondaryAction`, `PlayerMovementNetworkController` turns the replicated `FacingDirection`
toward the cursor, so the defensive cone and the six-direction presentation follow the aim.
`SecondaryAction` without a defendable shield keeps the locomotion facing. `PlayerCombatNetworkController`
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
replicated read model consumed by the six-direction shield presentation.

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
| **`PlayerShieldDefenseNetworkController`** | Implemented | Derives replicated sustained defense and evaluates the active shield's frontal coverage. | Six-direction shield presentation is driven by `IsDefending`. |
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

`PlayerMeleeAttackConfig` (radius 0.5, one target) is shared by the single-target melee weapons and is not edited
for another weapon. The Spellbook's "area / proximity" identity (GD 09) is its own `SpellbookMeleeAttackConfig`: the
same target mask with radius 1.0 and up to 4 targets, so with the weapon's 2.5 range the circle is centered 1.5 ahead
of the attacker and every target inside it is hit by one execution. Radius and target count are provisional balance
values, because the Game Design gives no numbers for them.

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

`WeaponDefinition` owns player weapon `BaseDamage`, `AttackIntervalSeconds`, `AttackReleaseSeconds` (the wind-up from
acceptance to the projectile spawn or the melee hit; it replaces the former ranged-only `RangedReleaseSeconds`, and the
serialized field keeps loading old assets through `FormerlySerializedAs`), effective `Range`,
`StaminaCost`, `DamageType`, `KnockbackForce`, handedness, requirements and natural scaling
attribute. `ArmorDefinition` owns integer Physical Defense, Magical Defense and one flat maximum
Health, Stamina or Mana modifier. `EquipmentStatisticsCalculator` rebuilds a complete immutable
`EquipmentStatisticsModifiers` snapshot from the four equipped armor definitions whenever
`EquipmentRevision` changes; it never accumulates deltas. `StaminaCost` is validated weapon
configuration but is not consumed yet. Bow, wand and staff templates reuse the shared ranged
behavior. Long Bow, Compound Bow, Magic Wand and Magic Staff own gameplay release delays of
0.45, 0.40, 0.35 and 0.90 seconds respectively. Every player melee weapon, including the Spellbook area melee,
calibrates its release to its attack VFX `StartSeconds + ReleaseLeadSeconds`, so the visible hit moment is the damage
moment (Arming Sword 0.10, Great Hammer 0.26, Long Sword 0.08, Magic Cinquedea and Rondel Dagger 0.14, Magic Sword
and Zweihander 0.25, Rapier 0.19, Spellbook 0.625 seconds);
weapon-specific projectile manifestation remains outside this contract.

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
4. **Acceptance**: If authorized and ready, `MeleeAttack.TryAcceptRelease` validates the configuration and resolved statistics and commits a networked `MeleeAttackRelease` (direction, active Main Hand catalog identity and `ReleaseTick = AcceptedTick + ceil(AttackReleaseSeconds / Runner.DeltaTime)`). Damage is not resolved on the acceptance tick unless the delay is zero. Attack Interval begins at acceptance, not at release. An outstanding swing blocks another acceptance (reported as `CooldownActive`) even if the interval expires first.
   * **Release**: before reading input, State Authority advances the pending swing on forward ticks. At or after the exact deadline it consumes the swing (`Pending = false` is committed first), samples the current authoritative `_attackOrigin`, and calls `MeleeAttack.Execute(in AttackRequest)` with the committed direction and the release tick as `SimulationTick`, so `RecordResolvedDamage` and damage requests carry the release tick. A zero delay releases on the acceptance tick through the same consume-then-execute path.
   * **Cancellation**: death, Downed, phase exit and explicit combat disable cancel the swing without refunding the cooldown. Changing the active Main Hand weapon discards it immediately, and a swing whose weapon no longer matches at the deadline is consumed without damage. A failed or rejected execution marks presentation cancelled through `LastAttackCancellationTick`, exactly like a failed ranged release. Defense prevents same-tick acceptance but does not cancel an accepted swing. A cleared or reconfigured strategy does not stall or alter the pending swing.
   * **Host Migration**: `PendingMeleeRelease` is a networked value restored with the avatar; the Fresh Spawn initialization resets it only for non-restore spawns, like `PendingRangedRelease`.
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

Player ranged attacks have one gameplay-owned acceptance-to-release timeline. `RangedAttack.Execute`
remains an immediate executor for existing enemies/tests/other consumers; scheduling belongs only to
the player combat boundary. No Animator, animation event, VFX, local input or RPC releases a shot.

1. **Input and facing**: transport remains `PlayerNetworkInput`. Movement resolves the normalized contextual `FacingDirection` before combat; it is locked at acceptance.
2. **Acceptance**: ready State Authority captures the ranged config and resolved statistics by value: direction, prefab GUID, impact mask, spawn offset, speed, lifetime, damage/type, range and knockback. The existing presentation fields capture the original Main Hand catalog identity, acceptance origin/tick and sequence.
3. **Deadline and cooldown**: `ReleaseTick = AcceptedTick + ceil(AttackReleaseSeconds / Runner.DeltaTime)`. Attack Interval begins now, not at release. An outstanding shot blocks another acceptance even if that interval expires first; acceptance validates finite origin/direction/config/timing before committing anything.
4. **Continuation/cancellation**: the match phase comes from the runner-scoped `NetworkSpawnManager.MatchController` (the match is a spawned object, not necessarily a component on the runner). Before input processing, State Authority advances the pending value on forward ticks. Missing input, a cleared/reconfigured strategy or an Equipment switch cannot stall or alter it. Movement continues. Death, Downed, phase exit and explicit combat disable cancel it without refunding cooldown. Defense prevents same-tick acceptance but does not cancel an accepted wind-up.
5. **Consume and spawn**: at or after the exact deadline, sample current authoritative AttackOrigin plus committed offset along committed aim, and use the containing avatar's current EntityId. Commit `Pending = false` before calling the spawner. Invalid release origin, absent spawner or spawn failure consumes the attempt and marks presentation cancelled; no perpetual pending state or retry loop remains.
6. **Spawner validation**: `FusionProjectileSpawner` requires State Authority and a forward tick. A committed request supplies prefab/mask directly rather than the newly equipped config; legacy requests without a prefab retain the configured fallback. Validate finite projectile parameters before `Runner.TrySpawn`.
7. **Pre-initialization**: `onBeforeSpawned` initializes `NetworkProjectile` with the committed request and mask before replication, retaining its existing collision and trajectory rules.
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
historical references. Weapon-specific grip point, angular correction and attack animation set
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
-> PlayerAnimatorView: melee trigger / ranged confirmed-phase seek + pinned weapon configuration
-> Main Hand Combat Animator layer
-> RightHand transform
-> MainHandGrip
-> MainHandWeaponVisual
```

`DirectionalAttackAnimationSet` owns exactly six static clips in N, NE, NW, S, SE, SW
order; completeness requires every clip. `WeaponDefinition.Presentation` holds one
optional set reference instead of six clips. A complete set enables `HasGenericAttack`
and replaces only the six neutral `GenericAttack_*` slots in the local per-Animator
override controller. The generic `Attack` route is the only attack route and depends only
on `HasGenericAttack`; an unarmed weapon or missing/incomplete set disables it
and restores placeholder slots. Arming Sword, Rapier, Magic Wand, Magic Sword, Long Sword, Zweihander,
Great Hammer, Magic Staff, Spellbook, Long Bow and Compound Bow reference their respective Sword1H, Rapier, Wand, MagicSword, LongSword,
Zweihander, GreatHammer, MagicStaff, Spellbook, LongBow and CompoundBow sets. Rondel Dagger and Magic Cinquedea
reference the same Dagger asset containing the generated Rondel clips. Reassigning a set changes
presentation without editing `Character.controller` or branching on weapon identity.
`DirectionalAnimationGenerator` bakes each set from one south-authored `<Weapon>_Attack.anim`:
it rotates the RightHand position trajectory (values and tangents) per facing, keeps the
RightHand rotation art, derives MainHandGrip, hand sprite and north sorting from the facing's
idle, keeps the source clip length, and always emits a one-shot clip. The generator takes the
weapon's `WeaponHandedness` as an explicit input and never branches on weapon identity.
For one-handed weapons only the `RightHandPivot/RightHand` hierarchy is part of the output; other
source curves, such as the LeftHand motion authored in `MagicSword_Attack.anim`, are dropped because
LeftHand carries `OffHandGrip` and belongs to separately authored off-hand clips. Spellbook is baked this way with
`Tools/Animations/Generate Spellbook Directional Attacks`: it is one-handed on the `HandHeld` rig like Magic Wand, its
`Spellbook_Attack.anim` source keeps a 1.0 s one-shot length, and the LeftHand motion that source authors is dropped.
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
Long Bow, Compound Bow and Light Crossbow are baked by the weapon-driven rig described in Weapon rig below: the weapon owns its pose and both
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
the clip's frame grid. `LongSword_Attack.anim` and `Zweihander_Attack.anim` hold the blade across the facing:
both hands turn rigidly on a handle that rests transverse to the facing in S, with the tip toward the screen
left, and the strike brings the tip onto the facing. Both therefore use `AngleCorrection` `180`, like Great
Hammer. Long Sword grips `(0, -0.46875)` and `(0, -0.71875)`, the first and last of the five 3 px handle rows
(22 and 26) of the 13x30 px `LongSword.png`; that 4 px handle is shorter than the ~5.5 px spacing its source
animates, so its re-derived second hand stays within 3.5 px of the authored path. `Zweihander.png` has a
3 px handle between guard and pommel, and Zweihander grips `(0, -0.4375)` and `(0, -0.75)` (rows 23 and 28,
5 px apart, matching the ~5.3 px its source animates). Each source remains its weapon's single south source
and bakes through the same two-handed contract without weapon-specific code. The
second-hand sprite is not part of the output: the LeftHand layer keeps owning it, so a walk cycle played
during an attack can still move the drawn second hand away from the handle.
`GreatHammer_Attack.anim` holds the hammer the same way: at rest its hands lie on a horizontal handle,
the head toward the screen left in S, and the strike at 0.55 s brings the head onto the facing. Great Hammer
therefore uses `AngleCorrection` `180` with grips `(0, -0.375)` and `(0, -0.8125)` (rows 22 and 29 of the
17x33 px `GreatHammer.png`, 7 px apart, the second on the last handle row above the collar) and `BladeTip`
`(0, 1)`. It is a two-handed `HoldsSecondaryGrip` weapon: both authored hands turn rigidly together, so the
bake re-derives the second hand onto the handle. Its clips keep the source's 1.05 s length rather than its
1.8 s attack interval. Its attack VFX is a provisional Slash alignment (see below).
The second hand draws over the handle and under the main hand: sorting order 25 in front facings (weapon 20,
attack VFX 21, main hand 30) and -5 in north facings (weapon -10, main hand -2), leaving the next slot for
its glove.
The legacy per-category routes (`LegacySword-Attack`, `LegacyRanged-Attack`) are retired: every catalog
weapon, melee or ranged, uses the generic `Attack` route through its `DirectionalAttackAnimationSet`.
`Character.controller` keeps no `WeaponAnimationCategory` parameter and `WeaponDefinition.Presentation`
serializes no animation category. `HasGenericAttack` is local presentation state,
not a replicated or authoritative combat decision.

Melee and ranged presentation both begin at confirmed acceptance, not at the damage or projectile spawn,
and both follow the shared release timeline (`HasReleaseTimeline`). A melee attack without a release timeline
(legacy or enemy consumers) keeps the trigger route. Local mouse input never starts the animation. Proxies
observe the same replicated sequence. Attack
clips are one-shot presentation only. They do not apply damage or emit gameplay decisions, and
Animation Events are not part of hit timing. The confirmed attack snapshot carries the deterministic
Main Hand catalog index captured at melee or ranged acceptance. Animation, held Main Hand
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
not the currently equipped Set. An attack without a release timeline still waits for the matching RightHand clip and
samples its phase minus `StartSeconds` on the *existing* `VisualRoot` Animator root. Player melee and ranged attacks
instead sample the shared confirmed elapsed clock minus rounded wind-up duration plus `ReleaseLeadSeconds`: a release-relative
art lead, never an independent start timer or simulation authority. The VFX clip binds only `AttackVfx/SpriteRenderer.m_Sprite`, never hand transforms,
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
the grip to blade tip reach. Both two-handed sword alignments are fitted to the blade tip of their
six clips as presented with the blade across the facing and mirrored across its own axis in NW/SW:
each pose places the arc center where the tip path `grip + reach * axis` keeps a constant radius for any
blade reach, and its reach offset is that center's distance behind the grip.
Great Hammer's attack VFX is provisional: until Art delivers a dedicated Smash/Impact visual, its own
`GreatHammerSlashAttackVfx` alignment reuses the shared Slash visual, unchanged, and must be replaced by that
visual when it exists. The alignment is fitted the same way to the head (`BladeTip`) of its six clips. The hammer
winds up until 0.3s, strikes from 0.4s and its head reaches the facing, the impact, at 0.55s in every facing;
the alignment starts at 0.26s, so frame 0 anticipates the windup apex, frames 1-2 cover the strike up to the
impact and frame 3 the follow-through, mirrored like the two-handed swords. Rapier aligns the four 50 ms frames of the Thrust visual from 0.19s,
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
Spellbook is the third Cast Flash consumer and the first on a melee attack: `SpellbookCastFlashAttackVfx` aligns the same
`CastFlashVfxVisual` with its own cast. Its `ReleaseLeadSeconds` stays `0`, so its `AttackReleaseSeconds` equals
`StartSeconds` (0.625). Its clips thrust the main hand forward from 0.5s, arrive at the strike hold
at 0.65s and hold it until 0.75s in every facing, then recover until the 1.0 s end. Each facing anchors the main-hand
grip at that hold with the grip to top-edge axis as rotation and no reach offset, so the flash lands on the book's top
edge. `BladeTip` is `(0, 0.46875)`, the middle of the two top pixels of the 16x16 px `Spellbook-Front.png`; the grip
is `(0, -0.15625)`, the center of the lowest solid row of its center columns, and the book is held top toward the target
with `AngleCorrection` `-90`. The four frames start at 0.625s, so frame 0 ignites as the book arrives and frame 1 bursts
on the hold; frames 2-3 fade in place while the hand recovers and the flash ends at 0.825s, inside the clip (the
presenter drops an effect that does not fit). Poses are never mirrored and sort like the wand's (21 in front facings,
-9 in north facings). The weapon definition has no directional art, so only the Front sprite is used for the icon, the
world sprite and the held book; `Spellbook-Back.png` is imported but unreferenced. The flash is provisional: it reuses the
shared Cast Flash art until Art delivers a dedicated spellbook visual.
Long Bow is the first Bow Shot consumer. `VFX-BowShot.png` is four 96x96 px cells at 16 PPU with centered
pivots: a release flash, then a streak, rings and remnants that travel along +X from the flash center. That
center, `(-0.875, -0.03125)` in sprite local units, is the `BowShotVfxVisual` origin in every frame.
`LongBowBowShotAttackVfx` plays the four 50 ms frames from 0.45s, the end of the Long Bow stringing sequence
where the string hand releases. This authored phase calibrates the weapon's gameplay release
configuration; stringing is presentation, not the runtime timing authority. Each facing
anchors the bow's pivot at that release: the grip on `WeaponPose`, with the facing as rotation, because the
presenter turns the bow along its shooting axis. The reach offset is 0.09375, from the grip to the limb's
front edge on the center column of `LongBow.png`, so the flash leaves the bow's front in every facing. NW
and SW mirror like the bow, and sorting sits just above the held bow (21 in front facings, -9 in north
facings). The shot is a release impulse, not a trajectory, so neither the blade reach nor the projectile's
range sizes it; the bow configures no blade tip for it.
Compound Bow is the second Bow Shot consumer: `CompoundBowBowShotAttackVfx` aligns the same `BowShotVfxVisual`
with its own attack. It starts at 0.4s, the end of the Compound Bow stringing sequence, and anchors each facing
at `WeaponPose` in that clip at the release, with the facing as rotation. Its reach offset is also 0.09375, from
the `(0, 0.09375)` grip to the limb's front edge on the center column of `RecurveBow.png`. Mirroring and sorting
follow the same rules as Long Bow's.
Light Crossbow is the third Bow Shot consumer: `LightCrossbowBowShotAttackVfx` aligns the same `BowShotVfxVisual`
with its own attack. Its baked clips are 0.5 s: the authored recoil at 0.2 s, delayed by the 0.1 s weapon-driven
blend, is both the gameplay release (`AttackReleaseSeconds` 0.3) and the calibrated key. The 0.2 s shot would end
exactly where the clip does, and the presenter drops an effect that does not fit inside the clip, so the art
ignites 25 ms before the release (`ReleaseLeadSeconds` 0.025, `StartSeconds` 0.275), like Magic Wand's flash, and
ends at 0.475 s. The crossbow has no stringing art, so it configures no `WeaponAttackSpriteAnimation`. Each facing
anchors at `WeaponPose` at the release, with the weapon's shooting axis as rotation. The authored hands carry a constant
4.434 degree rotation that `WeaponPose` inherits, so that axis leaves the facing by 4.434 degrees in every facing and
the poses follow the weapon rather than the facing. The reach offset is 0.65625, from the grip to the front tip
of `LightCrossbow.png` on its center column. Mirroring and sorting follow the same rules as the bows'. Loaded and
reload presentation (GD 09 section 13) is not part of this alignment.
Each alignment uses poses fitted to its own directional attacks; a weapon without an alignment has no VFX reference. The effect clears on interruption,
defeat, disable or completion. Proxies observe the same confirmed attack snapshot; no VFX-only
network state, Animator layer/state, second Animator, animation events or gameplay timing authority
is introduced.

Attack VFX tint is local presentation derived from the confirmed attack, so a neutral sprite serves elemental
variants without new art. `AttackVfxTintPalette` (`Assets/Scriptable Objects/AttackVfxTintPalette.asset`) is
static configuration with exactly one entry per `DamageType` value; `TryValidate` rejects a missing or duplicate
entry and `TryGetTint` is an allocation-free scan. `PlayerAttackVfxPresenter` references it through a serialized
`_tintPalette`, validates it in `OnEnable` and disables itself with a logged error when it is absent or incomplete.
When it resolves a confirmed attack it reads the damage type of the weapon in that attack's snapshot, never the
current Equipment, and sets the `AttackVfx` renderer color from the palette; `Clear` resets the color to white so
a tint never outlives its effect or reaches the next one. The renderer keeps the default Sprites material, whose
vertex color multiplies the sprite, and the VFX clips bind only `m_Sprite`, so they never animate the color. The
presenter hardcodes no damage type, weapon or archetype and the tint adds no networked state. The palette colors
are provisional art values until Art approves them: Physical is white, so every physical effect keeps its authored
colors; Magical is a soft arcane `(0.75, 0.6, 1, 1)`; TrueDamage is white until it has an elemental look.

Visual authoring keeps those responsibilities explicit. The presentation grip point is
serialized in `WeaponDefinition` in the weapon sprite's local units. It identifies the
point inside the visible handle that must coincide with `MainHandGrip`, so grip tuning
remains per-weapon static configuration instead of a player-prefab override or code
constant. The animated hand moves the complete weapon pose; the internal grip
point must not be used to compensate for an incorrect hand animation.

the six discrete visual directions (N, NE, NW, S, SE, SW) resolved by `CharacterVisualDirectionResolver`
serve as the common facing buckets for body, hands and held visuals. The shield remains under
`OffHandGrip`. The authored south source `Shield_Block.anim` animates only the
`LeftHandPivot/LeftHand` transform. `DirectionalAnimationGenerator.GenerateOffHandAssets` bakes it into
non-looping `Shield_Defend_{N,NE,NW,S,SE,SW}` clips that turn its position trajectory with the facing under
the Main Hand rule while keeping its rotation art, depth and key times, so south reproduces the source and a
state that keeps playing a clip holds its final pose. The generator rejects an off-hand source that animates
anything else. As idle clips key `MainHandGrip` on the drawn right hand, each Defend clip keys `OffHandGrip`
on its facing's drawn LeftHand idle anchor and keeps that idle hand sprite, so the shield rides the drawn hand.

The `LeftHand` Animator layer owns defense presentation: its `Defend` clips override the locomotion
`OffHandGrip` keys while `IsDefending` is true. Their initial grip anchors match the facing's Idle keys.
Its `Defend` state blends the six clips on
`MoveX`/`MoveY` at the locomotion facings, entered from either locomotion state and left for the matching one
with instant transitions on the `IsDefending` bool; Base Layer and `RightHand` never observe it. Movement and
Main Hand locomotion continue while defending. `PlayerAnimatorView` only mirrors
`PlayerShieldDefenseNetworkController.IsDefending` into that parameter, so every peer presents the replicated
state and Set change, shield removal, defeat or phase exit leave the pose when gameplay clears it.
The held shield's six facing sprites live in a `DirectionalShieldSpriteSet` presentation asset referenced by
the shield's `LootDefinition.DefenseSprites`, separate from the gameplay `ShieldDefinition`.
`PlayerWeaponPresenter` shows the active Off Hand shield's sprite for the visual direction in Idle, Walk and
Defend; the world sprite is only a fallback when directional art is unavailable. Defense changes the
authored hand pose, not the selected shield art.

Each Defend clip also keys the LeftHand renderer's sorting order for its facing: the base order 4 where
the raised hand stays beside the body (S, SE, NW), the north main hand's -2 behind the body where it
turns away from the view (N, NE), and 32 in SW, where it crosses in front of the main hand (30) and its
glove (31); the glove keeps following one slot above. In front facings `PlayerWeaponPresenter` draws the
Off Hand item at least two slots over its hand (`max(20, hand + 2)`), so locomotion keeps order 20 and
the SW shield covers the main hand; back facings keep -10.
The Animator never owns mitigation or coverage rules. Both held
renderers stay on the existing `Characters` Sorting Layer and derive front/back order from the
resolved visual bucket.

### Confirmed ranged presentation clock

Installed Fusion 2.1.1 XML defines `LocalRenderTime` and `RemoteRenderTime` relative to Tick 0.
Projectiles have no Input Authority: use LocalRenderTime on State Authority, RemoteRenderTime on
all observing Clients (including the predicted player's Input Authority). The latest confirmed
attack snapshot is not presented before its acceptance tick reaches that clock. Observation latency
never restarts its phase at zero. `AttackTiming.ClipSeconds` maps the rounded wind-up to the original
weapon's authored release point, then preserves recovery speed; only the RightHand layer is sought.
Locomotion and off-hand defense keep their existing owners.

Ranged and melee animation, held-weapon stringing and release VFX read this one clock, and the gameplay
release (projectile spawn or melee damage) happens on the same deadline. Bow VFX has zero lead;
wand/staff Cast Flash ignites 0.025 s ahead of gameplay release, preserving the authored 0.325/0.875 s
alignment without making VFX timing gameplay configuration. Ranged VFX never rewinds an observed
impulse and clears on completion or authoritative cancellation. No new VFX network state is added.
The replicated cancellation tick also ends the ranged attack pose and pin; a failed release uses
the same presentation-stop contract.

`Spawned` baselines completed sequences without replay. Only a pending ranged wind-up or melee swing reconstructs
presentation from its saved phase through `AttackPresentationResumed`; that event is separate from
`AttackPerformed`, so migration/late spawn never replays attack-start audio. Normal attack-start audio
continues at acceptance with the original weapon identity. Dedicated projectile-release audio is
not implemented here. Sequence replication retains the existing latest-attack semantics, not an event
history: intermediate attacks missed entirely between snapshots are not reconstructed.

### 1. Damage Feedback Visuals
When a character takes damage (authoritatively confirmed by `Health` changes on State Authority):
* Presentation components (`PlayerDamagePresenter`) trigger procedural feedback.
* **Sprite Flash**: Temporarily overrides the character's material colors to a bright flash color to signify a hit.
* **Scale Pulse**: Briefly scales the character's transform down/up to provide physical impact feedback.
* These reactions run completely client-side in the presentation loop (`Render` or via network property changed callbacks).


Players do not go from Active straight to definitive Defeat. The lifecycle is
**Active -> Downed -> definitive Defeat**; the pipeline below describes the final step and
only runs when the Downed reserve is exhausted.

**Downed state.** `PlayerDownedStateNetworkController` owns `[Networked] IsDowned`, `DownedHealth`
and `DownedCycle`. Only State Authority writes them; `Spawned()` skips fresh initialization on
Host Migration restore spawns.
* `Health` stays `0` while Downed and `PlayerCharacter.IsAlive` stays `true` (`Health > 0 || IsDowned`).
  Systems that must distinguish the two read `PlayerCharacter.IsDowned` (via `PlayerDownedGate`).
* **Damage routing.** The hit that takes `Health` to zero enters Downed and is non-fatal; its
  excess damage is discarded and the reserve starts full (`_initialDownedHealth`, 75 baseline).
  While Downed, reserve damage is `mitigated damage x _downedDamageMultiplier`, after the normal
  mitigation. The hit that depletes the reserve is fatal.
* **Drain.** State Authority drains the reserve each tick (`_downedDrainPerSecond`, 2.5/s baseline).
  Depletion clears `IsDowned` in the same tick and then resolves definitive Defeat exactly once.
  Pure rules live in `DownedHealthRules`.
* **No corpse or loot while Downed.** Corpse conversion runs only on definitive Defeat.
* **Rejected while Downed:** conventional healing (`CanReceiveHealing`), and status effects
  (`CanReceiveStatusEffects` is `IsAlive && !IsDowned`; this is a predicate only, no CC system exists yet).
* **Action gates** (`PlayerDownedGate`, enforced on State Authority): primary attack, shield
  defense, equipment/weapon-set changes, consumables, loot transfer, loot drop and world
  interaction. Extraction is intentionally allowed.
* **Presentation.** Weapon visuals are hidden while Downed and restored on exit
  (`PlayerWeaponPresenter`, `PlayerAttackVfxPresenter`). Voluntary movement is limited, see
  `PlayerMovementArchitecture.md`.
* **Disconnect and Host Migration.** A disconnected Downed player keeps draining, see
  `RaidDefeatAndSpectatorArchitecture.md` and `HostMigrationRecoveryArchitecture.md`.
* **Full contract.** Ownership of every transition, recovery, Accelerated Resolution, attribution,
  extraction and Host Migration rules are defined in `DownedAndReviveArchitecture.md`.
* **Out of scope (follow-ups):** Revive, Self-revive, Accelerated Resolution, a dedicated Downed
  pose/HUD, and PvP last-hit attribution.

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
| `arming_sword` | 1 | `PlayerMeleeAttackConfig` | Sword1H set |
| `rapier` | 1 | `PlayerMeleeAttackConfig` | Rapier set |
| `magic_sword` | 1 | `PlayerMeleeAttackConfig` | MagicSword set |
| `long_sword` | 2 | `PlayerMeleeAttackConfig` | LongSword set (two-handed) |
| `zweihander` | 2 | `PlayerMeleeAttackConfig` | Zweihander set (two-handed) |
| `great_hammer` | 2 | `PlayerMeleeAttackConfig` | GreatHammer set (two-handed) |
| `rondel_dagger` | 1 | `PlayerMeleeAttackConfig` | shared Dagger set (Rondel clips) |
| `magic_cinquedea` | 1 | `PlayerMeleeAttackConfig` | same Dagger set |
| `long_bow` | 2 | `RangePlayerAttackConfig` | LongBow set (two-handed, held by the left hand) |
| `compound_bow` | 2 | `RangePlayerAttackConfig` | CompoundBow set (two-handed, held by the left hand) |
| `magic_wand` | 1 | `RangePlayerAttackConfig` | Wand set |
| `magic_staff` | 2 | `RangePlayerAttackConfig` | MagicStaff set (two-handed, authored second hand) |
| `spell_book` | 1 | `SpellbookMeleeAttackConfig` | Spellbook set |

Grip points are expressed in sprite-local units from the centered pivot to the point that must
coincide with the owner of the weapon pose (`MainHandGrip` by default). Vertical weapon art uses a `-90` degree correction to align its
forward axis with the presenter's `+X`; the two-handed weapons whose sources hold them across the facing
(Long Sword, Zweihander, Great Hammer) use `180` instead. Bow art spans its limbs horizontally and shoots along sprite
`+Y`, so every bow uses the same `-90` correction.
A left facing mirrors the held weapon across its own art axis (sprite `+Y`), not across the facing axis.
`PlayerWeaponPresenter` and `DirectionalAnimationGenerator` share `PlayerWeaponPresentationMath.ResolveAngleCorrection`:
under the reflected facing pivot the mirrored visual takes `-180 - AngleCorrection`. Art laid along the
facing (`-90`) is therefore unchanged (`-180 - -90 = -90`), while art held across the facing keeps its side
and its strike toward the facing instead of flipping to the opposite side.
These values are static per-weapon presentation data and do not introduce LootId branches in the presenter.

### Weapon rig

The equipped Main Hand weapon has exactly one held visual hierarchy (`MainHandWeaponVisual` →
`WeaponSprite`). `WeaponDefinition.Presentation.Rig` (`WeaponRig`, static presentation data, never
replicated) selects what owns its pose:

- `HandHeld` (default): the main hand. The visual follows `RightHandPivot/RightHand/MainHandGrip`. Every
  weapon except the bows uses it.
- `WeaponDriven`: the weapon. The visual follows `WeaponPose`, a transform on the Animator root beside the
  hand pivots and inside no hand. Long Bow and Compound Bow use it.

`PlayerWeaponPresenter` reparents the same visual under the selected owner when the presented weapon
changes, and back to `MainHandGrip` when unarmed. The facing rotation, the left-facing Y mirror, `GripPoint`
and `AngleCorrection` (resolved for the mirror by `ResolveAngleCorrection`, so the held weapon mirrors across
its own art axis) apply unchanged under either owner. The rig is presentation only: equipment still
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
  height during its authored draw hold (the first two equal consecutive main-hand keys). A source that authors
  no hold, such as Light Crossbow, whose hands recoil together at the shot, aims from its first key, the ready pose.

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
0.7 s to 0.9 s, its attack interval, and the authored release plays 0.1 s later. Compound Bow keeps its own
authored timing: its clip grows from 0.6 s to 0.8 s and is not stretched to its 1.8 s attack interval, because
the gameplay cooldown and the presentation length are separate responsibilities. The Animator controller
and every hand-held weapon are unchanged.

Outside attacks, `WeaponPose` and `OffHandGrip` rest in the drawn left hand. The Idle and Walk body clips
key both positions, stepped, at every LeftHand sprite frame of the matching
`LeftHand_<Motion>_<Facing>` clip, on the opaque-pixel centroid of that frame, and hold the final
anchor through the body clip's end. `WeaponPose` also keys rotation to zero. The
`Tools/Animations/Generate Weapon Pose Locomotion` menu writes those keys; no runtime item-specific
offset or branch is needed. A weapon-driven attack bake requires none of them.

Long Bow grips `(0, 0.125)` of the 27x7 px `LongBow.png`: the center of the three-row limb in the sprite's
center column (outline, wood, outline), 2 px above the centered pivot. The string lies on the bottom row.
After the `-90` correction the bow shoots along the facing in every direction. The left-facing mirror
flips only its symmetric limbs, and the grip lies on the shooting axis, so NW and SW keep the
string → bow → target order.

Light Crossbow bakes from `Crossbow_Attack.anim`, the south source authored for its `LightCrossbow.png` art, into
its own `LightCrossbow_Attack_<Facing>` clips; `DirectionalAnimationGenerator` takes that source name explicitly. Unlike
the bows it is a recoil, not a draw: both hands rest together, kick back at 0.2 s and settle, and it authors no draw
hold, so its aim center is the string hand's ready height. It is a two-handed weapon-driven weapon (`FollowsAuthoredMotion`,
`AngleCorrection` `-90`) held by the left hand. It grips `(0, -0.125)` of the 16x17 px `LightCrossbow.png`: the center of the
plain foregrip rows of its 2 px wide stock (rows 4-8 from the bottom, between the limb bar of row 9 and the band of row 3),
2 px below the centered pivot. After the `-90` correction the crossbow points along the facing, plus the 4.434 degree
rotation its authored hands carry.

Compound Bow bakes from `RecurveBow_Attack.anim`, the south source authored for its `RecurveBow.png` art, into
its own `CompoundBow_Attack_<Facing>` clips; `DirectionalAnimationGenerator` takes that source name explicitly.
The source is the same gesture as Long Bow's with its own timing: the draw hold spans 0.25 s to 0.3 s and the
string hand draws to chest height rather than chin height. Its lower aim center therefore brings the north
facings' bow closer to the body, mostly behind the torso, while the draw still points at the target. Compound
Bow grips `(0, 0.09375)` of the 23x6 px `RecurveBow.png`: the center of the three-row limb in the sprite's
center column, 1.5 px above the centered pivot, with the string on the bottom row.

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

Compound Bow uses `CompoundBowStringingAttackSpriteAnimation` over the four `Weapon-CompoundBow-Stringing.png`
frames (rest, two partial draws, full draw), stored like Long Bow's in the world sprite's orientation (limb up,
string on the bottom row) at 16 PPU with point filtering. Each pivot sits 3 px below the top of the limb, on the
center column, so the `(0, 0.09375)` grip stays on the limb's center. The frames follow its own baked attack, not
Long Bow's: the draw starts at 0.1 s, reaches the partial draws at 0.2 s and 0.25 s and the full draw at 0.3 s
while the hands peak at 0.35 s, holds, and releases at 0.4 s, when the string hand leaves the hold. Its rest frame
spans the world sprite's 23 px and grip, but its art is one row deeper than `RecurveBow.png`, so the string shifts
by one pixel when the sequence starts and at the release.

There is no weapon animation category: attack presentation varies only by the weapon's
`DirectionalAttackAnimationSet`, with no per-weapon or per-category Animator route.

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
