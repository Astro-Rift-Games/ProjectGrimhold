# TASK-289 — Implementar pesos de aparición (Spawn Weight), filtrado por mazmorra y presupuesto de amenaza en Spawner de Dungeon

**Objective:**
Reemplazar la selección aleatoria uniforme del Spawner de enemigos de mazmorra por un sistema determinista con soporte para pesos de probabilidad ponderada (Spawn Weight), filtrado por zona/entorno de dungeon y consumo de presupuesto de amenaza (Threat Cost), garantizando que el Espectro aparezca exclusivamente en Catacumbas y con menor frecuencia que Esqueletos y Slimes.

**Baseline:** `New-Testing`, branch `feat/task-289-enemy-spawn-weights-threat-budget`.

**Sources:**
- HacknPlan: `TASK-289` (`#796`) (Sprint 17).
- Design Docs: Espectro de Catacumbas (§8 Integración con Dungeon y recompensas), 10 — Reglas de Sesión de Dungeon (§9 Sistema de encuentros por sala, §9.2 Estados del Encounter, §10 Leash, activación por proximidad y población, §10.3 Presupuestos globales), Project_Grimhold_Contratos_Base_v1.md.

**Acceptance Criteria (quoted from `#796`):**
- [x] En sesiones de Catacumbas, el Espectro aparece con una frecuencia proporcionalmente menor a la de Slimes y Esqueletos según los pesos asignados.
- [x] En mazmorras o zonas no pertenecientes a Catacumbas, el Espectro nunca es seleccionado por el Spawner.
- [x] Los generadores de encuentros no sobrepasan el presupuesto de amenaza asignado al spawnear grupos mixtos que incluyan al Espectro.

**Work Breakdown:**
- [x] Phase 1: Test-Driven Development (TDD) — Escribir tests unitarios que definan el comportamiento esperado (RED)
  - `WeightedEnemySelectorTests.cs`:
    - Test de distribución estadística en 10.000 iteraciones con semilla fija (`System.Random(42)`): Slime ~45%, Skeleton ~45%, Specter ~10%.
    - Test de exclusión estricta de entorno: el Espectro tiene 0 apariciones cuando el entorno no es Catacumbas (probado en forest, town, graveyard, null, empty).
    - Test de respeto estricto de Threat Budget: si el presupuesto restante es 1 y el Espectro cuesta 2, el selector solo elige candidatos que encajen en el presupuesto. Si el presupuesto es 0, no se generan entidades.
- [x] Phase 2: Arquitectura y Modelado de Selección Ponderada
  - Definición de configuración / reglas de spawn por arquetipo:
    - `EnemySpawnCandidate<T>`: Estructura inmutable con `Item`, `EnemyId`, `Weight`, `ThreatCost`, `RequiredEnvironment`.
    - `EnemySpawnMetadata`: Componente para inspección y autoría en Unity Inspector.
    - `DungeonEnvironmentResolver`: Resolución canónica de entorno de mazmorra según escena o contexto.
    - `EnemySpawnCandidateResolver`: Resolución automática e inferencia de arquetipos desde prefabs (`NetworkEnemySpecter`, metadatos, etc.).
    - `WeightedEnemySelector`: Motor estático determinista de selección ponderada con filtrado ambiental y límite de presupuesto de amenaza.
- [x] Phase 3: Integración en Spawning y Presupuesto de Amenaza
  - Actualizado `EnemyThreatCost.cs` para resolver el costo según arquetipo (Espectro = 2, default = 1).
  - Integrado `WeightedEnemySelector` en `NetworkSpawnManager.cs` (`SpawnEnemyAtTransform`, `SpawnEnemy`, `TrySpawnReinforcement`).
  - Integrado soporte para `ThreatBudget` en `SpawnGroupDefinition.cs` y consumo acumulativo en el bucle de bootstrap de salas/encuentros.
  - Actualizado `EnemyReinforcementDirector.cs` para respetar y consumir el presupuesto de amenaza restante en cada oleada de refuerzos.
- [x] Phase 4: Validación y Verificación
  - Creados tests de integración `EnemySpawnIntegrationTests.cs`.
  - Compilación limpia de la solución completa (`Assembly-CSharp` y `Assembly-CSharp-Editor`: 0 errores).
  - Ejecución verificada en Unity Editor:
    - `WeightedEnemySelectorTests`: 14/14 PASS.
    - `EnemySpawnIntegrationTests`: 7/7 PASS.
    - `ReinforcementSpawnPlannerTests`: 10/10 PASS.
    - `SpecterRulesAndCombatTests`: 15/15 PASS.
