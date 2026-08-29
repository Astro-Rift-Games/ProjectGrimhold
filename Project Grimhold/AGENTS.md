# AGENTS.md

## Project overview

Project Grimhold is a Unity 6 C# multiplayer 2D top-down extraction RPG.

The game uses Photon Fusion for networking. Raid gameplay uses Host/Client mode. The Town uses Shared Mode according to `Docs/Architecture/LobbyAndSessionTransitionArchitecture.md`.

The project favors simple, modular and explicit architecture suitable for a small team. Avoid speculative systems, unnecessary abstractions and framework-heavy solutions.

This file defines repository-wide development rules. System-specific technical decisions belong in `Docs/Architecture/`. Game Design decisions live in the connected Google Drive.

---

## Sources of truth

Before planning, implementing, refactoring or reviewing a change, determine which sources are relevant and inspect them.

Use this priority order:

1. Current code, prefabs, scenes, assets, packages and configuration in the repository.
2. Current relevant documents under `Docs/Architecture/`.
3. This `AGENTS.md`.
4. Current Game Design documents from the connected Google Drive.
5. Manually uploaded copies, implementation plans, walkthroughs and previous conversations.

Do not assume that documentation describes the current implementation.

Do not assume that the current implementation represents the intended Game Design.

When two sources conflict:

1. Identify the conflict.
2. Determine which responsibility each source owns.
3. Report the conflict when it affects the requested change.
4. Apply the smallest change that preserves a single source of truth.

Do not silently maintain two competing implementations or contracts.

---

## Google Drive Game Design documentation

The authoritative Game Design documentation is stored in the connected Google Drive.

When a task depends on Game Design behavior, read the relevant current Google Drive document before making implementation decisions.

Always consult the live Drive document. Do not rely on remembered, summarized, cached, exported or previously uploaded copies when the Drive version is available.

The catalog below is a routing aid, not an exhaustive whitelist. Game Design documentation evolves independently from this file. Before concluding that no Game Design document owns a behavior, search the connected Drive for newer or additional relevant documents. If a current authoritative document exists but is not yet listed here, consult it and resolve ownership by responsibility rather than falling back to an older listed document merely because this catalog is stale.

Document numbering does not define source priority between Game Design documents. When several documents are relevant, use the document that explicitly owns each behavior and read all documents required by the cross-system change.

### Primary documents

#### 00 - Desarrollo Conceptual

https://docs.google.com/document/d/1yFJvOddVa9ZuetCEgIxv-7tNW8iEp7pMmGHkrlZe2mE

Defines the high-level game concept, fantasy, pillars, differentiators and MVP scope.

Read when the task affects:

* Overall product direction.
* MVP scope.
* Core extraction RPG identity.
* High-level gameplay pillars.

#### 01 - Game Flow Principal

https://docs.google.com/document/d/1Ne9bqHwtzn5_rpFEfxxNBrII75TkYyszyqSFXyKS108

Defines the high-level player flow and transitions between major game states.

Read when the task affects:

* Application flow.
* Town → Raid transitions.
* Session lifecycle.
* Results flow.
* Shared session flow.

#### 02 - Estados del Juego

https://docs.google.com/document/d/1j-1yZ6eiJacTdVl4EOya570zt1pvVNYtWvXHDo2orDk

Defines responsibilities, allowed actions and transitions for each game state.

Read when the task affects:

* Town.
* Party management.
* Expedition preparation.
* Connecting.
* Dungeon.
* Results.
* State transitions.
* Disconnect or abandonment behavior.

#### 03 - Player Design

https://docs.google.com/document/d/1ljByoFJq8stTH4Pifr1xZprH73Slwt840O4U1uD1zok

Defines the common gameplay behavior of the player independently from character build.

Read when the task affects:

* Movement.
* Sprint.
* Orientation.
* Camera.
* Base combat behavior.
* Player controller responsibilities.

#### 04 - Character Build Design

https://docs.google.com/document/d/1crJZ2OSi9erUwfTM_Qgk4tmembY6yAAPk5mj4kc8V-0

Defines persistent character identity and the classless build model.

Read when the task affects:

* Character identity.
* Attribute set and conceptual responsibilities.
* Attribute allocation and redistribution as build decisions.
* Build specialization and hybridization.
* Melee or Ranged identity.
* Skills.
* Build-side consequences of character progression.
* Build persistence.
* Preparation before a Raid.

Do not interpret Melee or Ranged as permanent player classes unless a newer authoritative design document explicitly changes this decision.

