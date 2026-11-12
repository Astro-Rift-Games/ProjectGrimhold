# Raid Generation Lifecycle Architecture

## Context

Each Host/Client Raid owns one Fusion `NetworkRunner`. The Raid lifecycle closes that
generation without touching Town, profile, stash or loadout state. `RaidId` from
the launch manifest is the generation identity; direct development sessions create
one equivalent local identifier.

## Decision

`NetworkMatchController` remains the sole authoritative lifecycle owner. It closes
admission, rejects new gameplay spawns, waits for pending extraction persistence,
asks `NetworkSpawnManager` to perform idempotent runner-scoped cleanup, and then
publishes a retained Results state. It never orders a Town transition. A different
runner is created for the next raid only after an explicit return request is consumed.

The lifecycle is:

```text
WaitingForPlayers (Gameplay prepared; no initial PvPvE generation)
  -> Starting / one-time bootstrap
  -> InProgress
InProgress
  -> Closing / AwaitingPersistence
  -> Cleaning
  -> Finished / ResultsRetained

Starting / bootstrap failure
  -> Closing / AwaitingPersistence
  -> Cleaning
  -> Finished / ResultsRetained
```

`Start Raid` is authoritative and closes the session before invoking
`NetworkSpawnManager`'s one-time bootstrap API. A failed bootstrap uses
`RaidClosureReason.BootstrapFailure`; it does not pass through `InProgress` and
cleanup remains idempotent.

No participant, including the Host, can cancel the Raid (Cancel Raid was removed by FIX-49).
A Host abandonment, Defeat or extraction is an individual participant result. When the Host then
returns while another participant is still `Raiding`, the Host role is handed to that participant
through Host Migration and the Raid continues in the same generation
(`HostMigrationRecoveryArchitecture.md#voluntary-host-exit-handoff`); when none remains, the Raid
closes normally with `NaturalCompletion`.

## Ownership and boundaries

- `NetworkMatchController`: replicated phase, closure reason, generation identity,
  persistence barrier and cleanup diagnostics; State Authority only.
- `NetworkSpawnManager`: runner-scoped participant and connectivity queries, spawn
  blocking and one-shot cleanup of gameplay world objects while retaining participants,
  Results avatars, player routing and Controlled Return state.
- `NetworkRaidParticipant`: authoritative participant result and generation ID;
  no stash or loadout mutation during abort/cleanup.
- `SessionConnectionCoordinator`: never translates `Finished` directly into Town.
  Clients retain their individual Controlled Return path. The Host records an explicit
  request and consumes it only after `OnPlayerLeft`/runner connectivity confirms that no
  remote peer remains.
- `PlayerExtractionLootSaver`: the only source of extraction commit confirmation. A pending commit
  prevents cleanup and is never silently discarded.

Runner scope is the isolation boundary: cleanup cannot affect another raid because
the next generation uses a new runner. The retained Results cleanup is idempotent and
does not clear participant routing before `OnPlayerLeft`; shutdown performs the final
runner-scoped cleanup. Fusion Host Migration remains valid only while the phase is
`InProgress`; a closing generation is not resumed.

## Failure and validation

Cleanup attempts every object and records failures. A partial cleanup publishes a
diagnostic failure and keeps the session closed. Persistence failures remain in the
pending barrier until `PlayerExtractionLootSaver` retries or confirms the transaction.

Validation must distinguish compilation, EditMode, PlayMode and manual Host/Client
evidence. Two consecutive raids must not retain objects, seeds, registry entries or
temporary progress from the first runner.
