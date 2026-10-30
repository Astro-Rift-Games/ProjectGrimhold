# Workflow for HacknPlan task design

## Sources and scope

Read the selected project's AGENTS and existing planning conventions. Current code/serialized configuration prove implementation; approved Architecture owns contracts; current Game Design owns behavior. Use the project's authoritative document connector when available, not cached IDs or memory as the source of gameplay.

For Project Grimhold, route named existing work items through HacknPlan first, relationships through CodeGraph, gameplay intent through grimhold-docs, and Editor-dependent questions through Unity MCP only when needed. Do not open/mutate Unity merely to draft a task. Resolve referenced documents by search rather than baking private project IDs into this skill.

Check existing stories/tasks in the destination and relevant prior boards/backlog. Read likely overlapping items and their parents. Completed foundations may be referenced as predecessors of a corrective task; they do not prove a newly audited guarantee holds. Do not move, reopen, duplicate, or rewrite them without authorization.

Report a conflict before creating a new contract. A user-authorized additional art commission can explicitly sit outside an old production scope; that does not approve new gameplay or amend Game Design.

## Fixed format

Use exactly these labels, in this order, for every story, standalone task and child:

1. Documentos de diseño:
2. Objetivo:
3. Dependencias:
4. Alcance:
5. Fuera de alcance:
6. Criterios de aceptación:
7. Validación:

Use numbered subsections under Alcance for detail. Add the story's task breakdown inside Alcance, not as a substitute for child records. For a section that genuinely does not apply, say so briefly rather than deleting the label or inventing material. Replace every template placeholder before publication.

Choose the smallest useful unit: a task produces a concrete deliverable; a story groups work needed for a user outcome. Do not manufacture stories for one art prop task. Do not finish a default story-creation request without its executable children; skip children only on an explicit request to do so.

A child includes its parent's real ID and the specific criterion/result it owns, its own deliverables, exclusions, dependencies, acceptance and validation. Split on coherent outcomes and ownership, not an arbitrary number of tasks or lines. Implementation tasks include focused regression tests; an integration-closure task complements those tests rather than deferring them.

## Useful acceptance and evidence

- Separate a requested behavior from observed current behavior, a reproduced failure, a conditional threat, and a proposed decision.
- Make acceptance observable: valid outcome, meaningful invalid cases, preserved existing behavior, and unchanged authoritative/persistent state after rejection when applicable.
- Name only real available validation paths. Planning describes checks to run; it never reports those checks as passed.
- For runnable deterministic corrections, require observed RED then GREEN and relevant regression checks. Record why RED does not apply for passive art/docs or unavailable meaningful runners.
- State operational, dependency, schema/data, gameplay and design approvals where they are required; creating a ticket grants none of them.
- Avoid fake precision: estimate only from evidence or a user/team agreement. Missing dimensions/counts/timing become explicit production decisions, not guessed numbers.
- Keep operational state in HacknPlan; do not create duplicate local ODD/SDD trackers for an existing work item unless requested.

## Live metadata and capabilities

Before writes, discover current tools and resolve the exact project/board and metadata. Never assume Sprint 14, project IDs, priority IDs, category IDs, team members or tool availability from an example.

- Story: isStory=true and the live User story category when supported.
- Child: isStory=false, actual returned parentStoryId, correct work category and target board.
- Standalone task: isStory=false; no artificial parent.
- Resolve displayed US/TASK numbers by title/work-item lookup; title suffixes are not API IDs. Follow existing naming and verify numbering before adding it.
- Use live priority/stage mappings; do not invent a Critical tier when the service calls it Urgent.
- Verify actual pages/next-page behavior; do not assume totalCount equals returned items.
- Resolve an explicitly requested assignee by exact project membership/name/username. Creator/primaryUser is not assignedUsers. Never infer all future work belongs to a previously mentioned teammate.

### Taxonomía y gestión de tags

Los tags en HacknPlan permiten filtrar y entender el alcance temático de una tarea en el tablero sin tener que abrir su detalle. Sigue estas reglas estrictas:

