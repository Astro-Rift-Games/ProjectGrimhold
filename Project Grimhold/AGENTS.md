# AGENTS.md

## Project overview

* Unity 6 project written in C#.
* Multiplayer 2D top-down extraction game.
* Networking uses Photon Fusion 2.1 in Host/Client mode.
* Gameplay systems must support client prediction, state authority and resimulation.
* Prefer simple, modular architecture over framework-heavy solutions.
* Treat this file as repository-wide guidance.
* Task-specific decisions belong in the corresponding document under `Docs/`.

## Repository workflow

Before modifying code:

1. Inspect the relevant scripts, prefabs, package versions and existing documentation.
2. Identify the current source of truth for each affected state.
3. Determine whether the change affects local input, network simulation, presentation or persistent data.
4. For architectural or multi-file changes, produce a plan before implementation.
5. Keep changes limited to the requested outcome.

Do not make unrelated refactors while implementing a feature.

Do not introduce a new pattern, abstraction, package or framework without a concrete need in the current task.

Do not create Git worktrees unless explicitly requested.

Sub-agents may only be created when explicitly requested by the user. Do not infer permission to create them from task complexity, urgency or opportunities for parallel work.

## Architecture principles

* Prefer composition over inheritance.
* Keep responsibilities narrow and explicit.
* Depend on stable contracts only where a real variation point exists.
* Do not create interfaces with a single implementation unless they isolate infrastructure, enable deterministic testing or represent an expected variation.
* Keep Photon Fusion at the network boundary where practical.
* Keep Unity presentation concerns outside gameplay simulation.
* Keep input capture separate from input consumption.
* Keep configuration separate from runtime state.
* Avoid global mutable state.
* Avoid static service locators.
* Avoid static event buses.
* Avoid singleton managers unless the repository already defines them as an approved architectural dependency.
* Do not implement MVC as the general gameplay architecture.
* MVP or Presenter components may be used for visual presentation.
* Prefer explicit dependencies through serialized references, constructors for pure C# classes or initialization methods.

## Gameplay simulation

* Networked gameplay simulation must be tick-driven.
* Predicted gameplay logic must run from Fusion simulation callbacks such as `FixedUpdateNetwork`.
* Gameplay state must not advance through ordinary C# events.
* Events must not be the source of truth for player position, health, movement, combat or authoritative state.
* Simulation code must tolerate Fusion resimulation.
* Do not trigger irreversible side effects directly from predicted simulation.
* Audio, particles, UI and animation events must be handled by presentation components after observing simulation state.
* Avoid allocations, LINQ and unnecessary collection creation inside simulation loops.
* Avoid `GetComponent`, scene searches and string lookups every tick.
* Normalize or clamp client-provided input before applying it.
* Never trust client input as authoritative game state.

## Photon Fusion rules

* Confirm the installed Fusion version before using an API.
* Do not assume examples from Fusion 1 or older Fusion 2 versions remain valid.
* Respect Input Authority and State Authority explicitly.
* Only State Authority may perform authoritative state transitions unless the architecture documents another valid Fusion workflow.
* Use `[Networked]` properties only for state that must participate in snapshots, prediction or synchronization.
* Do not synchronize values that can be safely derived from existing networked state.
* Do not use RPCs for continuous movement or state that belongs in regular Fusion simulation.
* Do not publish ordinary gameplay events from predicted ticks without accounting for resimulation.
* Proxies must consume replicated state and must not execute local player input.

Current local-input flow:

```text
PlayerInputReader
→ FusionInputProvider
→ PlayerNetworkInput
→ NetworkBehaviour simulation
```

Preserve this separation unless an approved architecture document explicitly replaces it.

## Movement architecture

Movement must maintain separate responsibilities for:

* Local device input.
* Fusion input transport.
* Network simulation.
* Movement rules.
* Collision resolution.
* Runtime movement state.
* Configuration.
* Visual presentation.

The movement simulation must not reference:

* `Animator`
* `SpriteRenderer`
* UI
* Audio
* Particle systems
* Camera effects

The presentation layer may read movement state but must not modify authoritative simulation state.

Movement configuration should be data-driven where it provides value.

ScriptableObject assets may store shared configuration, but must not store mutable per-player runtime state.

Runtime movement state includes values such as:

* Current velocity.
* Active movement restrictions.
* Temporary modifiers.
* Current locomotion mode.
* Last valid movement direction.

Do not place these values in shared ScriptableObject assets.

Follow the approved architecture documented in:

```text
Docs/Architecture/PlayerMovementArchitecture.md
```

When that document conflicts with an older implementation, report the conflict before changing architecture.

## Event-driven rules

Event-driven communication is allowed for:

* Presentation updates.
* UI reactions.
* Audio feedback.
* Visual effects.
* Analytics.
* Decoupled notifications that do not advance predicted simulation.

Event-driven communication must not replace direct simulation flow.

Prefer typed event payloads.

Event subscriptions must have an explicit lifecycle:

