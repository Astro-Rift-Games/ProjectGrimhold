# Town and Raid Session Transition Architecture

## Context

TASK-57 replaces the previous lobby transition design. The normal application flow is:

```text
MainMenu -> Town -> Raid -> Town
```

The Town uses Fusion Shared Mode and a raid uses Fusion Host/Client Mode. A `NetworkRunner`
cannot change topology in place, so every transition shuts down and destroys the current
runner composition before creating the next one. Exactly one runner may exist in an
application at any time.

The selected `PlayerClassId` is retained only as the temporary build identifier required by
the existing join contracts. It does not establish permanent character classes.

## Decision

`SessionConnectionCoordinator` is created with the MainMenu systems composition, detached
from any scene parent, and preserved with `DontDestroyOnLoad`. It is the only owner of Town
and raid starts, shutdowns, recovery, and the direct development route. The launchers do not
reference one another.

The coordinator exposes:

- `ConnectToTownAsync(selectedBuild)`
- `EnterRaidAsync(request)`
- `ReturnToTownAsync()`
- `StartDirectRaidForDevelopmentAsync(...)`, which is explicitly outside the player flow

`SessionConnectionStateMachine` contains the transition rules without Unity or Fusion
dependencies. The coordinator serializes operations and rejects a second request while one
is active.

## States

The local connection lifecycle uses these states:

```text
MainMenu
  -> ConnectingTown -> Town
  -> ConnectingRaid -> Raid                 (development route only)

Town -> PreparingRaid -> ConnectingRaid -> Raid
Raid -> ReturningTown -> Town

Any active path -> Failed
Failed -> ConnectingTown | ConnectingRaid | ReturningTown
```

`Failed` means there is no usable active runner. A caller may retry Town connection or use
the explicit development entry point. Invalid transitions do not mutate state.

## Sources of truth and data ownership

| Concern | Source of truth | Owner |
| --- | --- | --- |
| Current local connection state | `SessionConnectionStateMachine.State` | Coordinator |
| Transition concurrency | Coordinator operation flag | Coordinator |
| Selected build | Coordinator field | Coordinator |
| Active raid request, reservation and progress | `RaidTransitionTicket` | Coordinator |
| Active Town runner identity | `HubSessionLauncher.Runner` | Hub launcher |
| Active raid runner identity | `FusionSessionLauncher.Runner` | Raid launcher |
| Town spawn positions | `HubSpawnSceneConfiguration` in `Lobby-Town` | Town scene |
| Spawned local social avatar | Fusion player object mapping | `HubPlayerSpawner` / runner |
| Gameplay simulation state | Existing raid network behaviours | Raid runner and State Authority |

The coordinator never stores references to objects owned by a unloadable gameplay scene.
The transition ticket is local lifecycle data; it is not replicated state and does not add a
second gameplay source of truth.

## Requests and results

`RaidConnectionRequest` contains the raid identifier, exact Fusion session name, and
`RaidConnectionRole` (`Host` or `Client`). A request is invalid when either identifier is
blank or the role is unsupported.

`RaidTransitionTicket` captures the validated request, selected build, immutable
`PendingLoadoutReservation` snapshot and current transition state. Admission is
complete only when participant, avatar, `CurrentAvatarId` and exact receiver
inventory are present. Before that boundary a failed launch rolls back; after it
confirmation is retried without rollback. `SessionTransitionResult` distinguishes
reservation, rollback and confirmation failures from connection failures. The
direct development route uses the same manifest, reservation, codec and admission
path as the Town queue.

## Runner lifecycle

Both launchers implement `ISessionRunnerOwner`. Each runner composition contains its own
`NetworkSceneManagerDefault` and a shutdown listener bound to the exact runner identity.

All explicit shutdowns converge on one operation:

1. Capture the runner and runner GameObject identities.
2. Await `runner.Shutdown(destroyGameObject: false)` when the runner is active.
3. Destroy the captured GameObject.
4. Wait until Unity has completed deferred destruction.
5. Clear launcher references only if they still point to the captured runner.

Callbacks whose runner identity no longer matches the launcher's active runner are ignored.
This prevents a late callback from an earlier connection from clearing a replacement runner.

## Scene loading

Normal launches always pass an explicit Fusion scene and scene manager:

- Town: `Lobby-Town`, `GameMode.Shared`, `LoadSceneMode.Single`.
- Raid: `Gameplay`, `GameMode.Host` or `GameMode.Client`, `LoadSceneMode.Single`.

