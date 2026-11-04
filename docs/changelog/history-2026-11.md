# 2026-11 — Unreleased history

[Archive index](README.md)

Release: Unreleased

34 original commits; 34 canonical entries.

## Canonical classification

19 features; 10 bug fixes; 3 documentation; 1 maintenance; 1 other changes

| System | Features | Fixes | Refactors / perf | Infrastructure | Secondary | Reverts / other |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Abilities | 2 | 6 | 0 | 0 | 1 | 1 |
| Art & Animation | 6 | 4 | 0 | 0 | 1 | 0 |
| Audio | 5 | 0 | 0 | 0 | 0 | 0 |
| Build & Tooling | 14 | 6 | 0 | 0 | 1 | 1 |
| Combat | 8 | 0 | 0 | 0 | 0 | 0 |
| Documentation | 10 | 6 | 0 | 0 | 1 | 0 |
| Extraction | 2 | 0 | 0 | 0 | 1 | 0 |
| Input | 1 | 0 | 0 | 0 | 0 | 0 |
| Inventory & Loot | 12 | 10 | 0 | 0 | 1 | 0 |
| Movement | 3 | 0 | 0 | 0 | 0 | 0 |
| Networking & Sessions | 4 | 1 | 0 | 0 | 0 | 0 |
| Persistence & Backend | 4 | 0 | 0 | 0 | 1 | 0 |
| Tests | 18 | 9 | 0 | 0 | 1 | 0 |
| UI | 10 | 3 | 0 | 0 | 2 | 0 |
| Unassigned | 0 | 0 | 0 | 0 | 1 | 0 |
| World & Visibility | 7 | 4 | 0 | 0 | 0 | 0 |

## Original commit records

### b4dfeabab36f58accbd4eb9b5efe8e5b686cf65c

[b4dfeabab36f58accbd4eb9b5efe8e5b686cf65c](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/b4dfeabab36f58accbd4eb9b5efe8e5b686cf65c)

Date: 2026-11-04T01:27:42-03:00 · Classification: Documentation (explicit) · Systems: Extraction, Inventory & Loot, UI, Persistence & Backend, Documentation

Flags: secondary

#### Original subject

```text
docs(inventory): document physical slots and TASK-281 split
```

#### Original body

```text
Record the approved Town/Stash physical-slot model and the TASK-281
split as implemented.

- LocalPlayerPersistenceArchitecture: slot model replaces the TASK-218
  compact exception; codec schema 10, inventorySchemaVersion, protocol
  gate, migration and rollback floors; Town StackMax stays deferred;
  Town Origin under D2 = O1 pending Game Design doc 07 reconciliation.
- RaidInventoryUIArchitecture: slot projection, Stash slot-range paging,
  Loadout fixed at 30 visible slots without paging.
- LootInteractionArchitecture: Raid and Town split contracts; UI stays
  in TASK-282.
- ExtractionArchitecture: extraction placement into Loadout slots.
- docs/proposal_task281_town_stash_physical_slots.md (approved rev 4),
  docs/defect_raid_loot_eligibility_other_team.md (open GD conflict) and
  the ODD tracker odd/tasks/stash-physical-slots.md.
```

#### Changed paths

```text
Project Grimhold/Docs/Architecture/ExtractionArchitecture.md
Project Grimhold/Docs/Architecture/LocalPlayerPersistenceArchitecture.md
Project Grimhold/Docs/Architecture/LootInteractionArchitecture.md
Project Grimhold/Docs/Architecture/RaidInventoryUIArchitecture.md
docs/defect_raid_loot_eligibility_other_team.md
docs/proposal_task281_town_stash_physical_slots.md
odd/tasks/stash-physical-slots.md
```

#### Classification evidence

- type → Documentation: docs
- path → Extraction: Project Grimhold/Docs/Architecture/ExtractionArchitecture.md
- message-term → Extraction: extraction
- path → Inventory &amp; Loot: Project Grimhold/Docs/Architecture/LootInteractionArchitecture.md
- path → Inventory &amp; Loot: Project Grimhold/Docs/Architecture/RaidInventoryUIArchitecture.md
- path → Inventory &amp; Loot: docs/defect\_raid\_loot\_eligibility\_other\_team.md
- scope → Inventory &amp; Loot: inventory
- message-term → Inventory &amp; Loot: inventory
- message-term → UI: UI
- path → Persistence &amp; Backend: Project Grimhold/Docs/Architecture/LocalPlayerPersistenceArchitecture.md
- path → Documentation: Project Grimhold/Docs/Architecture/ExtractionArchitecture.md
- path → Documentation: Project Grimhold/Docs/Architecture/LocalPlayerPersistenceArchitecture.md
- path → Documentation: Project Grimhold/Docs/Architecture/LootInteractionArchitecture.md
- path → Documentation: Project Grimhold/Docs/Architecture/RaidInventoryUIArchitecture.md
- path → Documentation: docs/defect\_raid\_loot\_eligibility\_other\_team.md
- path → Documentation: docs/proposal\_task281\_town\_stash\_physical\_slots.md

#### Diagnostics

- No classification ambiguity detected.

### fc872660c13a0c839d39c3edeab09c9d5ca85a00

[fc872660c13a0c839d39c3edeab09c9d5ca85a00](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/fc872660c13a0c839d39c3edeab09c9d5ca85a00)

Date: 2026-11-04T01:27:32-03:00 · Classification: Features (explicit) · Systems: Combat, Inventory & Loot, Networking & Sessions, UI, Persistence & Backend, Build & Tooling, Tests

Flags: primary or unclassified

#### Original subject

```text
feat(stash): adopt physical slots in Town inventory and Stash
```

#### Original body

```text
Move the Unity Town Loadout and Stash to the physical-slot model
served by the backend (D1, D3) and add the authoritative Stash split
for TASK-281 without UI.

- StashItem.Slot; save codec schema 10 persists slots, duplicate
  lootIds and holes, plus the backend inventorySchemaVersion. Schemas
  1-9 decode losslessly with slot = list index.
- Hydration no longer merges entries; every backend request sends
  X-Grimhold-Inventory-Protocol: 2. With a v1 backend document the
  coordinator reports every mutation as unavailable before any remote
  call.
- Slot rules in ProfileInventoryRules, LocalProfileStore and
  MerchantTradePreview: capacity by occupied slots, overflow
  reducing-only, receive into the lowest matching then lowest free
  slot, removal leaves a hole, consumption in ascending slot order,
  Quick Slot counts sum every stack, exact reservation rollback.
  A pending reservation is still reported before input errors.
- Slot-addressed transfer, sell, Sort and slot move through
  RemoteInventoryService and StashTransferCoordinator.
- LocalProfileStore.TrySplit and StashTransferCoordinator.SplitAsync
  (remote first, serialized queue, reconcile without retry).
- Raid admission aggregates reservation stacks by lootId
  (RaidAdmissionData, FusionSessionLauncher); its duplicate-ID and
  entry limits are unchanged.
- Lobby renders each entry at its slot, Stash pages by slot range,
  the Loadout keeps its 30 visible cells and cell drags use slot moves.

Tests: PhysicalProfileSlotTests, StashSplitTests,
InventoryProtocolSlotTests, RaidAdmissionSlotAggregationTests and
slot cases in StashTransferCoordinatorTests, StashReservationInvariant
and LobbyStashPresenter PlayMode; existing Stash, Equipment, Commerce,
persistence and shop fixtures updated to explicit slots.
```

#### Changed paths

```text
Project Grimhold/Assets/Scripts/Backend/AuthenticationClient.cs
Project Grimhold/Assets/Scripts/Backend/CharacterClient.cs
Project Grimhold/Assets/Scripts/Backend/InventoryClient.cs
Project Grimhold/Assets/Scripts/Backend/InventoryDataTransferObjects.cs
Project Grimhold/Assets/Scripts/Backend/InventoryProtocol.cs
Project Grimhold/Assets/Scripts/Backend/InventoryProtocol.cs.meta
Project Grimhold/Assets/Scripts/Backend/InventoryRequestJson.cs
Project Grimhold/Assets/Scripts/Backend/InventoryRequestJson.cs.meta
Project Grimhold/Assets/Scripts/Backend/InventorySlotMoveRequest.cs
Project Grimhold/Assets/Scripts/Backend/InventorySlotMoveRequest.cs.meta
Project Grimhold/Assets/Scripts/Backend/InventorySlotOrderRequest.cs
Project Grimhold/Assets/Scripts/Backend/InventorySlotOrderRequest.cs.meta
Project Grimhold/Assets/Scripts/Backend/InventorySlotSplitRequest.cs
Project Grimhold/Assets/Scripts/Backend/InventorySlotSplitRequest.cs.meta
Project Grimhold/Assets/Scripts/Backend/ProgressionClient.cs
Project Grimhold/Assets/Scripts/Commerce/MerchantTradePreview.cs
Project Grimhold/Assets/Scripts/Networking/FusionSessionLauncher.cs
Project Grimhold/Assets/Scripts/Networking/RaidAdmissionData.cs
Project Grimhold/Assets/Scripts/Stash/ApplicationStashServiceBootstrapper.cs
Project Grimhold/Assets/Scripts/Stash/IPlayerLoadoutService.cs
Project Grimhold/Assets/Scripts/Stash/IPlayerStashService.cs
Project Grimhold/Assets/Scripts/Stash/IRemoteInventoryService.cs
Project Grimhold/Assets/Scripts/Stash/ISlotPlayerLoadoutService.cs
Project Grimhold/Assets/Scripts/Stash/ISlotPlayerLoadoutService.cs.meta
Project Grimhold/Assets/Scripts/Stash/ISlotPlayerStashService.cs
Project Grimhold/Assets/Scripts/Stash/ISlotPlayerStashService.cs.meta
Project Grimhold/Assets/Scripts/Stash/ISlotRemoteInventoryService.cs
Project Grimhold/Assets/Scripts/Stash/ISlotRemoteInventoryService.cs.meta
Project Grimhold/Assets/Scripts/Stash/InMemoryPlayerLoadoutService.cs
Project Grimhold/Assets/Scripts/Stash/InMemoryPlayerStashService.cs
Project Grimhold/Assets/Scripts/Stash/LobbyStashPresenter.cs
Project Grimhold/Assets/Scripts/Stash/LobbyStashUI.cs
Project Grimhold/Assets/Scripts/Stash/LocalLoadoutInventoryReadSource.cs
Project Grimhold/Assets/Scripts/Stash/LocalProfileSaveCodec.cs
Project Grimhold/Assets/Scripts/Stash/LocalProfileSnapshot.cs
Project Grimhold/Assets/Scripts/Stash/LocalProfileStore.cs
Project Grimhold/Assets/Scripts/Stash/ProfileInventoryRules.cs
Project Grimhold/Assets/Scripts/Stash/RemoteInventoryService.cs
Project Grimhold/Assets/Scripts/Stash/RemoteShopTransactionService.cs
Project Grimhold/Assets/Scripts/Stash/StashDisplayOrder.cs
Project Grimhold/Assets/Scripts/Stash/StashItem.cs
Project Grimhold/Assets/Scripts/Stash/StashTransferCoordinator.cs
Project Grimhold/Assets/Scripts/Stash/TownInventoryProjection.cs
Project Grimhold/Assets/Scripts/Stash/TownInventoryProjection.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Backend.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Backend/InventoryProtocolSlotTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Backend/InventoryProtocolSlotTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Commerce/MerchantTradePreviewTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Equipment/PreparedEquipmentTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Networking/RaidAdmissionSlotAggregationTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Networking/RaidAdmissionSlotAggregationTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Stash/PhysicalProfileSlotTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Stash/PhysicalProfileSlotTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Stash/PhysicalSlotTestSeed.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Stash/PhysicalSlotTestSeed.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Stash/ProfileInventoryRulesTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Stash/RaidReservationLifecycleTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Stash/RecoveryWeaponConsentTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Stash/RemoteShopTransactionServiceTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Stash/StashCapacityTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Stash/StashCorruptLoginRecoveryTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Stash/StashDisplayOrderTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Stash/StashHydrationCapacityTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Stash/StashReservationInvariantTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Stash/StashSplitTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Stash/StashSplitTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Stash/StashTransferCoordinatorTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Stash/StashTransferEvaluationTests.cs
Project Grimhold/Assets/Tests/EditMode/LocalProfilePersistenceEditModeTests.cs
Project Grimhold/Assets/Tests/EditMode/ShopTransactionPersistenceEditModeTests.cs
Project Grimhold/Assets/Tests/PlayMode/PlayModeMerchantComposition.cs
Project Grimhold/Assets/Tests/PlayMode/Presentation/LobbyStashPresenterPlayModeTests.cs
Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidInventoryPresenterInteractionTests.cs
```

#### Classification evidence

- type → Features: feat
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/RecoveryWeaponConsentTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Backend/InventoryClient.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Backend/InventoryDataTransferObjects.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Backend/InventoryProtocol.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Backend/InventoryProtocol.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Backend/InventoryRequestJson.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Backend/InventoryRequestJson.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Backend/InventorySlotMoveRequest.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Backend/InventorySlotMoveRequest.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Backend/InventorySlotOrderRequest.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Backend/InventorySlotOrderRequest.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Backend/InventorySlotSplitRequest.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Backend/InventorySlotSplitRequest.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Stash/IRemoteInventoryService.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Stash/ISlotRemoteInventoryService.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Stash/ISlotRemoteInventoryService.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Stash/LocalLoadoutInventoryReadSource.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Stash/ProfileInventoryRules.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Stash/RemoteInventoryService.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Stash/TownInventoryProjection.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Stash/TownInventoryProjection.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Backend/InventoryProtocolSlotTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Backend/InventoryProtocolSlotTests.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Equipment/PreparedEquipmentTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/ProfileInventoryRulesTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidInventoryPresenterInteractionTests.cs
- message-term → Inventory &amp; Loot: inventory
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/FusionSessionLauncher.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/RaidAdmissionData.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Stash/LobbyStashPresenter.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Stash/LobbyStashUI.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/RaidAdmissionSlotAggregationTests.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/RaidAdmissionSlotAggregationTests.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Tests/PlayMode/Presentation/LobbyStashPresenterPlayModeTests.cs
- message-term → Networking &amp; Sessions: Lobby
- path → UI: Project Grimhold/Assets/Scripts/Stash/LobbyStashUI.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Backend/AuthenticationClient.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Backend/CharacterClient.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Backend/InventoryClient.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Backend/InventoryDataTransferObjects.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Backend/InventoryProtocol.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Backend/InventoryProtocol.cs.meta
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Backend/InventoryRequestJson.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Backend/InventoryRequestJson.cs.meta
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Backend/InventorySlotMoveRequest.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Backend/InventorySlotMoveRequest.cs.meta
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Backend/InventorySlotOrderRequest.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Backend/InventorySlotOrderRequest.cs.meta
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Backend/InventorySlotSplitRequest.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Backend/InventorySlotSplitRequest.cs.meta
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Backend/ProgressionClient.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Stash/LocalProfileSaveCodec.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Stash/LocalProfileSnapshot.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Stash/LocalProfileStore.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Stash/ProfileInventoryRules.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Tests/EditMode/Editor/Backend.meta
- path → Persistence &amp; Backend: Project Grimhold/Assets/Tests/EditMode/Editor/Backend/InventoryProtocolSlotTests.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Tests/EditMode/Editor/Backend/InventoryProtocolSlotTests.cs.meta
- path → Persistence &amp; Backend: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/PhysicalProfileSlotTests.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/PhysicalProfileSlotTests.cs.meta
- path → Persistence &amp; Backend: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/ProfileInventoryRulesTests.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Tests/EditMode/LocalProfilePersistenceEditModeTests.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Tests/EditMode/ShopTransactionPersistenceEditModeTests.cs
- message-term → Persistence &amp; Backend: backend
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Backend.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Backend/InventoryProtocolSlotTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Backend/InventoryProtocolSlotTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Commerce/MerchantTradePreviewTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Equipment/PreparedEquipmentTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/RaidAdmissionSlotAggregationTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/RaidAdmissionSlotAggregationTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/PhysicalProfileSlotTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/PhysicalProfileSlotTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/PhysicalSlotTestSeed.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/PhysicalSlotTestSeed.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/ProfileInventoryRulesTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/RaidReservationLifecycleTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/RecoveryWeaponConsentTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/RemoteShopTransactionServiceTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/StashCapacityTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/StashCorruptLoginRecoveryTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/StashDisplayOrderTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/StashHydrationCapacityTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/StashReservationInvariantTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/StashSplitTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/StashSplitTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/StashTransferCoordinatorTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/StashTransferEvaluationTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Backend.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Backend/InventoryProtocolSlotTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Backend/InventoryProtocolSlotTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Commerce/MerchantTradePreviewTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Equipment/PreparedEquipmentTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/RaidAdmissionSlotAggregationTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/RaidAdmissionSlotAggregationTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/PhysicalProfileSlotTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/PhysicalProfileSlotTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/PhysicalSlotTestSeed.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/PhysicalSlotTestSeed.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/ProfileInventoryRulesTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/RaidReservationLifecycleTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/RecoveryWeaponConsentTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/RemoteShopTransactionServiceTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/StashCapacityTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/StashCorruptLoginRecoveryTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/StashDisplayOrderTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/StashHydrationCapacityTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/StashReservationInvariantTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/StashSplitTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/StashSplitTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/StashTransferCoordinatorTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/StashTransferEvaluationTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/LocalProfilePersistenceEditModeTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/ShopTransactionPersistenceEditModeTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/PlayModeMerchantComposition.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/Presentation/LobbyStashPresenterPlayModeTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidInventoryPresenterInteractionTests.cs
- message-term → Tests: Tests

