# Plan de implementación — US-49 (#454) Presión PvE dinámica de la Dungeon

Branch objetivo: `New-Testing` · Board: MVP / Sprint 12 · Tareas cubiertas: #455, #456, #457, #458, #459, #460, #461, #462

---

## 0. Cómo usar este plan (leer primero, Antigravity)

### Reglas de ejecución (obligatorias)

1. **Una sola etapa por iteración.** Al terminar una etapa: presentar el walkthrough y **detenerse**. Está prohibido empezar la etapa siguiente sin aprobación explícita del usuario.
2. Antes de tocar código: leer `AGENTS.md` (raíz) y `Project Grimhold/AGENTS.md`, la Architecture relevante en `Project Grimhold/Docs/Architecture/`, y el skill de commit del repo (`skills/grimhold-commit-message/SKILL.md`) para el formato del mensaje.
3. **Antigravity solo modifica código y documentación.** No edita escenas, prefabs ni assets serializados. Todo cambio de escena/prefab/ScriptableObject-asset se lista en el walkthrough como instrucción manual para el usuario, con nombres exactos de GameObjects, componentes y campos.
4. Verificar la versión real de Photon Fusion instalada antes de usar APIs sensibles a versión (ver "Puntos a verificar", abajo).
5. No refactorizar nada ajeno a la US. No crear worktrees. No crear tracking duplicado (HacknPlan es la fuente de verdad del scope).
6. No declarar validación de escena/Play Mode/runtime que no se haya observado. Si no se pudo correr algo, decirlo.

### Plantilla de walkthrough (al cerrar cada etapa)

```
## Walkthrough — Etapa N: <nombre>
### Qué cambió (breve, por archivo)
### Decisiones y desvíos respecto al plan (si hubo)
### Configuración necesaria en Unity (manual)
   - Paso a paso, con paths, nombres de objetos y campos del Inspector
### Validación manual
   - Pasos, resultado esperado, logs esperados
### Tests
   - Qué tests se agregaron y cómo correrlos (EditMode)
### Qué NO se hizo (queda para etapas siguientes)
### Información para el commit
   - Tipo/scope, título, cuerpo, archivos incluidos, HacknPlan: #NNN
```

---

## 1. Decisiones cerradas con el usuario

| Tema | Decisión |
|---|---|
| Timer en Collapse | Queda en **0** (no se congela en otro valor). |
| Inicio del timer | Al entrar `NetworkMatchController.Phase` a `InProgress` (tras `TryStartRaid` + bootstrap exitoso). |
| Owner del sistema | `NetworkBehaviour` nuevo, **no** `NetworkSpawnManager`. |
| Puntos de refuerzo | **Grupo nuevo** en `SpawnGroupDefinition` (no reutiliza el grupo `Enemies`). |
| Prefabs de refuerzo | Los mismos de `_enemyPrefabs` por ahora. |
| Balance | ScriptableObject de configuración por fase. Sin valores hardcodeados en lógica. |
| Costo por enemigo | 1 enemigo = 1 por ahora, pero en **un único punto** reemplazable (Threat Cost futuro). |
| Sincronización del timer | Sincronizado entre peers; la solución técnica es libre. |
| Debug | Overlay **solo Unity Editor**; no debe compilar en la build final. |
| Tests | Lógica pura en C# sin Fusion → EditMode. |
| Cambios de escena | Los hace el usuario a mano siguiendo el walkthrough. |

## 2. Puntos a verificar en la Etapa 1 (discrepancias detectadas)

1. **Cómo se crea `NetworkMatchController`.** El usuario indica que se instancia al cargar la escena de la Raid. En el código (`FusionSessionLauncher`, ~línea 290) el Host hace `_runner.Spawn(_matchControllerPrefab, flags: NetworkSpawnFlags.DontDestroyOnLoad)`. Confirmar el flujo real y cuál de los dos aplica. Esto define dónde vive el controller nuevo (ver §4).
2. **Versión de Fusion.** Se mencionó Fusion 2, pero `Assets/Photon/Fusion/package.json` reporta `"version": "1.1.0"`. Leer la versión real (archivos del repo, no memoria) y usar solo APIs verificadas para esa versión (`TickTimer`, `NetworkBehaviour`, `Runner.Spawn`, resimulación).
3. **Documento de Game Design "10 - Reglas de Sesión de Dungeon"** (Drive, vía `grimhold-docs`). El plan se armó sin poder leer su contenido (solo el título/descripción). En Etapa 1 leerlo completo y reconciliarlo con la US (duración total de expedición, encuentros, cierre de instancia). Si contradice este plan: reportar el conflicto antes de crear contratos, no resolver por cuenta propia.
4. **Duración total de la expedición**: no confirmada. Es parámetro de configuración. El default del asset es provisorio y debe marcarse así.

