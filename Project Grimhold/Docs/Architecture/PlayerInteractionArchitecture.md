# Player Interaction Architecture

This document defines the architecture and data flow of the interaction system in **Project Grimhold**.

The system allows characters to perform contextual actions on world objects, such as collecting loot, activating mechanisms, and opening doors. Interaction is network-authoritative and safe under Fusion resimulation.

## 1. Structure and data flow

The flow keeps these responsibilities separate:

1. **Local input capture (Input Authority):** `PlayerInputReader` detects `PlayerInputButton.Interact` and writes it into `PlayerNetworkInput`.
2. **Input transport:** `FusionInputProvider` sends the input through Fusion.
3. **Network simulation (State Authority):** `PlayerInteractionNetworkController.FixedUpdateNetwork` validates and processes the interaction intent.
4. **Pure selection policy:** `InteractionResolver` selects a candidate from spatial and priority data.
5. **Interactable behavior:** each `IInteractable` owns the business rules for its interaction.
6. **Presentation:** confirmed results and local predictive candidates are published during `Render` for local presenters.

```text
PlayerInputReader (local input)
       |
       v
FusionInputProvider (input transport)
       |
       v
PlayerInteractionNetworkController.FixedUpdateNetwork (State Authority)
       |-- candidate query (IInteractionTargetQuery / Physics2D)
       |-- selection policy (InteractionResolver)
       |     |-- distance and exclusion checks
       |     `-- authoritative IInteractable.Interact invocation
       v
[Networked] InteractionSequence increment
       |
       v
PlayerInteractionNetworkController.Render
       `-- local InteractionResolved notification
```

## 2. Core components

### 2.1 `PlayerInteractionNetworkController`

The controller coordinates interaction during network simulation ticks:

- It processes gameplay exclusively under State Authority in `FixedUpdateNetwork`.
- It detects button edges with Fusion's `WasPressed` API so holding the button does not repeat interactions.
- It validates the character's interaction eligibility.
- It obtains spatial candidates through `IInteractionTargetQuery`.
- It delegates candidate selection and interaction execution to `InteractionResolver`.
- It records target, result, tick, and `InteractionSequence` in replicated state.

### 2.2 `IInteractionTargetQuery`

This contract defines how candidates are found in the 2D world. `Physics2DInteractionTargetQuery` uses `Physics2D.OverlapCircleNonAlloc`, applies the configured layer mask and maximum distance, resolves entities through `EntityRegistry`, and avoids recurring allocations in the simulation loop.

### 2.3 `InteractionResolver`

`InteractionResolver` contains the shared deterministic selection policy:

1. Exclude the interacting character.
2. Reject invalid or out-of-range distance values.
3. Resolve the target's `IInteractable` capability.
4. Check `CanInteract`.
5. Invoke `Interact` on the first valid candidate and stop immediately, guaranteeing a single interaction attempt.

`TrySelect` uses the same policy without executing `Interact`, allowing local predictive presentation to agree with authoritative selection.

### 2.4 Contracts

- `IInteractable` exposes `CanInteract(in InteractionRequest)` and `Interact(in InteractionRequest)`.
- `InteractionRequest` is immutable and contains `InteractorId`, `TargetId`, and `SimulationTick`.
- `InteractionResult` is immutable and contains success, consumption state, and `InteractionFailureReason`.

## 3. Local presentation and confirmed results

`LocalInteractionCandidateSource` runs `InteractionResolver.TrySelect` during `Render` only for the player with Input Authority. It exposes a read-only local candidate for the predictive prompt. This prompt does not guarantee acceptance and does not synchronize text or visual resources.

For a changed candidate, the source resolves the exact runner-local `NetworkObject` and reads optional `InteractionPromptMetadata` once. The cached local text remains until the target or resolved instance changes; missing metadata falls back to `Interactuar`. Candidate loss, disable, despawn or a runner/session change clears the cache. Metadata never enters `EntityRegistry`, gameplay contracts or network state.

