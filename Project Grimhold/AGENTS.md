# Project Grimhold — Unity instructions

## Scope and technical configuration

Applies to `Project Grimhold/`. Read [repository instructions](../AGENTS.md) first; they own work-item scope, source authority, cross-system contracts and repository workflow. This file owns Unity implementation and validation.

Paths below are relative to this Unity project. Recheck these files on the target branch before using version-sensitive APIs; the values are a verified baseline, not upgrade requirements.

| Technology | Verified version | Source |
| --- | --- | --- |
| Unity | `6000.5.1f1` | `ProjectSettings/ProjectVersion.txt` |
| Photon Fusion | `2.1.1 Stable 2177` | `Assets/Photon/Fusion/build_info.txt` (vendored under `Assets/Photon/`, not a UPM dependency) |
| Unity Input System | `1.19.0` | `Packages/manifest.json` |
| Universal Render Pipeline | `17.5.0` | `Packages/manifest.json` |
| Unity Test Framework | `1.7.0` | `Packages/manifest.json` |

Use `Packages/packages-lock.json` for resolved UPM dependencies and `Assets/Photon/Fusion/Resources/NetworkProjectConfig.fusion` for Fusion project settings.

## Architecture and state ownership

Read the relevant current sections of `Docs/Architecture/` before changing a covered system. The root file maps shared/cross-system contracts; Unity-specific entry points include:

| Change | Architecture document under `Docs/Architecture/` |
| --- | --- |
| Movement, prediction and collision | `PlayerMovementArchitecture.md` |
| Combat and presentation integration | `PlayerCombatArchitecture.md` |
| Interaction and loot | `PlayerInteractionArchitecture.md`, `LootInteractionArchitecture.md` |
| Host loss, restoration and recovery closure | `HostMigrationRecoveryArchitecture.md` |
| Visibility and fog | `VisibilityArchitecture.md` |

- Prefer composition, narrow responsibilities and explicit ownership/dependencies. Use serialized references for Unity dependencies, constructors for pure C# dependencies and explicit initialization where appropriate.
- Keep static configuration, persistent state, local runtime state, networked state and presentation state distinct. Keep Fusion at network integration boundaries and presentation separate from authoritative simulation.
- Avoid global mutable state, static service locators/gameplay event buses, unnecessary singleton managers and default-MVC gameplay architecture. Do not replace documented application- or runner-scoped composition with new global infrastructure.
- Introduce interfaces for real infrastructure boundaries, deterministic testing seams or actual variation points, not speculative extensibility.
- ScriptableObjects hold stable shared configuration, not mutable per-player runtime databases. Do not mutate shared configuration during gameplay; replicate identifiers/required runtime values rather than whole assets.
- C# events may notify presentation, UI, audio, VFX or analytics; they must not advance authoritative gameplay state. Use typed payloads and explicit subscription lifecycles. Do not add a general event bus without an approved concrete need.
- Do not introduce DI, ECS, reactive/event frameworks or alternative networking libraries without explicit approval and concrete justification. For dependencies, identify runtime/editor, licensing and platform impact.

## Photon Fusion and simulation

- Run network gameplay simulation on Fusion ticks (`FixedUpdateNetwork()`), not frame-driven presentation callbacks. Predicted logic must tolerate resimulation.
- Input Authority expresses intent and may predict the documented simulation; State Authority validates and commits authoritative outcomes. Preserve proxy behavior rather than treating every peer as a writer. A State Authority guard must not accidentally disable required local prediction.
- Use `[Networked]` for state that must replicate, snapshot or predict; do not replicate safely derivable state.
- Validate client input at the simulation boundary, including normalization/clamping where applicable. Do not use RPCs for continuous movement/state that belongs in simulation.
- Do not execute irreversible side effects from predicted simulation without explicit resimulation safety. Presentation observes confirmed results or replicated state for animation, UI, audio, VFX and camera.
- Network startup, shutdown and transition failures must leave a valid application state. Async operations/coroutines must account for cancellation, object destruction and session shutdown.

### Host Migration

Read `Docs/Architecture/HostMigrationRecoveryArchitecture.md` before changing eligibility, snapshots, rebind, deadlines or recovery closure.

