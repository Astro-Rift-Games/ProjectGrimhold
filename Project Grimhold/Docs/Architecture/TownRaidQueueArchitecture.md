# Town Raid Preparation Architecture

## Decision

Raid preparation is a replicated cohort owned by the Town Shared Mode runner. The
Host and Clients remain in Town until the Host explicitly starts the raid. The
Dungeon/Gameplay runner is not created by the NPC Create or Join actions.

```text
TownRaidNpcInteractable
  -> TownRaidQueuePresenter / TownRaidQueueView
  -> TownRaidQueueNetworkController (State Authority)
  -> Ready cohort freeze and launch envelope
  -> SessionConnectionCoordinator (single runner transition)
  -> Raid runner with the frozen profiles only
```

The State Authority generates one six-digit `RaidCode` at creation and replicates
it with the cohort. Joining requires that exact code. The replicated snapshot is
the presentation source of truth for the code, members, capacity and Ready flags;
there is no second client roster.

## Lifecycle

1. Create: Host is added to an `Empty` preparation and receives a fixed code.
2. Join: Clients validate the supplied code and are admitted to the Town cohort.
3. Ready: every current member, including the Host, explicitly sets Ready.
4. Start: only the Host may start and only when all current members are Ready.
   The authoritative controller changes to `Launching`, freezes membership and
   delivers one launch envelope. Create/Join/Ready do not reserve loadout data.
5. Transition: each frozen member acknowledges the envelope; the coordinator
   first creates its local Loadout reservation. That aggregate mutation requires
   at least one valid prepared weapon and captures the six Equipment assignments with
   the items. Only then does it shut down Town and create or join the exact
   code-derived Raid session.

Before Town shutdown, each member stores a `RaidLaunchContext` containing the
code, frozen profile identities, Host profile and local profile. It contains no
Town `NetworkObject`, `NetworkBehaviour`, `PlayerRef` or UI reference. A Client
may retry the same session name/code at most five times only for Fusion's typed
`GameNotFound` availability result. `GameClosed` is terminal because the session
already exists but no longer accepts joins. `GameFull`, authentication,
token, version, scene and generic failures are terminal; Host
`GameIdAlreadyExists` is terminal and never generates another code.

An admission or departure racing Start is resolved by State Authority ordering:
only members present in the frozen envelope participate. A member whose launch
acknowledgement cannot be completed is rolled back and returned to Town; no
partially admitted participant is retained.

## Identity and admission

`RaidCode` is the canonical identity. It deterministically derives `SessionName`
and `RaidId`; the old manifest remains only as a compatibility transport for the
frozen cohort and must use those same identities. Raid admission validates the
frozen profile list, not an open late-join roster. A code is never regenerated
after session creation or collision; failure is recovered through the normal
transition cleanup path.

Once the Raid runner exists, `WaitingForPlayers` is only a technical connecting
phase. The frozen cohort is admitted automatically; there is no second player-
facing Start button in Gameplay. Deferred PvPvE bootstrap, gameplay guards,
BootstrapFailure closure and Host Migration remain owned by their respective
architecture documents.

## Presentation

The Town presenter owns only local UI and input suppression. It observes the
replicated `TownRaidQueueSnapshot` and forwards typed intentions (`Create`,
`Join`, `Ready`, `Start`) to the network controller. It does not call the session
coordinator during Create/Join and does not mutate simulation state.

## Validation

EditMode tests cover create/join code matching, capacity, Ready/all-Ready rules,
Host-only Start and deterministic freeze ordering. Integration validation must
cover solo Host, multi-client preparation, a simultaneous Join/Start race,
transition failure rollback, and a second Town-to-Raid cycle.
