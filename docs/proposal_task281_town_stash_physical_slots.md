# Proposal: physical stacks for Town Inventory and Stash (TASK-281 dependency)

Status: **approved (rev 4); implemented T1-T6 on New-Testing, uncommitted; architecture updated; G1/G2 Game Design reconciliation and cutover (V10 dry run, A3 window) pending**. Source: HacknPlan US-80 / TASK-281 ("Lógica Autoritativa de División de Stacks (Stack Split) Preservando Origen").

The owner approved D1-D3 (§9). Approval of a decision is not yet a contract change:

- No Architecture document, Game Design document, HacknPlan item or code is modified by this revision.
- The approval gates in §11 must be satisfied before any contract or implementation work starts.

Revision 4 applies the owner's decisions on revision 3:

- It records D1, D2 (O1, pending reconciliation of Doc 07) and D3.
- It extends D3 so compatibility covers every inventory write and every old client or backend version, not only duplicate stacks. That changes the deployment order (§8).
- It moves the `RaidLootEligibilityResolver` finding to an independent defect record (`docs/defect_raid_loot_eligibility_other_team.md`).
- It consolidates dependencies, pending verifications, documentation updates and approvals (§10, §11).
- It keeps TASK-281 and TASK-282 separate.

## 1. Problem and current contracts

TASK-281 asks for a split in "Inventario y Alijo". The Raid split is implemented (`PlayerLootReceiver.TrySplitStack`; `LootInteractionArchitecture.md`, "Physical stack split"). Town Inventory (Loadout) and Stash cannot hold two stacks of one item:

- `LocalPlayerPersistenceArchitecture.md:101`: "One distinct LootId consumes one slot; quantities have no stack cap beyond integer overflow."
- `:113-115` (TASK-218): "Each list has one entry per `LootId` and is compact, with no holes."
- `:132-133`: "Exact Raid slot positions are persisted in neither the backend nor the local cache/save; extraction retains its aggregate append behavior."
- `:137`: "No position field is added to `itemSchema`."
- `:143-145`: the compact model is "an approved exception pending reconciliation of the live document".
- `odd/tasks/stash-hardening.md:32` (L2, user decision): "StackMax deferred; displaced item to Loadout; … accept over-capacity server data and allow only reducing operations".

Game Design asks for physical slots: Doc 16 §2 "una Unidad física o stack por slot", §3 split and Sort, §8 "Persisten posiciones exactas de slots en Inventario y Stash", INV-AC-03 and INV-AC-25.

## 2. Origin: persistent Origin versus Raid provenance

These are two different concepts, and the code today only has the second.

| Concept | Owner | What it is | Current implementation |
| --- | --- | --- | --- |
| **Persistent Origin** | Doc 07 §5-§7, §27 | A property of the Unit or Instance that "se conserva durante todo el ciclo de vida" and "no se recalcula por propietario, ubicación, Contenedor ni Equipamiento". Doc 07 §6 forbids merging units with different Origin. | Does not exist. `StashItem`, the save codec and backend `itemSchema` store only `{lootId, amount}`. |
| **Raid provenance** | `LootInteractionArchitecture.md`, "Raid loot origin" | `RaidLootOrigin` = `Dungeon` or `Player(RaidParticipantId)`, packed into each `LootInventorySlot`. Assigned at admission (`TryInitializeRaidLoadout`) and at Dungeon generation; dropped at extraction (`PlayerExtractionLootSaver` builds `StashItem(lootId, amount)`). | Expedition-scoped. It feeds XP eligibility (`RaidLootEligibilityResolver`) and owner restoration (Doc 16 §9). |

`Player(participant)` records who brought a unit into this expedition, not where the unit originally came from. A Dungeon item extracted in one Raid is admitted to the next Raid as `Player(p)`.

**Discrepancy.** The Game Design text supports two readings:

