# Nine PlayMode result transitions
Objective: classify nine Passed-to-Failed identities and correct only demonstrated fixture/infrastructure causes. Branch feat/unity-obsolete-tests, baseline HEAD 6f79b2e657c52eb35a1855c0ede656f318ed37e4. Focused local commit explicitly authorized on2026-10-21; no push or worktrees.
Runner: Unity MCP, exclusive sequential ownership. Route: delegated read-only mapping/long validation; inline justified fixture corrections. Delivery: focused local commit authorized; overall acceptance remains partial. Forecast: <=300 authored source lines, pending causality; no PR.

## Specs
S1. "Antes de ejecutar pruebas nuevas, comparar los dos resultados completos." "Confirmar que los nueve casos corresponden efectivamente a transiciones `Passed → Failed`."
S2. "No asumir que los nueve tests son regresiones ni que todos comparten una causa." "Comparar su ejecución aislada con la ejecución conjunta." "Junto con sus posibles tests predecesores."
S3. "Implementar solamente después de demostrar una causa raíz." "Preferir cambios locales y específicos." "No modificar expectativas funcionales para ocultar defectos." "No introducir esperas arbitrarias ni retrasos fijos como sustituto de una condición verificable de finalización."
S4. "Si un caso demuestra un defecto de runtime o un contrato funcional incumplido, documentarlo y dejarlo fuera de la implementación actual." "Si un test resulta ser un fallo preexistente independiente, registrarlo sin forzar su corrección."
S5. "Después de cualquier corrección:" "Verificar compilación de Unity." "Ejecutar los tests modificados individualmente." "Ejecutarlos junto con sus dependencias identificadas." "Verificar los tests recuperados en la iteración anterior." "Ejecutar PlayMode completo sin filtros." "Leer los resultados exactos del XML nativo." "Comparar nuevamente las identidades y transiciones." "Revisar el diff completo."
S6. "No reaparecieron los 119 fallos originales por ausencia de `LocalInputContext`." "No reaparecieron los 104 fallos originales por expectativas antiguas de extracción." "Los tres tests de composición siguen aprobados." "El teardown corregido de `ExtractionProgressPlayModeTests` mantiene su comportamiento." "No existen nuevas regresiones atribuibles a esta tarea."
S7. "No crear commits ni hacer push." Local-commit prohibition superseded by the explicit2026-10-21 request recorded inL10; push remains unauthorized. "No crear worktrees sin autorización." "Los cambios locales anteriores permanezcan intactos." "No ejecutar EditMode completo salvo que los cambios afecten componentes compartidos con esa suite."
S8. "Si alguna causa continúa indeterminada, explicar qué evidencia falta para resolverla." "Al finalizar, detenerse. No continuar automáticamente con Quick Slots, XP, Fusion, inventario u otros grupos de fallos."

## Tasks
- T1 | S1 | delegated read-only mapping (evidence budget) | complete | exact771 common;9 P->F;21 F->P;93 F->F (15 message changes);2 skipped unchanged. Included in the authorized focused local commit; hash recorded in post-commit evidence.
- T2 | S2,S4,S8 | delegated sequential long focused reproduction; parent causal inspection | partial: two isolated causes confirmed, seven historical causes indeterminate | nine individual9/9, group9/9, predecessor cohort18/18; controlled Hub RED targets7/9 -> GREEN9/9 proves two isolation cases. Other seven historical causes unproven. Included in the authorized focused local commit; hash recorded in post-commit evidence.
- T3 | S3,S4,S7 | inline demonstrated Hub-owned lifecycle correction | complete implementation; acceptance blocked by final S6 proof | Risk: item4 (async startup/shutdown and scene-unload ordering). Included in the authorized focused local commit; hash recorded in post-commit evidence.
- T4 | S5-S8 | independent item4 verifier + one final unfiltered full suite | partial: Needs your decision | final662/107/2; original9/9+sixguards6/6;38newUIsetupP->F attribution unresolved; no extra verification loop. Included in the authorized focused local commit; hash recorded in post-commit evidence.

## Log
L1 — Original user request (verbatim):
# TASK — Investigar y estabilizar los nueve fallos nuevos de PlayMode

## Rol

Actuá como orquestador técnico de Project Grimhold.

Planificá, delegá en subagentes cuando resulte conveniente, implementá las correcciones justificadas y verificá los resultados.

Usá High effort cuando la complejidad lo requiera. Priorizá evidencia reproducible y modificaciones mínimas.

No intentes resolver todos los fallos restantes de PlayMode.

## 1. Objetivo

Investigar los **nueve tests que aparecen como fallidos respecto del baseline anterior** y determinar si representan:

- Regresiones introducidas por las correcciones recientes.
- Contaminación entre tests o problemas de aislamiento.
- Fallos intermitentes dependientes del orden de ejecución.
- Errores preexistentes que estaban ocultos.
- Diferencias legítimas de comportamiento que necesitan otra investigación.

Corregir exclusivamente las causas confirmadas que pertenezcan a la infraestructura o los fixtures de pruebas dentro de este alcance.

**No asumir que los nueve tests son regresiones ni que todos comparten una causa.**

## 2. Contexto

Repositorio: `Astro-Rift-Games/ProjectGrimhold`.

Rama: `feat/unity-obsolete-tests`.

Último commit remoto verificado: `6305da47`.

La rama de trabajo contiene modificaciones locales posteriores todavía sin commit.

### Resultados de referencia

| PlayMode completo | Aprobados | Fallidos | Omitidos |
|---|---:|---:|---:|
| Antes de corregir aislamiento | 655 | 114 | 2 |
| Después de corregir aislamiento | 667 | 102 | 2 |

La última corrección identificó que una escena `Gameplay` quedaba cargada después de un test fallido y conservaba lectores locales que interferían con Town.

Se corrigió únicamente el teardown de `ExtractionProgressPlayModeTests.cs`.

Los tres tests originalmente afectados:

- Pasan individualmente.
- Pasan juntos.
- Pasan dentro de PlayMode completo.

Los tres tests nuevos de composición también siguen aprobando.

El informe señala nueve casos que fallaron en la ejecución final pese a haber aprobado anteriormente. Sus identidades y resultados deben recuperarse de los XML nativos.

## 3. Fuentes obligatorias

Antes de actuar:

1. Leer `AGENTS.md`.
2. Leer `Project Grimhold/AGENTS.md`.
3. Leer las skills aplicables del repositorio.
4. Leer los contratos pertinentes de `Docs/Architecture/`.
5. Inspeccionar los cambios locales recientes.
6. Consultar los XML completos de las dos ejecuciones.
7. Revisar el informe anterior de aislamiento de lectores.

Documentos relevantes:

- `SessionLifecycleAndSpawning.md`.
- `RaidInventoryUIArchitecture.md`.
- Otros contratos únicamente cuando un fallo concreto lo requiera.

Usar CodeGraph para descubrir dependencias compartidas cuando resulte útil y verificar los hallazgos en el código real.

Utilizar Unity MCP para las validaciones disponibles.

## 4. Preservación del estado local

Antes de modificar archivos:

- Confirmar rama, HEAD y working tree.
- Identificar todos los cambios locales existentes.
- Separar los archivos de esta tarea de los cambios acumulados anteriormente.
- Preservar los fixtures ya corregidos.
- No sobrescribir archivos ajenos.
- No ejecutar restauraciones masivas ni `git clean`.
- No crear worktrees sin autorización.

El FBX de MagicGarden mencionado en la sesión anterior es ajeno a esta tarea. No modificarlo, restaurarlo ni incorporarlo a los cambios de Project Grimhold.

No crear commits ni hacer push.

## 5. Fase A — Comparación exacta de XML

Antes de ejecutar pruebas nuevas, comparar los dos resultados completos.

Identificar cada test mediante su nombre completo y su identidad estable, no mediante la posición en el XML.

Generar una tabla de transición:

| Test | Resultado anterior | Resultado final | Primer fallo observado |
|---|---|---|---|
| Nombre completo | Passed | Failed | Mensaje y stack trace |

Identificar también las transiciones inversas:

- Failed → Passed.
- Failed → Failed con error diferente.
- Passed → Failed.
- Skipped → otro resultado.

Distinguir específicamente entre:

- Fallos realmente nuevos.
- Fallos que simplemente cambiaron de causa.
- Fallos antes ocultos por errores de setup.
- Cambios de identidad o descubrimiento de tests.

Confirmar que los nueve casos corresponden efectivamente a transiciones `Passed → Failed`.

No interpretar una diferencia agregada de contadores como prueba de regresión.

## 6. Fase B — Investigación de causalidad

Analizar los nueve casos identificados.

Para cada uno:

1. Localizar su fixture y setup.
2. Identificar el contrato funcional que protege.
3. Leer el mensaje de error completo y stack trace.
4. Determinar qué objetos, escenas, runners o servicios utiliza.
5. Revisar si comparte dependencias con los fixtures modificados recientemente.
6. Comparar su ejecución aislada con la ejecución conjunta.
7. Investigar el orden de ejecución cuando exista evidencia de dependencia temporal.

Prestar especial atención a:

- Escenas aditivas que permanecen cargadas.
- Runners Fusion no destruidos correctamente.
- Operaciones de shutdown asíncronas.
- Objetos `DontDestroyOnLoad`.
- `LocalInputContext` y `PlayerInputReader`.
- Registros duplicados.
- Suscripciones no liberadas.
- Estado estático entre tests.
- Objetos creados por fixtures anteriores.
- Operaciones de teardown que finalizan después de comenzar otro test.

