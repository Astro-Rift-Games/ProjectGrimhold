# AGENTS.md

## Scope and repository map

These instructions apply to the whole `Astro-Rift-Games/ProjectGrimhold` repository.

```text
ProjectGrimhold/
├── AGENTS.md
├── Project Grimhold/        # Unity project
│   ├── AGENTS.md            # Unity/Fusion rules
│   ├── Assets/
│   ├── Docs/Architecture/
│   ├── Packages/
│   └── ProjectSettings/
├── backend/
├── docs/
└── skills/
```

For work under `Project Grimhold/`, read `Project Grimhold/AGENTS.md` before task-specific work. Do not assume a nested `AGENTS.md` was auto-loaded when the session started from repository root.

Project Grimhold is a multiplayer 2D top-down PvPvE extraction RPG. Favor simple, explicit, modular solutions appropriate for a four-person MVP team. Avoid speculative systems and framework-heavy architecture.

---

## Existing HacknPlan work items

When the user names an existing User Story, TASK or subtask:

1. Read it first through HacknPlan MCP and read its parent when relevant.
2. Treat it as the scope/acceptance contract.
3. Do not redesign, recreate, split or update it unless explicitly requested.
4. Classify discovered missing work as current-task, follow-up, or blocker.
5. Do not make the current task responsible for unrelated future systems.
6. If HacknPlan is read-only, do not claim remote mutations were performed.

HacknPlan defines requested scope. It does not override current implementation, approved architecture, or authoritative Game Design for responsibilities owned by those sources.

For an existing named HacknPlan TASK/User Story, HacknPlan is the work-tracking
source of truth. Do not create odd/tasks, SDD task records, local task mirrors,
or other duplicate tracking artifacts unless explicitly requested.

---

## Sources of truth

Resolve authority by responsibility.

### Current technical state

1. Current code, prefabs, scenes, assets, packages and configuration in the target branch.
2. Relevant approved technical documents, primarily `Project Grimhold/Docs/Architecture/`.
3. Repository `AGENTS.md` instructions.
4. Current live Game Design when gameplay intent is relevant.
5. Engram, uploaded copies, plans, walkthroughs and prior conversations only as supplemental context.

Current implementation proves what exists; it does not automatically prove intended behavior.

### Gameplay intent

Authoritative gameplay rules live in the connected Google Drive.

When gameplay behavior matters:

- read the live owning document;
- do not substitute memory, cached summaries or uploaded/exported copies while live Drive is available;
- read all owning documents for cross-system changes;
- do not invent unresolved Game Design.

### Conflicts

When sources disagree:

1. identify the conflict;
2. identify which source owns each responsibility;
3. distinguish current implementation from intended behavior;
4. report the conflict before creating another contract;
5. apply the smallest change that restores one source of truth;
6. ask before changing approved architecture or Game Design.

---

## Game Design routing

Use this only to locate the live source.

- `00` Concept/MVP scope.
- `01` Game flow and session continuity.
- `02` game states, Town preparation, Ready, disconnect/participation.
- `03` player movement, sprint, orientation, camera, base controller.
- `04` persistent build, attributes, equipped abilities, preparation.
- `05` XP, levels and consolidation.
- `06` extraction.
- `07` loot lifecycle/persistence.
- `08` derived stats and Health/Stamina/Mana formulas.
- `09` concrete equipment and Weapon Sets.
- `10` Dungeon lifecycle/encounters/Collapse.
- `11` abilities.
- `12` missions.
- `13` downed/revive/final defeat.
- `UI - Auditoría Funcional de Interfaces MVP` for functional UI.
- `GD-11` for modular visual character direction.
- `GD-12` for structural equipment slots/compatibility.

Common cross-system routes:

- Abilities: `04` + `08` + `11` + relevant Input/Combat/Status/Presentation architecture.
- Equipment: `GD-12` + `09` + `08` when attributes participate.
- Town → Raid preparation: `01` + `02` + preparation/session architecture.
- Downed/revive: `02` + `13` + affected Combat/Extraction/Loot/Progression contracts.
- Missions: `12` + each system that owns consumed events + persistence architecture.
- Functional UI: UI audit + owning Game Design + relevant presentation architecture.