For XP generation, XP consolidation, level requirements, level-up processing or the progression curve, read `05 - Progresión Persistente, Experiencia y Niveles` rather than treating Character Build Design as the owner of those rules.

For concrete attribute formulas, limits and derived statistics, read `08 - Estadísticas Derivadas y Fórmulas de Atributos`.

For visual character identity, modularity and the composition of body/hands/equipment, read `GD-11 — Dirección Visual Modular del Personaje sin Clases`.

For specific equipment slots, main hand/off hand structure, weapon sets or equipment structural compatibilities, read `GD-12 — Estructura de Equipamiento del Personaje`.

For concrete equipment catalog, attribute requirements, weapon scaling, equipment statistics and training weapons, read `09 - Diseño de Equipamiento`.

#### 05 - Progresión Persistente, Experiencia y Niveles

https://docs.google.com/document/d/1_3QJsqyCRtFLGMwinsht3Y7sxE-qfGSeJ9J39xaSbb8

Defines the Game Design rules for Experience and Levels, including the distinction between provisional Expedition XP and consolidated persistent XP.

Read when the task affects:

* Character Level or Experience.
* Initial or maximum level.
* Expedition XP generation and individual reward ownership.
* Kill, Assist, exploration, extracted Loot or Mission XP.
* XP consolidation after extraction, defeat, abandonment or disconnect.
* Progression shown in Results.
* Level requirements and the progression curve.
* Multiple level-ups from one reward.
* Experience overflow between levels and behavior at maximum level.
* Integrity rules that prevent duplicate XP rewards or duplicate consolidation.

The numerical curve and reward values are balance configuration unless the document explicitly defines a structural rule. Do not turn a provisional balance value into a hard-coded domain invariant without a separate technical reason.

#### 06 - Sistema de Extracción

https://docs.google.com/document/d/1C8DBIByc1HbzUdABqkXR2syLOKYaBbankMMHdaH4jD4

Defines the Game Design rules of Raid extraction.

Read when the task affects:

* Extraction progression.
* Extraction assignment.
* Rituals.
* Extraction zones.
* Successful extraction.
* Individual extraction behavior.

#### 07 - Sistema de Loot

https://docs.google.com/document/d/1YUjTsTW_4_hdDUmxquHnOyWMy7zmQpp29N9ZeRpomF4

Defines the Game Design lifecycle of Loot during a Raid.

Read when the task affects:

* Loot sources.
* Pickups.
* Containers.
* Loot transfers.
* Loot persistence.
* Player corpses.
* Extracted items.
* Item loss after defeat or abandonment.

#### 08 - Estadísticas Derivadas y Fórmulas de Atributos

https://docs.google.com/document/d/1xCm3FrqVYX8NvZr3uFTJm_ELA0GB3JH-O2OLlcN5AXk

Defines how the attributes established by Character Build Design become concrete gameplay statistics and establishes the initial formulas, limits and numerical rules used for implementation and playtesting.

Read when the task affects:

* Derived statistics from character attributes.
* Maximum Health from Vitality.
* Maximum Stamina from Resistance.
* Luck and additional-loot probability.
* Strength, Dexterity or Intelligence values exposed to consuming systems.
* Initial, minimum or maximum attribute values.
* Total attribute-point pools and points gained per level.
* Attribute redistribution limits.
* Numerical rounding rules for character statistics.
* Current Health or Stamina behavior when their maximum changes.
* Separation between character statistics and properties owned by equipment.

This document does not own XP generation or the level progression curve. It also does not define concrete equipment requirements, weapon scaling grades or equipment scaling formulas.

For concrete equipment requirements, weapon scaling grades, equipment values and equipment scaling configuration, read `09 - Diseño de Equipamiento`.

#### 09 - Diseño de Equipamiento

https://docs.google.com/document/d/1otmwGRyMe4ZWQOb-fEY1cCGzrb7zIq995qvv__xkx0Y

The document is named `09 - Diseño de Equipamiento` in Drive and may refer to itself
internally as `GD-15`.

Defines the concrete Equipment Design and initial MVP equipment catalog.

Read when the task affects:

* Concrete weapon definitions.
* Concrete armor definitions and sets.
* Weapon attribute requirements.
* Weapon attribute scaling and scaling grades.
* Base weapon damage, attack interval, range and Stamina cost.
* Weapon-specific baseline behaviors.
* Training weapons.
* Concrete armor Physical Defense and Magical Defense values.
* Armor resource bonuses.
* Equipment evolution data structure.
* Item Value.
* Equipment definition and instance data fields.

