# Environmental Hazards & Traps Architecture

## Context and Decision

Environmental hazards and traps (e.g. `BaseTrap`, `SpikesTrap`, `DartTrap`) are authoritative network entities (`NetworkBehaviour`) placed in scenes to add tactical level hazards to Project Grimhold.

Traps execute a synchronous, tick-driven state machine controlled exclusively by State Authority in `FixedUpdateNetwork`. State changes, phase durations and active triggers are synchronized over the network using `[Networked]` properties (`State` and `PhaseTimer`). Presentation components consume this replicated state without modifying simulation logic.

## State Machine (`TrapState`)

All traps transition through a standard four-phase lifecycle defined in `BaseTrap`:

```text
       [Ready]
          │ (OnTriggerEnter2D / Player Detection)
          ▼
    [Telegraphing] ── (activationTime expired) ──► [Active]
                                                     │
                                                     ▼ (resetTime expired)
    [Ready] ◄── (cooldown expired) ───────────── [InCooldown]
```

| Phase | Description | Detection Collider | Presentation / Effect |
| :--- | :--- | :--- | :--- |
| **`Ready`** | Idle state waiting for target detection. | Active (`enabled = true`) | Neutral state (Visual: Green tint). |
| **`Telegraphing`** | Warning phase after trigger entry. Prepares attack. | Active / Disabled | Warning cue (Visual: Yellow tint). |
| **`Active`** | Attack phase. Damage or projectile emission occurs. | Disabled (`enabled = false`) | Hazard active (Visual: Red tint). |
| **`InCooldown`** | Recovery phase after attack completion. | Disabled | Cooldown state (Visual: Gray tint). |

## Trap Taxonomy and Implementations

### 1. Base Trap (`BaseTrap`)
* **Network Boundary**: Extends `NetworkBehaviour`.
* **State Authority**: Only State Authority handles state transitions in `FixedUpdateNetwork` using Fusion's `TickTimer`.
* **Trigger Detection**: Uses `OnTriggerEnter2D` on State Authority to set `_triggerEntered` when an entity enters the detection collider.
* **Phase Hooks**: Provides virtual lifecycle methods (`OnEnterTelegraphing`, `OnEnterActive`, `UpdateActive`, `OnEnterCooldown`, `OnEnterReady`) for subclasses.

### 2. Spikes Trap (`SpikesTrap`)
* **Behavior**: Instant area-of-effect damage upon entering the `Active` phase.
* **Target Query**: Uses non-allocating `Overlap` queries (`ContactFilter2D`, `_colliderBuffer`) on its dedicated `_impactCollider`.
* **Target Resolution**: Maps detected colliders to gameplay entities via `EntityRegistry.TryGetEntityId` and `TryGetDamageable`.
* **Deduplication**: Prevents duplicate hits on the same `EntityId` during a single spike pulse using `_processedTargets`.
* **Damage Pipeline**: Constructs an authoritative `DamageRequest` with source `EntityId(0)` (environment origin) and passes it directly to `IDamageResolver`.

### 3. Dart Trap (`DartTrap`)
* **Behavior**: Ranged hazard firing a sequence of networked projectiles during the `Active` phase.
* **Projectile Spawner**: Integrates `FusionProjectileSpawner` and reads static configuration from `RangedAttackConfig`.
* **Burst Control**: Replicates `DartsShot` count and `DartIntervalTimer` (`TickTimer`) across tick updates.
* **Projectile Simulation**: Spawns `NetworkProjectile` entities with source `EntityId(0)`. Darts use the exact same continuous collision cast and kinematic simulation as player and enemy ranged attacks.
* **Ownership**: `TrapInfo` supplies damage (`5`) and Physical damage type. `DartTrap` supplies its effective range (`5`), while `RangedAttackConfig` supplies projectile behavior. The previously serialized attack-config knockback was never passed to the projectile request; this refactor preserves zero knockback.

## Data Contracts and Configuration (`TrapInfo`)

Static configuration parameters are stored in `TrapInfo` ScriptableObjects:
* `activationTime`: Duration (in seconds) of the `Telegraphing` warning phase.
* `resetTime`: Duration (in seconds) of the `Active` hazard phase.
* `cooldown`: Duration (in seconds) of the `InCooldown` recovery phase.
* `damage`: Base damage value passed to `DamageRequest` or `ProjectileSpawnRequest`.
* `DamageType`: Damage classification (Physical, Elemental, etc.).

Runtime state (`TrapState`, `PhaseTimer`) remains separate in `BaseTrap` networked properties and is never stored in ScriptableObject assets.

## Sources of Truth and Network Authority

- **State Authority**: Evaluates trigger collisions, state transitions, `TickTimer` progression, area overlap queries, damage requests, and projectile spawning.
- **Proxy Clients**: Replicate `State` and `PhaseTimer` for rendering and local audio/visual presentation in `Render()`. Proxies do not execute physics queries or trigger damage logic.
- **Environment Origin**: Traps specify `EntityId(0)` as the damage origin to signify environmental hazard damage.

## Presentation Boundary

Visual feedback (sprite color modulation or animation state) is handled in `Render()` by observing the networked `State` property. Presentation does not drive state machine transitions or apply gameplay damage.
