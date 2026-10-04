# Downed and Revive Architecture

## 1. Context and decision

A player whose Health reaches zero is **Downed**, not defeated. Downed is a recoverable state on
the same avatar. Definitive Defeat (corpse, Loot, spectator, terminal Results) happens only when
the Downed reserve is exhausted, or when a forced-Defeat rule applies (section 14).

```text
Active --(fatal hit)--> Downed --(reserve = 0)--> definitive Defeat
                          |
                          +--(valid recovery completes)--> Active
```

Decisions:

* One authoritative owner per fact. The Downed controller owns the Active/Downed fact and the
  Downed reserve. `PlayerCharacter` owns Health and the Defeat resolution. A separate recovery
  component owns a recovery in progress. `NetworkRaidParticipant` owns the terminal result.
* Normal Health and Downed Health are two different networked values with different owners and
  are never converted into each other.
* Downed and every recovery form share one base state. Assisted revive, Self-revive and future
  forms differ only in who starts the recovery session; the Health controller never owns revive.
* Extraction, Loot and Results keep their own architectures. Downed adds no logic to them beyond
  the predicates listed here.

Status labels used below: **Implemented** (TASK 451, in the code today), **Planned** (specified
here, not implemented) with the owning task or `no task`. Gameplay rules are cited as
`GD 13 §n` (Game Design, "Sistema de Abatido y Reanimacion"), plus GD 02, 05, 06, 07, 10.

Related documents: `PlayerCombatArchitecture.md` (damage pipeline, corpse),
`RaidDefeatAndSpectatorArchitecture.md` (departure, spectator),
`HostMigrationRecoveryArchitecture.md`, `PlayerMovementArchitecture.md` (section 20.6.1),
`PlayerInteractionArchitecture.md`, `ExtractionArchitecture.md`, `RaidParticipantArchitecture.md`.

## 2. Components and responsibilities

| Component | Owns | Does not own |
|---|---|---|
| `PlayerDownedStateNetworkController` (Implemented) | `[Networked] IsDowned`, `DownedHealth`, `DownedCycle`; drain; entry/exit/depletion of Downed; Downed attribution data (section 11) | Health, corpse, Loot, participant state, recovery sessions |
| `DownedHealthRules` (Implemented) | Pure reserve math (create, drain, damage, depletion) | Any state |
| `PlayerCharacter` (Implemented, extended) | Health; the fatal-damage redirection; Defeat resolution (`HandleDeath`); Health restoration on exit | The reserve, the recovery session |
| `CharacterBase` (Implemented) | Damage pipeline and its hooks (`TryApplyAlternateDamage`, `TryInterceptFatalDamage`, `CanReceiveHealing`) | Anything player-specific |
| `PlayerDownedGate` (Implemented) | Shared "is Downed" predicate for action choke points | State |
| `PlayerDownedRecoveryNetworkController` (Planned, TASK 452) | The recovery session: kind, reviver, progress, cycle; interruption rules | Reserve value, Health, interaction targeting |
| `PlayerInteractionNetworkController` + `IInteractable` (Implemented) | Instant, press-edge interaction targeting | Held progress, recovery rules |
| `DownedTeamResolutionCoordinator` (Planned, no task) | Host-side Accelerated Resolution evaluation | The reserve (it only requests a rate) |
| `PlayerStaminaNetworkController` (Implemented, extended) | Stamina value | Any Downed rule |
| `NetworkRaidParticipant` (Implemented) | `Raiding -> Defeated` participant result | Downed state |
| `PlayerCorpseGenerationController` (Implemented) | Corpse and Loot conversion, once | When it runs |
| `NetworkSpawnManager` (Implemented) | Disconnect retention of a Downed raider | Reserve, Defeat |

## 3. Authoritative owner of Active/Downed

`PlayerDownedStateNetworkController.IsDowned` is the only source of truth. It is `[Networked]`
and written only by State Authority. Everything else reads a projection:

* `PlayerCharacter.IsDowned` reads it (guarded by a valid `Object`).
* `PlayerCharacter.IsAlive` is `Health > 0 || IsDowned`; a Downed player is alive for every
  system that gates on `ICharacter.IsAlive` (extraction, damage, registry).
