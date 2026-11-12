# ODD Task Tracker: TASK-291 — Cooldown de utilidades por instancia en contenedores y ciclo de pasivas al extraer

- **Work Item:** HacknPlan #798 (TASK-291)
- **Parent:** US-76 (Pasivas y Cooldowns de Utilidades)
- **Branch:** `feat/task-291-utility-cooldown-container-passives`
- **Design Sources:**
  - GD Doc 14: Objetos, Recursos y Consumibles (§6.3, §13)
  - GD Doc 06: Sistema de Extracción (§8)
  - `Project Grimhold/Docs/Architecture/LootInteractionArchitecture.md`

## Acceptance Criteria (from HacknPlan #798)

1. **Cooldown Persistence across Container Transfers:** Al transferir una Brújula Rúnica en cooldown hacia un contenedor y devolverla al inventario, el tiempo restante de cooldown se conserva intacto y no se reinicia.
2. **Network Word Budget Compliance:** La serialización del estado de cooldowns en contenedores de red no supera el presupuesto de palabras de red asignado al contenedor en LootInteractionArchitecture.md (1230 palabras máximo).
3. **Passive Deactivation on Extraction:** Al extraer de una incursión, las utilidades pasivas dejan de aplicar sus bonificaciones inmediatamente en el cliente y en el perfil persistente.
4. **MaxTransportable Enforcement on Containers:** No es posible eludir MaxTransportable acumulando múltiples instancias de utilidades restringidas mediante transferencias de contenedores.

## Work Units (TDD Cycle)

- [x] S1: Immediate Passive Deactivation upon Extraction in PlayerInventoryPassiveEffectsController (RED -> GREEN -> REFACTOR)
  - Added extraction awareness (`_extractionController`, `PlayerCharacter.IsExtracted`, `IsOwnerExtracted`, and `ExtractedOverrideForTesting`).
  - Implemented event-driven immediate cleanup via `_extractionController.ExtractionCompleted` and polling in `UpdatePassivesIfNeeded()`.
  - Cleared all passive treasure states (`_hasChaosCoin`, `_hasPilgrimLight`, `_hasSanctuaryHeart`, `_hasVoidBell`, `_hasFamineIdol`, `_hasWandererEye`), stopped pulse VFX and reset all multipliers upon extraction.
  - Verified tests: `PassiveTreasures_DeactivateImmediately_UponExtraction` and `PlayerExtractionController_CompleteExtraction_DeactivatesPassiveEffectsImmediately`.
- [x] S2: MaxTransportable Validation in NetworkLootContainer and LootInventoryRules (RED -> GREEN -> REFACTOR)
  - Updated `LootInventoryRules.ValidateReceive` to evaluate `maxTransportable > 0 && requestedAmount > maxTransportable - currentAmount` returning `LootTransferFailureReason.MaxTransportableExceeded`.
  - Updated `NetworkLootContainer.ValidateReceive` to pass `definition.MaxTransportable` to `LootInventoryRules.ValidateReceive`.
  - Added unit test cases for `LootInventoryRules.ValidateReceive` with `maxTransportable`.
  - Added test `LootTransferTransaction_RejectsDepositOrWithdrawal_WhenDestinationExceedsMaxTransportable` asserting rejection of hoarding or duplicate withdrawal.
- [x] S3: Cooldown Persistence Verification and Network Budget Compliance (RED -> GREEN -> REFACTOR)
  - Verified container cooldown retention via `LootTransferTransaction` and `NetworkLootContainer` (`RuneCompassCooldown`, `ArcaneDetectorCooldown`).
  - Verified word count compliance in `RaidLootOriginWordCountTests`: `LootContainer.prefab` passes within the 1230 woven-word budget ceiling.
- [x] S4: Full Test Suite Verification & Clean Architecture Check
  - EditMode suites: `InventoryPassiveEffectsTests` (12/12 PASS), `LootInventoryRulesTests` (40/40 PASS), `UtilityExpeditionRulesTests` (7/7 PASS), `RaidLootOriginWordCountTests` (6/6 PASS), `LocalProfilePersistenceEditModeTests` (35/35 PASS).
  - Backend suite: 609/609 PASS (100%).
