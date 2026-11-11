# TASK-287 — Reducción de huella de red en NetworkEnemy, aislamiento de spawner de test y contratos RPC

**Objective:**
Reducir la serialización de red de NetworkEnemy.prefab para cumplir el presupuesto oficial máximo de 1291 palabras, aislar las herramientas de testing en tiempo de ejecución para evitar trampas en builds de producción y reparar los fallos de contrato RPC en consumibles.

**Baseline:** `New-Testing`, branch `feat/task-287-networkenemy-footprint-spawner`.

**Sources:**
- HacknPlan: `TASK-287` (`#794`) (Sprint 17).
- Design Docs: Docs/Architecture/LootInteractionArchitecture.md, ItemInstanceArchitecture.md, 07 — Sistema de Loot (§9), Project_Grimhold_Contratos_Base_v1.md.

**Acceptance Criteria (quoted from `#794`):**
- [x] La suite RaidLootOriginWordCountTests pasa al 100% en verde con NetworkEnemy.prefab ocupando <= 1291 palabras de red.
- [x] RaidLootTestSpawner no existe ni puede ser ejecutado mediante F8 en builds de producción.
- [x] Los 3 tests de RpcAcceptanceContractTests sobre PlayerConsumableNetworkController pasan en verde sin errores.

**Work Breakdown:**
- [x] Phase 1: Test-Driven Development (TDD) — Reproducción de fallos y asserts (RED)
  - Word count: 1292 > 1291 en `NetworkEnemy.prefab`.
  - RPC Acceptance: 3 tests fallidos en `RpcAcceptanceContractTests` por ausencia de `WasAccepted` en `PlayerConsumableNetworkController`.
  - Seguridad/Aislamiento: test estructural de ausencia de `RaidLootTestSpawner` en builds no-development.
- [x] Phase 2: Reparación de contratos RPC en `PlayerConsumableNetworkController`
  - Implementado método estático privado `WasAccepted(in RpcInvokeInfo invokeInfo, bool hasStateAuthority)` y utilizado en la validación del despacho de RPC en `TryRequestConsume`.
- [x] Phase 3: Reducción de huella de red en `NetworkEnemy.prefab`
  - Optimizado `EnemyMovementAIController.ScanTimer`: des-networkeado el timer de escaneo interno de la autoridad de estado convirtiéndolo en campo privado no replicado (`_scanTimer`), reduciendo 1 palabra de red (18 -> 17) para llevar el total de `NetworkEnemy.prefab` de 1292 a exactamente 1291 palabras.
- [x] Phase 4: Aislamiento condicional de `RaidLootTestSpawner`
  - Condicionado `RaidLootTestSpawner.cs` bajo `#if UNITY_EDITOR || DEVELOPMENT_BUILD`.
  - Implementado procesador de escena de build `RaidLootTestSpawnerBuildProcessor.cs` para limpiar `[LootTestSpawner]` en builds de Release / no-development.
  - Implementada suite de tests de seguridad `RaidLootTestSpawnerSecurityTests.cs`.
- [x] Phase 5: Validación completa y verificación
  - Ejecución de `RaidLootOriginWordCountTests` (6/6 tests PASS, NetworkEnemy = 1291 palabras).
  - Ejecución de `RpcAcceptanceContractTests` (15/15 tests PASS en los 5 controladores).
  - Ejecución de `RaidLootTestSpawnerSecurityTests` (4/4 tests PASS).
  - Compilación C# limpia de `Assembly-CSharp.csproj` y `Assembly-CSharp-Editor.csproj` (0 Errores).
