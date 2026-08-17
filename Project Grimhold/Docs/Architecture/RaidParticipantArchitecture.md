# Raid Participant Architecture

## Context and decision

`NetworkRaidParticipant` is the raid `PlayerObject` for one admitted `PlayerRef`.
It owns session-scoped profile identity, selected build, terminal participant state and
the current avatar reference. `NetworkPlayerMelee` and `NetworkPlayerRanged` are
temporary avatars, never the persistent participant identity.

Each participant also replicates the `RaidGenerationId` assigned by
`NetworkMatchController`; it is an identity marker only and does not duplicate
inventory, extraction or lifecycle state.

State Authority alone transitions `Raiding` to `Defeated`, `Extracted` or `Aborted`.
The transition state is not another health, extraction or inventory source of truth:
it records the authoritative outcome after those existing systems have completed.

## Avatar lifecycle

`NetworkSpawnManager` spawns participant first, then exactly one build-selected avatar,
links both objects and registers only the participant with `SetPlayerObject`. The avatar
has the same Input Authority solely to consume regular player input.

On defeat, `PlayerCorpseGenerationController` completes the co-located inventory handoff
before `RaidAvatarParticipantLink` records `Defeated` and removes the avatar Input
Authority. The avatar stays spawned as the synchronised lootable body. On confirmed
abandon, the temporary avatar is despawned without any persistence operation.

Extraction enters `Extracted`, but return remains unavailable until TASK-80 confirms the
matching `ResultSequence` after its idempotent Loadout commit. `PlayerLootReceiver` derives
its mutation lock from the existing extraction state, so no second inventory-lock source
is replicated. TASK-58 deliberately disables the previous loadout-saving listener because
it cleared raid inventory before that ACK. TASK-80 retains the complete snapshot and
resends it from State Authority until the local commit is acknowledged; duplicate receipts
are accepted idempotently and a local persistence failure requires an explicit retry.

## Presentation and return

While `Raiding`, local camera, HUD and minimap verify that their avatar matches the
participant's `CurrentAvatarId`; they must not infer it from `runner.GetPlayerObject`.
An unresolved avatar-to-participant link leaves presentation unbound until `Render` can
resolve both directions. The direct-development composition without a participant link
may still fall back to avatar Input Authority.

After `Defeated`, the avatar is no longer current or locally controllable, but the local
participant retains Input Authority and therefore owns the terminal HUD composition. The
HUD remains bound without restoring gameplay input. `Spectating` is local presentation,
never a `RaidParticipantState`. A defeated Client may observe or request Return; the
canonical Host enters spectator automatically and cannot Return while sustaining the
runner's State Authority. Remote defeated bodies cannot own local HUD. Presentation
observes return authorization and delegates the local runner transition to
`SessionConnectionCoordinator`. No UI component shuts down a runner or loads a scene.

`Defeated` and `Extracted` use distinct result presentations. Defeat reports the fallen
avatar and exposes actions according to the canonical profile role. Extraction reports success, displays the pending
Loadout commit and keeps return disabled until `IsExtractionCommitConfirmed` corresponds
to the current `ResultSequence`.

Return authorization and departure classification are specified in
`Docs/Architecture/RaidDefeatAndSpectatorArchitecture.md`.

The avatar's Fusion `Spawned` callbacks run before `NetworkSpawnManager` can assign
`CurrentAvatarId` after `runner.Spawn` returns. Camera and HUD binders therefore observe
the relationship during `Render` and retry until both replicated directions resolve.
Camera and active-gameplay bindings may be released when the avatar stops being current;
the HUD binder instead retains only the local defeated participant's terminal composition.
This observation is presentation-only and never advances participant simulation state.

## Host migration

Snapshot restoration remaps both directions of the participant/avatar relationship before
reassigning PlayerObject and avatar input authority. A defeated participant has no current
avatar, so its restored body remains without player control.

## Follow-up dependencies

TASK-79 initializes the admitted avatar loadout. TASK-80 calls
`TryConfirmExtractionCommit(ResultSequence)` only after its local persistence ACK. Neither
task may change PlayerObject identity or create a parallel participant coordinator.

## Unity prefab composition

`Assets/Prefabs/NetworkRaidParticipant.prefab` is the registered Fusion participant prefab
and contains only its `NetworkObject` and `NetworkRaidParticipant` network behaviour.
`FusionSessionLauncher._raidParticipantPrefab` on `Assets/Prefabs/Systems.prefab` references
that asset. `RaidAvatarParticipantLink` lives on the base `NetworkPlayer` prefab and is
therefore inherited by both catalog avatars (`NetworkPlayerMelee` and
`NetworkPlayerRanged`). EditMode composition tests protect these assignments and the
corresponding Fusion `NetworkedBehaviours` lists.
