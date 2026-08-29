# Progression Architecture

## Sources of truth

`05 - Progresión Persistente, Experiencia y Niveles` owns the Game Design rules for
Expedition Experience, consolidation, levels and persistent Experience. This document
defines only their technical boundaries.

Persistent Level and Experience remain outside the Raid simulation. The Raid ledger owns
only provisional Expedition Experience. It does not apply `CharacterProgressionRules`,
write a profile, consolidate a result or present Results UI.

## Provisional experience ownership

Each admitted Raid participation owns one `PlayerExpeditionExperienceLedger`, co-located
with its `NetworkRaidParticipant` PlayerObject. The ledger belongs to the participation,
not to its temporary avatar, body, Input Authority or runner-local `PlayerRef`.

The ledger replicates four non-negative `long` accumulators:

- Kill Experience;
- Assist Experience;
- Exploration Experience;
- Extracted Loot Experience.

It also replicates the committed activity counts required by Results: PvE and PvP Kills,
PvE and PvP Assists, and valid first chest openings. Normal rewards enter through an explicit
`ExpeditionExperienceSource`; `None` and unknown values are invalid. A valid source maps to the
existing Kill, Assist or Exploration category, and its complete Experience candidate plus one
activity-count increment are committed atomically. PvP and Assist have no producer yet, so those
counters remain zero until their authoritative attribution rules exist.

Total Expedition Experience is derived from these accumulators and is never replicated as
a second source of truth. The public snapshot exposes only this gameplay breakdown.
Combat Experience is likewise derived as Kill plus Assist. Non-negative accumulators and a valid
complete `long` total make both projections safe without another replicated total or overflow rule.

## Deterministic domain and Fusion boundary

`ExpeditionExperienceRules` is pure C#. It validates the current breakdown, category,
positive amount, category overflow and total overflow, then produces a complete candidate
snapshot without mutating external state. It has no dependency on Unity, Fusion,
`RaidParticipantState`, persistence or Results.

`PlayerExpeditionExperienceLedger` is the Fusion boundary. It validates State Authority,
its co-located participant and the participant lifecycle before asking the pure rules for a
candidate. Normal Dungeon rewards are accepted only while the participant is `Raiding`.
The ledger commits all four accumulator values only after the candidate succeeds; every
rejection leaves the replicated breakdown unchanged.

Terminal participant states reject normal Dungeon rewards but do not erase the snapshot.
`PlayerExpeditionProgressionResolver` freezes the ledger only after every resolution and
application calculation has succeeded. The frozen breakdown remains the canonical provisional
snapshot used to reconstruct committed history.

## Definitive experience resolution

`ExpeditionExperienceResolutionRules` is a pure C# transition from a validated provisional
snapshot to one immutable `ExpeditionExperienceResolution`. Its outcome domain contains only
the definitive Progression results `Extracted`, `Defeated`, `Abandoned` and
`DefinitivelyDisconnected`; temporary disconnection, pre-Dungeon cancellation and the cause or
history that produced an outcome remain outside this domain.

`ExpeditionExperienceRetentionPolicy` supplies one independently configurable `0..10000`
basis-point percentage per outcome. The initial policy retains 100% after extraction, 20% after
defeat and 0% after abandonment or definitive disconnection. Resolution derives the validated
total from the preserved `ExpeditionExperienceSnapshot` and applies the percentage with integer
quotient-and-remainder arithmetic, so the result is floored without floating-point arithmetic or
an overflow at `long.MaxValue`. The complete original category breakdown remains available and
no second provisional total is stored.

The transition accepts an immutable previous resolution and rejects replacement once completed.
This deterministic one-shot rule does not store state by itself. `PlayerExpeditionExperienceLedger`
continues to own only provisional Raid Experience and does not resolve or apply it.

`ConsolidatedExperienceApplicationRules` is the separate pure C# transition that applies one
completed resolution to persistent Level and Experience. Its immutable application value has only
pending and applied states; an applied value retains the complete `ExperienceApplicationResult`.
Repeated application rejects before reevaluating the resolution or progression state. A completed
zero-Experience resolution is still consumed and produces a no-op result, while positive Experience
delegates level processing to `CharacterProgressionRules`.

`CharacterAttributePointGrantRules` is a separate pure consequence that consumes a structurally
consistent `ExperienceApplicationResult` and the current `CharacterAttributeState`. It never reads
an Experience curve or recalculates Experience, Level or `LevelsGained`; it only grants the configured
points per effective Level gained and preserves every assigned attribute. Its immutable grant has
pending and applied states, and a valid zero-Level transition is consumed as a no-op.