#### Diagnostics

- No classification ambiguity detected.

### 43fb6ad3c41e36f3d63761cfa7a8a87ec25289d0

[43fb6ad3c41e36f3d63761cfa7a8a87ec25289d0](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/43fb6ad3c41e36f3d63761cfa7a8a87ec25289d0)

Date: 2026-11-04T01:27:16-03:00 · Classification: Features (explicit) · Systems: Inventory & Loot, Persistence & Backend, Tests

Flags: primary or unclassified

#### Original subject

```text
feat(backend): add one-time inventory slot migration script
```

#### Original body

```text
Add backend/scripts/migrate_inventory_slots.js to move legacy aggregate
inventories to physical slots once, explicitly, during the cutover
window. Reads and ordinary writes never migrate data.

- Per list (stash, loadout, pendingReservation.items) in stored order:
  fold legacy duplicate lootIds into the first occurrence, assign
  slots 0..n-1, keep over-capacity entries in overflow slots and
  report unknown lootIds without dropping them.
- One revision-guarded write per character sets
  inventorySchemaVersion 2; per-lootId totals must match or the
  character is skipped unchanged. Version 2 is skipped, so reruns are
  no-ops. --dry-run writes nothing and prints a report.

Tests: migrate.inventory.slots (planner) and integration (dry run,
migration, rerun, revision conflict, aborted character unchanged).
```

#### Changed paths

```text
backend/scripts/migrate_inventory_slots.js
backend/tests/integration/migrate.inventory.slots.test.js
backend/tests/migrate.inventory.slots.test.js
```

#### Classification evidence

- type → Features: feat
- path → Inventory &amp; Loot: backend/scripts/migrate\_inventory\_slots.js
- path → Inventory &amp; Loot: backend/tests/integration/migrate.inventory.slots.test.js
- path → Inventory &amp; Loot: backend/tests/migrate.inventory.slots.test.js
- message-term → Inventory &amp; Loot: inventory
- path → Persistence &amp; Backend: backend/scripts/migrate\_inventory\_slots.js
- path → Persistence &amp; Backend: backend/tests/integration/migrate.inventory.slots.test.js
- path → Persistence &amp; Backend: backend/tests/migrate.inventory.slots.test.js
- scope → Persistence &amp; Backend: backend
- message-term → Persistence &amp; Backend: backend
- path → Tests: backend/tests/integration/migrate.inventory.slots.test.js
- path → Tests: backend/tests/migrate.inventory.slots.test.js
- message-term → Tests: Tests

#### Diagnostics

- No classification ambiguity detected.

### e36cafcb12bcddae3cae576069b83d0e63768cb6

[e36cafcb12bcddae3cae576069b83d0e63768cb6](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/e36cafcb12bcddae3cae576069b83d0e63768cb6)

Date: 2026-11-04T01:27:15-03:00 · Classification: Features (explicit) · Systems: Extraction, Inventory & Loot, Networking & Sessions, Persistence & Backend, Tests

Flags: primary or unclassified

#### Original subject

```text
feat(backend): persist physical inventory slots behind a protocol gate
```

#### Original body

```text
Move Town Loadout and Stash persistence from one aggregate entry per
lootId to physical slots (approved decisions D1 and D3), and add the
authoritative Stash/Loadout split for TASK-281 behind a feature flag.

- Character items gain an optional slot; inventory.inventorySchemaVersion
  1 = legacy aggregate lists, 2 = physical slots. Reads never merge or
  save; the read-time lootId normalizer is removed.
- X-Grimhold-Inventory-Protocol: 2 middleware on every inventory,
  reservation, extraction and admission route. INVENTORY_PROTOCOL_GATE
  (default off) rejects 426 CLIENT_UPGRADE_REQUIRED and v1 documents
  409 INVENTORY_MIGRATION_REQUIRED.
- v2 rules: capacity by occupied slots (Loadout 30, Stash 60), overflow
  slots reducing-only, receive into the lowest matching then lowest
  free slot, removal leaves a hole, sourceSlot-addressed transfer and
  sell, slotOrder Sort as the only compaction, POST /slots/move
  (move, merge, swap), exact reservation rollback, extraction placed
  into slots and rejected above capacity before claiming the result.
- POST /slots/split behind INVENTORY_SPLIT_ENABLED (default off,
  403 FEATURE_DISABLED); revision-guarded, rejected during a pending
  reservation.
- Raid admission tickets stay aggregated by lootId.

Tests: inventory.protocol, inventory.slots, inventory.split (unit,
integration, concurrency) and inventory.slots.expand; read-time
normalization expectations in inventory.service replaced.

Docs: backend/README.md (protocol, flags, DTOs, error codes).
```

#### Changed paths

```text
backend/README.md
backend/scripts/seed-test-accounts.js
backend/src/config/env.js
backend/src/middleware/inventoryProtocol.js
backend/src/models/Character.js
backend/src/routes/character.routes.js
backend/src/routes/inventory.routes.js
backend/src/routes/raid.routes.js
backend/src/services/CharacterService.js
backend/src/services/ExtractionCommitService.js
backend/src/services/InventoryEntries.js
backend/src/services/InventoryLootIdNormalizer.js
backend/src/services/InventoryService.js
backend/src/services/RaidResultService.js
backend/src/services/SessionTicketService.js
backend/src/validators/inventory.validators.js
backend/tests/concurrency/inventory.real.test.js
backend/tests/concurrency/inventory.split.real.test.js
backend/tests/integration/inventory.slots.expand.test.js
backend/tests/integration/inventory.slots.test.js
backend/tests/integration/inventory.split.test.js
backend/tests/inventory.protocol.test.js
backend/tests/inventory.service.test.js
backend/tests/inventory.slots.test.js
backend/tests/inventory.split.test.js
backend/tests/setup.js
```

#### Classification evidence

- type → Features: feat
- path → Extraction: backend/src/services/ExtractionCommitService.js
- message-term → Extraction: extraction
- path → Inventory &amp; Loot: backend/src/middleware/inventoryProtocol.js
- path → Inventory &amp; Loot: backend/src/routes/inventory.routes.js
- path → Inventory &amp; Loot: backend/src/services/InventoryEntries.js
- path → Inventory &amp; Loot: backend/src/services/InventoryLootIdNormalizer.js
- path → Inventory &amp; Loot: backend/src/services/InventoryService.js
- path → Inventory &amp; Loot: backend/src/validators/inventory.validators.js
- path → Inventory &amp; Loot: backend/tests/concurrency/inventory.real.test.js
- path → Inventory &amp; Loot: backend/tests/concurrency/inventory.split.real.test.js
- path → Inventory &amp; Loot: backend/tests/integration/inventory.slots.expand.test.js
- path → Inventory &amp; Loot: backend/tests/integration/inventory.slots.test.js
- path → Inventory &amp; Loot: backend/tests/integration/inventory.split.test.js
- path → Inventory &amp; Loot: backend/tests/inventory.protocol.test.js
- path → Inventory &amp; Loot: backend/tests/inventory.service.test.js
- path → Inventory &amp; Loot: backend/tests/inventory.slots.test.js
- path → Inventory &amp; Loot: backend/tests/inventory.split.test.js
- message-term → Inventory &amp; Loot: inventory
- path → Networking &amp; Sessions: backend/src/services/SessionTicketService.js
- path → Persistence &amp; Backend: backend/README.md
- path → Persistence &amp; Backend: backend/scripts/seed-test-accounts.js
- path → Persistence &amp; Backend: backend/src/config/env.js
- path → Persistence &amp; Backend: backend/src/middleware/inventoryProtocol.js
- path → Persistence &amp; Backend: backend/src/models/Character.js
- path → Persistence &amp; Backend: backend/src/routes/character.routes.js
- path → Persistence &amp; Backend: backend/src/routes/inventory.routes.js
- path → Persistence &amp; Backend: backend/src/routes/raid.routes.js
- path → Persistence &amp; Backend: backend/src/services/CharacterService.js
- path → Persistence &amp; Backend: backend/src/services/ExtractionCommitService.js
- path → Persistence &amp; Backend: backend/src/services/InventoryEntries.js
- path → Persistence &amp; Backend: backend/src/services/InventoryLootIdNormalizer.js
- path → Persistence &amp; Backend: backend/src/services/InventoryService.js
- path → Persistence &amp; Backend: backend/src/services/RaidResultService.js
- path → Persistence &amp; Backend: backend/src/services/SessionTicketService.js
- path → Persistence &amp; Backend: backend/src/validators/inventory.validators.js
- path → Persistence &amp; Backend: backend/tests/concurrency/inventory.real.test.js
- path → Persistence &amp; Backend: backend/tests/concurrency/inventory.split.real.test.js
- path → Persistence &amp; Backend: backend/tests/integration/inventory.slots.expand.test.js
- path → Persistence &amp; Backend: backend/tests/integration/inventory.slots.test.js
- path → Persistence &amp; Backend: backend/tests/integration/inventory.split.test.js
- path → Persistence &amp; Backend: backend/tests/inventory.protocol.test.js
- path → Persistence &amp; Backend: backend/tests/inventory.service.test.js
- path → Persistence &amp; Backend: backend/tests/inventory.slots.test.js
- path → Persistence &amp; Backend: backend/tests/inventory.split.test.js
- path → Persistence &amp; Backend: backend/tests/setup.js
- scope → Persistence &amp; Backend: backend
- message-term → Persistence &amp; Backend: backend
- path → Tests: backend/scripts/seed-test-accounts.js
- path → Tests: backend/tests/concurrency/inventory.real.test.js
- path → Tests: backend/tests/concurrency/inventory.split.real.test.js
- path → Tests: backend/tests/integration/inventory.slots.expand.test.js
- path → Tests: backend/tests/integration/inventory.slots.test.js
- path → Tests: backend/tests/integration/inventory.split.test.js
- path → Tests: backend/tests/inventory.protocol.test.js
- path → Tests: backend/tests/inventory.service.test.js
- path → Tests: backend/tests/inventory.slots.test.js
- path → Tests: backend/tests/inventory.split.test.js
- path → Tests: backend/tests/setup.js
- message-term → Tests: Tests

#### Diagnostics

- No classification ambiguity detected.

### b815be1341cce259b562fcc6de08eea2fa5590d7

[b815be1341cce259b562fcc6de08eea2fa5590d7](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/b815be1341cce259b562fcc6de08eea2fa5590d7)

Date: 2026-11-04T01:26:59-03:00 · Classification: Features (explicit) · Systems: Inventory & Loot, UI, Build & Tooling, Tests

Flags: primary or unclassified

#### Original subject

```text
feat(loot): add authoritative Raid stack split
```

#### Original body

```text
Add the State Authority split for the Raid inventory (TASK-281, Raid
half). A split moves part of one addressed physical stack into an
empty addressed slot; the new stack keeps the source item and Raid
origin, so the total and every origin bucket are conserved (INV-AC-03).

- LootInventoryRules.ValidateSplit/TrySplit: amount in [1, qty-1],
  empty distinct destination, occupied slots below the gameplay
  capacity; rejection writes nothing.
- PlayerLootReceiver.TrySplitStack: State Authority only, rejects a
  stale revision or extracted player, advances the revision once.
- Rejections reuse existing LootTransferFailureReason values.
- The Input Authority request path and UI stay in TASK-282.

Tests:
- LootInventorySlotRulesTests: 13 -> 5 + 8, mixed origins, invalid
  requests and capacity are atomic.
- PlayerLootReceiverSlotsPlayModeTests: mixed-origin split, stale and
  reused revision, occupied destination, whole-stack amount.
```

#### Changed paths

```text
Project Grimhold/Assets/Scripts/Gameplay/LootInventoryRules.cs
Project Grimhold/Assets/Scripts/Gameplay/PlayerLootReceiver.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootInventorySlotRulesTests.cs
Project Grimhold/Assets/Tests/PlayMode/PlayerLootReceiverSlotsPlayModeTests.cs
```

#### Classification evidence

- type → Features: feat
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/LootInventoryRules.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/PlayerLootReceiver.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootInventorySlotRulesTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/PlayMode/PlayerLootReceiverSlotsPlayModeTests.cs
- scope → Inventory &amp; Loot: loot
- message-term → Inventory &amp; Loot: loot
- message-term → UI: UI
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootInventorySlotRulesTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootInventorySlotRulesTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/PlayerLootReceiverSlotsPlayModeTests.cs
- message-term → Tests: Tests

#### Diagnostics

- No classification ambiguity detected.

### 9727c4f970c91b260ca6e1162ed2a0594ef2f636

[9727c4f970c91b260ca6e1162ed2a0594ef2f636](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/9727c4f970c91b260ca6e1162ed2a0594ef2f636)

Date: 2026-11-03T16:32:27-03:00 · Classification: Features (explicit) · Systems: Combat, Inventory & Loot, UI, World & Visibility, Persistence & Backend, Build & Tooling, Tests

Flags: primary or unclassified

#### Original subject

```text
feat(equipment): add weapon gem sockets and Dungeon variant generator
```

#### Original body

```text
Extend WeaponInstanceModifiers with 0-2 gem sockets, each empty or
holding one installed gem LootId, as described in Doc 09 sections 20.2
and 22. Add a pure, seed-deterministic WeaponInstanceGenerator that
rolls legal Dungeon variants per Doc 09 section 8.2: the primary scaling
matches the template's natural attribute, the secondary uses a
different attribute, and grades range from E to S.

Generation probabilities live in WeaponInstanceGenerationRules (basis
point chances, E-S grade weights, socket count weights, gem pool). Doc
09 section 8.1 assigns them to Loot/Balance, so no defaults are
authored. WeaponInstanceModifiersNetworkData is a blittable
INetworkStruct that encodes gems as catalog index + 1. It is
intentionally not replicated: PlayerCombatArchitecture.md section 3
keeps instance modifiers out of LootEntry, Equipment and persistence
until unique Item Instances exist end to end.

EquipmentTooltipPresentationBuilder gains an instance overload that
shows dual scaling grades ("FUE A · VIT D"), socket occupancy
("Ranuras: 2 (vacías)") and the effective damage from
WeaponDamageCalculator. The definition-only overload output is unchanged.

Chest integration and inventory/stash display remain blocked by the
Template/Instance migration.

Tests:
- WeaponGemSocketTests: socket limits, gems beyond the socket count,
  network data round trip and rejection paths.
- WeaponInstanceGeneratorTests: 1,000 weapons per STR/DEX/INT with
  legal combinations and socket limits, determinism, rules validation.
- EquipmentTooltipPresentationBuilderTests: dual scaling, socket
  occupancy, effective damage, invalid and non-weapon instances.

Validation: 104/104 focused EditMode tests pass. Full EditMode suite:
5516/5541; the 25 failures are in unrelated prefab, atlas, animator,
catalog and RPC tests. No baseline run without this change was made.

Refs: TASK-280
```

#### Changed paths

```text
Project Grimhold/Assets/Scripts/Player/Equipment/WeaponGemSocket.cs
Project Grimhold/Assets/Scripts/Player/Equipment/WeaponGemSocket.cs.meta
Project Grimhold/Assets/Scripts/Player/Equipment/WeaponInstanceGenerationRules.cs
Project Grimhold/Assets/Scripts/Player/Equipment/WeaponInstanceGenerationRules.cs.meta
Project Grimhold/Assets/Scripts/Player/Equipment/WeaponInstanceGenerator.cs
Project Grimhold/Assets/Scripts/Player/Equipment/WeaponInstanceGenerator.cs.meta
Project Grimhold/Assets/Scripts/Player/Equipment/WeaponInstanceModifiers.cs
Project Grimhold/Assets/Scripts/Player/Equipment/WeaponInstanceModifiersNetworkData.cs
Project Grimhold/Assets/Scripts/Player/Equipment/WeaponInstanceModifiersNetworkData.cs.meta
Project Grimhold/Assets/Scripts/Player/Presentation/EquipmentTooltipPresentationBuilder.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Equipment/WeaponGemSocketTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Equipment/WeaponGemSocketTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Equipment/WeaponInstanceGeneratorTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Equipment/WeaponInstanceGeneratorTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/EquipmentTooltipPresentationBuilderTests.cs
```

#### Classification evidence

