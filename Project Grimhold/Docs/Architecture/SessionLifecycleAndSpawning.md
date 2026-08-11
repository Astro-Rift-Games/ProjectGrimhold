# Session Lifecycle & Spawning Architecture

This document specifies the lifecycle phases of a multiplayer session, the authoritative flow for starting a match, late join prevention mechanisms, and defensive validations executed before player spawning.

---

## 1. Match Phases

The match coordinator (`NetworkMatchController`) maintains a single synchronized network state (`[Networked] MatchPhase Phase`) that defines the game's progress. 

```mermaid
stateDiagram-v2
    [*] --> WaitingForPlayers : Session Started
    WaitingForPlayers --> Starting : Host Starts Game
    Starting --> InProgress : Gameplay Scene Loaded
    InProgress --> Finished : Match Completed/Ended
```

The phases are defined as follows:
* **`WaitingForPlayers`**: The lobby phase. New clients can discover and connect to the session, and select classes.
* **`Starting`**: Transition phase initiated by the Host. The session is closed and hidden, and scene loading begins.
* **`InProgress`**: Gameplay is active. Spawned players are participating.
* **`Finished`**: The match has ended.

---

## 2. Prefab Coordinator and Lifecycle

The coordinator `NetworkMatchController` is spawned by the Host from a registered network prefab (`_matchControllerPrefab` in `FusionSessionLauncher`). 
- **Managed Persistence**: Instead of relying on local Unity calls, the coordinator is spawned with the flag `NetworkSpawnFlags.DontDestroyOnLoad` to let Photon Fusion manage its persistence across scene loads.
- **No DontDestroyOnLoad**: The coordinator does not call `UnityEngine.Object.DontDestroyOnLoad` directly.
- **Authority initialization**: `Phase` is initialized to `WaitingForPlayers` inside `Spawned()` on the State Authority (Host/Server).
- **Scene Load validation**: Prior to scene loading, the scene path is validated. The phase is set to `Starting`, and the session is closed (`IsOpen = false`, `IsVisible = false`). On load failure, the phase does not advance to `InProgress`.

---

## 3. Runner-Scoped Dependencies

- **Spawn Manager**: `NetworkSpawnManager` resides on the persistent runner GameObject. It is initialized explicitly via `InitializeForRunner` prior to `StartGame`.
- **Single Callback Registration**: Fusion automatically registers the spawn manager component as a callback listener of the runner GameObject, avoiding duplicate manual registrations.
- **Explicit Binding**: Once the coordinator is spawned on the Host, the launcher binds it to the spawn manager using `BindMatchController`. Clients bind the replicated coordinator in its `Spawned()` method.
- **Shutdown Cleanup**: When `OnShutdown` triggers, the manager clears all registries, unbinds the coordinator, and sets `_runner = null` to prevent reuse of dead runner instances.
- **Defeated Players**: A defeated player is still its original runner-owned `NetworkObject`; no persistent corpse object survives independently. Despawn removes the co-located receiver, loot-source, collider and interactable registrations. Runner shutdown destroys that object and its local HUD binding, while `LocalInputContext` clears its reader so a later runner cannot inherit UI targets, suppression tokens, requests, listeners, registry entries, or replicated loot collections from the previous session.

---

## 4. Scene Load Identity & Loading States

To avoid duplicate entity spawning and allow recarrying or reloading the same scene path cleanly, the spawner uses an incremental load generation ID engine instead of path-based strings:

* **Scene Load States**:
  - `None`: No scene is currently loaded or processing.
  - `Pending`: Scene loading started.
  - `Processing`: The scene finished loading and is currently resolving configuration and spawning initial entities.
  - `Failed`: An error occurred during lookup, validation, or spawning. Spawning remains locked.
  - `Completed`: Scene configuration and initial spawning finished successfully. Spawns are unblocked.

* **OnSceneLoadStart**: When a load starts, `OnSceneLoadStart` increments the generation counter (`_currentSceneLoadGeneration`), sets the state to `Pending`, clears spatial configuration lookups, and locks active spawning (`_spawnsBlocked = true`).
* **OnSceneLoadDone**: Sets state to `Processing` immediately before resolving configurations to prevent duplicate executions from repeated callbacks. Spawns the characters and initial entities. On success, sets state to `Completed` and unblocks spawns (`_spawnsBlocked = false`). On failure, the state becomes `Failed` and spawning remains locked. No auto-rebuild is attempted on failed generations; a reload is required.

