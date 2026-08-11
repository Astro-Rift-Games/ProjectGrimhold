# Local line-of-sight presentation

## Context

Line of sight is a local presentation concern. Each peer must reveal world
entities from its own raid avatar position without changing authoritative
gameplay, Fusion interest, interaction, or extraction state.

## Decision

`LocalPlayerVisibilityBinder` is attached to each raid avatar, but only the
avatar with Input Authority that is currently assigned to its participant may
activate the `VisibilityMesh` child. It registers that one
`VisibilityMeshBuilder` with the scene `EntityVisibilitySystem`.

The scene system never discovers an arbitrary builder. It evaluates registered
`IVisibilityTarget` instances against the explicitly registered local polygon.
When an avatar is replaced or despawned, its binder unregisters only its own
builder. Proxies leave their visibility mesh and mask camera inactive, so they
cannot overwrite shader globals for the local peer.

## Target presentation

`EntityVisibilityPresenter` owns renderer visibility only. Point-like entities
use their configured visibility point. Large authored entities may opt into
sampling their collider center and inset corners; `ExtractionSanctuary` uses
this mode so entering any meaningful part of its footprint reveals it.

No LOS result is networked and no RPC, event bus, gameplay timer, or scene-wide
per-frame lookup is introduced. Visibility may be recalculated freely because
it has no simulation side effects.

## Validation

Automated composition tests require raid avatars to include the binder with an
inactive default visibility child, and Sanctuary to opt into collider-based
sampling. Host/Client validation must prove that moving a remote avatar never
changes the local peer's revealed world.
