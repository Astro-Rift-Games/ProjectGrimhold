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
The context is marked `DontDestroyOnLoad`, so stash, loadout, pending loadout reservation
and extraction receipts survive Town-Raid-Town transitions.

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

Stash and loadout transfers, loadout reservations and extraction receipt application are
complete aggregate commits. Duplicate extraction receipts remain idempotent within the
current application process. Closing the application resets both the loot and the receipt
history, as intended by the temporary lifetime policy.

Fusion may carry `ProfileId` and session snapshots for the active runner, but it does not
own the local stash or loadout. A raid Host never reads another client's local aggregate.
`PlayerRef` is not a gameplay-persistence identity.

### Loadout reservation boundary

`TryCreateLoadoutReservation` moves the complete local loadout, including an empty
snapshot, into `PendingLoadoutReservation` before the Town queue ACK. The same reservation
id is idempotent; a different id is rejected while pending. Pre-admission failure rolls it
back. After participant, avatar and exact inventory are observed, confirmation consumes it.
All of these guarantees apply while the application remains open; an application close
discards an unfinished reservation.

### Extraction boundary

State Authority retains the raid result snapshot while Input Authority commits it to its
application-level aggregate. An ACK is sent only after that in-memory commit succeeds, and
the raid inventory is cleared only after the ACK. Transport duplicates cannot duplicate
loot during the same application run because extraction receipts remain in the aggregate.

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