Both scenes must be enabled in Build Settings. Scene and request configuration are validated
before the current runner is shut down. The direct MainMenu raid development route retains
the existing no-initial-scene behavior so its lobby tooling can control the next step.

## Town composition and spawning

`HubRunnerFactory` creates the minimal Shared Mode boundary needed by current reusable
movement and interaction contracts: the runner, scene manager, entity registry, local input
context, local join context, and `HubPlayerSpawner`. It does not create raid match,
extraction, host migration, loot, or raid spawning services.

`Lobby-Town` owns one `HubSpawnSceneConfiguration`. The spawner resolves it from
`runner.SceneManager.MainRunnerScene`, never with a cross-scene reference retained by the
coordinator. `OnPlayerJoined` and `OnSceneLoadDone` may arrive in either order: the local
player remains pending until both the player and scene configuration are ready. Before
spawning, the spawner checks the current player-object mapping; after spawning, it calls
`runner.SetPlayerObject` to make that mapping authoritative for the local runner.

## SocialPlayer boundary

`SocialPlayer` is a dedicated Fusion prefab. It retains movement, local input, visual
animation, local camera binding, and social interaction. `SocialPlayerCharacter` supplies
only entity identity and the always-available `ICharacter` contract required by interaction.

The prefab intentionally excludes combat, attacks, health and damage, death and corpse
generation, extraction, raid loot and inventory, raid HUD, visibility/minimap, and raid-only
feedback. Town presentation may observe local state but may not mutate raid gameplay state.

`Lobby-Town` contains the social spawn configuration and a `LocalCameraController`. It does
not contain a launcher or automatic debug starter; lifecycle ownership remains in the
persistent coordinator.

## MainMenu and development route

The existing Create button is relabeled `Enter Town` and calls `ConnectToTownAsync` after a
build is selected. Join Room and direct raid creation are hidden from the normal UI.

`DirectRaidDevelopmentStarter` exposes Inspector context-menu commands for direct Host and
Client startup. It still delegates to the coordinator, preserving the single-runner
invariant. `MainMenuController.JoinRoom` remains callable for development automation but its
controls are hidden in the player flow.

## Failure and recovery

If shutdown fails, the coordinator reports the appropriate shutdown or recovery result and
enters `Failed`. If raid startup fails after Town was left, the coordinator performs exactly
one recovery attempt: it cleans any partial runner and starts a fresh Shared Town runner. A
successful recovery returns the original raid failure while leaving the application in
`Town`; a failed recovery leaves no usable runner and enters `Failed`.

An unexpected shutdown of the current Town or raid runner uses the same recovery boundary.
Shutdown notifications produced during an intentional transition are ignored because the
operation already owns recovery. Application quit never starts another connection.

## Event boundaries

No event advances network simulation. Launcher shutdown notifications report infrastructure
lifecycle only, and `StateChanged` is local presentation feedback. Movement, combat, health,
extraction, and other gameplay state continue to advance through Fusion simulation and their
existing authority rules. No RPC, `[Networked]` property, event bus, or parallel coordinator
is introduced by TASK-57.

## Alternatives considered

- Reusing one runner across topology changes was rejected because Fusion sessions require a
  new runner lifecycle for the new topology.
- Allowing launchers to command each other was rejected because it creates competing
  lifecycle owners and makes recovery ordering ambiguous.
- Keeping the old scene-local Town starter was rejected because scene loading could create
  runners outside coordinator ownership.
- Deriving Town spawn data from MainMenu objects was rejected because spawn configuration
  belongs to the Fusion-loaded Town scene.
- Reusing the raid player prefab with disabled components was rejected because serialized
  raid dependencies could remain active or be re-enabled accidentally.

## Validation strategy

- EditMode: pure state transitions, request and ticket validation, busy behavior, lifecycle
  result mapping, recovery decisions, and stale runner identity handling.
- PlayMode: MainMenu entry, unique runner creation, destruction ordering, Fusion scene
  loading, one social spawn and player-object registration, camera/input rebinding, prefab
  exclusions, and a second complete cycle without stale listeners.
- Manual two-application validation: Shared Town, Host raid round trip, Client raid round
  trip, nonexistent client session recovery, unexpected shutdown recovery, second cycle, and
  observation that two runners never coexist.

Compilation, EditMode, PlayMode, and two-application Host/Client runs are separate evidence
and must be reported independently.

## Scope limits

TASK-57 does not implement party/cohort behavior, NPCs, Ready state, loadout, results,
persistence, backend, stores, or saving. TASK-58 and TASK-59 will consume the coordinator
APIs. The incorrect TASK-77 dependency is an administrative correction and does not affect
this repository architecture.