- **Lifecycle reading.** Doc 07 §5/§7 says Origin is conserved for the whole lifecycle. Doc 07 §6 then forbids merging Town stacks with different Origin.
- **Expedition-relative reading.** Doc 07 §7 classifies every object brought from Town as "Persistente / ingresado a expedición", whatever its earlier history. Doc 05 §8 grants extraction XP only to "origen Dungeon actual". Both describe Origin relative to the current expedition, which matches the code.

The persistence model depends on which reading applies, so this is decision **D2** (§9).

**Options for D2.**

- **O1, expedition-relative (recommended for this change).**
  - Persisted units carry no Origin and are all "Persistente" when admitted.
  - Raid provenance stays expedition-scoped. All Town stacks of one `lootId` are merge-compatible, and the split trivially preserves Origin.
  - Needs the Doc 07 owner to clarify that "todo el ciclo de vida" means the expedition lifecycle for Origin.
  - If O2 is chosen later, the field is additive (§7), but origin history extracted before then is unrecoverable.
- **O2, lifecycle Origin persisted.**
  - Each persisted stack carries `origin` (at least `Persistente` or `Dungeon`) and never merges with another Origin.
  - Consequences:
    - Extraction must keep Origin separation.
    - Raid needs a second per-slot field next to provenance, which changes the `LootInventorySlot` network struct and its packed budgets.
    - Re-admitting a Dungeon-origin unit must not grant XP again, so XP eligibility would need an expedition id.
    - Quick Slot consumption priority (Doc 16 §5) would apply in Town.
    - Stack count and capacity grow.
  - This is a separate, larger change. The slot model below does not block it.

The owner-directed handling of the `RaidLootEligibilityResolver` finding (rival-team `Player(p)` provenance counted as eligible, against Doc 05 §8 and Doc 07 §7) is recorded independently in `docs/defect_raid_loot_eligibility_other_team.md`. It is not part of this proposal or TASK-281.

## 3. StackMax: separable, stays deferred

The deferral in L2 is kept. **The split does not depend on `StackMax`:**

- A split needs only:
  - an addressed source with `amount >= 2`;
  - a split amount in `[1, amount - 1]`;
  - an empty destination slot inside capacity;
  - the same `lootId` (and Origin) on both stacks.
- The sum is conserved by construction. INV-AC-03 holds with no cap.
- Receiving in Town keeps today's uncapped merge, applied to physical slots: add to the lowest-slot compatible stack, otherwise to the lowest free slot.
- A later split can be undone by an explicit merge (move onto a compatible stack). Without a cap, the merge is total.

Deferring `StackMax` also removes three things from this change: the backend catalog mirror, the "oversized legacy stack" state and any amount-based reorganization in the migration.

Enforcing `StackMax` in Town later is a **separable extension**. It needs:

- a backend `lootId → stackMax` mirror with a parity test (precedent: `shop.catalog.parity.test.js`);
- capped merges;
- its own data migration for oversized stacks. That migration is a value migration, not a structural one, because the slot shape does not change.

It must also validate extraction amounts (§6, last row).

## 4. Persisted representation

**Slot entry (one per occupied slot).**

```
{ slot: int, lootId: string, amount: int >= 1 }
```

**Invariants.**

- `slot` is unique within its list. Holes are allowed.
- `slot` is a **location**, never an identity: moving a unit changes `slot` and nothing else.
- `lootId` may repeat across slots.
- Capacity counts occupied slots: Loadout 30 (`LocalProfileSnapshot.MaxLoadoutSlots`), Stash 60 (`StashCapacity`).
- Existing over-capacity data (L2) keeps overflow slots `slot >= capacity`. These accept only reducing operations and are never targeted by new placements.

**Order.**

- Slot index is the persisted position (Doc 16 §8, INV-AC-25). Removal leaves a hole.
- Moves and swaps address slots, with `lootId` as a guard.
- Sort is the only compaction. It is stable by category, name, `lootId`, then slot, and never merges.
- This replaces the TASK-218 permutation order (decision D1).

