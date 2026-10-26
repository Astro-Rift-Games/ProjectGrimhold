# Faithful authored dungeon minimap
Objective: render saved Gameplay tile art and automatically refresh authored scene/dependencies.
Branch: feat/gameplay-modular-hud (base c3c2b09b); delivery: one explicitly authorized local implementation commit, NO push/publishing; runner: Unity MCP EditMode/PlayMode.

## Specs
S1. "- Mantener `Dungeon_Graybox.prefab` como fuente y procesar exclusivamente `Floor`, `Walls` y `Obstacles`. Excluir personajes, enemigos, loot, efectos y objetos interactivos.
- Extender `MinimapLayout` con datos visuales generados: sprites, posiciones, geometría, UV, colores, transformaciones y orden de capas. Conservar los datos geométricos usados por la proyección.
- Adaptar `RaidMinimapGraphic` para dibujar esos sprites mediante geometría uGUI agrupada por textura, respetando transparencia y orden visual. Sin duplicar imágenes ni crear un objeto UI por tile.
- Conservar el filtrado pixel-art, las proporciones y la correspondencia entre posiciones del mundo y del minimapa, incluidos tiles desplazados o transformados.
- Ampliar la detección de cambios para incluir dependencias: tiles, sprites, texturas e importación. Regenerar tras guardar/importar cambios, sin bucles de reimportación ni escrituras cuando el resultado no cambie.
- Añadir una comprobación previa al build que actualice los datos generados y detenga la compilación si no puede producir un minimapa válido.
- Actualizar las pruebas y la documentación técnica que actualmente describen bloques de color." Checks: faithful geometry/UV/tints/transforms/layers, dependency invalidation and deterministic generation, prebuild validation.
S2. "- Mostrar únicamente **el jugador local y su extracción asignada**.
- Ocultar tanto el icono como la flecha de extracción antes de completar la cuota y recibir la asignación. Mostrarlos desde esa asignación, **sin esperar a terminar el ritual**.
- Conservar la privacidad de la asignación y limpiar marcadores al perderla, extraer, cambiar de sesión o desvincular el HUD.
- Mantener el comportamiento actual: norte arriba, jugador centrado, mapa desplazándose debajo, recorte del viewport y flecha cuando la extracción quede fuera.
- No modificar cuotas, rituales, asignaciones ni reglas de extracción." Checks: quota/assignment privacy, lifecycle cleanup, projection and mask unchanged.
S3. "- Observar pruebas fallidas antes de implementar los nuevos comportamientos.
- Comprobar cambios de forma, huecos, coordenadas negativas y sustituciones de arte sin cambiar la ocupación.
- Verificar transformaciones, superposición de capas, límites de geometría y regeneración determinista.
- Probar extracción oculta antes de la habilitación, visible tras asignación y nunca revelada a otro equipo.
- Ejecutar compilación y pruebas pertinentes mediante Unity MCP; comparar visualmente dungeon y minimapa, y comprobar privacidad en Host/Client. Informar cualquier verificación pendiente." Check results must be observed; report unavailable visual/multiplayer proof.
S4. "1. La actualización corresponde a cambios de diseño guardados en el prefab; quedan fuera los cambios exclusivos de escena y las modificaciones durante la partida.
2. El sistema actual ya genera la geometría automáticamente; la mejora añade fidelidad artística y seguimiento de dependencias.
3. Se preservarán los cambios pendientes del HUD, sin rediseñar su disposición ni realizar commits o publicaciones." Preserve dirty work, source GUIDs, current branch and HUD layout.

S1 update (explicit choice): user selected "1" = "Usar Gameplay — recomendado: generar el minimapa desde la instancia real, incluidos sus cambios, sin modificar la escena ni el prefab." This overrides the prefab-only source fragment of S1/S4; only saved Gameplay Floor/Walls/Obstacles are generated, with scene-world coordinates and no double offset.

S5. "esta bien, pero hay dos problemas a simple vista, al moverte los tiles de las paredes (dentro del minimapa) titilan, a veces incluso quedando oscuros/negros. Y otro problema es que no es del todo preciso con la ubicacion del jugador en el mapa, en los bordes sobre todo hay sectores donde el jugador queda por fuera de los limites del mapa dentro del minimapa" Acceptance: reproduce the reported moving-wall sampling and actual scene/player alignment defects before correction; verify motion and edge positions without changing extraction rules or original art assets.