This one-shot protection depends on the consumer preserving and resubmitting the previous grant.
It prevents reuse of that consumed application but does not globally identify a Level transition,
deduplicate two fresh pending applications, or introduce a receipt, sequence or journal.

`CharacterAttributeAssignmentRules` is the separate pure transition that assigns exactly one
available point to one selected attribute. The caller supplies the current balance maximum; the
operation consumes one available point, preserves every other attribute and rejects atomically when
the identifier is unknown, no point is available or the selected value is already at or above that
maximum. The maximum is an operational balance rule rather than a structural invariant, so an
existing `CharacterAttributeState` may remain structurally valid above it. The consumer is responsible
for exposing assignment only in Town; this domain rule has no UI, networking or persistence context.

The pure application domain does not identify a Raid participation, store state or write a profile.
`PlayerExpeditionProgressionResolver`, co-located with the participant and ledger, owns that
authoritative mapping and preserves one resolution together with its single application state.
Fresh admission injects and validates the durable Level/Experience baseline from the admitted
local profile. Level zero is the only missing-baseline sentinel. Fusion restoration preserves
the baseline and never replaces it with a fresh-composition fallback.

The resolver prepares all fallible work before mutation: authority, baseline, semantic cause,
participant lifecycle, unfrozen ledger snapshot, resolution and consolidated application. Its
commit then only freezes the ledger, copies outcome, retained basis points, consolidated Experience
and resulting Level/Experience, and writes `Committed` last. Public baseline, resolution and
application snapshots are side-effect-free; committed percentage and Experience are historical and
are not recalculated if balance changes. Repeated finalization returns `AlreadyCommitted`.

The commit also captures the three historical facts that cannot be recovered after the transaction
advances: exact eligible extracted-Loot Value, the next-Level Experience requirement and whether the
resulting Level is maximum. Extraction validates that its retained candidate belongs to the current
result and that its awarded Experience matches the ledger before capturing its Value; every other
outcome captures zero eligible Value. Maximum Level always stores a zero next-Level requirement,
while a non-maximum result stores one positive requirement.
The pending extraction candidate remains readable through `ProgressionPending`; it is cleared only
after the resolver has committed and the participant advances the transaction to `Complete`.

`TryGetProgressionResult` is available only after the resolver is committed and the ledger frozen.
It combines the existing committed resolution and application snapshots, frozen activity counts,
eligible Value and Level-progress facts into an immutable `ExpeditionProgressionResult`. Reading it
never evaluates producers, retention, Level application, the Experience curve or persistence.
Results presentation captures the first successful read as its local immutable snapshot and does
not query or replace it again during that active binding. Resolver commitment therefore makes the
summary available independently from durable persistence: pending, retryable or failed persistence
may change its own feedback and keep Return disabled, but cannot hide or alter an available summary.

The accepted semantic causes are extraction confirmed, defeat confirmed, voluntary abandonment
confirmed and definitive disconnection confirmed. `Aborted` is only a technical lifecycle state and
never implies an outcome. Bootstrap/pre-Dungeon cancellation has no participation Experience to
resolve. Active Host cancellation keeps its current technical closure; its Progression meaning is
not defined by this task and no outcome is invented for it.

## Durable result sequence and local consolidation

`LastAppliedProgressionResultSequence` is the last progression result confirmed durably by the
local profile. It is not a counter of every result ever produced by State Authority and it does
not restart at zero for every participation. Each fresh admission transports the current durable
watermark. `NetworkRaidParticipant` initializes `ResultSequence` from that baseline, and its one
definitive result proposes exactly `baseline + 1`.

The local profile accepts a new result only when its sequence equals the current durable
watermark plus one. After a successful atomic profile save, that sequence becomes the new
watermark. If a result cannot be persisted locally, including a result produced for a
definitively disconnected client, it does not permanently consume a profile sequence. A later
admission may therefore propose the same `watermark + 1`. This deliberate local-only contract
does not add backend recovery, remote synchronization or another authoritative store.

After the resolver commits its immutable resolution, Input Authority asks `LocalProfileStore`
to persist the matching `ProgressionReceipt`. `Success` and an exact `AlreadyApplied` permit an
ACK. `PersistenceFailed` remains retryable without ACK. `Stale`, `Conflict` and `Invalid` are
terminal local failures and never ACK. `AlreadyApplied` requires the sequence to equal the
current watermark and `LastProgressionReceipt` to match exactly; an older sequence is always
`Stale`, because later profile progress does not prove which historical receipt was applied.

