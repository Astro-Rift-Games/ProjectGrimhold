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
- [x] T1b Fix HUD prefab defects found by failing tests: RaidDuoHud root starts hidden; distinct `AttackText` label; invariant-culture cooldown seconds (commit: `fix(hud): ...`)
- [x] T2 Split `_extractionText` into quota, sanctuary and ritual sections (view, presenter, tests) (commit: `refactor(hud): split extraction text into quota, sanctuary and ritual sections`)
- [x] T3 Unified right panel: minimap, objectives, assigned sanctuary, ritual status (commit: `feat(hud): group minimap, objectives, sanctuary and ritual in a right panel`)
- [x] T4 Expedition progress indicator from `ExtractionProgressSnapshot` (commit: `feat(hud): add individual expedition progress bar`)
- [-] T5 Inventory capacity bar (no monetary value) — dropped by user: no bar needed, keep the existing "Inventario: n / m" text. Its leftover layout fix (overlap in the left frame) moves to T6.
- [x] T6 Bottom bar re-layout: weapon cooldown + abilities Q/E in one even 64x64 row; vitals block compacted, no overlap; no Quick Slot placeholders (commit: `feat(hud): align weapon and ability slots and compact the vitals block`)
- [ ] T6b Remove the "Inventario: n / m" text from the HUD (user decision), shrink vitals block accordingly
- [ ] T7 Interaction prompt with per-action key (check Input Actions asset first)
- [ ] T8 Update `Docs/Architecture/RaidMainHudArchitecture.md` for the changed contract

## Progress
T1 done: 10 new EditMode tests green (DungeonPressureHudPresenterTests). Route: delegated writer (writer trigger: prefab + 3 scripts + tests).
Unverified: Play Mode Host/Client; controller resolution via MatchController has no automated test.
Pre-existing failures on the base (identical with changes stashed): 3 EditMode Presentation tests and 6/10 RaidMainHudPlayModeTests, including NetworkPlayerPrefabHasOneCompleteRaidHudOnExistingCanvas.
Unity-touched assets (alagard SDF, EditorBuildSettings, EditorSettings) deliberately excluded from commits.

T1b done: RaidHudPresenterTests and RaidTeammateHudTests 35/35 green; PlayMode ClearAndPresentationOperationsKeepSafeValuesAndDefeatVisible green. Remaining failures are not HUD-rework scope: stale tests (deleted Melee/Ranged variants, duplicated return-gate row) handed to a separate task, and `NetworkPlayerPrefabHasOneCompleteRaidHudOnExistingCanvas` now fails later on `RaidMenuView.ProgressionExperienceFill` being null in NetworkPlayer.prefab (to investigate). New `AttackText` child is empty and not a raycast target; not checked visually in the Game view.

T2 done: RaidHudPresenterTests 30/30, RaidTeammateHudTests + DungeonPressureHudPresenterTests 20/20, RaidMinimapPrefabTests 5/5; PlayMode RaidMainHudPlayModeTests same 6 baseline failures, none new. RED was compile-level only.
Behavior change: "Cuota completada" is now persistent in the quota section (was transient and beat "Santuario asignado"); `_quotaCompletedFeedbackDuration` removed. Needs user confirmation.
Unverified: Game view layout of the new labels (QuotaText y -203, SanctuaryText y -235, panel height 279); prefab YAML for RaidMainHud was hand-written and loaded fine in tests but was not inspected in the Inspector.
`ExtractionArchitecture.md` still describes the single text: covered by T8.

T3 done: new `RaidRightPanel` column (top-right, 240 wide, VerticalLayoutGroup) in LocalGameplayHud with RaidMinimap, ObjectivesBlock, SanctuaryBlock, RitualBlock. RaidRightPanelPrefabTests 5/5; EditMode Presentation 607/609 (2 known baseline); PlayMode RaidMainHudPlayModeTests 4/10, same 6 baseline failures.
Deviations: labels are NEW objects (nested prefab children cannot be reparented), `_quotaText`/`_sanctuaryText`/`_extractionText` repointed; old labels in RaidMainHud.prefab only deactivated (delete by hand in Editor). LocalGameplayHud.prefab diff is large because Unity reserialized it.
Visual check: offscreen RenderTexture renders only (no Game view/Play Mode); minimap slot rendered empty; concept image was not available to the writer (wrong path given).
Known leftover: RaidMainHud left frame still has empty row and Stamina/Inventory overlap (T5/T6).

T4 done: horizontal bar + "78%" under the quota text in ObjectivesBlock (not a ring: UI.png sprites are dark/ornate and a sprite-less Filled Image ignores fillAmount; follows the health/stamina scale.x convention). Fraction math in `ExpeditionProgressMath` (clamped, floors so 199/200 never shows 100%). Individual progress, no team wording. EditMode Presentation 620/622 (2 known baseline); PlayMode 4/10 (same 6 baseline). RED compile-level only.
Visual: offscreen render at 78/100 seen; percent text sits close to the frame corner ornament. Not seen in Game view/Play Mode; concept image not viewed by the writer.
Open question for user: concept shows a round ring bottom-left; ring could be done later with a dedicated sprite.

T6 done: weapon (x -76), Q (0), E (+76), all 64x64 at the bottom center; vitals frame 279 -> 156 high; RaidDuoHud moved to y -250; NetworkPlayer.prefab hand-edited (Unity reserialization was ~7k lines, reverted). RaidBottomBarLayoutTests 9/9 (RED 7/9 first); EditMode Presentation 631 run, only the 2 known baseline failures; PlayMode 4/10, same 6 baseline.
Side effect: the T6 writer discarded uncommitted leftovers of the interrupted T5 (RaidHudPresenter.cs, RaidHudPresenterTests.cs, RaidMainHudPlayModeTests.cs reverted, RaidCapacityWidgetPrefabTests deleted); backup patch in the session scratchpad. Nothing committed was affected.
Unverified: Game view/Play Mode; ability/cooldown states not exercised visually; InteractionPrompt (y=120) and ability messages share the bar's vertical band (T7).

## Next step
Start T6b, then T7.
