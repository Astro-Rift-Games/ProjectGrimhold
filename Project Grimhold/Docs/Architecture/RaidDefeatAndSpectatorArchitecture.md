# Raid Defeat and Spectator Architecture

## Context and decision

Defeat must remove avatar gameplay control without confusing character death with peer
departure. `RaidParticipantState` remains the authoritative result state and is not extended
with `Spectating`; spectator mode is local presentation owned by `RaidMenuPresenter` and
`LocalRaidSpectatorController`. `NetworkMatchController` remains the only owner of global
raid closure.

The durable participant identity is `ProfileId`. `PlayerRef` and Input Authority are
runner-local routing and never decide the Host role. The canonical Host is the frozen
`RaidLaunchContext.HostProfileId` for that runner.

## PlayerObject invariant

`NetworkRaidParticipant` is registered as the Fusion PlayerObject for the whole connected
raid lifecycle. The temporary avatar is never registered as PlayerObject. Defeat clears
`CurrentAvatarId` and avatar Input Authority, while retaining the participant mapping and
participant Input Authority. This lets terminal local presentation remain bound without
making the corpse controllable.

## Return authorization

While an RPC requester is still connected, State Authority resolves:

```text
RpcInfo.Source
  -> Runner.GetPlayerObject(Source)
  -> exact receiving NetworkRaidParticipant
  -> ProfileId
  -> RaidLaunchContext.HostProfileId
```

Every ambiguous or inconsistent step rejects the request. A defeated canonical Host cannot
Return while the raid is active. A defeated Client may Return. Immediately before publishing
`IsReturnAuthorized`, State Authority registers a one-shot
`ControlledReturnKey(ProfileId, RaidGenerationId)` in the runner-owned
`ControlledReturnRegistry`. `Extracted` return and living abandonment keep their separate
existing contracts.

## Player departure

`OnPlayerLeft` cannot assume Fusion still exposes `GetPlayerObject(player)`. State Authority
therefore reads `_spawnedPlayers[player]`, captures the participant's `ProfileId` and
`RaidGenerationId`, and consumes the pending marker before removing PlayerRef routing.

A consumed marker makes only that profile and generation terminal/no-rejoin and preserves
the participant, corpse, loot and authoritative world state. A defeated departure without a
marker is unexpected: it is not added to terminal profiles, its NetworkObjects and stable
state are preserved, and only invalid PlayerRef routing is removed. Recovery/reconnect policy
is deliberately deferred. Pending and terminal markers are cleared with runner/generation
cleanup.

## Local spectator selection

Candidates come from `Runner.ActivePlayers`, their PlayerObjects and
`NetworkRaidParticipant`. A bounded, diagnostic replicated-object enumeration is used only
when PlayerObject enumeration has no valid target; the server-only `_spawnedPlayers` map is
not a Client spectator registry. A target must be another valid `Raiding` participant in the
same `RaidGenerationId`, with a valid `ProfileId` and current avatar.

Candidates are ordered with ordinal `ProfileId` comparison. Previous and Next wrap. When
target `X` becomes invalid, selection chooses the first remaining `ProfileId > X`, wrapping
to the first when none exists. With no target, the camera is cleared, navigation is disabled
and the serialized HUD bar reports `No hay jugadores para observar`.

`LocalCameraController` observes the target transform only. Spectator selection never changes
authority, PlayerObject, participant state or `LocalPlayerHudBinder` ownership.

## Input, inventory and cleanup

`Defeated`, rather than spectator mode, owns the local gameplay and inventory lock. Results,
active spectator and no-target spectator all retain gameplay suppression. Inventory opening,
transfer, take-all, drop and consumable intentions are rejected and any open inventory is
closed.

Cleanup is idempotent on authorized Return, match finish, shutdown, despawn, disable, scene
unload and destruction: it clears camera/target buffers, hides spectator UI, removes listeners,
releases suppression and restores the local inventory block as the composition leaves the
raid.

## Scope and validation boundary

This stage does not define reconnect/recovery, multi-client Host Migration, extracted-Host
policy or live-Host abandonment. Automated tests cover generation-key isolation, one-shot
consumption, deterministic selection, serialized prefab controls and Single Runner
PlayerObject/authority preservation. A real Host/Client session is still required to prove
that after the Host dies and observes the Client, the Client continues moving, attacking,
damaging enemies, interacting and collecting loot without Host Migration.
