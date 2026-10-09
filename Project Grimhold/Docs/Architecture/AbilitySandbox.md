# Ability Sandbox

Development-only test environment for abilities. It lets a developer customize the local player live, spawn any number of melee, ranged and training-dummy entities, and read ability state in real time. Everything is compiled under `UNITY_EDITOR || DEVELOPMENT_BUILD`; the launcher is Editor-only. Release builds contain none of it except the small dev-guarded hooks listed below, which are also compiled out.

## How to launch

Menu: **Grimhold > Ability Sandbox > Play (Direct Host Raid)**.

The launcher (`Assets/Scripts/Editor/AbilitySandboxLauncher.cs`) reuses the existing direct Host raid flow instead of adding a game mode:

1. Adds `AbilitySandbox` to the Editor build settings for this Play session only (the raid launcher resolves its gameplay scene by name from the build settings). It is removed again when Play Mode ends. Use **Grimhold > Ability Sandbox > Remove From Build Settings** if an Editor crash left it behind.
2. Opens `MainMenu` and enters Play Mode.
3. On the runtime `SessionConnectionCoordinator`, sets `_gameplaySceneName` to `AbilitySandbox`. The file-backed `DevelopmentProfileBootstrap` stays disabled; `SandboxTestPlayerInstaller.TryInstall` installs the synthetic test player instead (see "Test player"). The prefab is not modified.
4. Invokes `DirectRaidDevelopmentStarter.StartDirectHostRaid`, which loads `AbilitySandbox` as the raid scene. The normal admission pipeline then spawns the player and moves the match to `InProgress`, so the ability runtime works unmodified.

The Host connects through Photon, so a working network connection and Fusion app settings are required, exactly as for the regular direct raid.

## Test player

The sandbox never uses a real, logged-in or development (`dev-local-*`) profile. `SandboxTestProfile` (pure, EditMode tested) builds a fixed synthetic profile and `SandboxTestPlayerInstaller` installs it before the raid starts:

- Identity `sandbox-test-player`; Vitality, Resistance, Strength, Dexterity, Intelligence and Luck all `30`, no pending points.
- Every ability in the `AbilityDefinitionCatalog` unlocked; no abilities prepared (pick them in the Abilities tab).
- Empty stash and loadout, currency 0, no reservation or receipts. The configured recovery weapon (`LocalProfilePersistenceConfiguration`) is prepared in Weapon Set A Main Hand so the raid admission is valid.
- Held only by an `InMemoryLocalProfileRepository`. The installer bypasses `ApplicationStashServiceBootstrapper`, attaches only the in-memory stash, loadout and currency services, and adds no `RemoteInventoryService`, `ProfileReconciliationService` or shop service. Nothing is read from or written to `persistentDataPath`, no backend call is possible, and the game code uses no `PlayerPrefs`.
- It refuses to replace a profile that is already active.

Previous behaviour (T4-T6): the sandbox enabled `DevelopmentProfileBootstrap`, which loaded and rewrote `grimhold-profile-dev-local-host.json`, retried that profile's pending extraction commit and attached the remote inventory service.

## Scene

`Assets/Scenes/AbilitySandbox.unity` is a dedicated testing arena, not a Gameplay copy: a 40x30 flat floor (one tiled `SpriteRenderer` using an existing tile sprite), a one-tile boundary ring on the `WorldCollision` layer (`Walls` tilemap with composite collider, existing tile asset), one `PlayerSpawn` at the origin, the camera prefab, a 2D global light, `FusionInputProvider`/`PlayerInputReader`, `NetworkSpawnManager` with its scene configuration (one player area, empty spawn groups with amount `0`), `VisibilityManager`, `PathfindingGrid` (needs a Tilemap, built from the walls) and `SandboxServices` (`NetworkObject` with `SandboxEnemySpawner`, `SandboxPlayerController`, `SandboxPanel`). No dungeon rooms, traps, extraction, NPCs, loot, breakables, music or mission objects.

## Controls

`F9` toggles the IMGUI panel. Tabs:

- **Player**: Health set/refill, Mana refill, god mode, infinite mana, teleport, reset cooldowns, attribute offsets (step, +/-, reset), equipment from `LootDefinitionCatalog` (Weapon Set A/B, Off Hand option) and per-slot unequip.
- **Abilities**: **Ignore session rules** checkbox (default ON). Shows what is **applied to the player** and what is **selected here**; picking a Slot button applies immediately (the selection starts from the applied loadout, so Apply never clears it by accident). **Cast Slot 1 / Cast Slot 2** buttons start the ability through the real activation path. Every catalog ability is listed with its attribute requirement next to the live value (for example `needs Strength 10 (current 12)`). Entries without an execution behaviour are shown as "(no behaviour - cannot cast)" and cannot be selected. Live readout per slot: bound ability, remaining cooldown, execution phase and the last activation failure with a plain-language explanation; plus Mana and Stamina. Seismic Strike only starts with at least one enemy or dummy within 3 units (`BehaviourRejected` otherwise, by design).
- **Enemies**: kind (melee, ranged, dummy), count, pattern (ring, grid, line), spacing, spawn at player, clear all.
- **Dummy**: last damage, total damage and hit count per dummy and in total, reset.

## Component ownership

| Component | Owns |
| --- | --- |
| `AbilitySandboxLauncher` (Editor) | Temporary build-settings entry, test player install and the direct Host raid start. |
| `SandboxTestProfile`, `SandboxTestPlayerInstaller` | Synthetic in-memory test profile (pure builder) and its installation into `ApplicationStashContext`. |
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
- The sandbox always starts with EMPTY ability slots (the test player prepares none); pick abilities in the Abilities tab.
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