- type → Features: feat
- path → Combat: Project Grimhold/Assets/Scripts/Player/Equipment/WeaponGemSocket.cs
- path → Combat: Project Grimhold/Assets/Scripts/Player/Equipment/WeaponGemSocket.cs.meta
- path → Combat: Project Grimhold/Assets/Scripts/Player/Equipment/WeaponInstanceGenerationRules.cs
- path → Combat: Project Grimhold/Assets/Scripts/Player/Equipment/WeaponInstanceGenerationRules.cs.meta
- path → Combat: Project Grimhold/Assets/Scripts/Player/Equipment/WeaponInstanceGenerator.cs
- path → Combat: Project Grimhold/Assets/Scripts/Player/Equipment/WeaponInstanceGenerator.cs.meta
- path → Combat: Project Grimhold/Assets/Scripts/Player/Equipment/WeaponInstanceModifiers.cs
- path → Combat: Project Grimhold/Assets/Scripts/Player/Equipment/WeaponInstanceModifiersNetworkData.cs
- path → Combat: Project Grimhold/Assets/Scripts/Player/Equipment/WeaponInstanceModifiersNetworkData.cs.meta
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Editor/Equipment/WeaponGemSocketTests.cs
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Editor/Equipment/WeaponGemSocketTests.cs.meta
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Editor/Equipment/WeaponInstanceGeneratorTests.cs
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Editor/Equipment/WeaponInstanceGeneratorTests.cs.meta
- message-term → Combat: weapon
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Equipment/WeaponGemSocket.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Equipment/WeaponGemSocket.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Equipment/WeaponInstanceGenerationRules.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Equipment/WeaponInstanceGenerationRules.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Equipment/WeaponInstanceGenerator.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Equipment/WeaponInstanceGenerator.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Equipment/WeaponInstanceModifiers.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Equipment/WeaponInstanceModifiersNetworkData.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Equipment/WeaponInstanceModifiersNetworkData.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/EquipmentTooltipPresentationBuilder.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Equipment/WeaponGemSocketTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Equipment/WeaponGemSocketTests.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Equipment/WeaponInstanceGeneratorTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Equipment/WeaponInstanceGeneratorTests.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/EquipmentTooltipPresentationBuilderTests.cs
- scope → Inventory &amp; Loot: equipment
- message-term → Inventory &amp; Loot: equipment
- path → UI: Project Grimhold/Assets/Scripts/Player/Presentation/EquipmentTooltipPresentationBuilder.cs
- path → UI: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/EquipmentTooltipPresentationBuilderTests.cs
- message-term → World &amp; Visibility: Dungeon
- message-term → Persistence &amp; Backend: persistence
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Equipment/WeaponGemSocketTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Equipment/WeaponGemSocketTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Equipment/WeaponInstanceGeneratorTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Equipment/WeaponInstanceGeneratorTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/EquipmentTooltipPresentationBuilderTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Equipment/WeaponGemSocketTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Equipment/WeaponGemSocketTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Equipment/WeaponInstanceGeneratorTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Equipment/WeaponInstanceGeneratorTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/EquipmentTooltipPresentationBuilderTests.cs
- message-term → Tests: Tests

#### Diagnostics

- No classification ambiguity detected.

### 8acc80236d68d6a526ab268b703bb4374c601283

[8acc80236d68d6a526ab268b703bb4374c601283](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/8acc80236d68d6a526ab268b703bb4374c601283)

Date: 2026-11-02T20:38:54-03:00 · Classification: Documentation (explicit) · Systems: Unassigned

Flags: secondary

#### Original subject

```text
docs(odd): record T7 void bell cadence preservation
```

#### Original body

```text
Refs: US-76, TASK-264, #656, #657
```

#### Changed paths

```text
odd/tasks/treasure-passives-and-cooldowns.md
```

#### Classification evidence

- type → Documentation: docs

#### Diagnostics

- **unassigned-system:** No recognized exact path/scope token or bounded system term. Inspect the listed paths before assigning a system.

### 9c5cf4ae4857ff3957d277f4319744033db7d03d

[9c5cf4ae4857ff3957d277f4319744033db7d03d](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/9c5cf4ae4857ff3957d277f4319744033db7d03d)

Date: 2026-11-02T20:37:57-03:00 · Classification: Bug Fixes (explicit) · Systems: Abilities, Inventory & Loot, Tests

Flags: primary or unclassified

#### Original subject

```text
fix(loot): prevent void bell chime spam by preserving interval cadence
```

#### Original body

```text
Preserve the active VoidBellChimeTimer across intermittent sprint key releases instead of clearing it immediately to TickTimer.None, enforcing the authoritative 2.0s chime cadence interval and preventing rapid Shift-tap chime and aggro spam. Add an automated PlayMode assertion ensuring chime sequence stability during rapid sprint restarts.

Player-Fixed: Sprinting with the Void Bell no longer repeatedly chimes when pressing and releasing sprint rapidly.
Refs: US-76, TASK-264, #656, #657
```

#### Changed paths

```text
Project Grimhold/Assets/Scripts/Player/PlayerCharacter.cs
Project Grimhold/Assets/Tests/PlayMode/UtilityExpeditionPlayModeTests.cs
```

#### Classification evidence

- type → Bug Fixes: fix
- path → Abilities: Project Grimhold/Assets/Tests/PlayMode/UtilityExpeditionPlayModeTests.cs
- scope → Inventory &amp; Loot: loot
- message-term → Inventory &amp; Loot: loot
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/UtilityExpeditionPlayModeTests.cs

#### Diagnostics

- No classification ambiguity detected.

### 7ef8f7a27b0b7e964d2239ae071781ab4a99871c

[7ef8f7a27b0b7e964d2239ae071781ab4a99871c](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/7ef8f7a27b0b7e964d2239ae071781ab4a99871c)

Date: 2026-11-02T20:22:13-03:00 · Classification: Documentation (explicit) · Systems: Tests

Flags: secondary

#### Original subject

```text
docs(odd): record T6 rune compass tracking and test isolation
```

#### Original body

```text
Refs: US-76, TASK-265, #656, #658
```

#### Changed paths

```text
odd/tasks/treasure-passives-and-cooldowns.md
```

#### Classification evidence

- type → Documentation: docs
- message-term → Tests: test

#### Diagnostics

- No classification ambiguity detected.

### 8480bedab3e96c387caef697f775f23942fa78f2

[8480bedab3e96c387caef697f775f23942fa78f2](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/8480bedab3e96c387caef697f775f23942fa78f2)

Date: 2026-11-02T20:21:05-03:00 · Classification: Features (explicit) · Systems: Abilities, Extraction, Inventory & Loot, Art & Animation, Tests

Flags: primary or unclassified

#### Original subject

```text
feat(utilities): render rune compass pointer as dynamic tracking sprite
```

#### Original body

```text
Render the Rune Compass visual indicator via a crisp 2D sprite orbiting the player at 1.35m radius, continuously rotating and tracking the nearest Extraction Sanctuary in real-time throughout its 5.0s display duration. Pass sanctuary coordinates through RPC_ReceiveCompassActivation to support dynamic recalculation, and gate PassiveAndUtilityDebugOverlay against automated test runner scenes.

Player-Improved: Activating a Rune Compass now projects a dynamic celestial needle sprite that stays pointed toward the nearest Extraction Sanctuary in real time as you move.
Refs: US-76, TASK-265, #656, #658
```

#### Changed paths

```text
Project Grimhold/Assets/Scripts/Gameplay/Debugging/PassiveAndUtilityDebugOverlay.cs
Project Grimhold/Assets/Scripts/Gameplay/Utilities/RuneCompassPointerEffect.cs
Project Grimhold/Assets/Scripts/Gameplay/Utilities/RuneCompassPointerEffect.cs.meta
Project Grimhold/Assets/Scripts/Player/Interaction/PlayerConsumableNetworkController.cs
```

#### Classification evidence

- type → Features: feat
- path → Abilities: Project Grimhold/Assets/Scripts/Gameplay/Debugging/PassiveAndUtilityDebugOverlay.cs
- message-term → Extraction: Extraction
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/PlayerConsumableNetworkController.cs
- message-term → Art &amp; Animation: sprite
- message-term → Tests: test

#### Diagnostics

- No classification ambiguity detected.

### c79d5e225afbc0c4ab22c22620be96830d0c96fd

[c79d5e225afbc0c4ab22c22620be96830d0c96fd](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/c79d5e225afbc0c4ab22c22620be96830d0c96fd)

Date: 2026-11-02T17:59:23-03:00 · Classification: Bug Fixes (explicit) · Systems: Abilities, Inventory & Loot, UI

Flags: primary or unclassified

#### Original subject

```text
fix(utilities): add rune compass directional visual and prevent notice overwrite
```

#### Original body

```text
Prevent RaidHudPresenter from instantly overwriting the compass notice with generic Item used confirmation text. Implement RuneCompassPointerEffect to present a celestial directional arrow and crystal chime on the player pointing toward the nearest sanctuary, and log orientation in F10 overlay and console.

Refs: US-76, TASK-265
```

#### Changed paths

```text
Project Grimhold/Assets/Prefabs/Loot/LootPickup_ArcaneDetector.prefab
Project Grimhold/Assets/Scripts/Gameplay/Debugging/PassiveAndUtilityDebugOverlay.cs
Project Grimhold/Assets/Scripts/Gameplay/Utilities/CardinalDirectionHelper.cs
Project Grimhold/Assets/Scripts/Gameplay/Utilities/RuneCompassPointerEffect.cs
Project Grimhold/Assets/Scripts/Player/Interaction/PlayerConsumableNetworkController.cs
Project Grimhold/Assets/Scripts/Player/Presentation/RaidHudPresenter.cs
```

#### Classification evidence

- type → Bug Fixes: fix
- path → Abilities: Project Grimhold/Assets/Scripts/Gameplay/Debugging/PassiveAndUtilityDebugOverlay.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Prefabs/Loot/LootPickup\_ArcaneDetector.prefab
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/PlayerConsumableNetworkController.cs
- path → UI: Project Grimhold/Assets/Scripts/Player/Presentation/RaidHudPresenter.cs

#### Diagnostics

- No classification ambiguity detected.

### a1a97759e15381cca528f43576826e9857008abd

[a1a97759e15381cca528f43576826e9857008abd](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/a1a97759e15381cca528f43576826e9857008abd)

Date: 2026-11-02T17:24:09-03:00 · Classification: Bug Fixes (explicit) · Systems: Inventory & Loot, UI, Tests

Flags: primary or unclassified

#### Original subject

```text
fix(utilities): restore arcane detector pickup prefab and scene instance
```

#### Original body

```text
Restore valid YAML hierarchy and component definitions on LootPickup_ArcaneDetector prefab so Unity AssetDatabase imports it cleanly, and instantiate both Rune Compass and Arcane Detector test pickups into the Gameplay scene near Sanctuary 1.

Refs: US-76, TASK-265
```

#### Changed paths

```text
Project Grimhold/Assets/Placeholder/UI/LootIconMissing.png.meta
Project Grimhold/Assets/Prefabs/Loot/LootPickup_ArcaneDetector.prefab
Project Grimhold/Assets/Prefabs/Loot/LootPickup_RuneCompass.prefab
Project Grimhold/Assets/Scenes/Gameplay.unity
```

#### Classification evidence

- type → Bug Fixes: fix
- path → Inventory &amp; Loot: Project Grimhold/Assets/Placeholder/UI/LootIconMissing.png.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Prefabs/Loot/LootPickup\_ArcaneDetector.prefab
- path → Inventory &amp; Loot: Project Grimhold/Assets/Prefabs/Loot/LootPickup\_RuneCompass.prefab
- message-term → Inventory &amp; Loot: pickup
- path → UI: Project Grimhold/Assets/Placeholder/UI/LootIconMissing.png.meta
- message-term → Tests: test

#### Diagnostics

- No classification ambiguity detected.

### 6ac67a53ae11b03e80d008fac0a789e38bd6e20b

[6ac67a53ae11b03e80d008fac0a789e38bd6e20b](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/6ac67a53ae11b03e80d008fac0a789e38bd6e20b)

Date: 2026-11-02T16:45:57-03:00 · Classification: Features (explicit) · Systems: Abilities, Inventory & Loot, Audio, Build & Tooling, Tests

Flags: primary or unclassified

#### Original subject

```text
feat(loot): add utility test pickups and passive debug overlay
```

#### Original body

```text
- Polish Void Bell chime presentation with world-space pulse ring,
  domain reload safety, and standalone audio fallback.
- Provide F10 debug overlay and console logging for passive treasure
  modifiers and container cooldown retention.
- Create Rune Compass and Arcane Detector pickup prefabs, wire them to
  RaidLootTestSpawner, and place them in Gameplay scene.

Refs: US-76, TASK-264, TASK-265, #656, #657, #658
```

#### Changed paths

```text
Project Grimhold/Assets/Editor/Gameplay/RaidLootTestSpawnerEditor.cs
Project Grimhold/Assets/Prefabs/Loot/LootPickup_ArcaneDetector.prefab
Project Grimhold/Assets/Prefabs/Loot/LootPickup_ArcaneDetector.prefab.meta
Project Grimhold/Assets/Prefabs/Loot/LootPickup_RuneCompass.prefab
Project Grimhold/Assets/Prefabs/Loot/LootPickup_RuneCompass.prefab.meta
Project Grimhold/Assets/Scenes/Gameplay.unity
Project Grimhold/Assets/Scripts/Gameplay/Debugging/PassiveAndUtilityDebugOverlay.cs
Project Grimhold/Assets/Scripts/Gameplay/Debugging/PassiveAndUtilityDebugOverlay.cs.meta
Project Grimhold/Assets/Scripts/Gameplay/Debugging/RaidLootTestSpawner.cs
Project Grimhold/Assets/Scripts/Player/Interaction/PlayerInventoryPassiveEffectsController.cs
Project Grimhold/Assets/Scripts/Player/PlayerCharacter.cs
```

#### Classification evidence

- type → Features: feat
- path → Abilities: Project Grimhold/Assets/Scripts/Gameplay/Debugging/PassiveAndUtilityDebugOverlay.cs
- path → Abilities: Project Grimhold/Assets/Scripts/Gameplay/Debugging/PassiveAndUtilityDebugOverlay.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Editor/Gameplay/RaidLootTestSpawnerEditor.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Prefabs/Loot/LootPickup\_ArcaneDetector.prefab
- path → Inventory &amp; Loot: Project Grimhold/Assets/Prefabs/Loot/LootPickup\_ArcaneDetector.prefab.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Prefabs/Loot/LootPickup\_RuneCompass.prefab
- path → Inventory &amp; Loot: Project Grimhold/Assets/Prefabs/Loot/LootPickup\_RuneCompass.prefab.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/Debugging/RaidLootTestSpawner.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/PlayerInventoryPassiveEffectsController.cs
- scope → Inventory &amp; Loot: loot
- message-term → Inventory &amp; Loot: loot
- message-term → Audio: audio
- path → Build &amp; Tooling: Project Grimhold/Assets/Editor/Gameplay/RaidLootTestSpawnerEditor.cs
- path → Tests: Project Grimhold/Assets/Editor/Gameplay/RaidLootTestSpawnerEditor.cs
- path → Tests: Project Grimhold/Assets/Scripts/Gameplay/Debugging/RaidLootTestSpawner.cs
- message-term → Tests: test

#### Diagnostics

- No classification ambiguity detected.

### 56ddcb8b3519d7b2645ad0119d1a773ab3f0bf5a

[56ddcb8b3519d7b2645ad0119d1a773ab3f0bf5a](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/56ddcb8b3519d7b2645ad0119d1a773ab3f0bf5a)

Date: 2026-11-02T15:49:10-03:00 · Classification: Bug Fixes (explicit) · Systems: Abilities, Inventory & Loot, Tests

Flags: primary or unclassified

#### Original subject

```text
fix(utilities): resolve player cooldown owner over corpse container
```

#### Original body

```text
NetworkPlayer carries both a dormant corpse NetworkLootContainer and PlayerConsumableNetworkController on the same root GameObject. Once NetworkLootContainer implemented IUtilityCooldownProvider and IUtilityCooldownReceiver, generic sibling interface lookups resolved the co-located corpse container instead of the live player controller during drops, pickups, container transfers, and death conversion.

- Prefer PlayerConsumableNetworkController on sibling endpoint lookups in LootTransferTransaction, PlayerLootDropNetworkController, NetworkLootPickup, and PlayerCorpseGenerationController while preserving direct container-to-container/player cooldown transfers.
- Spawn concrete GreenSlime melee enemy prefab and cast NetworkBool in UtilityExpeditionPlayModeTests.
- Record live Unity MCP EditMode (15/15) and PlayMode (8/8) verification in the US-76 ODD tracker.

Refs: US-76, TASK-264, TASK-265
```

#### Changed paths

```text
Project Grimhold/Assets/Scripts/Gameplay/LootTransferTransaction.cs
Project Grimhold/Assets/Scripts/Gameplay/NetworkLootPickup.cs
Project Grimhold/Assets/Scripts/Player/Interaction/PlayerLootDropNetworkController.cs
Project Grimhold/Assets/Scripts/Player/PlayerCorpseGenerationController.cs
Project Grimhold/Assets/Tests/PlayMode/UtilityExpeditionPlayModeTests.cs
odd/tasks/treasure-passives-and-cooldowns.md
```

