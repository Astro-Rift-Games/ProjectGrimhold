# Lobby and Session Transition Architecture

## 1. Context and Decision

The game requires a persistent social hub (the Town or "Pueblo") where players can gather outside of active expeditions (Dungeons). Unlike expeditions which require strict state authority, prediction, and Host/Client topology, the Town requires a more flexible environment where players can drop in and out without relying on a single Host peer.

**Decision:**
- The Town operates in **Shared Mode** (`GameMode.Shared`).
- The Dungeon continues to operate in **Host/Client Mode**, with no changes to its existing architecture (`NetworkSpawnManager`, `NetworkMatchController`, Host Migration, etc. remain exactly as they are).

## 2. Independent Network Contexts

Photon Fusion does not allow a single `NetworkRunner` to change its `GameMode` or session dynamically without shutting down. 
Therefore, the Town and the Dungeon represent two completely independent network contexts.

- A client will only ever have **one** `NetworkRunner` alive at a time.
- There is never a scenario where a Town runner and a Dungeon runner coexist simultaneously on the same client.
- Transitioning between the Town and a Dungeon requires shutting down the current runner entirely and bootstrapping a new runner for the target game mode.

## 3. The Town as a Persistent Hub

The Town is a single, persistent session running in `GameMode.Shared`. 
All online players who are not actively in an expedition reside in this shared session. State authority for each player's character is delegated to their respective client (Input/State Authority reside on the peer that spawns the object), which is standard for Shared Mode.

## 4. Town Component Contracts

The Town does not rely on the complex match lifecycle and spawn validation used by the Dungeon. It uses a minimal, dedicated composition:

### `SessionConnectionCoordinator`
- **Responsibility:** Guarantees that a Town runner and a Dungeon runner never coexist simultaneously on the same client. It acts as the connection manager, analogous to the one described in the official Photon "Fusion Social Hub" sample.
- **Contract:** It is the only component that knows about both `HubSessionLauncher` and `FusionSessionLauncher` and decides which is active. The individual launchers must not directly reference or command each other.

### `HubSessionLauncher`
- **Responsibility:** Starts and shuts down the Shared Mode runner for the Town.
- **Contract:** It does not know about `NetworkMatchController`, match phases, or Host Migration. It simply connects the client to the persistent Town session.

### `HubRunnerFactory`
- **Responsibility:** Creates the `NetworkRunner` for the Town.
- **Contract:** Adds only the minimum required components to the runner GameObject, specifically `LocalInputContext` and `LocalPlayerJoinContext`.
- **Explicit Exclusions:** It MUST NOT add `NetworkSpawnManager`, `EntityRegistry`, `ExtractionSanctuaryAssignmentService`, `HostMigrationLifecycleController`, `HostMigrationSnapshotRestorer`, or `LauncherShutdownListener`.

### `HubPlayerSpawner`
- **Responsibility:** Handles spawning the player's social character when they join the Town.
- **Contract:** Listens to `OnPlayerJoined`. If `player == runner.LocalPlayer`, it spawns the Town social prefab at a designated spawn point in the Town scene. Because it's Shared Mode, the peer that calls `runner.Spawn` automatically receives State Authority over the object.

## 5. Town Social Prefab vs PlayerCharacter

The Town requires a lightweight version of the player character.

- **Visuals:** Identical to the Dungeon `PlayerCharacter` (same Animator, sprite, and visible equipped weapon).
- **Included Network Components:** `PlayerInputReader`, `FusionInputProvider`, `PlayerNetworkInput`, `PlayerMovementNetworkController`, and `LocalPlayerCameraBinder`. (These are verified to be GameMode-agnostic).
- **Excluded Components:** `PlayerCombatNetworkController`, `PlayerExtractionController`, `PlayerCorpseGenerationController`, raid HUD, and minimap. The Town social prefab cannot participate in combat or extraction.

## 6. Coding Conventions (Based on Fusion Social Hub Sample)

To support dual topologies (Shared Mode and Host/Client Mode), future development must adhere to the following conventions derived from the official Photon "Fusion Social Hub" sample:
- **Authority Checks:** Prefer `Object.HasStateAuthority` over `Runner.IsServer` for any logic that might run in both modes. (Note: Components like `PlayerMovementNetworkController` and `LocalPlayerCameraBinder` already follow this pattern and require no changes).
- **Topology Branching:** Use `Runner.Topology` to branch logic exclusive to a specific topology rather than relying on ad-hoc boolean checks.

## 7. Infrastructure Reusability

### Reused As-Is (Agnostic Infrastructure)
The following existing infrastructure is safely reused in the Town without modification:
- `PlayerInputReader`
- `FusionInputProvider`
- `LocalInputContext`
- `PlayerNetworkInput`
- `PlayerMovementNetworkController`
- `LocalPlayerCameraBinder`
- `LocalProfileProvider`
- `PlayerJoinData`
- `PlayerJoinDataCodec`
- `LocalPlayerJoinContext`

### Exclusive to Dungeon (Not Used in Town)
The following infrastructure is deeply tied to the Host/Client lifecycle and MUST remain exclusive to Dungeon mode:
- `NetworkRunnerFactory`
- `NetworkSpawnManager`
- `NetworkMatchController`
- `EntityRegistry`
- `ExtractionSanctuaryAssignmentService`
- `HostMigrationLifecycleController`
- `HostMigrationSnapshotRestorer`
- `LauncherShutdownListener`

## 8. Development Access to Dungeon

Direct access to the Dungeon (e.g., Create/Join Room from a main menu bypassing the Town) is preserved purely as a development and testing route. The existing `FusionSessionLauncher` remains fully intact to support this. This direct access is not intended to be the final player flow, but rather a tool to accelerate testing and iteration.

## 9. Out of Scope

The following features are explicitly out of scope for this foundational architecture and should not be implemented or designed at this stage:
- Transitions from Pueblo -> Dungeon or Dungeon -> Pueblo.
- Party system.
- Expedition matchmaking.
- NPCs, stores, buying/selling, or stash.
- Inventory persistence across contexts.
- Host Migration for the Town (Shared Mode handles peer departure automatically).
- Town events.
- Combat inside the Town.
- Global GameFlowManager or high-level state machines.

## 10. Pending Decisions

- **Transition Trigger Mechanism:** The exact mechanism (e.g., UI menu vs. physical in-world interactable) that a player uses to request leaving the Town and starting the Dungeon matchmaking/loading sequence is currently undetermined.
