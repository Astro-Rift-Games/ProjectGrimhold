# ODD Task Tracker: FIX-01 — Reintento del ACK de lanzamiento ante NotReady transitorio

- **Work Item:** HacknPlan #668 (FIX-01), parent FIX-US-01 (#665). Branch `fix/fix-01-launch-ack-retry` from `New-Testing` (`69b6a910`).
- **Sources:** GD 02 - Estados del Juego §8 Conectando; `Docs/Architecture/LobbyAndSessionTransitionArchitecture.md` (Launch acknowledgement and rejection); `TownRaidQueueArchitecture.md`.

## Specs

Quoted verbatim from HacknPlan #668.

- **S1** (Alcance 1): "Reintento acotado de la materialización y confirmación local de lanzamiento mientras la preparación permanezca en estado activo, la revisión no esté reconocida ni rechazada y la ventana de reconocimiento no haya expirado." — "entregables observables: reintentos periódicos de confirmación hasta resolución o timeout sin bloqueos por frame."
- **S2** (Alcance 2): "Diagnóstico explícito y observable de la causa de indisponibilidad transitoria (operación activa, resultado pendiente, ticket no coincidente o ausencia de continuidad de grupo) para alimentar el motivo de cancelación ante expiración." — "entregables observables: clasificación del motivo para feedback al jugador."
- **S3** (AC1): "Si el primer intento devuelve NotReady y la condición se resuelve antes del ACK deadline, el miembro envía RPC_AcknowledgeLaunch y el lanzamiento continúa."
- **S4** (AC2): "Si la condición no se resuelve, el lanzamiento se cancela por deadline como hoy, sin ACK duplicados ni reintentos por frame sin espaciado."
- **S5** (AC3): "Un resultado permanente distinto de NotReady sigue rechazando la revisión exactamente una vez."
- **S6** (Validación): "Test automatizado que reproduzca el defecto: RED observado antes del cambio y GREEN después; ejecutar la suite EditMode de Networking afectada." / "Validación manual multi-aplicación (Host + Cliente) del escenario, con logs de ambas instancias registrados." / "No se cierra sin evidencia registrada."
- Out of scope (verbatim): "Liberación de tickets obsoletos (FIX-02)." / "Mensajes de UI (FIX-12)."

## Tasks

| ID | Specs | Route | Commit |
| --- | --- | --- | --- |
| T1 | S2 | inline, TDD: coordinator classifies each transient `NotReady` cause (`RaidLaunchNotReadyReason`) — done | `fd60ef5c` |
| T2 | S1, S3, S4, S5 | inline, TDD: spaced, window-bounded local retry in `AdvanceLocalLaunch`; missing party continuation becomes a classified transient retry; wait reason observable and logged once per change/expiry — done | `fd60ef5c` |
| T3 | S6 | inline: EditMode suite via Unity CLI; architecture note — done | `fd60ef5c` |
| T4 | S6 | human: manual Host + Client validation with both logs recorded; also measure the reservation-save duration against the production backend (L8) | — |

## Log

- **L1** (user, 2026-11-12): "revisa FIX-01 — Reintento del ACK de lanzamiento ante NotReady transitorio y empeza la implementacion siguiendo ODD"
- **L2** (exploration, current `New-Testing`): the audit defect at `76865ed2` (retry only on `SnapshotRevision` change) is partially fixed: `Render` now calls `TryMaterializeLocalLaunchContext` every frame and `AdvanceLocalLaunch` keeps `_localLaunchPreparationPending`, so NotReady is retried **once per rendered frame with no spacing** (violates S4) and with no window bound (S1). NotReady carries no cause (S2). A missing party continuation returns silently in `TryMaterializeLocalLaunchContext` before `AdvanceLocalLaunch`. `TryStoreRaidLaunchContext` NotReady causes: `SessionConnectionCoordinator.cs:213-216` (unavailable/not Town, `_operationActive`, `_participationOutcomeTask` pending), `:224` (profile/party continuation), `:234` (ticket mismatch), `:298` (reservation save pending). ACK deadline: State Authority only, `Time.time + _coordinatedReleaseTimeoutSeconds` (15 s).
- **L4** (evidence, T1+T2): RED via Unity CLI `test run --filter ExpeditionWeaponRequirementTests` with API scaffold and old behavior: 26 total, 8 failed (all new FIX-01 tests; `PermanentRejectionIsReportedExactlyOnce` passed as the S5 regression guard). GREEN after implementation: 26/26 passed. Architecture note added to `LobbyAndSessionTransitionArchitecture.md` (Launch acknowledgement and rejection).
- **L3** (decision, technical): retry spacing 0.25 s (serialized), local window = `_coordinatedReleaseTimeoutSeconds` from the first local observation of the launch revision; a new snapshot revision is attempted immediately. The cause is exposed locally (coordinator `LastLaunchNotReadyReason`, controller `LocalLaunchWaitReason`) for FIX-12/FIX-03 to consume; sending it to the Host is FIX-03 scope, not here. The permanent-but-NotReady profile mismatch at `:224` keeps today's behavior (follow-up).
- **L5** (evidence, T3): full EditMode suite via Unity CLI on the implementation: 6651 total, 6648 passed, 3 failed, all in `Stash/PhysicalProfileSlotTests` (`Extraction_ReceivesAggregateDuplicatesIntoEmptyLoadout_AndPersistsAggregateRetry`, `RaidAdmissionCapacity_ExtractionThenReservation_KeepsEquipmentOutsideBackpack(1)` and `(30)`: `StashItem[]` vs `List<StashItem>` value comparison). That file does not reference the launch coordinator or controller, and the diff touches no Stash code. Not re-run on the base commit, so 'pre-existing' is unverified. T4 (manual Host + Client with both logs) is pending with the human; FIX-01 does not close without that evidence.
- **L6** (independent verify, gentle-ai-verify, static review): no blocking defects. Verdicts: S1-S5 met. S6 automated evidence was reported by the parent; manual T4 is pending. Advisories: (1) duplicate XML `<summary>` on `LastLaunchNotReadyReason` was fixed; (2) a pinning test for an immediate retry on a new snapshot revision was added, and `ExpeditionWeaponRequirementTests` now passes 27/27; (3) untested `OperationActive`/`ParticipationOutcomePending`/`CoordinatorUnavailable` classifications are a follow-up, since there is no public seam without reflection on private state; (4) edge case: on a State Authority change mid-launch the new authority re-arms its ACK deadline, but a peer whose local window already expired does not reopen it, so that revision ends by deadline cancel. This is rare and is a follow-up for the Host Migration/FIX-03 scope.
- **L7** (independent verify of `d01bf188`, static): no blocking defects. The coordinator diff is comments only. The new test uses the real snapshot API and is non-tautological: it pins the new-SnapshotRevision bypass, and together with the existing same-snapshot spacing test covers both sides of the rule. S1-S5 are met. S6 remains open until T4 (manual Host + Client with both logs).
- **L8** (user, 2026-11-12): "ok... lo dejamos asi, y lo medimos en t4". Decision: keep the existing 15 s `_coordinatedReleaseTimeoutSeconds` ACK window. During T4, measure how long the reservation save takes against the production backend (Render; `BackendConfiguration.asset` TimeoutSeconds 60, `RemoteIdempotentRetryPolicy` up to 3 retries) and record it. If it can exceed 15 s, the window value goes to FIX-04 (alignment of admission windows).