Loot spawning has an additional runner-local generation record. `InitialLootSpawnState` remains storage-only: the manager records a point only after Fusion returned the expected object, the pre-spawn override was applied, `NetworkLootContainer` initialized, and the production container became available. Duplicate callbacks therefore cannot produce a second batch. A failed instance is despawned authoritatively and leaves the point unrecorded; a retry in the same generation derives the same seed and roll. `InitializeForRunner`, the next scene-load generation, and `OnShutdown` clear the point record so a new generation can spawn its own clean batch. This record is non-static and does not claim containers spawned by other systems.

---

## 5. Separation of Configurations & Scene Resolution

Configurations are split between persistent data and scene-specific spatial layouts:
- **Persistent Configuration**: Player and enemy prefab catalogs are owned by the launcher and passed to the runner-scoped `NetworkSpawnManager` during initialization. Gameplay's scene-configured manager contributes the explicit `LootContainer.prefab` reference through `CopyReferencesFrom`; the duplicate component is then destroyed while its colocated spatial configuration remains.
- **Scene Configuration**: Scene-specific spawn points and entity quantities are stored in `NetworkSpawnSceneConfiguration` components located in the respective scenes.
- **Runner-Scoped Scene Resolution**: The manager resolves configurations strictly within the roots and children of `runner.SceneManager.MainRunnerScene`. Falling back to the active scene is prohibited.
- **Scene Config Validation**: Spawn points inside `NetworkSpawnSceneConfiguration` must belong to the same scene structure. If multiple configurations are found in the same scene, the pipeline fails closed.
- **Strict Ordering**: Upon loading a scene, the configuration is applied first, then pending players are spawned, and finally scene entities are spawned.

Initial scene groups use an explicit dispatch policy:

```text
Players -> SpawnPlayer
Enemies -> SpawnEnemy
Loot -> SpawnLootContainer
Breakables -> SpawnBreakable
NPCs / Bosses / Misc -> warning and skip
```

An unsupported group never falls back to an enemy prefab. A missing loot-container reference reports a contextual error and skips only `Loot`; player and enemy processing continues.

Breakables use the same point-bounded, generation-idempotent initial spawning
policy. State Authority validates and rolls their weighted drop content before
spawning, using a group-discriminated seed so container and breakable points do
not share random streams. See `Docs/Architecture/BreakableLootArchitecture.md`.

Gameplay configures `SpawnGroupType.Loot` with ordered scene transforms. The Host/Server spawns `LootContainer.prefab` without Input Authority, using loop index `0..N-1` as stable point identity. One cryptographic session seed is created locally on the authoritative runner; a pure 64-bit mixer combines it with scene-load generation and point index. The manager validates the prefab table, random-content component, loot table, catalog, weights, quantities and capacities before spawning. It rolls an immutable snapshot and applies the result with Fusion 2.1.1 `OnBeforeSpawned`; clients never roll or receive a seed. When requested amount exceeds point count, it is clamped to the available points with a warning, so an initial generation never overlaps two containers on one point.

`OnBeforeSpawned` is synchronous but returns `void` and is not a cancellation mechanism. After `runner.Spawn`, the manager verifies the returned identity, callback result, container initialization and final availability. Any failure causes immediate State Authority despawn and prevents point registration. A missing prefab, disabled random configuration or invalid table skips only Loot; player and enemy spawning continues. The synchronized container dictionary is the only replicated result, so late joiners receive existing content without rerolling.

---

## 6. Declarative Spawning Policy (Spawn-Point-Based vs Explicit-Transform)

Spawning behaviour per scene is explicitly declared via the serialized `SceneSpawnPointPolicy` configuration component:

* **SceneSpawnPointPolicy**:
  - `NotRequired`: The scene does not contain or require spawn points. Scene-point-based spawning methods are disabled. Mark loading as `Completed` without generating objects.
  - `Required`: The scene requires a valid configuration with configured spawn points. If missing or invalid, the load generation fails closed.