`GD-12 — Estructura de Equipamiento del Personaje` remains the source of truth for
structural equipment rules such as slots, Weapon Sets, Main Hand / Off Hand,
one-handed/two-handed compatibility, Dual Wield, accessories and Quick Slots.

`08 - Estadísticas Derivadas y Fórmulas de Atributos` remains the source of truth for
character attribute formulas and limits. This document consumes those rules when defining
equipment requirements and scaling; it does not redefine them.

#### GD-11 — Dirección Visual Modular del Personaje sin Clases

https://docs.google.com/document/d/166_nPvqQnEg3qYTBMSa83vY7u5TRgZbXm5SqMynJim0

Defines the visual representation and modularity of characters.

Read when the task affects:

* Visual character identity.
* Visual modularity.
* Visible equipment.
* Composition of body, hands and equipment.
* Visual families.
* Visual reading of a build.
* General visual direction of the character.

#### GD-12 — Estructura de Equipamiento del Personaje

https://docs.google.com/document/d/1JzL5635VDiFJYzSiWpgzIcMQ4pYfvSPflELYf_iEEQs

Defines the structural equipment model of the character.

Read when the task affects:

* Equipment slots.
* Weapon Sets.
* Main Hand / Off Hand mechanics.
* One-handed and two-handed weapons.
* Dual Wield.
* Shields.
* Armor categories.
* Accessories.
* Quick Slots.
* Structural compatibilities and restrictions.
* Equipping, unequipping and displacement of already-equipped items when an equip operation requires it.

For the concrete MVP equipment catalog, numerical weapon and armor properties, requirements, scaling and Item Value, read `09 - Diseño de Equipamiento`.

### Cross-system changes

Read every relevant document when a change crosses responsibilities.

Examples:

* Extracted inventory behavior:
  `06 - Sistema de Extracción` + `07 - Sistema de Loot` + relevant Inventory architecture.

* Character combat identity:
  `03 - Player Design` + `04 - Character Build Design` + relevant combat architecture.

* Town preparation and Raid entry:
  `01 - Game Flow Principal` + `02 - Estados del Juego` + `04 - Character Build Design` + relevant session architecture.

* Persistent Level and XP behavior:
  `04 - Character Build Design` for build consequences + `05 - Progresión Persistente, Experiencia y Niveles` for XP and Level rules + relevant persistence architecture when storage is involved.

* Attribute and derived-stat implementation:
  `04 - Character Build Design` for attribute responsibilities + `08 - Estadísticas Derivadas y Fórmulas de Atributos` for formulas and numerical rules + the architecture of the consuming gameplay system.

* Visual equipment representation:
  `GD-12 — Estructura de Equipamiento del Personaje` for the slots and rules + `GD-11 — Dirección Visual Modular del Personaje sin Clases` for the visual composition + relevant presentation architecture.

* XP produced by Raid outcomes or extracted Loot:
  `05 - Progresión Persistente, Experiencia y Niveles` + `06 - Sistema de Extracción` + `07 - Sistema de Loot` + relevant persistence or Results architecture when applicable.

* Equipment implementation:
`GD-12 — Estructura de Equipamiento del Personaje` for structural slots and compatibility
  + `09 - Diseño de Equipamiento` for concrete equipment definitions, requirements,
scaling and values
  + `08 - Estadísticas Derivadas y Fórmulas de Atributos` when character attributes
participate in requirements or scaling
  + relevant Inventory, Combat or presentation architecture depending on the change.

Do not read unrelated design documents by default.

### Drive access failure

If a relevant Google Drive document cannot be accessed, state that before making a definitive Game Design-dependent implementation decision.

Do not silently replace the unavailable live document with an older uploaded copy.

---

## Architecture documentation

Approved technical architecture lives under:

```text
Docs/Architecture/
```

Before modifying a system covered by an architecture document, read that document.

Architecture documents define technical responsibilities such as:

* Sources of truth.
* Component boundaries.
* Data ownership.
* Network authority.
* Persistence.
* Dependency direction.
* Simulation flow.
* Presentation boundaries.
* Runtime lifecycle.

Game Design documents define intended behavior.

Architecture documents define how that behavior is represented technically.

Do not make Game Design documents responsible for technical decisions they explicitly leave outside their scope.

When architecture documentation conflicts with current implementation, report the conflict before introducing another architecture.

---

## Repository workflow

Before modifying code:

1. Inspect the relevant current implementation.
2. Inspect affected prefabs, scenes, assets and configuration when the task depends on them.
3. Read relevant `Docs/Architecture/` documents.
4. Read relevant current Google Drive Game Design documents when behavior depends on Game Design.
5. Identify the current source of truth for each affected state.
6. Determine whether the change affects local input, network simulation, presentation, persistence or configuration.
7. For architectural or multi-file changes, produce a focused plan before implementation.
8. Keep the change limited to the requested outcome.

Do not make unrelated refactors.

Do not introduce patterns, abstractions, packages or frameworks without a concrete need in the current task.

Do not create Git worktrees unless explicitly requested.

Sub-agents may only be created when explicitly requested.

When the user specifies a branch, inspect that branch.

Do not assume another branch represents the implementation being modified.

Exact Unity, Photon Fusion and package versions must be verified from the repository rather than inferred from documentation or previous conversations. When project-wide dependency versions are needed, verify them against `New-Testing`.

---

## Architecture principles

Prefer:

* Composition over inheritance.
* Narrow responsibilities.
* Explicit ownership.
* Explicit dependencies.
* Stable contracts.
* Simple data flow.
* Small focused components.

Avoid:

* Global mutable state.
* Static service locators.
* Static event buses.
* Unnecessary singleton managers.
* General-purpose framework abstractions.
* MVC as the default gameplay architecture.
* Interfaces that exist only to wrap a single implementation without a real boundary or variation point.

Interfaces are justified when they:

* Isolate infrastructure.
* Enable deterministic testing.
* Represent an actual or expected variation point.

Use explicit dependencies through:

* Serialized references for Unity components.
* Constructors for pure C# objects.
* Explicit initialization methods where appropriate.

Keep configuration separate from runtime state.

Keep Unity presentation separate from gameplay simulation.

Keep Photon Fusion at the network boundary where practical.

---

## Gameplay simulation

Networked gameplay simulation must be tick-driven.

Predicted gameplay logic belongs in Fusion simulation callbacks such as:

```csharp
FixedUpdateNetwork()
```

Simulation code must tolerate resimulation.

Gameplay state must not advance through ordinary C# events.

Events must not be the source of truth for:

* Position.
* Movement.
* Health.
* Combat.
* Authoritative gameplay state.

Do not execute irreversible side effects directly from predicted simulation.

Presentation systems may observe simulation state and produce:

* Animation.
* UI.
* Audio.
* VFX.
* Camera feedback.

Avoid inside recurring simulation:

* LINQ.
* Managed allocations.
* Closures.
* Scene searches.
* Repeated `GetComponent`.
* Repeated string lookups.
* Repeated layer-name resolution.

Normalize or clamp client-provided input at the simulation boundary.

Never trust client input as authoritative game state.

---

## Photon Fusion

Verify the installed Fusion version before using an API.

Do not assume examples from Fusion 1 or another Fusion 2 version remain valid.

Respect explicitly:

* State Authority.
* Input Authority.
* Proxy behavior.
* Prediction.
* Resimulation.

Only State Authority may perform authoritative state transitions unless an approved architecture document defines another valid workflow.

Use `[Networked]` properties only for state that must participate in replication, snapshots or prediction.

Do not synchronize data that can safely be derived from existing synchronized state.

Do not use RPCs for continuous movement or state that belongs in regular simulation.

Do not emit ordinary gameplay events from predicted ticks unless resimulation has been accounted for.

Proxies consume replicated state and must not execute local player input.

### Local input flow

Current input flow:

```text
PlayerInputReader
→ FusionInputProvider
→ PlayerNetworkInput
→ NetworkBehaviour simulation
```

Preserve this separation unless an approved architecture document replaces it.

---

## Movement

Movement responsibilities must remain separated between:

* Local device input.
* Fusion input transport.
* Network simulation.
* Movement rules.
* Collision resolution.
* Runtime movement state.
* Configuration.
* Presentation.

Movement simulation must not depend on:

* `Animator`.
* `SpriteRenderer`.
* UI.
* Audio.
* Particle systems.
* Camera effects.

Presentation may read movement state but must not modify authoritative simulation.

Shared ScriptableObject assets may contain stable configuration but must not contain mutable per-player runtime state.

Runtime state includes values such as:

* Current velocity.
* Movement restrictions.
* Temporary modifiers.
* Locomotion mode.
* Last valid movement direction.

Follow:

```text
Docs/Architecture/PlayerMovementArchitecture.md
```

when modifying player movement.

---

## Character visual directions

Character gameplay movement remains continuous top-down movement.

