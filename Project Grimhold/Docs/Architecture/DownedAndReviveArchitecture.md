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
| `PlayerPvpCombatCreditNetworkController` (Implemented, TASK-246) | Provisional Last Hit/Assist per Downed cycle; confirmation, cancellation and ledger retries | Health, reserve, participant lifecycle |
| `PlayerDownedStateNetworkController` (Implemented) | `[Networked] IsDowned`, `DownedHealth`, `DownedCycle`; drain; entry/exit/depletion of Downed; Downed attribution data (section 11) | Health, corpse, Loot, participant state, recovery sessions |
| `DownedHealthRules` (Implemented) | Pure reserve math (create, drain, damage, depletion) | Any state |
| `PlayerCharacter` (Implemented, extended) | Health; the fatal-damage redirection; Defeat resolution (`HandleDeath`); Health restoration on exit | The reserve, the recovery session |
| `CharacterBase` (Implemented) | Damage pipeline and its hooks (`TryApplyAlternateDamage`, `TryInterceptFatalDamage`, `CanReceiveHealing`) | Anything player-specific |
| `PlayerDownedGate` (Implemented) | Shared "is Downed" predicate for action choke points | State |
| `PlayerDownedRecoveryNetworkController` (Implemented, TASK 452) | The recovery session: kind, reviver, progress, cycle; interruption rules | Reserve value, Health, interaction targeting |
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

Friendly Fire (TASK-245): resolver-authorized ally damage is clamped after mitigation to leave
at least 1 normal HP, so it cannot cause Downed. On an already Downed target, ally hits do not
reduce the reserve or call `NotifyDownedDamaged`, and therefore do not interrupt assisted revive
through damage. Zero effective damage to the reviver causes no Health loss for interruption polling;
allowed knockback remains subject to the movement/range rules in section 7.

## 5. Transitions and owners

| # | Transition | Trigger | Owning component | Authority | Side effects | Status |
|---|---|---|---|---|---|---|
| T1 | Active -> Downed | Mitigated damage takes `Health` to 0 | `PlayerCharacter.TryInterceptFatalDamage` -> `PlayerDownedStateNetworkController.TryEnterDowned` | State Authority | `IsDowned`, full reserve, `DownedCycle++`; excess of the entry hit discarded; result is non-fatal (`IsFatal = false`); records Downed causer (section 11); Mana-drain execution stopped through the ability runtime (see section 9) | Implemented (causer: TASK 453; PvP credit: TASK-246) |
| T2 | Downed -> Downed (damage) | Damage while Downed | `PlayerCharacter.TryApplyAlternateDamage` -> `TryApplyDownedDamage` | State Authority | Reserve reduced; `Health` untouched; notifies the recovery session (section 7) | Implemented (notify: `5ff5208b`, TASK 452) |
| T3 | Downed -> definitive Defeat (drain) | Reserve reaches 0 in `FixedUpdateNetwork` | `PlayerDownedStateNetworkController` clears `IsDowned`, then `PlayerCharacter.ResolveDefinitiveDefeatFromDowned` | State Authority | `HandleDeath` runs exactly once (section 12) | Implemented |
| T4 | Downed -> definitive Defeat (damage) | Damage depletes the reserve | `TryApplyDownedDamage` clears `IsDowned`; `PlayerCharacter.TryApplyAlternateDamage` calls `HandleDeath` | State Authority | Result is fatal (`IsFatal = true`); records Defeat causer | Implemented (causer: TASK 453; PvP credit: TASK-246) |
| T5 | Downed -> Active | A recovery session completes | `PlayerDownedRecoveryNetworkController` -> `PlayerCharacter.TryRestoreFromDowned` -> `TryExitDownedToActive` | State Authority | Section 6 | Implemented (Assisted: `93ac7eeb`, `5ff5208b`, `d88d5c01`, TASK 452; Self-revive: no task) |
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

## 6. Downed -> Active exit contract (Implemented, TASK 452: `93ac7eeb`)

Only a recovery owner calls the exit. Conventional healing stays rejected.

