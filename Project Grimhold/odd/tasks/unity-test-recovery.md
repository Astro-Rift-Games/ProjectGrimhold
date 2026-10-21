# Unity test recovery — obsolete contracts and runner composition
Branch: `feat/unity-obsolete-tests`; baseline: `6305da4770409c95174a833f790004c0b65bc11c`. Delivery: two focused local recovery commits explicitly authorized on2026-10-21; no push or release operations.
Runner: Unity MCP / native NUnit XML; one Editor owner. Forecast: approximately 450–650 authored changed lines, coherent A and B+C units; local A and B+C commit units are now authorized; no PR or push is authorized.

## Specs

### S1 — Work A (verbatim)
## 4. Trabajo A — Recuperar 11 tests EditMode obsoletos

### Resultado esperado

Las pruebas identificadas vuelven a comprobar los contratos funcionales actuales sin reducir su cobertura.

### Alcance

Corregir los siguientes grupos:

| Casos | Causa identificada |
|---|---|
| 1 | Persistencia/Return: referencia a `TryConfirmProgressionCommit`, método eliminado |
| 3 | Conectividad de extracción: responsabilidades trasladadas a `TeamExtractionCoordinator` |
| 2 | Catálogo de armaduras: cantidad y relación icono/sprite desactualizadas |
| 1 | Catálogo de armas: expectativa dependiente del sufijo `_0` |
| 2 | Jerarquía visual y XP de primera apertura: referencia a `NetworkPlayerMelee.prefab` retirado |
| 1 | Valores de loot: cantidad esperada desactualizada |
| 1 | Presentación NPC: referencia al prefab eliminado `Merchant.prefab` |

### Reglas

Para cada grupo:

1. Identificar qué comportamiento pretendía proteger la prueba original.
2. Verificar el contrato actual.
3. Actualizar solamente los fixtures o expectativas que estén demostrablemente obsoletos.
4. Preservar las aserciones funcionales relevantes.
5. Ejecutar los tests afectados.
6. Registrar los resultados.

No sustituir aserciones funcionales por simples comprobaciones de existencia.

No introducir conteos arbitrarios que vuelvan a romperse ante incorporaciones válidas de contenido. Utilizar invariantes estables cuando el contrato lo permita.

Si un caso descubre una regresión productiva real, separarla y reportarla. No corregir el runtime automáticamente.

### Criterio de aceptación

Los 11 casos identificados superan sus pruebas de manera independiente o cualquier bloqueo residual queda demostrado y documentado, sin eliminar cobertura funcional relevante.

### S2 — Work B (verbatim)
## 5. Trabajo B — Corregir composición del harness PlayMode

### Resultado esperado

Los tests afectados pueden crear runners con las dependencias exigidas por la arquitectura vigente, sin utilizar fallbacks artificiales en el código productivo.

### Evidencia inicial

La auditoría identificó 119 fallos asociados a la ausencia de `LocalInputContext`.

La implementación productiva de `NetworkRunnerFactory` agrega ese componente al runner.

Por lo tanto, investigar primero los fixtures que construyen runners incompletos.

### Implementación

1. Localizar los constructores, factories y setups compartidos de runners de prueba afectados.
2. Comparar su composición con `NetworkRunnerFactory`.
3. Identificar las dependencias mínimas necesarias para cada escenario.
4. Corregir la composición de los fixtures.
5. Reutilizar helpers de prueba cuando varias clases necesiten exactamente la misma preparación.
6. Evitar duplicar la construcción de dependencias en decenas de tests.
7. Preservar el aislamiento entre ejecuciones.

No copiar indiscriminadamente toda la fábrica productiva a cada fixture.

### Invariantes obligatorias

- Una instancia de `LocalInputContext` por runner cuando el escenario la requiera.
- Ownership explícito y sin registros locales duplicados.
- Liberación de recursos y suscripciones en teardown.
- Ausencia de estado estático residual entre tests.
- Diferenciación correcta entre State Authority, Input Authority y proxies.
- Ningún fallback de runtime introducido exclusivamente para satisfacer pruebas.

Si se detectan errores derivados de tener dos lectores locales activos, distinguirlos de los 119 casos originales y tratarlos como seguimiento independiente.

### Criterio de aceptación

Los casos afectados dejan de fallar por ausencia de `LocalInputContext`, mantienen sus aserciones originales de comportamiento y no introducen contaminación entre tests.

Si aparecen fallos posteriores, clasificarlos como nuevos resultados observables. No ocultarlos.

### S3 — Work C (verbatim)
## 6. Trabajo C — Migrar expectativas PlayMode de extracción

### Resultado esperado

Los tests de extracción validan el comportamiento actual del sistema por equipos y dejan de depender de mensajes o reglas individuales eliminadas.

### Evidencia inicial

La auditoría identificó:

- 101 casos que esperan un error de extracción que ya no se emite.
- 3 casos con expectativas antiguas respecto del orden de los errores.

El contrato vigente distingue:

- `TeamExtractionCoordinator`: coordinación de extracción por equipos.
- `TeamExtractionProgress`: progreso compartido.
- `PlayerExtractionController`: resultado individual y restricciones terminales.
- Los componentes y servicios responsables de admisión, estado y validación de la sesión.

