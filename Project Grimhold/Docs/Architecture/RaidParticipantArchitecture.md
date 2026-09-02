# Raid Participant Architecture

## Context and decision

`NetworkRaidParticipant` is the raid `PlayerObject` for one admitted `PlayerRef`.
It owns session-scoped profile identity, selected build, terminal participant state and
the current avatar reference. `NetworkPlayerMelee` and `NetworkPlayerRanged` are
temporary avatars, never the persistent participant identity.

Each participant also replicates the `RaidGenerationId` assigned by
`NetworkMatchController`; it is an identity marker only and does not duplicate
inventory, extraction or lifecycle state.

Each fresh participation also freezes the confirmed `CharacterAttributeState` supplied by the
current profile source. The flow is `profile source -> participation admission/initialization ->
NetworkRaidParticipant -> gameplay consumers`. State Authority writes the seven values and then
publishes the initialization marker; the public try-pattern read therefore never exposes a partial
snapshot. `NetworkRaidParticipant` remains the only source of truth for these attributes during
the participation. `RaidAvatarParticipantLink` delegates reads to it and owns no attribute copy.
Redistribution, profile validation and persistence remain outside the Raid runtime boundary.

The PlayerObject co-locates one `PlayerExpeditionExperienceLedger` that owns the
participant's provisional Expedition Experience independently from the avatar. Its
authority, lifecycle and producer-idempotency boundary are
defined in `Docs/Architecture/ProgressionArchitecture.md`.
It also co-locates one `PlayerExpeditionProgressionResolver`, which owns the admitted baseline
and the immutable resolution/application history for this participation.

State Authority alone transitions `Raiding` to `Defeated`, `Extracted` or `Aborted`.
The transition state is not another health, extraction or inventory source of truth:
it records the authoritative outcome after those existing systems have completed.

## Avatar lifecycle

`NetworkSpawnManager` spawns participant first, then exactly one build-selected avatar,
links both objects and registers only the participant with `SetPlayerObject`. The avatar
has the same Input Authority solely to consume regular player input.

On defeat, `PlayerCorpseGenerationController` completes the co-located inventory handoff
before `RaidAvatarParticipantLink` records `Defeated` and removes the avatar Input
Authority. The avatar stays spawned as the synchronised lootable body. Voluntary abandonment
reuses that material Loot handoff without synthesizing combat damage or Kill Experience; only
after it succeeds does the participant record technical `Aborted`, resolve `Abandoned`, and
expose Results. Its durable progression ACK confirms persistence but does not authorize Return;
the participant remains terminal until an explicit Return request is accepted by State Authority.

Extraction enters `Extracted`, but return remains unavailable until `PlayerExtractionLootSaver`
completes persistence, extracted-Loot Experience and Progression for the matching
`ResultSequence`. `PlayerLootReceiver` derives
its mutation lock from the existing extraction state, so no second inventory-lock source
is replicated. The previous loadout-saving listener remains disabled because it cleared Raid
inventory before that ACK. `PlayerExtractionLootSaver` retains the complete snapshot and
resends it from State Authority until the local commit is acknowledged; duplicate receipts
are accepted idempotently and a local persistence failure requires an explicit retry.

## Presentation and return

While `Raiding`, local camera, HUD and minimap verify that their avatar matches the
participant's `CurrentAvatarId`; they must not infer it from `runner.GetPlayerObject`.
An unresolved avatar-to-participant link leaves presentation unbound until `Render` can
resolve both directions. The direct-development composition without a participant link
may still fall back to avatar Input Authority.

After `Defeated`, the avatar is no longer current or locally controllable, but the local
participant retains Input Authority and therefore owns the terminal HUD composition. The same
terminal ownership is retained for `Aborted` only when its semantic cause is
`VoluntaryAbandonConfirmed`; other aborted states do not gain this exception. Existing extracted
HUD ownership remains unchanged. The HUD remains bound without restoring gameplay input.
`Spectating` is local presentation,
never a `RaidParticipantState`. A defeated Client may observe or request Return; the
canonical Host enters spectator automatically and cannot Return while sustaining the
runner's State Authority. Remote defeated bodies cannot own local HUD. Presentation
observes return authorization and delegates the local runner transition to
`SessionConnectionCoordinator`. No UI component shuts down a runner or loads a scene.

`Extracted`, `Defeated` and voluntary abandonment present the immutable
`ExpeditionProgressionResult` captured after resolver commitment. Before that snapshot is
available the menu reports that Results are processing. Persistence feedback remains independent
from the summary, and its ACK never authorizes Return by itself. The button is only a local
reflection of snapshot, ACK, extraction and role conditions; `RequestReturn` remains the
authoritative boundary and a rejected request does not consume future local attempts.

Once State Authority accepts an explicit request it publishes `IsReturnAuthorized`.
Presentation observes that state and delegates to `SessionConnectionCoordinator`, which alone
owns the Raid-to-Town runner transition.

Return authorization and departure classification are specified in
`Docs/Architecture/RaidDefeatAndSpectatorArchitecture.md`.

The avatar's Fusion `Spawned` callbacks run before `NetworkSpawnManager` can assign
`CurrentAvatarId` after `runner.Spawn` returns. Camera and HUD binders therefore observe
the relationship during `Render` and retry until both replicated directions resolve.
Camera and active-gameplay bindings may be released when the avatar stops being current;
the HUD binder retains the local terminal composition for defeat and confirmed voluntary
abandonment, without treating every `Aborted` state as abandonment.
This observation is presentation-only and never advances participant simulation state.

## Host migration

Snapshot restoration remaps both directions of the participant/avatar relationship before
reassigning PlayerObject and avatar input authority. A defeated participant has no current
avatar, so its restored body remains without player control.

`CopyStateFrom` restores the ledger accumulators/freeze/producer marker, extraction phase and
candidate, and resolver baseline/history with the participant. None is reinitialized or requires a
NetworkId remap.

The participant's networked character-attribute snapshot and its initialization marker are restored
by the same Fusion state copy. Host Migration never reloads or replaces them from persistence.

## Follow-up dependencies

The admission flow initializes the admitted avatar loadout. `PlayerExtractionLootSaver` calls
`TryConfirmExtractionCommit(ResultSequence)` only after its local persistence ACK. These
responsibilities may not change PlayerObject identity or create a parallel participant coordinator.

## Unity prefab composition

`Assets/Prefabs/NetworkRaidParticipant.prefab` is the registered Fusion participant prefab
and contains its `NetworkObject`, `NetworkRaidParticipant` and exactly one
`PlayerExpeditionExperienceLedger` plus exactly one `PlayerExpeditionProgressionResolver` network
behaviour.
`FusionSessionLauncher._raidParticipantPrefab` on `Assets/Prefabs/Systems.prefab` references
that asset. `RaidAvatarParticipantLink` lives on the base `NetworkPlayer` prefab and is
therefore inherited by both catalog avatars (`NetworkPlayerMelee` and
`NetworkPlayerRanged`). EditMode composition tests protect these assignments and the
corresponding Fusion `NetworkedBehaviours` lists.