```text
PlayerDownedStateNetworkController
    internal bool TryExitDownedToActive()
PlayerCharacter
    internal bool TryRestoreFromDowned(float restoredHealth)
CharacterBase
    protected void RestoreHealthAuthoritatively(float health)   // Health has a private setter
PlayerStaminaNetworkController
    internal bool ForceDepleteForRecovery()                     // sets CurrentStamina to 0
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
   `TrySpendContinuous`, which cannot force a value). Mana is not touched: `PlayerManaNetworkController` preserves the current balance through recovery.
5. No invulnerability and no resumption of interrupted actions: nothing is queued or granted.
   Control returns immediately because the Downed predicates read the networked flag.

The order guarantees `IsAlive` never flickers false between steps 2 and 3, because the whole
method runs inside one simulation tick on State Authority.

## 7. Recovery ownership (Implemented, TASK 452: `5ff5208b`, `d88d5c01`)

`PlayerDownedRecoveryNetworkController` lives on the Downed avatar and owns at most one session.
One session per Downed avatar enforces the 1 reviver : 1 Downed rule structurally.

```text
[Networked] RecoveryKind Kind              // None | Assisted | Self (Self reserved, not used yet)
[Networked] NetworkId     ReviverId        // reviver avatar NetworkId, resolved on the Host with Runner.TryFindObject
[Networked] int           SessionCycle     // DownedCycle at start
[Networked] TickTimer     Completion       // started at begin; duration from config (default 4 s)
[Networked] NetworkId     RevivingTargetId // reviver side: the Downed avatar this avatar is reviving
```

* Pure rules live in `DownedRecoveryRules` (`CanStartAssisted`, `EvaluateInterruption`,
  `IsSessionValid`); the component only gathers the snapshot and applies the verdict.
* **One session per reviver** is answered by `RevivingTargetId`, which is trusted only while the
  target's session is valid and still names the reviver (`IsRevivingAnotherAvatar`), so a stale
  value can never block anyone.
* A session is **valid** only if `Kind != None`, `SessionCycle == DownedCycle` and `IsDowned`.
  A stale session from an old cycle is ignored and cleared.
* **Drain pause is derived, never stored.** The Downed controller reads a small read-only
  interface, `IDownedDrainGate.IsDrainPaused`, implemented by the recovery component as
  "has a valid session". The reserve drain rate is `0` while paused. Damage still reduces the
  reserve during a session (GD 13). The Downed controller depends on the interface only, so there
  is no circular dependency and no duplicated flag.
* Damage to the Downed reaches the component through `IDownedDamageObserver.NotifyDownedDamaged()`,
  called by `TryApplyDownedDamage` when the applied damage is greater than 0.
* Defaults (serialized, GD 13 leaves them to balance): duration 4 s, range 1.5 units, restored
  Health fixed at 1.

* **Interruption** (Assisted), all evaluated on State Authority in the recovery component's
  `FixedUpdateNetwork`; partial progress is discarded by clearing the session:

| Interrupt | Detection |
|---|---|
| Damage to the Downed | `TryApplyDownedDamage` returns `applied > 0` and calls `NotifyDownedDamaged()` (direct call in the same authority) |
| Damage to the reviver | Reviver `Health` lower than at the last tick (polling), or reviver Downed. Detectability decision: damage means `Health` loss; a fully mitigated hit does not interrupt (Q8) |
| Movement | `PlayerMovementNetworkController.IsMoving` (already `[Networked]`) on either avatar. Knockback interrupts only when it breaks the range (Q7) |
| Range loss | Distance between the two avatars above the configured range |
| Reviver disconnect | Reviver avatar invalid, or reviver participant no longer `Raiding` |
| Downed definitive Defeat | `IsDowned` false without a completed exit, or `DownedCycle` changed |
| Release of Interact | Reviver `[Networked] IsInteractHeld` is false (section 8) |
| Incompatible reviver action | `PlayerReviveGate.InterruptIfReviving(character)` at the authoritative choke points (section 8) |

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

* **Outcome record (Implemented, TASK-215).** Clearing a session erases `Kind`, `ReviverId` and
  `Completion`, so clients could not tell an interruption from a completion nor its cause. The
  recovery component therefore also keeps a presentation-only record, written by State Authority
  immediately before the session is cleared and never read by gameplay rules:

```text
[Networked] RecoveryOutcomeKind          LastOutcomeKind       // None | Interrupted | Completed
[Networked] DownedRecoveryInterruptReason LastOutcomeReason    // cause when Interrupted
[Networked] NetworkId                    LastOutcomeReviverId  // lets the reviver recognise its own outcome
[Networked] int                          LastOutcomeSequence   // monotonic; presenters edge-detect on it
```

  It is reset in `Spawned` except on a Host Migration restore, so a restored avatar keeps its
  sequence and presenters baseline on it instead of replaying. `DownedDefeated` is recorded but
  has no interruption message (definitive Defeat owns its overlay). `TryGetProgress` derives
  revive progress from the replicated `Completion` timer and the configured duration, so progress
  adds no further state. Durations, ranges and interruption rules are unchanged.

Future recovery forms add a `RecoveryKind` value and a starter. They reuse sessions, drain
pause and the exit contract unchanged.

## 8. Relation with the interaction system

Facts verified in code: interactions are **instant and press-edge** (`WasPressed` in
`PlayerInteractionNetworkController`); targets come from `IInteractionTargetQuery`
(`Physics2DInteractionTargetQuery`) filtered by layer and `MaximumDistance`;
`InteractionResolver` selects the first valid `IInteractable` and calls `Interact` once.
A Downed player is rejected at the controller by `PlayerDownedGate`. There is no held-interaction
concept.

Implemented extension (TASK 452, `d88d5c01`):

1. `DownedReviveInteractable` on the player avatar is the revive target. The avatar's **primary**
   interactable slot in `EntityRegistry` is already owned by its corpse loot container
   (`NetworkLootContainerInteractable`, which presenters resolve with `TryGetInteractable`), and a
   registry slot holds one interactable per entity id. The revive interactable therefore registers
   through `EntityRegistry.TryRegisterSupplementalInteractable`, leaving the primary slot and
   every collider mapping untouched. `EntityRegistry.TryGetInteractionHandler` is what the
   interaction pipeline (`PlayerInteractionNetworkController`, `LocalInteractionCandidateSource`)
   now uses: with no supplemental it returns the primary interactable unchanged; otherwise one
   handler that tries the primary first and then each supplemental one. The avatar's existing
   interaction-layer trigger collider is already mapped to the avatar id, so no collider was added.
   `CanInteract` is false for every non-Downed avatar, so it never shadows another interactable,
   and false for the avatar's own interactor. It delegates the rules to
   `PlayerDownedRecoveryNetworkController.CanBeginAssisted`: interactor resolves to a
   `PlayerCharacter` that is alive, not Downed, a frozen initial teammate, in range, and the target
   has no valid session while the reviver is not already reviving.
2. `Interact` does **not** complete anything. It calls `TryBeginAssisted(reviver)`, which opens the
   session and starts `Completion`. The press edge stays the start gesture.
3. "Held" is a continuation condition, not a new interaction type. The reviver's
   `PlayerInteractionNetworkController` publishes `[Networked] IsInteractHeld`
   (`NetworkButtons.IsSet(PlayerInputButton.Interact)`) so the recovery component, which runs on
   the Downed avatar and cannot read the reviver's input, can cancel on release.
4. Downed players stay blocked from general interactions: `PlayerDownedGate` rejects the
   interactor, so a Downed player can be a target but never an interactor. Self-revive does not use
   targeting; it starts from the Downed player's own input in a dedicated component (no task).
5. **Incompatible reviver action:** `PlayerReviveGate.InterruptIfReviving(ICharacter)` (State
   Authority) interrupts the session the character is running, with `IncompatibleReviverAction`,
   and the action then proceeds under its normal rules. It is called at the authoritative choke
   points next to the Downed gates: primary attack, shield defense, equipment and Weapon Set
   changes, consumables, Loot transfer, Loot drop and new interactions. Sprint and movement already
   interrupt through `IsMoving`.
6. Self-reviver handoff (GD 13 §8) is a normal short instant interaction on an Active teammate,
   not a revive. It belongs to the item/inventory feature (no task).

`ExtractionSanctuary.CanInteract` checks `IsAlive`, which is true while Downed; the Downed
interaction gate, not `IsAlive`, is what prevents a Downed player from starting the Sanctuary
ritual (see section 13).

## 9. Functional restrictions while Downed

| Rule | Mechanism | Status |
|---|---|---|
| Limited movement, no Sprint | `PlayerMovementNetworkController` multiplies voluntary speed by `DownedMovementSpeedMultiplier` (0.35 provisional) and disables Sprint without Stamina cost | Implemented |
| Knockback eligibility (Doc 18 §6.4 / Doc 13 §5) | No new or replacement knockback while already Downed; eligibility is captured before damage. Previously authorized travel continues through Downed; definitive Defeat cancels it on the next movement tick | Implemented |
| No attack, shield, abilities | `PlayerCombatNetworkController`, `PlayerShieldDefenseNetworkController` check `PlayerDownedGate` | Implemented |
| No equipment / Weapon Set changes | `PlayerWeaponEquipmentNetworkController` gate | Implemented |
| No normal consumables | `PlayerConsumableNetworkController` gate | Implemented |
| No inventory / Loot | `PlayerLootTransferNetworkController`, `PlayerLootDropNetworkController` gates | Implemented |
| No general interactions | `PlayerInteractionNetworkController` gate | Implemented |
| No new CC/status | `PlayerCharacter.CanReceiveStatusEffects` is `IsAlive && !IsDowned`; predicate only, no CC system exists | Implemented (predicate) |
| Healing does not restore or revive | `PlayerCharacter.CanReceiveHealing` is `!IsDowned && base` | Implemented |
| Weapon visuals hidden | `PlayerWeaponPresenter` | Implemented |
| Mana-drain skills deactivate, Mana preserved | `PlayerManaNetworkController` does not reset Mana on Downed/recovery. The existing ability runtime stops active execution on Downed; periodic drain schedules and concrete abilities remain deferred. | Resource preservation implemented (TASK 199); periodic consumers deferred |
| Downed HUD (reserve, revive progress, interruption/completion notices, ally Downed) | `RaidDownedHudPresenter` reads replicated state only (see `RaidMainHudArchitecture.md`) | Implemented (TASK-215); not yet validated Host/Client |
| Downed pose | Presentation only, reads `IsDowned` | Planned (no task) |

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

## 11. Attribution of Downed and Defeat causers (Implemented, TASK-246)

GD 13 §13 and GD 05 require recording separately who caused Downed and who caused definitive
Defeat. The damage path now threads `DamageRequest.AttackerId` through the fatal-hit and Downed
reserve hooks. `EntityId` value `0` means environment or no identified attacker.

Data lives on the Downed controller, next to the cycle it belongs to:

```text
[Networked] int DownedCauserEntityId   // attacker of the entry hit; 0 = environment / none
[Networked] int DefeatCauserEntityId   // attacker of the depleting hit; 0 = drain, environment, forced
[Networked] int DefeatedCycle          // DownedCycle at definitive Defeat; 0 = never defeated
```

Implemented hooks: `TryInterceptFatalDamage(in DamageRequest)` and
`TryEnterDowned(EntityId attackerId)`; `TryApplyDownedDamage(..., EntityId attackerId, ...)`.

Rules:

* The Downed causer is written once per cycle at entry and is provisional (GD 05).
* Hits on a Downed target never write `DownedCauserEntityId` and never create a new credit.
* The Defeat causer is written only by a hit that depletes the reserve; pure drain writes `0`.
* **Consolidation:** the optional `IDownedCycleObserver` is called synchronously on State
  Authority after entry, after definitive clearing (`DefeatedCycle == X`), and on recovery.
  `PlayerPvpCombatCreditNetworkController` freezes Last Hit and the selected eligible Assists at entry,
  confirms once on definitive Defeat of that same cycle, and cancels all provisional credit on revive.
  Drain, reserve damage, absence and collapse use the same definitive-clear notification.
* Resolving `EntityId` to a participant uses the existing `RaidAvatarParticipantLink` /
  `NetworkRaidParticipant` path, as `DamageResolver` already does for contributions.
* **Authority:** GD 19 §§4–6, 14 owns PvP attribution; other Game Design documents complement it.
  Last Hit requires a resolvable original player source and target participant plus resolved initial
  enemy affiliation (fail closed). Downed/Defeated source state does not erase attribution.
  Creature/environment/unknown sources create no Last Hit or Assist.
* `CombatContributionTracker` records effective Active-target damage and Empower support with the
  latest kind and resolution tick per participant. The target is implicit in the owning avatar;
  recording implies effectiveness. Neither the downing hit nor any hit on a Downed target is recorded,
  including a hit that clears Downed. Existing PvE source/current-avatar behavior is preserved.
* Effective Restoration on another ally records `Healing` kind and tick on that ally's credit
  controller. Full-health and self heals grant no support. At entry, candidates include recent
  damage/Empower contributors and recent healers of the Last Hitter or a damage contributor.
  Every candidate must be an initial teammate of Last Hit, excluding Last Hit and the victim.
* **Assist limit:** serialized `_maxAssistsPerTarget`, default `1` per Downed cycle (GD 05 §19 table);
  `0` keeps every eligible teammate (GD 19 §6.2 / T03-05, which still reads unlimited — open design
  discrepancy). When the limit applies, the most recent in-window contribution or support before
  Downed wins; a same-tick tie goes to the lowest `RaidParticipantId`.
  CC/debuff and Protective Orb interception are not MVP producers.
* Each cycle resets **credits**, not contribution history. An effective Active contribution still
  within the configurable window can qualify again after revive in a new cycle (GD 19 §6.1).
  A contributor becoming Downed does not cancel Assist. A terminal participant may retain the
  relationship but receives no new XP.
* Definitive confirmation grants `PvpKill` (serialized base, default 25) and each `PvpAssist`
  (integer-floor base / 2, default 12) through the participant's existing Experience ledger.
  Replicated pending bits are consumed only after ledger acceptance, or dropped for terminal
  recipients/non-positive amounts. Missing participants or rejected ledgers retry every second.
* Credit cycle, Last Hit participant value, Assist/pending masks, confirmation, retry timer,
  contribution/support ticks and kinds are `[Networked]`. `CopyStateFrom` preserves them and
  `Spawned()` skips fresh initialization on Host Migration restore; stable participant values need
  no reference remap. State-copy tests do not prove real migration.
* The player avatar now composes `CombatContributionTracker`. Its registry unregistration is
  isolated (it owns no colliders), so it never clears the avatar's collider mappings when it is the
  last capability to unregister; projectiles keep recognising their despawning owner's collider.
* **Known limits:** a despawned original avatar or participant can no longer be resolved from
  `DamageRequest.AttackerId` (an avatar `EntityId`, not durable source identity). Still-spawned
  defeated support recipients are resolved through a runner-scoped fallback when their current-avatar
  binding has been cleared; no lifecycle state alone invalidates their contribution.
  Despawning the victim avatar during pending retries loses that producer; an absent participant
  can leave a pending bit until the producer despawns. No durable reward journal is added.

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

Implemented (`NetworkSpawnManager.OnPlayerLeft`, `RaidParticipantConnectivity`, `RaidConnectivityContinuityArchitecture.md`):

* A `Raiding` participant (Active or Downed) is retained on disconnect, as long as they have a valid decider teammate and configuration. The Host removes the avatar's input authority and tracks the participant's connectivity state and budget.
* For a Downed avatar, the reserve keeps draining and the avatar can still take damage. Depletion runs the normal
  `HandleDeath` path; `TryMarkDefeated` is keyed by `CurrentAvatarId`, not `PlayerRef`.
* While retained and still `Raiding`, the participant counts in `HasRaidingParticipants` and is
  aborted by `AbortRaidingParticipantsForClosure`.

Recovery behavior (TASK 452, US-52; the Self-revive row remains planned):

| Case | Behavior |
|---|---|
| Downed player disconnects, no session | Retained (if eligible); budget consumes. Continues draining; can still be revived or defeated (GD 13 §11). Can reconnect to resume. |
| Downed player disconnects during a session | Session continues and may complete. Reconnection restores input authority to the Active avatar. |
| Reviver disconnects | Session is interrupted (section 7) |
| Downed disconnected player is revived | Returns Active with Health 1 and no input authority until reconnect; reconnects directly to Active state. |
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
* `TickTimer Completion` is a Fusion value that survives migration; the recovery component
  revalidates the session on every authoritative tick (reviver resolves, cycle, range) and clears
  it when any check fails, which is what interrupts an open session after a migration.
* The coordinator keeps no state of its own: it recomputes from restored avatar and participant
  state on its first tick after the recovery window.
* **Known limitation (TASK 452):** an open recovery session is **interrupted, not resumed**, by
  Host Migration. `ReviverId` and `RevivingTargetId` are `NetworkId`s that migration does not
  remap, so the restored session fails to resolve its reviver on the first authoritative tick and
  is cleared safely (the Downed player keeps the reserve and a reviver can start again). Not
  validated with a real migration.
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
| Q4 | Revive range, hold duration, Accelerated Resolution seconds | Game Design / balance | Revive resolved: 4 s and 1.5 u, serialized on `PlayerDownedRecoveryNetworkController`. Acceleration (~5 s) still open |
| Q5 | Can a Downed player start the Sanctuary ritual, or only continue one in progress? | Game Design (GD 06 / 13) | Resolved by GD 06 §11 (only an Active member starts the ritual; becoming Downed does not cancel it) and GD 06 §13/§16 (the countdown starts by presence, so a connected Downed player in the area counts and can complete). The interaction gate already matches the start rule. Presence-based countdown acceptance of a Downed player is not yet verified by tests |
| Q6 | Does Dungeon absolute close force Defeat or Abort for Downed players? | Game Design (GD 10) | Resolved by GD 10 §5.1 and GD 13 §13: absolute close gives definitive Defeat to everyone still inside, ignoring reserve and recovery. **Implementation gap:** closure currently aborts `Raiding` participants, Active ones included (pre-existing). Owner: TASK 453 or a Dungeon Session task; section 14 describes `ForceDefinitiveDefeat` |
| Q7 | Do "both immobile" and "interrupted by movement" mean any movement, including the Downed's limited movement and knockback? | Game Design | Resolved: voluntary movement (`IsMoving`) of either avatar interrupts; knockback interrupts only when it breaks the range (checked by `PlayerDownedRecoveryNetworkController`, not a knockback signal). Doc 18 §6.4 / Doc 13 §5 reject new/replacement knockback on an already Downed avatar; previously authorized travel completes unless definitive Defeat cancels it on the next movement tick |
| Q8 | Reviver damage detection: any `Health` loss, or any hit including fully mitigated ones? | Combat Design | Resolved as a detectability decision: `Health` loss (polling); fully mitigated hits do not interrupt |
| Q9 | PvP Last Hit attribution rules and amounts | GD 19 §§4–6, 14; balance decision | Resolved for TASK-246: section 11, base 25, floor-half Assist, Assist limit 1 (0 = unlimited) |

## 18. Implementation status

| Item | Status | Reference |
|---|---|---|
| Downed reserve rules, controller, damage pipeline hooks | Implemented (TASK 451) | `562b077f`, `5765d017`, tests `2af5a355` |
| Action gates (attack, shield, consumables, Loot, interaction, equipment) | Implemented (TASK 451) | `066356fc` |
| Limited movement, no Sprint | Implemented (TASK 451) | `5d21e50d` |
| Weapon visuals hidden | Implemented (TASK 451) | `5ff7c40e` |
| Disconnect retention | Implemented (TASK 451) | `dc4745d5` |
| Downed docs (combat, movement, defeat, migration) | Implemented (TASK 451) | `ff95a44b`, `ea9be1d4` |
| Exit contract, `ForceDepleteForRecovery`, `RestoreHealthAuthoritatively`, pure rules | Implemented (TASK 452) | `84063f59`, `93ac7eeb` |
| Recovery session owner, `IsInteractHeld`, drain gate, damage observer | Implemented (TASK 452) | `5ff5208b` |
| Revive interactable (supplemental registry slot), incompatible-action gate | Implemented (TASK 452) | `d88d5c01` |
| End-to-end and interruption PlayMode coverage | Implemented (TASK 452) | `4bdf7d7f` |
| Revive progress UI and feedback (GD 13 §14) | Implemented (TASK-215), pending Host/Client validation | `RaidDownedHudPresenter`, outcome record in section 7 |
| Revive pose | Planned | no task |
| Attribution fields and damage hook signatures | Implemented (initial TASK 453 slice; section 11) | This change |
| PvP credit consolidation/freeze at Downed | Implemented; single-runner PlayMode verified, Host/Client pending | TASK-246, section 11 |
| Extraction re-evaluation on Defeat | Pending | TASK 453 |
| Forced definitive Defeat on absolute close (Q6 gap) | Planned | TASK 453 or a Dungeon Session task |
| Self-revive item, consumption, handoff, Self session | Planned | no task |
| Accelerated Resolution coordinator | Planned | no task, needs a task |
| Mana runtime interaction | Planned | no task (no Mana runtime exists) |
| Downed pose | Planned | no task |

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

## 20. TASK 452 coverage

| GD 13 interruption row | Covered at |
|---|---|
| Release of Interact | PlayMode (`PlayerDownedRecoveryPlayModeTests`) |
| Voluntary movement of the reviver | PlayMode with real input (`PlayerDownedReviveFlowPlayModeTests`) |
| Voluntary movement of the Downed | PlayMode with real input (`PlayerDownedReviveFlowPlayModeTests`) |
| Range loss | PlayMode (teleport); a knockback that breaks range: rule level only |
| Damage to the reviver / to the Downed | PlayMode |
| Incompatible reviver action | PlayMode: attack, consumable, equipment, Loot transfer and drop. Shield defense and a new interaction by the reviver: rule level and code review only |
| Reviver Downed | PlayMode |
| Reviver disconnect | PlayMode (avatar despawn); participant leaving `Raiding`: rule level only |
| Downed definitive Defeat | PlayMode |
| Downed disconnect does not interrupt | PlayMode (input authority removed); the real retention path is covered by TASK 451 tests |
| Retry from zero, no replay of a held input, full flow to Active with 1 HP and Stamina 0 | PlayMode |

Not validated (manual): Host and Client with two instances in both directions, a real disconnect
of either player during a session, and Host Migration in the middle of a session (section 16).

## 21. TASK 453 technical debt (2026-10-04)

**TASK 453 remains partial.** The initial attribution fields and damage hooks are implemented;
the remaining integration and validation gaps below are recorded as debt, not waived acceptance
criteria. HacknPlan remains the work-tracking source of truth; this section records technical
evidence and closure checks without creating another tracker or changing approved contracts.

| Debt | Current evidence and impact | Closure check |
|---|---|---|
| Frozen PvP Last Hit / Assist | Implemented by TASK-246 (section 11): entry freezes credit, definitive Defeat confirms it once, revive cancels it. Contribution history remains window-based across cycles. | `PvpCombatCreditPlayModeTests` (31 tests, passing) covers ordered hits, finishers, revive/new cycles, support, terminal recipients, retry and state copy. Real Host/Client sessions and Host Migration remain unverified. |
| Required-team extraction composition | Section 13: extraction is currently per player; no owning composition path has been identified for required-team re-evaluation. A member's definitive Defeat must update eligibility without treating Downed as terminal. | Identify the existing extraction owner before changing it; verify connected Downed presence/countdown, Defeat interruption and required-team re-evaluation. Do not invent a second coordinator. |
| Disconnected Downed retention after migration | Section 16: `_retainedDownedParticipants` is Host runtime-only bookkeeping, absent from the snapshot. A disconnected Downed avatar is not guaranteed to remain retained or reach Defeat after migration. | Resolve recovery ownership through the existing migration/departure path; verify retention, drain, damage and exactly-once terminal resolution after a real Host Migration. |
| Absolute close: Abort versus definitive Defeat | Sections 14 and 17/Q6: closure currently aborts `Raiding` participants; the documented GD rule requires definitive Defeat for everyone still inside. Ownership between TASK 453 and a Dungeon Session task remains unresolved. | Confirm the owning task/system, then verify authoritative, idempotent forced Defeat for Active and Downed avatars, ignoring reserve/recovery and using existing corpse/Loot/Results owners. |
| Multiplayer and migration proof | The focused tests reported for the initial slice do not establish Host/Client or migration behavior. Sections 12-16 still require runtime evidence. | Run Host/Client in both roles, damage/drain Defeat, revive and a second Downed cycle, extraction races, real disconnect and Host Migration; check exactly-once corpse, Loot and terminal Results. |

### Operational blockers (separate from gameplay debt)

The implementation attempt reported a commit blocked by `.git/index.lock`, a test-associated
`ProjectSettings/EditorSettings.asset` change from `0` to `1`, and extraction-configuration errors
in the Unity Console. Their root causes are not established here. Preserve the current source
slice, font and serialized settings; do not remove the lock or revert assets speculatively.
Native review and the work-unit commit remain pending. These observations are not new gameplay
requirements and do not prove the extraction integration debt caused the Console errors.
