# Rule Tile Builder visual assignment
Branch: Ruletile-tool, baseline d5e8cc37. Local edits only, no commits or push. Route: inline implementation after bounded read-only map.
Runner: Unity MCP EditMode; forecast ~850 authored changed lines, one coherent assignment behavior. Delivery: local diff only (no PR/commit slicing).

## Specs
### S1
## Objetivo

Rediseñar el paso **3. Assign pieces** de Rule Tile Builder para que un usuario sin conocimientos de autotiling pueda identificar fácilmente dónde debe asignar cada sprite.

**Problema actual:** las tarjetas con matrices 3×3 representan correctamente las conexiones, pero resulta difícil comprender qué posición del escenario corresponde a cada regla.

La solución debe permitir **asignar sprites directamente sobre ejemplos visuales de escenarios**, sin interpretar manualmente matrices de vecindad.


### S2
## Contexto

- Repositorio: `Astro-Rift-Games/ProjectGrimhold`.
- Rama de trabajo: `Ruletile-tool`.
- Implementación existente: `Assets/Editor/RuleTileBuilder/`.
- Leer `AGENTS.md` y las instrucciones de Unity antes de modificar archivos.
- Revisar la implementación actual y reutilizar sus componentes.

No reconstruir la herramienta. Esta tarea se limita a mejorar la experiencia de asignación visual y sus integraciones necesarias.


### S3
## 1. Editor visual de asignación

Reemplazar la presentación principal de tarjetas por un **escenario interactivo** que represente las conexiones de la plantilla seleccionada.

Flujo esperado:

1. El usuario selecciona uno o varios sprites en la biblioteca.
2. Selecciona una posición del escenario de ejemplo.
3. La herramienta identifica la regla correspondiente.
4. Muestra el nombre y la función de esa pieza.
5. Permite asignar los sprites seleccionados mediante clic o drag & drop.
6. Actualiza inmediatamente el escenario y la previsualización.

No exigir conocimientos de `This`, `Not This`, `Don't Care` ni matrices 3×3.

### Representación visual

- Mostrar las posiciones utilizando sprites reales cuando estén asignados.
- Distinguir claramente posiciones asignadas, pendientes y seleccionadas.
- Resaltar todas las posiciones que representan la misma regla.
- Mostrar el sprite de respaldo donde no exista una asignación.
- Identificar la pieza seleccionada mediante nombres comprensibles: centro, borde superior, esquina interior, extremo, unión, etc.
- Permitir reemplazar o eliminar una asignación.
- Conservar el soporte para múltiples sprites y variantes.


### S4
## 2. Escenarios de ejemplo

Una habitación rectangular no representa las 16 o 47 configuraciones posibles.

Incorporar ejemplos complementarios que permitan acceder visualmente a todas las reglas de las plantillas existentes:

- Habitación rectangular.
- Pasillos horizontales y verticales.
- Esquinas interiores y exteriores.
- Habitaciones con huecos.
- Intersecciones y uniones.
- Extremos y piezas aisladas.

Cada posición editable debe corresponder inequívocamente a una regla de la plantilla.

Garantizar que todas las configuraciones puedan seleccionarse mediante los ejemplos disponibles. Si alguna combinación no puede representarse claramente en escenarios convencionales, utilizar un ejemplo específico.

El usuario no debe necesitar recorrer una lista de 47 matrices para encontrar una pieza.


### S5
## 3. Interacción y feedback

Al seleccionar una posición:

- Resaltarla visualmente.
- Mostrar su función y sus conexiones relevantes.
- Indicar qué sprite tiene asignado.
- Permitir asignar o reemplazar sprites.
- Actualizar todas las posiciones equivalentes.
- Refrescar la previsualización nativa.

Separar claramente dos conceptos:

- **Asignación:** el escenario ilustrativo permite identificar y configurar las reglas.
- **Prueba:** el Tilemap nativo permite pintar libremente y comprobar el resultado real.

No presentar una simulación de asignación como si fuera el resultado nativo del RuleTile.

La interfaz debe conservar una buena legibilidad con tiles pequeños de pixel art, utilizando escalado apropiado y sin suavizado visual innecesario.


### S6
## 4. Compatibilidad

Reutilizar:

- `RuleTileBuilderTemplates`
- `RuleTileBuilderValidation`
- `RuleTileBuilderPreview`
- `RuleTileBuilderAssets`
- `RuleTileBuilderSpriteCatalog`

