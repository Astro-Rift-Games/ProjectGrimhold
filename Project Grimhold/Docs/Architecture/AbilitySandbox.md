# Ability Sandbox

Development-only test environment for abilities. It lets a developer customize the local player live, spawn any number of melee, ranged and training-dummy entities, and read ability state in real time. Everything is compiled under `UNITY_EDITOR || DEVELOPMENT_BUILD`; the launcher is Editor-only. Release builds contain none of it except the small dev-guarded hooks listed below, which are also compiled out.

## How to launch

Menu: **Grimhold > Ability Sandbox > Play (Direct Host Raid)**.

The launcher (`Assets/Scripts/Editor/AbilitySandboxLauncher.cs`) reuses the existing direct Host raid flow instead of adding a game mode:

1. Adds `AbilitySandbox` to the Editor build settings for this Play session only (the raid launcher resolves its gameplay scene by name from the build settings). It is removed again when Play Mode ends. Use **Grimhold > Ability Sandbox > Remove From Build Settings** if an Editor crash left it behind.
2. Opens `MainMenu` and enters Play Mode.
3. On the runtime `SessionConnectionCoordinator`, sets `_gameplaySceneName` to `AbilitySandbox` and enables the offline development profile on `DevelopmentProfileBootstrap` (runtime instance only; the prefab is not modified).
4. Invokes `DirectRaidDevelopmentStarter.StartDirectHostRaid`, which loads `AbilitySandbox` as the raid scene. The normal admission pipeline then spawns the player and moves the match to `InProgress`, so the ability runtime works unmodified.

The Host connects through Photon, so a working network connection and Fusion app settings are required, exactly as for the regular direct raid.

## Scene

`Assets/Scenes/AbilitySandbox.unity` is a trimmed copy of `Gameplay.unity` (dungeon graybox, camera, light, input providers, `NetworkSpawnManager` scene configuration). Removed: traps, extraction sanctuaries, NPC, patrol routes and the mission debugger. All spawn-group amounts are `0`, so the initial raid bootstrap spawns no enemies, loot or breakables. Player spawn points are plain `SandboxSpawnpoints` transforms placed at the former sanctuary locations (the original player spawn points lived inside the sanctuary prefabs). `SandboxServices` hosts a `NetworkObject` with `SandboxEnemySpawner`, `SandboxPlayerController` and `SandboxPanel`.

## Controls

`F9` toggles the IMGUI panel. Tabs:

- **Player**: Health set/refill, Mana refill, god mode, infinite mana, teleport, reset cooldowns, attribute offsets (step, +/-, reset), equipment from `LootDefinitionCatalog` (Weapon Set A/B, Off Hand option) and per-slot unequip.
- **Abilities**: pick Slot 1 and Slot 2 from the catalog and apply. Entries without an execution behaviour are shown as "no behaviour" and cannot be selected. Live readout per slot: remaining cooldown, execution phase, last activation failure; plus Mana and Stamina.
- **Enemies**: kind (melee, ranged, dummy), count, pattern (ring, grid, line), spacing, spawn at player, clear all.
- **Dummy**: last damage, total damage and hit count per dummy and in total, reset.

## Component ownership

| Component | Owns |
| --- | --- |
| `AbilitySandboxLauncher` (Editor) | Temporary build-settings entry and the direct Host raid start. |
| `SandboxPanel` | IMGUI rendering and local reads only; issues requests. |
| `SandboxPanelState` | Pure UI state: tab, selections, clamping, number parsing (EditMode tested). |
| `SandboxPlayerController` | Request queue and State Authority application of slots, cooldown reset, equipment, health, god mode, infinite mana, teleport; attribute facade over `RuntimeAttributeOverrideNetworkController`. |
| `SandboxEnemySpawner` | State Authority spawning/despawning through `Runner.Spawn`, bounded counts and live limit. |
| `SandboxSpawnPlanner`, `SandboxLoadoutRules`, `DummyDamageLog` | Pure placement, validation and damage-accumulation logic. |
| `TrainingDummyCharacter`, `DummyKnockbackMotor` | Targetable invulnerable dummy that records damage and accepts knockback. |

## Authority

All mutations run on State Authority. Any peer may submit a request through an RPC (`RpcSources.All`, target State Authority) because the sandbox objects are scene objects without Input Authority; the State Authority re-validates every request. Presentation (the panel) only reads.

## Known limitations

- Stamina refill is not available (`PlayerStaminaNetworkController` exposes no refill hook).
- No per-spawn max-health override (no runtime max-health hook on non-player characters) and no AI freeze (the enemy FSM re-enables control on each state).
- God mode refills Health every tick; a single hit larger than Max Health can still Down the player.
- Equipment set through the sandbox bypasses inventory provenance (items carry no Raid origin).
- The `NetworkEnemy` melee prefab has no `IAttack` assigned; spawning it logs `EnemyCombatAIController requires a component implementing IAttack` and the enemy cannot attack. The ranged variant is the one the production spawn list uses.
- Dummy damage counters are recorded on the State Authority only.
- Not validated under a Client peer, in a built player, or with ability casts, damage and knockback in Play Mode.

## Production hooks added (all dev-guarded)

- `IAbilityEnemyTarget` marker interface (not dev-guarded, no behaviour change): implemented by `EnemyCharacter` and the dummy; `AbilityTargetPredicate` now tests the marker instead of `candidate is EnemyCharacter` (T1).
- `PlayerAbilityRuntimeNetworkController.SandboxOverrideSlots`, `SandboxResetCooldowns`, `SandboxHasBehaviour` (T3, dev-guarded).
- `PlayerWeaponEquipmentNetworkController.SandboxSetEquipment` (T3, dev-guarded).
- The dummy reuses the existing `CharacterBase` damage extension points; `CharacterBase` itself is unchanged.

T4 adds no production-script changes: the launcher is a new Editor-only script and the scene is new.

See also: [Ability System Architecture](AbilitySystemArchitecture.md).