* **SceneSpawnConfigurationStatus**:
  - `None`: Spawning configuration status has not been evaluated.
  - `SpawnPointsNotRequired`: Non-point spawning mode is active.
  - `SpawnPointsReady`: Spawn points lookup has been successfully built and validated.
  - `Invalid`: The configuration was missing, duplicated, or invalid. Spawning remains locked.

* **CanUseCurrentSceneSpawnPoints**: Used for scene-point-based spawns (dependent on spawn points lookup). Requires `Completed` load state and `SpawnPointsReady` status.
* **CanSpawnAtExplicitTransform**: Used for explicit-transform spawns (using explicit `Vector3` coordinates). Validates runner authority without consuming points configured.

---

## 7. Fail-Closed Spawn Policy

Player spawning is protected by a strict fail-closed validation policy. `CanSpawnPlayer` returns `false` if:
* The runner does not match the associated runner.
* Spawning is blocked (`_spawnsBlocked` is true).
* The coordinator is missing or belongs to another runner.
* The player is not registered in `_admittedPlayers`.
* The player already has a character spawned in `_spawnedPlayers` or registered via `runner.GetPlayerObject(player)`.
* The current phase is not `WaitingForPlayers`, `Starting`, or `InProgress`.
* The required class catalog or spawn configuration is missing.

---

## 8. PlayerRef Reusability

- **PlayerRef Reusability**: `PlayerRef` values are reusable by Photon Fusion across sessions or connection handshakes. The registry tracks authorized active connection states; late joins are rejected, preventing slots from being hijacked by unadmitted players.

---

## 9. Launcher and Secure Shutdown

- **Try-Finally Safety**: The launcher's `StartSessionAsync` wraps the startup sequence in a `try/finally` block to guarantee `_isStarting` is reset to `false` in all execution paths.
- **Controlled Shutdown**: Any failure after `StartGame` triggers `ShutdownAndDestroyRunnerAsync()`. It calls `runner.Shutdown()` asynchronously and destroys the runner GameObject, ensuring clean releases of network slots and resources.

---

## 10. Session Startup Context & Host Migration Resume

To distinguish between a completely new session and one resumed via Host Migration, an immutable `SessionStartupContext` is used:
- **Two Modes**: It defines either a `FreshSession` or a `HostMigrationResume`.
- **Creation and Injection**: The context is created by the initiator of the session (e.g., `FusionSessionLauncher`) and explicitly injected into runner-scoped dependencies like `NetworkSpawnManager` via `InitializeForRunner`. It is not stored as a generic component on the runner.
- **FreshSession Operations**: This mode permits the initial Host player bootstrap, initialization of the `MatchPhase` to `WaitingForPlayers`, and the fresh bootstrapping of initial scene entities (players, enemies, loot, breakables, new random seeds).
- **HostMigrationResume Awaiting**: When resuming via migration, all initial bootstrap operations are skipped. Scene load completes by transitioning the spawn manager into an explicit `AwaitingHostMigrationRestore` state. Spawns remain locked and no fresh scene seed is generated.
- **Migration Status**:
  - **HM-01**: Supports `FreshSession` vs `HostMigrationResume` distinction and suppression of the initial bootstrap pipeline (no new random seeds or initial points).
  - **HM-02**: Features a temporary migration scene to preserve memory, creates a fully configured replacement runner using `NetworkRunnerFactory`, and utilizes `HostMigrationToken` to rebuild the session connection state.
  - **HM-03**: The core snapshot restoration is fully functional. 
    - Recreates dynamic snapshot objects using `Runner.Spawn`.
    - Obtains the initial transform exclusively from the `NetworkTRSP` snapshot to correctly position entities.
    - Applies `CopyStateFrom` during `onBeforeSpawned` and temporarily strips Input Authority (`AssignInputAuthority(PlayerRef.None)`).
    - Fresh initialization during their `Spawned()` callbacks is suppressed via `HostMigrationRestoreUtility.IsRestoreSpawn()`. Since Fusion invokes `Spawned()` asynchronously (on the next tick), the `HostMigrationSnapshotRestorer` maintains a `HashSet` (`_allRestoredObjects`) containing all recreated object references. This ensures the utility reliably identifies restored instances (preventing previous states like `Health=0` from being overwritten) without interfering with newly spawned objects created normally later.
