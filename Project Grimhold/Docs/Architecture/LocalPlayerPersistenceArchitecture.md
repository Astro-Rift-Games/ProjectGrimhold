# Local Player Persistence Architecture

## Context

Project Grimhold keeps the complete local character profile durable across scenes,
`NetworkRunner` replacement and application restarts. Backend persistence and local/remote
synchronization remain outside this architecture.

## Decision

The application owns one aggregate identified by the stable `ProfileId` stored under the
existing `grimhold_profile_id` PlayerPrefs key. `ApplicationStashServiceBootstrapper` creates
one `ApplicationStashContext` and composes `LocalProfileRepository` over
`LocalProfileFileStore` in `Application.persistentDataPath`. The context remains
`DontDestroyOnLoad`; the repository reloads the same aggregate after an application restart.

```text
IPlayerStashService / IPlayerLoadoutService
                -> LocalProfileStore
                -> ILocalProfileRepository
                -> LocalProfileRepository
                -> ILocalProfileFileStore
                -> LocalProfileFileStore
```

`LocalProfileStore` remains the transactional application boundary. It builds a candidate
snapshot, asks the repository to save it, and publishes the observable replacement and
`ProfileCommitted` only after the complete save succeeds. It never mutates the current
snapshot and rolls back after failure.

`LocalProfileSaveCodec` owns schema, serialization, validation and migration. Schema 2 adds
persistent Level, current per-level Experience and progression idempotency state; schema 1
migrates to Level 1, Experience 0 and watermark 0. Invalid progression state fails decode,
which lets the repository use its normal primary-to-backup recovery. Future schemas block
loading and writes. `LocalProfileFileStore` remains domain-agnostic and owns only temporary,
primary, backup, atomic replacement and physical restore operations.

### Durable progression commit

`LastAppliedProgressionResultSequence` is the at-most-once mechanism. A new
`ProgressionReceipt` is accepted only at `watermark + 1`; the exact receipt at the current
watermark is `AlreadyApplied`, a different payload at that sequence is `Conflict`, an older
sequence is `Stale`, and a sequence gap is invalid. The bounded receipt list is audit history
only and may be pruned without weakening rejection of an old result.

Level, current Experience, watermark, `LastProgressionReceipt` and receipt history are one
candidate mutation. Watermark zero requires no last receipt. A positive watermark requires
the last receipt to exist, match that sequence and belong to the snapshot ProfileId.
`LocalProfileStore` reuses `ConsolidatedExperienceApplicationRules`; it does not duplicate the
curve. A zero-XP resolution still advances the durable watermark. Only `Success` publishes a
commit event; `AlreadyApplied` performs no second write or notification.

## Domain and authority boundaries

Stash and loadout transfers, prepared Equipment assignments, loadout reservations and extraction
receipt application are complete aggregate commits. `PreparedEquipmentLoadout` covers the six
slots of `EquipmentSlot`: the two weapon quick slots plus Helmet, Armor, Gloves and Boots. Every
assignment is a non-owning `LootId` reference into the current Loadout: it never creates units,
`EquipmentSlotRules` decides which slot an identity may occupy, weapon slots additionally require
a usable Weapon definition, and one identity used by several slots requires one owned unit per
reference. Equipping an identity that still lives in the Stash pulls exactly the missing units
into the Loadout inside the same commit, because the Loadout is what the reservation transfers; a
rejected assignment moves nothing. Releasing a slot leaves its unit in the Loadout. Loadout
removals reconcile the assignments in the same commit, releasing the last slots first. Extraction
receipts and the rest of the aggregate remain durable across application restarts.

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
ownership are observed, confirmation consumes the reservation. If the application closes with
an unfinished reservation, the next bootstrap rolls it back through the same aggregate operation.

### Extraction boundary

State Authority retains the raid result snapshot while Input Authority commits it to its
application-level Loadout. A valid admission consumes the pending reservation and leaves
that Loadout empty during the raid; extraction then restores the exact authoritative raid
snapshot to it. Stash is not an automatic extraction destination. A new receipt is accepted
only when the Loadout is empty and fits its capacity, so an unexpected pre-existing Loadout
fails without changing either inventory. An ACK is sent only after that durable commit
succeeds, and the raid inventory is cleared only after the ACK. Transport duplicates cannot
duplicate loot because extraction receipts remain in the durable aggregate.

## Failure and recovery

- A failed atomic write leaves the observable repository snapshot unchanged and publishes no
  profile commit event.
- Invalid primary data falls through to the existing backup decode and physical restore path.
- An unsupported future schema blocks loading and subsequent writes instead of overwriting data.
- Backend durability, account-level identity and local/remote conflict resolution remain deferred.

## Validation strategy

EditMode tests verify schema migration, codec validation, primary/backup recovery, candidate-before-
save behavior, progression watermark semantics and existing aggregate transactions. Manual
validation covers application restart, Town-Raid-Town, a second Raid using the newly persisted
baseline, persistence failure/retry and real Host/Client plus Host Migration behavior.
