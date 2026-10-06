# Dungeon Pressure Architecture (PvE)

## 1. Context and Objective

The Dungeon Pressure system orchestrates the dynamic PvE escalation during a Raid. It introduces a phased expedition timer (Normal, Reinforcements, CriticalPressure, Collapse) and dynamically spawns reinforcement enemies based on configured budgets and live population tracking.

This document defines the technical contracts and ownership boundaries for the system, ensuring it integrates securely with the existing Raid generation lifecycle, Host Migration, and authoritative gameplay simulation.

## 2. Ownership and Boundaries

The system is designed with strict separation of concerns, avoiding global event buses and single-singleton managers.

- **`DungeonPressureController` (NetworkBehaviour):** The single source of truth for the authoritative expedition timer (`RemainingTicks`), the current `Phase`, and the running state. It lives on the `NetworkMatchController` prefab, which the Host spawns from `FusionSessionLauncher`. Only State Authority mutates its state. It is synchronized across peers and is resimulation-safe.
- **`EnemyReinforcementDirector` (NetworkBehaviour):** Evaluates spawn intervals and executes reinforcement spawns based on the active `DungeonPressureConfig` policy for the current phase. It runs exclusively under State Authority and only while the Raid is `InProgress`. Reinforcements are disabled only in `Normal`; `Reinforcements`, `CriticalPressure` and `Collapse` generate reinforcements with their own policy. The minimum distance to players is measured against the live avatars (`NetworkSpawnManager.ActiveAvatarObjects`), not against the participant objects, which stay at the spawn point.
- **`DungeonPressureConfig` (ScriptableObject):** The static configuration containing total duration, phase thresholds, and reinforcement policies (budgets, intervals, caps) per phase. It is never mutated at runtime. `Validate` enforces monotonic escalation: `Normal` has no budget, `CriticalPressure` is not weaker than `Reinforcements`, and `Collapse` has a positive budget and is not weaker than `CriticalPressure` (budget greater or equal, `EvaluationIntervalSeconds` lower or equal, `MaxSpawnsPerAttempt` greater or equal). Final values are owned by Balance.
- **`PvePopulationTracker` (Runner-scoped C# Service):** Tracks the live, active PvE population. It maintains a deduplicated registry of alive enemies, differentiated by `EnemyPopulationOrigin` (Bootstrap vs. Reinforcement). It is completely decoupled from the enemy AI itself.
- **`ReinforcementPointRegistry` (Runner-scoped C# Service):** Caches and validates `SpawnGroupType.Reinforcements` transforms from the scene configuration upon scene load, avoiding global searches during active gameplay.
- **`NetworkSpawnManager`:** Remains the low-level executor for spawns. It exposes a focused `internal` method to spawn an enemy at a specific point, but it **does not** own the pressure logic, budgets, or intervals.

## 3. Lifecycle Integration

### 3.1. Initialization and Raid Start
- The pressure system initiates its logic only when `NetworkMatchController.Phase` transitions to `InProgress` (after successful `TryStartRaid` and bootstrap).
- Bootstrap enemies are spawned by `NetworkSpawnManager` under the `Bootstrap` origin. They do not consume the `PopulationBudget` (which is exclusive to reinforcements), but they do count toward the global `MaxGlobalEnemies` cap.
- The `NetworkMatchController` prefab (which carries `DungeonPressureController` and `EnemyReinforcementDirector`) is dynamically spawned by the Host from `FusionSessionLauncher` with `NetworkSpawnFlags.DontDestroyOnLoad`.

### 3.2. Raid Closure
- The system stops consuming time and generating reinforcements when the Raid transitions to `Closing` or `Finished`.
- The `DungeonPressureController` transitions to `Stopped` when the Raid enters `Closing` or `Finished`.
- `NetworkSpawnManager.TryCleanupRaidWorldForResults` calls `ResetForRaidClosure()` on the `PvePopulationTracker` and `ReinforcementPointRegistry`, ensuring the runner-scoped state is cleanly wiped for the next generation.
- The results cleanup intentionally **skips** the `NetworkMatchController` object, so the `DungeonPressureController` is not despawned there. It is released together with the `NetworkMatchController` at runner shutdown, which remains the definitive generation cleanup boundary.

## 4. Host Migration Recovery

The system must remain deterministic and robust across Host Migration:
- **Timer and Phase:** The state (`RemainingTicks`, `Phase`, `State`) of the `DungeonPressureController` is restored using the standard snapshot copy (`CopyStateFrom`). The system relies on absolute remaining ticks, which decouple it from the underlying engine tick base that changes across migrations.
- **Recovery Window:** The timer is **paused** and the `EnemyReinforcementDirector` is suspended while `NetworkSpawnManager.IsHostMigrationRecoveryInProgress` is true (the recovery window).
- **Population Tracker Reconstruction:** `PvePopulationTracker` is not networked directly. Instead, it is **reconstructed** when restored alive enemies execute `Spawned()` on the new Host. The `[Networked]` field `EnemyPopulationOrigin` on the enemy guarantees correct re-categorization (Bootstrap vs. Reinforcements).
- **No Double Spawns:** The `Spawned()` implementation of `DungeonPressureController` explicitly checks `ShouldInitializeMatchPhase` (the resume guard) to avoid overriding restored values with fresh initialization.

## 5. Technical Contracts by Stage

- **Stage 2 (Timer & Phases):** `DungeonPressureController` dictates the phase. Transits sequentially `Normal -> Reinforcements -> CriticalPressure -> Collapse`. Collapse freezes the timer at 0 while `State` stays `Running`, so the Director keeps evaluating.
- **Collapse:** Applies the strongest reinforcement policy of the MVP for its whole duration. Entering Collapse does not despawn or otherwise remove active enemies; a phase change never destroys enemies that are already alive.
- **Collapse reinforcement loot:** Game Design requires that enemies spawned during Collapse are not an exploitable reward source. The concrete rule (no loot, reduced loot or another rule) is **pending Game Design confirmation**; until it is defined, Collapse reinforcements roll loot like any other reinforcement.
- **Stage 3 (Reinforcement Points):** `SpawnGroupType.Reinforcements` is added. The bootstrap flow explicitly ignores this group (`SpawnKind.ReinforcementPoints`).
- **Stage 4 (Population Tracker):** Enemies inform the tracker upon `Spawned()` and `HandleDeath()`. Defeated enemies stop consuming population, even if their network object remains alive as a loot container.
- **Stage 5 & 6 (Director & Config):** The Director retrieves the policy from `DungeonPressureConfig.GetPolicy(Phase)`. It relies on a pure `ReinforcementSpawnPlanner` to determine the amount to spawn based on `PopulationBudget` (active reinforcement threat limit), global caps, `MaxSpawnsPerAttempt`, `EvaluationIntervalSeconds`, and `MinSecondsBetweenSpawns`. On every phase change the evaluation timer is restarted with the new policy's interval, so a transition never triggers an instant wave. It surfaces rejections through the `ReinforcementRejection` enum.
- **Stage 7 (Host Migration):** Validates the snapshot integration, tracker reconstruction, and recovery pause logic.
- **Stage 8 (Debug):** Provides a strict `#if UNITY_EDITOR` overlay for timeline manipulation and spawn verification.
