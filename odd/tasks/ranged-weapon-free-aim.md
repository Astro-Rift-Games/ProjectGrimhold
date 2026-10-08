# Ranged weapon free aim (360°)

## Objective and cause
Ranged weapon visuals (Long Bow, Compound Bow, Light Crossbow, Magic Wand, Magic Staff) snap to the six animation facing buckets, while projectiles fire along the continuous aim. Make ranged weapons rotate 360° to the real aim so visual and shot agree.

## Approved contract (user decisions)
- Weapon orbits an anchor; hands are placed procedurally on GripPoint/SecondaryGripPoint. Body keeps the six-bucket animation.
- Weapon follows aim always (not only while attacking). Body keeps facing movement direction. Requires a networked continuous aim.
- Melee and Spellbook unchanged (`ExistingWeapons_KeepTheirHeldPoseBitIdentical` must keep passing).
- Game Design: "15 - Targeting de Habilidades" section 4.2 (aim from cursor/right stick, independent of movement, all directions valid).

## Scope and exclusions
In scope: networked `AimDirection`, ranged fire uses it, pure free-aim pose math, per-weapon `WeaponAimMode` opt-in, presenter LateUpdate override for WeaponDriven and HandHeld rigs, free-aim VFX anchoring, five ranged weapon assets, Architecture docs.
Out of scope: gamepad right-stick aim (no input action exists), clip regeneration, melee changes, dependency changes.

## Routing and checks
Substantial ODD, no SDD. Branch `feat/ranged-weapon-free-aim` from `New-Testing` (be0a3913). Strict TDD enabled (global CLAUDE.md); runner: Unity Test Framework via Unity MCP `run_tests` (EditMode/PlayMode). Writer delegation: units touch 2+ non-trivial files. Delivery strategy: ask-on-risk.

## Tasks
- [x] T1 Networked `AimDirection` + `PlayerAimMath.ResolveAimDirection` (zero sentinel keeps previous aim; restore-safe init). Route: delegated writer.
- [x] T2 Ranged path in `TryExecuteAttack` uses `AimDirection`; melee keeps `FacingDirection`. Route: delegated writer.
- [x] T3 `RangedWeaponAimPoseMath` pure type + EditMode tests. Route: delegated writer.
- [x] T4 `WeaponAimMode` in `PresentationConfig` + validation tests. Route: delegated writer.
- [ ] T5 `PlayerWeaponPresenter` free-aim override for both rigs (+ prefab anchor if needed). Route: delegated writer + Unity MCP inspection.
- [ ] T6 Free-aim VFX anchoring (bow shot, Cast Flash). Route: delegated writer.
- [ ] T7 Set `FreeAim` on five ranged assets; update PlayerCombatArchitecture.md and PlayerMovementArchitecture.md. Route: delegated writer.

## Evidence
- T1 `2bcabb38`: RED = compile errors (`ResolveAimDirection` missing). GREEN = EditMode job c5138816 PlayerAimMathTests 24/24; PlayerMovementRulesTests job 28be9075 23/23.
- T2 `e6181542`: direction choice extracted to pure `PlayerAimMath.TryResolveAttackDirection` (PlayMode fixture drives a full Fusion runner; no per-direction seam). RED = compile errors. GREEN = EditMode job bbcf1e5c PlayerAimMathTests 30/30; PlayMode job 5acf105c PlayerCombatNetworkControllerPlayModeTests 13/13.
- T4 `d513c4f7`: RED = compile error (`WeaponAimMode` missing). GREEN = EditMode job e44ec145 WeaponAimModeValidationTests 6/6.
- T3 `2d06f5ea`: RED = compile error (`RangedWeaponAimPose` missing). GREEN = EditMode job 470c2d17 RangedWeaponAimPoseMathTests (211 incl. ranged cases) all passed.
- Regression: PlayerWeaponPresentationMathTests job c7aca975 31/32; the one failure (`PlayerVariants_ReuseAnimatorOwnedHeldVisualHierarchy`) loads `NetworkPlayerMelee/Ranged.prefab`, which are not tracked in git (pre-existing). Full EditMode job 262b2a59 has other pre-existing failures (missing prefabs, unrelated) and also regenerated `.anim` files, which were reverted.

- Native review T1–T4 slice (023bf183..9d744dcf): assessed medium, `slice_budget_reached`; user declined (candidate-scoped). Unity-regenerated `alagard SDF.asset` candidate: user declined. Next review base: 9d744dcf.

## Next step
T5 presenter free-aim override. Open: `StanceOffset` is not consumed by any runtime script (only validated), so T5 needs its own anchor.
