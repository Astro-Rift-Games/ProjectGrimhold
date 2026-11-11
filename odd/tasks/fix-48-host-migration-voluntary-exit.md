# FIX-48 — Voluntary Host exit triggering Fusion 2.1 Host Migration (spike)

HacknPlan FIX-48 (ID 728), parent FIX-US-08 (ID 727). Blocks FIX-50 and FIX-51.
Execution tracker only; HacknPlan owns scope. Productive implementation belongs to FIX-50; the architecture rewrite belongs to FIX-52.

## Specs

- S1 (Objetivo, verbatim): "Confirmar con la documentación oficial y un prototipo multi-proceso cómo lograr que la salida voluntaria del Host (Shutdown/Abandon/Return) dispare la migración en los clientes con el estado actual, y qué limitaciones impone Fusion (snapshot, latencia, objetos no migrables)."
- S2 (AC, verbatim): "Evidencia de prototipo (logs de los tres procesos) o explicación documentada de por qué no es posible y alternativa propuesta."
- S3 (AC, verbatim): "Lista de estados que hoy no migran y su impacto."
- S4 (Alcance 2, verbatim): "Registro de conclusiones técnicas y definición de requisitos para la transferencia determinista de autoridad entre participantes activos." Deliverable: "especificación de diseño aprobada para su implementación."
- S5 (Validación, verbatim): "Ejecución multi-proceso registrada; no se agrega dependencia sin aprobación."
- S6 (Fuera de alcance, verbatim): "Implementación productiva (FIX-50)."

## Tasks

- [x] T1 — S1: official docs + code/architecture map (explorer mwbmbur0-1-3ol2). Route: explore. Commit: n/a.
- [x] T2 — S1, S2, S5, S6: dev-only prototype trigger in the real Raid: Host calls `PushHostMigrationSnapshot` then `Shutdown(ShutdownReason.HostMigration)`; clients run the existing `HostMigrationLifecycleController`. Route: inline, TDD. `VoluntaryHostHandoffProbe` (`UNITY_EDITOR || DEVELOPMENT_BUILD`), menu `Grimhold/Dev/FIX-48 Voluntary Host Handoff`. RED: CS0246 missing `IHostHandoffRunner`; GREEN: `VoluntaryHostHandoffProbeTests` 5/5 EditMode. Commit: this branch, `fix/fix-48-voluntary-host-handoff-probe`.
- [ ] T3 — S2, S5: 3-process run (Host + 2 Clients, Multiplayer Play Mode 3.0.0 already in manifest), logs captured under `odd/evidence/fix-48/` (or reported paths). Route: unity-operator / manual. Commit: pending.
- [ ] T4 — S3: list of state that does not migrate today and its impact (architecture `HostMigrationRecoveryArchitecture.md` :127-212 + run observations). Route: inline. Commit: pending.
- [ ] T5 — S4: conclusions + requirements document for FIX-50/FIX-52 under `Project Grimhold/Docs/Architecture/` or `docs/` (location to confirm). Route: inline. Commit: pending.

## Log

- L3 (decision): user answered "hace la que te parezca mas eficaz" on prototype disposition; chosen: dev-only probe committed on a feature branch so FIX-50 can reuse and rerun it.

- L1 (user, verbatim): "revisa FIX-48 — Verificación técnica del traspaso de Host en Fusion 2.1 ante salida voluntaria y empeza la implementacion siguiendo ODD"
- L2 (evidence, T1): Fusion Disconnect & Reconnect sample: "Shutdown the current `NetworkRunner`; it is recommending to provide the reason as `ShutdownReason.HostMigration`" after a pushed snapshot; normal Host leave closes the room (`DisconnectedByPluginLogic`). Project: `RunnerShutdownUtility.cs:20` shuts down with default reason; `SessionConnectionCoordinator.cs:1401-1409` and `NetworkRaidParticipant.cs:715-737` reject Host Return/Abandon; `SessionConnectionCoordinator.cs:1978-1981` already suppresses Town return on `HostMigration` shutdown in Raid. `NetworkProjectConfig.fusion:67-69` auto snapshot every 10 s. Only existing cloud fixture (`ProtectiveOrbsAbilityPlayModeTests.cs:609-626`) is single-process, not valid evidence per `Project Grimhold/AGENTS.md`. Contract `HostMigrationRecoveryArchitecture.md:10-12` forbids voluntary migration; user product decision 2026-11-01 (FIX-US-08) changes it; rewrite is FIX-52.
