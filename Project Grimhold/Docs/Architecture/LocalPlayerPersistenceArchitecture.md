# Local Player Persistence Architecture

## Context

Project Grimhold currently keeps the character aggregate alive across scenes and
`NetworkRunner` replacement during one application process. The backend owns character identity
and is the target source of truth for durable metagame state. The Unity file cache must not stand
in for backend authority; cache initialization and save failures can stop profile hydration after
authentication.

## Decision

The application owns one process-local profile aggregate identified by the backend `CharacterId`,
represented locally as `ProfileId`. `LocalProfileProvider` remains invalid before authentication
and never generates or restores a productive identity through `PlayerPrefs`. A successful login
injects the remote `CharacterId` before initializing profile services.

`ApplicationStashServiceBootstrapper` creates one `ApplicationStashContext` before the first scene
but defers store initialization until a valid remote identity is available. It composes a
`LocalProfileRepository` backed by `LocalProfileFileStore`, using the per-ProfileId JSON cache.
The context remains `DontDestroyOnLoad`; the aggregate survives scene and runner transitions.
Login fetches backend inventory and progression with consistent revisions, hydrates an isolated
candidate and saves it before publishing application services. The file cache preserves local-only
and unsent fields across restarts; it is not a backend acknowledgement.

`CharacterAttributeState` is part of that same character aggregate. New profiles initialize it
from `ProgressionBalanceDefaults.InitialCharacterAttributeState`; assigned values and available
points survive consumer, scene and runner replacement. Login hydrates attributes from backend
progression; the local aggregate is also encoded in the file cache.

```text
IPlayerStashService / IPlayerLoadoutService
                -> LocalProfileStore
                -> ILocalProfileRepository
                -> LocalProfileRepository -> LocalProfileFileStore
```

`LocalProfileStore` remains the transactional application boundary. It builds a candidate
snapshot, asks the repository to accept it, and publishes the observable replacement and
`ProfileCommitted` only after the complete in-process transaction succeeds. It never mutates the
current snapshot before acceptance.

Town personal-inventory presentation reads the confirmed Loadout through
`IPlayerLoadoutService`. `LocalLoadoutInventoryReadSource` owns a disposable, read-only
`LootEntry` projection for the bound profile and is the only owner of its
`ApplicationStashContext.ProfileCommitted` subscription. It filters commits by `ProfileId`,
advances its local presentation revision and rebuilds the projection on demand. That reusable
buffer is never persistent or authoritative state and is not copied into `SocialPlayer`.
The same source also exposes the confirmed `PreparedEquipmentLoadout` to the shared inventory
screen, so one matching profile commit refreshes Inventory and Equipment together. Town Equip and
Unequip intentions cross a local capability endpoint and then use only
`IPlayerLoadoutService.TryAssignPreparedEquipment` or `TryClearPreparedEquipment`; neither the UI
nor its presenter calls `LocalProfileStore` directly or holds optimistic Equipment state.

`SocialPlayer` therefore has no `PlayerLootReceiver`. `HubPlayerSpawner` does not seed a networked
Town inventory, and Fusion does not replicate the Loadout or the inventory screen's open state.
The Raid `NetworkPlayer` retains its separate authoritative `PlayerLootReceiver`; admission and
extraction continue to cross the existing reservation/receipt boundaries rather than sharing the
Town projection.

Commerce crosses this aggregate only through `IShopTransactionService`, whose accepted trade is one
`LocalProfileStore` transaction performing the final funds, capacity and owned-quantity validation
and applying shop receipt idempotency. No other Commerce component mutates the Loadout, Currency or
`LocalProfileStore`, and no replicated inventory shadow participates. Draft, presentation, stock
and request-identity ownership belong to `CommerceArchitecture.md`.

