# ODD Task Tracker: FIX-US-01 — Fiabilidad del lanzamiento de expediciones en Party (Preparación → Conectando → Dungeon)

- **Work Item:** HacknPlan #665 (FIX-US-01). Children in scope: FIX-02 #669, FIX-03 #670, FIX-04 #671, FIX-05 #672, FIX-06 #673. FIX-01 #668 is already in Testing (tracker `odd/tasks/fix-01-launch-ack-retry.md`) and is not reworked here.
- **Branch:** `fix/fix-us-01-launch-reliability` from `New-Testing` (`36c695b7`). Sources: GD 01 Game Flow §8/§14, GD 02 Estados §7-§9; `Docs/Architecture/LobbyAndSessionTransitionArchitecture.md`, `TownRaidQueueArchitecture.md`, `SessionLifecycleAndSpawning.md`, `RaidParticipantArchitecture.md`, `LocalPlayerPersistenceArchitecture.md`.

## Specs

Quoted verbatim from HacknPlan.

### Parent FIX-US-01 #665
- **S0** (AC): "Un lanzamiento de party termina en Dungeon para todos o se cancela para todo el grupo, sin expedición Duo iniciada como Solo (Estados §8)." / "Tras una cancelación, todos vuelven a Town/Preparación con la Party conservada y pueden lanzar de nuevo sin reiniciar la aplicación." / Out of scope: "Migración de Host, servidor dedicado o rediseño de la cola/Preparación." / "Mensajes al jugador (FIX-US-03)." / "Cambios de Game Design."

### FIX-02 #669 — Liberación de tickets de lanzamiento obsoletos y salida del estado Failed
- **S1** (Alcance 1): "Política explícita y observable de recuperación ante fallos de liberación o reversión de reservas de loadout durante cancelaciones o fallos de expedición: reintento acotado y transición a un estado recuperable documentado que impida el bloqueo permanente y garantice la integridad de items reservados."
- **S2** (Alcance 2): "Transición y salida definida desde el estado de fallo hacia Town cuando exista una sesión de Town operativa disponible."
- **S3** (AC1): "Tras cancelar una revisión con rollback exitoso, una nueva revisión puede reservar y ACKear."
- **S4** (AC2): "Tras un rollback fallido, el jugador puede volver a lanzar después de la recuperación definida, sin pérdida ni duplicación de items reservados."
- **S5** (AC3): "El estado almacenado de la reserva queda consistente (verificado vía API pública del store)."
- Out of scope (verbatim): "Cambios de contrato de reservas con el backend." / "Mensajes de UI (FIX-14)."

### FIX-03 #670 — Notificación al Host del fallo de entrada de un miembro y aborto coordinado inmediato
- **S6** (Alcance 1): "Notificación inmediata y observable hacia la autoridad de la partida ante el fallo definitivo de entrada o desconexión previa de un integrante requerido, disparando la cancelación coordinada del grupo sin agotar la ventana máxima de espera."
- **S7** (Alcance 2): "Interrupción inmediata de la espera de admisión en los clientes cuando la partida haya cerrado la admisión o iniciado la secuencia de aborto."
- **S8** (AC1): "Si un miembro requerido se desconecta antes de ser admitido, el Host inicia el cierre por BootstrapFailure en el tick siguiente y los demás vuelven a Town."
- **S9** (AC2): "Un cliente cuyo Host abortó la admisión abandona la espera en menos de un segundo tras observar el cierre."
- **S10** (AC3): "No se inicia silenciosamente una expedición Duo como Solo."
- Out of scope (verbatim): "Reintentos de conexión del miembro que falla (comportamiento actual de disponibilidad)." / "Mensajes de UI (FIX-14)."

### FIX-04 #671 — Alineación de ventanas de admisión y liberación escalonada Host-primero
- **S11** (Alcance 1): "Secuencia de liberación ordenada garantizando que la sesión de la partida esté creada y operativa antes del ingreso de los clientes no autoritativos, o documentación formal de la política de liberación simultánea."
- **S12** (Alcance 2): "Alineación de los presupuestos y ventanas de tiempo de conexión entre la autoridad de la partida y los clientes bajo un límite temporal común coherente."
- **S13** (AC1): "En una Party Duo con Host de raid distinto del Town authority, el cliente nunca intenta unirse antes de que la sesión del Host exista (verificado por logs)."
- **S14** (AC2): "Un miembro que completa la conexión dentro del presupuesto documentado nunca dispara BootstrapFailure por desalineación."
- **S15** (AC3): "La arquitectura refleja el orden y las ventanas implementadas."
- Out of scope (verbatim): "Cambiar valores de balance de diseño." / "Migración de Host."

