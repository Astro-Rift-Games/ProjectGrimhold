# Progression Architecture

## Sources of truth

`05 - Progresión Persistente, Experiencia y Niveles` owns the Game Design rules for
Expedition Experience, consolidation, levels and persistent Experience. This document
defines only their technical boundaries.

Persistent Level and Experience remain outside the Raid simulation. TASK-129 introduces
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

## Producer idempotency

The ledger receives only a reward that its authoritative producer has already recognized
as valid. It does not maintain a universal reward identifier or a replicated journal.
TASK-130 through TASK-133 own the exact one-shot state appropriate to their respective
Kill, Assist, chest and extracted-Loot domains.

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
generic transaction service or coordinator. Each producer task must validate its own
resimulation and Host Migration behavior.

## Extracted Loot boundary

`ExtractedLootExperience` exists in the breakdown and starts at zero, but TASK-129 exposes
no write path for it. The normal Dungeon API rejects the `ExtractedLoot` category.

TASK-133 must inspect the real extraction, eligibility and persistence flow before defining
how a successful extraction resolves Loot Experience atomically and idempotently. TASK-129
does not prescribe an order relative to `TryMarkExtracted`.

## Host Migration and reconnection

The ledger is a NetworkBehaviour on the participant NetworkObject. Fusion snapshots and
`CopyStateFrom` therefore restore its accumulators together with the participant. The
ledger has no restore-time initialization and no NetworkId reference requiring remapping.

Ordinary mid-Raid reconnection is not currently implemented outside the dedicated Host
Migration recovery path. A future networking task must rebind `ProfileId` to the same
participation and ledger; TASK-129 does not add that workflow.
