# Raid HUD rework

Branch: `feat/raid-hud-rework` (from `feat/ability-trap`)
Plan: `C:\Users\Dani\.claude\plans\hay-que-hacerle-un-binary-torvalds.md`
TDD: enabled (Strict TDD, from user config). Runner: Unity Test Runner (EditMode/PlayMode) via Unity MCP.
Delivery strategy: ask-on-risk.

## Objective
Rework the Raid HUD using the concept image as layout reference while respecting the GDD and `RaidMainHudArchitecture.md`. The HUD stays presentation-only (no new networked state, no RPCs).

## Decisions
- Bottom bar follows the GDD: weapon sets A/B, abilities Q/E, 4 Quick Slots. No 10-slot hotbar.
- Loot value stays out of the always-visible HUD (architecture contract).
- Expedition progress is individual (MVP), not team.
- Out of scope: enemy health bars, level/XP in Raid, universal Raid timer (need design decisions).

## Tasks
- [x] T1 Move `DungeonPressureHudPresenter` to `LocalPlayerHudBinder` (remove `FindObjectOfType`) and add tests (commit: `feat(hud): bind dungeon pressure HUD through LocalPlayerHudBinder`)
- [ ] T2 Split `_extractionText` into quota, sanctuary and ritual sections (view, presenter, tests)
- [ ] T3 Unified right panel: minimap, objectives, assigned sanctuary, ritual status
- [ ] T4 Expedition progress indicator from `ExtractionProgressSnapshot`
- [ ] T5 Inventory capacity bar (no monetary value)
- [ ] T6 Bottom bar re-layout: weapon cooldown + abilities Q/E; Quick Slot layout placeholder
- [ ] T7 Interaction prompt with per-action key (check Input Actions asset first)
- [ ] T8 Update `Docs/Architecture/RaidMainHudArchitecture.md` for the changed contract

## Progress
T1 done: 10 new EditMode tests green (DungeonPressureHudPresenterTests). Route: delegated writer (writer trigger: prefab + 3 scripts + tests).
Unverified: Play Mode Host/Client; controller resolution via MatchController has no automated test.
Pre-existing failures on the base (identical with changes stashed): 3 EditMode Presentation tests and 6/10 RaidMainHudPlayModeTests, including NetworkPlayerPrefabHasOneCompleteRaidHudOnExistingCanvas.
Unity-touched assets (alagard SDF, EditorBuildSettings, EditorSettings) deliberately excluded from commits.

## Next step
Start T2.