# Session Lifecycle & Spawning Architecture

This document specifies the lifecycle phases of a multiplayer session, the authoritative flow for starting a match, late join prevention mechanisms, and defensive validations executed before player spawning.

Raid-generation closure, persistence barriers and repeatable cleanup are specified in
`Docs/Architecture/RaidGenerationLifecycleArchitecture.md`.

---

## 1. Match Phases

The match coordinator (`NetworkMatchController`) maintains a single synchronized network state (`[Networked] MatchPhase Phase`) that defines the game's progress. 

```mermaid
stateDiagram-v2
    [*] --> WaitingForPlayers : Session Started
    WaitingForPlayers --> Starting : Host Starts Game
    Starting --> InProgress : Gameplay Scene Loaded
    InProgress --> Finished : Match Completed/Ended
```

The phases are defined as follows:
* **`WaitingForPlayers`**: The lobby phase. New clients can discover and connect to the session, and select classes.
* **`Starting`**: Transition phase initiated by the Host. The session is closed and hidden, and scene loading begins.
* **`InProgress`**: Gameplay is active. Spawned players are participating.
* **`Finished`**: The match has ended.

---

## 2. Prefab Coordinator and Lifecycle

The coordinator `NetworkMatchController` is spawned by the Host from a registered network prefab (`_matchControllerPrefab` in `FusionSessionLauncher`). 
- **Managed Persistence**: Instead of relying on local Unity calls, the coordinator is spawned with the flag `NetworkSpawnFlags.DontDestroyOnLoad` to let Photon Fusion manage its persistence across scene loads.
- **No DontDestroyOnLoad**: The coordinator does not call `UnityEngine.Object.DontDestroyOnLoad` directly.
- **Authority initialization**: `Phase` is initialized to `WaitingForPlayers` inside `Spawned()` on the State Authority (Host/Server).
- **Scene Load validation**: Prior to scene loading, the scene path is validated. The phase is set to `Starting`, and the session is closed (`IsOpen = false`, `IsVisible = false`). On load failure, the phase does not advance to `InProgress`.

---

## 3. Runner-Scoped Dependencies

- **Spawn Manager**: `NetworkSpawnManager` resides on the persistent runner GameObject. It is initialized explicitly via `InitializeForRunner` prior to `StartGame`.
- **Single Callback Registration**: Fusion automatically registers the spawn manager component as a callback listener of the runner GameObject, avoiding duplicate manual registrations.
- **Explicit Binding**: Once the coordinator is spawned on the Host, the launcher binds it to the spawn manager using `BindMatchController`. Clients bind the replicated coordinator in its `Spawned()` method.
- **Shutdown Cleanup**: When `OnShutdown` triggers, the manager clears all registries, unbinds the coordinator, and sets `_runner = null` to prevent reuse of dead runner instances.
- **Defeated Players**: A defeated player is still its original runner-owned `NetworkObject`; no persistent corpse object survives independently. Despawn removes the co-located receiver, loot-source, collider and interactable registrations. Runner shutdown destroys that object and its local HUD binding, while `LocalInputContext` clears its reader so a later runner cannot inherit UI targets, suppression tokens, requests, listeners, registry entries, or replicated loot collections from the previous session.

---

## 4. Scene Load Identity & Loading States

To avoid duplicate entity spawning and allow recarrying or reloading the same scene path cleanly, the spawner uses an incremental load generation ID engine instead of path-based strings:

* **Scene Load States**:
  - `None`: No scene is currently loaded or processing.
  - `Pending`: Scene loading started.
  - `Processing`: The scene finished loading and is currently resolving configuration and spawning initial entities.
  - `Failed`: An error occurred during lookup, validation, or spawning. Spawning remains locked.
  - `Completed`: Scene configuration and initial spawning finished successfully. Spawns are unblocked.

* **OnSceneLoadStart**: When a load starts, `OnSceneLoadStart` increments the generation counter (`_currentSceneLoadGeneration`), sets the state to `Pending`, clears spatial configuration lookups, and locks active spawning (`_spawnsBlocked = true`).
* **OnSceneLoadDone**: Sets state to `Processing` immediately before resolving configurations to prevent duplicate executions from repeated callbacks. Spawns the characters and initial entities. On success, sets state to `Completed` and unblocks spawns (`_spawnsBlocked = false`). On failure, the state becomes `Failed` and spawning remains locked. No auto-rebuild is attempted on failed generations; a reload is required.

