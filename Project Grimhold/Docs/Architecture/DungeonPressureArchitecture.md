# Dungeon Pressure Architecture (PvE)

## 1. Context and Objective

The Dungeon Pressure system orchestrates the dynamic PvE escalation during a Raid. It introduces a phased expedition timer (Normal, Reinforcements, CriticalPressure, Collapse) and dynamically spawns reinforcement enemies based on configured budgets and live population tracking.

This document defines the technical contracts and ownership boundaries for the system, ensuring it integrates securely with the existing Raid generation lifecycle, Host Migration, and authoritative gameplay simulation.

## 2. Ownership and Boundaries

The system is designed with strict separation of concerns, avoiding global event buses and single-singleton managers.

- **`DungeonPressureController` (NetworkBehaviour):** The single source of truth for the authoritative expedition timer (`RemainingTicks`), the current `Phase`, and the running state. It is a dynamic spawn created alongside `NetworkMatchController`. Only State Authority mutates its state. It is synchronized across peers and is resimulation-safe.
- **`EnemyReinforcementDirector` (NetworkBehaviour):** Evaluates spawn intervals and executes reinforcement spawns based on the active `DungeonPressureConfig` policy for the current phase. It runs exclusively under State Authority and only while the Raid is `InProgress`.
- **`DungeonPressureConfig` (ScriptableObject):** The static configuration containing total duration, phase thresholds, and reinforcement policies (budgets, intervals, caps) per phase. It is never mutated at runtime.
- **`PvePopulationTracker` (Runner-scoped C# Service):** Tracks the live, active PvE population. It maintains a deduplicated registry of alive enemies, differentiated by `EnemyPopulationOrigin` (Bootstrap vs. Reinforcement). It is completely decoupled from the enemy AI itself.
- **`ReinforcementPointRegistry` (Runner-scoped C# Service):** Caches and validates `SpawnGroupType.Reinforcements` transforms from the scene configuration upon scene load, avoiding global searches during active gameplay.
- **`NetworkSpawnManager`:** Remains the low-level executor for spawns. It exposes a focused `internal` method to spawn an enemy at a specific point, but it **does not** own the pressure logic, budgets, or intervals.

## 3. Lifecycle Integration

### 3.1. Initialization and Raid Start
- The pressure system initiates its logic only when `NetworkMatchController.Phase` transitions to `InProgress` (after successful `TryStartRaid` and bootstrap).
- Bootstrap enemies are spawned by `NetworkSpawnManager` under the `Bootstrap` origin. They do not consume the `PopulationBudget` (which is exclusive to reinforcements), but they do count toward the global `MaxActiveEnemies` cap.
- The `DungeonPressureController` is dynamically spawned by the Host (e.g., from `FusionSessionLauncher`) and uses `DontDestroyOnLoad`.

### 3.2. Raid Closure
- The system stops consuming time and generating reinforcements when the Raid transitions to `Closing` or `Finished`.
- `NetworkSpawnManager.TryCleanupRaidWorldForResults` calls `ResetForRaidClosure()` on the `PvePopulationTracker` and `ReinforcementPointRegistry`, ensuring the runner-scoped state is cleanly wiped for the next generation.
- The `DungeonPressureController` dynamic object is despawned automatically during the standard results cleanup.

## 4. Host Migration Recovery

The system must remain deterministic and robust across Host Migration:
- **Timer and Phase:** The state (`RemainingTicks`, `Phase`, `State`) of the `DungeonPressureController` is restored using the standard snapshot copy (`CopyStateFrom`). The system relies on absolute remaining ticks, which decouple it from the underlying engine tick base that changes across migrations.
- **Recovery Window:** The timer is **paused** and the `EnemyReinforcementDirector` is suspended while `NetworkSpawnManager.IsHostMigrationRecoveryInProgress` is true (the recovery window).
- **Population Tracker Reconstruction:** `PvePopulationTracker` is not networked directly. Instead, it is **reconstructed** when restored alive enemies execute `Spawned()` on the new Host. The `[Networked]` field `EnemyPopulationOrigin` on the enemy guarantees correct re-categorization (Bootstrap vs. Reinforcements).
- **No Double Spawns:** The `Spawned()` implementation of `DungeonPressureController` explicitly checks `ShouldInitializeMatchPhase` (the resume guard) to avoid overriding restored values with fresh initialization.

## 5. Technical Contracts by Stage

- **Stage 2 (Timer & Phases):** `DungeonPressureController` dictates the phase. Transits sequentially `Normal -> Reinforcements -> CriticalPressure -> Collapse`. Collapse freezes the timer at 0.
- **Stage 3 (Reinforcement Points):** `SpawnGroupType.Reinforcements` is added. The bootstrap flow explicitly ignores this group (`SpawnKind.ReinforcementPoints`).
- **Stage 4 (Population Tracker):** Enemies inform the tracker upon `Spawned()` and `HandleDeath()`. Defeated enemies stop consuming population, even if their network object remains alive as a loot container.
- **Stage 5 & 6 (Director & Config):** The Director retrieves the policy from `DungeonPressureConfig.GetPolicy(Phase)`. It spawns enemies targeting `MinDistanceToPlayer` rules via `ReinforcementSpawnPlanner`.
- **Stage 7 (Host Migration):** Validates the snapshot integration, tracker reconstruction, and recovery pause logic.
- **Stage 8 (Debug):** Provides a strict `#if UNITY_EDITOR` overlay for timeline manipulation and spawn verification.
