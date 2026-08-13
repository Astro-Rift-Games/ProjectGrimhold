# Player Combat Architecture

## TASK-38 shared aim direction

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
   ├── [AttackSequence, Cooldown Timer]
   ▼
Active Strategy (IAttack: MeleeAttack / RangedAttack)
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
* Delegates the actual attack execution to the currently active strategy component (`IAttack`).

### 3. Melee Attack Strategy (`MeleeAttack` & `MeleeAttackConfig`)
Executes instant damage detection in a localized area:
* Reads static data parameters from `MeleeAttackConfig` (ScriptableObject).
* Delegates short-range target detection to `IAttackTargetQuery` using the settings defined by `MeleeAttackConfig`.
* Passes damage requests directly to the centralized `IDamageResolver`.

### 4. Ranged Attack Strategy (`RangedAttack` & `RangedAttackConfig`)
Generates physical projectiles that traverse the world:
* Reads static parameters (speed, range, lifetime, layers, prefabs) from `RangedAttackConfig` (ScriptableObject).
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

---

## Implementation Status

| Component | Status | Responsibility | Notes |
| :--- | :--- | :--- | :--- |
| **`PlayerInputReader`** | Fully Implemented | Captures local buttons/aim and packs into `PlayerNetworkInput`. | Relies on local Unity input wrappers. |
| **`PlayerCombatNetworkController`** | Fully Implemented | Handles network input collection, TickTimer cooldowns, and strategies. | Requires inspector assignment of characters & strategies. |
| **`MeleeAttack`** | Fully Implemented | Melee execution strategy, queries targets, resolves damage. | Fully data-driven by `MeleeAttackConfig`. |
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

### 1. `MeleeAttackConfig` (ScriptableObject)
Inherits from `AttackConfig`. Validated fields:
* **`_damage`** (float, Min: 0.0): The base damage applied on hit.
* **`_damageType`** (DamageType): Physical, Magical, etc.
* **`_cooldownSeconds`** (float, Min: 0.0): Minimum seconds between attacks.
* **`_inputMode`** (AttackInputMode): Press or Hold.
* **`_range`** (float, Min: 0.1): Spatial offset of the detection circle's center from the attacker origin.
* **`_radius`** (float, Min: 0.1): Detection circle radius.
* **`_maximumTargets`** (int, Min: 1): Maximum number of targets hit in one execute.
* **`_targetLayerMask`** (LayerMask): Layer mask defining which objects are queried.

### 2. `RangedAttackConfig` (ScriptableObject)
Inherits from `AttackConfig`. Validated fields:
* **`_damage`** (float, Min: 0.0): The base damage applied on hit.
* **`_damageType`** (DamageType): Damage type.
* **`_cooldownSeconds`** (float, Min: 0.0): Cooldown between shots.
* **`_inputMode`** (AttackInputMode): Press or Hold.
* **`_projectileSpeed`** (float, Min: 0.1): Travel speed of the spawned projectile.
* **`_lifetimeSeconds`** (float, Min: 0.1): Duration before projectile expires.
* **`_maxRange`** (float, Min: 0.1): Maximum physical distance the projectile can travel.
* **`_projectileSpawnOffset`** (float, Min: 0.0): Distance in front of the attacker origin where the projectile spawns.
* **`_projectilePrefab`** (NetworkPrefabRef): Fusion registered prefab reference.
* **`_impactLayerMask`** (LayerMask): Collision mask including both target characters and blocking obstacle walls.

---

## Melee Attack Flow

1. **Input Collection**: `PlayerInputReader` latches primary attack input.
2. **Transport**: `PlayerNetworkInput` transports buttons to `FixedUpdateNetwork` via Fusion.
3. **Trigger**: `PlayerCombatNetworkController` processes input. On press/hold, it validates `AttackCooldown` (TickTimer) and character alive state.
4. **Execution**: If authorized and ready, calls `MeleeAttack.Execute(in AttackRequest)`.
5. **Direction**: Movement resolves the finite, normalized `PlayerMovementNetworkController.FacingDirection` from the final simulated player position before combat runs in the same tick.
6. **Query Targets**: `MeleeAttack` delegates queries to `Physics2DAttackTargetQuery.FindTargets()`.
   * Center is computed as: `Origin + FacingDirection * Range`.
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

## US-13 fatal defeat contribution

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
* **`DamageRequest`**: A plain C# struct transporting attacker ID, target ID, base damage amount, damage type, direction, hit point, and execution tick.
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

### Shared Player Hand and Modular Character Composition

`NetworkPlayer.prefab` owns one `PlayerWeaponPresenter` and coordinates the modular visual
hierarchy under `VisualRoot` alongside the procedural weapon presentation under
`CombatVisuals/WeaponPivot/{HandVisual, WeaponSprite}`. Both `NetworkPlayerMelee.prefab`
and `NetworkPlayerRanged.prefab` inherit that same component and hierarchy. Variants may
override only weapon-specific presentation data, such as the weapon sprite, stance offset,
grip point, angular correction, and necessary weapon visual adjustments. They do not
duplicate the presenter or the hand composition.

The character visual structure is modularized under `VisualRoot`:
* **`VisualRoot`**: Houses the single common `Animator` and `PlayerAnimatorView` for the character.
* **Modular Slots**: Contains independent `SpriteRenderer` components for `Legs`, `Body`, `Head`, `LeftHand`, and `RightHand`, sharing a uniform 96x96 canvas and local position origin `(0, 0, 0)`.
* **Single Animator**: A single common `Animator` on `VisualRoot` acts as the ancestor for all modular slots. Future locomotion will coordinately animate the five `SpriteRenderer` components across the six visual directions. The previous monolithic animation clips (which bound to path `""`) are obsolete and will be replaced by modular clips in Stage 3.
* **`HandOrbitAnchor`**: Located as a child under `Body` at local offset `(0, 0.45, 0)`. Modular animation clips must animate `SpriteRenderer.sprite` and must not animate `Body.transform.localPosition` to avoid displacing the weapon orbit origin.