Loot spawning has an additional runner-local generation record. `InitialLootSpawnState` remains storage-only: the manager records a point only after Fusion returned the expected object, the pre-spawn override was applied, `NetworkLootContainer` initialized, and the production container became available. Duplicate callbacks therefore cannot produce a second batch. A failed instance is despawned authoritatively and leaves the point unrecorded; a retry in the same generation derives the same seed and roll. `InitializeForRunner`, the next scene-load generation, and `OnShutdown` clear the point record so a new generation can spawn its own clean batch. This record is non-static and does not claim containers spawned by other systems.

---

## 5. Separation of Configurations & Scene Resolution

Configurations are split between persistent data and scene-specific spatial layouts:
- **Persistent Configuration**: Player and enemy prefab catalogs are owned by the launcher and passed to the runner-scoped `NetworkSpawnManager` during initialization. Gameplay's scene-configured manager contributes the explicit `LootContainer.prefab` reference through `CopyReferencesFrom`; the duplicate component is then destroyed while its colocated spatial configuration remains.
- **Scene Configuration**: Scene-specific spawn points and entity quantities are stored in `NetworkSpawnSceneConfiguration` components located in the respective scenes.
- **Runner-Scoped Scene Resolution**: The manager resolves configurations strictly within the roots and children of `runner.SceneManager.MainRunnerScene`. Falling back to the active scene is prohibited.
- **Scene Config Validation**: Spawn points inside `NetworkSpawnSceneConfiguration` must belong to the same scene structure. If multiple configurations are found in the same scene, the pipeline fails closed.
- **Strict Ordering**: Upon loading a scene, the configuration is applied first, then pending players are spawned, and finally scene entities are spawned.

Initial scene groups use an explicit dispatch policy:

```text
Players -> SpawnPlayer
Enemies -> SpawnEnemy
Loot -> SpawnLootContainer
Breakables -> SpawnBreakable
NPCs / Bosses / Misc -> warning and skip
```

An unsupported group never falls back to an enemy prefab. A missing loot-container reference reports a contextual error and skips only `Loot`; player and enemy processing continues.

Breakables use the same point-bounded, generation-idempotent initial spawning
policy. State Authority validates and rolls their weighted drop content before
spawning, using a group-discriminated seed so container and breakable points do
not share random streams. See `Docs/Architecture/BreakableLootArchitecture.md`.

Gameplay configures `SpawnGroupType.Loot` with ordered scene transforms. The Host/Server spawns `LootContainer.prefab` without Input Authority, using loop index `0..N-1` as stable point identity. One cryptographic session seed is created locally on the authoritative runner; a pure 64-bit mixer combines it with scene-load generation and point index. The manager validates the prefab table, random-content component, loot table, catalog, weights, quantities and capacities before spawning. It rolls an immutable snapshot and applies the result with Fusion 2.1.1 `OnBeforeSpawned`; clients never roll or receive a seed. When requested amount exceeds point count, it is clamped to the available points with a warning, so an initial generation never overlaps two containers on one point.

`OnBeforeSpawned` is synchronous but returns `void` and is not a cancellation mechanism. After `runner.Spawn`, the manager verifies the returned identity, callback result, container initialization and final availability. Any failure causes immediate State Authority despawn and prevents point registration. A missing prefab, disabled random configuration or invalid table skips only Loot; player and enemy spawning continues. The synchronized container dictionary is the only replicated result, so late joiners receive existing content without rerolling.

---

## 6. Declarative Spawning Policy (Spawn-Point-Based vs Explicit-Transform)

Spawning behaviour per scene is explicitly declared via the serialized `SceneSpawnPointPolicy` configuration component:

* **SceneSpawnPointPolicy**:
  - `NotRequired`: The scene does not contain or require spawn points. Scene-point-based spawning methods are disabled. Mark loading as `Completed` without generating objects.
  - `Required`: The scene requires a valid configuration with configured spawn points. If missing or invalid, the load generation fails closed.

