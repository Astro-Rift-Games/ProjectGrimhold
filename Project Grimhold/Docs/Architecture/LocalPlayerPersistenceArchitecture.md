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

### Progression transaction

`LastAppliedProgressionResultSequence` is the aggregate's at-most-once mechanism. A new
`ProgressionReceipt` is accepted only at `watermark + 1`; the exact receipt at the current
watermark is `AlreadyApplied`, a different payload at that sequence is `Conflict`, an older
sequence is `Stale`, and a sequence gap is invalid. The bounded receipt list is audit history
only and may be pruned without weakening rejection of an old result.

Level, current Experience, watermark, `LastProgressionReceipt` and receipt history are one
candidate mutation. Watermark zero requires no last receipt. A positive watermark requires
the last receipt to exist, match that sequence and belong to the snapshot ProfileId.
`LocalProfileStore` reuses `ConsolidatedExperienceApplicationRules`; it does not duplicate the
curve. A zero-XP resolution still advances the watermark. Only `Success` publishes a
commit event; `AlreadyApplied` performs no second write or notification.

## Domain and authority boundaries

Stash and loadout transfers, prepared Equipment assignments, loadout reservations and extraction
receipt application are complete aggregate transactions. `PreparedEquipmentLoadout` covers the six
slots of `EquipmentSlot`: the two weapon quick slots plus Helmet, Armor, Gloves and Boots. Every
assignment is a non-owning `LootId` reference into the current Loadout: it never creates units,
`EquipmentSlotRules` decides which slot an identity may occupy, weapon slots additionally require
a usable Weapon definition, and one identity used by several slots requires one owned unit per
reference. Equipping an identity that still lives in the Stash pulls exactly the missing units
into the Loadout inside the same commit, because the Loadout is what the reservation transfers; a
rejected assignment moves nothing. Releasing a slot leaves its unit in the Loadout. Loadout
removals reconcile the assignments in the same transaction, releasing the last slots first.
Extraction receipts and the rest of the aggregate remain available only for the current
application process until backend persistence is connected.

Fusion may carry `ProfileId` and session snapshots for the active runner, but it does not
own the local stash or loadout. A raid Host never reads another client's local aggregate.
`PlayerRef` is not a gameplay-persistence identity.

### Expedition preparation boundary

`TryPrepareExpeditionEquipment` runs immediately before the reservation boundary and is the only
place that may normalize the local Loadout and prepared Equipment. Only the weapon guarantee is
normalized: prepared armor is optional, is never granted and is never rewritten. It is atomic,
deterministic and idempotent, so retrying a launch never grants or duplicates anything:

* A valid weapon in Weapon Slot 1 is left untouched and commits nothing.
* When only Weapon Slot 2 is occupied, the effective selection is normalized towards Slot 1.
  Both assignments are non-owning references, so no unit moves.
* When no weapon is prepared, Town grants exactly one configured recovery weapon
  (`LocalProfilePersistenceConfiguration.RecoveryWeaponLootId`), reusing a unit the profile
  already owns before minting the guaranteed one. Without that configuration the preparation is
  rejected as `RecoveryWeaponUnavailable`; it never falls back to another weapon.
* A persisted assignment that no longer resolves to an owned, usable weapon fails explicitly as
  `InvalidPreparedWeapon` without mutating the aggregate. Corruption is never overwritten and
  never hidden behind a recovery grant.

Raid never grants a recovery weapon. Preparation is inert while a reservation is pending.

### Loadout reservation boundary

`TryCreateLoadoutReservation` requires at least one valid prepared weapon and atomically moves
the complete local Loadout plus its six prepared assignments into `PendingLoadoutReservation`
before the Town queue ACK. The active Town Loadout and its assignments are then empty and cannot
ambiguously reference units already reserved for Raid. The requirement stays enforced here as a
domain invariant even though preparation already guaranteed it. The same reservation id is idempotent; a
different id is rejected while pending. Pre-admission failure restores items and assignments in
one rollback. After participant, avatar and exact `Inventory + the six Equipment slots`
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
