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
six-digit `RaidCode` and admits only the creator, as Raid Host and Not Ready. Party membership never
grants automatic Raid admission: every additional participant, including the creator's Party
companion, must explicitly Join with the code. The preparation thereafter owns an independent roster
of one through `RaidSessionRules.MaxParticipants` (16) profiles.

Join by code adds only the requesting profile to that preparation. It neither imports the requester's
Party nor mutates any Party. Party merge/split never changes, cancels or populates an existing
preparation; preparation Join/Leave/Cancel never changes Party. Pending social invitations do not
block Join, Ready or Start.

## Launch and runner transition

### Recovery weapon consent

Start first opens a separate, replicated weapon-consent attempt while the preparation remains
`Waiting`. Its revision is independent of `LaunchRevision`; no reservation, ACK deadline or
countdown starts during this phase. Every member inspects the confirmed local profile without
mutating it. A prepared weapon proceeds silently. An already-owned configured recovery weapon in
Loadout or Stash also proceeds silently, retaining the existing deterministic reuse priority.
Only a genuinely new free recovery weapon opens the preauthored `RecoveryWeaponConsentView`.

The application coordinator retains an immutable inspection bound to the store, character and
accepted aggregate snapshot revision. Accept revalidates that binding; it is not a global grant
permission. The State Authority freezes the launch only after every current member accepts or
needs no new grant. Until then Cancel, membership/Ready changes or authority replacement invalidate
the entire attempt, reset Ready and leave every member in Town without granting or equipping.
There is deliberately no consent timeout.

After unanimous consent, the existing local launch materialization revalidates current need and
commits through `LocalProfileStore`'s existing recovery helper before reservation. A replaced profile
snapshot cannot authorize a new grant, even when its remote revision is unchanged. Double-clicks
are consumed once by the modal, member responses are idempotent and repeated materialization never
duplicates a granted unit. Persistence failures continue through the existing cohort rejection path.

The view owns only serialized UI controls and registers with the coordinator through explicit Town
composition. Its modal root is a child, not its own GameObject, so hiding it never removes its binding.
Unloading/disable cancels any pending consent. No runtime UI construction or view scene search is used.

Each preparation member controls only its own Ready flag. Only the Raid Host may Start, and only
when every current member is Ready. Start freezes the full preparation snapshot and the current ACK,
release and deadline workflow operates exclusively on that frozen roster. `RaidLaunchContext`
contains the code, Raid Host and up to 16 frozen profiles.

Ready locks the member's Town build (Game Design 02 §7.2). `TownRaidPreparationRules.CanMutateTownBuild`
is the single rule: a Waiting member who is not Ready may change attributes, abilities, Equipment and
Stash selection; Ready, a frozen launch, a non-member or an invalid observation is locked.
`TownEquipmentMutationGate` applies it to every Town screen (Attributes, Abilities, Inventory Equipment
and Stash transfers/splits/gem installs); Stash Sort and Quick Slot bindings stay available. Town
inventory is written through `LocalProfileStore` and the backend HTTP clients, not Fusion RPCs, so the
lock is enforced at those client mutation endpoints; the backend does not observe Ready.
`TownRaidPreparationSnapshot.FriendlyFireEnabled` is also frozen into immutable `RaidLaunchContext.FriendlyFireEnabled` (Host default: `false`). `SessionConnectionCoordinator` remains
the only owner of Town/Raid runner replacement and local loadout reservation.

The staged release keeps the profile that owns State Authority for the Town preparation as the final
Town departure. If that profile is not the Raid Host, the Raid Host is released first so it can create
the Raid session, followed by every other non-authority participant; the Town authority leaves only
after those departures are observed. If both roles coincide, the Clients remain the initial
departures. Party Host is unrelated to both choices and never controls the release order.

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

A member may return while the Duo partner is still in the Dungeon (Game Design 01 §6.3/§10, 02 §18):
only one expedition may be active per shared session. Town cannot observe the Raid runner, so the
unresolved Duo continuation is the signal: `TownPartyRules.IsAwaitingPartyCompletion` holds while the
local `TownPartyContinuationContext` has two members and has not been restored, which happens only
when the partner returns. `SessionConnectionCoordinator.IsAwaitingPartyCompletion` exposes it,
`SocialPlayerIdentity.IsAwaitingPartyCompletion` replicates it and `TownPartyDirectory` reads it per
player. `TownRaidPreparationDirectory` rejects Ready (cancelling Ready stays allowed) and Start from such
a player, and the panel locks Ready, Start, Create and Join with the notice
"Tu compañero sigue en la Dungeon. Espera a que finalice su expedición". The lock lifts automatically
when the restored Party clears the continuation. A claim whose other member already belongs to a Town
Party (`TownPartyRules.IsContinuationSuperseded`, for example a partner who went through the main menu)
is withdrawn and invalidated so the claimant abandons it and is unlocked. A partner who never reaches
Town again (application quit or Host loss) keeps the continuation pending; it has no expiry today and is
released only by returning to the main menu. In the Editor, `SessionConnectionCoordinator` "Simulate Party Member In Dungeon" forces
the lock for single-client validation.

## Presentation

`TownRaidPreparationPresenter` projects only the local profile's preparation. Without one it always
shows the initial `Crear raid` / code-join state; a pending Duo continuity only locks it with the
awaiting-partner notice described above. The
existing panel presents the 1-16 roster in its bounded scroll area.

`TownPartyHudPresenter` projects the authoritative Party only for `HasInputAuthority`. The preauthored
`TownPartyHudView` always shows the local display name, shows the companion and `Abandonar Party`
only for Duo, and resolves names from `SocialPlayerIdentity` with `ProfileId` as a temporary fallback.
Presentation does not mutate Party or preparation state directly.

## Validation boundaries

Pure/EditMode coverage owns Party validity/merge/creator-only admission rules, invitation contract separation,
16-member preparation capacity, Ready/Start/freeze, State-Authority-terminal release ordering,
continuation matching and HUD projection.
Serialized composition tests own the single Party directory and local HUD wiring. PlayMode must cover
Solo initialization and Input-Authority-only HUD activation. Manual multi-application validation must
cover independent Party/preparation mutations, cross-Party join by code, 16-member capacity, launch,
staggered return and a fresh Create state after return.
