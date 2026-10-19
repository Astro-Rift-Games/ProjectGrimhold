# Project Grimhold — repository instructions

## Scope and instruction levels

This file defines the shared development instructions for the entire `Astro-Rift-Games/ProjectGrimhold` repository.

These instructions must work independently of each contributor's personal agent configuration.

- **Repository instructions (this file):** shared development standards, repository navigation, sources of truth, work-item scope, and cross-system boundaries.
- **Unity instructions (`Project Grimhold/AGENTS.md`):** Unity/Fusion configuration, simulation, assets, performance, and testing.

Personal or global agent instructions may supplement these rules but must not be required to understand or work on this repository.

Before modifying files under `Project Grimhold/`, read the nested Unity instructions if they are not already loaded.

Project Grimhold is a multiplayer 2D top-down PvPvE extraction RPG built by a four-person MVP team.

## Shared Development Standards

- Follow existing architecture and coding conventions; inspect relevant implementation and contracts before changing them.
- Prefer simple, maintainable solutions. Keep changes within the requested scope; avoid unrelated refactors, speculative abstractions and unnecessary dependencies.
- Preserve contributors' uncommitted work. Do not overwrite, revert or clean up unrelated changes; ask when they conflict with the task.
- Commits, pushes, publishing and destructive operations require explicit authorization for that action and target. Permission to edit is not permission to deliver or discard work.
- Run relevant tests and available build, lint or structural checks. Use failing tests before behavior fixes when practical; documentation-only work needs proportionate Markdown/reference checks.
- Review the full task diff against acceptance criteria, including unintended generated or serialized changes. Report observed results, failed/skipped/unavailable checks, remaining manual checks and out-of-scope follow-ups honestly.

## Repository map

| Path | Responsibility |
| --- | --- |
| `Project Grimhold/Assets/Scripts/` | Unity gameplay, networking, application services and backend clients |
| `Project Grimhold/Assets/Tests/` | Unity EditMode and PlayMode tests |
| `Project Grimhold/Docs/Architecture/` | Technical contracts and system-specific architecture |
| `Project Grimhold/Packages/`, `Project Grimhold/ProjectSettings/` | Unity dependencies and project configuration |
| `backend/` | Node.js, Express and Mongoose service; see [backend README](backend/README.md) and [package scripts](backend/package.json) |
| `docs/` | Supporting audits and plans, not a replacement for architecture or live Game Design |
| `tools/grimhold-docs-mcp/` | Read-only live documentation access; see [server README](tools/grimhold-docs-mcp/README.md) |
| `.agents/skills/` | Project-specific task execution and commit-message guidance |

## Sources of truth

Resolve authority by responsibility, not by treating every document as interchangeable.

| Question | Authoritative source |
| --- | --- |
| What exists now? | Code, assets and configuration in the target branch |
| What technical contract must be preserved? | Relevant approved sections of `Project Grimhold/Docs/Architecture/`; check status and distinguish current decisions from historical plans and future extensions |
| What gameplay behavior is intended? | Current live Game Design in Google Drive, normally accessed through `grimhold-docs` |
| What does a named TASK/User Story require? | Existing HacknPlan item and relevant parent |
| What was previously discussed? | Engram, prior chats, exports and plans are supplemental only |

Implementation proves current behavior, not intended behavior or approval. If sources conflict, identify which responsibility each owns, report the discrepancy and ask before changing approved architecture or Game Design. Do not silently turn stale documentation or current implementation into a new contract.

For gameplay-dependent work, use `search_documents` to discover each owning live document, `get_document_outline` as needed, then section-scoped `get_document` reads. Do not hardcode document IDs or substitute cached copies while `grimhold-docs` is available. See tool fallbacks below when access is unavailable.

## Cross-system contracts

Read the owning documents before changing a boundary; paths below are relative to `Project Grimhold/Docs/Architecture/`.

| Boundary | References |
| --- | --- |
| Shared domain identifiers, capabilities and public gameplay contracts | `Project_Grimhold_Contratos_Base_v1.md` |
| Backend identity/authentication and Unity integration | `RemoteIdentityAndAuthArchitecture.md` |
| Character aggregate, local cache, durable persistence and backend synchronization | `LocalPlayerPersistenceArchitecture.md` |
| Town-to-Raid preparation and session transitions | `TownRaidQueueArchitecture.md`, `LobbyAndSessionTransitionArchitecture.md` |
| Runner lifecycle, participant/avatar ownership and Raid cleanup | `SessionLifecycleAndSpawning.md`, `RaidParticipantArchitecture.md`, `RaidGenerationLifecycleArchitecture.md` |