Do not read unrelated design documents by default. If a required live Drive source is unavailable, report that before making a definitive design-dependent decision.

---

## Tool routing

| Need | Route |
|---|---|
| Existing TASK/US | HacknPlan first |
| Code relationships / blast radius | CodeGraph first → source verification |
| Current implementation | Repository source |
| Technical contract | Relevant Architecture |
| Gameplay behavior | Live Google Drive |
| Prefab/scene/Inspector/serialized state | Unity MCP + source |
| Input Actions / ScriptableObject Editor state | Unity MCP when needed |
| Unity compile/Console/Test Runner/Play Mode | Unity MCP |
| Version-sensitive Unity/Fusion API | Verify repo version → Context7/official docs |
| Previous findings | Engram, supplemental only |

Do not use Unity MCP as a substitute for CodeGraph/source inspection.
Do not use CodeGraph as final behavioral authority.
Do not let Engram or prior chats override current repository/Architecture/live Game Design.

---

## Repository workflow

Before modifying anything:

1. Confirm repository, target branch and working tree.
2. If a branch was specified, work only from that branch.
3. Inspect current implementation; use CodeGraph first when relationships matter.
4. Read relevant Architecture.
5. Read live Game Design only when behavior depends on it.
6. Inspect serialized/Editor/runtime state only when the task depends on it.
7. Identify ownership, authority, persistence and dependency boundaries.
8. Keep the change limited to the work item.

For exact Unity, Photon Fusion or package versions, verify repository files in `New-Testing`; do not infer versions from memory or historical docs.

Do not create worktrees unless explicitly requested. Do not make unrelated refactors.

---

## Planning, SDD and delegation

A sufficiently defined HacknPlan TASK is already a work contract. Do not automatically generate another proposal/spec/task breakdown.

For architectural, cross-system or multi-file work, produce a focused implementation plan covering result, affected contracts, expected files/assets, exclusions and validation.

Use Gentle AI SDD only when the work is not sufficiently specified, needs proposal/spec/design before implementation, or the user explicitly requests SDD.

Subagents may be used for bounded exploration, review or verification when they materially reduce context pollution or parallelize independent work. Do not spawn them for trivial tasks. Give each delegated agent exact scope, required skills/sources, read/write boundaries and expected output. The parent agent reconciles all results.

---

## Project skill

For implementation, review, debugging or completion of an existing Project Grimhold work item, load:

```text
skills/grimhold-task-execution/SKILL.md
```

After adding/moving/editing project skills:

```text
gentle-ai skill-registry refresh --force
```

Do not duplicate the skill body here.

---

## Scope and change policy

A task should produce one concrete, verifiable result.

Do not implement adjacent future work opportunistically. Foundational tasks close only the decisions required by dependents.

Ask before:

- changing approved architecture;
- changing Game Design;
- adding/removing/updating dependencies;
- materially expanding scope;
- choosing between incompatible product behaviors not resolved by authoritative sources.

Keep diffs focused. Do not rename/reformat unrelated files, change public APIs without checking consumers, delete code without checking references, leave placeholder/commented-out obsolete code, or introduce speculative abstractions.

When replacing behavior: identify current contract/consumers, define target contract, migrate minimally, then remove obsolete paths when safe.

Commit subjects/descriptions should be in English.

---

## Validation and reporting

After changes:

1. review the full diff;
2. run relevant validation actually available;
3. check unintended generated/scene/prefab/asset changes;
4. compare against every acceptance criterion;
5. report only checks actually executed;
6. list remaining manual Unity/multiplayer checks;
7. report blockers/conflicts/follow-up work outside the current task.

Never claim tests, Play Mode, visuals, scenes, prefabs or multiplayer flows were validated unless actually run and observed.

A task is complete only when requirements inside its actual scope are resolved or an explicit blocker is reported.
