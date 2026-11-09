# Raid Inventory UI Architecture

## Shared personal-inventory screen

The personal-inventory screen is one prefab asset shared by Town and Raid, with one independent
runtime instance under each local player presentation. `NetworkPlayer` and `SocialPlayer` never
share a runtime presenter, view, input-suppression token, source, or open/closed state. Destroying
or unbinding one context therefore cannot retain presentation state into the next runner.

`IInventoryReadSource` is the neutral read boundary used by the shared presenter. It exposes only
a `LootEntry` snapshot, slot capacity, a revision and a local change notification. It
does not expose mutations, transfer endpoints, Town/Raid knowledge, catalog metadata or economic
value. `LootInventoryValueCalculator` derives the displayed total from the snapshot and
`LootDefinitionCatalog` outside the source.

The bindings remain context-specific:

```text
Raid: PlayerLootReceiver.TryGetInventorySlot(0..29) + Revision -> RaidInventoryPresenter
      PlayerLootReceiver.TryGetLootContent -> aggregate value and legacy readers
Town: IPlayerLoadoutService -> LocalLoadoutInventoryReadSource -> RaidInventoryPresenter
      IPlayerLoadoutService -> TownEquipmentMutationEndpoint -> RaidInventoryPresenter
```

Raid retains its replicated `PlayerLootReceiver`, container flows, Equipment, context actions and
authoritative gameplay controllers. Town projects the confirmed application Loadout and the eight
assignments in `PreparedEquipmentLoadout`. Its binding enables the personal panel, Equipment,
Town-only Equip/Unequip intentions, Tab toggle, Escape close and local gameplay-input suppression.
Container, Take All, drop, consume and every other Raid mutation remain absent from the Town
capabilities and listeners.

`LocalLoadoutInventoryReadSource` implements both inventory and prepared-Equipment read contracts,
owns the single matching `ProfileCommitted` subscription, and triggers one rebuild of both panels
after a confirmed commit. `TownEquipmentMutationEndpoint` forwards intentions only through
`IPlayerLoadoutService`; it does not update either projection optimistically. Context actions name
the exact destination slot. Armor uses its fixed destination; weapons expose compatible Main Hand
and Off Hand destinations in Set A and Set B.

## Context and decision

Chest-only first-opening presentation reuses the replicated `FirstOpenResolved` flag.
After a confirmed local interaction, `RaidInventoryPresenter` binds the container and
suppresses gameplay input while its configured `ChestOpeningPresenter` finishes the
brief opening animation. It then displays the existing inventory screen, including
empty chests. Sound duration never gates the screen. Missing or unavailable chest
presentation falls back to immediate display; corpse endpoints remain immediate.
Close, local E/Escape, inventory toggle, defeat, disable/unbind, target loss and range
loss cancel the pending screen and release its input token without reverting the
authoritative chest opening. Restored/already-open snapshots show the final sprite
without replaying animation or sound. Confirmed results and replicated observation
converge on one local playback latch, so their arrival order cannot restart opening.
The chest presenter is a presentation-only `NetworkBehaviour` with no replicated
fields or RPCs. Its baked position follows the interactable, allowing `Spawned()`
to capture fresh/restored state before presentation observations. Missing/looping
audio configuration skips only sound, never the animation or inventory flow.

The Raid inventory UI replaces the provisional textual loot summary with a local uGUI slot screen and composes the player inventory with an inspected `NetworkLootContainer` in that screen. Right click requests a full-stack transfer in container mode; drag/drop also requests a full stack. Left click does not transfer. A local “Tomar todo” sequence reuses full-stack container withdrawals one at a time. A provider-driven contextual menu exposes the authoritative world-drop transaction documented in `Docs/Architecture/InventoryWorldDropArchitecture.md`. Each network endpoint remains the source of truth for its own snapshot and State Authority remains the only writer.

The personal Raid inventory projects all eight replicated Equipment slots: Main Hand and Off Hand
for Set A and Set B plus `Helmet`, `Armor`, `Gloves` and `Boots`. All eight views reuse the same serialized
slot prefab inside the existing screen and emit only a typed `EquipmentSlot` unequip intention. Only
the two Main Hand views carry an active state and participate in Set selection; Off Hand views derive
a non-interactable blocked state while their Set's Main Hand contains a two-handed weapon. Armor views
show nothing but empty/occupied, the piece's icon and metadata, and the unequip intention. They do
not form a separate Equipment screen or a second inventory. Quick Slot assignment supports the
Inventory drag documented below; this does not define an Equipment drag operation.

The eight Equipment views are **authored in the prefab and never created at runtime**.
`RaidInventoryView` exposes one named serialized field per slot (Set A/B Main Hand and Off Hand,
plus `_helmetView`, `_armorView`, `_glovesView`, `_bootsView`) and only binds and
drives them; it owns no root transform, no slot prefab and no layout maths. Named fields rather than
an array keep the Inspector mapping impossible to mis-order, and leaving placement entirely to the
authored hierarchy keeps the panel's design free to change without touching code. An unassigned view
is reported once and skipped rather than throwing. An authoritative unequip validates receiver
capacity before
moving one unit back to `PlayerLootReceiver`; the UI waits for the confirmed replicated revision and
never predicts either side of the transfer.

The local presentation flow is:

```text
PlayerLootReceiver
    -> thirty physical slots, LootChangeSequence and local metadata
    -> RaidInventoryPresenter
    -> RaidLootPanelPresenter.RefreshInventory / RaidInventoryView
    -> RaidLootPanelView / eight Equipment slot views
    -> RaidInventorySlotView
```

