# Town Raid Code Architecture

## Context and decision

Town and raids use different Fusion runners. Automatically moving a complete Shared Mode cohort required the Host to deliver a final release RPC immediately before shutting down Town, which allowed the Host shutdown to race the Client's receipt of that RPC.

The current MVP uses explicit six-digit raid codes instead. The Host creates a raid with a code and leaves Town. Each Client enters the same code at the raid NPC and joins independently. There is no Ready cohort, launch deadline or automatic group transition.

## Ownership and flow

```text
TownRaidNpcInteractable (confirmed local interaction)
  -> TownRaidQueuePresenter / TownRaidQueueView (local code input)
  -> SessionConnectionCoordinator (single-runner lifecycle + loadout reservation)
  -> RaidLaunchManifest.Code (deterministic session identity)
  -> FusionSessionLauncher (Host creates / Client joins exact hidden session)
  -> NetworkSpawnManager (authoritative admission and loadout validation)
```

`TownRaidQueueView` generates a six-digit suggestion locally and lets the player edit or copy it. `RaidLaunchManifest.Code` accepts exactly six ASCII digits. The normalized code deterministically creates the same session name and raid id in every application process. The canonical admission token carries the `RaidCode`; it does not carry a separate raid id, access secret or profile roster.

Creating and joining are explicit UI actions. The coordinator reserves the local loadout before leaving Town, destroys the Shared runner, creates a fresh Host or Client runner and submits `RaidAdmissionData`. A failed Client lookup is definitive and recovers that player to Town. Clients do not poll for a Host that has not created the coded session yet.

## Authority and admission

The Host starts in Gameplay immediately. A code-admitted manifest has no frozen profile list; possession of the code authorizes any valid process-local profile until Fusion capacity is reached. The Host validates the code, unique profile, selected build, reservation and exact reserved loadout. Duplicate or departed profiles remain rejected.

The session is hidden from public matchmaking but remains open while the raid is `InProgress`, allowing Clients to join later with the code. It closes through the existing authoritative raid cancellation or natural-completion flow. There is no elapsed-time admission cutoff.

Frozen-cohort manifests and the previous network queue implementation remain in code for compatibility with existing tests and development paths, but the Town NPC presentation no longer invokes that workflow. The code path is the active player-facing source of truth.

## Presentation boundary

`TownRaidQueuePresenter` exists only for the local `SocialPlayer`. It observes the confirmed NPC interaction, opens the local view, suppresses gameplay input while the panel is open and forwards create/join requests to the application coordinator. Presentation never mutates Fusion simulation or authoritative raid state.

## Validation strategy

- EditMode: code normalization and deterministic manifest identity.
- EditMode: frozen and code-admitted manifest policies.
- Compilation: runtime UI, coordinator, launcher and authority boundary.
- Manual two-application validation: Host creates a code, Client joins it after Host reaches Gameplay, invalid code recovers to Town, solo Host can cancel, and a second Town-Raid-Town cycle uses a new code.

## Limits

The initial session cap remains four. The code is an MVP shared secret, not an account invitation, backend reservation or cryptographic credential. A Host cannot run Town and raid simultaneously, and there is no automatic party persistence or re-entry after a profile has departed the raid.
