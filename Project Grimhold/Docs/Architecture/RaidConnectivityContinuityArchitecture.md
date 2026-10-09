# Raid Connectivity and Continuity Architecture

## 1. Context and Dimensions

Historically, disconnecting from a Raid immediately finalized the participant as a definitive disconnect (or aborted). This architecture separates connectivity from the functional state of the participant.

A participant's state exists in two independent dimensions:
1. **Functional State**: `Raiding` (Active or Downed), `Extracted`, `Defeated`, `Aborted`.
2. **Connectivity**: `Connected` or `Disconnected`.

Disconnection is **neither** a gameplay result nor an expedition state. A disconnected player who was `Raiding` remains in the Dungeon, exposed, and functional.

## 2. Model: RaidParticipantConnectivity

The connectivity state and budget are managed by a new dedicated `NetworkBehaviour`, `RaidParticipantConnectivity`, co-located on the `NetworkRaidParticipant` prefab. It enforces `[DisallowMultipleComponent]`.

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
| Peer Departure &rarr; Retention | State Authority (Host) via `NetworkSpawnManager.OnPlayerLeft`, delegating to pure rules | Participant is `Raiding` (Active or Downed), has an eligible decider, and config is valid. |
| Budget Consumption | `RaidParticipantConnectivity` (State Authority, `FixedUpdateNetwork`) | `IsDisconnected && State == Raiding && RemainingBudgetTicks > 0` |
| Budget Exhausted &rarr; Decision Available | Derived from `RaidParticipantConnectivity` | `RemainingBudgetTicks == 0` (sticky state). |
| Retained &rarr; Connected (Reconnect) | State Authority via validated rebind | Requesting profile is retained, `RaidGenerationId` matches, `Raiding`, not yet resolved. Pauses budget consumption. |
| Confirm "Continue without teammate" | State Authority validating RPC from teammate | Teammate confirms absence. State Authority resolves race conditions. Idempotent. |
| No Decider &rarr; Legacy Finalization | `NetworkSpawnManager` (Host) | The last eligible decider disappears (disconnects/dies), leaving a retained participant without a decider. (Pending GD confirmation P-2) |
| Natural Defeat (drain/damage) | Existing `HandleDeath` &rarr; `TryMarkDefeated` | A retained Downed participant drains to 0 or takes fatal damage. |

## 4. Required Composition and Decider

### Required Composition
For extraction and session logic (GD 06 §15–16, GD 02 §15), an active/recoverable member is defined as any participant who is `Raiding` (Active or Downed), **regardless of connectivity**. 
Excluded states: `Defeated`, `Aborted`, `Extracted`. 
*Note: A disconnected participant is considered part of the composition but cannot personally initiate or complete extraction.*

### Decider Definition
A "Decider" is a teammate with the same `RaidTeamId` whose participant is `Raiding` and `Connected`. 
A teammate who is `Defeated` (spectating) is **not** eligible to decide the fate of a disconnected player.

## 5. Retention and Legacy Finalization Policies

### Not Marking Terminal
Crucially, when a profile is retained upon departure, **it must NOT be marked as terminal** in the `_controlledReturns` registry. Marking it terminal would cause `TryAdmitPlayer` to reject the reconnection attempt. The terminal marking is reserved for definitive exits (Defeated, Extracted, Abandoned).

### Session Reopening Policy
`SessionInfo.IsOpen` dictates whether the match accepts incoming connections. The session gate logic is:
* `IsOpen = true` **only** while there is &ge; 1 retained, disconnected participant.
* Reopening the session validates by `ProfileId` + `RaidGenerationId` and disconnects unauthorized joiners. 

### Fallback Without Decider
If a retained participant loses their last eligible decider (e.g., the teammate dies or disconnects), the retained participant undergoes the legacy finalization path (definitive disconnection). This fallback is handled during Raid closure re-evaluations. 

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
* If a Downed disconnected player naturally reaches Defeat, the standard `HandleDeath` corpse generation applies. The budget logic ceases to apply.
* Forced Defeat (via teammate confirmation) cleans up the Downed state (if any) and initiates `HandleDeath` without applying synthetic combat damage or Kill XP. 

### Extraction
* A disconnected participant **cannot** initiate or complete their extraction.
* If a countdown is in progress when the player disconnects, it is **cancelled**.
* The Sanctuary ritual (interaction) is **not** cancelled by disconnection.
* An Active/Downed connected participant within the zone remains valid.

### Match Closure
* Retained participants count as active for `HasRaidingParticipants`.
* `AbortRaidingParticipantsForClosure` ensures they are cleanly aborted if the match closes (e.g. Host shutdown) without leaks.

### Host Migration
* Retained disconnected participants face a known limitation: since `_retainedDownedParticipants` and retention status are Host-runtime-only state (not networked), a participant who is disconnected during Host Migration might not be correctly retained on the new Host. This is an accepted limitation and is not redesigned here. 

## 8. Pending Deadlines and Values (Game Design)

The following values are structurally supported but lack production defaults. They must not be invented by engineering:
* **P-1**: Consequences of XP for Forced Definitive Defeat of a disconnected player (document 05). Currently, `Defeated` retains 20%.
* **P-2**: Fallback behavior when a retained player is left without a decider (fallback to legacy completion).
* **P-3**: Time values (seconds) for the reconnect budget.

## 9. Follow-ups (Outside Scope)

* **FU-1**: Reconnecting a participant who is already `Defeated` (entering as a spectator or viewing Results).
* **FU-2**: Full team extraction mechanics (shared quota, shared Sanctuary, shared countdown as required by GD 06 §15).
* **FU-3**: Host Migration coverage for retained disconnected participants.
* **FU-4**: Automatic retry or countdown UI on the client side for reconnection.

## 10. Report on SessionInfo Mutability in Fusion 2.1.1

**Feasibility Verification for US-52:**
In Photon Fusion 2.1.1 (Host/Server Mode), `Runner.SessionInfo.IsOpen` and `Runner.SessionInfo.IsVisible` are mutable properties at runtime. 
* Current usage in `NetworkMatchController` sets both to `false` during the transition to `MatchPhase.Starting` without requiring a runner restart.
* Mutating `Runner.SessionInfo.IsOpen = true` during `MatchPhase.InProgress` correctly propagates the change to the matchmaking server, allowing new connections to the session.
* Conclusion: The architecture's requirement to reopen the session dynamically (to allow re-joins while preserving the session gate) is fully supported by the underlying networking framework without requiring session teardowns.