The presentation layer calls only read APIs, revision queries and local catalog projection. It never calls extractors, authoritative validators or commits. Personal right click opens the contextual menu; container-mode right click and drag/drop express full-stack intentions. Physical player-cell events carry the captured `RaidInventorySlotData`, not just `LootId`. Deposit, drop and equip forward its source address and observed revision unchanged to the existing addressed public controller overloads. State Authority resolves quantity and validates the complete operation. Container withdrawal, Take All, container equip and unequip keep their existing unaddressed routes. No destination cell address, editable amount or placement intention is supplied.

Every context request carries the exact originating slot `RectTransform`. The shared context menu
positions itself from that anchor in Canvas-local space, prefers the slot's right side, falls back
to its left side, and finally clamps its rendered bounds inside the Canvas. Inventory and Equipment
slots use the same rule in Town and Raid; mouse screen coordinates are not retained as layout state.

Weapon eligibility consumes the admitted `CharacterAttributeState` through the avatar's
`RaidAvatarParticipantLink`. Local `CanEquip` may suppress an impossible intention, but State
Authority reuses the same `WeaponAttributeRequirements` rule before extracting Inventory,
changing Equipment or configuring Combat. Prepared admission validates all weapon references
before mutating any Inventory or Equipment state. Selection and restoration preserve the single
invariant that an ineligible weapon never becomes active; they do not define a second eligibility
policy. Missing admitted attributes fail as an unavailable dependency, while insufficient values
return `AttributeRequirementsNotMet` through the existing Equipment feedback path. Armor remains
outside attribute requirements during the MVP.

## Slot projection and metadata

`RaidInventorySlotView` optionally binds authored charcoal empty, occupied, hover, selected,
invalid-drop and reserved sprites. The slot view, not Button SpriteSwap, owns this background.
Its existing selection/drop/reservation inputs determine the sprite; inactive-set alpha and the
unscaled confirmed-transfer pulse remain independent and restore the current semantic resting
appearance. Icons, tooltip data, gestures and gameplay capability rules are unchanged.
Without authored sprite references, existing color-only presentation is retained for callers and
test fixtures. The charcoal skin does not create new locked or pending gameplay states.

Equipment cells retain exactly one non-raycast `Icon` Image. Its optional serialized
`EquipmentSlotIconPresentation` swaps between the category sprite and actual item sprite;
no `EmptyCategoryGlyph` GameObject or second icon Image is used.
Every glyph is 16x16 source pixels, with the PNG and editable Aseprite importers configured at 16 PPU.
The six neutral pixel glyphs (main hand, off hand, helmet, armor, gloves and boots) identify only
genuinely empty equipment positions; A/B sets reuse their role glyphs. Main hand uses two crossed
swords based on the ArmingSword silhouette, and off hand uses a single shield. Boots use a paired
cuffed-boot silhouette with distinct toe boxes and soles. These are
category cues, not equipment-eligibility rules. Generic inventory and
Quick Slot presentation explicitly hides this layer. Occupied entries hide it even when item art
is missing, and two-handed off-hand reservation retains the existing weapon icon/alpha rather
than an empty-category indicator. Town character-sheet, preparation inspection and the three
member-card summary cells follow the same emptiness rule; portrait layers are unaffected.
Both empty and item styles (anchors, position, size, pivot and color) are authored with the
single Image. This preserves manually adjusted empty-category size/opacity independently
from item geometry and restores normal item alpha when equipping; reserved items retain
their existing reduced alpha. Generic inventory and Quick Slot paths clear the category
sprite rather than reusing it as an occupied fallback. Unconfigured views retain legacy behavior.
The single Icon and its category configuration are authored in source prefabs, not created or
discovered at runtime. The original
charcoal frame atlas, slot state sprites and equipment compatibility rules remain unchanged.

Raid player projection reads exactly addresses `0..29` through `TryGetInventorySlot`, independent
of the lower configurable occupancy limit. It emits thirty entries, including interior holes;
repeated `LootId`s remain separate even when their origins match. Each occupied entry retains its
physical quantity and exact `Dungeon` or `Player(RaidParticipantId)` origin. Origin is nullable
only for an empty or unaddressed presentation entry; the holder is never inferred as origin.

| Presentation value | Snapshot identity |
| --- | --- |
| Physical slot data, including holes | Source address and captured Inventory revision |
| Inventory context and drag payload | The same captured address/revision, copied unchanged |
| Legacy/default Town, container or Equipment values | Unaddressed `-1/-1` |

Revisions are opaque integers except reserved `-1`, matching `LootTransferRequest` validation.
`WithDisplayName` preserves all metadata. These disposable values never own inventory state.
Aggregate `TryGetLootContent` remains the read contract for total value, Quick Slot quantities,
backend/persistence and legacy feedback. It is not used to reconstruct Raid cell positions.
Town's `TownInventoryProjection` retains confirmed instances/attributes and projects each stored
`StashItem.Slot` to its exact Loadout cell, including interior holes. These Town addresses remain
separate from Raid address/revision/origin metadata. Out-of-range Town addresses make the panel
unavailable without changing stored data. Container panels preserve aggregate snapshot order and
retain `RaidInventoryProjection`, including capacity checks and appended empties.

Every occupied slot whose `LootDefinition` resolves carries an immutable equipment-tooltip
presentation built from definition-owned data. Weapons expose base damage/type, interval, range,
Stamina cost, handedness, requirements and the currently consumed legacy scaling; armor exposes
Physical Defense, Magical Defense and its direct maximum-resource bonus. The tooltip never combines
those values with character attributes, runtime statistics or equipped totals. Canonical legacy
coefficients are displayed with their matching grade, while any other valid coefficient remains a
numeric multiplier. A resolved non-equipment definition reports that it has no Equipment statistics;
an invalid weapon/armor configuration reports unavailable statistics without masquerading as a
non-equipment item. An unresolved catalog definition shows no tooltip and retains the existing slot
fallback behavior.

