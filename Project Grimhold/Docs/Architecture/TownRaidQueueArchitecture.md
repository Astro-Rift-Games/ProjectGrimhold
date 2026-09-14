# Town Party and Raid Preparation Architecture

## Decision

Town Party and Raid Preparation are independent replicated domains inside the Town Shared runner.

```text
SocialPlayer interaction -> TownPartyDirectory -> Solo/Duo Party (max 2)
TownRaidNpc interaction -> TownRaidPreparationDirectory -> Raid preparation (max 16)
Ready/Start -> frozen RaidLaunchContext -> SessionConnectionCoordinator -> Raid runner
```

`TownPartyDirectory` is the sole authoritative source for the social roster. Every registered Town
profile receives a Solo Party. A direct invitation references the inviter and recipient Parties plus
their revisions. Acceptance revalidates both Solo rosters and merges them with the inviter as Party
Host. Leaving a Duo splits it into two Solo Parties. Invitation expiry and pair cooldown remain
State-Authority `TickTimer` state. Party entries, invitations and continuation contain no RaidCode,
Ready state, preparation identity or launch revision.

`TownRaidPreparationDirectory` owns only concrete expeditions. Explicit Create generates the
six-digit `RaidCode` and copies the creator's current Party roster, with the creator first and as Raid
Host, into a new preparation. Every copied participant starts Not Ready. The preparation thereafter
owns an independent roster of one through `RaidSessionRules.MaxParticipants` (16) profiles.

Join by code adds only the requesting profile to that preparation. It neither imports the requester's
Party nor mutates any Party. Party merge/split never changes, cancels or populates an existing
preparation; preparation Join/Leave/Cancel never changes Party. Pending social invitations do not
block Join, Ready or Start.

## Launch and runner transition

Each preparation member controls only its own Ready flag. Only the Raid Host may Start, and only
when every current member is Ready. Start freezes the full preparation snapshot and the current ACK,
release and deadline workflow operates exclusively on that frozen roster. `RaidLaunchContext`
contains the code, Raid Host and up to 16 frozen profiles. `SessionConnectionCoordinator` remains
the only owner of Town/Raid runner replacement and local loadout reservation.

At local launch materialization, each application separately captures its current Party as a
`TownPartyContinuationContext` containing only Party Host and ordered Solo/Duo roster. Party members
do not need to belong to the Raid. The context is runner-independent and has no RaidCode,
preparation or launch revision.

## Return continuity

On Town return, `TownPartyDirectory` first treats an already-existing matching Party as authoritative.
If the local profile already belongs to a changed Party, that current roster wins and the stale local
context is discarded. Otherwise matching claims from every Solo/Duo member may reconstruct only the
Party. Restoration never creates a preparation or a RaidCode. A player consequently returns without
a preparation and the next NPC interaction presents Create/Join again.

Continuation claims are emitted only at lifecycle/replicated-state observation boundaries. Explicit
abandonment tombstones the local claim. This is application-session continuity; backend Party
persistence and global presence remain out of scope.

## Presentation

`TownRaidPreparationPresenter` projects only the local profile's preparation. Without one it always
shows the initial `Crear raid` / code-join state, regardless of Party or pending continuity. The
existing panel presents the 1-16 roster in its bounded scroll area.

`TownPartyHudPresenter` projects the authoritative Party only for `HasInputAuthority`. The preauthored
`TownPartyHudView` always shows the local display name, shows the companion and `Abandonar Party`
only for Duo, and resolves names from `SocialPlayerIdentity` with `ProfileId` as a temporary fallback.
Presentation does not mutate Party or preparation state directly.

## Validation boundaries

Pure/EditMode coverage owns Party validity/merge/roster-copy rules, invitation contract separation,
16-member preparation capacity, Ready/Start/freeze, continuation matching and HUD projection.
Serialized composition tests own the single Party directory and local HUD wiring. PlayMode must cover
Solo initialization and Input-Authority-only HUD activation. Manual multi-application validation must
cover independent Party/preparation mutations, cross-Party join by code, 16-member capacity, launch,
staggered return and a fresh Create state after return.
