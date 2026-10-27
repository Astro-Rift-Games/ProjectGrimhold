---
name: hacknplan-task-design
description: "Trigger: diseñar o crear tareas y user stories en HacknPlan. Use consistent Spanish briefs, executable child tasks, and verified creation."
license: Apache-2.0
metadata:
  author: "Dani"
  version: "1.0"
---

## Activation Contract

Use for designing, drafting, creating, or explicitly restructuring HacknPlan work items. Do not apply to implementing existing tasks, status lookups, or assignment-only requests.

## Hard Rules

- Default generated titles/descriptions to professional Spanish; honor explicit language overrides. Use the templates' seven labels in order, including child tasks.
- Tags identify game systems, areas, or governing design documents in uppercase Spanish (`CATACUMBAS`, `COMBATE`, `COLAPSO`, `LOOT`, etc.), NEVER functional roles (`Arte`, `Programación`, `Backend`, `Level Design`, `Game Design`), which belong to title prefixes or category. Always reuse existing tags from the project catalog or create a new tag on demand when introducing an uncataloged system or area.
- Planning-only requests do not authorize writes. Preserve existing work; do not duplicate stories, reopen completed items, or expand gameplay/infrastructure.
- Every created story needs executable child work items linked through its actual `parentStoryId`, unless the user explicitly requests no children. A standalone task needs no artificial story.
- Never invent requirements, asset dimensions, estimates, source links, IDs, ownership, tests, or mutation success. Distinguish confirmed defects, conditional risks, proposals, and pending decisions.
- Skill use grants no remote permission. Confirm the requested destination/operation/session; unsupported actions require an authorized alternative, not ambient credentials or recreated records.

## Decision Gates

| Request | Route |
| --- | --- |
| One concrete deliverable | Task template; create a real task |
| Broader user outcome | Story template plus coherent executable child tasks |
| Art production | Also read the art-brief reference |
| Open product decision | Ask once; do not choose silently |
| Technical production values unspecified | Record an explicit first activity to agree them before dependent production |
| Existing work item | Read it and its parent; change only authorized scope |

## Execution Steps

1. Read applicable project instructions and [workflow](references/workflow.md). Inspect current sources and existing items; resolve project, board, category, stage, priority, and requested assignee from live data.
2. Read the relevant [task](assets/task-template.md) or [story](assets/story-template.md) template. Preserve its labels; use numbered subsections within Alcance rather than inventing a different child-task format.
3. Draft concrete results, exclusions, dependencies, observable acceptance, and applicable validation. Put every requested rule in a task; include its own regression proof, not testing postponed until the end.
4. Check duplicates, source conflicts, unresolved decisions, and tool capabilities before mutation. Parent creation precedes child creation; use returned IDs, not title suffixes.
5. Execute only authorized operations. Capture each confirmed identity; stop blind retries on unknown mutation outcomes and reconcile read-only. Assign relevant reusable system/area tags to created items, creating any missing system/area tag on demand.
6. Read back every created item, parent link, and tags. Verify requested assignments separately from creator metadata and preserve unrelated fields/items. Report partial work and missing capabilities honestly.

## Output Contract

Return actual IDs/links, board, story/child counts, priority/stage/assignment status, and unresolved checks or decisions. Do not report story-only delivery as complete unless explicitly requested. Keep the reply concise; the complete specifications belong in the work items.

## References

- [Workflow and publication checks](references/workflow.md).
- [Task template](assets/task-template.md).
- [Story template](assets/story-template.md).
- [Art handoff details](references/art-brief.md) — only for art tasks.