`RaidInventoryUI.prefab` and `StashInventory.prefab` each author exactly one non-raycast tooltip
view. Panel slots forward hover intentions through `RaidLootPanelView`; the direct Equipment slots
bind to the same screen-local view. The tooltip prefers the anchor's right side, falls back to the
left and clamps to the Canvas. Pointer exit, content replacement, screen close/disable and opening a
context menu clear it. A physical cell also dismisses its tooltip when its captured revision changes,
even if its `LootId` stays the same. This local presentation has no networking, mutation or simulation role and is
shared by Town/Raid Inventory, Equipment, Stash, Loadout and an open container panel.

The view binds an authored stable slot pool and never creates inventory cells at runtime. Normal content refreshes reuse those views. The shared personal Inventory prefab authors 30 slots, matching `LocalProfileSnapshot.MaxLoadoutSlots` in Town and the fixed physical storage length in Raid, even when Raid's occupancy limit is lower. Container projections request their source's gameplay capacity and hide the unused remainder of their authored pool. An insufficient authored pool remains an explicit error; the known six-view versus thirty-capacity container case is not repaired by player projection. A missing icon uses the serialized project placeholder. Legacy aggregate projections can degrade an unresolved definition to raw `LootId`, placeholder and quantity, reporting the integration error once per ID. A physical slot requires a catalog-index-to-ID mapping; an invalid physical snapshot makes that panel unavailable rather than inventing an item identity.

`PanelsRow` contains reusable sibling panels. Personal mode shows the player panel and the Equipment
panel appropriate to the bound context; Town Equipment is mutable only through its explicit local
capability. Loot mode makes occupied slots in both the player and container panels selectable at
full capacity, including empty slots and `Contenedor vacío`; empty content does not close the
screen. A container-slot right click withdraws to the player, while a physical player-slot right click
deposits into the open container. Drag/drop preserves those directions. While the Inventory is in
Transfer mode, cells whose definition has `CanBeTransferred == false` are dimmed but remain
draggable toward Equipment and Quick Slots. A deposit gesture with such an item sends no request and
shows `Este objeto no se puede transferir a contenedores`, the same text used when State Authority
returns `NotTransferable`. `FullStack` from a player
cell means only that selected physical slot's quantity, not the item's aggregate; from a container
it keeps the existing aggregate-stack meaning. Left click does not transfer. These gestures do not
define authoritative sorting, moving, splitting, swapping, multiple selection or destination-hover
placement. Quick Slot assignment remains definition-based as documented below.

The container panel's “Tomar todo” button is enabled only for a valid open container with at least one visible occupied stack and no active request or batch. `RaidLootTakeAllState` copies the visible `LootId` values in order and tracks only the current local request. The presenter disables both panels and the button until that request finalizes, refreshes the replicated projections, and continues after success or rejection. It retains the last rejection feedback during the batch. New stacks are not appended, quantities remain authoritative, and close or target loss cancels unsent identities without affecting completed transfers.

Each panel owns a `RaidLootSelectionState`. Raid player selection contains its `LootId`, physical address and captured revision, so only one duplicate cell is highlighted. A new revision, missing/changed source identity or empty selected address clears it without selecting another matching item. Legacy Town/container selection remains item-based and unaddressed. Selecting one panel clears the other; close, target change and rebind clear local interactions. Selection state has no pending flag or controller reference. Slot interactivity is derived each time from loot mode, a valid current container, an occupied slot, and `!PlayerLootTransferNetworkController.HasRequestInFlight`.

## Confirmed opening, refresh and close

`LootTransferFailureReason.PlayerUnavailable` maps to the visible `Jugador no disponible` feedback and follows the same authoritative confirmation and pending-release path as every other transfer rejection.

`RaidInventoryPresenter` remains the sole owner of mode, target, subscriptions, transfer intent, watchdog and the input-suppression token. It opens loot mode only for a strictly new successful `InteractionPresentationEvent` belonging to the bound Input Authority player. The sequence baseline is captured before subscription and replaced on enable or player-object rebind, so replicated initial state and an old session cannot reopen the UI.

Opening reconstructs the target `NetworkId`, resolves the exact instance through the bound runner, requires a same-root `NetworkLootContainer` and registered `NetworkLootContainerInteractable` sharing that `NetworkObject`, and requires initialized/available state. The presenter then caches object, components and colliders. The watchdog only rechecks that instance, state and distance through cached colliders; it performs no component or global searches per frame.

Player and container `LootChangeSequence` values are the definitive Raid refresh signals, including remote transfers. Before refreshing or dispatching an Inventory action, the presenter checks the captured source address, revision, item and quantity. A stale menu is dismissed and an active drag is canceled; hidden stale buttons cannot dispatch. Refresh never updates an existing action's revision or retargets by `LootId`, and rejection never triggers automatic retry. `PlayerLootReceiver.Render` publishes one local `IInventoryReadSource.Changed` notification when it observes a new player revision; it does not modify network state. `RequestInFlightChanged` only recalculates interactivity. `TransferConfirmed` always refreshes the player; it reconciles the current container and selections only when either endpoint matches the open container. Therefore a late confirmation from A cannot alter B.

