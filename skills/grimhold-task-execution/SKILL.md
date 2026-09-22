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

1. Resolve TASK/US objective, acceptance criteria, dependencies and exclusions.
2. Confirm branch and working tree; preserve unrelated changes.
3. Load applicable AGENTS files.
4. Explore affected code with CodeGraph when code relationships matter.
5. Read relevant Architecture and grimhold-docs.
6. Inspect Unity serialized/runtime state through Unity MCP only when required.
7. Identify ownership, authority, persistence and affected contracts.
8. For architectural/cross-system/multi-file work, produce a focused plan limited to the TASK.
9. Implement the smallest coherent change satisfying the TASK.
10. Review the complete diff and unintended changes.
11. Run applicable validation; use Unity MCP for Unity-side checks when required.
12. Compare the result against every acceptance criterion.
13. Report blockers instead of inventing missing design, architecture or Inspector assignments.

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
