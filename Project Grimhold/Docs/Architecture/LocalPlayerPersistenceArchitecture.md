# Local Player Persistence Architecture

## Context

Project Grimhold currently needs stash and loadout state to survive scene changes and
`NetworkRunner` replacement during one Town-Raid-Town cycle. Durable progression across
application restarts is intentionally deferred until the persistence system is developed
further. Loading an older local save must not prevent a multiplayer raid from starting.

## Decision

The application owns one profile aggregate identified by `ProfileId` for the lifetime of
the current process. `ApplicationStashServiceBootstrapper` creates a single
`ApplicationStashContext` with `InMemoryLocalProfileRepository` before the first scene.
The context is marked `DontDestroyOnLoad`, so stash, loadout, prepared weapon assignments,
pending loadout reservation and extraction receipts survive Town-Raid-Town transitions.

The aggregate starts empty on every application launch and is discarded when the process
closes. The active composition does not read or write `Application.persistentDataPath`.
Existing `grimhold-profile.json` files and the previous `PlayerPrefs` identity are ignored.
`LocalProfileProvider` generates one `ProfileId` per application process. This value is
stable across runner and scene transitions, but a new process receives a new value. As a
result, multiple standalone processes under the same operating-system account remain
distinct Town queue members.

```text
IPlayerStashService / IPlayerLoadoutService
                -> LocalProfileStore
                -> ILocalProfileRepository
                -> InMemoryLocalProfileRepository
```

`LocalProfileStore` remains the transactional domain boundary. It validates mutations,
serializes operations and publishes one profile-commit notification after the in-memory
snapshot accepts a complete aggregate. The repository validates each replacement and
keeps its own snapshot for the remainder of the process.

The versioned codec, filesystem repository and atomic file store remain isolated behind
`ILocalProfileRepository`, but they are not part of the runtime composition. They may be
revisited or replaced when durable persistence receives an approved design.

## Domain and authority boundaries

Stash and loadout transfers, prepared weapon assignments, loadout reservations and extraction
receipt application are complete aggregate commits. Prepared Weapon Slot 1 and Slot 2 are
non-owning `LootId` references into the current Loadout: they never create units, each reference
must resolve to a valid Weapon, and using one identity twice requires at least two owned units.
Loadout removals reconcile the assignments in the same commit. Duplicate extraction receipts remain idempotent within the
current application process. Closing the application resets both the loot and the receipt
history, as intended by the temporary lifetime policy.

Fusion may carry `ProfileId` and session snapshots for the active runner, but it does not
own the local stash or loadout. A raid Host never reads another client's local aggregate.
`PlayerRef` is not a gameplay-persistence identity.

### Expedition preparation boundary

`TryPrepareExpeditionWeapons` runs immediately before the reservation boundary and is the only
place that may normalize the local Loadout and prepared Weapon Equipment. It is atomic,
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
the complete local Loadout plus both prepared assignments into `PendingLoadoutReservation`
before the Town queue ACK. The active Town Loadout and its assignments are then empty and cannot
ambiguously reference units already reserved for Raid. The requirement stays enforced here as a
domain invariant even though preparation already guaranteed it. The same reservation id is idempotent; a
different id is rejected while pending. Pre-admission failure restores items and assignments in
one rollback. After participant, avatar and exact `Inventory + Weapon Slot 1 + Weapon Slot 2`
ownership are observed, confirmation consumes the reservation.
All of these guarantees apply while the application remains open; an application close
discards an unfinished reservation.

### Extraction boundary

State Authority retains the raid result snapshot while Input Authority commits it to its
application-level Loadout. A valid admission consumes the pending reservation and leaves
that Loadout empty during the raid; extraction then restores the exact authoritative raid
snapshot to it. Stash is not an automatic extraction destination. A new receipt is accepted
only when the Loadout is empty and fits its capacity, so an unexpected pre-existing Loadout
fails without changing either inventory. An ACK is sent only after that in-memory commit
succeeds, and the raid inventory is cleared only after the ACK. Transport duplicates cannot
duplicate loot during the same application run because extraction receipts remain in the
aggregate.

## Risks and deferred durability

- Closing or crashing the application discards stash, loadout and secured extraction loot.
- Reopening the application permits the same raid receipt to be applied again because no
  receipt history survives the process boundary.
- The dormant file implementation is not evidence that restart persistence is supported.
- Durable storage requires a separate decision covering migration, corruption recovery,
  identity, save compatibility and multiplayer acknowledgement semantics.

## Validation strategy

EditMode tests verify that one in-memory repository retains committed state while a new
repository starts empty, and that profile boundaries are enforced. Existing codec and
filesystem-repository tests remain as isolated coverage for the dormant implementation.
Store tests continue to cover transactions, reservations, extraction idempotency and
failure behavior. Manual validation must cover a complete Town-Raid-Town cycle in one run
and confirm that a full application restart begins with an empty stash and loadout.
