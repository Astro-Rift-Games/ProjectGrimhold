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
- [ ] T4 Sandbox UI + scene + bootstrap + `Docs/Architecture/AbilitySandbox.md` — route: delegated writer

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

## Next step
T4.
