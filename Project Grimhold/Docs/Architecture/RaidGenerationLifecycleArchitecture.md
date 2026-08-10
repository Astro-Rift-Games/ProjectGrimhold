# Raid Generation Lifecycle Architecture

## Context

Each Host/Client raid owns one Fusion `NetworkRunner`. `TASK-60` closes that
generation without touching Town, profile, stash or loadout state. `RaidId` from
the launch manifest is the generation identity; direct development sessions create
one equivalent local identifier.

## Decision

`NetworkMatchController` remains the sole authoritative lifecycle owner. It closes
admission, rejects new gameplay spawns, waits for pending extraction persistence,
asks `NetworkSpawnManager` to perform idempotent runner-scoped cleanup, and then
publishes the return order. A different runner is created for the next raid.

The lifecycle is:

```text
InProgress
  -> Closing / AwaitingPersistence
  -> Cleaning
  -> Finished / ReturnOrdered
```

Host cancellation marks only `Raiding` participants as `Aborted`. An individual
Host abandonment remains a participant result: if another participant is still
`Raiding`, the existing Host Migration flow continues the raid.

## Ownership and boundaries

- `NetworkMatchController`: replicated phase, closure reason, generation identity,
  persistence barrier and cleanup diagnostics; State Authority only.
- `NetworkSpawnManager`: runner-scoped participant queries, spawn blocking and
  cleanup of all network objects except the match coordinator.
- `NetworkRaidParticipant`: authoritative participant result and generation ID;
  no stash or loadout mutation during abort/cleanup.
- `SessionConnectionCoordinator`: observes `Finished`; Clients return immediately,
  while the Host waits for the configured five-second grace before its normal
  shutdown path.
- `TASK-80`: the only source of extraction commit confirmation. A pending commit
  prevents cleanup and is never silently discarded.

Runner scope is the isolation boundary: cleanup cannot affect another raid because
the next generation uses a new runner. Fusion Host Migration remains valid only
while the phase is `InProgress`; a closing generation is not resumed.

## Failure and validation

Cleanup attempts every object and records failures. A partial cleanup publishes a
diagnostic failure and keeps the session closed. Persistence failures remain in the
pending barrier until TASK-80 retries or confirms the transaction.

Validation must distinguish compilation, EditMode, PlayMode and manual Host/Client
evidence. Two consecutive raids must not retain objects, seeds, registry entries or
temporary progress from the first runner.
