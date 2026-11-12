# Host Migration recovery-only multi-client

## Context and scope

Host Migration is the single mechanism that moves the Raid Host role to another
participant while a Raid is active. A Raid never depends on one person: when the
Host leaves the Dungeon, voluntarily or not, the role passes to a connected
participant who is still inside it (decision of 2026-11-01, FIX-US-08). It is
entered only from Fusion's `OnHostMigration` callback while the source
`NetworkMatchController` is exactly `InProgress`. A crash received while the Raid
was already `Closing` or `Finished` is intentionally outside this stage.

Two triggers share the replacement flow specified below:

- **Abrupt Host loss** (crash, kill, network loss): recovers from Fusion's latest
  snapshot (automatic every 10 s, `HostMigration.EnableAutoUpdate`).
- **Voluntary Host exit** (Return after Defeat or Results, Return after a completed
  extraction, or Abandon followed by Return): the outgoing Host resolves its own
  participation first, then hands the role off. See
  [Voluntary Host exit](#voluntary-host-exit-handoff).

There is no Host-only exit and no Cancel Raid: the Host uses the standard Abandon
and Results Return of any participant. `RaidClosureReason.HostCancellation`,
`NetworkMatchController.TryCancelRaid` and the Cancel Raid menu action were removed
(FIX-49); the `RaidClosureReason` byte value `1` stays reserved so networked values
keep their meaning. A plain runner shutdown that is not a handoff does not migrate the
Raid: Photon closes the room and the Clients leave (FIX-48 report, section 1).

## Voluntary Host exit (handoff)

The Host resolves its participation through the same server paths as a Client
(corpse generation, loot settlement, progression result and commit). Only then does
the role move, so the outgoing Host loses nothing it would not lose as a Client.

1. **Requests.** `NetworkRaidParticipant.RPC_RequestAbandon` and `RPC_RequestReturn`
   use `RpcHostMode.SourceIsHostPlayer`, so a call made by the Host on its own
   participant reports the Host player as `RpcInfo.Source` (the default
   `SourceIsServer` reports `PlayerRef.None` and `TryResolveReturnRequester` would
   reject it). `HostHandoffPolicy.AcceptsExitRequest` admits the operational Host
   only while the Raid is `InProgress`; `HostHandoffPolicy.CanAuthorizeReturn`
   defers a Host Return after a voluntary Abandon until the progression result is
   committed.
2. **Routing.** While the Raid is `InProgress` and at least one remote participant is
   connected and `Raiding` (`NetworkSpawnManager.CountConnectedRaidingRemoteParticipants`),
   the Host's Results Return goes through the participant Return RPC
   (`HostHandoffPolicy.ShouldRouteHostReturnThroughParticipant`,
   `RaidResultsReturnPolicy.CanRequestHostHandoffReturn`). Abandon alone never starts
   the handoff: the Host sees the loss Results like any participant and the handoff
   starts when it presses `Volver`.
3. **Resolved exit.** The Host's exit is resolved when its own participant has
   `IsReturnAuthorized` (`NetworkSpawnManager.IsLocalHostExitResolved`).
   `SessionConnectionCoordinator.ObserveHostHandoff` then calls
   `AttemptHostHandoffAsync` -> `HostHandoffService.TryHandOffAsync`, which asks
   `HostHandoffPolicy.Decide`:
   - `HandOff`: at least one connected `Raiding` participant remains. The Host pushes
     a snapshot (`PushHostMigrationSnapshot`) and, only if Photon accepts it, shuts the
     runner down with `ShutdownReason.HostMigration`
     (`FusionHostHandoffRunner`). Fusion promotes a Client and the replacement flow
     below runs.
   - `CloseNormally`: no `Raiding` participant remains. There is no handoff; the Raid
     closes through the normal `NaturalCompletion` path and the Host leaves through
     the Results Return that waits for `Finished` and for remote peers (see
     [Lobby and Session Transition](LobbyAndSessionTransitionArchitecture.md)).
   - `Stay`: not the server, not `InProgress`, or the exit is not resolved yet.
   A rejected snapshot (`HostHandoffOutcome.SnapshotRejected`) is never a handoff: the
   Host keeps the role, retries after 3 s, and the room is never closed for the others.
4. **Outgoing peer.** `SessionConnectionCoordinator.ShouldRecoverRaidShutdown` is told
   the shutdown is the peer's own handoff (`isOutgoingHostHandoff`), so only the
   outgoing Host recovers to Town, immediately and without waiting for anyone. Every
   other peer keeps suppressing Town recovery because the replacement flow owns the
   runner.
5. **Roster.** The replacement Host classifies the historical Host with
   `HostMigrationRosterPolicy.ClassifyHistoricHost`, from replicated state only: a
   participant with `IsReturnAuthorized`, or `Aborted` with
   `VoluntaryAbandonConfirmed`, is `DepartedTerminal` (so a Host that confirmed Abandon and
   dropped before pressing `Volver` is also released); anything else is `Unresolved`.
   A departed-terminal Host is marked terminal/no-rejoin, never re-finalized as
   `Aborted` and never persisted again. `ReleaseDepartedTerminalHostMigrationProfiles`
   despawns only its `NetworkRaidParticipant` and clears the body's Input Authority;
   its corpse stays lootable.
6. **Announcement.** After adoption, `HostMigrationLifecycleController` publishes
   `HostHandoffAnnouncement.Format(...)` through `HostHandoffNoticeSlot`, and
   `RaidDownedHudPresenter` shows it to the remaining participants:
   `{Nombre} salió. {Nuevo} es el nuevo Host.`. Names come from the replicated
   `NetworkRaidParticipant.DisplayName` (set by State Authority at admission from the
   backend-signed ticket and sanitized by `RaidDisplayNamePolicy`); when unavailable the
   text falls back to `El Host` / `Otro jugador`. The notice is published for every
   completed migration, including abrupt loss.

The operational Host is runner-scoped. `NetworkSpawnManager.TryResolveReturnRequester`
derives it from the State Authority peer; the frozen `RaidLaunchContext.HostProfileId`
identifies only the historical Host of the original runner.

**Known limitation (chained migration).** `RaidLaunchContext.HostProfileId` is not
rebuilt after a migration, so a second migration would not recognize the intermediate
Host as historical. The first migration is classified correctly. Outside the FIX-50
acceptance; unvalidated.

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
non-terminal `Raiding` or `Defeated`, plus `Extracted` participants whose extraction
commit is still pending (`ExtractionExperiencePhase` is not `Complete`, FIX-51). The historical
Host (it has left the session; see [Voluntary Host exit](#voluntary-host-exit-handoff) for how a
resolved exit is released), Controlled Return, `Extracted` with a completed commit, `Aborted`,
Return-authorized, and known terminal profiles are never eligible. A recovered `Extracted` participant gets its avatar
Input Authority back so `RPC_CommitExtractionOnInputAuthority` and the ACK complete on the
replacement Host; the republish is idempotent through the `(raidGenerationId, profileId,
resultSequence)` receipt (backend `alreadyPublished`/`alreadySecured`, local `AlreadySecured`).
Once complete it follows the normal terminal path. If it does not rebind before sealing, its
participant and avatar are kept (authority cleared, terminal/no-rejoin, never despawned), the same
as a non-migration disconnect with a pending commit. The promoted Host's own local profile uses the same rebind path as any
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
Return or persistence, clears authority/routing, records terminal/no-rejoin, completes the
material closure already owned by Networking, resolves Progression explicitly as
`DefinitivelyDisconnected`, and despawns its avatar and participant. TASK-110 does not define a
new Loot policy for that material closure.

An unrecovered `Defeated` profile loses its participant, `PlayerObject`, routing,
and Input Authority and becomes terminal/no-rejoin. The independent defeated
avatar/corpse NetworkObject and its `NetworkLootContainer` remain authoritative;
only the separate `NetworkRaidParticipant` is despawned. After an abrupt loss the historical
Host is unresolved and finalized by the same state-dependent rules (`Aborted` when its exit was
not resolved), and never blocks sealing. A Host whose exit was already resolved is instead
released as departed-terminal and is not finalized again
(`HostMigrationRosterPolicy.ClassifyHistoricHost`).

Temporary departures while recovery is `Open` or `Sealing` never resolve Progression. A recovered
profile keeps the same participant, baseline, ledger, extraction candidate/phase and committed
resolution/application. A restored `Defeated` participant remains `Defeated`; sealing never replaces
that result with disconnection.

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

The player avatar's `PlayerStaminaNetworkController` participates in the same snapshot copy.
`CurrentStamina`, `IsExhausted`, its initialization marker and regeneration-delay `TickTimer` are
networked state and are restored by `NetworkObject.CopyStateFrom`. The restore-spawn guard prevents
fresh initialization, but is not the restoration mechanism itself.

Maximum Stamina is not replicated. Until `RaidAvatarParticipantLink.ParticipantId` is remapped and
the restored participant exposes its frozen attributes, the Stamina controller does not initialize,
clamp, regenerate or consume the copied resource. After fixup it derives
`75 + (Resistance * 5)` again and resumes from the copied current value, Exhaustion state and delay.

`PlayerManaNetworkController` copies `CurrentMana` and its initialization marker with the same avatar snapshot. Restore spawns never refill Mana, including an uninitialized copied snapshot. While participant/avatar fixups or effective statistics are unavailable, the copied balance is frozen. Once the bidirectional binding resolves, the new Host derives Maximum Mana from current attributes/equipment and clamps only excess. No regeneration, restoration or accepted ability payment is replayed by resource rebind. Terminal participation and Raid closure dispose temporary Mana; it is not persisted to Town. State-copy tests are not proof of actual multi-process Host Migration.
`PlayerDownedStateNetworkController` follows the same pattern. `IsDowned`, `DownedHealth` and
`DownedCycle` are networked and restored by `CopyStateFrom`; its `Spawned()` skips the fresh reset on
restore spawns (`HostMigrationRestoreUtility.IsRestoreSpawn`), so a Downed avatar resumes draining from
the copied reserve on the new Host.

`PlayerMovementNetworkController` follows the same pattern for its aim state. `AimDirection`,
`IsAimStance` and `AimStanceStartTick` are networked and restored by `CopyStateFrom`; its `Spawned()`
initializes them only when `!IsRestoreSpawn`, so a restored avatar keeps its aim and its drawn stance.

### Weapon instance modifiers

Packed weapon modifiers are networked state restored by `CopyStateFrom`, never rebuilt by `Spawned()`:
`PlayerLootReceiver.LootModifiers` (one word per Inventory slot), the four hand modifier words of
`PlayerWeaponEquipmentNetworkController`, `NetworkLootContainer.WeaponUnits` (catalog index, origin and
modifiers per unit) and `NetworkLootPickup.Modifiers`. The equipment, container and pickup `Spawned()` paths
skip their fresh initialization on restore spawns (`HostMigrationRestoreUtility.IsRestoreSpawn`), and the
receiver's `Spawned()` never repacks slots. Focused PlayMode tests copy each behaviour and read the modifiers
back; they are state-copy tests and do not prove a real multi-process Host Migration (a separate Host plus
two Clients), which remains a manual check.

### Accepted ranged release restoration

`PlayerCombatNetworkController` also copies TASK-222's four `WeaponHandAttackState` values and
`WeaponExecutionEndTick` with the avatar. Every hand retains its independent absolute interval,
source slot, release payload and presentation sequence. Restoring or selecting Equipment cannot
restart these clocks. A due melee release uses its original hand identity and accepted statistics;
an opposite-hand attack cannot substitute its executor parameters. The legacy single-strategy
snapshot remains for avatars without an active Equipment set.

`PlayerCombatNetworkController` copies each `RangedAttackRelease` network value with the avatar:
pending flag, absolute release tick (already carrying the aimed release delay when the stance was fully drawn), locked direction and committed projectile prefab/mask/offset,
speed/lifetime, damage/type, range and knockback. Cooldown, original weapon catalog identity,
acceptance/release/cancellation ticks and sequence are networked too. The restore-spawn guard
skips fresh initialization; `CopyStateFrom` is the restoration mechanism, not local executor state.
Equipment may rebuild or clear its strategy before participant reference fixups finish, but that
cannot overwrite the pending shot. The first forward authoritative tick revalidates death, Downed,
combat enablement and the restored gameplay phase, then consumes a due shot independently of input.
The combat boundary retries resolving the match controller because dynamic restoration order may
spawn the avatar first. Restore and reference fixups are synchronous before normal simulation.

The pending shot belongs structurally to its containing avatar. It stores no old EntityId or
NetworkObject reference to remap: release uses that avatar's current `CharacterBase.Id` and current
AttackOrigin after restoration. It does not resolve the currently equipped weapon to rebuild payload.
The deadline is preserved exactly in Fusion's resumed tick domain, not restarted by wall time.
Consumption commits the network pending flag before attempting spawn; a snapshot with a consumed
flag cannot replay it, including on spawn failure. Existing projectile restoration/remapping remains
unchanged. This guarantee is relative to the received migration snapshot: Fusion's periodic snapshot
cannot preserve attacks/projectiles committed after that snapshot, and this change adds no durable
history beyond Fusion recovery state.

Trap entities are restored as dynamic objects. `NetworkTrap` keeps its caster, running lifetime timer,
trigger parameters and the immobilize effect parameters as [Networked] state, so a restored trap still
triggers and applies its effect without any C# subscription. Its caster EntityId is remapped like a
projectile owner; a caster that is not in the snapshot (for example a defeated avatar, since a trap outlives
its cast) expires the restored trap on its next tick without triggering, instead of aborting the whole
restore. `ImmobilizeEffect` on an enemy keeps both
timers and its damage values as [Networked] state and only its source EntityId is remapped; a source that is
not in the snapshot cancels the restored effect (immobilization and damage end together) instead of crediting
a stale id. The casting execution itself restores through the ability runtime snapshot and never spawns a
second trap (see [Ability System Architecture](AbilitySystemArchitecture.md), Trampa).

Presentation baselines completed sequences and reconstructs only a saved pending wind-up at its
confirmed elapsed phase, through a presentation-only resume event without repeating attack-start
audio. Animator state and mutable shared executor config are neither copied nor required for release.
Real multi-process migration during wind-up and immediately after consumption remains unvalidated.

Disconnected retained participants (FIX-51): a `Raiding` participant (Active or Downed) that is
`IsDisconnected` with `RemainingBudgetTicks > 0` and no confirmed absence is re-registered in the
replacement Host's disconnected registry from the replicated `RaidParticipantConnectivity` state
(`HostMigrationRetentionPolicy.Classify`). It is not part of the eligible/unresolved set, so it is
neither awaited by the recovery window nor finalized at sealing. The reconnect budget is paused
while the recovery window is unsealed (`IsReconnectBudgetPaused`) and resumes after sealing. At
sealing the room stays joinable while a retained participant exists: `NetworkSpawnManager.UpdateMatchSessionGate`
applies `RaidSessionGatePolicy.Evaluate` through `NetworkMatchController.SetSessionReopenState`,
and `RaidSessionGateTracker` re-applies a decision deferred because State Authority or
`InProgress` was not ready yet, writing from the last value it requested rather than from the
observed `SessionInfo.IsOpen`. Sealing no longer closes the room unconditionally. The participant
can rebind during the window through the recovery rebind, or after sealing through the normal
reconnect path; both clear `IsDisconnected`. A reconnect ticket presented from Town while the
window is still open is refused (`TryValidateEarlyHostMigrationToken` accepts only migration
admission tickets), so that Client reconnects after sealing. `RaidReconnectPolicy` keeps the reservation proof in the in-app reconnect
ticket and ends a refused admission as `ConnectionRefused` instead of retrying until the window
expires. An exhausted budget or confirmed absence keeps the existing finalization.

Validation status of these rules: the retained-disconnected reconnect (Host plus two Clients,
budget paused across the window and consumed afterwards) has recorded multi-process evidence
(FIX-51). Recoverability of an `Extracted` participant with a pending commit is covered by
EditMode tests only; its live multi-process run is still pending.

*Note: For Dungeon Pressure (PvE timers, phases, and active population) recovery rules, see `DungeonPressureArchitecture.md`.*

## Validation boundary

EditMode tests cover role routing, startup suppression, completion semantics,
eligibility policy, and the voluntary-exit rules (`HostHandoffPolicyTests`,
`HostHandoffServiceTests`, `HostMigrationRosterPolicyTests`, `HostMigrationRetentionPolicyTests`,
`RaidSessionGatePolicyTests`, `HostHandoffAnnouncementTests`). PlayMode must cover
defeated-participant despawn while corpse and loot remain registered and interactable. The
decisive acceptance test is a fresh multi-process Host plus at least two Clients, with one
Client promoted to Host and the other adopting as Client. Single Runner validation is not
evidence of real Host Migration behavior.

Recorded multi-process evidence for the voluntary exit: a Host that died, saw Results and
pressed `Volver` handed off to a remaining participant and returned to Town while the Raid
continued; abrupt Host loss with two Clients kept the existing recovery path and finalized
the historical Host as `Aborted`; the handoff notice was seen on the remaining peer. A Host
Abandon followed by `Volver` was reported working by the user (FIX-49 run; logs not reviewed).
The handoff after a completed Host extraction has no recorded multi-process run yet. Evidence is
summarized in the FIX-48 report
(`docs/fix-48_reporte_traspaso_host_salida_voluntaria.md`).
