# Stash bulk Take All / Leave All

Replace the per-stack HTTP round trips of Town Stash Take All / Leave All with one backend request.
Backend: `backend/`. Unity: `Project Grimhold/Assets/Scripts/Stash/`. HacknPlan: none (owner request, Sprint 16 audit follow-up).

## Specs

- S1. User problem, verbatim: "en el stash, al usar dejar todo o tomar todo, se van pasando uno por uno los items, si tenes el inventario lleno se toma su tiempo." and "tarda muchisimo el tomar todo, se puede mejorar? es problema de render?"
- S2. Approved fix, verbatim: "si hacelo". It answers the proposal: a backend bulk endpoint (`POST transfer-all`) handles Take All / Leave All in one request. It applies the same per-stack rules as today, skips the stacks that do not fit, persists in a single revision-guarded write and returns a result per item.
- S3. Per-stack semantics stay the same as today. A stack is moved under exactly the rules of the existing single full-stack transfer (`moveToLoadout` / `moveToStash`, v2/v3 slots and instances). Stacks are taken in source slot order. A refused stack is skipped and later stacks are still evaluated. The operation is not all-or-nothing per stack (RaidInventoryUIArchitecture.md, GD 07 §16).
- S4. Exactly one HTTP request per Take All / Leave All, and at most one inventory write. The write is guarded by `expectedRevision`. A revision conflict, a pending reservation or a missing character rejects the request and writes nothing. If nothing moves, nothing is written. The request uses the inventory protocol middleware like the other slot routes.
- S5. The response contains the resulting `stash`, `loadout` and `revision`, plus a per-stack result (`slot`, `lootId`, moved or refused with an `errorCode`).
- S6. Unity: `StashTransferCoordinator` Take All / Leave All issue the single bulk call. On success the local profile reflects exactly the backend result, with one local commit and one UI refresh. The completed/failed counts in `StashTransferOutcome` come from the per-stack results. Remote failure leaves the local state unchanged and reconciles once, as single transfers do today.
- S7. Docs: update `backend/README.md` (route, DTO, error codes), `Docs/Architecture/RaidInventoryUIArchitecture.md` and `LocalPlayerPersistenceArchitecture.md` (single request and write, with per-stack skip semantics preserved).

## Tasks

- T1 [S2-S5, S7 backend] Backend endpoint, service, validator, tests (unit, integration, protocol, concurrency) — route: worker — commit: not authorized — status: done. Evidence: RED `node --test tests/inventory.transferAll.test.js` 14 fail (`transferAll is not a function`) and protocol test 6 fail (404 route); GREEN `npm test` 605 pass/0 fail, `npm run test:integration` 213/0, `npm run test:concurrency` 22/0. Integration and concurrency files were written after the implementation (no RED observed).
- T2 [S6, S7 Unity] Unity client: InventoryClient, RemoteInventoryService, coordinator, tests — route: worker (after T1) — commit: not authorized — status: done. Evidence: RED EditMode StashTransferCoordinatorTests 13 fail of 74 (per-stack calls instead of `bulk:*`); GREEN 74/74; EditMode full 3630/3641 pass (11 failures in Combat/Loot/Presentation/Scenario, unrelated to Stash); PlayMode LobbyStashPresenterPlayModeTests 31/31.

## Log

- L1: "si hacelo" (approving the bulk endpoint proposal; see S1 and S2 for the original report).