* Subscribe during initialization or enable.
* Unsubscribe during disable, despawn or disposal.
* Do not leave subscriptions attached after an object is despawned.

Do not create a general-purpose event bus until at least two concrete systems require the same communication mechanism.

## Data-driven rules

Use ScriptableObject assets for stable shared configuration such as:

* Base movement values.
* Collision configuration.
* Ability definitions.
* Item definitions.
* Enemy archetypes.
* Static balance values.

Do not use ScriptableObject assets as runtime databases for mutable player state.

Do not mutate shared configuration assets during gameplay.

Separate:

```text
Static configuration
Runtime local state
Networked state
Presentation state
```

Do not synchronize the entire configuration asset. Synchronize identifiers or runtime values only when required.

## Unity conventions

* Use one primary type per C# file.
* File names must match their primary type.
* Prefer `sealed` classes when inheritance is not an intended extension point.
* Use private serialized fields instead of public mutable fields.
* Serialized private fields use the `_camelCase` naming convention.
* Public members use `PascalCase`.
* Local variables and parameters use `camelCase`.
* Use early returns to reduce nesting.
* Use `nameof` when referring to types or members in diagnostic messages.
* Use `[RequireComponent]` only for dependencies that must exist on the same GameObject.
* Use `[DisallowMultipleComponent]` for components that must have a single instance.
* Cache component references during initialization.
* Do not perform scene-wide searches during gameplay.
* Editor-only dependency lookup is allowed inside `Reset` or `OnValidate` when safe.
* Preserve the existing namespace strategy. Do not introduce a repository-wide namespace migration as part of an unrelated task.
* Avoid `async void` except Unity message entry points that cannot return `Task`.
* Coroutines and asynchronous operations must handle cancellation, object destruction and session shutdown when relevant.
* Do not suppress warnings without documenting the reason.

## Generated files

Never manually edit generated files.

This includes:

```text
PlayerInputActions.cs
```

Input actions must be modified through:

```text
PlayerInputActions.inputactions
```

After changing the Input Actions asset, allow Unity Input System to regenerate its C# wrapper.

Treat files containing headers such as `<auto-generated>` as read-only unless the task explicitly concerns the generator itself.

## Scenes and prefabs

* Do not modify scenes or prefabs unless the task explicitly requires it.
* Do not invent Inspector assignments that cannot be verified from serialized assets.
* When code requires manual Inspector configuration, document it separately.
* Do not claim that a scene, animation, visual effect or multiplayer flow was manually validated unless it was actually run and observed.
* Do not replace prefab references with runtime searches to avoid Inspector configuration.
* Preserve existing serialized field names unless a migration is included.

## Performance

For code executed every frame or simulation tick:

* Avoid managed allocations.
* Avoid LINQ.
* Avoid closures.
* Avoid repeated component lookup.
* Avoid repeated layer-name resolution.
* Avoid repeated string-based property lookup.
* Reuse buffers for physics casts and overlap queries when practical.
* Prefer non-allocating physics APIs for recurring queries.
* Do not optimize code outside a relevant hot path without evidence.

Readability has priority over speculative micro-optimization outside hot paths.

## Error handling

* Validate mandatory dependencies during initialization.
* Fail clearly when required configuration is missing.
* Include the affected object as the Unity log context when available.
* Do not silently fall back to behavior that can hide configuration errors.
* Do not use exceptions for normal gameplay control flow.
* Network startup and shutdown failures must leave the project in a valid state.
* Avoid logging every frame or every simulation tick.

## Testing and validation

For pure C# gameplay logic:

* Prefer EditMode unit tests.
* Test deterministic calculations independently from MonoBehaviours.
* Cover boundary cases and invalid input.
* Avoid tests that depend on scene timing when the logic can be extracted and tested directly.

For networked logic:

* Validate authority requirements.
* Validate missing-input behavior.
* Validate disabled-control behavior.
* Validate that predicted logic has no irreversible side effects.
* Validate host and client paths separately when automation supports it.

After changing code:

1. Review the complete diff.
2. Check for compilation errors.
3. Run relevant automated tests when available.
4. Check for generated-file modifications.
5. Check for accidental scene, prefab or asset changes.
6. Report which validations were actually executed.
7. Report validations that still require manual work.

Never claim that tests passed unless they were executed.

## Documentation

Architecture decisions that affect multiple systems must be documented under:

```text
Docs/Architecture/
```

A feature-specific architecture document should include:

* Context.
* Decision.
* Alternatives considered.
* Responsibilities.
* Sources of truth.
* Network authority.
* Data ownership.
* Event boundaries.
* Risks.
* Validation strategy.

Do not copy large task-specific specifications into `AGENTS.md`.

Reference their document path instead.

## Dependency policy

Do not add, remove or update Unity packages without explicit approval.

Before adding a dependency:

* Confirm that the existing project does not already solve the problem.
* Explain the reason for the dependency.
* Identify runtime and editor impact.
* Identify licensing or platform implications when relevant.

Do not introduce:

* Dependency-injection frameworks.
* ECS.
* Reactive frameworks.
* General-purpose event frameworks.
* Alternative networking libraries.

