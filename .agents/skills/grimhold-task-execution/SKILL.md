---
name: grimhold-task-execution
description: "Trigger: Project Grimhold task, HacknPlan TASK, US implementation. Execute an existing Grimhold work item through authoritative sources and verified delivery."
license: Apache-2.0
metadata:
  author: "Astro-Rift-Games"
  version: "1.0"
---

## Activation Contract

Load this skill when implementing, reviewing, debugging or completing an existing Project Grimhold TASK/User Story.

Do not load it for backlog creation, Game Design authoring or unrelated repositories.

## Hard Rules

- Read repository `AGENTS.md` first; for Unity work also read `Project Grimhold/AGENTS.md`.
- Read the existing HacknPlan work item and parent when relevant. Treat them as scope, not as a substitute for technical or Game Design sources.
- Do not redesign/recreate the task unless explicitly requested.
- Use CodeGraph first for broad code discovery, then verify important conclusions in source.
- Read only relevant Architecture and live Game Design documents.
- Use Unity MCP only when correctness depends on Editor, serialized or runtime Unity state.
- Never let Engram, uploads or prior chats override current repository/Architecture/live Game Design.
- Do not expand the task into adjacent systems.
- Do not automatically create SDD specs/tasks that duplicate a defined HacknPlan task.
- Minimize validation context: prefer targeted tests, filtered Console output, and concise success summaries; expand details only for failures.

For an existing named HacknPlan TASK/User Story, HacknPlan is the work-tracking
source of truth. Do not create odd/tasks, SDD task records, local task mirrors,
or other duplicate tracking artifacts unless explicitly requested.

## Decision Gates

| Need | Route |
|---|---|
| TASK/US scope | HacknPlan first |
| Code relationships/blast radius | CodeGraph → source |
| Technical contract | Relevant Architecture |
| Gameplay behavior | grimhold-docs |
| Prefab/scene/Inspector/Input/ScriptableObject | Unity MCP + source |
| Unity compile/tests/Play Mode/Console | Unity MCP |
| Version-sensitive external API | Verify repo version → Context7/official docs |
| Historical finding | Engram, supplemental only |

## Execution Steps

Follow the repository workflow and validation rules in the AGENTS files. The order that matters: resolve the TASK's objective, acceptance criteria and exclusions before touching code; establish ownership, authority and affected contracts before planning; plan only for architectural, cross-system or multi-file work; implement the smallest coherent change; then check the full diff and every acceptance criterion.

When design, architecture or an Inspector assignment is missing, report it as a blocker rather than inventing it.

## Output Contract

Return:

- TASK/US addressed;
- implemented result;
- files/assets changed;
- acceptance criteria status;
- validation actually executed and results;
- manual Unity/multiplayer validation still required;
- blockers/conflicts/follow-up work outside the current TASK.

## References

- `../../AGENTS.md`
- `../../Project Grimhold/AGENTS.md`
- `references/tool-routing.md`
