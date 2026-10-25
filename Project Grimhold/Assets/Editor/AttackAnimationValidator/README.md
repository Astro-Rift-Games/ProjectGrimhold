# Attack Animation Validator

Inspect equipment attack and defense presentation across all six facings without entering Play Mode. Open **Tools → Animations → Attack Animation Validator**.

## Quick path

1. Find an equipment/action row and select a facing cell.
2. Play or scrub the timeline; use **Validate Visible** for the filtered cells or **Validate All** for the entire catalog (both are cancelable).
3. Inspect warnings/failures in the detail panel, ping the source assets, then confirm runtime-dependent results in Play Mode.

## Grid and controls

The catalog has an **asset-dependent equipment/action row count × 6 facings** (`N`, `NE`, `NW`, `S`, `SE`, `SW`). Rows cover `MainAttack`, `OffAttack` and `Defend` for legal one-handed, two-handed, dual-wield and shield/weapon combinations. There are no simultaneous-start cells: runtime accepts at most one attack per tick, Main first ([combat contract](../../../Docs/Architecture/PlayerCombatArchitecture.md#equipped-hand-attacks-task-222)). The count follows the assets, not a fixed UI limit.

- **Filters:** weapon-name search, equipment kind, action, facing and validation outcome.
- **Timeline:** Play/Pause, Loop, 0.25×/0.5×/1× and normalized scrubbing; counters show Fail, Warning, Pass and Unvalidated.
- **Cache:** Samples/cell controls thumbnail sampling (default 16), Max textures bounds the cache (default 2048), and Tail adds post-action time (default 0.1 s). Thumbnail sampling is separate from validation's default **60 Hz**.
- **Detail:** live preview, optional Main/Off counterpart, independent detail scrubbing or global timeline follow, and grip markers (**cyan:** drawn-hand centroid; **magenta:** grip transform). Per-check range bars show Main above Off; click to scrub. Worst measured checks include values and thresholds. **Ping weapon** selects the source LootDefinition; **Ping clip** selects its directional attack clip when available.

## Checks and defaults

Distances use holding-hand sprite pixels and account for transforms/flips. Checks run only where applicable; a pass is not proof that every check applied.

| Check | Measurement and default result thresholds |
| --- | --- |
| `WeaponGripOnHand` | Weapon sprite's configured grip point to the alpha-weighted drawn-hand centroid: warning **>1.5 px**, error **>3 px**. |
| `GripTransformOnHand` | Holding grip transform to that centroid: warning **>1.5 px**, error **>3 px**. |
| `HandSpriteIdentity` | Sprite must belong to the anatomical hand's idle-clip references; mismatch/missing or fully transparent hand is an error. If references are unavailable, a matching RightHand/LeftHand name still warns; a mismatch errors. |
| `DepthOrder` | Visual body-side contract: weapon and holding hand must compare to the body on the same side by sorting layer/order; disagreement errors. This does not establish fist visibility. |
| `VfxOrigin` | Release/shot and cast flashes compare their visible origin to the resolved authored facing pose, or the live pivot/grip-resolved pose for aim-driven weapons (**1.5 px** tolerance). Canonical one-handed slashes independently compare to the **VisualRoot body origin**; other slash/thrust effects retain the grip-relative **BladeReach + 1.5 px** radius. Excess warns; configured effects that never appear also warn. |
| `SlashArcContract` | **Error**, applicable to Main/Off attacks using the Mirrored one-handed slash sets (ArmingSword/Sword1H and MagicSword). Across the generated source-derived stroke window, measure the grip relative to the body pivot in `(forward,right)` coordinates. Main must start back-right and end left with **−180° ±30°** unwrapped sweep; Off starts back-left and ends right with **+180° ±30°**. Both must pass through the forward hemisphere. Quadrant epsilon and angle-ignore radius are **2 holding-sprite pixels**. Blade-tip and visible VFX opaque-centroid sweeps must share the hand's sign; missing/insufficient samples fail. Exact stroke endpoints are included alongside the validation grid. Anticipation/recovery are excluded; whole-clip net sweep would hide the stroke because clips return to neutral. |
| `ReturnsToIdle` | After action duration, each hand layer must be in its idle state. Compare hand/grip (and applicable pivot) against the independent idle clip sampled at that layer's **current Animator state time**, not idle frame zero: position within **1.5 px**, rotation within **0.1°**, scale within **0.001** vector distance, and the exact idle sprite at that time; no transient Off Hand overlay may remain. Violations error and identify the failing state/position/rotation/scale/sprite/overlay condition. |
| `HandOccludedByWeapon` | **Weapons only** (including a two-handed weapon's support hand). Fraction of nontransparent hand texel centers covered by nontransparent weapon texels when weapon sorts in front: warning **>25%**, error **>50%**. Tied sorting with any overlap warns because draw order is unknown. **Shields are excluded**: shield coverage of its holding hand, including Defend presentation, is intentional. |

WeaponDriven rigs have a **LeftHand weapon holder** (checked against the main weapon's GripPoint, including depth and occlusion) and a **RightHand string hand** (not a held-weapon geometry/depth/occlusion participant; its attack VFX is still checked). HoldsSecondaryGrip support hands remain checked at rest and through attacks. Their persistent positional pin intentionally replaces locomotion hand translation; idle rotation, scale, sprite, pivot and grip restoration still apply, and the pinned position is independently checked against SecondaryGripPoint.

Defaults live in `AttackPreviewCheckSettings.cs`; measurements and applicability live in `AttackPreviewChecks.cs`.

## How it works and where it stops

`AttackPreviewRig` clones the real `Assets/Prefabs/NetworkPlayer.prefab` in an isolated preview scene. It deliberately does not create a connected Editor prefab instance: those forbid runtime weapon reparenting between MainHandGrip and WeaponPose. Deterministic `PreviewPlayerPresentationState` mirrors runtime equipment semantics (including raw hand slots, Main-hand equipped lookup and two-handed Off blocking), using preview-local catalog identities. Absolute seeks reset presentation and drive the real Animator, weapon, VFX and armor presenters through the presentation seam, rather than duplicating their pose logic.

This is an **Edit Mode specimen**: healthy, stationary, no Fusion runner/network simulation, no aim stance and no consumption. It does not validate participant attributes, ownership, migration or network event delivery. Occlusion measures **sprite alpha**, not shader output. Confirm anything depending on runtime-only state in **Play Mode**; these diagnostics do not replace visual/runtime checks.

## Extend

- **Add a check:** add an `AttackPreviewCheckId`, implement its applicable measurement in `AttackPreviewChecks`, and add configurable thresholds to `AttackPreviewCheckSettings` when needed. Record passing measurements as well as violations so reports retain useful values; cover it with the existing EditMode validator tests.
- **Add combinations:** valid weapon/shield `LootDefinition` assets are discovered automatically through the shared equipment legality rules. No hand-maintained row list is required; asset changes refresh the catalog and invalidate previews.
