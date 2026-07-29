# Raid Inventory UI Architecture

## Context and decision

TASK-32 replaces the provisional textual loot summary with a local uGUI slot screen. TASK-34 composes the player inventory and an inspected `NetworkLootContainer` in the same local screen. Each network endpoint remains the source of truth for its own snapshot and State Authority remains the only writer.

The local presentation flow is:

```text
PlayerLootReceiver
    -> snapshot, capacity, LootChangeSequence and local metadata
    -> RaidInventoryPresenter
    -> RaidInventoryProjection
    -> RaidLootPanelPresenter / RaidInventoryView
    -> RaidLootPanelView
    -> RaidInventorySlotView
```

The presentation layer calls only snapshot readers, capacities, change sequences and local catalog projection. It never accesses extractors, validators, commits or network dictionaries. A real uGUI button click makes the slot emit only its occupied `LootId`; the orchestrator supplies the player and open-container endpoint identities to `PlayerLootTransferNetworkController`, which requests the source's complete authoritative stack.

## Slot projection and metadata

`PlayerLootReceiver.TryGetLootContent` already emits catalog-index order. `RaidInventoryProjection` preserves that order, rejects content beyond gameplay capacity, and appends empty entries until the projection length equals `SlotCapacity`.

The view creates a stable slot pool when binding or capacity changes. Normal content refreshes reuse those views. A missing icon uses the serialized project placeholder. If a complete definition cannot be resolved, only that slot degrades to the placeholder, raw `LootId` text, and replicated quantity; the presenter reports the integration error once per ID and keeps other slots visible.

`PanelsRow` contains reusable sibling panels. Personal mode shows only the read-only player panel. Loot mode makes occupied slots in both the player and container panels selectable at full capacity, including empty slots and `Contenedor vacío`; empty content does not close the screen. A container-slot click withdraws to the player, while a player-slot click deposits into the open container. There is no drag and drop, editable amount, multiple selection or loot-all.

Each panel owns a `RaidLootSelectionState` containing only its selected `LootId`. Selecting one panel clears the other panel's selection. Both clear on close or target change, preserve a selection while that stack remains in the corresponding snapshot, and remove it when the stack disappears. Selection state intentionally has no pending flag or controller reference. Slot interactivity is derived each time from loot mode, a valid current container, an occupied slot, and `!PlayerLootTransferNetworkController.HasRequestInFlight`.

## Confirmed opening, refresh and close

`RaidInventoryPresenter` remains the sole owner of mode, target, subscriptions, transfer intent, watchdog and the input-suppression token. It opens loot mode only for a strictly new successful `InteractionPresentationEvent` belonging to the bound Input Authority player. The sequence baseline is captured before subscription and replaced on enable or player-object rebind, so replicated initial state and an old session cannot reopen the UI.

Opening reconstructs the target `NetworkId`, resolves the exact instance through the bound runner, requires a same-root `NetworkLootContainer` and registered `NetworkLootContainerInteractable` sharing that `NetworkObject`, and requires initialized/available state. The presenter then caches object, components and colliders. The watchdog only rechecks that instance, state and distance through cached colliders; it performs no component or global searches per frame.

Player and container `LootChangeSequence` values are the definitive refresh signals, including remote transfers. `RequestInFlightChanged` only recalculates interactivity. `TransferConfirmed` always refreshes the player; it reconciles the current container and selections only when either endpoint matches the open container. Therefore a late confirmation from A cannot alter B.

Transfer feedback is local presentation state inside the inventory screen. An Input Authority peer considers a request in flight only when Fusion reports that the RPC was sent to State Authority; local invocation alone is sufficient only on the State Authority peer. The request RPC uses `RpcHostMode.SourceIsHostPlayer`, so a local Host invocation reports the Host player as `RpcInfo.Source` and passes the same Input Authority validation as a remote Client. A failed local send shows a generic request message, while an authoritative confirmation maps its typed `LootTransferFailureReason` to a player-facing reason. Authority-side transport rejections also finalize the matching request before publishing their local feedback, so an unavailable dependency or rejected envelope cannot leave slots permanently blocked. Success, a new request, close, disable and unbind clear that message. Feedback never predicts or mutates content; replicated endpoint snapshots and their `LootChangeSequence` values remain the only sources of truth.

The request identity contains source, destination, catalog index and sequence. State Authority accepts only open-container-to-owning-player withdrawals or owning-player-to-exact-open-container deposits. It resolves the exact `NetworkLootContainer` from the supplied `NetworkId`, verifies that the other endpoint is the controller's co-located `PlayerLootReceiver`, rechecks range and reads the complete source amount during the simulation tick. Containers are not registered as generic receivers because defeated players colocate their inventory and container under one `NetworkId`; direct component resolution prevents a deposit from targeting the defeated player's inventory.

