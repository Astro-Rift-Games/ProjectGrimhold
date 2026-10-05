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
- [ ] T5 Docs + test updates

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
- Full EditMode (Presentation+Progression) 359 run: 5 failures, none reference menu/binder/attribute symbols; PlayMode Presentation 91 run: 12 failures (Raid HUD/menu/loot, Fusion 'Invalid prefab id', missing NetworkPlayerMelee.prefab). Baseline comparison against 8ab041de pending.

## Next step
T2 shell view/presenter. Read `TownInventoryBinder.cs`, `PlayerInputReader.cs` suppression/Escape API and `SocialPlayer.prefab` canvas first.