Host Migration recovers abrupt Host loss during an active Raid, not normal Return, Abandon, Extraction, cancellation or shutdown. Preserve the fresh-spawn versus restore boundary: `CopyStateFrom` restores network state; `Spawned()` and other initialization paths must not overwrite it. Restore guards alone are not restoration. Follow the document for role-specific recovery and runner-scoped ownership rather than creating a fresh-spawn fallback.

## Input, movement and presentation

Preserve the existing input pipeline:

```text
PlayerInputReader → FusionInputProvider → PlayerNetworkInput → NetworkBehaviour simulation
```

Input definitions live in `Assets/Input/PlayerInputActions.inputactions`; never manually edit generated `Assets/Input/PlayerInputActions.cs`. Inspect the actual asset and serialized bindings when input changes are in scope.

Movement is continuous top-down movement. The six visual facing buckets (`N`, `NE`, `NW`, `S`, `SE`, `SW`) are presentation only, not movement restrictions. Keep input capture, transport, movement rules, collision, runtime state, configuration and presentation separate. Movement simulation must not depend on Animator, SpriteRenderer, UI, audio, particles or camera effects.

## Serialized assets and Editor access

Use Unity MCP when correctness depends on Editor or runtime state: prefab/scene composition, Inspector references, Input Actions, ScriptableObjects, NetworkObject/NetworkBehaviour composition, compilation/Console, Test Runner, Play Mode or required visual checks. Source-only documentation work and deterministic pure C# inspection do not require opening or mutating Unity.

- Inspect the actual scene/prefab/asset when serialized state matters. Do not invent Inspector assignments or substitute runtime searches to avoid configuring dependencies.
- Change scenes/prefabs/assets only when required by the task. Preserve serialized field names unless migration is in scope and preserve `.meta` GUIDs.
- Check affected references before serialized edits and inspect the diff afterward for unrelated asset or generated-file changes.

## C# conventions and performance

- One primary type per file with a matching filename; preserve the current namespace strategy.
- Prefer `sealed` when inheritance is not intended and private serialized fields over public mutable fields.
- Private serialized fields use `_camelCase`, public members `PascalCase`, locals/parameters `camelCase`.
- Prefer early returns and `nameof`. Use `[RequireComponent]` only for mandatory same-GameObject dependencies and `[DisallowMultipleComponent]` when duplicates are invalid.
- Avoid `async void` except required Unity entry points. Do not suppress warnings without documenting why.
- Cache recurring component references; avoid gameplay-time scene-wide searches.
- In frame/tick hot paths, avoid LINQ, managed allocations, closures, repeated component/scene searches and repeated string/layer resolution. Prefer reusable buffers and non-alloc physics where practical; outside hot paths, favor readability over speculative micro-optimization.
- Validate required dependencies during initialization and fail clearly, using Unity object log context where available. Do not hide configuration errors behind silent fallbacks, use exceptions for normal gameplay rejection or emit per-frame/tick logs.

## Testing and validation

Tests live under `Assets/Tests/EditMode/` and `Assets/Tests/PlayMode/`. Prefer EditMode tests for deterministic pure C# logic; separate it from MonoBehaviours when that materially improves testability. Use PlayMode for component, scene and runtime integration where applicable.

For executable Unity changes:

1. Check Unity compilation and relevant automated tests through Unity MCP when available; report unavailable checks rather than substituting source inspection for Editor execution.
2. Inspect generated-file and scene/prefab/asset diffs for unintended changes.
3. For networked behavior, validate applicable State/Input Authority and proxy paths, Host/Client flows, missing/disabled input, prediction/resimulation and irreversible side effects.
4. When restoration is affected, check Host Migration restore behavior. Real migration requires a multi-process Host plus at least two Clients; single-runner or state-copy tests do not prove that flow.
5. List remaining manual multiplayer, visual or Editor checks and known limitations. Documentation-only changes need Markdown/reference validation, not a Unity runtime run.

Keep acceptance checks specific to the requested behavior and the owning architecture; do not treat an unexecuted multiplayer or visual check as completed validation.
