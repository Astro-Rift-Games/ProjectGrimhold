# Commerce system audit (2026-10-25)

Branch `audit/commerce-e2e` from `New-Testing` @ `4d8a6663`. Read-only audit of the Town Merchant (Fusion), shop transaction persistence, backend shop and Ability Vendor.

References: `Project Grimhold/Docs/Architecture/CommerceArchitecture.md` (contract), `docs/plan_us57_shop_autoritativo.md` (historical plan), live Game Design: *UI - Auditoría Funcional de Interfaces MVP* §7 UI.1.4 Comercio, *00 - Desarrollo Conceptual* (Object Value is not a price; economy is future work), *11 - Diseño de Habilidades* (Town merchants may sell abilities under their own conditions). There is no dedicated economy document.

Evidence levels: **confirmed** = read in source; **inferred** = follows from source but not executed. Runtime confirmation is pending the E2E phase.

## Baseline

- Unity commerce EditMode tests: 18 fixtures, 283/283 passed, 0 console errors (job `7774d012dca947e499a76d3abfa40783`).
- Backend tests: not run (`backend/node_modules` and `.env` absent locally; suites use `MongoMemoryServer`).
- Runtime: Unity loads `Resources/BackendConfiguration.asset` = `https://projectgrimhold.onrender.com` (health ok). `BackendConfiguration_Local.asset` (`localhost:3000`) is not loaded by any code path.

## Findings

### High

**H1 Ability Vendor purchases are local-only (confirmed).**
- `TownAbilityVendorPresenter.cs:261` calls `LocalProfileStore.TryPurchaseAbility` (`LocalProfileStore.cs:243`). That method deducts Currency and adds the unlock through a local `Commit`.
- No backend call, transaction id or receipt is involved, and the purchase never passes the reservation gate. The backend has no ability-unlock field (only `preparedAbilities`).
- Hydration overwrites `Currency` from the backend (`ApplicationStashServiceBootstrapper.cs:316`) but keeps `UnlockedAbilities` local (`:240`). After any reconciliation the spent gold comes back and the ability stays unlocked, so the ability is effectively free.
- Unlocks are lost whenever the local cache is lost (new device, corrupt cache recovery).
- No architecture contract covers the Ability Vendor.

**H2 Trades during a pending Raid loadout reservation corrupt inventory (confirmed, both sides).**
- Unity: `LocalProfileStore.TryCommitTrade` (`:797`) does not use the `BeginTownMutation` gate (`:881`), unlike the other Town mutations. This contradicts the invariant at `:1475-1480`. If the trade fills capacity, `TryRollbackLoadoutReservation` (`:1526`) returns `PersistenceFailed` and the reservation stays stuck.
- Backend: `shopBuy` (`InventoryService.js:733`) and `shopSell` (`:611`) never check `inventory.pendingReservation`, while `updateOrder` (`:60`) does.
  - A purchase lands in the emptied `loadout`.
  - `clearPendingReservation` with rollback restores reserved items only when the loadout and equipment are empty (`:491-496`). The reserved items and equipment are silently dropped.
  - Extraction commit fails with `LOADOUT_NOT_EMPTY` (`:559`, `ExtractionCommitService.js:108`).

**H3 Multi-line trades fail in production (confirmed; documented gap vs Game Design).**
- GD UI.1.4 §7.1 says the player prepares purchases and sales and confirms the whole operation.
- The authority prices and reserves multi-line tickets, but `RemoteShopTransactionService.TryExecuteTrade` (`:48`) refuses `LineCount != 1` with `PersistenceFailed`. `RemoteShopTransactionService` is the only service constructed (`ApplicationStashServiceBootstrapper.cs:162`).
- Any cart with two or more lines therefore fails at confirm.
- This is documented as a known gap in `CommerceArchitecture.md` (item 11, needs an atomic backend endpoint). The UI still allows building such carts.

**H4 Unity and backend catalogs diverge (confirmed).** Unity prices live in `Assets/Scriptable Objects/Loot/Definitions/*.asset`; backend prices in `backend/src/config/shopCatalog.js`.
- `forest_ranger_*`, `light_armor_*`, `heavy_armor_*`, `army_ranger_*` and `fire_mage_*` sell for 5 in Unity but have no backend entry.
  - The sale commits locally and the backend answers 404 `ITEM_NOT_SELLABLE`.
  - Reconciliation then reverts the profile, but the merchant stock has already been restocked (see H5).
- `arming_sword` and `shield` have sell value 0 in Unity, but the backend buys them for 12/10 and sells them for 25/20. Not reachable from the current Unity client.
- `normalizeLootId` maps `training_sword` to `arming_sword` before the catalog lookup. The `training_sword` entry (`shopCatalog.js:26,46`) is therefore unreachable.
- `health_potion` (`shopCatalog.js:18,38`) is a separate key from Unity's `healthpotion`.
- No test compares the two catalogs.

