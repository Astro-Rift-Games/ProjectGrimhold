# Player Combat Architecture

## Shared aim direction

`PlayerMovementNetworkController` resolves `AimWorldPosition` after the player's
kinematic displacement and writes the synchronized `FacingDirection`. It runs before
`PlayerCombatNetworkController` in every Fusion simulation tick. Melee and ranged both
validate and consume that same finite, normalized facing; combat does not recompute aim
from cursor input or `_attackOrigin`.

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
All combat behaviors implement the common strategy contract:
* **`IAttack`**: Interface defining the execution strategy for any weapon/ability.
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
Serves as the network boundary for character combat:
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
Equipment. It owns exactly six slots — the two quick weapon slots plus `Helmet`, `Armor`, `Gloves`
and `Boots` — and one active-slot selection that may only reference an occupied weapon slot. Each
slot replicates only the deterministic `LootDefinitionCatalog` index plus one (`0` means empty).
Equip intentions fill Slot 1 and then Slot 2 for weapons and the single matching slot for armor,
while unequip intentions identify one slot and return exactly one unit to `PlayerLootReceiver`.
Input Authority expresses those discrete intentions and State Authority validates and commits them
during `FixedUpdateNetwork`. A rejected operation mutates neither Inventory nor Equipment.

Slot compatibility lives in `EquipmentSlotRules`, not in Loot. `LootCategory` only classifies the
unit (`Weapon`, `Helmet`, `Armor`, `Gloves`, `Boots`); deciding which slot may receive it is an
Equipment rule. `PlayerLootReceiver` is never the source of truth for what is equipped.

Only the active weapon slot resolves `LootDefinition -> WeaponDefinition -> AttackConfig` together
with the participant's confirmed `CharacterAttributeState`. Equipment selects the attribute declared
by `WeaponOffensiveScaling`, calculates effective damage through `WeaponDamageCalculator`, builds a
local, non-replicated `AttackExecutionParameters` value from the active weapon's damage, type,
interval, effective range and knockback, configures the shared `MeleeAttack` or `RangedAttack`
executor, and assigns it
through `TrySetActiveAttack`. Inserting an inactive weapon never reconfigures either executor, and the four
armor slots never reach combat at all: they neither validate the combat dependencies nor
participate in `HasReplicatedWeaponStateChanged`, so equipping or removing a piece cannot rebuild
the strategy or disturb the authoritative cooldown. Slot-selection input travels in the normal
`PlayerNetworkInput` buttons; it uses no RPC and preserves the authoritative cooldown. Any mutation
of any of the six slots advances `EquipmentRevision`, which is what presentation observes.

On Host Migration restore, State Authority resolves the replicated slot identities and the active
slot again, rebuilding the strategy and recalculating effective damage from the restored confirmed
attributes without replaying equipment requests. Effective damage is derived runtime state and is
not replicated or persisted. The armor slots need no
dedicated restore logic — they are ordinary `[Networked]` properties. ScriptableObjects and
presentation state are never replicated.

`TryGetPrimaryAttackStatus` returns no presentable state while `HasActiveAttack` is false.
When presence is authoritative, the query does not require a local `IAttack`; it derives
availability from `IsAttackEnabled`, `ICharacter.IsAlive`, and the replicated cooldown, and
reports the replicated duration snapshot from the attack that started that timer. This allows
proxies to present the same cooldown without knowing strategy identity. `RaidHudPresenter`
clears its attack presentation whenever the query returns `false`, so a neutral player cannot
retain a stale weapon cooldown in the HUD.

### 3. Melee Attack Strategy (`MeleeAttack` & `MeleeAttackConfig`)
Executes instant damage detection in a localized area:
* Reads radius, maximum targets and target mask from `MeleeAttackConfig`.
* Receives damage, type, interval, effective range and knockback through `AttackExecutionParameters`.
* Converts effective weapon range into the query's circle-center offset as `Range - Radius` and rejects `Range < Radius`.
* Passes damage requests directly to the centralized `IDamageResolver`.