**Reservation.**

- `pendingReservation.items` stores the exact Loadout slot entries.
- Rollback restores each entry at its original slot when it is free. Otherwise it uses the lowest free slot, and otherwise an overflow slot. It never merges into another stack and never drops units.
- Whether moves and shop purchases are blocked during a pending reservation is unverified (§10). The rollback rule covers both answers.

**Raid boundary (unchanged contract).**

- Admission keeps aggregating the reserved items into `LootEntry` by `lootId`. The ticket (`SessionTicketService.js:176`) and the `RaidAdmissionData` duplicate-ID and `MaximumEntries` checks stay valid.
- Raid re-partitions items with `LootInventoryRules.TryReceive`.
- Extraction keeps its aggregate payload. Town placement uses the receive rule into the empty Loadout.
- Raid positions stay unpersisted (`:132`).

**Quick Slots.**

- Bindings stay local `LootId`s (`LocalPlayerPersistenceArchitecture.md:274-283`, Doc 16 §5).
- `ProfileInventoryRules.FindAmount` returns only the first matching entry today. It must sum every stack of the definition, which Doc 16 §5 allows "sin fusionarlos".

## 5. One-time migration, separate from ordinary operations

**Rule: no read reorganizes data.**

Today `normalizeCharacterInventory` merges by `lootId` and saves during `getInventory`, moves and extraction commit (`InventoryLootIdNormalizer.js:28-37,53-95`). The expand release (§8, step 1) removes that read-time merge-and-save.

From then on:

- Reads return stored data unchanged.
- Writes validate and either commit exactly or reject. They never repair, merge or renumber other entries.

**Migration script (once, idempotent, explicit).** `backend/scripts/migrate_inventory_slots.js`, following `migrate_legacy_data.js`:

1. **`--dry-run`** reports, per character:
   - legacy duplicate `lootId`s;
   - entries per list;
   - over-capacity lists;
   - unknown `lootId`s;
   - per-`lootId` totals before and after.
2. **Per list, in stored order:**
   - Legacy duplicate `lootId`s are folded into the first occurrence. This is exactly what the current approved normalizer would do on the next read; such documents exist only if they were never read after the normalizer shipped.
   - Slots are then assigned sequentially from 0. Over-capacity entries receive overflow slots.
   - Unknown `lootId`s keep their slot and are reported, not dropped.
   - The same applies to `pendingReservation.items`.
3. **Commit** is one revision-guarded write per character that also sets `inventorySchemaVersion: 2`. On revision conflict it re-reads and recomputes.
   - It aborts the character if per-`lootId` totals differ.
   - It skips characters already at version 2, so reruns are no-ops.
4. **Not migrated:** `AuthoritativeExtractionResult.items`, which stays an aggregate payload, and receipts.

**Runtime behavior around the migration.**

- After the migration and the gate (§8), inventory routes reject a document with `inventorySchemaVersion != 2` with `409 INVENTORY_MIGRATION_REQUIRED`. They never migrate it lazily.
- New characters are created at version 1 until the gate is on, and at version 2 from the migration onward (§8.1, §8.2 step 4).

**Local cache (Unity).**

- Codec v10 decodes v1-9 by assigning `slot` equal to the list index. This is a lossless format decode, not a reorganization: v9 already rejects duplicates and has no holes.
- Backend hydration remains the source that replaces local lists.
- Whether local mutations are refused until a successful hydration is unverified (§10).

## 6. Extraction capacity: verified cases

Claim: with `StackMax` deferred, extraction places at most 30 occupied Loadout slots. The claim holds only because of the guards below; it is not unconditional.

