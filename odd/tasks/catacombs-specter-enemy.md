# US-78 — Espectro de Catacumbas: Tercer Enemigo del MVP (IA, Incorporeidad y Combate Mágico)

**Objective:**
Implement the Catacombs Specter (Espectro de Catacumbas) as the third hostile enemy type of the MVP in Grimhold. Features authoritative FSM with alternating incorporeity (strict physical damage immunity in incorporeal states vs. physical vulnerability window during casting/detonation and 1.5s recovery; magical damage always effective), static 1.5-radius telegraph area attack with wall occlusion, reactive spectral displacement with 4s cooldown, damage-accumulating Hate List, Catacombs encounter integration, and exclusive alchemy loot (Spectral Essence and Soul Fragment).

**Baseline:** `New-Testing` (`3a933962`), branch `feat/us-78-catacombs-specter-enemy`.

**Sources:**
- HacknPlan `US-78` (`#719`): *Espectro de Catacumbas: Tercer Enemigo del MVP (IA, Incorporeidad y Combate Mágico)*
  - `TASK-272` (`#720`): *Entidad autoritativa, FSM e Incorporeidad alternante del Espectro*
  - `TASK-273` (`#721`): *Ataque mágico de área con telegraph fijo y oclusión del Espectro*
  - `TASK-274` (`#722`): *Desplazamiento espectral reactivo y sistema de agresión (Hate List)*
  - `TASK-275` (`#723`): *Spawning en Catacumbas, loot tables y colapso de Dungeon del Espectro*
- Design Docs: Espectro de Catacumbas §§1–12, Combat Design (Physical/Magical damage, mitigation, CC), Doc 10 (Session Rules, Leash, Collapse), Doc 14 (Monsters Loot Tables).
- Architecture: `Docs/Architecture/EnemyFSMArchitecture.md`, `PlayerCombatArchitecture.md`.

**Acceptance Criteria (quoted from `#719`):**
- La entidad del Espectro ejecuta su ciclo completo de estados bajo State Authority y sincroniza su estado visual en clientes.
- La incorporeidad otorga inmunidad estricta al daño físico durante reposo, persecución y retorno, pero vuelve al enemigo vulnerable a daño físico durante canalización, detonación y los 1.5s de recuperación.
- El daño mágico afecta al Espectro en todos sus estados visibles; las armas híbridas resuelven por separado sus componentes físico y mágico.
- El ataque mágico fija un círculo de 1.5 unidades que no sigue al jugador y cuyo daño es bloqueado por muros sólidos intermedios.
- El desplazamiento espectral se ejecuta tras cada ataque y puede dispararse reactivamente por daño mágico no letal respetando un cooldown interno de 4s.
- El sistema de agresión selecciona al objetivo con mayor daño acumulado tras cada desplazamiento espectral, desempatando por cercanía.
- La vista frontal en pixel art es revisada y validada por Game Design antes de avanzar a la producción completa de assets.

**Exclusions:**
- Animaciones finales en 6 direcciones, sprite sheets completos y VFX definitivos (arte en tareas posteriores).
- SFX originales orquestales/definitivos (se emplean placeholders o sintetizados en código para MVP).
- Variantes de élite o mecánicas de jefe.

**Work Breakdown:**
- [x] Phase 1: Specter Domain Models & Pure Incorporeity Rules (`TASK-272`).
- [x] Phase 2: Hate List & Annular Teleport Destination Search Algorithms (`TASK-274`).
- [x] Phase 3: Static Area Attack, Telegraph & Wall Occlusion Linecast Rules (`TASK-273`).
- [x] Phase 4: Authoritative Network FSM Controller & EnemyCharacter Integration (`SpecterAIController` & `EnemyCharacter`) (`TASK-272`, `TASK-274`).
- [x] Phase 5: Client Presentation & Visual Synchronization (`SpecterVisualPresenter`, Alpha & Telegraph Gizmos).
- [x] Phase 6: Loot Tables (`SpecterLootContainerContentTable`) & Spawning Integration in Catacombs (`TASK-275`).
- [x] Phase 7: Comprehensive EditMode test suite and compilation verification.

**Validation & Status:**
- Compilation: Passed (`dotnet build` on `Assembly-CSharp.csproj` with 0 Errors, 0 Warnings on Specter scripts).
- Prefab Variant & Serialization: `NetworkEnemySpecter.prefab` authored as a proper Prefab Variant of `Assets/Prefabs/Enemies/NetworkEnemy.prefab` (inherits `DamageHitbox`, `LootInteraction`, `DamageParticles`, `DamageResolver`, `EntityVisibilityPresenter`, `SortingGroup`, `NetworkObject`, `NetworkTransform`), with melee/ranged AI disabled and `SpecterAIController` + `SpecterVisualPresenter` wired. Registered in `Systems.prefab` (`GUID: 35da693761f9d7244bac33c1559a3918`) and indexed in Fusion Prefab Table (`[Index:13]`).
- Physics & Targeting: Detection and attack target lookup configured to layer 7 (`Character`, mask 128) using `GetComponentInParent<PlayerCharacter>()` and `EntityRegistry` to ensure reliable player detection from child hitbox colliders.
- Unit Tests: All 27 test cases in `SpecterRulesAndCombatTests.cs` passed in Unity Editor (covering state categorization, physical immunity, magical vulnerability in visible states, reactive displacement triggers/cooldowns, Hate List damage accumulation and proximity tiebreaking, annular teleport constraints, wall occlusion linecasts, and `SpecterLootContainerContentTable` snapshot validation).
- Work Items: US-78 (#719), TASK-272 (#720), TASK-273 (#721), TASK-274 (#722), TASK-275 (#723).
