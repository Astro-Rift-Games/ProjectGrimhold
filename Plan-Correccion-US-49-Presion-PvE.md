# Plan de corrección — US-49 (#454) Presión PvE dinámica de la Dungeon

Branch: `New-Testing` (incluye el merge `ef4058a` de `PvE-Pressure`) · Board: MVP / Sprint 12 · Tareas pendientes: #459, #460, #461 y cierre de #455

Este plan **reemplaza** a los anteriores. Parte del código actual, que ya cubre #456, #457, #458 y #462 en lo esencial, y corrige lo que todavía no cumple. Los hallazgos salen de leer el código y los assets; **no se ejecutó Unity ni los tests**, así que nada de lo descripto como "existente" está verificado en runtime.

---

## 0. Reglas de ejecución (Antigravity: leer primero)

1. **Una sola etapa por iteración.** Al terminar: presentar el walkthrough y **detenerse**. Prohibido avanzar sin aprobación explícita del usuario.
2. Antes de tocar código: leer `AGENTS.md` (raíz), `Project Grimhold/AGENTS.md`, `Docs/Architecture/DungeonPressureArchitecture.md` y `skills/grimhold-commit-message/SKILL.md`.
3. **Antigravity solo modifica código y documentación.** No edita escenas, prefabs ni assets serializados (`.unity`, `.prefab`, `.asset`). Todo cambio de ese tipo va en el walkthrough como instrucción manual, con paths y nombres de campos exactos.
4. **No romper lo que ya existe y está aprobado:** HUD (`DungeonPressureHudPresenter/View`), audio (`DungeonPressureAudioPresenter`), `MinDistanceToPlayer`, el overlay de debug (`Debug/DungeonPressureDebugOverlay`) y la API pública `DungeonPressureController.Phase / RemainingSeconds / IsRunning`. Si un cambio rompe la compilación del overlay, se corrige en la misma etapa.
5. Cada etapa **actualiza `DungeonPressureArchitecture.md` en lo que cambia**.
6. Clean Code: sin logs por frame o por N frames en `FixedUpdateNetwork`; logs solo en eventos o cambio de motivo; sin LINQ ni allocs por tick en simulación; sin `FindObjectsByType` en simulación.
7. Verificar la versión real de Fusion instalada (`Assets/Photon/Fusion/package.json`, que reportaba `1.1.0`) antes de usar APIs sensibles a versión.
8. No declarar validado nada que no se haya observado; si algo no pudo correrse, decirlo en el walkthrough.

### Plantilla de walkthrough

```
## Walkthrough — Etapa N: <nombre>
### Qué cambió (breve, por archivo)
### Decisiones y desvíos respecto al plan
### Configuración necesaria en Unity (manual)
### Validación manual (pasos, resultado esperado, logs esperados)
### Tests (qué se agregó, cómo correrlos en EditMode)
### Qué NO se hizo (queda para etapas siguientes)
### Información para el commit (tipo/scope, título, cuerpo, archivos, HacknPlan #NNN)
```

---

## 1. Decisiones cerradas

| Tema | Decisión |
|---|---|
| **Collapse** | **Se mantiene** el comportamiento actual: al entrar en `Collapse`, `EnemyCollapseDespawner` despawnea los enemigos vivos por lotes y luego llama a `StopForcefully()` sobre el controller. Collapse **no genera refuerzos**. |
| **Presupuesto** | `Budget` ("spawns totales por fase") se **reemplaza por `PopulationBudget`**: tope de amenaza activa de **refuerzos**. `MaxConcurrentSpawns` pasa a ser redundante y se elimina (hoy ya funciona como tope de amenaza activa de refuerzos). Sin tope total de spawns por fase: el ritmo lo limitan los intervalos y los topes de población. |
| Tope global | Se mantiene `MaxGlobalEnemies` en `DungeonPressureConfig` (bootstrap + refuerzos). |
| Costo por enemigo | 1 por ahora, único punto: `EnemyThreatCost`. |
| Duración total | **1 minuto (60 s) por ahora**; aumentará más adelante. Los umbrales actuales del asset son 45 s y 20 s restantes (de prueba). Los umbrales baseline de #456 (300 s / 120 s) solo entran cuando la duración total supere 300 s: al subir la duración hay que **reescalar los umbrales a mano**. |
| Timer en Collapse | Queda en 0. |
| HUD, audio, `MinDistanceToPlayer` | Se conservan. |
| Documento de Game Design "10 - Reglas de Sesión de Dungeon" | Sin leer al armar el plan. En la Etapa 1, leerlo con `grimhold-docs` y reportar conflictos con este plan **antes** de cambiar contratos. |

