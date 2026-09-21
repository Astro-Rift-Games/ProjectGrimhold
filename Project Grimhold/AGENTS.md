# AGENTS.md — Unity project

## Scope

Applies to `Project Grimhold/`, the Unity project. Read repository-root `../AGENTS.md` first.

This file owns Unity, Photon Fusion and gameplay implementation rules. Repository-wide task workflow, HacknPlan scope, source precedence, tool routing and delegation live in the root file.

Approved technical architecture lives under:

```text
Docs/Architecture/
```

Read the relevant Architecture before changing a covered system.

---

## Architecture principles

Prefer composition, narrow responsibilities, explicit ownership/dependencies, stable contracts and simple data flow.

Avoid global mutable state, static service locators, static gameplay event buses, unnecessary singleton managers, default-MVC gameplay architecture and generic frameworks without a current need.

Use interfaces only for real infrastructure boundaries, deterministic testing seams or actual variation points.

Use serialized references for Unity dependencies, constructors for pure C# dependencies and explicit initialization where appropriate.

Keep distinct:

```text
Static configuration
Persistent state
Runtime local state
Networked state
Presentation state
```

Keep presentation separate from authoritative simulation and keep Fusion near network boundaries.

---

## Unity MCP

Use Unity MCP when correctness depends on Editor state that source alone cannot reliably establish:

- prefab composition;
- scene hierarchy;
- serialized references / Inspector configuration;
- Input Action assets;
- ScriptableObject configuration;
- `NetworkObject` / `NetworkBehaviour` composition;
- Unity compilation / Console;
- Unity Test Runner;
- Play Mode/runtime behavior;
- visual/animation checks required by the task.

Do not open or mutate Unity merely because it is available. Documentation-only and pure deterministic C# work may not need it.

Do not use Unity MCP instead of CodeGraph/source inspection.
Do not modify scenes/prefabs/assets unless the task requires it.

---

## Photon Fusion and simulation

Verify the installed Fusion version before using version-sensitive APIs.

Network gameplay simulation must be tick-driven. Predicted logic belongs in Fusion simulation callbacks such as `FixedUpdateNetwork()` and must tolerate resimulation.

Respect State Authority, Input Authority, proxies, prediction and resimulation.

Only State Authority performs authoritative state transitions unless approved Architecture defines otherwise.

Use `[Networked]` only for state that must replicate, snapshot or predict. Do not synchronize safely derivable state.

Do not use RPCs for continuous movement/state that belongs in simulation.
Never trust client input as authoritative; normalize/clamp it at the simulation boundary.

Ordinary C# events must not advance authoritative gameplay state. Do not perform irreversible side effects directly from predicted simulation unless resimulation safety is explicitly handled.

Presentation may observe confirmed/networked state for animation, UI, audio, VFX and camera.

---

## Host Migration

Before changing migration eligibility, snapshot restoration, runtime rebind, deadlines or recovery closure, read:

```text
Docs/Architecture/HostMigrationRecoveryArchitecture.md
```

Host Migration is for abrupt Host loss during an active Raid, not normal Return, Abandon, Extraction, cancellation or shutdown.

Do not let Fresh Spawn initialization overwrite restored network state. For affected `NetworkBehaviour`s, check `Spawned()` and initialization paths for restore-safety.

---

## Input

Current flow:

```text
PlayerInputReader
→ FusionInputProvider
→ PlayerNetworkInput
→ NetworkBehaviour simulation
```

Preserve it unless approved Architecture changes it.

Never manually edit generated `PlayerInputActions.cs`.

Change input definitions through:

```text
Assets/Input/PlayerInputActions.inputactions
```

When input changes are in scope, inspect the real Input Actions asset and serialized bindings.

---

## Movement and presentation

Gameplay movement remains continuous top-down movement.

Visual animation uses six facing buckets:

```text
N
NE
NW
S
SE
SW
```

These are presentation only; never constrain movement to six directions.

Keep local input, Fusion transport, simulation, movement rules, collision, runtime state, configuration and presentation as separate responsibilities.