Mantener el `RuleTile` nativo como único modelo del borrador.

Preservar:

- Las tres plantillas existentes.
- Las reglas personalizadas.
- El panel Advanced.
- Single, Random y Animation.
- Undo/Redo.
- Guardado seguro y conservación de GUID.
- Advertencias y validaciones.
- La compatibilidad con el Inspector y Tile Palette nativos.

No inferir automáticamente el significado de los sprites ni inventar geometría. No aplicar rotaciones o reflejos automáticos.


### S7
## 5. Validación

Agregar tests EditMode que comprueben:

- Correspondencia correcta entre posición visual y regla nativa.
- Cobertura de las 16 y 47 configuraciones.
- Asignación y reemplazo de sprites.
- Actualización de todas las posiciones equivalentes.
- Persistencia de las asignaciones.
- Undo/Redo.
- Ausencia de modificaciones en el RuleTile original antes de guardar.

Validar mediante Unity MCP la compilación, los tests y la interacción real de la ventana cuando las herramientas disponibles lo permitan.

Realizar una prueba con Catacombs sin modificar sus sprites originales.


### S8
## Fuera de alcance

- Reescribir el generador de Rule Tiles.
- Crear un sistema nuevo de autotiling.
- Modificar escenas o Tilemaps existentes.
- Cambiar importadores o spritesheets.
- Implementar detección automática de piezas.
- Preparar la herramienta para Asset Store.
- Refactorizaciones ajenas a la asignación visual.


### S9
## Criterios de aceptación

1. Un usuario principiante puede identificar dónde asignar cada sprite sin comprender matrices de vecindad.
2. Puede configurar las conexiones mediante ejemplos visuales.
3. Todas las reglas de las plantillas existentes son accesibles.
4. Las asignaciones se reflejan inmediatamente en los ejemplos y en la previsualización.
5. El RuleTile generado conserva el comportamiento nativo correcto.
6. No se pierde ninguna funcionalidad existente.
7. Los tests relevantes pasan y no se introducen errores de compilación.


### S10
## Finalización

Implementar el cambio sobre la rama `Ruletile-tool`, sin crear commits ni realizar push.

Actualizar la documentación de uso en español e inglés para reflejar el nuevo flujo.

Entregar un resumen de los cambios y las verificaciones ejecutadas.

**Prioridad principal: usabilidad real.** No alcanza con mostrar las mismas matrices de otra manera. La nueva interfaz debe permitir que el usuario comprenda visualmente dónde aparecerá cada pieza mientras construye su Rule Tile.

## Tasks
- T1 — S1-S10 — inline — complete (8/8 focused GREEN711decb71d0546a7ae334670fb0052bd): occupancy scenarios, canonical slot mapping and coverage; focused RED/GREEN.
- T2 — S1-S10 — inline — complete (10/10 focused GREEN1c4576bfcff34e799fd62508dcbc4b80): interactive scene assignment, immediate feedback and unchanged native test integration; focused tests and Catacombs native UI probe.
- T3 — S1-S10 — inline — complete (119/119 final GREEN5c4337a48aac4b178f137b1022bd49af): bilingual guide, full Builder EditMode suite, diff and screenshot review. No commit.

## Log
L1 — Original request (verbatim):
# Mejorar asignación visual de sprites — Rule Tile Builder

## Objetivo

Rediseñar el paso **3. Assign pieces** de Rule Tile Builder para que un usuario sin conocimientos de autotiling pueda identificar fácilmente dónde debe asignar cada sprite.

**Problema actual:** las tarjetas con matrices 3×3 representan correctamente las conexiones, pero resulta difícil comprender qué posición del escenario corresponde a cada regla.

La solución debe permitir **asignar sprites directamente sobre ejemplos visuales de escenarios**, sin interpretar manualmente matrices de vecindad.

## Contexto

- Repositorio: `Astro-Rift-Games/ProjectGrimhold`.
- Rama de trabajo: `Ruletile-tool`.
- Implementación existente: `Assets/Editor/RuleTileBuilder/`.
- Leer `AGENTS.md` y las instrucciones de Unity antes de modificar archivos.
- Revisar la implementación actual y reutilizar sus componentes.

No reconstruir la herramienta. Esta tarea se limita a mejorar la experiencia de asignación visual y sus integraciones necesarias.

## 1. Editor visual de asignación