* There is no "Active" flag. Active means `Health > 0 && !IsDowned`. Defeated means
  `Health == 0 && !IsDowned` after the Downed state was cleared by depletion.
* `RaidParticipantState` is not extended with Downed or Active. The participant stays
  `Raiding` throughout (section 12).

## 4. Downed Health vs normal Health

| Aspect | Health | Downed Health |
|---|---|---|
| Owner | `CharacterBase` (`Health`, private setter) | `PlayerDownedStateNetworkController` (`DownedHealth`) |
| Value while Downed | `0`, never negative | Reserve, `0 < value <= initial` |
| Maximum | `ResolveMaximumHealth()` (Vitality, equipment) | `_initialDownedHealth`, 75 baseline, independent of VIT (GD 13) |
| Regeneration / healing | Conventional healing | None. `CanReceiveHealing` is false while Downed |
| Drain | None | `_downedDrainPerSecond` (2.5/s baseline, provisional) |
| Damage input | After defense mitigation | After the same mitigation, then `x _downedDamageMultiplier` (1.0) |
| Reset | Fresh spawn / restore | Full reserve on every `TryEnterDowned`; discarded on exit |

Rule: no code may read `DownedHealth` as Health or write `Health` as part of a Downed
transition except the dedicated restore path in section 6. Pure math stays in `DownedHealthRules`.
Status: **Implemented**.

## 5. Transitions and owners

| # | Transition | Trigger | Owning component | Authority | Side effects | Status |
|---|---|---|---|---|---|---|
| T1 | Active -> Downed | Mitigated damage takes `Health` to 0 | `PlayerCharacter.TryInterceptFatalDamage` -> `PlayerDownedStateNetworkController.TryEnterDowned` | State Authority | `IsDowned`, full reserve, `DownedCycle++`; excess of the entry hit discarded; result is non-fatal (`IsFatal = false`); records Downed causer (section 11); Mana-drain skills deactivated (no Mana runtime exists, see section 9) | Implemented (causer: Planned TASK 453) |
| T2 | Downed -> Downed (damage) | Damage while Downed | `PlayerCharacter.TryApplyAlternateDamage` -> `TryApplyDownedDamage` | State Authority | Reserve reduced; `Health` untouched; notifies the recovery session (section 7) | Implemented (notify: Planned TASK 452) |
| T3 | Downed -> definitive Defeat (drain) | Reserve reaches 0 in `FixedUpdateNetwork` | `PlayerDownedStateNetworkController` clears `IsDowned`, then `PlayerCharacter.ResolveDefinitiveDefeatFromDowned` | State Authority | `HandleDeath` runs exactly once (section 12) | Implemented |
| T4 | Downed -> definitive Defeat (damage) | Damage depletes the reserve | `TryApplyDownedDamage` clears `IsDowned`; `PlayerCharacter.TryApplyAlternateDamage` calls `HandleDeath` | State Authority | Result is fatal (`IsFatal = true`); records Defeat causer | Implemented (causer: Planned TASK 453) |
| T5 | Downed -> Active | A recovery session completes | `PlayerDownedRecoveryNetworkController` -> `PlayerCharacter.TryRestoreFromDowned` -> `TryExitDownedToActive` | State Authority | Section 6 | Planned (TASK 452; Self-revive: no task) |
| T6 | Downed -> definitive Defeat (forced) | Dungeon absolute close or equivalent rule | `PlayerCharacter.ForceDefinitiveDefeat` | State Authority | Section 14 | Planned (no task) |
| T7 | Downed -> Aborted participant | Raid closure with the avatar Downed | `NetworkSpawnManager.AbortRaidingParticipantsForClosure` -> `TryAbortForClosure` | Host | No Defeat, no corpse (existing behavior; see section 14) | Implemented |

