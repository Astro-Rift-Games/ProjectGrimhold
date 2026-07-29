# Enemy Combat & AI Architecture

This document describes the architecture, components, state machine (FSM), network authority model, and presentation layers for the Enemy system in Project Grimhold.

## Architectural Overview

The Enemy system matches the modular, Strategy-based architecture of the Player, replacing local input streams with authoritative AI controllers and a Finite State Machine (FSM).

```text
               EnemyFSM (Networked State Machine)
                              │
             ┌────────────────┴────────────────┐
             ▼                                 ▼
   EnemyMovementAIController        EnemyCombatAIController
   (IMovementState)                 (ICombatController)
             │                                 │
             ▼                                 ▼
   Kinematic2DMovementMotor         Active Strategy (IAttack)
             │                                 │
             └────────────────┬────────────────┘
                              ▼
           Presentation Layer (CharacterAnimatorView,
           EnemyCombatPresenter, EnemyDefeatPresenter)
```

---

## High-Level State Machine (`EnemyFSM`)

The enemy state machine coordinates high-level behaviors authoritatively via `EnemyFSM` (`NetworkBehaviour`).

### States

| State | Responsibility | Control Enabled | Combat Enabled |
| :--- | :--- | :--- | :--- |
| **`Patrol`** | Standard locomotion / wandering. | `true` | `false` |
| **`Chase`** | Pursuit of acquired target (`PlayerCharacter`). | `true` | `false` |
| **`Attack`** | Stationary combat execution when target is in range. | `false` | `true` |
| **`Dead`** | Terminal state triggered upon character death. | `false` | `false` |

### Extensibility

`EnemyFSM` supports registering custom states implementing `IEnemyState`:

```csharp
public interface IEnemyState
{
    EnemyStateType Type { get; }
    void Enter(EnemyFSM fsm);
    void FixedUpdateNetwork(EnemyFSM fsm);
    void Exit(EnemyFSM fsm);
}
```

New states (e.g. `StunnedState`, `FleeState`) can be added by implementing `IEnemyState` and calling `fsm.RegisterState(new CustomState())`.

---

## Network AI Combat (`EnemyCombatAIController`)

* **Network Boundary**: Extends `NetworkBehaviour` and implements `ICombatController`.
* **State Authority**: Only the State Authority evaluates attack intentions from `EnemyMovementAIController.IsAttacking` during network tick simulation (`FixedUpdateNetwork`).
* **Strategy Execution**: Delegates combat execution to an assigned `IAttack` strategy (e.g., `MeleeAttack` or `RangedAttack`).
* **Network Replication**: Replicates `AttackSequence`, `LastAttackOrigin`, `LastAttackDirection`, and tick information to allow proxy clients to render attack animations smoothly via `AttackPerformed` events.

---

## Shared Presentation Abstractions

To eliminate code duplication between Player and Enemy entities, presentation and animation components inherit from shared base classes:

1. **`IMovementState`**: Exposes `FacingDirection`, `IsMoving`, and `IsControlEnabled`. Implemented by both `PlayerMovementNetworkController` and `EnemyMovementAIController`.
2. **`ICombatController`**: Exposes `AttackPerformed` event and `IsAttackEnabled`. Implemented by both `PlayerCombatNetworkController` and `EnemyCombatAIController`.
3. **`IAnimatorController`**: Exposes animation override methods (`ApplyTemporalFacingDirection`, `ClearTemporalFacingDirection`, `SetDefeated`). Implemented by `CharacterAnimatorView`.
4. **`CharacterAnimatorView`**: Shared base animator view for players and enemies.
5. **`CombatPresenterBase`**: Shared base presenter for procedural attack animations (swings, arcs, weapon pivots).
6. **`DefeatPresenterBase`**: Shared base presenter for procedural death transitions (rotation, alpha fadeout).

---

## Defeated Enemy Loot Persistence (`EnemyCharacter`)

When an enemy is defeated (authoritative health reaches 0), `EnemyCharacter.HandleDeath()` handles the transition:
* **No Secondary Corpse Prefab**: The defeated enemy remains the exact same network entity (`NetworkObject`), preserving its `NetworkId`, position, colliders, and initial pre-rolled loot dictionary.
* **Simulation Termination**: State Authority disables movement (`_movementController.TrySetControlEnabled(false)`) and combat (`_combatController.TrySetAttackEnabled(false)`).
* **Container Exposure**: Enables the co-located `NetworkLootContainer` availability (`_lootContainer.SetAvailability(true)`) during simulation, allowing nearby players to interact with and extract loot through the standard `LootInteractionArchitecture.md` flow.