Transfer feedback is local presentation state inside the inventory screen. An Input Authority peer considers a request in flight only when Fusion reports that the RPC was sent to State Authority; local invocation alone is sufficient only on the State Authority peer. The request RPC uses `RpcHostMode.SourceIsHostPlayer`, so a local Host invocation reports the Host player as `RpcInfo.Source` and passes the same Input Authority validation as a remote Client. A failed local send shows a generic request message, while an authoritative confirmation maps its typed `LootTransferFailureReason` to a player-facing reason. Authority-side transport rejections also finalize the matching request before publishing their local feedback, so an unavailable dependency or rejected envelope cannot leave slots permanently blocked. A confirmed withdrawal into physical player Inventory retains textual success feedback: a confirmation containing only `LootId` cannot identify a destination cell, so it never pulses an arbitrary first matching physical stack. Legacy container pulses remain unchanged. New requests, close, disable and unbind clear feedback; during take-all, later successes preserve the most recent rejection until the sequence finishes. Feedback never predicts or mutates content; replicated endpoint snapshots and their `LootChangeSequence` values remain the only sources of truth.

The request identity contains source, destination, catalog index, quantity mode and sequence, plus source Inventory address/revision for a player-cell action. State Authority accepts only open-container-to-owning-player withdrawals or owning-player-to-exact-open-container deposits. It resolves the exact `NetworkLootContainer` from the supplied `NetworkId`, verifies that the other endpoint is the controller's co-located `PlayerLootReceiver`, rechecks range and reads the source amount during the simulation tick. `SingleUnit` resolves to one when the validated source still contains loot; addressed `FullStack` resolves only the selected slot after revision and identity validation. Unaddressed container `FullStack` retains its aggregate meaning. Containers are not registered as generic receivers because defeated players colocate their inventory and container under one `NetworkId`; direct component resolution prevents a deposit from targeting the defeated player's inventory.

Close is idempotent, releases one suppression token, clears mode, target, colliders, selection and any unsent take-all identities, and never rolls back gameplay. Distance, target replacement/despawn, unavailable/uninitialized state, local close (via local Tab toggle, Escape, or a new local interaction press edge `InteractPressedLocally`), session end, player despawn and HUD disable all close the screen. Closing and reopening while a request remains in flight observes the controller directly and keeps slots blocked.

## Local input boundary

`PlayerInputReader` owns one `PlayerInputActions` instance with the normal `Gameplay` map and local-only `LocalUI.ToggleInventory` and `LocalUI.CloseInventory` actions bound to Tab and Escape respectively. Tab toggles the personal or container screen; Escape only requests a close, so it never opens a closed inventory. These intentions and the local `InteractPressedLocally` edge notification are not part of `PlayerNetworkInput` or `PlayerInputButton`.

Opening the screen acquires a small owner-specific suppression token. While any token exists, `ConsumeNetworkInput` returns `default(PlayerNetworkInput)` and discrete attack/interaction buffers are discarded. Gameplay and LocalUI action maps remain under their normal component lifecycle and are not toggled by suppression.

`RaidInventoryPresenter` also owns a reversible local mutation block derived from the local
participant's authoritative `Defeated` state. Entering `Defeated` cancels take-all, closes
personal/container UI and rejects open, transfer, take-all, drop and consumable intentions.
This starts on the Results presentation, before the player chooses spectator or Return;
spectator state is not an inventory source of truth. Unbind and raid cleanup clear the block.

When `RaidInventoryPresenter` is in container looting mode (`ScreenMode.ContainerLoot`), a new local interaction press (`InteractPressedLocally`) immediately calls `Close()`. `PlayerInputReader` evaluates `wasSuppressed` before publishing `InteractPressedLocally`, preventing the closing press from being added to pending network input even if suppression is released synchronously inside the callback.

On final suppression release (transition from 1 to 0 active tokens), movement and aim are read directly from current continuous controls. Any discrete action (attack or interaction) held at the moment of release sets a rearm requirement (`_interactRequiresRelease`). Physical release of the key clears the requirement regardless of suppression state, and only a subsequent physical press edge can be transported to Fusion. The same press that closes the container cannot reopen or execute a new interaction. Chests and defeated persistent players and enemies share this exact local presentation logic through their common `NetworkLootContainer` and `NetworkLootContainerInteractable` composition.

## Town hosted mode

In Town the personal-inventory screen is a tab of the Town player menu (`TownPlayerMenuArchitecture.md`).
`TownInventoryBinder` binds with `BindTown(..., externallyHosted: true)` when a menu is wired. In that
mode the presenter does not subscribe to Tab or Escape and does not acquire the input-suppression token;
the menu owns both and drives the screen through `ShowHosted` and `HideHosted`. Raid binding and
unwired Town binding are unchanged.

## Town gem installation (TASK-283 / F4)

Only Town bindings compose `TownGemInstallSession` and the gem `ILootContextActionProvider`.
Dragging a gem onto a weapon in Inventory (Loadout) or Stash opens the screen-local confirmation;
other targets keep their normal move/merge/swap route. Equipped weapons and all Raid bindings
are excluded. Right-clicking a gem offers **Incrustar**; choosing it highlights stored weapons
with at least one free socket, including targets on subsequent Stash pages. Picking a highlighted
weapon opens `¿Incrustar {gema} en {arma}? Es permanente.` Cancel, Escape or screen close abandons
an unsent intention without touching ownership. A gem dropped onto a weapon with no sockets or
with all sockets full opens no confirmation and briefly shows `Esta arma no tiene ranuras libres`; neither item
moves or changes.

`Assets/Prefabs/UI/GemInstallConfirmation.prefab` is authored once and nested independently in
`RaidInventoryUI.prefab` and `StashInventory.prefab`. Its gem-specific view references `Modal`,
`Modal/Message`, `Modal/Confirm` and `Modal/Cancel`. Opening selects Cancel. It borrowed the visual structure of the former recovery
consent modal (removed by TASK-278), not its coordinator or behavior. The existing screen feedback channels show selection,
pending, success and refusal messages; no cells or modal hierarchy are generated at runtime.