### Desvíos respecto de la redacción original de la US, a registrar en HacknPlan
La implementación de Collapse difiere de dos criterios literales de #460. Dejar el texto sugerido en el walkthrough de la Etapa 4 para que el usuario actualice la tarea:
- "Collapse: utilizar la configuración máxima de presión" → **"Collapse despawnea los enemigos vivos, no genera refuerzos y detiene el sistema de presión."**
- "El cambio de fase no destruye enemigos ya activos" → **"…salvo la transición a Collapse."**

---

## ETAPA 1 — Modelo de política y Director (#459, con #460 en lo que toca)

**Objetivo:** Director que respete `PopulationBudget`, tope global, máximo por intento e intervalos separados, con motivos de rechazo estructurados.

**Alcance**
1. `ReinforcementPolicy` (en `Spawning`): campos nuevos
   - `PopulationBudget` (reemplaza a `Budget` y a `MaxConcurrentSpawns`)
   - `EvaluationIntervalSeconds` (equivale a `SpawnIntervalSeconds`)
   - `MinSecondsBetweenSpawns`
   - `MaxSpawnsPerAttempt`
   - `MinDistanceToPlayer` (**se conserva**)

   Usar `[FormerlySerializedAs]` donde la equivalencia sea clara (`SpawnIntervalSeconds` → `EvaluationIntervalSeconds`, `MaxConcurrentSpawns` → `PopulationBudget`). `Budget` se descarta. `ReinforcementPolicy.None` se actualiza. El walkthrough debe dar una **tabla de equivalencias viejo → nuevo** y avisar que los valores migrados hay que revisarlos.
2. `ReinforcementSpawnPlanner` pasa a tener una parte **pura** `Plan(...)` que recibe la política, la capacidad de budget (`PopulationTracker.GetAvailableCapacity(PopulationBudget, Reinforcement)`), la capacidad global (`GetAvailableGlobalCapacity(MaxGlobalEnemies)`), si el intervalo mínimo ya pasó y si hay puntos válidos, y devuelve `ReinforcementPlan { Count, Rejection }`. La cantidad es `min(MaxSpawnsPerAttempt, capacidadBudget / costo, capacidadGlobal)`.
3. `ReinforcementRejection`: enum **permanente** (no editor-only, sin strings): `None`, `Disabled`, `IntervalNotElapsed`, `PopulationBudgetExhausted`, `GlobalCapReached`, `NoReinforcementPoints`, `AllPointsTooCloseToPlayer`, `SpawnFailed`. Reemplaza los strings actuales de `LastRejectionReason`.
4. Selección de punto: conservar la lógica de distancia mínima a jugadores, pero **sin alocar una `List` por llamada** (buffer reutilizable).
5. `EnemyReinforcementDirector`:
   - Eliminar `_spawnsConsumedThisPhase` y el `float _spawnCooldownTimer`. Reemplazar por dos cuentas regresivas `[Networked]` **en ticks**: evaluación y mínimo entre generaciones.
   - **Una sola evaluación por tick**; cada evaluación genera hasta `Count` enemigos, nunca más. No hay múltiples oleadas por el mismo intento.
   - Al cambiar de fase, reiniciar la cuenta de evaluación con el intervalo de la política nueva (en vez de 0) para **no producir una oleada instantánea** al cambiar de fase. Dejarlo documentado como decisión.
   - `Normal` y `Collapse` se tratan como deshabilitados por lógica (además de por config).
   - Mantener: solo State Authority, solo `InProgress` y `IsRunning`, pausa en `IsHostMigrationRecoveryInProgress`, spawn solo vía `NetworkSpawnManager.TrySpawnReinforcement`, `ShouldInitializeMatchPhase` en `Spawned()`.
   - Exponer en solo lectura el último rechazo y la cantidad de refuerzos generados. Log únicamente cuando cambia el motivo de rechazo y en cada spawn exitoso.
   - Verificar si `EnemyCharacter.Spawned()` (que registra en el tracker) corre **sincrónicamente** dentro de `runner.Spawn` en la versión de Fusion instalada. Si no, el cálculo de capacidad dentro de una misma evaluación no debe depender de ese registro: descontar localmente lo ya generado.
