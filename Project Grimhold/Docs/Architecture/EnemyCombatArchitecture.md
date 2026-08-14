# Enemy Combat & AI Architecture

This document describes the architecture, components, state machine (FSM), network authority model, target reference model, and presentation layers for the Enemy system in Project Grimhold.

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
| **`Idle`** | Standard locomotion / wandering. | `true` | `false` |
| **`Chase`** | Pursuit of acquired target (`PlayerCharacter`). | `true` | `false` |
| **`Attack`** | Stationary combat execution when target is in range. | `false` | `true` |
| **`Dead`** | Terminal state triggered upon character death. | `false` | `false` |

---

## Canonical Target Representation & Invalidation (`EnemyTargetReference`)

To prevent target desynchronization and handle destroyed entity references safely:

1. **Atomic Representation**:
   `EnemyMovementAIController` encapsulates target identity inside an immutable `EnemyTargetReference` struct:
   ```csharp
   private readonly struct EnemyTargetReference
   {
       public EntityId Id { get; }
       public Transform Transform { get; }
   }
   ```
   - `Id` and `Transform` are assigned and cleared together by replacing the structure with `default`.
   - `Id.Value != 0` determines whether a stored target identity exists.
   - A non-zero `Id` with a destroyed (`null`) `Transform` represents a structurally invalid target pending invalidation.

2. **Target Query & Conditional Invalidation**:
   - `TryGetCurrentTarget(out EntityId targetId, out Transform targetTransform)`:
     - Returns snapshot of stored target without clearing internal state.
     - Returns `true` only when `targetId.Value != 0` AND `targetTransform != null`.
   - `TryInvalidateCurrentTarget(EntityId expectedTargetId)`:
     - Requires `expectedTargetId.Value != 0`.
     - Compares strictly against currently stored `_currentTarget.Id`.
     - If matched, clears target reference, pursuit, and attack flags together.
     - Protects newly acquired targets against late invalidations from previous targets.

3. **Authoritative Combat Target Revalidation (`EnemyCombatAIController`)**:
   During `FixedUpdateNetwork`:
   - Calls `TryGetCurrentTarget`.
   - If returned `false` with a valid `EntityId`: requests invalidation calling `TryInvalidateCurrentTarget(targetId)`.
   - If returned `true`: resolves target capabilities (`IDamageable` and `ICharacter`) via runner-scoped `EntityRegistry`.
   - If `EntityRegistry` resolution fails or target is not alive (`!ICharacter.IsAlive`) or cannot receive damage (`!IDamageable.CanReceiveDamage`), requests invalidation for `targetId`.
   - Only when target resolution and eligibility checks pass does it execute `IAttack`.
   - On any rejection: skips strategy execution, cooldown startup, and sequence increments, allowing FSM to observe clean flags and transition safely.

The attack hit-frame boundary stores the target `EntityId` accepted with the pending request. Immediately before strategy execution it requires that identity to remain the current target and resolves `ICharacter` and `IDamageable` again from the runner registry. Failure clears the pending hit and invalidates only the matching target; it never executes damage against a stale cached `Transform`. The AI depends only on shared capabilities and contains no reference to extraction state or controllers.

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
4. **`CharacterAnimatorView`**: Shared base animator view for players and enemies. In `Update()`, it converts continuous `FacingDirection` into canonical 6-direction vectors via `CharacterVisualDirectionResolver` before setting `MoveX` and `MoveY`, ensuring that both player and enemy BlendTrees sample discrete 6-way directional frames.
5. **`CombatPresenterBase`**: Shared base presenter for procedural attack animations (swings, arcs, weapon pivots).
6. **`DefeatPresenterBase`**: Shared base presenter for procedural death transitions (rotation, alpha fadeout).

---

## Defeated Enemy Loot Persistence (`EnemyCharacter`)

When an enemy is defeated (authoritative health reaches 0), `EnemyCharacter.HandleDeath()` handles the transition:
* **No Secondary Corpse Prefab**: The defeated enemy remains the exact same network entity (`NetworkObject`), preserving its `NetworkId`, position, colliders, and initial pre-rolled loot dictionary.
* **Simulation Termination**: State Authority disables movement (`_movementController.TrySetControlEnabled(false)`) and combat (`_combatController.TrySetAttackEnabled(false)`).
* **Container Exposure**: Enables the co-located `NetworkLootContainer` availability (`_lootContainer.SetAvailability(true)`) during simulation, allowing nearby players to interact with and extract loot through the standard `LootInteractionArchitecture.md` flow.