| Case | Evidence | Result |
| --- | --- | --- |
| Destination | Backend rejects a non-empty Loadout, `LOADOUT_NOT_EMPTY` (`ExtractionCommitService.js:107-110`). Unity `TryCommitExtraction` fails on a non-empty Loadout (`LocalProfileStore.cs:1715-1735`). | Placement starts from an empty Loadout. |
| Payload size | Ingest rejects more than 30 entries, `EXCEEDS_INVENTORY_CAPACITY` (`RaidResultService.js:12,230-234`). Unity rejects `items.Count > MaxLoadoutSlots` (`LocalProfileStore.cs:1732`). | At most 30 entries. |
| Duplicate entries in the payload | The commit merges by `lootId` (`ExtractionCommitService.js:164-171`). Ingest validation has no uniqueness rule. | Without `StackMax`, distinct `lootId`s ≤ entries ≤ 30, so slots ≤ 30. The new placement must keep folding duplicates by `lootId` (same Origin under O1) and reject defensively above 30, never truncate. |
| Equipment | Equipped units go to `PreparedEquipment`, not the Loadout (`PlayerExtractionLootSaver.cs:208-210`). The commit writes `authResult.preparedEquipment` or the reservation's (`ExtractionCommitService.js:194-205`). | Equipment never consumes Loadout slots at extraction. Unequipped Raid weapons are inside the payload, so already counted. |
| Reservation | Extraction unsets `pendingReservation` (`:203`). Rollback requires a non-admitted reservation (`InventoryService.js:439-440`). Both are revision-guarded. | Extraction and reservation rollback cannot both apply. |
| Pending results across the cutover | `AuthoritativeExtractionResult.items` and Unity `PendingExtractionCommit` are aggregate. | A result written before migration commits correctly after it. |
| Legacy over-capacity Loadout | Reservation requires Loadout ≤ 30 (`LocalProfileStore.cs:1634`), and extraction requires an empty Loadout. | Legacy overflow cannot reach Raid or extraction. |
| Under O2 (Origin persisted) | One aggregate entry per (`lootId`, Origin). Each Raid slot has one provenance. | Still ≤ 30, provided ingest keeps its 30-entry cap. |
| Future `StackMax` enforcement | Ingest validates entry count, not amounts. A host payload `{bone: 999}` with `StackMax` 10 needs 100 slots. | **Can exceed.** The `StackMax` extension must validate amounts against the Raid capacity or reject. This proposal does not claim the bound for it. |

## 7. Instance persistence: separate debt, no forced migration

Equipment instances (Doc 07 §5/§27; `WeaponInstanceModifiers`, gem sockets) are not persisted today (`PlayerCombatArchitecture.md:1456`). They remain separate debt. The slot model avoids a later structural migration by following these rules:

- **Identity is never derived from location.** No `slot` index or array position is used as a unit id, and no parallel store is keyed by slot.
- **Unit data travels inside the entry.**
  - A future instance is an optional sub-document on the entry: `instance: { instanceId, templateId, properties }`.
  - A future persisted Origin (O2) is an optional `origin` field.
  - An absent field has a defined meaning: no instance, and "Persistente" under O1. Adding the field later is additive and needs no rewrite.
- **Every writer preserves unknown fields** on entries it moves, in the backend (Mongoose subdocuments declared with the full entry shape) and in the Unity codec where it round-trips entries. This rule is what makes later fields additive. It is part of the expand release (§8).
- **Entry rules reserved now:**
  - An entry with `instance` always has `amount = 1`.
  - It never merges.
  - It cannot be split.
  - Split and merge compatibility compare `lootId`, Origin, and the absence of `instance`.

## 8. Compatibility and deployment (D3, approved)

**Requirement (owner, D3).** Compatibility must hold for every write and every old client or backend version, not only for duplicate stacks. Revision 3 relied on "old clients are safe while no duplicate `lootId` exists". Revision 4 drops that argument: no old client and no old backend may read or write a migrated document at all.

### 8.1 Gated surface

