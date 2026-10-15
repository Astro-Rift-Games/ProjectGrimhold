# Vision fog (local presentation, prototype)

Objective: add an atmospheric fog over the Gameplay scene that stays clear inside the local viewer's vision shape (near radius + facing cone, option A: geometric, no occlusion), to soften the vision cone and add dungeon game feel. Branch `feat/vision-fog` (from `feat/vision-cone-visibility` @ `521d4bcc`). No HacknPlan task yet: prototype first, task later (user decision).
Engram mirror topic: `odd/vision-fog/tasks`.

## Specs
- S1 Contract: `VisibilityArchitecture.md` changes from "no in-game presentation" to "environment fog presentation, local only, never decides entity visibility"; it documents every possible improvement.
- S2 Shape source: fog reads the active viewer through `EntityVisibilitySystem` (spectated viewer precedence, fail-closed: no viewer = full fog) and the shape from the same `VisionQuery` (single source: `VisibilitySettings`).
- S3 Rendering: hand-written URP 2D unlit shader on a camera-covering quad in a new top world sorting layer `Fog` (above `VFX`, below UI). Procedural world-space noise, soft angular/radial edges, noise-broken edges. No renderer feature, no package change.
- S4 Feel config: `VisionFogSettings` ScriptableObject (art values, separate from balance `VisibilitySettings`). Facing is smoothed in presentation only.
- S5 Out of scope: occlusion-aware mask (option B), torch clearing, explored memory, depth variation, accessibility option (documented as improvements).

## Tasks
- [x] T1 (S2, S4) Pure `VisionFogRules` (facing smoothing, clear-strength step, camera coverage) + EditMode tests; expose vision shape on `VisionQuery` and active query on `EntityVisibilitySystem` | route: delegated worker (RED stub, then GREEN) + parent Unity MCP runs | commit: `0a6500e1` `feat(visibility): add vision fog rules and expose the active vision query`
- [x] T2 (S3, S4) Shader, `VisionFogSettings`, `VisionFogPresenter`, material and settings assets | route: delegated worker (code), parent Unity MCP (assets) | commit: `8a34e73a` `feat(visibility): add the vision fog shader, settings and presenter`
- [x] T3 (S3) `Fog` sorting layer + Gameplay scene wiring + Unity compile/tests/visual check | route: parent Unity MCP | commit: `9c21b985` `feat(visibility): wire the vision fog into the Gameplay scene`
- [x] T4 (S1) Architecture doc update with the improvement backlog | route: inline (single doc) | commit: see `docs(visibility): document the vision fog and its improvement backlog`

## Log
- L1 (2026-10-14, user): "1. si, 2. A, 3. No hay tarea, primero probemos como queda, y despues la agregamos. En la documentacion deja aclaradas todas las mejoras posibles. La idea de esto es poder disimular un poco el cono de vision y sumar game feel dentro de la dungeon."
- L2 Finding: walls share the `Characters` sorting layer with characters, so fog cannot sit between walls and characters; it goes above every world layer. Dynamic entities are only visible inside the (clear) vision shape, so this is consistent; static occlusion-only objects outside the cone stay visible under partial fog.
- T1 evidence: RED observed in Unity, `VisionFogRulesTests` 18/18 executed, all failing on behavior against the stub. GREEN: `Tests.EditMode.Visibility` 57/57 passed, no compile errors. `NetworkPlayer.prefab` shows as modified after a Unity refresh with line-ending-only changes (empty content diff); left unstaged.
- T2/T3 evidence: shader compiles with no messages (`ShaderUtil.GetShaderMessages` = 0); no C# compile errors. Play Mode in `Gameplay.unity` without a Fusion session: presenter enabled, quad covers the camera (37.56 x 22 at ortho size 10), no viewer, full fog drawn (fail-closed). Shader mask checked by temporarily disabling the presenter and pushing a fake viewer at the camera center facing right (near radius 2, 90 deg cone): clear cone and near radius with soft, noise-broken edges. Fog looks lighter than alpha 0.85 suggests because of linear color space blending (art tuning). `Tests.EditMode.Visibility` 57/57, PlayMode `VisionOcclusionPlayModeTests` 11/11. Not validated: a real avatar viewer, spectating, Host/Client, performance on target hardware. Editor noise left unstaged: `alagard SDF.asset` (TMP dynamic atlas), `DungeonGrayboxMinimapLayout.asset` and `NetworkPlayer.prefab` (line endings). MCP screenshots under `Assets/Screenshots/` deleted.
