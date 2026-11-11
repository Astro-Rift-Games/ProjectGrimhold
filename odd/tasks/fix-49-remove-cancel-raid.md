# FIX-49 — Remove "Cancelar raid"; Abandon/Return available to the Host

HacknPlan FIX-49 (ID 729), parent FIX-US-08 (ID 727). Branch `fix/fix-49-remove-cancel-raid`, stacked on `fix/fix-50-host-handoff` (85e7549d).
Execution tracker only; scope and acceptance live in HacknPlan.

## Specs

- S1 (AC): "El menú de raid del Host no muestra Cancelar raid y sí Abandonar."
- S2 (AC): "No quedan referencias a HostCancellation en el código de producción."
- S3 (scope 1): "Eliminación total de la opción de cancelación de expedición por parte del anfitrión en interfaces, controladores y mensajería de juego, garantizando que ninguna partida activa pueda ser abortada unilateralmente para terceros."
- S4 (scope 2): "Homogeneización del flujo de abandono voluntario para el anfitrión, aplicando las mismas penalizaciones, generación de cadáver y consecuencias que para cualquier participante." User decision (L3): the Host sees the loss Results like a client and the FIX-50 handoff starts only when the Host presses Volver.
- S5 (validation): "Test automatizado del comportamiento: RED observado antes del cambio y GREEN después" and "Validación manual multi-proceso (Host + al menos un Cliente; para migración, Host + dos Clientes según HostMigrationRecoveryArchitecture.md) con logs registrados. Una prueba de un solo runner no es evidencia de migración." "No se cierra sin evidencia registrada."
- S6 (docs, L5): update only the architecture sentences about Cancel Raid / Host cannot Abandon; the migration-rule rewrite stays in FIX-52.

Constraints: `RaidClosureReason` is `[Networked]` byte — remove `HostCancellation = 1` without renumbering (`NaturalCompletion = 0`, `BootstrapFailure = 2`). Keep `BeginClosure` and `AbortRaidingParticipantsForClosure` (bootstrap failure). FIX-53 stays separate (L4).

## Tasks

- [x] T1 — S4: Host voluntary Abandon is resolved for handoff only after an explicit Return (`HostHandoffPolicy.IsHostExitResolved`, `AuthorizesReturnOnResolvedAbandon`, presenter host early-return at Abandon confirm). Route: inline. Commit: 71fe7864.
- [x] T2 — S1, S3: menu shows Abandon (with the standard two-step confirm) for the Host; Cancel Raid button, view API and presenter handler removed; prefab refs cleaned in the Editor. Route: inline. Commit: 71fe7864.
- [x] T3 — S2, S3: remove `TryCancelRaid`, `HostCancellation`, `ObserveHostCancellationClosure`, `ShouldArmHostCancellationReturn`, `_hostCancellationReturnArmed`; migrate tests. Route: inline. Commit: 71fe7864.
- [x] T4 — S6: architecture sentence updates. Route: inline. Commit: 71fe7864.
- [x] T5 — S5: suites green + multi-process evidence (Host + 1 Client Abandon; Host + 2 Clients handoff after Abandon; Solo Abandon). Route: user run + log read. Commit: n/a (evidence L10).

## Log

- L1 (user, verbatim): "ok vamos con fix-49 siguiendo con metodologia ODD."
- L2 (exploration, explore mwcf4qio-d-3jey): references at NetworkMatchController.cs:95-115, :445-449; SessionConnectionCoordinator.cs:87, :543, :604-653, :2244; RaidMenuPresenter.cs:298, :320, :383-389, :500-508, :540-548; RaidMenuView.cs (Cancel button API); LocalGameplayHud.prefab:2325-2326, RaidMenuPanel.prefab:1711. Today a Duo Host Abandon hands off immediately (no Results) because `AuthorizesReturnOnResolvedAbandon` sets `IsReturnAuthorized` on abandon. Solo Abandon closes via NaturalCompletion and Return unlocks at Finished (not live-verified).
- L3 (user decision): Host Abandon "Igual que un cliente (Recomendado)" — Host sees Results; handoff starts when the Host presses Volver.
- L4 (user decision): "FIX-53 aparte (Recomendado)".
- L5 (user decision): docs "Solo las frases de cancelación (Recomendado)".
- L6: live Game Design (`grimhold-docs`) timed out (30 s) on search "Abandono voluntario"; S4 rests on the HacknPlan scope text and L3.
- L7 (incident): first worker mwcfg27z-e-gy74 cancelled ("parent session shut down") after partial T1 edits; resumed as mwcft2ou-1-7bmr.
- L8 (implementation, worker mwcft2ou-1-7bmr): T1 `HostHandoffPolicy.CanAuthorizeReturn(requesterIsHost, cause, isDurablyCommitted)` replaces `IsHostExitResolved`/`AuthorizesReturnOnResolvedAbandon`; Abandon no longer authorizes Return; `IsLocalHostExitResolved` = `IsReturnAuthorized`; `ClassifyHistoricHost` keeps Aborted+abandon as DepartedTerminal; presenter Host early-return at Abandon confirm removed. T2 Cancel Raid removed from view/presenter; prefabs edited through the Editor (PrefabUtility): RaidMenuPanel +1/-279 (button), LocalGameplayHud +74/-139 (2 stale refs + stripped objects; rest TMP re-serialization noise `m_ActiveFontFeatures`, `m_MaxWidth/Height`). T3 `TryCancelRaid`, `HostCancellation` (byte 1 reserved, 0/2 kept), coordinator cancellation return removed; grep `HostCancellation|TryCancelRaid|CancelRaid` in Assets/Scripts empty. T4 cancellation sentences updated in 5 architecture docs. RED: only compile-level RED observed (missing `CanAuthorizeReturn`); behavioral RED of `AbandonWithoutReturnAuthorization_IsDepartedTerminal` NOT observed (reasoned only); removals have no RED. GREEN (EditMode): HostHandoffPolicyTests 28/28, HostHandoffServiceTests 8/8, HostMigrationRosterPolicyTests 8/8, RaidResultsReturnPolicyTests 20/20, RaidMenuPresenterTests 44/44, SessionConnectionStateMachineTests 45/45, RaidResultsLifecycleSourceTests 8/8, RaidDefeatLifecycleSourceTests 5/5, RaidClosureStateTests 2/2, SessionCompositionConfigurationTests 22/22, RaidConnectivityContinuityTests 15/15, VoluntaryHostHandoffProbeTests 6/6; PlayMode RaidMainHudPlayModeTests 8/8 (`--async-tests`). Not run: CharcoalUiMigrationTests (manifest still lists CancelRaid).
- L9 (regression, parent): CharcoalUiMigrationTests 5/6 — `EveryReviewedOwnerImageUsesItsAssignedSpriteOrPreservesItsExcludedSprite`: `Assets/Prefabs/UI/PlayerUI/RaidMenuPanel.prefab:578839607776989853 Expected: True But was: False` (manifest entry of the removed CancelRaidButton image). Removed that manifest entry (Assets/Editor/CharcoalUiMigrationManifest.json, -35 lines, JSON valid); rerun 6/6.
- L10 (user, verbatim): "perfecto funciona. Hace el commit" — user-reported live result for the T5 runs; Editor logs for these runs were not read by the agent.
