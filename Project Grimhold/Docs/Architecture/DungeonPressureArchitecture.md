# Dungeon Pressure System Architecture

## 1. Context and Objective

The Dungeon Pressure system acts as the instance timeline and escalating tension director within Raid matches. Its purpose is to guide players naturally toward extraction by escalating environment danger, dynamic monster reinforcements, visual communication, and final collapse closure. It orchestrates dynamic PvE escalation during a Raid through a phased expedition timer (Normal, Reinforcements, CriticalPressure, Collapse) and dynamically spawns reinforcement enemies based on configured budgets and live population tracking.

**Collapse Goal and Absolute Closure (TASK-192):**
- **Goal (Game Design):** An absolute closure of the dungeon after the Collapse phase, resulting in the definitive Defeat of any players remaining inside.
- **Implemented Contract (TASK-192):** When the normal/critical timer reaches 0, the dungeon enters the `Collapse` phase. During Collapse, the controller counts down `CollapseRemainingTicks` configured by `DungeonPressureConfig.CollapseDurationSeconds` (default: 120s, for a total of 17:00). When the collapse duration expires, `DungeonPressureController` transitions to `Stopped` and triggers `NetworkSpawnManager.DefeatRemainingRaidingParticipantsForAbsoluteClosure()`. This authoritatively forces canonical Definitive Defeat on all remaining raiding participants (clearing Downed state, zeroing health, and generating corpse loot) and marks disconnected participants terminal. With all participants resolved, `NetworkMatchController` naturally closes the raid (`RaidClosureReason.NaturalCompletion`).

The shipped `DefaultDungeonPressureConfig.asset` uses 900 seconds before Collapse,
120 seconds of Collapse (17:00 total), and remaining-time phase thresholds of 240/60
seconds. If canonical defeat cannot complete (including a missing avatar), the
participant remains tracked and an error is logged; disconnected participants are
not marked terminal or removed from retention while still `Raiding`. There is no
avatar-less defeat route that bypasses canonical corpse/material resolution.

This document defines the technical contracts and ownership boundaries for the system, ensuring it integrates securely with the existing Raid generation lifecycle, Host Migration, and authoritative gameplay simulation.

## 2. Ownership and Boundaries

- **State Authority:** Server/Host is the sole authority writing to `DungeonPressureController` and `EnemyReinforcementDirector`.
- **DungeonPressureController:** Sole source of truth for the active pressure phase, remaining instance time, and state transitions. Replicated across all clients.
- **EnemyReinforcementDirector:** Governs spawn evaluation intervals, budget calculations, and reinforcement requests to `NetworkSpawnManager`.
- **PvePopulationTracker:** Owns active PvE population tracking and informs the director of current headroom.
- **DungeonPressureAmbientPresenter:** Local client presentation component. Modulates global ambient lighting, camera trauma shake, post-process vignette, and falling ceiling debris particles without mutating simulation or networked state.
- **DungeonLightProp:** Local client presentation component. Modulates local point lights (torches, braziers, candles) with organic flickering, unstable drafts, and extinction support.
- **DungeonPressureAudioPresenter:** Local client audio presenter. Plays phase entry SFX, periodic tremor sounds, and executes crossfades to Collapse background music.

## 3. Session Lifecycle and Boundaries

### 3.1. Initialization and Raid Start
- Bootstrap spawns monsters according to initial room policies.
- Once players spawn and match begins, `DungeonPressureController` starts ticking down instance time.
- Successive thresholds trigger phase changes: `Normal` -> `Reinforcements` -> `CriticalPressure` -> `Collapse`.

### 3.2. Raid Closure and Absolute Collapse
- When Collapse duration expires, `DungeonPressureController` halts time, changes state to `Stopped`, and requests `NetworkSpawnManager` to defeat any remaining raiders who failed to extract.

## 4. Host Migration Recovery

- When a new host is elected, the runner snapshot reconstructs `DungeonPressureController` state, remaining ticks, and active phase.
- `PvePopulationTracker` repopulates its tracking set from active living enemy entities in the scene.
- Presenters resolve the current state idempotently without replaying obsolete transitions or historical shake bursts.