Keep backend identity/persistence, application-local state and runner-scoped replicated state distinct. Unity accesses the backend through its HTTP clients, not direct MongoDB access. Do not treat a local cache or a Fusion `PlayerRef` as durable character identity. Preserve each document's ownership and acceptance boundaries instead of adding parallel stores or coordinators.

## Existing work items and planning

When the user names an existing HacknPlan TASK, User Story or subtask:

1. Read it first through HacknPlan MCP, including the parent when relevant; use the fallback below if MCP is unavailable.
2. Use it as the scope/acceptance contract; do not recreate, split, redesign or update it without an explicit request.
3. Classify missing work as current-task, follow-up or blocker. Foundational tasks resolve only what their dependents need now.

HacknPlan is the tracking source of truth for a named existing item. Do not create duplicate `odd/tasks`, SDD records or local task mirrors unless explicitly requested. A sufficiently defined item does not need another proposal/specification. If specification/design is unresolved, use Gentle AI SDD when available or a concise ordinary proposal; either requires resolution of open decisions before affected implementation.

For architectural or cross-system changes, provide a focused plan covering the result, affected contracts/files, exclusions and validation. File count alone does not require a plan.

## Project workflow and tools

- Confirm the repository, target branch and working tree before edits. Stay on a specified branch; do not create worktrees unless explicitly requested.
- Inspect relevant architecture and identify ownership, authority, persistence and dependency boundaries before implementation.
- Verify version-sensitive APIs against the target branch's configuration, then Context7 or official documentation. `New-Testing` is the existing version baseline; compare it when relevant, but do not substitute its state for the branch being edited.
- Unity Editor, serialized-state and runtime checks belong to the nested Unity instructions; Unity MCP does not replace source inspection.
- Ask before changing approved architecture, Game Design or dependencies. Dependency proposals must establish the existing project's gap and impact before adoption.
- When replacing a contract, check consumers, migrate them minimally and remove the obsolete path only when safe. Update the owning architecture for an authorized contract change; do not rewrite Game Design just to match implementation.

### Tool availability and fallbacks

Use the preferred tools when available; their absence changes the access method, not source authority or approval requirements.

| Tool | Preferred workflow | Safe alternative when unavailable |
| --- | --- | --- |
| HacknPlan MCP | Read the named item and relevant parent before work | Read them through the web UI if accessible, or ask for their current descriptions, acceptance criteria and relevant parent context. Block scope-dependent changes until sufficient evidence is supplied; do not recreate or update the item as a workaround. |
| `grimhold-docs` | Discover and read the owning live Game Design sections | Read the current source directly in Google Drive if accessible, or ask the owner for the relevant current sections and confirmation of their authority. Unverified exports remain supplemental; block design-dependent changes if authoritative intent is unresolved. |
| CodeGraph | Map relationships/blast radius, then verify in source | Use targeted text/symbol searches and inspect definitions, callers and tests directly. |
| Gentle AI | Use applicable project skills and SDD for unresolved design | Read available `SKILL.md` files directly and use ordinary planning, editing and verification tools. If a skill is missing, follow these repository instructions and report the gap. No Gentle AI installation or personal agent configuration is required. |
| Unity MCP | Inspect Editor/runtime state and execute Unity checks as specified by the nested instructions | Use the Unity Editor and Test Runner directly. If unavailable too, report the unverified state and block changes whose correctness depends on that missing evidence. |

Report unavailable tools and the alternative actually used. Missing tools never authorize inventing requirements or Inspector state, bypassing approved contracts, changing Game Design without approval, or claiming remote updates or checks that were not performed.

## Project skills

Read the relevant repository skill when available rather than duplicating its procedure:

- Existing work-item implementation, review, debugging or completion: [grimhold-task-execution](.agents/skills/grimhold-task-execution/SKILL.md).
- Drafting a commit title/description: [grimhold-commit-message](.agents/skills/grimhold-commit-message/SKILL.md).

After adding, moving or editing project skills, run `gentle-ai skill-registry refresh --force` when available; otherwise verify the skill paths/references directly and report that registry refresh was not run.
