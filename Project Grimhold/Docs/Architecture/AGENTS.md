# AGENTS.md — Project Grimhold

## 1. Project identity

You are working on **Project Grimhold**, a multiplayer 2D top-down extraction RPG for PC.

Core product direction:

- Genre: PvPvE extraction RPG.
- Setting: serious medieval fantasy.
- Visual style: top-down pixel art.
- Character target: eight-directional animation.
- Visual references: Tinkerlands and Enter the Gungeon.
- Structural gameplay reference: Dark and Darker.
- Planned differentiator: optional in-match events inspired by MU Online events such as Blood Castle and Devil Square.
- MVP team:
  - Dani: programming, primarily player systems.
  - Tomás: programming, primarily architecture and other gameplay systems.
  - Jorge: game design and level design.
  - Juan: 2D art.

The MVP must remain achievable for a four-person team. Prefer focused, testable implementations over broad frameworks or production-scale infrastructure that the current task does not require.

## 2. Technical stack

Treat repository files as the source of truth and verify versions before making version-sensitive changes.

Known baseline:

- Unity `6000.5.1f1`.
- C#.
- Photon Fusion `2.1.1`.
- Unity Input System `1.19.0`.
- 2D physics.
- Host/client authoritative multiplayer.
- Git repository managed through GitHub.

Do not add, remove, or update Unity packages unless the task explicitly requires it and the impact is understood.

## 3. Source-of-truth order

Before modifying code, resolve conflicts using this order:

1. Current code, prefabs, scenes, assets, package versions, and project settings.
2. Architecture and contract documents already present in the repository.
3. This `AGENTS.md`.
4. Historical task descriptions, walkthroughs, and stale implementation plans.

Documentation may describe intended architecture or an older repository state. Verify whether each documented component is currently implemented before treating it as existing code.

Important documents to locate and read when relevant:

- `PlayerMovementArchitecture.md`
- `PlayerCombatArchitecture.md`
- `EnemyCombatArchitecture.md`
- `LootInteractionArchitecture.md`
- `BreakableLootArchitecture.md`
- `EnvironmentalHazardsArchitecture.md`
- `RaidMainHudArchitecture.md`
- `RaidInventoryUIArchitecture.md`
- `SessionLifecycleAndSpawning.md`
- `Project_Grimhold_Contratos_Base_v1.md`
- Other files under `Docs/Architecture`
- Task-specific walkthroughs or implementation plans committed to the repository

Do not create a second competing architecture document when an existing document owns that system. Update the existing source of truth when an architectural change is implemented.

## 4. General engineering principles

Code must be:

- Clean and readable.
- Modular and cohesive.
- Decoupled from concrete player, enemy, UI, or presentation implementations.
- Scalable only where a real extension point exists.
- Compatible with Unity serialization and Unity lifecycle rules.
- Safe for Photon Fusion prediction and resimulation.
- Efficient in frequently executed paths.

Prefer composition over deep inheritance.

Create abstractions only when they protect an actual boundary or have more than one meaningful implementation. Do not add speculative interfaces, managers, factories, service locators, generic pipelines, or event buses.

Use ScriptableObjects for stable configuration, not runtime mutable state.

Keep public APIs small. Use private serialized fields when Inspector configuration is required. Validate serialized configuration with `OnValidate`, assertions, or clear initialization failures where appropriate.

Comments and XML documentation must be written in technical English. Comments should explain non-obvious intent, constraints, authority, or resimulation behavior, not restate the code.

## 5. Photon Fusion rules

### Authority

- Input Authority captures and transmits player intention.
- State Authority validates and decides authoritative gameplay results.
- Proxies consume replicated state and presentation data.
- A client must never authoritatively confirm damage, pickups, spawns, despawns, cooldown completion, or interaction results.

### Simulation

Network gameplay simulation must run in `FixedUpdateNetwork`.

Do not use the following as the authoritative path for network gameplay:

- `Update`
- `LateUpdate`
- Input System callbacks
- Animator events
- presentation events
- RPCs sent every frame