### Implementación

1. Localizar las expectativas obsoletas.
2. Identificar qué dependencia o comportamiento estaban comprobando.
3. Contrastar las expectativas con los contratos actuales.
4. Actualizar las aserciones y fixtures necesarios.
5. Eliminar únicamente expectativas que ya no correspondan a ninguna obligación vigente.
6. Incorporar aserciones sobre el propietario actual cuando sea necesario para preservar cobertura.
7. Ejecutar los tests específicos de extracción.

No asumir que la ausencia de un error equivale automáticamente a éxito.

Un test anteriormente negativo debe seguir comprobando un rechazo válido cuando el comportamiento actual lo requiera.

No modificar la lógica productiva de extracción ni sus responsabilidades para adaptar el sistema a tests antiguos.

### Criterio de aceptación

Los 104 casos dejan de depender de expectativas eliminadas y comprueban los contratos vigentes sin perder cobertura de validaciones, autoridad y resultados de extracción.

### S4 — Exclusions (verbatim)
## 7. Trabajo explícitamente excluido

No intervenir en esta iteración:

**Presupuestos Fusion**

- `LootContainer`: 983 → 1159.
- `NetworkEnemy`: 1007 → 1214.
- `NetworkPlayer`: 1884 → 2223.

La auditoría explicó los excesos, pero no aprobó nuevos límites. Mantener los tres tests como fallos conocidos y reservar cualquier cambio de presupuesto para una tarea independiente.

**Inicialización del inventario**

No cambiar el estado serializado del prefab ni modificar su presenter. La verificación de visibilidad e input antes del binding pertenece a una tarea posterior.

**Discrepancias de UI**

No resolver en este lote:

- Drag-and-drop frente a transferencia por clic izquierdo.
- Referencia `_progressionExperienceFill` ausente.
- Otras decisiones de presentación todavía pendientes.

**Resto de PlayMode**

No incluir automáticamente en el alcance:

- Quick Slots y contexto persistente.
- Conflictos de ownership entre lectores locales.
- Setup de XP sin `NetworkSpawnManager`.
- Servicios de progreso por equipos ausentes.
- GUID y variantes retiradas.
- Aislamiento de visibilidad.
- Casos particulares restantes.
- Tests Host/Client y Host Migration omitidos.

Podés identificar sus dependencias durante la investigación, pero no extender la implementación hacia ellos.

**Releases**

No modificar:

- `weekly-release.yml`.
- SemVer.
- El generador de changelogs.
- Las GitHub Releases.
- La configuración de publicación.
- El indicador de versión del menú principal.

No activar ni debilitar las validaciones del pipeline semanal.

### S5 — Verification and stopping boundaries (verbatim)
## 9. Validación obligatoria

### Antes de modificar

- Identificar los casos concretos de A, B y C.
- Revisar las pruebas y su contrato.
- Revisar los XML nativos existentes.
- Registrar el estado inicial del working tree.
- Confirmar que los fallos no se atribuyen incorrectamente al commit de automatización.

### Durante la implementación

Para cada resultado independiente:

- Ejecutar sus tests específicos.
- Verificar las aserciones funcionales.
- Identificar regresiones nuevas.
- Registrar passed/failed/skipped.
- No eliminar tests ni utilizar `[Ignore]` como solución.

### Al finalizar

1. Comprobar compilación limpia.
2. Ejecutar la suite completa EditMode.
3. Ejecutar la suite completa PlayMode.
4. Obtener los resultados completos desde los XML nativos.
5. Comparar con el baseline anterior.
6. Clasificar los fallos restantes por causa raíz.
7. Verificar el diff completo.
8. Confirmar que no existen modificaciones serializadas o de runtime ajenas al objetivo.

Los 2 tests omitidos de Host/Client y migración deben seguir informándose como cobertura pendiente.

**No considerar completada una suite porque el proceso Unity terminó sin mostrar errores en MCP.** Leer su resultado real y los XML.

## 10. Condiciones de interrupción

Detener el trabajo afectado y solicitar una decisión si:

- Una corrección necesita cambiar Game Design.
- Un test contradice un contrato arquitectónico vigente y no puede resolverse la autoridad del requisito.
- Aparece un defecto productivo que exige modificar runtime fuera del alcance.
- Es necesario alterar un prefab, escena o asset no autorizado.
- La solución requiere modificar el sistema de Fusion o sus presupuestos.
- No puede comprobarse que una modificación preserve cobertura funcional.
- El working tree contiene cambios ajenos que entran en conflicto con la implementación.

No silenciar pruebas para completar artificialmente el objetivo.

## 11. Criterios de finalización

El trabajo se considera completado cuando:

- Los 11 tests EditMode obsoletos están corregidos y validados.
- Los 119 casos del harness ya no fallan por ausencia de `LocalInputContext`.
- Los 104 casos de extracción usan expectativas compatibles con el contrato vigente.
- Las correcciones conservan cobertura funcional.
- La compilación pasa.
- Se ejecutan las suites completas sin filtros.
- Se dispone del resultado exacto de cada suite.
- Se documentan los fallos nuevos o residuales.
- No se modificaron presupuestos Fusion, runtime o UI fuera del alcance.
- No se modificó ni activó la automatización de releases.

