# Local Player Persistence Architecture

## Context

Project Grimhold has one local player profile per installation. Stash and
loadout are application data and must survive scene changes, NetworkRunner
replacement and application restarts. Fusion state belongs only to the active
session and must never become the persistent identity source.

## Decision

The application owns one versioned local aggregate identified by `ProfileId`.
`PlayerPrefs` stores only the existing local identity. The aggregate is stored
as JSON under `Application.persistentDataPath` and contains schema version,
profile ID, stash, loadout, one pending loadout reservation and the most recent
256 applied extraction receipts.

The persistence boundary is split into three layers:

```text
IPlayerStashService / IPlayerLoadoutService
                -> LocalProfileStore
                -> ILocalProfileRepository
                -> ILocalProfileFileStore
```

`LocalProfileStore` owns the in-memory snapshot, validates domain mutations,
serializes operations and publishes one profile-commit notification after a
successful durable write. The repository loads and saves the complete
aggregate. The file store owns paths and atomic filesystem operations and is
replaceable in EditMode tests.

## Format and durability

Schema version 1 is encoded with Unity `JsonUtility` DTOs. A save is written to
`grimhold-profile.json.tmp`, closed, decoded and validated, then atomically
replaces `grimhold-profile.json` while retaining
`grimhold-profile.json.bak`. A failed write leaves the last valid main file and
the in-memory snapshot unchanged.

The main file may be recovered from a valid backup when it is malformed. The
recovered state is reported and the main file is repaired without replacing the
valid backup with the corrupt file. A future/unsupported schema never falls
back to an older version. If no valid state exists, persistence becomes
read-only and operations fail clearly; invalid data is never replaced by an
empty profile silently.

## Domain and authority boundaries

Stash and loadout transfers, loadout reservations and extraction receipt
application are complete aggregate commits. Duplicate extraction receipts are
idempotent and do not publish another change. Reservation primitives are local
storage capabilities for TASK-79; this task does not attach them to the raid
transition ticket. Extraction commits are local storage capabilities for
TASK-80; this task does not add the network ACK/RPC flow.

Fusion may carry `ProfileId` and session snapshots for the active runner, but it
does not read another player's local files and never owns persistent stash or
loadout state. `PlayerRef` is not persisted.

## Validation strategy

Pure codec, repository and store behavior is covered by EditMode tests using an
injected file store. PlayMode tests verify bootstrap lifetime, context
replacement and presenter subscription cleanup. A final build check verifies
that data survives a complete application restart through
`Application.persistentDataPath`.