### FIX-05 #672 — Recuperación a Town tolerante a excepciones tras fallo de raid
- **S16** (Alcance 1): "Captura y clasificación integral de errores durante la inicialización de la sesión de Town y el cierre de sesiones activas, garantizando la transición a un estado terminal definido con motivo observable."
- **S17** (Alcance 2): "Propagación estructurada del resultado de fallo del lanzamiento coordinado hacia la capa de presentación de Town para feedback directo al usuario."
- **S18** (AC1): "Si StartHubSessionAsync lanza una excepción durante la recuperación, el coordinador termina en un estado definido con un SessionTransitionResult consumible, sin excepciones no observadas."
- **S19** (AC2): "Un fallo del lanzamiento coordinado deja un resultado consumible por la UI de Town."
- Out of scope (verbatim): "Diseño visual del mensaje (FIX-14)."

### FIX-06 #673 — Reenvío de admisión del cliente y manejo explícito del ticket autoritativo
- **S20** (Estado): "sospecha por lectura de código; requiere reproducción antes del fix."
- **S21** (AC1): "Evidencia de reproducción registrada o cierre documentado como no reproducible."
- **S22** (AC2): "Si se corrige: un payload rechazado por orden se reenvía y el cliente es admitido; un ticket autoritativo no obtenible produce un fallo de conexión explícito en lugar de una espera de 30 s."
- Out of scope (verbatim): "Cambios de backend en emisión de tickets."

### Shared validation (every child)
- **S23**: "Test automatizado que reproduzca el defecto: RED observado antes del cambio y GREEN después; ejecutar la suite EditMode de Networking afectada." / "Validación manual multi-aplicación (Host + Cliente) del escenario, con logs de ambas instancias registrados." / "No se cierra sin evidencia registrada."

## Tasks

| ID | Specs | Route | Commit |
| --- | --- | --- | --- |
| T1 | S16, S18, S19, S23 | inline, TDD: FIX-05 guarded Town recovery (catch-path recovery, unguarded `StartHubSessionAsync`, discarded launch task) ending in a defined state with a consumable result — done (manual part in T6) | this commit |
| T2 | S1-S5, S23 | inline, TDD: FIX-02 stale ticket release, bounded rollback retry, recoverable state through existing `ReturningTown`/`ConnectingTown` routes (L6); `ReturnToMainMenuAsync` rolls back before dropping the ticket (L6) | — |
| T3 | S11-S15, S23 | TDD: FIX-04 Host-first release and aligned windows; architecture update | — |
| T4 | S6-S10, S23 | TDD: FIX-03 Host notification and immediate client abort, sharing FIX-04 timers | — |
| T5 | S20-S22 | inline: FIX-06 arrival-order and ticket-failure diagnostics only; fix waits for the human's Host + Client repro (L6) | — |
| T6 | S23 | human: manual Host + Client validation with both logs, per child | — |

## Log

