# Raid Participant Architecture

## Context and decision

`NetworkRaidParticipant` is the raid `PlayerObject` for one admitted `PlayerRef`.
It owns session-scoped profile identity, selected build, terminal participant state and
the current avatar reference. `NetworkPlayerMelee` and `NetworkPlayerRanged` are
temporary avatars, never the persistent participant identity.

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
matching `ResultSequence` after its idempotent stash commit. TASK-58 deliberately disables
the previous loadout-saving listener because it cleared raid inventory before that ACK.

## Presentation and return

Local camera, HUD and minimap verify that their avatar matches the participant's
`CurrentAvatarId`; they must not infer it from `runner.GetPlayerObject`. A terminal result
is observed by presentation, which sends one local return request to
`SessionConnectionCoordinator`. No UI component shuts down a runner or loads MainMenu.

`Defeated` and `Extracted` use distinct result presentations. Defeat reports the fallen
avatar and permits an explicit return. Extraction reports success, displays the pending
stash commit and keeps return disabled until `IsExtractionCommitConfirmed` corresponds
to the current `ResultSequence`.

The avatar's Fusion `Spawned` callbacks run before `NetworkSpawnManager` can assign
`CurrentAvatarId` after `runner.Spawn` returns. Camera and HUD binders therefore observe
the relationship during `Render`: they retry until both replicated directions resolve,
then release their local bindings if the avatar stops being current. This observation is
presentation-only and never advances participant simulation state.

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
