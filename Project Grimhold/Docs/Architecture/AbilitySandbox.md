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
- **Abilities**: **Ignore session rules** checkbox (default ON). Shows what is **applied to the player** and what is **selected here**; picking a Slot button applies immediately (the selection starts from the applied loadout, so Apply never clears it by accident). **Cast Slot 1 / Cast Slot 2** buttons start the ability through the real activation path. Every catalog ability is listed with its attribute requirement next to the live value (for example `needs Strength 10 (current 12)`). Entries without an execution behaviour are shown as "(no behaviour - cannot cast)" and cannot be selected. Live readout per slot: bound ability, remaining cooldown, execution phase and the last activation failure with a plain-language explanation; plus Mana and Stamina. Seismic Strike only starts with at least one enemy or dummy within 3 units (`BehaviourRejected` otherwise, by design).
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
- The melee `NetworkEnemy` is inert (no movement, no attacks) but is a valid, damageable, knockable ability target; it is useful as a passive target.
- The sandbox starts with EMPTY slots when the dev profile has no prepared loadout; pick abilities in the Abilities tab. A real profile loadout is replaced by the first sandbox apply.
- Not validated under a Client peer, in a built player, or with real mouse clicks on the IMGUI panel (the panel logic was driven by code).

## Production hooks added (all dev-guarded)

- `IAbilityEnemyTarget` marker interface (not dev-guarded, no behaviour change): implemented by `EnemyCharacter` and the dummy; `AbilityTargetPredicate` now tests the marker instead of `candidate is EnemyCharacter` (T1).
- `PlayerAbilityRuntimeNetworkController.SandboxOverrideSlots`, `SandboxResetCooldowns`, `SandboxHasBehaviour` (T3, dev-guarded).
- `PlayerWeaponEquipmentNetworkController.SandboxSetEquipment` (T3, dev-guarded).
- The dummy reuses the existing `CharacterBase` damage extension points; `CharacterBase` itself is unchanged.

T4 adds no production-script changes: the launcher is a new Editor-only script and the scene is new.

See also: [Ability System Architecture](AbilitySystemArchitecture.md).

## Ignore session rules

`SandboxPlayerController.IgnoreSessionRules` is a networked flag that State Authority sets to `true` at spawn and the Abilities tab can toggle. Each tick the controller pushes it to `PlayerAbilityRuntimeNetworkController.SandboxSetIgnoreSessionRules(bool)`.

- **Skipped when ON**: the cast-time attribute-requirement check (`SandboxRuleBypass.EvaluateRequirements`). Slot selection already bypasses profile unlock, Town and the prepared loadout (`SandboxOverrideSlots`; `AbilityRuntimeSlots.TryCreate` only checks shape and catalog membership).
- **Still enforced**: alive/downed, match phase (it is `InProgress` in the sandbox), cooldown, resource cost (use Reset cooldowns / infinite mana; stamina has no refill hook), aim and behaviour rules (for example Seismic Strike needs an enemy in range).
- **Limitation**: only Charge and Seismic Strike have an execution behaviour. The other catalog abilities are listed but cannot be cast; they are not selectable.
- **Attribute tool**: the override controller accepts only +/-1 and +/-5 and one pending request per tick. The sandbox facade now splits any step (1..10, total up to 50) into supported steps and dispatches one per Fusion tick, so clicks are no longer dropped.

### Production hooks added (dev-guarded)

- `PlayerAbilityRuntimeNetworkController.SandboxIgnoreSessionRules { get; }` and `bool SandboxSetIgnoreSessionRules(bool enabled)` (State Authority only, default off), consulted in `TryStartExecution`.

### Cast requests (T6, dev-guarded)

`SandboxPlayerController.RequestCast(1|2)` queues a request that State Authority executes inside `FixedUpdateNetwork` by calling `PlayerAbilityRuntimeNetworkController.SandboxCast(UniversalAbilitySlot)`, which runs the real `TryStartExecution` (same gates, resource spend, cooldown and behaviour as an input rising edge). The result is stored as the slot's last activation failure and in `SandboxPlayerController.LastCastFailure` / `CastCount` (State Authority only).

Production hook added (dev-guarded): `AbilityActivationFailure SandboxCast(UniversalAbilitySlot slot)`; returns `PlayerUnavailable` outside State Authority forward simulation, `MissingBehaviour` for an empty slot.