### 4. Ranged Attack Strategy (`RangedAttack` & `RangedAttackConfig`)
Generates physical projectiles that traverse the world:
* Reads projectile prefab, speed, lifetime, spawn offset and impact mask from `RangedAttackConfig`.
* Receives damage, type, interval, maximum range and knockback through `AttackExecutionParameters`.
* Integrates a configurable **`ProjectileSpawnOffset`**, configured according to the combined collision bounds of the shooter and projectile, which offsets the initial projectile spawn coordinate in the direction of the aim vector to clear the shooter's own collider bounds.
* Delegates spawning requests to an `IProjectileSpawner` instance.

### 5. Projectile Simulation (`NetworkProjectile`)
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

### 6. Entity Identity (`EntityRegistry`)
A fast-lookup database mapping physical colliders (`Collider2D`) to gameplay entity identities (`EntityId`) and damageable contracts (`IDamageable`):
* Allows the projectile simulation to instantly identify targets without expensive `GetComponent` searches.
* Enables precise owner filtering by checking `BelongsToOwner(Collider2D)`, ensuring a projectile never collides with its shooter or any of its child-objects, while allowing impacts against other players/enemies.
* Keeps explicit damage-collider registration separate from general collider identity. Damage queries use the explicit set when present and retain all-collider fallback behavior for legacy or non-character damageables.

### 7. Fatal PvE Kill Experience

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

### 3. `WeaponDefinition`, runtime parameters and scaling

`WeaponDefinition` is the single source of truth for player weapon `BaseDamage`,
`AttackIntervalSeconds`, effective `Range`, `StaminaCost`, `DamageType`, `KnockbackForce`,
requirements and scaling. `StaminaCost` is validated configuration but is not consumed yet.
Spellbook uses the shared ranged behavior; proximity or area manifestation is outside this contract.

`WeaponOffensiveScaling` stores one `CharacterAttribute` and one non-negative coefficient. A zero
coefficient means no scaling and contributes zero without reading an attribute. A positive
coefficient accepts only Strength, Dexterity or Intelligence and resolves its value from the
confirmed `CharacterAttributeState` already owned by the Raid participant. The provisional rule is:

```text
EffectiveDamage = WeaponDefinition.BaseDamage + (AttributeValue * ScalingCoefficient)
```

`WeaponDamageCalculator` owns only this pure arithmetic. Equipment performs the calculation when it
configures or rebuilds the active player weapon. `MeleeAttack` and `RangedAttack` retain the resolved
runtime value without modifying their shared `AttackConfig`. Non-player executors serialize their
own `AttackExecutionParameters`, keeping their existing behavior independent from Equipment.
Scaling grades remain Game Design concepts and are represented in runtime configuration only by their
resolved coefficient.

---

## Melee Attack Flow

1. **Input Collection**: `PlayerInputReader` latches primary attack input.
2. **Transport**: `PlayerNetworkInput` transports buttons to `FixedUpdateNetwork` via Fusion.
3. **Trigger**: `PlayerCombatNetworkController` processes input. Neutral players update button history and stop. An active player requires authoritative strategy presence, a local implementation, enabled combat, a living character, and an expired `AttackCooldown`.
4. **Execution**: If authorized and ready, calls `MeleeAttack.Execute(in AttackRequest)`.
5. **Direction**: Movement resolves the finite, normalized `PlayerMovementNetworkController.FacingDirection` from the final simulated player position before combat runs in the same tick.
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
2. **Facing**: `PlayerMovementNetworkController` has already resolved `FacingDirection` from the final simulated player position. Invalid or suppressed aim preserves the last valid facing.
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

### Modular Character Composition and Procedural Weapon Presentation

