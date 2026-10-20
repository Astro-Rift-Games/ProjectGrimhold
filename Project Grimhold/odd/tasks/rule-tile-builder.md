# Rule Tile Builder
Objective: native, beginner-friendly RuleTile authoring. Branch: New-Testing at 9f727ceaf27ae108f5c6508697354e051d7c76b5.
Delivery: local feature commit now authorized by L14, no push or PR; preserve the current Ruletile-tool branch. Runner: Unity MCP EditMode, plus isolated Editor functional probes.

## Specs
- S1: "Crear una herramienta exclusivamente de Editor, accesible desde `Tools/Grimhold/Rule Tile Builder`, que permita crear RuleTiles sin conocer previamente las matrices de Unity." No runtime code/dependencies/importer/existing scene/prefab/tilemap changes.
- S2: "Seleccionar sprites", "Elegir plantilla", "Asignar piezas", "Probar", "Generar o guardar". Floor, cardinal16 and contour47 templates, visual role cards, search/multiselect and contextual help; advanced native rules.
- S3: "Modelo único: un `RuleTile` nativo temporal será el borrador." Draft+Save, stable GUID, Undo, external-change and occupied-path protection; reject incompatible types/neighborhoods and override-dependent updates.
- S4: "Usar `PreviewRenderUtility` con Grid y Tilemap temporales aislados." Exact draft identity, paint/erase/foreign tile, rectangle/L/hole/corridor/isolated samples, cache refresh, native transforms/animation, cleanup.
- S5: "Exigir confirmación explícita para generar con advertencias." Block invalid references/outputs/parameters/path, warn optional gaps/duplicates/shadowing/fallback; analyze all 256 neighborhoods, keep animation coverage separate.
- S6: "Añadir tests EditMode" and "Realizar una prueba con Catacombs usando únicamente sprites inspeccionados visualmente." Compile/Console/tests/persistence/Undo/native preview/Tile Palette/clean diff; report unobserved restart/manual proof honestly.

## Tasks
- T1 | S2,S5,S6 | delegated: parallel independent core unit | complete | 13 RED then 13 GREEN, included in 42/42 focused run 52898e140ed04ac4aa92cf345b2d742e; no commit authorized.
- T2 | S4,S6 | delegated: parallel independent preview unit | complete | 15 RED then 15 GREEN, included in same 42/42 run; no commit authorized.
- T3 | S3,S6 | inline | complete | 11 RED then 11 GREEN; explicit Undo-group fix verified alone and in focused run. Risk item 1: updates existing stored assets.
- T4 | S1,S2,S3,S4,S5 | inline | complete | guided window/native helpers/docs and 3 GREEN integration tests. UI initial RED could not be observed: compile/bridge disruption; honest exception, not claimed RED.
- T5 | S6 | inline plus independent verifier (item 1) | complete with documented manual limits | final43/43 EditMode, independent persistence probes, native Catacombs render and48-cell GridBrush proof; restart/mouse/dialog/artistic approval pending, not claimed.

## Log
L1 (2026-10-19): User requested implementation of the approved plan below. "No habrá commits ni push."