The protocol header `X-Grimhold-Inventory-Protocol: 2` is required on every route that reads or writes item lists, the reservation or prepared Equipment. A request without it gets `426 CLIENT_UPGRADE_REQUIRED`, for reads as well as writes.

| Route (backend `src/routes`) | Touches | Gate |
| --- | --- | --- |
| `GET /me/inventory` (`inventory.routes.js:28`) | Lists, reservation | Required |
| `PUT /me/inventory/order` (`:39`) | Lists (`lootId` permutation) | Required; slot-addressed replacement |
| `POST …/stash/move-to-loadout`, `…/loadout/move-to-stash` (`:52`, `:65`) | Lists | Required |
| `POST …/shop/sell`, `…/shop/buy`, `…/shop/buyback` (`:78`, `:91`, `:103`) | Lists, currency, buyback entries | Required |
| `PUT …/prepared-equipment` (`:115`) | Prepared Equipment | Required |
| `POST`/`DELETE …/reservation`, `POST …/reservation/admit` (`:133`, `:149`, `:163`) | Reservation, Loadout | Required |
| `POST /me/inventory/extraction` (`:177`, legacy) | Lists | Required; stays disabled in production |
| `POST /me/extraction/commit` (`:204`) | Loadout, prepared Equipment, reservation | Required |
| `POST /debug/mock-fusion-result` (`:219`) | Extraction results | Disabled in production; required elsewhere |
| `POST /me/raid/admission-ticket`, `POST /raid/verify-admission` (`character.routes.js:94`, `:107`) | Reads reservation | Required (host and client builds) |
| `POST /raid/extraction-result` (`raid.routes.js:12`) | Host-published items | Required (host builds) |
| `GET /me`, `GET /me/profile`, `POST /me` (`character.routes.js:25`, `:38`, `:14`) | May return or create inventory | Required if it returns or creates lists (V3) |
| `POST /me/abilities/purchase`, `POST /me/progression/commit`, `POST /me/session-ticket/town` | Currency, progression, buyback reset | Required unless V4 proves path-scoped updates that never rewrite lists |

**Non-route writers** must also be updated before migration: `backend/scripts/seed-test-accounts.js`, `migrate_legacy_data.js`, `migrate_revisions.js`. New characters stay at `inventorySchemaVersion` 1 until the gate is on, so no old client ever reaches a version 2 document.

**Old backend versions** are the destructive case. `normalizeItems` merges, and a Mongoose schema without `slot` drops the field when it saves a modified array. They are excluded by the rollback floors in §8.3.

**Old Unity local caches.** An old build reading a v10 save fails closed (`UnsupportedVersion`). Backend hydration remains the source of truth.

### 8.2 Sequence

1. **B1, expand backend.**
   - The schema accepts `slot`, `inventorySchemaVersion` and unknown entry fields (§7).
   - Read-time merge-and-save is removed. Every writer preserves `slot` and unknown fields.
   - Protocol header handling exists, with the gate off.
   - On version 1 documents, the `lootId` APIs keep their current behavior. Slot-addressed endpoints reject version 1 documents with `409 INVENTORY_MIGRATION_REQUIRED`.
   - Before step 2: rolling deploy completes and old instances are drained.
2. **U2, Unity release.**
   - Sends the header and adds codec v10, no-merge hydration and slot APIs.
   - Against version 1 documents it reads normally and shows inventory mutations as unavailable until migration completes.
   - It can be distributed before the window. It has no split entry point (TASK-282 is separate).
3. **Maintenance window: gate on.** Every route in §8.1 requires protocol 2. From here, no old client or host build can read or write any inventory document.
4. **Maintenance window: migration** (§5).
   - The dry run was done earlier on a database copy.
   - Take a pre-migration backup, then run the real migration with per-`lootId` sum checks.
   - New characters are created at version 2 from this point.
5. **Window closes.**
   - Any remaining version 1 document is rejected with `409` and the script is rerun. Reads never migrate it.
   - Slot-addressed endpoints are now usable by U2.