## Tasks
T1 | S1,S3,S4 | inline | complete (25/25 EditMode + exact visual probe) | generated tile-art layout/renderer, refresh/build guard, regression tests | commit: this implementation commit (see git log --follow).
T2 | S2,S3,S4 | delegated | complete (five final-suite passes + failed case corrected/rechecked 1/1) | extraction visibility/private marker regression coverage, minimal presenter fixes only if reproduced | commit: this implementation commit (see git log --follow).
T3 | S1-S4 | inline + bounded verification | complete automated checks; manual Host/Client/full-build proof pending | architecture, generation, focused/suite/visual checks and diff audit | commit: this implementation commit (see git log --follow).

T4a | S1,S3,S4,S5 | inline | complete automated checks; manual gameplay proof pending | use actual Gameplay geometry/world coordinates; dependency/freshness/edge regression tests | commit: this implementation commit (see git log --follow).
T4b | S2,S3,S4,S5 | delegated | complete (motion11/11 and independent raster3/3) | physical-pixel-stable minimap translation and raster regression tests | commit: this implementation commit (see git log --follow).

## Log
L1 (2026-10-23): User implementation request verbatim:
PLEASE IMPLEMENT THIS PLAN:
# Min imapa fiel al diseño de la dungeon

## Resumen
Ampliar el sistema existente para mostrar los **mismos tiles de la dungeon a escala reducida**, conservando su forma, huecos y distribución. Se actualizará automáticamente al guardar cambios en el prefab o sus assets, sin generación procedural ni cámaras adicionales.

## Implementación
- Mantener `Dungeon_Graybox.prefab` como fuente y procesar exclusivamente `Floor`, `Walls` y `Obstacles`. Excluir personajes, enemigos, loot, efectos y objetos interactivos.
- Extender `MinimapLayout` con datos visuales generados: sprites, posiciones, geometría, UV, colores, transformaciones y orden de capas. Conservar los datos geométricos usados por la proyección.
- Adaptar `RaidMinimapGraphic` para dibujar esos sprites mediante geometría uGUI agrupada por textura, respetando transparencia y orden visual. Sin duplicar imágenes ni crear un objeto UI por tile.
- Conservar el filtrado pixel-art, las proporciones y la correspondencia entre posiciones del mundo y del minimapa, incluidos tiles desplazados o transformados.
- Ampliar la detección de cambios para incluir dependencias: tiles, sprites, texturas e importación. Regenerar tras guardar/importar cambios, sin bucles de reimportación ni escrituras cuando el resultado no cambie.
- Añadir una comprobación previa al build que actualice los datos generados y detenga la compilación si no puede producir un minimapa válido.
- Actualizar las pruebas y la documentación técnica que actualmente describen bloques de color.

## Marcadores y presentación
- Mostrar únicamente **el jugador local y su extracción asignada**.
- Ocultar tanto el icono como la flecha de extracción antes de completar la cuota y recibir la asignación. Mostrarlos desde esa asignación, **sin esperar a terminar el ritual**.
- Conservar la privacidad de la asignación y limpiar marcadores al perderla, extraer, cambiar de sesión o desvincular el HUD.
- Mantener el comportamiento actual: norte arriba, jugador centrado, mapa desplazándose debajo, recorte del viewport y flecha cuando la extracción quede fuera.
- No modificar cuotas, rituales, asignaciones ni reglas de extracción.

## Validación
- Observar pruebas fallidas antes de implementar los nuevos comportamientos.
- Comprobar cambios de forma, huecos, coordenadas negativas y sustituciones de arte sin cambiar la ocupación.
- Verificar transformaciones, superposición de capas, límites de geometría y regeneración determinista.
- Probar extracción oculta antes de la habilitación, visible tras asignación y nunca revelada a otro equipo.
- Ejecutar compilación y pruebas pertinentes mediante Unity MCP; comparar visualmente dungeon y minimapa, y comprobar privacidad en Host/Client. Informar cualquier verificación pendiente.