> # Rule Tile Builder visual y guiado
>
> ## 1. Resultado y alcance
>
> Crear una herramienta exclusivamente de Editor, accesible desde un menú `Tools/Grimhold/Rule Tile Builder`, que permita crear RuleTiles sin conocer previamente las matrices de Unity.
>
> Base verificada: `New-Testing` (`9f727cea`), working tree limpio, Unity `6000.5.1f1` y Tilemap Extras `8.0.3`. Catacombs dispone de tres spritesheets ya cortados. Unity MCP está conectado al proyecto correcto.
>
> Se utilizará `RuleTile` nativo, sin dependencias nuevas, lógica runtime ni cambios en importadores, escenas, prefabs o Tilemaps existentes. No habrá commits ni push.
>
> ## 2. Flujo de uso
>
> La ventana ofrecerá cinco pasos, con navegación hacia atrás sin perder el borrador:
>
> 1. **Seleccionar sprites:** aceptar sprites individuales y texturas previamente cortadas; mostrar miniaturas, nombres, búsqueda y selección múltiple. Una textura sin sprites mostrará instrucciones, sin modificar su importación.
> 2. **Elegir plantilla:** presentar ejemplos gráficos de:
>    - Suelo continuo, con variantes opcionales.
>    - Conexiones ortogonales: 16 configuraciones, sin distinguir esquinas diagonales.
>    - Contorno completo: 47 configuraciones válidas, incluyendo esquinas interiores y exteriores.
> 3. **Asignar piezas:** mostrar tarjetas agrupadas por centro, bordes, esquinas, extremos y uniones. Cada tarjeta incluirá un pequeño ejemplo de su función; permitirá asignar sprites mediante clic o arrastre.
> 4. **Probar:** pintar y borrar en una habitación de prueba, con ejemplos de rectángulo, forma en L, hueco interior, pasillo y pieza aislada.
> 5. **Generar o guardar:** revisar errores, advertencias, nombre y destino antes de persistir.
>
> Las plantillas producirán las conexiones automáticamente, pero **no identificarán sprites ni inventarán piezas**. No habrá rotación o reflejo automático del arte.
>
> Un panel **Avanzado** permitirá crear, eliminar, duplicar y reordenar reglas, editar la matriz 3×3 y configurar variantes nativas Single/Random/Animation. Los estados tendrán explicaciones visuales de “misma superficie”, “otra superficie o vacío” y “cualquiera”; la terminología nativa será secundaria.
>
> ## 3. Arquitectura y persistencia
>
> Separar ventana/interacción, catálogo de plantillas, validación, persistencia y previsualización. Mantener el patrón existente de carpetas Editor y tests, sin reorganizar assemblies.
>
> - **Modelo único:** un `RuleTile` nativo temporal será el borrador. Las plantillas serán definiciones estáticas del Editor que generan reglas directamente; no se creará otro formato persistente de configuración.
> - **Reutilización:** aprovechar los controles públicos del `RuleTileEditor` instalado para matriz y opciones nativas, sin reemplazar el Inspector estándar ni modificar el paquete.
> - **Edición existente:** clonar el asset y modificar únicamente el borrador. Guardar actualizará el objeto original conservando GUID e identidad; descartar no lo alterará.
> - **Compatibilidad:** reconocer las reglas compatibles con las plantillas al reabrir. Las reglas personalizadas conservarán contenido y orden; cambiar de plantilla exigirá confirmar su sustitución. Tipos derivados o vecindades no representables se rechazarán con explicación, sin conversión destructiva.
> - **Guardado seguro:** usar serialización, Undo y AssetDatabase. Crear en una ruta ocupada se rechazará; actualizar requerirá seleccionar explícitamente el asset. Detectar cambios externos antes de sobrescribirlos.
> - **Dependientes:** no actualizar silenciosamente RuleOverrideTiles. Si existen dependientes que requieren regeneración, bloquear esa actualización y explicar la limitación.
> - **Persistencia:** los assets guardados deberán sobrevivir al reinicio del Editor. Al cerrar o cambiar de asset con modificaciones pendientes, ofrecer guardar, descartar o cancelar.
>
> No se añadirán APIs ni tipos de runtime.
>
> ## 4. Previsualización y validación
>
> ### Previsualización real
>
> Usar `PreviewRenderUtility` con Grid y Tilemap temporales aislados. El Tilemap utilizará exactamente el RuleTile del borrador, no una reproducción del algoritmo.
>
> Permitir pintar la superficie, borrar y colocar otro tile para comprobar límites. Actualizar cachés de vecinos y refrescar tras cada cambio; liberar todos los recursos al cerrar o recargar.
>
> Mostrar el resultado nativo de sprites, transformaciones y animaciones, respetando que Unity evalúa las animaciones mediante su propia búsqueda de reglas. No simular colisiones físicas.
>
> ### Errores y advertencias
>
> **Bloquear generación** ante referencias inválidas, sprite de respaldo ausente, reglas activas con salidas incompletas, parámetros inválidos o destino no válido.
>
> **Advertir** sobre piezas opcionales faltantes, duplicados, reglas posiblemente ocultas por prioridad y conexiones sin cobertura:
>
> - Mostrar qué falta, dónde afecta y cómo resolverlo.
> - Resaltar en la previsualización las celdas que usan respaldo.
> - No guardar reglas incompletas con sprites nulos: omitir esas reglas opcionales.
> - Exigir confirmación explícita para generar con advertencias.
> - Permitir completar posteriormente las piezas y actualizar el mismo asset.
>
> Analizar las 256 combinaciones de los ocho vecinos para las reglas compatibles, considerando transformaciones y diferenciando cobertura visual de animación.
>
> ## 5. Pruebas y entrega
>
> Añadir tests EditMode para generación de las tres plantillas, validación, piezas opcionales, persistencia, GUID, Undo/Redo, descarte, rutas ocupadas, sandbox real y limpieza.
>
> Con Unity MCP, comprobar compilación, Console, tests y assets generados. Verificar funcionalmente apertura del menú, asignación visual, pintura, edición posterior y persistencia tras reinicio.
>
> Realizar una prueba con Catacombs usando únicamente sprites inspeccionados visualmente. Las paredes de varias piezas no se presentarán como construcciones automáticas de una sola celda. La prueba de Tile Palette se hará en un entorno temporal, sin modificar contenido existente.
>
> Entregar inventario de archivos, instrucciones breves, resultados observados y verificaciones pendientes. Revisar el diff completo para excluir cambios ajenos.

