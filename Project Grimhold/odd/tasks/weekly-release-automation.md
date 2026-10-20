# Weekly release automation
Branch: feat/weekly-release-automation; baseline 1afb1e36a51d6e1cef949ac43a767aa4edff53d5. Local feature commit explicitly authorized; no pushes, worktrees or activation.
Delivery: one explicitly requested local implementation commit, including tests and docs; no PR or remote delivery. Forecast 1600 authored lines (advisory; coherent feature retained without cosmetic splitting). Runners: Node test runner, temporary Git repositories, Unity Editor required.

## Specs

### S1

## 1. Objetivo

Implementar un sistema completamente automático que todos los lunes por la mañana:

1. Examine los cambios de `New-Testing`.
2. Prepare una integración segura hacia `main`.
3. Ejecute las validaciones necesarias.
4. Integre los cambios solamente si las verificaciones pasan.
5. Determine automáticamente el siguiente número de versión utilizando la clasificación del changelog de desarrolladores.
6. Actualice la versión del proyecto Unity.
7. Genere los changelogs correspondientes.
8. Cree un tag Git y publique una GitHub Release.
9. Mantenga la versión visible en el menú principal del juego.
10. Informe el resultado y los errores en GitHub Actions.

El sistema debe funcionar sin intervención humana durante su operación normal.

**Principio fundamental: ante conflictos, errores o validaciones incompletas, cancelar la publicación de forma segura. Nunca forzar la integración para cumplir el calendario.**

### S2

## 2. Contexto y fuentes obligatorias

Repositorio: `Astro-Rift-Games/ProjectGrimhold`.

Rama de desarrollo: `New-Testing`.

Rama de publicación: `main`.

Antes de implementar:

- Leer `AGENTS.md` y `Project Grimhold/AGENTS.md`.
- Leer los documentos pertinentes de `Project Grimhold/Docs/Architecture/`.
- Inspeccionar código, escenas, prefabs, configuración y workflows actuales.
- Revisar la implementación existente de `tools/changelog/`.
- Revisar `.github/workflows/changelog.yml` y `changelog-release.yml`.
- Revisar `tools/changelog/README.md` y los tests existentes.
- Verificar la escena `Assets/Scenes/MainMenu.unity`, `MainMenuController.cs` y `ProjectSettings.asset`.
- Verificar permisos y restricciones de GitHub, así como las diferencias actuales entre ramas.

Usar el repositorio como fuente de verdad. No basarse en información histórica cuando los archivos actuales indiquen otra cosa.

### S3

## 3. Arquitectura del flujo semanal

Configurar GitHub Actions para ejecutar la automatización los lunes a las 09:15, utilizando `America/Argentina/Buenos_Aires`.

Agregar también `workflow_dispatch`, con soporte para una ejecución de simulación (`dry-run`) que no modifique recursos remotos.

El workflow programado debe existir en `main`, porque GitHub ejecuta los workflows programados desde la rama predeterminada.

La operación debe utilizar un único orquestador de publicación o una secuencia explícita y verificable. No depender de que un push o una Release creados con `GITHUB_TOKEN` disparen otros workflows.

Configurar concurrencia para evitar que se ejecuten simultáneamente dos publicaciones o que los workflows existentes modifiquen las mismas ramas y archivos de forma incompatible.

### Preparación de integración

El pipeline deberá:

1. Obtener los SHA actuales de `main` y `New-Testing`.
2. Comparar los cambios desde la última integración.
3. Determinar si existen cambios nuevos.
4. Preparar un merge real de `New-Testing` sobre `main` en un checkout descartable del runner, preservando los commits originales.
5. Detectar conflictos de forma anticipada.
6. Ejecutar las validaciones sobre el resultado candidato del merge.
7. Volver a verificar que las ramas remotas no cambiaron antes de publicar.

No utilizar `force push`, reescritura de historial, resolución automática arbitraria de conflictos ni eliminación de cambios ajenos.

### Comportamiento ante conflictos

Si aparece un conflicto:

- Cancelar el merge candidato.
- No actualizar `main`.
- No crear tags ni publicar releases.
- No incrementar la versión.
- Registrar los archivos afectados.
- Terminar con un resultado fallido y un diagnóstico accionable.

No modificar automáticamente código de gameplay para resolver conflictos.

### S4

## 4. Versionado automático

Implementar Semantic Versioning (SemVer), adaptado a la etapa de desarrollo del proyecto.

El número de versión debe calcularse mediante el mismo análisis de commits que utiliza `CHANGELOG_DEV.md`.

**No implementar un segundo parser independiente ni interpretar el Markdown renderizado como fuente de verdad.**

Política inicial:

| Cambio detectado | Incremento |
|---|---|
| `feat` | Minor |
| `fix`, `perf` | Patch |
| Breaking change | Major desde `1.0.0`; durante `0.x`, Minor |
| Solo `docs`, `test`, `chore`, `ci`, `refactor`, sin impacto funcional declarado | No generar una nueva Release |

Aplicar siempre el incremento de mayor prioridad encontrado en el período.

Excluir commits generados por el propio pipeline.

Respetar las reglas actuales de deduplicación y detección de commits revertidos.

### Reglas adicionales

- Determinar la versión anterior desde el último tag estable válido del historial de publicación.
- Definir y documentar el comportamiento de la primera versión cuando todavía no existan tags.
- No reutilizar versiones.
- No modificar tags existentes.
- No incrementar la versión cuando no corresponda.
- Las ejecuciones repetidas deben producir el mismo resultado.
- Diferenciar los cambios integrables de los cambios que justifican una nueva versión: puede existir un merge sin Release.

### S5

## 5. Integración con el changelog actual

Reutilizar `tools/changelog/` y conservar sus contratos públicos cuando no sea necesario modificarlos.

Mantener:

- `CHANGELOG_DEV.md`: historial técnico.
- `CHANGELOG.md`: notas destinadas a jugadores.
- `changelog-players.json`: datos estructurados para el juego.
- `Player-Added`, `Player-Improved`, `Player-Fixed` y `Player-Balanced`.
- `player-overrides.json`.
- Referencias a commits, exclusión de duplicados y validación estricta.

Modificar el proceso para que las versiones públicas se definan desde `main`, no desde `New-Testing`.

El changelog técnico debe seguir siendo útil durante el desarrollo y conservar la sección `Unreleased`.

No convertir automáticamente todos los `feat` en novedades públicas. El número de versión y la selección de notas para jugadores son dos decisiones independientes.

Cuando se publique una versión, los changelogs deben representar exactamente los cambios comprendidos entre las versiones correspondientes.

### S6

## 6. Versionado dentro de Unity

Implementar la visualización automática de la versión en el menú principal.

### Requisitos visuales

Agregar un texto discreto, preferentemente en una esquina inferior del menú.

Formato:

`v0.3.2`

Debe utilizar TextMeshPro y respetar la estructura visual existente.

El indicador:

- Debe permanecer visible mientras el usuario esté en el menú principal.
- No debe interferir con botones, login ni navegación.
- No debe introducir dependencias de networking.
- No debe depender de servicios externos.
- No debe tener un número hardcodeado.

### Requisitos técnicos

Utilizar `Application.version` como fuente para la versión que muestra el juego.

Investigar y utilizar el mecanismo mínimo necesario para actualizar automáticamente `PlayerSettings.bundleVersion` durante el proceso de publicación.

Preferir un componente de presentación independiente, sin agregar responsabilidades innecesarias a `MainMenuController`.

Preservar las referencias serializadas y los archivos `.meta`.

Verificar la escena y sus referencias con Unity MCP cuando esté disponible. No inventar asignaciones de Inspector.

El tag publicado, la versión de Unity y la versión de los changelogs deben coincidir.

Una build generada desde una versión publicada debe mostrar exactamente ese número.

### S7

## 7. Publicación automática de GitHub Releases

Después de superar las validaciones y confirmar que corresponde publicar:

1. Confirmar el commit definitivo que se incorporará a `main`.
2. Calcular y aplicar la versión de Unity.
3. Preparar y validar los changelogs.
4. Integrar en `main` sin reescribir historial.
5. Crear un tag estable `vX.Y.Z` apuntando al commit correcto.
6. Generar y publicar la GitHub Release.
7. Adjuntar `changelog-players.json`.
8. Actualizar los changelogs versionados según el contrato de publicación.
9. Confirmar la consistencia de todos los artefactos.

Resolver explícitamente el orden entre la creación del tag y los archivos generados: el JSON actual contiene un campo `commit`, por lo que no se debe intentar incluir el SHA definitivo de un commit dentro de su propio contenido versionado si eso genera una dependencia circular.

Documentar qué artefactos pertenecen al commit etiquetado y cuáles se generan posteriormente como archivos de publicación.

### Sin builds automáticas por ahora

No implementar la generación ni distribución automática del ejecutable del juego.

Una GitHub Release podrá contener notas y archivos del changelog sin adjuntar una build.

No afirmar que una versión es jugable o distribuible si solamente se validó su metadata y no una build real.

### S8

## 8. Seguridad e idempotencia

El sistema debe ser seguro frente a:

- Conflictos de merge.
- Fallos de compilación o pruebas.
- Cambios en las ramas durante la ejecución.
- Ejecuciones concurrentes.
- Reintentos del mismo workflow.
- Tags que ya existen.
- Releases ya publicadas.
- Fallos de red o permisos.
- Ejecuciones canceladas a mitad del proceso.
- Errores al generar notas para jugadores.
- Diferencias entre la versión del tag y Unity.

