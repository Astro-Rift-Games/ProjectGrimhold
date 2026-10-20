# Local reader isolation regression recovery
Branch: feat/unity-obsolete-tests at 6305da4770409c95174a833f790004c0b65bc11c; fixture-only recovery; focused local commit explicitly authorized after validation, no push.
Runner: Unity MCP, one owner at a time. Route: inline implementation after delegated read-only mapping; delegated sequential long test execution. Forecast: narrow fixture lifecycle edit, under 150 authored lines; delivery ask-on-risk; focused local commit authorized after completion, no push.

## Specs
S1. "Investigar y corregir los **tres tests PlayMode que anteriormente aprobaban y comenzaron a fallar por conflictos de registro de lectores locales** después de las últimas correcciones de fixtures." "La correlación temporal no demuestra que los nuevos helpers sean la causa. Debe establecerse mediante evidencia reproducible."
S2. "Ejecutar los tres tests afectados:" "1. Individualmente." "2. Como grupo." "3. Junto con las pruebas que crean y destruyen runners relacionados." "Registrar diferencias entre ejecuciones aisladas y agrupadas."
S3. "Aplicar la corrección mínima en los fixtures o helpers responsables." "No modificar código productivo para resolver esta tarea." "No modificar las expectativas funcionales de los tres tests únicamente para hacerlos aprobar." "No utilizar búsquedas globales ni destrucciones indiscriminadas para ocultar dependencias mal gestionadas."
S4. "Los tres tests afectados aprueban individualmente." "Aprueban juntos." "Aprueban junto a los tests relacionados con runners." "Los tres tests nuevos de composición continúan aprobando." "No permanecen lectores registrados después del teardown." "No aparecen dependencias entre ejecuciones consecutivas."
S5. "Ejecutar PlayMode completo, sin filtros." "Leer los resultados exactos desde los XML nativos." "Comparar los resultados con el baseline de 655 aprobados, 114 fallidos y 2 omitidos." "Ejecutar también EditMode completo si los cambios afectan helpers compartidos o composición utilizada por esa suite." "La comparación debe identificar los tests por nombre e identidad, no solo por cantidades agregadas."
S6. "No aceptar la corrección si recupera los tres casos pero vuelve a introducir fallos por ausencia de LocalInputContext o expectativas obsoletas de extracción." "Si aparecen fallos nuevos, determinar si pertenecen al cambio antes de ampliar el alcance." "No crear commits ni hacer push sin autorización explícita." "Detener la implementación al completar este objetivo. No continuar automáticamente con otros grupos de fallos."
S7. "Si se demuestra que los fallos son preexistentes o requieren cambios fuera del alcance, documentar la evidencia y dejar el resultado correspondiente bloqueado, sin forzar su aprobación."

## Tasks
- T1 | S1-S2 | delegated read-only mapping + sequential reproduction (mapping budget/long tests) | complete | clean diagnostic group3/3; contaminated group0/5 with all3 original reader stacks and observed owner mapping.
- T2 | S3,S6-S7 | inline minimal fixture lifecycle correction if justified | complete | controlled A/B targets0/3 to3/3; final diagnostic waits removed by exact initial byte restoration; included in the authorized focused commit.
- T3 | S4-S6 | delegated sequential long checks and XML comparison; parent spot check | complete | individual3x1/1; group3/3; cohort16/11/0; full667/102/2; independent item4 PASS; included in the authorized focused commit.

## Log
L1 — Original user request (verbatim):
# TASK — Resolver regresiones de aislamiento de lectores locales en PlayMode

## Rol

Actuá como orquestador técnico de Project Grimhold.

Planificá, delegá en subagentes cuando resulte conveniente, implementá las correcciones necesarias y verificá los resultados. Coordiná las operaciones sobre Unity para evitar ejecuciones simultáneas que interfieran entre sí.

## 1. Objetivo

Investigar y corregir los **tres tests PlayMode que anteriormente aprobaban y comenzaron a fallar por conflictos de registro de lectores locales** después de las últimas correcciones de fixtures.

El objetivo es recuperar su comportamiento y garantizar el aislamiento de los runners de pruebas, sin perder los avances de la iteración anterior.

No abordar los otros fallos de PlayMode ni intentar dejar las suites completas en verde.

## 2. Contexto

Repositorio: `Astro-Rift-Games/ProjectGrimhold`.

Rama de trabajo: `feat/unity-obsolete-tests`.

Último commit remoto verificado: `6305da47`. Existen cambios locales posteriores todavía sin commit.

Resultados de referencia:

| Suite | Aprobados | Fallidos | Omitidos |
|---|---:|---:|---:|
| EditMode | 4162 | 5 | 0 |
| PlayMode | 655 | 114 | 2 |

La última iteración consiguió:

- A: 10 tests EditMode corregidos y aprobados; ACK pendiente.
- B: 119 fallos originales por ausencia de `LocalInputContext` eliminados.
- C: 104 fallos por expectativas obsoletas de extracción eliminados.
- Tres tests nuevos de composición aprobados.

Sin embargo, tres pruebas anteriormente aprobadas ahora fallan por lectores locales duplicados.