`NetworkPlayer.prefab` owns one `PlayerWeaponPresenter` and coordinates the modular visual
hierarchy under `VisualRoot` alongside the procedural weapon presentation under
`CombatVisuals/WeaponPivot/WeaponSprite`. `NetworkPlayer.prefab` is the productive Raid
avatar; the legacy `NetworkPlayerMelee.prefab` and `NetworkPlayerRanged.prefab` remain only
as historical references. Weapon-specific grip point, angular correction and swing arc
belong to `WeaponDefinition.Presentation`, while the sprite remains sourced from the linked
`LootDefinition`. Player prefabs do not select or override those values.

The character visual structure is modularized under `VisualRoot`:
* **`VisualRoot`**: Houses the single common `Animator` and `PlayerAnimatorView` for the character.
* **Modular Slots**: Contains independent `SpriteRenderer` components for `Legs`, `Body`, `Head`, `LeftHand`, and `RightHand`, sharing a uniform 96x96 canvas and local position origin `(0, 0, 0)`. `LeftHand` and `RightHand` are the sole visual hands of the character and are driven exclusively by the modular Animator clips.
* **Single Animator**: A single common `Animator` on `VisualRoot` acts as the ancestor for all modular slots, driving coordinated animation clips across the six visual directions.
* **`RightHandGrip`**: Located directly under `VisualRoot` and animated by every modular locomotion clip. It provides the live hand position used as the weapon pivot origin without making the weapon a child of the hand renderer.

The inherited attack-driven `PlayerCombatPresenter` is disabled on the base
composition, so equipped weapons do not run an attack swing or alter the visual
Animator. Weapon visuals remain enabled continuously during idle,
movement, and ordinary combat presentation rather than appearing only when an
attack is executed.

`PlayerWeaponPresenter` owns exclusively the procedural weapon presentation
(`CombatVisuals/WeaponPivot/WeaponSprite`). It reads the existing finite,
normalized `PlayerMovementNetworkController.FacingDirection` through `IMovementState`. It
resolves the replicated active slot and its catalog identity through
`PlayerWeaponEquipmentNetworkController`, then follows `LootDefinition -> WeaponDefinition`
to obtain the local static sprite and pose configuration. It does not capture input, add
networked state, or write back to movement or combat.
Every peer, including proxies, derives the same local presentation pose from the
replicated facing. Invalid or zero presentation samples retain the presenter's
last safe direction via `CharacterVisualDirectionResolver.SanitizeFacing`, with
`Vector2.down` as its initial fallback. Unity may enable the visual hierarchy while Fusion
is still instantiating the prefab; during that pre-spawn window the presenter applies the
fallback pose and does not read the networked property until its source `NetworkBehaviour.Object` is valid.

`RightHandGrip` is presentation-only and follows the animated right hand under
`VisualRoot`. Because `VisualRoot/RightHandGrip` and `CombatVisuals` are separate branches, the presenter
converts the anchor world position into the local space of `WeaponPivot.parent` (`CombatVisuals`).
It then composes the pose in this order:

```text
animated RightHandGrip (under VisualRoot)
-> conversion to WeaponPivot parent space (CombatVisuals)
-> visual direction resolution via CharacterVisualDirectionResolver (S, SE, NE, N, NW, SW)
-> continuous 360-degree facing rotation and left-hemisphere reflection
-> weapon grip aligned to the weapon pivot
-> bucket-driven sorting order (Front: S, SE, SW; Back: N, NE, NW)
```

The body origin and sprite bounds are not presentation centers. Moving the
anchor moves the complete orbit without introducing a second center. Within any
visual direction bucket, `WeaponPivot.localPosition` remains discrete and anchored
to the character's hand position for that directional frame, while `WeaponPivot.localRotation`
smoothly tracks the continuous 360° aim direction.

Visual authoring keeps those responsibilities explicit. The presentation grip point is
serialized in `WeaponDefinition` in the weapon sprite's local units. It identifies the
point inside the visible handle that must coincide with `WeaponPivot`, so grip tuning
remains per-weapon static configuration instead of a player-prefab override or code
constant. The animated `RightHandGrip` moves the complete weapon pose; the internal grip
point must not be used to compensate for an incorrect hand animation.