El criterio no exige cero fallos globales: exige que los resultados propios de esta iteración estén verificados.

### S6 — Delivery (verbatim)
## 12. Entrega

Presentar un informe final con:

1. Resultado de A, B y C por separado.
2. Archivos modificados y motivo.
3. Tests corregidos y cobertura preservada.
4. Tabla comparativa del baseline y resultados nuevos.
5. Fallos residuales agrupados por causa raíz.
6. Posibles defectos productivos descubiertos.
7. Cambios locales preexistentes preservados.
8. Validaciones ejecutadas y pendientes.
9. Próximas tareas recomendadas, sin implementarlas.

**No crear commits, hacer push ni modificar ramas remotas sin autorización explícita posterior.**

No declarar las suites completas como aprobadas mientras persistan fallos u omisiones.

Al terminar A, B y C y sus validaciones, detener la implementación. No continuar automáticamente con los demás grupos.

### S7 — Explicit ACK scope decision (verbatim)
Mantené el contrato vigente y dejá ese caso bloqueado. No sustituyas el ACK de persistencia por la finalización del cálculo de XP ni elimines la prueba.
Continuá con los otros diez casos de A y los bloques B/C. Registrá este caso como seguimiento independiente para identificar cómo se implementa actualmente el ACK y recuperar la prueba contra el contrato correcto, sin modificar runtime en esta tarea.

### S8 — Local commit authorization (2026-10-21, verbatim)
ok si sirve hay que commitearlo

Reviewed useful scope: seven EditMode fixtures (A), plus29 PlayMode fixtures,
three new test-only sources with their metadata and this record (B+C).
MagicGarden artwork and serialized font drift remain excluded and preserved.
This authorization does not approve failing suites or expand runtime/UI work.

## Tasks
- T1 | S1,S4-S7 | delegated direct: EditMode writer | validated: ten repaired cases pass focused and full; original ACK case unchanged and explicitly deferred to T5 | commit: 2572ba5218ad2fa71c49e679cc68dd40552cdbbc (A), explicitly authorized by S8.
- T2 | S2,S4-S7 | delegated direct: one PlayMode writer also owns T3 | implemented; original missing-context diagnostic zero in full cohort 119/119; full outcomes 88 passed / 31 other failures; clean global isolation is not established | commit: authorized B+C recovery commit containing this record; exact identity retained in post-commit evidence.
- T3 | S3-S7 | delegated direct: same PlayMode writer as T2 | implemented; obsolete extraction-log diagnostic zero in all 104 full targets; 89 passed / 15 other failures; original behavioral assertions preserved | commit: authorized B+C recovery commit containing this record; exact identity retained in post-commit evidence.
- T4 | S5-S7 | delegated validation plus parent XML/diff/hash readback | complete: one unfiltered run each, EditMode 4162/5/0 of 4167, PlayMode 655/114/2 of 771; three observed Passed→Failed reader-conflict cases explicitly reported; suites remain failing | commit: authorized B+C recovery commit containing this record; exact identity retained in post-commit evidence.

- T5 | S7 | independent follow-up record only; no implementation in this task | blocked/deferred: identify the actual durable post-resolution ACK owner and recover the original test against the retained contract; runtime changes require separate authorization | commit: follow-up record only included under S8; implementation remains unauthorized.

## Log
### L1 — Original user request (verbatim)
# TASK — Recuperar pruebas Unity obsoletas y corregir la composición del harness PlayMode

## Rol

Actuá como orquestador técnico de Project Grimhold.

Tu responsabilidad es analizar el estado actual, planificar la implementación, decidir qué tareas delegar a subagentes, coordinar su ejecución y verificar los resultados.

Podés crear subagentes según las necesidades identificadas. No necesitás solicitar autorización para delegar análisis o implementación dentro del alcance aprobado.

Trabajá con **High effort** cuando la complejidad técnica lo justifique. Evitá exploraciones extensas que no aporten directamente al objetivo.

## 1. Objetivo

Reducir los fallos de las suites Unity mediante correcciones acotadas en pruebas y fixtures cuya causa raíz ya fue identificada.

Esta iteración comprende tres resultados independientes:

**A. Recuperar los 11 tests EditMode inequívocamente obsoletos.**

**B. Corregir la composición de los runners de prueba responsables de los 119 fallos PlayMode asociados a `LocalInputContext`.**

**C. Actualizar los 104 tests PlayMode con expectativas de extracción incompatibles con el contrato actual de extracción por equipos.**

No se exige dejar las suites completas en verde durante esta tarea.

El objetivo es recuperar cobertura funcional válida, no simplemente reducir el contador de fallos.

## 2. Contexto obligatorio

Repositorio: `Astro-Rift-Games/ProjectGrimhold`.

Rama de trabajo: `feat/unity-obsolete-tests`.

Último commit remoto verificado: `6305da47`. Confirmar el estado actual antes de comenzar.

Fuentes obligatorias:

1. `AGENTS.md`.
2. `Project Grimhold/AGENTS.md`.
3. `.agents/skills/grimhold-task-execution/SKILL.md`.
4. Documentos pertinentes de `Project Grimhold/Docs/Architecture/`.
5. Código, fixtures, escenas y prefabs actuales.
6. Auditoría de causas raíz de las suites Unity proporcionada en la conversación.
7. Resultados XML nativos de la última ejecución completa.

Documentos especialmente relevantes:

- `ExtractionArchitecture.md`.
- `SessionLifecycleAndSpawning.md`.
- `RaidParticipantArchitecture.md`.
- `RaidConnectivityContinuityArchitecture.md`, cuando corresponda.
- `LocalPlayerPersistenceArchitecture.md`.
- `RaidInventoryUIArchitecture.md`, solo para identificar límites del trabajo.

Utilizar CodeGraph para localizar relaciones y responsabilidades cuando sea útil. Verificar siempre las conclusiones importantes contra el código real.

Usar Unity MCP para compilación, inspección de assets y ejecución de tests. Si una herramienta no está disponible, aplicar las alternativas previstas en `AGENTS.md`.

Antes de editar:

- Confirmar rama, HEAD y estado del working tree.
- Identificar modificaciones locales preexistentes, incluidas las serializaciones TMP.
- Preservar todos los cambios ajenos a esta tarea.
- No ejecutar `git clean`, descartes masivos ni restauraciones de archivos ajenos.
- No crear nuevos worktrees sin autorización.

## 3. Resultados de referencia

La última auditoría identificó:

### EditMode

- 4152 aprobados.
- 15 fallidos.
- 0 omitidos.

Distribución:

- 11 tests obsoletos o fixtures desactualizados.
- 3 incumplimientos de presupuestos Fusion.
- 1 test de inicialización del inventario con contrato inicial pendiente.

### PlayMode

- 469 aprobados.
- 297 fallidos.
- 2 omitidos.

Grupos prioritarios:

- 119 fallos: runners de pruebas sin `LocalInputContext`.
- 104 fallos: expectativas antiguas de extracción, incluyendo errores esperados ausentes y diferencias de orden.

Los grupos representan el **primer fallo observado**. Corregirlos puede descubrir fallos adicionales actualmente ocultos.

No asumir que esos números siguen vigentes sin comprobar los resultados y el estado actual.

## 4. Trabajo A — Recuperar 11 tests EditMode obsoletos

### Resultado esperado

Las pruebas identificadas vuelven a comprobar los contratos funcionales actuales sin reducir su cobertura.

### Alcance

Corregir los siguientes grupos:

| Casos | Causa identificada |
|---|---|
| 1 | Persistencia/Return: referencia a `TryConfirmProgressionCommit`, método eliminado |
| 3 | Conectividad de extracción: responsabilidades trasladadas a `TeamExtractionCoordinator` |
| 2 | Catálogo de armaduras: cantidad y relación icono/sprite desactualizadas |
| 1 | Catálogo de armas: expectativa dependiente del sufijo `_0` |
| 2 | Jerarquía visual y XP de primera apertura: referencia a `NetworkPlayerMelee.prefab` retirado |
| 1 | Valores de loot: cantidad esperada desactualizada |
| 1 | Presentación NPC: referencia al prefab eliminado `Merchant.prefab` |

### Reglas

Para cada grupo:

1. Identificar qué comportamiento pretendía proteger la prueba original.
2. Verificar el contrato actual.
3. Actualizar solamente los fixtures o expectativas que estén demostrablemente obsoletos.
4. Preservar las aserciones funcionales relevantes.
5. Ejecutar los tests afectados.
6. Registrar los resultados.

No sustituir aserciones funcionales por simples comprobaciones de existencia.

No introducir conteos arbitrarios que vuelvan a romperse ante incorporaciones válidas de contenido. Utilizar invariantes estables cuando el contrato lo permita.

Si un caso descubre una regresión productiva real, separarla y reportarla. No corregir el runtime automáticamente.

### Criterio de aceptación

Los 11 casos identificados superan sus pruebas de manera independiente o cualquier bloqueo residual queda demostrado y documentado, sin eliminar cobertura funcional relevante.

## 5. Trabajo B — Corregir composición del harness PlayMode

### Resultado esperado

Los tests afectados pueden crear runners con las dependencias exigidas por la arquitectura vigente, sin utilizar fallbacks artificiales en el código productivo.

### Evidencia inicial

La auditoría identificó 119 fallos asociados a la ausencia de `LocalInputContext`.

La implementación productiva de `NetworkRunnerFactory` agrega ese componente al runner.

Por lo tanto, investigar primero los fixtures que construyen runners incompletos.

### Implementación

1. Localizar los constructores, factories y setups compartidos de runners de prueba afectados.
2. Comparar su composición con `NetworkRunnerFactory`.
3. Identificar las dependencias mínimas necesarias para cada escenario.
4. Corregir la composición de los fixtures.
5. Reutilizar helpers de prueba cuando varias clases necesiten exactamente la misma preparación.
6. Evitar duplicar la construcción de dependencias en decenas de tests.
7. Preservar el aislamiento entre ejecuciones.

No copiar indiscriminadamente toda la fábrica productiva a cada fixture.

### Invariantes obligatorias