**La correlación temporal no demuestra que los nuevos helpers sean la causa. Debe establecerse mediante evidencia reproducible.**

## 3. Inspección inicial

Antes de modificar archivos:

1. Leer `AGENTS.md`, `Project Grimhold/AGENTS.md` y las skills aplicables.
2. Confirmar rama, HEAD y working tree.
3. Preservar íntegramente los cambios locales existentes.
4. Consultar los resultados XML nativos de ambas ejecuciones completas.
5. Identificar los nombres exactos de los tres tests afectados.
6. Revisar los cambios recientes de fixtures y helpers.
7. Leer los contratos pertinentes de `Docs/Architecture/`, especialmente `RaidInventoryUIArchitecture.md` y `SessionLifecycleAndSpawning.md`.

Inspeccionar las responsabilidades de:

- `LocalInputContext`.
- `PlayerInputReader`.
- `FusionInputProvider`.
- `NetworkRunnerFactory`.
- Los helpers de composición introducidos recientemente.
- Los métodos `SetUp`, `UnitySetUp`, `TearDown` y `UnityTearDown` de los tests afectados.

El contrato actual establece que `LocalInputContext` es runner-scoped, admite un único lector activo y debe liberar su asociación al finalizar la sesión.

## 4. Investigación de causa raíz

Determinar por qué aparecen registros duplicados.

Considerar y verificar:

- Instancias simultáneas de `PlayerInputReader`.
- Registros duplicados en un mismo `LocalInputContext`.
- Fixtures que crean dependencias ya existentes.
- Runners anteriores que continúan activos.
- Teardown incompleto.
- Objetos persistentes entre pruebas.
- Suscripciones no liberadas.
- Orden de ejecución de los tests.
- Estado compartido entre escenarios.
- Dependencias introducidas por los nuevos helpers.

No asumir ninguna de estas causas sin evidencia.

### Reproducción

Ejecutar los tres tests afectados:

1. Individualmente.
2. Como grupo.
3. Junto con las pruebas que crean y destruyen runners relacionados.

Registrar diferencias entre ejecuciones aisladas y agrupadas.

Cuando sea necesario, instrumentar temporalmente los fixtures para identificar:

- Runner propietario.
- Instancia de `LocalInputContext`.
- Lector registrado.
- Momento de registro.
- Momento de liberación.
- Objetos que permanecen activos después del teardown.

No conservar instrumentación temporal innecesaria.

## 5. Implementación

Una vez identificada la causa:

- Aplicar la corrección mínima en los fixtures o helpers responsables.
- Garantizar que cada escenario establezca explícitamente sus dependencias.
- Mantener una única asociación válida de lector por contexto.
- Liberar recursos y suscripciones correctamente.
- Evitar que un test dependa del estado dejado por otro.
- Preservar los checks de ownership vigentes.

Preferir corregir la composición o el lifecycle de las pruebas antes que introducir mecanismos globales de limpieza.

No utilizar búsquedas globales ni destrucciones indiscriminadas para ocultar dependencias mal gestionadas.

**No modificar código productivo para resolver esta tarea.** Si la investigación demuestra un defecto de runtime, documentarlo como un bloqueo independiente y detener la modificación afectada.

No modificar las expectativas funcionales de los tres tests únicamente para hacerlos aprobar.

## 6. Alcance excluido

No intervenir en:

- Los cinco fallos EditMode restantes.
- Los demás fallos PlayMode.
- Quick Slots y perfiles persistentes.
- Setup de XP.
- Servicios de extracción por equipos.
- Presupuestos Fusion.
- Inicialización del inventario.
- Interacciones drag-and-drop.
- UI y HUD.
- Host Migration.
- Automatización semanal de releases.

No modificar escenas, prefabs, assets productivos ni documentación arquitectónica salvo autorización explícita.

## 7. Validación

### Validación específica

Verificar que:

- Los tres tests afectados aprueban individualmente.
- Aprueban juntos.
- Aprueban junto a los tests relacionados con runners.
- Los tres tests nuevos de composición continúan aprobando.
- No permanecen lectores registrados después del teardown.
- No aparecen dependencias entre ejecuciones consecutivas.

### Validación general

Después de las correcciones:

1. Verificar compilación limpia.
2. Ejecutar PlayMode completo, sin filtros.
3. Leer los resultados exactos desde los XML nativos.
4. Comparar los resultados con el baseline de 655 aprobados, 114 fallidos y 2 omitidos.
5. Ejecutar también EditMode completo si los cambios afectan helpers compartidos o composición utilizada por esa suite.
6. Revisar el diff completo.

No considerar suficiente que las pruebas pasen aisladamente: deben conservar el resultado dentro de la suite completa.

La comparación debe identificar los tests por nombre e identidad, no solo por cantidades agregadas.

### Control de regresiones

No aceptar la corrección si recupera los tres casos pero vuelve a introducir fallos por ausencia de `LocalInputContext` o expectativas obsoletas de extracción.

Si aparecen fallos nuevos, determinar si pertenecen al cambio antes de ampliar el alcance.

## 8. Criterios de aceptación

La tarea estará terminada cuando:

- Se haya demostrado la causa raíz de los tres fallos.
- Los tres tests recuperen su resultado aprobado dentro de la suite completa.
- Los helpers mantengan ownership y teardown correctos.
- Los tres tests de composición nuevos sigan pasando.
- Se conserven los avances de los bloques A, B y C.
- No existan nuevas regresiones atribuibles a la corrección.
- La compilación no presente errores.
- Los XML permitan comparar los resultados antes y después.
- No se haya modificado código productivo ni elementos excluidos.

Si se demuestra que los fallos son preexistentes o requieren cambios fuera del alcance, documentar la evidencia y dejar el resultado correspondiente bloqueado, sin forzar su aprobación.

No es necesario resolver los 114 fallos PlayMode restantes para terminar esta tarea.

## 9. Entrega

Presentar un informe breve con:

1. Identidad de los tres tests afectados.
2. Causa raíz demostrada.
3. Diferencia entre ejecución aislada y suite completa.
4. Archivos modificados.
5. Corrección implementada.
6. Validaciones ejecutadas.
7. Comparación de resultados XML.
8. Regresiones nuevas o pendientes, si existen.
9. Confirmación de que se preservaron los cambios locales previos.

No crear commits ni hacer push sin autorización explícita.

No activar releases ni modificar sus validaciones.

**Detener la implementación al completar este objetivo. No continuar automáticamente con otros grupos de fallos.**
L2 — 2026-10-20: Initial branch/HEAD confirmed; 46 dirty/untracked file hashes and prior test bytes preserved under validation artifact directory. Existing untracked MagicGarden art is unrelated and must remain untouched. RDD clone-local off; do not enable. Native Editor initially idle MainMenu with 0 providers/readers/contexts. Mapping found exact three XML regressions and candidate ExtractionProgress scene cleanup, not yet causal proof. No source edits, no commits.

L3 — 2026-10-20: Unmodified individual/group/cohort/full runs all passed targets; full650/119/2 shows timing sensitivity, not recovery. Controlled identical0.65s post-start waits: clean3/3; Hub+Gameplay+targets0/5 with all3 exact original LocalInputContext/FusionInputProvider stacks.409 live events show Town first registers then Gameplay competes for same context. Root cause demonstrated: ambient providers from retained scenes, not empty composition helper. Minimal correction owns additive Gameplay scene teardown only; existing Hub failure and Town lifecycle remain excluded. Risk:item4(test teardown async completion/unload ordering); independent verifier required. No functional assertions changed.

L4 — 2026-10-20: Identical diagnostic waits and group: without cleanup targets0/3; with cleanup3/3. Hub and Gameplay retain2 preexisting failures.512-event trace shows Gameplay absent before progression, only Town provider remains. Both temporarily instrumented progression files restored to protected initial hashes. Only final source delta is ExtractionProgressPlayModeTests; all prior assertions/body behavior retained, no runtime/assets/helper changes. Full validation and independent item4 verification pending.

L5 — 2026-10-20: Final native771=667Passed102Failed2Skipped; all3targets/all3compositionPassed; no added/missing identities. Userbaseline655/114/2 tofinal21recovered9newFailed; unmodifiedRED650/119/2 tofinal24recovered7newFailed. Every union-new failure precedes first changed fixture(index501), no newFailedafter. Original missing-context/stale-extractionfirsterrors0; B119present89pass30fail,C104present97pass7fail.12progressionentrieswithoutGameplay,12teardownsno registeredreaders/contexts.7/7Editfilesunchanged; fullEditnotapplicable/notrun.
L6 — Independent Risk:item4 verifier PASS; five-case3targetPass2knownexcludedFail; functionalassertionspreserved119to120onlycleanupassert; targetfixturesexactinitial. Found retained ReaderDiagnosticProbe despite staticnull; explicitly unregistered only that diagnostic callback6to5 standardcallbacks remain. No fixture listener/editor delegates remain. Full trace received appended independent events; use cutoff20:53:21Z for full-run analysis; nativeXMLunchanged.
L7 — Parent independently read nativeXML/hash, reviewed wholeGitdiff/indexempty. RestoredONLYowned font/buildsettings generated drift with currenthash/backuppreflight guards, minimapunchanged; fontreimportedcleanMainMenu/no source changes after final checks. Initial45/46dirtyfilesexact; one untracked MagicGardenFBX changed709324to739004bytes duringwork, provenanceunverified, leftuntouched to preserve concurrentwork. Onlynewcode:ExtractionProgressPlayModeTests29add3delete. Runtime/assets/architecture/releasesuntouchedfinal. No commits/push/worktrees. Stop implementation at requested objective; other102Play failures5Edit remain excluded.

L8 — 2026-10-20: User authorization (verbatim): perfecto crea el commit
Focused local commit scope: only Assets/Tests/PlayMode/ExtractionProgressPlayModeTests.cs and this task document. Exclude all pre-existing A/B/C recovery files, their helpers/task record, and MagicGarden artwork. No push. Previously verified source SHA remains unchanged; documentation authorization update requires structural readback only. The resulting commit identity is recorded in the Engram recovery mirror/session evidence because a document cannot contain the hash of its own commit.