- **L1** (user, 2026-11-12): "revisa FIX-US-01 — Fiabilidad del lanzamiento de expediciones en Party (Preparación → Conectando → Dungeon) y empeza la implementacion de las tareas que aun no estan en testing, siguiendo ODD como metodologia."
- **L2** (HacknPlan, 2026-11-12): FIX-01 #668 Testing; FIX-02..FIX-06 (#669-#673) Planned.
- **L3** (exploration, FIX-02/FIX-05 on `36c695b7`): FIX-02 defects still present. `TryStoreRaidLaunchContext` returns `TicketMismatch` without releasing the stale ticket; `CancelPendingRaidLaunch` keeps `_activeTicket` when `TryRollbackActiveReservation` fails (single local attempt, backend rollback fire-and-forget); `ObserveLaunchReservationSave` returns Success when `_launchReservationSave == null`, so a retained same-revision ticket can report Success after cancel; `RecoverTownAfterRaidFailureAsync` enters `Failed` with the ticket set; `ReturnToMainMenuAsync` → `CompleteTownEntry` drops the ticket without rollback, leaving `LocalProfileStore.PendingReservation` set. FIX-05 defects still present: `EnterRaidAsync` awaits recovery inside both `catch` blocks; `RestoreTownAfterRaidFailureAsync` and `RecoverTownAfterFailedReconnectAsync` await `StartHubSessionAsync` unguarded (it throws `ArgumentException` for an unbuilt scene), leaving `ReturningTown`; `ShutdownActiveRunnersAsync` awaits `_participationOutcomeTask` unguarded; `BeginAcknowledgedRaidLaunchAsync` is discarded (`_ =`) with result only to `LogError`. Town UI consumes `TryConsumeLastTransitionFailure` only on `Bind()`. Test seams: `ExpeditionWeaponRequirementTests`, `SessionConnectionStateMachineTests` (real launchers, reflection helpers).
- **L4** (open decision): architecture `LobbyAndSessionTransitionArchitecture.md` L50-54 defines `Failed -> ConnectingTown | ConnectingRaid | ReturningTown` and "`Failed` means there is no usable active runner"; there is no `Failed -> Town` edge. S2 requires an exit from Failed to Town; whether to add the edge or route through `ReturningTown`, and whether `ReturnToMainMenuAsync` must roll back the reservation, are owner decisions.
- **L5** (exploration, FIX-03/04/06 on `36c695b7`): all audit points still present. Host aborts only on `ExpectedAdmissionDeadline` expiry (`NetworkMatchController.cs:230-241`); not-yet-spawned `OnPlayerLeft` only logs (`NetworkSpawnManager.cs:4814-4824`); client admission loop never reads `Phase`/`ClosureState` (`FusionSessionLauncher.cs:599-627`) and unadmitted clients keep the Host runner alive (`RaidResultsReturnPolicy.cs:79-97`). Release dispatches every initial departure in one loop (`TownRaidPreparationNetworkController.cs:653-711`) while the docs (`TownRaidQueueArchitecture.md:66-68`, `LobbyAndSessionTransitionArchitecture.md:174-180`) require Host-first. Windows: Host 30 s const from Host bootstrap, client 30 s const admission, 40 s serialized client availability from own launch, 30 s scene wait. FIX-03 and FIX-04 share the admission window and the client wait loop: design one origin and one replicated deadline. Admission send-once and silent ticket fallback (`FusionSessionLauncher.cs:310-363`) still present.
- **L6** (user, 2026-11-12, decisions): FIX-02 Failed exit: "Usar la ruta existente (Recomendado)" — no `Failed -> Town` edge; bounded rollback retry, then recovery through the existing `ReturningTown`/`ConnectingTown -> Town` routes, policy documented. `ReturnToMainMenuAsync`: "Sí, rollback antes de salir (Recomendado)". FIX-06: "Instrumentar y esperar tu repro" — add arrival-order/ticket diagnostics; the fix or a not-reproducible closure waits for the human's Host + Client run.
- **L7** (technical note): `LocalProfileStore.CreateReservation` rejects a different id while a pending reservation exists, so the defined recovery for S4 must resolve (roll back) a stale pending reservation before a new revision reserves; releasing the ticket alone does not unblock relaunch.
- **L8** (evidence, T1): RED via Unity CLI (`unity command test run --mode editor --filter SessionConnectionStateMachineTests`): 49 total, 4 failed, all new FIX-05 tests (`RaidFailureRecovery_HubStartupThrows_EndsFailedWithConsumableResult`, `FailedReconnectRecovery_HubStartupThrows_EndsFailedWithConsumableResult`, `RunnerShutdown_FaultedParticipationOutcome_StillShutsDownRunners`, `CoordinatedLaunchFailure_PublishesConsumableTransitionResult`). GREEN after implementation: 49/49; `ExpeditionWeaponRequirementTests` 27/27. Changes: `RestoreTownAfterRaidFailureAsync` and `RecoverTownAfterFailedReconnectAsync` guard shutdown/startup and end through `EnterRecoveryFailed` (`Failed` + consumable `RecoveryFailed`); `ShutdownActiveRunnersAsync` observes a faulted loss-clear task; `BeginAcknowledgedRaidLaunchAsync` publishes a non-successful result and observes faults. Architecture note in `LobbyAndSessionTransitionArchitecture.md` "Failure and recovery". Advisory: `TownRaidPreparationPresenter` consumes the transition failure only in `Bind()`, so a failure published while the panel is already bound shows on the next bind; display timing belongs to FIX-14 (message UI). S23 manual Host + Client evidence pending (T6).