## 3. Hallazgos del estado actual (base del plan)

- `NetworkSpawnManager` (3.840 líneas) hoy: ejecuta el bootstrap único (`TryExecuteInitialRaidBootstrap`, solo `FreshSession`), spawnea enemigos (`SpawnEnemy`: punto random de `_spawnPointLookup[Enemies]`, marca `_usedEnemySpawnPoints`, `runner.Spawn` con `InitializePatrolRoute` en `onBeforeSpawned`, guarda en `_spawnedEnemies`), y limpia todo en `TryCleanupRaidWorldForResults`. **No distingue bootstrap de refuerzo ni lleva población.**
- `EnemySpawnPoint` solo aporta `PatrolRoute` (se puede reutilizar para puntos de refuerzo).
- `SpawnGroupType` termina en `Breakables`. `InitialSpawnGroupPolicy.Resolve` devuelve `Unsupported` para grupos nuevos y el bootstrap loguea warning y los saltea.
- `NetworkMatchController`: `MatchPhase` (`WaitingForPlayers → Starting → InProgress → Closing → Finished`), `RaidGenerationId`, y `Spawned()` **no pisa la fase en `HostMigrationResume`** (patrón a imitar). Ya se suspende durante `IsHostMigrationRecoveryInProgress`.
- **No existe timer de expedición.**
- Patrón de servicios **runner-scoped**: `NetworkRunnerFactory` hace `runnerObject.AddComponent<EntityRegistry>()` y `ExtractionSanctuaryAssignmentService`, ambos con método de limpieza (`ClearForRaidClosure` / `ResetForRaidClosure`) invocado desde `TryCleanupRaidWorldForResults`. Los servicios nuevos siguen este patrón (sin editar prefabs).
- Los enemigos **no se despawnean al morir**: `EnemyCharacter.HandleDeath` los pasa a `Dead` y expone el loot en el mismo objeto. La población activa debe contar **solo enemigos vivos**.
- Host Migration: `HostMigrationSnapshotRestorer` re-spawnea los objetos dinámicos (`runner.Spawn(resumeNO…)` + `CopyStateFrom`, con `NetworkId` nuevos) y restaura los scene objects con `CopyStateFrom`. Lo `[Networked]` vuelve solo; todo lo no-networked hay que reconstruirlo. `HostMigrationRecoveryArchitecture.md` no menciona enemigos.
- `NetworkSpawnManager.TryCleanupRaidWorldForResults` **omite scene objects** (`NetworkTypeId.IsSceneObject`).
- Tests EditMode existentes en `Assets/Tests/EditMode/Editor/Networking/`.

## 4. Arquitectura objetivo

Convenciones: un tipo principal por archivo, `sealed` salvo necesidad, campos serializados `_camelCase`, sin LINQ ni allocs en `FixedUpdateNetwork`, sin buses de eventos globales, respetar la estrategia de namespaces actual (scripts de `Networking/` en namespace global; tipos de spawn en `Spawning`). Verificar si el proyecto usa asmdefs antes de ubicar archivos nuevos; carpeta sugerida: `Assets/Scripts/Gameplay/DungeonPressure/`.

| Componente | Tipo | Responsabilidad |
|---|---|---|
| `DungeonPressurePhase` | enum (byte) | `Normal, Reinforcements, CriticalPressure, Collapse`. |
| `DungeonPressureConfig` | ScriptableObject | Estático: duración total, umbrales por tiempo restante (`Reinforcements=300s`, `CriticalPressure=120s`), y (desde Etapa 6) política de refuerzos por fase. Con `OnValidate`/`Validate`. Nunca se muta en runtime. |
| `DungeonPhaseResolver` | clase estática pura | `Resolve(remainingTicks, timing)` → fase. Monótona, sin Fusion. |
| `DungeonPressureController` | `NetworkBehaviour` | **Única fuente de verdad** del tiempo y la fase. Solo State Authority escribe. |
| `EnemyReinforcementDirector` | `NetworkBehaviour` (mismo `NetworkObject` que el controller) | Decide y ejecuta refuerzos bajo State Authority. |
| `ReinforcementSpawnPlanner` | clase estática pura | Dada política + población + puntos + cooldowns → decisión o razón de rechazo. Testeable en EditMode. |
| `ReinforcementPointRegistry` | `MonoBehaviour` runner-scoped | Puntos válidos por escena/generación. Acceso sin `FindObjectsByType`. |
| `PvePopulationTracker` | `MonoBehaviour` runner-scoped | Población activa (solo vivos), por origen, con costo. No es owner de la IA ni del lifecycle del enemigo. |
| `EnemyPopulationOrigin` | enum (byte) | `Bootstrap`, `Reinforcement`. |
| `EnemyThreatCost` | clase estática | Punto único de costo por enemigo (hoy devuelve 1). |
| `DungeonPressureDebugOverlay` | `MonoBehaviour` | Solo `#if UNITY_EDITOR` (archivo completo). |

