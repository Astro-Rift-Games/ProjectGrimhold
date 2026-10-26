# Raid Connectivity and Continuity Architecture

**Status:** Implemented (US-52 / Story #471, TASK-187, TASK-188, TASK-189; aligned with US-53 / Story #475, TASK-190, TASK-191)

## 1. Context and Dimensions

Historically, disconnecting from a Raid immediately finalized the participant as a definitive disconnect (or aborted). This architecture separates connectivity from the functional state of the participant.

A participant's state exists in two independent dimensions:
1. **Functional State**: `Raiding` (Active or Downed), `Extracted`, `Defeated`, `Aborted`.
2. **Connectivity**: `Connected` or `Disconnected`.

Disconnection is **neither** a gameplay result nor an expedition state. A disconnected player who was `Raiding` remains in the Dungeon, exposed, and functional.

## 2. Model: RaidParticipantConnectivity

The connectivity state and budget are managed by a dedicated `NetworkBehaviour`, `RaidParticipantConnectivity`, co-located on the `NetworkRaidParticipant` prefab. It enforces `[DisallowMultipleComponent]`.

State maintained by this component:
* `[Networked] bool IsDisconnected`
* `[Networked] int RemainingBudgetTicks`: Initialized once on State Authority from `ReconnectBudgetConfig`. This budget is **cumulative**, **sticky at 0**, and is **never reset** upon reconnection.
* Initialization marker to respect `HostMigrationRestoreUtility.IsRestoreSpawn`.

Derived states (not replicated):
* `IsBudgetExhausted`: `RemainingBudgetTicks <= 0`
* `IsDecisionAvailable`: `IsDisconnected && IsBudgetExhausted && State == Raiding`

## 3. Owners and Transitions

Every transition has a strict owner and conditions:

| Transition | Owner | Condition |
|---|---|---|
| Peer Departure &rarr; Retention | State Authority (Host) via `NetworkSpawnManager.OnPlayerLeft`, delegating to pure rules | Participant is `Raiding` (Active or Downed) and budget config is valid (regardless of whether a teammate decider exists). |
| Budget Consumption | `RaidParticipantConnectivity` (State Authority, `FixedUpdateNetwork`) | `IsDisconnected && State == Raiding && RemainingBudgetTicks > 0` |
| Budget Exhausted &rarr; Decision Available | Derived from `RaidParticipantConnectivity` | `RemainingBudgetTicks == 0` (sticky state). |
| Retained &rarr; Connected (Reconnect) | State Authority via validated rebind | Requesting profile is retained, `RaidGenerationId` matches, `Raiding`, not yet resolved. Pauses budget consumption. |
| Confirm "Continue without teammate" | State Authority validating RPC from teammate | Teammate confirms absence. State Authority resolves race conditions. Idempotent. Forced defeat with 0% XP. |
| No Decider &rarr; Retention Maintained | `NetworkSpawnManager` (Host) | Under authoritative GD (Doc 06 §18 & §19), the absence of a decider (solo, or teammate dead/disconnected) does NOT finalize the retained raider. The character remains in Dungeon consuming budget until reconnection, natural defeat, or match closure. |
| Natural Defeat (drain/damage) | Existing `HandleDeath` &rarr; `TryMarkDefeated` | A retained Downed participant drains to 0 or takes fatal damage (0% provisional XP retained if disconnected). |

## 4. Required Composition and Decider

### Required Composition
For extraction and session logic (GD 06 §15–16, GD 02 §15), an active/recoverable member is defined as any participant who is `Raiding` (Active or Downed), **regardless of connectivity**. 
Excluded states: `Defeated`, `Aborted`, `Extracted`. 

Under team extraction (US-53, `TeamExtractionCoordinator` / `TeamExtractionRequirementRules`):
* A disconnected active or recoverable member remains part of the required team composition (`TeamMemberExtractionSnapshot.IsConnected == false`).
* Because all required members must be connected and within the Sanctuary zone, having a disconnected teammate blocks the start of the team extraction countdown (`TeamExtractionCancelReason.RequiredMemberDisconnected`).
* If a team countdown is running and a member disconnects, the countdown cancels immediately (fail-closed).
* When a teammate confirms absence ("Continuar sin compañero"), the absent member is forced into definitive defeat (`DefeatedWhileDisconnectedConfirmed`, 0% XP) and permanently removed from the required composition. The connected survivor can then initiate and complete team extraction alone.
* The team quota is **never** reduced when a member is defeated or absent; the team progress requirements remain constant.
* If the composition of required members changes while a countdown is in progress, the countdown cancels and restarts cleanly.

### Decider Definition
A "Decider" is a teammate with the same `RaidTeamId` whose participant is `Raiding` and `Connected`. 
A teammate who is `Defeated` (spectating) is **not** eligible to decide the fate of a disconnected player.

## 5. Retention and Disconnect Policies

### Not Marking Terminal
Crucially, when a profile is retained upon departure, **it must NOT be marked as terminal** in the `_controlledReturns` registry. Marking it terminal would cause `TryAdmitPlayer` to reject the reconnection attempt. The terminal marking is reserved for definitive exits (Defeated, Extracted, Abandoned).

### Session Reopening Policy
`SessionInfo.IsOpen` dictates whether the match accepts incoming connections. The session gate logic is:
* `IsOpen = true` **only** while there is &ge; 1 retained, disconnected participant.
* Reopening the session validates by `ProfileId` + `RaidGenerationId` and disconnects unauthorized joiners. 

### Misconfiguration Fail-safe (not a gameplay rule)
If `ReconnectBudgetConfig` is absent/invalid, or the participant lacks
`RaidParticipantConnectivity`, `RaidPlayerDeparturePolicy` cannot safely retain the
participant and selects definitive disconnect. The Host logs missing/invalid budget
configuration; retained participants are reevaluated with the same fail-safe.
This is an operational composition/configuration failure, not a rule that losing a
teammate decider permits eviction. With valid composition/configuration, P-2 always applies.

### Retention Without Decider
Under authoritative Game Design (Doc 06 §18 & §19), a player who disconnects without an eligible decider (e.g. Solo raid, or teammate already dead or disconnected) is retained with their reconnection budget active. Losing a decider does NOT trigger definitive disconnect or evict the raider; their character remains in the Dungeon consuming budget until reconnection, natural defeat, or match closure.

## 6. Reconnection and Race Conditions

### Payload Format
The reconnection payload is strictly **identity only**:
* `RaidCode`
* `ProfileId`
* `RaidGenerationId`
* **NEVER** `RaidAdmissionData` (loadout/baseline must not be reused, as it would cause a fresh initialization).

### Reconnection vs. Confirmation Race
Reconnections and teammate confirmations ("Continue without teammate") may race to the Host.
The Host enforces single-writer ordering:
* **The first to arrive wins.** 
* If reconnection arrives first, the option to confirm disappears, and a subsequent confirmation RPC is rejected cleanly with no effect.
* If confirmation arrives first, the participant undergoes forced Defeat. A subsequent reconnection attempt is rejected cleanly, as the profile is now terminal.

## 7. Integration Points

### Player / Downed
* The avatar remains in the world, receives damage, and its Downed drain continues.
* If a Downed disconnected player naturally reaches Defeat, the standard `HandleDeath` corpse generation applies with `DefeatWhileDisconnectedConfirmed` (0% XP). The budget logic ceases to apply.
* Forced Defeat (via teammate confirmation) cleans up the Downed state (if any) and initiates `HandleDeath` without applying synthetic combat damage or Kill XP, resolving 0% XP.

### Extraction
* Team-level extraction is coordinated authoritatively by `TeamExtractionCoordinator` (US-53, TASK-190/191).
* A disconnected participant **cannot** initiate or complete team extraction.
* A disconnected required teammate blocks the start of the team extraction countdown (`RequiredMemberDisconnected`).
* If a countdown is in progress when any required member disconnects, the countdown is **cancelled**.
* The Sanctuary ritual (interaction) is **not** cancelled by disconnection.
* `PlayerExtractionController` enforces individual restrictions and outcomes upon confirmed team extraction.

### Match Closure
* Retained participants count as active for `HasRaidingParticipants`.
* `AbortRaidingParticipantsForClosure` ensures they are cleanly aborted if the match closes (e.g. Host shutdown) without leaks.

### Host Migration
* Retained disconnected participants face a known limitation: since `_retainedDownedParticipants` and retention status are Host-runtime-only state (not networked), a participant who is disconnected during Host Migration might not be correctly retained on the new Host. This is an accepted limitation and is not redesigned here. 

## 8. Resolved Decisions and Operational Values

* **P-1 (Defeat XP)**: Forced definitive defeat from absence (`ForceDefinitiveDefeatFromAbsence`) or defeat during disconnection cleans up downed state, generates corpse loot without synthetic damage or Kill XP to the survivor, and resolves `DefeatWhileDisconnectedConfirmed` with canonical `Defeated` preserving 0% provisional experience as dictated by Game Design (Doc 05 §4 & §11).
* **P-2 (Fallback Without Decider)**: Under authoritative Game Design (Doc 06 §18 & §19), any `Raiding` participant is retained upon disconnection with their reconnection budget active, regardless of whether a teammate decider exists (Solo, or teammate dead/disconnected). Retained participants are never prematurely evicted when losing their teammate decider; their character remains in the Dungeon consuming budget until reconnection, natural defeat, or match closure.
* **P-3 (Budget Duration)**: Configured in `ReconnectBudgetConfig.asset` (300 seconds for production, 30 seconds for local rapid testing). The budget is cumulative across disconnections as confirmed by Game Design.

## 9. Follow-ups (Outside Scope)

* **FU-1**: Reconnecting a participant who is already `Defeated` (entering as a spectator or viewing Results).
* **FU-2**: Full team extraction mechanics (shared quota, shared Sanctuary, shared countdown as required by GD 06 §15) — **Implemented in US-53 (`TASK-190`, `TASK-191`)**.
* **FU-3**: Host Migration coverage for retained disconnected participants.
* **FU-4**: Automatic retry or countdown UI on the client side for reconnection.

## 10. Report on SessionInfo Mutability in Fusion 2.1.1

**Feasibility Verification for US-52:**
In Photon Fusion 2.1.1 (Host/Server Mode), `Runner.SessionInfo.IsOpen` and `Runner.SessionInfo.IsVisible` are mutable properties at runtime. 
* Current usage in `NetworkMatchController` sets both to `false` during the transition to `MatchPhase.Starting` without requiring a runner restart.
* Mutating `Runner.SessionInfo.IsOpen = true` during `MatchPhase.InProgress` correctly propagates the change to the matchmaking server, allowing new connections to the session.
* Conclusion: The architecture's requirement to reopen the session dynamically (to allow re-joins while preserving the session gate) is fully supported by the underlying networking framework without requiring session teardowns.
