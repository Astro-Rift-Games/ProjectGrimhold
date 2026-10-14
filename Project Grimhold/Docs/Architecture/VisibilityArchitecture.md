# Local vision: facing cone and near radius

## Context

The map of a top-down 2D raid is always fully visible, so entity visibility is
what limits what a player can see. Each peer hides world entities its own raid
avatar cannot see, without changing authoritative gameplay, Fusion interest,
interaction, or extraction state.

Vision has no in-game presentation: no fog, darkening, mask, mesh, or visible
cone. Floors and walls always render. Only entities are shown, faded, or hidden.

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
