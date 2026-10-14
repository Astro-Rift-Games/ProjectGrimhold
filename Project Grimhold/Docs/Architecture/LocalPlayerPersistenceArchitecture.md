# Local Player Persistence Architecture

## Context

Project Grimhold currently keeps the character aggregate alive across scenes and
`NetworkRunner` replacement during one application process. The backend owns character identity
and is the target source of truth for durable metagame state. The temporary Unity composition must
not let legacy local files block authentication or stand in for backend durability.

## Decision

The application owns one process-local profile aggregate identified by the backend `CharacterId`,
represented locally as `ProfileId`. `LocalProfileProvider` remains invalid before authentication
and never generates or restores a productive identity through `PlayerPrefs`. A successful login
injects the remote `CharacterId` before initializing profile services.

`ApplicationStashServiceBootstrapper` creates one `ApplicationStashContext` before the first scene
but defers store initialization until a valid remote identity is available. It then composes an
`InMemoryLocalProfileRepository`. The context remains `DontDestroyOnLoad`; stash, loadout, prepared
Equipment assignments, pending reservations, extraction receipts and progression state survive
Town-Raid-Town transitions while the application remains open. They are discarded on application
shutdown until the backend integration hydrates and commits this aggregate remotely.

`CharacterAttributeState` is part of that same character aggregate. New profiles initialize it
from `ProgressionBalanceDefaults.InitialCharacterAttributeState`; assigned values and available
points survive consumer, scene and runner replacement only while this application process remains
open. They are intended for durable backend persistence when that integration exists.