**H5 The merchant authority is told "persisted" before the backend acknowledges (confirmed).**
- `RemoteShopTransactionService.TryExecuteTrade` commits locally (`:55`), returns `Success` and syncs fire-and-forget. `TownMerchantTradeSession` immediately reports `persisted = true`, which consumes the stock reservation and restocks sold units.
- `RemoteOperationPolicy` does not retry (`:8-10`), though the class comment says it does. A backend rejection only reconciles the profile, so the shared merchant stock keeps a trade the backend never accepted.
- Overlapping trades read `_store.RemoteRevision` independently. A second trade started before the first sync completes sends a stale revision and gets `REVISION_CONFLICT`; reconciliation then reverts it (inferred).

### Medium

**M1 The authority trusts the requester's outcome and does not check sale ownership (confirmed).**
- `Rpc_ReportTradeOutcome` → `MerchantRequestValidator.TryCompleteTrade` accepts `persisted` from the record owner. `TryPriceLines` and `TryPlanStockEffects` do not check that the seller owns the units.
- A modified client can add units to finite stock or burn stock without paying.
- Reporting on the requester is by contract design; the ownership gap is not covered by the contract.

**M2 RPC handlers dereference a null validator (confirmed).** `Rpc_RequestTrade`, `Rpc_ReportTradeOutcome` and `Rpc_RequestPendingTrades` use `_requestValidator`, which stays null when `Spawned` rejects the stock configuration (`TownMerchantNetworkController.cs:108`) and after `Despawned`.

**M3 Quantity and capacity bounds are not aligned (confirmed).**
- Unity has no upper bound on amounts: `MerchantTradeWireCodec.TryDecodeRequest` does not check one, and `TryPriceLines` only checks `> 0`. `TownMerchantTradeSession.BuildInventoryRows` allows selling all owned units.
- The backend caps amounts at 999 (`VALIDATION_FAILED`) and has no capacity or stack limit server-side; repeated buys grow the loadout without bound.

**M4 Backend tests and error contract are weaker than they claim (confirmed).**
- The mock `applyUpdate` in `inventory.shop.test.js:31-67` ignores the `revision` and `accountId` filters.
- The "declaredPrice = 0" exploit test passes on the quantity check, not on pricing.
- The concurrency "Duplicate Receipts" test accepts 200 or 409 without checking the error code.
- `INVALID_QUANTITY` and `INTEGER_OVERFLOW` are unreachable over HTTP because the validators answer `VALIDATION_FAILED` first. `plan_us57` promises `INVALID_QUANTITY`.

### Low

- **L1** `RemoteShopTransactionService.Initialize` silently creates a default `BackendConfiguration` when none is found.
- **L2** `LocalShopTransactionService` is never constructed (dead code).
- **L3** The `RemoteShopTransactionService` docs claim retries on `REVISION_CONFLICT`; there are none.
- **L4** `TownMerchantTradeSession.AbandonSubmission` keeps `_unansweredRequestId` (harmless, unclear state).
- **L5** Backend input hardening:
  - `transactionId` has no length or format bound.
  - Shop routes have no rate limit.
  - `INSUFFICIENT_FUNDS` exposes the balance and price.
  - String `amount` and `expectedRevision` produce inconsistent error codes.
- **L6** Unity tests:
  - `TownMerchantNetworkControllerRpcTests` is reflection-only.
  - `MerchantStockConfigurationTests` hard-codes prefab quantities.
  - Nothing covers the remote service, the RPC path, sale ownership, the reservation gate or amount bounds (matches contract item 13).

## Conformance summary

| Rule (Architecture / GD) | Status |
| --- | --- |
| Only State Authority mutates stock; requests to State Authority; responses to requester only | Met |
| Identity `ProfileId + RequestId`; same key + payload replays; different payload rejected | Met |
| No client prices; authority and backend price from catalog | Met (backend ignores `declaredPrice`/`declaredSellValue`) |
| Backend ownership by JWT `accountId`; single-document atomic update with revision guard; receipt idempotency | Met |
| Pending draft without side effects; preview advisory; Confirm disabled while in flight | Met |
| GD §7.2 Confirm disabled on missing gold or capacity, evaluated on the final state | Met in preview (runtime check pending E2E) |
| GD §7.1 confirm the whole multi-line operation | **Not met** with remote persistence (H3) |
| Reservation gate on Town mutations | **Not met** for trades (H2) |
| Sold units enter stock only on persisted outcome | Met formally; "persisted" is optimistic (H5) |
| Ability Vendor durable and authoritative | **Not met** (H1); no contract |

## E2E focus (runtime confirmation)

1. Single-line buy and sell against Render: backend revision, currency and loadout match Unity.
2. Two-line cart: expect `PersistenceFailed` (H3).
3. Sell an armor piece: expect backend `ITEM_NOT_SELLABLE`, profile revert and merchant stock divergence (H4/H5).
4. Insufficient gold or capacity: Confirm disabled (GD §7.2).
5. Ability Vendor purchase followed by reconciliation: gold restored (H1).