Every interaction press processed by State Authority increments `InteractionSequence`, including disabled control, unavailable interactor, and missing-target failures. The result retains target, tick, success, consumption, and its typed failure reason.

State Authority sends each result through a reliable RPC directed to Input Authority. The RPC handler only queues the payload; `PlayerInteractionNetworkController.Render` publishes `InteractionResolved`. The presenter deduplicates by sequence, ignores initial replicated state, and resets when bound to a player object from a new session.

`LocalPlayerHudBinder` enables the prefab HUD only when `HasInputAuthority`. Proxies, animations, and views do not execute interactions or modify authoritative state.

## 4. Loot-container interaction adapter

`NetworkLootContainerInteractable` is a same-root adapter over `NetworkLootContainer`. Both must share exactly one `NetworkObject` and therefore one `EntityId`. The container owns loot-source and collider registration; the adapter independently registers only `IInteractable`, accepting either spawn order. Expected-instance unregistration means either despawn order preserves the other capability and an obsolete owner cannot remove a later instance.

The adapter accepts initialized and available containers even when empty. `Interact` runs only under State Authority, returns a successful non-consumed result, and never changes contents, availability, `LootChangeSequence` or object lifecycle. Invalid prefab composition is diagnosed once and disabled.

The defeated `NetworkPlayer` is one composition of this generic endpoint, not a separate interaction type. While the player is alive its co-located container is initialized but unavailable, so the adapter rejects it. After `PlayerCorpseGenerationController` completes the authoritative inventory handoff, the same adapter and `EntityId` become eligible. Emptying the container does not disable interaction, close presentation, or despawn the player.

The confirmed event is only an opening signal. `RaidInventoryPresenter` requires the exact adapter/container composition and registry mapping before entering loot mode; merely finding a container on another successful interactable is insufficient. It deduplicates sequences per bound player object and advances its baseline even for failures, while a failed result leaves the current mode unchanged.

When in loot mode, a subsequent press of the interaction input raises `PlayerInputReader.InteractPressedLocally`. `RaidInventoryPresenter` catches this local event to close the UI immediately without executing gameplay, sending RPCs, calling `Interact()`, or modifying container state. The closing press is consumed locally and suppressed from network transport.

## 5. Loot pickup integration

`NetworkLootPickup` implements `IInteractable` while preserving a strict reservation transaction:

1. `CanInteract` verifies that the pickup is not consumed.
2. `Interact` validates State Authority, the request, and availability.
3. It resolves `ILootReceiver` and builds the unified `LootTransferRequest`.
4. It reserves the pickup with `IsConsumed = true`.
5. It calls `ValidateReceive` without mutating the destination.
6. If prevalidation rejects, it restores `IsConsumed = false` and maps the precise loot reason to a general interaction result.
7. If prevalidation succeeds, it calls `CommitReceive` immediately without yielding State Authority or allowing an intervening mutation.
8. Only after the commit does it return `InteractionResult.Succeeded(isConsumed: true)` and despawn through `Runner.Despawn(Object)`.

The pickup is a consumable source with its own reservation. It does not implement extraction and does not coordinate transfers between two storage endpoints.

`LootTransferResult` preserves precise loot semantics. `InteractionResult` and `InteractionPresentationEvent` retain the nested `LootTransferFailureReason` when the general result is `LootRejected`, without expanding `InteractionFailureReason` with every transfer-specific case. This lets Input Authority distinguish `InventoryFull` from technical failures after authoritative confirmation.

For a full pickup, local presentation animates only the pickup's `WorldVisual` child through a short jump and complete rotation, then restores its exact local pose. The root `NetworkObject`, collider, identity and inventory remain untouched. The interaction sequence deduplicates the signal across proxies and resimulation. Contextual invalid/out-of-range rejections pulse the existing prompt; technical failures retain their diagnostic text.