unless explicitly requested and justified by the task.

## Change policy

Keep commits and diffs focused.

Write commit subjects and descriptions in English.

Do not:

* Rename unrelated files.
* Reformat unrelated code.
* Move folders without necessity.
* Change public APIs without identifying consumers.
* Delete code solely because it appears unused without searching references.
* Leave placeholder implementations.
* Leave commented-out obsolete code.
* Add speculative systems for future features.
* modify `.meta` GUIDs unnecessarily.

When existing code conflicts with the requested architecture:

1. Identify the conflict.
2. Explain its impact.
3. Propose the smallest safe migration.
4. Avoid maintaining two competing sources of truth.

## Definition of done

A coding task is complete only when:

* The requested behavior is implemented.
* The implementation follows the approved architecture.
* Responsibilities remain separated.
* Network authority is explicit.
* Predicted simulation supports resimulation.
* Relevant tests were added or updated where practical.
* Existing relevant tests pass when executable.
* No generated files were manually modified.
* No unrelated files were changed.
* The diff was reviewed.
* Manual Unity or multiplayer validation steps are listed accurately.
* Remaining limitations and unverified behavior are reported.

## Code documentation

Code must be understandable by developers who did not implement the system.

Prefer clear naming and small methods over comments that restate the implementation.

### Class documentation

Add XML documentation to gameplay classes whose responsibility or interaction with the architecture is not immediately obvious.

Class documentation should explain:

* The responsibility of the class.
* Which architectural layer it belongs to.
* Its main dependencies.
* Which components consume its output.
* Whether it owns state or only adapts another system.
* Its role in the network authority model when applicable.
* Whether it participates in prediction or resimulation.

Example:

```csharp
/// <summary>
/// Adapts Fusion player input into movement commands and executes the
/// player movement simulation during network ticks.
///
/// This component is the network boundary of the movement system.
/// It does not resolve collisions or update visual presentation.
/// </summary>
public sealed class NetworkPlayerMovementController : NetworkBehaviour
{
}
```

### Method documentation

Add XML documentation to:

* Public APIs.
* Methods used by multiple systems.
* Authority-sensitive methods.
* Methods with non-obvious side effects.
* Methods whose execution timing is important.
* Methods involved in prediction, resimulation or state synchronization.
* Extension points intended for future implementations.

Documentation should explain contracts, requirements and effects rather than repeat the method name.

Example:

```csharp
/// <summary>
/// Adds a movement restriction owned by State Authority.
///
/// Multiple restrictions may be active simultaneously. Removing one
/// restriction does not enable movement while other restrictions remain.
/// </summary>
/// <param name="reason">
/// Reason that prevents the player from controlling movement.
/// </param>
/// <returns>
/// <see langword="true"/> when the restriction was applied;
/// otherwise, <see langword="false"/> when this peer lacks authority.
/// </returns>
public bool TryAddMovementBlock(MovementBlockReason reason)
{
}
```

Private methods do not require XML documentation when their name, inputs and implementation make their purpose clear.

Document private methods when they contain:

* Non-obvious algorithms.
* Important ordering requirements.
* Physics assumptions.
* Network authority constraints.
* Prediction or resimulation restrictions.
* Performance-sensitive behavior.
* Workarounds for Unity, Fusion or platform limitations.

### Inline comments

Use inline comments to explain why a decision exists.

Good comments include:

* Why an operation must happen in `FixedUpdateNetwork`.
* Why an event cannot be emitted during predicted simulation.
* Why a value is derived instead of synchronized.
* Why a physics query uses a reusable buffer.
* Why a particular authority is required.
* Why an apparently simpler implementation is unsafe.

Example:

```csharp
// Clamp again inside the simulation boundary because network input is
// client-provided and must not be trusted as an already valid direction.
Vector2 direction = Vector2.ClampMagnitude(input.MoveDirection, 1f);
```

Do not add comments that only translate code into natural language.

Avoid:

```csharp
// Set the velocity to zero.
_velocity = Vector2.zero;

// Check if movement is blocked.
if (_movementBlockMask != 0)
{
}
```

### Architecture interaction

When several components form a workflow, document the complete interaction in the corresponding architecture document rather than duplicating the same explanation across every class.

Code documentation should reference the relevant document when additional context is necessary.

Example:

```csharp
/// <remarks>
/// See `Docs/Architecture/PlayerMovementArchitecture.md` for the complete
/// input, simulation, collision and presentation flow.
/// </remarks>
```

Architectural documents must describe:

* The complete component flow.
* Sources of truth.
* Ownership of state.
* Dependency direction.
* Network authorities.
* Event producers and consumers.
* Data flow between simulation and presentation.

### Comment maintenance

Comments are part of the implementation and must be updated when behavior changes.

Do not leave:

* Outdated comments.
* Commented-out code.
* TODO comments without actionable context.
* Comments that describe behavior no longer present.
* Documentation that contradicts the implementation.

A task is not complete when its code changes invalidate existing documentation.