6. **TASK-281 Stash split enabled** by configuration (§10).
7. TASK-282 Stash UI, as a separate delivery.

### 8.3 Rollback floors

- **Before step 3:** any backend at or above B1 with any client.
- **After step 4:** backend at or above B1 with clients at or above U2 only.
  - Rolling the backend back below B1 is forbidden: it would merge stacks and drop `slot`.
  - Turning the gate off is forbidden.
- **Restore from the pre-migration backup:** only inside the window, before the first post-migration write. After that it would lose committed operations.

## 9. Decisions (recorded)

| ID | Owner decision | Status |
| --- | --- | --- |
| D1 | Persist physical slots with holes and explicit Sort, replacing the TASK-218 compact exception | Approved. Contract update pending (§11). |
| D2 | O1 for MVP: Raid provenance used for eligibility is relative to the expedition; Town units persist no Origin | Approved for MVP. **Blocked on reconciliation of Doc 07 by its owner** before any contract changes. Not an authorization to change Game Design. |
| D3 | Protocol versioning with `426`, covering every write and every old version | Approved. §8 revised accordingly. |

No further decisions are needed for this proposal.

- `StackMax` stays deferred (L2).
- Over-capacity data is reducing-only (L2).
- The displaced equipped item goes to the Loadout (L2).
- Raid admission and extraction stay aggregate.
- Instances stay out of scope (§7).

## 10. Dependencies and work breakdown (no HacknPlan items created)

```
G1 Doc 07 reconciliation ─┐
G2 Doc 16 note ───────────┼─> P1 Architecture updates ─> P2 B1 ─> P3 migration script ─┐
                          │                         └─> P5 U2 (parallel) ───────────────┤
                          │                              P4 B2 slot endpoints/gate ─────┤
                          │                                                             v
                          │                                   P7 cutover (§8 steps 3-5)
                          │                                                             v
                          │                       TASK-281 Stash (authoritative split, flag on)
                          │                                                             v
                          │                       TASK-282 Stash UI "Dividir" (separate)
TASK-282 Raid request path + UI depends only on the Raid split already implemented.
```

| Task | Content | Depends on |
| --- | --- | --- |
| P1 | Architecture updates (§11.2) | G1, G2 |
| P2 | B1 expand backend: schema, field preservation, removal of read-time normalization, header handling (gate off), version 1/2 dual behavior, script writers, tests | P1 |
| P3 | Migration script: dry run, backup step, sum checks, idempotency marker, tests, dry-run report on a database copy | P2 |
| P4 | B2: slot-addressed move/swap/Sort; fill-rule placement for extraction, shop, unequip and rollback; exact reservation rollback; `409` enforcement; gate switch and its route tests for every route in §8.1 | P2 |
| P5 | U2 Unity: `StashItem.Slot`, codec v10, no-merge hydration, slot APIs across `ProfileInventoryRules`, `LocalProfileStore`, `MerchantTradePreview`, `RemoteInventoryService` and `StashTransferCoordinator`; `FindAmount` sum; header on every backend call; mutations unavailable on version 1 documents | P1; consumes P4 contracts |
| P6 | Lobby presentation of holes, slot paging and overflow markers | P5 |
| P7 | Cutover runbook and execution (§8.2 steps 3-5, §8.3) | P3, P4, P5, P6 |
| **TASK-281 (Stash half)** | Authoritative split only: backend endpoint `{list, sourceSlot, amount, destinationSlot, lootId, expectedRevision}` behind a configuration flag (revision-guarded, rejected during a pending reservation, no `StackMax`); `LocalProfileStore.TrySplit` mirroring `LootInventoryRules.ValidateSplit`; `StashTransferCoordinator.SplitAsync`, remote first; backend tests and `StashTransferCoordinatorTests`. No UI. | P4, P5; flag on after P7 |
| **TASK-282** | UI only, separate. Raid: Input Authority request path to `PlayerLootReceiver.TrySplitStack` and quantity dialog; depends only on the implemented Raid split. Stash: "Dividir" wired to `SplitAsync`; depends on the TASK-281 Stash half. | As stated |
| Separate | Defect `docs/defect_raid_loot_eligibility_other_team.md`; `StackMax` in Town (§3, §6); Equipment instance persistence (§7) | Independent |