* **SceneSpawnConfigurationStatus**:
  - `None`: Spawning configuration status has not been evaluated.
  - `SpawnPointsNotRequired`: Non-point spawning mode is active.
  - `SpawnPointsReady`: Spawn points lookup has been successfully built and validated.
  - `Invalid`: The configuration was missing, duplicated, or invalid. Spawning remains locked.

* **CanUseCurrentSceneSpawnPoints**: Used for scene-point-based spawns (dependent on spawn points lookup). Requires `Completed` load state and `SpawnPointsReady` status.
* **CanSpawnAtExplicitTransform**: Used for explicit-transform spawns (using explicit `Vector3` coordinates). Validates runner authority without consuming points configured.

---

## 7. Fail-Closed Spawn Policy

Player spawning is protected by a strict fail-closed validation policy. `CanSpawnPlayer` returns `false` if:
* The runner does not match the associated runner.
* Spawning is blocked (`_spawnsBlocked` is true).
* The coordinator is missing or belongs to another runner.
* The player is not registered in `_admittedPlayers`.
* The player already has a character spawned in `_spawnedPlayers` or registered via `runner.GetPlayerObject(player)`.
* The current phase is not `WaitingForPlayers`, `Starting`, or `InProgress`.
* The required class catalog or spawn configuration is missing.

---

## 8. PlayerRef Reusability

- **PlayerRef Reusability**: `PlayerRef` values are reusable by Photon Fusion across sessions or connection handshakes. The registry tracks authorized active connection states; late joins are rejected, preventing slots from being hijacked by unadmitted players.

---

## 9. Launcher and Secure Shutdown

- **Try-Finally Safety**: The launcher's `StartSessionAsync` wraps the startup sequence in a `try/finally` block to guarantee `_isStarting` is reset to `false` in all execution paths.
- **Controlled Shutdown**: Any failure after `StartGame` triggers `ShutdownAndDestroyRunnerAsync()`. It calls `runner.Shutdown()` asynchronously and destroys the runner GameObject, ensuring clean releases of network slots and resources.

---

## 10. Session Startup Context & Host Migration Resume

