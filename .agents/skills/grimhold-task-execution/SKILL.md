---
name: grimhold-task-execution
description: "Trigger: Project Grimhold task, HacknPlan TASK, US implementation. Execute an existing Grimhold work item through authoritative sources and verified delivery."
license: Apache-2.0
metadata:
  author: "Astro-Rift-Games"
  version: "1.1"
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
- Execute through the ODD workflow and its native Task Size classification (repository `AGENTS.md`, ODD execution). Subagent delegation is the orchestrator's decision.
- Never duplicate, split or recreate the HacknPlan item, in HacknPlan or in local proposal/specification records.
- Minimize validation context: prefer targeted tests, filtered Console output, and concise success summaries; expand details only for failures.

For an existing named HacknPlan TASK/User Story, HacknPlan is the scope and
work-tracking source of truth. A substantial task may use an ODD execution
tracker (`odd/tasks/<feature>.md`) that links the item and quotes its acceptance
criteria; it never adds or re-scopes work.

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

Follow the ODD sequence and the validation rules in the AGENTS files:

1. **Scope:** read the HacknPlan item (and parent) and resolve objective, acceptance criteria and exclusions before touching code.
2. **Explore:** relevant code, callers and tests; owning Architecture; live Game Design when behavior is gameplay-dependent; affected dependencies and versions. Establish ownership, authority and affected contracts.
3. **Resolve:** report missing design, architecture or approvals as blockers; ask instead of inventing.
4. **Classify:** apply ODD's native Task Size. Small: implement directly. Substantial: create the ODD tracker before the first write.
5. **Implement and validate:** smallest coherent change, test-first where applicable, proportionate checks.
6. **Close:** check the full diff and every acceptance criterion; commits and pushes only with explicit authorization.

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

- `../../../AGENTS.md`
- `../../../Project Grimhold/AGENTS.md`
- `references/tool-routing.md`
