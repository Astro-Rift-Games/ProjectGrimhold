# Project Grimhold Tool Routing

This file supports `grimhold-task-execution`. It does not replace repository `AGENTS.md`.

## HacknPlan

Use HacknPlan first when the user identifies an existing TASK/User Story.

Read the task, parent, acceptance criteria, dependencies and related existing work items. Do not assume the title contains the complete scope.

Treat HacknPlan as the work-scope contract. Do not treat it as proof of current implementation or as an architectural source.

If the connector is read-only, do not claim remote mutations were performed.

## CodeGraph

Use CodeGraph before broad manual repository reads when code relationships matter.

Prefer it for:

- symbol discovery;
- callers/callees;
- inheritance/dynamic dispatch;
- dependency paths;
- affected tests;
- blast radius;
- locating subsystem boundaries.

Then read the actual source files needed to verify behavior and contracts.

## Repository

The target branch is the source of current technical state.

Verify:

- current code;
- prefabs/scenes/assets;
- packages and exact versions;
- serialized configuration;
- implementation ownership;
- lifecycle and network authority.

Do not infer implementation state from old plans, walkthroughs or memory.

## Architecture

Read the smallest set of relevant files under `Project Grimhold/Docs/Architecture/`.

Architecture owns sources of truth, data ownership, network authority, persistence, dependency direction, lifecycle and presentation boundaries.

When Architecture and implementation disagree, report the conflict before creating a new contract.

## Google Drive

Use current connected Google Drive documents for gameplay intent.

Follow the routing in repository `AGENTS.md`.

Do not use uploaded/exported copies when the live source is available.

## Unity MCP

Use Unity MCP when correctness depends on Unity state that cannot be established reliably from source alone.

Typical triggers:

- prefab composition;
- scene hierarchy;
- serialized references;
- Inspector values;
- Input Action configuration;
- ScriptableObject configuration;
- NetworkObject/NetworkBehaviour composition;
- Unity compilation/Console;
- Test Runner;
- Play Mode;
- visual/animation verification.

Do not invoke Unity MCP for documentation-only or pure-code tasks unless the task actually depends on Editor/runtime state.

Do not mutate scenes, prefabs or assets unless the current work item requires it.

## Context7 / official docs

Use external documentation only after verifying the installed package/version in the repository.

Use it for version-sensitive Unity, Photon Fusion or other dependency API behavior.

Repository versions override remembered API assumptions.

## Engram

Use Engram only for continuity: previous findings, decisions or investigation state.

Never allow Engram to override current repository state, approved Architecture or live Game Design.

## SDD and subagents

A defined HacknPlan task should normally be executed directly.

Use SDD when the feature is not sufficiently specified, needs proposal/spec/design work before implementation, or the user explicitly requests SDD.

Use subagents for bounded independent exploration/review/verification when they reduce context pollution. Give them exact scope, sources, skills and write boundaries. The parent agent reconciles all findings.

### Validation economy

Minimize Unity MCP context without reducing verification quality.

- Prefer targeted tests before broad suites.
- For successful test runs, retain only suite name, totals, failures and duration.
- Expand individual test output only when tests fail.
- Query Console errors/exceptions first; do not retrieve unrelated logs.
- Prefer targeted prefab/component/property inspection over full hierarchy or serialized dumps.
- A Unity refresh/recompile is cheap; avoid optimizing it away unless it adds no validation value.
- For visual, game-feel or multi-peer exploratory checks, prefer explicit manual validation when automation would add substantial context without improving confidence.