The productive `LocalProfileRepository` validates candidate identity and codec compatibility,
then calls the file store's atomic-write operation before publishing a cloned Snapshot as Ready.
The current save contract is schema **11**; schemas 1–10 remain readable. Item data and prepared
Equipment retain the instance block from [Item Instances §§4.1, 6.1](ItemInstanceArchitecture.md#61-local-persistence).
Codec v10 introduced physical `slot` positions and the separate backend `inventorySchemaVersion`;
earlier decode assigns list indices as slots (including reservation items), retains the earlier
schema-specific migrations and marks inventory as version 1. Local decode never migrates the backend.
A v10 equipment stack decodes into plain instances without IDs only as transient cache state until
backend hydration; legacy equipment-ownership repair never runs on v11. Cache-only initialization
(including Editor domain reload) can publish the Store without backend hydration. Decoding schemas 1–10 therefore sets `RequiresEquipmentHydration`, preserved by subsequent
v11 cache saves until an inventory hydration succeeds. Local Equip/Unequip, Stash Equip and remote
prepared-equipment updates reject while it is set; gem install requires a backend-issued instance ID.
Unity never synthesizes IDs for those transient entries (Item Instances §9).
Pending extraction preserves per-unit modifiers. After a successful backend extraction commit,
both the live extraction path and login retry fetch revision-consistent authoritative inventory and
progression. `LocalProfileStore.TryCompleteExtractionCommit` binds that state to the pending receipt,
validates it through the existing hydration/codec boundary and replaces the ID-free extraction
ownership with backend-issued instances while clearing `PendingExtractionCommit` in **one write**.
Level, experience and attributes are hydrated in that same write. Fetch, validation or write failure
is reported and retains the pending payload and current snapshot for idempotent login retry; ordinary
reconciliation continues to preserve pending extraction ownership. Unity never fabricates instance IDs
or relaxes reservation validation to acknowledge extraction.

Known limitations (follow-up, not yet handled):

- **Superseded pending extraction.** Acknowledgement requires the backend's
  `lastAppliedExtractionReceipt` to match the pending receipt. If the backend has already applied a
  *later* extraction for the profile (for example from another device), the receipts never match:
  the pending payload is retained and logged on every login and keeps blocking new loadout
  reservations. This fails safe (no ownership is lost or invented) but there is no automatic
  recovery; a future rule must decide when a newer backend receipt proves the pending one applied.
- **Misleading live-path error.** When the pending extraction was already acknowledged (for example
  by the login retry), `PlayerExtractionLootSaver` still logs "authoritative hydration failed" after
  `CompleteExtractionAsync` returns false, although no state changed.
Older builds reject unsupported future cache
versions as `UnsupportedVersion`; backend hydration remains authoritative.
The atomic operation's boolean result is the acceptance boundary, not a claim of filesystem flush durability.

### Authenticated corrupt-cache recovery

Startup and ordinary `InitializeWithProfile` calls default to no reset permission. Only the
successful authenticated `LoginFlowController` path opts in after character/profile fetches and
inventory/progression fetches with equal revisions. Missing payloads, revision mismatch and invalid
hydration cannot reset the cache. Revisions need not be positive.

Repository eligibility is typed, not inferred from error text: an existing readable file must fail
decoding, every other existing file must also be readable, and neither may be a future schema.
Missing files alone, IO failures, missing configuration and invalid identity do not qualify.
Eligibility is cleared on every Initialize. A valid main or backup retains its existing load/repair
behavior; an unsupported main still blocks backup fallback.

Recovery builds a fresh snapshot for the initialized ProfileId, hydrates it from both backend
payloads, validates identity and codec compatibility, and attempts one atomic write before Snapshot
or Ready publication. Failure leaves the repository unavailable and does not admit the candidate;
ordinary TrySave remains fail-closed. Successful replacement emits an explicit warning that unsent
local state cannot be recovered. No deletion or backup manipulation precedes the validated write.
Healthy cached PendingExtractionCommit and local-only fields are retained through normal hydration;
the existing pending extraction retry is not replaced by corruption reset. Active missions, local
abilities, receipts and other unsent local-only state cannot be reconstructed by backend hydration.
A synchronous cache save adds login latency; save failure stops login hydration rather than admitting
an unsaved profile. Authentication/fetch failures never authorize local fallback.

### Stash capacity admission

D1 replaces the TASK-218 compact exception with persistent physical entries. The instance-aware
shape is `{ slot, lootId, amount, instance? }`: `slot` is a nonnegative integer unique within its
list, `amount` is positive, and `lootId` may repeat. Slot is a location, not item identity.
Equipment requires `instance` and `amount == 1`; non-equipment has no instance. Equipment never
stacks, merges or splits. See [Item Instances §§2, 4.1](ItemInstanceArchitecture.md#41-schema-v3)
for the instance block and validation. Removal leaves a hole; other entries are never implicitly
merged or renumbered. Backend writers copy stored subdocuments whole when moving them. The
Mongoose schema is strict: future fields require their own expand release, not arbitrary
unknown-field persistence.

Capacity counts occupied entries, not distinct IDs or quantities: Loadout 30, Stash 60.
Town Inventory (Loadout) has exactly 30 slots (`0..29`), all visible, with no pagination and no
prefab change; normal operations must never exceed 30. Loadout slots >= 30 are only a legacy
over-capacity-data case, not supported gameplay capacity. All accounts are test accounts: after
the migration dry run, correct affected test data by moving excess to Stash or resetting the
character, not through overflow UI. Stash retains its existing slot-range paging.
`LocalProfilePersistenceConfiguration.StashCapacity` supplies the shipped Stash value to the Store;
other callers default to 60 and nonpositive constructor capacity throws. Hydration and codec accept
legacy server excess without truncation. Overflow addresses (`slot >= capacity`) are reducing-only
sources, never ordinary placement/merge targets. Migration preserves legacy excess; the defensive
lossless rollback fallback below is not permission to expand normal Loadout capacity.
Town `StackMax` remains deferred (L2) for non-equipment; only integer-overflow checks bound its
merges. Equipment always occupies one entry per unit.
Town persists no Origin under D2 = O1 for MVP, **pending reconciliation of Game Design doc 07 by its
owner (G1)**. Raid provenance stays expedition-scoped; this is not a claim that Doc 07 is reconciled.

Backend v3 reception preserves the v2 placement rule for non-equipment: add to the lowest-slot
matching `lootId` inside capacity, otherwise use the lowest free slot below capacity when occupied
count is below capacity, otherwise reject `409 INVENTORY_FULL`. Equipment never merges; each
unit needs its own lowest free slot ([Item Instances §4.1](ItemInstanceArchitecture.md#41-schema-v3)).
Shop buys, transfers, unequip and extraction use these category-specific rules; equipment buy-back
is disabled. For non-equipment, Unity `ProfileInventoryRules.TryMerge`/`ExceedsCapacity` have the
previous implementation difference when the only matching stack is in overflow: they reject
rather than falling back to an in-capacity hole. This is not an additional approved rule. An
in-capacity matching non-equipment stack can still receive without adding an occupied slot,
even at/above the occupancy limit.

The Store preflights transfers, secure-loot packages and mission rewards on cloned candidates.
Rejection changes no ownership, currency, receipts or mission claim state and publishes no commit;
a refused mission package stays pending. Non-equipment aggregate inputs retain the secure-loot
duplicate-input `lootId` check (`InvalidInventory`); equipment entries remain individual instances,
including repeated template IDs. Stored slots may also repeat `lootId`.
Extraction and unequip still target Loadout, not Stash.

### Inventory order

Physical Town positions persist through the Store, codec v11 and backend v3. Responses and local
lists are ordered by slot, but array index is not a slot address. The Stash screen projects holes
and pages by slot ranges. Take All / Leave All are a single `POST /character/me/inventory/transfer-all`
and at most one revision-guarded write: the backend evaluates every source `(slot, lootId)` entry in
ascending slot order under the single-transfer rules, including duplicates and entries outside the
visible pool, skips refused stacks and returns the resulting lists, revision and a per-stack result.
The local profile applies that result in one commit; failure leaves it unchanged and reconciles once.

Explicit Sort is the only compaction: `StashDisplayOrder` compares category, display name,
`lootId`, then original slot, and the selected entries are assigned slots `0..n-1`. It changes no
identity or amount and never merges stacks. `PUT /character/me/inventory/order` takes
`{ list, slotOrder, expectedRevision }`; `slotOrder` must be an exact permutation of occupied slots,
with the entry at `slotOrder[i]` moving to slot `i`. The backend rejects more occupied entries than
capacity (`INVENTORY_FULL`), a pending reservation (`RESERVATION_PENDING`), stale revision
(`REVISION_CONFLICT`) or a malformed permutation (`VALIDATION_FAILED`). Sort remains allowed while
Ready. It shares the application-owned `StashTransferCoordinator` remote-first serialized queue:
backend confirmation, then local apply; failure/divergence reconciles once and stops without retry.

Slot-addressed transfers/sales carry `sourceSlot` plus a `lootId` guard. Whole-stack
`POST /character/me/inventory/slots/move` takes
`{ fromList, fromSlot, toList, toSlot, lootId, expectedRevision }`: empty target moves, matching
non-equipment `lootId` merges all, a different `lootId` swaps atomically, within or between lists.
Equipment moves with its instance and never merges, even for matching templates
([Item Instances §4.3](ItemInstanceArchitecture.md#43-operations)). Destinations must be inside
capacity; a cross-list insertion requires free occupancy and a swap cannot return a displaced
entry to an overflow source. A backend move to the same address is a no-op without revision change.
These operations leave unrelated slots unchanged.

Reservation items retain exact Town entries and instances, including prepared Equipment slots
shaped as `null | { lootId, instance }`. Admission preserves per-unit weapon modifiers but never
transports backend instance IDs; Raid partitions entries under the
[Player Inventory rules](LootInteractionArchitecture.md#player-physical-inventory) and
[Item Instances §§4.3, 6.3](ItemInstanceArchitecture.md#63-raid-transport). Raid Sort stays disabled
and exact Raid positions never enter the cache/backend. Extraction sends
`{ lootId, amount, modifiers? }`, keeps equipment units separate and places the result into fresh
Loadout slots rather than restoring Raid addresses.
D1 supersedes the compact/no-hole exception; Game Design doc 16 reconciliation (G2) remains an
owner action, not a completed documentation change.

### Backend persistence integration boundary

Backend persistence is asynchronous and must remain outside the synchronous
`ILocalProfileRepository` contract. The authenticated integration must use the token held by
`ApplicationAuthContext` to load the complete character snapshot before Town enables dependent
flows, submit mutations or receipts to the backend, and publish server-confirmed state back into
the application context. Network errors, cancellation, revision conflicts and retries are explicit
outcomes; none may create a local identity or silently accept a local file as authoritative.

The exact HTTP DTOs, route surface, environment flags and error codes belong to the
[backend contract](../../../backend/README.md#inventario-b2-t3--contrato-v2).
`ProfileCommitted` means local transaction acceptance, not durable backend acknowledgement.

`inventory.inventorySchemaVersion` absent or 1 means legacy aggregate lists; 2 means physical
slots; **3** gates the instance-aware format, including prepared Equipment
`null | { lootId, instance }` and instance-preserving reservation entries. Instance-aware flows
require v3; see [Item Instances §4.1](ItemInstanceArchitecture.md#41-schema-v3). Inventory reads
return the version and slot-ordered lists/reservation items without changing storage. No backend
read repairs aliases, merges, migrates, increments revision or saves.

The following v1/v2 protocol and gate notes describe the preceding physical-slot rollout; they do
not authorize v2 data for instance-aware flows. Existing gate rules otherwise remain unchanged
under Item Instances §4.1. The header advertises minimum compatible capability: instance-capable
Unity clients always send `3`, including before hydration, and select v2/v3 data behavior from
`inventorySchemaVersion`. The v2 gate accepts `2` or `3`; the instance gate requires `3` and schema 3.

Gate-off v1 retains lootId operations, but rejects ambiguous legacy duplicates rather than
repairing them. Slot input on v1 rejects `409 INVENTORY_MIGRATION_REQUIRED`; v2 move/sell without
`sourceSlot`, or order without `slotOrder`/with `lootIds`, rejects `409 INVENTORY_SLOT_ADDRESS_REQUIRED`.

Unity backend clients send `X-Grimhold-Inventory-Protocol: 3`, including host calls.
`INVENTORY_PROTOCOL_GATE` defaults off; when true it accepts protocol 2 or 3 and schema 2 or 3.
`INVENTORY_INSTANCE_PROTOCOL_GATE` takes precedence when true and requires protocol 3 and schema 3.
On protected routes a missing/unsupported header rejects `426 CLIENT_UPGRADE_REQUIRED` before body
validation; a compatible header with an unmigrated character rejects `409 INVENTORY_MIGRATION_REQUIRED`.
Protection covers inventory reads/writes, prepared Equipment, reservations, extraction publish/commit,
Raid admission ticket/verification and character creation, including the target participant/recipient
checks. Metadata-only `GET /character/me` and `/me/profile`, profile edits, ability purchase,
progression commit and Town tickets do not rewrite lists and are exempt. Debug/legacy extraction
routes remain unavailable in production, regardless of header. Split has a separate default-off
`INVENTORY_SPLIT_ENABLED` flag; `403 FEATURE_DISABLED` precedes protocol checks when off.
New characters are v3 with the instance gate on, v2 with only the slot gate on, otherwise v1.
See the backend README for exact routes.

Hydration (`ApplicationStashServiceBootstrapper.HydrateSnapshot`, also used by reconciliation)
validates lists before applying them: invalid/repeated slot addresses, unsupported inventory versions
or negative currency reject the payload. Unknown IDs and nonpositive amounts are skipped; valid v3
entries retain instances, duplicate template IDs, holes and overflow without merge; invalid equipment
instance/amount shapes reject under Item Instances §4.1. V1 hydration assigns list indices as
slots, not a persistent migration. Login hydrates a clone and saves through the repository before
publication; pending extraction and local-only state retain their existing preservation rules.
The coordinator makes Unity Town inventory mutations unavailable on v1; remote slot mutations and
remote commerce also refuse v1. This is not a blanket Store guard: direct Store methods and the
personal Town Equipment endpoint do not themselves check inventory version.
`TryCreateLoadoutReservation` rejects more than 30 occupied Loadout entries, never silently truncating.
The personal Town read source rejects an over-30-entry Loadout; the Stash screen's fixed Loadout
pool displays only `0..29`, all at once. Legacy Loadout excess requires test-data correction after
the migration dry run, not pagination or a prefab change (see
[Loadout presentation policy](RaidInventoryUIArchitecture.md#stash-screen-composition)).

The historical v1 → v2 migration is explicit and one-time via
[`backend/scripts/migrate_inventory_slots.js`](../../../backend/scripts/migrate_inventory_slots.js),
never a GET side effect. It folds legacy duplicate/alias IDs into their first stored occurrence,
assigns slots `0..n-1` separately to Stash, Loadout and reservation items, retains unknown IDs and
overflow, then sets version 2 in one revision-guarded write per character. It checks per-ID totals,
re-reads/recomputes revision conflicts (up to three retries), aborts invalid/mismatched characters
without writing, and skips v2 on rerun. `--dry-run` reports duplicates, counts, overflow and totals
without writes; unknown-ID diagnostics use backend catalogs only, not Unity's complete catalog.
That migration left Raid results and pending extraction item payloads aggregate; receipts were not
rewritten.

The explicit **v2 → v3** migration is owned by
[Item Instances §4.2](ItemInstanceArchitecture.md#42-migration-v2--v3): an equipment stack of N
becomes N plain instances with backend-issued IDs, preserving the original slot for the first
and using lowest free slots, then reducing-only overflow addresses, for the rest. Nothing is
discarded. Prepared Equipment, pending reservations and stored extraction results migrate under
that same contract; reruns skip migrated characters and each accepted write advances revision once.
The script follows the existing migration pattern, not a GET side effect. Plain instances,
including the fallback, follow [§4.4](ItemInstanceArchitecture.md#44-plain-instances).

Historical v2 cutover notes: V10 requires a dry run on a database copy; A3 requires the owner/team window.
Use the dry-run findings to correct legacy Loadout excess in the test data (move excess to Stash or
reset the character); no Loadout overflow UI is planned. This documentation does not perform that
cleanup. Complete the slot-aware B1/B2 rollout and drain old backend instances, distribute U2 clients/hosts,
then enable the gate and back up/migrate in maintenance before reopening. Backend v2 behavior is
implemented even with the gate off; that is not permission to migrate production without the gate.
Before gate activation the rollback floor is B1 (no older backend), with any client. After migration,
retain at least B1 and U2 and never turn the gate off; older writers can merge/drop slot data.
Backup restore is safe only inside the window before the first post-migration write. Split enablement
is independent of migration/protocol activation; the TASK-282 "Dividir" UI calls it and reports the remote
failure while the flag is off.

### Progression transaction

`LastAppliedProgressionResultSequence` is the aggregate's at-most-once mechanism. A new
`ProgressionReceipt` is accepted only at `watermark + 1`; the exact receipt at the current
watermark is `AlreadyApplied`, a different payload at that sequence is `Conflict`, an older
sequence is `Stale`, and a sequence gap is invalid. The bounded receipt list is audit history
only and may be pruned without weakening rejection of an old result.

Level, current Experience, available attribute points, watermark, `LastProgressionReceipt` and
receipt history are one candidate mutation. Watermark zero requires no last receipt. A positive watermark requires
the last receipt to exist, match that sequence and belong to the snapshot ProfileId.
`LocalProfileStore` reuses `ConsolidatedExperienceApplicationRules`; it does not duplicate the
curve, and delegates point calculation to `CharacterAttributePointGrantRules`. A zero-XP
resolution still advances the watermark. Only `Success` publishes a
commit event; `AlreadyApplied` performs no second write or notification.

Town attribute assignment is another `LocalProfileStore` transaction. The store delegates the
single-point operation and configurable maximum to `CharacterAttributeAssignmentRules`, submits a
complete candidate and publishes `ProfileCommitted` only after repository acceptance. Town UI
reads the confirmed `CharacterAttributeState` through the store's try-pattern query and treats
`ProfileCommitted` for the matching `ProfileId` as its sole refresh signal. This observable commit
does not imply cross-process durability.

## Domain and authority boundaries

Stash and loadout transfers, prepared Equipment assignments, loadout reservations and extraction
receipt application are complete aggregate transactions. `PreparedEquipmentLoadout` covers eight
slots: Set A and Set B each own Main Hand and Off Hand, plus Helmet, Armor, Gloves and Boots.
Inventory and prepared Equipment are mutually exclusive ownership locations. Equip moves one
`{ lootId, instance }` unit from Inventory to its Equipment slot, while Unequip moves it back;
identity and modifiers remain attached
([Item Instances §4.3](ItemInstanceArchitecture.md#43-operations)). Equip requires `instanceId`;
`EquipmentSlotRules` decides which slot an identity may occupy and weapon slots additionally require
a usable Weapon definition. A weapon assignment also evaluates its `WeaponAttributeRequirements` against the
confirmed `CharacterAttributeState` already owned by the aggregate. The same pure eligibility
rule is rechecked before preparation, reservation and rollback; armors have no attribute
requirements during the MVP. Ordinary Town Inventory Equip reads only Inventory. The Stash screen
also supports direct Stash Equip through `EvaluateEquipFromStash` / `TryEquipFromStash`: both share
one planner over an isolated snapshot clone. It removes exactly one Stash unit, returns displaced
units (including same-ID replacement and both hands for a two-handed weapon) to Loadout, and checks
final occupied-slot capacity before any commit. A full 30-slot Loadout can equip into an empty
Equipment slot only if no displaced unit needs another occupied Loadout slot; equipment cannot
merge into matching-template entries. Backend-hydrated excess remains accepted; this operation
cannot grow it. Evaluation publishes nothing;
application replans against current state and commits the whole candidate once.

The Stash coordinator checks the Ready gate, availability and Store rules, then sends an addressed
one-unit transfer from the lowest-slot matching Stash stack, then `UpdatePreparedEquipment(projected)`,
then the atomic local application.
The coordinator copies Stash, Loadout and prepared Equipment ownership before sending and checks
that ownership after each successful remote await. Drift reconciles once as divergence (or failed
recovery), without sending the next endpoint or applying the Stash equip. Revision metadata alone
is not drift. The final guard and Store revalidation/application have no intervening await; the
Store still rejects changed attributes or reservation state.
No local ownership or Equipment state changes during either request. Production
`RemoteInventoryService` calls `SetRemoteRevision` after each successful response, producing two
revision-only saves/events in addition to the single final ownership transaction. The test fake
does not save revisions by default. These two remote mutations
are **not** one transaction: the first may remain committed if the second fails. Failure or an
unknown remote result reconciles once and stops without retry or compensation; a local refusal
after remote success also reconciles and reports divergence (or failed recovery). Ordinary Loadout
Equip/Unequip retain their existing local-first synchronization behavior.

Design debt: Inventory document 16 section 4 says displaced Stash equipment returns to Stash,
whereas Game Design document 12 section 6 says Inventory. The user explicitly approved Loadout
(Inventory) for U5; this is an approved exception pending reconciliation of those live documents,
not a claim that their conflict has been resolved.

Ordinary Inventory replacement removes the new
unit before returning up to two displaced hand units so capacity is evaluated against the final state
without exposing an intermediate state. A two-handed weapon exists only in Main Hand and derives the
blocked state of its Set's Off Hand. Direct assignment to a blocked Off Hand is rejected and never
silently removes the two-handed weapon. Unequip rejects atomically when the returned unit would exceed Inventory
capacity. A rejected operation moves nothing and never creates a Pickup.
Extraction receipts and the rest of the aggregate are cached across processes; remote durability
and replay acceptance remain backend responsibilities.

### Ability Repertoire Hydration Contract (TASK-224)

Unlocked abilities (`UnlockedAbilities`) are durable and authoritative in the backend (`Character.unlockedAbilities`).
They are returned at login and during profile reconciliation via `GET /character/me/progression` and hydrated in
`ApplicationStashServiceBootstrapper.HydrateSnapshot`. Hydration populates `snapshot.UnlockedAbilities` strictly from
the authoritative backend list, dropping any unrecorded local unlocks and filtering unknown definitions against
`AbilityDefinitionCatalog`. Revalidation of prepared abilities (`PreparedAbilityLoadout.TryRevalidateAfterAttributeChange`)
is subsequently evaluated against this hydrated set.

Schema version 4 persists this Weapon Set contract. Schema 3 maps historical Weapon Slot 1 to Set A
Main Hand and Weapon Slot 2 to Set B Main Hand; both Off Hands start empty. Pending reservations use
the same migration. Earlier schemas did not distinguish
the former exclusive implementation from the temporary non-owning implementation. Decoding treats
a complete Inventory reference set as the latter and subtracts one unit per prepared slot; if the
complete set is absent, it preserves Inventory unchanged as an earlier exclusive save. The check is
all-or-nothing. This describes the schema-4 migration; new saves write schema 11 and never run
this legacy ownership repair ([Item Instances §6.1](ItemInstanceArchitecture.md#61-local-persistence)).

TASK-330 originally kept this migration local. The current prepared-Equipment adapter sends all
eight slots: `weaponSlot1` / `weaponSlot2` map to Set A / B Main Hand, `offHand1` / `offHand2`
map to their Off Hands, alongside Helmet, Armor, Gloves and Boots. The Stash flow above submits
that projected eight-slot value before its local ownership commit. Reservation transport and
recovery have their own contracts and are not changed by U5.

This structure does not yet implement shield defence, Dual Wield attacks or accessories.
Those behaviors require their own rules without adding a competing Equipment source of truth.

Quick Slots (TASK-214) are a local-only part of the same profile aggregate. Schema version 9 adds
`quickSlot1`..`quickSlot4`: each is a `LootId` or empty, never a stack, Origin or quantity. Older
schemas load with all four slots empty. `LocalProfileStore.TrySetQuickSlots` validates that every
bound definition is Quick Slot compatible in the loot catalog, commits through the usual
candidate/repository transaction and publishes `ProfileCommitted`. It does not check Ready, a
pending reservation or item ownership, because bindings do not move items; the caller checks that the
definition is present in the Inventory the player is looking at (the Town Loadout or the Raid
Inventory). A duplicated or malformed binding rejects the profile like other local-only state, while
a definition that left the catalog or stopped being compatible loads as an unassigned slot.
Bindings are not sent to the backend and backend hydration preserves them. Town quantity reads sum
all stacks of the bound definition without merging (`QuickSlotController.GetAvailableAmount`).
`ProfileInventoryRules.FindAmount` also sums all matching stacks, saturating its result at
`int.MaxValue`; definition-wide removal drains matching slots in ascending order.
Under Doc 14 §12.2 and TASK-230, available units dropping to zero no longer unassigns the slot. Instead, the slot binding is retained in an inactive state displaying count 0 and an unavailable overlay, automatically reactivating when units of that definition enter the inventory. Quick Slots strictly forbid assigning the same definition to more than one slot simultaneously (rejecting with QuickSlotAssignmentResult.AlreadyAssigned). Both Consumables and Utilities configured with QuickSlotCompatible = true are accepted. Explicit unassignment is performed via right-click or clear intention.

Fusion may carry `ProfileId` and session snapshots for the active runner, but it does not
own the local stash or loadout. A raid Host never reads another client's local aggregate.
`PlayerRef` is not a gameplay-persistence identity.

The confirmed `CharacterAttributeState` crosses the participation-admission boundary once when
the player starts an expedition. State Authority initializes the corresponding
`NetworkRaidParticipant`, which then owns the frozen, replicated snapshot for that participation.
Raid consumers read that participant snapshot through its read-only contract; they do not query
the application profile, UI or persistence implementation again. The admission transport does not
default, clamp or normalize the state supplied by the current profile source.

### Expedition preparation boundary

`TryPrepareExpeditionEquipment` is a read-only gate used by Town Ready and immediately before
reservation. It never grants, moves or equips an item. Prepared armor remains optional and untouched.
This rule supersedes the recovery-weapon contract under TASK-278 L20/L22 (GD Doc 04 §13 / Doc 09 §15):

* A valid weapon in Set A Main Hand is left untouched and commits nothing.
* When only Set B Main Hand is occupied, preparation leaves both Sets unchanged and admission marks
  Set B active.
* With no usable equipped weapon, preparation returns `PreparedWeaponRequired` before any
  reservation. Town directs the player to equip a weapon or ask the Trainer for a training weapon.
  Training weapons count as valid weapons. Owned `arming_sword` units in Stash or Loadout are not
  automatically equipped; `arming_sword` remains a normal lootable weapon.
* A persisted assignment that no longer resolves to a usable weapon fails explicitly as
  `InvalidPreparedWeapon` without mutating the aggregate. Corruption is never overwritten and
  never hidden behind a recovery grant.
* A prepared weapon whose requirements exceed the confirmed attributes fails explicitly as
  `AttributeRequirementsNotMet`; no assignment, ownership or reservation state is changed.

The Trainer issues the selected training weapon through the authoritative backend endpoint;
Unity hydrates the backend-issued instance rather than minting identity. The normal reservation
path carries exactly the equipped selection and its modifiers into admission, without substitution.
There is no recovery configuration, consent modal or networked consent phase. A backend reservation
rejection `409 PREPARED_WEAPON_REQUIRED` maps to the same Trainer-directed notice without retry.
The Town controller resumes pending local materialization on Render and does not ACK until the
normal reservation save succeeds. Preparation validates reserved equipment when a reservation is
already pending, without changing ownership. Raid never grants an automatic weapon.

### Loadout reservation boundary

`TryCreateLoadoutReservation` requires at least one valid prepared weapon and atomically moves
the complete local Inventory plus its eight separately owned prepared assignments into `PendingLoadoutReservation`
before the Town queue ACK. The active Town Loadout and its assignments are then empty and cannot
ambiguously reference units already reserved for Raid. The requirement stays enforced here as a
domain invariant even though preparation already guaranteed it. While a reservation is pending the
reservation owns Inventory and Equipment, so `LocalProfileStore` rejects every Stash/Loadout transfer,
`TryEquipFromStash`, `TryAssignPreparedEquipment` and `TryClearPreparedEquipment` with
`StashOperationResult.ReservationPending`, without changing or publishing anything: a later rollback
would overwrite their result and a non-empty Loadout would make `TryCommitExtraction` fail. The same
operations also fail with `InvalidInventory` instead of throwing when the profile is unavailable.
The same reservation id is idempotent; a different id is rejected while pending. Pre-admission failure restores items and assignments in
one rollback. The reservation stores complete instances with exact Town slots; admission omits
IDs but retains modifiers ([Item Instances §§4.3, 6.3](ItemInstanceArchitecture.md#63-raid-transport)). Backend rollback
first restores every free original address, then places collisions in the lowest free in-capacity
slot and finally the lowest free overflow slot, never merging or dropping units. That last branch
is defensive lossless restoration, not a normal operation allowed to exceed the 30-slot Loadout
contract; legacy excess is handled by the test-data cleanup policy above, not UI expansion.
Existing Equipment assignments are preserved; reserved assignments use empty Equipment slots and displaced units are
restored to Loadout. Unity `ProfileInventoryRules.RestoreSlots` instead visits reserved entries in
ascending order and immediately falls back to the lowest free address on collision; it does not
reserve later original addresses first. Local rollback replaces prepared Equipment after validation.
Unity's Town mutation block normally keeps Loadout/Equipment empty, but these collision semantics
are not identical to the backend and must not be described as full parity.
After participant, avatar and exact `Inventory + the eight Equipment slots`
ownership are observed, confirmation consumes the local reservation. On restart, login hydration restores
backend reservation data; bootstrap retries pending extraction or rolls back an orphaned reservation
through the existing paths.

### Backend reservation lifetime

The backend `inventory.pendingReservation` outlives admission: it is the recipient proof that
Bounded Player-Host extraction publishing requires (see [Extraction](ExtractionArchitecture.md)).

| Moment | Backend reservation | Reserved items |
| --- | --- | --- |
| Launch (`POST /character/me/inventory/reservation`) | Created from the backend-persisted Loadout and prepared Equipment | Owned by the reservation |
| Failure before admission (`DELETE ...?rollback=true`) | Removed | Restored |
| Admission confirmed (`POST /character/me/inventory/reservation/admit`) | Kept, marked `admittedAt` (idempotent) | Owned by the Raid participation |
| Extraction commit | Consumed by `ExtractionCommitService` | Replaced by the authoritative result |
| Any other ending (defeat, abandon, return without extraction, definitive disconnect, Absolute Closure, timeouts, Host Migration failure, crash or relogin) | Consumed by `DELETE ...` without rollback | Lost |

An admitted reservation is never rolled back: rollback returns `409 RESERVATION_ADMITTED`, and a new
launch never restores its items. A loss clear or a replacement while an unconsumed
`AuthoritativeExtractionResult` targets the character returns `409 EXTRACTION_RESULT_PENDING`, so
extracted loot is never discarded. On login, an admitted reservation without a local pending
extraction commit is discarded locally without restoring items and cleared on the backend as a loss;
a pending extraction commit keeps its retry path. Previously admission deleted the backend
reservation, which made every player-hosted extraction publish fail with `NO_ACTIVE_RESERVATION`
and left the backend Loadout empty for the next Raid.

The backend retains guarded reservation rollback support for callers that supply a reservation ID. `DELETE /character/me/inventory/reservation?rollback=true`
accepts optional `expectedReservationId` (query, or request body when omitted from the query). It must
be a 1–128 character identifier containing only letters, digits, `_` or `-`; invalid values return
`400 VALIDATION_FAILED`. A different active ID returns `409 RESERVATION_MISMATCH` without writes.
The ID is also part of the atomic account/revision write filter, so a replacement after the read
cannot be deleted; a failed atomic match returns `409 REVISION_CONFLICT`. Guarded already-absent
reservations are read-only, making repeated cleanup harmless. Omitted-ID callers retain the legacy
rollback/clear behavior, including its revision semantics; admitted-reservation protections remain.

TASK-278 removes the recovery-grant-specific Unity cleanup stage. Ordinary pre-admission
cancellation and failed-start rollback retain the normal reservation path described above;
automatic recovery grants and their consent bindings no longer exist. Full inventory/progression
reconciliation remains the authoritative hydration boundary for ordinary inventory reconciliation
and extraction completion.

### Extraction boundary

State Authority retains the raid result snapshot while Input Authority commits it to its
application-level Loadout. A valid admission consumes the pending reservation and leaves
that Loadout empty during the raid. Result items are `{ lootId, amount, modifiers? }`; equipment
units remain separate with `amount = 1`, while non-equipment may fold by `lootId`. The backend
validates legal modifiers against template rules and the gem set, never regenerates seeds, and
issues new equipment instance IDs on commit
([Item Instances §7](ItemInstanceArchitecture.md#7-extraction-and-reservation)). Reception uses
physical Town slots `0..n-1`; Equipment remains separately assigned. Raid slot positions and
provenance are not persisted, and instances carry no provenance. Stash is not an automatic extraction destination.
A new receipt is accepted only when the Loadout is empty and fits its capacity, so an unexpected pre-existing Loadout
fails without changing either inventory. An ACK is sent only after the current aggregate accepts
the transaction, and the raid inventory is cleared only after the ACK. Transport duplicates cannot
duplicate loot during the current process because extraction receipts remain in the aggregate.
Cross-process idempotency requires backend receipt persistence before this can be considered a
durable guarantee.

## Temporary limitations and failure policy

- The per-profile schema-11 file cache survives process exit but is not backend authority.
- PlayerPrefs never supplies a productive identity. Unsupported or unreadable caches block hydration;
  confirmed corruption can be replaced only through the explicit authenticated flow above.
- A backend load failure must remain visible and must not be replaced by a generated identity or
  legacy local profile.
- Durable progression and extraction acknowledgement require backend-confirmed, idempotent
  operations. In-memory success is temporary gameplay evidence only.

## Validation strategy

EditMode tests verify the process-local lifetime, profile boundaries, progression watermark
semantics and existing aggregate transactions. Codec and fake-file repository tests cover productive
cache behavior, including authenticated corruption recovery and rejection without writes. Manual
validation covers restart login, cache save failures and a complete Town-Raid-Town cycle. Backend
integration must add restart hydration, network failure/retry, revision-conflict, receipt-idempotency
and real Host/Client validation.