#### Classification evidence

- type → Bug Fixes: fix
- path → Abilities: Project Grimhold/Assets/Tests/PlayMode/UtilityExpeditionPlayModeTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/LootTransferTransaction.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/NetworkLootPickup.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/PlayerLootDropNetworkController.cs
- message-term → Inventory &amp; Loot: pickups
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/UtilityExpeditionPlayModeTests.cs

#### Diagnostics

- No classification ambiguity detected.

### 5367bc9dbadb5e262480edd2ca581757a89f9d52

[5367bc9dbadb5e262480edd2ca581757a89f9d52](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/5367bc9dbadb5e262480edd2ca581757a89f9d52)

Date: 2026-11-02T15:14:09-03:00 · Classification: Bug Fixes (explicit) · Systems: Abilities, Inventory & Loot, Build & Tooling, Documentation, Tests

Flags: primary or unclassified

#### Original subject

```text
fix(loot): gate equipment visual validation and stabilize US-76 tests
```

#### Original body

```text
Gate EquipmentVisualDefinition validation in LootDefinition.TryValidate
to armor categories so non-armor definitions deserialized without sprite
mappings do not invalidate LootDefinitionCatalog, world drops or loot
container initialization, and guard unspawned receivers in passive
treasure evaluation.

- Gate _equipmentVisualDefinition.TryValidate behind IsArmorCategory in
  LootDefinition while preserving strict validation on armor items.
- Guard PlayerInventoryPassiveEffectsController against unspawned
  PlayerLootReceiver instances before reading networked sequence state.
- Restore locked New-Testing budgets in RaidLootOriginWordCountTests and
  document the two container cooldown TickTimer words in
  LootInteractionArchitecture.md.
- Update InventoryPassiveEffectsTests and UtilityExpeditionPlayModeTests
  with productive catalog validation, required motor colliders, deferred
  Render rejection waits, runner input provision and Sanctuary service
  registration.

Player-Fixed: Dropping items in a Raid and opening loot chests or defeated player containers work properly again.
```

#### Changed paths

```text
Project Grimhold/Assets/Scripts/Loot/LootDefinition.cs
Project Grimhold/Assets/Scripts/Player/Interaction/PlayerInventoryPassiveEffectsController.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/InventoryPassiveEffectsTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/RaidLootOriginWordCountTests.cs
Project Grimhold/Assets/Tests/PlayMode/UtilityExpeditionPlayModeTests.cs
Project Grimhold/Docs/Architecture/LootInteractionArchitecture.md
odd/tasks/treasure-passives-and-cooldowns.md
```

#### Classification evidence

- type → Bug Fixes: fix
- path → Abilities: Project Grimhold/Assets/Tests/PlayMode/UtilityExpeditionPlayModeTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Loot/LootDefinition.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/PlayerInventoryPassiveEffectsController.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/InventoryPassiveEffectsTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/RaidLootOriginWordCountTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Docs/Architecture/LootInteractionArchitecture.md
- scope → Inventory &amp; Loot: loot
- message-term → Inventory &amp; Loot: loot
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/InventoryPassiveEffectsTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/RaidLootOriginWordCountTests.cs
- path → Documentation: Project Grimhold/Docs/Architecture/LootInteractionArchitecture.md
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/InventoryPassiveEffectsTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/RaidLootOriginWordCountTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/UtilityExpeditionPlayModeTests.cs
- message-term → Tests: tests

#### Diagnostics

- No classification ambiguity detected.

### e91525252cac0520492f462594d40387463aacb5

[e91525252cac0520492f462594d40387463aacb5](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/e91525252cac0520492f462594d40387463aacb5)

Date: 2026-11-02T13:54:16-03:00 · Classification: Bug Fixes (explicit) · Systems: Abilities, Inventory & Loot, Networking & Sessions, Build & Tooling, Documentation, Tests

Flags: primary or unclassified

#### Original subject

```text
fix(utilities): retain utility cooldowns across container transfers
```

#### Original body

```text
Preserve active Rune Compass and Arcane Detector TickTimer cooldowns
when depositing into or withdrawing from world, enemy, or player corpse
containers so cooldowns cannot be bypassed via container transfers.

Reviewers must verify:
- NetworkLootContainer replicates RuneCompassCooldown and
  ArcaneDetectorCooldown within approved Fusion word budgets and keeps
  Host Migration restore state intact.
- LootTransferTransaction transfers active utility cooldowns between
  source and destination endpoints and clears the source timer once zero
  units remain.
- PlayerCorpseGenerationController transfers active utility cooldowns
  into the defeated player's corpse container upon conversion.

Tests updated:
- UtilityExpeditionRulesTests (container deposit/withdrawal cooldown
  transfer across players)
- UtilityExpeditionPlayModeTests (container deposit/withdrawal retention,
  corpse retention, and treasure passive runtime coverage)
- RaidLootOriginWordCountTests

Docs updated:
- LootInteractionArchitecture.md
- odd/tasks/treasure-passives-and-cooldowns.md

Player-Fixed: Storing a Rune Compass or Arcane Detector in a chest or corpse no longer resets its active cooldown.
Refs: TASK-265, #656, #658
```

#### Changed paths

```text
Project Grimhold/Assets/Scripts/Gameplay/LootTransferTransaction.cs
Project Grimhold/Assets/Scripts/Gameplay/NetworkLootContainer.cs
Project Grimhold/Assets/Scripts/Player/PlayerCorpseGenerationController.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/RaidLootOriginWordCountTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Utilities/UtilityExpeditionRulesTests.cs
Project Grimhold/Assets/Tests/PlayMode/UtilityExpeditionPlayModeTests.cs
Project Grimhold/Docs/Architecture/LootInteractionArchitecture.md
odd/tasks/treasure-passives-and-cooldowns.md
```

#### Classification evidence

- type → Bug Fixes: fix
- path → Abilities: Project Grimhold/Assets/Tests/EditMode/Editor/Utilities/UtilityExpeditionRulesTests.cs
- path → Abilities: Project Grimhold/Assets/Tests/PlayMode/UtilityExpeditionPlayModeTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/LootTransferTransaction.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/NetworkLootContainer.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/RaidLootOriginWordCountTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Docs/Architecture/LootInteractionArchitecture.md
- message-term → Networking &amp; Sessions: Host Migration
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/RaidLootOriginWordCountTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Utilities/UtilityExpeditionRulesTests.cs
- path → Documentation: Project Grimhold/Docs/Architecture/LootInteractionArchitecture.md
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/RaidLootOriginWordCountTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Utilities/UtilityExpeditionRulesTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/UtilityExpeditionPlayModeTests.cs
- message-term → Tests: Tests

#### Diagnostics

- No classification ambiguity detected.

### 6c6923e8962ce0dce0b0e10e6be973d0b4e25d96

[6c6923e8962ce0dce0b0e10e6be973d0b4e25d96](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/6c6923e8962ce0dce0b0e10e6be973d0b4e25d96)

Date: 2026-11-02T13:53:54-03:00 · Classification: Features (explicit) · Systems: Inventory & Loot, UI, World & Visibility, Audio, Build & Tooling, Documentation, Tests

Flags: primary or unclassified

#### Original subject

```text
feat(loot): integrate passive treasure effects and chime alerts
```

#### Original body

```text
Complete runtime and local presentation integration for MVP passive
treasures carried in Raid inventory without stacking duplicates, and
revoke active modifiers immediately upon drop, deposit, or death.

Reviewers must verify:
- Pilgrim's Light applies a 1.35x stamina regeneration multiplier.
- Void Bell applies a 1.15x sprint speed multiplier and advances a
  tick-driven 2.0s chime on State Authority while sprinting, alerting
  live IAggroReceiver enemies within 15m and presenting audio plus a
  provisional ring pulse to observers within 15m.
- Wanderer's Eye scales local NearRadius, FarFog distances, and avatar
  Light2D radii by 0.75x without mutating shared ScriptableObjects,
  while blocking the local minimap.

Tests updated:
- InventoryPassiveEffectsTests (stamina regen, 15m aggro deduplication,
  15m chime presentation radius, vision and Light2D scaling, death reset)

Docs updated:
- VisibilityArchitecture.md

Player-Added: Pilgrim's Light, Void Bell, and Wanderer's Eye now apply their passive effects while carried in raids.
Refs: TASK-264, #656, #657
```

#### Changed paths

```text
Project Grimhold/Assets/Scripts/Audio/AudioManager.cs
Project Grimhold/Assets/Scripts/Gameplay/Visibility/VisionFogPresenter.cs
Project Grimhold/Assets/Scripts/Gameplay/Visibility/VisionQuery.cs
Project Grimhold/Assets/Scripts/Player/Interaction/PlayerInventoryPassiveEffectsController.cs
Project Grimhold/Assets/Scripts/Player/PlayerCharacter.cs
Project Grimhold/Assets/Scripts/Player/PlayerStaminaNetworkController.cs
Project Grimhold/Assets/Scripts/Player/Presentation/LocalVisionSource.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/InventoryPassiveEffectsTests.cs
Project Grimhold/Docs/Architecture/VisibilityArchitecture.md
```

#### Classification evidence

- type → Features: feat
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/PlayerInventoryPassiveEffectsController.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/InventoryPassiveEffectsTests.cs
- scope → Inventory &amp; Loot: loot
- message-term → Inventory &amp; Loot: loot
- message-term → UI: minimap
- path → World &amp; Visibility: Project Grimhold/Assets/Scripts/Gameplay/Visibility/VisionFogPresenter.cs
- path → World &amp; Visibility: Project Grimhold/Assets/Scripts/Gameplay/Visibility/VisionQuery.cs
- path → World &amp; Visibility: Project Grimhold/Docs/Architecture/VisibilityArchitecture.md
- path → Audio: Project Grimhold/Assets/Scripts/Audio/AudioManager.cs
- message-term → Audio: audio
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/InventoryPassiveEffectsTests.cs
- path → Documentation: Project Grimhold/Docs/Architecture/VisibilityArchitecture.md
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/InventoryPassiveEffectsTests.cs
- message-term → Tests: Tests

#### Diagnostics

- No classification ambiguity detected.

### 177ac662f35edbb3712ecec5dcc239678c023f31

[177ac662f35edbb3712ecec5dcc239678c023f31](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/177ac662f35edbb3712ecec5dcc239678c023f31)

Date: 2026-11-03T14:54:39-03:00 · Classification: Bug Fixes (explicit) · Systems: Abilities, Inventory & Loot, UI, World & Visibility, Art & Animation, Build & Tooling, Documentation, Tests

Flags: primary or unclassified

#### Original subject

```text
fix(doors): finalize north-only flow and add inventory drafts
```

#### Original body

```text
Use the approved edge-on artwork and a single north opening animation.
Remove obsolete south clips and states while retaining source sheets.

Allow closing from either side at boundary contact, but reject real
doorway occupancy. Share availability with prompt selection so blocked
closing is not advertised. Preserve replicated state and timing.

Add three standalone Charcoal inventory layout prototypes, their
tradeoff and provenance documentation, and a design manifest. Record the
game-asset, game UI/UX and uGUI skill sources in the lock file. These
prototypes are illustrative and are not integrated with gameplay.

Validation:
- 113 relevant Unity EditMode tests passed.
- 31 door and vision PlayMode tests passed.
- Prototype JSON parsed; three inline scripts passed node --check.
No fresh browser, Host/Client, migration or player build validation.

Player-Improved: Doors now open consistently toward the north.
Player-Fixed: Doors close from either side when the doorway is clear.
Player-Improved: The close prompt appears only when closing is available.
```

#### Changed paths

```text
Project Grimhold/Assets/Animations/Doors/Door_Closing_South.anim
Project Grimhold/Assets/Animations/Doors/Door_Closing_South.anim.meta
Project Grimhold/Assets/Animations/Doors/Door_Open_South.anim
Project Grimhold/Assets/Animations/Doors/Door_Open_South.anim.meta
Project Grimhold/Assets/Animations/Doors/Door_Opening_South.anim
Project Grimhold/Assets/Animations/Doors/Door_Opening_South.anim.meta
Project Grimhold/Assets/Animations/Doors/DungeonDoor.controller
Project Grimhold/Assets/Art/Props/Doors/Door-Open-Sheet.png
Project Grimhold/Assets/Art/Props/Doors/Door-Open-South-Sheet.png
Project Grimhold/Assets/Scripts/Scenario/Doors/DungeonDoorPresenter.cs
Project Grimhold/Assets/Scripts/Scenario/Doors/NetworkDoor.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorAnimationTests.cs
Project Grimhold/Assets/Tests/PlayMode/DungeonDoorPlayModeTests.cs
Project Grimhold/Docs/Architecture/PlayerInteractionArchitecture.md
skills-lock.json
```

#### Classification evidence

- type → Bug Fixes: fix
- path → Abilities: skills-lock.json
- path → Inventory &amp; Loot: Project Grimhold/Docs/Architecture/PlayerInteractionArchitecture.md
- message-term → Inventory &amp; Loot: inventory
- message-term → UI: UI
- path → World &amp; Visibility: Project Grimhold/Assets/Animations/Doors/DungeonDoor.controller
- path → World &amp; Visibility: Project Grimhold/Assets/Scripts/Scenario/Doors/DungeonDoorPresenter.cs
- path → World &amp; Visibility: Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorAnimationTests.cs
- path → World &amp; Visibility: Project Grimhold/Assets/Tests/PlayMode/DungeonDoorPlayModeTests.cs
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Doors/Door\_Closing\_South.anim
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Doors/Door\_Closing\_South.anim.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Doors/Door\_Open\_South.anim
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Doors/Door\_Open\_South.anim.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Doors/Door\_Opening\_South.anim
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Doors/Door\_Opening\_South.anim.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Doors/DungeonDoor.controller
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Props/Doors/Door-Open-Sheet.png
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Props/Doors/Door-Open-South-Sheet.png
- path → Art &amp; Animation: Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorAnimationTests.cs
- message-term → Art &amp; Animation: animation
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorAnimationTests.cs
- path → Build &amp; Tooling: skills-lock.json
- path → Documentation: Project Grimhold/Docs/Architecture/PlayerInteractionArchitecture.md
- message-term → Documentation: documentation
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorAnimationTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/DungeonDoorPlayModeTests.cs
- message-term → Tests: tests

#### Diagnostics

- No classification ambiguity detected.

### 136f0f48c582b77881ba017a5ce0d7db0aa485da

[136f0f48c582b77881ba017a5ce0d7db0aa485da](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/136f0f48c582b77881ba017a5ce0d7db0aa485da)

Date: 2026-11-03T11:50:53-03:00 · Classification: Maintenance (explicit) · Systems: Abilities, UI, Art & Animation, Build & Tooling

Flags: secondary

#### Original subject

```text
chore: add game UI/UX and Unity uGUI skills
```

#### Original body

```text

```

#### Changed paths

```text
.agents/skills/create-game-assets/SKILL.md
.agents/skills/create-game-assets/agents/openai.yaml
.agents/skills/create-game-assets/assets/art-direction-brief.md
.agents/skills/create-game-assets/assets/asset-manifest.json
.agents/skills/create-game-assets/references/art-direction.md
.agents/skills/create-game-assets/references/provenance.md
.agents/skills/create-game-assets/references/raster-pipeline.md
.agents/skills/create-game-assets/references/three-d-pipeline.md
.agents/skills/create-game-assets/scripts/asset_report.py
.agents/skills/create-game-assets/scripts/build_preview_sheet.py
.agents/skills/create-game-assets/scripts/requirements.txt
.agents/skills/game-ui-ux/SKILL.md
.agents/skills/game-ui-ux/references/layout-and-flow.md
.agents/skills/ui-ugui/SKILL.md
.agents/skills/ui-ugui/references/scrollview-setup.md
```

#### Classification evidence

