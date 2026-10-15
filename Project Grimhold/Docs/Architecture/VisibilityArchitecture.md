# Local vision: facing cone and near radius

## Context

The map of a top-down 2D raid is always fully visible, so entity visibility is
what limits what a player can see. Each peer hides world entities its own raid
avatar cannot see, without changing authoritative gameplay, Fusion interest,
interaction, or extraction state.

Floors and walls always render. Only entities are shown, faded, or hidden by
vision. The only in-game presentation of vision is an atmospheric fog that stays
clear inside the vision shape (see Vision fog); it softens the cone and adds
dungeon mood, but it never decides what is visible.

## Vision rule

Entities follow one of two rules, chosen per prefab on its
`EntityVisibilityPresenter`:

- **Dynamic entities** (players, enemies, projectiles, protective orbs) are seen
  when both conditions hold:
  1. They lie within `NearRadius` in any direction, or inside the cone of
     `ConeAngleDegrees` centered on the avatar's
     `PlayerMovementNetworkController.FacingDirection`. The cone has no range
     limit. During aim stance the facing already follows the aim through the
     shared facing rule.
  2. Nothing blocks the line of sight (see below).
- **Static objects** (loot containers, ground loot, breakables, sanctuaries,
  scene traps, player-placed traps) are marked `_occlusionOnly`. They ignore
  facing and distance and are hidden only when the line of sight is blocked, so
  turning around never makes a chest in the same room disappear.

The line of sight is blocked when a `Physics2D.Linecast` from the viewer to the
point hits a non-trigger collider on `ObstacleLayer` (walls, doors, obstacles),
ignoring colliders that belong to the target itself or to the viewer's avatar.
Obstacles are the only distance limit of vision. Occlusion queries live
colliders every frame, so a door or obstacle whose collider appears, moves, or
disappears at runtime blocks or unblocks vision without any cache refresh.

`VisionConeRules` holds the pure shape math. `VisionQuery` adds occlusion with a
reusable hit buffer and no per-frame allocation.

## Transitions

Visibility changes are softened instead of switching in one frame:

- Losing sight keeps the entity fully visible for `GraceSeconds`, so small
  facing changes do not make it flicker.
- After the grace period it fades out over `FadeOutSeconds`.
- Coming into sight fades it in over `FadeInSeconds`, starting from its current
  opacity, and restarts the grace period.

`VisibilityFadeRules` holds this pure, frame-rate independent logic. All balance
values (shape, obstacle layer, and transition timings) live in the
`VisibilitySettings` ScriptableObject and reach the system through the viewer's
`VisionQuery`.

## Ownership

`LocalPlayerVisibilityBinder` is attached to each raid avatar. Only the avatar
with Input Authority that is currently assigned to its participant registers its
`LocalVisionSource` with the scene `EntityVisibilitySystem` as the single
`IVisionViewer`. When an avatar is replaced or despawned, its binder unregisters
only its own viewer. Proxies never register their vision source.

While the local player spectates after definitive defeat,
`LocalRaidSpectatorController` sets the spectated avatar's `LocalVisionSource`
as the spectated viewer, so the spectator sees exactly what that teammate sees
(using its replicated facing). The spectated viewer takes precedence over the
local viewer and is cleared when the target changes or spectating ends.

`EntityVisibilitySystem` evaluates every registered `IVisibilityTarget` in
`LateUpdate` and pushes its opacity:

- The target owned by the viewer's own avatar is always seen.
- Without any viewer (before spawn, during avatar replacement, or while
  spectating with no valid target) every target is hidden immediately. The
  system fails closed instead of keeping the last state.
- Newly registered targets start hidden and fade in once seen.

Teammates follow the same rule as every other dynamic entity.

## Target presentation

`EntityVisibilityPresenter` owns renderer visibility only and must list every
renderer of its entity, including equipment sprites and particle systems, so no
part leaks through walls.

- At zero opacity it sets `Renderer.forceRenderingOff`, which leaves
  `Renderer.enabled` to other presenters such as armor, weapon, and VFX.