## Límites y Key Learnings
1. La actualización corresponde a cambios de diseño guardados en el prefab; quedan fuera los cambios exclusivos de escena y las modificaciones durante la partida.
2. El sistema actual ya genera la geometría automáticamente; la mejora añade fidelidad artística y seguimiento de dependencias.
3. Se preservarán los cambios pendientes del HUD, sin rediseñar su disposición ni realizar commits o publicaciones.

L2: Current branch/root verified; existing dirty HUD/NetworkPlayer/presentation/tests/docs/assets are unrelated and protected. Task is tracked because independent visual-data and marker units plus Editor/network proof need recoverable coordination. T1 and T2 are independent behavior units with disjoint source/test edit surfaces and run in parallel (Writer trigger: parallelism). Parent owns git, architecture and Unity test execution lease. Forecast ~800-1200 authored lines plus generated layout data; no commit/PR slicing applies because user explicitly forbids commits/publication. Risk: generated reproducible asset schema extension; no player persisted data or network contract changes. No gameplay rules changed.

L3: RED observed via Unity MCP EditMode RaidMinimapPrefabTests: 6 completed, 4 pass, 2 expected failures (no _artBatches; UnityWhite instead of source atlas). First implementation compile found generator signature patch mismatch and NUnit collection overload; corrected before GREEN test launch. T2 worker returned 6 PlayMode tests, presenter unchanged, awaiting native execution. Native RDD mode is off (clone_local); assessment unavailable due existing untracked inventory; do not start RDD or infer low risk. Risk: item 5 (new prebuild freshness gate may stop a build on invalid source); independent verifier required after own checks.

L4: Focused EditMode initial implementation 22/25 pass: packed SpriteAtlas mismatch and two fixture assertions. Live-reflected SpriteUtility unpacked APIs, switched generation to original mesh/UV/texture. Correction recheck native XML 3/3 pass (MCP metadata falsely timed out initialization; native XML preserved under external minimap-validation). Marker PlayMode 3/6 pass, three fixture-domain issues sent as one bounded correction (unrelated loot saver side effect, Single authority semantics, authored arrow correction). No presenter changes. Original sandbox TEMP backup was ephemeral across tool processes; durable protected pre-PlayMode snapshot now lives in the authorized external visualization root. No unrelated files intentionally modified.

L5: Source-vs-uGUI visual probe in an isolated, unloaded-after-use prefab preview scene: 472x416 pixels, original unlit Floor/Walls/Obstacles versus generated uGUI at identical projection; 0 differing pixels and 0 shape mismatches. Both PNGs outside worktree in minimap-validation. Temporary camera/RenderTexture existed ONLY in this validation probe and were destroyed, not production. Marker bounded correction native XML 3/3 pass; real Host/Client/proxy not executed. MCP job metadata lost callbacks across domain reload and falsely reports initialization timeout despite completed native XML; require native XML identity/case verification and terminal job cleanup before next run. All owned preview objects/materials/render target cleaned; active MainMenu scene preserved.

L6: Own final focused/relevant EditMode suite succeeded via Unity MCP and native XML: 25/25, 0 skipped, 1.58 s; dd7a7f3e00f146bd935ef6239134a7a4. T1 implementation ready. Independent verifier limited to Risk item5 prebuild guard plus regression acceptance; source frozen, RDD remains disabled/unmanaged.

L7: Independent verifier Risk item5 passed positive prebuild/idempotence and scratch baseline/static checks; final six-case PlayMode 5/6 pass. Remaining quota/assignment case passed all visibility/arrow assertions but direct Transform movement was overwritten by Fusion, causing flaky follow assertion. First independent-verifier correction is test-only: exercise existing public Bind observation Transform with owned stable input fixture Transform and assert exact projected delta. Production unchanged; request limited recheck of that single case, no new sweep. Full build/negative invalid-source build and real Host/Client remain unexecuted.