- type → Maintenance: chore
- path → Abilities: .agents/skills/create-game-assets/SKILL.md
- path → Abilities: .agents/skills/create-game-assets/agents/openai.yaml
- path → Abilities: .agents/skills/create-game-assets/assets/art-direction-brief.md
- path → Abilities: .agents/skills/create-game-assets/assets/asset-manifest.json
- path → Abilities: .agents/skills/create-game-assets/references/art-direction.md
- path → Abilities: .agents/skills/create-game-assets/references/provenance.md
- path → Abilities: .agents/skills/create-game-assets/references/raster-pipeline.md
- path → Abilities: .agents/skills/create-game-assets/references/three-d-pipeline.md
- path → Abilities: .agents/skills/create-game-assets/scripts/asset\_report.py
- path → Abilities: .agents/skills/create-game-assets/scripts/build\_preview\_sheet.py
- path → Abilities: .agents/skills/create-game-assets/scripts/requirements.txt
- path → Abilities: .agents/skills/game-ui-ux/SKILL.md
- path → Abilities: .agents/skills/game-ui-ux/references/layout-and-flow.md
- path → Abilities: .agents/skills/ui-ugui/SKILL.md
- path → Abilities: .agents/skills/ui-ugui/references/scrollview-setup.md
- path → UI: .agents/skills/game-ui-ux/SKILL.md
- path → UI: .agents/skills/game-ui-ux/references/layout-and-flow.md
- path → UI: .agents/skills/ui-ugui/SKILL.md
- path → UI: .agents/skills/ui-ugui/references/scrollview-setup.md
- message-term → UI: UI
- path → Art &amp; Animation: .agents/skills/create-game-assets/assets/art-direction-brief.md
- path → Art &amp; Animation: .agents/skills/create-game-assets/references/art-direction.md
- path → Build &amp; Tooling: .agents/skills/create-game-assets/SKILL.md
- path → Build &amp; Tooling: .agents/skills/create-game-assets/agents/openai.yaml
- path → Build &amp; Tooling: .agents/skills/create-game-assets/assets/art-direction-brief.md
- path → Build &amp; Tooling: .agents/skills/create-game-assets/assets/asset-manifest.json
- path → Build &amp; Tooling: .agents/skills/create-game-assets/references/art-direction.md
- path → Build &amp; Tooling: .agents/skills/create-game-assets/references/provenance.md
- path → Build &amp; Tooling: .agents/skills/create-game-assets/references/raster-pipeline.md
- path → Build &amp; Tooling: .agents/skills/create-game-assets/references/three-d-pipeline.md
- path → Build &amp; Tooling: .agents/skills/create-game-assets/scripts/asset\_report.py
- path → Build &amp; Tooling: .agents/skills/create-game-assets/scripts/build\_preview\_sheet.py
- path → Build &amp; Tooling: .agents/skills/create-game-assets/scripts/requirements.txt
- path → Build &amp; Tooling: .agents/skills/game-ui-ux/SKILL.md
- path → Build &amp; Tooling: .agents/skills/game-ui-ux/references/layout-and-flow.md
- path → Build &amp; Tooling: .agents/skills/ui-ugui/SKILL.md
- path → Build &amp; Tooling: .agents/skills/ui-ugui/references/scrollview-setup.md

#### Diagnostics

- No classification ambiguity detected.

### 0b4131a1a38043f4da8f8f5a3cd685a3d8ad41d7

[0b4131a1a38043f4da8f8f5a3cd685a3d8ad41d7](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/0b4131a1a38043f4da8f8f5a3cd685a3d8ad41d7)

Date: 2026-11-02T22:33:57-03:00 · Classification: Features (explicit) · Systems: Inventory & Loot, World & Visibility, Art & Animation, Documentation

Flags: primary or unclassified

#### Original subject

```text
feat(doors): use hand-drawn south swing frames
```

#### Original body

```text
Replaces the generated intermediate frames of the south-opening door
with hand-drawn ones: the leaf now swings smoothly toward the camera
from a fixed hinge instead of showing the column-shift steps. The
closed and fully open frames, sprite rects, pivots and clips are
unchanged, so the existing South clips pick the new frames up by
sprite reference.

Docs: PlayerInteractionArchitecture.md door sheet description.

Player-Improved: Doors opening toward you now swing smoothly from their hinge.
Refs: TASK-276
```

#### Changed paths

```text
Project Grimhold/Assets/Art/Props/Doors/Door-Open-South-Sheet.png
Project Grimhold/Docs/Architecture/PlayerInteractionArchitecture.md
odd/tasks/dungeon-doors.md
```

#### Classification evidence

- type → Features: feat
- path → Inventory &amp; Loot: Project Grimhold/Docs/Architecture/PlayerInteractionArchitecture.md
- path → World &amp; Visibility: odd/tasks/dungeon-doors.md
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Props/Doors/Door-Open-South-Sheet.png
- message-term → Art &amp; Animation: sprite
- path → Documentation: Project Grimhold/Docs/Architecture/PlayerInteractionArchitecture.md

#### Diagnostics

- No classification ambiguity detected.

### 3391260959169b5d6dc9d80ad819e28124a19bbc

[3391260959169b5d6dc9d80ad819e28124a19bbc](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/3391260959169b5d6dc9d80ad819e28124a19bbc)

Date: 2026-11-02T21:32:49-03:00 · Classification: Bug Fixes (explicit) · Systems: Inventory & Loot, World & Visibility, Art & Animation, Build & Tooling, Documentation, Tests

Flags: primary or unclassified

#### Original subject

```text
fix(doors): hang the fully open south door from its hinge
```

#### Original body

```text
The previous south frame flipped the edge-on strip within its own rows,
so the leaf stayed north of the wall line with only its tip turned. The
south frame 0 is now the north edge-on strip mirrored around the hinge
row at the wall base: the north leaf rises from the hinge and the south
leaf hangs from it toward the camera with the same length.

Tests:
- DungeonDoorAnimationTests: south open frame is the north strip
  mirrored around the wall-base hinge row.

Docs: PlayerInteractionArchitecture.md provisional south sheet note.

Player-Fixed: Doors opened from the north now hang open toward the south from their hinge.
Refs: TASK-276
```

#### Changed paths

```text
Project Grimhold/Assets/Art/Props/Doors/Door-Open-South-Sheet.png
Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorAnimationTests.cs
Project Grimhold/Docs/Architecture/PlayerInteractionArchitecture.md
odd/tasks/dungeon-doors.md
```

#### Classification evidence

- type → Bug Fixes: fix
- path → Inventory &amp; Loot: Project Grimhold/Docs/Architecture/PlayerInteractionArchitecture.md
- path → World &amp; Visibility: Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorAnimationTests.cs
- path → World &amp; Visibility: odd/tasks/dungeon-doors.md
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Props/Doors/Door-Open-South-Sheet.png
- path → Art &amp; Animation: Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorAnimationTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorAnimationTests.cs
- path → Documentation: Project Grimhold/Docs/Architecture/PlayerInteractionArchitecture.md
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorAnimationTests.cs
- message-term → Tests: Tests

#### Diagnostics

- No classification ambiguity detected.

### 93bfc02f7feb11599245c4e80e24f61ccf444ef1

[93bfc02f7feb11599245c4e80e24f61ccf444ef1](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/93bfc02f7feb11599245c4e80e24f61ccf444ef1)

Date: 2026-11-02T21:12:33-03:00 · Classification: Bug Fixes (explicit) · Systems: Inventory & Loot, World & Visibility, Art & Animation, Build & Tooling, Documentation, Tests

Flags: primary or unclassified

#### Original subject

```text
fix(doors): point the fully open south door toward the south
```

#### Original body

```text
The fully open south frame was identical to the north one, so doors
looked like they always opened to the same side. The south sheet's
frame 0 is now the north edge-on strip flipped vertically within its
own rows: the tip points south while the strip keeps the same
footprint on the wall base and the same hinge column.

Tests:
- DungeonDoorAnimationTests: south open frame equals the vertically
  mirrored north strip with an identical footprint.

Docs: PlayerInteractionArchitecture.md provisional south sheet note.

Player-Fixed: Doors opened from the north now visibly end open toward the south.
Refs: TASK-276
```

#### Changed paths

```text
Project Grimhold/Assets/Art/Props/Doors/Door-Open-South-Sheet.png
Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorAnimationTests.cs
Project Grimhold/Docs/Architecture/PlayerInteractionArchitecture.md
odd/tasks/dungeon-doors.md
```

#### Classification evidence

- type → Bug Fixes: fix
- path → Inventory &amp; Loot: Project Grimhold/Docs/Architecture/PlayerInteractionArchitecture.md
- path → World &amp; Visibility: Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorAnimationTests.cs
- path → World &amp; Visibility: odd/tasks/dungeon-doors.md
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Props/Doors/Door-Open-South-Sheet.png
- path → Art &amp; Animation: Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorAnimationTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorAnimationTests.cs
- path → Documentation: Project Grimhold/Docs/Architecture/PlayerInteractionArchitecture.md
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorAnimationTests.cs
- message-term → Tests: Tests

#### Diagnostics

- No classification ambiguity detected.

### 60008f1041b10bfb05cdf60267f852c2cbb87a48

[60008f1041b10bfb05cdf60267f852c2cbb87a48](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/60008f1041b10bfb05cdf60267f852c2cbb87a48)

Date: 2026-11-02T15:34:47-03:00 · Classification: Bug Fixes (explicit) · Systems: Inventory & Loot, World & Visibility, Art & Animation, Build & Tooling, Documentation, Tests

Flags: primary or unclassified

#### Original subject

```text
fix(doors): keep the south-open door hinged at the wall
```

#### Original body

```text
The fully open south frame shifted the edge-on strip below the wall
base, so the door looked like it slid down instead of turning on its
hinge. The south sheet now reuses the north edge-on strip for frame 0,
keeping the hinge on the wall base in both directions; the opening
direction still reads from the intermediate frames and the sorting
order.

Tests:
- DungeonDoorAnimationTests: south open frame matches the north strip
  and stays anchored at the wall base.

Docs: PlayerInteractionArchitecture.md provisional south sheet note.

Player-Fixed: Doors opening toward you now swing from their hinge instead of sliding down.
Refs: TASK-276
```

#### Changed paths

```text
Project Grimhold/Assets/Art/Props/Doors/Door-Open-South-Sheet.png
Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorAnimationTests.cs
Project Grimhold/Docs/Architecture/PlayerInteractionArchitecture.md
odd/tasks/dungeon-doors.md
```

#### Classification evidence

- type → Bug Fixes: fix
- path → Inventory &amp; Loot: Project Grimhold/Docs/Architecture/PlayerInteractionArchitecture.md
- path → World &amp; Visibility: Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorAnimationTests.cs
- path → World &amp; Visibility: odd/tasks/dungeon-doors.md
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Props/Doors/Door-Open-South-Sheet.png
- path → Art &amp; Animation: Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorAnimationTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorAnimationTests.cs
- path → Documentation: Project Grimhold/Docs/Architecture/PlayerInteractionArchitecture.md
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorAnimationTests.cs
- message-term → Tests: Tests

#### Diagnostics

- No classification ambiguity detected.

### 320c1cd3027e190dfc81eed063e67be512638444

[320c1cd3027e190dfc81eed063e67be512638444](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/320c1cd3027e190dfc81eed063e67be512638444)

Date: 2026-11-02T15:18:07-03:00 · Classification: Features (explicit) · Systems: UI, World & Visibility, Tests

Flags: primary or unclassified

#### Original subject

```text
feat(level): place a test door in the Gameplay dungeon
```

#### Original body

```text
Places one DungeonDoor scene instance in the southern passage of the
southwest spawn room so doors can be validated in a real raid. The
passage was 3 u wide, so two Walls cells narrow only that opening to
2 u for the native-size door; final door placement belongs to Level
Design.

- Gameplay.unity: Dungeon_Graybox/Walls cells (-12,-16) and (-11,-16);
  DungeonDoor at (-9.02, -24.86). The large diff is tilemap and
  collider cache reindexing.
- DungeonGrayboxMinimapLayout.asset re-baked from the updated walls.
- odd/tasks/dungeon-doors.md: ODD execution tracker for TASK-276.

Refs: TASK-276
```

#### Changed paths

```text
Project Grimhold/Assets/Data/Minimap/DungeonGrayboxMinimapLayout.asset
Project Grimhold/Assets/Scenes/Gameplay.unity
odd/tasks/dungeon-doors.md
```

#### Classification evidence

- type → Features: feat
- path → UI: Project Grimhold/Assets/Data/Minimap/DungeonGrayboxMinimapLayout.asset
- path → World &amp; Visibility: Project Grimhold/Assets/Data/Minimap/DungeonGrayboxMinimapLayout.asset
- path → World &amp; Visibility: odd/tasks/dungeon-doors.md
- message-term → World &amp; Visibility: dungeon
- message-term → Tests: test

#### Diagnostics

- No classification ambiguity detected.

### 74792e41ac23c03c157d8a5a40256e9761a4a316

[74792e41ac23c03c157d8a5a40256e9761a4a316](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/74792e41ac23c03c157d8a5a40256e9761a4a316)

Date: 2026-11-02T15:17:57-03:00 · Classification: Features (explicit) · Systems: World & Visibility, Build & Tooling, Documentation, Tests

Flags: primary or unclassified

#### Original subject

```text
feat(rendering): Y-sort characters against doors
```

#### Original body

```text
Characters and doors now sort by world Y instead of characters always
drawing over the door: a character north of a door base is drawn behind
it and one south of it in front.

- Renderer2D transparency sort mode: Custom Axis (0, 1, 0).
- Root SortingGroup (Characters/0, sortAtRoot off) on NetworkPlayer and
  NetworkEnemy (inherited by enemy variants); their particle children
  keep their own layers through sortAtRoot groups.
- TrainingDummy moves to Characters/0 with pivot sorting.
- The door SortAnchor is raised by the player root-to-feet offset
  (0.6875 u). Enemies with other root offsets keep small approximate
  bands (base 0.6875 u, ranged 0.25 u, slimes 0.1875 u). Walls are
  unchanged (Default/1).

Tests:
- DungeonDoorSortingTests: renderer sort axis, character groups,
  door anchor calibration against the measured player feet, door
  geometry and two-sided trigger reach.

Docs: VisibilityArchitecture.md rendering section (also corrects the
walls' sorting layer to Default/1).

Player-Improved: Characters now appear behind or in front of doors depending on where they stand.
Refs: TASK-276
```

#### Changed paths

```text
Project Grimhold/Assets/Prefabs/Enemies/NetworkEnemy.prefab
Project Grimhold/Assets/Prefabs/NetworkPlayer.prefab
Project Grimhold/Assets/Prefabs/Sandbox/TrainingDummy.prefab
Project Grimhold/Assets/Settings/Renderer2D.asset
Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/DungeonDoorSortingTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/DungeonDoorSortingTests.cs.meta
Project Grimhold/Docs/Architecture/VisibilityArchitecture.md
```

#### Classification evidence

- type → Features: feat
- path → World &amp; Visibility: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/DungeonDoorSortingTests.cs
- path → World &amp; Visibility: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/DungeonDoorSortingTests.cs.meta
- path → World &amp; Visibility: Project Grimhold/Docs/Architecture/VisibilityArchitecture.md
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/DungeonDoorSortingTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/DungeonDoorSortingTests.cs.meta
- path → Documentation: Project Grimhold/Docs/Architecture/VisibilityArchitecture.md
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/DungeonDoorSortingTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/DungeonDoorSortingTests.cs.meta
- message-term → Tests: Tests

#### Diagnostics

- No classification ambiguity detected.

### 9187b3537b3e31bdb1a31cfbbc3ff6197b0f5d13

[9187b3537b3e31bdb1a31cfbbc3ff6197b0f5d13](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/9187b3537b3e31bdb1a31cfbbc3ff6197b0f5d13)

Date: 2026-11-02T15:17:47-03:00 · Classification: Features (explicit) · Systems: Combat, Movement, Inventory & Loot, World & Visibility, Audio, Art & Animation, Build & Tooling, Documentation, Tests

Flags: primary or unclassified

#### Original subject

```text
feat(doors): add networked dungeon doors
```

#### Original body

```text
Adds the TASK-276 Dungeon door: a State Authority NetworkDoor that
players open and close with the contextual interaction, animated from
the delivered Catacumbas door sheet and blocking movement, projectiles,
vision and enemy navigation while closed.

- NetworkDoor (IInteractable): replicated State (Closed, Opening, Open,
  Closing), TransitionTimer (0.4 s) and OpenDirection. Only stable
  states accept interaction; presses during transitions are ignored and
  closing is rejected while a Character/Enemies body overlaps the
  doorway. The door opens away from the interactor (interactor north of
  the door -> South). Restore spawns keep the copied state.
- One non-trigger blocker on WorldCollision (2.125 x 1.0 u, same depth
  as the adjacent walls) drives movement, projectile and vision
  occlusion and refreshes the pathfinding region on State Authority.
  Only the dedicated Interactable trigger is registered in the
  EntityRegistry.
- Prompts "[F] Abrir puerta" / "[F] Cerrar puerta" through a door branch
  in LocalInteractionCandidateSource.
- DungeonDoorPresenter maps the replicated state to DungeonDoor.controller
  (7 sprite clips, transitions 0.4 s) in Render only, adopting the timer
  progress on late join/restore, and plays placeholder open/close audio.
- Door sprites imported at PPU 16, Point, uncompressed, shared pivot at
  the door base. Door-Open-South-Sheet.png is a provisional south-opening
  variant derived from the north sheet (hinge anchored at the wall) for
  Art to retouch.
- DungeonDoor.prefab: NetworkObject, Blocker, InteractionTrigger and a
  SortAnchor SortingGroup raised by the player root-to-feet offset.

Tests:
- DungeonDoorStateRulesTests, DungeonDoorOpenDirectionRulesTests,
  DungeonDoorAnimationTests (EditMode).
- DungeonDoorPlayModeTests: passage, projectile and vision occlusion,
  transitions, occupied-doorway rejection, navigation refresh,
  direction and Animator states.

Docs: PlayerInteractionArchitecture.md TASK-276 section.

Player-Added: Dungeon doors can be opened and closed with F; closed doors block movement, attacks and sight, and open away from you.
Refs: TASK-276
```