Use Fusion simulation time, ticks, or `TickTimer` for network cooldowns and durations. Do not use `Time.time` as the source of truth for network gameplay.

All predicted logic must tolerate resimulation. Avoid irreversible side effects in predicted execution. Visual and audio feedback must be derived from replicated state, confirmed events, or a deliberate deduplication policy.

Do not add `[Networked]` properties for values that can be reliably derived. Avoid duplicating position, transform, visual state, configuration, or other data already synchronized by an existing component.

Only State Authority may perform authoritative `Runner.Spawn` and `Runner.Despawn` operations.

## 6. Input architecture

The intended input flow is:

```text
Unity Input System
    -> PlayerInputReader
    -> FusionInputProvider
    -> PlayerNetworkInput
    -> Fusion input buffer
    -> network gameplay controllers
```

Rules:

- Input represents intention, not a gameplay result.
- Input code must not execute attacks, interactions, damage, movement, pickups, or spawning.
- Validate received input again at the simulation boundary.
- Continuous actions and discrete button presses must preserve their intended semantics.
- Explicitly map network button values so adding enum members does not silently change existing bindings.
- Local input suppression and authoritative gameplay blocking are different concepts.
- A local menu or text field may suppress local input, but it must not grant authority over gameplay state.

The aiming direction and player facing are expected to follow the mouse cursor for the local player and be transported through the approved network input flow when required by gameplay.

## 7. Player movement architecture

The movement flow is:

```text
PlayerInputReader
    -> FusionInputProvider
    -> PlayerNetworkInput
    -> NetworkPlayerMovementController
    -> Kinematic2DMovementMotor
    -> NetworkTransform
    -> presentation
```

Responsibilities:

- `NetworkPlayerMovementController`
  - Runs in `FixedUpdateNetwork`.
  - Reads and validates movement intention.
  - Applies authoritative movement restrictions.
  - Calculates desired displacement.
  - Delegates collision resolution.
  - Exposes read-only state for presentation.

- `Kinematic2DMovementMotor`
  - Remains independent from Fusion and input.
  - Resolves 2D movement through casts, skin width, iteration limits, and sliding.
  - Returns the actual displacement applied.
  - Does not decide whether movement is allowed.

- Presentation
  - Reads simulated or rendered state.
  - Updates Animator, sprite orientation, visual feedback, and camera.
  - Never changes authoritative position or movement rules.

The current movement direction is top-down. Diagonal input must not increase movement speed.

Do not introduce dynamic Rigidbody2D force-based locomotion unless a new requirement justifies replacing the approved kinematic approach.

Avoid allocations, uncached component lookups, and `GetComponent` calls in per-tick movement paths.

## 8. Local camera

The player camera is local presentation.

Rules:

- It follows only the local player with Input Authority.
- It is not network synchronized.
- It must tolerate target replacement and object destruction.
- Camera logic must not affect player simulation.
- Camera smoothing, offset, and depth are presentation configuration.

Do not add map bounds, shake, zoom, transitions, Cinemachine, or multi-target behavior unless required by the current task.

## 9. Combat architecture

The combat pipeline must remain independent from concrete player and enemy classes.

Core responsibilities:

- A combat controller reads network input, validates control and cooldown, and executes the active attack strategy.
- An attack describes melee, ranged, or another strategy.
- Target queries detect and filter valid entities.
- Projectiles move in Fusion simulation and request damage when they impact.
- A damage resolver validates and routes damage.
- Damageable entities own health, mitigation, and death.
- Presentation consumes results or synchronized execution state.

Approved shared concepts include:

- `EntityId`
- `IEntity`
- `ICharacter`
- `IAttacker`
- `IAttack`
- `IAttackTargetQuery`
- `IProjectileSpawner`
- `IDamageResolver`
- `IDamageable`
- immutable request and result structures
- an entity registry scoped to the active `NetworkRunner`

### Melee