The continuous weapon rotation does not require adding East or West body clips;
the six discrete visual directions (N, NE, NW, S, SE, SW) resolved by `CharacterVisualDirectionResolver`
serve as the common facing buckets for both character animation and weapon positioning.
The weapon renderer stays on the existing `Characters` Sorting Layer and derives its front/back
relative orders (`SortingOrderFront` / `SortingOrderBack`) directly from the resolved visual bucket.

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

### 5. Placeholder Weapon Content Set

`Assets/Scriptable Objects/Loot/Definitions` ships six placeholder weapons used to validate
Weapon Equipment, quick slots, switching, world presentation and the Raid HUD icon. They are
content identities only: they add no attack type, no weapon subtype and no presenter branch.
Each one owns a dedicated `LootDefinition` and a dedicated `WeaponDefinition`. Functional differences
may come from the reused `AttackConfig` plus per-weapon attribute requirements and offensive scaling;
visual differences remain in the static presentation triple.

Sprites come from `Assets/Placeholder/RPG Items 16x16 Pack 1` at the project pixel-art
convention (16 PPU, Point filter, no mipmaps, Tight mesh). Sword and staff cells use a
BottomRight sprite pivot and their art points up-left, which the `-135` angle correction maps
onto the presenter's `+X` forward axis. Spell-book cells use a Center pivot and an upright,
non blade-aligned silhouette, so their angle correction is `0`.

| Loot id | Sprite cell | Attack config | Stance offset | Grip point | Angle |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `recovery_sword` | `swords-16x16_0` | `PlayerMeleeAttackConfig` | `(0, 0)` | `(-0.1875, 0.1875)` | `-135` |
| `longsword` | `swords-16x16_7` | `PlayerMeleeAttackConfig` | `(0, 0)` | `(-0.25, 0.1875)` | `-135` |
| `greatsword` | `swords-16x16_15` | `PlayerMeleeAttackConfig` | `(0, -0.0625)` | `(-0.125, 0.125)` | `-135` |
| `wand` | `staves-16x16_37` | `RangePlayerAttackConfig` | `(0, 0)` | `(-0.1875, 0.0625)` | `-135` |
| `staff` | `staves-16x16_30` | `RangePlayerAttackConfig` | `(0, 0.0625)` | `(-0.1875, 0.125)` | `-135` |
| `spellbook` | `spell-books-16x16_13` | `RangePlayerAttackConfig` | `(0, 0.125)` | `(0, -0.3125)` | `0` |

Grip points are expressed in weapon-sprite local units as the offset from the sprite pivot to
the point that must coincide with `WeaponPivot`. The greatsword grips near the end of its
longer hilt instead of its visual center, the wand grips at the base of its short shaft so it
stays at the hand, the staff grips low on the shaft so most of its length extends forward, and
the spellbook grips below its lower edge so the tome is carried above the hand.

The five equippable placeholders are reachable during development through
`DefaultLootContainerContentTable`, the same route that already exposes Training Sword; loot
containers and breakable objects roll them. `recovery_sword` stays out of loot distribution and
out of the merchant stock, and keeps its single source: the Town recovery grant configured by
`LocalProfilePersistenceConfiguration.RecoveryWeaponLootId`.

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
* **Armor Is Equipment State Only**: `Helmet`, `Armor`, `Gloves` and `Boots` currently carry slot identity and compatibility and nothing else. There is no defence, attribute, requirement, rarity, affix or any other gameplay effect attached to them.
* **Town preparation covers the six slots**: `PreparedEquipmentLoadout` and `TryInitializePreparedEquipment` carry Helmet, Armor, Gloves and Boots alongside the two weapon slots, so armor prepared in Town enters Equipment at spawn exactly like a weapon. Only a weapon is required to launch (`04 - Character Build Design` §15.1); armor is optional and is never granted by the recovery guarantee.
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