- While partially visible, sprites fade by multiplying their own color alpha.
  Colors written by other presenters (damage flash, sanctuary states, armor
  tints) are adopted as the new base, and the base color is restored exactly
  once the entity is fully visible or hidden. Non-sprite renderers such as
  particle systems stay rendered until the fade ends.

Point-like entities use their configured visibility point. Large authored
entities may opt into sampling their collider center and inset corners;
`ExtractionSanctuary` uses this mode so seeing any meaningful part of its
footprint reveals it.

No vision result is networked and no RPC, event bus, gameplay timer, or
scene-wide per-frame lookup is introduced. Vision may be recalculated freely
because it has no simulation side effects.

## Vision fog

`VisionFogPresenter` draws a fog over the whole Gameplay world that stays clear
inside the active viewer's vision shape. It is a prototype of local presentation
only: nothing is networked, it has no gameplay side effects, and it never
decides entity visibility, which stays owned by `EntityVisibilitySystem`.

- **Shape source.** Each `LateUpdate` the presenter reads
  `EntityVisibilitySystem.TryBuildActiveQuery`, so it follows the same viewer
  as entity visibility (spectated viewer first, then the local viewer) and the
  same shape (`NearRadius`, `ConeAngleDegrees` from `VisibilitySettings`, exposed
  on `VisionQuery`). There is one source of truth for the cone.