#### Changed paths

```text
Project Grimhold/Assets/Animations/Doors.meta
Project Grimhold/Assets/Animations/Doors/Door_Closed.anim
Project Grimhold/Assets/Animations/Doors/Door_Closed.anim.meta
Project Grimhold/Assets/Animations/Doors/Door_Closing_North.anim
Project Grimhold/Assets/Animations/Doors/Door_Closing_North.anim.meta
Project Grimhold/Assets/Animations/Doors/Door_Closing_South.anim
Project Grimhold/Assets/Animations/Doors/Door_Closing_South.anim.meta
Project Grimhold/Assets/Animations/Doors/Door_Open_North.anim
Project Grimhold/Assets/Animations/Doors/Door_Open_North.anim.meta
Project Grimhold/Assets/Animations/Doors/Door_Open_South.anim
Project Grimhold/Assets/Animations/Doors/Door_Open_South.anim.meta
Project Grimhold/Assets/Animations/Doors/Door_Opening_North.anim
Project Grimhold/Assets/Animations/Doors/Door_Opening_North.anim.meta
Project Grimhold/Assets/Animations/Doors/Door_Opening_South.anim
Project Grimhold/Assets/Animations/Doors/Door_Opening_South.anim.meta
Project Grimhold/Assets/Animations/Doors/DungeonDoor.controller
Project Grimhold/Assets/Animations/Doors/DungeonDoor.controller.meta
Project Grimhold/Assets/Art/Props/Doors.meta
Project Grimhold/Assets/Art/Props/Doors/Door-Open-Sheet.png
Project Grimhold/Assets/Art/Props/Doors/Door-Open-Sheet.png.meta
Project Grimhold/Assets/Art/Props/Doors/Door-Open-South-Sheet.png
Project Grimhold/Assets/Art/Props/Doors/Door-Open-South-Sheet.png.meta
Project Grimhold/Assets/Art/Props/Doors/Door-Side.png
Project Grimhold/Assets/Art/Props/Doors/Door-Side.png.meta
Project Grimhold/Assets/Prefabs/Doors.meta
Project Grimhold/Assets/Prefabs/Doors/DungeonDoor.prefab
Project Grimhold/Assets/Prefabs/Doors/DungeonDoor.prefab.meta
Project Grimhold/Assets/Scripts/Player/Presentation/LocalInteractionCandidateSource.cs
Project Grimhold/Assets/Scripts/Scenario/Doors.meta
Project Grimhold/Assets/Scripts/Scenario/Doors/DungeonDoorOpenDirection.cs
Project Grimhold/Assets/Scripts/Scenario/Doors/DungeonDoorOpenDirection.cs.meta
Project Grimhold/Assets/Scripts/Scenario/Doors/DungeonDoorPresenter.cs
Project Grimhold/Assets/Scripts/Scenario/Doors/DungeonDoorPresenter.cs.meta
Project Grimhold/Assets/Scripts/Scenario/Doors/DungeonDoorState.cs
Project Grimhold/Assets/Scripts/Scenario/Doors/DungeonDoorState.cs.meta
Project Grimhold/Assets/Scripts/Scenario/Doors/DungeonDoorStateRules.cs
Project Grimhold/Assets/Scripts/Scenario/Doors/DungeonDoorStateRules.cs.meta
Project Grimhold/Assets/Scripts/Scenario/Doors/NetworkDoor.cs
Project Grimhold/Assets/Scripts/Scenario/Doors/NetworkDoor.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorAnimationTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorAnimationTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorOpenDirectionRulesTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorOpenDirectionRulesTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorStateRulesTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorStateRulesTests.cs.meta
Project Grimhold/Assets/Tests/PlayMode/DungeonDoorPlayModeTests.cs
Project Grimhold/Assets/Tests/PlayMode/DungeonDoorPlayModeTests.cs.meta
Project Grimhold/Docs/Architecture/PlayerInteractionArchitecture.md
```

#### Classification evidence

- type → Features: feat
- message-term → Combat: projectiles
- message-term → Movement: movement
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/LocalInteractionCandidateSource.cs
- path → Inventory &amp; Loot: Project Grimhold/Docs/Architecture/PlayerInteractionArchitecture.md
- message-term → Inventory &amp; Loot: interaction
- path → World &amp; Visibility: Project Grimhold/Assets/Animations/Doors/DungeonDoor.controller
- path → World &amp; Visibility: Project Grimhold/Assets/Animations/Doors/DungeonDoor.controller.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Prefabs/Doors/DungeonDoor.prefab
- path → World &amp; Visibility: Project Grimhold/Assets/Prefabs/Doors/DungeonDoor.prefab.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Scripts/Scenario/Doors/DungeonDoorOpenDirection.cs
- path → World &amp; Visibility: Project Grimhold/Assets/Scripts/Scenario/Doors/DungeonDoorOpenDirection.cs.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Scripts/Scenario/Doors/DungeonDoorPresenter.cs
- path → World &amp; Visibility: Project Grimhold/Assets/Scripts/Scenario/Doors/DungeonDoorPresenter.cs.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Scripts/Scenario/Doors/DungeonDoorState.cs
- path → World &amp; Visibility: Project Grimhold/Assets/Scripts/Scenario/Doors/DungeonDoorState.cs.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Scripts/Scenario/Doors/DungeonDoorStateRules.cs
- path → World &amp; Visibility: Project Grimhold/Assets/Scripts/Scenario/Doors/DungeonDoorStateRules.cs.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorAnimationTests.cs
- path → World &amp; Visibility: Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorAnimationTests.cs.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorOpenDirectionRulesTests.cs
- path → World &amp; Visibility: Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorOpenDirectionRulesTests.cs.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorStateRulesTests.cs
- path → World &amp; Visibility: Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorStateRulesTests.cs.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Tests/PlayMode/DungeonDoorPlayModeTests.cs
- path → World &amp; Visibility: Project Grimhold/Assets/Tests/PlayMode/DungeonDoorPlayModeTests.cs.meta
- message-term → World &amp; Visibility: dungeon
- message-term → Audio: audio
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Doors.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Doors/Door\_Closed.anim
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Doors/Door\_Closed.anim.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Doors/Door\_Closing\_North.anim
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Doors/Door\_Closing\_North.anim.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Doors/Door\_Closing\_South.anim
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Doors/Door\_Closing\_South.anim.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Doors/Door\_Open\_North.anim
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Doors/Door\_Open\_North.anim.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Doors/Door\_Open\_South.anim
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Doors/Door\_Open\_South.anim.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Doors/Door\_Opening\_North.anim
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Doors/Door\_Opening\_North.anim.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Doors/Door\_Opening\_South.anim
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Doors/Door\_Opening\_South.anim.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Doors/DungeonDoor.controller
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Doors/DungeonDoor.controller.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Props/Doors.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Props/Doors/Door-Open-Sheet.png
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Props/Doors/Door-Open-Sheet.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Props/Doors/Door-Open-South-Sheet.png
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Props/Doors/Door-Open-South-Sheet.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Props/Doors/Door-Side.png
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Props/Doors/Door-Side.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorAnimationTests.cs
- path → Art &amp; Animation: Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorAnimationTests.cs.meta
- message-term → Art &amp; Animation: sprite
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorAnimationTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorAnimationTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorOpenDirectionRulesTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorOpenDirectionRulesTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorStateRulesTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorStateRulesTests.cs.meta
- path → Documentation: Project Grimhold/Docs/Architecture/PlayerInteractionArchitecture.md
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorAnimationTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorAnimationTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorOpenDirectionRulesTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorOpenDirectionRulesTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorStateRulesTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorStateRulesTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/DungeonDoorPlayModeTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/DungeonDoorPlayModeTests.cs.meta
- message-term → Tests: Tests

#### Diagnostics

- No classification ambiguity detected.

### 31617bcabdf0c089f83dee1a1af1f902447dce00

[31617bcabdf0c089f83dee1a1af1f902447dce00](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/31617bcabdf0c089f83dee1a1af1f902447dce00)

Date: 2026-11-02T15:17:32-03:00 · Classification: Features (explicit) · Systems: Movement, Build & Tooling, Tests

Flags: primary or unclassified

#### Original subject

```text
feat(pathfinding): refresh navigation grid regions at runtime
```

#### Original body

```text
Runtime obstacles such as doors toggle their colliders after the grid
is built, so enemies kept pathing through closed doorways. The grid can
now re-evaluate only the nodes whose inflated detection box overlaps a
given world region, reusing the build-time obstacle query.

- PathfindingGrid.RefreshRegion(Bounds) is ignored before Build and
  leaves nodes outside the region untouched.
- PathNode.IsWalkable doc updated: Build or RefreshRegion only.

Tests:
- PathfindingGridRefreshRegionTests (disable/re-enable blocker,
  outside-region nodes untouched, ignored before build).
```

#### Changed paths

```text
Project Grimhold/Assets/Scripts/Pathfinding/PathNode.cs
Project Grimhold/Assets/Scripts/Pathfinding/PathfindingGrid.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Movement/PathfindingGridRefreshRegionTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Movement/PathfindingGridRefreshRegionTests.cs.meta
```

#### Classification evidence

- type → Features: feat
- path → Movement: Project Grimhold/Assets/Tests/EditMode/Editor/Movement/PathfindingGridRefreshRegionTests.cs
- path → Movement: Project Grimhold/Assets/Tests/EditMode/Editor/Movement/PathfindingGridRefreshRegionTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Movement/PathfindingGridRefreshRegionTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Movement/PathfindingGridRefreshRegionTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Movement/PathfindingGridRefreshRegionTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Movement/PathfindingGridRefreshRegionTests.cs.meta
- message-term → Tests: Tests

#### Diagnostics

- No classification ambiguity detected.

### f2062e7cc462fb019b9017ef5e8f52366ded3f63

[f2062e7cc462fb019b9017ef5e8f52366ded3f63](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/f2062e7cc462fb019b9017ef5e8f52366ded3f63)

Date: 2026-11-02T12:39:44-03:00 · Classification: Other Changes (unknown) · Systems: Abilities, Build & Tooling

Flags: primary or unclassified

#### Original subject

```text
Align task workflow with ODD execution
```

#### Original body

```text
Update repository guidance to use ODD classification and execution trackers for substantial tasks, while preserving HacknPlan as the scope source of truth. Refresh subagent routing and Unity operator model settings.
```

#### Changed paths

```text
.agents/skills/grimhold-task-execution/SKILL.md
.agents/skills/grimhold-task-execution/references/tool-routing.md
.agents/skills/hacknplan-task-design/references/workflow.md
.pi/subagents.json
AGENTS.md
```

#### Classification evidence

- path → Abilities: .agents/skills/grimhold-task-execution/SKILL.md
- path → Abilities: .agents/skills/grimhold-task-execution/references/tool-routing.md
- path → Abilities: .agents/skills/hacknplan-task-design/references/workflow.md
- path → Build &amp; Tooling: .agents/skills/grimhold-task-execution/SKILL.md
- path → Build &amp; Tooling: .agents/skills/grimhold-task-execution/references/tool-routing.md
- path → Build &amp; Tooling: .agents/skills/hacknplan-task-design/references/workflow.md
- path → Build &amp; Tooling: AGENTS.md

#### Diagnostics

- **ambiguous-kind:** No supported explicit type or anchored historical action. Inspect this commit diff; use an action and object or a Conventional Commit type in future messages.

### cdd4716869d658b437f2c1c738cefbe2b2809e90

[cdd4716869d658b437f2c1c738cefbe2b2809e90](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/cdd4716869d658b437f2c1c738cefbe2b2809e90)

Date: 2026-11-01T21:38:06-03:00 · Classification: Features (explicit) · Systems: Combat, Build & Tooling, Documentation, Tests

Flags: primary or unclassified

#### Original subject

```text
feat(combat): credit knockback pushers for hazard-caused Downed
```

#### Original body

```text
Track indirect responsibility when a knockback moves a player into an
environmental hazard (GD 18 6.5, GD 19 8). If the hazard's damage
causes Active -> Downed while the causal chain holds, the enemy pusher
receives the provisional PvP Last Hit; the direct damage source stays
the environment (DownedCauserEntityId = 0). Ally pushes are recorded but
never credited, and recent enemy damage does not inherit the credit.

Causal register contract (GD 19 10 left it to implementation):
- PlayerHazardExposureCausalityNetworkController on NetworkPlayer holds
  a replicated HazardExposureCausalityState mutated on State Authority
  through pure HazardExposureCausalityRules.
- DamageResolver records the player pusher of each Started/Replaced
  knockback; non-player displacements discard the pending cause.
- Hazards report presence every authority tick (BaseTrap.OnAuthorityTick,
  SpikesTrap) and tag damage with DamageRequest.EnvironmentalHazardId.
- The link persists while exposure continues, with one tick of ordering
  grace; it breaks on exit, on a later push into another hazard, and
  while Downed/defeated. Breaks are audited with hazard, pusher,
  knockback sequence, reason and tick.
- TryEnterDowned(attackerId, creditCauserId) separates the direct source
  from the credit causer passed to IDownedCycleObserver.

Tests:
- HazardExposureCausalityRulesTests: 13 EditMode cases for GD 19 T06
  continuity, rupture, voluntary entry, overlapping hazards and resets.
- PvpCombatCreditPlayModeTests: T06-01, T06-04/05 and T06-09 end to end.

Docs: DownedAndReviveArchitecture 11.1, EnvironmentalHazardsArchitecture,
PlayerCombatArchitecture.

Player-Added: Knocking an enemy into a trap now counts as your knockdown when the trap's damage takes them down.
Refs: TASK-247
```

#### Changed paths

```text
Project Grimhold/Assets/Prefabs/NetworkPlayer.prefab
Project Grimhold/Assets/Scripts/Combat/DamageResolver.cs
Project Grimhold/Assets/Scripts/Combat/HazardCausality.meta
Project Grimhold/Assets/Scripts/Combat/HazardCausality/HazardCausalityBreakReason.cs
Project Grimhold/Assets/Scripts/Combat/HazardCausality/HazardCausalityBreakReason.cs.meta
Project Grimhold/Assets/Scripts/Combat/HazardCausality/HazardExposureCausalityRules.cs
Project Grimhold/Assets/Scripts/Combat/HazardCausality/HazardExposureCausalityRules.cs.meta
Project Grimhold/Assets/Scripts/Combat/HazardCausality/HazardExposureCausalityState.cs
Project Grimhold/Assets/Scripts/Combat/HazardCausality/HazardExposureCausalityState.cs.meta
Project Grimhold/Assets/Scripts/Core/DamageRequest.cs
Project Grimhold/Assets/Scripts/Core/IDownedCycleObserver.cs
Project Grimhold/Assets/Scripts/Player/PlayerCharacter.cs
Project Grimhold/Assets/Scripts/Player/PlayerDownedStateNetworkController.cs
Project Grimhold/Assets/Scripts/Player/PlayerHazardExposureCausalityNetworkController.cs
Project Grimhold/Assets/Scripts/Player/PlayerHazardExposureCausalityNetworkController.cs.meta
Project Grimhold/Assets/Scripts/Scenario/Traps/BaseTrap.cs
Project Grimhold/Assets/Scripts/Scenario/Traps/SpikesTrap.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Combat/HazardExposureCausalityRulesTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Combat/HazardExposureCausalityRulesTests.cs.meta
Project Grimhold/Assets/Tests/PlayMode/PvpCombatCreditPlayModeTests.cs
Project Grimhold/Docs/Architecture/DownedAndReviveArchitecture.md
Project Grimhold/Docs/Architecture/EnvironmentalHazardsArchitecture.md
Project Grimhold/Docs/Architecture/PlayerCombatArchitecture.md
```

#### Classification evidence

- type → Features: feat
- path → Combat: Project Grimhold/Assets/Scripts/Combat/DamageResolver.cs
- path → Combat: Project Grimhold/Assets/Scripts/Combat/HazardCausality.meta
- path → Combat: Project Grimhold/Assets/Scripts/Combat/HazardCausality/HazardCausalityBreakReason.cs
- path → Combat: Project Grimhold/Assets/Scripts/Combat/HazardCausality/HazardCausalityBreakReason.cs.meta
- path → Combat: Project Grimhold/Assets/Scripts/Combat/HazardCausality/HazardExposureCausalityRules.cs
- path → Combat: Project Grimhold/Assets/Scripts/Combat/HazardCausality/HazardExposureCausalityRules.cs.meta
- path → Combat: Project Grimhold/Assets/Scripts/Combat/HazardCausality/HazardExposureCausalityState.cs
- path → Combat: Project Grimhold/Assets/Scripts/Combat/HazardCausality/HazardExposureCausalityState.cs.meta
- path → Combat: Project Grimhold/Assets/Scripts/Core/DamageRequest.cs
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/HazardExposureCausalityRulesTests.cs
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/HazardExposureCausalityRulesTests.cs.meta
- path → Combat: Project Grimhold/Assets/Tests/PlayMode/PvpCombatCreditPlayModeTests.cs
- path → Combat: Project Grimhold/Docs/Architecture/PlayerCombatArchitecture.md
- scope → Combat: combat
- message-term → Combat: combat
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/HazardExposureCausalityRulesTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/HazardExposureCausalityRulesTests.cs.meta
- path → Documentation: Project Grimhold/Docs/Architecture/DownedAndReviveArchitecture.md
- path → Documentation: Project Grimhold/Docs/Architecture/EnvironmentalHazardsArchitecture.md
- path → Documentation: Project Grimhold/Docs/Architecture/PlayerCombatArchitecture.md
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/HazardExposureCausalityRulesTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/HazardExposureCausalityRulesTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/PvpCombatCreditPlayModeTests.cs
- message-term → Tests: Tests