- State Authority performs authoritative target detection.
- Exclude the attacker.
- Ignore invalid or dead targets.
- Deduplicate targets by `EntityId`, including entities with multiple colliders.
- Respect configured range, radius, layers, and maximum targets.
- Produce at most one damage request per target for one attack execution.
- Do not modify health directly.

### Ranged

- Executing the attack and applying damage are separate moments.
- State Authority spawns the network projectile.
- The projectile stores the minimum authoritative data required for impact resolution.
- The projectile ignores its owner.
- A projectile may apply damage only once.
- It despawns on valid impact, lifetime expiration, or maximum range.
- The attack implementation does not apply immediate damage.

### Damage

All damage must pass through the common damage pipeline.

The resolver:

- Locates the target by `EntityId`.
- Rejects invalid targets and unauthorized requests.
- Rejects self-damage when the attack does not allow it.
- Delegates health, mitigation, and death to `IDamageable`.
- Returns an explicit result for gameplay and presentation.

Do not add enemy-specific logic to player attacks, projectiles, or the shared damage resolver. Fix missing enemy integration through the entity, registry, collider, layer, health, or damageable boundary.

## 10. Presentation rules

Gameplay and presentation are strictly separated.

Presentation may control:

- Animator parameters.
- Sprite orientation.
- Attack animation.
- Impact feedback.
- Audio.
- VFX.
- UI.
- Local camera.

Presentation must not:

- Apply damage.
- Decide attack validity.
- modify cooldowns.
- spawn or despawn authoritative objects.
- move authoritative transforms.
- unlock gameplay control.
- decide interaction or pickup success.

Disabling a presenter must not break gameplay.

Remote players must derive presentation from replicated state, sequence counters, or confirmed network notifications, not from local input.

## 11. Interaction and loot

Shared interaction must use contracts such as `IInteractable`, `IPickup`, `InteractionRequest`, and `InteractionResult` or the equivalent existing repository implementation.

For synchronized loot pickups:

- All clients observe the same pickup.
- State Authority validates the request.
- Validate interaction range.
- Validate that the pickup is still available.
- Two simultaneous requests must not duplicate the reward.
- Consumption occurs once.
- The pickup becomes non-interactable after consumption.
- Despawn or state change is visible to all clients.
- The pickup does not know the internal implementation of the player or inventory.
- Avoid direct prefab-to-prefab dependencies.
- Manual placement in a graybox scene is acceptable for MVP testing.
- Procedural loot generation is out of scope unless explicitly requested.

## 12. Session lifecycle

The session lifecycle must support repeated play sessions without restarting the application.

The intended coordinator is `FusionSessionLauncher` or the current equivalent.

Expected lifecycle:

```text
Idle -> Starting -> Running -> ShuttingDown -> Idle
```

Rules:

- Session start requests must be idempotent.
- The active runner and runner-scoped support components share one lifecycle.
- Network callbacks must be registered and unregistered correctly.
- Disconnecting must clean network objects, runner references, callback adapters, player mappings, registries, and local join context.
- Shutdown must return to the initial screen.
- After shutdown, the user must be able to create or join another valid session.
- Do not retain stale static references to a destroyed runner or scene object.
- A player must not be able to join a match after the host has transitioned that match from lobby/preparation into active gameplay.
- Reject or close late joins through authoritative session state, not only through UI.

When modifying shutdown or scene transitions, inspect all runner-owned objects and all `DontDestroyOnLoad` objects before changing the flow.

## 13. Repository navigation

Before implementing a task, inspect the relevant parts of the repository instead of assuming names or paths.

Common areas include:

```text
Assets/Scripts/Networking
Assets/Scripts/Player/Input
Assets/Scripts/Player/Movement
Assets/Scripts/Player/Combat
Assets/Scripts/Player/Presentation
Assets/Scripts/Combat
Assets/Scripts/Interaction
Assets/Prefabs
Assets/Input
Docs/Architecture
Packages/manifest.json
ProjectSettings/ProjectVersion.txt
Assets/Photon/Fusion/build_info.txt
```

Also inspect:

