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
- [x] T6b Remove the "Inventario: n / m" text from the HUD (user decision), shrink vitals block accordingly (commit: `refactor(hud): remove the inventory summary text from the raid HUD`)
- [x] T6c Delete dead labels (InventoryText, ExtractionText, QuotaText, SanctuaryText) from source RaidMainHud.prefab and the dangling removed-object entry in LocalGameplayHud; confirmed no leftovers from the reverted T5 (commit: `chore(hud): delete dead labels from the RaidMainHud prefab`)
- [x] T7 Interaction prompt: key label from a tested constant matching the real Interact binding, "[F] action" format, prompt moved above the bottom bar. Per-action keys NOT done: there is a single Interact action (`<Keyboard>/f`), E is only AbilitySlot2; distinct keys per action would be an input/design change (commit: `feat(hud): show the interact key label and lift the prompt above the action bar`)
- [x] T8 Update `RaidMainHudArchitecture.md`, `ExtractionArchitecture.md` and `PlayerInteractionArchitecture.md` for the changed contract (docs only; commit pending)

## Phase 2 — visual fidelity to the concept (after owner review of the first pass)
Owner ran the game and said the HUD "looks nothing like the reference". Cause: no task compared against the concept or was visually verified. A local, untracked preview harness now exists (`Assets/_HudPreview/`, excluded via .git/info/exclude): menu `Tools/HUD Preview/Build Scene`, Play Mode, `manage_camera screenshot` without camera, output folder `Temp/HudPreviewShots`. Every task below must capture, LOOK at the PNG and compare with the concept image before reporting.
- [x] V1 Vitals bottom-left: clean health/mana/stamina bars with icon, no duplicated labels, left-anchored fills; mana bar (approved by owner; source PlayerManaNetworkController); 3 new 16x16 procedural pixel icons in Assets/Art/UI/Hud (placeholders, replace with real art if available) (commit: `feat(hud): rebuild the vitals block with icon bars and a mana bar`). Seen in the preview harness: three icon + bar rows at the bottom-left, no duplicated text. Unverified: real Fusion mana binding in a live match; DERROTADO red colour not rendered; pre-existing teammate bar draws as a thin line overlapping its text (not touched). Differences vs concept: no round progress ring / level badge, flatter bars.
- [ ] V2 Bottom action bar: key labels (Q/E), icons, weapon slot label, presence similar to the concept; no 10 slots
- [ ] V3 Minimap frame: ornate frame, zone name, compass N, controls, using existing data only
- [ ] V4 Visual style pass: dark ornate frames and gold text across the HUD blocks; pixel font for the pressure timer/phase; interaction prompt framed; debug buttons out of the way

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

T6b done: removed `_inventoryText`/PresentInventory from RaidHudView, loot receiver reads from RaidHudPresenter (Bind overloads lost the PlayerLootReceiver parameter; LocalPlayerHudBinder and tests updated), InventoryText deleted from the LocalGameplayHud instance (nested removal), vitals frame 156 -> 120 high, RaidDuoHud y -214. EditMode 635 run, only the 2 known baseline failures; PlayMode 4/10, same 6 baseline.
Unverified: ALL visuals; the writer could not obtain a usable offscreen render (flat background). Geometry only. Please check the Game view.
Leftovers: the source RaidMainHud.prefab still contains the stale InventoryText object and the inactive Extraction/Quota/Sanctuary labels (delete by hand in the Editor). `RaidMainHudArchitecture.md` still describes the inventory summary (T8).

T6c done: source RaidMainHud.prefab lost the 4 dead labels (552 pure deletions, hand-edited YAML); `RaidMainHudPrefabCleanupTests` 2/2 (only the first was RED); EditMode 637 run with only the 2 baseline failures; PlayMode 4/10 same 6 baseline; console clean of missing-reference warnings. No T5 leftovers found (no Assets/Screenshots, RaidCapacityWidget*, InventoryFill or orphan .meta). Not opened in the Editor.

T7 done: `InteractionPromptText` (KeyLabel "F", Format -> "[F] action", blank -> "Interactuar"), used by InteractionHudPresenter and TownRaidPreparationView (small scope extension for the same hardcoded string); InteractionPrompt y 120 -> 190 (hand-edited YAML). No input bindings changed. EditMode 644 run, only the 2 baseline failures; PlayMode InteractionLootHudPlayModeTests all passed, RaidMainHudPlayModeTests same 6 baseline failures.
Caveats: KeyLabel is a constant guarded by a test against the real binding (not read at runtime, same approach as TownAbilitySlotKeyLabels); RaidMenuView.cs:16 still has a static "F — Interactuar" help line; no visual check in Play Mode; plain text, no key badge.

## Next step
All planned tasks are done (T5 dropped by user). T8 updated the three architecture docs (also corrected a pre-existing error: value recovery uses `LootInventoryValueCalculator.TryCalculate`, not `PlayerLootReceiver.TryCalculateTotalValue`). The notes above about stale labels and the architecture docs are resolved by T6c and T8.
Pending, outside this branch's scope: manual Game view / Play Mode validation (nothing visual was ever observed live), stale tests (separate spawned task), `NetworkPlayerPrefabHasOneCompleteRaidHudOnExistingCanvas` (RaidMenuView.ProgressionExperienceFill null), "F — Interactuar" static line in RaidMenuView, open design decisions listed in RaidMainHudArchitecture.md.