### Dónde vive el controller (recomendación, a confirmar en Etapa 1)

**`NetworkObject` de escena en `Gameplay.unity`**, con `DungeonPressureController` y `EnemyReinforcementDirector` como componentes. Razones: coincide con "se instancia al cargar la escena", el restorer ya restaura scene objects con `CopyStateFrom`, y `TryCleanupRaidWorldForResults` ya los omite. Requiere que el usuario agregue el objeto a la escena (instrucción de walkthrough). **Fallback** si la verificación de Etapa 1 lo desaconseja: spawnearlo desde el launcher junto al `NetworkMatchController`.

### Estado

- **Networked (controller):** `RemainingTicks` (int), `Phase` (byte, escrita solo por State Authority, monótona), `State` (`NotStarted / Running / Stopped`).
  Se usa un **contador de ticks decrementado por State Authority** (no un `TickTimer` absoluto): es sincronizado, resimulation-safe y no depende de que la base de ticks se conserve tras una Host Migration. Verificar el comportamiento en Etapa 7.
- **Networked (director):** cuentas regresivas de evaluación e intervalo mínimo (ticks), contador de refuerzos generados.
- **Networked (enemigo):** `EnemyPopulationOrigin` en `EnemyCharacter`, escrito una vez en `onBeforeSpawned`, para poder reconstruir la población tras Host Migration.
- **No networked:** `PvePopulationTracker`, `ReinforcementPointRegistry`, razón del último rechazo (solo debug, solo State Authority).

### Semántica de balance (a confirmar por el usuario, ver §8)

Por fase: `Enabled`, `PopulationBudget` (techo de **amenaza total activa**, bootstrap + refuerzos), `EvaluationIntervalSeconds`, `MinSecondsBetweenSpawns`, `MaxSpawnsPerAttempt`, `MaxActiveEnemies`. Capacidad disponible = `min(PopulationBudget − amenazaActual, MaxActiveEnemies − vivos)`. Reducir el budget nunca elimina enemigos existentes: solo condiciona spawns futuros.

---

## ETAPA 1 — Arquitectura técnica y documentación (#455)

**Objetivo:** cerrar el contrato técnico antes de escribir código. **Sin cambios de código de runtime.**

**Alcance**
1. Verificar los puntos de §2 (creación del match controller, versión de Fusion, Game Design).
2. Leer Architecture relacionada: `RaidGenerationLifecycleArchitecture`, `HostMigrationRecoveryArchitecture`, `EnemyFSMArchitecture`, `SessionLifecycleAndSpawning`, `MissionSystemArchitecture` (por integración con kills), `RaidDefeatAndSpectatorArchitecture`.
3. Crear `Docs/Architecture/DungeonPressureArchitecture.md` cubriendo: owner del timer, representación de fases, owner del sistema de refuerzos, separación bootstrap vs. spawn dinámico vs. fuente de verdad de población vs. config estática vs. estado runtime, interacción con `NetworkMatchController`, integración con el lifecycle de la Raid, requisitos bajo Host Migration, limpieza al cerrar la generación, y el rol acotado de `NetworkSpawnManager` (expone spawn de enemigo en un punto dado; **no** es owner de la presión).
4. Actualizar la documentación existente que quede desalineada (mínimo: referencias cruzadas en `EnemyFSMArchitecture.md` y `HostMigrationRecoveryArchitecture.md` se completan en Etapa 7).
5. Documentar los contratos de cada etapa siguiente: archivos esperados, dependencias, exclusiones.

**Fuera de alcance:** cualquier código de runtime, assets o escenas.