6. `DungeonPressureConfig` usa la política nueva; `GetPolicy` conserva su firma.
7. Actualizar `DungeonPressureDebugOverlay` y los accesores de debug del Director para los nombres y tipos nuevos (rechazo como enum, cuentas en ticks, refuerzos generados).
8. **Tests EditMode del planner:** deshabilitado, intervalo no cumplido, budget agotado, tope global alcanzado, sin puntos, todos los puntos cerca de jugadores, máximo por intento, capacidad parcial, `Count` nunca excede ninguno de los topes.

**Fuera de alcance:** reglas entre fases y validación del config (etapa 2), Host Migration, cambios al despawner, HUD y audio.

**Configuración Unity (manual — importante):** en `DefaultDungeonPressureConfig.asset` hay que revisar/re-ingresar las políticas de las 4 fases con los campos nuevos, siguiendo la tabla de equivalencias. Valores de arranque provisorios marcados como tales por el walkthrough.

**Validación manual:** Host + Client. Con política habilitada, los refuerzos salen de los puntos de refuerzo y nunca superan `PopulationBudget` ni `MaxGlobalEnemies`; respetan intervalo mínimo y máximo por intento; con budget 0 no sale nada; sin puntos válidos, un único log con el motivo. Sin duplicados entre Host y Client. El overlay (F9) sigue funcionando.

**Criterios:** todos los de #459.
**Commit sugerido:** `feat(dungeon-pressure): replace phase spawn budget with population budget and attempt limits` — `HacknPlan #459`.

---

## ETAPA 2 — Validación de políticas entre fases (#460)

**Objetivo:** reglas de balance verificables y config inválido imposible de usar.

**Alcance**
1. `DungeonPressureConfig.Validate` se amplía con:
   - `Normal` con `PopulationBudget = 0` (refuerzos deshabilitados).
   - `Collapse` con `PopulationBudget = 0` (**coherente con la decisión de Collapse**: no genera refuerzos).
   - `CriticalPressure.PopulationBudget ≥ Reinforcements.PopulationBudget`, y que Critical no sea más débil que Reinforcements en frecuencia (`EvaluationIntervalSeconds` menor o igual) ni en cantidad (`MaxSpawnsPerAttempt` mayor o igual).
   - Cada fase con budget > 0 requiere `EvaluationIntervalSeconds > 0` y `MaxSpawnsPerAttempt ≥ 1`.
   - Ninguna fase supera los límites: `PopulationBudget ≤ MaxGlobalEnemies`; `MaxSpawnsPerAttempt ≤ PopulationBudget`.
   - `MinSecondsBetweenSpawns ≥ 0` y `MinDistanceToPlayer ≥ 0`.
   - Mensajes de error con el nombre de la fase y del campo.
2. Revisión explícita de que no haya valores de Balance hardcodeados en lógica (los defaults de `[SerializeField]` del ScriptableObject están permitidos; los literales en código de lógica no). Dejar constancia en el walkthrough, con los lugares revisados.
3. Confirmar por código y test que **cambiar de fase y reducir `PopulationBudget` no elimina enemigos ya activos** (la única excepción es la transición a Collapse, ya implementada).
4. `OnValidate` del config: mantener coherencia con `Validate` sin reescribir silenciosamente valores de balance.
5. **Tests EditMode:** cada regla por separado (aceptar y rechazar), config completo válido, `Normal`/`Collapse` con budget > 0 inválidos, Critical más débil que Reinforcements inválido, límites excedidos, y que reducir budget no cambia la población del tracker.