#### Diagnostics

- No classification ambiguity detected.

### 07f13443b31d2faa85f2c3848c926cdb9056a71e

[07f13443b31d2faa85f2c3848c926cdb9056a71e](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/07f13443b31d2faa85f2c3848c926cdb9056a71e)

Date: 2026-11-01T02:06:11-03:00 · Classification: Features (explicit) · Systems: Inventory & Loot, Networking & Sessions, UI, Art & Animation, Build & Tooling, Documentation, Tests

Flags: primary or unclassified

#### Original subject

```text
feat(loot): add lockpicking and locked chest spawns
```

#### Original body

```text
Implement deterministic lockpick channels and replicated chest locks for
TASK-235. Movement and committed damage cancel without consumption;
success spends one lockpick using existing inventory provenance.

Add the static LockedChest prefab at a 10% chance per existing chest
point. Guarantee the first locked chest for Editor/development testing
without changing release probabilities or total chest counts.

Preserve unlocked state across restoration and safely cancel restored
channels whose target identities have not been remapped. Persist the
accepted existing ambient-particle scene cleanup and configuration.

Cover channel interruption, inventory spending, prefab configuration,
spawn selection and legacy behavior; document the interaction flow.
Latest focused checks: 38/38 EditMode and 19/19 PlayMode passed.
Real Host/Client, migration and manual visual checks remain pending.

Refs: TASK-235
Player-Added: Locked chests can be opened with a lockpick after a short uninterrupted interaction.
```

#### Changed paths

```text
Project Grimhold/Assets/Art/Props/LockedChest.png
Project Grimhold/Assets/Art/Props/LockedChest.png.meta
Project Grimhold/Assets/Prefabs/LockedChest.prefab
Project Grimhold/Assets/Prefabs/LockedChest.prefab.meta
Project Grimhold/Assets/Scenes/Gameplay.unity
Project Grimhold/Assets/Scripts/Core/InteractionResult.cs
Project Grimhold/Assets/Scripts/Gameplay/NetworkLootContainer.cs
Project Grimhold/Assets/Scripts/Gameplay/NetworkLootContainerInteractable.cs
Project Grimhold/Assets/Scripts/Networking/LockedChestSpawnRules.cs
Project Grimhold/Assets/Scripts/Networking/LockedChestSpawnRules.cs.meta
Project Grimhold/Assets/Scripts/Networking/NetworkSpawnManager.cs
Project Grimhold/Assets/Scripts/Player/Interaction/LockpickChannelRules.cs
Project Grimhold/Assets/Scripts/Player/Interaction/LockpickChannelRules.cs.meta
Project Grimhold/Assets/Scripts/Player/Interaction/PlayerInteractionNetworkController.cs
Project Grimhold/Assets/Scripts/Player/PlayerCharacter.cs
Project Grimhold/Assets/Scripts/Player/Presentation/InteractionHudPresenter.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Interaction.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Interaction/LockpickChannelRulesTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Interaction/LockpickChannelRulesTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LockedChestSpawnRulesTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LockedChestSpawnRulesTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LockedLootContainerTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LockedLootContainerTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootContainerPrefabTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/NetworkSpawnManagerLootTests.cs
Project Grimhold/Assets/Tests/PlayMode/LockedLootContainerPlayModeTests.cs
Project Grimhold/Assets/Tests/PlayMode/LockedLootContainerPlayModeTests.cs.meta
Project Grimhold/Assets/Tests/PlayMode/LockpickInteractionPlayModeTests.cs
Project Grimhold/Assets/Tests/PlayMode/LockpickInteractionPlayModeTests.cs.meta
Project Grimhold/Docs/Architecture/PlayerInteractionArchitecture.md
```

#### Classification evidence

- type → Features: feat
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Core/InteractionResult.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/NetworkLootContainer.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/NetworkLootContainerInteractable.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/LockpickChannelRules.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/LockpickChannelRules.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/PlayerInteractionNetworkController.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/InteractionHudPresenter.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Interaction.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Interaction/LockpickChannelRulesTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Interaction/LockpickChannelRulesTests.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LockedChestSpawnRulesTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LockedChestSpawnRulesTests.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LockedLootContainerTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LockedLootContainerTests.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootContainerPrefabTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/NetworkSpawnManagerLootTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/PlayMode/LockedLootContainerPlayModeTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/PlayMode/LockedLootContainerPlayModeTests.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/PlayMode/LockpickInteractionPlayModeTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/PlayMode/LockpickInteractionPlayModeTests.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Docs/Architecture/PlayerInteractionArchitecture.md
- scope → Inventory &amp; Loot: loot
- message-term → Inventory &amp; Loot: loot
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/LockedChestSpawnRules.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/LockedChestSpawnRules.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/NetworkSpawnManager.cs
- path → UI: Project Grimhold/Assets/Scripts/Player/Presentation/InteractionHudPresenter.cs
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Props/LockedChest.png
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Props/LockedChest.png.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Interaction.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Interaction/LockpickChannelRulesTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Interaction/LockpickChannelRulesTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LockedChestSpawnRulesTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LockedChestSpawnRulesTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LockedLootContainerTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LockedLootContainerTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootContainerPrefabTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/NetworkSpawnManagerLootTests.cs
- path → Documentation: Project Grimhold/Docs/Architecture/PlayerInteractionArchitecture.md
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Interaction.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Interaction/LockpickChannelRulesTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Interaction/LockpickChannelRulesTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LockedChestSpawnRulesTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LockedChestSpawnRulesTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LockedLootContainerTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LockedLootContainerTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootContainerPrefabTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/NetworkSpawnManagerLootTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/LockedLootContainerPlayModeTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/LockedLootContainerPlayModeTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/LockpickInteractionPlayModeTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/LockpickInteractionPlayModeTests.cs.meta

#### Diagnostics

- No classification ambiguity detected.

### 15e80657787f2601c3828703ae06c9b189a6a417

[15e80657787f2601c3828703ae06c9b189a6a417](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/15e80657787f2601c3828703ae06c9b189a6a417)

Date: 2026-10-31T22:22:59-03:00 · Classification: Features (explicit) · Systems: Combat, Movement, Inventory & Loot, Networking & Sessions, UI, Input, Build & Tooling, Documentation, Tests

Flags: primary or unclassified

#### Original subject

```text
feat(revive): add self-revive consumable and tap-to-hand-off flow
```

#### Original body

```text
Implement TASK-236 (GD 13 sections 7, 7.1, 8, 10; GD 14 section 11.4).
A Downed player carrying CON_SELF_REVIVE presses Interact to start an
authoritative 5 s Self-revive channel that locks voluntary movement and
pauses the Downed drain. Damage, a displacement that still moves the
avatar or definitive Defeat interrupts it without consuming the item;
disconnect does not. Completion consumes one unit (Town origin first),
restores 25 HP and spends the one Self-revive allowed per expedition.

An Active teammate taps Interact on a Downed ally to hand over their
Self-reviver and holds Interact to revive. The press still opens an
assisted session; a release within 0.3 s converts it into a 1 s handoff
when the giver carries the item and the ally can receive and use it.
The handoff does not pause the drain, transfers the unit atomically
without a world drop and starts the Self-revive automatically.

Reviewers should verify:
- RecoveryKind.Handoff and DownedRecoveryInterruptReason.HandoffRejected
  are appended, so existing byte values are unchanged.
- New networked SelfReviveUsed and LastOutcomeSessionKind are reset in
  Spawned except on Host Migration restore.
- Provisional balance values (25 HP, 5 s, 1 s, 0.3 s) are serialized on
  PlayerDownedRecoveryNetworkController.
- CON_SELF_REVIVE is no longer Quick Slot compatible; stored bindings
  are dropped on load by the existing codec rule.
- PlayerInputReader now transports a held Interact from an accepted
  press until release. Previously only the one-tick press reached
  simulation, so a real hold was read as a release.
- The revive panel bar fill is assigned and active in
  NetworkPlayer.prefab and follows the same progress as the ring.

Tests:
- DownedSelfReviveRulesTests, DownedSelfReviveFeedbackTests (EditMode)
- PlayerSelfRevivePlayModeTests (completion, interruption, quota,
  movement lock, tap handoff, hold revive, rejections)
- PlayerInputReaderPlayModeTests: held Interact transport
- ModularHudFillTests: revive panel bar progress and prefab wiring
- MvpItemCatalogTests: CON_SELF_REVIVE is not Quick Slot compatible

Docs: DownedAndReviveArchitecture section 7.1 and status tables;
RaidMainHudArchitecture revive panel bar note.

Player-Added: Carry a Self-reviver to get back up on your own while downed, or tap the interact key on a downed ally to give them yours.
Player-Fixed: Holding the interact key on a downed ally now keeps reviving them instead of stopping right away, and the revive bar now fills up.
```

#### Changed paths

```text
Project Grimhold/Assets/Prefabs/NetworkPlayer.prefab
Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/CON_SELF_REVIVE.asset
Project Grimhold/Assets/Scripts/Player/DownedRecoveryFeedbackRules.cs
Project Grimhold/Assets/Scripts/Player/DownedRecoveryInterruptReason.cs
Project Grimhold/Assets/Scripts/Player/DownedRecoveryRules.cs
Project Grimhold/Assets/Scripts/Player/Interaction/DownedReviveInteractable.cs
Project Grimhold/Assets/Scripts/Player/Interaction/PlayerInteractionNetworkController.cs
Project Grimhold/Assets/Scripts/Player/Movement/PlayerMovementNetworkController.cs
Project Grimhold/Assets/Scripts/Player/PlayerDownedRecoveryNetworkController.cs
Project Grimhold/Assets/Scripts/Player/PlayerInputReader.cs
Project Grimhold/Assets/Scripts/Player/Presentation/LocalInteractionCandidateSource.cs
Project Grimhold/Assets/Scripts/Player/Presentation/RaidDownedHudPresenter.cs
Project Grimhold/Assets/Scripts/Player/Presentation/RaidDownedHudView.cs
Project Grimhold/Assets/Scripts/Player/RecoveryKind.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/MvpItemCatalogTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Player/DownedSelfReviveFeedbackTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Player/DownedSelfReviveFeedbackTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Player/DownedSelfReviveRulesTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Player/DownedSelfReviveRulesTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/ModularHudFillTests.cs
Project Grimhold/Assets/Tests/PlayMode/Player/PlayerInputReaderPlayModeTests.cs
Project Grimhold/Assets/Tests/PlayMode/PlayerSelfRevivePlayModeTests.cs
Project Grimhold/Assets/Tests/PlayMode/PlayerSelfRevivePlayModeTests.cs.meta
Project Grimhold/Docs/Architecture/DownedAndReviveArchitecture.md
Project Grimhold/Docs/Architecture/RaidMainHudArchitecture.md
```

#### Classification evidence

- type → Features: feat
- message-term → Combat: Damage
- path → Movement: Project Grimhold/Assets/Scripts/Player/Movement/PlayerMovementNetworkController.cs
- message-term → Movement: movement
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/CON\_SELF\_REVIVE.asset
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/DownedReviveInteractable.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/PlayerInteractionNetworkController.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/LocalInteractionCandidateSource.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/MvpItemCatalogTests.cs
- message-term → Networking &amp; Sessions: Host Migration
- path → UI: Project Grimhold/Assets/Scripts/Player/Presentation/RaidDownedHudPresenter.cs
- path → UI: Project Grimhold/Assets/Scripts/Player/Presentation/RaidDownedHudView.cs
- path → UI: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/ModularHudFillTests.cs
- path → UI: Project Grimhold/Docs/Architecture/RaidMainHudArchitecture.md
- path → Input: Project Grimhold/Assets/Scripts/Player/PlayerInputReader.cs
- path → Input: Project Grimhold/Assets/Tests/PlayMode/Player/PlayerInputReaderPlayModeTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/MvpItemCatalogTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Player/DownedSelfReviveFeedbackTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Player/DownedSelfReviveFeedbackTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Player/DownedSelfReviveRulesTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Player/DownedSelfReviveRulesTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/ModularHudFillTests.cs
- path → Documentation: Project Grimhold/Docs/Architecture/DownedAndReviveArchitecture.md
- path → Documentation: Project Grimhold/Docs/Architecture/RaidMainHudArchitecture.md
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/MvpItemCatalogTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Player/DownedSelfReviveFeedbackTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Player/DownedSelfReviveFeedbackTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Player/DownedSelfReviveRulesTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Player/DownedSelfReviveRulesTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/ModularHudFillTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/Player/PlayerInputReaderPlayModeTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/PlayerSelfRevivePlayModeTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/PlayerSelfRevivePlayModeTests.cs.meta
- message-term → Tests: Tests

#### Diagnostics

- No classification ambiguity detected.

### 0200410152cc382ff8f2fc92db3fc0af77c3c813

[0200410152cc382ff8f2fc92db3fc0af77c3c813](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/0200410152cc382ff8f2fc92db3fc0af77c3c813)

Date: 2026-11-01T00:40:00-03:00 · Classification: Features (explicit) · Systems: Combat, UI, Audio, Art & Animation, Build & Tooling, Documentation, Tests

Flags: primary or unclassified

#### Original subject

```text
feat(presentation): add peripheral low-health blur and color split
```

#### Original body

```text
Sample the completed scene below the agony health threshold using a camera-local URP RenderGraph fullscreen effect. Preserve the clear center, avatar, cursor and overlay HUD, and release runtime materials when presentation ends.

Add configurable sampling radii, owner-scoped teardown and GPU regression tests for protected pixels, Renderer2D execution and exact baseline restoration. The focused presentation/audio/settings suite passed 104 tests; real multiplayer and visual calibration remain manual.

Player-Added: At critically low health, subtle blur and color separation at the screen edges reinforce the danger without obscuring your character or aim.

Refs: TASK-253
```

#### Changed paths

```text
Project Grimhold/Assets/Art/Shaders/ScreenDamagePeripheral.mat
Project Grimhold/Assets/Art/Shaders/ScreenDamagePeripheral.mat.meta
Project Grimhold/Assets/Art/Shaders/ScreenDamagePeripheral.shader
Project Grimhold/Assets/Art/Shaders/ScreenDamagePeripheral.shader.meta
Project Grimhold/Assets/Prefabs/Gameplay Camera.prefab
Project Grimhold/Assets/Scriptable Objects/ScreenDamageConfig.asset
Project Grimhold/Assets/Scripts/Player/Presentation/LocalPlayerScreenDamagePresenter.cs
Project Grimhold/Assets/Scripts/Player/Presentation/ScreenDamageCameraEffect.cs
Project Grimhold/Assets/Scripts/Player/Presentation/ScreenDamageCameraEffect.cs.meta
Project Grimhold/Assets/Scripts/Player/Presentation/ScreenDamageConfig.cs
Project Grimhold/Assets/Scripts/Player/Presentation/ScreenDamageFeedbackState.cs
Project Grimhold/Assets/Scripts/Player/Presentation/ScreenDamageRendererFeature.cs
Project Grimhold/Assets/Scripts/Player/Presentation/ScreenDamageRendererFeature.cs.meta
Project Grimhold/Assets/Settings/Renderer2D.asset
Project Grimhold/Assets/Tests/EditMode/Editor/Player/Presentation/ScreenDamagePeripheralTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Player/Presentation/ScreenDamagePeripheralTests.cs.meta
Project Grimhold/Docs/Architecture/RaidMainHudArchitecture.md
```

#### Classification evidence