Confirmation captures both list/slot addresses, backend weapon instance ID, gem LootId, expected
revision and a new transaction ID. `StashTransferCoordinator.InstallGemAsync` shares the existing
application-owned exclusive queue and calls `InventoryClient.InstallGemAsync` through
`IRemoteGemInstallService` / `RemoteInventoryService`. No local `TryInstallGem` result is committed:
it is only an eligibility preview. After remote success, `LocalProfileStore.ApplyConfirmedGemInstall`
passes the returned Stash/Loadout/revision through the existing `ReconcileRemoteState` hydration
transaction, preserving unrelated aggregate fields and publishing one confirmed commit. Inventory,
weapon instance and tooltip refresh from that commit. A backend rejection leaves the local lists
and revision untouched. An unknown remote result or a failed local apply follows existing queue
reconciliation, never an automatic mutation retry.

Selection/confirmation blocks other item intentions. Pending installation blocks item inputs across
both screens and close/reopen through the shared coordinator; disposal only detaches presentation,
never cancels an already-sent irreversible request. A profile commit invalidates any unsent selection
or confirmation. Installation requires schema 3, hydrated equipment, no pending Raid reservation and
the captured revision; the existing move/order/transfer queue supports both schema 2 and schema 3.

| Refusal | Spanish screen feedback |
| --- | --- |
| `INSTANCE_MISMATCH` | El arma ya no está disponible |
| `GEM_NOT_AVAILABLE` | La gema ya no está disponible |
| `GEM_NO_SOCKETS`, `GEM_NO_EMPTY_SOCKET` | Esta arma no tiene ranuras libres |
| `REVISION_CONFLICT` | El inventario cambió. Vuelve a intentarlo |
| `RESERVATION_PENDING` | No se puede modificar el inventario mientras hay una reserva de Raid pendiente |
| `INVENTORY_MIGRATION_REQUIRED` / hydration guard | El inventario debe sincronizarse antes de incrustar gemas |
| Other / unavailable endpoint | No se pudo incrustar la gema |

EditMode session/request tests cover exact addresses, confirmation, free/full/absent sockets,
remote-first application, rejection invariance, shared pending locks, authoritative tooltip data
and preservation of unrelated aggregate fields. Coordinator cases preserve schema-2 behavior while
enabling schema-3 move, swap, order and transfer. PlayMode exercises the authored dialogs, drag,
context selection/cancel, Stash page addresses and Town personal-inventory lifecycle alongside the
existing Raid and inventory interaction suites. Visual scaling, actual pointer raycasts, keyboard/
controller navigation and an authenticated backend session still require manual checks.

## Runner-scoped binding and lifecycle

`LocalInputContext` is a local-only component created on the runner. It stores at most one active `PlayerInputReader`, notifies changes, and clears on shutdown. It contains no networked state, inventory knowledge, or general service registry. `FusionInputProvider` registers its serialized reader through the runner reference obtained by its existing lookup flow. Replacing that lookup is separate technical debt.

`LocalPlayerHudBinder` binds only the Input Authority player's receiver and the reader exposed by its runner context. The inventory presenter remains outside the visual screen root.

`TownInventoryBinder` is the equivalent local lifecycle owner on `SocialPlayer`. It binds only
after a valid Input Authority spawn, requires the runner join profile to match the available
`ApplicationStashContext`, observes `LocalInputContext.ReaderChanged`, creates one
`LocalLoadoutInventoryReadSource`, and symmetrically unbinds and disposes it on reader replacement,
disable, despawn or destruction. The source itself is the sole subscriber to matching
`ProfileCommitted` events; the binder never observes profile commits or refreshes the view.
The binder also creates the Town mutation endpoint and injects a narrow readiness query.
When the replicated local preparation reports `Ready`, the endpoint rejects Equip/Unequip before
calling `IPlayerLoadoutService`; Equipment remains visible. Returning to `Not Ready` restores the
capability. This presentation-composition gate is temporary and does not add Town networking
knowledge to `IPlayerLoadoutService` or `LocalProfileStore`.

- `Close` hides the screen and releases suppression while retaining binding and slot pools.
- `OnDisable` closes and unsubscribes but retains bound dependencies for a safe re-enable.
- `OnEnable` establishes a fresh interaction-sequence baseline, resubscribes and rebuilds from current snapshots without replaying old results.
- `Unbind` removes listeners before releasing suppression and clearing references, sequence state, target, selection, diagnostics and visual content.
- `OnDestroy` performs the same cleanup idempotently.

Player despawn, runner shutdown, scene unload, or reader replacement therefore cannot leave local gameplay input suppressed. A later session creates a new runner context and receives a fresh player inventory.

## Stash screen composition

The Town and Lobby Stash screen (`Assets/Prefabs/UI/StashInventory.prefab`) reuses these same
views instead of owning a parallel inventory implementation. `LobbyStashUI` drives two
`RaidLootPanelView` panels — the stash and the loadout — plus the eight Equipment
`RaidInventorySlotView`s of its eight-slot equipment panel, and it creates nothing at runtime. The panels,
their slot pools, the equipment panel, the screen's `TownStashView` and its Canvas sorting are
authored in the prefab itself.

Both screens were seeded once by a temporary Editor tool that has been removed. The prefabs are
now the only source of truth for layout and are maintained by hand in the Inspector, so a
regenerating tool can no longer overwrite manual layout work or renumber the authored slots.
Adding a slot to a pool means adding the slot instance in the prefab and appending it to the
panel's `_authoredSlots`; the code only shows, hides and drives what the prefab already contains.

