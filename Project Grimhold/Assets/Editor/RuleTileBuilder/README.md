# Build a RuleTile without configuring neighbor matrices

[Guía en español](README.es.md)

Open **Tools > Grimhold > Rule Tile Builder**. This is an Editor-only authoring tool for the installed native square-grid RuleTile. No additional package is needed.

Hover over controls for English contextual tooltips, including destructive-action boundaries and native matrix/output options. Display labels and actions remain unchanged.

## First tile

1. **Sprites:** drag an already sliced texture or individual sprites into the library. Search by name. Ctrl/Cmd-click selects multiple variations. Unsliced textures are not changed automatically.
2. Set a **fallback sprite** and default collider. The fallback fills any connection you have not supplied. `None` is the initial collider; existing scene colliders are never edited.
3. **Template:** choose continuous floor (1 card), orthogonal connections (16 cards), or full contour including inner corners (47 cards). The little diagrams describe the surface, not the shape of the artwork: blue neighbors connect, dark neighbors are empty/other, faint neighbors can be either.
4. **Assign pieces:** select the matching sprite(s), then click **Assign selected** on a card, or drop sprites on its thumbnail. Multiple sprites on a new card use native Random output. Assign only artwork you actually have; sprites are not rotated or invented automatically.
5. **Paint & test:** paint/erase a small room. Right-click erases. **Other tile** tests boundaries with a different tile. Try Rectangle, L, Hole, Corridor and Isolated. Orange overlays identify fallback cells; hover a cell for its visual and animation rule numbers.
6. **Save:** review the diagnostics, choose a name and new `.asset` location, and generate. Warnings require explicit confirmation. Errors cannot be confirmed away. Drag the saved asset into the standard Tile Palette.

## Edit later

Open the saved RuleTile in the top field. The window edits a temporary native draft; the source and its GUID do not change until **Save changes to original asset**. Missing optional cards can be completed later. The native Inspector remains available.

- Reload, New and switching assets offer Save / Cancel / Discard when the draft is dirty. The window also participates in Unity's native unsaved-window close dialog.
- Undo/Redo applies to draft edits and saved in-place updates. As with Unity asset editing generally, save after undoing an asset update to persist that undo to disk.
- A template switch explicitly replaces the draft rules. Do not switch templates just to edit one sprite. Custom rules and their order are preserved on open.
- Template recognition uses native conditions, not hidden metadata. Sparse subsets can match more than one template; the least complex matching template is selected. Advanced always exposes every stored rule.
- Saved assets survive reload/restart. Preview layouts are temporary. Save before restarting Unity; this is not a separate recovery system for unsaved work.
- External source changes block an update. Reopen the source to start a fresh draft, or retain your work by generating a separate new asset from a separate window. Occupied paths are never overwritten by Create.
- Updating a tile referenced by a RuleOverrideTile is blocked because Unity's native save also regenerates dependents. Use the standard Inspector for that workflow rather than modifying additional assets silently.

## Advanced

The Advanced foldout exposes ordered native rules, duplicate/delete/move operations, the installed native 3×3 matrix and Single/Random/Animation controls. It is optional for template-based authoring. Green means same surface (`This`), red means other/empty (`Not This`), and blank means either (`Don't Care`). Rotation/reflection only happens when explicitly enabled here.

Diagnostics examine all 256 neighborhoods for the supported eight-neighbor square grid, including native rotation/mirror transforms. Visual priority is top to bottom. Unity independently searches animation rules, so a visually hidden rule can still supply animation. Unsupported derived/hex/isometric/custom-neighbor/extended-neighborhood tiles are rejected rather than truncated.

## Catacombs

The BaseTileset, FloorVariantsTileset and WallVariantsTileset are already sliced. Names alone do not identify connectivity; inspect each piece before assignment. The sample floor tile uses existing floor artwork, not generated art. Multi-row walls/arches are several art pieces and are not constructed automatically by one RuleTile. The sandbox does not simulate collider physics or modify any existing Tilemap.

Sample: `Assets/Art/Tilesets/Catacombs/RuleTiles/CatacombsFloorRuleTile.asset`. It is a continuous Random floor using FloorVariants sprites `_0` and `_1`, fallback `_4`, and no collider. It intentionally does not claim finished Catacombs wall/edge mappings.

## Verification

Focused fixtures: `RuleTileBuilderCoreTests`, `RuleTileBuilderAssetTests`, `RuleTileBuilderPreviewTests`, `RuleTileBuilderWindowTests` in the existing EditMode Editor test hierarchy. They cover templates, 256-mask comparison with native Tilemap for all transforms, diagnostics, native animation, isolation/cleanup, GUIDs, external-change/overwrite/dependent guards, draft assignment and Undo/Redo.

Actual Editor restart and human visual approval are separate checks; automated asset reload and synthetic sprite tests do not prove either.

Observed on 2026-10-19: Unity compilation succeeded; all 43 focused EditMode cases passed (0 skipped); an independent persistence pass found no reproduced blocker; the generated Catacombs sample was reopened and painted into 48 cells with the native GridBrush in an isolated preview. Native render output was captured and inspected. Existing scenes, prefabs, importer settings, runtime code and dependencies were unchanged.

Still manual: actual Editor restart, end-to-end mouse/drag interaction in the Tile Palette window, Save/Discard/Cancel dialog interaction, and human visual/usability approval. The native brush API probe is not a claim that those mouse-driven workflows were exercised.