- Una instancia de `LocalInputContext` por runner cuando el escenario la requiera.
- Ownership explícito y sin registros locales duplicados.
- Liberación de recursos y suscripciones en teardown.
- Ausencia de estado estático residual entre tests.
- Diferenciación correcta entre State Authority, Input Authority y proxies.
- Ningún fallback de runtime introducido exclusivamente para satisfacer pruebas.

Si se detectan errores derivados de tener dos lectores locales activos, distinguirlos de los 119 casos originales y tratarlos como seguimiento independiente.

### Criterio de aceptación

Los casos afectados dejan de fallar por ausencia de `LocalInputContext`, mantienen sus aserciones originales de comportamiento y no introducen contaminación entre tests.

Si aparecen fallos posteriores, clasificarlos como nuevos resultados observables. No ocultarlos.

## 6. Trabajo C — Migrar expectativas PlayMode de extracción

### Resultado esperado

Los tests de extracción validan el comportamiento actual del sistema por equipos y dejan de depender de mensajes o reglas individuales eliminadas.

### Evidencia inicial

La auditoría identificó:

- 101 casos que esperan un error de extracción que ya no se emite.
- 3 casos con expectativas antiguas respecto del orden de los errores.

El contrato vigente distingue:

- `TeamExtractionCoordinator`: coordinación de extracción por equipos.
- `TeamExtractionProgress`: progreso compartido.
- `PlayerExtractionController`: resultado individual y restricciones terminales.
- Los componentes y servicios responsables de admisión, estado y validación de la sesión.

### Implementación

1. Localizar las expectativas obsoletas.
2. Identificar qué dependencia o comportamiento estaban comprobando.
3. Contrastar las expectativas con los contratos actuales.
4. Actualizar las aserciones y fixtures necesarios.
5. Eliminar únicamente expectativas que ya no correspondan a ninguna obligación vigente.
6. Incorporar aserciones sobre el propietario actual cuando sea necesario para preservar cobertura.
7. Ejecutar los tests específicos de extracción.

No asumir que la ausencia de un error equivale automáticamente a éxito.

Un test anteriormente negativo debe seguir comprobando un rechazo válido cuando el comportamiento actual lo requiera.

No modificar la lógica productiva de extracción ni sus responsabilidades para adaptar el sistema a tests antiguos.

### Criterio de aceptación

Los 104 casos dejan de depender de expectativas eliminadas y comprueban los contratos vigentes sin perder cobertura de validaciones, autoridad y resultados de extracción.

## 7. Trabajo explícitamente excluido

No intervenir en esta iteración:

**Presupuestos Fusion**

- `LootContainer`: 983 → 1159.
- `NetworkEnemy`: 1007 → 1214.
- `NetworkPlayer`: 1884 → 2223.

La auditoría explicó los excesos, pero no aprobó nuevos límites. Mantener los tres tests como fallos conocidos y reservar cualquier cambio de presupuesto para una tarea independiente.

**Inicialización del inventario**

No cambiar el estado serializado del prefab ni modificar su presenter. La verificación de visibilidad e input antes del binding pertenece a una tarea posterior.

**Discrepancias de UI**

No resolver en este lote:

- Drag-and-drop frente a transferencia por clic izquierdo.
- Referencia `_progressionExperienceFill` ausente.
- Otras decisiones de presentación todavía pendientes.

**Resto de PlayMode**

No incluir automáticamente en el alcance:

- Quick Slots y contexto persistente.
- Conflictos de ownership entre lectores locales.
- Setup de XP sin `NetworkSpawnManager`.
- Servicios de progreso por equipos ausentes.
- GUID y variantes retiradas.
- Aislamiento de visibilidad.
- Casos particulares restantes.
- Tests Host/Client y Host Migration omitidos.

Podés identificar sus dependencias durante la investigación, pero no extender la implementación hacia ellos.

**Releases**

No modificar:

- `weekly-release.yml`.
- SemVer.
- El generador de changelogs.
- Las GitHub Releases.
- La configuración de publicación.
- El indicador de versión del menú principal.

No activar ni debilitar las validaciones del pipeline semanal.

## 8. Estrategia de ejecución y subagentes

Preparar un plan breve antes de modificar archivos.

Tratar A, B y C como resultados independientes, con sus propios criterios de aceptación.

El orquestador decide si asignarlos a subagentes.

Se permite avanzar en paralelo cuando los archivos y contratos no se superpongan, pero deben serializarse las operaciones que requieran controlar un mismo Unity Editor o ejecutar pruebas incompatibles simultáneamente sobre el mismo proyecto.

No crear subtareas artificiales por cada test individual si varios fallos comparten una causa raíz.

No concentrar en un único helper responsabilidades de sistemas no relacionados.

No crear tareas duplicadas en HacknPlan ni documentos de planificación adicionales si no son necesarios.

## 9. Validación obligatoria

### Antes de modificar

- Identificar los casos concretos de A, B y C.
- Revisar las pruebas y su contrato.
- Revisar los XML nativos existentes.
- Registrar el estado inicial del working tree.
- Confirmar que los fallos no se atribuyen incorrectamente al commit de automatización.

### Durante la implementación

