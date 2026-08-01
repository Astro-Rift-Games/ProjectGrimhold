# Extraction Subsystem Architecture

## Overview

The Extraction Subsystem governs player extraction from raids in Project Grimhold. It will combine data-driven parameters, physical spatial zone detection, networked tick-driven simulation, and visual/auditory presentation.

This document outlines the sources of truth, network authority model, state management, and separation of concerns across tasks **TASK-26** (Implemented) through **TASK-27**, **TASK-28**, and **TASK-29** (Planned Future Scope).

---

## Subsystem Implementation Status & Sources of Truth

| Concern | Layer / Component | Source of Truth | Task & Status |
| :--- | :--- | :--- | :--- |
| **Shared Parameters** | Data-driven ScriptableObject | `ExtractionConfig` asset | **TASK-26** `[Implemented]` |
| **Zone Physical Geometry** | Scene / Prefab Physics | Serialized `Collider2D` + `BoundaryTolerance` | **TASK-27** `[Planned / Future Scope]` |
| **Process State & Timers** | Fusion Network Simulation | State Authority (`FixedUpdateNetwork`) | **TASK-28** `[Planned / Future Scope]` |
| **Presentation & UI** | Client Presentation | Event-driven UI / Presenter components | **TASK-29** `[Planned / Future Scope]` |

---

## Architectural Boundaries & Task Responsibilities

### TASK-26 — Core Extraction Configuration `[Implemented]`
- **Responsibility**: Defines shared, immutable data configuration parameters for extraction routines.
- **Key Types**: `ExtractionConfig` (`ScriptableObject`).
- **Configured Parameters**:
  - `CountdownDurationSeconds`: Duration in seconds required to complete extraction.
  - `CancelWhenLeavingArea`: Automatically cancels progress if player leaves zone bounds.
  - `BoundaryTolerance`: Non-negative floating-point buffer added to `Collider2D` boundary checks.
  - `RequireAliveToStart`: Requires player to be alive (`ICharacter.IsAlive`) to initiate extraction.
  - `CancelWhenNotAlive`: Automatically cancels extraction if player ceases to be alive (`!ICharacter.IsAlive`).
- **Boundaries & Constraints**:
  - Does NOT inherit from `NetworkBehaviour`.
  - Contains NO `[Networked]` properties, Fusion dependencies, or mutable runtime state.
  - Contains NO references to zone prefabs, scene colliders, or player instances.
  - Does NOT execute timers, network ticks, or state transitions.
  - **Time Conversion Delegation**: `ExtractionConfig` stores duration strictly as seconds. Conversion into Fusion simulation time or `TickTimer` is explicitly delegated to **TASK-28**.

### TASK-27 — Zone Detection & Geometry `[Planned / Future Scope]`
- **Responsibility**: Manages physical trigger volumes for extraction locations.
- **Boundaries & Constraints**:
  - Physical `Collider2D` components attached to zone prefabs/instances define spatial boundaries.
  - Evaluates spatial inclusion by combining collider geometry with `ExtractionConfig.BoundaryTolerance`.
  - Zone colliders contain spatial configuration only; they do not own process timers or player extraction state.

### TASK-28 — Network Simulation & State Authority `[Planned / Future Scope]`
- **Responsibility**: Executes authoritative extraction state machine during network simulation ticks.
- **Boundaries & Constraints**:
  - Runs inside `FixedUpdateNetwork` on a `NetworkBehaviour` owned by **State Authority** (Host).
  - State Authority is the sole authority confirming extraction start, cancellation, and completion.
  - Converts `ExtractionConfig.CountdownDurationSeconds` into a network-synchronized `TickTimer` using `NetworkRunner`.
  - Re-uses the existing `ICharacter.IsAlive` contract for vitality checks without introducing a parallel vitality state enum.
  - Authoritative process state (e.g., `None`, `Extracting`, `Extracted`) is tracked via `[Networked]` properties.
  - **Extraction Start**: Requires `RequireAliveToStart` check (`ICharacter.IsAlive == true`) and process state == `None`.
  - **Re-extraction Prevention**: `Extracted` is a terminal process state managed by State Authority. Once in `Extracted` state, restarting extraction is permanently prevented.
  - **Cancellation Rules**: Cancels countdown if player leaves physical zone bounds (when `CancelWhenLeavingArea` is `true`) or if vitality drops (`CancelWhenNotAlive` is `true` and `!ICharacter.IsAlive`).

### TASK-29 — Presentation & Visual Representation `[Planned / Future Scope]`
- **Responsibility**: Displays extraction progress UI, HUD notifications, audio cues, and visual disappearance.
- **Boundaries & Constraints**:
  - Presentation components observe `[Networked]` simulation state changes.
  - Presentation effects (such as progress bars or player entity hide/disappearance delays) are strictly managed within presentation classes.
  - No presentation delays or UI logic exist inside `ExtractionConfig` or simulation code.

---

## Validation Strategy

1. **Static Data Validation (TASK-26)**:
   - `ExtractionConfig.TryValidate(out string error)` ensures finite countdown duration (`CountdownDurationSeconds > 0`) and non-negative finite boundary buffer (`BoundaryTolerance >= 0`).
2. **EditMode Unit Tests (TASK-26)**:
   - Verify immutable property accessors (no public setters).
   - Test boundary validation failure cases (zero, negative, NaN, or infinite parameters).
   - Confirm valid configuration loading and property preservation.
3. **Simulation & Integration Validation (Future Scope - TASK-27/28/29)**:
   - Validate State Authority transitions, zone spatial queries, and presentation reactions when those tasks are implemented.
