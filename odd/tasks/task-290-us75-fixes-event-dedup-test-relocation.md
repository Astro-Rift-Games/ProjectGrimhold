# TASK-290 — Reparación de fallos de US-75, deduplicación de eventos y reubicación de tests en Editor

**Objective:**
Reparar los fallos de ciclo de vida en `TownMissionGuildPresenter` (previniendo `MissingReferenceException` tras destrucción de vista), implementar deduplicación autoritativa e idempotente de eventos de misión (`GEN-02`), configurar formalmente el `ZoneId` de `CON_E_002` (`Catacombs_Main`), resincronizar recursos de recolección en incursión (`CON_E_005`) al transferir desde contenedores, proteger el depurador de PlayMode contra compilación en Release, y corregir la errata en la notificación de contrato completado.

**Baseline:** `New-Testing`, branch `feat/task-290-us75-fixes-event-dedup-test-relocation`.

**Sources:**
- HacknPlan: `TASK-290` (`#797`) (Sprint 17).
- Design Docs: 12 — Sistema de Misiones (§6 Familias de objetivos, §7 Persistencia del progreso, §9 Ciclo de vida y reseteo, §16 Integridad y Casos Borde), Project_Grimhold_Contratos_Base_v1.md.

**Acceptance Criteria (quoted from `#797`):**
- [x] La suite `TownMissionGuildPresenterTests` ejecuta y aprueba los tests sin lanzar `MissingReferenceException` gracias a la correcta desuscripción y validación de ciclo de vida del presenter.
- [x] La suite `RankEResourceRulesTests` ejecuta y aprueba E05_01, MAT_01 y MAT_02 sin errores de `InvalidInventory` en el flujo de producción.
- [x] Los eventos de progreso de misiones duplicados son descartados autoritativamente de forma idempotente sin incrementar contadores espurios.
- [x] El contrato `CON_E_002` cuenta con su `ZoneId` formal de Level Design asignado (`Catacombs_Main`) y filtra adecuadamente los eventos ocurridos fuera de él.
- [x] `MissionPlayModeDebugger` no se compila ni ejecuta en builds de Release (no development).
- [x] La notificación emergente de contrato completado muestra el nombre canónico "Guild de Misiones".
- [x] Los tests de EditMode residen bajo `Assets/Tests/EditMode/Editor/` compilando en `Assembly-CSharp-Editor`, sin dependencias de `UnityEditor` contaminando el runtime del juego.

**Work Breakdown:**
- [x] Phase 1: Test-Driven Development (TDD) — Escribir tests para reproducción y verificación (RED)
  - Test en `TownMissionGuildPresenterTests`: verificar que tras la destrucción de la vista, invocar `ClosePanel()`, `Unbind()` o cambiar pestañas no lance `MissingReferenceException`.
  - Test en `MissionDeduplicationTests`: verificar descarte idempotente de eventos duplicados con mismo `EventId`.
  - Test de validación de `ZoneId` en `CON_E_002` confirmando el filtrado ante eventos fuera de `Catacombs_Main`.
- [x] Phase 2: Correcciones del Presenter y Errata
  - Saneamiento de ciclo de vida en `TownMissionGuildPresenter.cs`: sustituir accesos `_view?.` por chequeos explícitos `if (_view != null)` y desuscribir eventos antes de destruir.
  - Corrección de errata en `RaidMissionTrackerPresenter.cs`: cambiar "Guild de Incursiones" por "Guild de Misiones".
  - Envoltura condicional en `MissionPlayModeDebugger.cs` con `#if UNITY_EDITOR || DEVELOPMENT_BUILD`.
- [x] Phase 3: Deduplicación Autoritativa y Sincronización en Incursión
  - Adición de `EventId` en `MissionContributionEvent` y generación/propagación en `PlayerMissionContributionLedger`.
  - Deduplicación idempotente en `LocalProfileStore.TryApplyMissionProgress`.
  - Notificación de `ObjectiveFamily.Obtencion` en `PlayerLootTransferNetworkController` al retirar recursos de contenedores en incursión.
  - Asignación de `ZoneId: Catacombs_Main` en `Mission_RankE_02.asset`.
- [x] Phase 4: Validación y Verificación Completa
  - Compilación limpia de la solución C# (`Assembly-CSharp` y `Assembly-CSharp-Editor` sin errores).
  - Ejecución de la suite completa de tests de Misiones en Unity Editor: 69 / 69 tests aprobados (100% PASS, 0 FAIL).
- [x] Phase 5: Corrección de Monotonicidad de Recibos en Extracción tras Progresión de Misiones
  - Reproducción por TDD: `TryCommitExtraction_WhenReceiptSequenceIsLowerThanLocalWatermark_PreservesMonotonicityAndSucceeds` en `LocalProfilePersistenceEditModeTests.cs`.
  - Salvaguarda de secuencia en `LocalProfileStore.TryCommitExtraction`: `effectiveProgressionSequence = Math.Max(receipt.ResultSequence, current.LastAppliedProgressionResultSequence + 1)`.
  - Propagación de watermark local en admisión y spawn de incursión (`FusionSessionLauncher.cs` y `NetworkSpawnManager.cs`).
  - Verificación en suite EditMode y backend (609 tests backend aprobados, 0 fallos).
- [x] Phase 6: Protección de Ciclo de Vida en DungeonPressure Controller & Presenters (Transición Raid -> Town)
  - Diagnóstico: Durante la descarga de la escena de incursión tras extraer, `DungeonPressureAmbientPresenter.Update()` continuaba ejecutándose mientras `DungeonPressureController` ya había sido despawneado, lanzando `InvalidOperationException` al acceder a la propiedad replicada `Phase`.
  - Implementación: Se agregaron salvaguardas autoritativas `Object != null && Object.IsValid` en `DungeonPressureController.cs` (`HasReadableState`, `TryGetPhase`, `RemainingSeconds`, `InstabilityThreshold`, `MinIntervalSeconds`).
  - Saneamiento en Presenters: `DungeonPressureAmbientPresenter.cs` y `DungeonPressureAudioPresenter.cs` validan `HasValidController` antes de acceder a propiedades `[Networked]`.
  - Tests unitarios: Añadidos `DungeonPressureControllerLifecycleTests.cs` y test en `DungeonPressureAmbientPresenterTests.cs`.
  - Verificación: 0 errores de compilación, 609 tests backend aprobados.