Estos puntos son hipótesis de investigación, no causas confirmadas.

### Validación de aislamiento

Ejecutar los tests afectados:

- Individualmente.
- Como grupo.
- Junto con sus posibles tests predecesores.

Cuando sea necesario, reproducir el orden exacto de la suite completa.

Evitar repetir suites completas indiscriminadamente. Hacer primero pruebas focalizadas y ejecutar la suite completa al final.

## 7. Fase C — Correcciones justificadas

Implementar solamente después de demostrar una causa raíz.

Las correcciones permitidas incluyen:

- Completar teardown de fixtures.
- Esperar correctamente operaciones asíncronas de limpieza.
- Descargar escenas creadas por el propio test.
- Liberar dependencias y suscripciones del fixture.
- Corregir ownership de objetos creados durante la prueba.
- Eliminar dependencias accidentales del orden de ejecución.

Preferir cambios locales y específicos.

No agregar un sistema global de limpieza ni destruir indiscriminadamente objetos ajenos.

No modificar expectativas funcionales para ocultar defectos.

No introducir esperas arbitrarias ni retrasos fijos como sustituto de una condición verificable de finalización.

Si un caso demuestra un defecto de runtime o un contrato funcional incumplido, documentarlo y dejarlo fuera de la implementación actual.

Si un test resulta ser un fallo preexistente independiente, registrarlo sin forzar su corrección.

## 8. Exclusiones

No incluir:

- Los otros fallos PlayMode no relacionados.
- Los cinco fallos EditMode pendientes.
- El ACK de persistencia de XP.
- Presupuestos Fusion.
- Inicialización del inventario.
- Cambios de comportamiento drag-and-drop.
- Barra de experiencia del HUD.
- Cambios de arquitectura de networking.
- Correcciones de gameplay.
- Cambios de prefabs o escenas productivas.
- Automatización semanal de releases.

No debilitar los tests ni modificar las condiciones de aprobación de CI.

## 9. Validación final

Después de cualquier corrección:

1. Verificar compilación de Unity.
2. Ejecutar los tests modificados individualmente.
3. Ejecutarlos junto con sus dependencias identificadas.
4. Verificar los tests recuperados en la iteración anterior.
5. Ejecutar PlayMode completo sin filtros.
6. Leer los resultados exactos del XML nativo.
7. Comparar nuevamente las identidades y transiciones.
8. Revisar el diff completo.

Confirmar que:

- No reaparecieron los 119 fallos originales por ausencia de `LocalInputContext`.
- No reaparecieron los 104 fallos originales por expectativas antiguas de extracción.
- Los tres tests de composición siguen aprobados.
- El teardown corregido de `ExtractionProgressPlayModeTests` mantiene su comportamiento.
- No existen nuevas regresiones atribuibles a esta tarea.

No ejecutar EditMode completo salvo que los cambios afecten componentes compartidos con esa suite. En ese caso, justificarlo y ejecutarlo.

No ocultar fallos ni omisiones.

## 10. Criterios de aceptación

La tarea estará completada cuando:

- Los nueve cambios de resultado estén identificados exactamente.
- Cada uno tenga una clasificación sustentada en evidencia.
- Las causas de aislamiento o fixtures incluidas en el alcance estén corregidas.
- Los casos corregidos aprueben aislados y dentro de la suite completa.
- No se introduzcan nuevas regresiones atribuibles al trabajo.
- La compilación esté limpia.
- Se conserve la cobertura funcional existente.
- Los cambios locales anteriores permanezcan intactos.
- Exista un informe final basado en los XML completos.

No exigir que los 102 fallos totales desaparezcan.

Tampoco exigir que los nueve casos aprueben si se demuestra que algunos corresponden a defectos reales o independientes fuera del alcance.

## 11. Entregable

Presentar:

1. Tabla de las nueve transiciones `Passed → Failed`.
2. Causa raíz demostrada de cada caso.
3. Clasificación: regresión, aislamiento, fallo preexistente, defecto productivo o indeterminado.
4. Correcciones implementadas.
5. Archivos modificados.
6. Pruebas focalizadas ejecutadas.
7. Comparación completa de PlayMode antes y después.
8. Regresiones y fallos pendientes.
9. Confirmación de preservación del working tree.

Si alguna causa continúa indeterminada, explicar qué evidencia falta para resolverla.

**No crear commits ni hacer push sin autorización.**