Establecer una secuencia de publicación con verificaciones antes de cada operación irreversible.

Distinguir explícitamente:

**Antes del primer push a `main`:** cancelar debe dejar ambas ramas remotas intactas.

**Después de integrar en `main`:** si falla la creación del tag o de la Release, no revertir automáticamente ni ejecutar force push. Registrar el estado parcial y permitir reanudar la publicación sobre el commit ya integrado, sin duplicar cambios ni versiones.

No presentar toda la operación como una transacción atómica si GitHub no garantiza esa atomicidad.

Definir cómo se sincronizan posteriormente los commits de publicación de `main` hacia `New-Testing` para evitar divergencia permanente, publicaciones duplicadas o conflictos semanales recurrentes. Esa sincronización debe ser segura y verificable.

### S9

## 9. Validaciones obligatorias

### Herramienta de changelog

- Ejecutar todas las pruebas existentes.
- Agregar pruebas para Semantic Versioning.
- Probar clasificación y prioridades.
- Probar primera versión y ausencia de cambios.
- Probar commits duplicados, revertidos y generados por el bot.
- Probar notas públicas incompletas o incorrectas.
- Verificar Markdown y JSON.
- Verificar generación determinista.

### Git y automatización

Probar mediante repositorios temporales o simulaciones:

- Merge exitoso.
- Merge con conflictos.
- Cambios simultáneos en las ramas.
- Push rechazado.
- Publicación parcial.
- Ejecución repetida.
- Tag existente.
- Release existente.
- Reanudación tras un fallo.
- Sin cambios desde la última versión.
- Cambios internos que no ameritan una nueva versión.

Validar YAML y scripts con herramientas apropiadas.

### Unity

Verificar:

- Compilación del proyecto.
- Pruebas automatizadas relevantes.
- Preservación de la escena `MainMenu`.
- Correcta referencia al componente de versión.
- Lectura de `Application.version`.
- Correspondencia entre la versión configurada y la mostrada.
- Ausencia de modificaciones serializadas ajenas al objetivo.

Si la compilación o las pruebas necesarias no pueden ejecutarse en GitHub Actions con la infraestructura disponible, identificar el requisito faltante y dejar la publicación bloqueada hasta resolverlo.

No convertir silenciosamente una validación obligatoria en opcional.

### S10

## 10. Preparación de la primera ejecución

Actualmente existe una diferencia importante entre `New-Testing` y `main`, y los workflows de changelog todavía no están instalados en ambas ramas.

Diseñar un procedimiento de inicialización para:

- Incorporar los componentes mínimos necesarios a la rama predeterminada.
- Verificar la compatibilidad de las herramientas.
- Probar el pipeline completo en modo `dry-run`.
- Evaluar la primera integración acumulada, que será considerablemente mayor que las semanales posteriores.
- Evitar que la primera ejecución programe una publicación accidental.

No utilizar el primer merge masivo como mecanismo improvisado para instalar la propia automatización.

La activación definitiva de la programación debe realizarse solamente después de validar el bootstrap.

### S11

## 11. Restricciones

- No modificar Game Design.
- No cambiar la arquitectura de gameplay.
- No agregar servicios externos de pago.
- No incorporar un framework nuevo de versionado si puede reutilizarse el generador existente.
- No duplicar los sistemas de changelog.
- No modificar escenas o prefabs no relacionados.
- No crear tareas duplicadas en sistemas de gestión.
- No crear worktrees de desarrollo sin autorización.
- No publicar releases de prueba en el repositorio oficial.
- No efectuar merges, pushes ni publicaciones reales durante esta tarea sin autorización explícita para esa operación.

La autorización para desarrollar la automatización no implica autorización para ejecutar su primera publicación real.

### S12

## 12. Organización del trabajo

Antes de implementar, preparar un plan breve que identifique resultados y dependencias.

Separar responsabilidades verificables:

- Adaptación del versionado y del generador de changelog.
- Automatización de integración y publicación.
- Indicador de versión en Unity.
- Validaciones, documentación y preparación del bootstrap.

El orquestador decide cuáles pueden avanzar en paralelo mediante subagentes y cuáles requieren ejecución secuencial.

Cada parte debe tener pruebas y entregables propios. No extender ninguna de ellas hacia sistemas ajenos al objetivo.

### S13

## 13. Criterios de aceptación

Considerar terminada la implementación cuando:

- Existe un workflow semanal para los lunes a las 09:15 de Argentina.
- Existe un modo manual de simulación sin escrituras remotas.
- El pipeline detecta conflictos antes de modificar `main`.
- Las validaciones obligatorias bloquean publicaciones defectuosas.
- La integración y el cálculo de versión utilizan el historial correcto.
- El versionado SemVer es automático, probado y determinista.
- La creación de tags y releases es idempotente.
- Los changelogs mantienen su separación técnica/pública.
- La versión de Unity coincide con la versión publicada.
- El menú principal muestra `Application.version`.
- Se gestionan correctamente reintentos, concurrencia y estados parciales.
- La documentación describe el funcionamiento, el bootstrap y la recuperación de fallos.
- Una simulación integral demuestra el flujo sin modificar las ramas oficiales.

No dar por validadas las operaciones remotas que todavía no se hayan ejecutado realmente.

### S14

## 14. Informe final

Entregar:

1. Resumen de la implementación.
2. Decisiones técnicas y justificación.
3. Archivos modificados.
4. Resultados de las pruebas.
5. Resultados del `dry-run`.
6. Operaciones de GitHub efectivamente verificadas.
7. Restricciones o permisos pendientes.
8. Procedimiento exacto para activar la primera ejecución real.

No crear commits, hacer push ni activar la publicación semanal sin autorización posterior.

## Tasks
- T1 | S4-S5,S9 | delegated (parallel independent SemVer + Unity units) | complete (117/117 suite) | local implementation commit authorized; shared parser, deterministic planned release and tests.
- T2 | S3,S7-S10,S13 | inline | locally verified; operational proof pending | local implementation commit authorized; recoverable orchestrator, inactive workflow, 14/14 local Git + 10/10 HTTP simulations; Risk items 2,4,5.
- T3 | S6,S9,S13 | delegated (independent presentation unit) | partial | local implementation commit authorized; Canvas wired, compilation/structural validator/focused PlayMode4/4passed after restart; full suites failed, visual/input acceptance pending.
- T4 | S2,S10-S14 | inline + delegated long Unity suites | blocked acceptance | local implementation commit authorized; Node141/141,differential6/6,independent2/2,YAML3/3,Bash10/10; full Unity EditMode4177/4177 andPlayMode768/768 completed butfailed; visual/operationalproof pending.

## Log
### L1 — Original request (2026-10-20)

# TASK — Automatizar integración semanal, versionado y publicación de Project Grimhold

## Rol

Actuá como orquestador técnico del proyecto Project Grimhold.

Tu responsabilidad es inspeccionar el estado actual, diseñar un plan de implementación seguro, decidir qué trabajo delegar a subagentes y verificar los resultados.

Podés crear y coordinar subagentes cuando lo consideres conveniente. La delegación y la estrategia de ejecución son responsabilidad del orquestador.

No comiences modificando archivos sin establecer primero los contratos y riesgos afectados.

## 1. Objetivo

Implementar un sistema completamente automático que todos los lunes por la mañana:

1. Examine los cambios de `New-Testing`.
2. Prepare una integración segura hacia `main`.
3. Ejecute las validaciones necesarias.
4. Integre los cambios solamente si las verificaciones pasan.
5. Determine automáticamente el siguiente número de versión utilizando la clasificación del changelog de desarrolladores.
6. Actualice la versión del proyecto Unity.
7. Genere los changelogs correspondientes.
8. Cree un tag Git y publique una GitHub Release.
9. Mantenga la versión visible en el menú principal del juego.
10. Informe el resultado y los errores en GitHub Actions.

El sistema debe funcionar sin intervención humana durante su operación normal.

**Principio fundamental: ante conflictos, errores o validaciones incompletas, cancelar la publicación de forma segura. Nunca forzar la integración para cumplir el calendario.**

## 2. Contexto y fuentes obligatorias

Repositorio: `Astro-Rift-Games/ProjectGrimhold`.

Rama de desarrollo: `New-Testing`.

Rama de publicación: `main`.

Antes de implementar:

- Leer `AGENTS.md` y `Project Grimhold/AGENTS.md`.
- Leer los documentos pertinentes de `Project Grimhold/Docs/Architecture/`.
- Inspeccionar código, escenas, prefabs, configuración y workflows actuales.
- Revisar la implementación existente de `tools/changelog/`.
- Revisar `.github/workflows/changelog.yml` y `changelog-release.yml`.
- Revisar `tools/changelog/README.md` y los tests existentes.
- Verificar la escena `Assets/Scenes/MainMenu.unity`, `MainMenuController.cs` y `ProjectSettings.asset`.
- Verificar permisos y restricciones de GitHub, así como las diferencias actuales entre ramas.

Usar el repositorio como fuente de verdad. No basarse en información histórica cuando los archivos actuales indiquen otra cosa.

## 3. Arquitectura del flujo semanal