**Criterios (#455):** una única fuente de verdad de fase; bootstrap y refuerzos con responsabilidades separadas; población con owner definido; `NetworkSpawnManager` no es owner de la presión; se documenta qué se restaura/reconstruye tras Host Migration; State Authority como único escritor; contratos documentados antes de implementar.

**Configuración Unity:** ninguna.
**Validación manual:** revisión del documento por el usuario.
**Commit sugerido:** `docs(architecture): define dungeon pressure and PvE reinforcement architecture` — `HacknPlan #455`.

---

## ETAPA 2 — Temporizador autoritativo y fases (#456)

**Objetivo:** timer de expedición sincronizado y fase actual autoritativa.

**Alcance**
1. `DungeonPressurePhase`, `DungeonPressureConfig` (solo bloque de timing), `DungeonPhaseResolver`.
2. `DungeonPressureController`:
   - Inicia cuando `NetworkMatchController.Phase == InProgress`, **una sola vez por generación** (guarda por `State`, no por evento).
   - Solo State Authority decrementa `RemainingTicks` y avanza `Phase`; transiciones en orden `Normal → Reinforcements → CriticalPressure → Collapse`, sin retroceder ni saltear por resimulación/callbacks duplicados.
   - `WaitingForPlayers` y `Starting` no consumen tiempo. `Closing` y `Finished` lo detienen (`State = Stopped`).
   - En `Collapse`, `RemainingTicks` queda en 0 y el timer se detiene; la fase permanece hasta el cierre.
   - Expone lectura para otros sistemas (fase actual, tiempo restante en segundos, `IsRunning`), sin eventos globales.
   - `Spawned()` restore-safe: no reinicializa si es `HostMigrationResume` (misma lógica que `NetworkMatchController`).
   - Pausa mientras `IsHostMigrationRecoveryInProgress` (a confirmar, §8).
3. Baseline configurable: umbrales `Reinforcements = 300s` y `CriticalPressure = 120s` restantes; Collapse al llegar a 0. Duración total: parámetro (default provisorio marcado). Validación: `total > reinforcementsThreshold > criticalThreshold > 0`.
4. Tests EditMode (`Assets/Tests/EditMode/Editor/Networking/`): `DungeonPhaseResolver` (bordes exactos 300/120/0, valores fuera de rango, monotonía), validación de config, reglas de arranque/parada de la lógica pura extraída (una sola inicialización, no consumo fuera de `InProgress`, parada en `Closing/Finished`).

**Fuera de alcance:** refuerzos, población, HUD, debug, lógica de Host Migration más allá de que `Spawned()` no pise estado.

**Configuración Unity (manual, a indicar en walkthrough)**
- Crear el asset `DungeonPressureConfig` (menú `Create`, ruta indicada) con los valores baseline.
- En `Gameplay.unity`: crear el GameObject con `NetworkObject` + `DungeonPressureController` (con referencia serializada al config); registrar el prefab/objeto en la tabla de Fusion si el flujo lo requiere.

**Validación manual:** Host + 1 Client (editor + build o dos editores). Entrar a la Raid, verificar que el timer no corre en Waiting/Starting, corre en InProgress, ambos peers muestran la misma fase (logs), transiciones en orden, Collapse deja el timer en 0, cerrar la Raid lo detiene.

**Criterios (#456):** una sola vez por generación · todos los peers ven la misma fase · orden previsto · tiempos configurables sin código · Waiting/Starting no consumen · Closing/Finished detienen · nueva Raid arranca limpia · tests de transición.
**Commit sugerido:** `feat(dungeon-pressure): add authoritative expedition timer and phases` — `HacknPlan #456`.

---

## ETAPA 3 — Puntos de refuerzo (#457)

**Objetivo:** configuración explícita de puntos de spawn de refuerzo, separada del bootstrap.

**Alcance**
1. Agregar `Reinforcements` **al final** de `SpawnGroupType` (no reordenar valores existentes, para no romper la serialización de escenas).
2. `InitialSpawnGroupPolicy`: nuevo `SpawnKind.ReinforcementPoints`; el bootstrap lo **ignora sin warning** (no spawnea desde ese grupo).
3. `ReinforcementPointRegistry` (runner-scoped, agregado en `NetworkRunnerFactory` como los demás servicios):
   - Se construye desde `NetworkSpawnSceneConfiguration` en el mismo punto donde `NetworkSpawnManager` arma `_spawnPointLookup` (incluida la ruta de resume tras Host Migration).
   - Valida: referencias nulas, duplicados dentro del grupo, y un mismo `Transform` presente a la vez en `Enemies` y `Reinforcements`. Un grupo inválido produce **diagnóstico claro** (log con nombre de escena/grupo/índice) y no puntos parciales silenciosos.
   - Consulta de puntos válidos sin `FindObjectsByType` y sin búsquedas globales en gameplay.
   - Soporta `EnemySpawnPoint`/`EnemyPatrolRoute` en el mismo `Transform`.
   - `ResetForRaidClosure()` invocado desde `TryCleanupRaidWorldForResults` y en los puntos donde se limpia el estado por generación.
4. Extender `NetworkSpawnSceneConfiguration.Validate` si corresponde a la política `Required/NotRequired`.
5. Tests EditMode: validación de grupos (nulos, duplicados, cruce con `Enemies`), política de bootstrap ignora `Reinforcements`, reset limpia.

**Fuera de alcance:** spawnear refuerzos, elegir puntos por criterio, población.

**Configuración Unity (manual):** en `Gameplay.unity`, en el `NetworkSpawnSceneConfiguration`, agregar un `SpawnGroupDefinition` con `Group = Reinforcements` y asignar los puntos (Transforms nuevos, distintos de los de bootstrap). Opcional: agregar `EnemySpawnPoint` con `PatrolRoute` a algunos.

**Validación manual:** entrar a la Raid con puntos válidos (log de registro correcto y **ningún** enemigo extra spawneado), con un grupo con referencia nula/duplicada (diagnóstico claro), y confirmar que el bootstrap de enemigos sigue idéntico.

**Criterios (#457):** configurable desde escena · no se confunden con bootstrap · config inválida = diagnóstico · consulta sin `FindObjectsByType` en simulación · un punto conserva su `EnemyPatrolRoute` · referencias pertenecen a la escena/generación · limpieza al cerrar.
**Commit sugerido:** `feat(spawning): add explicit PvE reinforcement point group and registry` — `HacknPlan #457`.

---

## ETAPA 4 — Seguimiento autoritativo de población PvE (#458)

**Objetivo:** fuente de verdad runner-scoped de la población activa.

**Alcance**
1. `EnemyPopulationOrigin`, `EnemyThreatCost` (hoy `1`; único lugar a cambiar para Threat Cost futuro).
2. `PvePopulationTracker` (runner-scoped, State Authority):
   - Registra generado en bootstrap, generado como refuerzo, derrotado y despawneado. Idempotente: **cada enemigo activo cuenta una sola vez** (clave por `NetworkId`), sin doble contabilización por resimulación o llamadas repetidas.
   - Consultas: población total activa, activa por origen, capacidad disponible dentro de un budget, con costo por enemigo guardado al registrar.
   - Un enemigo derrotado o despawneado deja de consumir población; bootstrap y refuerzos comparten el mismo límite global.
   - No depende de búsquedas globales periódicas. No toca la IA ni el lifecycle del enemigo.
   - `ResetForRaidClosure()` al cerrar la generación.
3. Integración mínima con enemigos: `EnemyCharacter` registra en `Spawned()` (solo State Authority y solo si está vivo), notifica derrota desde `HandleDeath`, y desregistra en `Despawned()` (override nuevo, idempotente). `[Networked] EnemyPopulationOrigin` escrito una vez en `onBeforeSpawned`.
4. `NetworkSpawnManager`: dividir `SpawnEnemy` en (a) selección de punto y (b) spawn en un punto dado (`internal`), sin cambiar el comportamiento del bootstrap, pasando `Origin = Bootstrap`. Sigue agregando a `_spawnedEnemies` para el cleanup de resultados.
5. Diseñar el registro para que sea **reconstruible** tras Host Migration (depende solo de enemigos vivos en escena + origen networked). La reconstrucción efectiva se valida en Etapa 7.
6. Tests EditMode (sobre la lógica pura del tracker): altas, bajas, derrotas, doble registro, capacidad contra budget, reset, costo por enemigo.

**Fuera de alcance:** generar refuerzos, políticas por fase, lógica de Host Migration.

**Configuración Unity (manual):** al agregar un `[Networked]` a `EnemyCharacter`, indicar reimportar/rebakear prefabs de enemigos según requiera la versión de Fusion (p. ej. regenerar la tabla de prefabs de Fusion) y confirmar que no hay cambios serializados no deseados.

**Validación manual:** Host + Client, iniciar Raid, verificar por log/inspección que la población inicial = enemigos del bootstrap; matar un enemigo y verificar que baja; cerrar la Raid y verificar el reset.

**Criterios (#458):** una sola vez por enemigo · derrotado/despawneado no consume · bootstrap y refuerzos comparten límite · sin búsquedas globales · limpieza al finalizar · reconstruible tras Host Migration · tests de altas/bajas/doble conteo.
**Commit sugerido:** `feat(dungeon-pressure): add authoritative PvE population tracking` — `HacknPlan #458`.

---

## ETAPA 5 — Enemy Reinforcement Director (#459)

**Objetivo:** generar refuerzos bajo State Authority respetando población y puntos.

**Alcance**
1. `ReinforcementPolicy` (struct/clase de datos): `Enabled`, `PopulationBudget`, `EvaluationIntervalSeconds`, `MinSecondsBetweenSpawns`, `MaxSpawnsPerAttempt`, `MaxActiveEnemies`.
2. `ReinforcementSpawnPlanner` (puro): entrada = política, capacidad del tracker, cantidad de puntos válidos, cooldowns; salida = cantidad a generar o `ReinforcementRejection` (`Disabled`, `PhaseNotActive`, `BudgetExhausted`, `PopulationLimit`, `NoValidPoint`, `IntervalNotElapsed`, `RecoveryInProgress`, `RaidNotActive`, `NoPrefab`, `SpawnFailed`).
3. `EnemyReinforcementDirector`:
   - Corre solo con State Authority y solo con `MatchPhase.InProgress`; no opera fuera de una Raid activa ni durante recuperación de Host Migration.
   - Cadencia por cuentas regresivas networked (evaluación y mínimo entre generaciones); **un único intento por evaluación**: no genera múltiples oleadas en el mismo tick/intento.
   - Selecciona punto de `ReinforcementPointRegistry` y prefab entre los disponibles; spawnea mediante el método `internal` de `NetworkSpawnManager` (Origin = `Reinforcement`), lo que registra al enemigo en el tracker.
   - Nunca supera el límite de población configurado; no spawnea sin punto válido; no spawnea con refuerzos deshabilitados.
   - No decide valores definitivos de Balance.
   - Sin ejecuciones duplicadas en Host/Client (guard de `HasStateAuthority`).
4. **Política provisoria:** en esta etapa el Director lee una `ReinforcementPolicy` serializada en su Inspector (dato, no hardcode) para poder validar antes de la Etapa 6. La Etapa 6 **elimina ese campo** y lo reemplaza por políticas por fase (esto está en alcance y se documenta como cambio de serialización).
5. Tests EditMode del planner: deshabilitado, budget agotado, límite de población, sin punto, intervalo no cumplido, máximo por intento, una sola tanda por intento, capacidad parcial.

**Fuera de alcance:** vincular con fases, debug tools, Host Migration.

**Configuración Unity (manual):** agregar `EnemyReinforcementDirector` al mismo GameObject del controller; configurar la política provisoria (habilitada solo para pruebas) y asegurar que los puntos de la Etapa 3 y los prefabs de enemigo estén asignados.

**Validación manual:** con política habilitada: los refuerzos aparecen en puntos de refuerzo, no superan budget ni máximo activo, respetan intervalo mínimo; con política deshabilitada no aparece nada; con grupo de puntos vacío no aparece nada y hay log claro; Host/Client sin duplicados.

**Criterios (#459):** todos los de la tarea (deshabilitado, límite, sin punto, sin múltiples oleadas, tracking, detención fuera de InProgress, parámetros sin recompilar, Host/Client sin duplicados).
**Commit sugerido:** `feat(dungeon-pressure): add enemy reinforcement director` — `HacknPlan #459`.

---

## ETAPA 6 — Fases integradas con presupuestos (#460)

**Objetivo:** cada fase define su política; el cambio de fase actualiza lo que usa el Director.

**Alcance**
1. Extender `DungeonPressureConfig` con una política por fase (`Normal`, `Reinforcements`, `CriticalPressure`, `Collapse`), y su validación: `Normal` con refuerzos deshabilitados (forzado por validación y por lógica); `Collapse` usa la configuración máxima definida para el MVP; ninguna fase excede los límites máximos configurados; todos los valores vienen del asset.
2. Director: reemplazar la política provisoria por `config.GetPolicy(controller.Phase)`. Al cambiar de fase se actualiza la política usada para **futuros** spawns; se re-evalúa la cadencia sin duplicar intentos.
3. Reglas de transición: el cambio de fase **no destruye** enemigos activos; reducir un budget **no elimina** existentes y solo condiciona spawns futuros.
4. Verificar que no queden valores de Balance hardcodeados en lógica (revisión explícita en el walkthrough).
5. Tests EditMode: política por fase, `Normal` nunca genera, cambio a política más restrictiva no reduce población, `Collapse` toma la config máxima, validación de límites, ausencia de fallback numérico hardcodeado.

**Fuera de alcance:** debug tools, Host Migration.

**Configuración Unity (manual):** completar las políticas de las 4 fases en el asset `DungeonPressureConfig` (valores de Balance provisorios, marcados como tales) y **reconfigurar/limpiar** el campo de política provisoria retirado del Director.

**Validación manual:** recorrer la expedición (con duraciones cortas configuradas para la prueba) y observar por log el cambio de política en cada transición, cero generación en `Normal`, generación en las demás fases, ningún enemigo destruido al cambiar de fase.

**Criterios (#460):** todos los de la tarea.
**Commit sugerido:** `feat(dungeon-pressure): drive reinforcement policy from dungeon phases` — `HacknPlan #460`.

---

## ETAPA 7 — Host Migration y lifecycle de Raid (#461)

**Objetivo:** estado coherente ante Host Migration y cierre de generación.

**Alcance** (leer antes `HostMigrationRecoveryArchitecture.md`)
1. **Timer y fase:** confirmar que `RemainingTicks`, `Phase`, `State` y las cuentas regresivas del Director se restauran con el snapshot (scene object + `CopyStateFrom`). Verificar explícitamente que el tiempo no se reinicia, que la fase no vuelve a `Normal` y que el timer no depende de la base de ticks anterior. Si se detecta un problema con la solución elegida, ajustarla y documentar la desviación.
2. **`Spawned()` restore-safe** en controller y Director: en `HostMigrationResume` no se inicializa estado fresco.
3. **Enemigos:** los vivos vuelven por el snapshot; `PvePopulationTracker` se **reconstruye** a partir de `EnemyCharacter.Spawned()` + origen networked; los enemigos restaurados no se duplican y la población restaurada se contabiliza correctamente.
4. **Bootstrap:** confirmar que no vuelve a ejecutarse (`_initialRaidBootstrapState` coherente en resume). **No** se genera oleada adicional por el hecho de restaurar el Host.
5. **Director:** no evalúa ni spawnea mientras dure `IsHostMigrationRecoveryInProgress` ni antes de completar la reconstrucción de población; luego continúa desde un estado temporal válido.
6. **Puntos de refuerzo:** confirmar que `ReinforcementPointRegistry` se reconstruye en la ruta de resume.
7. **Lifecycle:** `Closing` y `Finished` bloquean nuevos refuerzos y detienen el timer; el cleanup elimina todo estado runner-scoped de la generación terminada (`ResetForRaidClosure` en tracker y registry) y una nueva Raid arranca limpia.
8. **Documentación:** actualizar `HostMigrationRecoveryArchitecture.md` (qué se restaura y qué se reconstruye para presión PvE) y `DungeonPressureArchitecture.md` si hubo desvíos.
9. Tests EditMode de la lógica de reconstrucción y de reset; **el flujo Host → Client → Host Migration con refuerzos activos se valida manualmente** (documentar el procedimiento exacto y qué logs esperar).

**Configuración Unity (manual):** ninguna adicional, salvo lo que surja de la verificación.

**Validación manual (crítica):** Host + Client con refuerzos activos y enemigos vivos → matar abruptamente el Host → verificar: mismo tiempo restante (± tolerancia), misma fase, mismos enemigos sin duplicados, población coherente, sin oleada extra, Director continúa; luego cerrar la Raid y verificar cleanup y una nueva Raid limpia.

**Criterios (#461):** todos los de la tarea, incluido "al menos un flujo Host → Client → Host Migration con refuerzos activos".
**Commit sugerido:** `feat(dungeon-pressure): integrate pressure system with host migration and raid lifecycle` — `HacknPlan #461`.

---

## ETAPA 8 — Herramientas de debug (#462)

**Objetivo:** validar rápido sin esperar la duración completa. **Solo Editor.**

**Alcance**
1. `DungeonPressureDebugOverlay`: **archivo completo envuelto en `#if UNITY_EDITOR`**, así no llega a ninguna build. Muestra: fase actual, tiempo restante, población PvE activa, budget actual, capacidad disponible, estado del Director, próximo intento de refuerzo, cantidad de refuerzos generados, y motivo del último rechazo. Lee siempre de la fuente de verdad autoritativa (controller, Director, tracker).
2. Acciones (solo en el Host/State Authority del Editor): avanzar manualmente entre fases, acelerar el temporizador, forzar un intento de refuerzo, inspeccionar por qué se rechazó un intento (budget, población o falta de puntos).
   - **Sin RPCs de producción.** Los métodos mutadores del controller/Director son `internal` y van dentro de `#if UNITY_EDITOR`; los peers sin State Authority ven solo lectura.
   - "Avanzar de fase" escribe `RemainingTicks` en el umbral siguiente y deja que la **misma lógica productiva** haga la transición; "acelerar" aplica un multiplicador al decremento (campo editor-only); "forzar intento" ejecuta el **mismo pipeline** del Director ignorando solo las cuentas regresivas.
3. No modifica el comportamiento de builds productivas; no forma parte del HUD productivo; no introduce lógica requerida por el sistema productivo.
4. Verificar por compilación que una build de Player (o el análisis de símbolos `UNITY_EDITOR`) no incluye el overlay ni los mutadores. Cuidado con el weaver de Fusion: no envolver miembros `[Networked]` ni RPCs en `#if`.
5. Tests: no requeridos para el overlay; agregar tests EditMode de la lógica pura tocada (p. ej. cálculo del umbral siguiente).

**Configuración Unity (manual):** agregar el `DungeonPressureDebugOverlay` al GameObject de presión (o a uno de debug en la escena) y confirmar en el walkthrough cómo abrirlo/cerrarlo (tecla indicada por Antigravity).

**Validación manual:** recorrer `Normal → Reinforcements → Critical Pressure → Collapse` sin esperar el baseline; identificar rechazos por budget/población/puntos; comprobar que el estado mostrado coincide con el autoritativo; hacer una build de prueba y confirmar que el overlay no existe.

**Criterios (#462):** todos los de la tarea.
**Commit sugerido:** `feat(dungeon-pressure): add editor-only debug overlay for pressure system` — `HacknPlan #462`.

---

## 5. Trazabilidad US → etapas

| Requisito de la US (#454) | Etapas |
|---|---|
| Temporizador autoritativo de expedición | 2 |
| Fases temporales de la Dungeon (Normal/Reinforcements/Critical/Collapse) | 2, 6 |
| Puntos específicos para generación de refuerzos | 3 |
| Seguimiento de población PvE activa | 4 |
| Generación dinámica de enemigos | 5 |
| Presupuestos y límites configurables por fase | 5, 6 |
| Integración con el lifecycle de Raid | 2, 7 |
| Continuidad tras Host Migration | 7 |
| Operar independiente del Player, reutilizando infraestructura existente | 4, 5 (spawn vía `NetworkSpawnManager`), 1 |
| Documentación en Docs/Architecture | 1, 7 |
| Herramientas de debug | 8 |

## 6. Fuera de alcance de la US (no implementar)

Derrota definitiva a jugadores, HUD del jugador, señales visuales/ambientales del Colapso, balance final de cantidades/frecuencias/Threat Costs, cambios al comportamiento individual de enemigos, nuevos tipos de enemigo, eventos opcionales de expedición.

## 7. Riesgos principales

1. **Ubicación/creación del controller** (scene object vs. spawn del launcher) — resuelto en Etapa 1.
2. **Comportamiento real de tick/timers tras Host Migration** en la versión instalada de Fusion — mitigado con contador de ticks networked y validación manual en Etapa 7.
3. **Añadir `[Networked]` a `EnemyCharacter`** requiere rebakear prefabs; puede generar diffs de assets no deseados — se documenta y se verifica en el walkthrough de Etapa 4.
4. **`NetworkSpawnManager` es muy grande:** los cambios se limitan a extraer el spawn en punto dado, hooks de limpieza y construcción del registry; sin refactor adicional.
5. **Game Design "10 - Reglas de Sesión de Dungeon" no leído al armar el plan:** puede modificar duración, encuentros o cierre; se reconcilia en Etapa 1.

## 8. Decisiones abiertas a confirmar por el usuario (antes o durante Etapa 1)

1. **Semántica de `PopulationBudget`**: propuesta = techo de amenaza total activa (bootstrap + refuerzos). ¿Es correcto o debe ser un presupuesto solo para refuerzos?
2. **Pausa del timer durante la ventana de recuperación de Host Migration** (hasta ~30 s): propuesta = pausado (nadie tiene State Authority). ¿Conforme?
3. **Duración total de la expedición**: valor a definir (default provisorio en el asset).
4. ¿Debe un punto de refuerzo evitar generar enemigos cerca de jugadores (distancia mínima)? La US no lo pide; por defecto **no** se implementa.