Al finalizar, detenerse. No continuar automáticamente con Quick Slots, XP, Fusion, inventario u otros grupos de fallos.
L2 — Phase A independently spot-checked: exact original8EAC0C.. and final4ACE18.. XMLs;771 common,9P->F,21F->P,15of93F->F first messages differ. HEAD differs from request context: current local6f79b2e6 is prior approved reader-isolation commit.48initial files protected. RDD clone-local off preserved.
L3 — Focused validation:9 individual native runs passed, exactgroup9/9, immediate-predecessorcohort18/18; parent repeatedgroup9/9. All nine causes remain indeterminate; no fixture correction justified. One observational full run planned, no arbitrary delays and no full-suite repetition loop.

L4 — One observational full run native2082B970..:771=669/100/2; originalnine5pass4fail; all6priorguardsPASS. Hub starts clean and leavesLobby-Town/provider; provider binds foreigncombatrunner, syntheticAttackHeld true766frames with consumedhistory0. Frame-observation limitation: dynamicFusiontickactor rejected bytype lookup; outside-tickinputAPIunavailable, not evidenceofpayloadabsence. No source writes. ControlledHub+9comparison launched before deciding correction.

L5 — Controlled unchangedHub+9 RED native3D06653E..:7pass3fail=Hubexistingbinder+originalFreshPlayer+DownedRecovery; cleancontrols9/9. FreshPlayer syntheticheld763frames/consumedhistory0, foreignTownproviderbound763frames. Proven infrastructure scene retention; do not generalize allninecauses. Inline Hub fixture-only ownership cleanup added, no body expectation change. Wait startup/shutdown, retain ownlauncher across Singleload, capture newTownScene handle excluding preexisting. Restore empty TestRunner scene only whenTownislastScene; reloading savedbootstrap would spawn duplicatePlaymodeTestsController (verified installedUTF1.7CreateBootstrapSceneTask source). No productionchanges. Risk:item4 requires independent verifier after ownchecks.

L6 — Initialcandidatecompilefailed4errors(SceneHandleimplicitintobsolete2; NUnit/FusionAssertambiguous2). CorrectedHashSet<Scene>+Assertalias beforeverification. Parent mistakenlylaunchedHubjob8a14a52201f9433f95403646b46e6799 despitecompileerrors;0testsexecuted,EditorstalledbeforePlayMode. Boundedownedjobcancellation/nativeidlegreenrecoverydelegated; no validGREENclaimforthatjob. No externalstate/reset/sourcepackageeditsauthorized.

L7 — Onlyownednativejobc55341ab-9238-49db-998e-a0239af724b4 cancelledaftermatchingHubfilter/start; liveTestRunnerApi m_testJobDataHolder isPROPERTY notfield. No tests executed by stalledjob. CorrectedHubSHA62E49628037D580238BF248B771A3AB9CFC232D2E1898F6C870ABFEBCFEE852F compiledclean. Hubindividual0/1 expectedbinderfailure withcleanupverified; sameHub+9GREEN9/1(Hubonly), all9targetsPass;0foreignTowninputs/listeners atalltargetstarts. Finalobservercleanupzero and MainMenurestored; finalfull+independentRiskitem4verificationpending.

L8 — Independentfinalnative13A6EE68..:771=662/107/2, original9/9+sixguards6/6. All771identities retained2skips unchanged. Vs667:33F->P,38P->F(allunchangedRaidInventoryPresenterInteractionTests SetupAddStateChangeMonitorIndexOutOfRange),69F->F7messagechanges. Signature39total0in3priorfullXMLs; no baseline/candidatecausalityproof. Independentverdictpartial; no newcausalblockerproven, S6acceptanceblocked. TwohistoricalcasesconfirmedHubisolation; sevenremainindeterminate. Fullsreportsindependent-final/report.md+delivery-report.md. No UIimplementation/extra fullsuites.
L9 — Parentpreservationfinal48/48initialfilesexact,3/3protectedassets restoredexactwithpath/hashguards(fontgeneratedglyphs,BuildSettingslineendings);MagicGardenuntouched. HubSHA62E496..;ExtractionSHA7BAE73..exact;branchHEAD6f79b2e6,indexempty,diffcheckPASS. EndMainMenuCS0noownedcallbacks/runners/readers/providers/listeners. WorkerScratchalreadyabsent, no delete. Needsyourdecision: authorize bounded baseline/candidateUIsetupcausalcomparisonbeforeaccepting this uncommitted correction; no automatic adjacentwork.

L10 — 2026-10-21 user authorization (verbatim):
perefecto crea el commit
Commit scope: onlyAssets/Tests/PlayMode/HubSessionLauncherTests.cs and this feature record. The38UIsetup transitions remain causally unresolved; commit authorization does not authorize UI investigation or change acceptance to complete. Preserve all current unrelated work, including newly observedfontasset drift and MagicGarden. No push. Test source is byte-identical to the verified62E49628037D580238BF248B771A3AB9CFC232D2E1898F6C870ABFEBCFEE852F candidate; no Unity rerun required for commit-only operation.
