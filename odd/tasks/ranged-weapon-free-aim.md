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
- [ ] T1 Networked `AimDirection` + `PlayerAimMath.ResolveAimDirection` (zero sentinel keeps previous aim; restore-safe init). Route: delegated writer.
- [ ] T2 Ranged path in `TryExecuteAttack` uses `AimDirection`; melee keeps `FacingDirection`. Route: delegated writer.
- [ ] T3 `RangedWeaponAimPoseMath` pure type + EditMode tests. Route: delegated writer.
- [ ] T4 `WeaponAimMode` in `PresentationConfig` + validation tests. Route: delegated writer.
- [ ] T5 `PlayerWeaponPresenter` free-aim override for both rigs (+ prefab anchor if needed). Route: delegated writer + Unity MCP inspection.
- [ ] T6 Free-aim VFX anchoring (bow shot, Cast Flash). Route: delegated writer.
- [ ] T7 Set `FreeAim` on five ranged assets; update PlayerCombatArchitecture.md and PlayerMovementArchitecture.md. Route: delegated writer.

## Evidence
(none yet)

## Next step
T1–T4 via one delegated writer.