State Authority accepts an ACK only for the participant's current Input Authority, ProfileId,
raid generation, definitive resolution and current `ResultSequence`. Extraction, defeat and
voluntary abandonment cannot authorize Return before this durable ACK. The ACK flag and resolver
state are networked with the participant, so Host Migration preserves confirmed and pending work.
The ACK confirms persistence only and never publishes `IsReturnAuthorized`. After a snapshot is
available and the ACK plus outcome-specific barriers are observable, presentation may enable its
button and issue an explicit `RequestReturn`. State Authority revalidates the request before
publishing `IsReturnAuthorized`; `SessionConnectionCoordinator` remains the sole owner of the
subsequent Raid-to-Town transition.

The complete terminal flow is:

```text
resolver Committed + ledger frozen
-> Results snapshot available
-> durable persistence ACK
-> Return button locally eligible
-> explicit RequestReturn
-> State Authority validation
-> IsReturnAuthorized
-> SessionConnectionCoordinator
-> Town
```

## Town progression presentation

Town presents only the persistent Level and Experience already stored by the local profile:

```text
LocalProfileStore
-> ApplicationStashContext.ProfileCommitted
-> TownProgressionBinding
-> local Town progression HUD
```

The binding subscribes before its initial read so a commit cannot be missed between those two
operations. That initial read is always performed when the local `SocialPlayer` enters or returns
to Town, because the relevant commit may have completed during the Raid before the Town HUD existed.
Later `ProfileCommitted` notifications refresh an active HUD only when they identify the observed
local `ProfileId`.

The Town HUD does not consume `ExpeditionProgressionResult` or any Results presentation snapshot.
Results retains its independent immutable, read-only summary of one participation; Town observes
only the current persistent values exposed by `LocalProfileStore` and never recalculates or grants
Experience.

## Producer idempotency

The ledger receives only a reward that its authoritative producer has already recognized
as valid. It does not maintain a universal reward identifier or a replicated journal.
Each Kill, Assist, chest and extracted-Loot producer owns the exact one-shot state
appropriate to its domain.

Each producer must use authoritative, deterministic state that survives resimulation and,
when required, Host Migration. `PlayerRef`, a remappable `NetworkId`, `SimulationTick`, a
process-local counter or other non-restorable state is not sufficient by itself. Existing
one-shot damage results, first-opening state and Loot provenance/consumption are examples
to inspect, not a mandatory shared abstraction.

The producer integration order is:

1. validate that the producer-specific one-shot reward remains available;
2. synchronously request ledger application from State Authority;
3. let the ledger calculate, validate and commit its candidate internally;
4. preserve the producer reward when the ledger rejects it;
5. after acceptance, immediately complete the producer's one-shot transition in the same
   synchronous State Authority execution.

No producer may place an `await`, intermediate RPC or irreversible effect between ledger
acceptance and its one-shot transition. This is a direct simulation relationship, not a
generic transaction service or coordinator. Each producer integration must validate its own
resimulation and Host Migration behavior.

## Kill Experience producer

Each eligible PvE target owns one `KillExperienceSource`, registered in the runner-scoped
`EntityRegistry` under the target's canonical `EntityId`. Its serialized non-negative `long`
is independent from Extraction Progress configuration. The source replicates only `IsGranted`;
availability is derived from a positive configured value and that one-shot flag being false.
Its ledger source is explicitly `PveKill`; the current producer guarantees creature targets and
must not be reused for PvP without a separate authoritative affiliation integration.

After an authoritative applied fatal result, `DamageResolver` resolves the Last Hit attacker as
a current Raid avatar, follows `RaidAvatarParticipantLink` to its stable
`NetworkRaidParticipant`, and passes the co-located ledger directly to the source. The source
uses the required synchronous ledger-before-consumption order: it revalidates availability,
requests `Kill` application, preserves availability on rejection, and sets `IsGranted` only
after ledger acceptance in the same State Authority execution. There is no pending state,
retry, RPC, generic transaction service or coordinator.

Fusion snapshots and `CopyStateFrom` preserve `IsGranted` with the defeated target. Fresh
State Authority spawns initialize it to false; Host Migration restore spawns do not overwrite
the copied value and require no reference fixup. The current Kill Experience producer integrates creatures only.
Player Kill Experience remains blocked until an external authoritative affiliation contract can
distinguish allies from enemies; no runtime role or connection identity substitutes for it.

## First-open Exploration Experience producer