Pipeline integration (Implemented). `CharacterBase.ApplyDamage` order is: validity guards ->
`CalculateMitigatedDamage` -> `TryApplyAlternateDamage` (Downed reserve absorbs; returns early)
-> subtract `Health` -> if fatal, `TryInterceptFatalDamage` (enter Downed, return non-fatal) ->
otherwise `HandleDeath`. Fatal damage therefore never reaches `HandleDeath` while a Downed entry
is available, so no terminal resolution runs early. `TestDisableEntry` is the only seam that
turns entry off.

Excess damage: the entry hit sets `Health` to 0 and its excess is discarded. A hit that
depletes the reserve also discards its excess. There is no carry-over between the two values.

## 6. Downed -> Active exit contract (Planned, TASK 452)

Only a recovery owner calls the exit. Conventional healing stays rejected.

```text
PlayerDownedStateNetworkController
    internal bool TryExitDownedToActive()
PlayerCharacter
    internal bool TryRestoreFromDowned(float restoredHealth)
CharacterBase
    protected void RestoreHealthAuthoritatively(float health)   // Health has a private setter
PlayerStaminaNetworkController
    internal void ForceDepleteForRecovery()                     // sets CurrentStamina to 0
```

`PlayerCharacter.TryRestoreFromDowned(restoredHealth)` runs in this order, on State Authority
only, and returns false without side effects if any precondition fails:

1. Preconditions: `IsDowned`, `restoredHealth` finite and `> 0`, clamped to `ResolveMaximumHealth()`.
2. `_downedStateController.TryExitDownedToActive()`: sets `IsDowned = false`, `DownedHealth = 0`,
   clears any accelerated rate. `DownedCycle` keeps its value, so any attribution or recovery
   that references the old cycle becomes invalid by comparison (sections 7 and 11).
3. `RestoreHealthAuthoritatively(restoredHealth)`: Assisted revive passes exactly `1` (GD 13).
   Self-revive passes the item-variant value (open question, section 17).
4. `PlayerStaminaNetworkController.ForceDepleteForRecovery()`: Stamina to 0 with the existing
   exhaustion and regeneration-delay rules (today the controller only offers `TrySpend` and
   `TrySpendContinuous`, which cannot force a value). Mana is not touched (no Mana runtime exists).
5. No invulnerability and no resumption of interrupted actions: nothing is queued or granted.
   Control returns immediately because the Downed predicates read the networked flag.

The order guarantees `IsAlive` never flickers false between steps 2 and 3, because the whole
method runs inside one simulation tick on State Authority.

## 7. Recovery ownership (Planned, TASK 452)

`PlayerDownedRecoveryNetworkController` lives on the Downed avatar and owns at most one session.
One session per Downed avatar enforces the 1 reviver : 1 Downed rule structurally.

```text
[Networked] RecoveryKind Kind          // None | Assisted | Self
[Networked] int           ReviverEntityId   // 0 for Self
[Networked] int           SessionCycle      // DownedCycle at start
[Networked] TickTimer     Completion        // started at begin; duration from config
```

* A session is **valid** only if `Kind != None`, `SessionCycle == DownedCycle` and `IsDowned`.
  A stale session from an old cycle is ignored and cleared.
* **Drain pause is derived, never stored.** The Downed controller reads a small read-only
  interface, `IDownedDrainGate.IsDrainPaused`, implemented by the recovery component as
  "has a valid session". The reserve drain rate is `0` while paused. Damage still reduces the
  reserve during a session (GD 13). The Downed controller depends on the interface only, so there
  is no circular dependency and no duplicated flag.
* **Interruption** (Assisted), all evaluated on State Authority in the recovery component's
  `FixedUpdateNetwork`; partial progress is discarded by clearing the session:

| Interrupt | Detection |
|---|---|
| Damage to the Downed | `TryApplyDownedDamage` returns `applied > 0` and calls `NotifyDownedDamaged()` (direct call in the same authority) |
| Damage to the reviver | Reviver `Health` lower than at the last tick (polling), or reviver Downed |
| Movement | `PlayerMovementNetworkController.IsMoving` (already `[Networked]`) on either avatar |
| Range loss | Distance between the two avatars above the configured range |
| Reviver disconnect | Reviver avatar invalid, or reviver participant no longer `Raiding` |
| Downed definitive Defeat | `IsDowned` false without a completed exit, or `DownedCycle` changed |