```text
IPlayerStashService / IPlayerLoadoutService
                -> LocalProfileStore
                -> ILocalProfileRepository
                -> InMemoryLocalProfileRepository
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

The productive `InMemoryLocalProfileRepository` accepts an isolated clone of that complete
candidate after validating its readiness and profile identity. It never encodes or reconstructs
the aggregate through `LocalProfileSaveCodec`; domain mutations are validated by
`LocalProfileStore` and their pure rules.

`LocalProfileRepository`, `LocalProfileFileStore` and `LocalProfileSaveCodec` remain isolated,
test-covered legacy infrastructure. They are not part of the runtime composition and files such
as `grimhold-profile.json` are ignored. They must not become a fallback when authentication or a
backend operation fails.

### Backend persistence integration boundary

Backend persistence is asynchronous and must remain outside the synchronous
`ILocalProfileRepository` contract. The authenticated integration must use the token held by
`ApplicationAuthContext` to load the complete character snapshot before Town enables dependent
flows, submit mutations or receipts to the backend, and publish server-confirmed state back into
the application context. Network errors, cancellation, revision conflicts and retries are explicit
outcomes; none may create a local identity or silently accept a local file as authoritative.

The exact HTTP DTOs and endpoints belong to the backend contract and are not invented here.
Until that contract is integrated, `ProfileCommitted` means only that the process-local aggregate
accepted a transaction. It must not be interpreted as durable backend acknowledgement.

Hydration (`ApplicationStashServiceBootstrapper.HydrateSnapshot`, used at login and by
`LocalProfileStore.ReconcileRemoteState`) validates the whole backend payload before writing
anything, so a rejected payload leaves the snapshot untouched. Item lists are normalized to what the
save codec accepts: unknown LootIds and non-positive amounts are skipped, duplicate LootIds are
merged, and an amount overflow on merge or a negative currency rejects the payload. Login hydrates a
clone and persists it through `ILocalProfileRepository.TrySave`; it never mutates
`repository.Snapshot` in place, so the repository only holds a snapshot that passed the same save
path as every later commit. The backend has no Loadout slot limit, so a Loadout over
`LocalProfileSnapshot.MaxLoadoutSlots` is accepted and persisted: slot capacity is a Store rule, not
a codec rule. Store operations that need a new Loadout slot are refused (`InventoryFull`), so the
excess can only shrink, and `TryCreateLoadoutReservation` refuses an over-capacity Loadout with
`InventoryFull` so the excess never enters a Raid. The Town Inventory read source still reports an
over-capacity Loadout as unavailable; the Stash screen is where the excess is moved out.

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
`LootId + Amount` unit from Inventory to its Equipment slot, while Unequip moves it back;
`EquipmentSlotRules` decides which slot an identity may occupy and weapon slots additionally require
a usable Weapon definition. A weapon assignment also evaluates its `WeaponAttributeRequirements` against the
confirmed `CharacterAttributeState` already owned by the aggregate. The same pure eligibility
rule is rechecked before preparation, reservation and rollback; armors have no attribute
requirements during the MVP. Ordinary Town Inventory Equip reads only Inventory. The Stash screen
also supports direct Stash Equip through `EvaluateEquipFromStash` / `TryEquipFromStash`: both share
one planner over an isolated snapshot clone. It removes exactly one Stash unit, returns displaced
units (including same-ID replacement and both hands for a two-handed weapon) to Loadout, and checks
final distinct-ID capacity before any commit. A full 30-ID Loadout can equip into an empty slot or
merge displaced units into existing IDs, but cannot accept a new displaced ID. Backend-hydrated
excess remains accepted; this operation cannot grow that excess. Evaluation publishes nothing;
application replans against current state and commits the whole candidate once.

The Stash coordinator checks the Ready gate, availability and Store rules, then calls
`MoveToLoadout(id, 1)`, then `UpdatePreparedEquipment(projected)`, then the atomic local application.
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
Extraction receipts and the rest of the aggregate remain available only for the current
application process until backend persistence is connected.

Schema version 4 persists this Weapon Set contract. Schema 3 maps historical Weapon Slot 1 to Set A
Main Hand and Weapon Slot 2 to Set B Main Hand; both Off Hands start empty. Pending reservations use
the same migration. Earlier schemas did not distinguish
the former exclusive implementation from the temporary non-owning implementation. Decoding treats
a complete Inventory reference set as the latter and subtracts one unit per prepared slot; if the
complete set is absent, it preserves Inventory unchanged as an earlier exclusive save. The check is
all-or-nothing, and new saves always write schema 4.

TASK-330 originally kept this migration local. The current prepared-Equipment adapter sends all
eight slots: `weaponSlot1` / `weaponSlot2` map to Set A / B Main Hand, `offHand1` / `offHand2`
map to their Off Hands, alongside Helmet, Armor, Gloves and Boots. The Stash flow above submits
that projected eight-slot value before its local ownership commit. Reservation transport and
recovery have their own contracts and are not changed by U5.

This structure does not yet implement shield defence, Dual Wield attacks, accessories or Quick Slots.
Those behaviors require their own rules without adding a competing Equipment source of truth.

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

`TryPrepareExpeditionEquipment` runs immediately before the reservation boundary and is the only
place that may normalize the local Loadout and prepared Equipment. Only the weapon guarantee is
normalized: prepared armor is optional, is never granted and is never rewritten. It is atomic,
deterministic and idempotent, so retrying a launch never grants or duplicates anything:

* A valid weapon in Set A Main Hand is left untouched and commits nothing.
* When only Set B Main Hand is occupied, preparation leaves both Sets unchanged and admission marks
  Set B active.
* When no weapon is prepared, Town grants exactly one configured recovery weapon
  (`LocalProfilePersistenceConfiguration.RecoveryWeaponLootId`), reusing a unit the profile
  already owns before minting the guaranteed one. Without that configuration the preparation is
  rejected as `RecoveryWeaponUnavailable`; it never falls back to another weapon.
* A persisted assignment that no longer resolves to a usable weapon fails explicitly as
  `InvalidPreparedWeapon` without mutating the aggregate. Corruption is never overwritten and
  never hidden behind a recovery grant.
* A prepared weapon whose requirements exceed the confirmed attributes fails explicitly as
  `AttributeRequirementsNotMet`; no assignment, ownership or reservation state is changed.

Raid never grants a recovery weapon. Preparation is inert while a reservation is pending.

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
one rollback. After participant, avatar and exact `Inventory + the eight Equipment slots`
ownership are observed, confirmation consumes the reservation. Closing the application currently
discards an unfinished reservation; backend persistence must define recovery for remotely stored
reservations.

### Extraction boundary

State Authority retains the raid result snapshot while Input Authority commits it to its
application-level Loadout. A valid admission consumes the pending reservation and leaves
that Loadout empty during the raid; extraction then restores the exact authoritative raid
snapshot to it. Stash is not an automatic extraction destination. A new receipt is accepted
only when the Loadout is empty and fits its capacity, so an unexpected pre-existing Loadout
fails without changing either inventory. An ACK is sent only after the current aggregate accepts
the transaction, and the raid inventory is cleared only after the ACK. Transport duplicates cannot
duplicate loot during the current process because extraction receipts remain in the aggregate.
Cross-process idempotency requires backend receipt persistence before this can be considered a
durable guarantee.

## Temporary limitations and failure policy

- Closing or crashing the application discards the process-local aggregate.
- Legacy JSON and `PlayerPrefs` state are ignored and never block login.
- A backend load failure must remain visible and must not be replaced by a generated identity or
  legacy local profile.
- Durable progression and extraction acknowledgement require backend-confirmed, idempotent
  operations. In-memory success is temporary gameplay evidence only.

## Validation strategy

EditMode tests verify the process-local lifetime, profile boundaries, progression watermark
semantics and existing aggregate transactions. Codec and filesystem tests remain isolated coverage
for inactive legacy infrastructure. Manual validation covers login with legacy files present and a
complete Town-Raid-Town cycle in one run. Backend integration must add restart hydration, network
failure/retry, revision-conflict, receipt-idempotency and real Host/Client validation.