Reemplazar la presentación principal de tarjetas por un **escenario interactivo** que represente las conexiones de la plantilla seleccionada.

Flujo esperado:

1. El usuario selecciona uno o varios sprites en la biblioteca.
2. Selecciona una posición del escenario de ejemplo.
3. La herramienta identifica la regla correspondiente.
4. Muestra el nombre y la función de esa pieza.
5. Permite asignar los sprites seleccionados mediante clic o drag & drop.
6. Actualiza inmediatamente el escenario y la previsualización.

No exigir conocimientos de `This`, `Not This`, `Don't Care` ni matrices 3×3.

### Representación visual

- Mostrar las posiciones utilizando sprites reales cuando estén asignados.
- Distinguir claramente posiciones asignadas, pendientes y seleccionadas.
- Resaltar todas las posiciones que representan la misma regla.
- Mostrar el sprite de respaldo donde no exista una asignación.
- Identificar la pieza seleccionada mediante nombres comprensibles: centro, borde superior, esquina interior, extremo, unión, etc.
- Permitir reemplazar o eliminar una asignación.
- Conservar el soporte para múltiples sprites y variantes.

## 2. Escenarios de ejemplo

Una habitación rectangular no representa las 16 o 47 configuraciones posibles.

Incorporar ejemplos complementarios que permitan acceder visualmente a todas las reglas de las plantillas existentes:

- Habitación rectangular.
- Pasillos horizontales y verticales.
- Esquinas interiores y exteriores.
- Habitaciones con huecos.
- Intersecciones y uniones.
- Extremos y piezas aisladas.

Cada posición editable debe corresponder inequívocamente a una regla de la plantilla.

Garantizar que todas las configuraciones puedan seleccionarse mediante los ejemplos disponibles. Si alguna combinación no puede representarse claramente en escenarios convencionales, utilizar un ejemplo específico.

El usuario no debe necesitar recorrer una lista de 47 matrices para encontrar una pieza.

## 3. Interacción y feedback

Al seleccionar una posición:

- Resaltarla visualmente.
- Mostrar su función y sus conexiones relevantes.
- Indicar qué sprite tiene asignado.
- Permitir asignar o reemplazar sprites.
- Actualizar todas las posiciones equivalentes.
- Refrescar la previsualización nativa.

Separar claramente dos conceptos:

- **Asignación:** el escenario ilustrativo permite identificar y configurar las reglas.
- **Prueba:** el Tilemap nativo permite pintar libremente y comprobar el resultado real.

No presentar una simulación de asignación como si fuera el resultado nativo del RuleTile.

La interfaz debe conservar una buena legibilidad con tiles pequeños de pixel art, utilizando escalado apropiado y sin suavizado visual innecesario.

## 4. Compatibilidad

Reutilizar:

- `RuleTileBuilderTemplates`
- `RuleTileBuilderValidation`
- `RuleTileBuilderPreview`
- `RuleTileBuilderAssets`
- `RuleTileBuilderSpriteCatalog`

Mantener el `RuleTile` nativo como único modelo del borrador.

Preservar:

- Las tres plantillas existentes.
- Las reglas personalizadas.
- El panel Advanced.
- Single, Random y Animation.
- Undo/Redo.
- Guardado seguro y conservación de GUID.
- Advertencias y validaciones.
- La compatibilidad con el Inspector y Tile Palette nativos.

No inferir automáticamente el significado de los sprites ni inventar geometría. No aplicar rotaciones o reflejos automáticos.

## 5. Validación

Agregar tests EditMode que comprueben:

- Correspondencia correcta entre posición visual y regla nativa.
- Cobertura de las 16 y 47 configuraciones.
- Asignación y reemplazo de sprites.
- Actualización de todas las posiciones equivalentes.
- Persistencia de las asignaciones.
- Undo/Redo.
- Ausencia de modificaciones en el RuleTile original antes de guardar.

Validar mediante Unity MCP la compilación, los tests y la interacción real de la ventana cuando las herramientas disponibles lo permitan.

Realizar una prueba con Catacombs sin modificar sus sprites originales.

## Fuera de alcance

- Reescribir el generador de Rule Tiles.
- Crear un sistema nuevo de autotiling.
- Modificar escenas o Tilemaps existentes.
- Cambiar importadores o spritesheets.
- Implementar detección automática de piezas.
- Preparar la herramienta para Asset Store.
- Refactorizaciones ajenas a la asignación visual.