- **Prohibido usar roles como tags**: Nunca asignes tags de disciplina o rol como `Arte`, `Level Design`, `Game Design`, `Programación` o `Backend`. Esos roles ya están declarados en el prefijo del título (`ART`, `TASK`, `GD`, `SEC`) o en la categoría (`Category`) del work item.
- **Tags de área, sistema o documento rector**: Los tags deben nombrar el subsistema, mapa o documento al que pertenece la tarea, siempre en **español** y en **mayúsculas**.
- **Reutilización y creación bajo demanda**:
  - Consulta primero los tags existentes en el proyecto (`GET /projects/{projectId}/tags`).
  - Reutiliza siempre los tags existentes si la tarea corresponde a un sistema o área ya catalogada.
  - Si una tarea introduce un área, mapa o subsistema nuevo que no tiene tag (por ejemplo, el futuro mapa del pueblo `PUEBLO`, un nuevo sistema de comercio `COMERCIO`, etc.), **crea el tag bajo demanda** con un color distintivo antes de asociarlo (`POST /projects/{projectId}/tags`).
- **Catálogo base de tags en Project Grimhold**:
  - `CATACUMBAS` (`#455A64`): Mapa actual de Catacumbas, arquitectura y estructuras del mapa.
  - `TILESET` (`#546E7A`): Kit modular de tiles (se combina con el mapa actual, ej. `TILESET` + `CATACUMBAS`; a futuro `TILESET` + `PUEBLO`).
  - `COMBATE` (`#E03B24`): Combate, proyectiles, knockback, friendly fire, weapon sets (Docs 11, 17, 18, 19).
  - `LOOT` (`#F39C12`): Inventario, recolección, consumibles, utilidades, cofres, quick slots (Docs 07, 14, 16).
  - `COLAPSO` (`#E65100`): Inestabilidad de la dungeon, ondas de ruptura, grietas y evacuación (Doc 20).
  - `ABATIDO` (`#9C27B0`): Ciclo de abatido (*downed*), reanimación, auto-reanimador y post-proceso visual (Doc 13).
  - `PRESENTACION` (`#00BCD4`): Feedback visual en pantalla (*screen damage*), efectos de cámara, post-procesos y HUD (Doc 03).
  - `SEGURIDAD` (`#880E4F`): Prevención de cheating, HMAC, sanitización de payloads de red y protección de credenciales/secretos.
  - `HABILIDADES` (`#3F51B5`): Compra y desbloqueo autoritativo de habilidades (*Ability Vendor*).
  - `MISIONES` (`#009688`): Contratos y sistema de misiones (Doc 21).
- **Asignación múltiple coherente**: Una tarea puede tener varios tags si cruza sistemas o áreas (ej. una puerta interactiva lleva `TILESET` + `CATACUMBAS`; screen damage lleva `COMBATE` + `PRESENTACION`; auto-reanimador lleva `LOOT` + `ABATIDO`).
- **Operaciones de API**:
  - Asignar tag: `POST /projects/{projectId}/workitems/{workItemId}/tags` enviando el `tagId` en crudo en el body como número (ej. `"3"`).
  - Desasignar tag: `DELETE /projects/{projectId}/workitems/{workItemId}/tags/{tagId}`.

Use connector mutations when supported. If editing/assignment is unavailable, disclose that and ask for an authorized alternative. Do not obtain ambient API keys, cookies, credentials or browser sessions. Browser approval is scoped to its stated records/operation; an earlier assignment authorization is not permanent permission. Do not recreate/delete existing records merely to change metadata without an explicit user choice.

If dependency relations cannot be set by the available tool, put references in Dependencias and report that formal blocking links were not created. Descriptions are not proof of API relationships.

## Publication and recovery

Build the parent first, capture its confirmed identity, then create its children with that actual ID. Record each confirmed creation immediately so a later failure does not lose successful work.

For a failed or unknown mutation outcome, stop dependent writes and do not blindly retry. Reconcile read-only against confirmed IDs or exact titles in the intended scope. Resolve the exact outcome before another creation; preserve existing partial results and report the next action.

Read back titles, full descriptions, project/board, category, story flag, parent, stage, priority and requested assignees. Check each story's expected child set, no unintended duplicates, and unchanged unrelated records. Verify externally changed state rather than treating a click or API attempt as completion.

A partial batch is not complete: report created IDs, missing children/assignments, and the precise blocker. When only drafting was authorized, return drafts and perform no remote writes.