**Fuera de alcance:** Host Migration, documentación final, cambios al despawner.

**Configuración Unity (manual):** ajustar el asset si `Validate` rechaza los valores migrados (el controller loguea el motivo y no arranca el timer). Mantener `TotalDurationSeconds = 60` con umbrales 45/20 hasta que se defina la duración real.

**Validación manual:** con el asset válido, recorrer las 4 fases y confirmar por log el cambio de política, cero generación en `Normal` y en `Collapse`, y ningún enemigo destruido al pasar de `Normal` a `Reinforcements` o de `Reinforcements` a `CriticalPressure`. Con un valor inválido a propósito: error claro y sin timer.

**Criterios:** todos los de #460, con las dos reformulaciones registradas en §1.
**Commit sugerido:** `feat(dungeon-pressure): validate phase reinforcement policies and collapse constraints` — `HacknPlan #460`.

---

## ETAPA 3 — Host Migration, Collapse y lifecycle (#461)

**Objetivo:** estado coherente tras una Host Migration y cierre limpio de generación, incluido el Collapse.

**Alcance** (leer antes `HostMigrationRecoveryArchitecture.md`)
1. **Controller y Director:** confirmar que `RemainingTicks`, `Phase`, `State` y las cuentas regresivas del Director se restauran con el snapshot (el prefab `NetworkMatchController` es un objeto dinámico restaurado con `CopyStateFrom`). Verificar que el tiempo no se reinicia, la fase no vuelve a `Normal` y el Director continúa desde un estado temporal válido, sin oleada adicional.
2. **Tracker:** se reconstruye desde `EnemyCharacter.Spawned()` de los enemigos restaurados, usando `PopulationOrigin` networked. Verificar que vida y origen ya están copiados cuando corre `Spawned()`. Agregar una **reconciliación única al terminar la recuperación** (sin `FindObjectsByType`, usando el conjunto de objetos restaurados que ya maneja el restorer) que compare el conteo del tracker con los enemigos vivos restaurados y loguee cualquier discrepancia.
3. **Director y Despawner en recuperación:** ninguno actúa mientras dure `IsHostMigrationRecoveryInProgress` ni antes de completar la reconstrucción de población.
4. **Collapse y Host Migration:** caso específico a cubrir. Si la migración ocurre en pleno Collapse, el `EnemyCollapseDespawner` (que guarda cola y flag en memoria) debe reanudar el despawn desde la población reconstruida y terminar deteniendo el controller, sin dejar enemigos vivos ni dejar el sistema colgado en `Running`. Agregar test de la cola para el caso de repoblado a mitad de proceso.
5. **`_spawnedEnemies` y despawns de Collapse:** `EnemyCollapseDespawner` hace `Runner.Despawn` de enemigos que `NetworkSpawnManager` mantiene en `_spawnedEnemies`. Verificar que `TryCleanupRaidWorldForResults` y cualquier recorrido de esa lista toleran objetos ya despawneados (nulos/inválidos) sin errores ni doble despawn; corregir si no.
6. **Bootstrap y registry:** confirmar que el bootstrap no se re-ejecuta, que los enemigos restaurados no se duplican y que `ReinforcementPointRegistry` se reconstruye en la ruta de resume (la configuración de escena debe reaplicarse ahí).
7. **Lifecycle:** `Closing` y `Finished` bloquean refuerzos y detienen el timer; el cleanup resetea tracker y registry; una nueva Raid arranca limpia.
8. **Tests EditMode** de la lógica pura tocada (reconciliación, repoblado de la cola). **El flujo Host → Client → Host Migration con refuerzos activos se valida manualmente**; el walkthrough documenta el procedimiento y los logs esperados.

**Configuración Unity (manual):** ninguna adicional salvo lo que surja de la verificación.

**Validación manual (crítica):**
- Host + Client con refuerzos activos y enemigos vivos → cerrar abruptamente el Host → mismo tiempo restante (± tolerancia), misma fase, mismos enemigos sin duplicar, población coherente, sin oleada extra, Director continúa.
- Repetir **durante el Collapse** (con enemigos aún pendientes de despawn): el despawn se completa y el controller termina en `Stopped`.
- Cerrar la Raid y verificar cleanup y una nueva Raid limpia.

