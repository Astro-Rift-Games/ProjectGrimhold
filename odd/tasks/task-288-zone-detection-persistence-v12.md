# ODD Task Tracker: TASK-288 — Detección estática en zonas, revalidación al reclamar y migración de persistencia v11 a v12 con Expedición y Origen

- **Work Item:** HacknPlan #795 (TASK-288)
- **Parent:** US-75 (Sistema de Misiones — Rango E)
- **Branch:** `feat/task-288-zone-detection-persistence-v12`
- **Design Sources:**
  - GD Doc 12: Sistema de Misiones (§6, §7, §9, §14)
  - GD Doc 14: Objetos, Recursos y Consumibles (§6.1, §6.2)
  - `Project Grimhold/Docs/Architecture/LocalPlayerPersistenceArchitecture.md`

## Acceptance Criteria (from HacknPlan #795)

1. **Static Zone Detection:** Un jugador presente dentro del volumen de `RaidZoneTrigger` al iniciar o recibir el contrato recibe el crédito de exploración sin requerir salir y reingresar.
2. **Revalidation on Claim:** El reclamo de contratos basados en expedición falla si los ítems fueron extraídos en expediciones distintas o si las unidades ya no se encuentran en el inventario/alijo.
3. **Save Schema Migration (v11 -> v12):** Snapshots guardados bajo el esquema v11 se deserializan y migran limpiamente al esquema v12 sin corrupción ni pérdida de inventario. `CurrentSchemaVersion` pasa a 12, `StashItem` incluye `ExpeditionId` y `Origin` con valores por defecto no nulos.
4. **Canonical Stacking Rule (Doc 14 §6.1):** Stacks de recursos con distinto `Origin` o `ExpeditionId` no se mezclan automáticamente ni permiten merge manual, preservando identidades independientes en slots separados. Si se intenta combinar stacks incompatibles, la operación rechaza la fusión y genera un nuevo slot según la capacidad disponible.

## Work Units (TDD Cycle)

- [x] S1: Save Schema Migration v11 -> v12 & StashItem fields (RED -> GREEN -> REFACTOR)
  - Incremented `LocalProfileSnapshot.CurrentSchemaVersion` to 12.
  - Added `ExpeditionId` and `Origin` to `StashItem` with non-null string defaults.
  - Updated `LocalProfileSaveCodec` (`ItemData`, `ToItems`, `TryReadItems`, `ExpandLegacyEquipment`) for seamless backward compatibility.
  - Verified tests: `Codec_MigratesVersionElevenToTwelve_PreservingInventoryAndAssigningDefaults` and `Codec_EncodesAndDecodesVersionTwelve_PreservingExpeditionIdAndOrigin`.
- [x] S2: Canonical Stacking Separation by Origin and ExpeditionId in ProfileInventoryRules (RED -> GREEN -> REFACTOR)
  - Implemented `ProfileInventoryRules.CanMerge` enforcing identity separation per Doc 14 §6.1.
  - Updated `TryMerge`, `TryMove`, `ExceedsCapacity`, `CountNewSlots`, `TryRemove`, `FindAmount`. Incompatible stacks are rejected for merge and placed into separate slots, or swapped on move.
  - Preserved metadata during transfers and splits in `LocalProfileStore`.
  - Verified tests in `ProfileInventoryRulesTests`.
- [x] S3: Revalidation on Claim for Expedition Contracts in LocalProfileStore (RED -> GREEN -> REFACTOR)
  - Revalidation in `CanClaimMission` for `ObjectiveFamily.ExtraccionDeObjetos` verifying required units exist across Loadout/Stash and share the contract's `ExpeditionId`.
  - Rejection with clear messages if extracted in different expeditions, missing, or located in Stash.
  - Deduction in `TryClaimMission` consuming matching expedition units atomically.
  - Stamping of `ExpeditionId` and `Origin` on extracted items in `TryCommitExtraction` and `ExtractionPayloadBuilder`.
  - Verified tests in `RankEContractsAndGuildReputationTests.LocalProfileStore_TryClaimMission_RevalidatesSameExpeditionAndPossession`.
- [x] S4: Static and Spatial Zone Detection in RaidZoneTrigger (RED -> GREEN -> REFACTOR)
  - Implemented non-allocating spatial check `IsPlayerInside`, `TryTriggerExplorationIfInside`, `EvaluateOverlappingPlayers`, and static registry `EvaluateAllForPlayer`.
  - Added evaluation on `Start()` and `FixedUpdate()` in `RaidZoneTrigger`, plus trigger on `PlayerCharacter.Spawned()`.
  - Verified tests in `RaidMissionEventProducersTests.RaidZoneTrigger_StaticSpatialDetection_DetectsPlayerAlreadyInside`.
- [x] S5: Full Integration & Regression Validation
  - All 609 backend unit & integration tests passing.
  - All relevant EditMode test suites passing cleanly with zero errors.
