# Commerce Architecture

## Scope

This document owns the Unity-side technical contract for Town Merchant commerce: pending trade
state, presentation, Fusion authority over shared merchant stock, request identity and the
persistence boundary. `LocalPlayerPersistenceArchitecture.md` owns the character aggregate and
its transactions; this document only defines what Commerce submits to it.

Intended gameplay behavior lives in the live Game Design ("UI - Auditoría Funcional de Interfaces
MVP", section UI.1.4 Comercio). Backend endpoints, DTOs and server-side validation are out of scope.

## Decision

A trade is one pending, local draft of buy and sell lines that is confirmed as a single atomic
operation. Only State Authority decides shared stock; only the confirmed profile holds persistent
Inventory and Currency.

```text
Merchant interaction
  -> TownMerchantPresenter (lifecycle, binding, input)
  -> MerchantTradeDraft (local pending lines, no side effects)
  -> local preview (confirmed profile + draft, pure rules)
  -> Confirm intention
  -> Fusion State Authority (stock, concurrency, request identity, dedup, stock reservation)
  -> authority-issued trade ticket
  -> IShopTransactionService (persistence boundary, one atomic profile transaction)
  -> ProfileCommitted (confirmed state)
  -> outcome reported to State Authority (commit or release reservation)
  -> UI refresh from confirmed profile and replicated stock
```

## Ownership

### MerchantTradeDraft

- Plain C# local state owned by the Input Authority client for one open merchant session.
- Holds ordered buy lines and sell lines (`LootId + Amount`), the client request identity once
  Confirm is pressed, and an in-flight flag.
- Never mutates the profile, Currency, Inventory, merchant stock or any `[Networked]` value.
  Adding, removing or changing a line has no side effect outside the draft.
- Cancel or close discards the draft; the confirmed state is unchanged by construction.
- After a successful confirmation the draft is cleared. After a rejection it is kept so the
  player can edit and retry, and a new request identity is issued for the edited content.
- It is not replicated and not persisted.

### Local preview

- A pure, deterministic projection: confirmed Inventory/Currency + draft lines + replicated stock
  view -> resulting Inventory, Currency balance, buy total, sell total and blocking reasons
  (insufficient Gold, capacity exceeded, stock unavailable, missing owned units).
- Capacity is evaluated on the final state, counting sold and bought units together, with the same
  capacity/merge rules the aggregate transaction uses. The preview reuses those pure rules; it does
  not reimplement them.
- Unit prices come from the authoritative static economy configuration available to the client for
  preview. The preview may display them but they are never trusted as authoritative transaction
  input.
- That configuration is currently owned by `LootDefinition`: `BuyValuePerUnit` is the buy price and
  `SellValuePerUnit` the sell value, resolved through the merchant's `LootDefinitionCatalog`.
  `ExtractionValuePerUnit` is extraction progress only and is never a Commerce price or fallback.
  Every merchant stock item must reference a valid definition with a positive buy value.
- The preview is advisory. The persistent transaction revalidates everything.

Current representation: `MerchantTradePreview.Calculate` projects confirmed Inventory entries, slot
capacity, confirmed Currency, the draft and an available-stock lookup into an immutable result with
buy/sell totals, balance, projected Currency, projected Inventory and typed
`MerchantTradeBlock`s; `CanConfirm` means no block. It also reports the confirmed and projected
occupied slots next to the slot capacity. Sale lines are checked against confirmed owned
units and purchase lines against current available stock; units sold in the same trade are not
purchasable in it, because sold units enter stock only on a persisted outcome. Stack merge, removal
and slot capacity come from `ProfileInventoryRules`, which `LocalProfileStore` also uses.

### MerchantShopUI

- Presentation and intentions only: renders stock, confirmed Inventory, draft lines and preview
  results; raises intentions (add/remove line, change quantity, confirm, cancel, close).
- Does not hold `TownMerchantNetworkController`, `ApplicationStashContext` or persistence
  services, does not compute prices or totals, and does not decide whether Confirm is allowed.
  It receives an already computed view model from the presenter.
- Disables Confirm while the preview reports a blocking reason or a request is in flight.

Current representation: `MerchantShopUI` renders a `MerchantShopViewModel` (confirmed and projected
Gold, Currency shortfall, preview totals and balance, occupied and projected slots against capacity,
blocks, `CanConfirm`/`CanClear`/`CanEdit`, priced merchant and Inventory rows with their draft
amounts) and raises `IMerchantShopIntentions` (add/set/remove buy or sell line, clear, confirm)
through one flow: select a row (stock for purchase, Inventory for sale), choose a quantity, add the
line or update the one already in the draft, review, then Confirm or Clear. A separate action
removes the selected line; Close keeps discarding the session. The selection, the chosen quantity
and which action applies live in the pure `MerchantShopInteraction`, which reads limits and
permissions from the view model and sends Confirm only when `CanConfirm` holds, once per view model.
While a request is in flight every edit and Confirm are disabled. `MerchantTradeFeedback` maps the
main `MerchantTradeBlockReason` and each `MerchantTransactionResult` to presentation messages;
those strings are never domain rules. Item details reuse `EquipmentTooltipPresentationBuilder`.
The prefab lays out a header (title and Close only), three columns (Inventory grid on the left, the
selected-item panel in the center, merchant stock grid on the right) and an independent footer. Both
grids use five fixed columns aligned from the top inside a vertical `ScrollRect` that only scrolls
when the content exceeds it, so slots keep their size. The Inventory grid always renders
`SlotCapacity` slots (or more when rows exceed it): empty slots show only their background and raise
no selection or intention; the merchant grid shows only offered items. The center panel separates
identity (icon, name, category, rarity), a scrollable block with properties/requirements and
description, and the line editor (unit price, quantity, line total, line state, Add/Update, Remove)
that stays in place. The line total is `MerchantShopInteraction.LineTotal`, read from the row view
model. The footer has a summary row (Gold and occupied slots as current -> projected, purchase and
sale totals, balance with its sign and a "a favor"/"a pagar"/"sin cambio" label besides its color)
above a row with feedback on the left and Clear and Confirm on the right. `StoreItemUI` is one
compact grid slot: icon, confirmed amount (owned units, or finite unreserved stock) bottom-right,
unit price top-left on merchant slots, a separate "+N"/"-N" badge top-right, colored by side, while
the draft holds a line for that item, and selection; name, description and properties appear only
in the center panel. It raises only its selection; slots have no immediate buy or sell action. It is
not `RaidInventorySlotView`, whose drag, context and tooltip responsibilities do not apply here.
None of them holds services, the network controller or economy rules.

### TownMerchantPresenter

- Owns lifecycle, binding and input for the local player's merchant session: resolves the target
  merchant from the interaction, creates/binds the view, owns the `MerchantTradeDraft`, builds
  the preview view model, acquires/releases input suppression and closes on invalid runner/NPC.
- Receives its dependencies (confirmed-profile read sources, shop transaction boundary) through
  explicit composition, not scene-wide searches.
- Forwards Confirm to the merchant network endpoint and routes the outcome back to the view.
- Never mutates the profile or stock directly.

Current representation: each open merchant owns one pure `TownMerchantTradeSession` (draft, preview
recomputed on edit, on `ProfileCommitted` through the Inventory read source and on replicated stock
changes, Confirm only when `CanConfirm`, ticket persistence, outcome report, view model). Closing
disposes it and discards the draft. `TownInventoryBinder`, the existing Town composition of the
confirmed local profile, supplies a `TownMerchantProfileSource` (`ProfileId`, the shared
`LocalLoadoutInventoryReadSource`, Currency service, `IShopTransactionService`) through a
serialized reference on `SocialPlayer`. A submission unanswered for 35 s, or a merchant authority
change, only unlocks the draft with the same request id; it never releases stock. The session asks
State Authority for its profile's pending trades when it opens, when the merchant authority
changes and when an edit discards a request it already sent, and reports each one it no longer
uses from the confirmed profile: persisted if the profile holds its receipt
(`IShopTransactionService.IsTradeApplied`), otherwise not persisted. That report is final because
a session only ever persists the ticket of its in-flight request.

### Fusion State Authority (merchant NetworkObject)

The Town runner is Shared mode; State Authority of the merchant object is the single decision
point for commerce requests.

- **Shared stock**: owns the merchant stock as replicated `[Networked]` state so every client
  observes the same quantities and the state survives a Master Client change. Serialized prefab
  data is only the initial configuration.
- **Concurrency**: requests are evaluated in arrival order. A buy line is accepted only against
  available, unreserved stock. Two players competing for the last unit get one acceptance and one
  rejection.
- **Stock reservation**: accepting a request reserves its buy quantities. The reservation becomes
  a stock change only when the requester reports persistence success, and is released only when
  the requester reports that the ticket is not persisted, a report that excludes a later success.
  Time, requester departure and authority changes never release it, because a persisted outcome
  may still arrive: a persisted trade can never leave stock as if it had not happened. Sold units
  enter stock only on success.
- **Request identity and deduplication**: the key is `ProfileId + client request id`, never
  `PlayerRef` or a per-open sequence. A duplicate of an accepted request returns the same ticket;
  a different payload under the same key is rejected. Authority mints exactly one
  `ShopTransactionId` per accepted request.
- **Ticket**: the accepted response carries the transaction id and the priced lines resolved by
  authority from the authoritative static economy configuration. Clients do not declare prices.
- Only State Authority runs validation. Request RPCs target State Authority; responses target only
  the requesting player.
- Dedup/reservation bookkeeping is bounded. Reservations end only through the requester's outcome,
  never through departure or elapsed time.

Current representation on `TownMerchantNetworkController` (Fusion 2.1.1):

- Protocol: `Rpc_RequestTrade(profileId, requestId, purchases[], sales[])` to State Authority,
  with lines as catalog index + amount and no client prices; `Rpc_TradeResponse` to the requester
  only, with approval, `ShopTransactionId` and the authority-priced lines;
  `Rpc_ReportTradeOutcome(requestId, transactionId, persisted)` to State Authority. The requester's
  `ProfileId` is resolved from its replicated `SocialPlayerIdentity` and must match the declared
  one; `PlayerRef` is only the reply target. `Rpc_RequestPendingTrades(profileId)` asks for the
  sender profile's pending trades, answered to the requester with one `Rpc_PendingTrade(requestId,
  transactionId)` each.
- `StockQuantities`: `NetworkArray<int>` with capacity `MaxStockSlots`, index-aligned with the
  serialized `MerchantStockItem` offering, seeded once by State Authority (`IsStockInitialized`).
  `MerchantStockItem.UnlimitedQuantity` marks unlimited stock; an item outside the offering is
  not purchasable.
- `TradeRecords`: `NetworkArray<MerchantTradeRecord>` with capacity `MaxTradeRecords`, keyed by a
  deterministic 64-bit hash of the `ProfileId` plus the request id. Each record keeps its status
  (`Pending`, `Committed`, `Released`), a hash of the requested lines and the transaction id.
  Priced lines are not stored: a retry with identical lines is answered by pricing
  them again from static configuration under the stored transaction id. The same key with other
  lines is rejected. `Pending` and `Committed` retries get the same ticket without another
  reservation; a `Released` retry reserves again under the same transaction id. Completed records
  are evicted oldest first; pending ones never are, and a table full of pending trades rejects new
  requests.
- `TradeStockLines`: `NetworkArray<MerchantTradeStockLine>` with capacity `MaxTradeStockLines`:
  the per-slot effects of pending trades on finite offered stock. Purchase lines are the
  reservations (available stock is the quantity minus them); sale lines add units on a persisted
  outcome. A trade needing more free lines than remain is rejected whole.
- A persisted outcome for a pending trade consumes its reservations and restocks sold offered
  units; a not-persisted outcome only releases them. Outcomes for trades that are not pending are
  ignored, so a released ticket is never persisted without reserving again. An approved response
  that no local session handles is reported as not persisted.
- `MerchantRequestValidator` holds these rules as pure C# over `IMerchantAuthorityState`, which
  the controller backs with the replicated arrays and refuses to mutate without State Authority.

### IShopTransactionService

- The only persistence boundary for Commerce. It receives the complete ticket for one profile and
  applies all its lines as one atomic `LocalProfileStore` transaction: funds, final capacity and
  owned-quantity validation, receipt idempotency keyed by the authority `ShopTransactionId`.
- Returns an explicit outcome (`Success`, `AlreadyApplied`, rejection reason, persistence failure).
  `AlreadyApplied` is a success for reservation purposes and publishes nothing twice.
- Backend submission, retries, revision conflicts and reconciliation are implementation details
  behind this boundary and are owned by the backend contract, not by Commerce.
- Its behavior never depends on scene names or UI state.

Current representation: `TryExecuteTrade(ProfileId, MerchantTradeTicket)`. The ticket is immutable:
`ShopTransactionId` plus read-only `Purchases` and `Sales` of `MerchantPricedTradeLine`
(`LootId`, `Amount`, authority `UnitPrice`). `LocalProfileStore.TryCommitTrade` rejects malformed
tickets (empty, invalid id/loot, non-positive amount or buy price, negative sell price, repeated
loot on one side), returns `AlreadyApplied` for a known receipt before any content check, then
builds one candidate snapshot: sales against the confirmed Inventory, overflow-safe totals, final
Currency and final slot capacity through `ProfileInventoryRules`, one `Commit`. Buy and sell lines
of the same loot stay independent, matching `MerchantTradePreview`. Every rejection returns
`InvalidInventory` without publishing `ProfileCommitted`.

### Confirmed profile

- The character aggregate (`LocalProfileStore` via `IPlayerLoadoutService` and
  `IPlayerCurrencyService`) is the only source of persistent Inventory and Currency.
- `ProfileCommitted` for the matching `ProfileId` is the only refresh signal for Inventory and
  Currency in Commerce presentation.
- Neither Fusion nor the draft holds a copy of persistent Inventory or Currency.

## Relation to shared Inventory/Equipment infrastructure

Commerce does not introduce its own inventory model.

- Commerce reads the confirmed Inventory through the existing Town read path
  (`LocalLoadoutInventoryReadSource` / `IInventoryReadSource`) rather than keeping a separate
  list of owned items.
- Tradeable ownership is the Inventory location only. Prepared Equipment is a separate exclusive
  ownership location (see `LocalPlayerPersistenceArchitecture.md`) and is not a sell source; units
  must be unequipped through the existing Town Equipment endpoint first.
- The provisional placement of bought items in the Inventory screen, allowed by Game Design, is a
  presentation overlay derived from the preview. It is never written to the aggregate or to a
  Commerce-owned inventory.
- Capacity and stack merge rules are the aggregate's pure rules, shared by preview and commit.
- Stash is not a Commerce source or destination.

## Failure policy

- No response or authority loss: the draft unlocks with the same request id; the player may retry
  it, and dedup guarantees at most one acceptance. An accepted reservation stays held.
- Ticket accepted but persistence rejects or fails: the profile is unchanged, the reservation is
  released and the draft stays editable.
- Outcome report lost: the reservation stays held until the requester's next session resolves it
  from the confirmed profile's receipts.
- Requester leaves after acceptance and does not return: the reservation stays held for the rest of
  the merchant session. Availability is sacrificed so a unit is never sold twice.
- Duplicate responses or retried tickets are absorbed by transaction-id idempotency.

## Out of scope and open design points

- Backend endpoints, DTOs, server-side price/stock validation and durable merchant stock.
- Stock replenishment and rotation cadence, and whether stock persists across Town sessions, are
  not specified by current Game Design and must be resolved there before implementation.
- Game Design does not state whether one trade may buy and sell the same loot. The preview does not
  forbid or net it: each line is evaluated independently under the rules above.
- Item information and equipped-item comparison (Game Design UI.1.4, section 7.3) follow shared
  tooltip presentation and are not a Commerce authority or persistence contract.

## Current implementation incompatibilities (follow-ups COM-02+)

Observed on `New-Testing`. These are documented, not resolved, by this architecture; items marked
resolved record the follow-up task that closed them.

1. **Resolved in COM-07:** per-item transactions; the running flow edits one draft, previews it
   and confirms it as one multi-line trade.
2. **Resolved in COM-04:** State Authority gate, shared replicated stock, reservation until the
   persistence outcome, and authority-resolved prices returned in the response.
3. **Sold items outside the offering do not enter stock.** Stock slots are fixed to the configured
   offering, so a persisted sale of any other item credits Gold without becoming purchasable.
   Adding dynamic slots belongs with restock/rotation design.
4. **Stock is session-scoped.** Shared stock lives only for the merchant `NetworkObject`; it is
   reseeded from configuration when a new Town session spawns the merchant.
5. **Resolved in COM-07:** request identity is `ProfileId + RequestId`, deduplicated also after
   the outcome. Deduplication is bounded by `MaxTradeRecords`: a retry arriving after its record
   was evicted is evaluated as a new request.
6. **Resolved in COM-07:** State Authority issues the priced multi-line ticket; the client never
   rebuilds prices.
7. **Resolved in COM-07:** `MerchantShopUI` is presentation and intentions only.
8. **Resolved in COM-07 for the merchant:** the presenter no longer locates
   `ApplicationStashContext`; it receives the profile from `TownInventoryBinder`, which still
   finds the context with `FindAnyObjectByType` like the other Town presenters.
9. **Resolved in COM-06:** scene-name (`isLobby`) coupling in persistence; every trade reads and
   writes the Inventory (Loadout).
10. **Resolved in COM-06 locally; backend pending.** `IShopTransactionService` persists one atomic
    multi-line ticket. The backend exposes only single-item buy/sell endpoints, so
    `RemoteShopTransactionService` refuses any ticket with more than one line with
    `PersistenceFailed` before mutating anything, and syncs a one-line ticket through the matching
    endpoint. Multi-line trades through the remote service need an atomic backend trade endpoint.
    Persistence rejections are not typed: every one is `InvalidInventory`.
11. **Optimistic remote commit.** `RemoteShopTransactionService` commits the local aggregate, returns
    `Success`, then synchronizes the backend fire-and-forget and relies on reconciliation. The
    confirmation reported to State Authority therefore does not imply backend durability; this
    is consistent with the current process-local persistence limitation and remains a backend
    follow-up.
12. **Partially resolved in COM-07.** `ShopTransactionResponse`, `IMasterClientRpcSender` and the
    orchestrator were removed and `MerchantTransactionResult` now names trade outcomes.
    `LocalShopTransactionService` is still not composed.
13. **Test gaps.** EditMode covers the validator, wire codec, trade session (draft, preview,
    confirm, outcome), shop interaction and feedback mapping, UI boundaries, ticket and store trade
    transactions. There is no coverage for
    the Fusion RPC path itself, the presenter lifecycle, or `RemoteShopTransactionService`.
14. **Held reservations of absent requesters.** A requester who leaves between ticket and outcome
    and never reopens the merchant keeps that trade's stock reserved, and its record pending, until
    the merchant `NetworkObject` ends. Enough such records would fill `MaxTradeRecords` and reject
    new trades. Resolving them without the requester needs an authoritative record of persisted
    tickets (backend).

## Validation strategy

EditMode owns draft editing/cancel, preview totals and final-state capacity, authority request
dedup/reservation/release rules and atomic multi-line persistence idempotency. PlayMode or manual
multi-client validation must cover shared stock visibility, last-unit contention between two
clients, Master Client change during an open trade, requester departure with a reservation, and
UI refresh only from confirmed profile commits.