Para cada resultado independiente:

- Ejecutar sus tests específicos.
- Verificar las aserciones funcionales.
- Identificar regresiones nuevas.
- Registrar passed/failed/skipped.
- No eliminar tests ni utilizar `[Ignore]` como solución.

### Al finalizar

1. Comprobar compilación limpia.
2. Ejecutar la suite completa EditMode.
3. Ejecutar la suite completa PlayMode.
4. Obtener los resultados completos desde los XML nativos.
5. Comparar con el baseline anterior.
6. Clasificar los fallos restantes por causa raíz.
7. Verificar el diff completo.
8. Confirmar que no existen modificaciones serializadas o de runtime ajenas al objetivo.

Los 2 tests omitidos de Host/Client y migración deben seguir informándose como cobertura pendiente.

**No considerar completada una suite porque el proceso Unity terminó sin mostrar errores en MCP.** Leer su resultado real y los XML.

## 10. Condiciones de interrupción

Detener el trabajo afectado y solicitar una decisión si:

- Una corrección necesita cambiar Game Design.
- Un test contradice un contrato arquitectónico vigente y no puede resolverse la autoridad del requisito.
- Aparece un defecto productivo que exige modificar runtime fuera del alcance.
- Es necesario alterar un prefab, escena o asset no autorizado.
- La solución requiere modificar el sistema de Fusion o sus presupuestos.
- No puede comprobarse que una modificación preserve cobertura funcional.
- El working tree contiene cambios ajenos que entran en conflicto con la implementación.

No silenciar pruebas para completar artificialmente el objetivo.

## 11. Criterios de finalización

El trabajo se considera completado cuando:

- Los 11 tests EditMode obsoletos están corregidos y validados.
- Los 119 casos del harness ya no fallan por ausencia de `LocalInputContext`.
- Los 104 casos de extracción usan expectativas compatibles con el contrato vigente.
- Las correcciones conservan cobertura funcional.
- La compilación pasa.
- Se ejecutan las suites completas sin filtros.
- Se dispone del resultado exacto de cada suite.
- Se documentan los fallos nuevos o residuales.
- No se modificaron presupuestos Fusion, runtime o UI fuera del alcance.
- No se modificó ni activó la automatización de releases.

El criterio no exige cero fallos globales: exige que los resultados propios de esta iteración estén verificados.

## 12. Entrega

Presentar un informe final con:

1. Resultado de A, B y C por separado.
2. Archivos modificados y motivo.
3. Tests corregidos y cobertura preservada.
4. Tabla comparativa del baseline y resultados nuevos.
5. Fallos residuales agrupados por causa raíz.
6. Posibles defectos productivos descubiertos.
7. Cambios locales preexistentes preservados.
8. Validaciones ejecutadas y pendientes.
9. Próximas tareas recomendadas, sin implementarlas.

**No crear commits, hacer push ni modificar ramas remotas sin autorización explícita posterior.**

No declarar las suites completas como aprobadas mientras persistan fallos u omisiones.

Al terminar A, B y C y sus validaciones, detener la implementación. No continuar automáticamente con los demás grupos.

### L2 — Initial evidence and routing
- Current branch and HEAD verified; working tree and index initially clean. No pre-existing TMP or other local changes observed.
- Baseline native XML hashes match the previous complete runs: EditMode 4167 / 4152 passed / 15 failed / 0 skipped; PlayMode 768 / 469 passed / 297 failed / 2 skipped.
- A affects eight existing EditMode fixture files. B and C overlap across 29 PlayMode fixture files, therefore one writer owns their shared surfaces and two narrowly scoped helpers.
- Writer reason: A and B+C are independent behavioral test-maintenance units with disjoint files and several minutes of work each, launched together after RED verification. No runtime, production assets, dependencies, Fusion budgets, inventory UI behavior, releases, commits, push or new worktrees.
- Existing XML is observed RED against matching current source bytes; fresh bounded targeted RED will be captured before source writes. Parent owns Git; one validation worker owns the Editor.
- RDD was clone-local OFF in the audit; it will not be enabled automatically. Test-only changes are expected Risk: none; reassess actual diffs.

### L3 — Fresh RED, preservation, and explicit resume
- Initial A run 8370d33bb0024248ac221488946163c6: 64 total, 53 passed, 11 failed, 0 skipped; all eleven selected historical failures reproduced.
- Initial B/C run 8ec634fdb39c43ddadd083cec5fbc08f: three AbilityAreaTargeting representatives, all failed on the obsolete extraction LogAssert. The isolated run did not reproduce missing LocalInputContext as first failure; the full-suite 119-case evidence remains separate.
- RED artifacts and protected original asset backups: session visualization folder /unity-test-recovery-validation/. Validation produced alagard font glyph serialization (+112/-27) and EditorBuildSettings line-ending-only drift. Minimap unchanged. Initial working tree was clean; no foreign local modifications existed. No assets restored yet.
- User repeated the identical attached task, interrupted accidentally, then explicitly said: "continua, fue un error mio". After interruption HEAD remained 6305da47 and no test source had been written; only the known validation serialization and this task document existed. Both bounded writers resumed with unchanged scopes.
- A writer raised an unresolved durable progression ACK/Return contract question; only that case is paused pending one bounded read-only owner-flow check. Other A cases and B+C continue. No runtime fixes authorized.