* **Not an interruption:** the Downed player disconnecting (GD 13 §11); the session continues.
* **Completion:** when `Completion` expires with the session still valid, the recovery component
  calls `PlayerCharacter.TryRestoreFromDowned(1)` and clears the session. Success and cleanup
  happen in the same tick, so a double completion is impossible.
* **Self-revive:** the same component with `Kind = Self`; progress uses the same timer. A
  started Self-revive is not interrupted by disconnect (GD 13) and its own movement lock is
  expressed by the session (see below). The item is consumed only at completion, by the
  self-revive consumer calling into the component, not by the Health controller.
* **Movement lock for Self-revive:** the movement controller reads "session of kind Self is
  active" through the same interface family. It does not use `TrySetControlEnabled`, which is a
  single shared boolean that could conflict with other owners.

Future recovery forms add a `RecoveryKind` value and a starter. They reuse sessions, drain
pause and the exit contract unchanged.

## 8. Relation with the interaction system

Facts verified in code: interactions are **instant and press-edge** (`WasPressed` in
`PlayerInteractionNetworkController`); targets come from `IInteractionTargetQuery`
(`Physics2DInteractionTargetQuery`) filtered by layer and `MaximumDistance`;
`InteractionResolver` selects the first valid `IInteractable` and calls `Interact` once.
A Downed player is rejected at the controller by `PlayerDownedGate`. There is no held-interaction
concept.

Planned minimal extension (TASK 452):

1. The Downed avatar exposes an `IInteractable` revive target (registered in `EntityRegistry`,
   on a collider the interaction layer mask includes). `CanInteract(request)` accepts only an
   Active teammate: interactor resolves to a `PlayerCharacter` that is alive, not Downed, a
   frozen initial teammate, in range, and the target has no valid session.
2. `Interact` does **not** complete anything. It calls
   `PlayerDownedRecoveryNetworkController.TryBeginAssisted(reviverEntityId)`, which opens the
   session and starts `Completion`. The press edge stays the start gesture.
3. "Held" is a continuation condition, not a new interaction type. The reviver's
   `PlayerInteractionNetworkController` publishes `[Networked] IsInteractHeld`
   (`NetworkButtons.IsSet(PlayerInputButton.Interact)`) so the recovery component, which runs on
   the Downed avatar and cannot read the reviver's input, can cancel on release.
4. Downed players stay blocked from general interactions. Self-revive does not use targeting; it
   starts from the Downed player's own input in a dedicated component (no task).
5. Self-reviver handoff (GD 13 §8) is a normal short instant interaction on an Active teammate,
   not a revive. It belongs to the item/inventory feature (no task).

`ExtractionSanctuary.CanInteract` checks `IsAlive`, which is true while Downed; the Downed
interaction gate, not `IsAlive`, is what prevents a Downed player from starting the Sanctuary
ritual (see section 13).

## 9. Functional restrictions while Downed

| Rule | Mechanism | Status |
|---|---|---|
| Limited movement, no Sprint | `PlayerMovementNetworkController` multiplies voluntary speed by `DownedMovementSpeedMultiplier` (0.35 provisional) and disables Sprint without Stamina cost | Implemented |
| Knockback allowed | Knockback motor unchanged | Implemented |
| No attack, shield, abilities | `PlayerCombatNetworkController`, `PlayerShieldDefenseNetworkController` check `PlayerDownedGate` | Implemented |
| No equipment / Weapon Set changes | `PlayerWeaponEquipmentNetworkController` gate | Implemented |
| No normal consumables | `PlayerConsumableNetworkController` gate | Implemented |
| No inventory / Loot | `PlayerLootTransferNetworkController`, `PlayerLootDropNetworkController` gates | Implemented |
| No general interactions | `PlayerInteractionNetworkController` gate | Implemented |
| No new CC/status | `PlayerCharacter.CanReceiveStatusEffects` is `IsAlive && !IsDowned`; predicate only, no CC system exists | Implemented (predicate) |
| Healing does not restore or revive | `PlayerCharacter.CanReceiveHealing` is `!IsDowned && base` | Implemented |
| Weapon visuals hidden | `PlayerWeaponPresenter` | Implemented |
| Mana-drain skills deactivate, Mana preserved | No Mana runtime exists (only `AbilityResourceType.Mana` and maximum-resource modifiers). Contract: the future Mana owner deactivates persistent drain on `IsDowned` rising and never restores Mana on exit | Planned (no task) |
| Downed pose / HUD | Presentation only, reads `IsDowned` | Planned (no task) |

