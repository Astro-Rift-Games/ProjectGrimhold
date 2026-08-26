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

Total Expedition Experience is derived from these accumulators and is never replicated as
a second source of truth. The public snapshot exposes only this gameplay breakdown.

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

`Defeated`, `Extracted` and `Aborted` freeze normal Dungeon rewards but do not erase the
snapshot. Later result resolution may read the frozen breakdown without making the ledger
responsible for consolidation.

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
continues to own only provisional Raid Experience and does not resolve or apply it. TASK-110 owns
the authoritative mapping from Raid context to a definitive Progression outcome and the storage of
the resulting resolution for that participation. Applying consolidated Experience to persistent
Level and Experience remains a separate responsibility.

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

## Extracted Loot Experience

The normal Dungeon API continues to reject the `ExtractedLoot` category. Confirmed extraction
uses a separate ledger operation that accepts the matching confirmed `ResultSequence`, changes
only `ExtractedLootExperience`, and preserves Kill, Assist and Exploration Experience. The
extraction transaction owns one-shot protection through its existing pending state and
`IsExtractionCommitConfirmed`; the ledger adds no parallel reward journal.

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

`ExtractedLootExperienceProducer` is a non-networked `MonoBehaviour` on the Raid player prefab.
It owns only the static Value catalog reference and rate. `PlayerExtractionLootSaver` owns the
process-local candidate, binds it to the pending `ResultSequence`, and discards it whenever that
pending transaction is replaced or cleared. No candidate or configuration is replicated.

State Authority prepares eligibility, eligible Value and candidate Experience from the retained
authoritative ownership snapshot before Loot consumption. On a valid persistence ACK it performs
exact-clear, confirms the extraction, then attempts the matching ledger reward. Calculation or
ledger failure is diagnostic only: it cannot block or revert Loot extraction, and the candidate is
discarded after confirmation without retry. Provenance and the candidate never enter stash/backend
persistence.

## Host Migration and reconnection

The ledger is a NetworkBehaviour on the participant NetworkObject. Fusion snapshots and
`CopyStateFrom` therefore restore its accumulators together with the participant. The
ledger has no restore-time initialization and no NetworkId reference requiring remapping.

Ordinary mid-Raid reconnection is not currently implemented outside the dedicated Host
Migration recovery path. Future reconnection support must rebind `ProfileId` to the same
participation and ledger; the current ledger contract does not add that workflow.
