# Melee release timing

## Objective
Apply player melee damage on an authoritative `ReleaseTick` that matches the clip moment where the weapon crosses the hit area, sharing one clock with animation and VFX (same model as ranged).

## Problem
`PlayerCombatNetworkController.TryExecuteAttack` executes `MeleeAttack.Execute` on the click tick, and `PlayerWeaponEquipmentNetworkController` forces melee release delay to 0. Damage lands on click while the slash VFX/animation reach the blade-crossing frame later.

## Scope
- Generalize `WeaponDefinition._rangedReleaseSeconds` -> `_attackReleaseSeconds` (`FormerlySerializedAs`).
- New `MeleeAttackRelease` network struct; pending melee release in `PlayerCombatNetworkController`.
- Melee presentation enters the timed path (`HasReleaseTimeline`).
- Calibrate melee weapon assets: `release = vfx.StartSeconds + vfx.ReleaseLeadSeconds`.
- Update `Docs/Architecture/PlayerCombatArchitecture.md`.

Out of scope: enemy melee, lag compensation, continuous sweep hitboxes.

## Acceptance criteria
- No melee damage on acceptance tick when release > 0; damage at `ReleaseTick`.
- Zero delay keeps same-tick execution.
- Pending melee blocks another acceptance; cancelled by downed/disabled/phase/weapon change.
- Origin sampled at release; cooldown starts at acceptance.
- Melee VFX timing visually unchanged; animation/VFX/damage on one clock.

## TDD
Mode: strict (source: user global CLAUDE.md "Strict TDD Mode: enabled"). Runner: Unity Test Runner via Unity MCP `run_tests` (EditMode/PlayMode).

## Delivery
Strategy: ask-on-risk. Forecast: ~300-450 authored lines.

## Tasks
- [x] T1 — Rename release field to attack-wide + consumers/tests (route: delegated writer; trigger: 2+ non-trivial files)
- [x] T2 — `MeleeAttackRelease` + controller pending release + presentation timed path, with RED/GREEN tests (route: delegated writer)
- [x] T3 — Calibrate melee weapon assets + calibration test (route: delegated writer, Unity MCP)
- [x] T4 — Architecture doc update (route: delegated writer)

## Progress / evidence
- Branch `fix/melee-release-timing` from `New-Testing` @ bf204d41.
- T1: 4e5aa04a. Unity compile clean; BowShot/Ranged calibration tests green after fixing the FormerlySerializedAs argument.
- T2: 968efe4b. RED: MeleeAttackReleaseTests 9/13 failing against stub; PlayMode MeleeSwing_* 5/5 failing before controller change. GREEN: 13/13 EditMode, PlayerCombatNetworkControllerPlayModeTests 13/13 (5 new).
- T3: e6c7c3b3. RED: MeleeReleaseCalibrationTests 9/10 failing before asset edit. GREEN after. 9 melee weapons calibrated (incl. LongSwordCombatDefinition, Spellbook).
- T4: eb6ec0c4. Architecture doc updated.
- Full suites at HEAD: EditMode 2553 tests, PlayMode 555 tests; remaining failures are pre-existing/unrelated (missing legacy prefabs, persistence, backend category names, UI presenters). Tool caps failure listing at 30.

## Next step
Manual Host/Client check; review and PR (user decision).