The inherited attack-driven `PlayerCombatPresenter` is disabled on the base
composition, so neither weapon variant runs an attack swing or alters the visual
Animator. Hand and weapon visuals remain enabled continuously during idle,
movement, and ordinary combat presentation rather than appearing only when an
attack is executed.

`PlayerWeaponPresenter` continues to own exclusively the procedural weapon presentation
(`CombatVisuals/WeaponPivot/{HandVisual, WeaponSprite}`). It reads the existing finite,
normalized `PlayerMovementNetworkController.FacingDirection` through `IMovementState`. It
does not capture input, add networked state, or write back to movement or combat.
Every peer, including proxies, derives the same local presentation pose from the
replicated facing. Invalid or zero presentation samples retain the presenter's
last safe direction, with `Vector2.down` as its initial fallback. Unity may enable
the visual hierarchy while Fusion is still instantiating the prefab; during that
pre-spawn window the presenter applies the fallback pose and does not read the
networked property until its source `NetworkBehaviour.Object` is valid.

`HandOrbitAnchor` is presentation-only and is positioned manually over the
torso under `Body`. Because `VisualRoot/Body` and `CombatVisuals` are separate branches, the presenter
converts the anchor world position into the local space of `WeaponPivot.parent` (`CombatVisuals`).
It then composes the pose in this order:

```text
base HandOrbitAnchor (under VisualRoot/Body)
-> conversion to WeaponPivot parent space (CombatVisuals)
-> shared elliptical hand orbit
-> variant weapon stance offset
-> continuous facing rotation and shared reflection
-> weapon grip aligned to the weapon pivot
```

The body origin and sprite bounds are not presentation centers. Moving the
anchor moves the complete orbit without introducing a second center. `HandVisual`
and `WeaponSprite` remain children of `WeaponPivot`, so a left-hemisphere reflection
applies to both as one composition. The weapon local position is derived from its
configured grip point after weapon scale and angular correction, keeping that
grip at the pivot through rotation and reflection.

Visual authoring keeps those responsibilities explicit. The hand sprite is
imported with its pivot at the visual center, so the shared prefab normally uses
a zero hand visual offset; `_handVisualOffset` remains an optional relative art
correction rather than another orbit center. `_weaponGripPoint` is serialized in
the weapon sprite's local units and is overridden on the player variant when its
weapon art changes. It identifies the point inside the visible handle that must
coincide with `WeaponPivot`, so grip tuning remains prefab configuration instead of
a code constant. Weapon stance offsets move the complete hand-and-weapon pose and
must not be used to compensate for an incorrect internal grip.

The continuous weapon pose does not depend on adding East or West body clips;
the six discrete visual Animator directions (N, NE, NW, S, SE, SW) remain the visual
facing buckets for character animation. Both weapon renderers stay on the existing
`Characters` Sorting Layer and use the established front/back relative orders.
The hand renderer (`HandVisual`) is always one relative order above the weapon renderer
so the fingers cover the handle at their overlap.

**Temporary Coexistence Limitation**: In the current structural stage (Stage 2),
`VisualRoot/LeftHand` and `VisualRoot/RightHand` represent the base character's modular hands,
while `HandVisual` represents the procedural weapon-grip hand on `WeaponPivot`. During
armed combat states, both representations may coexist until a subsequent presentation stage
coordinates dynamic modular hand hiding/swapping. This is a known temporary visual limitation
and does not affect gameplay simulation or combat authority.

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
  * The defeated player retains its own `NetworkObject`. Its initially unavailable `NetworkLootContainer` receives and verifies the exact temporary-inventory snapshot before that inventory is cleared, then becomes available. A load or clear failure leaves the container empty and unavailable while preserving the inventory; no replacement corpse is spawned.
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
    * `_activeAttackSource` -> Assigned by each playable variant to its active strategy component.
    * `_movementController` -> Reference to `PlayerMovementNetworkController`.
  * **Shared defeat and loot composition**: `PlayerCharacter`, `PlayerLootReceiver`, `PlayerCorpseGenerationController`, `NetworkLootContainer`, `NetworkLootContainerInteractable`, `InteractionPromptMetadata`, and the dedicated interaction trigger share the root `NetworkObject`.
  * The base `NetworkPlayer.prefab` contains shared dependencies but intentionally has no active `IAttack` strategy.
  * `NetworkPlayerMelee.prefab` adds `MeleeAttack` and assigns it as the active attack source.
  * `NetworkPlayerRanged.prefab` adds `RangedAttack`, assigns it as the active attack source, and composes its projectile-spawning dependencies.

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
| **Defeated player keeps network identity** | Implemented | `PlayerCharacter.HandleDeath` delegates to the co-located `PlayerCorpseGenerationController`; no replacement object is spawned. | Yes (Host/Client identity observation) |
| **Persistent inspectable player body** | Implemented | `PlayerDefeatPresenter.HideBodyVisualAfterTransition` is `false`; the co-located generic loot endpoint becomes available only after authoritative conversion. | Yes (final pose and interaction on both peers) |

---

## Known Limitations and Technical Debt

* **Layer Configuration Dependency**: The system requires strict layer separation. If targets or obstacles are not on the correct layers specified in `MeleeAttackConfig` and `RangedAttackConfig`, collision queries will fail to report hits.
* **Component Casting**: Strategies (`MeleeAttack` and `RangedAttack`) rely on `MonoBehaviour` fields cast to interface references at runtime, which requires careful assignment in the Inspector. Missing component assignments on the prefab will lead to validation errors.

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