Visual character animation uses six facing buckets:

```text
N
NE
NW
S
SE
SW
```

These directions describe presentation only.

Do not restrict gameplay movement to six directions.

---

## Event-driven communication

Events are appropriate for:

* Presentation updates.
* UI reactions.
* Audio.
* VFX.
* Analytics.
* Notifications that do not advance predicted simulation.

Events must not replace direct simulation flow.

Prefer typed payloads.

Every subscription must have an explicit lifecycle.

Subscribe during initialization or enable.

Unsubscribe during disable, despawn or disposal.

Do not create a general-purpose event bus unless multiple concrete systems require the same mechanism and the architecture justifies it.

---

## Data-driven configuration

Use ScriptableObjects for stable shared configuration when appropriate, such as:

* Movement configuration.
* Collision configuration.
* Item definitions.
* Weapon definitions.
* Ability definitions.
* Enemy archetypes.
* Static balance values.

Do not use ScriptableObjects as mutable runtime player databases.

Do not mutate shared configuration assets during gameplay.

Keep these categories separate:

```text
Static configuration
Runtime local state
Networked state
Presentation state
Persistent state
```

Synchronize identifiers or required runtime values rather than entire configuration assets.

---

## Unity conventions

* One primary type per C# file.
* File name matches the primary type.
* Prefer `sealed` when inheritance is not an intended extension point.
* Use private serialized fields instead of public mutable fields.
* Serialized private fields use `_camelCase`.
* Public members use `PascalCase`.
* Parameters and local variables use `camelCase`.
* Prefer early returns over unnecessary nesting.
* Use `nameof` for member and type names in diagnostics.
* Cache recurring component references.
* Avoid scene-wide searches during gameplay.
* Preserve the existing namespace strategy.
* Avoid `async void` except where Unity entry points require it.
* Async operations and coroutines must account for cancellation, destruction and session shutdown when relevant.
* Do not suppress warnings without documenting why.

Use `[RequireComponent]` only when the dependency must exist on the same GameObject.

Use `[DisallowMultipleComponent]` when multiple instances would be invalid.

Editor-only dependency lookup is acceptable inside `Reset` or `OnValidate` when safe.

---

## Generated files

Never manually edit generated files.

This includes:

```text
PlayerInputActions.cs
```

Input actions must be changed through:

```text
PlayerInputActions.inputactions
```

Allow Unity Input System to regenerate the C# wrapper.

Treat files marked as generated or containing `<auto-generated>` headers as read-only unless the task explicitly concerns their generator.

---

## Scenes, prefabs and serialized assets

Do not modify scenes or prefabs unless the requested change requires it.

When a task depends on current serialized configuration, inspect the actual prefab or scene rather than inferring Inspector values.

Do not invent Inspector assignments.

Do not replace serialized dependencies with runtime searches merely to avoid Inspector configuration.

Preserve serialized field names unless a migration is part of the task.

Do not modify `.meta` GUIDs unnecessarily.

Do not claim that a scene, animation, visual effect or multiplayer flow was manually validated unless it was actually run and observed.

When manual Unity validation remains necessary, list it separately.

---

## Performance

Optimize recurring gameplay and simulation paths deliberately.

For per-frame or per-tick code:

* Avoid managed allocations.
* Avoid LINQ.
* Avoid closures.
* Avoid repeated component lookup.
* Avoid repeated string operations.
* Prefer reusable buffers for recurring physics queries.
* Prefer non-allocating physics APIs when practical.

Outside hot paths, readability has priority over speculative micro-optimization.

---

## Error handling

Validate mandatory dependencies during initialization.

Fail clearly when required configuration is missing.

Use the affected Unity object as log context when available.

Do not silently fall back to behavior that hides configuration errors.

Do not use exceptions for normal gameplay flow.

Network startup, shutdown and transition failures must leave the application in a valid state.

Avoid logs every frame or simulation tick.

---

## Testing and validation

Prefer EditMode tests for deterministic pure C# gameplay logic.

Separate deterministic calculations from MonoBehaviours when that improves testability.

For networked code, validate when applicable:

* Authority requirements.
* Host and client paths.
* Missing input.
* Disabled controls.
* Prediction.
* Resimulation.
* Irreversible side effects.

After modifying code:

1. Review the complete diff.
2. Check compilation.
3. Run relevant automated tests when available.
4. Check for unintended generated-file changes.
5. Check for accidental scene, prefab or asset changes.
6. Report what was actually validated.
7. List validation that still requires Unity Editor or multiplayer testing.

