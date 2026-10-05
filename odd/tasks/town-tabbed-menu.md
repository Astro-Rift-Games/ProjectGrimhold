# Town tabbed player menu

Branch: `feat/town-tabbed-menu` (from `New-Testing`). Plan: `C:\Users\Dani\.claude\plans\quiero-integrar-dentro-de-transient-clock.md`.
Engram mirror topic: `odd/town-tabbed-menu/tasks`.

## Objective
One Town window with a tab bar hosting Inventory and Attributes now, with a registry that lets Abilities (and later Stash / Raid Prep) plug in without changing the shell.

## Problem
Inventory (Tab) and Attributes (C) are separate overlays, each with its own Canvas, input-suppression token and Escape handling. They can be open simultaneously. No tab host exists.

## Scope
- In: Town only. Shell + tab state, Inventory hosting mode, Attributes hosting, docs, test updates.
- Out: Raid UI (unchanged), Abilities tab, Stash / Raid Prep tabs, Ready gating for attributes.

## Constraints
- Raid behavior of `RaidInventoryPresenter` must not change.
- Keep child names looked up by tests (`EquipmentPanel`).
- Planning heuristic ~400 authored changed lines per task (advisory only).

## Resolved test configuration
- TDD: enabled (Strict TDD Mode, source: user CLAUDE.md).
- Runner: Unity Test Runner (EditMode/PlayMode). UnityMCP bridge not connected at session start; pure-logic tests are additionally run through a scratchpad `dotnet test` harness.

## Tasks
- [x] T1 `TownMenuTabState` pure logic + EditMode tests
- [x] T2 Shell view/presenter + `TownPlayerMenu.prefab`, wired from `TownInventoryBinder`
- [x] T3 Inventory hosting mode in `RaidInventoryPresenter.BindTown`
- [x] T4 Attributes hosting (`TownAttributeAssignmentPresenter` / view)
- [x] T6 Hide Town HUD while the menu is open; party HUD only for an active party (companion present)
- [x] T7 Escape owned by PlayerInputReader; pause menu follows MenuToggleRequested
- [x] T8 Options tab replaces the Town pause menu; Escape opens it when no other panel is open
- [x] T9 Attributes tab redesign (3 columns: attributes / equipment / statistics) following the user's reference image
  - [x] T9a Pure `TownCharacterStatisticsBuilder` + presentation model + EditMode tests (delegated writer)
  - [x] T9b Binding/presenter read prepared equipment + loot catalog; refresh on ProfileCommitted
  - [x] T9c View + prefab redesign (left attributes with +, center read-only equipment slots, right statistics)
  - [x] T9d Update `TownAttributeAssignmentConfigurationTests`, docs
- [x] T5 Docs + test updates (new TownPlayerMenuArchitecture.md; RaidInventoryUIArchitecture.md and ProgressionArchitecture.md updated; no existing test needed changes)

## Route declaration
- T1: direct inline (one new file + one test file, understood).
- T2-T4: delegated writer or inline per trigger evidence; recorded below.

