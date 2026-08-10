# Town Raid Queue Architecture

## Context and decision

`TASK-59` connects the Shared Mode Town to private Host/Client raids without ever keeping two runners on one client. A manually configured Town NPC exposes a local queue view; it does not own queue state.

## Ownership and flow

```text
TownRaidNpcInteractable (authoritative interaction result)
  -> TownRaidQueueNetworkController (Shared Master Client authority)
  -> TownRaidQueuePresenter / TownRaidQueueView (local presentation)
  -> four directed reliable manifest fragments + local reassembly + ACK
  -> SessionConnectionCoordinator ticket
  -> Host creates hidden session / clients join its exact name
  -> NetworkSpawnManager validates RaidAdmissionData
```

The queue is a `NetworkObject` configured as `MasterClientObject`. It owns the only pending cohort, each member's Ready state and the launch sequence. `ProfileId` is the identity; `PlayerRef` only validates the active sender. `SocialPlayerIdentity` replicates that profile from the local Town join context.

The Host creates the manifest only when every member, including itself, is Ready. It has a new raid id, session name and secret. Fusion limits one RPC payload to 512 bytes and serializes each `NetworkString` at its declared capacity, so the queue sends identity, credential and two member pairs as four targeted reliable fragments. The receiver isolates fragments by `LaunchSequence`, reconstructs and validates the complete manifest, persists it locally, and only then sends its ACK. The queue waits for every ACK, releases those peers, then resets for a new cohort. Stale fragments, repeated acknowledgements and release requests do not create another raid.

Interaction results use a local fast path when Shared Mode gives the same `SocialPlayer` State Authority and Input Authority. A State Authority to Input Authority RPC is reserved for a genuinely remote owner and has local invocation disabled. Presentation is dispatched from `Render`; neither the NPC nor an RPC opens UI or shuts down a runner synchronously from simulation.

`TownRaidQueuePresenter` exists only on the local `SocialPlayer`. It renders the nearby prompt, resolves the confirmed NPC by `TargetId`, opens the queue view and converts buttons into discrete queue requests. Opening the panel suppresses gameplay input until it closes.

The queue observes `IPlayerLeft`: Host departure dissolves the cohort, while another departure removes that member. Because the frozen manifest and ACK set intentionally contain a private credential and are not replicated, a Master Client transfer during `Launching` cancels the launch and clears every locally stored ticket. `Forming` survives authority transfer.

## Raid admission

`RaidAdmissionDataCodec` is intentionally separate from the Town `PlayerJoinDataCodec`. A private raid token includes raid id, secret, profile and selected build. `NetworkSpawnManager` validates it during connection request and claims each profile once during admission. The Host session stays hidden from public matchmaking while it is open for the frozen cohort.

The admission closes when every manifest profile is admitted or after the configured timeout. A player whose admission fails after connecting is disconnected, including duplicate-profile races. The raid scene was already loaded by the coordinated launch, so the match controller advances to `InProgress` without loading it again.

## Configuration required outside this task

The Town NPC prefab must remain a `MasterClientObject` with a trigger collider, `TownRaidQueueNetworkController`, `TownRaidNpcInteractable` and `InteractionPromptMetadata`. The SocialPlayer network prefab contains `SocialPlayerIdentity` and `TownRaidQueuePresenter`; its local view is created only for Input Authority. Queue request methods return whether Fusion accepted the local invocation or transport attempt so presentation can report immediate transport failure.

## Limits

The initial cohort cap is four and solo launch is permitted. Host departure before launch dissolves the queue. This feature does not add party persistence, automatic matchmaking, a backend, re-entry, or general Host Migration changes. TASK-79 may extend the manifest ticket with loadout data; TASK-80 owns extraction persistence.