Unlisted abilities are blocked by the same rule: any new action choke point must call
`PlayerDownedGate.IsDowned`. Extraction is intentionally not gated.

## 10. Accelerated Resolution (Planned, no task)

GD 13 §9: when the team has no valid recovery route, every Downed member's drain accelerates so
definitive Defeat arrives after a configurable time (about 5 s). The bar is not cut instantly.

* **Owner:** `DownedTeamResolutionCoordinator`, a Host-only evaluator. It lives outside
  `NetworkSpawnManager` (already very large) and reads team membership from the frozen
  `RaidInitialAffiliationSnapshot` (`TryGetTeam`, `TryAreInitialTeammates`).
* **Inputs per member:** Active, Downed or Defeated/other (participant `State` plus the avatar's
  `IsDowned`), and `CanSelfRevive` (default `false` until Self-revive exists).
* **Rule (pure, testable):** a team has a route if any member is Active (an assisted reviver) or
  any Downed member can Self-revive or has a valid Self-revive session. Solo without Self-reviver
  and Duo with both Downed and no Self-reviver have no route.
* **Evaluation:** on State Authority, compare a compact per-member signature each tick (at most
  two members) and recompute only when it changes. No C# event drives authoritative state.
* **Effect:** on "no route" the coordinator calls `RequestAcceleratedResolution()` on each Downed
  member's controller, which stores `[Networked] float AcceleratedDrainPerSecond =
  max(base, currentReserve / _acceleratedResolutionSeconds)`. The drain rate used each tick is that
  value while it is non-zero, so the remaining reserve empties in the configured time with the
  normal bar, and damage during the window still shortens it.
* **Restore-safe and deterministic:** the rate is a networked value, not a derived timer, so a
  restored avatar resumes with the same rate. A valid recovery session still pauses drain.
* **Open question (GD silent): route reappears.** Default chosen: the acceleration **latches**
  for the current Downed cycle and clears only on exit or Defeat. Rationale: the player already
  saw a short timer, and a route can only reappear through reconnect or a handoff, both deferred.
  Needs a Game Design decision (section 17).

## 11. Attribution of Downed and Defeat causers (Planned, TASK 453)

GD 13 §13 and GD 05 require recording separately who caused Downed and who caused definitive
Defeat. Today `TryInterceptFatalDamage()` and `TryApplyDownedDamage` do not receive the
`DamageRequest`, so no attacker is known. `DamageRequest.AttackerId` (`EntityId`, `0` = none)
is the available identity.

Data lives on the Downed controller, next to the cycle it belongs to:

```text
[Networked] int DownedCauserEntityId   // attacker of the entry hit; 0 = environment / none
[Networked] int DefeatCauserEntityId   // attacker of the depleting hit; 0 = drain, environment, forced
[Networked] int DefeatedCycle          // DownedCycle at definitive Defeat; 0 = never defeated
```

Required hook changes: `TryInterceptFatalDamage(in DamageRequest)` and
`TryEnterDowned(EntityId attackerId)`; `TryApplyDownedDamage(..., EntityId attackerId, ...)`.

Rules:

* The Downed causer is written once per cycle at entry and is provisional (GD 05).
* Hits on a Downed target never write `DownedCauserEntityId` and never create a new credit.
* The Defeat causer is written only by a hit that depletes the reserve; pure drain writes `0`.
* **Consolidation:** a provisional credit created for cycle `X` is consolidated only if
  `DefeatedCycle == X`. A revive never sets it, so it cancels the credit with no extra state;
  a later Downed entry increments `DownedCycle` and cannot reuse the old one.
* Resolving `EntityId` to a participant uses the existing `RaidAvatarParticipantLink` /
  `NetworkRaidParticipant` path, as `DamageResolver` already does for contributions.
* Amounts belong to GD 05; attribution rules belong to Combat Design (US-50, TASK 442, not yet
  written). This document defines only where the data lives. `DamageResolver` and
  `ICombatContributionTracker` are not extended here.
* Note: the entry hit returns `IsFatal = false`, so `DamageResolver` fatal-reward paths
  (`TryAwardFatal*`) do not fire at Downed entry; they fire only on definitive Defeat.

## 12. NetworkRaidParticipant, corpse, Loot, spectator and Results

Everything terminal is gated on definitive Defeat. Downed produces none of it.

| Concern | While Downed | At definitive Defeat | Owner |
|---|---|---|---|
| `RaidParticipantState` | Stays `Raiding` (in `HasRaidingParticipants`) | `Defeated` via `TryMarkDefeated(avatar)` | `NetworkRaidParticipant`, called from `RaidAvatarParticipantLink.NotifyCorpseConversionCompleted` |
| `CurrentAvatarId` | Unchanged | Cleared by `TryMarkDefeated` | `NetworkRaidParticipant` |
| Corpse and Loot | None. Inventory and Equipment stay with the player | One-time conversion by `PlayerCorpseGenerationController.TryConvertInventoryToCorpseLoot` | `PlayerCharacter.HandleDeath` |
| Spectator | A Downed player is not a spectator; others may still observe it as a valid `Raiding` target | Local spectator mode on `Defeated` | `RaidMenuPresenter`, `LocalRaidSpectatorController` |
| Results / Return | None | Terminal Results and Return authorization on `Defeated` | Existing Results flow |
| Input authority | Retained (connected) | Removed by the existing link code | `RaidAvatarParticipantLink` |

Defeat sequence (Implemented): reserve depletion clears `IsDowned` in the same tick, then
`HandleDeath` runs exactly once -> corpse conversion -> `NotifyCorpseConversionCompleted` ->
`TryMarkDefeated`. Existing limitation: `TryMarkDefeated` is reached only if corpse conversion
succeeded (`corpseReady`); a failed conversion leaves the participant `Raiding` with
`IsDowned = false` and `Health = 0`. That failure mode is shared with the pre-Downed pipeline and
is not changed here.

The Downed controller never calls `NetworkRaidParticipant`, `NetworkMatchController`, extraction
or Results code. It only reaches `PlayerCharacter`, which reaches the corpse controller and
participant link exactly as before. No extraction, Loot or Results responsibility is duplicated.

## 13. Extraction while Downed

GD 13 / GD 06: a connected Downed player inside the area can extract; drain continues during the
countdown; Defeat first interrupts that player's extraction; the countdown first is a successful
extraction while Downed.

Verified in code:

* Extraction gates are **not** Downed-gated, and `IsAlive` is true while Downed.
  `PlayerExtractionController.EvaluateContinuation` (`CancelWhenNotAlive`) therefore keeps an
  in-progress countdown running while Downed.
* On definitive Defeat `IsAlive` becomes false, so the next tick cancels with
  `CancellationReason.CharacterNotAlive`. `ExtractionSanctuary` also cancels its ritual when
  `owner.IsAlive` is false. No new extraction code is needed for the per-player interruption.
* `TryBeginExtraction` requires `IsAlive` only; a Downed player may begin a countdown if the
  start call reaches the controller. The **Sanctuary ritual start** is an interaction, which the
  Downed interaction gate rejects. A Downed player can therefore continue an extraction already
  started, but cannot start the ritual. GD 13 says a Downed player "can extract"; whether that
  includes starting the ritual while Downed is an open question (section 17).
* Extraction today is **per player**. There is no code that re-evaluates a required team when a
  member is defeated. Re-evaluating the required team on Defeat is a **TASK 453 / GD 06
  requirement**: it must read the participant `Defeated` state (the only terminal signal Downed
  produces), not `IsDowned`. This document adds no extraction logic.

## 14. Forced definitive Defeat and raid closure

Contract (Planned, no task):

```text
PlayerCharacter
    internal void ForceDefinitiveDefeat()
        // State Authority only. If IsDowned: ClearDownedState() (reserve ignored),
        // set DefeatedCycle = DownedCycle, DefeatCauserEntityId = 0, then HandleDeath().
        // If Active: set Health to 0 through the dedicated restore/clamp path, then HandleDeath().
        // Idempotent: a second call after Defeat is a no-op.
