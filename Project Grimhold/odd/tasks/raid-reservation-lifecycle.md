# Raid reservation lifecycle

Keep the backend `inventory.pendingReservation` alive from Raid admission until the participation outcome.
Branch: `fix/raid-reservation-lifecycle`. Root cause: admission confirmation deletes the backend reservation
(`SessionConnectionCoordinator.cs` `TryConfirmActiveReservation`), while since `63f574df6` publishing an
authoritative extraction result requires it (`RaidResultService.js`, `ExtractionArchitecture.md:341`).

## Specs

- **S1 Admission keeps the reservation.** Confirming Raid admission no longer deletes the backend reservation.
  It marks it admitted (`admittedAt`) through a new idempotent endpoint
  `POST /character/me/inventory/reservation/admit` with body `{ reservationId }`. Same id already admitted:
  success, unchanged. No pending reservation or different id: `422 NO_ACTIVE_RESERVATION`, unchanged.
  The serialized reservation exposes `admittedAt` (ISO string or null) to Unity.
- **S2 Extraction succeeds.** With the reservation admitted, the host publish and the recipient commit of an
  extraction succeed; the commit consumes the reservation (existing `ExtractionCommitService` behavior).
  User report: "al extraer me tira todos estos errores" — `NO_ACTIVE_RESERVATION: Target character does not have
  an active raid reservation.` and `NO_AUTHORITATIVE_RESULT - No authoritative extraction result found for this raid.`
- **S3 Every non-extraction ending is a loss.** User decision: "Todo final sin extracción = pérdida". Defeat,
  abandon, return without extraction, definitive disconnect, Absolute Closure, host cancel/timeouts, Host
  Migration failure and crash/relogin consume the admitted reservation without returning any reserved item or
  Equipment: `DELETE /character/me/inventory/reservation` (no rollback).
- **S4 Admitted reservations cannot be rolled back.** `DELETE ...?rollback=true` on an admitted reservation
  returns `409 RESERVATION_ADMITTED` and changes nothing. `savePendingReservation` never restores the items of an
  admitted reservation (its empty-loadout recovery branch applies only to non-admitted reservations).
  Pre-admission rollback keeps its current behavior.
- **S5 Extracted loot is never lost by a loss clear.** Clearing (S3) or replacing (S4 new reservation) an admitted
  reservation while an unconsumed `AuthoritativeExtractionResult` targets that character returns
  `409 EXTRACTION_RESULT_PENDING` and changes nothing.
- **S6 Unity calls the right endpoint per outcome.** Admission confirmation calls the admit endpoint (S1).
  Leaving a Raid without an extraction commit for that participation calls the loss clear (S3). Login bootstrap:
  a hydrated backend reservation that is admitted, with no local pending extraction commit, is discarded locally
  without restoring items and cleared on the backend as a loss; a pending extraction commit keeps its existing
  retry path. Non-admitted orphans keep the current rollback.
- **S7 Next Raid carries the Town equipment.** User report: "en el town estoy con equipamiento pero cuando entro a
  la raid no tengo nada". After S1–S6, a Town→NPC Create Raid admits the avatar with the Town Inventory and the
  eight prepared Equipment slots.

## Tasks

| ID | Specs | Route | Commit |
|---|---|---|---|
| T1 Backend reservation lifecycle (admit endpoint, admitted guards, loss clear, tests) | S1, S3, S4, S5 | worker (backend) | `43eefd27` |
| T2 Unity outcome wiring (admit on confirm, loss on non-extraction exit, bootstrap) + tests | S1, S3, S6 | worker (Unity) | this commit |
| T3 Architecture docs (Extraction, LocalPlayerPersistence, RemoteIdentityAndAuth) | S1–S6 | inline | this commit |
| T4 Verification: backend suite, Unity EditMode/PlayMode, manual Raid S2/S7 | S2, S7 | inline | — |

## Log

- **L1** (user, original): "ok, esta bien, commitealo. Hay un problema, en el town estoy con equipamiento pero cuando
  entro a la raid no tengo nada. Esto va por fuera de la tarea pero tengo que arreglarlo."
- **L2** (user): entry route "Town → NPC de Raid (Create)"; missing "Armadura (casco/pecho/guantes/botas), Armas,
  Inventario (items del loadout)".
- **L3** (user): extraction errors `NO_AUTHORITATIVE_RESULT`, `NO_ACTIVE_RESERVATION` (x2),
  `DungeonPressureController.Phase` InvalidOperationException; "Estoy en el juego, extraje. Tengo las cosas en el
  inventario despues de extraer."
- **L4** (decision): option "A — La reserva vive hasta el resultado".
- **L5** (decision): "Todo final sin extracción = pérdida".
- **L6** (evidence): only extraction writes to the backend after admission; defeat/abandon/disconnect/closure have no
  backend call (explore handoff mvudxfts-1-2g21).
- **L7** (evidence, T1): focused backend tests 69/69, `npm test` 235/236, `npm run test:integration` 81/81. The one
  failure, `shop catalogs mirror every Unity LootDefinition price and eligibility`, reads only `shopCatalog.js` and
  Unity `Loot/Definitions`, which this change does not touch: pre-existing, out of scope.
- **L8** (evidence, T2): diff reviewed; bootstrap orphan-recovery failure changed back to non-fatal (log, skip backend
  cleanup, login continues). `dotnet build Assembly-CSharp-Editor.csproj` (includes the new tests): 0 errors. Unity
  MCP unavailable (`no_unity_session`), so EditMode tests are not yet run and T2 is not committed. Out of scope: the existing diverged dev character
  (local items, empty backend) and the `DungeonPressureController.Phase` exception.
- **L9** (evidence, T2): Unity EditMode full suite 5080/5103; the 23 failures are outside Stash/reservation/persistence
  (UI layout, loot catalogs, prefab budgets, Off Hand assets; `RaidDefeatLifecycleSourceTests` reads untouched
  `NetworkRaidParticipant.cs`). Targeted RaidReservationLifecycle/StashReservationInvariant/HydrateSnapshot/
  LocalProfilePersistence: 18/18.