## 11. Before implementation: verifications, documentation and approvals

### 11.1 Pending verifications

| ID | Verification | Blocks |
| --- | --- | --- |
| V1 | Whether moves, shop and order are blocked during a pending reservation (`InventoryService`) | P4 rollback rule |
| V2 | `shopSell` and `shopBuyback` bodies: list read and written, capacity, shape of buyback entries | P4 |
| V3 | Whether `GET /me`, `GET /me/profile` and `POST /me` return or create item lists | §8.1 gate surface |
| V4 | Whether ability purchase, progression commit and town session ticket use path-scoped updates or full-document `save()` | §8.1 gate surface, P2 |
| V5 | Every backend writer of `inventory.*` uses path-scoped updates or a slot-aware schema (`AbilityService`, `ExtractionCommitService`, `InventoryService`, `RaidResultService`, `SessionTicketService`, scripts) | P2 |
| V6 | `TryImportItems` callers in Unity | P5 |
| V7 | Whether Unity refuses local mutations before a successful hydration | P5, §8.2 step 2 |
| V8 | Backend Quick Slot handling (`backend/tests/qs.compatibility.test.js`) | P5 |
| V9 | `TryPrepareExpeditionLoadout` production body and how reserved entries map to admission | P4, P5 |
| V10 | Dry-run report on a database copy: duplicates, over-capacity lists, unknown IDs | P7 go/no-go |

### 11.2 Documentation updates (task P1, after G1/G2)

| Document | Update |
| --- | --- |
| `Docs/Architecture/LocalPlayerPersistenceArchitecture.md` | Replace `:101`, `:113-115`, `:137`, `:143-145` with the slot model (D1). Protocol gate and rollback floors (D3). Migration and `inventorySchemaVersion`. Codec v10. Fix the "schema 8" drift at `:69`. Quick Slot count rule. |
| `Docs/Architecture/RaidInventoryUIArchitecture.md` | Town and Stash slot projection with holes, paging and Sort. |
| `Docs/Architecture/LootInteractionArchitecture.md` | Point the "Physical stack split" note to the approved Town model. Record O1 wording for Raid provenance once G1 is resolved. |
| `Docs/Architecture/ExtractionArchitecture.md` | Extraction placement into Loadout slots. The eligibility wording is untouched here (defect record). |
| Backend contract notes (README/DTOs) | Header, `426`, `409 INVENTORY_MIGRATION_REQUIRED`, slot DTOs. |

### 11.3 Approvals and owner actions required

| ID | Required | Owner |
| --- | --- | --- |
| G1 | Reconcile Doc 07 §5/§7 with D2 (O1: Origin relative to the expedition for MVP). Prerequisite for any contract change. | Game Design owner |
| G2 | Note on Doc 16 that §2/§3/§8 now align with D1. The open deviation §4 ("retorna a Stash" vs Loadout, L2) remains recorded. | Game Design owner |
| G3 | Decision on the eligibility defect (Doc 05/07 vs architecture) | Game Design owner |
| A1 | Authorization to modify the Architecture documents in §11.2 | Owner, after G1/G2 |
| A2 | Authorization to create HacknPlan items for P1-P7, the defect and the separate debts | Owner |
| A3 | Cutover scheduling: maintenance window, backup, all clients updated to U2 | Owner and team |
| A4 | Commits, pushes and backend deploys per delivery policy | Owner |

TASK-281 stays partially implemented: Raid done, Stash blocked on G1, G2, P1-P5 and P7.