- type → Features: feat
- path → Combat: Project Grimhold/Assets/Art/Shaders/ScreenDamagePeripheral.mat
- path → Combat: Project Grimhold/Assets/Art/Shaders/ScreenDamagePeripheral.mat.meta
- path → Combat: Project Grimhold/Assets/Art/Shaders/ScreenDamagePeripheral.shader
- path → Combat: Project Grimhold/Assets/Art/Shaders/ScreenDamagePeripheral.shader.meta
- path → Combat: Project Grimhold/Assets/Scriptable Objects/ScreenDamageConfig.asset
- path → Combat: Project Grimhold/Assets/Scripts/Player/Presentation/LocalPlayerScreenDamagePresenter.cs
- path → Combat: Project Grimhold/Assets/Scripts/Player/Presentation/ScreenDamageCameraEffect.cs
- path → Combat: Project Grimhold/Assets/Scripts/Player/Presentation/ScreenDamageCameraEffect.cs.meta
- path → Combat: Project Grimhold/Assets/Scripts/Player/Presentation/ScreenDamageConfig.cs
- path → Combat: Project Grimhold/Assets/Scripts/Player/Presentation/ScreenDamageFeedbackState.cs
- path → Combat: Project Grimhold/Assets/Scripts/Player/Presentation/ScreenDamageRendererFeature.cs
- path → Combat: Project Grimhold/Assets/Scripts/Player/Presentation/ScreenDamageRendererFeature.cs.meta
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Editor/Player/Presentation/ScreenDamagePeripheralTests.cs
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Editor/Player/Presentation/ScreenDamagePeripheralTests.cs.meta
- path → UI: Project Grimhold/Docs/Architecture/RaidMainHudArchitecture.md
- message-term → UI: HUD
- message-term → Audio: audio
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Shaders/ScreenDamagePeripheral.mat
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Shaders/ScreenDamagePeripheral.mat.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Shaders/ScreenDamagePeripheral.shader
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Shaders/ScreenDamagePeripheral.shader.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Player/Presentation/ScreenDamagePeripheralTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Player/Presentation/ScreenDamagePeripheralTests.cs.meta
- path → Documentation: Project Grimhold/Docs/Architecture/RaidMainHudArchitecture.md
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Player/Presentation/ScreenDamagePeripheralTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Player/Presentation/ScreenDamagePeripheralTests.cs.meta
- message-term → Tests: tests

#### Diagnostics

- No classification ambiguity detected.

### 21c13f917a6eb230ab66ffb438ad439e2be3938d

[21c13f917a6eb230ab66ffb438ad439e2be3938d](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/21c13f917a6eb230ab66ffb438ad439e2be3938d)

Date: 2026-11-01T00:23:20-03:00 · Classification: Features (explicit) · Systems: Combat, UI, Audio, Art & Animation, Build & Tooling, Documentation, Tests

Flags: primary or unclassified

#### Original subject

```text
feat(presentation): add progressive local screen damage spray
```

#### Original body

```text
Bind translucent screen damage to confirmed local health with smooth severity, heartbeat, hit flash and an accessibility intensity control. Preserve the clear center and cover viewport edges and corners with the v05 pixel spray.

Include serialized HUD/configuration assets and EditMode health, lifecycle, mask and settings tests. Presentation/audio/settings regression suite: 95 passed. Peripheral scene blur and chromatic aberration remain for the next implementation slice.

Refs: TASK-253
```

#### Changed paths

```text
Project Grimhold/Assets/Art/UI/ScreenDamage.meta
Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-00.png
Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-00.png.meta
Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-01.png
Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-01.png.meta
Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-02.png
Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-02.png.meta
Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-03.png
Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-03.png.meta
Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-04.png
Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-04.png.meta
Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-05.png
Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-05.png.meta
Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-06.png
Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-06.png.meta
Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-07.png
Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-07.png.meta
Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-08.png
Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-08.png.meta
Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-09.png
Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-09.png.meta
Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-10.png
Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-10.png.meta
Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-11.png
Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-11.png.meta
Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-12.png
Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-12.png.meta
Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-source.aseprite
Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-source.aseprite.meta
Project Grimhold/Assets/Prefabs/NetworkPlayer.prefab
Project Grimhold/Assets/Prefabs/UI/PlayerUI/LocalGameplayHud.prefab
Project Grimhold/Assets/Prefabs/UI/SettingsPanel.prefab
Project Grimhold/Assets/Scriptable Objects/ScreenDamageConfig.asset
Project Grimhold/Assets/Scriptable Objects/ScreenDamageConfig.asset.meta
Project Grimhold/Assets/Scripts/Player/Presentation/IScreenDamageSource.cs
Project Grimhold/Assets/Scripts/Player/Presentation/IScreenDamageSource.cs.meta
Project Grimhold/Assets/Scripts/Player/Presentation/LocalPlayerHudBinder.cs
Project Grimhold/Assets/Scripts/Player/Presentation/LocalPlayerScreenDamagePresenter.cs
Project Grimhold/Assets/Scripts/Player/Presentation/LocalPlayerScreenDamagePresenter.cs.meta
Project Grimhold/Assets/Scripts/Player/Presentation/PlayerScreenDamageSource.cs
Project Grimhold/Assets/Scripts/Player/Presentation/PlayerScreenDamageSource.cs.meta
Project Grimhold/Assets/Scripts/Player/Presentation/ScreenDamageAccessibility.cs
Project Grimhold/Assets/Scripts/Player/Presentation/ScreenDamageAccessibility.cs.meta
Project Grimhold/Assets/Scripts/Player/Presentation/ScreenDamageConfig.cs
Project Grimhold/Assets/Scripts/Player/Presentation/ScreenDamageConfig.cs.meta
Project Grimhold/Assets/Scripts/Player/Presentation/ScreenDamageFeedbackState.cs
Project Grimhold/Assets/Scripts/Player/Presentation/ScreenDamageFeedbackState.cs.meta
Project Grimhold/Assets/Scripts/Player/Presentation/ScreenDamageView.cs
Project Grimhold/Assets/Scripts/Player/Presentation/ScreenDamageView.cs.meta
Project Grimhold/Assets/Scripts/UI/Settings/ScreenDamageOptionsView.cs
Project Grimhold/Assets/Scripts/UI/Settings/ScreenDamageOptionsView.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Player/Presentation/ScreenDamageTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Player/Presentation/ScreenDamageTests.cs.meta
Project Grimhold/Docs/Architecture/RaidMainHudArchitecture.md
```

#### Classification evidence

- type → Features: feat
- path → Combat: Project Grimhold/Assets/Art/UI/ScreenDamage.meta
- path → Combat: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-00.png
- path → Combat: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-00.png.meta
- path → Combat: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-01.png
- path → Combat: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-01.png.meta
- path → Combat: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-02.png
- path → Combat: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-02.png.meta
- path → Combat: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-03.png
- path → Combat: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-03.png.meta
- path → Combat: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-04.png
- path → Combat: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-04.png.meta
- path → Combat: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-05.png
- path → Combat: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-05.png.meta
- path → Combat: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-06.png
- path → Combat: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-06.png.meta
- path → Combat: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-07.png
- path → Combat: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-07.png.meta
- path → Combat: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-08.png
- path → Combat: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-08.png.meta
- path → Combat: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-09.png
- path → Combat: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-09.png.meta
- path → Combat: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-10.png
- path → Combat: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-10.png.meta
- path → Combat: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-11.png
- path → Combat: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-11.png.meta
- path → Combat: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-12.png
- path → Combat: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-12.png.meta
- path → Combat: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-source.aseprite
- path → Combat: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-source.aseprite.meta
- path → Combat: Project Grimhold/Assets/Scriptable Objects/ScreenDamageConfig.asset
- path → Combat: Project Grimhold/Assets/Scriptable Objects/ScreenDamageConfig.asset.meta
- path → Combat: Project Grimhold/Assets/Scripts/Player/Presentation/IScreenDamageSource.cs
- path → Combat: Project Grimhold/Assets/Scripts/Player/Presentation/IScreenDamageSource.cs.meta
- path → Combat: Project Grimhold/Assets/Scripts/Player/Presentation/LocalPlayerScreenDamagePresenter.cs
- path → Combat: Project Grimhold/Assets/Scripts/Player/Presentation/LocalPlayerScreenDamagePresenter.cs.meta
- path → Combat: Project Grimhold/Assets/Scripts/Player/Presentation/PlayerScreenDamageSource.cs
- path → Combat: Project Grimhold/Assets/Scripts/Player/Presentation/PlayerScreenDamageSource.cs.meta
- path → Combat: Project Grimhold/Assets/Scripts/Player/Presentation/ScreenDamageAccessibility.cs
- path → Combat: Project Grimhold/Assets/Scripts/Player/Presentation/ScreenDamageAccessibility.cs.meta
- path → Combat: Project Grimhold/Assets/Scripts/Player/Presentation/ScreenDamageConfig.cs
- path → Combat: Project Grimhold/Assets/Scripts/Player/Presentation/ScreenDamageConfig.cs.meta
- path → Combat: Project Grimhold/Assets/Scripts/Player/Presentation/ScreenDamageFeedbackState.cs
- path → Combat: Project Grimhold/Assets/Scripts/Player/Presentation/ScreenDamageFeedbackState.cs.meta
- path → Combat: Project Grimhold/Assets/Scripts/Player/Presentation/ScreenDamageView.cs
- path → Combat: Project Grimhold/Assets/Scripts/Player/Presentation/ScreenDamageView.cs.meta
- path → Combat: Project Grimhold/Assets/Scripts/UI/Settings/ScreenDamageOptionsView.cs
- path → Combat: Project Grimhold/Assets/Scripts/UI/Settings/ScreenDamageOptionsView.cs.meta
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Editor/Player/Presentation/ScreenDamageTests.cs
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Editor/Player/Presentation/ScreenDamageTests.cs.meta
- message-term → Combat: damage
- path → UI: Project Grimhold/Assets/Art/UI/ScreenDamage.meta
- path → UI: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-00.png
- path → UI: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-00.png.meta
- path → UI: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-01.png
- path → UI: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-01.png.meta
- path → UI: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-02.png
- path → UI: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-02.png.meta
- path → UI: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-03.png
- path → UI: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-03.png.meta
- path → UI: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-04.png
- path → UI: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-04.png.meta
- path → UI: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-05.png
- path → UI: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-05.png.meta
- path → UI: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-06.png
- path → UI: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-06.png.meta
- path → UI: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-07.png
- path → UI: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-07.png.meta
- path → UI: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-08.png
- path → UI: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-08.png.meta
- path → UI: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-09.png
- path → UI: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-09.png.meta
- path → UI: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-10.png
- path → UI: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-10.png.meta
- path → UI: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-11.png
- path → UI: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-11.png.meta
- path → UI: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-12.png
- path → UI: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-12.png.meta
- path → UI: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-source.aseprite
- path → UI: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-source.aseprite.meta
- path → UI: Project Grimhold/Assets/Prefabs/UI/PlayerUI/LocalGameplayHud.prefab
- path → UI: Project Grimhold/Assets/Prefabs/UI/SettingsPanel.prefab
- path → UI: Project Grimhold/Assets/Scripts/Player/Presentation/LocalPlayerHudBinder.cs
- path → UI: Project Grimhold/Assets/Scripts/UI/Settings/ScreenDamageOptionsView.cs
- path → UI: Project Grimhold/Assets/Scripts/UI/Settings/ScreenDamageOptionsView.cs.meta
- path → UI: Project Grimhold/Docs/Architecture/RaidMainHudArchitecture.md
- message-term → UI: HUD
- message-term → Audio: audio
- path → Art &amp; Animation: Project Grimhold/Assets/Art/UI/ScreenDamage.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-00.png
- path → Art &amp; Animation: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-00.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-01.png
- path → Art &amp; Animation: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-01.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-02.png
- path → Art &amp; Animation: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-02.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-03.png
- path → Art &amp; Animation: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-03.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-04.png
- path → Art &amp; Animation: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-04.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-05.png
- path → Art &amp; Animation: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-05.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-06.png
- path → Art &amp; Animation: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-06.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-07.png
- path → Art &amp; Animation: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-07.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-08.png
- path → Art &amp; Animation: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-08.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-09.png
- path → Art &amp; Animation: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-09.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-10.png
- path → Art &amp; Animation: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-10.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-11.png
- path → Art &amp; Animation: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-11.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-12.png
- path → Art &amp; Animation: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-12.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-source.aseprite
- path → Art &amp; Animation: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-source.aseprite.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Player/Presentation/ScreenDamageTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Player/Presentation/ScreenDamageTests.cs.meta
- path → Documentation: Project Grimhold/Docs/Architecture/RaidMainHudArchitecture.md
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Player/Presentation/ScreenDamageTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Player/Presentation/ScreenDamageTests.cs.meta
- message-term → Tests: tests

#### Diagnostics

- No classification ambiguity detected.

### 735417b34141b2eb9b37e8bb5c5eb0a87cdc3f3c

[735417b34141b2eb9b37e8bb5c5eb0a87cdc3f3c](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/735417b34141b2eb9b37e8bb5c5eb0a87cdc3f3c)

Date: 2026-10-31T23:42:18-03:00 · Classification: Features (explicit) · Systems: Combat, UI, World & Visibility, Build & Tooling, Documentation, Tests

Flags: primary or unclassified

#### Original subject

```text
feat(presentation): fade the local view to black and white while Downed
```

#### Original body

```text
When the local player enters Downed the screen now blends to a
balanced-contrast black and white with a soft vignette, stays that way
while crawling or waiting for revive, and blends back to full colour on
revive or definitive Defeat. Only the Input Authority avatar's HUD
binds it, so proxies and other peers keep their normal camera, and the
Screen Space Overlay HUD is never desaturated.

- LocalPlayerDownedPostProcessPresenter owns a runtime global Volume
  (ColorAdjustments + Vignette, priority above the dungeon ambient
  Volume) with its own profile. It exists only while the effect is
  visible, is a child of the presenter (never DontSave) and is released
  on fade-out, unbind, disable or avatar loss. No renderer feature,
  shader or shared Volume asset is modified.
- DownedPostProcessBlend (pure): fade-in 0.4 s (config range 0.3-0.5),
  fade-out 0.4 s through Volume weight; vignette and dimming deepen as
  the Downed reserve drains (eased so damage chunks never pop); an
  assisted or self revive session gradually recovers part of the
  colour. Reserve and revive values are latched when Downed ends.
- DownedPostProcessConfig ScriptableObject (blend curve, timings,
  saturation -100, contrast, vignette, reserve and revive modulation)
  and its asset, wired on NetworkPlayer RaidDownedHud and bound by
  LocalPlayerHudBinder.
- Reads IsDowned, DownedHealth and TryGetProgress only; no networked
  state is added or written.

Tests: DownedPostProcessBlendTests, LocalPlayerDownedPostProcessPresenterTests
(including a regression that the Volume cannot outlive its presenter).

Docs: DownedAndReviveArchitecture section 9.1 and status table.

Refs: TASK-254
Player-Added: Your screen fades to black and white while you are downed and regains its colour as you are revived.
```

#### Changed paths

```text
Project Grimhold/Assets/Prefabs/NetworkPlayer.prefab
Project Grimhold/Assets/Scriptable Objects/DownedPostProcessConfig.asset
Project Grimhold/Assets/Scriptable Objects/DownedPostProcessConfig.asset.meta
Project Grimhold/Assets/Scripts/Player/Presentation/DownedPostProcessBlend.cs
Project Grimhold/Assets/Scripts/Player/Presentation/DownedPostProcessBlend.cs.meta
Project Grimhold/Assets/Scripts/Player/Presentation/DownedPostProcessConfig.cs
Project Grimhold/Assets/Scripts/Player/Presentation/DownedPostProcessConfig.cs.meta
Project Grimhold/Assets/Scripts/Player/Presentation/DownedPostProcessFrame.cs
Project Grimhold/Assets/Scripts/Player/Presentation/DownedPostProcessFrame.cs.meta
Project Grimhold/Assets/Scripts/Player/Presentation/IDownedPostProcessSource.cs
Project Grimhold/Assets/Scripts/Player/Presentation/IDownedPostProcessSource.cs.meta
Project Grimhold/Assets/Scripts/Player/Presentation/LocalPlayerDownedPostProcessPresenter.cs
Project Grimhold/Assets/Scripts/Player/Presentation/LocalPlayerDownedPostProcessPresenter.cs.meta
Project Grimhold/Assets/Scripts/Player/Presentation/LocalPlayerHudBinder.cs
Project Grimhold/Assets/Scripts/Player/Presentation/PlayerDownedPostProcessSource.cs
Project Grimhold/Assets/Scripts/Player/Presentation/PlayerDownedPostProcessSource.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Player/Presentation/DownedPostProcessBlendTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Player/Presentation/DownedPostProcessBlendTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Player/Presentation/LocalPlayerDownedPostProcessPresenterTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Player/Presentation/LocalPlayerDownedPostProcessPresenterTests.cs.meta
Project Grimhold/Docs/Architecture/DownedAndReviveArchitecture.md
```

#### Classification evidence

- type → Features: feat
- message-term → Combat: damage
- path → UI: Project Grimhold/Assets/Scripts/Player/Presentation/LocalPlayerHudBinder.cs
- message-term → UI: HUD
- message-term → World &amp; Visibility: dungeon
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Player/Presentation/DownedPostProcessBlendTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Player/Presentation/DownedPostProcessBlendTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Player/Presentation/LocalPlayerDownedPostProcessPresenterTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Player/Presentation/LocalPlayerDownedPostProcessPresenterTests.cs.meta
- path → Documentation: Project Grimhold/Docs/Architecture/DownedAndReviveArchitecture.md
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Player/Presentation/DownedPostProcessBlendTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Player/Presentation/DownedPostProcessBlendTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Player/Presentation/LocalPlayerDownedPostProcessPresenterTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Player/Presentation/LocalPlayerDownedPostProcessPresenterTests.cs.meta
- message-term → Tests: Tests

#### Diagnostics

- No classification ambiguity detected.