Configurar GitHub Actions para ejecutar la automatización los lunes a las 09:15, utilizando `America/Argentina/Buenos_Aires`.

Agregar también `workflow_dispatch`, con soporte para una ejecución de simulación (`dry-run`) que no modifique recursos remotos.

El workflow programado debe existir en `main`, porque GitHub ejecuta los workflows programados desde la rama predeterminada.

La operación debe utilizar un único orquestador de publicación o una secuencia explícita y verificable. No depender de que un push o una Release creados con `GITHUB_TOKEN` disparen otros workflows.

Configurar concurrencia para evitar que se ejecuten simultáneamente dos publicaciones o que los workflows existentes modifiquen las mismas ramas y archivos de forma incompatible.

### Preparación de integración

El pipeline deberá:

1. Obtener los SHA actuales de `main` y `New-Testing`.
2. Comparar los cambios desde la última integración.
3. Determinar si existen cambios nuevos.
4. Preparar un merge real de `New-Testing` sobre `main` en un checkout descartable del runner, preservando los commits originales.
5. Detectar conflictos de forma anticipada.
6. Ejecutar las validaciones sobre el resultado candidato del merge.
7. Volver a verificar que las ramas remotas no cambiaron antes de publicar.

No utilizar `force push`, reescritura de historial, resolución automática arbitraria de conflictos ni eliminación de cambios ajenos.

### Comportamiento ante conflictos

Si aparece un conflicto:

- Cancelar el merge candidato.
- No actualizar `main`.
- No crear tags ni publicar releases.
- No incrementar la versión.
- Registrar los archivos afectados.
- Terminar con un resultado fallido y un diagnóstico accionable.

No modificar automáticamente código de gameplay para resolver conflictos.

## 4. Versionado automático

Implementar Semantic Versioning (SemVer), adaptado a la etapa de desarrollo del proyecto.

El número de versión debe calcularse mediante el mismo análisis de commits que utiliza `CHANGELOG_DEV.md`.

**No implementar un segundo parser independiente ni interpretar el Markdown renderizado como fuente de verdad.**

Política inicial:

| Cambio detectado | Incremento |
|---|---|
| `feat` | Minor |
| `fix`, `perf` | Patch |
| Breaking change | Major desde `1.0.0`; durante `0.x`, Minor |
| Solo `docs`, `test`, `chore`, `ci`, `refactor`, sin impacto funcional declarado | No generar una nueva Release |

Aplicar siempre el incremento de mayor prioridad encontrado en el período.

Excluir commits generados por el propio pipeline.

Respetar las reglas actuales de deduplicación y detección de commits revertidos.

### Reglas adicionales

- Determinar la versión anterior desde el último tag estable válido del historial de publicación.
- Definir y documentar el comportamiento de la primera versión cuando todavía no existan tags.
- No reutilizar versiones.
- No modificar tags existentes.
- No incrementar la versión cuando no corresponda.
- Las ejecuciones repetidas deben producir el mismo resultado.
- Diferenciar los cambios integrables de los cambios que justifican una nueva versión: puede existir un merge sin Release.

## 5. Integración con el changelog actual

Reutilizar `tools/changelog/` y conservar sus contratos públicos cuando no sea necesario modificarlos.

Mantener:

- `CHANGELOG_DEV.md`: historial técnico.
- `CHANGELOG.md`: notas destinadas a jugadores.
- `changelog-players.json`: datos estructurados para el juego.
- `Player-Added`, `Player-Improved`, `Player-Fixed` y `Player-Balanced`.
- `player-overrides.json`.
- Referencias a commits, exclusión de duplicados y validación estricta.

Modificar el proceso para que las versiones públicas se definan desde `main`, no desde `New-Testing`.

El changelog técnico debe seguir siendo útil durante el desarrollo y conservar la sección `Unreleased`.

No convertir automáticamente todos los `feat` en novedades públicas. El número de versión y la selección de notas para jugadores son dos decisiones independientes.

Cuando se publique una versión, los changelogs deben representar exactamente los cambios comprendidos entre las versiones correspondientes.

## 6. Versionado dentro de Unity

Implementar la visualización automática de la versión en el menú principal.

### Requisitos visuales

Agregar un texto discreto, preferentemente en una esquina inferior del menú.

Formato:

`v0.3.2`

Debe utilizar TextMeshPro y respetar la estructura visual existente.

El indicador:

- Debe permanecer visible mientras el usuario esté en el menú principal.
- No debe interferir con botones, login ni navegación.
- No debe introducir dependencias de networking.
- No debe depender de servicios externos.
- No debe tener un número hardcodeado.

### Requisitos técnicos

Utilizar `Application.version` como fuente para la versión que muestra el juego.