```

It is not implemented because **no current system defeats players on collapse**. Verified:
`Dungeon Pressure` code references no player damage or Defeat, and
`NetworkSpawnManager.AbortRaidingParticipantsForClosure` moves `Raiding` participants (including
retained Downed ones) to `Aborted`, not `Defeated`, with no corpse. If GD 10 ("absolute close forces
definitive Defeat") is meant to mean Defeat rather than Abort for Downed players, that is a
Game Design conflict with the current closure behavior and needs a decision before implementation.
`ClearDownedState()` already exists as the internal primitive ("used by forced definitive defeat").

## 15. Disconnect

Implemented (`NetworkSpawnManager.OnPlayerLeft`, `RaidPlayerDeparturePolicy.ShouldRetainDownedRaider`):

* A `Raiding` participant whose avatar is Downed is retained: not despawned, not finalized as a
  definitive disconnect. The Host removes the avatar's input authority and tracks the participant
  in `_retainedDownedParticipants`.
* The reserve keeps draining and the avatar can still take damage. Depletion runs the normal
  `HandleDeath` path; `TryMarkDefeated` is keyed by `CurrentAvatarId`, not `PlayerRef`.
* While retained and still `Raiding`, the participant counts in `HasRaidingParticipants` and is
  aborted by `AbortRaidingParticipantsForClosure`; it is not a connected remote participant.

Planned recovery behavior (TASK 452):

| Case | Behavior |
|---|---|
| Downed player disconnects, no session | Continues draining; can still be revived or defeated (GD 13 §11) |
| Downed player disconnects during a session | Session continues and may complete |
| Reviver disconnects | Session is interrupted (section 7) |
| Downed disconnected player is revived | Returns Active with Health 1 and no input authority until reconnect; reconnect policy is deferred |
| Self-revive started, then disconnect | Not interrupted (GD 13) |

The Accelerated Resolution evaluator treats a disconnected-but-retained Downed member as Downed
(it still counts) and a disconnected reviver as not Active for route purposes.

## 16. Host Migration

Design intent, not a guaranteed MVP requirement (GD). Contract for everything this document adds:

* All Downed, recovery, accelerated and attribution state is `[Networked]` on the avatar
  (`IsDowned`, `DownedHealth`, `DownedCycle`, recovery session fields, `AcceleratedDrainPerSecond`,
  `DownedCauserEntityId`, `DefeatCauserEntityId`, `DefeatedCycle`). Fusion copies them with the
  avatar through `HostMigrationSnapshotRestorer` (`NetworkObject.CopyStateFrom`).
* Every new `Spawned()` must follow the existing guard
  `HasStateAuthority && !HostMigrationRestoreUtility.IsRestoreSpawn(this)` before any fresh
  initialization, as `PlayerDownedStateNetworkController` does today.
* `TickTimer Completion` is a Fusion value that survives migration; the recovery component must
  revalidate it against the new runner tick on the first authoritative tick and clear the session
  if reviver, cycle or range no longer validate.
* The coordinator keeps no state of its own: it recomputes from restored avatar and participant
  state on its first tick after the recovery window.
* **Known limitation (Implemented, unvalidated):** `_retainedDownedParticipants` is runtime-only
  Host bookkeeping and is not in the snapshot. A Downed player who is also disconnected during
  migration is not guaranteed to be re-retained, so reaching Defeat is not guaranteed.
  Reconnect/recovery policy remains deferred (`RaidDefeatAndSpectatorArchitecture.md`).

## 17. Open questions

| # | Question | Owner | Default used in this document |
|---|---|---|---|
| Q1 | Final Downed drain rate, damage multiplier, movement multiplier | Game Design / balance | 2.5/s, 1.0, 0.35 (provisional, serialized) |
| Q2 | Does Accelerated Resolution revert if a route reappears? | Game Design | Latches until exit or Defeat |
| Q3 | Self-revive restored Health per item variant | Game Design | Parameter of `TryRestoreFromDowned` |
| Q4 | Revive range, hold duration, Accelerated Resolution seconds | Game Design / balance | Serialized config; ~5 s for acceleration |
| Q5 | Can a Downed player start the Sanctuary ritual, or only continue one in progress? | Game Design (GD 06 / 13) | Only continue (gate unchanged) |
| Q6 | Does Dungeon absolute close force Defeat or Abort for Downed players? | Game Design (GD 10) | Abort (current behavior) |
| Q7 | Do "both immobile" and "interrupted by movement" mean any movement, including the Downed's limited movement and knockback? | Game Design | Any `IsMoving` on either avatar cancels |
| Q8 | Reviver damage detection: any `Health` loss, or any hit including fully mitigated ones? | Combat Design | `Health` loss (polling) |
| Q9 | PvP Last Hit attribution rules and amounts | Combat Design (US-50 / TASK 442), GD 05 | Data only (section 11) |

## 18. Implementation status

| Item | Status | Reference |
|---|---|---|
| Downed reserve rules, controller, damage pipeline hooks | Implemented (TASK 451) | `562b077f`, `5765d017`, tests `2af5a355` |
| Action gates (attack, shield, consumables, Loot, interaction, equipment) | Implemented (TASK 451) | `066356fc` |
| Limited movement, no Sprint | Implemented (TASK 451) | `5d21e50d` |
| Weapon visuals hidden | Implemented (TASK 451) | `5ff7c40e` |
| Disconnect retention | Implemented (TASK 451) | `dc4745d5` |
| Downed docs (combat, movement, defeat, migration) | Implemented (TASK 451) | `ff95a44b`, `ea9be1d4` |
| Exit contract, recovery component, Assisted revive, `IsInteractHeld`, `ForceDepleteForRecovery`, `RestoreHealthAuthoritatively` | Planned | TASK 452 |
| Attribution fields and hook signature changes, extraction re-evaluation on Defeat, forced Defeat | Planned | TASK 453 (forced Defeat: no task until Q6) |
| Self-revive item, consumption, handoff, Self session | Planned | no task |
| Accelerated Resolution coordinator | Planned | no task, needs a task |
| Mana runtime interaction | Planned | no task (no Mana runtime exists) |
| Downed pose and HUD | Planned | no task |

## 19. TASK 450 coverage

| TASK 450 scope item | Section |
|---|---|
| Authoritative owner of Active/Downed | 3 |
| Downed Health representation | 4 |
| Separation normal Health vs Downed Health | 4 |
| Transitions Active->Downed, Downed->Active, Downed->Defeat | 5, 6 |
| Damage pipeline integration | 5 |
| Excess damage of the entry hit | 5 |
| Drain | 4, 5 (T3), 7 (pause) |
| Accelerated Resolution | 10 |
| Functional restrictions while Downed | 9 |
| Ownership of a recovery in progress | 7 |
| Relation with the interaction system | 8 |
| Disconnect | 15 |
| Extraction while Downed | 13 |
| Integration with `NetworkRaidParticipant` | 12 |
| Corpse and Loot | 12 |
| Spectator and Results | 12 |
| Separate record of Downed causer and Defeat causer | 11 |
| Host Migration requirements | 16 |

| Acceptance criterion | Where |
|---|---|
| Downed is recoverable, not Defeat | 1, 3, 12 |
| Different sources of truth for Health and Downed Health | 4 |
| Fatal damage redirects to Downed without early terminal resolution | 5 |
| Table of which component owns each transition | 5 |
| Corpse, Loot, spectator, terminal results belong to Defeat | 12 |
| Disconnect and Host Migration interaction defined | 15, 16 |
| No duplicated extraction / Loot / Results responsibilities | 12, 13 |
| Assisted revive and future recovery forms share the base state, outside the Health controller | 6, 7 |