To distinguish between a completely new session and one resumed via Host Migration, an immutable `SessionStartupContext` is used:
- **Two Modes**: It defines either a `FreshSession` or a `HostMigrationResume`.
- **Creation and Injection**: The context is created by the initiator of the session (e.g., `FusionSessionLauncher`) and explicitly injected into runner-scoped dependencies like `NetworkSpawnManager` via `InitializeForRunner`. It is not stored as a generic component on the runner.
- **FreshSession Operations**: This mode permits the initial Host player bootstrap, initialization of the `MatchPhase` to `WaitingForPlayers`, and the fresh bootstrapping of initial scene entities (players, enemies, loot, breakables, new random seeds).
- **HostMigrationResume Awaiting**: When resuming via migration, all initial bootstrap operations are skipped. Scene load completes by transitioning the spawn manager into an explicit `AwaitingHostMigrationRestore` state. Spawns remain locked and no fresh scene seed is generated.
- **Migration Status**:
  - **HM-01**: Supports `FreshSession` vs `HostMigrationResume` distinction and suppression of the initial bootstrap pipeline (no new random seeds or initial points).
  - **HM-02**: Features a temporary migration scene to preserve memory, creates a fully configured replacement runner using `NetworkRunnerFactory`, and utilizes `HostMigrationToken` to rebuild the session connection state.
  - **HM-03**: The core snapshot restoration is fully functional. 
    - Recreates dynamic snapshot objects using `Runner.Spawn`.
    - Obtains the initial transform exclusively from the `NetworkTRSP` snapshot to correctly position entities.
    - Applies `CopyStateFrom` during `onBeforeSpawned` and temporarily strips Input Authority (`AssignInputAuthority(PlayerRef.None)`).
    - Fresh initialization during their `Spawned()` callbacks is suppressed via `HostMigrationRestoreUtility.IsRestoreSpawn()`. Since Fusion invokes `Spawned()` asynchronously (on the next tick), the `HostMigrationSnapshotRestorer` maintains a `HashSet` (`_allRestoredObjects`) containing all recreated object references. This ensures the utility reliably identifies restored instances (preventing previous states like `Health=0` from being overwritten) without interfering with newly spawned objects created normally later.
    - Safely hydrates scene objects utilizing `GetResumeSnapshotNetworkSceneObjects`.
    - Retains an internal immutable mapping from `old -> current` and applies explicit fixups for nested references (`EntityId`).
    - Validates that the state of `NetworkMatchController` is strictly preserved (`MatchPhase.InProgress`).
    - The spawning pipeline remains blocked (`_spawnsBlocked = true`).
    - Once completed, the session transitions to an explicit barrier state: `SnapshotRestoredAwaitingRuntimeRebind`.
  - **HM-04: Runner-local completion barrier**:
    - `StartGameResult.Ok` means only that the replacement runner started; Fusion may invoke `HostMigrationResume` after `StartGame` has returned.
    - Each `NetworkSpawnManager` initialized with `HostMigrationResume` owns a one-shot completion for that replacement runner. Fresh sessions do not create or await it.
    - Snapshot success alone is not terminal. The completion becomes successful only after the resumed scene pipeline is ready, restored PlayerObjects and avatar mappings are repopulated, pending reconnects are retained, runtime authorities have been restored, and spawns are unblocked.
    - A known scene, snapshot, validation, or rebind failure completes immediately as failure. A 30-second timeout bounds the interval from successful replacement `StartGame` to terminal completion and enters the existing cleanup/recovery policy.
    - `HostMigrationLifecycleController` adopts the replacement composition only after completion success and final validation that the runner is an active server with `MatchPhase.InProgress`.
  
  - **HM-05: Edge Cases & Graceful Aborts**:
    - **Lobby Disconnects**: If the original Host disconnects prematurely and the cloud delivers an outdated snapshot in the `WaitingForPlayers` phase, validation fails in a controlled manner: the restorer rolls back and shuts down the invalid replacement, while `SessionConnectionCoordinator` applies the existing Town recovery policy. The restorer never loads a scene directly.
    - **Snapshot Rollbacks on Quick Disconnects**: The Client-Hosted model suffers from _rollbacks_ if the Host disconnects before the _snapshot_ reaches the cloud. To mitigate accidental resurrections (e.g., if the Host dies and clicks "Abandon" immediately), a small *delay* was introduced in the server-exclusive abandonment logic before executing the shutdown. This guarantees the necessary network time for the corpse to be registered in the final migration snapshot.

  **Important Distinctions**:
  - **Character defeat != Host peer loss**: The death of a character belonging to the Host player does not execute `OnHostMigration`, does not replace the runner, and does not reload the scene. Host Migration exclusively occurs when the peer Host becomes unavailable and Fusion fires the `OnHostMigration` callback.
  - **Defeated Host participant return**: `RaidMenuPresenter` requests an individual return through `ReturnParticipantToTownAsync`. A Client or a solo Host uses the normal local Town return. A Host with another connected peer shuts down only its own raid runner without closing the match; the surviving peer may then receive `OnHostMigration` and remains logically in `SessionConnectionState.Raid`.
  - **Migration vs recovery**: The old surviving Client runner is intentionally shut down with `ShutdownReason.HostMigration`. `SessionConnectionCoordinator` suppresses unexpected-shutdown Town recovery for that reason, so migration and recovery cannot own the same replacement concurrently. A migration failure is reported separately and re-enters the existing recovery policy.
  - **Runner ownership handoff**: `HostMigrationLifecycleController` creates and starts the replacement composition, but `FusionSessionLauncher` remains the sole long-lived owner. `StartGame` returning successfully does not authorize adoption. Only after the runner-local completion reports successful snapshot restoration and runtime rebind, plus final `MatchPhase.InProgress` validation, does the launcher adopt the replacement runner, restored match controller, spawn manager, Sanctuary service, and a new shutdown listener. Until that point the launcher never exposes the replacement as active.
  - **Simulation vs Spawning**: The barrier `_spawnsBlocked` only halts the spawning pipeline (initial entity bootstrap and new spawns). It **DOES NOT** pause `FixedUpdateNetwork`. Consequently, enemies and restored simulation can continue advancing even though HM-04 has not yet restored participants and input authorities. The world should not be considered "paused".