Investigar y utilizar el mecanismo mínimo necesario para actualizar automáticamente `PlayerSettings.bundleVersion` durante el proceso de publicación.

Preferir un componente de presentación independiente, sin agregar responsabilidades innecesarias a `MainMenuController`.

Preservar las referencias serializadas y los archivos `.meta`.

Verificar la escena y sus referencias con Unity MCP cuando esté disponible. No inventar asignaciones de Inspector.

El tag publicado, la versión de Unity y la versión de los changelogs deben coincidir.

Una build generada desde una versión publicada debe mostrar exactamente ese número.

## 7. Publicación automática de GitHub Releases

Después de superar las validaciones y confirmar que corresponde publicar:

1. Confirmar el commit definitivo que se incorporará a `main`.
2. Calcular y aplicar la versión de Unity.
3. Preparar y validar los changelogs.
4. Integrar en `main` sin reescribir historial.
5. Crear un tag estable `vX.Y.Z` apuntando al commit correcto.
6. Generar y publicar la GitHub Release.
7. Adjuntar `changelog-players.json`.
8. Actualizar los changelogs versionados según el contrato de publicación.
9. Confirmar la consistencia de todos los artefactos.

Resolver explícitamente el orden entre la creación del tag y los archivos generados: el JSON actual contiene un campo `commit`, por lo que no se debe intentar incluir el SHA definitivo de un commit dentro de su propio contenido versionado si eso genera una dependencia circular.

Documentar qué artefactos pertenecen al commit etiquetado y cuáles se generan posteriormente como archivos de publicación.

### Sin builds automáticas por ahora

No implementar la generación ni distribución automática del ejecutable del juego.

Una GitHub Release podrá contener notas y archivos del changelog sin adjuntar una build.

No afirmar que una versión es jugable o distribuible si solamente se validó su metadata y no una build real.

## 8. Seguridad e idempotencia

El sistema debe ser seguro frente a:

- Conflictos de merge.
- Fallos de compilación o pruebas.
- Cambios en las ramas durante la ejecución.
- Ejecuciones concurrentes.
- Reintentos del mismo workflow.
- Tags que ya existen.
- Releases ya publicadas.
- Fallos de red o permisos.
- Ejecuciones canceladas a mitad del proceso.
- Errores al generar notas para jugadores.
- Diferencias entre la versión del tag y Unity.

Establecer una secuencia de publicación con verificaciones antes de cada operación irreversible.

Distinguir explícitamente:

**Antes del primer push a `main`:** cancelar debe dejar ambas ramas remotas intactas.

**Después de integrar en `main`:** si falla la creación del tag o de la Release, no revertir automáticamente ni ejecutar force push. Registrar el estado parcial y permitir reanudar la publicación sobre el commit ya integrado, sin duplicar cambios ni versiones.

No presentar toda la operación como una transacción atómica si GitHub no garantiza esa atomicidad.

Definir cómo se sincronizan posteriormente los commits de publicación de `main` hacia `New-Testing` para evitar divergencia permanente, publicaciones duplicadas o conflictos semanales recurrentes. Esa sincronización debe ser segura y verificable.

## 9. Validaciones obligatorias

### Herramienta de changelog

- Ejecutar todas las pruebas existentes.
- Agregar pruebas para Semantic Versioning.
- Probar clasificación y prioridades.
- Probar primera versión y ausencia de cambios.
- Probar commits duplicados, revertidos y generados por el bot.
- Probar notas públicas incompletas o incorrectas.
- Verificar Markdown y JSON.
- Verificar generación determinista.

### Git y automatización

Probar mediante repositorios temporales o simulaciones:

- Merge exitoso.
- Merge con conflictos.
- Cambios simultáneos en las ramas.
- Push rechazado.
- Publicación parcial.
- Ejecución repetida.
- Tag existente.
- Release existente.
- Reanudación tras un fallo.
- Sin cambios desde la última versión.
- Cambios internos que no ameritan una nueva versión.

Validar YAML y scripts con herramientas apropiadas.

### Unity

Verificar:

- Compilación del proyecto.
- Pruebas automatizadas relevantes.
- Preservación de la escena `MainMenu`.
- Correcta referencia al componente de versión.
- Lectura de `Application.version`.
- Correspondencia entre la versión configurada y la mostrada.
- Ausencia de modificaciones serializadas ajenas al objetivo.

Si la compilación o las pruebas necesarias no pueden ejecutarse en GitHub Actions con la infraestructura disponible, identificar el requisito faltante y dejar la publicación bloqueada hasta resolverlo.

No convertir silenciosamente una validación obligatoria en opcional.

## 10. Preparación de la primera ejecución

Actualmente existe una diferencia importante entre `New-Testing` y `main`, y los workflows de changelog todavía no están instalados en ambas ramas.