L8: Independent limited recheck succeeded 1/1, job706947f733e240b7a5f25404678ee113, native XML playmode-follow-recheck.xml. Other five retain passing evidence from final suite; no post-correction full-six rerun claimed. Parent spot check repeated positive prebuild: valid true, hash unchanged true, dirty false, one batch; production source unchanged. Tracked minimap diff --check exit0. Owned verifier scratch removed (sandbox scratch already absent).
L9: PlayMode tests regenerated only alagard font cache blocks: glyph/character tables, used/free rectangles and its embedded Texture2D bytes/dimensions (1x1 ->1024x1024). Protected pre-PlayMode backup hash verified; post-test final hash5F0B717DF5B3A44366897C6180AE74693E27139EEF55C4965599CD85A4DE0838 verified immediately before restoring ONLY that test-owned cache drift. All eight protected files match durable pre-PlayMode snapshots. Force-reimport of this unrelated font was rejected by auto-review; no bypass/retry/alternate refresh performed. Disk restoration is complete; source minimap checks unaffected. Final Console read0errors. No commits/staging/push/publication; RDD disabled/unmanaged. Real Host/Client/proxy, full player build, negative invalid-source build execution and broad project suite remain unexecuted; visual probe is raw tiles versus generated geometry, not complete gameplay HUD acceptance.

L10 (2026-10-23): User correction verbatim: esta bien, pero hay dos problemas a simple vista, al moverte los tiles de las paredes (dentro del minimapa) titilan, a veces incluso quedando oscuros/negros. Y otro problema es que no es del todo preciso con la ubicacion del jugador en el mapa, en los bordes sobre todo hay sectores donde el jugador queda por fuera de los limites del mapa dentro del minimapa
L11: Reopened visual fidelity/placement acceptance, not extraction rules or HUD redesign. Same branch/SHA and dirty baseline retained. One read-only mapping/reproduction explorer required after bounded parent batch; parent owns subsequent implementation/tests. Previous raw 8px-per-world-unit static comparison did not prove 4UI-unit moving/downsampled rendering or active scene placement. No root cause assumed yet.

L12: Read-only diagnosis confirmed Gameplay has overridden geometry (Floor3477 vs prefab1126; Walls2048vs1942; Obstacles12vs47), with matching offset and unit scale. GPU point-minification probe at4UIunits/world reproduced phase-dependent darkening (quarterpx RGBsum-4.35%; .75px242lit-to-nearblack). Source choice blocks accuracy fix because prior user explicitly selected prefab: ask useGameplayinstance without design edits vs applyGameplayTilemaps to prefab (affects other instances). request_user_input is unavailable in Default mode, so relay as one plain-chat single-select question. No corrective source writes or tests yet; do not silently change design/source authority. T4 pending this user decision.

L13: User source decision verbatim: 1. Choice1 authorizes actual saved Gameplay dungeon instance including overrides, without design changes to scene/prefab. Implement T4a inline and T4b as independent pixel-stability unit in parallel, declared disjoint edit surfaces (source generator/presenter vs View/pixel tests); parent owns git/test runner. No new dependency or source texture import change, no production camera/RT, no marker clamping or arbitrary position offset. Retain legacy serialized origin field but stop applying it to generated scene-world coordinates. Risk item5: source generator participates in prebuild gate; independent verification after own checks. Source/scene assets protected by pre-correction hashes.

L14: RED native+MCP11cases completed,8expectedfails/3pass (b04f21e55e7d41239bebe2ad0e3736aa). Source fails Floor(-32,-27) missing beyond Xbounds and Gameplay invalidation false; pixel6cases fail fractional positions atCanvas.73/1/1.37. Initial attempt discovered0tests because new .cs were not imported by if_dirty refresh; explicitly imported only2ownedtest scripts, then realcompile and RED, no unrelatedfontrefresh. Source correction uses hidden preview of savedGameplay, unique prefabidentity selection, same whitelist, worldspacepivot (legacy serialized offset retained/ignored), direct source/meta file hashes to avoid transitive scene->HUD->layout hash cycle. Pixel worker authorized GREEN within View/tests only.

L15: Corrections GREEN through Unity MCP and matching native XML: focused14/14 (ac8e5b60a7a345b9b3625185c83ca98d), relevant EditMode39/39 (5039d62b78bf4199b8e53e9691a9d6fc), PlayMode7/7 (2f8ddec0e33547f38d3bfa71fc5e5f11), no skipped. PlayMode includes four extreme floor cells aligned with fixed player and unchanged private extraction lifecycle. Initial short-class PlayMode filter returned0cases, not proof; fully-qualified class filter executed all7. Raster tests cover actual4UI/world at Canvas.73/1/1.37 for stable subpixel buckets and translated-equivalent whole-pixel pans. Source/prefab/otherprotected geometry hashes unchanged; fontcache drift requires narrow finalcleanup. Independent Risk5 verifier and final visual/protected audit pending; no source art/filter/HUDdesign edits.

