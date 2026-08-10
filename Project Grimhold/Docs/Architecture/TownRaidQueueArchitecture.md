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

`RaidAdmissionDataCodec` is intentionally separate from the Town `PlayerJoinDataCodec`.
Version 2 carries raid id, secret, profile, selected build, reservation id and the
player's reserved snapshot. It uses explicit UTF-8 lengths, Int32 quantities and
an exact 512-byte project limit (maximum 16 distinct entries, 9,999 each).
The coordinator creates the durable reservation before ACKing the manifest.
`NetworkSpawnManager` validates credential, profile, build, reservation shape,
catalog and capacity in State Authority; it never reads a remote local profile.
Admission succeeds only after participant, avatar and exact receiver inventory
exist. Pre-admission failure rolls back; post-admission failure keeps it pending.

The admission closes when every manifest profile is admitted or after the configured timeout. A player whose admission fails after connecting is disconnected, including duplicate-profile races. The raid scene was already loaded by the coordinated launch, so the match controller advances to `InProgress` without loading it again.

## Configuration required outside this task

The Town NPC prefab must remain a `MasterClientObject` with a trigger collider, `TownRaidQueueNetworkController`, `TownRaidNpcInteractable` and `InteractionPromptMetadata`. The SocialPlayer network prefab contains `SocialPlayerIdentity` and `TownRaidQueuePresenter`; its local view is created only for Input Authority. Queue request methods return whether Fusion accepted the local invocation or transport attempt so presentation can report immediate transport failure.

## NPC de stash y presentación local

El NPC de stash comparte la frontera de interacción confirmada, pero no comparte
el estado de la cola ni el estado de red del jugador:

```text
TownStashNpcInteractable
  -> InteractionResolved confirmado
  -> TownStashPresenter (sólo Input Authority)
  -> TownStashView local
  -> StashInventory.prefab / LobbyStashPresenter
  -> ApplicationStashContext / LocalProfileStore
```

`TownStashNpcInteractable` sólo registra sus colliders en `EntityRegistry` y
devuelve un resultado exitoso para el `TargetId` correcto. `TownStashPresenter`
resuelve ese objetivo exacto, comprueba que el perfil local está disponible y
abre una única instancia local de `StashInventory`; no publica stash ni loadout
en Fusion. La UI reutiliza el prompt genérico de
`LocalInteractionCandidateSource` y `InteractionPromptMetadata`.

Mientras la vista está abierta se adquiere un único token de supresión de input.
Escape, una nueva pulsación de interacción, pérdida del runner, despawn,
disable o destrucción cierran la vista y desuscriben los listeners. Los commits
mostrados siguen siendo los de `ApplicationStashContext`, cuya fuente
persistente es el repositorio local versionado documentado en
`Docs/Architecture/LocalPlayerPersistenceArchitecture.md`.

## Limits

The initial cohort cap is four and solo launch is permitted. Host departure before launch dissolves the queue. This feature does not add party persistence, automatic matchmaking, a backend, re-entry, or general Host Migration changes. TASK-79 may extend the manifest ticket with loadout data; TASK-80 owns extraction persistence.