`NetworkLootContainerInteractable` owns a serialized non-negative first-open Experience
reward and two replicated pieces of producer state: the resolved one-shot and its stable
owner `ProfileId`. These are independent from the existing `FirstOpenResolved` state and
first-open Extraction Progress reward. Container contents affect only the existing Progress
path; an empty eligible chest may still grant Exploration Experience.

On every otherwise valid authoritative interaction while the Experience reward remains
unresolved, the interactable resolves the `InteractorId` as a current Raid avatar, follows
its `RaidAvatarParticipantLink`, and validates the co-located participant. The first valid
participant claims ownership before ledger resolution. An absent ledger or ledger rejection
keeps both that owner and the one-shot pending; another participant cannot replace the owner,
while a later interaction by the owner may retry. Ledger acceptance is followed immediately
by `FirstOpenExperienceResolved` in the same execution.

The owner uses the participant's replicated `NetworkString<_32>` `ProfileId`, not a
remappable `NetworkId`, `PlayerRef` or avatar identity. Fusion snapshots and `CopyStateFrom`
preserve the owner and resolved state with the container. Fresh State Authority spawns clear
both fields, while Host Migration restore spawns do not overwrite copied values. XP success,
rejection or ineligibility never gates the existing interaction or Extraction Progress path,
and the Progress result cannot undo or repeat accepted Experience.
An accepted reward uses `FirstOpenChest`, so its Exploration Experience and first-opening count
advance in the same authoritative operation.

## Extracted Loot Experience

The normal Dungeon API continues to reject the `ExtractedLoot` category. Confirmed extraction
uses a separate ledger operation that accepts the matching `ResultSequence`, changes only
`ExtractedLootExperience`, and preserves Kill, Assist and Exploration Experience. The ledger stores
the resolved extraction sequence because retry and Host Migration must distinguish a newly applied
candidate from the same candidate already applied. This is a single producer marker, not a generic
reward journal.

Raid loot provenance preserves quantified `Dungeon` and `Player(RaidParticipantId)` buckets through
transfers, Equipment, world drops, corpses, snapshots, Host Migration and the pending extraction
boundary. The authority assigns the stable Raid-scoped ID from the frozen admission cohort while
`NetworkRaidParticipant` retains the independent logical `ProfileId`. Provenance therefore accepts
the repository's full ProfileId domain without storing, hashing, truncating or normalizing it.

`RaidLootEligibilityResolver` is a pure projection over `PlayerExpeditionLootSnapshot` and
`RaidInitialAffiliationSnapshot`. Dungeon quantities are eligible; Player quantities are eligible
only when their original participant belongs to a different initial `RaidTeamId` than the extractor.
The resolver validates exact totals and never mutates provenance or Experience state.

`IRaidLootValueSource` is independent from `ExtractionValuePerUnit` and `SellValuePerUnit`.
The current `RaidLootValueCatalog` is a replaceable local source with one positive `long` Value
per productive `LootId`; its values are provisional configuration, not economic balance.
`ExtractedLootExperienceCalculator` multiplies only eligible quantities by their configured Value,
sums with checked `long` arithmetic and applies a `1..10000` basis-point rate using integer quotient
and remainder. The current rate is 1000 basis points. Missing or invalid Values for eligible
quantities, invalid rates and overflow reject the complete calculation without mutating eligibility.

`ExtractedLootExperienceProducer` remains a non-networked owner of the static Value catalog and
rate. Before persistence, State Authority calculates one candidate from retained authoritative
ownership and stores its eligible Value and awarded Experience on the participant, bound to the
current `ResultSequence`. This durable candidate is not granted Experience and is never
recalculated for the same transaction after retry or Host Migration.

The extraction phases are `AwaitingExperiencePreparation`, `AwaitingPersistenceAck`,
`ExtractedLootPending`, `ProgressionPending` and `Complete`. After persistence ACK and exact-clear,
the ledger applies the candidate idempotently. New application, the same sequence already resolved,
or a valid zero award advances to `ProgressionPending`; a real failure keeps the candidate pending
and leaves Progression unfrozen. Once pending Progression begins, retries never execute extracted
Loot again. Only resolver `Success` or `AlreadyCommitted` completes the transaction.

## Host Migration and reconnection

The ledger is a NetworkBehaviour on the participant NetworkObject. Fusion snapshots and
`CopyStateFrom` therefore restore its accumulators together with the participant. The
ledger has no restore-time initialization and no NetworkId reference requiring remapping.

Ordinary mid-Raid reconnection is not currently implemented outside the dedicated Host
Migration recovery path. Future reconnection support must rebind `ProfileId` to the same
participation and ledger; the current ledger contract does not add that workflow.