L2: Mapping reused from this session; current git/versions rechecked clean. Parallel T1/T2 have disjoint edit surfaces. Parent owns all Unity compilation/test execution to avoid concurrent runner interference. Forecast exceeds 400 lines; user explicitly chose no commits/PR, so delivery is local-only rather than chaining. Restarting the human's Editor is not authorized automatically; report real restart check pending unless performed by the user.
L3: Observed RED jobs e08a454277b246c189f9dc9afadd2d64 (24 failures: core13 + storage11) and ff77ed163a6d436b8ab22170c328743f (preview15). Bridge disconnected during later compilation, recovered. Unity6000.5 EntityId API and project-global EntityId collision fixed using native IsValid(); preview reflection test needed explicit object[] to avoid implicit Object-to-bool overload. Isolated Create/Update Undo groups fixed Redo failure.
L4: Focused final source run 52898e140ed04ac4aa92cf345b2d742e:42/42 passed,0 skipped; actual new integration test presence confirmed by reflection before run. Native review assess unassessable due untracked scope; not treated as low risk. RDD mode clone-local off, no review lifecycle. Independent item1 verifier still required. No commits/push.
L5: Independent verifier rule_tile_verify (high-risk item1), snapshot/live source hashes equal; rejection guards preserved asset JSON/disk/GUID; warning confirmation, occupied path, override guards, external source-change rejection and stable update/reimport Tilemap reference all verified. Invalid native SaveChanges callback retained dirty state; no dialog interaction. No blockers reproduced. Scratch C:/Users/Dani/AppData/Local/Temp/RuleTileBuilderVerify_b8b879d2213e486ca9eb863f86b96404 safely removed by parent after validating exact identity under temp.
L6: Created native CatacombsFloorRuleTile at Assets/Art/Tilesets/Catacombs/RuleTiles/CatacombsFloorRuleTile.asset, GUID2c5ef752eb017d14da1e0adfeb82d80c. Visual source sheet inspection; floor variants0/1, fallback4, colliderNone. Saved/reopened via window APIs, native GridBrush painted48/48 cells in disposable preview; active scene, dirty state and selection unchanged. Direct PreviewRenderUtility image captured at task visualization path and inspected; no wall geometry invented.
L7: Actual window menu discovered/executed; window listed by Editor resources. Manual forced repaint initially logged a layout exception at the hover-dependent control. Fixed by always reserving hover label and exiting GUI on wizard toolbar transition; grouped assignment cards by visual role. Added native layout/repaint regression probe (did not itself reproduce prior event). Final compiled43/43 focused GREEN job4eecc9047c684d8db79730bf80d66432,0 skipped. Prior historical layout exception remains in Console; subsequent layout/repaint probe used final compiled assembly. Compilation errors0. Full unrelated gameplay suites not run (no runtime changes).
L8: Final inspection: all feature paths new/untracked, no existing tracked file modifications, no runtime/dependency/importer/scenes/prefabs diffs; all10 C# source files free of trailing whitespace. No owned test scratch folders remain. Real Editor restart, mouse-driven Tile Palette workflow, dialog clicks, and human artistic/usability approval remain documented manual limits; not a completed proof. No commits/push.
L9: Final HEAD recheck detected external advancement to1afb1e36 (chore(changelog): update developer changelog [skip ci]). Compared with initial9f727cea: only CHANGELOG_DEV.md changed,2 lines; no candidate/editor/tests/art/dependencies/configuration changes. No commit or push command was run by this task. Preserve that unrelated external update; no reset/revert. Tool contains6 Editor scripts,4 test files,README,native sample and this log (plus Unity metas).
L10 (2026-10-19): User: "arma una guia de uso en español tambien". Small inline documentation follow-up: added README.es.md with beginner walkthrough, exact English UI labels, Catacombs example, warnings/errors, draft editing, advanced controls, troubleshooting and honest validation limits; linked it from the English guide. No code or asset behavior changes, no commits/push. Structural/reference checks only; Unity tests not rerun for passive documentation.
L11 (2026-10-19): User: "tambien agrega tooltips a las herramientas disponibles para saber que hace cada una+". Small inline presentation-only follow-up: Spanish GUIContent tooltips on Builder controls, source library, templates/cards, preview brushes, guarded save and advanced native fields. Kept display labels/callbacks unchanged; passive fixed-rect overlays use RuleTileEditor.k_SingleLineHeight and do not intercept input. Clear library/piece/preview have distinct boundary explanations. Added RuleTileBuilderTooltipTests:61 observed RED (missing Help), job67b777c050e04acdbcb84ed4fb5b391d, then61 GREEN jobbcf72fd24e81443a94a93b008a500b52. Whole Builder suite104/104 GREEN,0 skipped, jobb754d67714ba4b2a9ab8c2fbb64837e9. Compilation loaded Help reflected before run. Tooltip visual dwell remains manual. RDD remains clone-local off; no commits/push. Risk:none (private UI presentation only).
L12: During tooltip follow-up Unity/test activity, Git began marking Assets/Scenes/MainMenu.unity modified, but git diff/stat/name-only show no content delta. Index, raw worktree and filtered worktree hashes all equal7789eecfd7c39fcc3ab86b33cbe8b91830dcad57 (an LF→CRLF warning is also emitted). No scene edit/save/restore command was issued by this task; preserve that status/metadata condition rather than reverting identical bytes. Owned test scratch folders removed by teardown.
L13 (2026-10-19): User correction: "todos los tooltips tienen que estar en ingles". Replaced all60 Builder tooltip descriptions and2 dynamic native-matrix context lines with English; native Inspector remains untouched/English. Spanish guide remains Spanish and now accurately describes English tooltips. Updated Clear-boundary test to English; observedRED03740a4ca9cd410c95bd528115afd2e8,61/61 tooltipGREEN081212351e3e484894139906c925d347,104/104 BuilderGREEN18999f49deb645248d9c4c588176fef7,0 skipped. Compiled English help reflected before tests; no remaining Spanish tooltip phrases in window source. Branch externally changed to Ruletile-tool before this request; preserved it and prior dirty work, no branch/commit/push commands. Risk:none (copy only, labels/actions unchanged). Native visual dwell still manual.
L14 (2026-10-19): User: "ok crea el commit. Si se puede quiero un par de capturas de las diferentes interfaces". Explicitly authorized one local feature commit; supersedes earlier no-commit constraint, no push authorized. Preserve Ruletile-tool and exclude unrelated MainMenu.unity status. Commit scope: six Editor scripts, five test fixtures, bilingual guides, native Catacombs floor sample, Unity metas and this log; screenshots are task artifacts outside the repository. Current final suite104/104 passed with latest English help. RDD clone-local off, no new review lifecycle. Native GUIView offscreen capture used because the Unity application is not foreground; no desktop pixels or unrelated windows captured.
