# Player Downed state (Abatido)

Objective: Replace the direct Active -> definitive Defeat transition with a recoverable Downed state that owns an independent Downed Health reserve (75 baseline), authoritative drain, damage multiplier, single definitive Defeat at 0, action/CC blocking, limited movement, hidden weapons, disconnect continuity and Host Migration restore safety. Revive, Self-revive and Accelerated Resolution are out of scope.

Baseline: New-Testing 07cadabeb143738f810d41f4308a6165d609b4ba, clean tree. Approved plan: C:\Users\Dani\.claude\plans\pasted-content-id-be0f-objetivo-impleme-drifting-diffie.md (user-approved 2026-10-03, includes T5b weapon hiding requested by user).

Sources: Game Design doc 13 "Sistema de Abatido y Reanimación" §3, §4, §5, §11, §13 (grimhold-docs, read 2026-10-03); doc 07 §19-20 (no corpse while Downed). Drain default 2.5/s (75 in 30 s) chosen by user. Movement multiplier 0.35 provisional (GD gives no number). Weapon visual hiding is a user decision (GD defers presentation).

Constraints: Health stays 0 while Downed; IsAlive becomes virtual and PlayerCharacter returns Health > 0 || IsDowned. Entry hit non-fatal, excess discarded; multiplier applied after existing mitigation. Only State Authority writes Downed state; restore spawns skip fresh init. No new frameworks, no CC system invented (predicate only).

TDD: Strict TDD enabled (user global CLAUDE.md). Runner: Unity Test Runner via Unity MCP (EditMode + PlayMode). RDD: on (global). Delivery strategy: ask-on-risk; forecast ~1200 authored lines across tasks; work-unit commits stay local on New-Testing; push/PR not authorized, so chain strategy will be asked before any PR.

- [x] T1 Pure DownedHealthRules + EditMode tests. Route: delegated direct (writer; trigger 2+ non-trivial files). Commit 562b077f. RED: CS0103 missing type. GREEN: EditMode job c6c4b2aa 21/21.
- [x] T2 PlayerDownedStateNetworkController (networked IsDowned/DownedHealth/DownedCycle, authoritative drain, restore-safe Spawned). Route: delegated direct. Commit 5765d017 (shared with T3, compile dependency) + prefab wiring in NetworkPlayer.prefab.
- [x] T3 CharacterBase/PlayerCharacter damage + healing hooks. Route: delegated direct. Commits 5765d017, 2af5a355 (PlayMode). RED: PlayMode job 046ec0a1 0/5 (component missing). GREEN: PlayerDownedPlayModeTests job ef81f59f 5/5. TDD deviation: controller/hooks written before the PlayMode test file. Collapse finding: Dungeon collapse never defeats players today (no ApplyDamage on players), so no forced-defeat entry added; GD doc 10 absolute close defeat is pre-existing missing behavior -> follow-up. Pre-existing PlayMode failures identical with changes stashed (jobs 3c30f1d2 vs 5672f5fb; bccd0fe7 vs 8964fef2). EditMode full run 42092bd2 has unrelated failures, not baselined. Legacy corpse tests use driver AllowDowned=false via internal TestDisableEntry seam.
- [ ] T4 Action gates (attack, shield, equipment/weapon sets, consumables, transfer, drop, interaction) + CanReceiveStatusEffects. Route: delegated direct.
- [ ] T5 Limited movement (speed multiplier, no sprint, knockback intact). Route: delegated direct.
- [ ] T5b Weapons: cancel in-flight attack/defense on entry; hide weapon sprites while Downed, restore on exit. Route: delegated direct.
- [ ] T6 OnPlayerLeft keeps Downed Raiding avatar/participant alive; drain continues to Defeat. Route: delegated direct.
- [ ] T7 Architecture docs + player prefab wiring + PlayMode coverage. Route: delegated direct.

Progress: T1-T3 done (719 authored lines incl. tests/prefab). RDD assess on 07cadabe..HEAD: medium, slice_budget_reached -> review due.

Next step: native review of T1-T3 slice, then T4+T5+T5b writer.