`ApplicationStashContext` owns the profile services and the single `StashTransferCoordinator` for
its bound profile/service instances. `LobbyStashPresenter` borrows it through `GetTransferCoordinator`;
`TownInventoryBinder` injects that same coordinator into the personal inventory presenter. Transfers,
Take All / Leave All, equip, unequip and Town Sort share its serialized queue across screen close/reopen
and simultaneous screens. The context rebuilds only when the profile or a service instance changes.
Catalog metadata and the caller's Equipment capability are supplied per operation, so changing a
screen catalog cannot create a second queue and the context retains no UI dependencies. A transfer is first checked with the Store's dry-run
evaluators (`IPlayerLoadoutService.EvaluateTransfer*`), so one the Store would refuse, such as a move
into a full Inventory (`StashOperationResult.InventoryFull`), never reaches the backend; it then keeps
the remote-first order and reconciles the profile with the backend when a remote call fails or the
local profile cannot apply a transfer the backend accepted. Take All / Leave All are not one indivisible
batch (GD 07, section 16) but are one request and at most one write: the coordinator issues a single
`POST /character/me/inventory/transfer-all` (`IRemoteBulkTransferService`) and the backend applies the
single-transfer rules to every source stack in ascending physical-slot order, including duplicate IDs
and stacks outside the visible pool. A refused stack is skipped and later stacks are still evaluated;
the accepted ones persist in one revision-guarded write that a stale revision, a pending reservation
or a missing character rejects entirely. On success the local profile takes the returned `stash`,
`loadout` and `revision` in one commit (`LocalProfileStore.ApplyConfirmedTransferAll`), so the screen
refreshes once; when nothing moved nothing is written or committed. Bulk summaries count the per-stack
results: `Completed` are moved stacks and `Failed` are refused ones, returning `Success`, `Partial`
(some moved, some refused) or `Rejected` (none moved, with the first refusal's rule). A remote error or
undetermined result leaves the local profile unchanged and reconciles once without retry, returning
`RemoteFailed`; failed reconciliation or an unappliable backend result returns `RecoveryFailed` /
`Diverged`, preserving the original remote code/message or local rule separately from
`RECONCILIATION_FAILED`, with every source stack counted in `Failed`. Equipment changes use the same Ready gate
as the Town Inventory screen (`TownEquipmentMutationGate`, supplied by `TownStashPresenter` through
`LobbyStashPresenter.SetEquipmentGate`). Stash alone paginates over its 16 authored slots;
no slots are instantiated. Serialized previous/next buttons and a TMP page label belong to the
actual Stash prefab. D1 physical-slot persistence supersedes the TASK-218 compact exception:
`LobbyStashPresenter.MapToPresentation` indexes entries by `StashItem.Slot` and emits empty cells
for holes, never a compact occupied-item projection. Both panels retain duplicate `lootId` stacks.
Each panel's Sort button changes only its own persistent list through the transaction below;
there is no automatic Sort on open or refresh. The personal Town Inventory also exposes Sort for
that same Loadout list. `RaidInventorySlotData.WithDisplayName` preserves metadata for blank-name
fallback; Town cell addresses are tracked separately from Raid address/revision/origin metadata.

Stash pages cover physical ranges `0..15`, `16..31`, etc., not batches of occupied entries.
The extent is `max(configured capacity, highest occupied slot + 1)`, so holes and accepted overflow
remain reachable. The last page hides authored cells beyond that extent rather than inventing
capacity. Occupied cells at `slot >= capacity` use the invalid-drop highlight as an overflow marker;
this is presentation, not a capacity exemption. Refresh preserves/clamps the page; reopen resets to
page one. Page rebind/refresh dismisses tooltip/context and cancels drag. Context transfers capture
`(sourceSlot, lootId)`; cell-to-cell drag/drop captures both list/slot addresses and calls
`StashTransferCoordinator.MoveAsync` for move, total merge or swap, including within a list.
Empty targets keep their real addresses. Town split UI is the Stash screen's "Dividir" action ([Loot Interaction](LootInteractionArchitecture.md#physical-stack-split-task-281)).
In Raid, "Soltar cantidad..." and "Dividir" open the shared quantity dialog with range
`[1, selected slot quantity - 1]`. Confirming sends an addressed, revision-bound intention;
"Dividir" chooses the lowest empty physical slot and is disabled without spare occupied-slot
capacity or an empty slot. State Authority validates and commits, then confirmation refreshes
the panel with operation-specific feedback. Closing, Downed or a changed revision cancels the dialog.
Take All / Leave All still traverse the complete source inside the shared queue, never just a page.
Capacity comes from the context's Store, not a global lookup.

Owner decision: Town Inventory (Loadout) has **exactly 30 slots (`0..29`), all visible, with no
pagination and no prefab change**. Normal operations must never exceed those 30 slots. Loadout
addresses >= 30 are legacy over-capacity data, not additional gameplay slots or a UI requirement.
All accounts are test accounts: after the migration dry run, correct affected test data by moving
excess to Stash or resetting the character, not by adding overflow UI. The existing view reports
out-of-range physical extent without discarding stored entries; this defensive behavior is not an
overflow-management flow. Stash keeps its existing slot-range paging described above.

The Stash equipment panel authors the same eight slots as the Raid screen and drives them with the
same `RaidInventorySlotView.PresentEquipmentSlot`. Both panels are `TransferWithContextMenu`, so a
right click on an owned stack opens the contextual menu. Transfers preserve the captured source
slot. Equip still emits LootId, EquipmentSlot and the Stash-source flag rather than a selected stack
address; the coordinator resolves the lowest-slot matching source. `LobbyStashPresenter` preserves
that list source into the coordinator. Stash-only units can be equipped directly: the Store projects
final ownership without mutation, the coordinator moves one unit remotely then submits projected eight-slot Equipment,
and only after both successes applies one atomic local Stash-to-Equipment transaction. Displaced
units return to Loadout, including both hands and same-ID replacements. Capacity is checked against
the final occupied-slot Loadout, not the intermediate remote move. The remote pair is nontransactional:
failure or an unknown result reconciles once and stops without compensation/retry; the first move
may remain committed. Copied preflight Stash/Loadout/Equipment ownership is checked after each
successful await; drift reconciles once and stops before the next endpoint or local equip. Metadata
revision changes are allowed: production responses each save/publish `SetRemoteRevision`, separate
from the one final ownership transaction (the fake does not do this by default). The final ownership
guard and Store apply have no intervening await. Local apply failure after remote success reports
divergence or failed recovery.
Loadout-source Equip retains its existing local-first path. The menu
offers only the slots `EquipmentSlotRules` reports as compatible with that unit's category. The
view resolves the category from the `RaidInventorySlotData` already projected into the panels, so
it needs no catalog of its own. A left click on an occupied Equipment slot emits the release
intention for that exact slot. The view emits typed equip intentions with their source;
`LobbyStashPresenter` forwards them to `IPlayerLoadoutService`, which owns every rule about
ownership, compatibility and the exclusive Inventory/Stash-to-Equipment moves described in
`Docs/Architecture/LocalPlayerPersistenceArchitecture.md`.
`TownStashPresenter` instantiates the authored prefab, resolves its `TownStashView` and only shows
or hides it; it never adds components or configures the Canvas at runtime.

## Player-controlled order and Sort (TASK-218)

D1 replaces TASK-218's compact one-entry-per-ID representation, retaining its explicit-only Sort
policy. Stash and Town Loadout persist physical slots with holes and duplicate IDs. Removal leaves
a hole; reception fills the lowest matching in-capacity stack or lowest free in-capacity slot under
the [persistence rules](LocalPlayerPersistenceArchitecture.md#stash-capacity-admission). Addressed
cell moves/merges/swaps may change positions without compacting other entries. Only Sort compacts.
TASK-227/#613 hides and disables Raid Sort entirely; Raid addresses and holes remain in physical order.
`StashDisplayOrder` is the shared Sort comparer, not a display projection: category rank Valuable, Material, Quest, Miscellaneous, Weapon,
Helmet, Armor, Gloves, Boots, Shield, then None/unresolved/unrecognized at one last rank; display
name ordinal-ignore-case next, then raw `LootId` ordinal and original slot as the final tie-breaker.
Blank names fall back to `LootId`; missing icons do not alter a resolved category. Sort preserves
identity and amount, never merges stacks and never reorders fixed Equipment slots. Town entries
carry no Origin. The slot permutation assigns selected entries to contiguous slots `0..n-1`.

Town Sort persists through the Store, cache and backend, using the order
transaction and authenticated backend write in `LocalPlayerPersistenceArchitecture.md`, through the
same application-owned `StashTransferCoordinator` serialized queue as transfers. The Town binder
injects it with a reservation-only Sort capability (Ready remains allowed); the shared view exposes
one optional `_sortButton` and `SortRequested`. Backend write precedes local apply;
remote failure or local divergence reconciles once and stops without retry. The v2 DTO is
`{ list, slotOrder, expectedRevision }`, an exact permutation of occupied source slots, not LootIds.
Backend Sort refuses occupied count above capacity rather than compacting into overflow. A pending
Loadout reservation rejects Sort with `ReservationPending`; Ready does not block it because ownership
is unchanged. The Unity coordinator makes mutations unavailable on v1 inventories until migration;
Stash and Loadout buttons affect only their own lists.

Raid no longer seeds, reconciles or sorts a personal `LootId` display permutation. Its player panel
reads the receiver's thirty physical slots directly, preserving duplicate IDs, origins and holes.
The shared Sort control is hidden (inactive) and disabled for every Raid mode, including after Downed recovery. There is
no new sorting RPC or authoritative rearrangement. Town's comparer, persistent Sort queue and Ready/
reservation gates remain unchanged. Container panels have no Sort button. Admission/extraction
stay aggregate at the Raid boundary; extraction placement uses fresh physical Town Loadout slots,
not Raid positions or an append to a compact Town list.

## Equipment presentation and Downed read-only (TASK-218)

Implemented (TASK-218; Host/Client validation pending): the shared Equipment slot view in Raid, Town and Stash presents the Off Hand
of a Set with a two-handed Main Hand as reserved by that same weapon: a dimmed weapon icon and
`reserved by <name>` label, never a second copy. It has no tooltip, context action, drag source or
drop target. Local `DropPolicy` refuses drops there; State Authority continues rejecting them with
`IncompatibleHandConfiguration`. Both slots of the active Weapon Set are highlighted and both
slots of the inactive Set are dimmed, superseding the Main-Hand-only active visual described above.
Code drives existing slot visuals and refreshes through the existing `EquipmentRevision` observation
on Set swap; no prefab rework or #335 work is included.

Implemented (TASK-218; Host/Client validation pending): `RaidInventoryPresenter` derives a reversible local read-only block from the
local avatar's `PlayerDownedGate.IsDowned`, distinct from the participant's `Defeated` mutation
block. Personal or container screens may stay open and visible while Downed, but entering Downed
cancels an active drag and rejects drag, drop, context menu, unequip, transfer, take-all, world drop,
Quick Slot assignment and Sort intentions. A visible reason explains that inventory is read-only
while Downed; Spanish UI wording is an implementation choice. Leaving Downed restores interactivity
without reopening. Raid binding caches the avatar's `PlayerCharacter` and compares the Downed
predicate each frame, like Equipment revision observation. A private predicate seam supports
non-networked presentation tests. Close clears local read-only presentation; reopening observes
Downed again. The existing feedback channel keeps the persistent reason ahead of late transfer
feedback until recovery or close. This is UX only: authoritative gates in `DownedAndReviveArchitecture.md` section 9
remain unchanged.

## Validation strategy

Physical-slot UI (TASK-227/#613) has parent-observed Unity compilation and focused automated passes:

| Check | Observed result |
| --- | --- |
| Fresh compilation | 0 C# errors |
| EditMode: `RaidInventorySlotDataTests`, `RaidLootSelectionStateTests` | 19 passed, 0 failed, 0 skipped; job `92d06125b0fc45ccbdecf043a3082849` |
| Full UI PlayMode: `RaidInventoryPresenterInteractionTests`, `RaidInventoryViewPlayModeTests` | 79 passed, 0 failed, 0 skipped in 13.8495613 seconds; terminal job `4a255f03db3c4fefb3c42dd57c9565c1` |

The Console had 0 errors and 0 warnings after the full UI run. Coverage includes thirty addresses,
holes and exact origins; duplicate-stack selection; immutable/default metadata; selected-slot
deposit/drop/equip metadata; stale context/drag dispatch before refresh and cancellation after
refresh; hidden pooled-button rejection; tooltip revision changes; Raid Sort hidden with Town
persistent Sort retained; ID-only physical-cell pulse suppression with legacy container feedback;
and definition-only PlayerFirst consumption. Repeated bind/disable/enable is exercised with a real
button confirmation; one confirmation does not establish an exact delegate invocation count.
Four earlier failures were resolved by correcting the fixtures' active-Canvas lifecycle, not by a
production fix. The initial missing-metadata compile failure and an earlier initialization failure
that ran zero tests are not behavioral RED evidence.

These results verify the bounded UI unit, not full TASK-227 closure. Manual visual checks,
multi-peer RPC behavior and real Host Migration with a Host plus two Clients remain unexecuted.
Broader suites are not all green: outstanding failures concern damage fixture configuration,
endpoint word budget, the six-authored-view/thirty-required container pool, a runnerless ritual
authority fixture and an old Take All test with incompatible stack expectations. Strict container
shortage logs remain unsuppressed. Previously recorded core/receiver/gear passes are separate
evidence, not new UI verification. Exact-position persistence was outside that Raid UI work unit;
Town persistence is now implemented under D1 as described above, not proven by those earlier runs.

TASK-218's explicit-Sort policy is retained under D1: current pure tests cover slot permutations,
amount invariance, duplicate stacks, holes and physical-slot receive/remove behavior. PlayMode tests
cover the panel-specific Sort buttons, reserved Off Hand behavior, both-slot active/inactive Set
visuals and the reversible Downed block.
Manual Host/Client checks cover Downed entry/recovery and Set swap on a remote client.

Pure tests cover projection order/capacity, slot fallback and tooltip data, selection reconciliation, quantity resolution, bidirectional request identity and registry composition. Input tests cover continuous restoration, discrete rearming, nested suppression and the local toggle. Play Mode view tests cover both mouse buttons, both panels, stable slot reuse, clearing, empty capacity, tooltip hover/clamping/lifecycle and direction-aware transfer feedback. Focused Single Runner tests activate occupied slots in both panels for generated chests, defeated enemies and defeated players; they also verify in-flight click blocking, authoritative capacity feedback and that a defeated-player deposit reaches its container rather than its co-located inventory. Exact Host/Client interaction confirmation, replication races, competing clients, distance, despawn and local-HUD isolation remain manual multiplayer validation because the project has no automated multi-runner harness.

The defeated-player integration reuses these contracts without adding a new
screen mode or target type. Focused Single Runner tests use the co-located
`NetworkLootContainer` and `NetworkLootContainerInteractable` from the real
player prefab to cover confirmed `ContainerLoot` opening, single-unit and full-stack transfer,
both change-sequence refreshes, the existing empty-container presentation,
distance close, despawn cleanup and shutdown suppression release.

Single Runner coverage does not prove that two peers observed the same
replicated snapshot. Host/Client checks remain manual for real client
interaction, remote refresh, competing clients, disconnect cleanup, session
restart and the defeated body's final visual transition.

## Quick Slots (TASK-214)

The shared personal-inventory screen carries four authored Quick Slot views (`QuickSlot1..4` under `EquipmentPanel` in `RaidInventoryUI.prefab`, assigned to `RaidInventoryView`). They bind a definition, never a stack: dropping an Inventory slot on one (`DragSlotLocation.QuickSlot`, only from `Inventory`) or choosing "Asignar a Quick Slot N" from the context menu (`LootQuickSlotContextActionProvider`, only for Quick Slot compatible definitions) calls `QuickSlotController.TryAssign`, which moves an existing binding of the same definition to the new slot. A right click on an occupied slot unassigns it. A bound definition with no available units stays visible with a zero amount. Both Raid and Town bind a `QuickSlotController` for the inventory they show, and assignment is available in Town even while Ready, because it changes no equipment or items; it is blocked for a defeated participant (`SetGameplayMutationsBlocked`) and, Implemented (TASK-218; Host/Client validation pending),
while Downed through the separate reversible read-only block. The slot layout is provisional until the UI assets of #335 land.
