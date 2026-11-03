# Mission System: Guild de Contratos en Pueblo, HUD y Productores de Eventos (US-75)

Objective: Complete the full cycle of the Mission System for Project Grimhold: Town Guild NPC and Contract Board UI, active mission HUD tracker in Raid, 6 canonical Rank E daily contracts, dynamic resource counting and atomic claim deduction, and authoritative event producers for all MVP mission families with Duo co-op rules.

Baseline: `New-Testing` (`8acc8023`), branch `feat/us-75-missions-guild-contracts-hud`.

Sources:
- HacknPlan `US-75` (`#650`): *Sistema de Misiones: Guild de Contratos en Pueblo, HUD y Productores de Eventos*
  - `TASK-259` (`#651`): *NPC interactable de la Guild de Misiones en Pueblo (TownMissionNpcInteractable)*
  - `TASK-260` (`#652`): *Interfaz de la Guild de Misiones en Pueblo (Disponibles, Aceptar, Abandonar, Reclamar)*
  - `TASK-261` (`#653`): *Tracker de misiones activas en el HUD de Incursión*
  - `TASK-262` (`#654`): *Emisores de eventos para familias restantes de misiones (Exploración, Obtención, Extracción y PvP)*
  - `TASK-263` (`#655`): *Reputación de Guild y progresión de Rangos en perfil persistente*
  - `TASK-270` (`#687`): *Configuración y Registro en Catálogo de los 6 Contratos Rango E (CON_E_001 a CON_E_006)*
  - `TASK-271` (`#695`): *Reglas de Recursos para Contratos E-005/E-006: Posesión Dinámica, Misma Expedición y Deducción en Claim*
- Design Docs: Doc 12 §4, §5, §6, §7, §8, §9, §10, §11, §14; Contratos Diarios Rango E §§3–7.
- Architecture: `Docs/Architecture/MissionSystemArchitecture.md`, `LocalPlayerPersistenceArchitecture.md`.

Acceptance Criteria (quoted from `#650`):
- El jugador puede abrir el panel de contratos interactuando con el NPC de la Guild en Pueblo.
- El panel permite aceptar contratos Rango E respetando el límite de activas (3), abandonar misiones y reclamar atómicamente recompensas de misiones completadas.
- Si el Stash no tiene espacio suficiente para recibir los items de recompensa, la reclamación se bloquea y la misión permanece en estado de cobro pendiente.
- Durante la incursión, el tracker del HUD actualiza los contadores inmediatamente al producirse los eventos correspondientes.
- Las reglas cooperativas de Dúo permiten avance compartido en bajas PvE e interacción de cofres en la misma zona/proximidad, mientras que extracción y recolección se validan individualmente.
- Los contratos de entrega de recursos (E-005 y E-006) reflejan dinámicamente la posesión en inventario personal, validan expedición única y deducen atómicamente los materiales en claim sin tocar el Stash.

Exclusions:
- Misiones semanales, rotaciones automáticas de reset global o cadenas narrativas avanzadas.
- Contratos Rango D o superior (reservados para expansiones de contenido).
- Persistencia remota en base de datos física (el perfil local transaccional es la fuente de verdad MVP).

Work breakdown:
- [x] Phase 1: Canonical Rank E contracts (`TASK-270`) & Guild reputation model + persistence (`TASK-263`).
- [ ] Phase 2: Dynamic inventory resource counting, single expedition validation & atomic claim deduction (`TASK-271`).
- [ ] Phase 3: Authoritative Raid event producers (Exploration, Items, Extraction, PvP) & Duo co-op credit (`TASK-262`).
- [ ] Phase 4: Town Guild NPC interactable (`TASK-259`) & Guild Contracts UI Panel (`TASK-260`).
- [ ] Phase 5: Active Mission HUD Tracker in Raid (`TASK-261`).
- [ ] Phase 6: Comprehensive EditMode & PlayMode test suites and manual validation in Town and Raid.

Validation & status:
- Compilation (`Assembly-CSharp` & `Assembly-CSharp-Editor`): 0 errors.
- Unit tests (`RankEContractsAndGuildReputationTests.cs`): Productive catalog validation, 6 canonical Rank E definitions, GuildRank transitions (0..99 -> E, 100+ -> D), savegame codec SchemaVersion 10 migration & backward compatibility, atomic claim reputation accumulation, and rank-gated mission acceptance.
