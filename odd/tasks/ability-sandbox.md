# Feature: ability-sandbox

## Objective
Dev-only sandbox scene to test abilities: fully customizable player, any number/type of enemies, and an invulnerable training dummy.

## Constraints
- Dev-only code under `UNITY_EDITOR || DEVELOPMENT_BUILD`. No new GameMode (reuse direct Host raid flow). No new packages.
- Approved plan: `C:\Users\Dani\.claude\plans\necesito-crear-un-entorno-rustling-lemur.md`.
- Branch: `feat/ability-sandbox` (from `feat/ability-charge-seismic-strike`).
- TDD: enabled (Strict), source: user CLAUDE.md. Runner: Unity Test Runner EditMode via Unity MCP `run_tests`.
- Delivery strategy: `ask-on-risk`. Per-task heuristic ~400 authored changed lines (advisory).
- Commits: conventional, no AI attribution, docs/tracking folded into work-unit commits.

## Tasks
- [x] T1 Targetability marker + `TrainingDummyCharacter` (+ EditMode tests) — route: delegated writer
- [x] T2 Dummy prefab + `SandboxEnemySpawner` — route: delegated writer
- [x] T3 Player customization RPCs + ability slot/cooldown/resource hooks (+tests) — route: delegated writer
- [x] T4 Sandbox UI + scene + launcher + `Docs/Architecture/AbilitySandbox.md` — route: delegated writer

## Acceptance
- Dummy is hit by abilities, records damage, never loses health, receives knockback.
- Any number of melee/ranged/dummy enemies spawnable and clearable at runtime.
- Attributes, equipment, ability slots, cooldowns, resources, health editable live.
- EditMode tests green; Unity compiles with no errors.

## Evidence / commits
- T1: RED = compile error CS0246 `IAbilityEnemyTarget` not found (tests written first). GREEN = `Tests.EditMode.Abilities` 44/44 passed, 0 failed. Full EditMode: 3090 run; failures are pre-existing prefab/scene/catalog tests (Merchant, NetworkPlayerMelee/Ranged prefabs, SessionComposition, ArmorSetCatalog, etc.), none in Abilities or touching T1 files. Not covered: dummy `ApplyDamage` end-to-end (needs Fusion State Authority; covered later by PlayMode/manual in T2/T4). Marker `IAbilityEnemyTarget` implemented by EnemyCharacter and TrainingDummyCharacter (dev-only, hooks `TryApplyAlternateDamage` so Health is never touched).
- T1 commit: `a4a0a3b4` `feat(sandbox): add training dummy and enemy-target marker`.
- T1 caveat: full-suite failures (Merchant, NetworkPlayer prefabs, SessionComposition, ...) were NOT verified against the base branch; "pre-existing" is unconfirmed.
- T2: RED = CS0246 `SandboxSpawnPattern` not found (planner tests written first). GREEN = `Tests.EditMode.Sandbox` 19/19, then Sandbox + `Tests.EditMode.Abilities` 63/63 passed, 0 failed; no CS errors in console. Prefab `Assets/Prefabs/Sandbox/TrainingDummy.prefab` built via Unity MCP; YAML confirms layer 7 (Character), `_damageHitboxes` -> trigger hitbox, NetworkedBehaviours baked (NetworkTransform, DummyKnockbackMotor, TrainingDummyCharacter). Added dev-only `DummyKnockbackMotor` (no existing IKnockbackMotor outside player/enemy AI). Max-health override is rejected (no CharacterBase hook); AI freeze left out (no clean hook, FSM re-enables control each state). Spawner RPCs use `RpcSources.All` (scene object has no Input Authority).
- T2 NOT validated: Fusion runtime spawn, EnemyCharacter/KillExperience/MissionProgress sources without a match controller, knockback visuals, RPC from a Client, Play Mode.

- T2 commit: `53d4d6ae` `feat(sandbox): add training dummy prefab and enemy spawner`.
- T3: partial work from a previous writer was audited and kept. RED = batchmode compile error CS0234 (`Fusion.Addons` unused using in `SandboxPlayerController`), fixed. GREEN = Unity compiled with 0 CS errors; `Tests.EditMode.Abilities` + `Tests.EditMode.Sandbox` 101/101 passed, 0 failed (includes new `SandboxLoadoutRulesTests`). Dev-guarded hooks: `PlayerAbilityRuntimeNetworkController.SandboxOverrideSlots/SandboxResetCooldowns/SandboxHasBehaviour`, `PlayerWeaponEquipmentNetworkController.SandboxSetEquipment`. Attributes via thin facade over `RuntimeAttributeOverrideNetworkController`. God mode = Health refill each tick; infinite Mana = refill each tick.
- T3 left out: infinite/refill Stamina (`PlayerStaminaNetworkController` has no refill hook; smallest change: a dev-guarded `SandboxRefill()` there).
- T3 NOT validated: Fusion runtime (RPC from Client, State Authority paths), Play Mode, equipment/slot hooks end-to-end, god-mode burst > Max Health, teleport.
- T3 commit: `d4db2649` `feat(sandbox): add live player customization controller and ability hooks`.
- T4: RED = CS0246 `SandboxPanelState` not found (tests written first). GREEN = `Tests.EditMode.Sandbox` 87/87, then Sandbox + `Tests.EditMode.Abilities` 131/131 passed, 0 failed; 0 CS errors. Raid-flow finding: the direct Host raid loads its gameplay scene by name from build settings and needs MainMenu's Systems (coordinator, stash, dev profile), so a bare scene cannot start it. Chosen wiring (no production script changed): Editor-only `AbilitySandboxLauncher` (menu Grimhold > Ability Sandbox) temporarily adds the scene to build settings, opens MainMenu, sets the runtime coordinator `_gameplaySceneName`, enables the dev profile on the runtime instance and invokes `StartDirectHostRaid`; the build-settings entry is removed on exit (observed restored). `AbilitySandbox.unity` is a trimmed Gameplay copy (no traps/extraction/NPC/patrols, spawn-group amounts 0, plain player spawn points because the originals lived inside the sanctuary prefabs).
- T4 Play Mode OBSERVED (Editor, Host): scene loaded as raid scene, state Raid, match InProgress, 1 player spawned (100/100 HP), no errors on start. Via code calls (not the IMGUI): slots set to Charge/Seismic Strike (both bound), 1 dummy + 2 melee spawned (SpawnedCount 3), direct dummy ApplyDamage(25) kept HP 1000 and logged total 25 / 1 hit, god mode refilled HP 40 -> 100. Console: `EnemyCombatAIController requires a component implementing IAttack` x2 from the `NetworkEnemy` melee prefab (no IAttack assigned; production spawn list uses only the ranged variant).
- T4 NOT validated: F9 panel rendering/clicks, ability casts, ability damage, knockback, equipment, teleport, attributes, Client peer, built player, stamina.
- T4 commit: see git log (`feat(sandbox): add ability sandbox scene, panel and docs`).

## Known follow-ups
- Add a dev-guarded `SandboxRefill()` to `PlayerStaminaNetworkController` (stamina refill/infinite stamina).
- Fix or retire the `NetworkEnemy` melee prefab (missing `IAttack`) so the melee kind is usable.
- Per-spawn max-health override and AI freeze need small runtime hooks on `CharacterBase` / enemy FSM.
- Validate under a Client peer and manually cast Charge / Seismic Strike on the dummy (damage + knockback).
- `Gameplay.unity` player spawn points live inside the ExtractionSanctuary prefab; consider standalone spawn points.

## Next step
Manual Play Mode pass of the panel and ability casts; then delivery under `ask-on-risk`.