## 5. Technical Contracts by Stage

- **Stage 1 (Phases & Timers):** Networked state holds remaining ticks and current `DungeonPressurePhase`.
- **Stage 2 (Spawning Flow):** Requests flow through `NetworkSpawnManager.SpawnEnemyFromPressure(...)`.
- **Stage 3 (Reinforcement Points):** `SpawnGroupType.Reinforcements` is added. The bootstrap flow explicitly ignores this group (`SpawnKind.ReinforcementPoints`).
- **Stage 4 (Population Tracker):** Enemies inform the tracker upon `Spawned()` and `HandleDeath()`. Defeated enemies stop consuming population, even if their network object remains alive as a loot container.
- **Stage 5 & 6 (Director & Config):** The Director retrieves the policy from `DungeonPressureConfig.GetPolicy(Phase)`. `Validate` enforces strict rules: `Normal` must have 0 budget, `Collapse` must have >0 budget, `MaxSpawnsPerAttempt` cannot exceed `PopulationBudget`, and successive phases (Reinforcements -> CriticalPressure -> Collapse) must have an equal or greater `PopulationBudget`, an equal or lesser `EvaluationIntervalSeconds`, and an equal or greater `MaxSpawnsPerAttempt` compared to the previous phase. On every phase change the evaluation timer is restarted with the new policy's interval, so a transition never triggers an instant wave. It surfaces rejections through the `ReinforcementRejection` enum.
- **Stage 7 (Host Migration):** Validates the snapshot integration, tracker reconstruction, and recovery pause logic.
- **Stage 8 (Debug):** Provides a strict `#if UNITY_EDITOR` overlay for timeline manipulation and spawn verification.
- **Stage 9 (Ambient Communication & Collapse Presentation - #479, #520):** `DungeonPressureAmbientPresenter` translates the replicated `DungeonPressurePhase` into global environmental presentation without modifying gameplay or adding networked state. Uses `DungeonAmbientConfig` for global light modulation, screen shake (entry shake, sustained continuous tremor, and periodic interval tremors) via `CameraShakeController`, native URP post-process vignette modulation via `Volume` and `Vignette`, and falling ceiling debris & dust particles via `ParticleSystem` following active camera viewport. Modulates local props (torches, braziers) via `DungeonLightProp` and `DungeonLightPropConfig` with customizable effect patterns (`Steady`, `TorchFlicker`, `UnstableFlicker`, `Pulse`) and unlit sprite/flame extinction. `DungeonPressureAudioPresenter` transitions background music to Collapse track with crossfade and triggers synchronized tremor SFX. Supports late join, host migration, and offline editor preview idempotently without replaying past transition shakes.

## 6. Verification Criteria Mapping

| Criterio de Aceptación (#455, #478, #479, #520) | Sección / Entidad |
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
| Cierre absoluto de Dungeon y derrota de participantes | [1. Context and Objective](#1-context-and-objective), [5. Technical Contracts by Stage](#5-technical-contracts-by-stage) |
| Comunicación ambiental y luces de props por fase | [5. Technical Contracts by Stage](#5-technical-contracts-by-stage) (`DungeonPressureAmbientPresenter`, `DungeonLightProp`) |
| Presentación completa del Colapso (polvo/rocas, audio, música, extinción, accesibilidad) | [5. Technical Contracts by Stage](#5-technical-contracts-by-stage) (`DungeonPressureAmbientPresenter`, `DungeonPressureAudioPresenter`, `CameraShakeController`) |

## 7. Pending / Technical Debt

- **[Game Design / Balance]** Final values for time phases, Threat Costs, Spawn Weights, Population Budgets, and EvaluationIntervals are pending testing and balance adjustments.
- **[Art Assets]** Definite Catacombs unlit torch/brazier sprites and animated flame assets from Art team are pending drop-in into `DungeonLightProp` slots.