Movement simulation must not depend on Animator, SpriteRenderer, UI, audio, particles or camera effects.

Read `Docs/Architecture/PlayerMovementArchitecture.md` for movement changes.

---

## Events and configuration

Events are appropriate for presentation, UI, audio, VFX, analytics and non-authoritative notifications. They are not sources of truth for movement, Health, Combat or authoritative state.

Prefer typed payloads and explicit subscription lifecycles. Do not create a general event bus without an approved concrete need.

Use ScriptableObjects for stable shared configuration such as movement, items, weapons, abilities, enemies and balance values.

Never use ScriptableObjects as mutable per-player runtime databases or mutate shared configuration during gameplay.

Synchronize identifiers/required runtime values rather than whole configuration assets.

---

## Scenes, prefabs and serialized assets

When correctness depends on serialized state, inspect the actual prefab/scene/asset.

Do not invent Inspector assignments or replace serialized dependencies with runtime searches merely to avoid configuration.

Preserve serialized field names unless migration is in scope. Do not change `.meta` GUIDs unnecessarily.

Before serialized changes: inspect current consumers and references, make the smallest required change, then verify no unrelated serialized changes occurred.

Never claim scene/prefab/visual/runtime validation unless actually observed.

---

## C# conventions

- One primary type per file; filename matches it.
- Prefer `sealed` when inheritance is not intended.
- Prefer private serialized fields over public mutable fields.
- Serialized private fields: `_camelCase`; public members: `PascalCase`; locals/parameters: `camelCase`.
- Prefer early returns and `nameof`.
- Cache recurring component references; avoid gameplay-time scene-wide searches.
- Preserve current namespace strategy.
- Avoid `async void` except required Unity entry points.
- Async/coroutines must consider cancellation, destruction and session shutdown.
- Do not suppress warnings without documenting why.
- Use `[RequireComponent]` only for same-GameObject mandatory dependencies.
- Use `[DisallowMultipleComponent]` when duplicates are invalid.

---

## Performance and errors

In recurring frame/tick paths avoid LINQ, managed allocations, closures, repeated component/scene searches and repeated string/layer resolution. Prefer reusable buffers/non-alloc physics where practical.

Outside hot paths, readability beats speculative micro-optimization.

Validate mandatory dependencies during initialization and fail clearly on missing configuration. Use Unity object log context when available.

Do not hide configuration errors with silent fallback behavior. Do not use exceptions for normal gameplay flow. Avoid per-frame/tick logs.

Network startup/shutdown/transition failures must leave a valid application state.

---

## Testing and validation

Prefer EditMode tests for deterministic pure C# logic. Separate deterministic logic from MonoBehaviours when it materially improves testability.

For networked changes validate when applicable:

- authority;
- Host and Client paths;
- missing/disabled input;
- prediction/resimulation;
- irreversible side effects;
- Host Migration restore behavior.

After Unity changes:

1. review full diff;
2. check Unity compilation;
3. run relevant automated tests;
4. inspect generated-file changes;
5. inspect accidental prefab/scene/asset changes;
6. use Unity MCP for required Editor-side validation;
7. list manual multiplayer/visual validation still needed.

Never claim a test passed unless it ran successfully.

---

## Documentation and dependencies

Architecture decisions crossing systems belong in `Docs/Architecture/`. Architecture owns technical contracts; Game Design owns intended behavior.

Update Architecture when implementation intentionally changes an approved contract. Do not edit Game Design merely to match implementation unless the task changes design.

Do not add/remove/update Unity packages without explicit approval. Before a new dependency, prove the project does not already solve the need and identify runtime/editor, licensing and platform impact.

Do not introduce DI frameworks, ECS, reactive frameworks, general-purpose event frameworks or alternative networking libraries without explicit approval and concrete justification.

---

## Unity definition of done

A Unity coding task is complete only when requested behavior is implemented, current Game Design and approved Architecture are respected, authority/resimulation requirements are explicit, relevant validation actually ran, the complete diff was reviewed, no unrelated/generated changes were introduced, and remaining manual Unity/multiplayer checks or known limitations are reported.