### L4 — Bounded persistence/Return contract finding and A implementation
- T1: seven allowed EditMode files now implement the other ten obsolete-case repairs; focused GREEN remains pending. The original Return ACK case/file is unchanged.
- Bounded owner-flow check confirms a source-versus-architecture conflict, not a simple renamed ACK: an earlier extraction receipt is persisted by PlayerExtractionLootSaver/LocalProfileStore, but the required post-resolver matching ProgressionReceipt ACK is not demonstrated before current Return acceptance. See ProgressionArchitecture:218-232, ExtractionArchitecture:126-132, NetworkRaidParticipant:359-372/623-643/774-797, and RaidResultsReturnPolicy.
- Do not substitute a resolver-only assertion: it would erase durability coverage. This one case is paused for a decision on the authoritative ACK contract; no runtime fix is authorized. Other ten A cases and B+C continue independently.
- Parent read the seven-file A diff; central git diff --check passes (line-ending warnings only). No test deletion, Ignore, blanket logging suppression, budget edits or runtime edits were found in that unit. Remaining verification is executable, not inferred from static readback.

### L5 — User confirms ACK contract and independent follow-up
Mantené el contrato vigente y dejá ese caso bloqueado. No sustituyas el ACK de persistencia por la finalización del cálculo de XP ni elimines la prueba.
Continuá con los otros diez casos de A y los bloques B/C. Registrá este caso como seguimiento independiente para identificar cómo se implementa actualmente el ACK y recuperar la prueba contra el contrato correcto, sin modificar runtime en esta tarea.
- This confirms the retained contract; it does not authorize runtime remediation. The original ACK test stays unchanged and known-failing. T5 records the independent follow-up without creating a HacknPlan item or another planning document.
- Both writers returned; parent inspected helpers and representative B/C diffs, central diff check passed. Native assessment with explicitly declared seven untracked files: medium, under_budget; RDD remains clone-local OFF. No named high-risk item is touched by the synchronous test-only composition.
- T4 now owns Editor for compilation and focused A plus all B/C fixture classes and three helper invariant tests; full suites follow after focused results. No other writes to test sources while validation is active.

### L6 — Compilation correction and focused A result
- First compilation stopped with 26 CS0104 ambiguities between Fusion.Assert and NUnit.Framework.Assert in the three new helper/test files. Parent applied one mechanical correction batch: explicit NUnit Assert aliases in those files only.
- Compilation then passed with zero error-CS entries. Focused A native XML: 64 total, 63 passed, one failure, zero skipped. All ten implemented former failures cleared; the sole failure is the retained, untouched ACK case under the user's S7 decision.
- Native Editor is idle and MainMenu clean. MCP job metadata remained stale despite fresh completed XML; the executor waits its bounded initialization timeout before one B/C launch, without replay. B/C focused and complete suites remain pending.

### L7 — Focused B/C outcome and start of complete suites
- Focused B/C job 02ca1f2c17924f19a2f6d45d9b7d0ea6: 274 total, 246 passed, 26 failed, two Explicit skips. All 223 baseline B/C names found exactly once: B 114/119 passed (five other failures), C 102/104 passed (two RaidMenu failures). Missing-context and obsolete extraction-log first failures are both zero; three new helper invariant tests passed.
- The 26 remaining focused failures were all already Failed in the archived baseline: 16 NRE, six missing team-progress service, two booleans, one null assertion, one pickup-provenance dependency. This comparison establishes status preservation, not a blanket root-cause claim. No excluded group was fixed.
- Parent independently parsed native XML and target cohorts: A SHA256 1AAF86F26152C1C206A2776C04DF7CE1E7F3F09938B490DB686F512831D7A088; B/C SHA256 6DC25EADFC970C52BD877DBBA3926F4A1B16DED679DF36185A5706A2D9DDB2D5. Readback saved as parent-focused-readback.json.
- Source bytes remain frozen. T4 now executes exactly one complete unfiltered EditMode suite followed by one complete unfiltered PlayMode suite, archiving native XML before overwrite. The two real Host/Client/cloud-migration cases remain pending explicit skips. No complete-suite success is inferred from focused counts.