Diseñar un procedimiento de inicialización para:

- Incorporar los componentes mínimos necesarios a la rama predeterminada.
- Verificar la compatibilidad de las herramientas.
- Probar el pipeline completo en modo `dry-run`.
- Evaluar la primera integración acumulada, que será considerablemente mayor que las semanales posteriores.
- Evitar que la primera ejecución programe una publicación accidental.

No utilizar el primer merge masivo como mecanismo improvisado para instalar la propia automatización.

La activación definitiva de la programación debe realizarse solamente después de validar el bootstrap.

## 11. Restricciones

- No modificar Game Design.
- No cambiar la arquitectura de gameplay.
- No agregar servicios externos de pago.
- No incorporar un framework nuevo de versionado si puede reutilizarse el generador existente.
- No duplicar los sistemas de changelog.
- No modificar escenas o prefabs no relacionados.
- No crear tareas duplicadas en sistemas de gestión.
- No crear worktrees de desarrollo sin autorización.
- No publicar releases de prueba en el repositorio oficial.
- No efectuar merges, pushes ni publicaciones reales durante esta tarea sin autorización explícita para esa operación.

La autorización para desarrollar la automatización no implica autorización para ejecutar su primera publicación real.

## 12. Organización del trabajo

Antes de implementar, preparar un plan breve que identifique resultados y dependencias.

Separar responsabilidades verificables:

- Adaptación del versionado y del generador de changelog.
- Automatización de integración y publicación.
- Indicador de versión en Unity.
- Validaciones, documentación y preparación del bootstrap.

El orquestador decide cuáles pueden avanzar en paralelo mediante subagentes y cuáles requieren ejecución secuencial.

Cada parte debe tener pruebas y entregables propios. No extender ninguna de ellas hacia sistemas ajenos al objetivo.

## 13. Criterios de aceptación

Considerar terminada la implementación cuando:

- Existe un workflow semanal para los lunes a las 09:15 de Argentina.
- Existe un modo manual de simulación sin escrituras remotas.
- El pipeline detecta conflictos antes de modificar `main`.
- Las validaciones obligatorias bloquean publicaciones defectuosas.
- La integración y el cálculo de versión utilizan el historial correcto.
- El versionado SemVer es automático, probado y determinista.
- La creación de tags y releases es idempotente.
- Los changelogs mantienen su separación técnica/pública.
- La versión de Unity coincide con la versión publicada.
- El menú principal muestra `Application.version`.
- Se gestionan correctamente reintentos, concurrencia y estados parciales.
- La documentación describe el funcionamiento, el bootstrap y la recuperación de fallos.
- Una simulación integral demuestra el flujo sin modificar las ramas oficiales.

No dar por validadas las operaciones remotas que todavía no se hayan ejecutado realmente.

## 14. Informe final

Entregar:

1. Resumen de la implementación.
2. Decisiones técnicas y justificación.
3. Archivos modificados.
4. Resultados de las pruebas.
5. Resultados del `dry-run`.
6. Operaciones de GitHub efectivamente verificadas.
7. Restricciones o permisos pendientes.
8. Procedimiento exacto para activar la primera ejecución real.

No crear commits, hacer push ni activar la publicación semanal sin autorización posterior.

### L2 — Initial evidence and plan
Clean New-Testing baseline verified. Connected GitHub confirms main default, 814 commits ahead / 0 behind. Existing changelog tests 88/88 green. No current Unity CI workflow or exposed Unity MCP. Automation remains inactive; mandatory unavailable proof blocks publication, not a silent skip. Temporary repos for candidate/simulations only. Existing generated contracts preserved; no new framework. No artifact publication is authorized in this session.

### L3 — Permission clarification (2026-10-20)
Original YAML save was rejected by auto-review. Asked explicit local-only YAML authorization; user replied verbatim: "si autorizo". Saved three workflows locally without changing or inspecting remote activation variables; no remote config/write/run. T1 worker observed RED then 117/117 GREEN. Parent observed weekly simulations RED then 14/14 GREEN and HTTP mocks 10/10 GREEN. RDD clone-local OFF remains OFF; informational assessment was unassessable due to undeclared untracked inventory, risk remains high (items 2,4,5). Mandatory Unity/editor/scene and full operational dry-run remain unavailable.

### L4 — Independent verification and delivery boundary (2026-10-20)
Frozen-copy full suite141/141,0skipped,68.62s; legacy differential6/6 identical; independent rejected-validation and late-development recovery probes2/2. Three workflows actionlint1.7.12 valid, parent rerun passed;10Bash snippets pass bash -n; ShellCheck unavailable.27frozen hashes unchanged; no scene/prefab edits. Report: Project Grimhold/Docs/WeeklyReleaseImplementationReport.md. Needs your decision: supply accessible Unity Editor/MCP for actual menu wiring, compilation and tests, then provision licensed backend-equipped runner/protection/token proof before operational bootstrap. No further remote/activation authorization granted. All code remains uncommitted on original SHA.