## Criterios de aceptación

1. Un usuario principiante puede identificar dónde asignar cada sprite sin comprender matrices de vecindad.
2. Puede configurar las conexiones mediante ejemplos visuales.
3. Todas las reglas de las plantillas existentes son accesibles.
4. Las asignaciones se reflejan inmediatamente en los ejemplos y en la previsualización.
5. El RuleTile generado conserva el comportamiento nativo correcto.
6. No se pierde ninguna funcionalidad existente.
7. Los tests relevantes pasan y no se introducen errores de compilación.

## Finalización

Implementar el cambio sobre la rama `Ruletile-tool`, sin crear commits ni realizar push.

Actualizar la documentación de uso en español e inglés para reflejar el nuevo flujo.

Entregar un resumen de los cambios y las verificaciones ejecutadas.

**Prioridad principal: usabilidad real.** No alcanza con mostrar las mismas matrices de otra manera. La nueva interfaz debe permitir que el usuario comprenda visualmente dónde aparecerá cada pieza mientras construye su Rule Tile.
L2 — Branch Ruletile-tool at d5e8cc37, clean working tree. Unity6000.5.1f1, Tilemap Extras8.0.3. Read-only map shows existing ApplySlot/Changed/Undo/native preview can be reused. No persistence service, generator, source importer or runtime edits planned. Main implementation inline: one dependent behavior, no independent parallel-writer reason. Scenario occupancy is Editor-only static illustration, not another draft/configuration model. All tooltips remain English.
L3 — Added7 deterministic EditMode cases before source changes; RED job37c900f1d9b54163ad22a63615510a30 started but transport disconnected and bounded result retrieval timed out. RED observation unavailable, not claimed. Continued source-only implementation while Editor proof is pending. No Editor restart or source asset mutation performed.

L4 — First RED request returned0 tests after reconnect, not a passing proof. Reflection then confirmed compiled helper and fixture. Focused job711decb71d0546a7ae334670fb0052bd passed8/8,0 skipped. Every scenario cell agrees with native Tilemap output;1/16/47 total reachable roles. Added native window click/drop and point-texture cleanup probe plus4 tooltip cases; expanded run pending. Illustrations use source texture copies with Point filtering, never importer edits; same native draft retains all assignment data. Risk:none: assignment UI/integration only; persistence/generator/validator/preview implementations unchanged.

L5 — Native window test reproduced non-readable texture Instantiate error (job0dd1c944e5124f95b28a816135afcb2b). Fixed via same-format GPU CopyTexture; source import settings untouched. GUI/point-texture cleanup9/9 GREEN46486112ef334212a43585c8ae56a280. Added independently scrollable illustration viewport and collapsed fallback controls to retain assignment controls at860x660; native click/drop/viewport regression now10/10 GREEN1c4576bfcff34e799fd62508dcbc4b80,0 skipped. Repaint screenshot inspected with Catacombs floor. Native synthetic events selected the actual top edge and assigned variants; active scene/dirty/selection and original importer hash unchanged. Full suite pending; test initialization failures ac087e5892f04718bc3ce4e3790dc136 and5d399561a2eb4ea599cf234b4797867f are environmental timeouts (no test failures), not passing evidence. RDD remains clone-local off, no review lifecycle.

L6 — Full six-fixture suite with exact namespaces:119/119 passed,0 skipped, final job5c4337a48aac4b178f137b1022bd49af (12.785s). Earlier91/91 run8220b2359c124c2fbe677fdc4b5f313e omitted two fixture namespaces and was not the full suite; corrected run9c35b26ccaa247e19a165239bc183dc8 also119/119, then final copy normalization validated again. No compilation errors (Console error CS filter0). Console historical GUI/capture messages and the fixed non-readable texture RED message remain; not cleared. Native screenshots of floor, combined courtyards and minimum-size window inspected; all owned windows/textures released; source JSON, active scene/dirty state and selection unchanged. Tests leave no owned scratch asset folders. Full diff and authored-source whitespace checks passed, only expected4 tracked feature files plus7 new feature/meta/task paths; no scene/Art/importer/package/runtime changes. No commits, push or branch operations; HEAD remains d5e8cc37. Manual limitations: actual human novice usability/art review, physical OS pointer drag (native synthetic events were exercised), complete Tile Palette and restart workflows not repeated. Risk:none (contained Editor assignment presentation/integration; native persistence and evaluation implementations untouched).
