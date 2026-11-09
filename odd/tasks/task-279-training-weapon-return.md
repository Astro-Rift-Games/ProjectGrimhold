# TASK-279 — Retorno Automático en Pueblo y Exclusión de Cadáver para Armas de Entrenamiento

Execution tracker for HacknPlan TASK-279 (#777), parent US-79 (#774). HacknPlan owns scope; this file records execution and evidence only.
Branch: `New-Testing`. Design: Doc 09 §15.2 ("Persistencia de la contingencia"), Doc 07 (Conversión a Cadáver), Doc 02 (Liquidación de Resultados de Raid).

## Specs

- S1 (scope 1): "Al procesar el retorno a Pueblo, si el jugador posee al menos 1 arma normal válida en su inventario, equipamiento o alijo, retirar el arma de entrenamiento. Si no posee ninguna arma normal, conservarla para no dejarlo indefenso."
  - Owner decision (L2): implemented in the authoritative backend extraction settlement (`ExtractionCommitService`) plus the same rule in the Unity local mirror (`LocalProfileStore.TryCommitExtraction`), not in `RaidResultsLootSummaryBuilder` (which only projects the Results summary and writes no inventory).
  - Owner decision (L2): "arma normal válida" = any item of category Weapon that is not a training weapon, in loadout/inventory, prepared equipment or stash, regardless of attribute requirements.
- S2 (scope 2): "Omitir cualquier item con IsTrainingWeapon = true de la transferencia de loot al cadáver ante Derrota Definitiva o Abandono Voluntario (conceptualmente se pierde o retorna al Entrenador)."
- S3 (AC 1): "Al volver a Pueblo tras extraer con un arma normal en el botín, el arma de entrenamiento se elimina automáticamente del inventario."
- S4 (AC 2): "Si el jugador extrae sin conseguir armas normales, conserva el arma de entrenamiento."
- S5 (AC 3): "Al morir definitivamente en Mazmorra, el cadáver no contiene el arma de entrenamiento (no se puede lootear por enemigos ni compañeros)."
- S6 (validation): "Tests automatizados simulando: 1) retorno con arma normal (se retira), 2) retorno sin armas normales (se conserva), y 3) generación de cadáver (se omite del loot transferido)."
- Design (Doc 09 §15.2, verbatim): "Al regresar a Pueblo, si el jugador posee al menos un arma normal válida, la arma de entrenamiento se retira automáticamente y retorna conceptualmente al Entrenador." / "Si el jugador no posee ninguna arma normal válida, puede conservar la arma de entrenamiento." / "Ante Derrota definitiva o Abandono voluntario no se genera como Loot de cadáver: retorna conceptualmente al sistema de entrenamiento."
- Out of scope (verbatim): "Drops manuales en el suelo de Mazmorra (bloqueado por CanBeDiscarded = false)." / "Reembolsos o compensaciones en Oro."

## Tasks

- [x] T1 — S1, S3, S4, S6: Backend extraction commit removes training weapons from loadout, prepared equipment and stash when a normal weapon exists across them; keeps them otherwise. Route: inline (worker failed: quota). Commit: — (not authorized yet)
  - Evidence: `backend/src/services/TrainingWeapons.js` (`returnTrainingWeapons`, shared `isTrainingWeapon`), applied in `ExtractionCommitService.commit`; stash written only when changed. `tests/extraction.training-return.test.js`: RED 3 failing as expected, GREEN 5/5; `npm test` 586/586; `npm run test:integration` 200/200. Legacy `InventoryService.commitExtraction` route is gated by `enableLegacyExtraction` (disabled) and left unchanged.
- [x] T2 — S1, S3, S4, S6: Unity `LocalProfileStore.TryCommitExtraction` applies the same rule to the local cache. Route: inline (worker failed: quota). Commit: — (not authorized yet)
  - Evidence: `Scripts/Stash/TrainingWeaponReturnRule.cs`; `PendingExtractionCommit` keeps the exact raid result. `TrainingWeaponExtractionReturnTests`: RED 2 behavioural failures, GREEN 4/4; Stash suites PostExtraction 9/9, RaidReservationLifecycle 7/7, StashReservationInvariant 23/23, PhysicalProfileSlot 19/19, StashCorruptLogin 32/32, filter TrainingWeapon 32/32.
- [x] T3 — S2, S5, S6: Corpse generation omits training weapons from the container load while still clearing them from the player. Route: inline (worker failed: quota). Commit: — (not authorized yet)
  - Evidence: PlayMode filter "Corpse" after wiring: new test passed, 6 passed / 2 failed (both pre-existing, below).
  - Earlier evidence: `Scripts/Player/CorpseLootProjection.cs` + `CorpseLootProjectionTests` 3/3. PlayMode `TrainingWeapon_IsClearedFromThePlayerButNeverLoadedIntoTheCorpse` RED ("Expected: 0 But was: 1"). Voluntary abandon (`NetworkRaidParticipant.TryAdvanceVoluntaryAbandon`) and definitive defeat (`PlayerCharacter.HandleDeath`) share `TryConvertInventoryToCorpseLoot`. Pre-existing unrelated failure: `MultipleStacks_AreLoadedThenThePlayerInventoryIsClearedOnce` fails at the prefab "Body" child lookup (line 98) before corpse logic.

## Log

- L1 (user request, verbatim): "implementa TASK-279 — Retorno Automático en Pueblo y Exclusión de Cadáver para Armas de Entrenamiento siguiendo ODD."
- L2 (owner decisions): Retiro point → "Backend + espejo local (Recomendado)". Valid weapon → "Cualquier arma normal (Recomendado)".
- L5 (verification): gentle-ai-verify (Risk item 1) verdict S1–S6 pass, no blockers; backend 586/586 and 200/200 plus 6 probes against in-memory Mongo (revision conflict leaves stash intact and releases the claim; replay no-op; v2/v3 shapes). Minor findings: local stash may briefly show a removed training weapon until the backend acknowledgement reconciles; Results lost-loot summary still lists the training weapon (display, out of scope); legacy `training_sword` alias maps to `arming_sword` (pre-existing).
- L6 (evidence): full EditMode suite 6015 total, 5958 passed, 57 failed. Every failing test is in a fixture outside this diff (sprite identity, Doc 14 item config, prefab word budgets, `NetworkRaidParticipant` source assertions, shield/attack presentation); none in Stash, corpse or training-weapon suites. No baseline run was made to prove they predate the task. Commits not made: repository rules require explicit authorization.
- L4 (incident): both background workers failed with a provider quota error before editing; work continued inline, not retried.
- L3 (evidence): Backend `ExtractionCommitService.commit` rebuilds `inventory.loadout` and `inventory.preparedEquipment` from the authoritative raid result (`backend/src/services/ExtractionCommitService.js:116-123,196-197`), so a client-only removal would be reverted by the backend refresh. Corpse path: `PlayerCorpseGenerationController.TryConvertInventoryToCorpseLoot` loads `PlayerExpeditionLootSnapshot.Combined` into the container and verifies it with exact-match checks (`HasExactContent`, `MatchesCurrent`) before `TryClearExact`; the filter must keep load and verification consistent and still clear the training weapon from the player.