### L8 — Final complete-suite evidence, scope closure and independent follow-ups
- Both required full suites executed once without filters. EditMode job 5c6a64570df4424d94f4420e8e7ec198: 4167 total / 4162 passed / 5 failed / 0 skipped; PlayMode job fe82f76681b640efa13d485b8d17392d: 771 total / 655 passed / 114 failed / 2 Explicit skipped. Sixteen PlayMode Error labels are included in 114; invalid/inconclusive counts zero.
- Baseline EditMode 4152/15/0 becomes 4162/5/0: ten Failed→Passed, zero Passed→Failed. Remaining five are exactly ACK1 + unchanged Fusion3 + unchanged inventory-initialization1. Baseline PlayMode 469/297/2 becomes 655/114/2: 186 Failed→Passed, three Passed→Failed, no missing baseline case, three new passing helper tests. Three EditMode renames are purpose-mapped explicitly.
- Final exact cohorts: B119 = 88 passed / 31 failed; C104 = 89 passed / 15 failed, every name exactly once. Original missing-context and obsolete extraction-log diagnostics are both zero. Do not project focused 114/119 and 102/104 pass counts onto the complete execution.
- Newly failing previously-passed cases: PlayerExpeditionExperienceLedgerPlayModeTests.ConfirmedExtractionReward_RequiresMatchingConfirmationAndPreservesBreakdown; PlayerExpeditionProgressionResolverPlayModeTests.CommittedResult_IsCompleteRepeatableAndPreservedByStateCopy; PlayerExpeditionProgressionResolverPlayModeTests.MissingBaseline_ReturnsIntegrationFailureWithoutFreezingLedger. All report different active local reader (TryRegister32 → FusionInputProvider68 → Update27). Candidate causality/order/environment is not established; reader-ownership remediation remains an independent excluded follow-up. No suppression or repeat full runs were used.
- PlayMode residual evidence groups: duplicate reader18, Quick Slots/profile15, NRE14, missing team service6, invalid prefab ID2, visibility2, Town binder1, other functional/asset/contract assertions56. All names/stacks archived; the 56 other assertions are not assigned invented closed root causes.
- Parent independently parsed final XML/hash/no-filter counts. Full XML SHA256: Edit92D1E6AEDD22A09F306A302D25FCEF8A9AD44F78E9AD95B20DA6FDBFF84D8A31; Play8EAC0CF071FBD9A13C81631AD009C4191E0EEF626668F626FA37E2B810294C11. Exact transitions, target intersections, complete residuals and file manifest are in unity-test-recovery-validation.
- After all tests, parent restored ONLY proven validation-owned font and BuildSettings drift to exact protected original bytes with two-path preflight/current/backup hash guards. Reimported font only; font and MainMenu clean, not playing/compiling, compiler errors zero. Minimap remained unchanged. No foreign changes existed initially or were restored.
- Final Git boundary: 36 modified existing test .cs + 3 new test .cs + 3 new .meta + this task document = 43 paths; no unexpected path, no runtime/assets/scenes/settings/Photon/release/tool changes, ACK/budget/inventory-contract files untouched, index empty, HEAD6305da47 unchanged, diff check passed. No commit, push or worktree created.
- Detailed delivery report: session visualization folder /unity-test-recovery-validation/validation-report.md. Changed-file reasons: changed-files.json. Full diff structural audit found no removed PlayMode non-log behavior assertion, deleted test attribute, new Ignore/Pass/log suppression or early exit.
- T5 remains deferred under the user's explicit retained ACK contract. Other recommended follow-ups (not implemented): reader ownership/full-run isolation including the three regressions, persistent-profile/Quick Slots composition, XP setup, team services and remaining contract/asset assertions. The two real Host/Client/cloud-migration cases remain pending.
- Scoped implementation stops here. No complete-suite approval or clean-isolation claim is made. Risk: none for the synchronous test-only setup/assertion changes; observed regressions remain explicitly reported.








### L9 — Reviewed recovery commits explicitly authorized (2026-10-21)
User authorization (verbatim):
ok si sirve hay que commitearlo

- Parent verified all43 candidate hashes against the previously validated state; read-only explorer inspected every candidate and found useful, coherent recovery with no concrete new blocking defect or weakened functional coverage. Parent repeated the scoped diff check.
- Two independent work units: A seven EditMode fixtures, committed as2572ba5218ad2fa71c49e679cc68dd40552cdbbc (+85/-64); B+C29 modified PlayMode fixtures, three new test-only sources, three metadata files and this record. B+C shares setup surfaces and stays together; the original request/evidence log is retained intact with the code despite exceeding the advisory400-line heuristic when passive documentation is included.
- The B+C commit identity is retained in post-commit Git/Engram evidence instead of creating a circular self-hash in this file. The rollback boundary is the exact seven A paths or the exact B+C paths recorded by each commit.
- Source bytes did not change and Unity tests were not rerun for this commit-only operation. Archived full EditMode remains4162/5/0. Latest separately executed full PlayMode is662/107/2; B119=114passed/5failed, C104=102passed/2failed; original missing-context/obsolete-extraction first signatures0; original nine targets and six prior recovery guards pass.
- This is NOT full acceptance:38 new UI setup Passed-to-Failed transitions still lack causal attribution, seven historical nine-target causes remain indeterminate, and the durable progression ACK remains blocked under the user's retained contract. No UI/runtime correction or new investigation is authorized by the commit request.
- Existing cleanup commits6f79b2e6 (ExtractionProgress) anddaf57901 (Hub) remain separate and are not duplicated. Exactly four unrelated artwork/font paths are excluded and preserved byte-for-byte; no restore, cleanup, push, release, PR or worktree operation is part of this delivery.
- Artifacts: current session visualization/nine-playmode/recovery-commit-readiness.md (43-file rationale), recovery-commit-candidate-manifest.json, recovery-commit-latest-BC-readback.json and recovery-commit-excluded-files.txt. RDD remains clone-local OFF. Risk: none for the unchanged synchronous test-only A/B/C recovery; existing suite limitations remain explicit.