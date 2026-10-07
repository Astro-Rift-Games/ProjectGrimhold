# Dungeon Pressure Architecture (PvE)

## 1. Context and Objective

The Dungeon Pressure system orchestrates the dynamic PvE escalation during a Raid. It introduces a phased expedition timer (Normal, Reinforcements, CriticalPressure, Collapse) and dynamically spawns reinforcement enemies based on configured budgets and live population tracking.

**Collapse Goal vs. Current State:**
- **Goal (Game Design):** An absolute closure of the dungeon after the Collapse phase, resulting in the definitive Defeat of any players remaining inside.
- **Current MVP State:** The actual collapse absolute closure is pending implementation. Currently, when the timer reaches 0, the dungeon enters the Collapse phase. The timer freezes at 0, but the Director continues to evaluate and generate reinforcements with the strongest policy of the MVP to apply maximum PvE pressure. Enemies are not despawned.

This document defines the technical contracts and ownership boundaries for the system, ensuring it integrates securely with the existing Raid generation lifecycle, Host Migration, and authoritative gameplay simulation.

## 2. Ownership and Boundaries

The system is designed with strict separation of concerns, avoiding global event buses and single-singleton managers.

- **`DungeonPressureController` (NetworkBehaviour):** The single source of truth for the authoritative expedition timer (`RemainingTicks`), the current `Phase`, and the running state. It lives on the `NetworkMatchController` prefab, which the Host spawns from `FusionSessionLauncher`. Only State Authority mutates its state. It is synchronized across peers and is resimulation-safe.
- **`EnemyReinforcementDirector` (NetworkBehaviour):** Evaluates spawn intervals and executes reinforcement spawns based on the active `DungeonPressureConfig` policy for the current phase. It runs exclusively under State Authority and only while the Raid is `InProgress` and the controller is `Running`. The minimum distance to players is measured against the live avatars (`NetworkSpawnManager.ActiveAvatarObjects`), not against the participant objects, which stay at the spawn point.
- **`DungeonPressureConfig` (ScriptableObject):** The static configuration containing total duration, phase thresholds, and reinforcement policies (budgets, intervals, caps) per phase. It is never mutated at runtime. `Validate` enforces specific escalation rules (see Stage 5 & 6). Final values are owned by Balance.
- **`PvePopulationTracker` (Runner-scoped C# Service):** The single source of truth for the live, active PvE population. It maintains a deduplicated registry of alive enemies, differentiated by `EnemyPopulationOrigin` (Bootstrap vs. Reinforcement). It is completely decoupled from the enemy AI itself.
- **`ReinforcementPointRegistry` (Runner-scoped C# Service):** Caches and validates `SpawnGroupType.Reinforcements` transforms from the scene configuration upon scene load, avoiding global searches during active gameplay.
- **`NetworkSpawnManager`:** Remains the low-level executor for spawns. It exposes a focused `internal` method to spawn an enemy at a specific point, but it **is not the owner** of the pressure system, logic, budgets, or intervals.

## 3. Lifecycle Integration

### 3.1. Initialization and Raid Start
- The pressure system initiates its logic only when `NetworkMatchController.Phase` transitions to `InProgress` (after successful `TryStartRaid` and bootstrap).
- Bootstrap enemies are spawned by `NetworkSpawnManager` under the `Bootstrap` origin. They are strictly separated from reinforcements: they do not consume the `PopulationBudget` (which is exclusive to dynamic reinforcements), but they do count toward the global `MaxGlobalEnemies` cap.
- The `NetworkMatchController` prefab (which carries `DungeonPressureController` and `EnemyReinforcementDirector`) is dynamically spawned by the Host from `FusionSessionLauncher` with `NetworkSpawnFlags.DontDestroyOnLoad`.

### 3.2. Raid Closure
- The system stops consuming time and generating reinforcements when the Raid transitions to `Closing` or `Finished`.
- The `DungeonPressureController` transitions to `Stopped` when the Raid enters `Closing` or `Finished`.
- `NetworkSpawnManager.TryCleanupRaidWorldForResults` calls `ResetForRaidClosure()` on the `PvePopulationTracker` and `ReinforcementPointRegistry`, ensuring the runner-scoped state is cleanly wiped for the next generation.
- The results cleanup intentionally **skips** the `NetworkMatchController` object, so the `DungeonPressureController` is not despawned there. It is released together with the `NetworkMatchController` at runner shutdown, which remains the definitive generation cleanup boundary.

## 4. Host Migration Recovery

The system must remain deterministic and robust across Host Migration:
- **Timer and Phase:** The state (`RemainingTicks`, `Phase`, `State`) of the `DungeonPressureController` is restored using the standard snapshot copy (`CopyStateFrom`). The system relies on absolute remaining ticks, which decouple it from the underlying engine tick base that changes across migrations.
- **Director State:** The `EnemyReinforcementDirector` state is also restored using `CopyStateFrom`, preserving its `[Networked]` fields (`TotalSpawnsGenerated`, `_evaluationTimerTicks`, `_minSpawnTimerTicks`, and `_lastPhase`).
- **Recovery Window:** The timer is **paused** and the `EnemyReinforcementDirector` is suspended while `NetworkSpawnManager.IsHostMigrationRecoveryInProgress` is true (the recovery window).
- **Population Tracker Reconstruction:** `PvePopulationTracker` is not networked directly. Instead, it is **reconstructed** when restored alive enemies execute `Spawned()` on the new Host. The `[Networked]` field `EnemyPopulationOrigin` on the enemy guarantees correct re-categorization (Bootstrap vs. Reinforcements).
- **No Double Spawns:** The `Spawned()` implementation of `DungeonPressureController` explicitly checks `ShouldInitializeMatchPhase` (the resume guard) to avoid overriding restored values with fresh initialization.

## 5. Technical Contracts by Stage

- **Stage 2 (Timer & Phases):** `DungeonPressureController` dictates the phase. Transits sequentially `Normal -> Reinforcements -> CriticalPressure -> Collapse`. When entering `Collapse`, the timer freezes at 0 while `State` stays `Running`. The Director applies the strongest reinforcement policy of the MVP for its whole duration. Entering Collapse does not despawn or otherwise remove active enemies; a phase change never destroys enemies that are already alive.
- **Collapse reinforcement loot:** Game Design requires that enemies spawned during Collapse are not an exploitable reward source. The rule is: reinforcements generated during Collapse do not grant rewards (they are generated without loot).
- **Stage 3 (Reinforcement Points):** `SpawnGroupType.Reinforcements` is added. The bootstrap flow explicitly ignores this group (`SpawnKind.ReinforcementPoints`).
- **Stage 4 (Population Tracker):** Enemies inform the tracker upon `Spawned()` and `HandleDeath()`. Defeated enemies stop consuming population, even if their network object remains alive as a loot container.
- **Stage 5 & 6 (Director & Config):** The Director retrieves the policy from `DungeonPressureConfig.GetPolicy(Phase)`. `Validate` enforces strict rules: `Normal` must have 0 budget, `Collapse` must have >0 budget, `MaxSpawnsPerAttempt` cannot exceed `PopulationBudget`, and successive phases (Reinforcements -> CriticalPressure -> Collapse) must have an equal or greater `PopulationBudget`, an equal or lesser `EvaluationIntervalSeconds`, and an equal or greater `MaxSpawnsPerAttempt` compared to the previous phase. On every phase change the evaluation timer is restarted with the new policy's interval, so a transition never triggers an instant wave. It surfaces rejections through the `ReinforcementRejection` enum.
- **Stage 7 (Host Migration):** Validates the snapshot integration, tracker reconstruction, and recovery pause logic.
- **Stage 8 (Debug):** Provides a strict `#if UNITY_EDITOR` overlay for timeline manipulation and spawn verification.

## 6. Verification Criteria Mapping

| Criterio de Aceptación (#455) | Sección / Entidad |
| :--- | :--- |
| **Única fuente de verdad para la fase temporal** | [2. Ownership and Boundaries](#2-ownership-and-boundaries) (`DungeonPressureController`) |
| **Bootstrap y refuerzos tienen responsabilidades separadas** | [3.1. Initialization and Raid Start](#31-initialization-and-raid-start) |
| **Owner de la población PvE activa** | [2. Ownership and Boundaries](#2-ownership-and-boundaries) (`PvePopulationTracker`) |
| **NetworkSpawnManager no es owner del sistema de presión PvE** | [2. Ownership and Boundaries](#2-ownership-and-boundaries) (`NetworkSpawnManager`) |
| **Qué estado se restaura tras Host Migration** | [4. Host Migration Recovery](#4-host-migration-recovery) |
| **State Authority es el único escritor** | [2. Ownership and Boundaries](#2-ownership-and-boundaries) |
| Representación de fases temporales | [5. Technical Contracts by Stage](#5-technical-contracts-by-stage) (`Normal`, `Reinforcements`, `CriticalPressure`, `Collapse`) |
| Configuración estática de tiempos, budgets y cadencias | [2. Ownership and Boundaries](#2-ownership-and-boundaries) (`DungeonPressureConfig`) |
| Owner del sistema de refuerzos | [2. Ownership and Boundaries](#2-ownership-and-boundaries) (`EnemyReinforcementDirector`) |
| Interacción con NetworkMatchController | [3.1. Initialization and Raid Start](#31-initialization-and-raid-start) |
| Limpieza del estado al finalizar Raid | [3.2. Raid Closure](#32-raid-closure) |

## 7. Pending / Technical Debt

- **[Game Design / Implementation]** The automatic absolute closure of the dungeon, which should definitively defeat all players remaining in the instance after the time ends (17:00), is pending implementation. 
- **[Game Design / Balance]** Final values for time phases, Threat Costs, Spawn Weights, Population Budgets, and EvaluationIntervals are pending testing and balance adjustments.