- **Occlusion-aware clear area.** `VisionFogMask` casts a fan of rays from the
  viewer each frame: dense rays across the facing cone (`ConeRayStepDegrees`,
  long enough to leave the camera view) and sparse near-radius rays around the
  rest of the circle (`OuterRayStepDegrees`), with a paired ray at each cone
  edge (`VisionFogFanRules`). Each ray stops at
  `VisionQuery.CastOcclusion`, the same obstacle rule entity visibility uses
  (`ObstacleLayer`, no triggers, the viewer's own colliders ignored), plus
  `WallRevealDepth` so the face of the blocking wall is revealed. The
  resulting polygon is drawn into a low-resolution mask render texture
  (`MaskResolutionScale` of the camera size) covering the camera view, and the
  fog shader samples it with a small blur (`EdgeSoftness`) and noise jitter
  (`EdgeNoiseAmplitude`). The fog therefore stops at walls exactly where
  entities are hidden by occlusion, instead of shining over them.
- **Cost.** About 140 raycasts per frame with the default steps, one small
  mesh update and one draw into the mask, all without per-frame allocations.
  Only the local peer pays it.
- **Fail-closed.** Without a viewer the clear shape fades out over
  `ClearFadeSeconds` and the fog covers everything, matching the hidden
  entities.
- **Feel.** Facing is smoothed in presentation only (`FacingSharpness`), so
  the clear cone turns smoothly while entity visibility keeps using the raw
  facing. Edges are softened by the mask blur and broken up with noise,
  fog creeps back inside the cone with distance (`FarFog*`), and faint wisps
  remain inside the clear shape (`InsideDensity`). The noise is procedural, in
  world space, drifts with `WindVelocity` and is snapped to `PixelsPerUnit` to
  match the pixel art.
- **Rendering.** `Grimhold/Visibility/VisionFog` is a hand-written URP unlit
  shader drawn by the 2D Renderer on a built-in quad (`VisionFog` in
  `Gameplay.unity`). It sits on the `Fog` sorting layer, above every world
  layer. Walls share the `Characters` layer with characters, so the fog cannot
  sit between them; this is consistent because dynamic entities are only
  visible inside the clear shape, and static occlusion-only objects outside it
  stay visible under partial fog. Screen-space UI draws above it. The quad is
  moved and scaled to cover the orthographic camera in
  `RenderPipelineManager.beginCameraRendering`, after camera followers move,
  so it never lags a frame. It is one full-screen transparent draw with no
  renderer feature and no package dependency.
- **Configuration.** Art values live in the `VisionFogSettings`
  ScriptableObject, separate from the balance values in `VisibilitySettings`.
  The presenter pushes them every frame through a `MaterialPropertyBlock`, so
  they can be tuned live in Play Mode. The project renders in linear color
  space, so a dark fog looks lighter than its alpha suggests; tune
  `FogColor` alpha with that in mind.

`VisionFogRules` holds the pure presentation logic: facing smoothing, the
clear-strength fade and the camera coverage.

### Possible improvements

These are not implemented. They are listed so the prototype can grow
deliberately once the team validates the look.

1. **Smarter ray placement.** Cast extra rays toward obstacle corners
   (shadow-casting) so wall edges stay crisp at long range with fewer rays.
2. **Lights thin the fog.** Pass a bounded list of active torches and braziers
   (position, radius) to the shader so lit areas are clearer. This fits the
   Game Design rule that torch light is shared by every nearby player and
   reveals that someone passed through.
3. **Explored memory.** Keep a persistent low-resolution texture per raid of
   the areas already seen and draw a lighter fog over them. This changes
   gameplay readability and needs a Game Design decision first.
4. **Depth-based look.** Vary `FogColor`, density and wind per dungeon floor,
   following the environmental progression by depth in the art direction.
   Implement it by swapping `VisionFogSettings` assets per floor.
5. **Accessibility and options.** A player setting to lower fog intensity or
   disable its motion, for contrast and motion sensitivity.
6. **Gameplay-driven feel.** Short presentation responses such as a
   denser, closer fog while downed, a pulse on taking damage, or a widened
   clear area during specific abilities. Each one must stay presentation-only.
7. **Fog over entity edges.** Fade entities in sync with the fog edge (or
   derive the visibility fade from the same smoothed facing) so an entity never
   pops in visibly sharper than the fog around it.
8. **Layered fog.** A second, slower and larger noise layer, or a separate
   fog below the walls, for parallax depth.
9. **Retire the legacy visibility mesh.** `Assets/Art/Shaders/VisibilityMesh.shader`
   (built-in pipeline CG) and its material are leftovers from an earlier
   approach, now superseded by `VisionFogMask`; remove them once their test
   reference is checked.
10. **Validation tooling.** A debug toggle that fakes a viewer at the camera
    center, so the look can be tuned without starting a Fusion session.

## Debugging

- `LocalVisionSource` draws Scene-view gizmos: the near radius, the cone edges,
  and a fan of rays cut where obstacles block them (blocked points are marked).
  Rays that hit nothing are drawn to a visual-only length.
- In Play Mode each `EntityVisibilityPresenter` draws its state: green fully
  visible (including grace), yellow fading, red hidden.

## Limitation

This is local presentation only. Every client still receives the state of every
entity, so a modified client can see through walls. Real PvP protection
requires Fusion interest management and is outside this contract.

## Validation

- EditMode `VisionConeRulesTests` cover the cone edge, the near radius behind
  the viewer, the unlimited cone range, and the facing fallback.
- EditMode `VisibilityFadeRulesTests` cover fade-in, grace, fade-out, frames
  crossing the end of grace, regaining sight mid-fade, and instant timings.
- EditMode `VisionFogRulesTests` cover facing smoothing (snapping, shortest
  arc, frame-rate independence), the clear-strength fade and camera coverage.
- EditMode `VisionFogFanRulesTests` cover the ray fan: paired cone-edge rays,
  ordering and step limits, facing shift, full and zero cones, capacity and
  short buffers.
- PlayMode `VisionOcclusionPlayModeTests` also prove `CastOcclusion` stops at a
  wall face and ignores clear rays, the viewer's own collider and triggers.
  The fog look itself requires manual Play Mode review.
- EditMode `VisibilityPrefabCoverageTests` require each hidden entity prefab to
  list every renderer it owns and to use the expected static or dynamic rule.
- Composition tests require raid avatars to include the binder with a
  `LocalVisionSource` referenced by it, and Sanctuary to opt into
  collider-based sampling.
- PlayMode `VisionOcclusionPlayModeTests` prove obstacle occlusion, self-collider
  immunity, runtime-collider blocking, the near radius, unlimited cone range,
  occlusion-only static objects, grace and fade-out with tint preservation,
  fail-closed behavior without a viewer, the viewer's own avatar staying
  visible, and spectated vision taking precedence over the local viewer.
- Host/Client validation must prove that moving a remote avatar never changes
  the local peer's revealed world.