### L5 — Unity MCP available and bounded follow-up (2026-10-20)
User verbatim: "ya inicie el server de unity mcp". Verified local MCP3.4.10/project path/Unity6000.5.1f1. Native adapter retained startup failure; used authorized direct local MCP protocol. MainMenu clean and initial validator failed label0; related Canvas prefab now has one lower-right alagard22 label with real reference, no raycast, GUID unchanged. ValidateMainMenu passed; source scene and ProjectSettings unchanged. Added152prefab lines only; normalized4new blank-value spaces.
Two first focused EditMode attempts ran0tests; moved new tests to Editor preserving GUID, then4actualEditMode cases failed because ordinary MonoBehaviour OnEnable does not execute there. Correct lifecycle mode is PlayMode; moved tests to Assets/Tests/PlayMode/ApplicationVersionLabelTests.cs, originalGUID retained, initialized TMP before inactive presenter setup. Weekly XML gate now requires4passed PlayMode cases. PlayMode attempts06e8... and716d... failed initialization with0executed. Actual workspace Logs/Editor.log/Bee .rsp retain old Editor test path (CS2001) despite correct AssetDatabase GUID mapping. Canceled only verified native run28f..., cleanup restored clean MainMenu. Forced refresh then ONE official CleanBuildCache request; fresh log stillCS2001. No more retries/cache deletion. Need user to restartUnity+MCP before compilation/tests can complete. Preview-clone callback text matchedv1.0/raycastfalse, but rendered image blank, therefore NO visual proof claimed. Publication remains disabled; no external writes/commits.


### L6 — Final delta and scope correction (2026-10-20)
Final independent frozen automation suite after PlayMode XML gate update passed141/141,0failed/skipped/cancelled,60.40s. TMP preview serialized unrelated shared alagard SDF font tables; restored only that confirmed task-generated noise to original baseline. One targeted independent correction recheck passed: font baseline/current blob dbddeefff3c73e23ac4652a0ace960288766fb99 identical; only intended MainMenu Canvas prefab remains as tracked Unity asset/settings change. MCP connection refused during dirty-clear/import attempts, so in-memory reload not claimed. No further tests, cache recovery or full review. Documentation now reflects existing wiring and failed Unity proof, not initial unavailable-MCP state. Need Unity+MCP restart, clean compilation,4PlayModecases,fullsuites,visual/input acceptance and operational runner dry-run before acceptance; no publication/activation/commits authorized.


### L7 — User restart and actual complete-suite results (2026-10-20)
User verbatim: "ya reinicie unity y el mcp". Verified same Unity6000.5.1f1/project, fresh Tundra compilation success, zero initial Console errors, correct PlayMode GUID path, MainMenu clean, font not dirty, live ValidateMainMenu passed. Focused PlayMode job3379653728514407b11ee406f50f084f passed4/4,0failed/skipped,0.057635s.
Delegated long suites per per-action rule to weekly_release_verify, once each sequentially, no repairs/retries: EditMode job10f8efe57a0d4e8893b127808e9547b3 completed4177/4177failed194.420s; PlayMode job2a8be88f82604a68bb9b482761db707e completed768/768failed144.139s. Both MCP terminal result null and failures_capped true with25diagnostics; exact pass/fail/skip totals unknown. Parent cached PlayMode readback matched. Example EditMode MerchantShopUIBoundaryTests footer expectednonnull gotnull; PlayMode BackendErrorUtilityTests invalid Category characters. No baseline comparison, causality not established.
Postrun Editor idle, native not active, MainMenu clean, font not dirty; no additional tracked serialized noise. Evidence visualizations/session/weekly-release-validation/unity-restart. Need decision on expanding scope to general-suite investigation/corrections; publication stays blocked, visual/operational proof still pending. No source edits or delivery.


### L8 — Local feature branch and commit authorization (2026-10-20)
User verbatim: "ok crea una nueva branch para esta feature, hace el commit de la implementacion y despues revisamos los errores".
Authorized only a new feature branch and local commit of the existing implementation; no push, remote merge, tag, Release or activation. Selected feat/weekly-release-automation from New-Testing baseline. One coherent implementation commit includes release tooling, workflows, Unity presentation, tests and documentation; general-suite remediation remains deferred. Commit identity is recorded by Git and in the Engram delivery record, avoiding a self-referential commit hash in this file. Overall acceptance remains partial: complete Unity suites failed, visual/input and operational runner dry-run pending.
