# Host Migration recovery-only multi-client

## Context and scope

Host Migration is a recovery path for an abrupt Host peer loss during a Raid. It
is entered only from Fusion's `OnHostMigration` callback while the source
`NetworkMatchController` is exactly `InProgress`. A crash received while the Raid
was already `Closing` or `Finished` is intentionally outside this stage.

Return, Abandon, extraction Return, cancellation, and voluntary shutdown never
start Host Migration. An operational connected Host cannot personally leave the
Raid through those paths.

## Authority and routing

Every process owns a local 65-second lifecycle budget beginning at its own
`OnHostMigration`; it is not a synchronized cross-process timestamp. Startup,
scene replacement, and snapshot restore may use at most the first 30 seconds.
The authoritative recovery window may remain open for up to 30 seconds after
restore, within the remaining local budget.

The replacement role is derived from the started runner, not assumed from the
old peer:

```text
GameMode.Host + IsServer     -> RecoveredAsHost
GameMode.Client + !IsServer -> RecoveredAsClient
anything else               -> failure
```

Only `RecoveredAsHost` receives `HostMigrationSnapshotRestorer` as its resume
callback and publishes authoritative completion. `RecoveredAsClient` never runs
the restorer or server completion. It waits for its replicated local
`PlayerObject`, durable `ProfileId`, Raid generation, and authorities, then
adopts the replacement runner.

## Sources of truth and ownership

`NetworkRunnerFactory` initializes `NetworkSpawnManager` before `StartGame`.
Consequently that runner-scoped manager owns the entire recovery roster,
including arrivals received before the restored `NetworkMatchController` exists:

- `ProfileId -> new PlayerRef` early arrivals;
- `ProfileId -> restored participant NetworkObject`;
- eligible and unresolved profile sets;
- `ProfileId -> current PlayerRef` recovered mappings;
- terminal/no-rejoin profiles for the restored Raid generation;
- recovery state `Inactive -> Open -> Sealing -> Closed`, or `Failed`.

`HostMigrationLifecycleController` owns only asynchronous orchestration, local
deadlines, role routing, cleanup, and final launcher adoption. Stable identity is
always `ProfileId`; an old `PlayerRef` is diagnostic snapshot data only.

Normal admission and recovery admission are disjoint. A valid early arrival is
queued without creating a participant or avatar. After restore it is validated
against the frozen cohort, restored generation, participant state, and terminal
policy before its existing participant is assigned Input Authority and becomes
its `PlayerObject`. No recovery fallback executes fresh bootstrap.

## Recovery eligibility

Eligible survivors are frozen-cohort profiles whose restored participant is
non-terminal `Raiding` or `Defeated`. The historical Host, Controlled Return,
`Extracted`, `Aborted`, Return-authorized, and known terminal profiles are never
eligible. The promoted Host's own local profile uses the same rebind path as any
other survivor.

During `Open`, an unexpected `PlayerLeft` invalidates the recovered mapping,
clears participant/avatar authority, and returns that profile to unresolved. It
may rebind the same restored objects again only within this recovery window.

## Atomic roster sealing

Sealing is one synchronous runner-thread boundary with no `await` between its
authoritative operations:

1. transition `Open -> Sealing` and reject new arrivals;
2. reconcile recovered mappings against current `Runner.ActivePlayers` and
   current `PlayerObject` mappings;
3. invalidate absent mappings and terminalize every unresolved participant;
4. validate the final active roster and gameplay authorities;
5. clear early/pending state, transition to `Closed`, and publish completion.

A callback observed during `Sealing` may still invalidate a recovered mapping.
The final validation queries current runner state rather than trusting a roster
copy created before sealing.

## Unrecovered finalization

An unrecovered `Raiding` participant takes the Host-Migration-specific
`Raiding -> Aborted` route. It does not set `IsReturnAuthorized`, does not invoke
Return or persistence, clears authority/routing, records terminal/no-rejoin, and
despawns its avatar and participant.

An unrecovered `Defeated` profile loses its participant, `PlayerObject`, routing,
and Input Authority and becomes terminal/no-rejoin. The independent defeated
avatar/corpse NetworkObject and its `NetworkLootContainer` remain authoritative;
only the separate `NetworkRaidParticipant` is despawned. The historical Host is
finalized by the same state-dependent rules and never blocks sealing.

After completion, every remaining `Raiding` participant has an active peer,
correct `PlayerObject`, and avatar Input Authority. No unrecovered participant is
operational or spectator-eligible.

## Match closure and restore protection

Natural completion is suspended while recovery is `Open` or `Sealing`. Sealing
restores the non-networked participant-observation latch from the snapshot and
then reenables normal evaluation. A Raid with recovered `Raiding` participants
continues `InProgress`. A sealed roster with none may legitimately transition to
`Closing`/`Finished` only with `NaturalCompletion`; that post-recovery phase is
not treated as migration failure.

`SessionStartupContext.HostMigrationResume` remains the no-bootstrap boundary.
Restored objects use `CopyStateFrom`, reference fixups, and restore guards in
state-writing `Spawned()` paths, including extraction sanctuary/zone and traps.

## Validation boundary

EditMode tests cover role routing, startup suppression, completion semantics, and
eligibility policy. PlayMode must cover defeated-participant despawn while corpse
and loot remain registered and interactable. The decisive acceptance test is a
fresh multi-process Host plus at least two Clients, with one Client promoted to
Host and the other adopting as Client. Single Runner validation is not evidence
of real Host Migration behavior.