## Progress / evidence
- T1 RED observed: scratchpad `dotnet test` harness failed to compile (`TownMenuTabState` missing). GREEN observed: 11 passed, 0 failed. Unity Test Runner EditMode (UnityMCP bridge connected later): 11/11 passed, 0 failed.
- T1 review: `gentle-ai review assess --base-ref 8ab041de --committed-only` -> medium, `review_due: false` (`under_budget`). A separate workspace candidate (`.claude/settings.json`, user's pre-existing change, not part of this feature) got the consent prompt; the user chose "Skip this time" (`declined_this_candidate`).

- T3 RED observed in Unity (CS1739 `externallyHosted`, CS1061 `ShowHosted`/`HideHosted`). GREEN: PlayMode TownInventoryPresenterPlayModeTests + RaidInventoryPresenterInteractionTests 44/44 passed (route: inline; 2 non-trivial files, planned writer delegation skipped because the design was already fully resolved in the parent).
- Decision: framed window (user choice), shell instantiated by a presenter on SocialPlayer, content reparented at runtime (`RaidInventoryView.ScreenRootRect`, attributes view root).
- T2 RED: CS0246 (`TownPlayerMenuPresenter`, `TownMenuTabRegistration`). GREEN: `TownPlayerMenuPresenterPlayModeTests` 9/9. Prefab `TownPlayerMenu.prefab` built by an editor script (window, tab bar, template, content root).
- T4 RED: `TownPlayerMenuConfigurationTests.SocialPlayer_Wires...` failed (0 presenters on SocialPlayer). GREEN after wiring `SocialPlayer.prefab` (`TownPlayerMenuPresenter` on the root, `TownInventoryBinder._menuPresenter`, `TownAttributeAssignmentPresenter._menu`).
- Full EditMode (Presentation+Progression) 359 run: 5 failures, none reference menu/binder/attribute symbols; PlayMode Presentation 91 run: 12 failures (Raid HUD/menu/loot, Fusion 'Invalid prefab id', missing NetworkPlayerMelee.prefab). Baseline comparison done: checked out 8ab041de, same 5 EditMode and same 12 PlayMode failures (pre-existing, unrelated); branch has +14 EditMode and +11 PlayMode tests, all passing.
- T6 (user request): RED = CS1061 (`RegisterHud`) + new view/visibility tests. GREEN: EditMode 374 run (5 pre-existing failures only), PlayMode Presentation 95 run (12 pre-existing failures only; +4 new menu/HUD tests pass). Assumption: 'active party' = party with a companion (solo parties are auto-created for every player). `TownPartyLocalAuthorityPlayModeTests` assertion changed to expect a hidden HUD for solo; that Fusion test also fails identically on base 8ab041de (party not created in the test environment, line 62), so the changed assertion is not exercised yet.
- Not committed on purpose: `TownPlayerMenu.prefab` layout tweak made in the Editor by the user.
- T7 (Escape double handling): RED = 3 of 4 new `TownPauseMenuPresenterPlayModeTests` failed (pause ignored the reader). Fix: `TownPauseMenuPresenter` listens to `PlayerInputReader.MenuToggleRequested` via `LocalInputContext`, legacy polling only without a reader. GREEN: PlayMode Presentation 99 run, only the 12 pre-existing failures. Remaining: `MissionBoardUI` legacy Escape.
- T8 (user request): RED = CS0117 (`TownMenuTabIds.Options`) then missing TownOptions types. GREEN: PlayMode Presentation 106 run (only the 12 pre-existing failures), EditMode 377 run (only the 5 pre-existing). Found by the new tests: restoring hosted content to its original parent on teardown logs 'Cannot set the parent ... being destroyed' (would also happen on SocialPlayer despawn); removed restoration, content is never moved back. Removed TownPauseMenuPresenter/View, their test and the TownPauseMenu object in Lobby-Town (scene re-serialization adds default Unity fields). Exit game = Application.Quit (ExitPlaymode in Editor) is new behavior inferred from the request. Known gap: no Escape-to-logout before the player binds.
- T9 decisions: Game Design (docs 08 and 09) defines only Max Health/Stamina/Mana (+gear modifiers), Physical/Magical Defense (+mitigation D/(D+100)), per-weapon damage (base x scaling) and additional loot chance (Luck). It does NOT define Melee/Ranged/Magic Power or Move Speed, nor ring/amulet slots, so those mock-up rows are omitted. User chose 'damage per weapon' for the offense section. Center equipment column is read-only (equipping stays in the Inventory tab) and no character preview widget exists, so none is built now. Code/GD conflict reported, not fixed: GD fixes max attribute value at 30, `ProgressionBalanceDefaults` uses 25.
- T9a (delegated writer, route: delegated direct; trigger = reading 4+ files to write): RED observed by the writer (CS0246/CS0103). GREEN verified by the parent: `TownCharacterStatisticsBuilderTests` 26/26 passed, re-run after splitting the four types into one file each (AGENTS.md rule).
- T9b/T9c: RED observed at each step (CS1729 binding ctor, CS0246 TownStatLine/TownStatLineView, 3 failing wording tests, 1 failing scale test). GREEN: EditMode 422 run (only the 5 pre-existing failures), PlayMode Presentation 107 run (only the 12 pre-existing). New: binding tests 4, lines tests 8, view tests 6, builder tests 26. The SocialPlayer catalog wiring test was written before the code but only run after both, so its RED was not observed.
- Visual verification (Play Mode screenshots with real catalog data) found a T4 bug: the hosted Attributes panel was invisible because its Canvas-root prefab is saved with localScale (0,0,0). `TownPlayerMenuPresenter.HostContent` now resets the scale (PlayMode test added, RED observed). Edit-mode rendering of this panel was unreliable, so Play Mode was used. Numbers match the Game Design: VIT 21 -> 75+105+20 gear = 200 Health; Physical Defense 55 -> 35.5%.
- Not verified: the real Town scene with a Fusion session (hotkey -> panel with the real profile); the PlayMode party test `TownPartyLocalAuthorityPlayModeTests` still fails on base for an unrelated reason.

## Next step
Manual verification in the Town scene (Tab / C / tabs / Escape / equip / assign points), visual layout of hosted content inside the frame, Escape vs TownPauseMenuPresenter double handling. Then Abilities tab as a separate task.