Close is idempotent, releases one suppression token, clears mode, target, colliders and selection, and never cancels gameplay. Distance, target replacement/despawn, unavailable/uninitialized state, local close (via local Tab toggle, Escape, or a new local interaction press edge `InteractPressedLocally`), session end, player despawn and HUD disable all close the screen. Closing and reopening while a request remains in flight observes the controller directly and keeps slots blocked.

## Local input boundary

`PlayerInputReader` owns one `PlayerInputActions` instance with the normal `Gameplay` map and local-only `LocalUI.ToggleInventory` and `LocalUI.CloseInventory` actions bound to Tab and Escape respectively. Tab toggles the personal or container screen; Escape only requests a close, so it never opens a closed inventory. These intentions and the local `InteractPressedLocally` edge notification are not part of `PlayerNetworkInput` or `PlayerInputButton`.

Opening the screen acquires a small owner-specific suppression token. While any token exists, `ConsumeNetworkInput` returns `default(PlayerNetworkInput)` and discrete attack/interaction buffers are discarded. Gameplay and LocalUI action maps remain under their normal component lifecycle and are not toggled by suppression.

When `RaidInventoryPresenter` is in container looting mode (`ScreenMode.ContainerLoot`), a new local interaction press (`InteractPressedLocally`) immediately calls `Close()`. `PlayerInputReader` evaluates `wasSuppressed` before publishing `InteractPressedLocally`, preventing the closing press from being added to pending network input even if suppression is released synchronously inside the callback.

On final suppression release (transition from 1 to 0 active tokens), movement and aim are read directly from current continuous controls. Any discrete action (attack or interaction) held at the moment of release sets a rearm requirement (`_interactRequiresRelease`). Physical release of the key clears the requirement regardless of suppression state, and only a subsequent physical press edge can be transported to Fusion. The same press that closes the container cannot reopen or execute a new interaction. Chests and defeated persistent players and enemies share this exact local presentation logic through their common `NetworkLootContainer` and `NetworkLootContainerInteractable` composition.

## Runner-scoped binding and lifecycle

`LocalInputContext` is a local-only component created on the runner. It stores at most one active `PlayerInputReader`, notifies changes, and clears on shutdown. It contains no networked state, inventory knowledge, or general service registry. `FusionInputProvider` registers its serialized reader through the runner reference obtained by its existing lookup flow. Replacing that lookup is separate technical debt.

`LocalPlayerHudBinder` binds only the Input Authority player's receiver and the reader exposed by its runner context. The inventory presenter remains outside the visual screen root.

- `Close` hides the screen and releases suppression while retaining binding and slot pools.
- `OnDisable` closes and unsubscribes but retains bound dependencies for a safe re-enable.
- `OnEnable` establishes a fresh interaction-sequence baseline, resubscribes and rebuilds from current snapshots without replaying old results.
- `Unbind` removes listeners before releasing suppression and clearing references, sequence state, target, selection, diagnostics and visual content.
- `OnDestroy` performs the same cleanup idempotently.

Player despawn, runner shutdown, scene unload, or reader replacement therefore cannot leave local gameplay input suppressed. A later session creates a new runner context and receives a fresh player inventory.

## Validation strategy

Pure tests cover projection order/capacity, slot fallback data, selection reconciliation, bidirectional request identity and registry composition. Input tests cover continuous restoration, discrete rearming, nested suppression and the local toggle. Play Mode view tests cover both panels, stable slot reuse, clearing, empty capacity and direction-aware transfer feedback. Focused Single Runner tests activate the occupied slot's real `Button` in both panels for generated chests, defeated enemies and defeated players; they also verify in-flight click blocking, authoritative capacity feedback and that a defeated-player deposit reaches its container rather than its co-located inventory. Exact Host/Client interaction confirmation, replication races, competing clients, distance, despawn and local-HUD isolation remain manual multiplayer validation because the project has no automated multi-runner harness.

The defeated-player integration reuses these contracts without adding a new
screen mode or target type. Focused Single Runner tests use the co-located
`NetworkLootContainer` and `NetworkLootContainerInteractable` from the real
player prefab to cover confirmed `ContainerLoot` opening, full-stack transfer,
both change-sequence refreshes, the existing empty-container presentation,
distance close, despawn cleanup and shutdown suppression release.

Single Runner coverage does not prove that two peers observed the same
replicated snapshot. Host/Client checks remain manual for real client
interaction, remote refresh, competing clients, disconnect cleanup, session
restart and the defeated body's final visual transition.