**Criterios:** todos los de #461, incluido al menos un flujo Host → Client → Host Migration con refuerzos activos.
**Commit sugerido:** `feat(dungeon-pressure): validate pressure and collapse recovery across host migration` — `HacknPlan #461`.

---

## ETAPA 4 — Cierre de documentación (#455)

**Objetivo:** que `DungeonPressureArchitecture.md` describa el sistema final.

**Alcance**
1. Revisar el documento completo contra el código final y corregir todo desvío. Como mínimo debe reflejar:
   - `PopulationBudget` como tope de amenaza activa de refuerzos y `MaxGlobalEnemies` como tope global compartido con el bootstrap.
   - Intervalo de evaluación, mínimo entre generaciones y máximo por intento.
   - `ReinforcementRejection` y cómo se expone.
   - **Comportamiento de Collapse:** despawn por lotes de enemigos vivos, sin refuerzos, y detención del controller; los enemigos derrotados con loot no se despawnean (confirmarlo en el código antes de afirmarlo).
   - Tracker desacoplado (clave por `NetworkId`), costo por enemigo y reconstrucción tras Host Migration.
   - Registry con validación y diagnósticos.
   - Overlay de debug solo Editor: tecla F9, acciones y límites (solo Host).
   - Cómo se crea realmente el controller (prefab `NetworkMatchController`) y cómo conviven HUD y audio con la API del controller.
2. Completar la sección de Dungeon Pressure en `HostMigrationRecoveryArchitecture.md` con lo validado en la Etapa 3.
3. Agregar una nota explícita: la duración total (60 s) y los umbrales son provisorios y deben reescalarse juntos.
4. Incluir en el walkthrough el texto sugerido para actualizar #460 en HacknPlan (ver §1).

**Fuera de alcance:** cambios de código.

**Configuración Unity:** ninguna.
**Validación manual:** revisión del documento por el usuario.
**Commit sugerido:** `docs(architecture): align dungeon pressure architecture with final implementation` — `HacknPlan #455`.

---

## 2. Estado de cada tarea tras el plan

| Tarea | Estado actual | Cierre |
|---|---|---|
| #455 | Doc desactualizado | Etapa 4 |
| #456 | Implementada y con tests | — (mantener) |
| #457 | Implementada con validación y tests | — (verificar reconstrucción en Etapa 3) |
| #458 | Implementada con tests | — (reconstrucción tras migración en Etapa 3) |
| #459 | Parcial | Etapa 1 |
| #460 | Parcial | Etapas 1 y 2 |
| #461 | Sin trabajo dedicado | Etapa 3 |
| #462 | Implementada | — (se adapta a cambios en Etapa 1) |

## 3. Fuera de alcance de la US

Derrota definitiva de jugadores, rediseño del HUD o del audio (ya existen y se conservan), señales visuales o ambientales del Colapso más allá de lo implementado, balance final de cantidades/frecuencias/Threat Costs, comportamiento individual de enemigos, nuevos tipos de enemigo, eventos opcionales de expedición.

## 4. Riesgos

1. **Migración de campos serializados** de la política (Etapa 1): requiere revisión manual del asset; mitigada con tabla de equivalencias y `FormerlySerializedAs`.
2. **Duración provisoria de 60 s:** la fase `Normal` dura solo 15 s con los umbrales 45/20. Al aumentar la duración hay que reescalar umbrales.
3. **Comportamiento de `Spawned()` dentro de `runner.Spawn`** en la versión de Fusion instalada: afecta el cálculo de capacidad dentro de una evaluación (Etapa 1).
4. **Collapse durante una Host Migration** y **referencias obsoletas en `_spawnedEnemies`**: casos concretos a probar en la Etapa 3.
5. **Documento de Game Design no leído:** puede modificar reglas; se reconcilia al inicio de la Etapa 1.
6. **El HUD ubica el controller con `FindObjectOfType`** (y el overlay también): fuera de alcance, anotado como deuda técnica.
