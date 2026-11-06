# Handoff: Equipment Item Instances (TASK-283), remaining work

Branch: `feat/equipment-item-instances` (from `New-Testing`, never pushed). HEAD: `a0579835`.
Tracker with the full decision log: `odd/tasks/equipment-item-instances.md` (S1-S19, L1-L50).
Approved contract: `Project Grimhold/Docs/Architecture/ItemInstanceArchitecture.md`.
HacknPlan: stay under TASK-283 (#788, parent US-80 #778); the owner updates it, never create tasks.

## What is done (all committed)

| Unit | Commit | Result |
| --- | --- | --- |
| T0 | bada9b0a4 | Pure gem slice: `GemDefinition`, `TryInstallGem`, gem damage bonus, tooltip |
| T2 | 1c1f79b2 | Gems in Default (weight 3) and Enemy (weight 1) loot tables |
| T1 | b1f30da6 | Contract + five architecture docs aligned |
| T3 | f2fd344a, 6d65daf5 | Backend schema v3, migration script, `POST /character/me/inventory/gems/install`, extraction rule validation, protocol header as minimum version |
| T4 | 90ec4367 | Unity save codec v11, v3 DTOs, header 3, hydration guard for the v10 cache |
| T5 | be00d8ad | Town damage, statistics and tooltips from the instance |
| T6 | f644df3a | "Incrustar" UI: drag, context action, confirmation prefab, remote-first |
| T7a | e4018eeb, 702d34ac | 1-word packed modifiers, ID-free signed admission, 16 KiB admission bound |
| T7b | 4e55f297 | `PlayerLootReceiver` replicates modifiers per slot |
| T7c | d5e73de3 | Hand modifiers and combat damage with gems |
| T7d1 | 99991229 | Container weapon units (`ContainerWeaponUnit`, 34 per container) |
| T7d2a | b84b02df | Container unit address in request, RPC and authority |
| T7d2b | ec5dbdc4 | One cell and tooltip per weapon unit in the container UI |
| T7d3 | a0579835 | Pickups keep modifiers |

## What is NOT implemented

### T7d4 Corpses keep modifiers
- Corpse load creates **plain units only**: `NetworkLootContainer.TryLoadExactContent` adds units without modifiers
  (comment "Snapshot entries are plain until corpse snapshots carry modifiers").
- Work: carry modifiers through `PlayerExpeditionLootSnapshot` (inventory and the four hands),
  `PlayerCorpseGenerationController`, `TryLoadExactContent` / `TryClearExactContent` / `HasExactContent`.
  Capacity is already there (34 units). `TryClearExact*` must match modifiers too.
- Existing related tests: `PlayerCorpseGenerationPlayModeTests`, `DefeatedPlayerLootIntegrationPlayModeTests`
  (both have known baseline failures, see below).

### T7e Host Migration restore
- New networked fields must survive `CopyStateFrom` and must not be overwritten in `Spawned()`:
  `PlayerLootReceiver.LootModifiers`, equipment hand modifiers (4), `NetworkLootContainer.WeaponUnits`,
  `NetworkLootPickup.Modifiers`.
- Only the receiver has a `CopyStateFrom` test (`BehaviourCopyStateFrom_PreservesModifiersAcrossSpawnedReplay`).
  Add the same for equipment, container and pickup. Read `HostMigrationRecoveryArchitecture.md` first.
- Real Host Migration needs a separate Host plus two Clients. It cannot be claimed from single-runner tests.

### T7f Extraction payload, network ceilings, docs, independent verification
- **Extraction publish**: the host must publish `{ lootId, amount, modifiers? }` (equipment amount 1) and prepared
  result `{ lootId, modifiers? }`. Touch `PlayerExtractionLootSaver`, `RaidResultsLootWire`,
  `NetworkRaidParticipant` (`ResultsLoot*`), `RemoteInventoryService` mapping and `InventoryRequestJson`.
  The backend side already validates modifiers by rules and issues fresh IDs on commit.
- **Ceilings**: re-measure the woven sizes and set the `RaidLootOriginWordCountTests` ceilings to the measured values
  (owner decision S18). Three budget tests already fail today (container 1162 vs 983, enemy 1223 vs 1007,
  pickup 23 vs 22). Receiver, equipment, container and pickup all grew since the last measurement.
- **Docs** to update with what was actually built: `LootInteractionArchitecture`, `ExtractionArchitecture`,
  `HostMigrationRecoveryArchitecture`, `PlayerCombatArchitecture`, `ItemInstanceArchitecture`.
- **Independent verification** (`gentle-ai-verify` or a second reviewer) before merging: network and Host Migration risk.

### T8 Dungeon variants in chests
- `WeaponInstanceGenerator` still has **no production caller**. Hook it where chest content is rolled
  (`LootContainerContentRoller.TryRoll`, `NetworkLootContainer.TryResolveRandomContent`, `BreakableObject`,
  `NetworkSpawnManager` roll sites). Natural content must then create units **with** modifiers
  (today it creates plain units).
- Needs provisional generation weights (`WeaponInstanceGenerationRules`, Doc 09 section 8.1 gives them to Balance).
  Ask the owner for values before implementing.
- The backend already accepts and validates Raid-found variants (S12).

### T9 Game Design corrections (last step, owner request)
Propose edits to the live docs: Doc 09 sections 20-23 (instances, owner-triggered incrustation in Town),
Doc 14 (provisional gems), buy-back (GD UI.1.4 section 7.1: disabled for equipment, owner wants a redesign such as
a pawn model), sale price (template plus gems, every equipment sellable even for 0).

## Known issues and open decisions

1. **Extra EditMode failure**: `PlayerMovementRulesTests.FusionConfiguration_RegistersPlayerControllerExecutionOrder`
   expects -9, gets -7. `PlayerCombatNetworkController` declares `[DefaultExecutionOrder(-7)]` and is untouched by
   this work. It passed in earlier full runs; the cause is unattributed. Investigate before trusting a green branch.
2. **Corpse panel**: the Raid container panel authors 6 cells but corpse containers have capacity 30, so a corpse
   panel reports unavailable. Pre-existing and acknowledged in `RaidInventoryPresenterInteractionTests.BindUiContainer`.
   Owner decision needed.
3. **Manual checks never run**: incrustation visuals and scaling, keyboard/controller navigation, a real incrustation
   against the backend with a logged-in session, multiplayer pickup/container/corpse flows, Host Migration.
4. **Rollout**: `INVENTORY_INSTANCE_PROTOCOL_GATE` defaults to false. The migration
   (`backend/scripts/migrate_item_instances.js`) needs a maintenance window and backups of both collections; it is
   resumable but not transactional across collections. It was only tested on a disposable MongoDB.
5. **Signed results**: the HMAC canonical string of Raid results does not encode modifiers. Unity does not sign today.
   Future work when dedicated-server signing exists.
6. **Fallback weapon**: `arming_sword` is issued as a plain instance until the separate training-weapons task lands.
7. **Backend**: integration test `Duplicate Receipts (Shop Idempotency)` is an intermittent race in the test itself.

## Test baselines (for regression comparison)

- **Backend** (`cd backend`): `npm test` 517/517, `npm run test:integration` 171/171 (intermittent race above),
  `npm run test:concurrency` 19/19.
- **EditMode, 25 known failures** by class: `RpcAcceptanceContractTests` (3), `ChaosCoinRollerTests` (3),
  `RaidLootOriginWordCountTests` (3), `DungeonPressureAmbientPresenterTests` (2), `SessionCompositionConfigurationTests` (2),
  `OffHandRetargetingTests` (2), `CharcoalUiMigrationTests` (2), `RaidBottomBarLayoutTests`, `RaidDefeatLifecycleSourceTests`,
  `InventoryPassiveEffectsTests`, `MvpItemCatalogTests` (CON_SMOKE_BOMB), `WeaponCatalogTests` (generic attack route),
  `EquipmentLayoutParityTests`, `PlayerWeaponPresentationMathTests`, `ExtractionProgressPrefabTests`.
  Last full run: 5776/5802 (those 25 plus the execution-order test above).
- **PlayMode, 21 known failures** in the loot/equipment fixtures: 2 in `PlayerEquipmentPlayModeTests`
  (damage oracles 45 vs 48 and 30 vs 22, obsolete expectations), `PlayerCorpseGenerationPlayModeTests.MultipleStacks_...`,
  and 18 in `DefeatedPlayerLootIntegrationPlayModeTests` ("no active RaidSceneMinimap").
  Fixtures run: PlayerLootReceiverSlots, PlayerEquipment, NetworkLootPickupPartialPickup, LootContainerPreSpawn,
  PlayerCorpseGeneration, DefeatedPlayerLootIntegration, PlayerLootReceiverWeaponModifiers, LockedLootContainer,
  RaidInventoryPresenterInteraction, LootTransfer, RaidInventoryView, LobbyStashPresenter: 203/224.

## How to work on this branch

- Unity MCP is at `http://127.0.0.1:8081/mcp` (JSON-RPC over HTTP, not registered as Pi tools). The helper scripts
  `call.sh` and `runtests.sh` lived in a temp folder that was wiped once; recreate them if missing:
  `initialize` -> `mcp-session-id` -> `tools/call` (`refresh_unity`, `read_console`, `run_tests`, `get_test_job`).
  Always force a compile (`refresh_unity` with `compile: request`) and check the console for `error CS` before running tests.
- Large C# edits were applied with small Python scripts that assert each replacement matches exactly once.
- Running the suites reserializes assets. Revert `*_OffHand.asset` before committing. Never commit:
  `alagard SDF.asset`, `DungeonGrayboxMinimapLayout.asset`, `Renderer2D.asset`, `EditorBuildSettings.asset`,
  `TimeManager.asset`, `.claude/agents/`. `design/main-menu/*` is staged by someone else: always commit with explicit paths.
- The `gentle-ai-worker` subagent failed three times at provider level (first turn, zero tool calls). T7b onwards
  was done inline. Retry delegation before assuming it is still broken.
- Rules that apply: test-first (observe RED), one validated unit per commit, English Conventional Commit messages,
  `Refs: TASK-283`, a `Player-Added` trailer only when a player can observe it, ask the owner about any design
  decision the contract does not cover.
- Player-facing term is "Incrustar gema" (never "Engarzar"); code uses *InstallGem*.
