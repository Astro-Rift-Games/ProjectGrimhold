# Inventory World Drop Architecture

## Context and decision

The world-drop flow allows an Input Authority player to drop loot from the personal Raid
inventory. The local UI selects an occupied stack and requests an intention;
State Authority remains the only peer that resolves the current quantity,
removes inventory content and publishes a world pickup.

The personal inventory uses a contextual action menu. Container looting keeps
the existing transfer controls unchanged, and read-only state accepts no slot
action:

```text
Personal inventory: right click -> contextual actions
Open container:     left click -> transfer one, right click -> transfer all
Read only:          no slot interaction
```

## Contextual action providers

`RaidInventoryPresenter` owns the selected `LootId` while the menu is open. It
resolves the selected catalog definition and stack snapshot, then asks its local
`ILootContextActionProvider` instances to append applicable descriptors in
provider order. A descriptor contains a stable action identifier, visible text,
availability and its owning provider.

The view only renders descriptors and emits the selected identifier. It does
not execute gameplay. The presenter routes the identifier back to the owning
provider with the captured context. This keeps future consume or equip rules
outside the view and outside the drop controller.

The inventory registers one `LootDropContextActionProvider` with two actions, always
in this order for valid loot:

1. `Soltar`, mapped to `LootTransferQuantityMode.SingleUnit`.
2. `Soltar todo`, mapped to `LootTransferQuantityMode.FullStack`.

The menu closes after an action, on an outside left click, Escape, inventory
close, interaction-mode change, selected-stack disappearance, or a new drop
request. Merely moving the pointer outside the popup does not close it.

## Request and authority boundary

`PlayerLootDropNetworkController` is colocated with the player's
`PlayerLootReceiver` and runs the authoritative operation from
`FixedUpdateNetwork`. Its request transports only the catalog index, quantity
mode and sequence. State Authority resolves all mutable facts: player life,
current stack quantity, facing, world origin and valid placement.

Only one local request and one authority-side pending request are allowed at a
time. `LootDropRequestState` records the last processed identity and typed
confirmation. An exact duplicate replays that confirmation; a conflicting,
obsolete or concurrent identity is rejected without spawning or extracting.
Presentation callbacks are deferred to `Render`, so simulation and
resimulation do not directly drive UI side effects.

`Soltar` resolves to one unit when the stack still exists. `Soltar todo`
resolves to the complete authoritative amount observed during the processing
tick, not the quantity previously displayed by the client.

## Atomic extraction and provisional pickup

The authoritative transaction is deliberately ordered:

1. Validate authority, player availability, catalog identity and current stack.
2. Resolve quantity and a collision-free position.
3. Spawn an unpublished `NetworkLootPickup` with the intended content.
4. Verify its network identity, content and quantity.
5. Validate and jointly commit quantity plus Raid provenance through `PlayerLootReceiver`.
6. Publish the pickup.

An unpublished pickup disables renderers and colliders and does not register
for interaction. If placement, spawn, pickup validation or extraction fails,
the provisional object is despawned before any inventory mutation is exposed.
After publication the pickup uses the existing registration, interaction and
collection flow without a separate dropped-item type.

Existing pickup producers use the original initialization overload and start
published. Publication is replicated state because proxies must never display
or interact with a pickup before the extraction commit has succeeded.

## Placement source of truth

Placement starts 0.75 world units from the authoritative player origin. The
normalized authoritative `FacingDirection` rotates a stable candidate order:
front, front-left, front-right, left, right, back-left, back-right and back. An
invalid zero facing falls back to down.

Each candidate must pass a non-allocating overlap-circle check with radius 0.27
against the serialized `WorldCollision` mask. If every candidate is blocked,
the result is `NoValidPosition`; inventory content is unchanged and no pickup
remains.

## Sources of truth and lifecycle

- `PlayerLootReceiver` owns authoritative inventory content and quantity.
- The loot catalog owns static loot definitions and catalog indices.
- Player network state owns alive state and facing.
- `PlayerLootDropNetworkController` owns request sequencing and confirmation.
- `NetworkLootPickup.IsPublished` owns replicated pickup availability.
- `RaidInventoryPresenter` owns only local menu selection and feedback.

Closing the inventory never cancels a request already accepted by the network
controller. Reopening observes `HasRequestInFlight` and keeps slots/actions
disabled until a matching confirmation or transport rejection finalizes it.

## Validation strategy

Edit Mode tests cover candidate order and facing rotation, blocked-candidate
failure, quantity modes, provider filtering/order and idempotent request-state
rules. Play Mode presentation tests cover personal right-click intentions while
preserving container transfer clicks. Prefab import and Fusion baking validate
the new network behaviour registration and inherited melee/ranged variants.

Manual Host/Client validation remains required for replicated publication,
authoritative quantities under latency, competing pickup collection, wall and
corner placement, and closing the inventory while a request is in flight.

## Credited world drops

All content held by `PlayerLootReceiver` is already credited for first acquisition and therefore carries no first-acquisition-eligible quantity. Raid ownership provenance is preserved independently. Before spawning a drop, State Authority resolves the exact requested origin buckets in Dungeon-then-`RaidParticipantId` order. The provisional `NetworkLootPickup` stores those logical IDs and quantities directly, is initialized with eligible quantity zero, and remains unpublished until the source jointly commits quantity and provenance. Recollection copies the same logical origins into the destination Inventory.

Failed spawn, verification, extraction or publication despawns the provisional object, so quantity, ownership provenance and eligibility cannot be published inconsistently. World drops do not grant first-acquisition progress and do not reinterpret Player origins as Dungeon.