- relevant `.asmdef` files
- the network player prefab
- projectile and enemy prefabs
- input action assets
- scene build settings
- Fusion network prefab registration
- layer and collision configuration
- current git diff and uncommitted changes

Search for an existing contract or component before creating a new one.

## 14. Task workflow

For every implementation task:

1. Read this file and the relevant architecture documents.
2. Inspect the current implementation and prefab/asset dependencies.
3. Identify which parts already exist, which are incomplete, and which are only documented.
4. Preserve established contracts unless the task requires changing them.
5. Implement the smallest complete change that satisfies the task.
6. Add or update automated tests when the logic can be tested meaningfully.
7. Run all available relevant checks.
8. Review the final diff for unrelated changes.
9. Update the owning architecture document when architecture or public contracts changed.
10. Report:
    - what changed
    - files changed
    - important design decisions
    - checks actually executed
    - remaining manual validation

Do not create branches, commits, pull requests, or push changes unless the user explicitly asks.

## 15. Autonomous decisions and confirmation boundaries

Resolve implementation details autonomously when they stay within the existing architecture and task scope.

Request confirmation before:

- replacing an approved architecture
- changing a public shared contract used by multiple systems
- adding or updating a package
- introducing a new global service, singleton, event bus, or service locator
- making broad scene or prefab restructures unrelated to the task
- changing authority ownership or the host/client model
- deleting assets or migrations that may lose configured data
- choosing between materially different gameplay behaviors not defined by the task

Do not request confirmation for ordinary naming, private helper extraction, caching, validation, null handling, or small implementation details that can be resolved from the codebase.

## 16. Prohibited patterns

Do not:

- Overengineer an MVP task.
- Add abstractions only for hypothetical future use.
- Add a general Event Bus.
- Use gameplay presenters as controllers.
- Use Animator events as gameplay authority.
- Use `Time.time` for network gameplay cooldowns.
- Send RPCs every frame for movement or aiming.
- Let input code execute gameplay.
- Apply health changes directly from attacks or projectiles outside the damage pipeline.
- Add direct dependencies between concrete player and enemy implementations.
- Store runtime state in shared ScriptableObjects.
- Duplicate transform or visual data in networked state without a concrete need.
- use `FindObjectOfType`, `GameObject.Find`, or repeated component searches in simulation paths when explicit references or runner-scoped registries exist.
- Claim a scene, visual result, multiplayer behavior, or Play Mode flow was validated unless it was actually executed and observed.

## 17. Performance expectations

Optimize meaningful hot paths, especially:

- `FixedUpdateNetwork`
- target queries
- projectile simulation
- movement collision resolution
- entity lookup
- replicated state changes

Prefer:

- cached references
- reusable buffers
- non-alloc physics queries when appropriate
- stable entity lookup
- early validation
- explicit ownership of collections

Avoid premature micro-optimization in setup, editor-only, or infrequent paths.

## 18. Definition of done

A task is complete when:

- Its acceptance criteria are satisfied by the implementation.
- The solution follows the existing authority and architecture boundaries.
- The code compiles in every assembly affected by the change, when compilation tools are available.
- No new avoidable warnings or errors are introduced.
- Automated checks relevant to the changed logic pass, when available.
- The final diff contains no unrelated changes.
- Required configuration changes are implemented in serialized assets when safely editable by code.
- Any remaining Inspector, scene, network, latency, or visual validation is listed as a manual checklist.
- The final response does not claim manual Unity validation that was not performed.
- Architecture documentation is updated when the implemented architecture or shared contracts changed.

## 19. Manual validation format

When manual Unity validation remains, report only checks that require human or editor verification, for example:

- Open the specified scene.
- Confirm prefab references and serialized configuration.
- Test host and client simultaneously.
- Test under simulated latency or packet loss.
- Verify remote presentation.
- Verify disconnect, return to menu, and starting a second session.
- Confirm no duplicate damage, projectile impact, interaction, or pickup reward.
- Confirm cursor aiming and facing for local and remote players.

Do not assign manual validation that the agent could have completed through code inspection or automated commands.
