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
- [x] T2 — S1, S2, S5, S6: dev-only prototype trigger in the real Raid: Host calls `PushHostMigrationSnapshot` then `Shutdown(ShutdownReason.HostMigration)`; clients run the existing `HostMigrationLifecycleController`. Route: inline, TDD. `VoluntaryHostHandoffProbe` (`UNITY_EDITOR || DEVELOPMENT_BUILD`), menu `Grimhold/Dev/FIX-48 Voluntary Host Handoff`. RED: CS0246 missing `IHostHandoffRunner`; GREEN: `VoluntaryHostHandoffProbeTests` 5/5 EditMode; verifier (mwbmo9dz-2-v9c0) S1/S6 covered, S2/S5 blocked on T3; null-runner case added, 6/6. Commit: this branch, `fix/fix-48-voluntary-host-handoff-probe`.
- [x] T3 — S2, S5: 3-process Raid run (Duo Party + Solo player = Host + 2 Clients, see L7), logs in `odd/evidence/fix-48/`. Commit: this branch (report commit).
- [x] T4 — S3: list of state that does not migrate today and its impact (architecture `HostMigrationRecoveryArchitecture.md` :127-212 + run observations). Route: inline. Delivered in report §5. Commit: this branch (report commit).
- [x] T5 — S4: `docs/fix-48_reporte_traspaso_host_salida_voluntaria.md` (T4 list in §5, requirements in §6). Route: inline. Commit: this branch (report commit).

## Log

- L3 (decision): user answered "hace la que te parezca mas eficaz" on prototype disposition; chosen: dev-only probe committed on a feature branch so FIX-50 can reuse and rerun it.

- L1 (user, verbatim): "revisa FIX-48 — Verificación técnica del traspaso de Host en Fusion 2.1 ante salida voluntaria y empeza la implementacion siguiendo ODD"
- L2 (evidence, T1): Fusion Disconnect & Reconnect sample: "Shutdown the current `NetworkRunner`; it is recommending to provide the reason as `ShutdownReason.HostMigration`" after a pushed snapshot; normal Host leave closes the room (`DisconnectedByPluginLogic`). Project: `RunnerShutdownUtility.cs:20` shuts down with default reason; `SessionConnectionCoordinator.cs:1401-1409` and `NetworkRaidParticipant.cs:715-737` reject Host Return/Abandon; `SessionConnectionCoordinator.cs:1978-1981` already suppresses Town return on `HostMigration` shutdown in Raid. `NetworkProjectConfig.fusion:67-69` auto snapshot every 10 s. Only existing cloud fixture (`ProtectiveOrbsAbilityPlayModeTests.cs:609-626`) is single-process, not valid evidence per `Project Grimhold/AGENTS.md`. Contract `HostMigrationRecoveryArchitecture.md:10-12` forbids voluntary migration; user product decision 2026-11-01 (FIX-US-08) changes it; rewrite is FIX-52.
- L4 (evidence): MPPM clones failed Town with "Failed to load NetworkProjectConfigAsset" (clone AssetDatabase had no import result for the ScriptedImporter-backed `NetworkProjectConfig.fusion`; reimport in main + clone Refresh did not help). Deactivating/reactivating the clones fixed it; user: "estan los 3 en town".
- L5 (user, verbatim): "solo se pueden hacer duos". Code: `TownPartyRules.MaxMembers = 2` (Assets/Scripts/Networking/TownPartyRules.cs:5). Host + 2 Clients Raid is impossible under the Duo rule; conflicts with FIX-48 "logs de los tres procesos" and FIX-US-08 "validación multi-proceso Host + dos Clientes obligatoria".
- L6 (decision): user chose "Duo: Host + 1 cliente". T3 runs with 2 processes; S2 closes through "explicación documentada de por qué no es posible y alternativa propuesta"; the 3-process requirement conflict is reported for the FIX-US-08 owner.
- L7 (evidence, T3): 2026-11-11 12:22:39 UTC probe run on Player 2 (mppm45af563a, Host); Raid had 3 participants (Duo Party + Solo). Player 3 (mppm78c6231b) "Replacement adopted as RecoveredAsHost", restored population 3, recovered 2, old Host 6af3ba84d4467f2fdbf3dd12 "Unrecovered participant finalized ... State=Aborted". Main Editor "Replacement adopted as RecoveredAsClient". Old Host: outcome=HandedOff, elapsedMs=665, then stuck in Gameplay, State=Raid, runner null ("Town recovery suppressed"). An earlier attempt on the Main Editor returned "no running Raid Host runner in this process" with no side effects. MPPM clone incident fixed by reactivating clones.