L16: Independent Risk5 verification found no concrete change-caused blocker: source3/3 and raster3/3 nativeXML, successful prebuild callback twice, invalid isolated source rejected without outputwrite, all repeatedhash/cell/batchJSON stable, unique Gameplay instance and sourceworldpivot exact. Parent spotcheck reran prebuild callback: scene/prefab/outputbytes unchanged; idle MainMenu. Visual4UI/world comparison252x236 has0alpha-mask mismatches but2607color-different pixels from TilemapRenderer vs uGUI minification paths (walls dark vs visiblepattern); NOT pixel-identical gameplay acceptance. Production changes preserve originalart/sampling and motion3case regression demonstrates stable samples; complete in-game visual proof pending. No realHost/Client/fullbuild/broad project suite or invalid-source build execution claimed. Scratch already absent. Font dynamic glyph/character/rect/embeddedtexture cache changed during tests; regex-normalized all otherfields identical, currenthash8EAD76F8F86D1829B4124F0911DEA6E3A577D60E5E85465E78850260246C1429 verified before restoring hash-verified protectedbackup, no import/refresh/retry of deniedoperation. All8protectedfiles match pre-correction snapshots. Risk: item5 (Editor source-generation/prebuild gate); bounded independentverification passed. RDD off/disabled-unmanaged; no commits/staging/publication.

L17: Final owned tracked diff whitespacecheck passes with WindowsCRLF accepted; owned untracked C# files have0trailing-whitespace lines. Repository-wide check also reports pre-existing whitespace in unrelated dirty prefabs; left untouched. Branch/SHA unchanged. All8protected hashes unchanged after verification cleanup. Source import/compiler checks complete; no further refreshes. Needs manual proof: in-game visual acceptance, realHost/Client privacy, full player build/negative-sourcebuild and broad suite remain pending; implementation and scoped automated proof complete.

L18 (2026-10-23): User authorization verbatim: ok, hace el commit de esta implementacion. Supersedes the earlier no-commit restriction for this implementation only; no push/publication authorized. Commit uses an explicit minimap allowlist and only the first minimap hunk of RaidMainHudArchitecture.md; excludes unrelated HUDart/prefabs/fonts/tests/docs. HEAD already has required minimap serializedwiring and tracked sourceatlas, structurally checked; tests were executed on current workingtree, not on isolated HEAD+allowlist. Previous validation remains39/39EditMode and7/7PlayMode; no source changes for commit preparation. Rollback boundary: revert this minimap code/generatedlayout/tests/docs commit without changing sourceGameplay/prefab or unrelated HUD work. Risk: item5 (existing prebuildgeneration gate), independently verified in L16. RDD remains disabled/unmanaged.

L19 (2026-10-26): Wall flicker while moving diagnosed: `LocalGameplayHud` Overlay Canvas is a child of the moving `NetworkPlayer`, so world-space pixel snapping drifted by the player's fractional position. Fixed in `RaidMinimapView` by snapping in root-canvas pixel space; RED 3/3 (0.37 px drift) then RaidMinimap EditMode 28/28 via Unity MCP. User confirmed in game. Commit 7c43a127.

L20 (2026-10-26): Remaining dark band at wall bases is 2:1 Point decimation of 1-texel brick/mortar/base rows. User explicitly chose to supersede the "original textures, not a copied/baked PNG" architecture decision: the generator bakes an alpha-weighted 2x2 box-averaged copy of each source atlas as a sub-asset of the generated layout (source PNG/import untouched, UVs unchanged), hash version bumped, guard test and architecture docs updated.

T5 | S1,S3,S4 | delegated writer | complete automated checks (RED 4 intended fails; GREEN Minimap 53/55 with 2 pre-existing RaidHudStylePassPrefabTests failures); layout regenerated (24x28 embedded atlas); in-game visual proof pending | averaged minimap atlas sub-asset, tests, docs | commit: this implementation commit (flicker fix in 7c43a127; user authorized both 2026-10-26).