Never claim a test passed unless it was executed successfully.

Do not claim manual Play Mode or visual validation that was not performed.

---

## Documentation changes

Architecture decisions affecting multiple systems belong under:

```text
Docs/Architecture/
```

Do not duplicate large architecture or Game Design specifications inside `AGENTS.md`.

Reference their authoritative source instead.

Update architecture documentation when implementation intentionally changes an architectural contract.

Do not update Game Design documentation merely to make it agree with an implementation unless the task explicitly changes Game Design.

Comments and technical documentation are part of the implementation and must remain consistent with the code.

---

## Dependency policy

Do not add, remove or update Unity packages without explicit approval.

Before introducing a dependency:

* Confirm the project does not already solve the problem.
* Explain why the dependency is necessary.
* Identify runtime and editor impact.
* Consider licensing and platform implications when relevant.

Do not introduce without explicit approval and concrete justification:

* Dependency injection frameworks.
* ECS.
* Reactive frameworks.
* General-purpose event frameworks.
* Alternative networking libraries.

---

## Change policy

Keep diffs focused on the requested outcome.

Write commit subjects and descriptions in English.

Do not:

* Rename unrelated files.
* Reformat unrelated code.
* Move folders unnecessarily.
* Change public APIs without identifying consumers.
* Delete code solely because it appears unused without searching references.
* Leave placeholder implementations.
* Leave obsolete commented-out code.
* Add speculative systems for future features.

When replacing an existing behavior, remove the obsolete source of truth when safe rather than keeping parallel implementations.

For migrations:

1. Identify the existing contract.
2. Identify its consumers.
3. Define the target contract.
4. Apply the smallest safe migration.
5. Remove obsolete paths when no longer needed.

---

## Task scope

A task should produce one concrete and verifiable result.

Do not expand a task into the complete design or implementation of adjacent systems.

When reviewing an existing task, determine whether missing work:

* belongs to the task;
* should become an independent subtask;
* or belongs to a later task.

Do not consider a task incomplete because unrelated future decisions remain unresolved.

Subtasks should:

* Represent independently completable work.
* Avoid duplicating each other.
* Have explicit dependencies when necessary.

Foundational tasks should establish only the decisions required by dependent work.

Do not convert future design decisions into acceptance criteria for the current task.

Keep scope realistic for the project team and MVP.

---

## Implementation plans

Plans should describe the intended result and important architectural decisions without prescribing unnecessary implementation details.

A plan must make clear:

* What will change.
* Which existing contracts are affected.
* Which files or systems are expected to be involved.
* What remains outside scope.
* What conditions indicate completion.
* What requires manual validation.

Do not describe every trivial coding step.

Do not instruct an implementation agent to perform validation it cannot actually perform.

The implementation agent may independently resolve ordinary code-level decisions that remain consistent with:

* Current repository architecture.
* Relevant architecture documents.
* Current Game Design.
* The requested scope.

Ask for confirmation when the task requires:

* Changing an approved architecture.
* Changing Game Design.
* Adding dependencies.
* Expanding scope materially.
* Choosing between incompatible product-level behaviors not resolved by authoritative sources.

---

## Code documentation

Code should be understandable without comments that merely translate the implementation into prose.

Prefer:

* Clear names.
* Narrow responsibilities.
* Small methods.
* Explicit contracts.

Use XML documentation for public or architecture-sensitive APIs when their contract is not obvious.

Documentation is particularly useful for:

* Authority-sensitive methods.
* Prediction or resimulation behavior.
* Methods with non-obvious side effects.
* Shared APIs.
* Important execution-order requirements.
* Architectural boundaries.

Inline comments should explain why a constraint or non-obvious decision exists.

Do not add comments that merely repeat what the code already says.

Do not leave outdated comments, obsolete TODOs or commented-out code.

When a workflow spans multiple components, document the complete relationship in the relevant architecture document rather than duplicating it across every class.

---

## Definition of done

A coding task is complete when:

* The requested behavior is implemented.
* The implementation is consistent with current Game Design.
* Relevant approved architecture is respected or intentionally updated.
* Responsibilities remain clear.
* Network authority is explicit where applicable.
* Predicted code supports resimulation where applicable.
* Relevant automated validation was executed when available.
* The complete diff was reviewed.
* No unrelated files were changed.
* No generated files were manually edited.
* Remaining manual Unity or multiplayer checks are explicitly listed.
* Known limitations or unverified behavior are reported.

Do not declare work complete while known requirements inside the task's actual scope remain unresolved.
